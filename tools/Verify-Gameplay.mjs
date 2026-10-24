import assert from 'node:assert/strict';
import http from 'node:http';
import { randomBytes } from 'node:crypto';

const base=process.env.TUTIEN_URL??'http://127.0.0.1:5080';
class Client
{
    cookies=new Map();
    async request(path,value)
    {
        const headers={};
        if(value!==undefined)
        {
            const csrf=await this.request('/api/csrf');
            headers['X-CSRF-Token']=csrf.body.token;
            headers['Content-Type']='application/json';
        }
        headers.Cookie=[...this.cookies].map(([key,value])=>key+'='+value).join('; ');
        const response=await fetch(base+path,{method:value===undefined?'GET':'POST',headers,body:value===undefined?undefined:JSON.stringify(value)});
        for(const cookie of response.headers.getSetCookie())
        {
            const pair=cookie.split(';')[0],index=pair.indexOf('=');
            this.cookies.set(pair.slice(0,index),pair.slice(index+1));
        }
        const text=await response.text();
        return {status:response.status,body:text?JSON.parse(text):null};
    }
    cookie(){return [...this.cookies].map(([key,value])=>key+'='+value).join('; ');}
}

async function socket(client,id)
{
    const url=new URL(`/ws/game/${id}`,base);
    const request=http.request(url,{headers:{Cookie:client.cookie(),Origin:base,Connection:'Upgrade',Upgrade:'websocket','Sec-WebSocket-Version':'13','Sec-WebSocket-Key':randomBytes(16).toString('base64')}});
    const stream=await new Promise((resolve,reject)=>
    {
        request.on('upgrade',(response,socket,head)=>{if(head.length)socket.unshift(head);resolve(socket);});
        request.on('response',response=>reject(new Error('Socket status '+response.statusCode)));
        request.on('error',reject);request.end();
    });
    let buffer=Buffer.alloc(0);
    let latest;
    const waits=[];
    let closed=false;
    const close=new Promise(resolve=>stream.on('close',()=>{closed=true;resolve();}));
    stream.on('error',()=>{});
    stream.on('data',bytes=>
    {
        buffer=Buffer.concat([buffer,bytes]);
        while(buffer.length>=2)
        {
            const opcode=buffer[0]&15;
            let size=buffer[1]&127,offset=2;
            if(size===126){if(buffer.length<4)return;size=buffer.readUInt16BE(2);offset=4;}
            if(size===127){if(buffer.length<10)return;size=Number(buffer.readBigUInt64BE(2));offset=10;}
            if(buffer.length<offset+size)return;
            const payload=buffer.subarray(offset,offset+size);buffer=buffer.subarray(offset+size);
            if(opcode===8){stream.end(Buffer.from([0x88,0x80,0,0,0,0]));return;}
            if(opcode!==2)continue;
            const frame=JSON.parse(payload.toString());
            if(frame.character)latest=frame.character;
            const data={...frame,character:latest};
            for(const wait of [...waits])if(wait.test(data)){waits.splice(waits.indexOf(wait),1);clearTimeout(wait.timer);wait.resolve(data);}
        }
    });
    let sequence=0;
    return {
        move(x,y,extra={})
        {
            const payload=Buffer.from(JSON.stringify({version:1,sequence:++sequence,type:'move',x,y,...extra}));
            const header=Buffer.alloc(payload.length<126?6:8);header[0]=0x82;
            let offset;
            if(payload.length<126){header[1]=0x80|payload.length;offset=2;}
            else{header[1]=0x80|126;header.writeUInt16BE(payload.length,2);offset=4;}
            const mask=randomBytes(4);mask.copy(header,offset);
            for(let index=0;index<payload.length;index++)payload[index]^=mask[index%4];
            stream.write(Buffer.concat([header,payload]));
        },
        wait(test)
        {
            return new Promise((resolve,reject)=>
            {
                const value={test,resolve,timer:setTimeout(()=>{waits.splice(waits.indexOf(value),1);reject(new Error('World state timeout'));},5000)};
                waits.push(value);
            });
        },
        close(){stream.end(Buffer.from([0x88,0x80,0,0,0,0]));},
        closed:close,
        get isClosed(){return closed;}
    };
}
const client=new Client();
const unique=Date.now().toString(36)+randomBytes(2).toString('hex');
assert.equal((await client.request('/api/register',{username:'play_'+unique,password:'Gameplay123!'})).status,201);
const created=await client.request('/api/characters',{slot:1,name:'Du Hành '+unique,classId:1,gender:0,appearanceId:101});
assert.equal(created.status,201);
const id=created.body.id;
await client.request(`/api/characters/${id}/enter`,{});
await client.request(`/api/characters/${id}/talent/complete`,{});
let world=await socket(client,id);
let frame=await world.wait(frame=>Boolean(frame.character?.game.version));
let character=frame.character;
assert.equal(character.game.itemBag.length,8);
assert.equal(character.game.pointBudget,100);
const weapon=character.game.itemBag.find(item=>item.id===1101);
let used=await client.request(`/api/characters/${id}/bag/use`,{uid:weapon.uid,playToken:character.playToken,revision:character.gameRevision});
assert.equal(used.status,200);
character=used.body;
assert.equal(character.game.itemBody[0].uid,weapon.uid);
assert.equal(character.game.itemBody[0].indexUI,0);
assert.equal((await client.request(`/api/characters/${id}/bag/use`,{uid:weapon.uid,playToken:character.playToken,revision:0})).status,409);
let points=await client.request(`/api/characters/${id}/potential`,{points:[5,10,0,0,0],playToken:character.playToken,revision:character.gameRevision});
assert.equal(points.status,200);
character=points.body;
assert.equal(character.stats.chanLucFull,110*(100+character.race.chanLucPercent)/100+5);
assert.equal((await client.request(`/api/characters/${id}/potential`,{points:[1000,0,0,0,0],playToken:character.playToken,revision:character.gameRevision})).status,409);
const pill=character.game.itemBag.find(item=>item.id===3010);
const simultaneous=await Promise.all([1,2].map(()=>client.request(`/api/characters/${id}/bag/use`,{uid:pill.uid,playToken:character.playToken,revision:character.gameRevision})));
assert.deepEqual(simultaneous.map(value=>value.status).sort(),[200,409]);
character=simultaneous.find(value=>value.status===200).body;
assert.equal(character.game.itemBag.find(item=>item.uid===pill.uid).quantity,4);
assert.equal(character.game.effects.length,1);
assert.ok(character.game.effects[0].expireTime>Date.now()+290000);
const before=await world.wait(frame=>frame.character.gameRevision>=character.gameRevision);
const position=before.players.find(value=>value.id===id);
const started=Date.now();
world.move(position.x+300,position.y);
const moved=await world.wait(frame=>frame.players.find(value=>value.id===id).x>position.x+45);
const distance=moved.players.find(value=>value.id===id).x-position.x;
assert.ok(distance<=(Date.now()-started)/1000*character.moveSpeed+30);
world.move(moved.players.find(value=>value.id===id).x,moved.players.find(value=>value.id===id).y);
world.close();
await world.closed;
world=await socket(client,id);
const restored=await world.wait(frame=>frame.character?.game.version===1);
assert.equal(restored.character.game.itemBox.length,0);
assert.deepEqual(restored.npcs.map(npc=>npc.id).sort((a,b)=>a-b),[101,102]);
assert.equal(restored.character.game.itemBody[0].uid,weapon.uid);
assert.equal(restored.character.game.effects[0].expireTime,character.game.effects[0].expireTime);
assert.deepEqual(restored.character.game.points,[5,10,0,0,0]);
assert.equal(restored.character.game.itemBag.length,7);
assert.ok(restored.character.position.x>=position.x+45);
assert.equal((await client.request(`/api/characters/${id}/potential`,{points:[0,0,0,0,0],revision:restored.character.gameRevision,playToken:character.playToken})).status,409);
const previous=world;
world=await socket(client,id);
const replaced=await world.wait(frame=>frame.character?.game.version===1);
await Promise.race([previous.closed,new Promise((_,reject)=>setTimeout(()=>reject(new Error('Old world session was not replaced')),4000))]);
const afterReplace=await client.request(`/api/characters/${id}/potential`,{points:[5,10,0,0,0],revision:replaced.character.gameRevision,playToken:replaced.character.playToken});
assert.equal(afterReplace.status,200);
assert.equal((await client.request(`/api/characters/${id}/potential`,{points:[5,10,0,0,0],revision:afterReplace.body.gameRevision,playToken:restored.character.playToken})).status,409);
world.move(0,0,{hp:999999});
await Promise.race([world.closed,new Promise((_,reject)=>setTimeout(()=>reject(new Error('Forged game state was not rejected')),4000))]);
console.log('Gameplay PASS: authenticated world, starter grant once, equipment, points, pill concurrency, movement speed, map arrNpc, durable reconnect, character registry takeover, forged input rejection.');

if(process.argv.includes('--shutdown'))
{
    world=await socket(client,id);
    await world.wait(frame=>frame.character?.game.version===1);
    const response=await fetch(base+'/api/dev/shutdown',
    {
        method:'POST',
        headers:{'X-TuTien-Local-Control':'shutdown'}
    });
    assert.equal(response.status,202);
    await Promise.race([world.closed,new Promise((_,reject)=>setTimeout(()=>reject(new Error('Active world did not close during shutdown')),8000))]);
    console.log('Shutdown PASS: active world session closed after character leave/save.');
}

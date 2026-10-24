import assert from 'node:assert/strict';
import net from 'node:net';
import {randomBytes} from 'node:crypto';

const base=process.env.TUTIEN_URL??'http://127.0.0.1:5080';
const bootstrap=await (await fetch(base+'/api/bootstrap')).json();
assert.equal(bootstrap.transport,'tcp');
const host=process.env.TUTIEN_TCP_HOST??'127.0.0.1';
const port=Number(process.env.TUTIEN_TCP_PORT??bootstrap.tcpPort);
const clients=[];

function packet(value)
{
    const body=Buffer.from(JSON.stringify(value));
    const header=Buffer.alloc(4);
    header.writeUInt32BE(body.length);
    return Buffer.concat([header,body]);
}

async function connect()
{
    const socket=net.createConnection({host,port});
    await new Promise((resolve,reject)=>{socket.once('connect',resolve);socket.once('error',reject);});
    socket.setNoDelay(true);
    let bytes=Buffer.alloc(0),sequence=0,state=null,character=null;
    const pending=new Map(),waits=[];
    const closed=new Promise(resolve=>socket.once('close',resolve));
    socket.on('error',()=>{});
    socket.on('close',()=>
    {
        for(const value of pending.values()){clearTimeout(value.timer);value.reject(new Error('TCP connection closed'));}
        pending.clear();
        for(const value of waits){clearTimeout(value.timer);value.reject(new Error('TCP world closed'));}
        waits.length=0;
    });
    socket.on('data',chunk=>
    {
        bytes=Buffer.concat([bytes,chunk]);
        while(bytes.length>=4)
        {
            const length=bytes.readUInt32BE(0);
            assert.ok(length>0&&length<=65536);
            if(bytes.length<length+4)return;
            const frame=JSON.parse(bytes.subarray(4,length+4));
            bytes=bytes.subarray(length+4);
            if(frame.type==='result')
            {
                const request=pending.get(frame.requestSequence);
                if(request){pending.delete(frame.requestSequence);clearTimeout(request.timer);request.resolve(frame);}
            }
            else if(frame.type==='state')
            {
                if(frame.character)character=frame.character;
                state={...frame,character};
                for(const value of [...waits])if(value.test(state))
                {
                    waits.splice(waits.indexOf(value),1);clearTimeout(value.timer);value.resolve(state);
                }
            }
        }
    });
    const client={
        socket,closed,
        request(type,data,fragment=false)
        {
            const id=++sequence;
            const response=new Promise((resolve,reject)=>
            {
                pending.set(id,{resolve,reject,timer:setTimeout(()=>{pending.delete(id);reject(new Error('TCP request timeout: '+type));},5000)});
            });
            const bytes=packet({version:1,sequence:id,type,...(data===undefined?{}:{data})});
            if(fragment)
            {
                socket.write(bytes.subarray(0,2));
                setImmediate(()=>{socket.write(bytes.subarray(2,5));setImmediate(()=>socket.write(bytes.subarray(5)));});
            }
            else socket.write(bytes);
            return response;
        },
        notify(type,data){socket.write(packet({version:1,sequence:++sequence,type,data}));},
        waitState(test)
        {
            if(state&&test(state))return Promise.resolve(state);
            return new Promise((resolve,reject)=>
            {
                const value={test,resolve,reject,timer:setTimeout(()=>{waits.splice(waits.indexOf(value),1);reject(new Error('TCP state timeout'));},5000)};
                waits.push(value);
            });
        },
        close(){socket.end();}
    };
    clients.push(client);
    return client;
}

async function expectClosed(client,bytes)
{
    client.socket.write(bytes);
    let timer;
    try
    {
        await Promise.race([client.closed,new Promise((_,reject)=>{timer=setTimeout(()=>reject(new Error('Invalid TCP packet was not closed')),4000);})]);
    }
    finally{clearTimeout(timer);}
}

try
{
    let client=await connect();
    assert.equal((await client.request('characters')).success,false);
    assert.equal((await client.request('bootstrap',undefined,true)).data.transport,'tcp');
    client.socket.cork();
    const joined=await Promise.all([client.request('ping'),client.request('bootstrap')].map((request,index)=>
    {
        if(index===1)client.socket.uncork();
        return request;
    }));
    assert.ok(joined.every(value=>value.success));
    const unique=Date.now().toString(36)+randomBytes(2).toString('hex');
    const account={username:'tcp_'+unique,password:'TcpDemo123!'};
    assert.equal((await client.request('register',account)).success,true);
    const created=await client.request('character.create',{slot:1,name:'TCP '+unique,classId:1,gender:0,appearanceId:101});
    assert.equal(created.success,true);
    const id=created.data.id;
    assert.equal((await client.request('characters')).data.slots.length,3);
    const enter=await client.request('character.enter',{id});
    assert.equal(enter.success,true);
    assert.equal(enter.data.needsTalentReveal,true);
    let state=await client.waitState(value=>value.character?.game.version===1);
    let character=state.character;
    assert.equal(character.game.itemBag.length,8);
    assert.equal(character.game.itemBox.length,0);
    assert.deepEqual(state.npcs.map(value=>value.id).sort((a,b)=>a-b),[101,102]);
    assert.equal((await client.request('move',{x:1000,y:600})).success,false);
    assert.equal((await client.request('talent.complete')).success,true);
    await client.waitState(value=>value.character.talentSeen);
    const weapon=character.game.itemBag.find(value=>value.id===1101);
    let response=await client.request('bag.use',{uid:weapon.uid,revision:character.gameRevision,playToken:character.playToken});
    assert.equal(response.success,true);
    character=response.data;
    assert.equal(character.game.itemBody[0].uid,weapon.uid);
    response=await client.request('potential',{points:[0,5,0,0,0],revision:character.gameRevision,playToken:character.playToken});
    assert.equal(response.success,true);
    character=response.data;
    assert.equal(character.stats.chanLucFull,105*(100+character.race.chanLucPercent)/100+5);
    const pill=character.game.itemBag.find(value=>value.id===3010);
    const uses=await Promise.all([1,2].map(()=>client.request('bag.use',{uid:pill.uid,revision:character.gameRevision,playToken:character.playToken})));
    assert.deepEqual(uses.map(value=>value.success).sort(),[false,true]);
    character=uses.find(value=>value.success).data;
    assert.equal(character.game.itemBag.find(value=>value.uid===pill.uid).quantity,4);
    assert.equal(character.game.effects.length,1);
    response=await client.request('body.remove',{slot:0,revision:character.gameRevision,playToken:character.playToken});
    assert.equal(response.success,true);
    character=response.data;
    assert.equal(character.game.itemBody.length,0);
    const before=await client.waitState(value=>value.character.gameRevision===character.gameRevision);
    const position=before.players.find(value=>value.id===id);
    const started=Date.now();
    client.notify('move',{x:position.x+300,y:position.y});
    const moved=await client.waitState(value=>value.players.find(player=>player.id===id).x>position.x+45);
    assert.ok(moved.players.find(value=>value.id===id).x-position.x<=(Date.now()-started)/1000*character.moveSpeed+30);
    client.notify('move',{x:moved.players.find(value=>value.id===id).x,y:position.y});
    const other=await connect();
    assert.equal((await other.request('register',{username:'other_'+unique,password:'TcpDemo123!'})).success,true);
    assert.equal((await other.request('character.enter',{id})).success,false);
    const victimCreated=await other.request('character.create',{slot:1,name:'TCP Dich '+unique,classId:1,gender:0,appearanceId:101});
    assert.equal(victimCreated.success,true);
    const victimId=victimCreated.data.id;
    assert.equal((await other.request('character.enter',{id:victimId})).success,true);
    assert.equal((await other.request('talent.complete')).success,true);
    const victim=await other.waitState(value=>value.character?.talentSeen);
    const source=await client.waitState(value=>value.character?.id===id);
    const attack=await client.request('attack',{targetId:victimId,playToken:source.character.playToken});
    assert.equal(attack.success,true);
    const expected=attack.data.isDodge?0:Math.max(0,source.character.stats.damageFull-victim.character.stats.defenceFull)*(attack.data.isCrit?2:1);
    assert.equal(attack.data.damage,expected);
    assert.equal((await client.request('attack',{targetId:victimId,playToken:source.character.playToken})).success,false);
    await other.waitState(value=>value.character.gameRevision>victim.character.gameRevision);
    other.close();
    await other.closed;
    client.close();
    await client.closed;
    client=await connect();
    assert.equal((await client.request('login',{...account,password:'wrong-password'})).success,false);
    assert.equal((await client.request('login',account)).success,true);
    response=await client.request('character.enter',{id});
    assert.equal(response.success,true);
    assert.equal(response.data.needsTalentReveal,false);
    const restored=await client.waitState(value=>value.character?.id===id);
    assert.equal(restored.character.game.itemBag.find(value=>value.uid===pill.uid).quantity,4);
    assert.equal(restored.character.game.effects[0].expireTime,character.game.effects[0].expireTime);
    assert.deepEqual(restored.character.game.points,[0,5,0,0,0]);
    assert.ok(restored.players.find(value=>value.id===id).x>=position.x+45);
    assert.equal((await client.request('potential',{points:[0,0,0,0,0],revision:restored.character.gameRevision,playToken:character.playToken})).success,false);
    const replacement=await connect();
    assert.equal((await replacement.request('login',account)).success,true);
    assert.equal((await replacement.request('character.enter',{id})).success,true);
    await client.closed;
    client=replacement;
    state=await client.waitState(value=>value.character?.id===id);
    assert.equal((await client.request('potential',{points:[0,0,0,0,0],revision:state.character.gameRevision,playToken:state.character.playToken})).success,true);
    const second=await client.request('character.create',{slot:2,name:'TCP Hai '+unique,classId:3,gender:1,appearanceId:201});
    assert.equal(second.success,true);
    assert.equal((await client.request('character.enter',{id:second.data.id})).success,true);
    await client.waitState(value=>value.character?.id===second.data.id);
    assert.equal((await client.request('logout')).success,true);
    assert.equal((await client.request('characters')).success,false);
    client.close();
    await client.closed;
    for(const length of [0,0xffffffff,65537])
    {
        const invalid=await connect();
        const header=Buffer.alloc(4);header.writeUInt32BE(length);
        await expectClosed(invalid,header);
    }
    let invalid=await connect();
    await expectClosed(invalid,packet({version:2,sequence:1,type:'ping'}));
    invalid=await connect();
    assert.equal((await invalid.request('ping')).success,true);
    await expectClosed(invalid,packet({version:1,sequence:1,type:'ping'}));
    invalid=await connect();
    await expectClosed(invalid,packet({version:1,sequence:1,type:'ping',hp:999999}));
    console.log('TCP PASS: fragmented/coalesced frames, authentication, three slots, talent, movement, inventory, points, pills, basic combat/cooldown, ownership, durable reconnect, session takeover, character switch, logout, invalid packets.');
    if(process.argv.includes('--shutdown'))
    {
        client=await connect();
        assert.equal((await client.request('login',account)).success,true);
        assert.equal((await client.request('character.enter',{id})).success,true);
        await client.waitState(value=>value.character?.id===id);
        const response=await fetch(base+'/api/dev/shutdown',{method:'POST',headers:{'X-TuTien-Local-Control':'shutdown'}});
        assert.equal(response.status,202);
        let timer;
        try{await Promise.race([client.closed,new Promise((_,reject)=>{timer=setTimeout(()=>reject(new Error('TCP shutdown timeout')),8000);})]);}
        finally{clearTimeout(timer);}
        console.log('TCP shutdown PASS: active character saved and connection closed.');
    }
}
finally
{
    for(const client of clients)client.socket.destroy();
}

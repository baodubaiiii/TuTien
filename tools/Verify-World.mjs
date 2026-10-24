import assert from 'node:assert/strict';
import {randomBytes} from 'node:crypto';
import {setTimeout as delay} from 'node:timers/promises';
import {connect} from './TcpClient.mjs';

const clients=[];
const unique=Date.now().toString(36)+randomBytes(2).toString('hex');
async function account(suffix)
{
    const client=await connect();clients.push(client);
    const credentials={username:'world_'+unique+suffix,password:'WorldDemo123!'};
    assert.equal((await client.request('register',credentials)).success,true);
    const result=await client.request('character.create',{slot:1,name:'World '+unique+suffix,classId:1,gender:0,appearanceId:101});
    assert.equal(result.success,true);
    const id=result.data.id;
    assert.equal((await client.request('character.enter',{id})).success,true);
    await client.wait(state=>state.character?.game.version===1);
    assert.equal((await client.request('talent.complete')).success,true);
    await client.wait(state=>state.character?.talentSeen);
    return {client,credentials,id};
}
function token(client){return client.state.character.playToken;}
function self(client){return client.state.players.find(value=>value.id===client.state.character.id);}
async function move(client,x,y)
{
    client.notify('move',{x,y,playToken:token(client)});
    await client.wait(()=>{const value=self(client);return value&&Math.hypot(value.x-x,value.y-y)<90;},'movement');
}
async function change(client,type,data)
{
    const result=await client.request(type,{...data,playToken:token(client)});
    assert.equal(result.success,true,result.message);
    await client.wait(state=>state.character.playToken===result.data.playToken,'new map token');
    return result;
}
try
{
    const a=await account('a'),b=await account('b');
    const client=a.client,peer=b.client;
    assert.equal((await client.request('map.change',{mapId:2,playToken:token(client)})).success,false);
    assert.equal((await client.request('character.revive',{playToken:token(client)})).success,false);
    assert.equal((await client.request('npc.menu',{id:101,playToken:token(client)})).success,false);
    await move(client,550,600);
    const talk=await client.request('npc.menu',{id:101,playToken:token(client)});
    assert.equal(talk.success,true,talk.message);
    assert.ok(talk.data.text.length>0);
    assert.equal((await client.request('world.zones')).data.zones.length,4);
    let character=client.state.character;
    let result=await client.request('potential',{points:[0,100,0,0,0],revision:character.gameRevision,playToken:token(client)});
    assert.equal(result.success,true,result.message);
    await Promise.all([move(client,1440,600),move(peer,1440,600)]);
    const oldToken=token(client);
    await change(client,'map.change',{mapId:2});
    await change(peer,'map.change',{mapId:2});
    assert.equal(client.state.mapId,2);
    assert.equal((await client.request('potential',{points:[0,0,0,0,0],revision:client.state.character.gameRevision,playToken:oldToken})).success,false);
    await client.wait(state=>state.monsters.length===3,'monster spawn');
    let target=client.state.monsters[0];
    const life=target.lifeId;
    for(let tries=0;tries<15;tries++)
    {
        target=client.state.monsters.find(value=>value.id===target.id);
        if(target.hp===0)break;
        const player=self(client);
        if(Math.hypot(target.x-player.x,target.y-player.y)>115)client.notify('move',{x:target.x,y:target.y,playToken:token(client)});
        result=await client.request('monster.attack',{targetId:target.id,lifeId:life,playToken:token(client)});
        await delay(850);
    }
    await client.wait(state=>state.character.game.tuVi>0&&state.items.length>0,'kill reward');
    const earned=client.state.character.game.tuVi;
    const drop=client.state.items.find(value=>value.playerId===a.id);
    assert.ok(drop);
    assert.equal((await peer.request('item.pick',{id:drop.id,playToken:token(peer)})).success,false);
    assert.equal((await client.request('monster.attack',{targetId:target.id,lifeId:life,playToken:token(client)})).success,false);
    const before=client.state.character.game.itemBag.filter(value=>value.id===drop.itemId).reduce((sum,value)=>sum+value.quantity,0);
    await move(client,drop.x,drop.y);
    result=await client.request('item.pick',{id:drop.id,playToken:token(client)});
    assert.equal(result.success,true,result.message);
    assert.equal(result.data.game.itemBag.filter(value=>value.id===drop.itemId).reduce((sum,value)=>sum+value.quantity,0),before+drop.quantity);
    assert.equal((await client.request('item.pick',{id:drop.id,playToken:token(client)})).success,false);
    await change(client,'zone.change',{zoneId:2});
    assert.equal(client.state.zoneId,2);
    assert.ok(!client.state.players.some(value=>value.id===b.id));
    assert.equal((await client.request('monster.attack',{targetId:target.id,lifeId:life,playToken:token(client)})).success,false);
    await client.wait(state=>state.character.game.hp===0,'monster AI death',45000);
    assert.equal((await client.request('character.revive',{playToken:token(client)})).success,false);
    const potion=client.state.character.game.itemBag.find(value=>value.id===3010);
    assert.equal((await client.request('bag.use',{uid:potion.uid,revision:client.state.character.gameRevision,playToken:token(client)})).success,false);
    await delay(3200);
    await change(client,'character.revive',{});
    assert.equal(client.state.mapId,1);
    assert.ok(client.state.character.game.hp>0);
    assert.equal(client.state.character.game.tuVi,earned);
    assert.equal((await client.request('logout')).success,true);
    assert.equal((await client.request('login',a.credentials)).success,true);
    const entered=await client.request('character.enter',{id:a.id});
    assert.equal(entered.success,true);
    await client.wait(state=>state.character.playToken===entered.data.playToken,'relogin');
    assert.equal(client.state.character.game.tuVi,earned);
    assert.equal(client.state.character.mapId,1);
    await move(client,550,600);
    assert.equal((await client.request('npc.menu',{id:101,playToken:token(client)})).success,true);
    console.log('World PASS: waypoint/map/zone/token, NPC range/menu, monster AI/kill/drop/owner/pick-once, tu vi, death/revive, durable login.');
}
finally
{
    for(const client of clients)client.close();
}

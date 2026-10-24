import assert from 'node:assert/strict';
import {randomBytes} from 'node:crypto';
import {setTimeout as delay} from 'node:timers/promises';
import {connect} from './TcpClient.mjs';

const unique=Date.now().toString(36)+randomBytes(2).toString('hex');
const credentials={username:'task_'+unique,password:'ProgressionDemo123!'};
let client=await connect();
function hero(){return client.state.character;}
function token(){return hero().playToken;}
function self(){return client.state.players.find(value=>value.id===hero().id);}
async function ok(type,data={})
{
    const mutation=type.startsWith('task.')||type.startsWith('skill.')||type.startsWith('realm.')||type==='potential'||type==='bag.use';
    const result=await client.request(type,{...data,...(mutation?{revision:hero().gameRevision}:{}),playToken:token()});
    assert.equal(result.success,true,type+': '+result.message);
    if(result.data?.gameRevision)await client.wait(state=>state.character.gameRevision>=result.data.gameRevision,type+' saved');
    return result;
}
async function move(x,y)
{
    client.notify('move',{x,y,playToken:token()});
    await client.wait(()=>{const player=self();return player&&Math.hypot(player.x-x,player.y-y)<85;},'movement');
}
async function npc(id)
{
    const target=client.state.npcs.find(value=>value.id===id);
    await move(target.x,target.y);
}
async function travel(mapId)
{
    const portal=hero().map.waypoint.find(value=>value.mapId===mapId);
    await move(portal.x,portal.y);
    const result=await ok('map.change',{mapId});
    await client.wait(state=>state.character.playToken===result.data.playToken,'map token');
    await delay(1100);
    if(mapId===2)
    {
        const zone=await ok('zone.change',{zoneId:2});
        await client.wait(state=>state.character.playToken===zone.data.playToken,'zone token');
    }
}
async function fight(skill)
{
    for(let tries=0;tries<90&&hero().infoTask.mainTask.count<2;tries++)
    {
        const target=client.state.monsters.find(value=>value.hp>0);
        if(!target){await delay(500);continue;}
        if(hero().game.hp<hero().stats.hpFull*0.65)
        {
            const pill=hero().game.itemBag.find(value=>value.id===3010);
            if(pill)await ok('bag.use',{uid:pill.uid});
        }
        const player=self();
        if(Math.hypot(player.x-target.x,player.y-target.y)>110)client.notify('move',{x:target.x,y:target.y,playToken:token()});
        else await client.request(skill?'skill.use':'monster.attack',{...(skill?{slot:0,targetType:'monster'}:{}),targetId:target.id,lifeId:target.lifeId,playToken:token()});
        await delay(skill?2100:850);
        assert.ok(hero().game.hp>0,'Quest combat died');
    }
    assert.equal(hero().infoTask.mainTask.count,2);
}
try
{
    assert.equal((await client.request('register',credentials)).success,true);
    const created=await client.request('character.create',{slot:1,name:'Task '+unique,classId:1,gender:0,appearanceId:101});
    assert.equal(created.success,true);
    const id=created.data.id;
    assert.equal((await client.request('character.enter',{id})).success,true);
    await client.wait(state=>state.character?.game.version===1);
    assert.equal((await client.request('talent.complete')).success,true);
    await client.wait(state=>state.character?.talentSeen);
    assert.equal((await client.request('task.accept',{id:1,revision:hero().gameRevision,playToken:token()})).success,false);
    await ok('potential',{points:[0,100,0,0,0]});
    for(const item of [...hero().game.itemBag].filter(value=>value.id>=1100&&value.id<3000))await ok('bag.use',{uid:item.uid});
    await npc(101);
    await ok('task.accept',{id:1});
    await npc(102);
    await ok('npc.menu',{id:102});
    await client.wait(()=>hero().infoTask.mainTask.count===1,'talk count');
    await npc(101);
    await ok('task.claim',{id:1});
    assert.equal((await client.request('task.claim',{id:1,revision:hero().gameRevision,playToken:token()})).success,false);
    await ok('task.accept',{id:2});
    await travel(2);
    await fight(false);
    await travel(1);
    await npc(101);
    await ok('task.claim',{id:2});
    await ok('task.accept',{id:3});
    await npc(102);
    assert.equal((await client.request('skill.learn',{id:201,revision:hero().gameRevision,playToken:token()})).success,false);
    await ok('skill.learn',{id:101});
    assert.equal(hero().game.skillSlots[0],101);
    await npc(101);
    await ok('task.claim',{id:3});
    await ok('task.accept',{id:4});
    while(hero().game.hp<hero().stats.hpFull)
    {
        const pill=hero().game.itemBag.find(value=>value.id===3010);
        assert.ok(pill);
        await ok('bag.use',{uid:pill.uid});
    }
    await travel(2);
    await fight(true);
    const lastUse=hero().skills[0].lastTimeUse;
    assert.ok(lastUse>0);
    await travel(1);
    await npc(101);
    await ok('task.claim',{id:4});
    while(hero().game.hp<hero().stats.hpFull)
    {
        const item=hero().game.itemBag.find(value=>value.id===3010);
        assert.ok(item,'HP pill supply');
        await ok('bag.use',{uid:item.uid});
    }
    const budget=hero().game.pointBudget;
    const tuVi=hero().game.tuVi;
    await ok('realm.advance');
    assert.equal(hero().game.tuVi,tuVi-100);
    assert.equal((await client.request('skill.assign',{id:101,slot:0,revision:hero().gameRevision,playToken:token()})).success,false);
    await client.wait(()=>hero().game.breakthrough.strikeCount===1,'first strike');
    const due=hero().game.breakthrough.nextStrikeTime;
    const oldToken=token();
    await ok('character.leave');
    await delay(6500);
    client.close();
    client=await connect();
    assert.equal((await client.request('login',credentials)).success,true);
    assert.equal((await client.request('character.enter',{id})).success,true);
    await client.wait(state=>state.character?.realmId===2,'trial resume after reconnect');
    assert.notEqual(token(),oldToken);
    assert.equal(hero().game.breakthrough.strikeCount,3);
    assert.equal(hero().game.breakthrough.result,'success');
    assert.equal(hero().game.pointBudget,budget+10);
    assert.equal(hero().skills[0].lastTimeUse,lastUse);
    assert.ok(hero().game.breakthrough.nextStrikeTime>due);
    await ok('skill.assign',{id:101,slot:2});
    assert.deepEqual(hero().game.skillSlots,[0,0,101]);
    const revision=hero().gameRevision;
    await delay(1200);
    assert.equal(hero().gameRevision,revision,'Trial rewarded again');
    assert.equal((await client.request('realm.cancel',{revision,playToken:token()})).success,false);
    console.log('Progression PASS: quest chain/count/claim-once, learn/class/slots, MP/cooldown, trial reservation/disconnect/catchup/reward-once, durable realm/skills.');
}
finally
{
    client.close();
}

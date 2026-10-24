import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const groupNames = ['Phàm Nhân', 'Luyện Khí', 'Trúc Cơ', 'Kim Đan', 'Nguyên Anh', 'Hóa Thần', 'Luyện Hư', 'Hợp Thể'];
const features = ['Tutorial; võ cơ bản', 'Hấp thu linh khí; 3 slot skill', 'Chọn nền móng; công pháp chính; tông môn', 'Kết đan phẩm chất; pháp bảo bản mệnh', 'Nguyên thần; thần thức; phân thân/bảo mệnh', 'Ý cảnh/Đạo ý; skill biến thể', 'Không gian; đạo vực sơ cấp', 'Hợp nhất pháp tướng; set bonus cao cấp'];
const stages = [['1'], Array.from({length:10}, (_,i) => String(i+1)), ['Sơ Kỳ','Trung Kỳ','Hậu Kỳ','Viên Mãn'], Array.from({length:9}, (_,i) => (i+1)+' vân'), ...Array.from({length:4}, () => ['Sơ Kỳ','Trung Kỳ','Hậu Kỳ','Viên Mãn'])];
const realms = [];
const firstRealm = [];
for (let g = 0; g < groupNames.length; g++)
{
    firstRealm.push(realms.length + 1);
    for (let s = 0; s < stages[g].length; s++) realms.push({id: realms.length + 1, groupId:g+1, name:groupNames[g], stage:stages[g][s], rank:realms.length+1, description:features[g], skillSlots:g===0 ? 0 : 3});
}
const optionNames = ['Khí Huyết +#', 'Chân Lực +#', 'Pháp Lực +#', 'Linh Giác +#', 'Sát Cơ +#'];
const itemOptions = optionNames.map((name,i) => ({id:i+1, name, type:0}));
const weapons = [
    ['Mộc Kiếm','Thanh Phong Kiếm','Trúc Ảnh Kiếm','Kim Huy Kiếm','Nguyên Thần Kiếm','Thiên Ý Kiếm','Hư Không Kiếm','Hợp Đạo Kiếm'],
    ['Thiết Thương','Lưu Vân Thương','Phá Trúc Thương','Xích Kim Thương','Long Hồn Thương','Thiên Uy Thương','Toái Hư Thương','Vạn Tượng Thương'],
    ['Trúc Bút','Linh Văn Bút','Ngọc Hào Bút','Kim Phù Bút','Thần Niệm Bút','Thiên Thư Bút','Hư Giới Bút','Đạo Văn Bút'],
    ['Bố Hồn Phiên','Tụ Hồn Phiên','U Minh Phiên','Cửu U Phiên','Nguyên Hồn Phiên','Thiên Ma Phiên','Hư Linh Phiên','Vạn Hồn Phiên'],
    ['Vải Quyền','Huyền Thiết Quyền','Linh Ngọc Quyền','Kim Cang Quyền','Thần Lực Quyền','Thiên Cương Quyền','Phá Hư Quyền','Hợp Đạo Quyền']
];
const classNames = ['Kiếm','Thương','Bút','Hồn Phiên','Quyền'];
const items = [];
function item(id,name,type,group,extra = {})
{
    return {id,type,skill:0,gender:3,name,subName:groupNames[group-1],description:`${name}, yêu cầu cảnh giới ${groupNames[group-1]}.`,level:group,iconId:0,part:-1,isUpToUp:type<=5,isDrop:false,require:0,isExpires:false,secondsExpires:0,saleCoin:0,options:[],classId:0,realmId:firstRealm[group-1],elementId:0,rarity:Math.min(group-1,4),maxStack:type<=5?1:999,assetKey:'',effectStatId:type===6?(name.includes('Hồi Huyết')?1:3):0,effectParam:type===6?group*20:0,durationSeconds:type===6?300:0,restorePercent:type===6?25:0,...extra};
}
for (let g=1; g<=8; g++)
{
    for (let c=1;c<=5;c++) items.push(item(1000+g*100+c,weapons[c-1][g-1],0,g,{classId:c,options:[{id:c===3||c===4?3:2,param:g*5,type:0}]}));
    ['Pháp Bào','Đạo Quan','Đạo Ngoa','Hộ Đái','Ngọc Bội'].forEach((name,index) => items.push(item(1000+g*100+11+index,groupNames[g-1]+' '+name,index+1,g,{options:[{id:[1,4,4,1,5][index],param:g*5,type:0}]})));
    ['Hồi Huyết Đan','Tụ Linh Đan'].forEach((name,index) => items.push(item(3000+g*10+index,groupNames[g-1]+' '+name,6,g,{maxStack:99})));
    ['Tinh Thiết','Linh Mộc'].forEach((name,index) => items.push(item(4000+g*10+index,groupNames[g-1]+' '+name,7,g)));
}
for (let c=1;c<=5;c++)
{
    ['Kim','Mộc','Thủy','Hỏa','Thổ'].forEach((name,index) => items.push(item(2000+c*100+index+1,name+' Linh '+classNames[c-1],0,2,{classId:c,elementId:index+1,options:[{id:c===3||c===4?3:2,param:12,type:0}]})));
}
['Hạ Phẩm','Trung Phẩm','Thượng Phẩm','Cực Phẩm'].forEach((name,index) => items.push(item(5001+index,'Linh Thạch '+name,8,1,{maxStack:99999,rarity:index})));
items.push(item(6001,'Lệnh Bài Tiếp Dẫn',9,1),item(6002,'Tín Vật Tông Môn',9,3));
items.sort((a,b)=>a.id-b.id);

const hubNames = ['Thanh Vân Thôn','Thanh Vân Sơn Môn','Trúc Cơ Đài','Kim Đan Điện','Nguyên Anh Các','Thiên Ý Phong','Hư Không Thành','Hợp Đạo Thiên Cung'];
const fieldNames = ['Rừng Trúc Ngoại Vi','Linh Khí Sơn Cốc','Trúc Ảnh Bí Cảnh','Kim Hà Động Thiên','Nguyên Thần Cổ Vực','Lôi Kiếp Hoang Nguyên','Hư Giới Liệt Phùng','Vạn Tượng Đạo Vực'];
const maps = [];
const npcs = [];
const npcNames = [ ['Lâm Bá','Tiếp Dẫn Sứ'], ['Vân Chân Nhân','Linh Thạch Thương Nhân'], ['Trúc Cơ Trưởng Lão','Luyện Khí Sư'], ['Kim Đan Chấp Sự','Đan Sư Thanh Ngọc'], ['Nguyên Anh Trưởng Lão','Thần Thức Sứ'], ['Thiên Ý Đạo Nhân','Lôi Kiếp Hộ Pháp'], ['Hư Không Hành Giả','Truyền Tống Sứ'], ['Hợp Đạo Tôn Giả','Vạn Bảo Các Chủ'] ];
for (let g=1;g<=8;g++)
{
    for(let kind=0;kind<2;kind++)
    {
        const id=(g-1)*2+kind+1;
        const connected = [];
        if(id>1) connected.push(id-1);
        if(id<16) connected.push(id+1);
        maps.push({id,name:kind===0?hubNames[g-1]:fieldNames[g-1],type:kind,realmId:firstRealm[g-1],maxPlayer:50,zoneNumber:4,width:1600,height:900,spawnX:800,spawnY:600,tileId:0,bgId:0,bgType:0,isDouble:false,teleport:0,waypoint:connected.map(target=>({mapId:target,x:target<id?100:1500,y:600})),mob:[],arrNpc:[],background:[],action:[],mapCollisions:id===1?[{x:400,y:260,width:160,height:100},{x:1050,y:260,width:200,height:100},{x:700,y:730,width:200,height:50}]:[],assetKey:''});
    }
    npcNames[g-1].forEach((name,index) =>
    {
        const id=100+(g-1)*2+index+1;
        npcs.push({id,name,head:0,body:0,leg:0,avatar:0,menu:[],chats:[`Chào đạo hữu. Nơi đây là ${hubNames[g-1]}.`],realmId:firstRealm[g-1],role:index===0?'guide':'service',assetKey:''});
        maps.find(map=>map.id===(g-1)*2+1).arrNpc.push([1,index===0?500:1100,600,id,0]);
    });
}
const skillNames = [
    ['Trảm Kích','Thanh Phong Trảm','Kiếm Khí Quyết','Ngự Kiếm Thuật','Bạch Hồng Quán Nhật','Kim Quang Kiếm Trận','Nguyên Thần Ngự Kiếm','Thiên Ý Nhất Kiếm','Hư Không Kiếm Vực','Vạn Kiếm Quy Tông'],
    ['Thrust Cơ Bản','Lưu Vân Thức','Hồi Phong Thương','Phá Trận Kích','Bạch Long Xuất Hải','Kim Long Phá Không','Long Hồn Hộ Thể','Thiên Uy Trấn Nhạc','Toái Hư Thương Vực','Vạn Tượng Quy Thương'],
    ['Điểm Bút','Linh Văn Quyết','Họa Phù Thuật','Phong Linh Ấn','Ngọc Phù Trận','Kim Phù Trấn Ấn','Thần Niệm Hóa Phù','Thiên Thư Phán','Hư Giới Phong Ấn','Đạo Văn Thiên Địa'],
    ['Dẫn Hồn','Tụ Hồn Chú','U Hỏa Thuật','Âm Phong Quyết','U Minh Triệu Ảnh','Cửu U Trấn Hồn','Nguyên Hồn Hộ Mệnh','Thiên Ma Huyễn Ảnh','Hư Linh Hồn Vực','Vạn Hồn Quy Nhất'],
    ['Trực Quyền','Liên Hoàn Quyền','Thiết Bố Sam','Phá Sơn Kình','Linh Ngọc Hộ Thể','Kim Cang Bất Hoại','Thần Lực Bạo Phát','Thiên Cương Chấn','Phá Hư Quyền Vực','Hợp Đạo Kim Thân']
];
skillNames[1][0]='Đâm Thương';
const skills=[];
for(let c=1;c<=5;c++)
{
    skillNames[c-1].forEach((name,index)=>
    {
        const group=index===0?1:index<=3?2:index-1;
        const cooldown=index===0?800:index<=3?[0,1500,4000,8000][index]:10000;
        skills.push({id:c*100+index+1,classId:c,name,maxPoint:10,manaUseType:0,type:index===0?0:1,iconId:0,damageInfo:c===3||c===4?'Pháp Lực':'Chân Lực',description:`${name}, pháp môn ${classNames[c-1]}, cảnh giới ${groupNames[group-1]}.`,cooldown,realmId:firstRealm[group-1],elementId:0,assetKey:'',skillDataTemplates:Array.from({length:10},(_,rank)=>({skillId:c*100+index+1,point:rank+1,powRequire:0,manaUse:index===0?0:(rank+1)*5,coolDown:cooldown,dx:0,dy:0,maxFight:1,damage:0,price:0,moreInfo:'',iconId:0}))});
    });
}
const catalog={items,itemOptions,maps,npcs,skills,realms};
fs.writeFileSync(path.join(root,'database/catalog/templates.json'),JSON.stringify(catalog,null,2)+'\n');

const columns={
    realm_template:['id','groupId','name','stage','rank','description','skillSlots'],
    item_option_template:['id','name','type'],
    item_template:Object.keys(items[0]),
    map_template:Object.keys(maps[0]),
    npc_template:Object.keys(npcs[0]),
    skill_template:Object.keys(skills[0])
};
const groups={realm_template:realms,item_option_template:itemOptions,item_template:items,map_template:maps,npc_template:npcs,skill_template:skills};
const snake=value=>value==='arrNpc'?'arrNpc':value.replace(/[A-Z]/g,letter=>'_'+letter.toLowerCase());
const literal=value=> value===null ? 'NULL' : typeof value==='boolean' ? (value?'1':'0') : typeof value==='number' ? String(value) : "'"+(typeof value==='object'?JSON.stringify(value):String(value)).replace(/\\/g,'\\\\').replace(/'/g,"''")+"'";
let sql='START TRANSACTION;\n';
for(const [table, rows] of Object.entries(groups))
{
    const keys=columns[table];
    for(const row of rows)
    {
        sql+='INSERT INTO `'+table+'` ('+keys.map(key=>'`'+snake(key)+'`').join(', ')+') VALUES ('+keys.map(key=>literal(row[key])).join(', ')+')\nON DUPLICATE KEY UPDATE '+keys.filter(key=>key!=='id').map(key=>'`'+snake(key)+'` = VALUES(`'+snake(key)+'`)').join(', ')+';\n';
    }
}
sql+='COMMIT;\n';
fs.writeFileSync(path.join(root,'database/seeds/001_tutien_templates.mysql.sql'),sql);
const creationPath=path.join(root,'content/source/base/catalog.json');
const creation=JSON.parse(fs.readFileSync(creationPath,'utf8').replace(/^\uFEFF/,''));
Object.assign(creation,catalog);
delete creation.mapNpcs;
fs.writeFileSync(creationPath,JSON.stringify(creation,null,2)+'\n');

for(const [table,rows] of Object.entries(groups))
{
    const fields=table==='item_template'?['id','name','type','classId','realmId','elementId']:table==='realm_template'?['id','name','stage','description']:['id','name','realmId'].filter(key=>key in rows[0]);
    const csv=fields.join(',')+'\n'+rows.map(row=>fields.map(key=>'"'+String(row[key]).replace(/"/g,'""')+'"').join(',')).join('\n')+'\n';
    fs.writeFileSync(path.join(root,'database/catalog',table+'.csv'),'\uFEFF'+csv);
}
console.log(JSON.stringify(Object.fromEntries(Object.entries(groups).map(([key,value])=>[key,value.length]))));

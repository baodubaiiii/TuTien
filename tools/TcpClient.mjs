import net from 'node:net';

export async function connect()
{
    const socket=net.createConnection({host:'127.0.0.1',port:7777});
    await new Promise((resolve,reject)=>{socket.once('connect',resolve);socket.once('error',reject);});
    socket.setNoDelay(true);
    let buffer=Buffer.alloc(0),sequence=0,state=null,character=null;
    const pending=new Map(),waits=new Set();
    socket.on('error',()=>{});
    socket.on('close',()=>
    {
        for(const request of pending.values()){clearTimeout(request.timer);request.reject(new Error('TCP closed'));}
        for(const wait of waits){clearTimeout(wait.timer);wait.reject(new Error('TCP closed'));}
        pending.clear();waits.clear();
    });
    socket.on('data',chunk=>
    {
        buffer=Buffer.concat([buffer,chunk]);
        while(buffer.length>=4)
        {
            const length=buffer.readUInt32BE(0);
            if(length<1||length>65536){socket.destroy();return;}
            if(buffer.length<length+4)return;
            const packet=JSON.parse(buffer.subarray(4,length+4));
            buffer=buffer.subarray(length+4);
            if(packet.type==='result')
            {
                const request=pending.get(packet.requestSequence);
                if(request){clearTimeout(request.timer);pending.delete(packet.requestSequence);request.resolve(packet);}
            }
            if(packet.type==='state')
            {
                if(packet.character)character=packet.character;
                state={...packet,character};
                for(const wait of waits)if(wait.test(state)){clearTimeout(wait.timer);waits.delete(wait);wait.resolve(state);}
            }
        }
    });
    function send(type,data,response)
    {
        const id=++sequence;
        let result;
        if(response)result=new Promise((resolve,reject)=>pending.set(id,{resolve,reject,timer:setTimeout(()=>{pending.delete(id);reject(new Error('TCP timeout: '+type));},15000)}));
        const bytes=Buffer.from(JSON.stringify({version:1,sequence:id,type,...(data===undefined?{}:{data})}));
        const header=Buffer.alloc(4);header.writeUInt32BE(bytes.length);
        socket.write(Buffer.concat([header,bytes]));
        return result;
    }
    return {
        get state(){return state;},
        request(type,data){return send(type,data,true);},
        notify(type,data){send(type,data,false);},
        wait(test,name='world',timeout=20000)
        {
            if(state&&test(state))return Promise.resolve(state);
            return new Promise((resolve,reject)=>
            {
                const wait={test,resolve,reject,timer:setTimeout(()=>{waits.delete(wait);reject(new Error('State timeout: '+name));},timeout)};
                waits.add(wait);
            });
        },
        close(){socket.destroy();}
    };
}

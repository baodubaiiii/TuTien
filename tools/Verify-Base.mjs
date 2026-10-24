import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';

const base = process.env.TUTIEN_URL ?? 'http://127.0.0.1:5080';
const health = await (await fetch(base + '/health')).json();
assert.equal(health.mode, 'gameplay');
assert.equal(health.status, 'ready');
assert.equal(health.creationReady, true);
const manifest = await (await fetch(base + '/cdn/base/manifest.json')).json();
assert.equal(manifest.version, 'base');
const entry = manifest.files[0];
const bytes = Buffer.from(await (await fetch(base + '/cdn/base/' + entry.path)).arrayBuffer());
assert.equal(createHash('sha256').update(bytes).digest('hex'), entry.sha256);
const catalog = JSON.parse(bytes);
for (const [key,count] of Object.entries({items:143,itemOptions:5,skills:50,npcs:16,maps:16,realms:40,tasks:4,bosses:0})) assert.equal(catalog[key].length,count);
for (const key of ['races', 'elements', 'classes']) assert.equal(catalog[key].length, 5);

function fields(bytes)
{
    const result = new Map();
    let offset = 0;
    function varint()
    {
        let value = 0n;
        let shift = 0n;
        while (offset < bytes.length && shift < 70n)
        {
            const current = bytes[offset++];
            value |= BigInt(current & 127) << shift;
            if (!(current & 128)) return Number(value);
            shift += 7n;
        }
        throw new Error('Invalid protobuf varint');
    }
    while (offset < bytes.length)
    {
        const tag = varint();
        const wire = tag & 7;
        let value;
        if (wire === 0) value = varint();
        else if (wire === 1) { value = bytes.subarray(offset, offset + 8); offset += 8; }
        else if (wire === 2) { const length = varint(); value = bytes.subarray(offset, offset + length); offset += length; }
        else throw new Error('Unexpected protobuf wire type');
        result.set(tag >> 3, value);
    }
    return result;
}

async function open()
{
    const socket = new WebSocket(base.replace(/^http/, 'ws') + '/ws');
    socket.binaryType = 'arraybuffer';
    await new Promise((resolve, reject) =>
    {
        const timer = setTimeout(() => reject(new Error('Socket open timeout')), 4000);
        socket.addEventListener('open', () => { clearTimeout(timer); resolve(); }, { once: true });
        socket.addEventListener('error', () => { clearTimeout(timer); reject(new Error('Socket open failed')); }, { once: true });
    });
    return socket;
}

function exchange(socket, packet)
{
    return new Promise((resolve, reject) =>
    {
        const timer = setTimeout(() => reject(new Error('Reply timeout')), 4000);
        socket.addEventListener('message', event => { clearTimeout(timer); resolve(fields(Buffer.from(event.data))); }, { once: true });
        socket.send(packet);
    });
}

function closed(socket, packet)
{
    return new Promise((resolve, reject) =>
    {
        const timer = setTimeout(() => { socket.close(); reject(new Error('Close timeout')); }, 4000);
        socket.addEventListener('close', () => { clearTimeout(timer); resolve(); }, { once: true });
        socket.send(packet);
    });
}

const ping = Buffer.from([8, 1, 16, 1, 146, 1, 9, 9, 0, 0, 0, 0, 0, 0, 69, 64]);
assert.equal(catalog.maps.reduce((count,map)=>count+map.arrNpc.length,0),16);
assert.equal('mapNpcs' in catalog,false);
let socket = await open();
const pong = await exchange(socket, ping);
assert.equal(pong.get(1), 1);
assert.equal(fields(pong.get(13)).get(1).readDoubleLE(0), 42);
const reply = await exchange(socket, Buffer.from([8, 1, 16, 2, 82, 0]));
const rejected = fields(reply.get(12));
assert.equal(rejected.get(2) ?? 0, 0);
assert.equal(rejected.get(3).toString(), 'base_only');
await closed(socket, Buffer.from([8, 1, 16, 2, 82, 0]));
socket = await open();
await closed(socket, '{}');
socket = await open();
await closed(socket, Buffer.alloc(20000));
socket = await open();
await closed(socket, Buffer.from([8, 2, 16, 1, 82, 0]));
console.log('Network PASS: verified CDN, binary ping, gameplay rejection, sequence replay, text/oversize/version rejection.');

if (process.argv.includes('--shutdown'))
{
    socket = await open();
    const disconnected = closed(socket, ping);
    const response = await fetch(base + '/api/dev/shutdown',
    {
        method: 'POST',
        headers: { 'X-TuTien-Local-Control': 'shutdown' }
    });
    assert.equal(response.status, 202);
    await disconnected;
    console.log('Shutdown PASS: active session closed.');
}

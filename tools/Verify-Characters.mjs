import assert from 'node:assert/strict';

const base = process.env.TUTIEN_URL ?? 'http://127.0.0.1:5080';
const unique = Date.now().toString(36) + Math.random().toString(36).slice(2, 6);

class Client
{
    cookies = new Map();

    async request(path, value, csrf = true)
    {
        const headers = {};
        if (value !== undefined)
        {
            if (csrf)
            {
                const token = await this.request('/api/csrf');
                headers['X-CSRF-Token'] = token.body.token;
            }
            headers['Content-Type'] = 'application/json';
        }
        headers.Cookie = [...this.cookies].map(([key, value]) => key + '=' + value).join('; ');
        const response = await fetch(base + path,
        {
            method: value === undefined ? 'GET' : 'POST',
            headers,
            body: value === undefined ? undefined : JSON.stringify(value)
        });
        for (const cookie of response.headers.getSetCookie())
        {
            const pair = cookie.split(';')[0];
            const equals = pair.indexOf('=');
            this.cookies.set(pair.slice(0, equals), pair.slice(equals + 1));
        }
        const text = await response.text();
        return { status: response.status, body: text ? JSON.parse(text) : null, headers: response.headers };
    }
}

const one = new Client();
const two = new Client();
assert.equal((await one.request('/api/characters')).status, 401);
assert.equal((await one.request('/api/register', { username: 'a_' + unique, password: 'TestAccount123!' }, false)).status, 400);
const registered = await one.request('/api/register', { username: 'a_' + unique, password: 'TestAccount123!' });
assert.equal(registered.status, 201);
assert.equal(registered.body.passwordHash, undefined);
assert.ok(registered.headers.getSetCookie().some(cookie => cookie.includes('TuTien.Session=') && cookie.includes('httponly')));
assert.equal((await two.request('/api/register', { username: 'b_' + unique, password: 'TestAccount123!' })).status, 201);
assert.equal((await one.request('/api/characters')).body.slots.length, 3);

function character(slot, suffix)
{
    return { slot, name: 'Đạo ' + unique + suffix, classId: 4, gender: 1, appearanceId: 202 };
}
assert.equal((await one.request('/api/characters', character(4, 'x'))).status, 400);
assert.equal((await one.request('/api/characters', { ...character(1, 'x'), gender: 0 })).status, 400);
assert.equal((await one.request('/api/characters', { ...character(1, 'x'), raceId: 3 })).status, 400);
const first = await one.request('/api/characters', character(1, 'a'));
assert.equal(first.status, 201);
assert.equal(first.body.raceId, 0);
assert.equal(first.body.elementId, 0);
assert.equal((await two.request('/api/characters', { ...character(1, 'a'), name: first.body.name.toLowerCase() })).status, 409);
assert.equal((await two.request(`/api/characters/${first.body.id}/enter`, {})).status, 404);
assert.equal((await one.request(`/api/characters/${first.body.id}/talent/complete`, {})).status, 404);

const race = await Promise.all([
    one.request('/api/characters', character(2, 'b')),
    one.request('/api/characters', character(2, 'c'))
]);
assert.deepEqual(race.map(value => value.status).sort(), [201, 409]);
assert.equal((await one.request('/api/characters', character(3, 'd'))).status, 201);
assert.equal((await one.request('/api/characters', character(1, 'e'))).status, 409);
assert.equal((await one.request('/api/characters')).body.slots.filter(value => value.character).length, 3);

const assigned = await Promise.all(Array.from({ length: 8 }, () => one.request(`/api/characters/${first.body.id}/enter`, {})));
const result = assigned[0].body.character;
assert.ok(result.raceId >= 1 && result.raceId <= 5);
assert.ok(result.elementId >= 1 && result.elementId <= 5);
for (const value of assigned)
{
    assert.equal(value.status, 200);
    assert.equal(value.body.needsTalentReveal, true);
    assert.equal(value.body.character.raceId, result.raceId);
    assert.equal(value.body.character.elementId, result.elementId);
}
assert.equal((await one.request(`/api/characters/${first.body.id}/talent/complete`, {})).status, 204);
assert.equal((await one.request(`/api/characters/${first.body.id}/enter`, {})).body.needsTalentReveal, false);
assert.equal((await one.request('/api/logout', {})).status, 204);
assert.equal((await one.request('/api/characters')).status, 401);
assert.equal((await one.request('/api/login', { username: 'a_' + unique, password: 'wrong-password' })).status, 401);
assert.equal((await one.request('/api/login', { username: 'a_' + unique, password: 'TestAccount123!' })).status, 200);
const saved = (await one.request('/api/characters')).body.slots[0].character;
assert.equal(saved.raceId, result.raceId);
assert.equal(saved.elementId, result.elementId);
assert.equal(saved.talentSeen, true);
console.log('Character API PASS: authentication, CSRF, 3 slots, name uniqueness, gender, ownership, concurrent creation/entry, permanent talent, logout/login.');

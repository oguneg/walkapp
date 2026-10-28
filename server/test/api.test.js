'use strict';
// End-to-end against the Firebase emulators (run through `npm test` in /server, which starts them).
const test = require('node:test');
const assert = require('node:assert/strict');

const PROJECT = 'demo-walkapp';
const AUTH = 'http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1';
const API = `http://127.0.0.1:5001/${PROJECT}/europe-north1/api`;

async function post(url, body, token) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: JSON.stringify(body || {}),
  });
  return { status: res.status, body: await res.json() };
}

async function get(url, token) {
  const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
  return { status: res.status, body: await res.json() };
}

/** A guest account, like the game creates on first launch. */
async function guest() {
  const r = await post(`${AUTH}/accounts:signUp?key=fake`, { returnSecureToken: true });
  return r.body.idToken;
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

test('a convoy end to end', async () => {
  const a = await guest(), b = await guest();

  assert.equal((await get(`${API}/party`, 'not-a-token')).status, 401, 'tokens are checked');

  assert.equal((await post(`${API}/me`, { name: 'Alice' }, a)).body.name, 'Alice');
  assert.equal((await post(`${API}/me`, { name: 'Bob' }, b)).body.name, 'Bob');
  assert.equal((await get(`${API}/party`, a)).body.hasParty, false);

  const created = (await post(`${API}/party/create`, { name: 'Morning walkers', tzOffset: 60 }, a)).body;
  assert.equal(created.party.name, 'Morning walkers');
  assert.equal(created.party.isLeader, true);
  const code = created.party.code;

  assert.equal((await post(`${API}/party/join`, { code: 'ZZZZZZ' }, b)).status, 404, 'unknown code');
  const joined = (await post(`${API}/party/join`, { code: code.toLowerCase() }, b)).body;
  assert.equal(joined.party.memberCount, 2);
  assert.equal(joined.party.week.goal, 40000, 'chill, 2 members');
  assert.equal((await post(`${API}/party/join`, { code }, b)).status, 409, 'already in a convoy');

  // Steps: Alice walked 25,000 this week, Bob 16,000 -> the 40,000 goal is reached.
  await post(`${API}/steps`, { tzOffset: 60, days: [{ date: today(), steps: 25000 }] }, a);
  await post(`${API}/steps`, { tzOffset: 60, days: [{ date: today(), steps: 16000 }] }, b);
  const view = (await get(`${API}/party`, b)).body.party;
  assert.equal(view.week.steps, 41000);
  assert.equal(view.members[0].name, 'Alice', 'leaderboard sorted by steps');
  const goal = view.claimables.find((c) => c.id.endsWith(':goal'));
  assert.ok(goal, 'the weekly goal is claimable');

  const claim = (await post(`${API}/party/claim`, { id: goal.id }, b)).body;
  assert.equal(claim.coins, 2000);
  assert.equal((await post(`${API}/party/claim`, { id: goal.id }, b)).status, 409, 'only once per player');
  assert.equal((await post(`${API}/party/claim`, { id: goal.id }, a)).body.coins, 2000, 'each member claims their own');
  assert.equal((await post(`${API}/party/claim`, { id: 'w:2020-01-06:goal' }, a)).status, 409, 'not a current stage');

  // Votes for next week.
  await post(`${API}/party/vote`, { tier: 'impossible' }, a);
  const voted = (await post(`${API}/party/vote`, { tier: 'leader' }, b)).body.party.vote;
  assert.equal(voted.voters, 2);
  assert.equal(voted.leaderVotes, 1);
  assert.equal(voted.leaderPick, 'impossible');
  assert.equal((await post(`${API}/party/vote`, { tier: 'nonsense' }, a)).status, 400);

  // Full convoy.
  const more = [await guest(), await guest(), await guest()];
  for (const t of more) assert.equal((await post(`${API}/party/join`, { code }, t)).status, 200);
  assert.equal((await post(`${API}/party/join`, { code }, await guest())).status, 409, 'max 5 members');

  // The leader leaves: the longest-standing member (Bob) takes over.
  await post(`${API}/party/leave`, {}, a);
  const afterLeave = (await get(`${API}/party`, b)).body.party;
  assert.equal(afterLeave.isLeader, true);
  assert.equal(afterLeave.memberCount, 4);

  // Delete my data: out of the convoy, profile gone.
  assert.equal((await post(`${API}/me/delete`, {}, b)).body.deleted, true);
  assert.equal((await get(`${API}/party`, b)).body.hasParty, false);
});

test('a guest can link a Google account and keep the same player', async () => {
  const guestSignUp = await post(`${AUTH}/accounts:signUp?key=fake`, { returnSecureToken: true });
  const fakeGoogleToken = JSON.stringify({ sub: `google-${Date.now()}`, email: 'driver@example.com', name: 'Test Driver' });
  const linked = await post(`${AUTH}/accounts:signInWithIdp?key=fake`, {
    idToken: guestSignUp.body.idToken,
    postBody: `id_token=${encodeURIComponent(fakeGoogleToken)}&providerId=google.com`,
    requestUri: 'http://localhost',
    returnSecureToken: true,
  });
  assert.equal(linked.status, 200);
  assert.equal(linked.body.localId, guestSignUp.body.localId, 'same uid after linking');
  assert.equal(linked.body.displayName, 'Test Driver', 'the sign-in response carries the Google name');
  // The game sends that name to the server (the ID token doesn't carry it after linking a guest).
  const me = (await post(`${API}/me`, { name: linked.body.displayName }, linked.body.idToken)).body;
  assert.equal(me.name, 'Test Driver');
  assert.equal(me.uid, guestSignUp.body.localId);
});

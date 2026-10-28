'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const C = require('../convoy');

test('weeks start on Monday', () => {
  assert.equal(C.weekStart('2026-10-28'), '2026-10-26'); // Wednesday
  assert.equal(C.weekStart('2026-10-26'), '2026-10-26'); // Monday
  assert.equal(C.weekStart('2026-11-01'), '2026-10-26'); // Sunday
  assert.deepEqual(C.weekDays('2026-10-26').slice(-1), ['2026-11-01']);
});

test('local date follows the convoy time zone', () => {
  const lateSundayUtc = Date.parse('2026-11-01T23:30:00Z');
  assert.equal(C.localDate(lateSundayUtc, 0), '2026-11-01');
  assert.equal(C.localDate(lateSundayUtc, 60), '2026-11-02'); // Stockholm (CET) is already Monday
});

test('months have their real number of days', () => {
  assert.equal(C.monthDays('2026-02').length, 28);
  assert.equal(C.monthDays('2028-02').length, 29);
  assert.equal(C.monthDays('2026-10').slice(-1)[0], '2026-10-31');
});

test('goals scale with members: 4 members, 1,000,000 a month', () => {
  assert.equal(C.monthGoal(4), 1000000);
  assert.equal(C.weekGoal('chill', 5), 100000);
});

test('sumSteps adds members over the given dates only', () => {
  const days = { a: { '2026-10-26': 1000, '2026-10-25': 9999 }, b: { '2026-10-27': 500 } };
  const r = C.sumSteps(['a', 'b'], days, C.weekDays('2026-10-26'));
  assert.equal(r.total, 1500);
  assert.equal(r.perMember.a, 1000);
});

test('votes: most votes win', () => {
  const r = C.resolveVotes({ a: 'impossible', b: 'impossible', c: 'chill' }, 'c', 'chill');
  assert.equal(r.tier, 'impossible');
});

test('votes: "leader decides" counts for the leader\'s pick', () => {
  const r = C.resolveVotes({ lead: 'chill', b: 'leader', c: 'impossible' }, 'lead', 'challenging');
  assert.equal(r.tier, 'chill');
  assert.equal(r.tally.chill, 2);
});

test('votes: the leader breaks a tie', () => {
  const r = C.resolveVotes({ lead: 'challenging', b: 'impossible' }, 'lead', 'chill');
  assert.equal(r.tier, 'challenging');
});

test('votes: nobody voted keeps the current tier, non-voters are not counted', () => {
  assert.equal(C.resolveVotes({}, 'lead', 'challenging').tier, 'challenging');
  assert.equal(C.resolveVotes({ b: 'impossible' }, 'lead', 'chill').tier, 'impossible');
});

test('step reports only raise a day, are capped, and ignore junk dates', () => {
  const merged = C.mergeDays(
    { '2026-10-27': 4000 },
    [
      { date: '2026-10-27', steps: 3000 },   // lower: ignored
      { date: '2026-10-28', steps: 999999 }, // capped
      { date: '2027-01-01', steps: 100 },    // future
      { date: 'garbage', steps: 100 },
    ],
    '2026-10-28');
  assert.deepEqual(merged, { '2026-10-27': 4000, '2026-10-28': C.MAX_STEPS_PER_DAY });
});

test('week stages: goal, 1.5x and 2x', () => {
  const s = C.weekStages('chill', 2, 61000, [], '2026-10-26');
  assert.deepEqual(s.map((x) => [x.steps, x.reached]), [[40000, true], [60000, true], [80000, false]]);
});

test('names are cleaned', () => {
  assert.equal(C.cleanName('  <b>Bob</b>  ', 'x'), 'bBob/b');
  assert.equal(C.cleanName('', 'Driver'), 'Driver');
});

test('invite codes avoid lookalike characters', () => {
  for (let i = 0; i < 200; i++) assert.match(C.newInviteCode(), /^[A-HJ-NP-Z2-9]{6}$/);
});

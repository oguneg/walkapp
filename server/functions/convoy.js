'use strict';
// Convoy (party) rules, kept free of storage so they can be unit tested.
//
// A convoy is 2-5 friends or partners walking together. It always runs two goals on the calendar:
//   - a weekly goal, Monday to Sunday, whose difficulty the members vote on during the week before;
//   - a monthly goal for the calendar month, with milestone rewards at a quarter and half way.
// Goals scale with the number of members. Only walked steps count (each phone reports its daily totals).

const TIERS = {
  chill:       { label: 'Chill',       perMember: 20000, reward: 2000 },
  challenging: { label: 'Challenging', perMember: 35000, reward: 4000 },
  impossible:  { label: 'Impossible',  perMember: 56000, reward: 8000 },
};
const TIER_ORDER = ['chill', 'challenging', 'impossible'];
const LEADER_VOTE = 'leader';

// Past the weekly goal, bonus stages keep fast convoys busy until Monday.
const WEEK_STAGES = [
  { id: 'goal', mult: 1.0, rewardShare: 1.0 },
  { id: 'bonus1', mult: 1.5, rewardShare: 0.5 },
  { id: 'bonus2', mult: 2.0, rewardShare: 0.5 },
];

const MONTH_PER_MEMBER = 250000; // e.g. 4 members, 1,000,000 steps: tough but doable
const MONTH_MILESTONES = [
  { id: 'quarter', share: 0.25, reward: 5000 },
  { id: 'half', share: 0.5, reward: 10000 },
  { id: 'full', share: 1.0, reward: 30000 },
];

// Themes for the live-service months. Placeholder names until the monthly pass gives each month its route.
const MONTH_THEMES = [
  'New Year Haul', 'Winter Roads', 'Spring Thaw', 'April Showers', 'Blossom Run', 'Midsummer Miles',
  'Summer Road Trip', 'Harvest Lanes', 'Back to the Road', 'Autumn Haul', 'Long Nights', 'Holiday Express',
];

const MAX_MEMBERS = 5;
const MAX_STEPS_PER_DAY = 80000;
const KEEP_DAYS = 62;

// ------------------------------------------------------------------ dates (all as 'YYYY-MM-DD' strings in the convoy's time zone)

/** Local date of `now` (ms) for a time zone given as minutes east of UTC. */
function localDate(nowMs, tzOffsetMinutes) {
  return new Date(nowMs + tzOffsetMinutes * 60000).toISOString().slice(0, 10);
}

function addDays(date, days) {
  const d = new Date(date + 'T00:00:00Z');
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** The Monday starting the week that contains `date`; also the week's key. */
function weekStart(date) {
  const d = new Date(date + 'T00:00:00Z');
  const sinceMonday = (d.getUTCDay() + 6) % 7;
  return addDays(date, -sinceMonday);
}

function monthKey(date) {
  return date.slice(0, 7);
}

function monthDays(key) {
  const [y, m] = key.split('-').map(Number);
  const days = new Date(Date.UTC(y, m, 0)).getUTCDate();
  const out = [];
  for (let i = 1; i <= days; i++) out.push(`${key}-${String(i).padStart(2, '0')}`);
  return out;
}

function weekDays(start) {
  return Array.from({ length: 7 }, (_, i) => addDays(start, i));
}

function monthTheme(key) {
  return MONTH_THEMES[Number(key.slice(5, 7)) - 1];
}

// ------------------------------------------------------------------ goals

function weekGoal(tier, memberCount) {
  return TIERS[tier].perMember * Math.max(1, memberCount);
}

function monthGoal(memberCount) {
  return MONTH_PER_MEMBER * Math.max(1, memberCount);
}

/** Sum of the members' steps on the given dates. `days` maps uid -> { 'YYYY-MM-DD': steps }. */
function sumSteps(memberUids, days, dates) {
  const perMember = {};
  let total = 0;
  for (const uid of memberUids) {
    const own = days[uid] || {};
    let sum = 0;
    for (const date of dates) sum += own[date] || 0;
    perMember[uid] = sum;
    total += sum;
  }
  return { total, perMember };
}

function weekStages(tier, memberCount, steps, claimedIds, weekKey) {
  const goal = weekGoal(tier, memberCount);
  return WEEK_STAGES.map((s) => {
    const id = `w:${weekKey}:${s.id}`;
    const target = Math.round(goal * s.mult);
    return {
      id,
      steps: target,
      reward: Math.round(TIERS[tier].reward * s.rewardShare),
      reached: steps >= target,
      claimed: claimedIds.includes(id),
    };
  });
}

function monthMilestones(memberCount, steps, claimedIds, key) {
  const goal = monthGoal(memberCount);
  return MONTH_MILESTONES.map((m) => {
    const id = `m:${key}:${m.id}`;
    const target = Math.round(goal * m.share);
    return { id, steps: target, reward: m.reward, reached: steps >= target, claimed: claimedIds.includes(id) };
  });
}

// ------------------------------------------------------------------ votes

/**
 * Next week's difficulty from the votes cast this week.
 * Every member has one vote: a tier, or "leader decides" (which counts for the leader's pick).
 * Members who don't vote aren't counted. The leader's vote counts once but breaks any tie.
 * Nobody voted: keep the current tier.
 */
function resolveVotes(votes, leaderUid, currentTier) {
  const leaderPick = TIERS[votes[leaderUid]] ? votes[leaderUid] : null;
  const tally = { chill: 0, challenging: 0, impossible: 0 };
  let cast = 0;
  for (const vote of Object.values(votes)) {
    const tier = vote === LEADER_VOTE ? (leaderPick || currentTier) : vote;
    if (!TIERS[tier]) continue;
    tally[tier]++;
    cast++;
  }
  if (cast === 0) return { tier: currentTier, tally };

  const best = Math.max(...Object.values(tally));
  const tied = TIER_ORDER.filter((t) => tally[t] === best);
  let tier;
  if (tied.length === 1) tier = tied[0];
  else if (leaderPick && tied.includes(leaderPick)) tier = leaderPick;
  else if (tied.includes(currentTier)) tier = currentTier;
  else tier = tied[0];
  return { tier, tally };
}

// ------------------------------------------------------------------ input checks

function cleanName(name, fallback) {
  const s = String(name || '').replace(/[\u0000-\u001f<>]/g, '').trim().slice(0, 20);
  return s || fallback;
}

/** Keep plausible daily totals within the kept window; reports can only raise a day, never lower it. */
function mergeDays(existing, reported, today) {
  const oldest = addDays(today, -KEEP_DAYS);
  const tomorrow = addDays(today, 1); // time zones ahead of the convoy's
  const out = {};
  for (const [date, steps] of Object.entries(existing || {})) if (date >= oldest) out[date] = steps;
  for (const item of reported || []) {
    const date = String(item.date || '');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || date < oldest || date > tomorrow) continue;
    const steps = Math.max(0, Math.min(MAX_STEPS_PER_DAY, Math.floor(Number(item.steps) || 0)));
    out[date] = Math.max(out[date] || 0, steps);
  }
  return out;
}

function newInviteCode(random = Math.random) {
  const letters = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // no 0/O, 1/I
  let code = '';
  for (let i = 0; i < 6; i++) code += letters[Math.floor(random() * letters.length)];
  return code;
}

module.exports = {
  TIERS, TIER_ORDER, LEADER_VOTE, WEEK_STAGES, MONTH_MILESTONES, MAX_MEMBERS, MAX_STEPS_PER_DAY, KEEP_DAYS,
  localDate, addDays, weekStart, weekDays, monthKey, monthDays, monthTheme,
  weekGoal, monthGoal, sumSteps, weekStages, monthMilestones, resolveVotes,
  cleanName, mergeDays, newInviteCode,
};

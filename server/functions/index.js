'use strict';
// Walkapp API: one HTTPS function, JSON in and out. Every request carries a Firebase ID token
// (guest or Google account). Clients never touch the database directly; Firestore rules deny everything.
//
//   POST /me            { name?, tzOffset? }          profile (created on first call)
//   POST /me/delete                                    leave the convoy and delete everything stored for this player
//   POST /steps         { days: [{ date, steps }] }    daily step totals from the phone's step history
//   GET  /party                                        the player's convoy, or { hasParty: false }
//   POST /party/create  { name, tzOffset }
//   POST /party/join    { code }
//   POST /party/leave
//   POST /party/vote    { tier }                       next week's difficulty: chill | challenging | impossible | leader
//   POST /party/claim   { id }                         a reached goal stage or milestone -> { coins }

const { onRequest } = require('firebase-functions/v2/https');
const { setGlobalOptions } = require('firebase-functions/v2');
const admin = require('firebase-admin');
const C = require('./convoy');

admin.initializeApp();
setGlobalOptions({ region: 'europe-north1', maxInstances: 10 });
const db = admin.firestore();

class ApiError extends Error {
  constructor(status, code, message) {
    super(message || code);
    this.status = status;
    this.code = code;
  }
}

// ------------------------------------------------------------------ helpers

async function authenticate(req) {
  const header = req.get('Authorization') || '';
  const match = header.match(/^Bearer (.+)$/);
  if (!match) throw new ApiError(401, 'unauthenticated');
  try {
    return await admin.auth().verifyIdToken(match[1]);
  } catch (e) {
    throw new ApiError(401, 'unauthenticated');
  }
}

function defaultName(uid) {
  return `Driver ${uid.slice(-4).toUpperCase()}`;
}

function partyToday(party, now) {
  return C.localDate(now, party.tzOffset || 0);
}

/** Moves a convoy into the current week: last week's votes decide the new difficulty. */
function rollWeek(party, today) {
  const current = C.weekStart(today);
  if (party.weekKey === current) return false;
  const { tier, tally } = C.resolveVotes(party.votes || {}, party.leader, party.tier || 'chill');
  party.lastWeek = { key: party.weekKey, tier: party.tier, tally };
  party.weekKey = current;
  party.tier = tier;
  party.votes = {};
  return true;
}

async function loadDays(uids) {
  if (uids.length === 0) return { days: {}, names: {} };
  const snaps = await db.getAll(...uids.map((uid) => db.collection('users').doc(uid)));
  const days = {}, names = {};
  snaps.forEach((snap, i) => {
    const data = snap.exists ? snap.data() : {};
    days[uids[i]] = data.days || {};
    names[uids[i]] = data.name || defaultName(uids[i]);
  });
  return { days, names };
}

function claimsFor(party, uid) {
  const claims = party.claims || {};
  return Object.keys(claims).filter((id) => (claims[id] || []).includes(uid));
}

async function buildView(partyId, party, uid, now) {
  const members = party.members || [];
  const { days, names } = await loadDays(members);
  const today = partyToday(party, now);
  const mine = claimsFor(party, uid);

  const weekDates = C.weekDays(party.weekKey);
  const week = C.sumSteps(members, days, weekDates);
  const mKey = C.monthKey(today);
  const month = C.sumSteps(members, days, C.monthDays(mKey));

  const stages = C.weekStages(party.tier, members.length, week.total, mine, party.weekKey);
  const milestones = C.monthMilestones(members.length, month.total, mine, mKey);

  // Reached but unclaimed: this week and month, and the previous ones (players who were away).
  const claimables = [];
  const addClaimables = (list, label) => {
    for (const s of list) if (s.reached && !s.claimed) claimables.push({ id: s.id, label, reward: s.reward });
  };
  addClaimables(stages, 'This week');
  addClaimables(milestones, C.monthTheme(mKey));
  if (party.lastWeek && party.lastWeek.key && party.lastWeek.tier) {
    const last = C.sumSteps(members, days, C.weekDays(party.lastWeek.key));
    addClaimables(C.weekStages(party.lastWeek.tier, members.length, last.total, mine, party.lastWeek.key), 'Last week');
  }
  const prevMonth = C.monthKey(C.addDays(`${mKey}-01`, -1));
  const prev = C.sumSteps(members, days, C.monthDays(prevMonth));
  addClaimables(C.monthMilestones(members.length, prev.total, mine, prevMonth), C.monthTheme(prevMonth));

  const votes = party.votes || {};
  const leaderPick = C.TIERS[votes[party.leader]] ? votes[party.leader] : null;
  const options = C.TIER_ORDER.map((tier) => ({
    tier,
    label: C.TIERS[tier].label,
    perMember: C.TIERS[tier].perMember,
    goal: C.weekGoal(tier, members.length),
    reward: C.TIERS[tier].reward,
    votes: Object.values(votes).filter((v) => v === tier).length,
  }));

  const monthEnd = C.monthDays(mKey).slice(-1)[0];
  const daysBetween = (a, b) => Math.round((Date.parse(b) - Date.parse(a)) / 86400000);

  return {
    hasParty: true,
    party: {
      id: partyId,
      name: party.name,
      code: party.code,
      isLeader: party.leader === uid,
      memberCount: members.length,
      maxMembers: C.MAX_MEMBERS,
      members: members
        .map((m) => ({
          uid: m,
          name: names[m],
          isLeader: m === party.leader,
          isMe: m === uid,
          weekSteps: week.perMember[m],
          monthSteps: month.perMember[m],
        }))
        .sort((a, b) => b.weekSteps - a.weekSteps),
      week: {
        key: party.weekKey,
        start: weekDates[0],
        end: weekDates[6],
        daysLeft: Math.max(0, daysBetween(today, weekDates[6]) + 1),
        tier: party.tier,
        tierLabel: C.TIERS[party.tier].label,
        goal: C.weekGoal(party.tier, members.length),
        steps: week.total,
        stages,
      },
      vote: {
        nextStart: C.addDays(party.weekKey, 7),
        myVote: votes[uid] || '',
        leaderVotes: Object.values(votes).filter((v) => v === C.LEADER_VOTE).length,
        leaderPick: leaderPick || '',
        voters: Object.keys(votes).length,
        options,
      },
      month: {
        key: mKey,
        theme: C.monthTheme(mKey),
        goal: C.monthGoal(members.length),
        steps: month.total,
        daysLeft: Math.max(0, daysBetween(today, monthEnd) + 1),
        milestones,
      },
      claimables,
    },
  };
}

/** Reads the player's convoy inside a transaction, rolling it into the current week when needed. */
async function withParty(tx, uid, now) {
  const userRef = db.collection('users').doc(uid);
  const user = await tx.get(userRef);
  const partyId = user.exists ? user.data().partyId : null;
  if (!partyId) return { userRef, user };
  const partyRef = db.collection('parties').doc(partyId);
  const partySnap = await tx.get(partyRef);
  if (!partySnap.exists) return { userRef, user };
  const party = partySnap.data();
  const rolled = rollWeek(party, partyToday(party, now));
  return { userRef, user, partyId, partyRef, party, rolled };
}

// ------------------------------------------------------------------ routes

const routes = {
  async 'POST /me'(uid, token, body) {
    const ref = db.collection('users').doc(uid);
    const snap = await ref.get();
    const existing = snap.exists ? snap.data() : {};
    const name = C.cleanName(body.name || existing.name || token.name, defaultName(uid));
    await ref.set({ name, updatedAt: Date.now() }, { merge: true });
    return { uid, name, partyId: existing.partyId || '' };
  },

  async 'POST /me/delete'(uid, token, body, now) {
    await leaveParty(uid, now);
    await db.collection('users').doc(uid).delete();
    return { deleted: true };
  },

  async 'POST /steps'(uid, token, body, now) {
    const ref = db.collection('users').doc(uid);
    await db.runTransaction(async (tx) => {
      const snap = await tx.get(ref);
      const today = C.localDate(now, Number(body.tzOffset) || 0);
      const days = C.mergeDays(snap.exists ? snap.data().days : {}, body.days, today);
      tx.set(ref, { days, updatedAt: now }, { merge: true });
    });
    return { ok: true };
  },

  async 'GET /party'(uid, token, body, now) {
    const result = await db.runTransaction(async (tx) => {
      const ctx = await withParty(tx, uid, now);
      if (!ctx.party) return null;
      if (ctx.rolled) tx.set(ctx.partyRef, ctx.party);
      return ctx;
    });
    if (!result) return { hasParty: false };
    return buildView(result.partyId, result.party, uid, now);
  },

  async 'POST /party/create'(uid, token, body, now) {
    const userRef = db.collection('users').doc(uid);
    const partyRef = db.collection('parties').doc();
    const result = await db.runTransaction(async (tx) => {
      const user = await tx.get(userRef);
      if (user.exists && user.data().partyId) throw new ApiError(409, 'already_in_party');

      let code, codeRef;
      for (let i = 0; i < 5; i++) {
        code = C.newInviteCode();
        codeRef = db.collection('codes').doc(code);
        if (!(await tx.get(codeRef)).exists) break;
        code = null;
      }
      if (!code) throw new ApiError(503, 'try_again');

      const tzOffset = Math.max(-720, Math.min(840, Math.round(Number(body.tzOffset) || 0)));
      const userName = (user.exists && user.data().name) || defaultName(uid);
      const party = {
        name: C.cleanName(body.name, `${userName}'s convoy`),
        code,
        leader: uid,
        members: [uid],
        tzOffset,
        weekKey: C.weekStart(C.localDate(now, tzOffset)),
        tier: 'chill',
        votes: {},
        claims: {},
        createdAt: now,
      };
      tx.set(partyRef, party);
      tx.set(codeRef, { partyId: partyRef.id });
      tx.set(userRef, { partyId: partyRef.id, name: userName }, { merge: true });
      return party;
    });
    return buildView(partyRef.id, result, uid, now);
  },

  async 'POST /party/join'(uid, token, body, now) {
    const code = String(body.code || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
    const userRef = db.collection('users').doc(uid);
    const result = await db.runTransaction(async (tx) => {
      const user = await tx.get(userRef);
      if (user.exists && user.data().partyId) throw new ApiError(409, 'already_in_party');
      const codeSnap = await tx.get(db.collection('codes').doc(code || '-'));
      if (!codeSnap.exists) throw new ApiError(404, 'code_not_found');
      const partyRef = db.collection('parties').doc(codeSnap.data().partyId);
      const partySnap = await tx.get(partyRef);
      if (!partySnap.exists) throw new ApiError(404, 'code_not_found');
      const party = partySnap.data();
      if (party.members.length >= C.MAX_MEMBERS) throw new ApiError(409, 'party_full');
      rollWeek(party, partyToday(party, now));
      party.members.push(uid);
      tx.set(partyRef, party);
      tx.set(userRef, { partyId: partyRef.id, name: (user.exists && user.data().name) || defaultName(uid) }, { merge: true });
      return { partyId: partyRef.id, party };
    });
    return buildView(result.partyId, result.party, uid, now);
  },

  async 'POST /party/leave'(uid, token, body, now) {
    await leaveParty(uid, now);
    return { hasParty: false };
  },

  async 'POST /party/vote'(uid, token, body, now) {
    const tier = String(body.tier || '');
    if (!C.TIERS[tier] && tier !== C.LEADER_VOTE) throw new ApiError(400, 'bad_tier');
    const result = await db.runTransaction(async (tx) => {
      const ctx = await withParty(tx, uid, now);
      if (!ctx.party) throw new ApiError(404, 'no_party');
      ctx.party.votes = ctx.party.votes || {};
      ctx.party.votes[uid] = tier;
      tx.set(ctx.partyRef, ctx.party);
      return ctx;
    });
    return buildView(result.partyId, result.party, uid, now);
  },

  async 'POST /party/claim'(uid, token, body, now) {
    const id = String(body.id || '');
    let coins = 0;
    const result = await db.runTransaction(async (tx) => {
      const ctx = await withParty(tx, uid, now);
      if (!ctx.party) throw new ApiError(404, 'no_party');
      // Same rules as the view: only reached, unclaimed stages of this or the previous period.
      const view = await buildView(ctx.partyId, ctx.party, uid, now);
      const item = view.party.claimables.find((c) => c.id === id);
      if (!item) throw new ApiError(409, 'not_claimable');
      ctx.party.claims = ctx.party.claims || {};
      ctx.party.claims[id] = [...(ctx.party.claims[id] || []), uid];
      tx.set(ctx.partyRef, ctx.party);
      coins = item.reward;
      return ctx;
    });
    const view = await buildView(result.partyId, result.party, uid, now);
    return { coins, ...view };
  },
};

async function leaveParty(uid, now) {
  const userRef = db.collection('users').doc(uid);
  await db.runTransaction(async (tx) => {
    const ctx = await withParty(tx, uid, now);
    if (!ctx.party) {
      if (ctx.user.exists) tx.set(userRef, { partyId: null }, { merge: true });
      return;
    }
    const party = ctx.party;
    party.members = party.members.filter((m) => m !== uid);
    if (party.votes) delete party.votes[uid];
    if (party.members.length === 0) {
      tx.delete(ctx.partyRef);
      tx.delete(db.collection('codes').doc(party.code));
    } else {
      if (party.leader === uid) party.leader = party.members[0]; // longest-standing member takes over
      tx.set(ctx.partyRef, party);
    }
    tx.set(userRef, { partyId: null }, { merge: true });
  });
}

exports.api = onRequest({ cors: false }, async (req, res) => {
  try {
    const path = (req.path || '/').replace(/\/+$/, '') || '/';
    const handler = routes[`${req.method} ${path}`];
    if (!handler) throw new ApiError(404, 'not_found');
    const token = await authenticate(req);
    const body = req.body && typeof req.body === 'object' ? req.body : {};
    const now = Date.now() + (Number(process.env.WALKAPP_TEST_OFFSET_MS) || 0);
    res.json(await handler(token.uid, token, body, now));
  } catch (e) {
    const status = e instanceof ApiError ? e.status : 500;
    if (status === 500) console.error(e);
    res.status(status).json({ error: e.code || 'internal' });
  }
});

// Exposed for tests.
exports._rollWeek = rollWeek;

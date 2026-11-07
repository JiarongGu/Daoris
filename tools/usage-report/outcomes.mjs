import { join } from 'node:path';
import { named, record, text } from './reading.mjs';
import { sampleOf } from './figures.mjs';

export const minute = (iso) => iso.slice(0, 16).replace('T', ' ');

/**
 * The routes the outcomes read (OUTCOME1), each a GET the local host answers, closed records included: the quests as
 * their operations replay to (a local host serves no operations, D143's note), the session records as this machine is
 * answered them (its accounts included, D49 §4), and the asks with the person's words (DRIFT1a), a local host's alone.
 */
export const RECORD_ROUTES = Object.freeze({
  quests: '/api/quests?includeClosed=true',
  sessions: '/api/sessions?includeClosed=true',
  asks: '/api/asks?includeClosed=true',
});

/**
 * The host's records for the outcomes: each route's list, or null and the sentence saying why it was not read (a route
 * that failed, answered no list, or did not answer). Every request is a GET; nothing is sent but the route.
 */
export async function readRecords(service, { fetch: get = globalThis.fetch, timeoutMs = 15_000 } = {}) {
  const read = async (route) => {
    let body;
    try {
      const response = await get(`${service}${route}`, {
        method: 'GET', headers: { accept: 'application/json' }, signal: AbortSignal.timeout(timeoutMs),
      });
      if (!response.ok) return { list: null, unread: `${route} answered ${response.status}` };
      body = await response.text();
    } catch (error) {
      // Node's fetch says only `fetch failed`; its cause carries the system's code, or the reason it refused the address.
      const reason = named(error?.cause?.code) ?? sampleOf(error?.cause?.message) ?? sampleOf(error?.message) ?? 'no reason given';
      return { list: null, unread: `${route} did not answer: ${reason}` };
    }
    let root;
    try {
      root = JSON.parse(body);
    } catch {
      return { list: null, unread: `${route} answered something that is not JSON` };
    }
    return Array.isArray(root) ? { list: root, unread: null } : { list: null, unread: `${route} answered no list` };
  };
  const [quests, sessions, asks] = await Promise.all(
    [RECORD_ROUTES.quests, RECORD_ROUTES.sessions, RECORD_ROUTES.asks].map(read));
  return {
    quests: quests.list, questsUnread: quests.unread,
    sessions: sessions.list, sessionsUnread: sessions.unread,
    asks: asks.list, asksUnread: asks.unread,
  };
}

/** A sender that is an ask: `ask #<id>`, the service's `AskDesk.SenderOf` (D65 §1a), a chain step's included. */
const ASK_SENDER = 'ask #';

/**
 * The ask a quest's sender names, or null for a quest a repository asked: the service's `AskDesk.AskOf`, read the same
 * way (ordinal, and an id after the prefix), as the driver's `AskWords.AskOf` reads it.
 */
export function askOf(sender) {
  return typeof sender === 'string' && sender.length > ASK_SENDER.length && sender.startsWith(ASK_SENDER)
    ? sender.slice(ASK_SENDER.length)
    : null;
}

/**
 * How a session record ended, as the strikes read it (`ServiceClient.ReadStrikes`, D58 as D104 and D125 amend it): a
 * failure an account's limit made is `limit`, a stop that was not the person's is `interrupted`, and anything else is
 * its state as the record says it. A flag counts only as the boolean true.
 */
export function endingOf(session) {
  const state = named(session?.state);
  if (state === 'failed' && session.limit === true) return 'limit';
  if (state === 'stopped' && session.interrupted === true) return 'interrupted';
  return state ?? '(unsaid)';
}

/** The endings after which the next run on the quest carries it on (D80, D104): a failure, a limit, an interrupted stop. */
const CUT_OFF = ['failed', 'limit', 'interrupted'];

/** The endings that are strikes (D58 as amended): a failure that was not a limit, and a stop that was not the person's. */
const STRIKES = ['failed', 'interrupted'];

/** A moment a record carries, in milliseconds, or null where it carries none that reads. */
export const momentOf = (value) => {
  const time = typeof value === 'string' ? Date.parse(value) : Number.NaN;
  return Number.isNaN(time) ? null : time;
};
const isoOf = (time) => (time === null ? null : new Date(time).toISOString());

/** The outcomes when the records were not read, and why. */
const unreadOutcomes = (why) => ({ read: false, unread: why, quests: [], byRepository: [], byAgent: [] });

/**
 * One quest's outcome (OUTCOME1), from its record, its sessions on the host, its ask's words and the log's parks. Each
 * fact no store keeps is null and said in `missing`, with why. A teammate's record (keyed `origin/id`, SYNC4) is listed
 * and counted apart: its account and its log are on its machine, and a strike is this machine's judgement of its own
 * runs (`ReadStrikes`).
 */
function outcomeOf(quest, runs, facts) {
  const missing = [];
  const status = (named(quest.status) ?? '(unsaid)').toLowerCase();
  const filed = momentOf(quest.filed);
  const accepted = momentOf(quest.accepted);

  const sessions = runs.map((run) => {
    const teammate = run.id.includes('/');
    return {
      session: run.id, state: named(run.state) ?? '(unsaid)', ending: endingOf(run), adapter: named(run.door),
      account: teammate ? '(a teammate\'s)' : named(run.profile) ?? '(own sign-in)', teammate,
      created: isoOf(momentOf(run.created)),
      // The log is this machine's, and holds a session's parks only where the period holds its start.
      parked: !teammate && facts.started.has(run.id) ? facts.parked.get(run.id) ?? 0 : null,
    };
  });
  const mine = sessions.filter((session) => !session.teammate);

  // A carry-on is a run that follows a cut-off on its quest; a stand-down is the race resolving, not a run (`ReadLastRun`).
  let carryOns = 0;
  let previous = null;
  for (const session of mine) {
    if (session.ending === 'stood-down') continue;
    if (previous && CUT_OFF.includes(previous.ending)) carryOns++;
    previous = session;
  }

  const unparked = mine.filter((session) => session.parked === null).map((session) => session.session);
  const parks = unparked.length > 0 ? null : mine.reduce((sum, session) => sum + session.parked, 0);
  if (parks === null) {
    missing.push({
      fact: 'parks',
      why: `the period read holds no start for ${unparked.join(', ')} (one that began before the period, or that no log saw)`,
    });
  }

  let answers = null;
  let added = null;
  const askId = askOf(quest.from);
  const ask = askId && facts.asks ? facts.asks.get(askId) : null;
  const keptFrom = momentOf(ask?.wordsKeptFrom);
  if (!askId) {
    missing.push({ fact: 'answers', why: 'asked by a repository, and only an ask keeps the person\'s words (D133)' });
  } else if (!facts.asks) {
    missing.push({ fact: 'answers', why: `its ask's words were not read: ${facts.asksUnread ?? 'the host answered no asks'}` });
  } else if (!ask) {
    missing.push({ fact: 'answers', why: `its ask #${askId} is not on the host` });
  } else if (!Array.isArray(ask.words)) {
    missing.push({ fact: 'answers', why: 'the host answered its ask without the person\'s words (a host from before DRIFT1a)' });
  } else if (keptFrom !== null && (filed === null || keptFrom > filed)) {
    missing.push({
      fact: 'answers',
      why: `its ask keeps the person's words from ${minute(isoOf(keptFrom))} UTC, after the quest was published`,
    });
  } else {
    const on = ask.words.filter((each) => record(each) && each.quest === quest.id);
    answers = on.filter((each) => each.kind === 'answered').length;
    added = on.filter((each) => each.kind === 'added' || each.kind === 'reopened').length;
  }

  const replies = Array.isArray(quest.answers) ? quest.answers.filter(record) : [];
  const departures = replies.filter((reply) => text(reply.departed)).length;

  let closed = null;
  if (status === 'done' || status === 'declined') {
    // A yes is an operation that moves the quest's moment (`QuestLog.Step`), and a local host serves no operations.
    const at = status === 'done' && accepted !== null ? null : momentOf(quest.updated);
    closed = { how: status, at: isoOf(at), afterMs: at !== null && filed !== null && at >= filed ? at - filed : null };
    if (status === 'declined') closed.bySession = sessions.some((session) => session.ending === 'declined');
    if (at === null) {
      missing.push({
        fact: 'closed',
        why: accepted !== null
          ? 'the person\'s yes is its last moment, and a local host serves a quest\'s state, not when its done was made'
          : 'the host said no moment for it',
      });
    }
  }

  // A first pass: done by its one working session, with no departure and nothing asked of the person (an answer on its
  // ask, or a park in the log). What the person chose to add while it ran is theirs, not the work's asking.
  let firstPass = null;
  if (status === 'done') {
    const working = sessions.filter((session) => session.ending !== 'stood-down');
    const logKnows = working.length > 0 && working.every((session) => session.parked !== null);
    const asked = (answers ?? 0) > 0 || working.some((session) => (session.parked ?? 0) > 0);
    if (working.length === 0) {
      missing.push({ fact: 'firstPass', why: 'no session record of its work' });
    } else if (working.length > 1 || departures > 0 || asked) {
      firstPass = false;
    } else if (answers === null && !logKnows) {
      missing.push({ fact: 'firstPass', why: 'neither its ask\'s words nor the log say whether the person was asked' });
    } else {
      firstPass = true;
    }
  }

  return {
    quest: quest.id,
    repository: named(quest.to) ?? '(unnamed)',
    workspace: named(quest.workspace),
    status,
    waiting: text(quest.awaits) !== null,
    filed: isoOf(filed),
    closed,
    sessions,
    teammates: sessions.length - mine.length,
    carryOns,
    strikes: mine.filter((session) => STRIKES.includes(session.ending)).length,
    limits: sessions.filter((session) => session.ending === 'limit').length,
    parks,
    answers,
    added,
    requirements: Array.isArray(quest.requirements) ? quest.requirements.length : 0,
    departures,
    held: quest.held === true,
    accepted: isoOf(accepted),
    firstPass,
    missing,
  };
}

/**
 * The outcomes rolled up by a key (OUTCOME1): how many quests, done and declined; first passes of those done, with how
 * many are not known; the quests carried on of those this machine ran, and the carry-ons; and the answers over the quests
 * whose answers are kept, with how many are not. Each figure carries its count, and none is folded into a score (D54).
 */
function rollUp(outcomes, keyOf) {
  const groups = new Map();
  for (const outcome of outcomes) {
    const { id, fields } = keyOf(outcome);
    const group = groups.get(id) ?? {
      ...fields, quests: 0, done: 0, declined: 0,
      firstPass: { yes: 0, of: 0, unknown: 0 },
      carryOn: { quests: 0, of: 0, carryOns: 0 },
      answers: { total: 0, over: 0, unknown: 0 },
    };
    group.quests++;
    if (outcome.status === 'done') {
      group.done++;
      group.firstPass.of++;
      if (outcome.firstPass === true) group.firstPass.yes++;
      else if (outcome.firstPass === null) group.firstPass.unknown++;
    }
    if (outcome.status === 'declined') group.declined++;
    if (outcome.sessions.some((session) => !session.teammate)) {
      group.carryOn.of++;
      if (outcome.carryOns > 0) group.carryOn.quests++;
      group.carryOn.carryOns += outcome.carryOns;
    }
    if (outcome.answers === null) group.answers.unknown++;
    else {
      group.answers.over++;
      group.answers.total += outcome.answers;
    }
    groups.set(id, group);
  }
  return [...groups.values()];
}

/** No session yet: the agent and account a quest is rolled up under before anything ran it. */
const NO_SESSION = '(no session)';

/**
 * Each quest the period touched (published, moved, accepted, or a session on it opened or moved within it), oldest
 * published first, with its outcome; then the roll-ups by repository and by agent and account. A quest is rolled up under
 * the agent and account of its first working session: the one that took the first pass. Null records are the outcomes
 * not read, and why.
 */
export function outcomesOf(records, lines, { from, to }) {
  if (!records) return unreadOutcomes('no host was named: --service <url> reads its quests and sessions');
  const problem = records.questsUnread ?? records.sessionsUnread ?? null;
  if (problem || !Array.isArray(records.quests) || !Array.isArray(records.sessions)) {
    return unreadOutcomes(problem ?? 'the host\'s quests and sessions did not read');
  }

  const started = new Set();
  const parked = new Map();
  for (const line of lines) {
    const session = named(line.data.session);
    if (!session) continue;
    if (line.event === 'session.started') started.add(session);
    else if (line.event === 'session.parked') parked.set(session, (parked.get(session) ?? 0) + 1);
  }
  const asks = Array.isArray(records.asks)
    ? new Map(records.asks.filter((ask) => record(ask) && named(ask.id)).map((ask) => [ask.id, ask]))
    : null;

  const runsOf = new Map();
  for (const run of records.sessions) {
    if (!record(run) || !named(run.id) || !named(run.quest)) continue;
    runsOf.set(run.quest, [...(runsOf.get(run.quest) ?? []), run]);
  }
  const opened = (run) => momentOf(run.created) ?? Number.NEGATIVE_INFINITY;
  const inPeriod = (time) => time !== null && time >= from.getTime() && time <= to.getTime();

  const facts = { started, parked, asks, asksUnread: records.asksUnread };
  const touched = [];
  for (const quest of records.quests) {
    if (!record(quest) || !named(quest.id)) continue;
    const runs = [...(runsOf.get(quest.id) ?? [])].sort((a, b) => opened(a) - opened(b) || a.id.localeCompare(b.id));
    const moments = [quest.filed, quest.updated, quest.accepted, ...runs.flatMap((run) => [run.created, run.updated])];
    if (!moments.map(momentOf).some(inPeriod)) continue;
    touched.push({ filed: momentOf(quest.filed) ?? Number.NEGATIVE_INFINITY, outcome: outcomeOf(quest, runs, facts) });
  }
  const quests = touched
    .sort((a, b) => a.filed - b.filed || a.outcome.quest.localeCompare(b.outcome.quest))
    .map((each) => each.outcome);

  const byRepository = rollUp(quests, (outcome) => ({ id: outcome.repository, fields: { repository: outcome.repository } }))
    .sort((a, b) => b.quests - a.quests || a.repository.localeCompare(b.repository));
  const byAgent = rollUp(quests, (outcome) => {
    const first = outcome.sessions.find((session) => session.ending !== 'stood-down');
    const agent = first ? first.adapter ?? '(unnamed)' : NO_SESSION;
    const account = first ? first.account : null;
    return { id: `${agent}\u0000${account}`, fields: { agent, account } };
  }).sort((a, b) => b.quests - a.quests
    || Number(a.agent === NO_SESSION) - Number(b.agent === NO_SESSION)
    || a.agent.localeCompare(b.agent)
    || (a.account ?? '').localeCompare(b.account ?? ''));

  return { read: true, unread: null, quests, byRepository, byAgent };
}

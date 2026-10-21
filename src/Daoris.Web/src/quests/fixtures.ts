import type { Quest, Session } from '../data/api';
import type { Consideration, TrustHold } from '../data/signals';

// Quests in each state a real machine reaches (FRAME1d), for the stories and the molecule tests alike — one set, so
// what a reviewer designs against is what the tests assert against.

const daysAgo = (count: number) => new Date(Date.now() - count * 86_400_000).toISOString();
const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();

/** Open, a day old, carrying a link and two files, one kept here and one elsewhere. */
export const OPEN: Quest = {
  id: 'abc123', from: 'game', to: 'engine', status: 'Open',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap; today hydration is unbounded.\n\nEvidence: seams whenever more than three chunks hydrate in one frame.',
  filed: daysAgo(1), updated: daysAgo(1),
  links: ['https://tickets.example/T-1'],
  attachments: [
    { name: 'before.png', sha256: `ab12cd34ef56${'0'.repeat(52)}`, bytes: 2_048 },
    { name: 'trace.log', sha256: `cd34${'1'.repeat(60)}`, bytes: 300 },
  ],
};

/** Taken by the engine's agent, and moved since it was filed. */
export const TAKEN: Quest = { ...OPEN, id: 'def456', status: 'Taken', title: 'Read the media field names from config', updated: hoursAgo(3), links: [], attachments: [] };

/** Open for nine days: a week of silence wears its mark. */
export const SITTING: Quest = { ...OPEN, id: 'aa11bb', title: 'Document the save format', filed: daysAgo(9), updated: daysAgo(9), links: [], attachments: [] };

/** Taken, its taker waiting on another repository's answer (D79). */
export const QUESTION: Quest = {
  id: 'q2q2q2', from: 'engine', to: 'backend', status: 'Open', title: 'What does the notes endpoint take?',
  body: 'We need the contract before the client can be written.', filed: hoursAgo(5), updated: hoursAgo(5),
};
export const WAITING: Quest = { ...TAKEN, id: 'w4w4w4', title: 'Sync the notes to the server', awaits: QUESTION.id };

/** A step of a chain (D65 §4): it follows one quest, and its close publishes the next. */
export const CHAINED: Quest = {
  ...OPEN, id: 'c7c7c7', title: 'Verify the streaming cap in a playtest', parent: 'f0f0f0', links: [], attachments: [],
  then: [{ to: 'game', title: 'Report on {parent}', body: 'Say what was done.' }],
};

/** A move that reached the remote second (D68 §5), kept for a person. */
export const CONFLICTED: Quest = {
  ...TAKEN, id: 'cf0cf0',
  conflicts: [{ machine: 'b7f2c9d1', sequence: 12, attempted: 'Taken', note: 'machine b, offline', at: hoursAgo(2) }],
};

/** Addressed to two of the engine's lanes (D115 §2.2). */
export const LANED: Quest = { ...OPEN, id: 'la1la1', title: 'Cap the asset pipeline\'s memory', lanes: ['assets', 'core'], links: [], attachments: [] };

export const DONE: Quest = { ...OPEN, id: 'd0d0d0', status: 'Done', title: 'Bump the engine to the new allocator', updated: hoursAgo(20), links: [], attachments: [] };
export const DECLINED: Quest = {
  ...OPEN, id: 'de0de0', status: 'Declined', title: 'Rewrite the renderer in another language', updated: hoursAgo(30),
  note: 'Not ours: the renderer is the platform team\'s, and they have a plan of their own.', links: [], attachments: [],
};

/** Nobody has started on it, so the service says it may go (D95). */
export const DELETABLE: Quest = { ...OPEN, deletable: true };

/** What the person required (DRIFT1c, D133 §3): their words, each with the check that proves them. */
const REQUIRED: NonNullable<Quest['requirements']> = [
  { quote: 'use the v3 bridge', check: 'the tiles arrive through the v3 bridge, as its trace shows' },
  { quote: 'keep the budget under the ceiling the engine sets', check: 'no frame hydrates more than the ceiling in a playtest' },
];

/** Taken, with what the person required and no answer yet: its done answers each. */
export const REQUIRING: Quest = { ...TAKEN, id: 'rq0rq0', requirements: REQUIRED };

/** Done, each requirement met (DRIFT1d, D133 §4), with how its check was met in the done's words. */
export const MET: Quest = {
  ...DONE, id: 'me0me0', title: 'Stream the tiles over the v3 bridge', requirements: REQUIRED,
  answers: [
    { requirement: 1, met: 'the tiles go through the v3 bridge; the trace in the closing note shows each hop' },
    { requirement: 2, met: 'a playtest held every frame under the ceiling' },
  ],
};

/** The departure's reason, in the done's words. */
export const DEPARTED_WHY = 'a cold cache cannot hold the ceiling on its first frame, so that frame may hydrate twice the ceiling';

/** Done, departing from its second requirement with the person's words it turns on, held for their yes, holding its next step. */
export const HELD: Quest = {
  ...MET, id: 'he0he0', title: 'Stream the tiles from the cold cache',
  answers: [
    MET.answers![0],
    { requirement: 2, departed: DEPARTED_WHY, quote: 'keep the budget under the ceiling the engine sets' },
  ],
  held: true,
  then: [{ to: 'game', title: 'Verify {parent} in a playtest', body: 'Say whether the first frame still seams.' }],
};

/** The same departure, accepted an hour ago: what it held went on. */
export const ACCEPTED: Quest = { ...HELD, id: 'ac0ac0', held: false, accepted: hoursAgo(1), then: [] };

/** Held, in 中文: the person's words and the done's as they were written, the chrome in the window's language. */
export const HELD_CJK: Quest = {
  ...HELD, id: 'hz0hz0', from: '游戏', to: '引擎', title: '冷缓存时按帧流式加载瓦片',
  requirements: [
    { quote: '用 v3 桥接', check: '瓦片经由 v3 桥接到达，追踪记录可证' },
    { quote: '每帧的加载量不能超过引擎设定的上限', check: '试玩中没有任何一帧超过上限' },
  ],
  answers: [
    { requirement: 1, met: '瓦片经由 v3 桥接传输，完成说明里的追踪记录列出了每一跳' },
    { requirement: 2, departed: '冷缓存的第一帧做不到不超过上限，所以第一帧允许加载上限的两倍', quote: '每帧的加载量不能超过引擎设定的上限' },
  ],
  then: [{ to: '游戏', title: '在试玩中验证 {parent}', body: '说明第一帧是否仍有接缝。' }],
};

/** A title in 中文, long enough to try the row's one line and the page's title. */
export const CJK: Quest = {
  ...OPEN, id: 'cj1cj1', from: '游戏', to: '引擎', title: '为分块接口公开每帧的流式加载预算，并在冷缓存时限制水合量',
  body: '世界流式加载需要每帧上限；目前水合量没有限制。', links: [], attachments: [],
};

/**
 * The install's re-filed quest (UX7c, D152 §0): its title is the note its ask put on its first line, and its body opens on
 * the same line. Nobody gave it a short title, so the service named it from its words (SESSUX1j).
 */
export const REFILED: Quest = {
  ...OPEN, id: 'e67690366b56', from: 'ask #f6d947', links: [], attachments: [],
  title: '(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)',
  body: '(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)\n'
    + 'continue the prod half of the ticket (the owner, 2026-10-04: the dev half is done, carry on in production)\n'
    + 'Where it stands: the dev half is done and landed on feature/report-dev.\n\n— asked at workspace `work` (ask `#f6d947`).',
  filed: hoursAgo(4), updated: hoursAgo(4),
  // The service's own name for it, read from its words past the note line (SESSUX1j).
  short: 'continue the prod half of the ticket…',
};

/** The same quest as an older host answers it, with no short title: its title names it. */
export const REFILED_UNNAMED: Quest = { ...REFILED, short: undefined };

/** The same quest named by its intake's short title, its whole title said once under the head. */
export const REFILED_NAMED: Quest = { ...REFILED, short: 'TK-2203 continue the production half' };

/** A session starting for the re-filed quest, as the install's list said while its page said nothing of it. */
export const STARTING: Session = {
  id: 'c2b8d293', quest: REFILED.id, repository: 'engine', adapter: 'claude-code-acp', harnessVersion: '0.84.0',
  profile: 'account-1', state: 'starting', created: hoursAgo(0), updated: hoursAgo(0),
};

/** The driven session working the open quest, and a failed attempt before it. */
export const WORKING: Session = {
  id: 's1a2b3c4', quest: OPEN.id, repository: 'engine', adapter: 'claude-code', harnessVersion: '2.1.4', state: 'working',
  note: 'the process is alive', evidence: 'commits landed:\nfff000 engine: cap hydration per frame',
  created: hoursAgo(2), updated: hoursAgo(1),
};
export const FAILED: Session = { ...WORKING, id: 's0f1r2s3', state: 'failed', note: 'the first attempt died', evidence: undefined, created: hoursAgo(4), updated: hoursAgo(3) };

/**
 * The install's case (QUESTCLOSE1, 2026-10-08): a taken quest's driven session the person finished at a checkpoint, its
 * record completed by them and the quest still taken, with nothing here to carry it on.
 */
export const FINISHED_HERE: Session = {
  id: 'f1n15h3d', quest: TAKEN.id, repository: 'engine', adapter: 'claude-code', state: 'completed', kind: 'driven',
  note: 'The person finished this at a checkpoint.',
  noteParts: [{ code: 'stopped.checkpoint-finished', text: 'The person finished this at a checkpoint.' }],
  created: hoursAgo(3), updated: hoursAgo(1),
};

/** Why the driver leaves a quest waiting, as its last tick said (D46 §3). */
export const HELD_BY_PERSON: Consideration = { quest: OPEN.id, repository: 'engine', verdict: 'Held', reason: '`engine` is held by the person.' };
export const EXHAUSTED: Consideration = {
  quest: OPEN.id, repository: 'engine', verdict: 'Exhausted',
  reason: '3 sessions failed on this quest, so it is parked. `daoris driver retry abc123` starts it again.',
};
/**
 * Taken on another machine after this machine's session on it was cut off (CARRY2b), naming whose take it is as the tick's
 * facts (CARRY2c): the machine and its session, and the session here that is not carried on.
 */
export const TAKEN_ELSEWHERE: Consideration = {
  quest: TAKEN.id, repository: 'engine', verdict: 'TakenElsewhere',
  takenBy: { machine: 'alice-laptop', session: 'alice-laptop/ab12cd34', here: false, last: 's0f1r2s3' },
  reason: 'Quest `#def456` is taken on `alice-laptop`, by session `alice-laptop/ab12cd34`: the take is theirs, so session `s0f1r2s3` is not carried on over it.',
};

/** Held by the person's stop (SESSUX1b), naming the session the tick says holds it (SESSUX1d). */
export const STOPPED: Consideration = {
  quest: OPEN.id, repository: 'engine', verdict: 'Stopped', heldBy: 's1a2b3c4',
  reason: 'you stopped session `s1a2b3c4`; Try again starts it again — `daoris driver retry abc123 --session s1a2b3c4`.',
};

/**
 * Waits for an account (TOOL4g, D125 §4): held at spawn because the only account its start may use is cooling until the
 * reset the agent named. Not parked, so no *Try again*; it starts by itself then.
 */
export const WAITS_FOR_ACCOUNT: Consideration = {
  quest: OPEN.id, repository: 'engine', verdict: 'Blocked',
  reason: 'the `claude-code` account `account-1` is cooling until Oct 3, 16:02 (Etc/UTC), as the agent said. Daoris starts nothing on it until then.',
  waitsFor: { agent: 'claude-code', account: 'account-1', until: '2026-10-03T16:02:00Z', stated: true },
};

/** Paused with its ask (PAUSE1b, D132 §2.3): the tick names the ask whose pause holds it, and *Resume* is on the ask. */
export const PAUSED_WITH_ASK: Consideration = {
  quest: OPEN.id, repository: 'engine', verdict: 'Paused', pausedBy: { scope: 'ask', id: 'a1b2c3' },
  reason: 'paused with ask `#a1b2c3`; Resume starts it — `daoris-driver ask --resume a1b2c3`.',
};

/** Paused on its own: *Resume* stands where *Try again* would. */
export const PAUSED_ITSELF: Consideration = {
  quest: OPEN.id, repository: 'engine', verdict: 'Paused', pausedBy: { scope: 'quest', id: OPEN.id },
  reason: 'you paused `#abc123`; Resume starts it — `daoris-driver quest resume abc123`.',
};

/** A question its asker's pause holds: the quest whose pause it is, a door away. */
export const PAUSED_WITH_QUEST: Consideration = {
  quest: OPEN.id, repository: 'engine', verdict: 'Paused', pausedBy: { scope: 'quest', id: 'def456' },
  reason: 'you paused `#def456`; Resume starts it — `daoris-driver quest resume def456`.',
};

/** The submission a closure graph's quest stands at (FABRIC-CLOSE1a): its agent's `ready`, two hours ago. */
export const SUBMISSION = { machine: 'desk', sequence: 41, at: hoursAgo(2), note: 'ready' };

/**
 * Taken in a closure graph and submitted (FABRIC-CLOSE1d, §E5): its submission departs from what the person required, so it is
 * held for their yes, as a done would be. It reads *Submitted*, then the hold, and is done only at the graph's close.
 */
export const SUBMITTED: Quest = { ...HELD, id: 'su0su0', status: 'Taken', closure: 'gph-1', submission: SUBMISSION, hold: 'departed' };

/** Submitted and unheld: its chain's next step was published from that submission, which it seals (FABRIC-CLOSE1c). */
export const SEALED: Quest = {
  ...TAKEN, id: 'se0se0', title: 'Stream the tiles over the v3 bridge', closure: 'gph-1', submission: SUBMISSION, held: false,
};

/** The step its submission published, standing on it (FABRIC-CLOSE1c's `grewFrom`). */
export const STANDING: Quest = {
  ...OPEN, id: 'st0st0', title: 'Verify the tiles in a playtest', parent: SEALED.id, grewFrom: { machine: 'desk', sequence: 41 },
  links: [], attachments: [],
};

/** That step taken and started, then held because the person reopened its source (FABRIC-CLOSE1c, `source-reopened`). */
export const REOPENED: Quest = {
  ...STANDING, id: 'ro0ro0', status: 'Taken', sourceReopened: { machine: 'desk', sequence: 41 }, held: true, hold: 'source-reopened',
};

/** The same step reopened before anyone took it: withdrawn, taken by nobody until the source is ready again. */
export const WITHDRAWN: Quest = { ...REOPENED, id: 'wd0wd0', status: 'Open' };

/**
 * The source the person reopened (FABRIC-CLOSE1c4): still taken, its submission cleared, and the reopen it answers until its
 * next `ready`, with their words and the session that said the reopened one (FABRIC-CLOSE1d3).
 */
export const SOURCE_REOPENED: Quest = {
  ...SEALED, id: 'sr0sr0', submission: null,
  reopened: {
    machine: 'desk', sequence: 44, of: { machine: 'desk', sequence: 41 }, note: 'The cold cache still seams.', at: hoursAgo(1),
    session: 's-7f3a21',
  },
};

// SYNCMERGE1c2 (D178 points 2, 7 and 8): two machines' work on one step, read from where this machine (`desk`) stands. The quest
// answer names this machine (`here`), the machine holding the step while it is taken (`holder`), and how each settled conflict ended.

/** This machine's id, as a local host answers it to its own page. */
export const HERE = 'desk';
/** Another machine of the family. */
export const THERE = 'b7f2c9d1';
/** This machine's take, lost: the conflict every state below starts from. */
const MY_TAKE = { machine: HERE, sequence: 7, attempted: 'Taken' as const, note: 'desk, on the train', at: hoursAgo(3) };
/** The fresh take a bring-forward made, by its machine and sequence. */
const FRESH = { machine: HERE, sequence: 9, at: hoursAgo(1) };

/** This machine's take lost, and the step open again: both choices stand. */
export const MINE_LOST: Quest = { ...STANDING, id: 'ml0ml0', here: HERE, conflicts: [MY_TAKE] };

/** Another machine's take lost on a step open here: only what was true before, its dismissal. */
export const THEIRS_LOST: Quest = {
  ...STANDING, id: 'tl0tl0', here: HERE, conflicts: [{ ...MY_TAKE, machine: THERE, sequence: 12, note: 'machine b, offline' }],
};

/** This machine's take lost to the person's reopen of the work it grew from, and another machine's beside it. */
export const LOST_TO_REOPEN: Quest = {
  ...STANDING, id: 'lr0lr0', here: HERE,
  conflicts: [{ ...MY_TAKE, lostToTheReopen: true }, { ...MY_TAKE, machine: THERE, sequence: 12, note: null, lostToTheReopen: true }],
};

/** This machine's take lost, and another machine holds the step: no bring-forward while it does. */
export const HELD_ELSEWHERE: Quest = { ...MINE_LOST, id: 'he0he0', status: 'Taken', holder: THERE };

/** This machine's take lost, and this machine holds the step again by a later take: nothing is left to bring forward. */
export const HELD_HERE: Quest = { ...MINE_LOST, id: 'hh0hh0', status: 'Taken', holder: HERE };

/** This machine's take lost on a step since closed. */
export const LOST_CLOSED: Quest = { ...MINE_LOST, id: 'lc0lc0', status: 'Done' };

/** This machine's take lost on a step its source's reopen withdrew: the bring-forward waits for the source. */
export const LOST_WITHDRAWN: Quest = { ...WITHDRAWN, id: 'lw0lw0', here: HERE, conflicts: [MY_TAKE] };

/**
 * Brought forward and lost again: another machine's take reached the remote first, so the fresh take is a conflict of its own (a
 * second loss) and the first stays unresolved, naming the bring-forward that lost.
 */
export const LOST_AGAIN: Quest = {
  ...MINE_LOST, id: 'la0la0', status: 'Taken', holder: THERE,
  conflicts: [{ ...MY_TAKE, forward: { ...FRESH, standing: 'lost' } }, { ...MY_TAKE, ...FRESH, note: null }],
};

/** This machine's take, dropped: settled, and nothing waits. */
export const SETTLED_DROPPED: Quest = { ...MINE_LOST, id: 'sd0sd0', conflicts: [], settled: [{ ...MY_TAKE, ended: 'dropped' }] };

/** Brought forward, and the remote confirmed the fresh take: this machine holds the step again. */
export const BROUGHT_FORWARD: Quest = {
  ...MINE_LOST, id: 'bf0bf0', status: 'Taken', holder: HERE, conflicts: [],
  settled: [{ ...MY_TAKE, ended: 'brought-forward', forward: { ...FRESH, standing: 'held' } }],
};

/** Brought forward offline: the fresh take stands here unconfirmed until a sync reaches the remote, never said as settled. */
export const BROUGHT_FORWARD_UNCONFIRMED: Quest = {
  ...BROUGHT_FORWARD, id: 'bu0bu0',
  settled: [{ ...MY_TAKE, ended: 'brought-forward', forward: { ...FRESH, standing: 'unconfirmed' } }],
};

/** Brought forward, but another machine's dismissal of the same conflict reached the remote first: dropped, the bring-forward lost. */
export const DROPPED_FIRST: Quest = {
  ...MINE_LOST, id: 'df0df0', conflicts: [{ ...MY_TAKE, ...FRESH, note: null }],
  settled: [{ ...MY_TAKE, ended: 'dropped', forward: { ...FRESH, standing: 'lost' } }],
};

/** A step its source's reopen held after this machine's take landed: its go-on and abandon are this machine's two choices. */
export const REOPENED_HERE: Quest = { ...REOPENED, id: 'rh0rh0', here: HERE, holder: HERE };

/** A start the driver holds for the agent's trust (D73). */
export const TRUST: TrustHold = {
  folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json', quest: OPEN.id,
};

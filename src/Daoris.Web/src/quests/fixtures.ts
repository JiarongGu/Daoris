import type { Quest, Session } from '../api';
import type { Consideration, TrustHold } from '../signals';

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
export const REFILED_NAMED: Quest = { ...REFILED, short: 'AR-2203 continue the production half' };

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

/** A start the driver holds for the agent's trust (D73). */
export const TRUST: TrustHold = {
  folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json', quest: OPEN.id,
};

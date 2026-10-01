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

/** A title in 中文, long enough to try the row's one line and the page's title. */
export const CJK: Quest = {
  ...OPEN, id: 'cj1cj1', from: '游戏', to: '引擎', title: '为分块接口公开每帧的流式加载预算，并在冷缓存时限制水合量',
  body: '世界流式加载需要每帧上限；目前水合量没有限制。', links: [], attachments: [],
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

/** A start the driver holds for the agent's trust (D73). */
export const TRUST: TrustHold = {
  folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json', quest: OPEN.id,
};

import type { Consideration } from '../signals';
import type { AbandonAnswer, AbandonEntry, WorkPlan } from './pausing';

// The work of an ask and of a quest as `WORK_PLAN` answers them (PAUSE1e, D132): the stories' and the tests' one copy, every
// state a person meets on the ask's page, the quest's page and a session's header.

/** An ask's work in flight: one quest taken here and running, one open; a parked session; a tree with work only here. */
export const PAUSABLE_ASK: WorkPlan = {
  scope: 'ask', id: 'a1b2c3', pausable: true, paused: null,
  quests: [
    {
      quest: '9a8b7c', title: 'Cap chunk hydration per frame', to: 'engine', status: 'Taken', joined: 'asked', by: null,
      pause: 'paused', pausedBy: null, key: 'quest:9a8b7c', abandon: 'decline', kept: null, machine: null, whileOpen: false,
    },
    {
      quest: '5e4f3d', title: 'Expose the streaming budget to the HUD', to: 'game', status: 'Open', joined: 'asked', by: null,
      pause: 'paused', pausedBy: null, key: 'quest:5e4f3d', abandon: 'decline', kept: null, machine: null, whileOpen: true,
    },
  ],
  sessions: [
    {
      session: 's1a2b3c4', repository: 'engine', state: 'working', quest: '9a8b7c', intake: false, teammate: false,
      branch: 'daoris/s-1a2b3c4d', pause: 'stopped', key: 'session:s1a2b3c4', abandon: 'stop', archive: true, kept: null, machine: null,
    },
    {
      session: 'p4rk3d00', repository: 'game', state: 'awaiting-person', quest: '5e4f3d', intake: false, teammate: false,
      branch: null, pause: 'parked', key: 'session:p4rk3d00', abandon: 'end', archive: true, kept: null, machine: null,
    },
  ],
  trees: [
    {
      repository: 'engine', branch: 'daoris/s-1a2b3c4d', sessions: ['s1a2b3c4'], key: 'tree:engine:daoris/s-1a2b3c4d',
      abandon: 'discard', kept: null, gone: false, tip: '9f3e2a1', commits: 3, uncommitted: 7,
      files: ['src/stream/hydrate.ts', 'src/stream/budget.ts', 'src/stream/frame.ts', 'docs/streaming.md', 'test/hydrate.test.ts'],
      where: null,
    },
  ],
  landings: [],
  abandon: {
    abandonable: true,
    pieces: ['quest:9a8b7c', 'quest:5e4f3d', 'session:s1a2b3c4', 'session:p4rk3d00', 'tree:engine:daoris/s-1a2b3c4d', 'ask:a1b2c3'],
    closes: 'a1b2c3',
    abandoned: null,
  },
};

/** The same ask paused here: its running session stopped by the pause, and nothing of it starting until *Resume*. */
export const PAUSED_ASK: WorkPlan = {
  ...PAUSABLE_ASK,
  paused: { at: '2026-10-03T09:12:00Z', stopped: [{ quest: '9a8b7c', session: 's1a2b3c4' }] },
  quests: PAUSABLE_ASK.quests.map((quest) => ({ ...quest, pausedBy: { scope: 'ask', id: 'a1b2c3' } })),
  sessions: PAUSABLE_ASK.sessions.map((session) => (session.pause === 'stopped' ? { ...session, state: 'stopped', pause: 'ended' } : session)),
};

/** An ask's work some of which is not Daoris's here: a quest taken on another machine, one done, a tree whose commits are on `main`. */
export const MIXED_ASK: WorkPlan = {
  ...PAUSABLE_ASK,
  quests: [
    ...PAUSABLE_ASK.quests,
    {
      quest: '0c1d2e', title: 'Profile the cold-cache path', to: 'lantern', status: 'Taken', joined: 'published', by: 's1a2b3c4',
      pause: 'elsewhere', pausedBy: null, key: 'quest:0c1d2e', abandon: 'keep', kept: 'taken-elsewhere', machine: 'studio-pc', whileOpen: false,
    },
    {
      quest: '7f6e5d', title: 'Write the streaming budget down', to: 'engine', status: 'Done', joined: 'asked', by: null,
      pause: 'closed', pausedBy: null, key: 'quest:7f6e5d', abandon: 'keep', kept: 'done', machine: null, whileOpen: false,
    },
  ],
  trees: [
    ...PAUSABLE_ASK.trees,
    {
      repository: 'game', branch: 'daoris/s-5e6f7a8b', sessions: ['e4d3c2b1'], key: 'tree:game:daoris/s-5e6f7a8b',
      abandon: 'keep', kept: 'elsewhere', gone: false, tip: 'b2c3d4e', commits: 2, uncommitted: 0, files: [], where: 'main',
    },
  ],
};

/** An ask whose work has nothing left to take here: everything kept, so *Abandon…* is not offered. */
export const SPENT_ASK: WorkPlan = {
  ...MIXED_ASK,
  pausable: false,
  quests: MIXED_ASK.quests.filter((quest) => quest.abandon === 'keep'),
  sessions: [],
  trees: MIXED_ASK.trees.filter((tree) => tree.abandon === 'keep'),
  abandon: { abandonable: false, pieces: [], closes: 'a1b2c3', abandoned: null },
};

/** This machine's record of the ask's abandon (`abandoned.json`), read on a later look. */
export const ABANDONED_ENTRY: AbandonEntry = {
  at: '2026-10-03T10:41:00Z', door: 'screen', reason: 'The streaming work went the wrong way; we are taking another approach.',
  declined: ['9a8b7c', '5e4f3d'], closed: true,
  trees: [{ repository: 'engine', branch: 'daoris/s-1a2b3c4d', tip: '9f3e2a1', commits: 3, uncommitted: 7, sessions: ['s1a2b3c4'], alone: false }],
  stopped: ['s1a2b3c4', 'p4rk3d00'], archived: ['s1a2b3c4', 'p4rk3d00'],
  stayed: [
    { piece: 'quest:0c1d2e', why: 'taken-elsewhere', machine: 'studio-pc' },
    { piece: 'quest:7f6e5d', why: 'done' },
    { piece: 'tree:game:daoris/s-5e6f7a8b', why: 'elsewhere', where: 'main' },
  ],
  declines: [{ quest: '9a8b7c', answer: 'confirmed' }, { quest: '5e4f3d', answer: 'confirmed' }],
};

/** The ask abandoned: closed with the reason, and its record read back. */
export const ABANDONED_ASK: WorkPlan = {
  ...SPENT_ASK,
  abandon: { abandonable: false, pieces: [], closes: null, abandoned: ABANDONED_ENTRY },
};

/** The second press's answer, said at once: one piece changed since the list, and a decline the remote lost. */
export const ABANDON_ANSWER: AbandonAnswer = {
  scope: 'ask', id: 'a1b2c3', did: 'abandoned', listed: 6, went: 5,
  changed: [{ piece: 'session:p4rk3d00', why: 'review', changed: true }],
  failed: [], joined: [],
  declined: ['9a8b7c', '5e4f3d'], closed: true,
  stopped: [{ session: 's1a2b3c4', quest: '9a8b7c' }],
  discarded: [{ repository: 'engine', branch: 'daoris/s-1a2b3c4d', tip: '9f3e2a1', commits: 3, uncommitted: 7, sessions: ['s1a2b3c4'], alone: false }],
  archived: ['s1a2b3c4'],
  stayed: ABANDONED_ENTRY.stayed,
  declines: [{ quest: '9a8b7c', answer: 'confirmed' }, { quest: '5e4f3d', answer: 'lost' }],
  stillPaused: false,
};

/** One quest's work: taken here, its session running in its own tree. */
export const PAUSABLE_QUEST: WorkPlan = {
  ...PAUSABLE_ASK,
  scope: 'quest', id: '9a8b7c',
  quests: [{ ...PAUSABLE_ASK.quests[0], joined: 'named' }],
  sessions: [PAUSABLE_ASK.sessions[0]],
  abandon: { abandonable: true, pieces: ['quest:9a8b7c', 'session:s1a2b3c4', 'tree:engine:daoris/s-1a2b3c4d'], closes: null, abandoned: null },
};

/**
 * An ask's work as its page lists it (PAUSE1h, D132 §7.1): a quest whose first session failed and whose second asked two
 * questions of other repositories, one a teammate works on another machine and one answered; a quest taken here, its
 * session working; one open in a held repository; and one its person paused on its own, its session stopped by that pause.
 * Its intake is listed by the plan and named in the page's section of its own.
 */
export const LISTED_ASK: WorkPlan = {
  scope: 'ask', id: 'a1b2c3', pausable: true, paused: null,
  quests: [
    {
      quest: '9a8b7c', title: 'Cap chunk hydration per frame', to: 'engine', status: 'Taken', joined: 'asked', by: null,
      pause: 'paused', pausedBy: null, key: 'quest:9a8b7c', abandon: 'decline', kept: null, machine: null, whileOpen: false,
    },
    {
      quest: '5e4f3d', title: 'Expose the streaming budget to the HUD', to: 'game', status: 'Taken', joined: 'asked', by: null,
      pause: 'paused', pausedBy: null, key: 'quest:5e4f3d', abandon: 'decline', kept: null, machine: null, whileOpen: false,
    },
    {
      quest: '2d3e4f', title: 'Document the budget in the HUD guide', to: 'game', status: 'Open', joined: 'asked', by: null,
      pause: 'paused', pausedBy: null, key: 'quest:2d3e4f', abandon: 'decline', kept: null, machine: null, whileOpen: true,
    },
    {
      quest: '3c2b1a', title: 'Tune the frame-time histogram buckets', to: 'engine', status: 'Taken', joined: 'asked', by: null,
      pause: 'paused', pausedBy: { scope: 'quest', id: '3c2b1a' }, key: 'quest:3c2b1a', abandon: 'decline', kept: null,
      machine: null, whileOpen: false,
    },
    {
      quest: '0c1d2e', title: 'Profile the cold-cache path', to: 'lantern', status: 'Taken', joined: 'published', by: 's1a2b3c4',
      pause: 'elsewhere', pausedBy: null, key: 'quest:0c1d2e', abandon: 'keep', kept: 'taken-elsewhere', machine: 'studio-pc',
      whileOpen: false,
    },
    {
      quest: '7f6e5d', title: 'Which chunk sizes does the HUD assume?', to: 'game', status: 'Done', joined: 'published', by: 's1a2b3c4',
      pause: 'closed', pausedBy: null, key: 'quest:7f6e5d', abandon: 'keep', kept: 'done', machine: null, whileOpen: false,
    },
  ],
  sessions: [
    {
      session: 'f41led00', repository: 'engine', state: 'failed', quest: '9a8b7c', intake: false, teammate: false,
      branch: 'daoris/s-f41led00', pause: 'ended', key: 'session:f41led00', abandon: 'archive', archive: true, kept: null, machine: null,
    },
    {
      session: 's1a2b3c4', repository: 'engine', state: 'completed', quest: '9a8b7c', intake: false, teammate: false,
      branch: 'daoris/s-1a2b3c4d', pause: 'ended', key: 'session:s1a2b3c4', abandon: 'archive', archive: true, kept: null, machine: null,
    },
    {
      session: 'w0rk1ng0', repository: 'game', state: 'working', quest: '5e4f3d', intake: false, teammate: false,
      branch: 'daoris/s-w0rk1ng0', pause: 'stopped', key: 'session:w0rk1ng0', abandon: 'stop', archive: true, kept: null, machine: null,
    },
    {
      session: 'st0pp3d0', repository: 'engine', state: 'stopped', quest: '3c2b1a', intake: false, teammate: false,
      branch: 'daoris/s-st0pp3d0', pause: 'ended', key: 'session:st0pp3d0', abandon: 'archive', archive: true, kept: null, machine: null,
    },
    {
      session: 'studio/t3amm8t0', repository: 'lantern', state: 'working', quest: '0c1d2e', intake: false, teammate: true,
      branch: null, pause: 'teammate', key: 'session:studio/t3amm8t0', abandon: 'keep', archive: false, kept: 'teammate',
      machine: 'studio-pc',
    },
    {
      session: 'a5w3r3d0', repository: 'game', state: 'completed', quest: '7f6e5d', intake: false, teammate: false,
      branch: null, pause: 'ended', key: 'session:a5w3r3d0', abandon: 'archive', archive: true, kept: null, machine: null,
    },
    {
      session: 'i9n8t7k6', repository: 'ask #a1b2c3', state: 'completed', quest: null, intake: true, teammate: false,
      branch: null, pause: 'ended', key: 'session:i9n8t7k6', abandon: 'archive', archive: true, kept: null, machine: null,
    },
  ],
  trees: PAUSABLE_ASK.trees,
  landings: [],
  abandon: {
    abandonable: true,
    pieces: [
      'quest:9a8b7c', 'quest:5e4f3d', 'quest:2d3e4f', 'quest:3c2b1a', 'session:f41led00', 'session:s1a2b3c4', 'session:w0rk1ng0',
      'session:st0pp3d0', 'session:a5w3r3d0', 'session:i9n8t7k6', 'tree:engine:daoris/s-1a2b3c4d', 'ask:a1b2c3',
    ],
    closes: 'a1b2c3',
    abandoned: null,
  },
};

/**
 * Why the driver's last look left each quest of `LISTED_ASK` sitting (the tick's `considered`): the first waits on its question,
 * the open one's repository is held, and the one paused on its own says so. The two whose sessions ended or run it has
 * nothing to say of.
 */
export const LISTED_CONSIDERED: Consideration[] = [
  {
    quest: '9a8b7c', repository: 'engine', verdict: 'Waiting',
    reason: 'waits on `#0c1d2e`, asked of `lantern` — it resumes, in the same tree, once that is answered.',
  },
  { quest: '2d3e4f', repository: 'game', verdict: 'Held', reason: '`game` is held by the person.' },
  {
    quest: '3c2b1a', repository: 'engine', verdict: 'Paused', pausedBy: { scope: 'quest', id: '3c2b1a' },
    reason: 'you paused `#3c2b1a`; Resume carries it on — `daoris-driver quest resume 3c2b1a`.',
  },
];

/** `LISTED_ASK` paused here: its working session stopped by the pause, and every quest of it held here by the ask's pause. */
export const PAUSED_LISTED_ASK: WorkPlan = {
  ...LISTED_ASK,
  paused: { at: '2026-10-03T09:12:00Z', stopped: [{ quest: '5e4f3d', session: 'w0rk1ng0' }] },
  quests: LISTED_ASK.quests.map((quest) => (quest.pausedBy ? quest : { ...quest, pausedBy: { scope: 'ask', id: 'a1b2c3' } })),
  sessions: LISTED_ASK.sessions.map((session) => (session.pause === 'stopped' ? { ...session, state: 'stopped', pause: 'ended' } : session)),
};

/** The tick's verdicts once the ask is paused: the pause comes before every other reason, and a quest's own before its ask's. */
export const PAUSED_LISTED_CONSIDERED: Consideration[] = [
  ...['9a8b7c', '5e4f3d', '2d3e4f'].map((quest) => ({
    quest, repository: quest === '9a8b7c' ? 'engine' : 'game', verdict: 'Paused', pausedBy: { scope: 'ask' as const, id: 'a1b2c3' },
    reason: `paused with ask \`#a1b2c3\`; Resume ${quest === '2d3e4f' ? 'starts' : 'carries'} it on — \`daoris-driver ask --resume a1b2c3\`.`,
  })),
  LISTED_CONSIDERED[2],
];

/** The quest paused on its own. */
export const PAUSED_QUEST: WorkPlan = {
  ...PAUSABLE_QUEST,
  paused: { at: '2026-10-03T09:20:00Z', stopped: [{ quest: '9a8b7c', session: 's1a2b3c4' }] },
  quests: PAUSABLE_QUEST.quests.map((quest) => ({ ...quest, pausedBy: { scope: 'quest', id: '9a8b7c' } })),
  sessions: PAUSABLE_QUEST.sessions.map((session) => ({ ...session, state: 'stopped', pause: 'ended' })),
};

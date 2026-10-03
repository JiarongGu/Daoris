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

/** The quest paused on its own. */
export const PAUSED_QUEST: WorkPlan = {
  ...PAUSABLE_QUEST,
  paused: { at: '2026-10-03T09:20:00Z', stopped: [{ quest: '9a8b7c', session: 's1a2b3c4' }] },
  quests: PAUSABLE_QUEST.quests.map((quest) => ({ ...quest, pausedBy: { scope: 'quest', id: '9a8b7c' } })),
  sessions: PAUSABLE_QUEST.sessions.map((session) => ({ ...session, state: 'stopped', pause: 'ended' })),
};

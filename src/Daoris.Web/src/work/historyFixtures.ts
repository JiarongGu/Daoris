import type { HistoryClearAnswer, HistoryPlan, HistoryReading, HistorySizes, HistoryUnit } from './history';

// What the driver's `HISTORY_PLAN` and `HISTORY_CLEAR` answer (HIST1c, D153; the history-clearing design §2.4, §5, §6.3),
// as the stories and the tests hand them to the clear's screens (HIST1e): a closed quest's work, its failed sessions, an
// ask's work, and a workspace's finished history with what it keeps and why. Ids, counts and bytes only, as the routes
// answer: never a path or a title.

const KB = 1024;
const MB = 1024 * KB;

/** What the home holds of something, by kind: here mostly conversations and transcripts, as a session's are. */
export const sizes = (total: number): HistorySizes => {
  const conversations = Math.round(total * 0.6);
  const transcripts = Math.round(total * 0.3);
  const files = total - conversations - transcripts;
  return { conversations, transcripts, files, kept: 0, other: 0, total };
};

/** One unit as both halves judged it: by default a closed quest's work that may go, holding nothing. */
export const unit = (kind: HistoryUnit['kind'], id: string, over: Partial<HistoryUnit> = {}): HistoryUnit => ({
  kind, id, workspace: 'aurora', clearable: true,
  quests: kind === 'quest' ? [id] : [], forgotten: [], asks: kind === 'ask' ? [id] : [], sessions: [], teammates: [],
  keep: null, kept: [], bytes: sizes(0),
  ...over,
});

/** A done quest with three sessions here, which may go: never numbered by a remote, so it simply goes. */
export const QUEST_PLAN: HistoryPlan = {
  scope: 'quest', id: '9a8b7c',
  units: [unit('quest', '9a8b7c', { sessions: ['s1a2b3c4', 's5e6f7a8', 's9b0c1d2'], bytes: sizes(Math.round(2.1 * MB)) })],
  reading: null,
};

/** The same on a wired workspace, numbered by its remote: forgotten here, the team's copy kept. */
export const QUEST_FORGOTTEN: HistoryPlan = {
  ...QUEST_PLAN,
  units: [{ ...QUEST_PLAN.units[0]!, forgotten: ['9a8b7c'] }],
};

/** A declined quest no session of this machine served: it goes alone. */
export const QUEST_ALONE: HistoryPlan = {
  scope: 'quest', id: '9a8b7c', units: [unit('quest', '9a8b7c', { bytes: sizes(12 * KB) })], reading: null,
};

/** A quest an ask asked: kept, since only its ask clears it. */
export const QUEST_ASKED: HistoryPlan = {
  scope: 'quest', id: '9a8b7c',
  units: [unit('quest', '9a8b7c', { clearable: false, keep: { code: 'HISTORY_ASKED', quest: '9a8b7c', ask: 'a1b2c3' } })],
  reading: null,
};

/** A closed quest whose session's tree is still here: kept. */
export const QUEST_TREE_HERE: HistoryPlan = {
  scope: 'quest', id: '9a8b7c',
  units: [unit('quest', '9a8b7c', {
    clearable: false, sessions: ['s1a2b3c4'], keep: { code: 'HISTORY_TREE_HERE', session: 's1a2b3c4' },
  })],
  reading: null,
};

/** Two failed sessions of this machine's, and a teammate's failed session the list names and keeps. */
export const FAILED_PLAN: HistoryPlan = {
  scope: 'failed', id: '9a8b7c',
  units: [unit('failed', '9a8b7c', {
    sessions: ['f1a2b3c4', 'f5e6f7a8'],
    kept: [{ code: 'HISTORY_NOT_OURS', session: 'laptop/f9e8d7c6', machine: 'laptop' }],
    bytes: sizes(Math.round(1.4 * MB)),
  })],
  reading: null,
};

/** A closed quest with no failed session here: nothing to clear, which is information and never a refusal. */
export const FAILED_NONE: HistoryPlan = { scope: 'failed', id: '9a8b7c', units: [unit('failed', '9a8b7c')], reading: null };

/** An ask whose two quests closed, with the five sessions that served them and its intake. */
export const ASK_PLAN: HistoryPlan = {
  scope: 'ask', id: 'a1b2c3',
  units: [unit('ask', 'a1b2c3', {
    quests: ['9a8b7c', '5e4f3d'], asks: ['a1b2c3'],
    sessions: ['i0n1t2k3', 's1a2b3c4', 's5e6f7a8', 's9b0c1d2', 'sd3e4f5a'],
    bytes: { conversations: Math.round(3.2 * MB), transcripts: Math.round(1.1 * MB), files: 220 * KB, kept: 48 * KB, other: 2 * KB, total: Math.round(4.6 * MB) },
  })],
  reading: null,
};

/** An ask a quest of whose work is still taken: kept. */
export const ASK_OPEN: HistoryPlan = {
  scope: 'ask', id: 'a1b2c3',
  units: [unit('ask', 'a1b2c3', { clearable: false, keep: { code: 'HISTORY_OPEN', context: 'taken', quest: '5e4f3d' } })],
  reading: null,
};

const GOING: HistoryUnit[] = [
  unit('ask', 'a1b2c3', { quests: ['9a8b7c', '5e4f3d'], asks: ['a1b2c3'], sessions: ['i0n1t2k3', 's1a2b3c4', 's5e6f7a8'], bytes: sizes(9 * MB) }),
  unit('quest', '0c1d2e', { sessions: ['s0c1d2e3', 's4f5a6b7'], forgotten: ['0c1d2e'], teammates: ['laptop/s7d8e9f0'], bytes: sizes(6 * MB) }),
  unit('quest', '3f4a5b', { sessions: ['s3f4a5b6'], bytes: sizes(Math.round(2.5 * MB)) }),
];

const STAYING: HistoryUnit[] = [
  unit('quest', '6c7d8e', {
    clearable: false, sessions: ['s6c7d8e9'],
    keep: { code: 'HISTORY_LANDING_STANDS', session: 's6c7d8e9', repository: 'engine', branch: 'feature/chunk-budget-6c7d8e' },
  }),
  unit('ask', 'b2c3d4', { clearable: false, keep: { code: 'HISTORY_NEEDS_YOU', context: 'ask', ask: 'b2c3d4' } }),
  unit('quest', '9f0a1b', { clearable: false, keep: { code: 'HISTORY_NEEDS_YOU', context: 'held', quest: '9f0a1b' } }),
  unit('quest', '2b3c4d', { clearable: false, keep: { code: 'HISTORY_LIVE', session: 's2b3c4d5' } }),
  unit('quest', '5d6e7f', { clearable: false, keep: { code: 'HISTORY_UNPUSHED', quest: '5d6e7f', workspace: 'aurora' } }),
];

const READING: HistoryReading = {
  workspace: 'aurora', quests: 11, asks: 2, sessions: 14, teammates: 1, bytes: sizes(Math.round(23.6 * MB)), intake: 0,
  takes: { quests: 4, asks: 1, sessions: 6, teammates: 1, bytes: Math.round(17.5 * MB) + Math.round(3.1 * MB) },
  keptBy: { HISTORY_LANDING_STANDS: 1, HISTORY_NEEDS_YOU: 2, HISTORY_LIVE: 1, HISTORY_UNPUSHED: 1 },
  conversations: { count: 3, bytes: Math.round(1.2 * MB) },
  leftOver: { count: 4, bytes: Math.round(3.1 * MB) },
  log: Math.round(2.4 * MB),
};

/** A workspace's finished history: three units go, five stay with their reasons, and the home's left-over files go too. */
export const WORKSPACE_PLAN: HistoryPlan = { scope: 'workspace', id: 'aurora', units: [...GOING, ...STAYING], reading: READING };

/** Nothing may go: every closed unit is kept, and nothing is left over. */
export const WORKSPACE_KEPT: HistoryPlan = {
  scope: 'workspace', id: 'aurora', units: STAYING,
  reading: {
    ...READING, quests: 4, asks: 1, sessions: 2, teammates: 0, bytes: sizes(Math.round(1.8 * MB)),
    takes: { quests: 0, asks: 0, sessions: 0, teammates: 0, bytes: 0 }, leftOver: { count: 0, bytes: 0 },
  },
};

/** A workspace with nothing finished, and nothing left over: the reading says so and offers no press. */
export const WORKSPACE_EMPTY: HistoryPlan = {
  scope: 'workspace', id: 'aurora', units: [],
  reading: {
    workspace: 'aurora', quests: 0, asks: 0, sessions: 0, teammates: 0, bytes: sizes(0), intake: 0,
    takes: { quests: 0, asks: 0, sessions: 0, teammates: 0, bytes: 0 }, keptBy: {},
    conversations: { count: 0, bytes: 0 }, leftOver: { count: 0, bytes: 0 }, log: 640 * KB,
  },
};

/**
 * A closed quest no session here served, whose record holds no file on this machine: a clear takes it and frees nothing on
 * the disk, so the reading says what goes rather than that nothing would (HIST1k).
 */
export const WORKSPACE_RECORDS_ONLY: HistoryPlan = {
  scope: 'workspace', id: 'aurora', units: [unit('quest', '7e8f9a')],
  reading: {
    ...WORKSPACE_EMPTY.reading!, quests: 1,
    takes: { quests: 1, asks: 0, sessions: 0, teammates: 0, bytes: 0 },
  },
};

/**
 * The same quest beside two left-over files that hold nothing (HIST1n): the reading has read records and 0 B, and says only
 * that, since files of 0 B go too and *What goes* names them.
 */
export const WORKSPACE_RECORDS_EMPTY_FILES: HistoryPlan = {
  ...WORKSPACE_RECORDS_ONLY,
  reading: { ...WORKSPACE_RECORDS_ONLY.reading!, leftOver: { count: 2, bytes: 0 } },
};

/** Only what records already gone left behind, and the intake's room once no ask is held: the press sends no unit. */
export const WORKSPACE_LEFT_OVER: HistoryPlan = {
  scope: 'workspace', id: 'aurora', units: [],
  reading: {
    ...WORKSPACE_EMPTY.reading!, intake: 40 * KB,
    takes: { quests: 0, asks: 0, sessions: 0, teammates: 0, bytes: Math.round(3.1 * MB) + 40 * KB },
    leftOver: { count: 4, bytes: Math.round(3.1 * MB) },
  },
};

/**
 * Only left-over files, as the install had them (HIST1o): no finished work may go and no intake's room is here, so the press
 * takes 1.3 KB of files no record holds, and its opening says only that.
 */
export const WORKSPACE_FILES_ONLY: HistoryPlan = {
  scope: 'workspace', id: 'aurora', units: [],
  reading: {
    ...WORKSPACE_EMPTY.reading!,
    takes: { quests: 0, asks: 0, sessions: 0, teammates: 0, bytes: Math.round(1.3 * KB) },
    leftOver: { count: 2, bytes: Math.round(1.3 * KB) },
  },
};

/** Only the intake's room, once no ask is held and nothing is left over: the press sends no unit. */
export const WORKSPACE_ROOM_ONLY: HistoryPlan = {
  scope: 'workspace', id: 'aurora', units: [],
  reading: {
    ...WORKSPACE_EMPTY.reading!, intake: 40 * KB,
    takes: { quests: 0, asks: 0, sessions: 0, teammates: 0, bytes: 40 * KB },
  },
};

/** The workspace's second press: two of the three went, one changed since the list and stayed. */
export const WORKSPACE_CLEARED: HistoryClearAnswer = {
  scope: 'workspace', id: 'aurora', listed: 3,
  cleared: [{ kind: 'ask', id: 'a1b2c3' }, { kind: 'quest', id: '3f4a5b' }],
  quests: 3, asks: 1, sessions: 4, teammates: 0, forgotten: 0, bytes: Math.round(14.6 * MB), leftOver: 4, intake: false, failed: 0,
  changed: [{ kind: 'quest', id: '0c1d2e', keep: { code: 'HISTORY_LIVE', session: 's0c1d2e3' } }],
};

/**
 * The workspace's second press where every unit it listed changed since the list and was kept, and nothing else went
 * (HIST1n): the driver answered, and nothing left this machine.
 */
export const WORKSPACE_ALL_KEPT: HistoryClearAnswer = {
  scope: 'workspace', id: 'aurora', listed: 3, cleared: [],
  quests: 0, asks: 0, sessions: 0, teammates: 0, forgotten: 0, bytes: 0, leftOver: 0, intake: false, failed: 0,
  changed: [
    { kind: 'ask', id: 'a1b2c3', keep: { code: 'HISTORY_NEEDS_YOU', context: 'proposalAsk', ask: 'a1b2c3' } },
    { kind: 'quest', id: '0c1d2e', keep: { code: 'HISTORY_LIVE', session: 's0c1d2e3' } },
    { kind: 'quest', id: '3f4a5b', keep: { code: 'HISTORY_UNPUSHED', quest: '3f4a5b', workspace: 'aurora' } },
  ],
};

/** One quest's second press. */
export const QUEST_CLEARED: HistoryClearAnswer = {
  scope: 'quest', id: '9a8b7c', listed: 1, cleared: [{ kind: 'quest', id: '9a8b7c' }],
  quests: 1, asks: 0, sessions: 3, teammates: 0, forgotten: 0, bytes: Math.round(2.1 * MB), leftOver: 0, intake: false, failed: 0,
  changed: [],
};

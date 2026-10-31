import type { AtWorkA, AtWorkChild, AtWorkHeld, AtWorkSubagents } from './types';

// Who is at work (COWORKVIEW1a): the answers the view's stories and tests draw, each a state the cowork view design §5.3 names.

const AT = '2026-10-31T14:02:00Z';
const LATER = '2026-10-31T14:05:00Z';

export const child = (key: string, state: AtWorkChild['state'], extra: Partial<AtWorkChild> = {}): AtWorkChild => ({
  key, state, startSaid: true, since: AT, depth: 1, parentKey: 'root', type: 'general-purpose', ...extra,
});

const subagents = (children: AtWorkChild[], extra: Partial<AtWorkSubagents> = {}): AtWorkSubagents => ({
  read: 'read', harness: 'claude-code', working: children.filter((each) => each.state === 'working').length, children, ...extra,
});

export const answerOf = (sub: AtWorkSubagents, extra: Partial<AtWorkA> = {}): AtWorkHeld => ({
  answer: {
    of: { session: 's1' }, attended: 's1',
    parts: [{ key: 's1', agent: 'claude-code', state: 'working', children: { count: sub.children.length, working: sub.working } }],
    subagents: sub,
    ...extra,
  },
  at: { epoch: 'e1', n: 1 },
});

/** A chat with three subagents: one working, one completed, one failed. */
export const THREE = answerOf(subagents([
  child('a1', 'working', { label: { text: 'Read the parser', claim: true } }),
  child('a2', 'completed', { ended: AT }),
  child('a3', 'failed', { ended: LATER }),
]));

/** On the protocol door, its turn waiting for the child working. */
export const TURN_WAITS = answerOf({ ...subagents([child('a1', 'working')]), turnWaits: true });

/** A nested child whose parent no harness said. */
export const NO_PARENT = answerOf(subagents([child('a1', 'working'), child('a9', 'working', { depth: undefined, parentKey: undefined })]));

/** A Codex lead: one child first seen, its end not said; and one with no lineage yet. */
export const CODEX = answerOf({ ...subagents([child('t-1', 'unsaid', { startSaid: false })], { harness: 'codex' }), seenByCalls: true });
export const CODEX_NONE = answerOf({ read: 'none', working: 0, children: [], seenByCalls: true });

/** A lost child, and one whose agent's exit this window saw before its end said how. */
export const LOST = answerOf(subagents([
  child('a1', 'lost', { ended: LATER }),
  child('a2', 'agentEnded', { lastSeen: LATER }),
]));

/** Not known whether it still runs: another Daoris process's run, beside one this window watches and one lost. */
export const UNCERTAIN = answerOf(subagents([
  child('a1', 'working'),
  child('a2', 'uncertain', { lastSeen: LATER }),
  child('a3', 'lost', { ended: LATER }),
]));

/** Agent ids alone: unmatched and not tied to a process. */
export const IDS_ONLY = answerOf({ read: 'read', working: 0, children: [], observed: 2, unplaced: 2 });

/** A driven record's history: one child whose end was not recorded beside one its harness ended. */
export const HISTORY = answerOf(subagents([
  child('a1', 'unrecorded', { lastSeen: LATER }),
  child('a2', 'completed', { ended: AT }),
  child('a3', 'finished', { ended: AT }),
  child('a4', 'stopped', { ended: AT }),
]));

/** An ended first process beside its working continuation. */
export const CONTINUED = answerOf(subagents([child('a1', 'ended', { ended: AT }), child('a2', 'working')]));

/** The bound: forty-one working, forty drawn, one more not drawn; and three ended folded. */
export const BOUND = answerOf({
  ...subagents(Array.from({ length: 40 }, (_, i) => child(`w${i}`, 'working'))),
  working: 41, more: { working: 1 }, folded: 3,
});

/** An entry of three parts, the attended first, one a teammate's. */
export const PARTS = answerOf(subagents([child('a1', 'working')]), {
  parts: [
    { key: 's1', agent: 'claude-code', state: 'working', children: { count: 1, working: 1 } },
    { key: 's0', agent: 'codex', state: 'completed', children: { count: 2, working: 0 } },
    { key: 'origin/s7', state: 'working', children: { elsewhere: true } },
  ],
  moreParts: { completed: 2 },
});

export const EMPTY = answerOf({ read: 'none', working: 0, children: [] });

export const ELSEWHERE = answerOf({ read: 'elsewhere', working: 0, children: [] });

export const UNREADABLE = answerOf({ read: 'unreadable', unreadable: 'IOException', working: 0, children: [] });

export const STALE: AtWorkHeld = { ...(THREE as { answer: AtWorkA }), stale: { why: 'the service timed out' } };

export const FAILED: AtWorkHeld = { failed: { why: 'the sessions could not be read' } };

export const READING: AtWorkHeld = { reading: true };

/** The clock the fixtures' times are read against: the same day. */
export const NOW = new Date('2026-10-31T15:00:00Z');

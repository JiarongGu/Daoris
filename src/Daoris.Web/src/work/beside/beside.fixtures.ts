import type { SessionEvent } from '../conversation/conversationTypes';

// A recorded session whose subagent spawned another, and a watch task, shaped as the door replays keep them: the session's
// record holds each child's and task's start and end with the call that started it, a subagent's own events its own record.

const at = (minute: number, second = 0) => `2026-11-19T10:${String(minute).padStart(2, '0')}:${String(second).padStart(2, '0')}Z`;

/** The session's record: the person's ask, a sub-agent's spawn, a watch task, the grandchild's lifetime, the ends. */
export const SESSION: SessionEvent[] = [
  { seq: 1, at: at(0), kind: 'user', origin: 'person', text: 'also cover the expiry path' },
  { seq: 2, at: at(0, 5), kind: 'tool', id: 'toolu_ag', title: 'Agent', toolKind: 'think', status: 'in_progress' },
  {
    seq: 3, at: at(0, 6), kind: 'child', id: 'a1', call: 'toolu_ag', depth: 1, name: 'find expiry tests',
    task: 'Find the tests that cover token expiry', by: 'reported',
  },
  { seq: 4, at: at(0, 10), kind: 'tool', id: 'toolu_watch', title: 'npm test --watch', toolKind: 'execute', status: 'completed' },
  { seq: 5, at: at(0, 11), kind: 'task', id: 'b1', call: 'toolu_watch', name: 'npm test --watch', by: 'reported' },
  // The grandchild, started by the sub-agent's own call: its start names its parent and that call, kept in the parent's record.
  {
    seq: 6, at: at(1), kind: 'child', id: 'a2', call: 'toolu_inner', parent: 'a1', depth: 2, name: 'search the fixtures',
    task: 'Search the fixtures for expiry', by: 'reported',
  },
  { seq: 7, at: at(2), kind: 'child', id: 'a2', end: 'ok', by: 'reported' },
  { seq: 8, at: at(3), kind: 'child', id: 'a1', end: 'ok', by: 'reported' },
  { seq: 9, at: at(3, 1), kind: 'tool', id: 'toolu_ag', status: 'completed' },
  { seq: 10, at: at(3, 5), kind: 'message', id: 'm1', text: 'The refresh now checks expiry first.' },
  { seq: 11, at: at(3, 6), kind: 'turn', stopReason: 'end_turn' },
  { seq: 12, at: at(12, 10), kind: 'task', id: 'b1', end: 'stopped', by: 'reported' },
];

/** The sub-agent's own record: its prompt, its calls, the call that spawned the grandchild, its closing words. */
export const CHILD: SessionEvent[] = [
  { seq: 1, at: at(0, 6), kind: 'user', origin: 'target', text: 'Find the tests that cover token expiry' },
  { seq: 2, at: at(0, 20), kind: 'tool', id: 'toolu_read', title: 'Read test/auth.test.ts', toolKind: 'read', status: 'completed' },
  { seq: 3, at: at(0, 40), kind: 'tool', id: 'toolu_inner', title: 'Agent', toolKind: 'think', status: 'completed' },
  { seq: 4, at: at(2, 50), kind: 'message', id: 'c1', text: 'It found two tests that cover expiry.' },
];

/** The grandchild's own record. */
export const GRANDCHILD: SessionEvent[] = [
  { seq: 1, at: at(1), kind: 'user', origin: 'target', text: 'Search the fixtures for expiry' },
  { seq: 2, at: at(1, 10), kind: 'tool', id: 'toolu_search', title: 'Search "expir"', toolKind: 'search', status: 'completed' },
  { seq: 3, at: at(1, 50), kind: 'message', id: 'g1', text: 'Two fixture files mention expiry.' },
];

/** Each child's own record by its id, as the driver's `SESSION_CHILD_HISTORY` answers it. */
export const RECORDS: Readonly<Record<string, SessionEvent[]>> = { a1: CHILD, a2: GRANDCHILD };

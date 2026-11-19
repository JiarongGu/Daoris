import type { SessionEvent } from '../conversation/conversationTypes';
import type { ReadMark } from './readMark';
import { SESSION as BESIDE } from '../beside/beside.fixtures';

// Three recorded sessions a person left and came back to, shaped as the driver keeps their records, each with the mark it was left at.

const at = (hour: number, minute: number, second = 0) =>
  `2026-11-19T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:${String(second).padStart(2, '0')}Z`;

/**
 * A conversation read through its first turn, then two more while the person was away: a failed test run and a passing one, two
 * files edited, and a push.
 */
export const CHAT: SessionEvent[] = [
  { seq: 1, at: at(9, 0), kind: 'user', origin: 'person', text: 'Add the expiry check' },
  { seq: 2, at: at(9, 1), kind: 'tool', id: 't1', title: 'Read src/auth.ts', toolKind: 'read', status: 'completed', locations: ['src/auth.ts'] },
  { seq: 3, at: at(9, 2), kind: 'tool', id: 't2', title: 'Edit src/auth.ts', toolKind: 'edit', status: 'completed', locations: ['src/auth.ts'] },
  { seq: 4, at: at(9, 3), kind: 'message', id: 'm1', text: 'Expiry is checked before refresh.' },
  { seq: 5, at: at(9, 3, 5), kind: 'turn', stopReason: 'end_turn' },
  { seq: 6, at: at(11, 0), kind: 'user', origin: 'person', text: 'also cover the tests' },
  { seq: 7, at: at(11, 1), kind: 'tool', id: 't3', title: 'npm test', toolKind: 'execute', status: 'failed' },
  { seq: 8, at: at(11, 2), kind: 'tool', id: 't4', title: 'Edit test/auth.test.ts', toolKind: 'edit', status: 'completed', locations: ['test/auth.test.ts'] },
  { seq: 9, at: at(11, 3), kind: 'tool', id: 't5', title: 'Edit src/auth.ts', toolKind: 'edit', status: 'completed', locations: ['src/auth.ts'] },
  { seq: 10, at: at(11, 4), kind: 'tool', id: 't6', title: 'npm test', toolKind: 'execute', status: 'completed' },
  { seq: 11, at: at(11, 5), kind: 'message', id: 'm2', text: 'I added a test for the expiry path.' },
  { seq: 12, at: at(11, 6), kind: 'message', id: 'm3', text: 'Both tests pass now.\nThe refresh covers expiry.' },
  { seq: 13, at: at(11, 9), kind: 'turn', stopReason: 'end_turn' },
  { seq: 14, at: at(11, 20), kind: 'user', origin: 'person', text: 'push it' },
  { seq: 15, at: at(11, 21), kind: 'tool', id: 't7', title: 'git push origin fix-login', toolKind: 'execute', status: 'completed' },
  { seq: 16, at: at(11, 22), kind: 'message', id: 'm4', text: 'Pushed fix-login.' },
  { seq: 17, at: at(11, 22, 5), kind: 'turn', stopReason: 'end_turn' },
];

export const CHAT_MARK: ReadMark = { part: 's-chat', seq: 5, at: at(9, 3, 5), read: at(9, 10) };

/** A driven session whose subagent spawned another and whose watch task the person stopped, read only as far as its ask. */
export const DRIVEN: SessionEvent[] = BESIDE;

export const DRIVEN_MARK: ReadMark = { part: 's-driven', seq: 1, at: '2026-11-19T10:00:00Z', read: '2026-11-19T10:00:30Z', steps: 1 };

/**
 * The newest part of a line that went on after a limit while the person was away: their mark is in the part before. Its first turn
 * was stopped with lint failing, and the next opened a pull request.
 */
export const MOVED: SessionEvent[] = [
  { seq: 1, at: at(10, 30), kind: 'note', text: 'Went on after the account limit.' },
  { seq: 2, at: at(10, 30, 5), kind: 'user', origin: 'target', text: 'Go on with the expiry tests' },
  { seq: 3, at: at(10, 31), kind: 'tool', id: 'u1', title: 'Search "expir"', toolKind: 'search', status: 'completed' },
  { seq: 4, at: at(10, 32), kind: 'tool', id: 'u2', title: 'npm run lint', toolKind: 'execute', status: 'failed' },
  { seq: 5, at: at(10, 33), kind: 'message', id: 'n1', text: 'Lint fails on a rule I cannot change.' },
  { seq: 6, at: at(10, 34), kind: 'turn', stopReason: 'cancelled' },
  { seq: 7, at: at(10, 40), kind: 'user', origin: 'person', text: 'leave lint, ship it' },
  { seq: 8, at: at(10, 41), kind: 'tool', id: 'u3', title: 'gh pr create --fill', toolKind: 'execute', status: 'completed' },
  { seq: 9, at: at(10, 42), kind: 'message', id: 'n2', text: 'Opened the pull request.' },
  { seq: 10, at: at(10, 42, 5), kind: 'turn', stopReason: 'end_turn' },
];

export const MOVED_MARK: ReadMark = { part: 's-before', seq: 88, at: at(10, 0), read: at(10, 1) };

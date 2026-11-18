import { describe, expect, it } from 'vitest';
import { toTurns } from './conversation';
import type { SessionEvent } from './conversationTypes';

const at = (second: number) => `2026-11-19T10:00:${String(second).padStart(2, '0')}Z`;

/** A session's record with a sub-agent and a background task, each started by a call, as the driver keeps them. */
const record: SessionEvent[] = [
  { seq: 1, at: at(0), kind: 'user', origin: 'person', text: 'split the work' },
  { seq: 2, at: at(1), kind: 'tool', id: 'toolu_ag', title: 'Agent', toolKind: 'think', status: 'in_progress' },
  { seq: 3, at: at(2), kind: 'child', id: 'a1', call: 'toolu_ag', name: 'Read README first line', by: 'reported' },
  { seq: 4, at: at(3), kind: 'tool', id: 'toolu_gates', title: 'npm run gates', toolKind: 'execute', status: 'completed' },
  { seq: 5, at: at(4), kind: 'task', id: 'b1', call: 'toolu_gates', name: 'npm run gates', by: 'reported' },
  { seq: 6, at: at(5), kind: 'message', id: 'm1', text: 'The gates are ' },
  // An end lands while the agent is still writing: its words stay one message.
  { seq: 7, at: at(6), kind: 'child', id: 'a1', end: 'ok', by: 'reported' },
  { seq: 8, at: at(7), kind: 'message', id: 'm1', text: 'running.' },
  { seq: 9, at: at(8), kind: 'turn', stopReason: 'end_turn' },
  { seq: 10, at: at(9), kind: 'task', id: 'b1', end: 'lost', by: 'inferred' },
  // One whose call this page does not hold is not drawn.
  { seq: 11, at: at(10), kind: 'task', id: 'b9', call: 'toolu_elsewhere', name: 'elsewhere', by: 'reported' },
];

describe('a sub-agent and a background task in the record', () => {
  it('names each on the card of the call that started it and says how it ended there, a task\'s end a row where it ended', () => {
    const { turns, where } = toTurns(record);
    const items = turns.flatMap((turn) => turn.items);

    expect(items.map((item) => item.kind)).toEqual(['tool', 'tool', 'message', 'beside']);
    expect(items.find((item) => item.id === 'toolu_ag')?.beside).toEqual(
      { kind: 'child', id: 'a1', name: 'Read README first line', started: at(2), end: 'ok', by: 'reported', ended: at(6) });
    expect(items.find((item) => item.id === 'toolu_gates')?.beside).toEqual(
      { kind: 'task', id: 'b1', name: 'npm run gates', started: at(4), end: 'lost', by: 'inferred', ended: at(9), endKey: 'e10' });
    expect(items.find((item) => item.kind === 'beside')).toMatchObject({ key: 'e10', beside: { id: 'b1', startKey: 'e4', end: 'lost' } });
    expect(items.find((item) => item.kind === 'message')?.text).toBe('The gates are running.');
    expect(where[3]).toBe(where[2]);
    expect(where[10]).toBe('e10');
    expect(where[11]).toBeUndefined();
  });

  it('rewords the row where a task ended when a later end its door says follows the inferred one', () => {
    const later: SessionEvent = { seq: 12, at: at(11), kind: 'task', id: 'b1', end: 'failed', by: 'reported' };
    const items = toTurns([...record, later]).turns.flatMap((turn) => turn.items);

    expect(items.filter((item) => item.kind === 'beside')).toHaveLength(1);
    expect(items.find((item) => item.kind === 'beside')?.beside).toMatchObject({ end: 'failed', by: 'reported', startKey: 'e4' });
  });
});

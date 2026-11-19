import { describe, expect, it } from 'vitest';
import type { Quest } from '../../data/api';
import { dockKeyOfExchange } from '../waits/dock';
import { aliveIn, lineRowsOf, rowsWithin } from './rows';
import { CHANNEL_WORDS, HELPER, OPINION, PIECE, at } from './rows.fixtures';

describe("a line's exchanges and channel messages as rows", () => {
  it('draws an exchange where it was asked and where it came back, a channel message where it was said, oldest first', () => {
    const rows = lineRowsOf({ exchanges: [OPINION, HELPER], messages: CHANNEL_WORDS });
    expect(rows.map((row) => row.key)).toEqual([
      'helper:h1/asked', 'message:m1', 'helper:h1/back',
      // An opinion's read keeps no moment: its rows stand at the foot, in the read's order.
      'opinion:o1/asked', 'opinion:o1/back',
    ]);
  });

  it('draws no row twice: a helper the agent asked is its card at its call, a quest it published once', () => {
    const quest = { id: 'q7', to: 'docs', status: 'Done', filed: at(1), updated: at(9) } as Quest;
    const rows = lineRowsOf({
      exchanges: [HELPER, { id: 'quest:q7', part: 's1', expects: 'work', to: 'docs', delivery: 'taken', outcome: 'done', holds: false }],
      carded: new Set(['helper:h1']),
      asked: [{ quest, question: true }],
    });
    expect(rows.map((row) => row.key)).toEqual(['quest:q7/asked', 'quest:q7/back']);
  });

  it('draws one row for a request not come back, and none for a source the read could not reach', () => {
    const rows = lineRowsOf({ exchanges: [PIECE, { ...HELPER, id: 'helper:unread', outcome: 'unread', at: undefined }] });
    expect(rows.map((row) => row.key)).toEqual(['piece:p1/asked']);
  });

  it('draws a row where the line resumed after the question its quest waited on came back', () => {
    const question = { id: 'q9', to: 'docs', status: 'Done', filed: at(1), updated: at(7) } as Quest;
    const rows = lineRowsOf({ exchanges: [HELPER], resumedAfter: question });
    expect(rows.map((row) => row.key)).toEqual(['helper:h1/asked', 'helper:h1/back', 'resumed:q9']);
    expect(rows.find((row) => row.kind === 'resumed')).toMatchObject({ at: Date.parse(at(7)), quest: question });
    expect(lineRowsOf({ exchanges: [HELPER] }).some((row) => row.kind === 'resumed')).toBe(false);
  });

  it('places each row between the turns it fell between, an untimed one under the last', () => {
    const rows = lineRowsOf({ exchanges: [HELPER, OPINION], messages: CHANNEL_WORDS });
    expect(rowsWithin(rows, at(0), at(3)).map((row) => row.key)).toEqual(['helper:h1/asked']);
    expect(rowsWithin(rows, at(3), undefined).map((row) => row.key))
      .toEqual(['message:m1', 'helper:h1/back', 'opinion:o1/asked', 'opinion:o1/back']);
    expect(rowsWithin(rows, at(5), at(9)).map((row) => row.key)).toEqual(['helper:h1/back']);
  });

  it('finds what works for the line now: a request held and taken up by a session', () => {
    expect(aliveIn([HELPER, PIECE, { ...OPINION, holds: true, outcome: 'asked' }]).map((each) => each.id)).toEqual(['opinion:o1']);
  });
});

describe('an exchange that waits on the person', () => {
  it('stands in the dock by the line its own kind keeps, never a second', () => {
    expect(dockKeyOfExchange('helper:h1')).toBe('helper:h1');
    expect(dockKeyOfExchange('hand-over:x2')).toBe('handOver:x2');
    expect(dockKeyOfExchange('opinion:o1')).toBeNull();
    expect(dockKeyOfExchange('quest:q7')).toBeNull();
  });
});

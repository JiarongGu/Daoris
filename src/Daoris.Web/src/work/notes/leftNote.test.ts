import { describe, expect, it } from 'vitest';
import type { Session } from '../../data/api';
import { leftParts } from './leftNote';

const PLAIN: NonNullable<Session['noteParts']> = [{ code: 'ended.done', values: {}, text: 'The quest reached done.' }];
const DEPARTED = { status: 'Done' as const, answers: [{ requirement: 1, met: 'built' }, { requirement: 2, departed: 'held', quote: 'theirs' }, { requirement: 3, departed: 'also', quote: 'theirs' }] };

describe("a session's note where its quest closed done with part left", () => {
  it('reads the plain done of an earlier record as the unfinished ending, counting what it departed from', () => {
    expect(leftParts(PLAIN, DEPARTED)).toEqual([{
      code: 'ended.done-left', values: { left: 2 }, text: 'The quest reached done, but it departed from 2 of its requirements: part of the work is left.',
    }]);
  });

  it('leaves every other note as written: a finished quest, no quest, other parts, and a note with none', () => {
    expect(leftParts(PLAIN, { status: 'Done', answers: [{ requirement: 1, met: 'built' }] })).toBe(PLAIN);
    expect(leftParts(PLAIN, null)).toBe(PLAIN);
    expect(leftParts(PLAIN, { status: 'Taken', answers: DEPARTED.answers })).toBe(PLAIN);
    const other: NonNullable<Session['noteParts']> = [{ code: 'ended.declined', values: {}, text: 'x' }, { words: 'said', by: 'agent' }];
    expect(leftParts(other, DEPARTED)).toEqual(other);
    expect(leftParts(null, DEPARTED)).toBeNull();
  });
});

import { describe, expect, it } from 'vitest';
import { ranked, widest, type IndexedRepository } from './overview';

const repo = (name: string, total: number, local = total): IndexedRepository =>
  ({ name, total, local });

/**
 * The Overview's repository chart, capped after the deployed application showed what the fixture
 * could not (D62): two repositories in the dev loop, fourteen on a real machine.
 */
describe('ranked', () => {
  it('puts the biggest first', () => {
    const { shown } = ranked([repo('small', 4), repo('big', 400), repo('middle', 40)]);
    expect(shown.map((r) => r.name)).toEqual(['big', 'middle', 'small']);
  });

  it('leaves a short list whole and hides nothing', () => {
    const { shown, hidden } = ranked([repo('one', 1), repo('two', 2)]);
    expect(shown).toHaveLength(2);
    expect(hidden).toBe(0);
  });

  /**
   * 🔴 The remainder is COUNTED. A list that silently stops reads as complete, and then the missing
   * repositories are a defect rather than a button — which is the failure this whole cap risks
   * introducing while fixing a different one.
   */
  it('caps the list and says how many it did not show', () => {
    const many = Array.from({ length: 14 }, (_, index) => repo(`r${index}`, index));
    const { shown, hidden } = ranked(many);

    expect(shown).toHaveLength(8);
    expect(hidden).toBe(6);
    // The eight shown are the eight biggest, not the first eight given.
    expect(shown.map((r) => r.total)).toEqual([13, 12, 11, 10, 9, 8, 7, 6]);
  });

  it('does not mutate what it was given', () => {
    const given = [repo('a', 1), repo('b', 9)];
    ranked(given);
    expect(given.map((r) => r.name)).toEqual(['a', 'b']);
  });

  it('survives a limit of nothing and an empty family', () => {
    expect(ranked([], 8)).toEqual({ shown: [], hidden: 0 });
    expect(ranked([repo('a', 1)], 0)).toEqual({ shown: [], hidden: 1 });
    expect(ranked([repo('a', 1)], -3).hidden).toBe(1);
  });
});

describe('widest', () => {
  /**
   * 🔴 Scaled to what is SHOWN. Measured against a total that is off the list, the longest visible
   * bar is never full — which reads as "nothing here is significant" when the truth is the opposite.
   */
  it('is the largest of the bars actually drawn', () => {
    const { shown } = ranked(Array.from({ length: 14 }, (_, index) => repo(`r${index}`, index)));
    expect(widest(shown)).toBe(13);
  });

  /** Never zero: a bar divided by it would be a division by zero on an empty or all-zero family. */
  it('is at least one, so no bar divides by nothing', () => {
    expect(widest([])).toBe(1);
    expect(widest([repo('a', 0)])).toBe(1);
  });
});

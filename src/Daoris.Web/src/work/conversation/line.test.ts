import { describe, expect, it } from 'vitest';
import { drawnOf, EARLIER_AT_MOST } from './line';

// A line's session draws its earlier parts by membership (`lineState.test.ts`); the nearest three, a press for up to three more.

describe('the bound on what is drawn', () => {
  const line = ['p1', 'p2', 'p3', 'p4', 'p5'];

  it('draws the three nearest, and says how many one press draws', () => {
    expect(EARLIER_AT_MOST).toBe(3);
    expect(drawnOf(line, 0)).toEqual({ drawn: ['p3', 'p4', 'p5'], more: 2 });
  });

  it('draws up to three more a press, the nearest still last', () => {
    expect(drawnOf(line, 1)).toEqual({ drawn: ['p1', 'p2', 'p3', 'p4', 'p5'], more: 0 });
    const long = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
    expect(drawnOf(long, 0)).toEqual({ drawn: ['f', 'g', 'h'], more: 3 });
    expect(drawnOf(long, 1)).toEqual({ drawn: ['c', 'd', 'e', 'f', 'g', 'h'], more: 2 });
  });

  it('presses for nothing where the line is short', () => {
    expect(drawnOf(['a'], 0)).toEqual({ drawn: ['a'], more: 0 });
    expect(drawnOf([], 0)).toEqual({ drawn: [], more: 0 });
  });
});

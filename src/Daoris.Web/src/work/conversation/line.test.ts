import { describe, expect, it } from 'vitest';
import { ancestryOf, drawnOf, EARLIER_AT_MOST } from './line';

// SWITCHCTX1c-i (the switch-context design §2.7): a session's page draws the parts the attended one continues by `from`, and
// only those; the nearest three, a press for up to three more. Pure, so every line a person could have is an argument.

const row = (id: string, from?: string | null) => ({ id, from });

describe('the parts a line draws above the attended one', () => {
  it('walks the attended part’s `from` links, oldest first', () => {
    const rows = [row('a'), row('b', 'a'), row('c', 'b'), row('d', 'c')];
    expect(ancestryOf(rows, 'd')).toEqual(['a', 'b', 'c']);
    // Pressing an earlier part draws its own ancestry, so the parts after it are no longer drawn.
    expect(ancestryOf(rows, 'b')).toEqual(['a']);
    expect(ancestryOf(rows, 'a')).toEqual([]);
  });

  it('leaves a part running alongside to the strip: it continues none by `from`', () => {
    const rows = [row('a'), row('b', 'a'), row('side'), row('c', 'b')];
    expect(ancestryOf(rows, 'c')).toEqual(['a', 'b']);
    expect(ancestryOf(rows, 'side')).toEqual([]);
  });

  it('stops at a record the page does not hold, or one outside the entry', () => {
    const rows = [row('b', 'gone'), row('c', 'b'), row('x'), row('y', 'x')];
    expect(ancestryOf(rows, 'c')).toEqual(['b']);
    expect(ancestryOf(rows, 'y', new Set(['y']))).toEqual([]);
    expect(ancestryOf(rows, 'y', new Set(['x', 'y']))).toEqual(['x']);
  });

  it('stops at a part that is not on this machine, whose conversation the page does not hold (D47 §4)', () => {
    const rows = [row('mate/a'), row('b', 'mate/a'), row('c', 'b')];
    expect(ancestryOf(rows, 'c')).toEqual(['b']);
  });

  it('is not led round a loop its records make', () => {
    const rows = [row('a', 'c'), row('b', 'a'), row('c', 'b')];
    expect(ancestryOf(rows, 'c')).toEqual(['a', 'b']);
  });
});

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

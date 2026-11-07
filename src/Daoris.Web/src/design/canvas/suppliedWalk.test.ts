import { describe, expect, it } from 'vitest';
import { createWalk, type Stop } from './suppliedWalk';

// The walk over a supplied world's cards and its lines. → from a card reaches its
// first line out and then the card beyond, ← the line it came by, ↑ and ↓ cycle the lines at one end, and every line is reached.

const box = (x: number, y: number) => ({ x, y, w: 100, h: 40 });
const BOXES = new Map([['a', box(0, 0)], ['b', box(300, 0)], ['c', box(300, 100)], ['d', box(0, 200)], ['$', box(0, 100)]]);
const LINES = [
  { id: 'a>b', from: 'a', to: 'b' }, { id: 'a>c', from: 'a', to: 'c' }, { id: 'b>c', from: 'b', to: 'c' }, { id: 'c>a', from: 'c', to: 'a' },
  { id: '$>d', from: '$', to: 'd' },
];
const node = (id: string): Stop => ({ line: false, id });
const line = (id: string): Stop => ({ line: true, id });
const walker = () => createWalk({ order: ['a', 'b', 'c', 'd'], boxes: BOXES, lines: LINES, statics: new Set(['$']) });

describe('the walk over a supplied world', () => {
  it('goes from a card to its first line out and on to the card beyond, and comes back by the line it came by', () => {
    const walk = walker();
    expect(walk.step(node('a'), 'ArrowRight')).toEqual(line('a>b'));
    expect(walk.step(line('a>b'), 'ArrowRight')).toEqual(node('b'));
    // The way back is the line it came by, then the card it left.
    expect(walk.step(node('b'), 'ArrowLeft')).toEqual(line('a>b'));
    expect(walk.step(line('a>b'), 'ArrowLeft')).toEqual(node('a'));
  });

  it('cycles the lines out of one card on the vertical arrows, wrapping at both ends', () => {
    const walk = walker();
    expect(walk.step(node('a'), 'ArrowRight')).toEqual(line('a>b'));
    expect(walk.step(line('a>b'), 'ArrowDown')).toEqual(line('a>c'));
    expect(walk.step(line('a>c'), 'ArrowDown')).toEqual(line('a>b'));
    expect(walk.step(line('a>b'), 'ArrowUp')).toEqual(line('a>c'));
  });

  it('reaches the lines into a card by ←, each once, and the card at the far end of each', () => {
    const walk = walker();
    expect(walk.step(node('c'), 'ArrowLeft')).toEqual(line('a>c'));
    expect(walk.step(line('a>c'), 'ArrowDown')).toEqual(line('b>c'));
    expect(walk.step(line('b>c'), 'ArrowLeft')).toEqual(node('b'));
  });

  it('reaches every line from one of its ends (trap 15)', () => {
    const reached = new Set<string>();
    for (const card of ['a', 'b', 'c', 'd']) {
      const walk = walker();
      const first = walk.step(node(card), 'ArrowRight');
      let at: Stop | null = first;
      while (at && !reached.has(`${card}:${at.id}`)) {
        reached.add(`${card}:${at.id}`);
        at = walk.step(at, 'ArrowDown');
      }
    }
    expect([...reached].map((each) => each.split(':')[1]).sort()).toEqual(['a>b', 'a>c', 'b>c', 'c>a']);
    // A source that is no card has no stop to leave from; its line is reached by the card it ends on.
    expect(walker().step(node('d'), 'ArrowLeft')).toEqual(line('$>d'));
  });

  it('stays where there is nothing beyond, and never lands on a static source', () => {
    const walk = walker();
    expect(walk.step(node('d'), 'ArrowRight')).toBeNull();
    expect(walk.step(line('$>d'), 'ArrowLeft')).toBeNull();
    expect(walk.step(node('a'), 'ArrowDown')).toEqual(node('d'));
  });

  it('walks the cards on the vertical arrows by place, and Home and End to the ends of the reading order', () => {
    const walk = walker();
    expect(walk.step(node('a'), 'ArrowDown')).toEqual(node('d'));
    expect(walk.step(node('b'), 'ArrowDown')).toEqual(node('c'));
    expect(walk.step(node('c'), 'Home')).toEqual(node('a'));
    expect(walk.step(node('a'), 'End')).toEqual(node('d'));
  });
});

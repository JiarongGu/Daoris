import { describe, expect, it } from 'vitest';
import { boxSelect, edgeDrift, worldRect } from './gesture';
import { placeChips } from './marks';

// The arithmetic of the box drawn to select, the pan at the edge, and where a mark's chip stands.

describe('a box drawn to select', () => {
  it('is the world rectangle the two points cover, whichever corner is first and at whatever scale', () => {
    expect(worldRect({ x: 100, y: 50, k: 2 }, { x: 40, y: 30 }, { x: 10, y: 10 })).toEqual({ x: 105, y: 55, w: 15, h: 10 });
  });

  it('selects what it touches, in reading order, and nothing it misses', () => {
    const boxes = new Map([
      ['low', { x: 0, y: 200, w: 100, h: 50 }], ['right', { x: 200, y: 0, w: 100, h: 50 }], ['left', { x: 0, y: 0, w: 100, h: 50 }],
      ['far', { x: 900, y: 900, w: 10, h: 10 }],
    ]);
    expect(boxSelect({ x: 50, y: 20, w: 300, h: 200 }, boxes)).toEqual(['left', 'right', 'low']);
    expect(boxSelect({ x: 100, y: 50, w: 100, h: 150 }, boxes)).toEqual([]);
  });
});

describe('the pan at the viewport\'s edge', () => {
  const size = { w: 800, h: 400 };
  it('is none inside the margin and toward the edge the pointer is at, growing to the speed at the edge', () => {
    expect(edgeDrift({ x: 400, y: 200 }, size, 32, 12)).toEqual({ x: 0, y: 0 });
    expect(edgeDrift({ x: 0, y: 200 }, size, 32, 12)).toEqual({ x: -12, y: 0 });
    expect(edgeDrift({ x: 800, y: 400 }, size, 32, 12)).toEqual({ x: 12, y: 12 });
    expect(edgeDrift({ x: 16, y: 200 }, size, 32, 12).x).toBe(-6);
  });
});

describe('a mark\'s chip', () => {
  const node = { x: 100, y: 100, w: 200, h: 60 };
  it('stands above its node\'s right end where that clears every box', () => {
    const [chip] = placeChips([{ id: 'a', node, w: 80, h: 20 }], [node]);
    expect(chip).toEqual({ id: 'a', x: 220, y: 78, shown: true });
  });

  it('moves to the next place that clears, and is dropped where none does', () => {
    const above = { x: 0, y: 70, w: 400, h: 28 };
    const [moved] = placeChips([{ id: 'a', node, w: 80, h: 20 }], [node, above]);
    expect(moved).toMatchObject({ x: 220, y: 162, shown: true });
    const below = { x: 0, y: 160, w: 400, h: 28 };
    const [dropped] = placeChips([{ id: 'a', node, w: 80, h: 20 }], [node, above, below]);
    expect(dropped!.shown).toBe(false);
  });

  it('clears the chips placed before it', () => {
    const [first, second] = placeChips([{ id: 'a', node, w: 80, h: 20 }, { id: 'b', node, w: 80, h: 20 }], [node]);
    expect(first!.shown && second!.shown).toBe(true);
    expect(second!.x === first!.x && second!.y === first!.y).toBe(false);
  });
});

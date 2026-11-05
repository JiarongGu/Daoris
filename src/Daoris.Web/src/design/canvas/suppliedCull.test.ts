import { describe, expect, it } from 'vitest';
import { buildGrid } from './spatialGrid';
import { cullSupplied, lineDrawn } from './suppliedCull';
import type { Box } from './viewMath';

// CANVAS1e (the canvas design §2.6, §4, trap 11): a supplied world's cards culled past 400 and drawn as dots below 4 px of `text-meta`,
// the focused, the selected and find's match always mounted.

const field = (count: number): Map<string, Box> => {
  const side = Math.ceil(Math.sqrt(count));
  return new Map(Array.from({ length: count }, (_, at) => [`card-${at}`, { x: (at % side) * 200, y: Math.floor(at / side) * 100, w: 100, h: 40 }]));
};
const VIEWPORT = { w: 800, h: 600 };

describe('a supplied world\'s cull', () => {
  it('mounts every card at the full tier before a viewport holds it', () => {
    const grid = buildGrid(field(900));
    const cull = cullSupplied(grid, null, 16, []);
    expect(cull).toMatchObject({ tier: 'full', culled: null, dots: false });
    expect(cull.mounted.size).toBe(900);
  });

  it('mounts every card to 400 and draws every line', () => {
    const grid = buildGrid(field(400));
    const cull = cullSupplied(grid, { view: { x: 0, y: 0, k: 1 }, viewport: VIEWPORT }, 16, []);
    expect(cull.mounted.size).toBe(400);
    expect(cull.culled).toBeNull();
  });

  it('past 400, mounts what is near the view and the pinned wherever they lie, and draws only the lines that reach one', () => {
    const grid = buildGrid(field(900));
    const cull = cullSupplied(grid, { view: { x: 0, y: 0, k: 1 }, viewport: VIEWPORT }, 16, ['card-899', null]);
    expect(cull.tier).toBe('full');
    expect(cull.mounted.size).toBeLessThan(900);
    expect(cull.mounted.has('card-0')).toBe(true);
    expect(cull.mounted.has('card-899')).toBe(true);
    expect(cull.culled).toBe(cull.mounted);
    expect(lineDrawn(cull.culled, 'card-0', 'card-450')).toBe(true);
    expect(lineDrawn(cull.culled, 'card-450', 'card-451')).toBe(cull.mounted.has('card-450') || cull.mounted.has('card-451'));
  });

  it('draws dots below 4 px of text-meta, the pinned alone mounted, and the window\'s zoom counted', () => {
    const grid = buildGrid(field(100));
    const far = cullSupplied(grid, { view: { x: 0, y: 0, k: 0.3 }, viewport: VIEWPORT }, 16, ['card-7']);
    expect(far).toMatchObject({ tier: 'dot', dots: true, culled: null });
    expect([...far.mounted]).toEqual(['card-7']);
    // At a window zoom of 200%, the same scale is twice the size on screen: a chip, not a dot.
    expect(cullSupplied(grid, { view: { x: 0, y: 0, k: 0.3 }, viewport: VIEWPORT }, 32, []).tier).toBe('chip');
  });

  it('keeps a line\'s pinned ends mounted at the dot tier past 400, and draws only that line', () => {
    const grid = buildGrid(field(900));
    const cull = cullSupplied(grid, { view: { x: 0, y: 0, k: 0.05 }, viewport: VIEWPORT }, 16, ['card-3', 'card-500']);
    expect(cull.tier).toBe('dot');
    expect([...cull.mounted].sort()).toEqual(['card-3', 'card-500']);
    expect(lineDrawn(cull.culled, 'card-3', 'card-500')).toBe(true);
    expect(lineDrawn(cull.culled, 'card-1', 'card-2')).toBe(false);
  });
});

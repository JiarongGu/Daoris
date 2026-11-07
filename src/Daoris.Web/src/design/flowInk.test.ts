import { describe, expect, it } from 'vitest';
import { type Box, type InkLabel, type InkPath, placeInserts, routeInk } from './flowInk';
import { type FlowShape, layoutFlow } from './flowLayout';

// A decision's `when` path leaves its own port's box, not a spot spread along the node's edge; a data wire is
// drawn outside the nodes on a lane of its own at the right, only for the ends it is given.

const SHAPE: FlowShape = {
  nodes: [{ id: 'find' }, { id: 'record' }, { id: 'none' }, { id: 'end' }],
  edges: [
    { id: 'a', from: 'find', to: 'record', style: 'when', port: 'release' },
    { id: 'b', from: 'find', to: 'none', style: 'when', port: 'none' },
    { id: 'c', from: 'record', to: 'end' },
  ],
  entries: ['find'],
};

const boxes = (): Map<string, Box> => new Map<string, Box>([
  ['find', { x: 100, y: 0, w: 200, h: 80 }],
  ['record', { x: 0, y: 160, w: 200, h: 60 }],
  ['none', { x: 220, y: 160, w: 200, h: 60 }],
  ['end', { x: 0, y: 300, w: 200, h: 60 }],
]);

/** The path's first point, read from its `M` command. */
const start = (d: string) => d.slice(1).split(' ').slice(0, 2).map(Number) as [number, number];

describe('ports', () => {
  it('start a when path at the bottom centre of its port\'s box', () => {
    const layout = layoutFlow(SHAPE);
    const ports = new Map<string, Box>([
      ['find:release', { x: 110, y: 50, w: 90, h: 30 }],
      ['find:none', { x: 200, y: 50, w: 90, h: 30 }],
    ]);
    const paths = routeInk(layout, 'down', boxes(), { ports });
    expect(start(paths.find((path) => path.to === 'record')!.d)).toEqual([155, 80]);
    expect(start(paths.find((path) => path.to === 'none')!.d)).toEqual([245, 80]);
  });

  it('spread as before where no port was measured', () => {
    const layout = layoutFlow(SHAPE);
    const there = routeInk(layout, 'down', boxes(), { ports: new Map() });
    const before = routeInk(layout, 'down', boxes());
    expect(there.map((path) => path.d)).toEqual(before.map((path) => path.d));
  });
});

describe('data wires', () => {
  it('run from the source\'s foot to the target\'s head by a lane outside the nodes at the right', () => {
    const layout = layoutFlow(SHAPE);
    const all = routeInk(layout, 'down', boxes(), { data: [{ id: 'd', from: 'find', to: 'end' }] });
    const data = all.find((path) => path.id === 'd')!;
    expect(data).toMatchObject({ style: 'data', back: false, from: 'find', to: 'end' });
    const right = Math.max(...[...boxes().values()].map((box) => box.x + box.w));
    expect(Math.max(...data.points.map((point) => point.x))).toBeGreaterThan(right);
    // It never enters a node but its own ends.
    for (const [id, box] of boxes()) {
      if (id === 'find' || id === 'end') continue;
      expect(data.points.some((p) => p.x > box.x + 1 && p.x < box.x + box.w - 1 && p.y > box.y + 1 && p.y < box.y + box.h - 1), id).toBe(false);
    }
    expect(data.mid).not.toBeNull();
  });

  it('are drawn for no wire that is not given, and none that does not go on to a later rank', () => {
    const layout = layoutFlow(SHAPE);
    expect(routeInk(layout, 'down', boxes()).some((path) => path.style === 'data')).toBe(false);
    const backwards = routeInk(layout, 'down', boxes(), { data: [{ id: 'd', from: 'end', to: 'find' }] });
    expect(backwards.some((path) => path.id === 'd')).toBe(false);
  });
});

// An editor's ＋ on a way, placed from its path's middle outward where it clears every node, every label shown and
// every ＋ placed before it, and not shown where it clears none.
describe('an editor’s ＋ on a way', () => {
  const ABOVE: Box = { x: 0, y: 0, w: 200, h: 40 };
  const BELOW: Box = { x: 0, y: 80, w: 200, h: 40 };
  /** A straight link from one node's foot to the next one's head, 40 px long, sampled each pixel. */
  const link = (id: string, x = 100): InkPath => ({
    id, from: 'a', to: 'b', style: 'next', back: false, port: null, d: '', mid: { x, y: 60 },
    points: Array.from({ length: 41 }, (_, at) => ({ x, y: 40 + at })),
  });
  const size = (ids: string[]) => new Map(ids.map((id) => [id, { w: 20, h: 20 }]));

  it('stands at its path\'s middle where that clears the nodes, and moves along it past a label there', () => {
    expect(placeInserts([link('p')], size(['p']), [ABOVE, BELOW], [])).toEqual([{ id: 'p', x: 100, y: 60, w: 20, h: 20, shown: true }]);
    const chip: InkLabel = { id: 'p', x: 100, y: 52, w: 30, h: 8, shown: true };
    const moved = placeInserts([link('p')], size(['p']), [ABOVE, BELOW], [chip])[0]!;
    expect(moved.shown).toBe(true);
    expect(moved.y - 10).toBeGreaterThanOrEqual(chip.y + chip.h / 2 + 2);
  });

  it('takes an empty label as no room, and is not shown where it clears nothing, nor over a ＋ placed before it', () => {
    const empty: InkLabel = { id: 'p', x: 100, y: 60, w: 8, h: 0, shown: true };
    expect(placeInserts([link('p')], size(['p']), [ABOVE, BELOW], [empty])[0]!.shown).toBe(true);
    const tight: Box = { x: 0, y: 46, w: 200, h: 28 };
    expect(placeInserts([link('p')], size(['p']), [tight], [])[0]!.shown).toBe(false);
    const [first, second] = placeInserts([link('p'), link('q', 105)], size(['p', 'q']), [ABOVE, BELOW], []);
    expect([first!.shown, second!.shown]).toEqual([true, false]);
  });
});

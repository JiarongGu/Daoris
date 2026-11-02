import { describe, expect, it } from 'vitest';
import { syntheticScene } from './canvasFixtures';
import { findMatches, stepMatch } from './findNodes';
import { createLayoutClient, type WorkerLike } from './layoutClient';
import { layoutScene, NODE_H, NODE_W } from './layoutScene';
import { minimapGeometry, viewAtMinimap, wantsMinimap } from './minimapRule';
import { buildGrid, nearestWay, queryGrid } from './spatialGrid';
import { budgeted, CULL_PAST, mountedIds, tierAt, viewRect } from './tiers';
import { anchorOf, graphIdentity, keepView, memoryKey, recallView, restoreView } from './viewMemory';
import type { Box } from './viewMath';

const boxesOf = (layout: ReturnType<typeof layoutScene>) =>
  new Map(layout.ids.map((id, index): [string, Box] => [id, { x: layout.at[index * 2]!, y: layout.at[index * 2 + 1]!, w: NODE_W, h: NODE_H }]));

describe('the layout', () => {
  it('places every node once, none over another, and is the same twice', () => {
    for (const count of [1, 100, 1000]) {
      const layout = layoutScene(syntheticScene(count));
      const boxes = [...boxesOf(layout).values()];
      expect(boxes).toHaveLength(count);
      const grid = buildGrid(boxesOf(layout));
      for (const [id, box] of boxesOf(layout)) {
        const touching = [...queryGrid(grid, box)].filter((other) => other !== id);
        expect(touching, `${id} in ${count}`).toEqual([]);
      }
      expect(layoutScene(syntheticScene(count)).at).toEqual(layout.at);
    }
  });

  it('puts a node below what points at it, and survives a cycle', () => {
    const layout = layoutScene({
      nodes: ['a', 'b', 'c'].map((id) => ({ id, title: id })),
      edges: [{ id: 'ab', from: 'a', to: 'b' }, { id: 'bc', from: 'b', to: 'c' }, { id: 'ca', from: 'c', to: 'a' }],
    });
    const boxes = boxesOf(layout);
    expect(boxes.get('b')!.y).toBeGreaterThan(boxes.get('a')!.y);
    expect(boxes.get('c')!.y).toBeGreaterThan(boxes.get('b')!.y);
  });

  it('boxes groups at sizes their counts predict and sums the edges between them exactly, by direction and kind', () => {
    const scene = syntheticScene(500, { groups: 5 });
    const layout = layoutScene(scene);
    expect(layout.groups).toHaveLength(5);
    expect(layout.groups.reduce((sum, group) => sum + group.count, 0)).toBe(500);
    const between = scene.edges.filter((edge) => scene.nodes.find((n) => n.id === edge.from)!.group !== scene.nodes.find((n) => n.id === edge.to)!.group);
    expect(layout.aggregates.reduce((sum, each) => sum + each.count, 0)).toBe(between.length);
    expect(new Set(layout.aggregates.flatMap((each) => each.members)).size).toBe(between.length);
    // Every node lies inside its group's box.
    const boxes = boxesOf(layout);
    for (const node of scene.nodes) {
      const group = layout.groups.find((each) => each.id === node.group)!;
      const box = boxes.get(node.id)!;
      expect(box.x).toBeGreaterThanOrEqual(group.x);
      expect(box.x + box.w).toBeLessThanOrEqual(group.x + group.w);
      expect(box.y).toBeGreaterThanOrEqual(group.y);
      expect(box.y + box.h).toBeLessThanOrEqual(group.y + group.h);
    }
  });

  it('keeps a reciprocal pair of groups as two aggregates, one each way', () => {
    const layout = layoutScene({
      nodes: [{ id: 'a', title: 'a', group: 'x' }, { id: 'b', title: 'b', group: 'y' }],
      edges: [{ id: 'ab', from: 'a', to: 'b', kind: 'k' }, { id: 'ba', from: 'b', to: 'a', kind: 'k' }],
      groups: [{ id: 'x', title: 'X' }, { id: 'y', title: 'Y' }],
    });
    expect(layout.aggregates.map((each) => [each.from, each.to, each.count])).toEqual([['x', 'y', 1], ['y', 'x', 1]]);
  });

  it('lays the 10,000 nodes and 50,000 edges out in a few seconds at most', () => {
    const scene = syntheticScene(10_000, { groups: 100, edgesPerNode: 5 });
    expect(scene.edges.length).toBeGreaterThanOrEqual(50_000);
    const start = performance.now();
    const layout = layoutScene(scene);
    expect(layout.ids).toHaveLength(10_000);
    expect(performance.now() - start).toBeLessThan(5000);
  });
});

describe('the spatial grid and the cull', () => {
  const layout = layoutScene(syntheticScene(1000));
  const boxes = boxesOf(layout);
  const grid = buildGrid(boxes);

  it('finds what a rect meets and nothing else', () => {
    const rect = { x: 0, y: 2000, w: 600, h: 800 };
    const found = queryGrid(grid, rect);
    const expected = [...boxes].filter(([, box]) => box.x < rect.x + rect.w && rect.x < box.x + box.w && box.y < rect.y + rect.h && rect.y < box.y + box.h).map(([id]) => id);
    expect([...found].sort()).toEqual(expected.sort());
    expect(found.size).toBeLessThan(boxes.size / 4);
  });

  it('mounts all to 400 and the view plus the pinned past it', () => {
    const few = buildGrid(new Map([...boxes].slice(0, CULL_PAST)));
    expect(mountedIds(few, { x: 0, y: 0, w: 1, h: 1 }, []).size).toBe(CULL_PAST);
    const view = viewRect({ x: 0, y: 0, k: 1 }, { w: 800, h: 600 }, 0.5);
    const mounted = mountedIds(grid, view, ['n900', null, 'nothing']);
    expect(mounted.size).toBeLessThan(120);
    expect(mounted.has('n900')).toBe(true);
    expect(mounted.has('n1')).toBe(true);
  });

  it('walks a key to the nearest node on that side, culled or not, and to none past the edge', () => {
    const first = [...boxes].sort((a, b) => a[1].y - b[1].y || a[1].x - b[1].x)[0]![0];
    const down = nearestWay(grid, first, 'down')!;
    expect(boxes.get(down)!.y).toBeGreaterThan(boxes.get(first)!.y);
    expect(nearestWay(grid, first, 'up')).toBeNull();
    // Out and back on a column returns to where it began.
    expect(nearestWay(grid, down, 'up')).toBe(first);
  });
});

describe('the tiers and the budget', () => {
  it('follows text-meta at 11 px × the window\'s zoom × k: 9 px is the full tier, 4 px a chip, below it a dot', () => {
    expect(tierAt(1, 16)).toBe('full');
    expect(tierAt(0.82, 16)).toBe('full');
    expect(tierAt(0.8, 16)).toBe('chip');
    expect(tierAt(0.37, 16)).toBe('chip');
    expect(tierAt(0.36, 16)).toBe('dot');
    // The window's 50%: the root is 8 px, so the same k reads at half the size.
    expect(tierAt(1, 8)).toBe('chip');
    expect(tierAt(0.74, 32)).toBe('full');
    expect(tierAt(0.42, 32)).toBe('full');
    expect(tierAt(0.4, 8)).toBe('dot');
  });

  it('drops the tier before the frame: past 250 full-tier nodes or 2,000 paths', () => {
    expect(budgeted('full', 250, 2000)).toBe('full');
    expect(budgeted('full', 251, 10)).toBe('chip');
    expect(budgeted('full', 10, 2001)).toBe('dot');
    expect(budgeted('chip', 1501, 0)).toBe('dot');
    expect(budgeted('dot', 100_000, 100_000)).toBe('dot');
  });
});

describe('a layout in a worker', () => {
  /** A worker that answers when told, so the test chooses the order. */
  function fake() {
    const sent: { revision: number; scene: ReturnType<typeof syntheticScene> }[] = [];
    let ended = 0;
    const worker: WorkerLike = {
      onmessage: null, onerror: null, terminate: () => { ended += 1; },
      postMessage: (message) => { sent.push(message); },
    };
    return { worker, sent, ended: () => ended, answer: (at: number) => worker.onmessage?.({ data: layoutScene(sent[at]!.scene, sent[at]!.revision) }) };
  }

  it('lays a small scene out here and a large one in the worker, under the scene\'s revision', async () => {
    const made = fake();
    const client = createLayoutClient(() => made.worker, 50);
    expect((await client.run(syntheticScene(5)))?.ids).toHaveLength(5);
    expect(made.sent).toHaveLength(0);
    const pending = client.run(syntheticScene(100));
    expect(made.sent).toHaveLength(1);
    made.answer(0);
    expect((await pending)?.revision).toBe(made.sent[0]!.revision);
  });

  it('drops a stale answer and ends a worker it cancels', async () => {
    const made = fake();
    const client = createLayoutClient(() => made.worker, 50);
    const old = client.run(syntheticScene(100));
    const fresh = client.run(syntheticScene(120));
    expect(await old).toBeNull();
    expect(made.ended()).toBe(1);
    made.answer(0);
    made.answer(1);
    expect((await fresh)?.ids).toHaveLength(120);
    const last = client.run(syntheticScene(100));
    client.cancel();
    expect(await last).toBeNull();
    expect(made.ended()).toBe(2);
  });

  it('lays out here when a worker fails', async () => {
    const made = fake();
    const client = createLayoutClient(() => made.worker, 50);
    const pending = client.run(syntheticScene(100));
    made.worker.onerror?.({});
    expect((await pending)?.ids).toHaveLength(100);
  });
});

describe('the place the viewer keeps', () => {
  const memory = () => {
    const map = new Map<string, string>();
    return { getItem: (key: string) => map.get(key) ?? null, setItem: (key: string, value: string) => { map.set(key, value); } };
  };
  const nodes = new Map<string, Box>([['a', { x: 100, y: 100, w: 160, h: 56 }], ['b', { x: 400, y: 100, w: 160, h: 56 }]]);
  const view = { x: 50, y: 40, k: 0.8 };

  it('restores an unchanged graph exactly and claims nothing under a changed fed commit', () => {
    const store = memory();
    const at = memoryKey('code', 'repo');
    const identity = graphIdentity(['a', 'b'], 'commit-1');
    keepView({ identity, view, anchor: anchorOf(view, { w: 800, h: 600 }, nodes, () => null), selected: 'a' }, at, store);
    expect(restoreView(recallView(at, store)!, identity, nodes, new Map())).toEqual({ view, selectionGone: false });
    expect(graphIdentity(['a', 'b'], 'commit-2')).not.toBe(identity);
  });

  it('keeps the surviving anchor\'s screen place when the graph changed, else its group, else nothing, and says the selection is gone', () => {
    const kept = { identity: 'old', view, anchor: { id: 'a', group: 'g', screen: { x: 40, y: 48 } }, selected: 'c' };
    const moved = new Map<string, Box>([['a', { x: 300, y: 200, w: 160, h: 56 }]]);
    const result = restoreView(kept, 'new', moved, new Map())!;
    expect(result.selectionGone).toBe(true);
    // The anchor's top left is at (300 - view.x) * k = its old screen place.
    expect((300 - result.view.x) * result.view.k).toBeCloseTo(40);
    expect((200 - result.view.y) * result.view.k).toBeCloseTo(48);
    const byGroup = restoreView(kept, 'new', new Map(), new Map([['g', { x: 0, y: 0, w: 500, h: 500 }]]))!;
    expect((0 - byGroup.view.x) * byGroup.view.k).toBeCloseTo(40);
    expect(restoreView(kept, 'new', new Map(), new Map())).toBeNull();
  });

  it('reads nothing from a store that throws or holds rubbish', () => {
    expect(recallView('x', { getItem: () => { throw new Error('no'); }, setItem: () => undefined })).toBeNull();
    expect(recallView('x', { getItem: () => '{"identity":1}', setItem: () => undefined })).toBeNull();
    expect(recallView('x', { getItem: () => 'not json', setItem: () => undefined })).toBeNull();
  });
});

describe('the minimap', () => {
  it('is wanted past twice the viewport either way, whatever the zoom', () => {
    expect(wantsMinimap({ w: 1600, h: 400 }, { w: 800, h: 500 })).toBe(false);
    expect(wantsMinimap({ w: 1601, h: 400 }, { w: 800, h: 500 })).toBe(true);
    expect(wantsMinimap({ w: 700, h: 1001 }, { w: 800, h: 500 })).toBe(true);
  });

  it('draws a rectangle equal to the view, and a press centres the view there', () => {
    const world = { w: 4000, h: 20_000 };
    const viewport = { w: 900, h: 600 };
    const view = { x: 300, y: 5000, k: 0.5 };
    const geometry = minimapGeometry(world, view, viewport);
    expect(Math.max(geometry.width, geometry.height)).toBeCloseTo(160);
    expect(geometry.rect.x / geometry.scale).toBeCloseTo(view.x);
    expect(geometry.rect.y / geometry.scale).toBeCloseTo(view.y);
    expect(geometry.rect.w / geometry.scale).toBeCloseTo(viewport.w / view.k);
    expect(geometry.rect.h / geometry.scale).toBeCloseTo(viewport.h / view.k);
    const centred = viewAtMinimap({ x: geometry.rect.x + geometry.rect.w / 2, y: geometry.rect.y + geometry.rect.h / 2 }, geometry, { ...view, x: 0, y: 0 }, viewport);
    expect(centred.x).toBeCloseTo(view.x);
    expect(centred.y).toBeCloseTo(view.y);
  });
});

describe('find', () => {
  const items = [{ id: '1', title: 'Build the Release' }, { id: '2', title: 'Test' }, { id: '3', title: 'release notes' }];
  it('folds case, keeps the order, and steps around', () => {
    expect(findMatches(items, ' RELEASE ')).toEqual(['1', '3']);
    expect(findMatches(items, '')).toEqual([]);
    expect(stepMatch(-1, 2, false)).toBe(0);
    expect(stepMatch(1, 2, false)).toBe(0);
    expect(stepMatch(0, 2, true)).toBe(1);
    expect(stepMatch(-1, 0, false)).toBe(-1);
  });
});

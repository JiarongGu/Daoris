import { describe, expect, it } from 'vitest';
import type { Box } from '../design/canvas/viewMath';
import { layeredGeometry, ringGeometry, START_LEAST } from './mapLayout';
import type { MapNode, QuestEdge, Topology } from './topology';

// The map's geometry as a positioned world — one line for every relationship, none lost to a block or a
// filter, no card over another at the widths the window gives, and the ring and the layers the same cards and lines.

const node = (id: string, workspace?: string): MapNode =>
  ({ id, ...(workspace ? { workspace } : {}), owns: [], accepts: [], open: 1, working: false, parked: false, sessions: 0 });
const quest = (from: string, to: string): QuestEdge => ({ from, to, quests: [], open: 1, live: null });
const WORDS = { parked: 'waiting on you', working: 'working' };

/** A circle as large as the biggest real workspace: twenty-nine repositories, quests along a chain with some skipping, both ways once. */
function large(count = 29): Topology {
  const ids = Array.from({ length: count }, (_, at) => `repo-${String(at).padStart(2, '0')}`);
  const at = (n: number) => ids[n % count]!;
  return {
    nodes: ids.map((id) => node(id)),
    quests: [
      ...ids.slice(1, 20).map((id, index) => quest(ids[index]!, id)),
      quest(at(0), at(5)), quest(at(3), at(9)), quest(at(9), at(3)),
    ],
    asks: [{ to: at(1), asks: ['a1'], quests: [], open: 1, live: null }],
    chains: [{ from: at(2), to: at(6), steps: [], waiting: [], live: null }],
    depends: [{ from: at(4), to: at(8) }],
    knowledge: [{ a: at(10), b: at(12), groups: 2 }],
    outside: 0,
    circles: 1,
  };
}

const overlaps = (a: Box, b: Box) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
const edgeCount = (topology: Topology) =>
  topology.quests.length + topology.asks.length + topology.chains.length + topology.depends.length + topology.knowledge.length;

describe('the layers as a positioned world', () => {
  it.each([1280, 624])('lays the largest circle out at %i px with no card over another and a line for every relationship', (width) => {
    const topology = large();
    const geometry = layeredGeometry(topology, width / START_LEAST);
    const boxes = [...geometry.world.boxes.values()];
    for (let a = 0; a < boxes.length; a += 1) for (let b = a + 1; b < boxes.length; b += 1) expect(overlaps(boxes[a]!, boxes[b]!)).toBe(false);
    expect(geometry.lines).toHaveLength(edgeCount(topology));
    expect(new Set(geometry.lines.map((line) => line.id)).size).toBe(geometry.lines.length);
    // The asks stand as a card that is no stop, and every repository is a stop in reading order.
    expect(geometry.world.statics?.size).toBe(1);
    expect(geometry.world.order.filter((id) => !geometry.world.statics!.has(id))).toEqual(topology.nodes.map((each) => each.id));
  });

  it('draws each direction of a pair asked both ways as a line of its own, the paths apart', () => {
    const geometry = layeredGeometry(large(), 1100);
    const there = geometry.lines.find((line) => line.id === 'q:repo-03>repo-09')!;
    const back = geometry.lines.find((line) => line.id === 'q:repo-09>repo-03')!;
    expect(there.d).not.toBe(back.d);
    expect(Math.abs(there.mid.y - back.mid.y)).toBeGreaterThan(22);
    expect(there.selection).toEqual({ kind: 'quests', from: 'repo-03', to: 'repo-09' });
    expect(back.selection).toEqual({ kind: 'quests', from: 'repo-09', to: 'repo-03' });
  });
});

describe('several workspaces', () => {
  const several = (): Topology => {
    const mine = Array.from({ length: 8 }, (_, at) => `m${at}`);
    const yours = Array.from({ length: 8 }, (_, at) => `y${at}`);
    return {
      nodes: [...yours.map((id) => node(id, 'beta')), ...mine.map((id) => node(id, 'alpha'))],
      quests: [quest('m0', 'm1'), quest('m1', 'm2'), quest('y0', 'y1'), quest('m3', 'y2'), quest('y2', 'm3')],
      asks: [{ to: 'm4', asks: ['a'], quests: [], open: 1, live: null }, { to: 'y4', asks: ['b'], quests: [], open: 1, live: null }],
      chains: [{ from: 'm0', to: 'y0', steps: [], waiting: [], live: null }],
      depends: [{ from: 'm5', to: 'm6' }],
      knowledge: [{ a: 'y5', b: 'm7', groups: 1 }],
      outside: 0,
      circles: 2,
    };
  };

  it('stacks a block per workspace by name, each under its title, and keeps every line, the ones between blocks too', () => {
    const topology = several();
    const geometry = layeredGeometry(topology, 1100);
    const { groups } = geometry.world;
    expect(groups?.map((group) => group.title)).toEqual(['alpha', 'beta']);
    expect(groups![0]!.box.y + groups![0]!.box.h).toBeLessThan(groups![1]!.box.y);
    expect(geometry.lines).toHaveLength(edgeCount(topology));
    // Each card stands inside its own workspace's block.
    for (const each of topology.nodes) {
      const block = groups!.find((group) => group.title === each.workspace)!;
      const box = geometry.world.boxes.get(each.id)!;
      expect(box.y).toBeGreaterThanOrEqual(block.box.y);
      expect(box.y + box.h).toBeLessThanOrEqual(block.box.y + block.box.h);
    }
    // The pair asked both ways across the blocks is two lines, each chosen alone.
    expect(geometry.lines.filter((line) => line.id === 'q:m3>y2' || line.id === 'q:y2>m3')).toHaveLength(2);
    expect(geometry.world.groupOf?.('y2')).toBe('beta');
  });

  it('is one workspace\'s map, unchanged, when there is one circle', () => {
    const one = large(14);
    const geometry = layeredGeometry(one, 900);
    expect(geometry.world.groups).toBeUndefined();
  });
});

describe('the ring and the layers', () => {
  it('draw the same repositories and the same lines, so crossing twelve changes the layout and nothing else', () => {
    const topology = large(12);
    const ring = ringGeometry(topology, 200, WORDS);
    const layers = layeredGeometry(topology, 900);
    expect(ring.ring).toBe(true);
    expect(layers.ring).toBe(false);
    expect([...ring.world.boxes.keys()].sort()).toEqual([...layers.world.boxes.keys()].sort());
    expect(ring.lines.map((line) => line.id).sort()).toEqual(layers.lines.map((line) => line.id).sort());
    expect(ring.world.order).toEqual(layers.world.order);
  });
});

import type { FlowEdge, FlowNode } from '../FlowCanvas';
import type { Scene } from './layoutScene';

// The synthetic placed worlds the canvas is measured on: stories and the measured spec only.

/** The ranks' sizes, repeated: a step, a split three ways, two that join. */
const RANKS = [1, 3, 2] as const;

/**
 * A placed world of `count` nodes in ranks of one to three, each node joined to one or two of the rank below, deterministic: the same
 * count gives the same graph. About one and a half edges a node.
 */
export function syntheticFlow(count: number): { nodes: FlowNode[]; edges: FlowEdge[]; entries: string[] } {
  const ranks: string[][] = [];
  for (let made = 0, at = 0; made < count; at += 1) {
    const size = Math.min(RANKS[at % RANKS.length]!, count - made);
    ranks.push(Array.from({ length: size }, (_, index) => `n${made + index}`));
    made += size;
  }
  const nodes = ranks.flat().map((id, index): FlowNode => ({ id, title: `Step ${index + 1}` }));
  const edges: FlowEdge[] = [];
  ranks.forEach((rank, at) => {
    const next = ranks[at + 1];
    if (!next) return;
    rank.forEach((from, index) => {
      const targets = new Set([next[index % next.length]!, next[(index + 1) % next.length]!]);
      for (const to of targets) edges.push({ id: `${from}-${to}`, from, to, says: 'then' });
    });
  });
  return { nodes, edges, entries: [ranks[0]![0]!] };
}

/**
 * A scene of `count` nodes in `groups` contiguous groups (one if none), `edgesPerNode` edges from each: most to a node a few places on in
 * its own group, one in seven to another group, a few back so cycles exist. Deterministic: the same arguments give the same scene. The
 * 5,000 and the 10,000 nodes with 50,000 edges the large-graph stories and specs are measured on.
 */
export function syntheticScene(count: number, { groups = 1, edgesPerNode = 1.5 }: { groups?: number; edgesPerNode?: number } = {}): Scene {
  let seed = 7;
  const next = (below: number) => { seed = (Math.imul(seed, 1103515245) + 12345) >>> 0; return seed % below; };
  const size = Math.ceil(count / groups);
  const grouped = groups > 1;
  const nodes = Array.from({ length: count }, (_, index) => ({
    id: `n${index}`, title: `Step ${index + 1}`, ...(grouped ? { group: `g${Math.floor(index / size)}` } : {}),
  }));
  const edges: Scene['edges'] = [];
  const total = Math.round(count * edgesPerNode);
  for (let made = 0; made < total; made += 1) {
    const from = made % count;
    const reach = next(7);
    const home = Math.floor(from / size) * size;
    // Across groups mostly to a near neighbour, as modules lean on the ones beside them: a few hundred lines, not every pair.
    const near = Math.min(count - 1, Math.max(0, from + (next(7) - 3) * size + next(size)));
    let to = reach === 0 && grouped ? near : reach === 1 ? Math.max(home, from - 1 - next(4)) : Math.min(home + size - 1, count - 1, from + 1 + next(8));
    if (to === from) to = (from + 1) % count;
    edges.push({ id: `e${made}`, from: `n${from}`, to: `n${to}`, kind: made % 3 === 0 ? 'asks' : 'then' });
  }
  return {
    nodes, edges,
    ...(grouped ? { groups: Array.from({ length: groups }, (_, index) => ({ id: `g${index}`, title: `Group ${index + 1}` })) } : {}),
  };
}

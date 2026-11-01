import type { FlowEdge, FlowNode } from '../FlowCanvas';

// The synthetic placed worlds the canvas is measured on (CANVAS1a; the canvas design §5's fixtures): stories and the measured spec only.

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

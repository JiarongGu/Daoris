import { type EdgeStyle, type FlowLayout, layoutFlow, placementFor, type Slot } from '../../design/flowLayout';
import { foldGroups, type GroupFold, groupFolds } from '../../design/groupFolds';
import { type Diagram, placeId } from './roleStructure';

/**
 * **The roles' diagram, laid out** (COMPANYVIEW1f; the company-view design §4): ranked by `design/flowLayout.ts`, whose ranks are here
 * its three columns, places, roles and agents, and placed by its own rule. A node names a role's first choice (*Claude Code · sonnet ·
 * high*), so its floor is 12rem, and across needs three tracks and two 3rem gaps, 42rem: it is across where that fits, and its list
 * otherwise, never top to bottom, which would need about 90rem for eight roles side by side. Pure.
 */

/** A node's track in rem: its floor, and the most it takes where the pane has room. */
export const DIAGRAM_REM = { node: 12, most: 16 } as const;

const COLUMN = { place: 0, role: 1, appointed: 1, agent: 2 } as const;

/** What the layout reads: each node drawn and its column, a workspace folded past 24 nodes, and the edges with their ink. */
export type DiagramShape = {
  nodes: { id: string; column: number; fold: GroupFold | null }[];
  edges: { id: string; from: string; to: string; style: EdgeStyle }[];
  /** Where each place folded away is drawn: its workspace's fold. */
  foldOf: Map<string, string>;
};

/** The diagram as drawn: each workspace past 24 nodes one fold unless opened, a choice that cannot run and a dormant appointment dashed. */
export function diagramShape(diagram: Diagram, opened: ReadonlySet<string> = new Set()): DiagramShape {
  const nodes = diagram.nodes.map((node) => ({ id: node.id, ...(node.kind === 'place' && node.group !== null ? { group: node.group } : {}) }));
  const edges = diagram.edges.map((edge) => ({
    id: edge.id, from: edge.from, to: edge.to, style: (edge.kind !== 'placed' && edge.dashed ? 'conditional' : 'next') as EdgeStyle,
  }));
  const folded = foldGroups({ nodes, edges }, groupFolds({ nodes, edges }, opened, (group) => placeId({ workspace: group })));
  const kindOf = new Map(diagram.nodes.map((node) => [node.id, node.kind]));
  return {
    nodes: folded.nodes.map((node) => ({ id: node.id, column: node.fold ? COLUMN.place : COLUMN[kindOf.get(node.id)!], fold: node.fold })),
    edges: folded.edges,
    foldOf: folded.foldOf,
  };
}

/**
 * The layout: `layoutFlow` ranks by the longest path, which puts every node an edge reaches in its column; a role offered that nobody
 * holds has no edge, and is set in its column rather than among the places. The places keep their declared order (This Daoris, then each
 * workspace before its repositories), which a reader follows; each later column is ordered once by where its nodes' edges come from, a
 * tie in declared order and a node no edge reaches last.
 */
export function layoutDiagram(shape: DiagramShape): FlowLayout {
  const layout = layoutFlow({
    nodes: shape.nodes.map((node) => ({ id: node.id })), edges: shape.edges,
    entries: shape.nodes.filter((node) => node.column === COLUMN.place).map((node) => node.id),
  });
  const touched = new Set(shape.edges.flatMap((edge) => [edge.from, edge.to]));
  const misplaced = new Set(shape.nodes.filter((node) => !touched.has(node.id) && layout.rank[node.id] !== node.column).map((node) => node.id));
  const count = Math.max(layout.ranks.length, ...shape.nodes.filter((node) => misplaced.has(node.id)).map((node) => node.column + 1));
  const ranks: Slot[][] = Array.from({ length: count }, (_, index) =>
    (layout.ranks[index] ?? []).filter((slot) => slot.kind !== 'node' || !misplaced.has(slot.id)));
  const rank = { ...layout.rank };
  for (const node of shape.nodes.filter((each) => misplaced.has(each.id))) {
    ranks[node.column]!.push({ kind: 'node', id: node.id });
    rank[node.id] = node.column;
  }
  const declared = new Map(shape.nodes.map((node, index) => [node.id, index]));
  const key = (slot: Slot) => (slot.kind === 'node' ? slot.id : slot.key);
  ranks[0] = [...ranks[0]!].sort((a, b) => (declared.get(key(a)) ?? 0) - (declared.get(key(b)) ?? 0));
  for (let index = 1; index < ranks.length; index += 1) {
    const before = new Map(ranks[index - 1]!.map((slot, at) => [key(slot), at]));
    const weighed = ranks[index]!.map((slot) => {
      const from = shape.edges.filter((edge) => edge.to === key(slot) && before.has(edge.from)).map((edge) => before.get(edge.from)!);
      return { slot, weight: from.length > 0 ? from.reduce((sum, value) => sum + value, 0) / from.length : Infinity };
    });
    ranks[index] = weighed
      .sort((a, b) => (a.weight === b.weight ? 0 : a.weight - b.weight) || (declared.get(key(a.slot)) ?? 0) - (declared.get(key(b.slot)) ?? 0))
      .map(({ slot }) => slot);
  }
  const order = ranks.flatMap((slots) => slots.flatMap((slot) => (slot.kind === 'node' ? [slot.id] : [])));
  return { ...layout, ranks, rank, order };
}

/** Across where its columns fit at their floor, however tall each is; else, and wherever nothing measured or the list was chosen, its list. */
export function diagramPlacement(layout: FlowLayout, width: number | undefined, asList = false): 'across' | 'rail' {
  return placementFor(layout, width, asList, 16, { least: DIAGRAM_REM.node, column: Infinity }) === 'across' ? 'across' : 'rail';
}

/** A node track's width across, in px: what the pane leaves each column after its gaps, from the floor to the most. */
export function diagramTrack(layout: FlowLayout, width: number): number {
  const ranks = Math.max(1, layout.ranks.length);
  const room = width - (ranks - 1) * 3 * 16;
  return Math.max(DIAGRAM_REM.node * 16, Math.min(DIAGRAM_REM.most * 16, Math.floor(room / ranks)));
}

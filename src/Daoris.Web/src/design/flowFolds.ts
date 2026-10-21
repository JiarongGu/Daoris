import { findStacks, type FlowShape, layoutFlow, type FlowLayout } from './flowLayout';

/**
 * **A flow too large to show whole** (FLOWCHART1d; D186 point 7, the flowchart design §3.6 and trap 16): which nodes fold, as a
 * pure function of the graph's shape, what is current and settled, and which folds the person opened. `Flow` draws each fold as
 * one node with a press, every edge across its boundary drawn to it with its cause, those between its members said in words.
 *
 * @remarks
 * A flow is drawn whole up to 24 nodes, D157's authoring bound; a decoded graph may reach 96 with its synthetic nodes. Past
 * that, after the stacks (`findStacks`, at every size, FLOWCHART1c), in this order:
 * 1. **Settled stretches.** Four or more consecutive done nodes (a caller's `settled`; a step passed over is not, so a skip stays
 *    in view), each a link of a chain (one edge in and one out), fold into *n steps done*, as far as they end two ranks or more
 *    before every current node.
 * 2. **Focus.** If it is still past 24, the ranks from two before to three after each current node are drawn, their union where
 *    several are current. With none current: the ranks after the entries where nothing has started, those before the end where
 *    all has settled, and otherwise those around the furthest rank any node was reached at, where the run got to. *Earlier*,
 *    *between* and *later* folds hold the rest, each two nodes or more, since a fold of one hides nothing.
 *
 * **The one way a change of state moves anything** (§3.1 step 5, §3.6): the folds follow the state, so a node that becomes current
 * may open or close a fold. A fold the person opened stays open by its id: *earlier* and *later* by their place, a settled
 * stretch and a fold between by their first member.
 */

/** Drawn whole up to this many nodes (§3.6). */
export const WHOLE_MOST = 24;
/** A settled stretch folds from four (§3.6 step 2). */
const SETTLED_LEAST = 4;
/** The focus draws two ranks before each current node and three after (§3.6 step 3). */
const FOCUS = { before: 2, after: 3 } as const;
/** A fold of fewer hides nothing worth its press. */
const FOLD_LEAST = 2;
const SETTLED = '\u0000settled';

/** What the folds read: the shape, and of each node whether it is current, settled (done or passed over) and reached. */
export type FoldShape = {
  nodes: readonly { id: string; settled?: boolean; current?: boolean; reached?: boolean }[];
  edges: FlowShape['edges'];
  entries: readonly string[];
};

/** A settled stretch, or the ranks before, between or after the focus. */
export type FoldKind = 'done' | 'earlier' | 'between' | 'later';

/** One fold: its members in declared order, and the edges between them, which its description says. */
export type Fold = { id: string; kind: FoldKind; members: string[]; within: string[] };

/** The folds a flow past 24 nodes draws, settled stretches first; none for a flow drawn whole. */
export function largeFolds(shape: FoldShape, opened: ReadonlySet<string> = new Set()): Fold[] {
  if (shape.nodes.length <= WHOLE_MOST) return [];
  const folds: Fold[] = [];

  // 1. Settled stretches: a chain of settled links, as far as it ends two ranks before every current node.
  const first = layoutFlow(shape);
  const limit = Math.min(...anchorsOf(shape.nodes, first)) - FOCUS.before;
  const stretches = findStacks({
    nodes: shape.nodes.map((node) => (node.settled ? { id: node.id, stack: SETTLED } : { id: node.id })),
    edges: shape.edges,
    entries: shape.entries,
  });
  for (const stretch of stretches) {
    // A chain's ranks rise along it, so what ends in time is a run from its start.
    const members = stretch.members.filter((id) => first.rank[id]! <= limit);
    const id = `fold:done:${members[0]}`;
    if (members.length < SETTLED_LEAST || opened.has(id)) continue;
    folds.push({ id, kind: 'done', members, within: stretch.within.slice(0, members.length - 1) });
  }

  const folded = collapse(shape, folds);
  if (folded.nodes.length <= WHOLE_MOST) return folds;

  // 2. Focus: the ranks around each anchor are drawn, and each run of the others, two nodes or more, is one fold.
  const layout = layoutFlow(folded);
  const count = layout.ranks.length;
  const keep = new Set<number>();
  for (const anchor of anchorsOf(folded.nodes, layout)) {
    for (let rank = anchor - FOCUS.before; rank <= anchor + FOCUS.after; rank += 1) keep.add(rank);
  }
  const runs: [number, number][] = [];
  let start = -1;
  for (let rank = 0; rank <= count; rank += 1) {
    const out = rank < count && !keep.has(rank);
    if (out && start < 0) start = rank;
    if (!out && start >= 0) { runs.push([start, rank - 1]); start = -1; }
  }
  const settledFold = new Map(folds.map((fold) => [fold.id, fold]));
  const ranged: Fold[] = [];
  for (const [low, high] of runs) {
    const there = folded.nodes.filter((node) => layout.rank[node.id]! >= low && layout.rank[node.id]! <= high).map((node) => node.id);
    if (there.length < FOLD_LEAST) continue;
    // A settled stretch inside it is held whole by it.
    const members = there.flatMap((id) => settledFold.get(id)?.members ?? [id]);
    const kind: FoldKind = low === 0 ? 'earlier' : high === count - 1 ? 'later' : 'between';
    const id = kind === 'between' ? `fold:between:${members[0]}` : `fold:${kind}`;
    if (opened.has(id)) continue;
    for (const inner of there) settledFold.delete(inner);
    const held = new Set(members);
    const within = shape.edges.filter((edge) => held.has(edge.from) && held.has(edge.to)).map((edge) => edge.id);
    ranged.push({ id, kind, members, within });
  }
  return [...folds.filter((fold) => settledFold.has(fold.id)), ...ranged];
}

/**
 * The ranks the focus stands on: each current node's; with none current, after the entries where nothing has started, else the
 * furthest rank any node was reached or settled at, no nearer the end than leaves the focus its six ranks: before the end where all
 * has settled, or a step passed over beside them (a skip is not done, so it is not settled), and where the run got to otherwise.
 */
function anchorsOf(nodes: FoldShape['nodes'], layout: FlowLayout): number[] {
  const count = layout.ranks.length;
  const current = nodes.filter((node) => node.current).map((node) => layout.rank[node.id]!);
  if (current.length > 0) return current;
  const reached = nodes.filter((node) => node.settled || node.reached === true).map((node) => layout.rank[node.id]!);
  if (reached.length === 0) return [FOCUS.before];
  return [Math.min(Math.max(...reached), count - 1 - FOCUS.after)];
}

/** The shape with each fold drawn as one node in its first member's place: settled where all of its members are. */
function collapse(shape: FoldShape, folds: readonly Fold[]): FoldShape {
  if (folds.length === 0) return shape;
  const byId = new Map(shape.nodes.map((node) => [node.id, node]));
  const foldOf = new Map(folds.flatMap((fold) => fold.members.map((member) => [member, fold] as const)));
  const nodes = shape.nodes.flatMap((node) => {
    const fold = foldOf.get(node.id);
    if (!fold) return [node];
    if (fold.members[0] !== node.id) return [];
    const members = fold.members.map((member) => byId.get(member)!);
    return [{
      id: fold.id,
      settled: members.every((member) => member.settled),
      current: members.some((member) => member.current),
      ...(members.every((member) => member.reached === true) ? { reached: true } : {}),
    }];
  });
  const edges = shape.edges.flatMap((edge) => {
    const from = foldOf.get(edge.from)?.id ?? edge.from;
    const to = foldOf.get(edge.to)?.id ?? edge.to;
    if (from === to && foldOf.has(edge.from)) return [];
    return [{ ...edge, from, to }];
  });
  return { nodes, edges, entries: [...new Set(shape.entries.map((id) => foldOf.get(id)?.id ?? id))] };
}

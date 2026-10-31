import type { EdgeStyle } from './flowLayout';
import { WHOLE_MOST } from './flowFolds';

/**
 * **A drawing of places too large to show whole** (COMPANYVIEW1f; the company-view design §4, *Legible at size*): past 24 nodes, each
 * group of nodes (a workspace and its repositories) folds to one node, as a pure function of the nodes' groups, the edges and which
 * folds the person opened. `flowFolds.ts` folds a process by its stacks, settled stretches and focus; places have none of those.
 *
 * @remarks
 * A fold keeps its boundary, as a flow's does (the flowchart design §3.6): every edge across it is drawn to the fold under its own
 * id, so a layout merges those of one style and one other end into one path and counts them; an edge between two members is said in
 * the fold's description. Nothing is drawn smaller to fit.
 */

/** What the folds read: each node with the group it is drawn in, if any, and the edges. */
export type GroupShape = {
  nodes: readonly { id: string; group?: string }[];
  edges: readonly { id: string; from: string; to: string; style?: EdgeStyle }[];
};

/** One group folded: its members in declared order and the edges between them. */
export type GroupFold = { id: string; group: string; members: string[]; within: string[] };

/** A fold of fewer hides nothing worth its press. */
const FOLD_LEAST = 2;

/** The folds of a drawing past 24 nodes, one per group of two or more not opened; none for a drawing drawn whole. */
export function groupFolds(
  shape: GroupShape, opened: ReadonlySet<string> = new Set(), foldId: (group: string) => string = (group) => `fold:${group}`,
): GroupFold[] {
  if (shape.nodes.length <= WHOLE_MOST) return [];
  const groups = new Map<string, string[]>();
  for (const node of shape.nodes) if (node.group !== undefined) groups.set(node.group, [...(groups.get(node.group) ?? []), node.id]);
  const folds: GroupFold[] = [];
  for (const [group, members] of groups) {
    const id = foldId(group);
    if (members.length < FOLD_LEAST || opened.has(id)) continue;
    const held = new Set(members);
    folds.push({ id, group, members, within: shape.edges.filter((edge) => held.has(edge.from) && held.has(edge.to)).map((edge) => edge.id) });
  }
  return folds;
}

/** The drawing with each fold as one node in its first member's place, and where each member is drawn. */
export function foldGroups<E extends GroupShape['edges'][number]>(
  shape: { nodes: GroupShape['nodes']; edges: readonly E[] }, folds: readonly GroupFold[],
): { nodes: { id: string; fold: GroupFold | null }[]; edges: E[]; foldOf: Map<string, string> } {
  const foldOf = new Map(folds.flatMap((fold) => fold.members.map((member) => [member, fold.id] as const)));
  const byId = new Map(folds.map((fold) => [fold.id, fold]));
  const nodes = shape.nodes.flatMap((node) => {
    const fold = foldOf.get(node.id);
    if (fold === undefined) return [{ id: node.id, fold: null }];
    return byId.get(fold)!.members[0] === node.id ? [{ id: fold, fold: byId.get(fold)! }] : [];
  });
  const within = new Set(folds.flatMap((fold) => fold.within));
  const edges = shape.edges.filter((edge) => !within.has(edge.id)).map((edge) => {
    const from = foldOf.get(edge.from) ?? edge.from;
    const to = foldOf.get(edge.to) ?? edge.to;
    return from === edge.from && to === edge.to ? edge : { ...edge, from, to };
  });
  return { nodes, edges, foldOf };
}

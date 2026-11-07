import { canonicalJson } from '../draftFingerprint';
import type { DraftNode, GraphDraft } from './graphDraft';

// What a save changes: the steps a draft adds, takes out or changes against the version it began from,
// the count of ways that differ, and the key an append's consent is bound to. Pure.

export type StepChanges = { added: DraftNode[]; removed: DraftNode[]; changed: DraftNode[]; ways: number };

/** The draft against the one it began from (none: a start from nothing, all new). A step changed is one of the same id that says otherwise. */
export function changesBetween(base: GraphDraft | null, next: GraphDraft): StepChanges {
  if (base === null) return { added: next.nodes, removed: [], changed: [], ways: next.wires.length };
  const was = new Map(base.nodes.map((node) => [node.id, canonicalJson(node.node)]));
  const now = new Map(next.nodes.map((node) => [node.id, canonicalJson(node.node)]));
  const wires = (draft: GraphDraft) => new Set(draft.wires.map((wire) => canonicalJson(wire)));
  const before = wires(base);
  const after = wires(next);
  let ways = 0;
  for (const each of after) if (!before.has(each)) ways += 1;
  for (const each of before) if (!after.has(each)) ways += 1;
  // The end, which a removed or added step moves, is part of how the steps go on.
  if (base.end !== next.end) ways += 1;
  return {
    added: next.nodes.filter((node) => !was.has(node.id)),
    removed: base.nodes.filter((node) => !now.has(node.id)),
    changed: next.nodes.filter((node) => was.has(node.id) && was.get(node.id) !== now.get(node.id)),
    ways,
  };
}

/** One key for the places an append reaches, whatever their order: consent given to it holds for exactly that set. */
export const consentKey = (scopes: readonly { key: string }[]): string => scopes.map((scope) => scope.key).sort().join('\n');

import { type CodeTree, ROOT } from './codeClusters';

// One folder folded alone (CANVAS1f; the canvas design §4, one mechanism folds). Pure. *Show every folder* and *Fold the folders again*
// move every group at once; this closes the one group a chosen module is drawn in, and what lies beneath it, and leaves the rest open.

/** The open group a module is drawn in directly (its folder, or its cycle), which *Fold* closes; `null` at the root or when closed. */
export function foldable(tree: CodeTree, open: ReadonlySet<string>, module: string): string | null {
  const group = tree.owner.get(module);
  return group !== undefined && group !== ROOT && open.has(group) ? group : null;
}

/** `open` with `group` closed, and every group beneath it, the rest as they were. */
export function foldGroup(tree: CodeTree, open: ReadonlySet<string>, group: string): Set<string> {
  const next = new Set(open);
  const close = (id: string) => {
    next.delete(id);
    for (const child of tree.groups.get(id)?.groups ?? []) close(child);
  };
  close(group);
  return next;
}

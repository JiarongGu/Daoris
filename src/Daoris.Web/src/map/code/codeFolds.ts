import type { CodeTree } from './codeClusters';

// One folder folded alone. Pure. *Show every folder* and *Fold the folders again*
// move every group at once; this closes the one group an open frame's header names, and what lies beneath it, and leaves the rest open.

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

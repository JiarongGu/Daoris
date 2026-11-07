import type { TreeGroup } from '../list/mapTreeModel';
import { CLOSED_PAST, type CodeTree, defaultOpen, ROOT } from './codeClusters';

// The code map as a tree: one group per folder by path, closed past 60 modules as the drawing is, each
// module a row in the folder that holds it, and a cycle a group of its own under the folder nearest it.

const byName = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

export function codeListGroups(tree: CodeTree, words: {
  /** A cycle's title, by its size and the folders it crosses. */
  cycle: (count: number, folders: number) => string;
  /** A module's row: its id with what it uses and what uses it. */
  module: (id: string) => string;
}): TreeGroup[] {
  const open = defaultOpen(tree);
  const row = (id: string) => ({ id, title: words.module(id) });
  const folders = new Map<string, { items: string[]; cycles: TreeGroup[] }>();
  const home = (id: string) => { let held = folders.get(id); if (!held) { held = { items: [], cycles: [] }; folders.set(id, held); } return held; };
  for (const [id, owner] of tree.owner) {
    const group = tree.groups.get(owner)!;
    if (group.kind === 'folder') home(owner).items.push(id);
  }
  for (const group of tree.groups.values()) {
    if (group.kind !== 'cycle') continue;
    home(group.parent!).cycles.push({
      id: group.id, title: words.cycle(group.modules.length, group.across?.length ?? 1), open: open.has(group.id),
      items: [...group.modules].sort(byName).map(row),
    });
  }
  return [...folders]
    .map(([id, held]) => ({
      id, title: tree.groups.get(id)!.dir || '/', open: id === ROOT ? tree.modules.size <= CLOSED_PAST : open.has(id),
      items: held.items.sort(byName).map(row), groups: held.cycles,
    }))
    .sort((a, b) => byName(a.title, b.title));
}

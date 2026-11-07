import type { PlaceRow, PlacesRead } from '../../data/wiring';
import { isComposing, type KeyPress } from '../../lib/composing';
import { MACHINE, repositoryId, workspaceId } from '../modes';

/** The place a row stands for, as the list's chosen item names it. */
export const placeIdOf = (row: PlaceRow): string =>
  row.repository ? repositoryId(row.workspace, row.repository) : row.workspace ? workspaceId(row.workspace) : MACHINE;

/** The Setup mode's places as the view holds them: the search, the filter, the open fold and the page asked, and the driver's answer. */
export type SetupState = {
  search: string;
  onSearch: (search: string) => void;
  needsLook: boolean;
  onNeedsLook: (on: boolean) => void;
  /** The workspace whose not-driven repositories are open, and the page of them. */
  fold: string | null;
  onFold: (workspace: string | null) => void;
  /** The cursor of the open fold's page; none on its first. */
  foldAfter: string | null;
  onFoldAfter: (after: string | null) => void;
  onMore: () => void;
  read: PlacesRead | null;
  /** `loading` until the first answer; `failed` where the shell does not offer places or could not read them. */
  status: 'loading' | 'failed' | 'ok';
};

/** One row of the places tree as the person sees it now, in the order they read: what its keys need and nothing of how it is drawn. */
export type TreeItem = {
  id: string;
  kind: 'machine' | 'workspace' | 'repository' | 'fold' | 'imported';
  level: 1 | 2 | 3;
  parent: string | null;
  /** Whether it has children to show or hide; a leaf has none. */
  expandable: boolean;
  expanded: boolean;
};

/** The id a workspace's *imported, not driven* row is held under; it opens the paged read and chooses no place. */
export const foldId = (workspace: string): string => `fold:${workspace}`;

/**
 * The rows in view, flat and in reading order: *This machine*, then each workspace, its driven repositories and its imported fold, and
 * under an open fold its page of imported ones. A collapsed workspace shows none of its own.
 */
export function treeItems(read: PlacesRead, collapsed: ReadonlySet<string>, fold: string | null): TreeItem[] {
  const items: TreeItem[] = [{ id: MACHINE, kind: 'machine', level: 1, parent: null, expandable: false, expanded: false }];
  for (const { place, notDriven } of read.workspaces) {
    const workspace = place.workspace ?? '';
    const id = workspaceId(workspace);
    const own = read.repositories.filter((row) => row.workspace === workspace);
    const expanded = !collapsed.has(id);
    items.push({ id, kind: 'workspace', level: 1, parent: null, expandable: own.length > 0 || notDriven > 0, expanded });
    if (!expanded) continue;
    for (const row of own) items.push({ id: placeIdOf(row), kind: 'repository', level: 2, parent: id, expandable: false, expanded: false });
    if (notDriven === 0) continue;
    const open = fold === workspace;
    items.push({ id: foldId(workspace), kind: 'fold', level: 2, parent: id, expandable: true, expanded: open });
    if (open && read.undriven?.workspace === workspace) {
      for (const row of read.undriven.rows) {
        items.push({ id: repositoryId(row.workspace, row.repository), kind: 'imported', level: 3, parent: foldId(workspace), expandable: false, expanded: false });
      }
    }
  }
  return items;
}

/** What a key asks of the tree: focus another row, open or close one, or choose one. */
export type TreeMove = { focus?: string; toggle?: string; choose?: string };

/**
 * The tree's keys, the WAI tree pattern's: ↓ and ↑ walk the rows in view, Home and End go to either end, → opens a closed row and
 * then enters it, ← closes an open row and then leaves for its parent, and Enter or Space chooses (a fold toggles instead, since it is no
 * place). Null where the key is not the tree's, or is an input method's.
 */
export function keyInTree(items: readonly TreeItem[], current: string, press: KeyPress & { key: string }): TreeMove | null {
  if (isComposing(press)) return null;
  const at = items.findIndex((each) => each.id === current);
  const item = items[at];
  if (!item) return null;
  switch (press.key) {
    case 'ArrowDown': return at < items.length - 1 ? { focus: items[at + 1]!.id } : null;
    case 'ArrowUp': return at > 0 ? { focus: items[at - 1]!.id } : null;
    case 'Home': return { focus: items[0]!.id };
    case 'End': return { focus: items[items.length - 1]!.id };
    case 'ArrowRight': {
      if (!item.expandable) return null;
      if (!item.expanded) return { toggle: item.id };
      const child = items[at + 1];
      return child && child.parent === item.id ? { focus: child.id } : null;
    }
    case 'ArrowLeft':
      if (item.expandable && item.expanded) return { toggle: item.id };
      return item.parent ? { focus: item.parent } : null;
    case 'Enter':
    case ' ':
      return item.kind === 'fold' ? { toggle: item.id } : { choose: item.id };
    default: return null;
  }
}

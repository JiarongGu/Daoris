import { isComposing, type KeyPress } from '../../lib/composing';

// The Map's list as one tree on both levels (MAPUX1e; the map UX design §6, §7): the WAI tree pattern the places tree of Agents → Setup
// keeps (`agents/setup/setupTreeModel.ts`). Pure: the groups in hand and the ones the person opened become the rows in view, and a key
// becomes what the tree does with it.

/** A group of rows: a heading that opens and closes, its items, and the groups it holds (a cycle under its folder). */
export type TreeGroup = {
  id: string;
  title: string;
  items: readonly { id: string; title: string }[];
  groups?: readonly TreeGroup[];
  /** Whether it opens drawn: true unless said (a code map past 60 modules opens its folders closed). */
  open?: boolean;
};

/** One row as the person sees it now, in the order they read: what its keys need and nothing of how it is drawn. */
export type TreeRow = {
  id: string;
  title: string;
  kind: 'group' | 'item';
  level: 1 | 2 | 3;
  parent: string | null;
  /** The group's item count, said on its heading. */
  count: number;
  expandable: boolean;
  expanded: boolean;
};

/** A group's row id: apart from an item's, since a repository may be named as a group is (`quests`). */
export const groupRow = (id: string): string => `group:${id}`;

/** The row ids of the groups drawn open at the start: those that open by default, and any that hold the chosen item. */
export function openedAtStart(groups: readonly TreeGroup[], selected: string | null): Set<string> {
  const open = new Set<string>();
  const walk = (group: TreeGroup): boolean => {
    const holds = [...(group.groups ?? [])].map(walk).includes(true) || group.items.some((item) => item.id === selected);
    if (group.open !== false || holds) open.add(groupRow(group.id));
    return holds;
  };
  for (const group of groups) walk(group);
  return open;
}

/** The rows in view, flat and in reading order: each group's heading, and under an open one its groups and then its items. */
export function treeRows(groups: readonly TreeGroup[], open: ReadonlySet<string>): TreeRow[] {
  const rows: TreeRow[] = [];
  const add = (group: TreeGroup, level: 1 | 2, parent: string | null) => {
    const id = groupRow(group.id);
    const expanded = open.has(id);
    const count = group.items.length + (group.groups ?? []).reduce((sum, each) => sum + each.items.length, 0);
    rows.push({ id, title: group.title, kind: 'group', level, parent, count, expandable: true, expanded });
    if (!expanded) return;
    for (const each of group.groups ?? []) add(each, 2, id);
    for (const item of group.items) rows.push({ id: item.id, title: item.title, kind: 'item', level: level === 1 ? 2 : 3, parent: id, count: 0, expandable: false, expanded: false });
  };
  for (const group of groups) add(group, 1, null);
  return rows;
}

/** What a key asks of the tree: focus another row, open or close a group, choose an item, or release the choice. */
export type TreeMove = { focus?: string; toggle?: string; choose?: string; release?: true };

/**
 * ↓ and ↑ walk the rows in view, Home and End go to either end, → opens a closed group and then enters it, ← closes an open group and
 * then leaves for the group above, Enter or Space chooses an item (a group opens or closes instead), and Escape releases the choice.
 * Null where the key is not the tree's, or is an input method's.
 */
export function keyInTree(rows: readonly TreeRow[], current: string, press: KeyPress & { key: string }, chosen: string | null): TreeMove | null {
  if (isComposing(press)) return null;
  if (press.key === 'Escape') return chosen === null ? null : { release: true };
  const at = rows.findIndex((each) => each.id === current);
  const row = rows[at];
  if (!row) return null;
  switch (press.key) {
    case 'ArrowDown': return at < rows.length - 1 ? { focus: rows[at + 1]!.id } : null;
    case 'ArrowUp': return at > 0 ? { focus: rows[at - 1]!.id } : null;
    case 'Home': return { focus: rows[0]!.id };
    case 'End': return { focus: rows[rows.length - 1]!.id };
    case 'ArrowRight': {
      if (!row.expandable) return null;
      if (!row.expanded) return { toggle: row.id };
      const child = rows[at + 1];
      return child && child.parent === row.id ? { focus: child.id } : null;
    }
    case 'ArrowLeft':
      if (row.expandable && row.expanded) return { toggle: row.id };
      return row.parent ? { focus: row.parent } : null;
    case 'Enter':
    case ' ':
      return row.kind === 'group' ? { toggle: row.id } : { choose: row.id };
    default: return null;
  }
}

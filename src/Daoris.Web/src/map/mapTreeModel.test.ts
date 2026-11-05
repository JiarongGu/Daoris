import { describe, expect, it } from 'vitest';
import { groupRow, keyInTree, openedAtStart, type TreeGroup, treeRows } from './mapTreeModel';

// MAPUX1e (the map UX design §6): the Map's list as the WAI tree, its rows and its keys.

const GROUPS: TreeGroup[] = [
  { id: 'repositories', title: 'Repositories', items: [{ id: 'a', title: 'a' }, { id: 'b', title: 'b' }] },
  { id: 'quests', title: 'Quests', items: [{ id: 'a>b', title: 'a → b' }], open: false },
  {
    id: 'folder:src', title: 'src', items: [{ id: 'm', title: 'm' }],
    groups: [{ id: 'cycle', title: 'cycle of 2', items: [{ id: 'x', title: 'x' }, { id: 'y', title: 'y' }] }],
  },
];
const press = (key: string) => ({ key, keyCode: 0 });

describe('the rows in view', () => {
  it('reads each open group\'s heading, its groups, then its items, and a closed group\'s heading alone', () => {
    const rows = treeRows(GROUPS, openedAtStart(GROUPS, null));
    expect(rows.map((row) => row.id)).toEqual([
      groupRow('repositories'), 'a', 'b', groupRow('quests'), groupRow('folder:src'), groupRow('cycle'), 'x', 'y', 'm',
    ]);
    expect(rows.map((row) => row.level)).toEqual([1, 2, 2, 1, 1, 2, 3, 3, 2]);
    expect(rows.find((row) => row.id === groupRow('folder:src'))!.count).toBe(3);
  });

  it('opens the group that holds the chosen item, and keeps an item named as a group apart from it', () => {
    expect(openedAtStart(GROUPS, 'a>b').has(groupRow('quests'))).toBe(true);
    const clash: TreeGroup[] = [{ id: 'quests', title: 'Quests', items: [{ id: 'quests', title: 'quests' }] }];
    expect(treeRows(clash, openedAtStart(clash, null)).map((row) => row.id)).toEqual([groupRow('quests'), 'quests']);
  });
});

describe('the keys', () => {
  const rows = treeRows(GROUPS, openedAtStart(GROUPS, null));
  const at = (id: string, key: string, chosen: string | null = null) => keyInTree(rows, id, press(key), chosen);

  it('walks with the arrows, Home and End', () => {
    expect(at('a', 'ArrowDown')).toEqual({ focus: 'b' });
    expect(at('b', 'ArrowUp')).toEqual({ focus: 'a' });
    expect(at('b', 'Home')).toEqual({ focus: groupRow('repositories') });
    expect(at('b', 'End')).toEqual({ focus: 'm' });
    expect(at('m', 'ArrowDown')).toBeNull();
  });

  it('opens a closed group with →, enters an open one, closes with ←, and climbs to the group above', () => {
    expect(at(groupRow('quests'), 'ArrowRight')).toEqual({ toggle: groupRow('quests') });
    expect(at(groupRow('repositories'), 'ArrowRight')).toEqual({ focus: 'a' });
    expect(at(groupRow('repositories'), 'ArrowLeft')).toEqual({ toggle: groupRow('repositories') });
    expect(at('a', 'ArrowLeft')).toEqual({ focus: groupRow('repositories') });
    expect(at('x', 'ArrowLeft')).toEqual({ focus: groupRow('cycle') });
  });

  it('chooses an item on Enter and Space, opens or closes a group on them, and releases on Escape', () => {
    expect(at('a', 'Enter')).toEqual({ choose: 'a' });
    expect(at('a', ' ')).toEqual({ choose: 'a' });
    expect(at(groupRow('quests'), 'Enter')).toEqual({ toggle: groupRow('quests') });
    expect(at('a', 'Escape', 'a')).toEqual({ release: true });
    expect(at('a', 'Escape')).toBeNull();
  });

  it('leaves a key an input method is composing with', () => {
    expect(keyInTree(rows, 'a', { key: 'Enter', keyCode: 229 }, null)).toBeNull();
    expect(keyInTree(rows, 'a', { key: 'Enter', keyCode: 13, isComposing: true }, null)).toBeNull();
  });
});

import { describe, expect, it } from 'vitest';
import type { PlaceRow, PlacesRead } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { placeLine } from './SetupList';
import { keyInTree, treeItems } from './setupTreeModel';

// SETUPUX1i: the places tree's rows in reading order, its WAI-pattern keys, and a row's second line.

const place = (patch: Partial<PlaceRow> = {}): PlaceRow => ({
  workspace: 'aurora', repository: 'engine', driven: true, held: false, trees: false, checkout: true, adopted: true, works: [], needs: [], ...patch,
});

const read = (open = false): PlacesRead => ({
  machine: place({ workspace: null, repository: null }),
  workspaces: [{ place: place({ repository: null }), driven: 2, notDriven: 3 }],
  repositories: [place({ repository: 'engine' }), place({ repository: 'site' })],
  next: null,
  matching: 2,
  undriven: open ? { workspace: 'aurora', rows: [{ workspace: 'aurora', repository: 'old', checkout: true }], next: null, total: 3 } : null,
  detail: null,
  members: null,
});

const ids = (items: ReturnType<typeof treeItems>) => items.map((each) => each.id);

describe('treeItems', () => {
  it('lists the machine, each workspace with its repositories and its fold, and a page under an open fold', () => {
    expect(ids(treeItems(read(), new Set(), null))).toEqual(['machine', 'workspace:aurora', 'repo:aurora/engine', 'repo:aurora/site', 'fold:aurora']);
    const open = treeItems(read(true), new Set(), 'aurora');
    expect(ids(open).slice(-2)).toEqual(['fold:aurora', 'repo:aurora/old']);
    expect(open.at(-1)).toMatchObject({ level: 3, parent: 'fold:aurora' });
  });

  it('shows none of a collapsed workspace', () => {
    expect(ids(treeItems(read(), new Set(['workspace:aurora']), null))).toEqual(['machine', 'workspace:aurora']);
  });
});

describe('keyInTree', () => {
  const items = treeItems(read(true), new Set(), 'aurora');
  it('walks, and stops at either end', () => {
    expect(keyInTree(items, 'machine', 'ArrowDown')).toEqual({ focus: 'workspace:aurora' });
    expect(keyInTree(items, 'machine', 'ArrowUp')).toBeNull();
    expect(keyInTree(items, 'machine', 'End')).toEqual({ focus: 'repo:aurora/old' });
    expect(keyInTree(items, 'repo:aurora/old', 'ArrowDown')).toBeNull();
    expect(keyInTree(items, 'repo:aurora/old', 'Home')).toEqual({ focus: 'machine' });
  });

  it('opens a closed row, then enters it; closes an open one, then leaves for the parent', () => {
    const shut = treeItems(read(), new Set(['workspace:aurora']), null);
    expect(keyInTree(shut, 'workspace:aurora', 'ArrowRight')).toEqual({ toggle: 'workspace:aurora' });
    expect(keyInTree(items, 'workspace:aurora', 'ArrowRight')).toEqual({ focus: 'repo:aurora/engine' });
    expect(keyInTree(items, 'workspace:aurora', 'ArrowLeft')).toEqual({ toggle: 'workspace:aurora' });
    expect(keyInTree(items, 'repo:aurora/engine', 'ArrowLeft')).toEqual({ focus: 'workspace:aurora' });
    expect(keyInTree(items, 'repo:aurora/engine', 'ArrowRight')).toBeNull();
    expect(keyInTree(items, 'repo:aurora/old', 'ArrowLeft')).toEqual({ focus: 'fold:aurora' });
  });

  it('chooses a place on Enter or Space, and toggles a fold, which is no place', () => {
    expect(keyInTree(items, 'repo:aurora/engine', 'Enter')).toEqual({ choose: 'repo:aurora/engine' });
    expect(keyInTree(items, 'workspace:aurora', ' ')).toEqual({ choose: 'workspace:aurora' });
    expect(keyInTree(items, 'fold:aurora', 'Enter')).toEqual({ toggle: 'fold:aurora' });
    expect(keyInTree(items, 'machine', 'a')).toBeNull();
  });
});

describe('placeLine', () => {
  const t = i18n.getFixedT('en');
  it('puts a need first, then a held start, then adoption running, then how it lands', () => {
    expect(placeLine(place({ needs: [{ code: 'no-checkout' }], held: true }), t)).toEqual({ text: '⚠ No checkout on this machine', need: true });
    expect(placeLine(place({ held: true }), t)).toEqual({ text: 'Held', need: false });
    expect(placeLine(place({ driven: false, adoption: 'running' }), t)).toEqual({ text: 'Adoption running', need: false });
    expect(placeLine(place({ driven: false }), t).text).toBeNull();
  });
});

import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import type { PlaceRow, PlacesRead } from '../../data/wiring';
import { findPlace } from './SetupPlaces';
import { useKeptPlace } from './useKeptPlace';

// The place chosen in the tree is kept across its pages and searches.

const place = (patch: Partial<PlaceRow> = {}): PlaceRow => ({
  workspace: 'aurora', repository: 'engine', driven: true, held: false, trees: false, checkout: true, adopted: true, works: [], needs: [], ...patch,
});
const read = (repositories: string[], undriven: string[] | null = null): PlacesRead => ({
  machine: place({ workspace: null, repository: null }),
  workspaces: [{ place: place({ repository: null }), driven: repositories.length, notDriven: 3 }],
  repositories: repositories.map((repository) => place({ repository })),
  next: null, matching: repositories.length,
  undriven: undriven ? { workspace: 'aurora', rows: undriven.map((repository) => ({ workspace: 'aurora', repository, checkout: true })), next: null, total: 3 } : null,
  detail: null, members: null,
});

const props = (initial: { read: PlacesRead; id: string | null; narrowed: boolean }) =>
  renderHook((now: typeof initial) => useKeptPlace(findPlace(now.read, now.id), now.id, now.narrowed), { initialProps: initial });

describe('the kept place', () => {
  it('stays while a search leaves its row out, and goes when an unnarrowed answer lacks it', () => {
    const view = props({ read: read(['engine', 'site']), id: 'repo:aurora/engine', narrowed: false });
    expect(view.result.current).toMatchObject({ kind: 'repository' });
    view.rerender({ read: read(['site']), id: 'repo:aurora/engine', narrowed: true });
    expect(view.result.current).toMatchObject({ kind: 'repository', row: { repository: 'engine' } });
    view.rerender({ read: read(['site']), id: 'repo:aurora/engine', narrowed: false });
    expect(view.result.current).toBeNull();
  });

  it('keeps an imported repository across the pages and the closing of its fold', () => {
    const view = props({ read: read(['engine'], ['old', 'older']), id: 'repo:aurora/old', narrowed: false });
    expect(view.result.current).toMatchObject({ kind: 'imported', repository: 'old' });
    view.rerender({ read: read(['engine'], ['newer']), id: 'repo:aurora/old', narrowed: false });
    expect(view.result.current).toMatchObject({ kind: 'imported', repository: 'old' });
    view.rerender({ read: read(['engine']), id: 'repo:aurora/old', narrowed: false });
    expect(view.result.current).toMatchObject({ kind: 'imported', repository: 'old' });
  });

  it('keeps nothing for another id the answer does not hold', () => {
    const view = props({ read: read(['engine']), id: 'repo:aurora/engine', narrowed: false });
    view.rerender({ read: read(['site']), id: 'repo:aurora/other', narrowed: true });
    expect(view.result.current).toBeNull();
  });
});

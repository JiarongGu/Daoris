import { afterEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { LIST_BOUNDS, type ListView } from './layout';
import { listKey, readList, useListPanes } from './listPanes';

// Each view's list remembers its own (D118 §3f): its closing, its width, its chosen item and its filters,
// per view, because each list differs in kind and in width. Sessions' and Settings' keys predate the frame,
// and are kept, so nothing reopens or forgets itself on the upgrade.

afterEach(() => window.localStorage.clear());

describe('where each list keeps what it remembers', () => {
  it("keeps Sessions' rail keys and its attended session, and Settings' domain", () => {
    expect(listKey('sessions', 'closed')).toBe('daoris.railClosed');
    expect(listKey('sessions', 'width')).toBe('daoris.railWidth');
    expect(listKey('sessions', 'chosen')).toBe('daoris.attending');
    expect(listKey('settings', 'chosen')).toBe('daoris.settings');
  });

  it('keeps every other part under the view it belongs to', () => {
    expect(listKey('quests', 'closed')).toBe('daoris.list.quests.closed');
    expect(listKey('quests', 'filters')).toBe('daoris.list.quests.filters');
    expect(listKey('settings', 'width')).toBe('daoris.list.settings.width');
    expect(listKey('sessions', 'filters')).toBe('daoris.list.sessions.filters');
    // The monitor window's rail is its own list (FRAME1h): closing it closes no main window's rail.
    expect(listKey('monitor', 'closed')).toBe('daoris.list.monitor.closed');
    expect(listKey('monitor', 'width')).toBe('daoris.list.monitor.width');
    // One home per part per view: no two parts of any list share a key.
    const every = (Object.keys(LIST_BOUNDS) as ListView[])
      .flatMap((view) => (['closed', 'width', 'chosen', 'filters'] as const).map((part) => listKey(view, part)));
    expect(new Set(every).size).toBe(every.length);
  });
});

describe('what a list remembers', () => {
  it('is nothing, open at its own width, until the person chooses', () => {
    expect(readList('quests')).toEqual({ closed: false, width: null, chosen: null, filters: {} });
  });

  it("reads Sessions' old keys: its closing, its width and its attended session", () => {
    window.localStorage.setItem('daoris.railClosed', '1');
    window.localStorage.setItem('daoris.railWidth', '330');
    window.localStorage.setItem('daoris.attending', 's1a2b3c4');
    expect(readList('sessions')).toEqual({ closed: true, width: 330, chosen: 's1a2b3c4', filters: {} });
  });

  it('reads a width that is not a width, or filters that are not an object, as none', () => {
    window.localStorage.setItem('daoris.list.search.width', 'wide');
    window.localStorage.setItem('daoris.list.search.filters', '[1,2]');
    expect(readList('search')).toMatchObject({ width: null, filters: {} });
    window.localStorage.setItem('daoris.list.search.filters', '{not json');
    expect(readList('search').filters).toEqual({});
  });

  it('keeps each view its own: closing one list closes no other', () => {
    const { result } = renderHook(() => useListPanes());
    act(() => result.current.setClosed('quests', true));

    expect(result.current.pane('quests').closed).toBe(true);
    expect(result.current.pane('sessions').closed).toBe(false);
    expect(window.localStorage.getItem('daoris.list.quests.closed')).toBe('1');
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();

    act(() => result.current.setClosed('quests', false));
    expect(window.localStorage.getItem('daoris.list.quests.closed')).toBeNull();
  });

  it('remembers a width dragged to, and forgets it on a reset', () => {
    const { result } = renderHook(() => useListPanes());
    act(() => result.current.setWidth('sessions', 304));
    expect(result.current.pane('sessions').width).toBe(304);
    expect(window.localStorage.getItem('daoris.railWidth')).toBe('304');

    act(() => result.current.setWidth('sessions', null));
    expect(result.current.pane('sessions').width).toBeNull();
    expect(window.localStorage.getItem('daoris.railWidth')).toBeNull();
  });

  it('remembers the chosen item, and a choice of none', () => {
    const { result } = renderHook(() => useListPanes());
    act(() => result.current.choose('settings', 'permissions'));
    act(() => result.current.choose('quests', 'abc123'));
    expect(result.current.pane('settings').chosen).toBe('permissions');
    expect(window.localStorage.getItem('daoris.settings')).toBe('permissions');
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('abc123');

    act(() => result.current.choose('quests', null));
    expect(result.current.pane('quests').chosen).toBeNull();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
  });

  it("remembers a list's filters, and forgets them when handed none", () => {
    const { result } = renderHook(() => useListPanes());
    act(() => result.current.setFilters('quests', { to: 'engine', closed: true }));
    expect(renderHook(() => useListPanes()).result.current.pane('quests').filters).toEqual({ to: 'engine', closed: true });

    act(() => result.current.setFilters('quests', null));
    expect(result.current.pane('quests').filters).toEqual({});
    expect(window.localStorage.getItem('daoris.list.quests.filters')).toBeNull();
  });

  it('hands out the same setters from one render to the next', () => {
    const { result, rerender } = renderHook(() => useListPanes());
    const first = result.current;
    act(() => first.setClosed('quests', true));
    rerender();
    expect(result.current.choose).toBe(first.choose);
    expect(result.current.setClosed).toBe(first.setClosed);
  });
});

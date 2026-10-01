import { useCallback, useMemo, useState } from 'react';
import { store, stored } from '../lib/stored';
import type { ListView } from './layout';

/**
 * What a view's list remembers (D118 §3f), per view, beside `closings.ts`, which holds what the frame
 * remembers for every view.
 *
 * @remarks
 * **Per view, because each view's list differs in kind and in width.** A person who closes Sessions' rail
 * to give a conversation room has not asked to lose Quests' list. A list laid over the main area is not
 * here: it is never kept (`closings.ts`), and a search's query is not either, since it is what the person
 * is typing now.
 *
 * Per-viewer conveniences (D42), through the page's one storage guard: never machine wiring and never a
 * tracked file.
 */
export type ListMemory = {
  /** The person closed it to its strip. */
  closed: boolean;
  /** The width it was dragged to, or null for the view's own. */
  width: number | null;
  /** The item the list has chosen, which the main area shows. */
  chosen: string | null;
  /** The list's own filters: Quests' *addressed to*, Search's *local only*, Convergence's threshold. */
  filters: Record<string, unknown>;
};

type Part = keyof ListMemory;

/**
 * The keys that predate the frame (FRAME6, D56, D75), kept so nothing a person closed, widened or chose
 * reopens or forgets itself on the upgrade: Sessions' rail and its attended session, and Settings' domain.
 */
const KEPT: Partial<Record<ListView, Partial<Record<Part, string>>>> = {
  sessions: { closed: 'daoris.railClosed', width: 'daoris.railWidth', chosen: 'daoris.attending' },
  settings: { chosen: 'daoris.settings' },
};

/** Where one part of one view's list is kept: `daoris.list.<view>.<part>`, or the key that predates it. */
export function listKey(view: ListView, part: Part): string {
  return KEPT[view]?.[part] ?? `daoris.list.${view}.${part}`;
}

function keptFilters(view: ListView): Record<string, unknown> {
  try {
    const kept = JSON.parse(stored(listKey(view, 'filters')) ?? '{}') as unknown;
    return kept && typeof kept === 'object' && !Array.isArray(kept) ? kept as Record<string, unknown> : {};
  } catch {
    return {};
  }
}

/** What one view's list remembers, as kept; anything unreadable is nothing remembered. */
export function readList(view: ListView): ListMemory {
  const width = Number(stored(listKey(view, 'width')));
  return {
    closed: stored(listKey(view, 'closed')) === '1',
    width: Number.isFinite(width) && width > 0 ? width : null,
    chosen: stored(listKey(view, 'chosen')),
    filters: keptFilters(view),
  };
}

/** Every view's list memory, and its setters, which are the same from one render to the next. */
export type ListPanes = {
  pane: (view: ListView) => ListMemory;
  setClosed: (view: ListView, closed: boolean) => void;
  setWidth: (view: ListView, width: number | null) => void;
  choose: (view: ListView, item: string | null) => void;
  /** Null forgets them. */
  setFilters: (view: ListView, filters: Record<string, unknown> | null) => void;
};

/**
 * Each view's list memory, held by the application so its doors reach every view's list (the strip's
 * toggle, the View menu, Ctrl+B, a press on the current place), and by a frame drawn alone for itself.
 */
export function useListPanes(): ListPanes {
  const [held, setHeld] = useState<Partial<Record<ListView, ListMemory>>>({});

  const change = useCallback(<P extends Part>(view: ListView, part: P, value: ListMemory[P], kept: string | null) => {
    setHeld((was) => ({ ...was, [view]: { ...(was[view] ?? readList(view)), [part]: value } }));
    store(listKey(view, part), kept);
  }, []);

  const setClosed = useCallback((view: ListView, closed: boolean) => change(view, 'closed', closed, closed ? '1' : null), [change]);
  const setWidth = useCallback((view: ListView, width: number | null) =>
    change(view, 'width', width, width === null ? null : String(width)), [change]);
  const choose = useCallback((view: ListView, item: string | null) => change(view, 'chosen', item, item), [change]);
  const setFilters = useCallback((view: ListView, filters: Record<string, unknown> | null) =>
    change(view, 'filters', filters ?? {}, filters === null ? null : JSON.stringify(filters)), [change]);

  return useMemo(() => ({
    pane: (view: ListView) => held[view] ?? readList(view),
    setClosed, setWidth, choose, setFilters,
  }), [held, setClosed, setWidth, choose, setFilters]);
}

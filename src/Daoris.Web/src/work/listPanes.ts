import { useCallback, useMemo, useRef, useState } from 'react';
import { store, stored } from '../lib/stored';
import { LIST_BOUNDS, type ListView } from './layout';

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

/**
 * What a list's chosen item is now, as its view reads it (UX6b, D150 §8): still there and waiting on something (an open
 * quest, a finding still listed, a repository still registered), closed (a done or declined quest, a closed ask), gone
 * (deleted, retired, no longer listed), or not read yet.
 */
export type ChoiceStanding = 'live' | 'closed' | 'gone' | 'unread';

/** Whether a remembered choice ends with its item: closed or gone, never on a guess while the view is still asking. */
export const endsChoice = (standing: ChoiceStanding): boolean => standing === 'closed' || standing === 'gone';

/** Every view's list memory, and its setters, which are the same from one render to the next. */
export type ListPanes = {
  pane: (view: ListView) => ListMemory;
  setClosed: (view: ListView, closed: boolean) => void;
  setWidth: (view: ListView, width: number | null) => void;
  choose: (view: ListView, item: string | null) => void;
  /** Null forgets them. */
  setFilters: (view: ListView, filters: Record<string, unknown> | null) => void;
  /**
   * The view opened by a door that names no item (its place, a menu, the palette): what it chose is a remembered choice
   * again, read once more before it is shown. A door that names an item chooses it instead.
   */
  reopen: (view: ListView) => void;
  /** What the view read of its chosen item: a remembered choice whose item closed or went is let go, once. */
  settle: (view: ListView, standing: ChoiceStanding) => void;
};

/**
 * Each view's list memory, held by the application so its doors reach every view's list (the strip's
 * toggle, the View menu, Ctrl+B, a press on the current place), and by a frame drawn alone for itself.
 *
 * @remarks
 * **A remembered choice ends with what it chose** (UX6b, design §1 rule 6): a view reopens its chosen item only while
 * that item still waits on something. What a launch read from the keys, and what a view held when a door that names
 * nothing opened it again, is remembered; it is read once by the view, and let go if its item closed or went, so the view
 * opens with nothing chosen and never on the *gone* state. An item chosen now, by the person in the list or by a door that
 * names it, is never let go here: one that closes stays as its act left it, and one that goes while open says so.
 */
export function useListPanes(): ListPanes {
  const [held, setHeld] = useState<Partial<Record<ListView, ListMemory>>>({});
  // The views whose choice is remembered rather than made now: every view at a launch, since each read its keys.
  const remembered = useRef(new Set<ListView>(Object.keys(LIST_BOUNDS) as ListView[]));

  const change = useCallback(<P extends Part>(view: ListView, part: P, value: ListMemory[P], kept: string | null) => {
    setHeld((was) => ({ ...was, [view]: { ...(was[view] ?? readList(view)), [part]: value } }));
    store(listKey(view, part), kept);
  }, []);

  const setClosed = useCallback((view: ListView, closed: boolean) => change(view, 'closed', closed, closed ? '1' : null), [change]);
  const setWidth = useCallback((view: ListView, width: number | null) =>
    change(view, 'width', width, width === null ? null : String(width)), [change]);
  const choose = useCallback((view: ListView, item: string | null) => {
    remembered.current.delete(view);
    change(view, 'chosen', item, item);
  }, [change]);
  const setFilters = useCallback((view: ListView, filters: Record<string, unknown> | null) =>
    change(view, 'filters', filters ?? {}, filters === null ? null : JSON.stringify(filters)), [change]);
  const reopen = useCallback((view: ListView) => { remembered.current.add(view); }, []);
  const settle = useCallback((view: ListView, standing: ChoiceStanding) => {
    if (standing === 'unread' || !remembered.current.has(view)) return;
    // Read once: from here the item is what the person is looking at, whatever becomes of it.
    remembered.current.delete(view);
    if (endsChoice(standing)) change(view, 'chosen', null, null);
  }, [change]);

  return useMemo(() => ({
    pane: (view: ListView) => held[view] ?? readList(view),
    setClosed, setWidth, choose, setFilters, reopen, settle,
  }), [held, setClosed, setWidth, choose, setFilters, reopen, settle]);
}

import { type KeyboardEvent, useCallback } from 'react';
import { isComposing } from '../lib/composing';

/**
 * A list's own keys (D118 §3e, audit A7): ↑ and ↓ move between rows, Home and End go to either end, and
 * Enter opens the row, which a row does by being a button. A row with a menu of its own keeps the menu,
 * reached by Tab.
 */

/** The attribute a list's row carries: the element the keys move between, and whose first control takes the focus. */
export const LIST_ROW = 'data-list-row';

/**
 * The attribute a door to the list carries where it stands outside it: the strip's toggle, the current
 * place on the activity bar. A press there is the door's to answer, not a press outside a list laid over
 * the main area, which would close it on the press and open it again on the click.
 */
export const LIST_DOOR = 'data-list-door';

/**
 * The row a key moves to among `count`, from `current` (null, or out of range, when the focus is outside
 * every row), or null where the key is not a move or there is no row. It stops at either end rather than
 * wrapping, as a workbench's lists do, so holding a key never carries the focus round to where it began.
 */
export function nextRow(count: number, current: number | null, key: string): number | null {
  if (count <= 0) return null;
  const at = current !== null && current >= 0 && current < count ? current : null;
  switch (key) {
    case 'ArrowDown': return at === null ? 0 : Math.min(at + 1, count - 1);
    case 'ArrowUp': return at === null ? count - 1 : Math.max(at - 1, 0);
    case 'Home': return 0;
    case 'End': return count - 1;
    default: return null;
  }
}

const FOCUSABLE = 'button:not([disabled]), a[href], input, [tabindex]:not([tabindex="-1"])';

/**
 * The keys bound to a list: its handler goes on the element that holds the rows, and every row carries
 * {@link LIST_ROW}. It reaches no data, so a molecule may use it.
 *
 * @remarks
 * **A field keeps its own keys.** In a search box Home and End move the caret, so the one key taken there
 * is ↓, into the list, as VS Code's filter boxes hand it on. A key another control already answered — a
 * menu's trigger opens on ↓ — or one with a modifier is left alone, and so is one that arrived through a
 * portal, since a menu drawn elsewhere is not this list. Nor is a key an input method is composing with (IME1): its ↓
 * walks the candidates, and the focus stays in the box.
 */
export function useListKeys() {
  return useCallback((event: KeyboardEvent<HTMLElement>) => {
    if (isComposing(event)) return;
    if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
    const target = event.target as HTMLElement;
    const list = event.currentTarget;
    if (!list.contains(target)) return;

    const typing = target.closest('input, textarea, select, [contenteditable="true"]') !== null;
    if (typing && event.key !== 'ArrowDown') return;

    const rows = [...list.querySelectorAll<HTMLElement>(`[${LIST_ROW}]`)];
    const current = typing ? null : rows.findIndex((row) => row.contains(target));
    const next = nextRow(rows.length, current, event.key);
    if (next === null) return;

    const row = rows[next]!;
    const focus = row.matches(FOCUSABLE) ? row : row.querySelector<HTMLElement>(FOCUSABLE);
    if (!focus) return;
    event.preventDefault();
    focus.focus();
    focus.scrollIntoView?.({ block: 'nearest' });
  }, []);
}

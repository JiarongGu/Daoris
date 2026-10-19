import { LIST_ROW } from './listKeys';

/**
 * The window's regions for F6 and Shift+F6 (D118 §3e), VS Code's *Focus Next Part*: the activity bar, the
 * view's list, the main area, the panel, the side bar and the status bar, in the order the window draws
 * them. Each carries {@link REGION}, and a region that is not drawn is simply not in the round.
 *
 * @remarks
 * **Built before it was measured.** D118 has F6 measured on the window first, since the engine may keep F6
 * for itself, and dropped if it does. A branch cannot start the window, so the key is built alone, in one
 * commit, and the look on the window decides whether it stays.
 */

/** The attribute a region of the window carries, naming it. */
export const REGION = 'data-region';

/** The region F6 moves to among `count`, from `current` (null, or out of range, outside every region); it goes round. */
export function nextRegion(count: number, current: number | null, backwards: boolean): number | null {
  if (count <= 0) return null;
  const at = current !== null && current >= 0 && current < count ? current : null;
  if (at === null) return backwards ? count - 1 : 0;
  return (at + (backwards ? count - 1 : 1)) % count;
}

const FOCUSABLE = 'button:not([disabled]), a[href], input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Focus the next region after the one holding the focus, or the previous. Where the focus lands: the
 * region's chosen item (the current place, the attended session), else its list's first row, else its
 * first control; a region with nothing to press takes the focus itself, so the next press goes on from it.
 */
export function focusRegion(root: ParentNode, backwards: boolean): void {
  const regions = [...root.querySelectorAll<HTMLElement>(`[${REGION}]`)];
  const active = document.activeElement;
  const current = regions.findIndex((region) => region === active || region.contains(active));
  const next = nextRegion(regions.length, current, backwards);
  if (next === null) return;

  const region = regions[next]!;
  const chosen = region.querySelector<HTMLElement>('[aria-current]:not([aria-current="false"])');
  const row = region.querySelector<HTMLElement>(`[${LIST_ROW}]`);
  const rowControl = row && (row.matches(FOCUSABLE) ? row : row.querySelector<HTMLElement>(FOCUSABLE));
  const target = chosen?.matches(FOCUSABLE) ? chosen : rowControl ?? region.querySelector<HTMLElement>(FOCUSABLE);
  if (target) {
    target.focus();
    return;
  }
  if (!region.hasAttribute('tabindex')) region.setAttribute('tabindex', '-1');
  region.focus();
}

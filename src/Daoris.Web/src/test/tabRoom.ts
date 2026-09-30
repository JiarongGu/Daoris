import { act } from '@testing-library/react';

/** A tab drawn as its icon alone: its padding and its icon. */
const ICON_TAB = 38;
/** The gap between a tab's icon and its name, and what each character of the name takes. */
const GAP = 6;
const CHAR = 12;

type Observed = { callback: ResizeObserverCallback; observer: ResizeObserver };

/**
 * A row of tabs laid out for jsdom, which lays nothing out (TABS1).
 *
 * @remarks
 * `useTabFit` reads two numbers and nothing else: the row's `clientWidth`, and each tab list's
 * `scrollWidth` against its `clientWidth`. So those are the seam, stubbed here for the row the hook
 * marks with `data-fit` and the tab lists inside it. A list's room is the row less its buttons, and
 * what it needs is read from what the tabs show now, so a tab drawn as its icon alone is narrower
 * than one drawn with its name, and a longer name or another language needs more: the stub answers
 * as the window would, whichever state it is asked in.
 *
 * The row's `ResizeObserver` is one the test fires with `resize`, since jsdom's is a stub that never
 * calls back. `restore` puts jsdom's measures and observer back.
 */
export function layTabs(width: number, { buttons = 80 }: { buttons?: number } = {}) {
  let row = width;
  const room = () => Math.max(0, row - buttons);
  const needs = (list: HTMLElement) => Array.from(list.querySelectorAll<HTMLElement>('[role="tab"]'))
    .reduce((sum, tab) => sum + ICON_TAB + (tab.textContent ? GAP + tab.textContent.length * CHAR : 0), 0);
  const listInRow = (element: HTMLElement) =>
    element.getAttribute('role') === 'tablist' && element.closest('[data-fit]') !== null;

  const proto = HTMLElement.prototype;
  Object.defineProperty(proto, 'clientWidth', {
    configurable: true,
    get(this: HTMLElement) {
      if (this.hasAttribute('data-fit')) return row;
      return listInRow(this) ? room() : 0;
    },
  });
  Object.defineProperty(proto, 'scrollWidth', {
    configurable: true,
    get(this: HTMLElement) {
      return listInRow(this) ? Math.max(room(), needs(this)) : 0;
    },
  });

  const observed = new Set<Observed>();
  const jsdoms = globalThis.ResizeObserver;
  class Observer {
    readonly entry: Observed;
    constructor(callback: ResizeObserverCallback) {
      this.entry = { callback, observer: this as unknown as ResizeObserver };
      observed.add(this.entry);
    }
    observe() {}
    unobserve() {}
    disconnect() { observed.delete(this.entry); }
  }
  globalThis.ResizeObserver = Observer as unknown as typeof ResizeObserver;

  return {
    /** The row is now `next` wide, and its observers are told, as the window would tell them. */
    resize(next: number) {
      row = next;
      act(() => {
        for (const { callback, observer } of [...observed]) callback([], observer);
      });
    },
    restore() {
      delete (proto as unknown as Record<string, unknown>).clientWidth;
      delete (proto as unknown as Record<string, unknown>).scrollWidth;
      globalThis.ResizeObserver = jsdoms;
    },
  };
}

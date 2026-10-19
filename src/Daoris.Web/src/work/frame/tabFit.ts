import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';

/**
 * How much more room a row's tab lists need than they have: each list's content past its box.
 *
 * @remarks
 * Every list in the row counts, because the lists share the row. The panel's views and its console's
 * streams both give way when the header is short, so the views' names fit only once the whole row does.
 */
function overflow(row: HTMLElement): number {
  let over = 0;
  for (const list of row.querySelectorAll<HTMLElement>('[role="tablist"]')) {
    over += Math.max(0, list.scrollWidth - list.clientWidth);
  }
  return over;
}

/**
 * Whether a row of tabs draws its unselected tabs as their icons alone (TABS1): a tab shows its whole
 * name or its icon, never a name cut to one character.
 *
 * @remarks
 * **Measured, since no CSS rule can say it.** CSS can hide a name at a width it is told in advance,
 * but the width the names need changes with the tabs, a file's name and the language.
 * - With the names drawn, it reads whether the row's lists overflow. If they do, it remembers the row's
 *   width plus what they lacked, which is the width the names need, and turns to icons.
 * - With the icons drawn, the names are not there to measure, so it compares the row's width with
 *   that remembered need and turns back to names once the row holds them.
 *
 * **No flicker loop.** The row is a line of the region, so its width never depends on what its tabs
 * show. Turning to icons therefore never resizes it, and the observer is not told again. Each turn is
 * made in a layout effect, or in the observer under `flushSync`, so it lands before the browser paints.
 * A turn back to names that finds them still overflowing remembers a larger need, so at one width it
 * settles in one step.
 *
 * **`content` says what the tabs show**: their names, and anything else that changes what the row
 * needs. When it changes, the remembered need is stale, so the row draws the names once more and
 * measures them before painting.
 *
 * A row that is not laid out (hidden, or a unit test's DOM) has no width and is not measured. It keeps
 * what it drew, which is the names until a measure says otherwise.
 * `data-fit` on the row says which it drew, for a test and for a look at the window.
 */
export function useTabFit(content: string): { row: (element: HTMLElement | null) => void; compact: boolean } {
  const [element, setElement] = useState<HTMLElement | null>(null);
  const [compact, setCompact] = useState(false);
  // What the committed DOM draws. The observer reads this rather than the state, which may be ahead.
  const drawn = useRef(false);
  // The row's width the whole names need, known once they were seen not to fit.
  const need = useRef<number | null>(null);
  // The content the remembered need was measured for.
  const measured = useRef<string | null>(null);

  const fit = useCallback((row: HTMLElement) => {
    const width = row.clientWidth;
    if (width <= 0) return;
    if (drawn.current) {
      if (need.current === null || width >= need.current) setCompact(false);
      return;
    }
    const over = overflow(row);
    if (over > 0) {
      need.current = width + over;
      setCompact(true);
    }
  }, []);

  // When the row arrives, when what the tabs show changes, and after each turn. Not on every commit: a
  // panel re-renders with its session, and a width change reaches the observer below anyway.
  useLayoutEffect(() => {
    drawn.current = compact;
    if (!element) return;
    if (measured.current !== content) {
      measured.current = content;
      need.current = null;
      // The icons are drawn and the need is stale: draw the names, and measure them in the next pass.
      if (compact) {
        setCompact(false);
        return;
      }
    }
    fit(element);
  }, [element, content, compact, fit]);

  // The row resized: its region's edge dragged, the window, or a region beside it.
  useEffect(() => {
    if (!element || typeof ResizeObserver === 'undefined') return undefined;
    const observer = new ResizeObserver(() => flushSync(() => fit(element)));
    observer.observe(element);
    return () => observer.disconnect();
  }, [element, fit]);

  return { row: setElement, compact };
}

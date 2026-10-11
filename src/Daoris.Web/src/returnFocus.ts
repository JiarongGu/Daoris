/**
 * **The way back returns the focus** (UX6h3d, the platform UX contract's load-more foot rule): a page opened from a press
 * goes back to the page that held it, and the focus goes back to that press, so a keyboard reader keeps their place. The page
 * is redrawn after the press, and what it lists may still be on its way, so the focus waits for it, as the History foot's
 * waits for the row it targets (`help/AskHistory.tsx`).
 *
 * @remarks
 * `find` is the press, found when it is drawn. `settled` says the page has finished drawing what could hold it: then, with
 * no press to be found (a row the list no longer holds, a page opened by a door), `fallback`, the page's heading, takes the
 * focus, so it is never lost to the body. It stops where the reader has gone elsewhere meanwhile, and at last after a few
 * seconds. It is a function over the document, not a hook: the way back is a press, and the page's holder owns no ref to a
 * node another component draws.
 */
export function returnFocus({ find, settled, fallback }: {
  find: () => HTMLElement | null;
  settled: () => boolean;
  fallback: () => HTMLElement | null;
}): void {
  const observer = new MutationObserver(() => attempt());
  const stop = () => { observer.disconnect(); window.clearTimeout(giveUp); };
  const giveUp = window.setTimeout(() => observer.disconnect(), 5000);
  function attempt() {
    // The way back's own button left with its page, so the focus is on the body; anywhere else, the reader chose it.
    const focused = document.activeElement;
    if (focused && focused !== document.body) { stop(); return; }
    const press = find();
    if (press) { press.focus(); stop(); return; }
    if (!settled()) return;
    const heading = fallback();
    if (heading) {
      heading.tabIndex = -1;
      heading.focus();
    }
    stop();
  }
  observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-busy', 'aria-selected'] });
}

/** A button of the document by its accessible name: its `aria-label`, else its text. */
export function buttonNamed(name: string): HTMLElement | null {
  return [...document.querySelectorAll<HTMLElement>('button')]
    .find((button) => (button.getAttribute('aria-label') ?? button.textContent?.trim()) === name) ?? null;
}

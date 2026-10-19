import { type RefObject, useEffect, useRef, useState } from 'react';

/** The head's sections a wait stands in (WAITFOOT2a): its park's card, its go-aheads, its rules, its work. */
export const WAIT_SECTION = '[data-wait-section]';

/** How much of a section is on screen for it to be: all of it, short of a rounding. */
const WHOLE = 0.99;

type Watching = { observer: IntersectionObserver; sections: readonly Element[]; margin: string };

/**
 * **Whether the head's waits are on screen** (WAITFOOT2a, D168's WAITFOOT2 note): every section of the record's head a wait stands
 * in (`data-wait-section`), wholly inside the scroll box below the page head pinned at its top (`--session-head`, which that
 * head tells its box). The strip at the foot stands aside while they are, so a wait is drawn in one place on the screen.
 *
 * @remarks
 * Whole, not touched: a section half under the pinned head may hold its press in the half that is not seen, and while it does the
 * strip stands. A section taller than the box is never whole, so the strip stands beside it, as it did before. With no observer
 * (a test's DOM) and with no section drawn, `false`: the strip stands as it always did. `null` from the moment sections are first
 * watched until the observer's first word, a frame later, so a session whose head is on screen as it opens never flashes its
 * strip; once told, a section added or the pinned head grown keeps the last word until the next.
 *
 * The sections and the pinned head's height are the page's, read after each drawing; the observer is made again only when the
 * one or the other changed.
 */
export function useWaitsOnScreen(scroller: RefObject<HTMLElement | null>): boolean | null {
  const [onScreen, setOnScreen] = useState<boolean | null>(false);
  const watching = useRef<Watching | null>(null);
  useEffect(() => {
    const root = scroller.current;
    // Read in the head alone: the conversation below it may be long, and this is read at every drawing.
    const sections = [...(root?.querySelector('[data-record-head]')?.querySelectorAll(WAIT_SECTION) ?? [])];
    const margin = `-${root?.style.getPropertyValue('--session-head') || '0px'} 0px 0px 0px`;
    const was = watching.current;
    if (was && was.margin === margin && was.sections.length === sections.length
      && was.sections.every((section, at) => section === sections[at])) return;
    was?.observer.disconnect();
    watching.current = null;
    if (!root || sections.length === 0 || typeof IntersectionObserver === 'undefined') {
      setOnScreen(false);
      return;
    }
    const whole = new Map<Element, boolean>();
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) whole.set(entry.target, entry.isIntersecting && entry.intersectionRatio >= WHOLE);
      setOnScreen(sections.every((section) => whole.get(section) === true));
    }, { root, rootMargin: margin, threshold: [0, WHOLE] });
    for (const section of sections) observer.observe(section);
    watching.current = { observer, sections, margin };
    if (!was) setOnScreen(null);
  });
  useEffect(() => () => {
    watching.current?.observer.disconnect();
    watching.current = null;
  }, []);
  return onScreen;
}

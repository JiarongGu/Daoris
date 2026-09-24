import { type RefObject, useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';

/** How close to the bottom still counts as "at the tail" — a line's slack, not a pixel's. */
const SLACK = 48;

/**
 * Follow a growing region's tail until the person scrolls up, and offer the way back (D76, CONV2).
 *
 * @remarks
 * The reference console's rule, and the console's own since SURF4c: **a reader at the bottom stays at
 * the bottom as the conversation grows; a reader who scrolled up to read is left where they are**,
 * and a "back to bottom" control appears for them. Pulling someone mid-sentence to the tail is the
 * one thing a live view must never do.
 *
 * `grew` is anything that changes when the content does — the last event's sequence and its text's
 * length, so a message growing chunk by chunk counts. `opened` changes when a different session is
 * attended, which always starts at the tail.
 */
export function useFollowTail(scroller: RefObject<HTMLElement | null>, grew: unknown, opened: unknown) {
  const [atTail, setAtTail] = useState(true);
  const following = useRef(true);

  const toTail = useCallback((smooth = false) => {
    const element = scroller.current;
    if (!element) return;
    // A test's DOM has no scrollTo on elements; a property set is the same place without the glide.
    if (typeof element.scrollTo === 'function') {
      element.scrollTo({ top: element.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
    } else {
      element.scrollTop = element.scrollHeight;
    }
    following.current = true;
    setAtTail(true);
  }, [scroller]);

  useEffect(() => {
    const element = scroller.current;
    if (!element) return;
    const onScroll = () => {
      const near = element.scrollHeight - element.scrollTop - element.clientHeight < SLACK;
      following.current = near;
      setAtTail(near);
    };
    element.addEventListener('scroll', onScroll, { passive: true });
    return () => element.removeEventListener('scroll', onScroll);
  }, [scroller]);

  // A new session opens at its tail.
  useLayoutEffect(() => { toTail(); }, [opened, toTail]);

  // Growth follows only a reader who was following.
  useLayoutEffect(() => {
    if (following.current) toTail();
  }, [grew, toTail]);

  return { atTail, toTail };
}

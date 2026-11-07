import { type RefObject, useEffect, useMemo, useState } from 'react';
import { useZoom } from './zoom';

/** The root's size in px as the page draws it, which the window's zoom moves (trap 12); 16 where nothing says. */
export function rootRem(): number {
  if (typeof document === 'undefined') return 16;
  const size = Number.parseFloat(getComputedStyle(document.documentElement).fontSize);
  return Number.isFinite(size) && size > 0 ? size : 16;
}

/** A rem in px, read again when the window's zoom changes: what a drawing places by, never 16 under a zoom. */
export function useRootRem(): number {
  // The level is what moves the root's size, so it is what the size is read again by.
  const level = useZoom();
  return useMemo(() => rootRem(), [level]);
}

/**
 * How wide an element is, followed as it changes: the room a drawing has in its card.
 *
 * @remarks
 * `undefined` where nothing measures it (a unit test's DOM), so a drawing falls back to its fixed
 * size. Mirrors the frame's own measure (`WorkFrame`'s `useFrameWidth`).
 */
export function useWidth(element: RefObject<HTMLElement | null>): number | undefined {
  const [width, setWidth] = useState<number | undefined>(undefined);

  useEffect(() => {
    const target = element.current;
    if (!target) return undefined;
    const measure = (value: number) => setWidth(value > 0 ? value : undefined);
    measure(target.getBoundingClientRect().width);
    const observer = typeof ResizeObserver !== 'undefined'
      ? new ResizeObserver(([entry]) => { if (entry) measure(entry.contentRect.width); })
      : null;
    observer?.observe(target);
    return () => observer?.disconnect();
  }, [element]);

  return width;
}

/**
 * How tall a drawing may be: the window's height below the element's top, less `reserve` for what
 * follows it in the view (a legend, the page's foot). Followed as the window resizes, so a map grows
 * and gives way with it. `undefined` where nothing measures it, as `useWidth` is.
 */
export function useTall(element: RefObject<HTMLElement | null>, reserve: number): number | undefined {
  const [tall, setTall] = useState<number | undefined>(undefined);

  useEffect(() => {
    const target = element.current;
    if (!target || typeof window === 'undefined') return undefined;
    const measure = () => {
      const top = target.getBoundingClientRect().top;
      const room = window.innerHeight - top - reserve;
      // jsdom lays nothing out and has a zero-height window: that is no measure at all.
      setTall(window.innerHeight > 0 && room > 0 ? room : undefined);
    };
    measure();
    window.addEventListener('resize', measure);
    return () => window.removeEventListener('resize', measure);
  }, [element, reserve]);

  return tall;
}

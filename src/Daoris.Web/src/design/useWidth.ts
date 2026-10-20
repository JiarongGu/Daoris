import { type RefObject, useEffect, useState } from 'react';

/**
 * How wide an element is, followed as it changes: the room a drawing has in its card (UX5 U44).
 *
 * @remarks
 * `undefined` where nothing measures it (a unit test's DOM), so a drawing falls back to the size it
 * was always drawn at. Mirrors the frame's own measure (`WorkFrame`'s `useFrameWidth`).
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
 * and gives way with it (UX5 U59). `undefined` where nothing measures it, as `useWidth` is.
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

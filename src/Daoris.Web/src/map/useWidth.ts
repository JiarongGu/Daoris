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

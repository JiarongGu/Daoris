import { useEffect, useRef, useState } from 'react';

/**
 * Pressing a part in the strip or on the flow's rail opens its line's session at it and scrolls to it, so the part pressed is the
 * one in view: an earlier part's box (`data-opened`), else the newest part's.
 */
export function useSeekPart() {
  const [seeking, setSeeking] = useState(0);
  const partBox = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (seeking === 0) return undefined;
    const frame = requestAnimationFrame(() => {
      const box = partBox.current;
      (box?.parentElement?.querySelector<HTMLElement>('[data-opened]') ?? box)?.scrollIntoView?.({ block: 'start' });
    });
    return () => cancelAnimationFrame(frame);
  }, [seeking]);
  return { partBox, seek: () => setSeeking((count) => count + 1) };
}

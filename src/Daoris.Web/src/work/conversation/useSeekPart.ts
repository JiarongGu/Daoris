import { useEffect, useState } from 'react';

/** Pressing a part opens its line's session at it and scrolls the story to it: an earlier part's box, else the newest's. */
export function useSeekPart() {
  const [seeking, setSeeking] = useState(0);
  useEffect(() => {
    if (seeking === 0) return undefined;
    const frame = requestAnimationFrame(() => {
      const story = document.querySelector<HTMLElement>('[data-region="story"]');
      (story?.querySelector<HTMLElement>('[data-opened]') ?? story?.querySelector<HTMLElement>('ol'))?.scrollIntoView?.({ block: 'start' });
    });
    return () => cancelAnimationFrame(frame);
  }, [seeking]);
  return { seek: () => setSeeking((count) => count + 1) };
}

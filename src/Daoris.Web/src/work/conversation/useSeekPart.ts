import { useEffect, useRef, useState } from 'react';

/** Pressing a part in the strip or on the flow's rail attends it and scrolls to it, so the part shown is the one in view. */
export function useSeekPart() {
  const [seeking, setSeeking] = useState(0);
  const partBox = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (seeking === 0) return undefined;
    const frame = requestAnimationFrame(() => partBox.current?.scrollIntoView?.({ block: 'start' }));
    return () => cancelAnimationFrame(frame);
  }, [seeking]);
  return { partBox, seek: () => setSeeking((count) => count + 1) };
}

import { useEffect, useState } from 'react';

/**
 * Re-reads the clock every `every` ms while mounted, so a count of elapsed time steps without a new record (WAITSHOWN1,
 * WAITSAY1). `null` stops the ticking, for a host that has no count to step.
 */
export function useNow(every: number | null): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (every === null) return undefined;
    setNow(Date.now());
    const timer = setInterval(() => setNow(Date.now()), every);
    return () => clearInterval(timer);
  }, [every]);
  return now;
}

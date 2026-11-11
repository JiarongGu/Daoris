import { useEffect, useRef } from 'react';

/** Whether this window is the one in front of the person: visible and focused, so it, and no other, speaks (§5). */
export function looking(): boolean {
  return typeof document !== 'undefined' && document.visibilityState === 'visible' && document.hasFocus();
}

/**
 * Calls `onFront` each time this window comes in front of the person: focused, or made visible while it holds the focus, where
 * what it heard out of focus (a terminal's press, asked from the terminal) is said.
 */
export function useInFront(onFront: () => void): void {
  const latest = useRef(onFront);
  latest.current = onFront;
  useEffect(() => {
    if (typeof window === 'undefined') return undefined;
    const front = () => { if (looking()) latest.current(); };
    window.addEventListener('focus', front);
    document.addEventListener('visibilitychange', front);
    return () => {
      window.removeEventListener('focus', front);
      document.removeEventListener('visibilitychange', front);
    };
  }, []);
}

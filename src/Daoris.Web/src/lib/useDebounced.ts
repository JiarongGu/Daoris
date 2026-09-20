import { useEffect, useState } from 'react';

/**
 * The trailing value, once it has held still. Both live filters use this — a slider position and a
 * search box are the same problem: every intermediate value is an index query answering a question
 * the person had not finished asking.
 */
export function useDebounced<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

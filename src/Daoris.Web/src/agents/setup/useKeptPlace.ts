import { useRef } from 'react';

/**
 * The place a chosen id names, kept across the list's pages and searches. `found` is what the
 * answer in hand holds of it, or nothing: a search or a filter narrows the rows, and a page of an open fold shows others, so a place the
 * answer no longer holds is kept as last read while the list is narrowed, and an imported repository (which no page of the tree holds
 * once its fold is closed) until another place is chosen. Unnarrowed, an answer without a driven place is the place gone.
 */
export function useKeptPlace<T extends { kind: string }>(found: T | null, id: string | null, narrowed: boolean): T | null {
  const last = useRef<{ id: string; found: T } | null>(null);
  if (found && id) last.current = { id, found };
  if (found) return found;
  const kept = id !== null && last.current?.id === id ? last.current.found : null;
  return kept && (narrowed || kept.kind === 'imported') ? kept : null;
}

import { useState } from 'react';
import { store, stored } from '../lib/stored';

// What the person has already seen of a standing line in Notifications: the newest time among what it holds when they said *Got it*,
// kept in this browser so a reload does not bring the same line back. A line is drawn only for something newer than that.

/** The mark for a line that held nothing stamped: a tier set outside Daoris. */
const NO_STAMP = '';

/** The newest of these times, or null where none is a time. */
export function newestOf(times: readonly (string | null | undefined)[]): string | null {
  let newest: string | null = null;
  for (const each of times) {
    if (!each || Number.isNaN(Date.parse(each))) continue;
    if (newest === null || Date.parse(each) > Date.parse(newest)) newest = each;
  }
  return newest;
}

/** Whether a line holding something up to `newest` has anything the person has not seen, given their mark `since` (null: none yet). */
export function isNewSince(newest: string | null, since: string | null | undefined): boolean {
  if (since === null || since === undefined) return true;
  if (newest === null) return false;
  return since === NO_STAMP || Date.parse(newest) > Date.parse(since);
}

/** The mark a *Got it* on a line holding up to `newest` sets. */
export function markOf(newest: string | null): string {
  return newest ?? NO_STAMP;
}

/** A line's seen mark under `key`, and the press that moves it. */
export function useSeenMark(key: string): [string | null, (mark: string) => void] {
  const [since, setSince] = useState<string | null>(() => stored(key));
  return [since, (mark) => {
    store(key, mark);
    setSince(mark);
  }];
}

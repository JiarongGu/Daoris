import { store, stored } from '../../lib/stored';
import { type Place, readPlace, samePlace } from '../focus';

// The places last chosen as Ask Daoris's focus on this machine, for the focus picker's *Recent*: kept as the new conversation's
// Recent is (`work/start/startRecent.ts`), under its own key, per viewer; a storage absent or refused costs the memory alone.

/** Where the places last chosen are kept, newest first. */
export const FOCUS_RECENT = 'daoris.help.focus.recent';

const MAX_RECENT = 5;

/** The places last chosen, newest first, or none where what is kept is not a list of places. */
export function readFocusRecent(): Place[] {
  try {
    const held: unknown = JSON.parse(stored(FOCUS_RECENT) ?? '[]');
    return Array.isArray(held) ? held.map(readPlace).filter((place): place is Place => place !== null).slice(0, MAX_RECENT) : [];
  } catch {
    return [];
  }
}

/** Remember a place chosen, newest first, none twice, five at most; the list as it now stands. */
export function keepFocusRecent(place: Place): Place[] {
  const next = [place, ...readFocusRecent().filter((each) => !samePlace(each, place))].slice(0, MAX_RECENT);
  store(FOCUS_RECENT, JSON.stringify(next));
  return next;
}

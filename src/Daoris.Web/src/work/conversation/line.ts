import type { Session } from '../../data/api';
import { sessionOrigin } from '../identity';

/** How many earlier parts a line draws above the attended one, and how many one press draws more (SWITCHCTX1c-i). */
export const EARLIER_AT_MOST = 3;

/**
 * **The parts a session's page draws above the attended one** (SWITCHCTX1c-i; the switch-context design §2.7): the parts it
 * continues by `from`, the part it came from, that part's, and so on, oldest first. A part of the same entry running alongside
 * continues none of these and is not among them: it stays a press away in the *Parts* strip.
 *
 * @remarks
 * Pressing an earlier part attends it, and its own ancestry is what is drawn, so the parts after it are not. The walk stops at a
 * record the page does not hold, at one outside `within` (the entry's parts, where the reader answered entries), at a record
 * from another machine, whose conversation stays there (D47 §4), and where the links loop.
 */
export function ancestryOf(
  sessions: readonly Pick<Session, 'id' | 'from'>[], attended: string, within?: ReadonlySet<string> | null,
): string[] {
  const held = new Map(sessions.map((row) => [row.id, row]));
  const line: string[] = [];
  const seen = new Set([attended]);
  for (let at = held.get(attended)?.from; at && !seen.has(at); at = held.get(at)?.from) {
    const part = held.get(at);
    if (!part || sessionOrigin(part) !== null || (within && !within.has(at))) break;
    seen.add(at);
    line.unshift(at);
  }
  return line;
}

/**
 * The earlier parts drawn, the nearest to the attended one last: the three nearest and, for each press, up to three more, and how
 * many the next press would draw (`more`, which its words say).
 */
export function drawnOf(line: readonly string[], pressed: number): { drawn: string[]; more: number } {
  const drawn = line.slice(Math.max(0, line.length - EARLIER_AT_MOST * (pressed + 1)));
  return { drawn, more: Math.min(EARLIER_AT_MOST, line.length - drawn.length) };
}

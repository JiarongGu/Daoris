import type { Session } from '../api';

// What the rail and the monitor share of a session's row. The list's groups, by state and by repository, and its
// Ended cut are `groups.ts` (SESSUX1c), which took over `partition` and its cap.

/**
 * A session waiting on the person before one that is not — the one ordering that earns its keep
 * (components plan §3a). The monitor sorts its tiles by it; the rail's list by repository sorts by
 * `groups.ts`'s, which counts a parked quest's last session with them (D126 §4.3).
 */
export const waitingFirst = (a: Pick<Session, 'state'>, b: Pick<Session, 'state'>): number =>
  Number(a.state !== 'awaiting-person') - Number(b.state !== 'awaiting-person');

/**
 * When a session last moved (RAIL2): its record's time, or a live chat's last turn on this machine
 * when that is later. The record moves on state changes only, so seconds after an answer a chat read
 * *moved 4m ago*. Compared as times, not strings: the driver writes an offset where the record writes Z.
 */
export function movedAt(session: Session, lastTurn?: string | null): string {
  if (!lastTurn || Number.isNaN(Date.parse(lastTurn))) return session.updated;
  return Date.parse(lastTurn) > Date.parse(session.updated) ? lastTurn : session.updated;
}

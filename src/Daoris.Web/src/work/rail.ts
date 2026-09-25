import type { Session } from '../api';
import { SESSION_ACTIVE } from '../ui';

/** How many ended sessions the rail lists before it counts the rest. */
export const ENDED_SHOWN = 12;

/**
 * A session waiting on the person before one that is not — the one ordering that earns its keep
 * (components plan §3a). The rail and the monitor both sort by it; each wrote it out (REV3 CLEAN1).
 */
export const waitingFirst = (a: Pick<Session, 'state'>, b: Pick<Session, 'state'>): number =>
  Number(a.state !== 'awaiting-person') - Number(b.state !== 'awaiting-person');

/**
 * What the rail lists: the live sessions, and beneath them the ones that ended.
 *
 * @remarks
 * 🔴 **Found on the deployed application** (D62): the Work frame — the frame whose subject is
 * sessions — opened on a machine holding four real session records and showed four sentences saying
 * there was nothing. The rail listed live sessions only, so a record was reachable after a restart
 * exactly never, and the transcripts the first-deployment case study spent a whole document reading
 * were unreachable from the surface built to read them. The working-surface design's §7 is the
 * contract this breaks: *"after a restart the records are the service's and the transcripts are on
 * disk … read when a surface next opens."*
 *
 * **Live first, grouped as before; ended beneath, newest first, counted past the cap.** The live
 * groups keep their headers because those carry live facts — drivable, held, which tree is busy —
 * and an ended session has none of those to say. It has a record, which is what the row shows.
 *
 * **The attended session stays in the live list whatever state it reached** — the rule that was
 * already here, kept exactly: a session that ends while you are reading it must not jump down the
 * rail out from under you.
 */
export function partition(
  sessions: readonly Session[],
  selected: string | null | undefined,
  limit: number = ENDED_SHOWN,
): { active: Session[]; ended: Session[]; hiddenEnded: number } {
  const isLive = (session: Session) => SESSION_ACTIVE.has(session.state) || session.id === selected;

  const active = sessions.filter(isLive);
  // ISO-8601 timestamps order correctly as strings; newest first is the reading order for a record.
  const rest = sessions.filter((session) => !isLive(session))
    .sort((a, b) => b.updated.localeCompare(a.updated));

  const cap = Math.max(0, limit);
  return {
    active,
    ended: rest.slice(0, cap),
    hiddenEnded: Math.max(0, rest.length - cap),
  };
}

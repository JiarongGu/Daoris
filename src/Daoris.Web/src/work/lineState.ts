import type { TFunction } from 'i18next';
import type { Session } from '../data/api';
import { type ShownState, shownKey } from '../design/ui';
import { type SessionGrouping, type SessionStateName, shownOf } from './groups';
import { sessionOrigin } from './identity';

// A line of work read as one session: its parts are one conversation and its state is its every part's and co-worker's, as the
// driver's reader answers it on each row (`line`). Pure, so every list a person could have is an argument.

/** A state's word where no record of the session it is read from is held. */
const STATE_WORD: Record<SessionStateName, ShownState> = {
  'needs-you': 'awaiting-person', working: 'working', pending: 'queued', failed: 'failed', done: 'completed',
};

/** A line's state read from another session than the one drawn: its word, who it is read from, and that part's number. */
export type LineSaid = { shown: ShownState; by: string; role: string; part: number | null };

/**
 * The line's state where another part or a co-worker decides it, named; null where the session drawn decides it itself, and where
 * the reader answered no line.
 */
export function lineSaid(
  id: string, grouping: SessionGrouping | null | undefined, groupings: readonly SessionGrouping[] | undefined,
  sessions: readonly Session[] | undefined, parts?: readonly string[],
): LineSaid | null {
  const line = grouping?.line;
  if (!line || line.by === id) return null;
  const record = sessions?.find((each) => each.id === line.by);
  const shown = record ? shownOf(record, groupings?.find((each) => each.session === line.by), undefined) : STATE_WORD[line.state];
  const at = parts?.indexOf(line.by) ?? -1;
  return { shown, by: line.by, role: line.role, part: at >= 0 ? at + 1 : null };
}

/** A line's state said where another part or co-worker decides it: *working in part 2*, *working: its helper*. */
export function lineWords(t: TFunction, said: LineSaid): string {
  const state = t(shownKey(said.shown));
  if (said.role !== 'part') return t(`work.parts.line.${said.role}`, { state, defaultValue: state });
  return said.part ? t('work.parts.line.part', { state, n: said.part }) : t('work.parts.line.partUnnumbered', { state });
}

/** The conversation a record is a part of: its `line`, else itself. */
const lineKey = (session: Pick<Session, 'id' | 'line'>) => session.line ?? session.id;

/** Its line's parts held here, oldest first: this machine's records whose `line` names it. A teammate's is on no line here. */
function members(sessions: readonly Session[], session: Session): Session[] {
  if (sessionOrigin(session) !== null) return [session];
  const key = lineKey(session);
  return sessions
    .filter((each) => sessionOrigin(each) === null && lineKey(each) === key)
    .sort((a, b) => Date.parse(a.created) - Date.parse(b.created) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}

/** The session a part opens as: its line's newest part, which the page heads, its composer and its state are of. */
export function lineHeadOf(sessions: readonly Session[] | undefined, id: string | null | undefined): Session | null {
  const session = sessions?.find((each) => each.id === id);
  return session ? members(sessions!, session).at(-1) ?? session : null;
}

/** The parts of a session's line before it, oldest first: what its page draws above it as one conversation. */
export function lineMembersBefore(sessions: readonly Session[] | undefined, id: string): string[] {
  const session = sessions?.find((each) => each.id === id);
  if (!session) return [];
  const line = members(sessions!, session).map((each) => each.id);
  return line.slice(0, Math.max(0, line.indexOf(id)));
}

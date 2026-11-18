import type { TFunction } from 'i18next';
import type { Session } from '../../data/api';
import type { PILL_TONE, ShownState } from '../../design/ui';
import { shownKey } from '../../design/ui';
import { ago, elapsed } from '../../locales/format';
import { lineOf, type SessionGrouping } from '../groups';
import { DOCKED_REASONS, type DockLine, dockName } from '../waits/dock';

/** What the head's state line says, by the reader's group: §5 of the session view's design. */
export type StateKind = 'needs-you' | 'step' | 'working' | 'pending' | 'held' | 'failed' | 'done' | 'unread';

/** Where the dock and the reader disagree: the reader's group and reason, and the oldest docked kind ('' where none is docked). */
export type StateDisagreement = { group: string; reason: string; docked: string };

/**
 * A session's state line, read from the driver's reader (`SESSION_GROUPS`' group and reason, a line's by `LineStandings`) and the
 * dock, never from the record's state word: its kind, since when, its cause, how many more waits than the one it names, and where
 * the dock and the reader disagree.
 */
export type StateLine = {
  kind: StateKind;
  since: string | null;
  /** The oldest wait in the dock, which the line names. */
  wait: DockLine | null;
  /** The cause where no wait names it: a step it waits on, the call it is inside, a hold's or a failure's reason, how it ended. */
  cause: { said: string } | { reason: string } | { shown: string } | null;
  more: number;
  disagrees: StateDisagreement | null;
};

/** The pill's tone by the line's kind. */
export const STATE_TONE: Record<StateKind, keyof typeof PILL_TONE> = {
  'needs-you': 'open', step: 'neutral', working: 'taken', pending: 'neutral', held: 'neutral', failed: 'declined', done: 'done', unread: 'neutral',
};

/**
 * **The state line**: an open wait the person settles places the session in Needs you whatever its record says, so it never reads
 * done while it waits. The dock and the reader read the same facts until the driver reads every docked kind (`DOCK_CENSUS`); where
 * they disagree the line reads Waiting on you and says so (`disagrees`), for the window's log. No reader row yet reads `unread`,
 * never the record's word.
 */
export function stateLineOf({ session, grouping, docked }: {
  session: Pick<Session, 'state' | 'updated'>;
  grouping: SessionGrouping | null | undefined;
  docked: readonly DockLine[];
}): StateLine {
  const oldest = docked[0] ?? null;
  const more = Math.max(0, docked.length - 1);
  const line = grouping ? lineOf(grouping) : null;
  const group = line?.group === 'archived' ? (line.state ?? 'done') : line?.group;
  const reason = line?.reason ?? '';
  const needs = group === 'needs-you';
  // The reader places a docked kind under Needs you with nothing docked, or the dock holds a wait the reader does not place.
  const disagrees: StateDisagreement | null = oldest && !needs
    ? { group: group ?? '', reason: grouping?.reason ?? '', docked: oldest.wait.kind }
    : !oldest && needs && DOCKED_REASONS.has(reason) && line?.by === grouping?.session
      ? { group: 'needs-you', reason, docked: '' }
      : null;
  if (oldest || needs) {
    const since = oldest?.since ?? grouping?.waits?.[0]?.since ?? session.updated;
    return { kind: 'needs-you', since, wait: oldest, cause: oldest ? null : { reason }, more, disagrees };
  }
  const base = { wait: null, more: 0, disagrees };
  if (!grouping || !group) return { ...base, kind: 'unread', since: null, cause: null };
  switch (group) {
    case 'working':
      if (session.state === 'awaiting-step') return { ...base, kind: 'step', since: session.updated, cause: grouping.awaits ? { said: grouping.awaits } : null };
      return {
        ...base, kind: 'working', since: grouping.calling?.since ?? session.updated, cause: grouping.calling?.title ? { said: grouping.calling.title } : null,
      };
    case 'pending':
      return grouping.holds
        ? { ...base, kind: 'held', since: null, cause: { said: grouping.holds.reason } }
        : { ...base, kind: 'pending', since: null, cause: null };
    case 'failed':
      return { ...base, kind: 'failed', since: session.updated, cause: reason && reason !== grouping.shown ? { reason } : { shown: grouping.shown } };
    default:
      return { ...base, kind: 'done', since: session.updated, cause: reason && reason !== grouping.shown ? { reason } : { shown: grouping.shown } };
  }
}

const KIND_KEY: Record<StateKind, string> = {
  'needs-you': 'work.head.state.needsYou', step: 'work.head.state.step', working: 'work.head.state.working', pending: 'work.head.state.pending',
  held: 'work.head.state.held', failed: 'work.head.state.failed', done: 'work.head.state.done', unread: 'work.head.state.reading',
};

/** A reader's reason in the rail's words, where it has some; else nothing, never a code. */
const reasonSaid = (t: TFunction, reason: string) => {
  const key = `work.rail.reason.${reason}`;
  return i18nHas(t, key) ? t(key) : '';
};

const i18nHas = (t: TFunction, key: string) => t(key, { defaultValue: '' }) !== '';

/**
 * The line's words: its state's word on the pill, and the rest after it, its age (or, ended, when) and its cause, joined by
 * ` · `: *Waiting on you* · 38m · Accept, and 1 more.
 */
export function stateLineSaid(t: TFunction, line: StateLine, now: Date = new Date()): { word: string; rest: string } {
  const parts: string[] = [];
  const done = line.kind === 'done' || line.kind === 'failed';
  if (line.since && !done) parts.push(elapsed(line.since, now.toISOString()));
  if (line.wait) {
    const name = dockName(t, line.wait.wait);
    parts.push(line.more > 0 ? t('work.head.state.more', { name, n: line.more }) : name);
  } else if (line.cause && 'said' in line.cause) {
    parts.push(line.cause.said);
  } else if (line.cause && 'reason' in line.cause) {
    const said = reasonSaid(t, line.cause.reason);
    if (said) parts.push(said);
  } else if (line.cause && 'shown' in line.cause) {
    parts.push(t(shownKey(line.cause.shown as ShownState)));
  }
  if (line.since && done) parts.push(ago(line.since));
  return { word: t(KIND_KEY[line.kind]), rest: parts.filter(Boolean).join(' · ') };
}

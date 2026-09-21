import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { EmptyState } from '../ui';
import type { Resolution } from './AwaitingPerson';
import { SessionHead } from './SessionHead';
import { SessionTimeline } from './SessionTimeline';

/**
 * The one session the person is attending: its record, and what was observed about it.
 *
 * @remarks
 * **The stream is not here, and that is D55.** The console came out of the quest drawer with
 * SURF4c and reached its home with SURF4d: an **output panel** the person can grow, shrink and
 * hide, which is the one thing a well inside a region can never be. `SessionConsole` itself never
 * changed — it was extracted once (SURF4a) and has been carried since.
 *
 * **The timeline sits beside the stream, never inside it.** One is what a tool printed and the
 * other is what Daoris observed, and folding them together is how the second stops being
 * trustworthy.
 *
 * **It is handed its session rather than fetching one**: the frame holds the selection, which is
 * what lets one choice bind the rail, this region and the panel at once.
 *
 * The composer sits beneath this region rather than inside it, for the same reason the panel does:
 * the record scrolls and the things you act with do not.
 */
export function AttendedSession({ session, quest, resolving, onResolve }: {
  /** The attended session, or null when the person has not chosen one. */
  session: Session | null;
  quest?: Quest | null;
  resolving?: boolean;
  /** Passed straight through to the head, where a parked session's three moves live (design §4). */
  onResolve?: (state: Resolution, note: string | null) => void;
}) {
  const { t } = useTranslation();

  if (!session) {
    return (
      <EmptyState
        icon="inbox"
        headline={t('work.attended.none.headline')}
        body={t('work.attended.none.body')}
      />
    );
  }

  return (
    <article className="grid content-start gap-4">
      <SessionHead session={session} quest={quest} resolving={resolving} onResolve={onResolve} />
      <SessionTimeline
        session={session}
        quest={quest}
        hideCurrentNote={session.state === 'awaiting-person' && Boolean(onResolve)}
      />
    </article>
  );
}

import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { SessionConsole } from '../SessionConsole';
import { EmptyState } from '../ui';
import { SessionHead } from './SessionHead';
import { SessionTimeline } from './SessionTimeline';

/**
 * The one session the person is attending: its record, what was observed, and its console.
 *
 * @remarks
 * **The stream is promoted out of the drawer** (design §3). `SessionConsole` moves here unchanged —
 * it already holds its own hook so that `MonoWell` holds none, and rewriting it would have thrown
 * away the one piece of this surface that was already right. In a browser it renders nothing,
 * because only a driver has a stream to give (D47 §4); in Storybook the same absence is what a
 * reviewer sees, and that is honest rather than a gap.
 *
 * **The timeline sits beside the stream, never inside it.** One is what a tool printed and the
 * other is what Daoris observed, and folding them together is how the second stops being
 * trustworthy.
 *
 * **It is handed its session rather than fetching one**: the frame holds the selection (SURF4d),
 * which is what lets one choice bind the rail, this region and the panel at once.
 *
 * The composer is not here yet — it arrives with the frame (SURF4d), where starting and ending a
 * session both live.
 */
export function AttendedSession({ session, quest }: {
  /** The attended session, or null when the person has not chosen one. */
  session: Session | null;
  quest?: Quest | null;
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
      <SessionHead session={session} quest={quest} />
      <SessionConsole id={session.id} tall />
      <SessionTimeline session={session} quest={quest} />
    </article>
  );
}

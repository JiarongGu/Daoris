import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { EmptyState } from '../ui';
import type { ChainStep } from '../map/chain';
import { ChainStrip } from '../map/ChainStrip';
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
/**
 * Whether a session's current note is already shown in its head — the parked card, with its moves —
 * so the timeline leaves it out rather than saying it twice. ONE rule for the column and the dock
 * (REV3): the dock hid every parked note, while the head shows one only where it can be answered
 * here, so another machine's parked note was shown nowhere on a wide window.
 */
export const noteIsInTheHead = (session: Session, answerableHere: boolean) =>
  session.state === 'awaiting-person' && answerableHere;

export function AttendedSession({
  session, quest, resolving, stopping, onResolve, onAnswerAsk, onStop, chain = [], onSession,
  timeline = 'narrow',
}: {
  /** The attended session, or null when the person has not chosen one. */
  session: Session | null;
  quest?: Quest | null;
  resolving?: boolean;
  stopping?: boolean;
  /** Passed straight through to the head, where a parked session's three moves live (design §4). */
  onResolve?: (state: Resolution, note: string | null) => void;
  /** Passed straight through too: where an intake's answer is, its ask (INT4g). */
  onAnswerAsk?: (ask: string) => void;
  /** And a running intake's stop, which has no composer to live on (INT4h). */
  onStop?: () => void;
  /**
   * The chain its quest belongs to (MAP1), the same strip a quest's drawer shows. Rendered only
   * when there is one, like there: a lone quest has nothing before or after it to show.
   */
  chain?: ChainStep[];
  /** Attend another session of the chain — the frame's own selection, so every region follows. */
  onSession?: (session: Session) => void;
  /**
   * Where the timeline is: here only on a narrow window, because the main window's dock holds it
   * when wide — or here always, in a window with no dock (a detached session, REV3).
   */
  timeline?: 'narrow' | 'always';
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
      <SessionHead
        session={session}
        quest={quest}
        resolving={resolving}
        stopping={stopping}
        onResolve={onResolve}
        onAnswerAsk={onAnswerAsk}
        onStop={onStop}
      />
      {chain.length > 1 && <ChainStrip chain={chain} level={3} onSession={onSession} />}
      {/* The timeline lives in the right dock since SURF6 gave the dock its second occupant. It
          stays here on a narrow window, where the dock is not rendered at all — the column is the
          fallback, so nothing is unreachable on a laptop. */}
      <div className={timeline === 'narrow' ? 'lg:hidden' : undefined}>
        <SessionTimeline
          session={session}
          quest={quest}
          hideCurrentNote={noteIsInTheHead(session, Boolean(onResolve))}
        />
      </div>
    </article>
  );
}

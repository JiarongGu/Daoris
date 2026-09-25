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
  session, quest, opening, resolving, stopping, onResolve, onAnswerAsk, onStop, chain = [], onSession,
  timeline = 'dock',
}: {
  /** The attended session, or null when the person has not chosen one. */
  session: Session | null;
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name (RAIL1). */
  opening?: string | null;
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
   * Where the timeline is: in the frame's dock, which since FRAME6 is there at every width — or here,
   * in a window with no dock (a detached session, REV3).
   */
  timeline?: 'dock' | 'always';
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
    // 🔴 The centre's width, whatever it is (UX5 U16, the owner): a 768px measure here left the record
    // at half a maximized window. The pill stays beside its title in the head instead.
    <article className="grid w-full content-start gap-4">
      <SessionHead
        session={session}
        quest={quest}
        opening={opening}
        resolving={resolving}
        stopping={stopping}
        onResolve={onResolve}
        onAnswerAsk={onAnswerAsk}
        onStop={onStop}
      />
      {chain.length > 1 && <ChainStrip chain={chain} level={3} onSession={onSession} />}
      {/* The timeline lives in the right dock since SURF6 gave the dock its second occupant. It stayed
          here on a narrow window while the dock was hidden there; FRAME6 keeps the dock at every width,
          and the copy here drew it twice. Only a window with no dock carries it. */}
      {timeline === 'always' && (
        <SessionTimeline
          session={session}
          quest={quest}
          hideCurrentNote={noteIsInTheHead(session, Boolean(onResolve))}
        />
      )}
    </article>
  );
}

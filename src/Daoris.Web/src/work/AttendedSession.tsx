import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GoAhead, Quest, Session } from '../api';
import { answeredPark, Button, EmptyState } from '../ui';
import type { ChainStep } from '../map/chain';
import { ChainLine } from '../map/ChainLine';
import { ChainStrip } from '../map/ChainStrip';
import type { SweepBranch } from '../settings/Sweep';
import type { AccountNamer } from '../tools';
import type { QuestClose, Resolution } from './AwaitingPerson';
import { HowItCameToBe, type TraceDoor } from './HowItCameToBe';
import type { Answered } from './InlineConfirm';
import type { Relations } from './relations';
import { SessionHead } from './SessionHead';
import { SessionRelations } from './SessionRelations';
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
 * here, so another machine's parked note was shown nowhere on a wide window. A park the person answered shows its
 * answer in the head and not its note (ANSWER1c), so its note stays the timeline's.
 */
export const noteIsInTheHead = (session: Session, answerableHere: boolean) =>
  session.state === 'awaiting-person' && !answeredPark(session) && answerableHere;

export function AttendedSession({
  session, quest, opening, taking, lastTurn, resolving, onResolve, onAnswerAsk, onAnswerSession,
  chain = [], onSession, onQuest, onReview, relations, timeline = 'dock', branch, onDiscardBranch, discardingBranch,
  headed = false, goAheads, onGoAhead, trace, ownSignIn = false, nameOf,
}: {
  /** Its agent has accounts, so a record naming none ran on the tool's own sign-in (D125 §3.7), said in its head. */
  ownSignIn?: boolean;
  /**
   * What a person calls an account (ACCTNAME1, D152 §4.2), from the roster the frame holds: its head's *Details*, its chain's
   * rows and its trace say each account by it. Absent, each record's id is said.
   */
  nameOf?: AccountNamer;
  /**
   * How it came to be (TRACE1b, D143, D50), folded at the foot of its record, and the doors its chain opens by id: the page
   * holds the fold and the read. Absent where nothing can read the trace: a browser, which has no driver.
   */
  trace?: TraceDoor & { onSession?: (id: string) => void; onQuest?: (id: string) => void; onAsk?: (id: string) => void };
  /** The go-aheads it asked on its quest's ask, shown in the head while it is parked (KNOWUSE1a2). */
  goAheads?: GoAhead[];
  /** Passed straight through to the head: one of them answered, and the park with it (KNOWUSE1a2). */
  onGoAhead?: (number: number, approved: boolean, words?: string) => void;
  /**
   * A page header above carries its state, its id and its acts (SESSUX1d, D126 §3.2), so the record head says neither
   * again. A window with no header (a detached session) keeps them in the head.
   */
  headed?: boolean;
  /** Open its review — the move beside work it left that no branch of the person's holds (SESS2 H4). */
  onReview?: () => void;
  /** Who it worked with beyond its chain (SESS1): the session that asked, and what it asked of others. */
  relations?: Relations;
  /** Open a quest's record — a stop on the chain, or one it asked. Absent where there is nowhere to open it. */
  onQuest?: (quest: Quest) => void;
  /** The branch its own tree left, as the clean-up judged it (SESS1 S10) — the head's. */
  branch?: SweepBranch | null;
  /** Passed straight through to the head: that branch discarded once its tree is gone (LAND3b), told back to its ask. */
  onDiscardBranch?: (answered: Answered) => void;
  /** That discard is on its way. */
  discardingBranch?: boolean;
  /** The attended session, or null when the person has not chosen one. */
  session: Session | null;
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name (RAIL1). */
  opening?: string | null;
  /** Whether a turn is in flight, as the driver says — the head reads a chat between turns as idle (UX5 U17). */
  taking?: boolean;
  /** When its last turn ended here (RAIL2), for the head's *moved*. */
  lastTurn?: string | null;
  resolving?: boolean;
  /** Passed straight through to the head, where a parked session's moves live (design §4); its stop is the page header's. */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose) => void;
  /** Passed straight through too: where an intake's answer is, its ask (INT4g). */
  onAnswerAsk?: (ask: string) => void;
  /** And the answer to a driven session that parked to ask the person (STANDDOWN2). */
  onAnswerSession?: (answer: string | null) => void;
  /**
   * The chain its quest belongs to (MAP1), the same strip a quest's page shows. Rendered only
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
  // The chain as one line, or whole: folded on every session it opens on (UX7c, D152 §7, amending SESS2 H7's remembered
  // choice), since a strip opened on one session stood between the next one's head and its conversation.
  const [chainWhole, setChainWhole] = useState<string | null>(null);
  const showChain = (whole: boolean) => setChainWhole(whole && session ? session.id : null);

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
        taking={taking}
        lastTurn={lastTurn}
        resolving={resolving}
        onResolve={onResolve}
        onAnswerAsk={onAnswerAsk}
        onAnswerSession={onAnswerSession}
        branch={branch}
        onReview={onReview}
        onDiscardBranch={onDiscardBranch}
        discardingBranch={discardingBranch}
        headed={headed}
        goAheads={goAheads}
        onGoAhead={onGoAhead}
        ownSignIn={ownSignIn}
        nameOf={nameOf}
      />
      {/* Every stop a door (the study's §5: "every stop pressable"): a session attends, a quest opens.
          One line of stops by default, since the whole strip stood 350 to 450px between the head and
          the conversation, saying the head's title again (SESS2 H7); whole on a press. */}
      {chain.length > 1 && (chainWhole === session.id
        ? (
          <div className="grid justify-items-start gap-1">
            <ChainStrip chain={chain} level={3} attended={session.id} onQuest={onQuest} onSession={onSession} nameOf={nameOf} />
            <Button variant="ghost" className="px-1.5 py-0 text-small" onClick={() => showChain(false)}>{t('chain.hide')}</Button>
          </div>
        )
        : <ChainLine chain={chain} onQuest={onQuest} onExpand={() => showChain(true)} />)}
      {relations && <SessionRelations relations={relations} onQuest={onQuest} onSession={onSession} />}
      {/* Folded, a line above the conversation: nothing is read until the person opens it (TRACE1b). */}
      {trace && <HowItCameToBe kind="session" id={session.id} {...trace} nameOf={nameOf} />}
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

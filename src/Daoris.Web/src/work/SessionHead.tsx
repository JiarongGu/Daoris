import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SweepBranch } from '../settings/Sweep';
import type { Quest, Session } from '../api';
import { ago, elapsed, sessionTool } from '../format';
import { cn } from '../lib/cn';
import { Button, MetaLine, Pill, SESSION_ACTIVE, SESSION_TONE, shownState, WaitingCard } from '../ui';
import { AwaitingIntake } from './AwaitingIntake';
import { AwaitingPerson, type Resolution } from './AwaitingPerson';
import { isIntake, sessionOrigin, sessionTitle } from './identity';
import { movedAt } from './rail';
import { RunningIntake } from './RunningIntake';

/**
 * The attended session's record (design §3): what it is, where it runs, what it ran as, and — when
 * it is parked — the analysis that is waiting on a person.
 *
 * @remarks
 * **The rail's row and this head must not disagree**, so both derive their identity from
 * `sessionTitle` and neither invents one. What the head adds is the room the rail does not have:
 * how it stands, what it left, the tool with its version and account, and both ends of the clock —
 * and the tree's whole path on hover (SESS2: at the head's full weight it was its longest line).
 *
 * **`AwaitingPerson` gets a surface at last** (design §4). It has meant "only the person can clear
 * this" since D46 and had never been rendered anywhere. It sits **above** the record, because it is
 * the reason the person is looking, and it carries the three moves — which is why `onResolve` is a
 * prop: the head knows nothing about the bridge, and the frame that does hands it down.
 *
 * **It says how an ended session stands, in the record's note** (SESS2, reversing the rule that it
 * did not): the rule rested on *"the timeline below carries it"*, and since FRAME6 the timeline is in
 * the side bar, which starts closed (U7) — so a failed session's reason and a finished one's ending
 * were on no screen the person had open. A running session says nothing here, since its conversation
 * is the answer, and a parked one's note is its card's.
 *
 * **What it left comes with its move** (SESS2): work no branch of the person's holds has *review*
 * beside it, where the metadata's last pair used to say it with nothing to press.
 *
 * **An absence is never a dash.** No tree is the registered root, no profile is the harness's own
 * configuration home, no machine is this deployment's own — and a browser over a keyed remote is
 * told none of them (D47 §4). `MetaLine` drops a pair it has no value for, which is why all four
 * can be passed unconditionally.
 */
export function SessionHead({
  session, quest, opening, taking, lastTurn, resolving = false, stopping = false, onResolve, onAnswerAsk, onStop,
  onAnswerSession, branch, onReview,
}: {
  /** When its last turn ended here, as the driver says (RAIL2): *moved* reads the later of it and the record. */
  lastTurn?: string | null;
  /** Open its review (the side bar's, or the panel's where it was moved) — what work left unlanded asks for. */
  onReview?: () => void;
  session: Session;
  /**
   * The branch its own tree left, as the clean-up judged it (SESS1 S10, D88): whether its work landed,
   * where, or what only it holds. Absent where it has no tree of its own here, or nothing has answered.
   */
  branch?: SweepBranch | null;
  /** Whether a turn is in flight, as the driver says: a live chat between turns reads idle (UX5 U17). */
  taking?: boolean;
  /** The quest it serves, where the caller has it — absent is a state, not a gap (D49 §3). */
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name (RAIL1). */
  opening?: string | null;
  resolving?: boolean;
  /** A running intake's stop is in flight. */
  stopping?: boolean;
  /** How a parked session is cleared. Absent where nothing can act — a browser, or a story. */
  onResolve?: (state: Resolution, note: string | null) => void;
  /**
   * Open the ask an intake serves (INT4g) — a parked one's answer is there, not on the session.
   * Absent where nothing can open it.
   */
  onAnswerAsk?: (ask: string) => void;
  /**
   * Cut a RUNNING intake off (INT4h). It has no composer to carry the stop, because it takes no
   * messages. Absent where nothing can reach the process's machine.
   */
  onStop?: () => void;
  /**
   * Answer a driven session that parked to ask the person (STANDDOWN2): its quest is carried on in the
   * same tree, handed the words. Absent where nothing can reach this machine's host.
   */
  onAnswerSession?: (answer: string | null) => void;
}) {
  const { t } = useTranslation();
  const running = SESSION_ACTIVE.has(session.state);
  const parked = session.state === 'awaiting-person';
  const intake = isIntake(session);
  const shown = shownState(session, taking);

  return (
    <header className="grid gap-2.5">
      {/* `running` counts a park as alive; a parked intake is AwaitingIntake's, below. */}
      {running && !parked && intake && (
        <RunningIntake ask={session.ask!} pending={stopping} onOpen={onAnswerAsk} onStop={onStop} />
      )}
      {parked && intake && (onResolve || onAnswerAsk) && (
        <AwaitingIntake
          ask={session.ask!}
          note={session.note}
          pending={resolving}
          onAnswer={onAnswerAsk}
          onStop={onResolve ? () => onResolve('stopped', null) : undefined}
        />
      )}
      {parked && !intake && (onResolve || onAnswerSession) && (
        // Keyed: a half-written decline reason belongs to the session it was written for, and it
        // carried into the next parked one, ready to decline it with somebody else's reason (REV3).
        <AwaitingPerson
          key={session.id}
          note={session.note}
          pending={resolving}
          onResolve={onResolve}
          // A driven session parked to ask the person has no process left to message (STANDDOWN2).
          onAnswer={session.quest ? onAnswerSession : undefined}
        />
      )}
      {/* Nothing here can act — a browser, or a mirrored record from another machine — so the
          analysis is shown and the moves are not. Half a control is worse than none. */}
      {parked && !onResolve && !onAnswerSession && !(intake && onAnswerAsk) && session.note && (
        <WaitingCard title={t('work.head.waiting')}>
          <p className="m-0 mt-1.5 whitespace-pre-wrap text-body leading-relaxed">{session.note}</p>
        </WaitingCard>
      )}

      {/* The state follows the title rather than the far edge (§4: status leads): the head is as wide
          as the centre (UX5 U16), and at the edge the pill sat a thousand pixels from what it names.
          Two lines at most, whole on hover: a quest's title can run to a paragraph (SESS2). */}
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1.5">
        <h2 className="m-0 line-clamp-2 text-title font-[650] leading-[1.35]" title={sessionTitle(session, quest, opening)}>
          {sessionTitle(session, quest, opening)}
        </h2>
        <span className="flex shrink-0 items-baseline gap-2">
          <Pill tone={SESSION_TONE[shown]}>{t(`sessionState.${shown}`)}</Pill>
          <span className="font-mono text-meta text-ink-faint">{session.id}</span>
        </span>
      </div>

      {/* How it stands, in the record's own words (SESS2 H5, H6): why a failed session failed, how a
          finished one ended. Not while it runs, when the conversation below is the answer; not parked,
          where the card above already carries it. */}
      {!running && session.note && <Said note={session.note} />}

      {/* What it left, and the move that acts on it (SESS2 H4): work no branch of the person's holds is
          theirs to review, in the waiting hue; landed work is a quiet fact. */}
      {branch && <Left branch={branch} onReview={onReview} />}

      {/* The reference, quiet and on one line where it fits (SESS2 H2, H3): the tree's machine path is
          the repository's on hover, and the branch is on the line above, where it means something. */}
      <MetaLine
        className="text-meta"
        items={[
          // An intake serves an ask and runs in Daoris's own room, never a repository's tree
          // (INT4b) — its record says so rather than `repository: ask #…` (INT4g).
          intake
            ? { label: t('work.intake.ask'), value: <span title={session.tree ?? undefined}>#{session.ask}</span>, mono: true }
            : { label: t('work.head.repository'), value: <span title={session.tree ?? undefined}>{session.repository}</span> },
          { label: t('work.head.quest'), value: session.quest ? `#${session.quest}` : null, mono: true },
          { label: t('work.head.tool'), value: sessionTool(session) },
          { label: t('work.head.machine'), value: sessionOrigin(session) },
          { label: t('work.head.started'), value: ago(session.created) },
          // A running session has an age and a finished one has a lifetime. Same number, different
          // question, so the label says which rather than leaving the reader to guess.
          {
            label: running ? t('work.head.elapsed') : t('work.head.ran'),
            value: elapsed(session.created, running ? null : session.updated),
          },
          // Only while it runs: an ended session's last move is its end, which *ran* already says.
          { label: t('work.head.moved'), value: running ? ago(movedAt(session, lastTurn)) : null },
        ]}
      />

    </header>
  );
}

/** How much of the record's note the head shows before the rest is a press away. */
const NOTE_LINES = 3;

/** The record's own sentence about how the session stands — content, never translated. */
function Said({ note }: { note: string }) {
  const { t } = useTranslation();
  const [whole, setWhole] = useState(false);
  const long = note.length > 280 || note.split('\n').length > NOTE_LINES;
  return (
    <div className="grid justify-items-start gap-1">
      <p className={cn('m-0 whitespace-pre-wrap text-body leading-relaxed text-ink-soft', !whole && 'line-clamp-3')}>{note}</p>
      {long && (
        <Button variant="ghost" className="px-0 py-0 text-small" onClick={() => setWhole((was) => !was)}>
          {t(whole ? 'work.head.noteLess' : 'work.head.noteMore')}
        </Button>
      )}
    </div>
  );
}

/** What its own tree left, and whether the person has anything to do about it (SESS1 S10, SESS2 H4). */
function Left({ branch, onReview }: { branch: SweepBranch; onReview?: () => void }) {
  const { t } = useTranslation();
  const theirs = branch.kind === 'unlanded' || branch.kind === 'dirty';
  return (
    <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
      <span className="text-ink-faint">{t('work.head.itsWork')}</span>
      <span className={theirs ? 'text-st-open' : 'text-ink-soft'}>{landed(t, branch)}</span>
      <span className="min-w-0 truncate font-mono text-meta text-ink-faint">{branch.branch}</span>
      {theirs && onReview && (
        <Button className="px-2 py-0.5 text-small" onClick={onReview}>{t('work.head.review')}</Button>
      )}
    </p>
  );
}

/** Whether a session's branch landed, in a few words for the head (D88's proof). */
function landed(t: (key: string, options?: Record<string, unknown>) => string, branch: SweepBranch): string {
  switch (branch.kind) {
    case 'empty': return t('work.head.landed.empty', { line: branch.where ?? '' });
    case 'landed': return branch.where ? t('work.head.landed.on', { where: branch.where }) : t('work.head.landed.somewhere');
    case 'unlanded': return t('work.head.landed.not', { count: branch.commits });
    case 'dirty': return t('work.head.landed.dirty');
    default: return t('work.head.landed.inUse');
  }
}

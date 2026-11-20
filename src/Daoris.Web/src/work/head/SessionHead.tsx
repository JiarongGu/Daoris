import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { SweepBranch } from '../../settings/Sweep';
import type { Quest, Session } from '../../data/api';
import type { AccountNamer } from '../../agents/tools';
import { ago, elapsed, sessionTool, stamp } from '../../locales/format';
import {
  answeredPark, Inline, PathText, personsSaid, Pill, SESSION_ACTIVE, shownState, WaitingCard,
} from '../../design/ui';
import { WaitingOnLine } from '../atWork/AtWorkView';
import type { AtWorkA } from '../atWork/types';
import { AnsweredPark } from '../waits/AnsweredPark';
import { AwaitingIntake } from '../waits/AwaitingIntake';
import type { QuestClose, Resolution } from '../waits/AwaitingPerson';
import { STATE_TONE, type StateLine } from './stateLine';
import { type DetailRow, DetailsFold } from './DetailsFold';
import { AccessChip, accessRow, startedFull } from './AccessFact';
import { gitAccessRow, type SessionGitAccessHold } from './GitAccessFact';
import { placeHoldRow } from './PlaceHoldFact';
import { countRows, courseRow, type LineSupervision, NO_SUPERVISION, supervisorRow } from './lineSupervision';
import { ChoiceChip } from './ChoiceChip';
import type { Answered } from '../InlineConfirm';
import { isIntake, sessionOrigin, sessionTitle } from '../identity';
import { Note } from '../Note';
import { Left, Said } from './HeadNote';
import { leftParts } from '../notes/leftNote';
import { type HeadLink, linkRow } from './headRows';
import { type AskOrigin, originsOf, OriginLine, startedRows, startingOf } from './startedFrom';
import { type HelperWaits, waitsOnWord } from '../waits/HelperAsks';
import { CoWorkers } from '../team/CoWorkers';
import { MemberThread } from '../team/MemberThread';
import { type PieceSection, Pieces } from '../pieces/Pieces';
import { hasNote } from '../noteLines';
import { processCommitsSays, type SessionProcess } from './process';
import { movedAt } from '../rail/rail';
import { RunningIntake } from './RunningIntake';
import { actorRows, notStartedLine } from '../actors';

// The work's line is its own molecule, which the foot's strip draws too; its shapes are read from here too.
export type { HeadOpinion, HeadReview, LandingPlan, WorkAsk } from '../review/Lands';

/**
 * The attended session's record: what it is, where it runs, what it ran as, and — when
 * it is parked — the analysis that is waiting on a person.
 *
 * @remarks
 * **The rail's row and this head must not disagree**, so both derive their identity from
 * `sessionTitle` and neither invents one. What the head adds is the room the rail does not have:
 * how it stands, what it left, the tool with its version and account, and both ends of the clock —
 * and the tree's whole path on hover (at the head's full weight it would be its longest line).
 *
 * **`AwaitingPerson` has a surface.** It means "only the person can clear
 * this". It sits **above** the record, because it is
 * the reason the person is looking, and it carries the three moves — which is why `onResolve` is a
 * prop: the head knows nothing about the bridge, and the frame that does hands it down.
 *
 * **A park the person answered is `AnsweredPark`'s**: the record stays parked with the answer until the
 * driver's next look, when the same session goes on, so the head shows the answer and says so, with no moves, wherever
 * the frame could act.
 *
 * **The go-aheads a park asked stand beneath its card**, the ask's own list: each with *Approve*
 * and *Refuse* where the frame can answer, and answering the last one open there sends the same session on, as on the
 * ask's page. They stay while it is parked, answered or not, so one still waiting can be answered before it
 * goes on.
 *
 * **The rules it proposed stand beneath them, whatever its state**: a session refused something files a
 * proposal and goes on or ends its turn, so its own open proposals are answered here, *Allow…* asking once, as at the foot.
 *
 * **It says how an ended session stands, in the record's note**: the timeline is in
 * the side bar, which starts closed, so a failed session's reason and a finished one's ending
 * are said here, on a screen the person has open. A running session says nothing here, since its conversation
 * is the answer, and a parked one's note is its card's.
 *
 * **What it left comes with its move**: work no branch of the person's holds has *review*
 * beside it. A failed attempt's branch whose tree is
 * gone has *Discard branch…* too, asking once, since no clean-up takes it and the review has no tree to discard.
 *
 * **Commits its tree holds are offered, whatever its ending**: where the driver's reader says what the tree offers (`lands`),
 * the line names the commits, the branch and the tree, with a *Review* door to the run's review, where the Accept is pressed.
 *
 * **Commits the line holds by content are never offered again**: where a squash merge or a cherry-pick put its work elsewhere
 * (`discards`), the line offers *Discard branch…* alone, asking once with the driver's sentence naming the ref its commits
 * stay at; the press is the review's Discard, unforced.
 *
 * **Where a review's gate holds the work, it stands in the door's place**: *Review in `<environment>`* in the gate's state, with
 * the set-up step's showing and the verdict's presses. *Second opinion*, wherever a level asks one, stands before it.
 *
 * **An absence is never a dash.** No tree is the registered root, no profile is the harness's own
 * configuration home, no machine is this deployment's own — and a browser over a keyed remote is
 * told none of them. `MetaLine` drops a pair it has no value for, which is why all four
 * can be passed unconditionally.
 */
export function SessionHead({
  session, quest, opening, brief, taking, lastTurn, onResolve, onAnswerAsk, onAnswerSession, branch, ownBranch, onReview, onDiscardBranch,
  discardingBranch = false, headed = false, ownSignIn = false, nameOf, onOpenQuest, ask, continues, helps, onOpenSession, heavy, process, helpers,
  pieces, waitingOn, gitAccess, terminalHold, supervisorCourse, placeHold, supervision = NO_SUPERVISION, state = null,
}: {
  /** The supervisor's decision on a point its rule referred, the frame's `SessionSupervisorCourse`: *Overrule* while it is held. */
  supervisorCourse?: ReactNode;
  placeHold?: ReactNode;
  /** The line's counts and the acts the person's rules made on it: *Details*' course rows and its Supervisor line. Absent, none is drawn. */
  supervision?: LineSupervision;
  /** A terminal holding this conversation, the frame's `SessionTerminalHold`: absent where none can be read. */
  terminalHold?: ReactNode;
  /** What each running session holds of git access, the frame's: *Details*' *Git access* row, with *Tighten…*. */
  gitAccess?: SessionGitAccessHold | null;
  /**
   * What its line waits on: *At work*'s answer, whose *Waiting on* line the head draws in the same words,
   * and the door that opens *At work*. Absent where the frame holds no answer for it.
   */
  waitingOn?: { answer: AtWorkA; nameOf?: (agent: string) => string; onOpen?: (exchange: string) => void } | null;
  /**
   * Its pieces, the frame's: what it asked other agents to write, where each stands, the
   * ask's press and each piece's door and stop. Absent, a browser, a teammate's record or a session that cannot lead one, none is drawn.
   */
  pieces?: PieceSection | null;
  /** Its state line (`stateLineOf`), said beside its title in a window with no header (a detached session). Absent, none is said. */
  state?: { line: StateLine; said: { word: string; rest: string } } | null;
  /**
   * The helpers it asked for, the frame's: its asks as the driver's look leaves them, the agents' names, the
   * person's yes and a door to each helper. Absent, a browser or a teammate's record, none is drawn.
   */
  helpers?: HelperWaits | null;
  /**
   * Open the quest it serves: the press its *Started from* names it by, in the head and in *Details*. Absent where
   * nothing can open it, and then the quest is named with no door.
   */
  onOpenQuest?: (quest: Quest) => void;
  /** The ask it came from, its id and its words where the frame holds the record: an intake's own, or the one its line began in. */
  ask?: AskOrigin | null;
  /**
   * The session its record says it continues (`from`), where the frame holds it, with its title. Absent, *Started from*
   * says the record's id, with no door.
   */
  continues?: HeadLink | null;
  /** The session a helper helps (`serves`), where the frame holds it, with its title; absent, its id. */
  helps?: HeadLink | null;
  /** Attend one of those sessions: the door beside its title in *Details*. Absent, the title has no door. */
  onOpenSession?: (session: Session) => void;
  /**
   * *Details*' *Heavy gates*: its heavy run's gates as phases, or whom it waits behind, a door
   * (`heavyRow`). Absent where it holds and waits for no slot.
   */
  heavy?: DetailRow | null;
  /**
   * The process its own tree runs under: how its work is committed and how it lands, each
   * with where it is set, *Details*' rows. Absent or null where it is told none (no tree of its own here) or nothing answered.
   */
  process?: SessionProcess | null;
  /**
   * Its agent has accounts, so a record naming none ran on the tool's own sign-in, and the head says so where it shows an
   * account.
   */
  ownSignIn?: boolean;
  /** What a person calls an account, from the roster; absent, the record's id is said. */
  nameOf?: AccountNamer;
  /**
   * A page header above carries its state, its title and its facts: the head says
   * none again, the quest's whole title included: that is a row of *Details*. Absent, a window with no header
   * (a detached session), the head keeps the title, its word and its id.
   */
  headed?: boolean;
  /** When its last turn ended here, as the driver says: *moved* reads the later of it and the record. */
  lastTurn?: string | null;
  /** Open its review (the side bar's, or the panel's where it is moved) — what work left unlanded asks for. */
  onReview?: () => void;
  session: Session;
  /**
   * The branch its own tree left, as the clean-up judged it: whether its work landed,
   * where, or what only it holds. Absent where it has no tree of its own here, or nothing has answered.
   */
  branch?: SweepBranch | null;
  /**
   * The branch its tree is on where the agent moved it onto one of its own, as the driver's `work` says it:
   * named when the clean-up lists no row, since it lists only `daoris/` branches and the tidy removed that one.
   */
  ownBranch?: string | null;
  /**
   * Discard that branch, its commits with it: offered once its tree is gone, where the driver says its commits are a
   * failed or superseded attempt's, and asked once first; told back to its ask. Absent where nothing here can press
   * it.
   */
  onDiscardBranch?: (answered: Answered) => void;
  /** That discard is on its way: its presses wait for it. */
  discardingBranch?: boolean;
  /** Whether a turn is in flight, as the driver says: a live chat between turns reads idle. */
  taking?: boolean;
  /** The quest it serves, where the caller has it — absent is a state, not a gap. */
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name. */
  opening?: string | null;
  brief?: string | null;
  /**
   * Whether anything can finish or decline a parked session here (the dock's card does); absent where nothing can
   * act — a browser, or a story, which then say where it is answered. Its stop is the page header's, its one owner.
   */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
  /**
   * Open the ask an intake serves — a parked one's answer is there, not on the session.
   * Absent where nothing can open it.
   */
  onAnswerAsk?: (ask: string) => void;
  /**
   * Answer a driven session that parked to ask the person: the same session goes on with the
   * words at the driver's next look. Absent where nothing can reach this machine's host. Where the box below answers the
   * park too, the card's answer goes through the box's own door; either settles with a refusal's sentence, or nothing.
   */
  onAnswerSession?: (answer: string) => Promise<string | null> | void;
}) {
  const { t } = useTranslation();
  const running = SESSION_ACTIVE.has(session.state);
  const parked = session.state === 'awaiting-person';
  // Answered, it goes on at the driver's next look, so nothing below asks the person again. Its words read from `said`.
  const answered = answeredPark(session) ? personsSaid(session) : null;
  const waiting = parked && !answered;
  const intake = isIntake(session);
  const shown = shownState(session, taking);
  const noted = hasNote({ parts: session.noteParts });
  const origins = originsOf(t, session, quest, ask, continues, { onOpenQuest, onOpenAsk: onAnswerAsk, onOpenSession });

  return (
    // Where the foot's *To the top* puts the focus.
    <header data-record-head tabIndex={-1} className="grid gap-2.5 outline-none">
      {/* `running` counts a park as alive; a parked intake is AwaitingIntake's, below. */}
      {running && !parked && intake && <RunningIntake ask={session.ask!} onOpen={onAnswerAsk} />}
      {terminalHold}
      {supervisorCourse}
      {/* A member's thread: whom it works for, *Back to lead*, and once it answered, *Ask again…* and the pass. */}
      {helpers?.team?.thread && <MemberThread {...helpers.team.thread} lead={helps?.title ?? helpers.team.thread.lead} />}
      {answered !== null && <AnsweredPark answer={answered} />}
      {/* What the line waits on of others, At work's line from the same read; the person's own waits stand below. */}
      {waitingOn && <WaitingOnLine answer={waitingOn.answer} nameOf={waitingOn.nameOf} onOpen={waitingOn.onOpen} />}
      {/* Its intake's park reads its ask's words here; its *Answer* is the dock's. */}
      {waiting && intake && onAnswerAsk && <AwaitingIntake ask={session.ask!} parts={session.noteParts} />}
      {/* Nothing here can act — a browser, or a mirrored record from another machine — so the
          analysis is shown and the moves are not. Half a control is worse than none, and none with no word why is worse still:
          the card says where it is answered. */}
      {waiting && !onResolve && !onAnswerSession && !(intake && onAnswerAsk) && (noted || !intake) && (
        <WaitingCard title={t('work.head.waiting')}>
          <Note parts={session.noteParts} className="mt-1.5 text-body leading-relaxed" />
          {!intake && (
            <p className="m-0 mt-2 text-small text-ink-faint">
              {t(sessionOrigin(session) !== null ? 'work.awaiting.cannot.teammate' : 'work.awaiting.cannot.elsewhere')}
            </p>
          )}
        </WaitingCard>
      )}
      {/* Its co-workers: the lead and each member, its thread's door and the pass; an agent's ask that waits on the person's
          yes is the dock's, with the yes and the no. */}
      {helpers && (
        <CoWorkers
          session={session.id}
          helpers={{ ...helpers, asks: helpers.asks.filter((ask) => !waitsOnWord(ask)) }}
          lead={helpers.team?.lead ?? session.door}
          onPass={helpers.team?.onPass}
          passing={helpers.team?.passing}
          choices={helpers.team?.choices}
          outside={helpers.team?.outside}
          onRemoveOutside={helpers.team?.onRemoveOutside}
          removingOutside={helpers.team?.removingOutside}
        />
      )}

      {/* Its pieces: what other agents write for it, each in a clone of its own, and the ask of one. */}
      {pieces && (pieces.pieces.length > 0 || pieces.ask) && <Pieces section={pieces} headingId={`pieces-${session.id}`} />}

      {!headed && (
        /* A window with no header (a detached session): the title and its word are said here; its id is its *Details*',
           as on the page. The state follows the title rather than the far edge (status leads), two lines at
           most, whole on hover. */
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1.5">
          <h2 className="m-0 line-clamp-2 text-heading font-[650] leading-[1.35]" title={sessionTitle(session, quest, opening)}>
            {sessionTitle(session, quest, opening)}
          </h2>
          {state && (
            <span data-state-line={state.line.kind} className="inline-flex min-w-0 items-baseline gap-1.5 text-small text-ink-soft">
              <Pill tone={STATE_TONE[state.line.kind]}>{state.said.word}</Pill>
              {state.said.rest && <span>{state.said.rest}</span>}
            </span>
          )}
          {/* With no header's facts line to wear them, the choice its actor ran and the full-access chip follow the word. */}
          <ChoiceChip session={session} />
          {startedFull(session) && <AccessChip />}
        </div>
      )}

      {/* How it stands, in the record's note: why a failed session failed, how a finished
          one ended, Daoris's lines in the reader's language and the agent's words as written.
          Not while it runs, when the conversation below is the answer; not parked, where the card above
          already carries it. */}
      {/* A record its agent's refusal of the choice ended before anything ran says so by its code's
          sentence, quiet, in place of the failure's note: the account it is refused on here, none on a teammate's mirror. */}
      {shown === 'not-started'
        ? <p className="m-0 text-body leading-relaxed text-ink-soft">{notStartedLine(t, session)}</p>
        : !running && noted && <Said parts={leftParts(session.noteParts, quest)} />}

      {/* What it left once its tree is gone: work no branch of the person's holds is theirs to review, in the waiting hue; landed
          work is a quiet fact. Work its tree still offers to land is the dock's, with its Accept. Keyed by the branch: an ask to
          discard one session's branch never carries to another's. */}
      {branch ? (
        <Left key={branch.branch} branch={branch} onReview={onReview} onDiscard={onDiscardBranch} discarding={discardingBranch} />
      ) : ownBranch ? (
        <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
          <span className="text-ink-faint">{t('work.head.itsWork')}</span>
          <PathText path={ownBranch} className="min-w-0 text-caption text-ink-faint" />
        </p>
      ) : null}

      <DetailsFold
        aside={origins.length > 0 ? <OriginLine origins={origins} t={t} /> : undefined}
        summary={[
          intake ? t('work.scope.ask', { id: session.ask }) : session.quest ? t('work.scope.quest', { id: session.quest }) : null,
          sessionTool({ door: session.door, doorVersion: session.doorVersion }),
          t('work.facts.started', { ago: ago(session.created) }),
        ].filter(Boolean).join(' · ')}
        rows={[
          intake
            ? { label: t('work.intake.ask'), value: `#${session.ask}`, mono: true }
            : { label: t('work.head.repository'), value: <span title={session.tree ?? undefined}>{session.repository}</span> },
          ...startedRows(t, origins, startingOf(quest, ask, session, opening, brief)),
          linkRow(t, t('work.head.helping'), session.opinion ? null : session.serves, helps, onOpenSession),
          // Its heavy run: the head's line opens to its gates here, or its wait with the holder a door.
          ...(heavy ? [heavy] : []),
          // The commit rule its own tree runs under and where it is set, in Setup's words.
          { label: t('work.head.commits'), value: process ? <Inline text={processCommitsSays(t, process)} /> : null },
          // The access it started under: each pause as its record keeps it, never today's settings.
          accessRow(t, session),
          // What it holds of git access now, one fact, narrowed by what the person tightened.
          gitAccessRow(t, session, gitAccess),
          // A parked line keeping its checkout with uncommitted paths of its own, and the press that lets others work there.
          placeHoldRow(t, session, placeHold),
          // The line's counts and what the person's rules did with them, as the driver counted them.
          ...countRows(t, supervision),
          supervisorRow(t, supervision),
          courseRow(t, supervision),
          { label: t('work.head.quest'), value: session.quest ? `#${session.quest}` : null, mono: true },
          { label: t('work.head.tool'), value: sessionTool({ door: session.door, doorVersion: session.doorVersion }) },
          ...actorRows(t, session),
          {
            label: t('work.head.account'),
            value: session.profile
              ? (nameOf ? nameOf(session.door, session.profile) : session.profile)
              : ownSignIn ? t('quests.session.ownSignIn') : null,
          },
          { label: t('work.head.machine'), value: sessionOrigin(session) },
          { label: t('work.head.started'), value: `${stamp(session.created)} · ${ago(session.created)}` },
          // A running session has an age and a finished one has a lifetime: the label says which.
          {
            label: running ? t('work.head.elapsed') : t('work.head.ran'),
            value: elapsed(session.created, running ? null : session.updated),
          },
          // Only while it runs: an ended session's last move is its end, which *ran* already says.
          { label: t('work.head.moved'), value: running ? ago(movedAt(session, lastTurn)) : null },
          { label: t('work.head.id'), value: session.id, mono: true, copy: session.id },
        ]}
      />

    </header>
  );
}


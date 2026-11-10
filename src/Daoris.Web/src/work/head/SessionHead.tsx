import { type ReactNode, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { SweepBranch } from '../../settings/Sweep';
import type { GoAhead, Quest, Session } from '../../data/api';
import type { AccountNamer } from '../../agents/tools';
import { type GoAheadAnswer, GoAheadList } from '../../asks/GoAheadList';
import { ago, elapsed, sessionTool, stamp } from '../../locales/format';
import {
  answeredPark, Inline, PathText, personsSaid, Pill, SESSION_ACTIVE, SESSION_TONE, shownKey, shownState, WaitingCard,
} from '../../design/ui';
import { cn } from '../../lib/cn';
import { WaitingOnLine } from '../atWork/AtWorkView';
import type { AtWorkA } from '../atWork/types';
import { AnsweredPark } from '../waits/AnsweredPark';
import { AwaitingIntake } from '../waits/AwaitingIntake';
import { type AnswerPick, AwaitingPerson, type QuestClose, type Resolution } from '../waits/AwaitingPerson';
import type { ParkQuestion } from '../waits/parkQuestion';
import { type DetailRow, DetailsFold } from './DetailsFold';
import { AccessChip, accessRow, startedFull } from './AccessFact';
import { gitAccessRow, type SessionGitAccessHold } from './GitAccessFact';
import { placeHoldRow } from './PlaceHoldFact';
import { countRows, courseRow, type LineSupervision, NO_SUPERVISION, supervisorRow } from './lineSupervision';
import { ChoiceChip } from './ChoiceChip';
import type { WaitEvents } from '../waits/anchors';
import type { Answered } from '../InlineConfirm';
import type { LineBehind } from '../review/diff';
import type { DiscardOffer, LandOffer } from '../groups';
import { isIntake, sessionOrigin, sessionTitle } from '../identity';
import type { SayRebase } from '../review/LandingSync';
import { Discards, EntryWork, type HeadOpinion, type HeadReview, type LandingPlan, Lands, type WorkAsk } from '../review/Lands';
import type { EntryPress } from '../review/EntryAccept';
import { Note } from '../Note';
import { Left, Said } from './HeadNote';
import { type HeadLink, linkRow } from './headRows';
import { type AskOrigin, originsOf, OriginLinks, startedRows, startingOf } from './startedFrom';
import type { HelperWaits } from '../waits/HelperAsks';
import { CoWorkers } from '../team/CoWorkers';
import { MemberThread } from '../team/MemberThread';
import { type PieceSection, Pieces } from '../pieces/Pieces';
import { hasNote } from '../noteLines';
import { processCommitsSays, type SessionProcess } from './process';
import { heldFor, PermissionAsks, type PermissionWaits } from '../waits/PermissionAsks';
import { AnswerAlone, answerFor, type AnswerWaits } from '../waits/AnswerAlone';
import { GitAsk, gitAsksFor, type GitAskWaits } from '../waits/GitAsk';
import { ProposedRules, type RuleAsk, type RuleWaits } from '../waits/ProposedRules';
import { movedAt } from '../rail/rail';
import { RunningIntake } from './RunningIntake';
import { leftFor, type Seek, SOUGHT, useSought } from '../waits/WaitAnchor';
import { rulesAsked } from '../waits/waits';
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
 * **Commits its tree holds are offered to land, whatever its ending**: where the driver's reader
 * says what the tree offers (`lands`), the line names the commits, the branch and the tree, and *Accept…* asks once under it,
 * saying where accepting puts the work by the repository's rule, as the review's foot says it, and for a session that did
 * not finish that only what it committed lands. The press is the review's Accept; a refusal is said inside the ask.
 *
 * **Commits the line holds by content are never offered to land again**: where a squash merge or
 * a cherry-pick put its work elsewhere (`discards`), accepting would make a branch of work already there, so no *Accept…*
 * stands. The driver's sentence says where the work is, in Discard's own clause, and *Discard branch…* asks once with the
 * driver's sentence naming the ref its commits stay at; the press is the review's Discard, unforced.
 *
 * **Where a review's gate holds the work, it stands where *Accept…* would**: *Review in `<environment>`* in the gate's state, as the landing's plan says it, with the set-up step's showing
 * and the verdict's presses, and no *Accept…*, which the landing door would refuse.
 *
 * **The second opinion stands before it**: wherever a level asks one,
 * *Second opinion* in its state, its findings beside their answers and its presses, before the review's gate as the gate's order
 * has it. While it holds the work *Accept…* is not offered, except where *Accept…* is the press that answers it (a dispute, or
 * commits nobody read): then its ask says what it answers, and the press sends the gate's token back.
 *
 * **An absence is never a dash.** No tree is the registered root, no profile is the harness's own
 * configuration home, no machine is this deployment's own — and a browser over a keyed remote is
 * told none of them. `MetaLine` drops a pair it has no value for, which is why all four
 * can be passed unconditionally.
 */
export function SessionHead({
  session, quest, opening, brief, taking, lastTurn, resolving = false, onResolve, onAnswerAsk,
  onAnswerSession, onAnswerChoice, answerPick, branch, ownBranch, onReview, onDiscardBranch, discardingBranch = false, headed = false, goAheads = [], onGoAhead,
  ownSignIn = false, nameOf, lands, landing, planning, onLand, onSayRebase, onName, discards, onDiscardTree, review, behind, opinion, onOpenQuest, ask,
  continues, helps, onOpenSession, heavy, process, workAsk, workBusy = false, toEvent, rules, ruleAsk, seek, helpers, permissions, answers, gitAsks,
  question = null, parkBox = false, pieces, entry, waitingOn, gitAccess, terminalHold, supervisorCourse, supervision = NO_SUPERVISION,
}: {
  /** The supervisor's decision on a point its rule referred, the frame's `SessionSupervisorCourse`: *Overrule* while it is held. */
  supervisorCourse?: ReactNode;
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
  /** The session's one *Accept…* where its parts offer work to land, in the place of a part's own. */
  entry?: EntryPress | null;
  /**
   * Its pieces, the frame's: what it asked other agents to write, where each stands, the
   * ask's press and each piece's door and stop. Absent, a browser, a teammate's record or a session that cannot lead one, none is drawn.
   */
  pieces?: PieceSection | null;
  /**
   * Its question as its agent asked it, which its waiting card shows in place of its whole last
   * message; null where it asked none through the connector.
   */
  question?: ParkQuestion | null;
  /**
   * The box below is the park's answer box, and carries every press of what it asks: its go-aheads are listed here with
   * none, so the person answers in one place. False where no such box is below, and the head keeps their presses.
   */
  parkBox?: boolean;
  /**
   * The answers the answers-alone bound holds for the person, the frame's,
   * handed to the foot's strip alike: every one the shell read, its own drawn, the agents' names, the answer's door and its pending.
   */
  answers?: AnswerWaits | null;
  /**
   * The helpers it asked for, the frame's: its asks as the driver's look leaves them, the agents' names, the
   * person's yes and a door to each helper. Absent, a browser or a teammate's record, none is drawn.
   */
  helpers?: HelperWaits | null;
  /**
   * The tool calls it holds for the person's answer under asking, the frame's, handed to the foot's
   * strip alike: every request the shell read, its own drawn, the answer's one door and its pending. Absent, none is drawn.
   */
  permissions?: PermissionWaits | null;
  /** The git go-aheads its git door holds, the frame's, handed to the foot's strip alike. */
  gitAsks?: GitAskWaits | null;
  /**
   * The wait a raising block's *To the wait* leads to while the head's waits are on screen,
   * counted so the same one pressed again is shown again: its line takes the focus, outlined, as the strip's does.
   */
  seek?: Seek | null;
  /**
   * The rules it proposed that the person still answers, the frame's, handed to the foot's strip alike:
   * the machine's proposals as the shell read them, the answer's door, its pending and whether the session takes words now.
   */
  rules?: RuleWaits | null;
  /** Where a rule's *Allow…* is open, the frame's, so it is open in the head or at the foot, never both. */
  ruleAsk?: RuleAsk;
  /**
   * The way from each wait to the event in the conversation that raised it, where the conversation found one:
   * *In the conversation* on the park's card, each go-ahead and the work's line. The frame's, handed to the foot's
   * strip alike.
   */
  toEvent?: WaitEvents;
  /**
   * Where the ask of its work's line (*Accept…*, *Discard branch…*) is open, the frame's, so it is open in the head or in the
   * foot's strip, never both. Absent, the line holds its own.
   */
  workAsk?: WorkAsk;
  /** A landing or a discard of its work is on its way, pressed here or at the foot: the line's press waits for it. */
  workBusy?: boolean;
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
  /** The landing plan shown is the previous one while the entered name's is read; the press waits. */
  planning?: boolean;
  /**
   * The second opinion's gate where a level asks one: its findings beside their answers, where the frame read them,
   * and its presses. Drawn from the landing's plan's own `opinion`.
   */
  opinion?: HeadOpinion | null;
  /**
   * What its own tree offers to land, as the driver's reader says it: commits no branch of the person's holds, on its
   * branch in its tree, whatever its ending. Absent where it offers none, or nothing has answered.
   */
  lands?: LandOffer | null;
  /**
   * The review's gate where it holds that work: the set-up step's record, whether its tab is still served, and
   * the verdict's presses. Read only where the landing's plan says the gate holds.
   */
  review?: HeadReview | null;
  /**
   * How the tree stands against its line, from the review's answer: where `ahead` is 0 and nothing is
   * uncommitted, there is nothing to land and no *Accept…* is offered, as in the review. Absent before it answers or where not read.
   */
  behind?: LineBehind | null;
  /**
   * What its own tree offers where the line holds its commits by content, as the driver's reader says it: its
   * sentences, shown as they are, and whether an unforced discard would go. Absent where it offers none.
   */
  discards?: DiscardOffer | null;
  /** Discard its tree, unforced: the review's Discard, told back to its ask. Absent where nothing can press it. */
  onDiscardTree?: (answered: Answered) => void;
  /**
   * Where accepting would put it, by the repository's rule: the review's own plan, said in the ask before the
   * press. Absent while it is read.
   */
  landing?: LandingPlan | null;
  /**
   * Land it: the review's Accept, told back to its ask, with the second opinion's token where the press answers what it showed
   * Absent where nothing can press it (a browser, a story).
   */
  onLand?: (answered: Answered, answers?: string | null, branch?: string | null) => void;
  /** The press that says the rebase words where replaying conflicts; the frame holds the say door. */
  onSayRebase?: SayRebase;
  /** The branch name typed in the land ask, so the frame asks the plan again with it; `''` when the ask is put down. */
  onName?: (name: string) => void;
  /**
   * Its agent has accounts, so a record naming none ran on the tool's own sign-in, and the head says so where it shows an
   * account.
   */
  ownSignIn?: boolean;
  /** What a person calls an account, from the roster; absent, the record's id is said. */
  nameOf?: AccountNamer;
  /** The go-aheads this session asked on its quest's ask, as the frame reads them; shown while it is parked. */
  goAheads?: GoAhead[];
  /**
   * Answer one of them, which sends the park on once none is open: which go-ahead, yes or no, and
   * the person's words where they gave any. Absent where nothing can answer them here, and then they are shown with no door.
   */
  onGoAhead?: GoAheadAnswer;
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
  resolving?: boolean;
  /**
   * How a parked session is finished or declined, and a finish's close of its quest. Absent where nothing can
   * act — a browser, or a story. Its stop is the page header's, its one owner.
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
  /** A press of the park's question's answer from its card, with the card's words. */
  onAnswerChoice?: (label: string, words: string) => Promise<string | null> | void;
  /** The answer selected in its question, the frame's, shared with the box below; absent, the card holds its own. */
  answerPick?: AnswerPick;
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
  const proposed = rulesAsked(rules?.proposals, session.id);
  const held = heldFor(permissions?.waits ?? [], session.id);
  const alone = answerFor(answers?.waits ?? [], session.id);
  const gitAsked = gitAsksFor(gitAsks?.waits ?? [], session.id);
  const own = useRef<HTMLElement>(null);
  const { sought, letGo } = useSought(own, seek);
  const origins = originsOf(t, session, quest, ask, continues, { onOpenQuest, onOpenAsk: onAnswerAsk, onOpenSession });

  return (
    // Where the foot's *To the top* puts the focus: every wait stands here with its reading. The
    // outline a *To the wait* leaves goes once the person moves on from the head.
    <header ref={own} data-record-head tabIndex={-1} className="grid gap-2.5 outline-none" onBlur={(event) => { if (leftFor(event)) letGo(); }}>
      {/* `running` counts a park as alive; a parked intake is AwaitingIntake's, below. */}
      {running && !parked && intake && <RunningIntake ask={session.ask!} onOpen={onAnswerAsk} />}
      {terminalHold}
      {supervisorCourse}
      {/* A member's thread: whom it works for, *Back to lead*, and once it answered, *Ask again…* and the pass. */}
      {helpers?.team?.thread && <MemberThread {...helpers.team.thread} lead={helps?.title ?? helpers.team.thread.lead} />}
      {answered !== null && <AnsweredPark answer={answered} />}
      {/* What the line waits on of others, At work's line from the same read; the person's own waits stand below. */}
      {waitingOn && <WaitingOnLine answer={waitingOn.answer} nameOf={waitingOn.nameOf} onOpen={waitingOn.onOpen} />}
      {/* Each section a wait stands in is marked: the frame watches them, and the strip at the foot stands aside
          while they are on screen. */}
      {waiting && intake && (onResolve || onAnswerAsk) && (
        <div data-wait-section data-wait="question" tabIndex={-1} className={sought === 'question' ? SOUGHT : 'outline-none'}>
          <AwaitingIntake ask={session.ask!} parts={session.noteParts} onAnswer={onAnswerAsk} onConversation={toEvent?.question} />
        </div>
      )}
      {waiting && !intake && (onResolve || onAnswerSession) && (
        <div data-wait-section data-wait="question" tabIndex={-1} className={sought === 'question' ? SOUGHT : 'outline-none'}>
          {/* Keyed: a half-written decline reason belongs to the session it is written for, and would
              carry into the next parked one, ready to decline it with somebody else's reason. */}
          <AwaitingPerson
            key={session.id}
            parts={session.noteParts}
            question={question}
            // The quest it serves, which its finish may close as the person's: only its own, and a driven one's.
            quest={session.kind !== 'chat' && quest && quest.id === session.quest ? quest : null}
            pending={resolving}
            onResolve={onResolve}
            // A driven session parked to ask the person has no process left to message, and is answered from its card
            // whether or not it holds a quest, and whether or not the box below answers it too. A parked chat's answer
            // is its composer's.
            onAnswer={session.kind !== 'chat' ? onAnswerSession : undefined}
            onChoice={session.kind !== 'chat' ? onAnswerChoice : undefined}
            pick={answerPick}
            below={parkBox}
            onConversation={toEvent?.question}
          />
        </div>
      )}
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
      {parked && !intake && goAheads.length > 0 && (
        <section data-wait-section aria-labelledby={`go-aheads-${session.id}`} className="grid gap-1">
          <h3 id={`go-aheads-${session.id}`} className="m-0 text-small font-semibold">{t('work.awaiting.goAheads')}</h3>
          <GoAheadList
            goAheads={goAheads}
            busy={resolving}
            // Where the box below answers the park, its presses are the box's, so the person answers in one place.
            onAnswer={parkBox ? undefined : onGoAhead}
            lead={t(parkBox ? 'work.park.inTheBox' : 'work.awaiting.goAheadsLead')}
            inConversation={toEvent?.goAheads}
            marked={sought?.startsWith('goAhead:') ? Number(sought.slice('goAhead:'.length)) : undefined}
          />
        </section>
      )}
      {/* The tool calls it holds for the person's answer: its turn stands still until they answer, or
          the bound refuses it. A wait's section where an answer can be given here, so the strip stands aside for it too. */}
      {held.length > 0 && (
        <section
          aria-labelledby={`permissions-${session.id}`}
          data-wait-section={permissions?.onAnswer ? '' : undefined}
          className="grid gap-1"
        >
          <h3 id={`permissions-${session.id}`} className="m-0 text-small font-semibold">{t('work.wait.permission.title')}</h3>
          <PermissionAsks
            waits={held}
            onAnswer={permissions?.onAnswer}
            busy={permissions?.busy}
            lead={t('work.wait.permission.lead')}
            inConversation={toEvent?.permissions}
            marked={sought?.startsWith('permission:') ? sought.slice('permission:'.length) : undefined}
          />
        </section>
      )}
      {/* The git go-aheads its git door holds: the act on the remote stands still until they answer, or its
          bound. A wait's section where an answer can be given here, so the strip stands aside for it too. */}
      {gitAsked.length > 0 && (
        <section
          aria-labelledby={`git-asks-${session.id}`}
          data-wait-section={gitAsks?.onAnswer ? '' : undefined}
          className="grid gap-1"
        >
          <h3 id={`git-asks-${session.id}`} className="m-0 text-small font-semibold">{t('work.wait.gitAsk.title')}</h3>
          {gitAsked.map((ask) => (
            <GitAsk key={ask.id} ask={ask} onAnswer={gitAsks?.onAnswer} busy={gitAsks?.busy} marked={sought === `gitAsk:${ask.id}`} />
          ))}
        </section>
      )}
      {/* A third answer in a row with nobody between, which the answers-alone bound holds for the person: its line
          stands still until they answer. A wait's section where it can be answered here, so the strip stands aside for it too. */}
      {alone && (
        <section
          aria-labelledby={`answer-alone-${session.id}`}
          data-wait-section={answers?.onAnswer ? '' : undefined}
          className="grid gap-1"
        >
          <h3 id={`answer-alone-${session.id}`} className="m-0 text-small font-semibold">{t('work.wait.answer.title')}</h3>
          <AnswerAlone
            wait={alone}
            nameOf={answers?.nameOf}
            onAnswer={answers?.onAnswer}
            onChangeRule={answers?.onChangeRule}
            busy={answers?.busy}
          />
        </section>
      )}
      {proposed.length > 0 && (
        <section data-wait-section aria-labelledby={`rules-${session.id}`} className="grid gap-1">
          <h3 id={`rules-${session.id}`} className="m-0 text-small font-semibold">{t('work.wait.rule.title')}</h3>
          <ProposedRules
            proposals={proposed}
            busy={rules?.busy}
            onAnswer={rules?.onAnswer}
            heard={rules?.heard}
            lead={t('work.wait.rule.lead')}
            inConversation={toEvent?.rules}
            marked={sought?.startsWith('rule:') ? sought.slice('rule:'.length) : undefined}
            ask={ruleAsk}
          />
        </section>
      )}

      {/* Its co-workers: the lead and each member, its thread's door and the pass, an agent's
          ask that waits on the person's yes with the yes and the no, a wait's section while one does. */}
      {helpers && (
        <CoWorkers
          session={session.id}
          helpers={helpers}
          lead={helpers.team?.lead ?? session.door}
          onPass={helpers.team?.onPass}
          passing={helpers.team?.passing}
          choices={helpers.team?.choices}
          outside={helpers.team?.outside}
          onRemoveOutside={helpers.team?.onRemoveOutside}
          removingOutside={helpers.team?.removingOutside}
          inConversation={toEvent?.helpers}
          marked={sought?.startsWith('helper:') ? sought.slice('helper:'.length) : undefined}
        />
      )}

      {/* Its pieces: what other agents write for it, each in a clone of its own, and the ask of one. */}
      {pieces && (pieces.pieces.length > 0 || pieces.ask) && <Pieces section={pieces} headingId={`pieces-${session.id}`} />}

      {!headed && (
        /* A window with no header (a detached session): the title and its word are said here; its id is its *Details*',
           as on the page. The state follows the title rather than the far edge (status leads), two lines at
           most, whole on hover. */
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1.5">
          <h2 className="m-0 line-clamp-2 text-title font-[650] leading-[1.35]" title={sessionTitle(session, quest, opening)}>
            {sessionTitle(session, quest, opening)}
          </h2>
          <span className="shrink-0"><Pill tone={SESSION_TONE[shown]}>{t(shownKey(shown))}</Pill></span>
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
        : !running && noted && <Said parts={session.noteParts} />}

      {/* What it left, and the move that acts on it: work no branch of the person's holds is
          theirs to review, in the waiting hue; landed work is a quiet fact. */}
      {/* Keyed by the branch: an ask to discard one session's branch never carries to another's. Commits
          its tree offers to land are the reader's to say, keyed by the session for the same reason. */}
      {lands ? (
        // Spaced as the head spaces its own.
        <div data-wait-section data-wait="work" tabIndex={-1} className={cn('grid gap-2.5', sought === 'work' || sought === 'opinion' ? SOUGHT : 'outline-none')}>
          <Lands
            key={session.id} session={session} lands={lands} landing={landing} planning={planning} onReview={onReview} onLand={onLand} onSayRebase={onSayRebase} onName={onName} review={review} behind={behind}
            opinion={opinion} ask={workAsk} busy={workBusy} toEvent={toEvent} entry={entry}
          />
        </div>
      ) : entry ? (
        <div data-wait-section data-wait="work" tabIndex={-1} className={cn('grid gap-2.5', sought === 'work' ? SOUGHT : 'outline-none')}>
          <EntryWork entry={entry} />
        </div>
      ) : discards ? (
        <div data-wait-section className="grid gap-2.5">
          <Discards key={session.id} discards={discards} onReview={onReview} onDiscard={onDiscardTree} ask={workAsk} busy={workBusy} />
        </div>
      ) : branch ? (
        <Left key={branch.branch} branch={branch} onReview={onReview} onDiscard={onDiscardBranch} discarding={discardingBranch} />
      ) : ownBranch ? (
        <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
          <span className="text-ink-faint">{t('work.head.itsWork')}</span>
          <PathText path={ownBranch} className="min-w-0 text-meta text-ink-faint" />
        </p>
      ) : null}

      {/* The reference, a folded *Details*, its line naming the quest,
          the agent and the start, so the conversation is not pushed down by facts that are looked up. The tree's machine
          path is the repository's on hover; an intake names its ask, never `repository: ask #…`. */}
      {origins.length > 0 && (
        <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-4 gap-y-1 text-small text-ink-soft">
          <span className="text-ink-faint">{t('work.origin.from')}</span>
          <OriginLinks origins={origins} t={t} />
        </p>
      )}
      <DetailsFold
        summary={[
          intake ? t('work.scope.ask', { id: session.ask }) : session.quest ? t('work.scope.quest', { id: session.quest }) : null,
          sessionTool({ door: session.door, doorVersion: session.doorVersion }),
          t('work.facts.started', { ago: ago(session.created) }),
        ].filter(Boolean).join(' · ')}
        rows={[
          intake
            ? { label: t('work.intake.ask'), value: `#${session.ask}`, mono: true }
            : { label: t('work.head.repository'), value: <span title={session.tree ?? undefined}>{session.repository}</span> },
          // Where it came from and the requirement it started with, each said by the record that holds it.
          ...startedRows(t, origins, startingOf(quest, ask, session, opening, brief)),
          // Whom it helps, the record's own link, a door where the frame holds it.
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
          placeHoldRow(t, session),
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


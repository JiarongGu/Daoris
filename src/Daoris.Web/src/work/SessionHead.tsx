import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { workflowHolds } from '../workflow/gate';
import { WorkflowHold } from '../workflow/WorkflowHold';
import { DiscardBranchAsk, type SweepBranch } from '../settings/Sweep';
import type { GoAhead, Quest, Session } from '../api';
import type { AccountNamer } from '../tools';
import { GoAheadList } from '../asks/GoAheadList';
import { ago, elapsed, sessionTool, stamp } from '../format';
import {
  answeredPark, Button, Inline, PathText, Pill, SESSION_ACTIVE, SESSION_TONE, shownKey, shownState, WaitingCard,
} from '../ui';
import { cn } from '../lib/cn';
import { AnsweredPark } from './AnsweredPark';
import { AwaitingIntake } from './AwaitingIntake';
import { AwaitingPerson, type QuestClose, type Resolution } from './AwaitingPerson';
import { DetailsFold } from './DetailsFold';
import { type Answered, InlineConfirm } from './InlineConfirm';
import { useCut } from './ViewMain';
import type { DiscardOffer, LandOffer } from './groups';
import { HeldDiscardAsk, HeldDiscardButton, heldDiscardable } from './HeldDiscard';
import { isIntake, sessionOrigin, sessionTitle, shortened } from './identity';
import { Note } from './Note';
import { hasNote, noteBlocks, noteLines } from './noteLines';
import {
  acceptAnswers, type OpinionDetail, type OpinionGate as OpinionGateState, opinionAsked, opinionHolds, opinionState,
} from './opinion';
import { OpinionGate, type OpinionGateActs, unsettledWords } from './OpinionGate';
import type { FileOpen } from './preview';
import { movedAt } from './rail';
import { reviewHolds, reviewState, type ReviewWaits } from './review';
import { ReviewGate, type ReviewGateActs } from './ReviewGate';
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
 * **A park the person answered is `AnsweredPark`'s** (ANSWER1c): the record stays parked with the answer until the
 * driver's next look, when the same session goes on, so the head shows the answer and says so, with no moves, wherever
 * the frame could act.
 *
 * **The go-aheads a park asked stand beneath its card** (KNOWUSE1a2, D135 §2), the ask's own list: each with *Approve*
 * and *Refuse* where the frame can answer, and answering the last one open there sends the same session on, as on the
 * ask's page (GOAHEAD2b). They stay while it is parked, answered or not, so one still waiting can be answered before it
 * goes on.
 *
 * **It says how an ended session stands, in the record's note** (SESS2, reversing the rule that it
 * did not): the rule rested on *"the timeline below carries it"*, and since FRAME6 the timeline is in
 * the side bar, which starts closed (U7) — so a failed session's reason and a finished one's ending
 * were on no screen the person had open. A running session says nothing here, since its conversation
 * is the answer, and a parked one's note is its card's.
 *
 * **What it left comes with its move** (SESS2): work no branch of the person's holds has *review*
 * beside it, where the metadata's last pair used to say it with nothing to press. A failed attempt's branch whose tree is
 * gone has *Discard branch…* too (LAND3b), asking once, since no clean-up takes it and the review has no tree to discard.
 *
 * **Commits its tree holds are offered to land, whatever its ending** (LAND4, D102's LAND4 note): where the driver's reader
 * says what the tree offers (`lands`), the line names the commits, the branch and the tree, and *Accept…* asks once under it,
 * saying where accepting puts the work by the repository's rule, as the review's foot says it, and for a session that did
 * not finish that only what it committed lands. The press is the review's Accept; a refusal is said inside the ask.
 *
 * **Commits the line holds by content are never offered to land again** (SQUASHTIDY1b, D102's note): where a squash merge or
 * a cherry-pick put its work elsewhere (`discards`), accepting would make a branch of work already there, so no *Accept…*
 * stands. The driver's sentence says where the work is, in Discard's own clause, and *Discard branch…* asks once with the
 * driver's sentence naming the ref its commits stay at; the press is the review's Discard, unforced.
 *
 * **Where a review's gate holds the work, it stands where *Accept…* would** (REVIEWENV1g, D154 point 7; the review environment
 * design §3.1): *Review in `<environment>`* in the gate's state, as the landing's plan says it, with the set-up step's showing
 * and the verdict's presses, and no *Accept…*, which the landing door would refuse.
 *
 * **The second opinion stands before it** (XAGENT1g, D155 point 9; the second-agent design §7, §9): wherever a level asks one,
 * *Second opinion* in its state, its findings beside their answers and its presses, before the review's gate as the gate's order
 * has it. While it holds the work *Accept…* is not offered, except where *Accept…* is the press that answers it (a dispute, or
 * commits nobody read): then its ask says what it answers, and the press sends the gate's token back (§8.2–§8.3).
 *
 * **An absence is never a dash.** No tree is the registered root, no profile is the harness's own
 * configuration home, no machine is this deployment's own — and a browser over a keyed remote is
 * told none of them (D47 §4). `MetaLine` drops a pair it has no value for, which is why all four
 * can be passed unconditionally.
 */
export function SessionHead({
  session, quest, opening, taking, lastTurn, resolving = false, onResolve, onAnswerAsk,
  onAnswerSession, branch, onReview, onDiscardBranch, discardingBranch = false, headed = false, goAheads = [], onGoAhead,
  ownSignIn = false, nameOf, lands, landing, onLand, discards, onDiscardTree, review, opinion,
}: {
  /**
   * The second opinion's gate where a level asks one (XAGENT1g): its findings beside their answers, where the frame read them,
   * and its presses. Drawn from the landing's plan's own `opinion`.
   */
  opinion?: HeadOpinion | null;
  /**
   * What its own tree offers to land (LAND4), as the driver's reader said it: commits no branch of the person's holds, on its
   * branch in its tree, whatever its ending. Absent where it offers none, or nothing has answered.
   */
  lands?: LandOffer | null;
  /**
   * The review's gate where it holds that work (REVIEWENV1g): the set-up step's record, whether its tab is still served, and
   * the verdict's presses. Read only where the landing's plan says the gate holds.
   */
  review?: HeadReview | null;
  /**
   * What its own tree offers where the line holds its commits by content (SQUASHTIDY1b), as the driver's reader said it: its
   * sentences, shown as they are, and whether an unforced discard would go. Absent where it offers none.
   */
  discards?: DiscardOffer | null;
  /** Discard its tree, unforced: the review's Discard, told back to its ask. Absent where nothing can press it. */
  onDiscardTree?: (answered: Answered) => void;
  /**
   * Where accepting would put it, by the repository's rule (D87, D100): the review's own plan, said in the ask before the
   * press. Absent while it is read.
   */
  landing?: LandingPlan | null;
  /**
   * Land it: the review's Accept, told back to its ask, with the second opinion's token where the press answers what it showed
   * (XAGENT1g). Absent where nothing can press it (a browser, a story).
   */
  onLand?: (answered: Answered, answers?: string | null) => void;
  /**
   * Its agent has accounts, so a record naming none ran on the tool's own sign-in, and the head says so where it shows an
   * account (D125 §3.7, TOOL4m's rest with UX6e).
   */
  ownSignIn?: boolean;
  /** What a person calls an account (ACCTNAME1, D152 §4.2), from the roster; absent, the record's id is said. */
  nameOf?: AccountNamer;
  /** The go-aheads this session asked on its quest's ask (KNOWUSE1a2), as the frame read them; shown while it is parked. */
  goAheads?: GoAhead[];
  /**
   * Answer one of them (KNOWUSE1a2), which sends the park on once none is open (GOAHEAD2b): which go-ahead, yes or no, and
   * the person's words where they gave any. Absent where nothing can answer them here, and then they are shown with no door.
   */
  onGoAhead?: (number: number, approved: boolean, words?: string) => void;
  /**
   * A page header above carries its state, its title and its facts (SESSUX1d, D126 §3.2; UX7c, D152 §7): the head says
   * none again, and the quest's whole title only where the header showed its short title. Absent, a window with no header
   * (a detached session), the head keeps the title, its word and its id.
   */
  headed?: boolean;
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
  /**
   * Discard that branch, its commits with it (LAND3b): offered once its tree is gone, where the driver says its commits are a
   * failed or superseded attempt's, and asked once first; told back to its ask (UXFIX2). Absent where nothing here can press
   * it.
   */
  onDiscardBranch?: (answered: Answered) => void;
  /** That discard is on its way: its presses wait for it. */
  discardingBranch?: boolean;
  /** Whether a turn is in flight, as the driver says: a live chat between turns reads idle (UX5 U17). */
  taking?: boolean;
  /** The quest it serves, where the caller has it — absent is a state, not a gap (D49 §3). */
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name (RAIL1). */
  opening?: string | null;
  resolving?: boolean;
  /**
   * How a parked session is finished or declined, and a finish's close of its quest (QUESTCLOSE1). Absent where nothing can
   * act — a browser, or a story. Its stop is the page header's, its one owner (D126 §3.3).
   */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose) => void;
  /**
   * Open the ask an intake serves (INT4g) — a parked one's answer is there, not on the session.
   * Absent where nothing can open it.
   */
  onAnswerAsk?: (ask: string) => void;
  /**
   * Answer a driven session that parked to ask the person (STANDDOWN2): the same session goes on with the
   * words at the driver's next look (D131). Absent where nothing can reach this machine's host.
   */
  onAnswerSession?: (answer: string | null) => void;
}) {
  const { t } = useTranslation();
  const running = SESSION_ACTIVE.has(session.state);
  const parked = session.state === 'awaiting-person';
  // ANSWER1c: answered, it goes on at the driver's next look, so nothing below asks the person again.
  const answered = answeredPark(session);
  const waiting = parked && !answered;
  const intake = isIntake(session);
  const shown = shownState(session, taking);
  const noted = hasNote({ note: session.note, parts: session.noteParts });

  return (
    <header className="grid gap-2.5">
      {/* `running` counts a park as alive; a parked intake is AwaitingIntake's, below. */}
      {running && !parked && intake && <RunningIntake ask={session.ask!} onOpen={onAnswerAsk} />}
      {answered && <AnsweredPark answer={session.answer!} />}
      {waiting && intake && (onResolve || onAnswerAsk) && (
        <AwaitingIntake ask={session.ask!} note={session.note} parts={session.noteParts} onAnswer={onAnswerAsk} />
      )}
      {waiting && !intake && (onResolve || onAnswerSession) && (
        // Keyed: a half-written decline reason belongs to the session it was written for, and it
        // carried into the next parked one, ready to decline it with somebody else's reason (REV3).
        <AwaitingPerson
          key={session.id}
          note={session.note}
          parts={session.noteParts}
          // The quest it serves, which its finish may close as the person's (QUESTCLOSE1): only its own, and a driven one's.
          quest={session.kind !== 'chat' && quest && quest.id === session.quest ? quest : null}
          pending={resolving}
          onResolve={onResolve}
          // A driven session parked to ask the person has no process left to message (STANDDOWN2).
          onAnswer={session.quest ? onAnswerSession : undefined}
        />
      )}
      {/* Nothing here can act — a browser, or a mirrored record from another machine — so the
          analysis is shown and the moves are not. Half a control is worse than none. */}
      {waiting && !onResolve && !onAnswerSession && !(intake && onAnswerAsk) && noted && (
        <WaitingCard title={t('work.head.waiting')}>
          <Note note={session.note} parts={session.noteParts} className="mt-1.5 text-body leading-relaxed" />
        </WaitingCard>
      )}
      {parked && !intake && goAheads.length > 0 && (
        <section aria-labelledby={`go-aheads-${session.id}`} className="grid gap-1">
          <h3 id={`go-aheads-${session.id}`} className="m-0 text-small font-semibold">{t('work.awaiting.goAheads')}</h3>
          <GoAheadList
            goAheads={goAheads}
            busy={resolving}
            onAnswer={onGoAhead}
            lead={t('work.awaiting.goAheadsLead')}
          />
        </section>
      )}

      {headed ? (
        /* Under a page header (UX7c, D152 §7): the header says the title, so the record says it again only where the
           header's was not all of it, the quest's short title (SESSUX1j): then the whole title, once, at body size. */
        shortened(quest) && <WholeTitle title={quest!.title} />
      ) : (
        /* A window with no header (a detached session): the title and its word are said here; its id is its *Details*'
           (UX7c), as on the page. The state follows the title rather than the far edge (§4: status leads), two lines at
           most, whole on hover (SESS2). */
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1.5">
          <h2 className="m-0 line-clamp-2 text-title font-[650] leading-[1.35]" title={sessionTitle(session, quest, opening)}>
            {sessionTitle(session, quest, opening)}
          </h2>
          <span className="shrink-0"><Pill tone={SESSION_TONE[shown]}>{t(shownKey(shown))}</Pill></span>
        </div>
      )}

      {/* How it stands, in the record's note (SESS2 H5, H6): why a failed session failed, how a finished
          one ended, Daoris's lines in the reader's language and the agent's words as written (LANG1b).
          Not while it runs, when the conversation below is the answer; not parked, where the card above
          already carries it. */}
      {!running && noted && <Said note={session.note} parts={session.noteParts} />}

      {/* What it left, and the move that acts on it (SESS2 H4): work no branch of the person's holds is
          theirs to review, in the waiting hue; landed work is a quiet fact. */}
      {/* Keyed by the branch: an ask to discard one session's branch never carries to another's (REV3's lesson). Commits
          its tree offers to land are the reader's to say (LAND4), keyed by the session for the same reason. */}
      {lands ? (
        <Lands
          key={session.id} session={session} lands={lands} landing={landing} onReview={onReview} onLand={onLand} review={review}
          opinion={opinion}
        />
      ) : discards ? (
        <Discards key={session.id} discards={discards} onReview={onReview} onDiscard={onDiscardTree} />
      ) : branch && (
        <Left key={branch.branch} branch={branch} onReview={onReview} onDiscard={onDiscardBranch} discarding={discardingBranch} />
      )}

      {/* The reference (UX7c, D152 §7): the meta line's eight pairs became a folded *Details*, its line naming the quest,
          the agent and the start, so the conversation is not pushed down by facts that are looked up. The tree's machine
          path is the repository's on hover; an intake names its ask, never `repository: ask #…` (INT4g). */}
      <DetailsFold
        summary={[
          intake ? t('work.scope.ask', { id: session.ask }) : session.quest ? t('work.scope.quest', { id: session.quest }) : null,
          sessionTool({ adapter: session.adapter, harnessVersion: session.harnessVersion }),
          t('work.facts.started', { ago: ago(session.created) }),
        ].filter(Boolean).join(' · ')}
        rows={[
          intake
            ? { label: t('work.intake.ask'), value: `#${session.ask}`, mono: true }
            : { label: t('work.head.repository'), value: <span title={session.tree ?? undefined}>{session.repository}</span> },
          { label: t('work.head.quest'), value: session.quest ? `#${session.quest}` : null, mono: true },
          { label: t('work.head.tool'), value: sessionTool({ adapter: session.adapter, harnessVersion: session.harnessVersion }) },
          {
            label: t('work.head.account'),
            value: session.profile
              ? (nameOf ? nameOf(session.adapter, session.profile) : session.profile)
              : ownSignIn ? t('quests.session.ownSignIn') : null,
          },
          { label: t('work.head.machine'), value: sessionOrigin(session) },
          { label: t('work.head.started'), value: `${stamp(session.created)} · ${ago(session.created)}` },
          // A running session has an age and a finished one has a lifetime: the label says which (SESS2).
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

/** How much of the record's note the head shows before the rest is a press away. */
const NOTE_LINES = 3;

/**
 * The quest's whole title, once, under a head that showed its short title (UX7c, D152 §7): body size, two lines, the rest
 * a press away. Content, shown as it is (platform language §4).
 */
export function WholeTitle({ title }: { title: string }) {
  const { t } = useTranslation();
  const own = useRef<HTMLParagraphElement>(null);
  const [whole, setWhole] = useState(false);
  const cut = useCut(own, title);
  return (
    <div className="grid justify-items-start gap-1">
      <p
        ref={own}
        className={cn('m-0 w-full text-body leading-relaxed text-ink [overflow-wrap:anywhere]', !whole && 'line-clamp-2')}
        title={title}
      >
        {title}
      </p>
      {(cut || whole) && (
        <Button variant="ghost" className="px-0 py-0 text-small" onClick={() => setWhole((was) => !was)}>
          {t(whole ? 'work.head.noteLess' : 'work.head.noteMore')}
        </Button>
      )}
    </div>
  );
}

/**
 * The record's note about how the session stands (LANG1b): Daoris's lines worded in the reader's language, someone's
 * words as written, a record from before as it was kept. Long is judged on what is shown, a block at least a line each.
 */
function Said({ note, parts }: { note?: string | null; parts?: Session['noteParts'] }) {
  const { t, i18n } = useTranslation();
  const [whole, setWhole] = useState(false);
  const blocks = noteBlocks(t, noteLines(t, { note, parts }, i18n.language));
  const shown = blocks.reduce((count, block) => count + block.text.split('\n').length, 0);
  const long = blocks.reduce((count, block) => count + block.text.length, 0) > 280 || shown > NOTE_LINES;
  return (
    <div className="grid justify-items-start gap-1">
      <Note
        note={note}
        parts={parts}
        clamp={whole ? undefined : NOTE_LINES}
        className="w-full text-body leading-relaxed text-ink-soft"
      />
      {long && (
        <Button variant="ghost" className="px-0 py-0 text-small" onClick={() => setWhole((was) => !was)}>
          {t(whole ? 'work.head.noteLess' : 'work.head.noteMore')}
        </Button>
      )}
    </div>
  );
}

/**
 * What its own tree left, and whether the person has anything to do about it (SESS1 S10, SESS2 H4). A failed or superseded
 * attempt's branch whose tree is gone is discarded here (LAND3b, D102's LAND3 note), asking once under the line: no clean-up
 * takes its commits, and the review has no tree left to discard. While the tree is here, the review's Discard serves.
 */
function Left({ branch, onReview, onDiscard, discarding = false }: {
  branch: SweepBranch;
  onReview?: () => void;
  onDiscard?: (answered: Answered) => void;
  discarding?: boolean;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const theirs = branch.kind === 'unlanded' || branch.kind === 'dirty';
  const discardable = Boolean(onDiscard && !branch.hasTree && branch.discardable);
  return (
    <>
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
        <span className="text-ink-faint">{t('work.head.itsWork')}</span>
        <span className={theirs ? 'text-ink-open' : 'text-ink-soft'}>{landed(t, branch)}</span>
        {/* Whole, as a branch row's name is (UXFIX4b): it wraps after its separators on this line's own row, never cut. */}
        <PathText path={branch.branch} className="min-w-0 text-meta text-ink-faint" />
        {theirs && onReview && (
          <Button className="px-2 py-0.5 text-small" onClick={onReview}>{t('work.head.review')}</Button>
        )}
        {discardable && !asking && (
          <Button variant="danger" className="px-2 py-0.5 text-small" disabled={discarding} onClick={() => setAsking(true)}>
            {t('settings.sweep.discard')}
          </Button>
        )}
      </p>
      {/* Open until the discard answers, a branch the driver kept said inside it (UXFIX2). */}
      {discardable && asking && (
        <DiscardBranchAsk branch={branch} busy={discarding} onDiscard={onDiscard!} onClose={() => setAsking(false)} />
      )}
    </>
  );
}

/**
 * Where accepting a session's work would put it (D87, D100): the driver's `LANDING` answer, which the review reads too; and the
 * review's gate while it holds the work (REVIEWENV1c), absent where nothing waits for a review.
 */
export type LandingPlan = {
  workflow?: import('../workflow/gate').WorkflowGateState | null;
  session?: string; form?: string; target?: string; source?: string; plugin?: string; problem?: string;
  review?: ReviewWaits | null;
  /** The second opinion's gate where a level asks one (XAGENT1f), which the head draws before the review's (XAGENT1g). */
  opinion?: OpinionGateState | null;
};

/** The review's gate as the head draws it (REVIEWENV1g): the set-up step's record where the frame holds it, and the presses. */
export type HeadReview = { step?: Quest | null; served?: boolean | null; acts?: ReviewGateActs; busy?: boolean };

/**
 * The second opinion as the head draws it (XAGENT1g): its findings beside their answers where the frame read them, the presses,
 * and the side bar's preview a finding's place opens.
 */
export type HeadOpinion = {
  detail?: OpinionDetail | null; acts?: OpinionGateActs; busy?: boolean; onOpenFile?: (open: FileOpen) => void;
};

/**
 * What its own tree offers to land, and its press (LAND4, D102's LAND4 note): the commits no branch of the person's holds,
 * on its branch in its tree, with *Review* where the review can open and *Accept…*, which asks once under the line. The ask
 * says where accepting puts the work by the repository's rule, what would refuse it now, the uncommitted work a landing
 * refuses, and for a session that did not finish that only what it committed lands. The move is the review's Accept, in
 * the primary's hue: a landing destroys nothing. Open until the landing answers, a refusal said inside it (UXFIX2).
 */
function Lands({ session, lands, landing, onReview, onLand, review, opinion }: {
  session: Session;
  lands: LandOffer;
  landing?: LandingPlan | null;
  onReview?: () => void;
  onLand?: (answered: Answered, answers?: string | null) => void;
  review?: HeadReview | null;
  opinion?: HeadOpinion | null;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  // While the review's gate holds the work (REVIEWENV1g, design §3.1), *Review in `<environment>`* stands where *Accept…*
  // would, in the gate's state, and no *Accept…* is offered: the landing door would refuse it.
  const waits = reviewHolds(landing);
  const workflow = workflowHolds(landing);
  // The second opinion's gate comes first (XAGENT1g, the second-agent design §7): while it holds, no *Accept…*, unless *Accept…*
  // is the press that answers it, drawn with the token it sends back (§8.2–§8.3).
  const second = opinionAsked(landing);
  const opinionWaits = opinionHolds(landing);
  const answers = opinionWaits && acceptAnswers(opinionWaits) ? opinionWaits.answers ?? null : null;
  const acceptable = !workflow && !waits && (!opinionWaits || answers !== null);
  // A session that did not finish lands what it committed before it ended (LAND4); one finished, at a checkpoint or by
  // itself, lands its work.
  const unfinished = session.state !== 'completed';
  const where = !landing?.target ? null
    : landing.form === 'merge' ? t('work.review.landsOnLine', { line: landing.target })
      : landing.plugin ? t('work.review.landsOnBranchPlugin', { branch: landing.target, plugin: landing.plugin })
        : t('work.review.landsOnBranch', { branch: landing.target });
  return (
    <>
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
        <span className="text-ink-faint">{t('work.head.itsWork')}</span>
        <span className="text-ink-open">{t('work.head.landed.not', { count: lands.commits })}</span>
        <PathText path={lands.branch} className="min-w-0 text-meta text-ink-faint" />
        <span className="text-ink-faint">{t('work.head.inTree', { tree: lands.tree })}</span>
        {onLand && !asking && acceptable && (
          <Button variant="primary" className="px-2 py-0.5 text-small" onClick={() => setAsking(true)}>{t('work.head.accept')}</Button>
        )}
        {onReview && <Button className="px-2 py-0.5 text-small" onClick={onReview}>{t('work.head.review')}</Button>}
      </p>
      {workflow && <WorkflowHold gate={workflow} />}
      {second && (
        <OpinionGate
          gate={second}
          detail={opinion?.detail ?? null}
          acts={opinion?.acts}
          busy={opinion?.busy}
          // A press of the person's answers a dispute here: *Accept…*, or D154's *Reviewed* while its look holds the work.
          pressComing={Boolean(!workflow && ((onLand && acceptable) || waits))}
          working={session.id}
          onOpenFile={opinion?.onOpenFile}
        />
      )}
      {waits && (
        <ReviewGate
          environment={waits.environment ?? ''}
          state={reviewState(waits.state)}
          step={review?.step ?? null}
          stepId={waits.quest ?? null}
          served={review?.served ?? null}
          said={waits.says ?? null}
          acts={review?.acts}
          busy={review?.busy}
        />
      )}
      {onLand && asking && acceptable && (
        <InlineConfirm
          tone="primary"
          block
          label={t('work.head.landTitle', { branch: lands.branch })}
          says={(
            <>
              <p className="m-0 text-small text-ink-soft"><Inline text={where ?? t('work.head.landReading')} /></p>
              {/* What the press answers, shown in it (the second-agent design §8.2–§8.3): making it is the person's answer. */}
              {answers && opinionWaits && (
                <p className="m-0 border-l-[3px] border-st-open pl-2 text-small text-ink-soft">
                  {t('opinion.accept.answers', { unsettled: unsettledWords(t, opinionState(opinionWaits.state), opinionWaits) })}
                </p>
              )}
              {landing?.problem && (
                <p className="m-0 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
                  <Inline text={t('work.review.landingProblem', { problem: landing.problem })} />
                </p>
              )}
              {typeof lands.uncommitted === 'number' && lands.uncommitted > 0 && (
                <p className="m-0 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
                  {t('work.head.landUncommitted', { count: lands.uncommitted })}
                </p>
              )}
              {unfinished && <p className="m-0 text-small text-ink-soft">{t('work.head.landUnfinished')}</p>}
            </>
          )}
          meanIt={t('work.head.landMeanIt')}
          onConfirm={(answered) => onLand?.(answered, answers)}
          onClose={() => setAsking(false)}
        />
      )}
    </>
  );
}

/**
 * What its own tree offers where the line holds its commits by content (SQUASHTIDY1b, D102's note): the driver's sentence where
 * *Accept…* would be, its branch and tree, *Review*, and *Discard branch…* where an unforced discard would go now. The ask says
 * the driver's sentence naming the ref the commits stay at; the press is the review's Discard, unforced, and its tree goes
 * with the branch. Open until the discard answers, a refusal said inside it (UXFIX2). The driver's words are content here, as
 * the review's own sentence is (`DiffPane`'s `said`), so no catalogue words them again. The press and its ask are
 * `HeldDiscard`'s, which the review offers in *Accept*'s place too (SQUASHTIDY1f).
 */
function Discards({ discards, onReview, onDiscard }: {
  discards: DiscardOffer;
  onReview?: () => void;
  onDiscard?: (answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const discardable = heldDiscardable(discards, onDiscard);
  return (
    <>
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
        <span className="min-w-0 text-ink-soft"><Inline text={discards.says} /></span>
        <PathText path={discards.branch} className="min-w-0 text-meta text-ink-faint" />
        <span className="text-ink-faint">{t('work.head.inTree', { tree: discards.tree })}</span>
        {discardable && !asking && <HeldDiscardButton className="px-2 py-0.5 text-small" onAsk={() => setAsking(true)} />}
        {onReview && <Button className="px-2 py-0.5 text-small" onClick={onReview}>{t('work.head.review')}</Button>}
      </p>
      {discardable && asking && (
        <HeldDiscardAsk branch={discards.branch} keptAt={discards.keptAt!} onDiscard={onDiscard!} onClose={() => setAsking(false)} />
      )}
    </>
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

import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { DiscardBranchAsk, type SweepBranch } from '../settings/Sweep';
import type { GoAhead, Quest, Session } from '../data/api';
import type { AccountNamer } from '../agents/tools';
import { type GoAheadAnswer, GoAheadList } from '../asks/GoAheadList';
import { ago, elapsed, sessionTool, stamp } from '../locales/format';
import {
  answeredPark, Button, Inline, PathText, Pill, SESSION_ACTIVE, SESSION_TONE, shownKey, shownState, WaitingCard,
} from '../design/ui';
import { cn } from '../lib/cn';
import { AnsweredPark } from './AnsweredPark';
import { AwaitingIntake } from './AwaitingIntake';
import { AwaitingPerson, type QuestClose, type Resolution } from './AwaitingPerson';
import { type DetailRow, DetailsFold } from './DetailsFold';
import type { WaitEvents } from './anchors';
import type { Answered } from './InlineConfirm';
import type { LineBehind } from './diff';
import type { DiscardOffer, LandOffer } from './groups';
import { isIntake, sessionOrigin, sessionTitle } from './identity';
import type { SayRebase } from './LandingSync';
import { Discards, type HeadOpinion, type HeadReview, type LandingPlan, Lands, type WorkAsk } from './Lands';
import { Note } from './Note';
import { HelperAsks, type HelperWaits, openAsks, waitsOnWord } from './HelperAsks';
import { hasNote, noteBlocks, noteLines } from './noteLines';
import { processCommitsSays, processLandingSays, type SessionProcess } from './process';
import { ProposedRules, type RuleAsk, type RuleWaits } from './ProposedRules';
import { movedAt } from './rail';
import { RunningIntake } from './RunningIntake';
import { leftFor, type Seek, SOUGHT, useSought } from './WaitAnchor';
import { rulesAsked } from './waits';

// The work's line is its own molecule, which the foot's strip draws too (WAITFOOT1b, D168); its shapes are read from here too.
export type { HeadOpinion, HeadReview, LandingPlan, WorkAsk } from './Lands';

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
 * **The rules it proposed stand beneath them, whatever its state** (PROPOSEHERE1b, D74): a session refused something files a
 * proposal and goes on or ends its turn, so its own open proposals are answered here, *Allow…* asking once, as at the foot.
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
  onAnswerSession, branch, ownBranch, onReview, onDiscardBranch, discardingBranch = false, headed = false, goAheads = [], onGoAhead,
  ownSignIn = false, nameOf, lands, landing, planning, onLand, onSayRebase, onName, discards, onDiscardTree, review, behind, opinion, onOpenQuest,
  continues, helps, onOpenSession, process, workAsk, workBusy = false, toEvent, rules, ruleAsk, seek, helpers,
}: {
  /**
   * The helpers it asked for (MIXAGENT2b, D163), the frame's: its asks as the driver's look left them, the agents' names, the
   * person's yes and a door to each helper. Absent, a browser or a teammate's record, none is drawn.
   */
  helpers?: HelperWaits | null;
  /**
   * The wait a raising block's *To the wait* leads to while the head's waits are on screen (WAITFOOT2b, D168's WAITFOOT2 note),
   * counted so the same one pressed again is shown again: its line takes the focus, outlined, as the strip's does.
   */
  seek?: Seek | null;
  /**
   * The rules it proposed that the person still answers (PROPOSEHERE1b, D74), the frame's, handed to the foot's strip alike:
   * the machine's proposals as the shell read them, the answer's door, its pending and whether the session takes words now.
   */
  rules?: RuleWaits | null;
  /** Where a rule's *Allow…* is open, the frame's, so it is open in the head or at the foot, never both (D168 point 3). */
  ruleAsk?: RuleAsk;
  /**
   * The way from each wait to the event in the conversation that raised it, where the conversation found one (WAITFOOT1c, D168
   * point 4): *In the conversation* on the park's card, each go-ahead and the work's line. The frame's, handed to the foot's
   * strip alike.
   */
  toEvent?: WaitEvents;
  /**
   * Where the ask of its work's line (*Accept…*, *Discard branch…*) is open, the frame's, so it is open in the head or in the
   * foot's strip, never both (WAITFOOT1b, D168 point 3). Absent, the line holds its own.
   */
  workAsk?: WorkAsk;
  /** A landing or a discard of its work is on its way, pressed here or at the foot: the line's press waits for it (D168). */
  workBusy?: boolean;
  /**
   * Open the quest it serves (HEAD2c): the door beside the request as written in *Details*. Absent where nothing can open
   * it, and then the request is shown with no door.
   */
  onOpenQuest?: (quest: Quest) => void;
  /**
   * The session its record says it continues (`from`, MIXAGENT3a-i), where the frame holds it, with its title. Absent, *Details*
   * says the record's id, with no door.
   */
  continues?: HeadLink | null;
  /** The session a helper helps (`serves`, MIXAGENT3a-i), where the frame holds it, with its title; absent, its id. */
  helps?: HeadLink | null;
  /** Attend one of those sessions (MIXAGENT3a-i): the door beside its title in *Details*. Absent, the title has no door. */
  onOpenSession?: (session: Session) => void;
  /**
   * The process its own tree runs under (PROCESS1e, D164's amendment point 3): how its work is committed and how it lands, each
   * with where it was set, *Details*' rows. Absent or null where it was told none (no tree of its own here) or nothing answered.
   */
  process?: SessionProcess | null;
  /** The landing plan shown is the previous one while the entered name's is read (LANDNAME1b); the press waits. */
  planning?: boolean;
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
   * How the tree stands against its line, from the review's answer (REVIEWBASE5, D164's note): where `ahead` is 0 and nothing is
   * uncommitted, there is nothing to land and no *Accept…* is offered, as in the review. Absent before it answers or where not read.
   */
  behind?: LineBehind | null;
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
  onLand?: (answered: Answered, answers?: string | null, branch?: string | null) => void;
  /** The press that says the rebase words where replaying conflicts (LANDSYNC1w); the frame holds the say door. */
  onSayRebase?: SayRebase;
  /** The branch name typed in the land ask (LANDNAME1b), so the frame asks the plan again with it; `''` when the ask is put down. */
  onName?: (name: string) => void;
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
  onGoAhead?: GoAheadAnswer;
  /**
   * A page header above carries its state, its title and its facts (SESSUX1d, D126 §3.2; UX7c, D152 §7): the head says
   * none again, the quest's whole title included: that is a row of *Details* (HEAD2c). Absent, a window with no header
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
   * The branch its tree is on where the agent moved it onto one of its own (TREEFOLLOW1), as the driver's `work` says it
   * (TREEFOLLOWWEB2): named when the clean-up lists no row, since it lists only `daoris/` branches and the tidy removed that one.
   */
  ownBranch?: string | null;
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
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
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
  const proposed = rulesAsked(rules?.proposals, session.id);
  const own = useRef<HTMLElement>(null);
  const { sought, letGo } = useSought(own, seek);

  return (
    // Where the foot's *To the top* puts the focus (WAITFOOT1b, D168 point 1): every wait stands here with its reading. The
    // outline a *To the wait* left goes once the person moves on from the head (WAITFOOT2b).
    <header ref={own} data-record-head tabIndex={-1} className="grid gap-2.5 outline-none" onBlur={(event) => { if (leftFor(event)) letGo(); }}>
      {/* `running` counts a park as alive; a parked intake is AwaitingIntake's, below. */}
      {running && !parked && intake && <RunningIntake ask={session.ask!} onOpen={onAnswerAsk} />}
      {answered && <AnsweredPark answer={session.answer!} />}
      {/* Each section a wait stands in is marked (WAITFOOT2a): the frame watches them, and the strip at the foot stands aside
          while they are on screen. */}
      {waiting && intake && (onResolve || onAnswerAsk) && (
        <div data-wait-section data-wait="question" tabIndex={-1} className={sought === 'question' ? SOUGHT : 'outline-none'}>
          <AwaitingIntake ask={session.ask!} note={session.note} parts={session.noteParts} onAnswer={onAnswerAsk} onConversation={toEvent?.question} />
        </div>
      )}
      {waiting && !intake && (onResolve || onAnswerSession) && (
        <div data-wait-section data-wait="question" tabIndex={-1} className={sought === 'question' ? SOUGHT : 'outline-none'}>
          {/* Keyed: a half-written decline reason belongs to the session it was written for, and it
              carried into the next parked one, ready to decline it with somebody else's reason (REV3). */}
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
            onConversation={toEvent?.question}
          />
        </div>
      )}
      {/* Nothing here can act — a browser, or a mirrored record from another machine — so the
          analysis is shown and the moves are not. Half a control is worse than none. */}
      {waiting && !onResolve && !onAnswerSession && !(intake && onAnswerAsk) && noted && (
        <WaitingCard title={t('work.head.waiting')}>
          <Note note={session.note} parts={session.noteParts} className="mt-1.5 text-body leading-relaxed" />
        </WaitingCard>
      )}
      {parked && !intake && goAheads.length > 0 && (
        <section data-wait-section aria-labelledby={`go-aheads-${session.id}`} className="grid gap-1">
          <h3 id={`go-aheads-${session.id}`} className="m-0 text-small font-semibold">{t('work.awaiting.goAheads')}</h3>
          <GoAheadList
            goAheads={goAheads}
            busy={resolving}
            onAnswer={onGoAhead}
            lead={t('work.awaiting.goAheadsLead')}
            inConversation={toEvent?.goAheads}
            marked={sought?.startsWith('goAhead:') ? Number(sought.slice('goAhead:'.length)) : undefined}
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

      {/* The helpers it asked for that are still on their way (MIXAGENT2b, D163): an agent's ask that waits on the person's yes
          says so here, with the yes and the no. A settled one is said where it settled, in the conversation and the timeline.
          Marked a wait's section while one waits on the person's word (MIXAGENT2c), so the strip stands aside for it too. */}
      {helpers && openAsks(helpers.asks).length > 0 && (
        <section
          aria-labelledby={`helpers-${session.id}`}
          data-wait-section={helpers.onApprove && helpers.asks.some(waitsOnWord) ? '' : undefined}
          className="grid gap-1"
        >
          <h3 id={`helpers-${session.id}`} className="m-0 text-small font-semibold">{t('work.helper.heading')}</h3>
          <HelperAsks {...helpers} asks={openAsks(helpers.asks)} />
        </section>
      )}

      {!headed && (
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
        // Spaced as the head spaces its own, since the line's parts were the head's before they were the section's.
        <div data-wait-section data-wait="work" tabIndex={-1} className={cn('grid gap-2.5', sought === 'work' || sought === 'opinion' ? SOUGHT : 'outline-none')}>
          <Lands
            key={session.id} session={session} lands={lands} landing={landing} planning={planning} onReview={onReview} onLand={onLand} onSayRebase={onSayRebase} onName={onName} review={review} behind={behind}
            opinion={opinion} ask={workAsk} busy={workBusy} toEvent={toEvent}
          />
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
          requestRow(t, quest, session.kind === 'chat' ? opening : null, session.quest ?? session.ask, onOpenQuest),
          // Whom it continues and whom it helps (MIXAGENT3a-i), the record's own links, each a door where the frame holds it.
          linkRow(t, t('work.head.continues'), session.from, continues, onOpenSession),
          linkRow(t, t('work.head.helping'), session.serves, helps, onOpenSession),
          // The process its own tree runs under and where each rule was set (PROCESS1e, D164's amendment point 3), in Setup's words.
          { label: t('work.head.commits'), value: process ? <Inline text={processCommitsSays(t, process)} /> : null },
          { label: t('work.head.landing'), value: process ? <Inline text={processLandingSays(t, process)} /> : null },
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
 * A conversation's opening is the header's title (`sessionTitle`), which holds one line: past this many characters the
 * header may have cut it, and *Details* holds it whole (HEAD2c). A long line is never wrongly said twice; it is said in the
 * fold, which is closed.
 */
const ONE_LINE = 60;

/**
 * The request as written, a row of *Details* (HEAD2c, D162 point 3): the quest's whole title with the quest's door, or, for
 * a conversation with no quest, its opening where the header's one line may have cut it. Content, shown as it is.
 */
function requestRow(
  t: TFunction, quest: Quest | null | undefined, opening: string | null | undefined, id: string | null | undefined,
  onOpenQuest: ((quest: Quest) => void) | undefined,
): DetailRow {
  const label = t('work.head.request');
  if (quest?.title) {
    return {
      label,
      value: (
        <>
          <span>{quest.title}</span>
          {onOpenQuest && (
            <button
              type="button"
              onClick={() => onOpenQuest(quest)}
              className="ml-2 cursor-pointer border-0 bg-transparent p-0 font-sans text-small text-accent hover:underline"
            >
              {t('work.head.openQuest', { id: quest.id })}
            </button>
          )}
        </>
      ),
    };
  }
  return { label, value: !id && opening && opening.length > ONE_LINE ? opening : null };
}

/** A session another one's record names (MIXAGENT3a-i), as the frame holds it: its record, and its title (`sessionTitle`). */
export type HeadLink = { session: Session; title: string };

/**
 * A link the record holds, a row of *Details* (MIXAGENT3a-i, the mixed-agents design §4): the other session's title with a door
 * that attends it, where the frame holds that session; its id in mono with no door where it does not (deleted, or not listed).
 * No row where the record names none.
 */
function linkRow(
  t: TFunction, label: string, id: string | null | undefined, link: HeadLink | null | undefined,
  onOpenSession: ((session: Session) => void) | undefined,
): DetailRow {
  if (!id) return { label, value: null };
  if (!link || link.session.id !== id) return { label, value: id, mono: true };
  return {
    label,
    value: (
      <>
        <span>{link.title}</span>
        {onOpenSession && (
          <button
            type="button"
            onClick={() => onOpenSession(link.session)}
            className="ml-2 cursor-pointer border-0 bg-transparent p-0 font-sans text-small text-accent hover:underline"
          >
            {t('work.head.openSession')}
          </button>
        )}
      </>
    ),
  };
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

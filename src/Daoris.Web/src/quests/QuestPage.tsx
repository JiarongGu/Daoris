import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Quest, type Session, type SubmissionRef } from '../data/api';
import { isImage } from '../compose/attachments';
import { ago, sessionTool, size, stamp } from '../locales/format';
import { ExternalLink } from '../design/links';
import type { ChainStep } from '../map/chain';
import { ChainStrip } from '../map/ChainStrip';
import type { AccountNamer } from '../agents/tools';
import { type Consideration, sittingSentence, type TrustHold, waitsForAccount } from '../data/signals';
import { Button, Icon, type IconName, Inline, Menu, Pill, Prose, QUEST_TONE, SectionTitle, SESSION_TONE, shownKey } from '../design/ui';
import { type SessionGrouping, shownOf } from '../work/groups';
import { liveParts } from '../work/noteLines';
import { placedFact } from '../work/rail/SessionRow';
import { lastAbandon, pauseAsk, type WorkDoor, workOffers, type WorkPlan, type WorkTarget } from '../work/pausing/pausing';
import { ClearAsk } from '../work/history/ClearAsk';
import { DetailsFold } from '../work/head/DetailsFold';
import { clearOffered, type HistoryDoor, type HistoryTarget } from '../work/history/history';
import { questFacts } from '../work/head/headFacts';
import { HowItCameToBe, type TraceDoor } from '../work/head/HowItCameToBe';
import { questName, sessionOrigin } from '../work/identity';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import { Note } from '../work/Note';
import { stepState } from '../work/gates/review';
import { ReviewGate, type ReviewGateActs } from '../work/gates/ReviewGate';
import { QuestConflicts, readConflicts, SettledConflicts } from './Conflicts';
import { QuestRequirements } from './Requirements';
import { shownStatus } from './records';
import { TrustAsk } from '../work/waits/TrustAsk';
import { type MainNotice, PageHead, PageSection, ViewMain } from '../work/frame/ViewMain';
import { AbandonAsk, AbandonedWork, PauseAsk } from '../work/pausing/WorkAsks';
import type { RunLineDoor } from '../workflow/run';
import { WorkflowLine } from '../workflow/WorkflowLine';

/** The acts a quest's page offers: its header's, then its body's (CTX1, D138 §4). */
type QuestAct =
  | 'take' | 'done' | 'pause' | 'decline' | 'abandon' | 'delete' | 'clear' | 'clearFailed' | 'accept' | 'resume' | 'retry'
  | 'trust' | 'session' | 'reopen' | 'goOn';

/** Each act's name, the one its button says, and its glyph in a menu. */
const QUEST_ACT: Record<QuestAct, { label: string; icon?: IconName }> = {
  take: { label: 'quests.detail.take' },
  done: { label: 'quests.detail.done', icon: 'check' },
  pause: { label: 'quests.detail.pause', icon: 'pause' },
  decline: { label: 'quests.detail.decline' },
  abandon: { label: 'quests.detail.abandon' },
  delete: { label: 'quests.detail.delete', icon: 'remove' },
  clear: { label: 'quests.detail.clear' },
  clearFailed: { label: 'quests.detail.clearFailed' },
  accept: { label: 'quests.requirements.accept', icon: 'check' },
  resume: { label: 'quests.detail.resume', icon: 'resume' },
  retry: { label: 'quests.detail.retry', icon: 'refresh' },
  trust: { label: 'trust.open', icon: 'shield' },
  session: { label: 'work.open', icon: 'frameWork' },
  reopen: { label: 'quests.detail.reopen' },
  goOn: { label: 'quests.reopened.goOn', icon: 'resume' },
};

/**
 * A header act's button: the one loud control is the quest's next step (UX5 U31), taking it while it is open and closing
 * it once it is taken; Pause… plain; the rest quiet.
 */
function buttonOf(act: QuestAct, status: Quest['status']): 'primary' | 'default' | 'ghost' {
  if (act === 'take') return 'primary';
  if (act === 'done') return status === 'Taken' ? 'primary' : 'default';
  if (act === 'pause') return 'default';
  return 'ghost';
}

/**
 * A quest's body as its page shows it under the head (UX7c, D152 §7): its first line left out where it is the title the
 * head said, as the ask page leaves its first line out (POLISH4); where the head said a short title instead, the body as
 * written, opening on the whole title, which is prepended where the body does not already open on it. Content is never
 * rewritten otherwise. Empty where nothing is left to say.
 */
export function bodyUnderHead(title: string, body: string, shortTitled: boolean): string {
  const [first = '', ...rest] = body.split('\n');
  const opensOnTitle = first.trim() === title.trim();
  if (shortTitled) return opensOnTitle ? body : `${title}\n\n${body}`;
  return opensOnTitle ? rest.join('\n').replace(/^\s*\n/, '') : body;
}

/** A title longer than this (two lines of the head, about) is read whole in the request below. */
const LONG_TITLE = 80;

/** A record's row of facts: its name, and what it holds. */
function Fact({ name, children }: { name: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-ink-faint">{name}</dt>
      <dd className="m-0">{children}</dd>
    </>
  );
}

/**
 * **A quest's page** (FRAME1d, D118 §3d): its record in Quests' main area, where it was a drawer that lay
 * over the side bar and the panel. Its acts are in its header and the loud one is its next step (UX5 U31):
 * *Take* while it is open, *Mark done* once it is taken. Then what it is waiting on, the holds only the person lifts
 * (a folder to trust, D73; a quest parked by its strikes, RETRY1; one their stop holds, D126 §3.4), a conflict a person
 * must see (D68 §5), the ask itself, what it carries (D65 §2), its chain (MAP1) and its driven session's record (D46 §4).
 *
 * @remarks
 * **A molecule**: every state is reached by its props, and every press goes out. What asks under the header —
 * *Decline…*'s reason, *Delete…*'s sentence — is its own, so it resets with the quest it was asked of.
 *
 * - **A delete asks once** (D95): its first press opens a sentence saying the second removes the record, which
 *   nothing gives back; offered only where the service says it may go (`deletable`). It is the one inline confirmation
 *   (UXFIX2): open until the service answers, a refusal said inside it.
 * - **A decline needs its reason**, which the service refuses without; the form does not offer the mistake.
 * - **What the service and the driver say is said verbatim**: the session's evidence, a conflict's note, the driver's
 *   sentence about why it sits (translated by its verdict, never by its words, U27). The session's note is `Note`'s
 *   (LANG1b): Daoris's lines by their codes in the reader's language, someone's words as written.
 * - **A page never prints a machine path it was answered** (D47 §4): a kept file opens through the host's own
 *   route, and one kept elsewhere says so.
 * - **Its work is paused and abandoned here** (PAUSE1e, D132 §7.1), from this machine's driver's plan: *Pause…* while open
 *   or taken and not paused on its own, asking once where it ends work in flight (§2.6); *Abandon…* beside *Decline…*,
 *   listing first and abandoning on its second press with the person's reason (§3.1). *Resume* stands under *Sitting* for
 *   its own pause, where *Try again* stands for a stop; for another's pause the sentence names whose, and a door opens it.
 *   After an abandon it says when, *What went* and *What stayed* (§4.2). A browser names the terminal's commands instead.
 * - **What the person required, and how its done answered** (DRIFT1d2, D133 §3–§4): each requirement in their words with
 *   its check, met or departed with the reason and the words it relied on, after the body. A departure that holds the
 *   quest for their yes is what it waits on, so it moves above the body, as a conflict sits, with *Accept the departure*:
 *   one press, the service's accept door, and its header says *awaits your yes*.
 * - **A closed quest's work is cleared from this machine here** (HIST1e, D153; the history-clearing design §6.1): *Clear
 *   from this machine…* and *Clear failed sessions…* in its ⋯, each where its plan says something may go and absent where
 *   it does not (a quest an ask asked is cleared on the ask's page), listing under the header first and clearing on the
 *   second press exactly what it listed (§5). A browser has no driver, and offers neither.
 * - **The person's done is theirs** (QUESTCLOSE1, D126's note): *Mark done…* asks once under the header, with their note,
 *   and sends their done, which the service writes as theirs and which answers none of their requirements one by one; the
 *   ask says so where it carries some. A taken quest whose last session here ended in a way nothing carries on (finished or
 *   declined at a checkpoint) says so under *Sitting*, with *Mark done…* there: on the install such a quest sat taken with
 *   nothing on its page that said why or closed it.
 * - **A set-up step's page is where its review is answered** (REVIEWENV1g, D154 point 8; the review environment design §3.3):
 *   *Review in `<environment>`* above the body, every set-up it said newest first, and under the newest *Reviewed*, *Not
 *   yet…*, *Show it again* and *Skip…*. A done its review holds waits for that, not for a yes: its header says *awaits
 *   review* and no *Accept the departure* is offered for it, which the accept door would refuse.
 * - **A closure graph's quest reads *Submitted*** (FABRIC-CLOSE1d, the fabric events design §E5): taken at its agent's `ready`, then
 *   the hold it waits on, as a done's, then done at the graph's close. Its sealed submission is reopened here (FABRIC-CLOSE1c3,
 *   D156): *Reopen…* asks once with the step it publishes named, and sends the submission it showed. A step its source's reopen
 *   holds says so in its own words, offering *Go on with it* once the source is ready again and *Abandon…*, never the yes. A quest
 *   no closure graph holds, which is every quest on an install that binds none, reads as before.
 */
export function QuestPage({
  quest, lanes, question, sitting, hold, chain = [], session,
  busy = false, retrying = false, trusting = false, granting = false, dismissing = false, forwarding = false, accepting = false,
  onRespond, onDelete, onDismiss, onForward, onRetry, onTrusting, onGrant, onOpenQuest, onAttend, onOpenAsk, onAccept, work, history, trace,
  nameOf, review, workflow, grouping, reopen, goOn,
}: {
  /**
   * The person's reopen of its sealed submission (FABRIC-CLOSE1c3, D156): the step of its chain that stands on it, and the press,
   * told back to its ask so a refusal is said inside it. Absent where no step stands on its submission; offered only on a quest
   * at an unheld submission.
   */
  reopen?: { step: string; onReopen: (note: string | null, answered: Answered) => void };
  /**
   * A step its source's reopen holds (FABRIC-CLOSE1c3): whether the source is ready again, so the person's go-on can take, and the
   * press. Absent where there is no door to say it through.
   */
  goOn?: { ready: boolean; going?: boolean; onGoOn: () => void };
  /**
   * Where its chain's work here stands in its workflow (WORKFLOW1c, the workflow design §7), said in one line among the head's
   * facts whose door opens the run. Absent in a browser, which has no driver to read one.
   */
  workflow?: RunLineDoor;
  /**
   * A set-up step's review (REVIEWENV1g): its presses, whether one is on its way, and whether Daoris still serves its newest
   * set-up's tab here (null where no shell says). Absent, its review is shown with no press.
   */
  review?: { acts: ReviewGateActs; busy?: boolean; served?: boolean | null };
  /**
   * What a person calls an account (ACCTNAME1, D152 §4.2), from the roster its organism holds: its session line, its chain
   * and its trace say each account by it. Absent, as in a browser, each record's id is said.
   */
  nameOf?: AccountNamer;
  /**
   * How it came to be (TRACE1b, D143, D50), folded at the foot of its page: the page's organism holds the fold and the read.
   * Absent in a browser, which has no driver to read this machine's records (D47 §4).
   */
  trace?: TraceDoor;
  quest: Quest;
  /** Its lanes as its repository declares them (D115 §2.2), each named where its registration says. */
  lanes?: string;
  /** What its taker asked another repository and waits on (D79), with the quest where the page holds it. */
  question?: { id: string; quest?: Quest } | null;
  /** Why this machine's driver leaves it waiting, in the driver's words (D46 §3). */
  sitting?: Consideration | null;
  /** A start the driver holds for the agent's trust where it would run (D73). */
  hold?: TrustHold | null;
  /** The chain it belongs to (MAP1); drawn only where there is more than this quest. */
  chain?: ChainStep[];
  /**
   * Its driven session's freshest record: a retry is its own record, and the page says where things stand now. Read
   * here and managed in Sessions (D126 §3.6): its stop is its page header's there, behind the door below.
   */
  session?: Session | null;
  /**
   * Where the session list's reader placed that session (D126 §2.4), which says the words the record cannot: *awaiting reply*
   * for the last session of a quest that waits on another repository's answer (WAITSHOWN1). Absent where no driver answers,
   * and the record's own state shows.
   */
  grouping?: SessionGrouping | null;
  busy?: boolean;
  retrying?: boolean;
  /** The agent's trust question is open for the hold (D73), asked only on the press. */
  trusting?: boolean;
  granting?: boolean;
  dismissing?: boolean;
  /** A bring-forward of this machine's lost take is on its way (SYNCMERGE1c2). */
  forwarding?: boolean;
  /** The person's yes to a departure is on its way (DRIFT1d2). */
  accepting?: boolean;
  /**
   * Take, decline with its reason, or the person's done (QUESTCLOSE1) with their note, or null for none, told back to its
   * ask (UXFIX2) so a refusal is said inside it.
   */
  onRespond: (action: 'take' | 'done' | 'decline', reason?: string | null, answered?: Answered) => void;
  /** Delete the quest (D95), told back to its ask (UXFIX2) — absent where there is no door to do it. */
  onDelete?: (answered: Answered) => void;
  /**
   * Dismiss a conflict by its name (SYNC6c): this machine's lost take's *Drop mine*, then the fresh takes' conflicts its lost
   * bring-forwards left, which its row reads as its own (SYNCMERGE1c2).
   */
  onDismiss: (machine: string, sequence: number, also?: SubmissionRef[]) => void;
  /**
   * Bring this machine's lost take forward by the conflict's name (SYNCMERGE1c2, D178 point 3). Absent where there is no door to say
   * it through, and then never offered.
   */
  onForward?: (machine: string, sequence: number) => void;
  /**
   * *Try again* (RETRY1; D126 §3.4): start a quest parked by its strikes again, or release one the person's stop holds
   * — the shell's, which the driver judges by its verdict.
   */
  onRetry?: () => void;
  /** Open or put down the trust question (D73) — the shell's, which closes it once the grant is written. */
  onTrusting?: (open: boolean) => void;
  /** Grant the agent's trust for the folder held (D73) — the shell's; a refusal is said in the question (UXFIX2b3b). */
  onGrant?: (hold: TrustHold, answered: Answered) => void;
  onOpenQuest: (id: string) => void;
  /** The door into Sessions: absent where there are none, a browser, or no driver is attached. */
  onAttend?: (session: string) => void;
  /** Open an ask's page: where the pause that holds this quest is resumed (PAUSE1e). */
  onOpenAsk?: (id: string) => void;
  /**
   * The person's yes to a done's departure from what they required (DRIFT1d2, D133 §4): the service's accept door, which
   * lets what the departure held go on. Absent where there is no door to say it through.
   */
  onAccept?: () => void;
  /** This machine's driver's half (PAUSE1e): the plan and the three presses. Absent in a browser, which has no driver. */
  work?: WorkDoor;
  /**
   * Clearing its finished work from this machine (HIST1e, D153 §6.1): the plans of its work and of its failed sessions, and
   * the second press. Absent in a browser, which has no driver and no home.
   */
  history?: HistoryDoor;
}) {
  const { t } = useTranslation();
  // One question asks under the header at a time: a decline's reason, a delete's sentence, a pause's, the abandon's list, a
  // clear's list.
  const [asking, setAsking] = useState<'done' | 'decline' | 'delete' | 'pause' | 'abandon' | 'clear' | 'clearFailed' | 'reopen' | null>(null);
  // The person's words on a reopen (FABRIC-CLOSE1c3), kept while its ask is open.
  const [reopenNote, setReopenNote] = useState('');
  // The plan the abandon's list showed, held from when it opened: the second press sends exactly what it listed (§3.1).
  const [listed, setListed] = useState<WorkPlan | null>(null);
  const [reason, setReason] = useState('');
  // The person's words on their done (QUESTCLOSE1), kept while its ask is open.
  const [doneNote, setDoneNote] = useState('');
  const declining = asking === 'decline';
  // A delete asks once (D95): the first press arms it, and only the second removes the record.
  const deleting = asking === 'delete';
  const moving = quest.status === 'Open' || quest.status === 'Taken';
  const deletable = quest.deletable === true && onDelete !== undefined;
  const answered = question?.quest?.status === 'Done' || question?.quest?.status === 'Declined';
  // A wait (D79) is said by its own row, with the question; the driver's sentence under it would say it again.
  const because = sitting?.verdict === 'Waiting' ? null : sitting ?? null;
  const target: WorkTarget = { scope: 'quest', id: quest.id };
  const offers = workOffers(work?.plan);
  const waiting = busy || work?.busy === true || history?.busy === true;
  // Its clears, where it is closed and its plans say something may go (§6.1): absent, never disabled, anywhere else.
  const closed = quest.status === 'Done' || quest.status === 'Declined';
  const clearable = closed && clearOffered(history?.plan);
  const failedClearable = closed && clearOffered(history?.failed);
  const clearTarget = (scope: 'clear' | 'clearFailed'): HistoryTarget => ({ scope: scope === 'clear' ? 'quest' : 'failed', id: quest.id });
  const pauseLines = work?.plan ? pauseAsk(work.plan, { wired: work.wired }) : null;
  const abandoned = lastAbandon(work);
  // Whose pause holds it, from the tick's verdict: its own is resumed here, an ask's or another quest's on that page.
  const pausedBy = because?.verdict === 'Paused' ? because.pausedBy ?? null : null;
  const ownPause = pausedBy?.scope === 'quest' && pausedBy.id === quest.id;
  // Its own pause where the tick has no verdict for it (its session waits on you, D132 §2.1): said from the plan, with Resume.
  const pausedUnseen = offers.paused && !pausedBy;
  // The session as the list's reader reads it (D126 §2.4, WAITSHOWN1): its derived word where it said one, so the last session
  // of a quest that awaits another repository's answer reads as awaiting, not as completed; the record's own otherwise.
  const shown = session ? shownOf(session, grouping, undefined) : null;
  const waitsOn = shown === 'awaiting-reply' ? placedFact(grouping, shown) : null;
  // Taken, and its last session here ended in a way nothing here carries on (QUESTCLOSE1): finished or declined at a
  // checkpoint, no answer waiting. The planner says nothing of it, so the page does, with the person's done beside it. A
  // failure, a stop and an answered park are the driver's to say (a carry-on, a park, *Try again*), and a teammate's record
  // is not this machine's take.
  const endedHere = quest.status === 'Taken' && !because && !hold && !pausedUnseen && session != null
    && sessionOrigin(session) === null
    && ((session.state === 'completed' && !session.answer) || session.state === 'declined');

  // A pause that stops nothing applies at once, with its notice, since nothing is lost (§2.6).
  const onPauseFirst = () => {
    if (pauseLines === null) work?.onPause();
    else setAsking('pause');
  };

  // The header's acts, in its order (D118 §3b), each where it applies. While one asks under the header, its first press
  // is not offered twice. One list for its buttons and the page's right-click (CTX1), so the two never disagree.
  // A closure graph's quest (FABRIC-CLOSE1d, CLOSE1c3): what its pill says, whether its sealed submission may be reopened here, and
  // whether its source's reopen holds it, which no take, yes or done of the agent's moves.
  const reads = shownStatus(quest);
  const reopenHeld = quest.held === true && quest.hold === 'source-reopened';
  const reopenable = reopen !== undefined && reads === 'Submitted' && quest.held !== true;
  const goesOn = reopenHeld && quest.status === 'Taken' && goOn?.ready === true;
  // SYNCMERGE1c2 (D178 point 2): a held step this machine's take holds offers its go-on and abandon as a lost take's two choices, by
  // their names; held by another machine, or where no host says which, each keeps its own.
  const holdsHere = quest.here != null && quest.holder === quest.here;
  const heldMine = reopenHeld && holdsHere;
  const labelOf = (act: QuestAct) => (act === 'goOn' && heldMine ? 'quests.conflict.forward' : QUEST_ACT[act].label);
  // This machine's take lost and the step is not its own (D178 point 7): the person's done here is refused as its session's would
  // be, so the done's ask names the choices that stand for it.
  const lostHere = !holdsHere && readConflicts(quest).rows.some((row) => row.mine);
  const headActs: QuestAct[] = [
    ...(!(moving || offers.abandon) ? [] : [
      ...(quest.status === 'Open' && !reopenHeld ? ['take' as const] : []),
      ...(moving && asking !== 'done' ? ['done' as const] : []),
      ...(offers.pause && moving && asking !== 'pause' ? ['pause' as const] : []),
      ...(moving && !declining ? ['decline' as const] : []),
      ...(offers.abandon && asking !== 'abandon' ? ['abandon' as const] : []),
      ...(deletable && !deleting ? ['delete' as const] : []),
    ]),
    // A closed quest's two clears (HIST1e), in its ⋯ (§6.1).
    ...(clearable && asking !== 'clear' ? ['clear' as const] : []),
    ...(failedClearable && asking !== 'clearFailed' ? ['clearFailed' as const] : []),
  ];
  // What its body offers (the sitting line's Resume, Try again and trust, its session's door), which the right-click
  // offers after the header's.
  const ownResume = (ownPause || pausedUnseen) && work !== undefined;
  const retryable = (because?.verdict === 'Exhausted' || because?.verdict === 'Stopped') && onRetry !== undefined;
  const trustable = Boolean(hold && onGrant && onTrusting && !trusting);
  // A departure that holds it for the person's yes (DRIFT1d2): what it waits on, so the yes leads its body's acts. A set-up
  // step its review holds waits for the review's verdict instead (REVIEWENV1g), which no yes lifts (D154 point 9).
  // A step its source's reopen holds waits on the person's go-on or abandon (FABRIC-CLOSE1c3), which the accept door would refuse.
  const unreviewed = quest.held === true && quest.hold === 'unreviewed';
  const held = quest.held === true && !unreviewed && !reopenHeld;
  const requirements = quest.requirements ?? [];
  const acceptable = held && onAccept !== undefined;
  const bodyActs: QuestAct[] = [
    ...(acceptable ? ['accept' as const] : []),
    ...(goesOn ? ['goOn' as const] : []),
    ...(reopenable && asking !== 'reopen' ? ['reopen' as const] : []),
    ...(ownResume ? ['resume' as const] : []),
    ...(retryable ? ['retry' as const] : []),
    ...(trustable ? ['trust' as const] : []),
    ...(session && onAttend ? ['session' as const] : []),
  ];

  const press = (act: QuestAct) => {
    switch (act) {
      case 'take': onRespond('take'); return;
      case 'done': setAsking('done'); return;
      case 'pause': onPauseFirst(); return;
      case 'decline': setAsking('decline'); return;
      case 'abandon': setListed(work!.plan); setAsking('abandon'); return;
      case 'delete': setAsking('delete'); return;
      case 'clear': setAsking('clear'); return;
      case 'clearFailed': setAsking('clearFailed'); return;
      case 'accept': onAccept?.(); return;
      case 'resume': work?.onResume(); return;
      case 'retry': onRetry?.(); return;
      case 'trust': onTrusting?.(true); return;
      case 'session': if (session) onAttend?.(session.id); return;
      case 'reopen': setAsking('reopen'); return;
      case 'goOn': goOn?.onGoOn(); return;
    }
  };

  // The head's acts as the UX7 design §5.3 draws them (UX7c, D152 §7): its next step loud (*Take* while open, *Mark done*
  // once taken) and *Decline…* beside it; the rest of the same list, and its id, in its ⋯. One list still, so the buttons,
  // the ⋯ and the right-click never disagree (CTX1).
  const lead: QuestAct | null = headActs.includes('take') ? 'take' : quest.status === 'Taken' && headActs.includes('done') ? 'done' : null;
  // HEAD2b (D162 §3): the primary holds still in `acts`; *Decline…* is the fold's, shown above 640 px of the main area, and said
  // again in the ⋯ only below it. The ⋯'s menu is a portal outside the area's container query, so the width is read when it
  // opens (`narrow`), and at that width the ⋯ lists what the fold hides.
  const primary = headActs.filter((act) => act === lead);
  const declinable = headActs.includes('decline');
  const [narrow, setNarrow] = useState(true);
  const moreRef = useRef<HTMLSpanElement>(null);
  const folded = headActs.filter((act) => act !== lead && (act !== 'decline' || narrow));
  const copyId = { id: 'copy', label: t('contextMenu.act.copyQuest'), icon: 'copy' as const, copy: quest.id };
  const acts = (
    <>
      {primary.map((act) => (
        <Button key={act} variant={buttonOf(act, quest.status)} disabled={waiting} onClick={() => press(act)}>
          {t(QUEST_ACT[act].label)}
        </Button>
      ))}
    </>
  );
  const fold = declinable ? (
    <Button variant={buttonOf('decline', quest.status)} disabled={waiting} onClick={() => press('decline')}>
      {t(QUEST_ACT.decline.label)}
    </Button>
  ) : undefined;
  const more = (
    <span ref={moreRef} className="contents">
    <Menu.Root
      onOpenChange={(open) => {
        const area = moreRef.current?.closest('[class*="@container/main"]');
        if (open) setNarrow(!area || area.clientWidth <= 640);
      }}
    >
      <Menu.Trigger asChild>
        <Button variant="ghost" aria-label={t('work.head.more')} className="h-[1.9rem] w-[1.9rem] justify-center px-0">
          <Icon name="more" size={15} />
        </Button>
      </Menu.Trigger>
      <Menu.Content side="bottom" align="end" className="min-w-48">
        <Menu.Acts
          acts={[
            ...folded.map((act) => ({
              id: act, label: t(QUEST_ACT[act].label), icon: QUEST_ACT[act].icon, disabled: waiting, onSelect: () => press(act),
            })),
            copyId,
          ]}
          onCopy={(text) => { void navigator.clipboard?.writeText(text).catch(() => {}); }}
        />
      </Menu.Content>
    </Menu.Root>
    </span>
  );

  const menu = {
    label: questName(quest),
    acts: [
      ...[...headActs, ...bodyActs].map((act) => ({
        id: act, label: t(labelOf(act)), icon: QUEST_ACT[act].icon,
        // What waits on a press waits in the menu too: the header's on the quest's own, Try again on its own.
        disabled: act === 'retry' ? retrying : act === 'accept' ? accepting : act === 'goOn' ? goOn?.going === true
          : act === 'session' || act === 'trust' ? false : waiting,
        onSelect: () => press(act),
      })),
      copyId,
    ],
  };

  // Titles first (UX7c, D152 §7; the UX7 design §5.3): its state leads, its name follows on two lines at most, and one line
  // of facts says whom it asks, who asked, when, and the one live fact. The state line, the id and the fact rows went: the
  // state's sentence is its pill's tip, and the id, the full times and the lanes are its *Details*.
  const named = questName(quest);
  // The body, its first line dropped where it is the title the head already said (POLISH4's rule, as the ask page drops
  // it); where the head said a short title, the body opens on the whole title, once.
  const body = bodyUnderHead(quest.title, quest.body, named !== quest.title);
  const request = useRef<HTMLParagraphElement>(null);
  const head = (
    <PageHead
      title={named}
      lead={<Pill tone={QUEST_TONE[quest.status]} title={t(`statusHint.${reads}`)}>{t(`status.${reads}`)}</Pill>}
      clamp={2}
      wholeElsewhere={!!body && named.length > LONG_TITLE}
      facts={questFacts(t, { quest, session, sitting })}
      // *View full request* (HEAD2b, as the ask's HEAD2d): a long title is read whole, as written, in the request below, where
      // the focus goes; on the facts line, since the head has no door slot. Where the body holds nothing more, nothing to go to.
      fact={!(workflow && workflow.runs.length > 0) && !(body && named.length > LONG_TITLE) ? undefined : (
        <>
          {workflow && workflow.runs.length > 0 && <WorkflowLine runs={workflow.runs} onOpen={workflow.onOpen} />}
          {body && named.length > LONG_TITLE && (
            <Button variant="ghost" className="ml-1 px-0 py-0 text-small" onClick={() => request.current?.focus()}>
              {t('work.head.viewRequest')}
            </Button>
          )}
        </>
      )}
      acts={acts}
      fold={fold}
      more={more}
    />
  );

  return (
    <ViewMain header={head} menu={menu}>
      {asking === 'pause' && work && pauseLines && (
        <PauseAsk
          className="mb-4"
          target={target}
          lines={pauseLines}
          meanIt={t('quests.detail.pauseMeanIt')}
          busy={waiting}
          onPause={(answered) => work.onPause(answered)}
          onClose={() => setAsking(null)}
        />
      )}

      {asking === 'abandon' && work && listed && (
        <AbandonAsk
          target={target}
          plan={listed}
          meanIt={t('quests.detail.abandonMeanIt')}
          placeholder={t('quests.detail.abandonWhy')}
          busy={waiting}
          onAbandon={(why, answered) => work.onAbandon(why, listed.abandon.pieces, answered)}
          onClose={() => { setAsking(null); setListed(null); }}
        />
      )}

      {history && (asking === 'clear' || asking === 'clearFailed') && (asking === 'clear' ? history.plan : history.failed) && (
        /* 🔴 A clear removes what this machine kept, which nothing gives back (D153): the first press lists what goes and
           what stays, held from here, and the second sends exactly that (§5). */
        <ClearAsk
          key={asking}
          className="mb-4"
          target={clearTarget(asking)}
          plan={(asking === 'clear' ? history.plan : history.failed)!}
          meanIt={asking === 'clear'
            ? t('quests.detail.clearMeanIt')
            : (list) => t('quests.detail.clearFailedMeanIt', { count: list.sessions })}
          busy={waiting}
          onClear={(units, answered) => history.onClear(clearTarget(asking), units, answered)}
          onClose={() => setAsking(null)}
        />
      )}

      {deleting && deletable && (
        /* 🔴 A delete removes the record, which nothing gives back (D95) — so the first press only asks, the
           way removing an account does, and the second is the one that deletes. It stays open until the service
           answers, and says a refusal inside itself (UXFIX2). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.deleteTitle')}
          says={t('quests.detail.deleteConfirm')}
          meanIt={t('quests.detail.deleteMeanIt')}
          busy={busy}
          onConfirm={onDelete!}
          onClose={() => setAsking((was) => (was === 'delete' ? null : was))}
        />
      )}

      {asking === 'done' && moving && (
        /* The person's done (QUESTCLOSE1): it asks once, since a done does not move again, with their note, which the
           record keeps after the sentence saying the done was theirs. Open until the service answers, a refusal said inside
           it (UXFIX2). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.doneTitle')}
          says={[
            ...(requirements.length > 0 ? [t('quests.detail.doneRequirements')] : []),
            ...(lostHere ? [t('quests.conflict.doneChoices')] : []),
          ].reduce((first, second) => t('quests.requirements.join', { first, second }), t('quests.detail.doneConfirm', { id: quest.id }))}
          meanIt={t('quests.detail.doneMeanIt')}
          busy={busy}
          onConfirm={(answered) => onRespond('done', doneNote.trim() || null, answered)}
          onClose={() => {
            setAsking((was) => (was === 'done' ? null : was));
            setDoneNote('');
          }}
        >
          <input
            aria-label={t('quests.detail.doneNote')}
            placeholder={t('quests.detail.doneNote')}
            value={doneNote}
            onChange={(e) => setDoneNote(e.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}

      {asking === 'reopen' && reopenable && (
        /* The person's reopen of a sealed submission (FABRIC-CLOSE1c3, D156): it asks once, naming the step it holds, and sends
           the submission this page showed, which the door refuses once another is current. Open until the service answers, a
           refusal said inside it (UXFIX2). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.reopenTitle')}
          says={t('quests.detail.reopenConfirm', { id: quest.id, step: reopen!.step })}
          meanIt={t('quests.detail.reopenMeanIt')}
          busy={busy}
          onConfirm={(answered) => reopen!.onReopen(reopenNote.trim() || null, answered)}
          onClose={() => {
            setAsking((was) => (was === 'reopen' ? null : was));
            setReopenNote('');
          }}
        >
          <input
            aria-label={t('quests.detail.reopenNote')}
            placeholder={t('quests.detail.reopenNote')}
            value={reopenNote}
            onChange={(e) => setReopenNote(e.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}

      {declining && moving && (
        /* Declining without a reason is refused by the service; the ask does not offer the mistake. It stays open until
           the service answers, a refusal said inside it (UXFIX2b2b). */
        <InlineConfirm
          className="mb-4"
          label={t('quests.detail.declineTitle')}
          says={t('quests.detail.declineSays', { id: quest.id })}
          meanIt={t('quests.detail.declineConfirm')}
          busy={busy}
          ready={reason.trim() !== ''}
          onConfirm={(answered) => onRespond('decline', reason, answered)}
          onClose={() => {
            setAsking((was) => (was === 'decline' ? null : was));
            setReason('');
          }}
        >
          <input
            aria-label={t('quests.detail.declinePlaceholder')}
            placeholder={t('quests.detail.declinePlaceholder')}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}

      {/* What its head leaves out (UX7c, D152 §7): its id with *Copy*, whom it asks and who asked, its lanes, and the full
          times. Folded, since these are looked up rather than read. */}
      <DetailsFold
        className="mb-4"
        summary={[
          `#${quest.id}`,
          t('quests.facts.filedAt', { at: stamp(quest.filed) }),
          ...(quest.updated !== quest.filed ? [t('quests.facts.movedAt', { at: stamp(quest.updated) })] : []),
        ].join(' · ')}
        rows={[
          { label: t('work.head.id'), value: `#${quest.id}`, mono: true, copy: quest.id },
          { label: t('quests.detail.from'), value: quest.from },
          { label: t('quests.detail.to'), value: quest.to },
          // Each lane as the repository declares it, named where its registration says (D115 §2.2).
          { label: t('quests.detail.lanes'), value: lanes || null },
          { label: t('quests.detail.filed'), value: `${stamp(quest.filed)} · ${ago(quest.filed)}` },
          { label: t('quests.detail.moved'), value: quest.updated !== quest.filed ? `${stamp(quest.updated)} · ${ago(quest.updated)}` : null },
          // The short title its publisher gave (SESSUX1j), where the head says it and the whole title stands below.
          { label: t('quests.field.short'), value: quest.short && quest.short !== quest.title ? quest.short : null },
        ]}
      />

      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-body empty:hidden">
        {question && (
          // What its taker asked and waits on (D79), opened in place: the answer is read there.
          <Fact name={t(answered ? 'quests.detail.asked' : 'quests.detail.waitsOn')}>
            <span className="inline-flex flex-wrap items-baseline gap-1.5">
              {question.quest ? (
                <button
                  type="button"
                  className="cursor-pointer border-0 bg-transparent p-0 text-left text-body text-accent underline-offset-2 hover:underline"
                  onClick={() => onOpenQuest(question.id)}
                >
                  <span className="font-mono text-meta">#{question.id}</span> {question.quest.title}
                </button>
              ) : (
                <span className="font-mono text-meta">#{question.id}</span>
              )}
              {question.quest && (
                <>
                  <span className="text-meta text-ink-faint">→ {question.quest.to}</span>
                  <Pill tone={QUEST_TONE[question.quest.status]}>{t(`status.${question.quest.status}`)}</Pill>
                </>
              )}
            </span>
            <span className="mt-0.5 block text-small text-ink-soft">
              {t(answered ? 'quests.detail.answered' : 'quests.detail.waitsWhy')}
            </span>
          </Fact>
        )}
        {(because || hold || pausedUnseen) && (
          // Why this machine's driver is not starting it, in its own words (D46 §3) — the whole sentence here,
          // where there is room. The trust hold stands on its own: it arrives in the same tick as the sentence,
          // and the grant must not wait on a second list having arrived too.
          <Fact name={t('quests.detail.sitting')}>
            {/* A wait for an account (TOOL4g, D125 §4): not parked, so no Retry; it starts by itself at the reset. */}
            {waitsForAccount(because) && (
              <span className="mb-1 block"><Pill tone="neutral">{t('work.waitsForAccount')}</Pill></span>
            )}
            <Inline
              text={because ? sittingSentence(because) : pausedUnseen ? t('quests.detail.pausedOwn') : t('work.attention.trustWhy')}
            />
            {/* A pause comes before every other reason (D132 §2.3): its own is resumed here, where Try again stands for a
                stop; an ask's, or the quest's whose question this is, is resumed on that page, a door away. */}
            {(ownPause || pausedUnseen) && work && (
              <span className="mt-1.5 block">
                <Button disabled={waiting} onClick={() => press('resume')}>{t('quests.detail.resume')}</Button>
              </span>
            )}
            {pausedBy?.scope === 'ask' && onOpenAsk && (
              <span className="mt-1.5 block">
                <Button variant="ghost" onClick={() => onOpenAsk(pausedBy.id)}>{t('quests.detail.openAsk', { id: pausedBy.id })}</Button>
              </span>
            )}
            {pausedBy?.scope === 'quest' && !ownPause && (
              <span className="mt-1.5 block">
                <Button variant="ghost" onClick={() => onOpenQuest(pausedBy.id)}>{t('quests.detail.openPausing', { id: pausedBy.id })}</Button>
              </span>
            )}
            {/* The one hold only the person can lift, offered where it is read (D73). */}
            {hold && onGrant && onTrusting && !trusting && (
              <span className="mt-1.5 block"><Button onClick={() => press('trust')}>{t('trust.open')}</Button></span>
            )}
            {/* And the other two: a quest parked by its strikes, started again on the press (RETRY1), counted from
                where it stands so the next failures park it again; and one the person's stop holds, released from that
                stop (SESSUX1b, D126 §3.4). One act and one word, on this page and on the session that holds it. */}
            {(because?.verdict === 'Exhausted' || because?.verdict === 'Stopped') && onRetry && (
              <span className="mt-1.5 block">
                <Button disabled={retrying} onClick={() => press('retry')}>{t('quests.detail.retry')}</Button>
              </span>
            )}
          </Fact>
        )}
        {endedHere && (
          /* QUESTCLOSE1: no verdict says why it sits, since nothing here carries a finished session's quest on, so the page
             says it, with the person's done where it is read, as Try again stands under a stop's sentence. */
          <Fact name={t('quests.detail.sitting')}>
            <Inline text={t('quests.detail.endedTaken', { session: session!.id })} />
            {asking !== 'done' && (
              <span className="mt-1.5 block">
                <Button disabled={waiting} onClick={() => press('done')}>{t(QUEST_ACT.done.label)}</Button>
              </span>
            )}
          </Fact>
        )}
        {reads === 'Submitted' && quest.submission && (
          /* When its agent made it ready (FABRIC-CLOSE1d), and the person's reopen of it where a step stands on it (CLOSE1c3). */
          <Fact name={t('quests.detail.submitted')}>
            {stamp(quest.submission.at)} · {ago(quest.submission.at)}
            {reopenable && asking !== 'reopen' && (
              <span className="mt-1.5 block">
                <Button disabled={waiting} onClick={() => press('reopen')}>{t(QUEST_ACT.reopen.label)}</Button>
              </span>
            )}
          </Fact>
        )}
        {abandoned && (
          /* When its work was abandoned on this machine (§4.2); the reason is the decline's note, on the record below. */
          <Fact name={t('quests.detail.abandonedAt')}>{stamp(abandoned.at)} · {ago(abandoned.at)}</Fact>
        )}
      </dl>

      {abandoned && (
        <div className="mb-4">
          <AbandonedWork target={target} outcome={abandoned.outcome} went={t('quests.detail.went')} stayed={t('quests.detail.stayed')} />
        </div>
      )}

      {!work && moving && (
        /* A browser has no driver (D47 §4): none of the three is offered, and the terminal's commands are named. */
        <Prose className="mb-4 text-small"><Inline text={t('quests.detail.noDriver', { id: quest.id })} /></Prose>
      )}

      {hold && onGrant && trusting && (
        <div className="mb-4">
          <TrustAsk hold={hold} busy={granting} onCancel={onTrusting && (() => onTrusting(false))} onGrant={(answered) => onGrant(hold, answered)} />
        </div>
      )}

      {reopenHeld && (
        /* A step its source's reopen holds (FABRIC-CLOSE1c3; D173's CLOSE1c note, choice 2): what it waits on, so above the body.
           Withdrawn while open, it grows from the source's next unheld submission by itself; taken, it waits for the person's word,
           go on once the source is ready again or abandon, which is their decline. Daoris never chooses, and no yes lifts it. */
        <section
          aria-label={t('quests.reopened.title')}
          className="mb-4 rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-3 py-2.5"
        >
          <SectionTitle>{t('quests.reopened.title')}</SectionTitle>
          <p className="m-0 text-body">
            {t(quest.status === 'Open' ? 'quests.reopened.withdrawn' : goesOn ? 'quests.reopened.ready' : 'quests.reopened.waits', {
              parent: quest.parent ?? '',
            })}
          </p>
          {moving && (
            <span className="mt-2 flex flex-wrap gap-2">
              {goesOn && (
                <Button variant="primary" disabled={goOn?.going === true} onClick={() => press('goOn')}>
                  {t(labelOf('goOn'))}
                </Button>
              )}
              {/* Abandoning it is the person's decline (CLOSE1c's choice 2): this machine's abandon where its driver offers one,
                  which stops its work too, and the decline's own ask otherwise. */}
              <Button disabled={waiting} onClick={() => press(offers.abandon ? 'abandon' : 'decline')}>
                {t(heldMine ? 'quests.reopened.drop' : QUEST_ACT.abandon.label)}
              </Button>
            </span>
          )}
          {/* A decline closes the step for every machine, where a lost take's dismissal moves only its conflict (D178 point 2). */}
          {moving && <p className="m-0 mt-2 text-small text-ink-soft">{t('quests.reopened.declines')}</p>}
        </section>
      )}

      {held && requirements.length > 0 && (
        /* A departure that holds the quest for the person's yes (DRIFT1d2, D133 §4): what it waits on, so above the body,
           as a conflict sits, with the yes where the departure is read. */
        <QuestRequirements
          className="mb-4"
          id={quest.id}
          requirements={requirements}
          answers={quest.answers}
          status={quest.status}
          held
          next={quest.then?.[0]}
          accepting={accepting}
          onAccept={acceptable ? () => press('accept') : undefined}
        />
      )}

      {quest.setUpIn && (
        /* A set-up step's review (REVIEWENV1g): what it waits on, so above the body, as a held departure sits; every set-up
           it said, newest first, and the verdict's presses under the newest. */
        <ReviewGate
          className="mb-4"
          environment={quest.setUpIn}
          state={stepState(quest)}
          step={quest}
          served={review?.served ?? null}
          acts={review?.acts}
          busy={review?.busy}
          whole
        />
      )}

      {/* A move that reached the remote second (D68 §5): kept on the quest for a person and never merged, so it sits above the body,
          as what this quest waits on; this machine's own lost take with its two choices (SYNCMERGE1c2, D178 point 2). */}
      <QuestConflicts className="mb-4" quest={quest} busy={dismissing || forwarding} onDismiss={onDismiss} onForward={onForward} />

      {/* The ask itself, as the asker wrote it: content, wrapping at the column's edge with the title above it (D141: a
          65ch body beside a title that ran the pane left 750 px empty on the install). Its first line is left out where
          the head already said it (UX7c). */}
      {body && (
        <p
          ref={request}
          id="quest-request"
          tabIndex={-1}
          className="m-0 whitespace-pre-wrap text-body leading-relaxed outline-none"
        >
          {body}
        </p>
      )}

      {!held && requirements.length > 0 && (
        /* What the person required (DRIFT1c, D133 §3), in their words: the measure, where the body above is the intake's
           reading of the ask. Then how its done answered each, and when a departure was accepted. */
        <QuestRequirements
          className="mt-4"
          id={quest.id}
          requirements={requirements}
          answers={quest.answers}
          status={quest.status}
          accepted={quest.accepted}
        />
      )}

      {(quest.links?.length ?? 0) > 0 && (
        /* The asker's addresses, as links — the service accepted only http and https, so each is an address
           and nothing that would run. They open where the person chose, their browser or Daoris's (BRW7). */
        <div className="mt-4">
          <PageSection title={t('quests.detail.links')}>
            <ul className="m-0 grid list-none gap-1 p-0">
              {quest.links!.map((link) => (
                <li key={link} className="min-w-0">
                  <ExternalLink
                    href={link}
                    className="inline-flex max-w-full items-center gap-1.5 text-body text-accent underline-offset-2 hover:underline"
                  >
                    <Icon name="link" size={12} />
                    <span className="truncate">{link}</span>
                  </ExternalLink>
                </li>
              ))}
            </ul>
          </PageSection>
        </div>
      )}

      {(quest.attachments?.length ?? 0) > 0 && (
        /* The files, by name (D65 §2). One this machine keeps opens through the host's own route — never by its
           path, which a page does not show — and a picture is shown as one. One named on the record and kept
           elsewhere says so, rather than looking like a broken link. */
        <div className="mt-4">
          <PageSection title={t('quests.detail.files')}>
            <ul className="m-0 grid list-none gap-2 p-0">
              {quest.attachments!.map((file) => (
                <li key={file.sha256} className="min-w-0 text-body">
                  {file.path ? (
                    <ExternalLink href={api.attachmentUrl(quest.id, file.sha256)} className="group inline-grid max-w-full gap-1.5 text-accent">
                      <span className="inline-flex min-w-0 items-center gap-1.5">
                        <Icon name="attach" size={12} />
                        <span className="truncate underline-offset-2 group-hover:underline">{file.name}</span>
                        <span className="shrink-0 font-mono text-meta text-ink-faint">{size(file.bytes)}</span>
                      </span>
                      {isImage(file.name) && (
                        <img
                          src={api.attachmentUrl(quest.id, file.sha256)} alt={file.name}
                          className="max-h-60 max-w-full rounded-control border border-line object-contain"
                        />
                      )}
                    </ExternalLink>
                  ) : (
                    <span className="inline-flex min-w-0 flex-wrap items-center gap-1.5 text-ink-soft">
                      <Icon name="attach" size={12} />
                      <span className="truncate">{file.name}</span>
                      <span className="font-mono text-meta text-ink-faint">{size(file.bytes)} · {t('quests.detail.fileElsewhere')}</span>
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </PageSection>
        </div>
      )}

      {/* How its conflicts ended (SYNCMERGE1c2, D178 point 7): history, so below what it carries, with nothing to press. */}
      <SettledConflicts quest={quest} />

      {chain.length > 1 && (
        /* The chain this quest belongs to (MAP1): the ask, the steps before and after it, what its close will
           still publish (D65 §4, `{parent}` as the asker wrote it), and on what each step ran. */
        <div className="mt-4">
          <ChainStrip
            chain={chain}
            onQuest={(other) => onOpenQuest(other.id)}
            onSession={onAttend ? (ran) => onAttend(ran.id) : undefined}
            nameOf={nameOf}
          />
        </div>
      )}

      {quest.note && (quest.noteParts?.length
        // Daoris's lines in the reader's language, the person's words as theirs; a note without parts as kept (QUESTCLOSE1b2).
        ? <Note note={quest.note} parts={quest.noteParts} className="mt-4 rounded-control bg-accent-soft px-3 py-2.5 text-body italic" />
        : <p className="mt-4 rounded-control bg-accent-soft px-3 py-2.5 text-body italic">{quest.note}</p>)}

      {session && (
        /* The driven session's RECORD (D46 §4) — read-only here: a quest is decided on its page, and its sessions
           are managed on theirs (D126 §3.6), so the stop is the session's page header's, behind the door below. Its
           note's Daoris lines are worded in the reader's language and the agent's words shown as written (LANG1b);
           the evidence is the driver's observation and renders verbatim. */
        <section className="mt-5" aria-label={t('quests.session.title')}>
          <SectionTitle>{t('quests.session.title')}</SectionTitle>
          <div className="flex flex-wrap items-center gap-2">
            <Pill tone={SESSION_TONE[shown ?? session.state]}>{t(shownKey(shown ?? session.state))}</Pill>
            <span className="font-mono text-meta text-ink-faint">
              {session.id} · {sessionTool(session, false, nameOf)}
              {' · '}{t('quests.session.moved', { ago: ago(session.updated) })}
            </span>
          </div>
          {/* What it waits on, in the row's own words (D126 §2.4): its last end reads as finished beside a quest that is not. */}
          {waitsOn && <p className="m-0 mt-2 text-body text-ink-soft">{t(waitsOn.line, waitsOn.values)}</p>}
          {/* A working session's past ending is left out of the reading, never of the record (WAITSHOWN1, D137 §2.3). */}
          <Note
            note={session.state === 'working' ? (session.noteParts?.length ? undefined : session.note) : session.note}
            parts={session.state === 'working' ? liveParts(session.noteParts) : session.noteParts}
            className="mt-2 text-body text-ink-soft"
          />
          {session.evidence && (
            <pre className="mt-2 mb-0 overflow-x-auto whitespace-pre-wrap rounded-control border border-line-strong bg-raised px-3 py-2.5 font-mono text-small">
              {session.evidence}
            </pre>
          )}
          {/* One home for the stream (design §3, D55): a session's console is the frame's panel. This keeps the
              record summary and becomes a DOOR — which is only offered where Sessions exists at all. */}
          {onAttend && <Button className="mt-2.5" onClick={() => press('session')}>{t('work.open')}</Button>}
          <p className="mt-2 mb-0 text-small text-ink-faint">{t('quests.session.hint')}</p>
        </section>
      )}

      {trace && (
        /* How it came to be (TRACE1b): folded, so nothing is read until the person opens it. Its sessions open in
           Sessions, its other quests here and its ask on its page, each where this page already has the door. */
        <HowItCameToBe
          className="mt-5"
          kind="quest"
          id={quest.id}
          {...trace}
          onSession={onAttend}
          onQuest={onOpenQuest}
          onAsk={onOpenAsk}
          nameOf={nameOf}
        />
      )}
    </ViewMain>
  );
}

/**
 * Quests' main area with no record to show (D118 §3b): **nothing chosen** says how to choose and offers the
 * list's `＋` kinds, *Ask* first; **gone** says the chosen quest or ask is no longer here; **loading** is skeleton
 * rows, never the empty state; and a record that never had an answer says the error's sentence in place.
 */
export function QuestsMainNotice({ state, gone, actions = [], sentence, onAct }: {
  state: 'none' | 'gone' | 'loading' | 'unanswered';
  /** What has gone: a quest, or an ask. */
  gone?: 'quest' | 'ask';
  /** The list's `＋` kinds, offered with nothing chosen. */
  actions?: { id: string; label: string }[];
  /** The sentence for a record that has never had an answer. */
  sentence?: string;
  onAct?: (id: string) => void;
}) {
  const { t } = useTranslation();
  if (state === 'unanswered') return <ViewMain><Prose><Inline text={sentence ?? ''} /></Prose></ViewMain>;
  const none: MainNotice = {
    icon: 'quests',
    headline: t('quests.none.headline'),
    body: t('quests.none.body'),
    action: actions.length > 0 && (
      <div className="flex flex-wrap justify-center gap-2">
        {actions.map((action) => <Button key={action.id} onClick={() => onAct?.(action.id)}>{action.label}</Button>)}
      </div>
    ),
  };
  const went: MainNotice = gone === 'ask'
    ? { icon: 'quests', headline: t('asks.gone.headline'), body: t('asks.gone.body') }
    : { icon: 'quests', headline: t('quests.gone.headline'), body: t('quests.gone.body') };
  return <ViewMain state={state} none={none} gone={went} />;
}

import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type Quest, type Session, type SubmissionRef } from '../data/api';
import { type ChainStep } from '../map/chain';
import { type AccountNamer } from '../agents/tools';
import { type Consideration, type TrustHold } from '../data/signals';
import { Button, Icon, Inline, Menu, personsSaid, Pill, Prose, QUEST_TONE, SectionTitle } from '../design/ui';
import { type SessionGrouping, shownOf } from '../work/groups';
import { placedFact } from '../work/rail/SessionRow';
import { lastAbandon, pauseAsk, type WorkDoor, workOffers, type WorkPlan, type WorkTarget } from '../work/pausing/pausing';
import { type GraphEnd } from '../work/head/milestone';
import { clearOffered, type HistoryDoor, type HistoryTarget } from '../work/history/history';
import { questFacts, questNext } from '../work/head/headFacts';
import { NextDoor } from '../work/head/NextDoor';
import { type TraceDoor } from '../work/head/HowItCameToBe';
import { questName, sessionOrigin } from '../work/identity';
import { type Answered } from '../work/InlineConfirm';
import { stepState } from '../work/gates/review';
import { ReviewGate, type ReviewGateActs } from '../work/gates/ReviewGate';
import { QuestConflicts, readConflicts, SettledConflicts } from './Conflicts';
import { type QuestMovesDoor } from './QuestFlow';
import { QuestRequirements } from './Requirements';
import { shownStatus } from './records';
import { TrustAsk } from '../work/waits/TrustAsk';
import { type MainNotice, PageHead, ViewMain } from '../work/frame/ViewMain';
import { AbandonedWork } from '../work/pausing/WorkAsks';
import { type RunLineDoor } from '../workflow/run';
import { WorkflowLine } from '../workflow/WorkflowLine';
import { QuestContent } from './page/QuestContent';
import { QuestFacts } from './page/QuestFacts';
import { QuestAsks } from './page/QuestAsks';
import { type QuestAct, QUEST_ACT, buttonOf } from './page/questActs';
import { QuestRecord } from './page/QuestRecord';


/**
 * A quest's body as its page shows it under the head: its first line left out where it is the title the
 * head said, as the ask page leaves its first line out; where the head said a short title instead, the body as
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

/**
 * **A quest's page**: its record in Quests' main area. Its acts are in its header and the loud one is its next step:
 * *Take* while it is open, *Mark done* once it is taken. Then what it is waiting on, the holds only the person lifts
 * (a folder to trust; a quest parked by its strikes; one their stop holds), a conflict a person
 * must see, the ask itself, what it carries, its chain and its driven session's record.
 *
 * @remarks
 * **A molecule**: every state is reached by its props, and every press goes out. What asks under the header —
 * *Decline…*'s reason, *Delete…*'s sentence — is its own, so it resets with the quest it asks about.
 *
 * - **A delete asks once**: its first press opens a sentence saying the second removes the record, which
 *   nothing gives back; offered only where the service says it may go (`deletable`). It is the one inline confirmation:
 * open until the service answers, a refusal said inside it.
 * - **A decline needs its reason**, which the service refuses without; the form does not offer the mistake.
 * - **What the service and the driver say is said verbatim**: the session's evidence, a conflict's note, the driver's
 *   sentence about why it sits (translated by its verdict, never by its words). The session's note is `Note`'s:
 * Daoris's lines by their codes in the reader's language, someone's words as written.
 * - **A page never prints a machine path it is answered**: a kept file opens through the host's own
 *   route, and one kept elsewhere says so.
 * - **Its work is paused and abandoned here**, from this machine's driver's plan: *Pause…* while open
 *   or taken and not paused on its own, asking once where it ends work in flight; *Abandon…* beside *Decline…*,
 *   listing first and abandoning on its second press with the person's reason. *Resume* stands under *Sitting* for
 *   its own pause, where *Try again* stands for a stop; for another's pause the sentence names whose, and a door opens it.
 *   After an abandon it says when, *What went* and *What stayed*. A browser names the terminal's commands instead.
 * - **What the person required, and how its done answered**: each requirement in their words with
 *   its check, met or departed with the reason and the words it relies on, after the body. A departure that holds the
 *   quest for their yes is what it waits on, so it moves above the body, as a conflict sits, with *Accept the departure*:
 *   one press, the service's accept door, and its header says *awaits your yes*.
 * - **A closed quest's work is cleared from this machine here**: *Clear
 *   from this machine…* and *Clear failed sessions…* in its ⋯, each where its plan says something may go and absent where
 *   it does not (a quest an ask asked is cleared on the ask's page), listing under the header first and clearing on the
 *   second press exactly what it listed. A browser has no driver, and offers neither.
 * - **The person's done is theirs**: *Mark done…* asks once under the header, with their note,
 *   and sends their done, which the service writes as theirs and which answers none of their requirements one by one; the
 *   ask says so where it carries some. A taken quest whose last session here ended in a way nothing carries on (finished or
 *   declined at a checkpoint) says so under *Sitting*, with *Mark done…* there, since such a quest would otherwise sit taken
 *   with nothing on its page that says why or closes it.
 * - **A set-up step's page is where its review is answered**:
 *   *Review in `<environment>`* above the body, every set-up it said newest first, and under the newest *Reviewed*, *Not
 *   yet…*, *Show it again* and *Skip…*. A done its review holds waits for that, not for a yes: its header says *awaits
 *   review* and no *Accept the departure* is offered for it, which the accept door would refuse.
 * - **A closure graph's quest reads *Submitted***: taken at its agent's `ready`, then
 *   the hold it waits on, as a done's, then done at the graph's close. Its sealed submission is reopened here:
 *   *Reopen…* asks once with the step it publishes named, and sends the submission it showed. A step its source's reopen
 *   holds says so in its own words, offering *Go on with it* once the source is ready again and *Abandon…*, never the yes. A quest
 *   no closure graph holds, which is every quest on an install that binds none, reads as any other.
 * - **Its closure graph's end is one of its facts**: *Close*, in the milestone's words, where its
 *   unit's record says the person's own done ended it before its end, naming the nodes it stopped, or that it closed only in part,
 *   naming the quest lost, its close node's stop offered in the danger ink. That stop is What needs you's press, as on a work's foot.
 */
export function QuestPage({
  quest, lanes, question, sitting, hold, chain = [], session,
  busy = false, retrying = false, trusting = false, granting = false, dismissing = false, forwarding = false, accepting = false,
  onRespond, onDelete, onDismiss, onForward, onRetry, onTrusting, onGrant, onOpenQuest, onAttend, onOpenAsk, onAccept, work, history, trace,
  nameOf, review, workflow, grouping, reopen, goOn, moves, ends = [], limitWait,
}: {
  /**
   * Its closure graph's ends, which this machine's driver reads from its unit's record
   * (`WORK_STATE`'s `ends` of `quest:<id>`): said among its facts. Absent in a browser, which has no driver, and on a quest no closure
   * graph ended early or in part.
   */
  ends?: GraphEnd[];
  /**
   * Its flowchart's fold and its moves: the page's organism holds both, so the moves are read only once the
   * person opens it. Absent, the section holds its own fold and draws the statuses alone.
   */
  moves?: QuestMovesDoor;
  /**
   * The person's reopen of its sealed submission: the step of its chain that stands on it, and the press,
   * told back to its ask so a refusal is said inside it. Absent where no step stands on its submission; offered only on a quest
   * at an unheld submission.
   */
  reopen?: { step: string; onReopen: (note: string | null, answered: Answered) => void };
  /**
   * A step its source's reopen holds: whether the source is ready again, so the person's go-on can take, and the
   * press. Absent where there is no door to say it through.
   */
  goOn?: { ready: boolean; going?: boolean; onGoOn: () => void };
  /**
   * Where its chain's work here stands in its workflow, said in one line among the head's
   * facts whose door opens the run. Absent in a browser, which has no driver to read one.
   */
  workflow?: RunLineDoor;
  /**
   * A set-up step's review: its presses, whether one is on its way, and whether Daoris still serves its newest
   * set-up's tab here (null where no shell says). Absent, its review is shown with no press.
   */
  review?: { acts: ReviewGateActs; busy?: boolean; served?: boolean | null };
  /**
   * What a person calls an account, from the roster its organism holds: its session line, its chain
   * and its trace say each account by it. Absent, as in a browser, each record's id is said.
   */
  nameOf?: AccountNamer;
  /**
   * When its session, a line its account's limit cut off, goes on by itself and how: the head's sentence,
   * worded by its organism from the accounts it holds (`limitWaitLines`). Absent, as in a browser, nothing is claimed.
   */
  limitWait?: readonly string[] | null;
  /**
   * How it came to be, folded at the foot of its page: the page's organism holds the fold and the read.
   * Absent in a browser, which has no driver to read this machine's records.
   */
  trace?: TraceDoor;
  quest: Quest;
  /** Its lanes as its repository declares them, each named where its registration says. */
  lanes?: string;
  /** What its taker asked another repository and waits on, with the quest where the page holds it. */
  question?: { id: string; quest?: Quest } | null;
  /** Why this machine's driver leaves it waiting, in the driver's words. */
  sitting?: Consideration | null;
  /** A start the driver holds for the agent's trust where it would run. */
  hold?: TrustHold | null;
  /** The chain it belongs to; drawn only where there is more than this quest. */
  chain?: ChainStep[];
  /**
   * Its driven session's freshest record: a retry is its own record, and the page says where things stand now. Read
   * here and managed in Sessions: its stop is its page header's there, behind the door below.
   */
  session?: Session | null;
  /**
   * Where the session list's reader placed that session, which says the words the record cannot: *awaiting reply*
   * for the last session of a quest that waits on another repository's answer. Absent where no driver answers,
   * and the record's own state shows.
   */
  grouping?: SessionGrouping | null;
  busy?: boolean;
  retrying?: boolean;
  /** The agent's trust question is open for the hold, asked only on the press. */
  trusting?: boolean;
  granting?: boolean;
  dismissing?: boolean;
  /** A bring-forward of this machine's lost take is on its way. */
  forwarding?: boolean;
  /** The person's yes to a departure is on its way. */
  accepting?: boolean;
  /**
   * Take, decline with its reason, or the person's done with their note, or null for none, told back to its
   * ask so a refusal is said inside it.
   */
  onRespond: (action: 'take' | 'done' | 'decline', reason?: string | null, answered?: Answered) => void;
  /** Delete the quest, told back to its ask — absent where there is no door to do it. */
  onDelete?: (answered: Answered) => void;
  /**
   * Dismiss a conflict by its name: this machine's lost take's *Drop mine*, then the fresh takes' conflicts its lost
   * bring-forwards left, which its row reads as its own.
   */
  onDismiss: (machine: string, sequence: number, also?: SubmissionRef[]) => void;
  /**
   * Bring this machine's lost take forward by the conflict's name. Absent where there is no door to say
   * it through, and then never offered.
   */
  onForward?: (machine: string, sequence: number) => void;
  /**
   * *Try again*: start a quest parked by its strikes again, or release one the person's stop holds
   * — the shell's, which the driver judges by its verdict.
   */
  onRetry?: () => void;
  /** Open or put down the trust question — the shell's, which closes it once the grant is written. */
  onTrusting?: (open: boolean) => void;
  /** Grant the agent's trust for the folder held — the shell's; a refusal is said in the question. */
  onGrant?: (hold: TrustHold, answered: Answered) => void;
  onOpenQuest: (id: string) => void;
  /** The door into Sessions: absent where there are none, a browser, or no driver is attached. */
  onAttend?: (session: string) => void;
  /** Open an ask's page: where the pause that holds this quest is resumed. */
  onOpenAsk?: (id: string) => void;
  /**
   * The person's yes to a done's departure from what they required: the service's accept door, which
   * lets what the departure held go on. Absent where there is no door to say it through.
   */
  onAccept?: () => void;
  /** This machine's driver's half: the plan and the three presses. Absent in a browser, which has no driver. */
  work?: WorkDoor;
  /**
   * Clearing its finished work from this machine: the plans of its work and of its failed sessions, and
   * the second press. Absent in a browser, which has no driver and no home.
   */
  history?: HistoryDoor;
}) {
  const { t } = useTranslation();
  // One question asks under the header at a time: a decline's reason, a delete's sentence, a pause's, the abandon's list, a
  // clear's list.
  const [asking, setAsking] = useState<'done' | 'decline' | 'delete' | 'pause' | 'abandon' | 'clear' | 'clearFailed' | 'reopen' | null>(null);
  // The person's words on a reopen, kept while its ask is open.
  const [reopenNote, setReopenNote] = useState('');
  // The plan the abandon's list showed, held from when it opened: the second press sends exactly what it listed.
  const [listed, setListed] = useState<WorkPlan | null>(null);
  const [reason, setReason] = useState('');
  // The person's words on their done, kept while its ask is open.
  const [doneNote, setDoneNote] = useState('');
  const declining = asking === 'decline';
  // A delete asks once: the first press arms it, and only the second removes the record.
  const deleting = asking === 'delete';
  const moving = quest.status === 'Open' || quest.status === 'Taken';
  const deletable = quest.deletable === true && onDelete !== undefined;
  const answered = question?.quest?.status === 'Done' || question?.quest?.status === 'Declined';
  // A wait is said by its own row, with the question; the driver's sentence under it would say it again.
  const because = sitting?.verdict === 'Waiting' ? null : sitting ?? null;
  const target: WorkTarget = { scope: 'quest', id: quest.id };
  const offers = workOffers(work?.plan);
  const waiting = busy || work?.busy === true || history?.busy === true;
  // Its clears, where it is closed and its plans say something may go: absent, never disabled, anywhere else.
  const closed = quest.status === 'Done' || quest.status === 'Declined';
  const clearable = closed && clearOffered(history?.plan);
  const failedClearable = closed && clearOffered(history?.failed);
  const clearTarget = (scope: 'clear' | 'clearFailed'): HistoryTarget => ({ scope: scope === 'clear' ? 'quest' : 'failed', id: quest.id });
  const pauseLines = work?.plan ? pauseAsk(work.plan, { wired: work.wired }) : null;
  const abandoned = lastAbandon(work);
  // Whose pause holds it, from the tick's verdict: its own is resumed here, an ask's or another quest's on that page.
  const pausedBy = because?.verdict === 'Paused' ? because.pausedBy ?? null : null;
  const ownPause = pausedBy?.scope === 'quest' && pausedBy.id === quest.id;
  // Its own pause where the tick has no verdict for it (its session waits on you): said from the plan, with Resume.
  const pausedUnseen = offers.paused && !pausedBy;
  // The session as the list's reader reads it: its derived word where it said one, so the last session
  // of a quest that awaits another repository's answer reads as awaiting, not as completed; the record's own otherwise.
  const shown = session ? shownOf(session, grouping, undefined) : null;
  const waitsOn = shown === 'awaiting-reply' ? placedFact(grouping, shown) : null;
  // Taken, and its last session here ended in a way nothing here carries on: finished or declined at a
  // checkpoint, no answer waiting. The planner says nothing of it, so the page does, with the person's done beside it. A
  // failure, a stop and an answered park are the driver's to say (a carry-on, a park, *Try again*), and a teammate's record
  // is not this machine's take.
  const endedHere = quest.status === 'Taken' && !because && !hold && !pausedUnseen && session != null
    && sessionOrigin(session) === null
    && ((session.state === 'completed' && personsSaid(session) === null) || session.state === 'declined');

  // A pause that stops nothing applies at once, with its notice, since nothing is lost.
  const onPauseFirst = () => {
    if (pauseLines === null) work?.onPause();
    else setAsking('pause');
  };

  // The header's acts, in its order, each where it applies. While one asks under the header, its first press
  // is not offered twice. One list for its buttons and the page's right-click, so the two never disagree.
  // A closure graph's quest: what its pill says, whether its sealed submission may be reopened here, and
  // whether its source's reopen holds it, which no take, yes or done of the agent's moves.
  const reads = shownStatus(quest);
  const reopenHeld = quest.held === true && quest.hold === 'source-reopened';
  const reopenable = reopen !== undefined && reads === 'Submitted' && quest.held !== true;
  const goesOn = reopenHeld && quest.status === 'Taken' && goOn?.ready === true;
  // A held step this machine's take holds offers its go-on and abandon as a lost take's two choices, by
  // their names; held by another machine, or where no host says which, each keeps its own.
  const holdsHere = quest.here != null && quest.holder === quest.here;
  const heldMine = reopenHeld && holdsHere;
  const labelOf = (act: QuestAct) => (act === 'goOn' && heldMine ? 'quests.conflict.forward' : QUEST_ACT[act].label);
  // This machine's take lost and the step is not its own: the person's done here is refused as its session's would
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
    // A closed quest's two clears, in its ⋯.
    ...(clearable && asking !== 'clear' ? ['clear' as const] : []),
    ...(failedClearable && asking !== 'clearFailed' ? ['clearFailed' as const] : []),
  ];
  // What its body offers (the sitting line's Resume, Try again and trust, its session's door), which the right-click
  // offers after the header's.
  const ownResume = (ownPause || pausedUnseen) && work !== undefined;
  const retryable = (because?.verdict === 'Exhausted' || because?.verdict === 'Stopped') && onRetry !== undefined;
  const trustable = Boolean(hold && onGrant && onTrusting && !trusting);
  // A departure that holds it for the person's yes: what it waits on, so the yes leads its body's acts. A set-up
  // step its review holds waits for the review's verdict instead, which no yes lifts.
  // A step its source's reopen holds waits on the person's go-on or abandon, which the accept door would refuse.
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

  // The head's acts: its next step loud (*Take* while open, *Mark done*
  // once taken) and *Decline…* beside it; the rest of the same list, and its id, in its ⋯. One list still, so the buttons,
  // the ⋯ and the right-click never disagree.
  const lead: QuestAct | null = headActs.includes('take') ? 'take' : quest.status === 'Taken' && headActs.includes('done') ? 'done' : null;
  // The primary holds still in `acts`; *Decline…* is the fold's, shown above 640 px of the main area, and said
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

  // Titles first: its state leads, its name follows on two lines at most, and one line
  // of facts says whom it asks, who asked, when, and the one live fact. The state line, the id and the fact rows went: the
  // state's sentence is its pill's tip, and the id, the full times and the lanes are its *Details*.
  const named = questName(quest);
  // The body, its first line dropped where it is the title the head already said (as the ask page drops
  // it); where the head said a short title, the body opens on the whole title, once.
  const body = bodyUnderHead(quest.title, quest.body, named !== quest.title);
  const request = useRef<HTMLParagraphElement>(null);
  // *next* is the first body act that moves it on, or its session where that waits on the person.
  const next = questNext(bodyActs, session?.state === 'awaiting-person');
  const head = (
    <PageHead
      title={named}
      lead={<Pill tone={QUEST_TONE[quest.status]} title={t(`statusHint.${reads}`)}>{t(`status.${reads}`)}</Pill>}
      clamp={2}
      wholeElsewhere={!!body && named.length > LONG_TITLE}
      facts={questFacts(t, { quest, session, sitting, flow: true })}
      // *View full request* (as the ask's): a long title is read whole, as written, in the request below, where
      // the focus goes; on the facts line, since the head has no door slot. Where the body holds nothing more, nothing to go to.
      fact={!next && !(workflow && workflow.runs.length > 0) && !(body && named.length > LONG_TITLE) ? undefined : (
        <>
          {next && <NextDoor press={t(QUEST_ACT[next].label)} onPress={() => press(next)} />}
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
      <QuestAsks asking={asking} work={work} pauseLines={pauseLines} target={target} t={t} waiting={waiting} setAsking={setAsking} listed={listed} setListed={setListed} history={history} clearTarget={clearTarget} deleting={deleting} deletable={deletable} busy={busy} onDelete={onDelete} moving={moving} requirements={requirements} lostHere={lostHere} quest={quest} onRespond={onRespond} doneNote={doneNote} setDoneNote={setDoneNote} reopenable={reopenable} reopen={reopen} reopenNote={reopenNote} setReopenNote={setReopenNote} declining={declining} reason={reason} setReason={setReason} />

      {/* What its head leaves out: its id with *Copy*, whom it asks and who asked, its lanes, and the full
          times. Folded, since these are looked up rather than read. */}
      <QuestFacts quest={quest} t={t} lanes={lanes} question={question} answered={answered} onOpenQuest={onOpenQuest} because={because} hold={hold} pausedUnseen={pausedUnseen} ownPause={ownPause} work={work} waiting={waiting} press={press} pausedBy={pausedBy} onOpenAsk={onOpenAsk} onGrant={onGrant} onTrusting={onTrusting} trusting={trusting} onRetry={onRetry} retrying={retrying} endedHere={endedHere} session={session} asking={asking} reads={reads} reopenable={reopenable} ends={ends} abandoned={abandoned} />

      {abandoned && (
        <div className="mb-4">
          <AbandonedWork target={target} outcome={abandoned.outcome} went={t('quests.detail.went')} stayed={t('quests.detail.stayed')} />
        </div>
      )}

      {!work && moving && (
        /* A browser has no driver: none of the three is offered, and the terminal's commands are named. */
        <Prose className="mb-4 text-small"><Inline text={t('quests.detail.noDriver', { id: quest.id })} /></Prose>
      )}

      {hold && onGrant && trusting && (
        <div className="mb-4">
          <TrustAsk hold={hold} busy={granting} onCancel={onTrusting && (() => onTrusting(false))} onGrant={(answered) => onGrant(hold, answered)} />
        </div>
      )}

      {reopenHeld && (
        /* A step its source's reopen holds: what it waits on, so above the body.
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
              {/* Abandoning it is the person's decline: this machine's abandon where its driver offers one,
                  which stops its work too, and the decline's own ask otherwise. */}
              <Button disabled={waiting} onClick={() => press(offers.abandon ? 'abandon' : 'decline')}>
                {t(heldMine ? 'quests.reopened.drop' : QUEST_ACT.abandon.label)}
              </Button>
            </span>
          )}
          {/* A decline closes the step for every machine, where a lost take's dismissal moves only its conflict. */}
          {moving && <p className="m-0 mt-2 text-small text-ink-soft">{t('quests.reopened.declines')}</p>}
        </section>
      )}

      {held && requirements.length > 0 && (
        /* A departure that holds the quest for the person's yes: what it waits on, so above the body,
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
        /* A set-up step's review: what it waits on, so above the body, as a held departure sits; every set-up
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

      {/* A move that reached the remote second: kept on the quest for a person and never merged, so it sits above the body,
          as what this quest waits on; this machine's own lost take with its two choices. */}
      <QuestConflicts className="mb-4" quest={quest} busy={dismissing || forwarding} onDismiss={onDismiss} onForward={onForward} />

      {/* The ask itself, as the asker wrote it: content, wrapping at the column's edge with the title above it (a
          65ch body beside a title that runs the pane leaves 750 px empty). Its first line is left out where
          the head already said it. */}
      <QuestContent body={body} request={request} held={held} requirements={requirements} quest={quest} t={t} />
      <SettledConflicts quest={quest} />

      <QuestRecord chain={chain} onOpenQuest={onOpenQuest} onAttend={onAttend} nameOf={nameOf} quest={quest} moves={moves} session={session} t={t} shown={shown} waitsOn={waitsOn} limitWait={limitWait} press={press} trace={trace} onOpenAsk={onOpenAsk} />
    </ViewMain>
  );
}

/**
 * Quests' main area with no record to show: **nothing chosen** says how to choose and offers the
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

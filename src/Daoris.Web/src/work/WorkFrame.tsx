import { type ComponentProps, type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SessionConsole } from './console/SessionConsole';
import { sentence } from '../locales/format';
import { buildChain } from '../map/chain';
import type { Compose, OpenGroup } from '../app/opener';
import { useAsks } from '../data/queries';
import {
  useSessionStreams, useStopTask, useTreeFile, useReviewedPatch,
  useTerminals, useRemotes, useWorkPlan, useLanding, useLandSessionTree, useSessionDiff,
  useDiscardSessionTree, useDriver, useOpinionGate, useWorkflowRun, useRules, useSessionEvents, useUnderWay,
  useEntryLanding, useLandEntry, useSessionHelpers, useSessionPartners, useUnderWaySubject, usePushedCalls, withCall,
  useAnswerPermission, usePermissionWaits, useSessionHandOverAsk, useApproveHandOver, useDeclineHandOver, type HandOverWord,
  useAnswerWaits, useAnswerAlone, useRunNodeWord, useResolveHeld, useStopWaiting,
  useConsidered, useAttention, useGitAnswer,
} from '../bridge/shell';
import { useAttendedSession } from './head/useAttendedSession';
import { useAttendedRecord } from './head/useAttendedRecord';
import { useAttendedBox } from './conversation/useAttendedBox';
import { type Refusal, useWords } from './conversation/useWords';
import { useBoxWords } from './conversation/useBoxWords';
import { handOverRefused, type HandOverAskWait } from './waits/HandOverAgentAsk';
import { permissionSaid, type PermissionWaits } from './waits/PermissionAsks';
import { answerAloneSaid, type AnswerWaits } from './waits/AnswerAlone';
import type { GitAskWaits } from './waits/GitAsk';
import { type Sent, useSending, useSettled } from './conversation/sending';
import { WorkflowRunView } from '../workflow/WorkflowRunView';
import { AtWorkView } from './atWork/AtWorkView';
import { useSessionAtWork } from '../bridge/sessions/atWork';
import { opinionAsked, opinionHolds } from './gates/opinion';
import { useOpinionActs } from './gates/opinionActs';
import { OpinionGate } from './gates/OpinionGate';
import { reviewHolds, reviewState } from './gates/review';
import { ReviewGate } from './gates/ReviewGate';
import { useReviewActs } from './gates/reviewActs';
import type { Session } from '../data/api';
import type { HeadLink, HeadOpinion, HeadReview, WorkAsk } from './head/SessionHead';
import type { EntryAccept } from './review/EntryAccept';
import { type WaitProps, WaitStrip } from './waits/WaitStrip';
import { WaitPanel } from './waits/WaitPanel';
import { useWaitPanelHeight } from './waits/waitPanelHeight';
import { type AnchorWait, anchorOf, waitEvents } from './waits/anchors';
import type { RuleAsk } from './waits/ProposedRules';
import { useRuleAnswer } from './waits/ruleAnswer';
import { conversationOffer, rulesAsked, waitsOf } from './waits/waits';
import { useWaitsOnScreen } from './waits/waitsOnScreen';
import { sayDiscard } from '../settings/Sweep';
import { toolOf } from '../agents/tools';
import { agentOf, machineScope } from '../settings/accounts/accounts';
import { namer } from '../settings/accounts/namer';
import { accountNames } from './accountNames';
import { type ChatLimitHeld, chatLimitWaitSaid, limitedLine, limitWaitOf, limitWaitSaid } from './waits/limitWait';
import { LimitWaitLine } from './waits/LimitWaitLine';
import { coolingFor, takesWords } from './say';
import { SessionBox } from './conversation/SessionBox';
import { pauseAsk, wiredFor, type WorkTarget } from './pausing/pausing';
import { lastWords } from './channel';
import { openGoAheads } from './waits/parkQuestion';
import { PauseAsk } from './pausing/WorkAsks';
import { FilePreview } from './review/FilePreview';
import { FileOpener, fileName } from './review/preview';
import { TerminalView } from './terminals/TerminalView';
import { failure, type Notify, SESSION_ACTIVE } from '../design/ui';
import type { FrameIntent } from '../menus/commands';
import { AttendedSession, noteIsInTheHead } from './AttendedSession';
import { type AnchorState, BackToBottom, SessionConversation, type TailState, ToTheTop } from './conversation/SessionConversation';
import { type EarlierPart, EarlierParts } from './conversation/EarlierParts';
import { ancestryOf } from './conversation/line';
import { useAwayFromTop } from './conversation/followTail';
import { useNow } from './useNow';
import { useActors, useHeavySlot } from '../bridge/driver';
import { heavyPartOf } from './heavySlot';
import { heavyLine, heavyRow } from './HeavySlotLine';
import { askName, linkedSessions, ownTree, sessionTitle } from './identity';
import { doorLabel } from '../agents/tools';
import { Composer } from './conversation/Composer';
import { DiffPane } from './review/DiffPane';
import { NothingFollowed, RightDock } from './frame/RightDock';
import { SessionTimeline } from './head/SessionTimeline';
import { SessionRail } from './rail/SessionRail';
import { type ActFacts, actMenu, headerActs, offeredActs, primaryAct, stopAsk } from './acts';
import { askOfEntry, entryOfSession, isIntakeEntry, nameOfEntry } from './entries';
import { useAccountUse } from '../bridge/shell';
import { keptSessionFilters, sessionFilters, shownOf } from './groups';
import { leadingAgentOf, nextPress, type SessionFactsOf, sessionFacts, sessionFactsTip } from './head/headFacts';
import type { Answered } from './InlineConfirm';
import type { AgentChoice } from './start/AgentChip';
import { HandOverAsk, type HandOverRoster } from './start/HandOverAsk';
import { AskHelper } from './start/AskHelper';
import { partnersOf } from './pieces/pieceRows';
import type { PieceSection } from './pieces/Pieces';
import { handOverOffer, nextChoices } from './start/handOverOffer';
import { HandOverOfferLine } from './start/HandOverOfferLine';
import { DeleteAsk, SessionPageHead, StopAsk } from './head/SessionPageHead';
import { type SessionDoors, useSessionActs } from './sessionActs';
import { ListMore } from './frame/ListPane';
import { useNewStart } from './start/useNewStart';
import { StartPane } from './start/StartPane';
import { OutputPanel, PANEL_MIN, StreamTabs } from './frame/frame';
import { panelTabs } from './console/streams';
import { dockRange, type ListMode } from './frame/layout';
import type { FrameClosings } from './frame/closings';
import type { ListPanes } from './frame/listPanes';
import { type ListSpec, type ViewLayout, ViewListPane } from './frame/ViewFrame';
import { ViewMain } from './frame/ViewMain';
import type { Place, Placements, ViewId } from './frame/placements';
import { relationsOf } from './head/relations';
import { beganLine } from './head/partsLife';
import { cn } from '../lib/cn';
import { useFrameLayout } from './frame/useFrameLayout';
import { useDockViews } from './frame/useDockViews';

/** Whether a door reports context, from the roster's word on it — undefined until the roster says. */
const door = (structured?: boolean): 'structured' | 'text' | undefined =>
  structured === true ? 'structured' : structured === false ? 'text' : undefined;

/**
 * Settles the person's words whose send the page stopped waiting on (SENDLOST1b) by the attended session's record and queue.
 * Mounted only while some wait, so the record is read a second time, beside its conversation's own reading, only then.
 */
function SentSettler({ sending, giveBack }: { sending: ReturnType<typeof useSending>; giveBack: (sent: Sent) => void }) {
  useSettled(sending, useSessionEvents(sending.lateTo), giveBack);
  return null;
}

/**
 * The **Work frame** (D55): the rail, the attended session, the composer and the output panel.
 *
 * @remarks
 * **One selection binds every region** (IDE study §3), which is why it is held here and passed
 * down rather than kept by the rail. It is also why the composer and the panel need no id of their
 * own: they are looking at whatever the person is.
 *
 * **It is a frame, not a view** — it fills the window rather than sitting in the management
 * shell's content column, and the mode switch and the status bar that come with D55 belong to the
 * application, because both frames want them.
 *
 * **Desktop-only, structurally.** Over a keyed remote none of this is rendered: the stream and a
 * tree path are machine-local (D47 §4), and a frame whose centre is a stream has nothing honest to
 * show a browser. `App` gates on the same "is a shell here" answer every control uses.
 *
 * **The centre is the record, then the conversation** (D76): the head and the session's
 * conversation scroll together, and the conversation follows its tail. The right dock holds the
 * timeline and the review (components plan §3a); the console is the panel below, the
 * conversation's raw view.
 *
 * **Every view hands it a `ViewLayout`** (D118 §5): its list pane where it has one, and its main area. It
 * draws the list for every view that hands one, and Sessions' rail is one such list, built here because
 * the attended session is this frame's to hold.
 */
export function WorkFrame({
  selected, onSelect, notify, onSendBack, onAnswerAsk, onOpenQuest, intent, onIntentTaken, ask, askFocus = 0, closings, placements,
  lists, layout: viewLayout, onOpenSessions, terminal = false, onListMode, group = null, onGroupBrought, compose = null, onMakeTask,
  onOpenBranch, notifications, notificationsShow = 0, notificationsToggle = 0,
}: {
  /**
   * *Notifications* (NOTIFY2b, the notification design §2.1): everything that comes to the person, a view of the regions on the
   * right by default, as Ask Daoris is. Absent where no shell is attached.
   */
  notifications?: ReactNode;
  /** Bumped each time a door asks for *Notifications* (View, the palette, Overview's summary, a toast's *Open*): it is shown. */
  notificationsShow?: number;
  /**
   * Bumped by each press of the title bar's bell (§2.1): it shows the view where it stands, or, pressed while the view is shown,
   * gives the region back to the view it held, or closes the region where it was closed.
   */
  notificationsToggle?: number;
  /**
   * What each view's list remembers (D118 §3f), held by the application so the list's doors reach it from
   * every view; this frame's own where it is rendered alone.
   */
  lists?: ListPanes;
  /**
   * The group of the view's list a door into what waits on the person named (ENTRY1b): Sessions' is handed to the rail, which
   * brings it into view and tells `onGroupBrought`, and shown by state for it; another view's is its own to bring. Either
   * way the frame lays the list over the main area for it where the list is a strip (ENTRY1g).
   */
  group?: OpenGroup | null;
  onGroupBrought?: () => void;
  /**
   * What the view's list is now — open, a strip, laid over, or none (D118 §3a) — for the application,
   * whose doors toggle it and say whether it is shown. The room decides it, and only this frame measures.
   */
  onListMode?: (mode: ListMode | null) => void;
  /**
   * The person's own terminal as a view of the regions (CONSOLE4b, D96): only where a shell is attached,
   * as Ask Daoris is, since its shells are this machine's and ride the bridge alone (D47 §4).
   */
  terminal?: boolean;
  /** Go to Sessions, where the attended session is read whole — the door its line offers off Sessions. */
  onOpenSessions?: () => void;
  /**
   * Another view in the frame (DOCK1a, D118 §5): its list pane where it has one, and its main area. Overview,
   * Quests and the rest keep the right side bar and the panel, with every view that stands in them, as VS
   * Code's workbench is one frame whatever its editor shows. Absent, the view is Sessions: the rail and the
   * attended session.
   */
  layout?: ViewLayout;
  /**
   * Open a quest's record in Quests (SESS1): a stop on the chain, or a quest the session asked. The
   * record is the application's to open, as an ask's is.
   */
  onOpenQuest?: (quest: string) => void;
  /**
   * Open a landed branch's page in Repositories, with how it landed (FLOWCHART1c): the work's milestone and a run's landing step
   * name the branch. The page is the application's to open, as a quest's is.
   */
  onOpenBranch?: (repository: string, branch: string) => void;
  /**
   * The attended session, held by the application — because a door into Work from somewhere else
   * (a quest's record) has to be able to say WHICH session, and a selection this frame kept to
   * itself could not be told.
   */
  selected: string | null;
  onSelect: (id: string | null) => void;
  notify: Notify;
  /**
   * Send the reviewed work back as a quest (SURF6b) — a door into the platform's own composer, not
   * a second publish path. It is the one review move Daoris has that an editor does not, and it
   * goes through the channel `repository-owns-its-work` sanctions rather than around it.
   */
  onSendBack?: (repository: string) => void;
  /**
   * Open the ask a parked intake is waiting on (INT4g). Its answer is there — publish or close —
   * and the ask's record is the application's to open, in Quests, the way the attention band does.
   */
  onAnswerAsk?: (ask: string) => void;
  /**
   * What a menu, the palette or a key asked for (SURF9, UX7a) — an event, consumed on arrival, because leaving it
   * set would reopen the drawer every time anything here re-rendered: a start, a review, the attended session's
   * answer, a region's view, a new terminal, or *Archive what ended…*.
   */
  intent?: FrameIntent | null;
  onIntentTaken?: () => void;
  /**
   * Ask Daoris's go to New conversation (NEW1d-b, D162): the repository to choose and the words to put in the composer, each
   * only where the go named one. An event like `intent`, consumed once by its `id` and never by "if it is set" (FIX-LOG
   * 2026-09-23): the attended session is put down, the prompt owed the focus, the words put above any already typed.
   */
  compose?: (Compose & { id: number }) | null;
  /** *Make it a task with Daoris…* in the composer: its draft, to Ask Daoris as a draft the person sends (NEW1d-b). */
  onMakeTask?: (text: string) => void;
  /**
   * Ask Daoris, as a tab of the right dock: one right region, as VS Code's chat is a view of its secondary side bar, never a
   * second column beside the dock. Absent where no shell is attached.
   */
  ask?: ReactNode;
  /** Bumped each time the person asks for Ask Daoris (its door, `F1`), which opens the dock on it. */
  askFocus?: number;
  /** What the person closed, held by the application so the strip reaches it (DOCK1c). */
  closings?: FrameClosings;
  /** Where each view stands, held by the application so the View menu's reset reaches it (DOCK1b). */
  placements?: Placements;
}) {
  const { t, i18n } = useTranslation();
  // A refusal belongs to the session that gave it: attending another by any door (a notification, the
  // palette, a quest's record) must not show one session's "went nowhere" on another's composer (REV3).
  const [refusal, setRefusal] = useState<Refusal | null>(null);
  const attend = (id: string) => {
    onSelect(id);
    setRefusal(null);
  };
  // *Archive what ended…* pressed in the list's ⋯ (SESSUX1e): its first press stands under the list's header until it
  // archives or the person never minds. Not remembered: it is a question asked now.
  const [archivingEnded, setArchivingEnded] = useState(false);
  // The session whose stop asks under the header (SESSUX1d, D126 §3.3), from the header's *Stop…* or its row's: a
  // question asked now, of that session only, so attending another closes it.
  const [stopAsking, setStopAsking] = useState<string | null>(null);
  // The session whose delete asks under the header (SESSUX1f, D126 §5.4), the stop's way: of that session only.
  const [deleteAsking, setDeleteAsking] = useState<string | null>(null);
  // The session whose hand-over asks under the header (MIXAGENT1b-i, D163), the stop's way: of that session only.
  const [handOverAsking, setHandOverAsking] = useState<string | null>(null);
  // The session whose helper's ask opens under the header (MIXAGENT2b, D163), the stop's way: of that session only.
  const [helperAsking, setHelperAsking] = useState<string | null>(null);
  // The agent and account Daoris's offer named, and why, when *Choose…* opened that ask (MIXAGENT1b-ii): the ask opens with
  // the choice made and its press carries the reason. Cleared by the head's own door, whose press is the person's.
  const [handOverChosen, setHandOverChosen] = useState<{ why: 'near' | 'limit'; choice: AgentChoice } | null>(null);
  // The conversations whose offer to hand over the person put down: for this run of the frame, never stored (MIXAGENT1b-ii).
  const [offerDismissed, setOfferDismissed] = useState<ReadonlySet<string>>(() => new Set());
  // LIMITRESUME1d: what *Try now* on a waiting line came to where it was refused, by the session it was pressed on.
  const [limitRefusal, setLimitRefusal] = useState<{ session: string; sentence: string } | null>(null);
  const accountUse = useAccountUse();
  // LIMITRESUME1d2: a conversation's limit wait, as its events hold it, told by the conversation that reads them.
  const [chatHeld, setChatHeld] = useState<{ session: string; held: ChatLimitHeld } | null>(null);
  const stopWaiting = useStopWaiting();
  // The session whose pause asks under the header (PAUSE1e, D132 §2.6), with the work it pauses: of that session only.
  const [pauseAsking, setPauseAsking] = useState<{ session: string; target: WorkTarget } | null>(null);
  // *Answer…* from a row (D126 §3.1): the session it was pressed for, and a count, so the box at its foot takes the focus
  // once per press, including a second press on the same session.
  const [answerFocus, setAnswerFocus] = useState<{ session: string; at: number } | null>(null);
  // The centre scrolls the head and the conversation together; the conversation follows its tail.
  const centre = useRef<HTMLElement>(null);
  const {
    root, width, listed, closed, placed, collapsed, dockClosed, dockFull, setDockFull, go, layout, list, chosen, resizeDock, closeDock,
    height, resize, toggle,
  } = useFrameLayout({ lists, closings, placements, viewLayout, group, onGroupBrought, onListMode });
  // Another view in the frame (DOCK1a): its own list or none, and its main area. Absent, it is Sessions.
  const elsewhere = viewLayout !== undefined;

  const {
    sessions, sessionsResyncing, openings, groups, entries, where, quests, registry, harnesses, accounts, attended, attendedEntry,
    channelAsk, quest, live, conversation, here, intake, oneTurn,
  } = useAttendedSession({ selected, onSelect, notify });
  const start = useNewStart({ t, notify, attended, selected, onSelect, compose, attend, setRefusal });
  const { newConversationPressed } = start;

  // Which workspaces sync with a remote here: a pause's ask names an open quest another machine may still take (PAUSE1e).
  const wiring = useRemotes().data;

  const {
    spent, related, relatedOwn, together, bringTogether, togetherAct, memoryRows, decideMemory, sessionRecord, channel, channelOpened,
    setChannelOpened, postChannel, forwardChannel, branch, discardBranch, told, tracing, setTracing, trace, milestone, integration,
  } = useAttendedRecord({ attended, attendedEntry, channelAsk, where, here, intake });

  // What the session runs beside itself, a tab each in the panel (CONSOLE2c). The panel shows the
  // session's own console unless the person picked a stream of it, and one it no longer lists falls
  // back to the session rather than to an empty well.
  const streams = useSessionStreams(attended?.id ?? null);
  const [picked, setPicked] = useState<string | null>(null);
  useEffect(() => setPicked(null), [attended?.id]);
  const shown = attended
    ? (picked && streams.some((row) => row.key === picked) ? picked : attended.id)
    : null;

  // The person's own terminals (CONSOLE4b), held here rather than by their view, which unmounts whenever
  // it moves or another view of its region is shown: a shell outlives that, as VS Code's terminal does.
  const terminals = useTerminals();

  // The ask its entry came from, for the head's name: the rail's read, so it costs nothing it does not already ask.
  const entryAsks = useAsks(true, channelAsk !== null);
  const {
    editQueued, parkAsk, goAheads, question, talking, draft, setDraft, chatTurns, turns, held, box, stands, goes, levels,
    keepingStop, answering, taking, lastTurns, offered, setOption, setMentioning, treeFiles, reading, onUsage, waiting, onWaiting,
  } = useAttendedBox({
    t, attended, here, intake, oneTurn, conversation, live, quest, sessions: sessions.data, attendedEntry, groups: groups.data,
  });
  const words = useWords({
    t, notify, attended, here, oneTurn, conversation, live, box, chatTurns, draft, setDraft, setRefusal, sessions, attendedEntry, stands,
    attend,
  });
  const {
    newSession, send, end, cancelTurn, onMoveWord, onDraftToHead, sending, giveBackSent, onSend, say, onSayAgain, onStartFrom,
    onGoOnBrief, goOnNew, onGoOnNew, onKeepingStop, onStopTurn,
  } = words;
  const roster = Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : [];
  const {
    resolve, personDone, parkGoAhead, parkPicked, onParkPick, ending, boxAccepts, onSay, onResolve, answerFromCard, choiceFromCard,
    onGoAhead, onBoxGoAhead,
  } = useBoxWords({ t, notify, attended, box, answering, question, goAheads, parkAsk, draft, setDraft, setRefusal, roster, words });
  // A stop's ask is a question asked now, of the session it was asked of (D126 §3.3): attending another by any door
  // puts it down, so coming back later does not find it still open.
  useEffect(() => {
    setStopAsking((asked) => (asked === selected ? asked : null));
    setDeleteAsking((asked) => (asked === selected ? asked : null));
    setHandOverAsking((asked) => (asked === selected ? asked : null));
    setHelperAsking((asked) => (asked === selected ? asked : null));
    setPauseAsking((asked) => (asked?.session === selected ? asked : null));
  }, [selected]);

  // The rules agents proposed (PERM2, D74), the band's own read, and their answer's one owner: the attended session's own open
  // ones are a wait where the person talks to it (PROPOSEHERE1b, D168 point 2). The machine's file, so a browser has none.
  const ruleState = useRules();
  const ruleAnswer = useRuleAnswer({ notify });

  // 🔴 Consumed by IDENTITY, the way the quest composer's opening draft is (FIX-LOG 2026-09-23):
  // "if (intent) set…" during render made React re-run this frame with the same prop on every pass,
  // so the palette's ask never landed. The parent is told from an effect, not during this render.
  const {
    setDocked, following, rightViews, panelViews, previewing, dock, setDock, setPanelPick, dragging, onDrag, panelView, openDock,
    openPreview, closePreview, openView, moveView,
  } = useDockViews({
    attended, elsewhere, here, ask, notifications, terminal, placed, closed, collapsed, dockClosed, closeDock, askFocus,
    notificationsShow, notificationsToggle,
  });
  // Where the followed session's work stands (WORKFLOW1c, the workflow design §7): asked only while the Workflow view shows,
  // in whichever region it stands, and again at every tick under the driver's key.
  const workflowShown = following !== null && (placed.places.workflow === 'panel'
    ? !collapsed && panelView === 'workflow'
    : !dockClosed && dock === 'workflow');
  const workflowRun = useWorkflowRun(following ? { session: following.id } : null, { enabled: workflowShown });
  // Who is at work on the followed session (COWORKVIEW1a), asked whatever its state and whether *At work* shows (COMMS1i): the head's
  // *Waiting on* is this read's line, and *working with it* (§3.7) its count, at its mount and each `atWork` wake.
  const atWork = useSessionAtWork(following?.id ?? null);
  const atWorkHeld = atWork.data;
  // Why an untaken quest sits is the driver's last look's, read beside the answer from memory (COWORKVIEW1b-ii).
  const considered = useConsidered().data;
  const withIt = atWorkHeld && 'answer' in atWorkHeld
    ? atWorkHeld.answer.subagents.working + atWorkHeld.answer.parts.filter((part) => part.key !== atWorkHeld.answer.attended && part.state === 'working').length
    : 0;
  // FABRIC-CUTOVER1f: the person's skip or stop of a graph run's node, pressed under the node in the view, kept for the driver's next look.
  const runNodeWord = useRunNodeWord();
  // FABRIC-PIPELINE1d: *Run again* and *Not now* under a check held unknown, the person's word on the held attempt.
  const resolveHeld = useResolveHeld();
  const previewFile = useTreeFile(previewing ? following!.id : null, previewing?.path ?? null);
  const previewPatch = useReviewedPatch(previewing ? following!.id : null, previewing?.path ?? null);

  const [taken, setTaken] = useState<typeof intent>(null);
  if (intent && intent !== taken) {
    setTaken(intent);
    if (intent === 'start') newConversationPressed();
    else if (intent === 'review') openView('review');
    else if (intent === 'workflow') openView('workflow');
    else if (intent === 'atWork') openView('atWork');
  }
  if (!intent && taken) setTaken(null);

  // The console and its streams (CONSOLE2c), for whichever region it stands in. A session with nothing
  // held here says so as a sentence, not as an empty bordered well, which read as a field on the
  // installed window.
  const consoleView = following && shown
    ? <SessionConsole id={shown} fill quiet={t(shown === following.id ? 'work.panel.silent' : 'work.panel.streamSilent')} />
    : null;
  const streamTabs = following ? panelTabs(following.id, streams) : undefined;
  const pickStream = (key: string) => setPicked(key === attended?.id ? null : key);
  // A task stopped from its tab (CONSOLE3a): asked of the session that runs it; its tab changes when the
  // wire says how it ended. What it was called is the tab's, for the notice.
  const stopTask = useStopTask();
  const stopStream = (key: string) => {
    if (!attended) return;
    const name = streams.find((row) => row.key === key)?.name ?? key;
    stopTask.mutate({ id: attended.id, key }, {
      onSuccess: ({ stopped }) => notify(t(stopped ? 'work.panel.stream.stopping' : 'work.panel.stream.notStopped', { name }),
        stopped ? undefined : 'error'),
      onError: (error) => notify(sentence(error), 'error'),
    });
  };

  // Off Sessions (DOCK1a) the session views speak for the session attended there, which nothing else on
  // the screen names — so each says whose it is, with the way back to it (found looking at Overview).
  const whose = (flush: boolean) => (elsewhere && following ? (
    // Flush in the panel, whose body carries its own gutter; inset in the side bar, as its views are.
    <p className={cn('m-0 flex min-w-0 shrink-0 items-center gap-2 border-b border-line py-1.5 text-meta text-ink-faint', flush ? 'mb-2' : 'px-3')}>
      <span className="min-w-0 truncate">
        {t('work.frame.attending', { title: sessionTitle(following, quest, openings[following.id]) })}
      </span>
      {onOpenSessions && (
        <button
          type="button"
          onClick={onOpenSessions}
          className="shrink-0 cursor-pointer border-0 bg-transparent p-0 text-meta text-ink-soft underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
        >
          {t('work.frame.open')}
        </button>
      )}
    </p>
  ) : null);
  const withWhose = (surface: ReactNode, flush = false) => (elsewhere && following ? <>{whose(flush)}{surface}</> : surface);

  // Where a terminal opened here starts (D96): the attended session's tree (a record with none works in
  // its repository's root), else the first repository of the workspace in scope that has a checkout here.
  // Said by the page, which knows what is attended and what the viewer is scoped to; with nothing to say,
  // the module opens it in the home, and it opens there too when this folder is not on the machine.
  const rootOf = (repository: string) => (registry.data ?? []).find((row) => row.repository === repository)?.root;
  const terminalCwd = (attended && here ? attended.tree ?? rootOf(attended.repository) : undefined)
    ?? [...(registry.data ?? [])].filter((row) => row.root).sort((a, b) => a.repository.localeCompare(b.repository))[0]?.root
    ?? undefined;

  // *Send back…* in a review (SURF6; MSG1f, D137 §5.1): the box on that session, with the focus in it, which is what the
  // glossary's send back means — its work returned to it with a note, so it carries on. Off Sessions, Sessions is opened,
  // where the box is. Where the session takes no words, the door to the quest composer stays (SURF6b).
  const sendBack = () => {
    if (!attended) return;
    if (!takesWords(box)) {
      onSendBack?.(attended.repository);
      return;
    }
    if (elsewhere) onOpenSessions?.();
    setAnswerFocus((was) => ({ session: attended.id, at: (was?.at ?? 0) + 1 }));
  };

  /** A view's surface, drawn in whichever region it stands (DOCK1b). The panel draws the console itself. */
  const surface = (view: ViewId, where: Place = 'panel') => {
    if (view === 'ask') return ask;
    if (view === 'notifications') return notifications;
    if (view === 'terminal') return <TerminalView terminals={terminals} cwd={terminalCwd} />;
    if (view === 'review') {
      // The line of work the reviewed part belongs to, and its ask: either's one Accept holds this one's while it lands (ACCEPTALL1h).
      const followingEntry = following ? entryOfSession(entries, following.id) : null;
      return withWhose(
        <DiffPane
          // The session's one press, where the part reviewed is the attended one (REVIEWFLOW1a, D168).
          entryPress={following && following.id === attended?.id ? entryAt('review') ?? null : null}
          entry={followingEntry?.entry ?? null}
          ask={followingEntry && !isIntakeEntry(followingEntry) ? askOfEntry(followingEntry) : null}
          // The followed session's (UX6b): off Sessions an ended one is reviewed on Sessions, where it is attended.
          session={following?.id ?? null}
          // What its frame says while git reads (REVIEW4); off Sessions the line above already names the session.
          record={following}
          title={following && !elsewhere ? sessionTitle(following, quest, openings[following.id]) : null}
          // A tree of its OWN: the repository's checkout is never merged or discarded (UX5 U66).
          hasTree={Boolean(following && ownTree(following, (registry.data ?? []).find((row) => row.repository === following.repository)?.root))}
          onSendBack={following && (takesWords(box) || onSendBack) ? sendBack : undefined}
          onPreview={following && here ? (path) => openPreview({ path }) : undefined}
          // SQUASHTIDY1f: work the line holds by content offers the head's discard in Accept's place, through the head's press.
          discards={following && following.id === attended?.id ? discards : null}
          onDiscardTree={following && following.id === attended?.id ? discardHeld : undefined}
        />,
      );
    }
    if (view === 'workflow') {
      // Where the followed session's work stands in its workflow (WORKFLOW1c, design §7). Its look's presses are the review's
      // own gate, drawn under the step where it waits, never a second implementation; every other step opens where its record is.
      const lookControl = following && following.id === attended?.id && gateHolds && headReview ? (
        <ReviewGate
          environment={gateHolds.environment ?? ''}
          state={reviewState(gateHolds.state)}
          step={headReview.step ?? null}
          stepId={gateHolds.quest ?? null}
          served={headReview.served ?? null}
          said={gateHolds.says ?? null}
          chat={Boolean(gateHolds.chatRun)}
          acts={headReview.acts}
          busy={headReview.busy}
        />
      ) : null;
      // Its second opinion's own gate under its step while the gate holds the work (XAGENT1g, WORKFLOW1c3's remainder): *Go on
      // anyway…* and every other press through the gate's one owner, never a second implementation. No *Accept…* is drawn beside
      // it here, so *Go on anyway…* is offered where the session's page lists the dispute inside *Accept…*.
      const opinionHeld = following && following.id === attended?.id && headOpinion ? opinionHolds(landing.data) : null;
      const opinionControl = opinionHeld && headOpinion && attended ? (
        <OpinionGate
          gate={opinionHeld}
          detail={headOpinion.detail}
          acts={headOpinion.acts}
          busy={headOpinion.busy}
          working={attended.id}
          onOpenFile={headOpinion.onOpenFile}
        />
      ) : null;
      const cells = (Array.isArray(workflowRun.data?.runs) ? workflowRun.data.runs : []).flatMap((run) => run.steps);
      const controls = Object.fromEntries(cells.flatMap(({ step }) => {
        const control = step.kind === 'look' ? lookControl : step.kind === 'opinion' ? opinionControl : null;
        return control ? [[step.id, control]] : [];
      }));
      const ownKind = following && following.kind !== 'chat' && following.quest ? quest?.setUpIn ? 'look' : 'work' : null;
      const ownStep = cells.find(({ step }) => step.kind === ownKind)?.step.id ?? null;
      return following
        ? withWhose(
          <div className="p-3">
            <WorkflowRunView
              // A shell older than the route answers something else, which is no run.
              answer={Array.isArray(workflowRun.data?.runs) ? workflowRun.data : null}
              reading={workflowRun.isPending}
              refusal={workflowRun.error ? sentence(workflowRun.error) : null}
              // A service that did not answer is read again at once, before the view's next tick (CHATFLOW1b).
              onRetry={() => { void workflowRun.refetch(); }}
              // The step the followed session is its own: a set-up step's session shows the work for the look.
              here={ownStep}
              doors={{
                session: (id) => { attend(id); if (elsewhere) onOpenSessions?.(); },
                ...(onOpenQuest ? { quest: onOpenQuest } : {}),
                ...(onAnswerAsk ? { ask: onAnswerAsk } : {}),
                review: (id) => { doors.review?.(id); if (elsewhere) onOpenSessions?.(); },
                ...(onOpenBranch ? { branch: onOpenBranch } : {}),
              }}
              controls={lookControl || opinionControl ? controls : undefined}
              onNodeWord={runNodeWord.mutateAsync}
              onHeld={resolveHeld.mutateAsync}
            />
          </div>,
        )
        : elsewhere
          ? <NothingFollowed ended={attended !== null} onOpenSessions={onOpenSessions} />
          : <p className="m-0 p-3 text-small text-ink-faint">{t('work.attended.none.body')}</p>;
    }
    if (view === 'atWork') {
      // Who is at work on the followed session (COWORKVIEW1a, the cowork view design §5): its parts and its subagents, from Daoris's
      // records; each part opens its page.
      return following
        ? withWhose(
          <AtWorkView
            held={atWork.data}
            error={atWork.error ? sentence(atWork.error) : null}
            onOpen={(id) => { attend(id); if (elsewhere) onOpenSessions?.(); }}
            onOpenQuest={onOpenQuest}
            considered={considered}
            // COMMS1i: its co-workers' agents as the person calls them, as the head's *Waiting on* names them.
            nameOf={(agent) => agentRoster.names?.[agent] ?? agent}
            // The newest three messages of the page's own channel read, the panel's (COWORKVIEW1c); none for a session with no ask.
            channel={attendedEntry && channelAsk && channel.available && channel.channel ? {
              words: lastWords(channel.channel, { entry: attendedEntry.entry, repository: attendedEntry.repository }),
              onOpen: () => {
                setChannelOpened((n) => n + 1);
                // Under 768 px the view replaces the conversation: leaving it shows the panel it opened.
                if (layout.dock.mode === 'full') closeDock(true);
              },
            } : undefined}
            onRetry={() => { void atWork.refetch(); }}
            // A child's console tab, while the window holds its stream (the harness's agent id ends the stream's key).
            streamOf={(child) => streams.find((row) => row.key.endsWith(`/${child}`))?.key}
            onOpenStream={(key) => { pickStream(key); openView('console'); }}
          />,
        )
        : elsewhere
          ? <NothingFollowed ended={attended !== null} onOpenSessions={onOpenSessions} />
          : <p className="m-0 p-3 text-small text-ink-faint">{t('work.attended.none.body')}</p>;
    }
    if (view === 'console' && where === 'right') {
      // In the side bar its streams go with it, above it, where the panel carries them in its header.
      return withWhose(
        <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
          {streamTabs && streamTabs.length > 1 && (
            <StreamTabs tabs={streamTabs} selected={shown ?? undefined} onSelect={pickStream} onStop={stopStream} />
          )}
          {consoleView ?? <p className="m-0 text-small text-ink-faint">{t('work.panel.none')}</p>}
        </div>,
      );
    }
    return following
      ? withWhose(
        <div className="p-3">
          <SessionTimeline
            session={following}
            quest={quest}
            // The word the session list shows it as, which the states' flow lights its state with (FLOWCHART1b).
            shown={shownOf(following, (groups.data ?? []).find((row) => row.session === following.id), taking[following.id])}
            // Off Sessions there is no head to carry the parked note, so the timeline keeps it.
            hideCurrentNote={!elsewhere && noteIsInTheHead(following, here)}
            titled={false}
          />
        </div>,
      )
      : elsewhere
        // Off Sessions there is no session list beside it to point at (audit SE11): it says nothing is
        // attended, or that the one attended has ended and is read on Sessions (UX6b), and offers the view.
        ? <NothingFollowed ended={attended !== null} onOpenSessions={onOpenSessions} />
        : <p className="m-0 p-3 text-small text-ink-faint">{t('work.attended.none.body')}</p>;
  };
  // The file previewed for the followed session (PREVIEW1): the host's answer, or its sentence, and the
  // review's patch for that path where the review already holds one.
  const previewSurface = previewing && following
    ? withWhose(
      <FilePreview
        key={`${following.id}:${previewing.path}`}
        path={previewing.path}
        file={previewFile.data ?? null}
        pending={previewFile.isPending && !previewFile.error}
        refusal={previewFile.error ? sentence(previewFile.error) : null}
        lines={previewing.lines}
        patch={previewPatch}
        onReload={() => { void previewFile.refetch(); }}
      />,
    )
    : null;
  useEffect(() => { if (intent) onIntentTaken?.(); }, [intent, onIntentTaken]);

  // Each actor's order (MODELROLE1e): the hand-over's offer and its ask draw from the session's actor's, empty in a browser.
  const actors = useActors();
  const spawning = roster.find((row) => row.harness === (harnesses.data?.adapter ?? ''));
  // What a person calls an account (ACCTNAME1, D152 §4.2): the roster's one namer, so the head's facts, its Details, its
  // chain and its trace say the name the agent's page leads its row with.
  const nameOf = namer(t, roster);
  // The agents a conversation may be started or handed over on, with their accounts: *New conversation*'s chips and the
  // hand-over's ask (MIXAGENT1b-i) choose from the one roster.
  const agentRoster: HandOverRoster = {
    harnesses: roster.filter((row) => row.present).map((row) => row.harness),
    labels: Object.fromEntries(roster.map((row) => [row.harness, doorLabel(t, row)])),
    names: Object.fromEntries(roster.map((row) => [row.harness, row.product || row.harness])),
    accounts: Object.fromEntries(roster.map((row) => [row.harness, Array.isArray(row.profiles) ? row.profiles : []])),
    // The set's order of the agent's accounts of Daoris's, every workspace's (POOL1c, D197), by the accounts' owner (AGT7),
    // from the files the frame already reads.
    orderOf: (harness) => {
      const door = roster.find((row) => row.harness === harness);
      const use = door ? agentOf(accounts.data, toolOf(door)) : null;
      return use ? machineScope(use).list : null;
    },
  };
  // The helpers the attended session asked for (MIXAGENT2b, D163): this machine's own files, so only for its own record. Its head
  // lists those still on their way, with the person's yes to an agent's ask that waits on it and a door to each helper.
  const helperAsks = useSessionHelpers(attended && here ? attended.id : null);
  // The pieces it asked other agents to write (COAGENT1b, D199): this machine's own files, so only for its own record. A driven session
  // working in a tree of its own leads them; a conversation's lead is COAGENT1c's, and a partner, a helper, a reviewer or an intake
  // leads none.
  const leads = Boolean(attended && here && attended.kind !== 'chat' && !attended.serves && !attended.opinion && !attended.ask && attended.tree);
  const partnerPieces = useSessionPartners(attended && here ? attended.id : null);
  // Its agent's own ask to hand it over (MIXAGENT1c, D163 point 3): this machine's own files, so only for its own record.
  const handOverAsk = useSessionHandOverAsk(attended && here ? attended.id : null);
  const approveHandOver = useApproveHandOver();
  const declineHandOver = useDeclineHandOver();
  // Words a cooling account holds (MSG1g2, D137 §2.2): a driven record of this machine's between runs, parked or ended, whose
  // own account cools. A running one hears words at its door, and a chat's go on with the next word said to it.
  const between = attended && here && attended.kind !== 'chat'
    && !(SESSION_ACTIVE.has(attended.state) && attended.state !== 'awaiting-person');
  const coolsUntil = between ? coolingFor(attended, roster, accounts.data, new Date()) : null;

  // What only this frame can do with a session (SESSUX1d, D126 §3.1), handed to the one owner of the acts at both doors,
  // the page header and the rows: each attends the session first, as a press on its row would.
  const doors: SessionDoors = {
    answer: (id) => {
      chosen();
      attend(id);
      setAnswerFocus((was) => ({ session: id, at: (was?.at ?? 0) + 1 }));
    },
    stop: (id) => {
      chosen();
      attend(id);
      setDeleteAsking(null);
      setHandOverAsking(null);
      setHelperAsking(null);
      setPauseAsking(null);
      setStopAsking(id);
    },
    // *Delete…* (SESSUX1f): attended, its ask under its header, as a stop asks.
    delete: (id) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setHandOverAsking(null);
      setHelperAsking(null);
      setPauseAsking(null);
      setDeleteAsking(id);
    },
    // *Go on with another agent…* and *Stop and hand over…* (MIXAGENT1b-i): attended, the hand-over's ask under its header.
    handOver: (id) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setDeleteAsking(null);
      setHelperAsking(null);
      setPauseAsking(null);
      setHandOverChosen(null);
      setHandOverAsking(id);
    },
    // *Ask another agent…* (MIXAGENT2b): attended, the helper's ask under its header, as a hand-over asks.
    askHelper: (id) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setDeleteAsking(null);
      setHandOverAsking(null);
      setPauseAsking(null);
      setHelperAsking(id);
    },
    // The conversation a hand-over opened, attended as a start's is (MIXAGENT1b-i).
    attend: (id) => {
      chosen();
      attend(id);
    },
    // *Pause quest…* and *Pause ask…* (PAUSE1e): attended, the pause's ask under its header, as a stop asks.
    pause: (id, target) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setDeleteAsking(null);
      setHandOverAsking(null);
      setHelperAsking(null);
      setPauseAsking({ session: id, target });
    },
    // Its review: attended, with the dock open on its work.
    review: (id) => {
      chosen();
      attend(id);
      setDocked((was) => ({ ...was, [id]: 'review' }));
      closeDock(false);
    },
    // A terminal in its folder (§3.5), only where the terminal is a view here: the panel's, shown if it was hidden.
    ...(terminal ? {
      terminal: (folder: string) => {
        terminals.open({ cwd: folder });
        openView('terminal');
      },
    } : {}),
  };
  const actions = useSessionActs({ notify, doors, entries });
  // The head's helpers (MIXAGENT2b): the asks read above, the yes and the no (HELPERNO1) through the acts' one owner, and a door
  // to each helper.
  const helperWaits = attended && here && helperAsks.data ? {
    asks: helperAsks.data,
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    onApprove: (ask: string, answered: Answered) => actions.approveHelperNow(attended.id, ask, answered),
    onDecline: (ask: string, answered: Answered) => actions.declineHelperNow(attended.id, ask, answered),
    onOpen: (helper: string) => {
      chosen();
      attend(helper);
    },
    busy: actions.approvingHelper,
  } : null;
  // Its pieces (COAGENT1b, D199; the co-agent design §6.1): the pieces read above, the ask where it may lead one, a door to each
  // partner and a working piece's stop, through the acts' one owner.
  const pieceSection: PieceSection | null = attended && here && (leads || (partnerPieces.data?.length ?? 0) > 0) ? {
    pieces: partnerPieces.data ?? [],
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    ask: leads ? {
      from: attended.adapter,
      roster: agentRoster,
      busy: actions.askingPartner,
      onAsk: (asked, answered) => actions.askPartnerNow(attended, asked, answered),
    } : undefined,
    onOpen: (partner: string) => {
      chosen();
      attend(partner);
    },
    onStop: (partner: string, answered: Answered) => actions.stopPartnerNow(partner, answered),
    stopping: actions.stopping,
  } : null;
  // Its agent's own ask to hand it over (MIXAGENT1c, D163 point 3), with the person's yes and no: a wait at the foot while the look
  // holds it on their word. A refusal is said beside the presses, in the page's words for its code.
  const handOverWord = (answer: HandOverWord, answered: Answered) =>
    (answer.stands ? answered.done() : answered.refused(handOverRefused(t, answer.why, answer.message)));
  const handOverWait: HandOverAskWait | null = attended && here && handOverAsk.data ? {
    ask: handOverAsk.data,
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    onApprove: (ask, answered) => approveHandOver.mutate({ session: attended.id, ask }, {
      onSuccess: (answer) => handOverWord(answer, answered),
      onError: (error) => answered.refused(sentence(error)),
    }),
    onDecline: (ask, answered) => declineHandOver.mutate({ session: attended.id, ask }, {
      onSuccess: (answer) => handOverWord(answer, answered),
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: approveHandOver.isPending || declineHandOver.isPending,
  } : null;
  // The tool calls the attended session holds for the person's answer under asking (AUTONOMY1b3c, D184 point 3): the driver's own
  // list, this machine's record alone, asked every few seconds while it runs (the routes publish nothing), and the answer's one
  // owner, so the head and the foot press the same. A refusal of the answer is said where it was pressed, in the catalogue's words.
  const permissionWaits = usePermissionWaits(Boolean(attended && here && (attended.state === 'working' || attended.state === 'starting')));
  const answerPermission = useAnswerPermission();
  const heldPermissions: PermissionWaits | null = attended && here && permissionWaits.data ? {
    waits: permissionWaits.data,
    onAnswer: (held, option, answered) => answerPermission.mutate({ session: held.session, request: held.request, option }, {
      onSuccess: (answer) => (answer.answered ? answered.done() : answered.refused(permissionSaid(t, answer.why, answer.message))),
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: answerPermission.isPending,
  } : null;
  // The answers the answers-alone bound holds for the person (COWORK1e, D188 point 4; the message-urgency design §8.3): read off this
  // machine's marks under the sessions' key, and the answer's one owner, so the head and the foot press the same. Its answer said where
  // the press was made; a refusal in the catalogue's words.
  const answerWaits = useAnswerWaits();
  const answerAlone = useAnswerAlone();
  const heldAnswers: AnswerWaits | null = attended && here && answerWaits.data ? {
    waits: answerWaits.data,
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    onAnswer: (wait, goOn, answered) => answerAlone.mutate({ session: wait.session, goOn }, {
      onSuccess: (answer) => {
        if (!answer.answered) {
          answered.refused(answerAloneSaid(t, answer.why, answer.message));
          return;
        }
        notify(t(goOn ? 'work.wait.answer.wentOn' : 'work.wait.answer.kept'));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: answerAlone.isPending,
  } : null;
  // The git go-aheads its git door holds (GITAUTH1f, D198 point 12): the rows What needs you was pushed, so the head, the foot and the
  // band read one judgement and press one door; its answer said where it was pressed, a refusal in the driver's words.
  const attention = useAttention();
  const gitAnswer = useGitAnswer();
  const heldGitAsks: GitAskWaits | null = attended && here && attention.state === 'known' ? {
    waits: attention.rows.flatMap((row) => (row.kind === 'git-ask' && row.gitAsk ? [{ id: row.id, since: row.since, gitAsk: row.gitAsk }] : [])),
    onAnswer: (ask, allow, answered) => gitAnswer.mutate({ ask: ask.id, answer: allow ? 'allow' : 'refuse' }, {
      onSuccess: (answer) => {
        notify(t(!allow ? 'work.wait.gitAsk.refused' : answer.waiting ? 'work.wait.gitAsk.allowed' : 'work.wait.gitAsk.kept'));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: gitAnswer.isPending,
  } : null;

  // The menu bar's verbs that are this frame's (UX7a, D152 §3.2): the attended session's answer, the views of the regions,
  // a new terminal where the attended session works, and *Archive what ended…*'s first press. Each from an effect, since
  // attending and opening a terminal reach past this render; the parent is told it was taken by the effect below it.
  useEffect(() => {
    switch (intent) {
      case 'answer':
        if (attended) doors.answer?.(attended.id);
        return;
      case 'timeline':
      case 'console':
        openView(intent);
        return;
      case 'terminal':
        if (terminal) openView('terminal');
        return;
      case 'newTerminal':
        if (!terminal) return;
        terminals.open({ cwd: terminalCwd });
        openView('terminal');
        return;
      case 'archiveEnded':
        setArchivingEnded(true);
        return;
      default:
    }
    // Only a new ask; what the frame holds is read, not watched.
  }, [intent]);

  // The attended session's page header (§3.2): its acts by the one rule, its stop asking under it (§3.3).
  // The call it is making is the shell's push where that is live (WAITSAY1c2), laid over the pull's, and stepped by its own clock.
  const pushedCalls = usePushedCalls();
  const pulled = attended ? (groups.data ?? []).find((row) => row.session === attended.id) ?? null : null;
  const grouping = withCall(pulled, pushedCalls) ?? null;
  // HEAVYSLOT1 (the UI/UX design §3.5): its part in its repository's heavy-run slot, for the head's line and *Details*' gates, the
  // holder it waits behind named by its title and a door to it. Asked only while it runs.
  const attendedRuns = Boolean(attended && SESSION_ACTIVE.has(attended.state));
  const heavySlot = useHeavySlot(attended?.repository ?? '', { enabled: attendedRuns });
  const heavyTitleOf = (id: string) => {
    const other = (sessions.data ?? []).find((row) => row.id === id);
    return other ? sessionTitle(other, other.quest ? (quests.data ?? []).find((row) => row.id === other.quest) : null, openings[other.id]) : id;
  };
  const heavyPart = attended && attendedRuns ? heavyPartOf(heavySlot.data, attended.id) : null;
  useNow(grouping?.calling || heavyPart ? 15_000 : null);
  // What its own tree offers to land, whatever its ending (LAND4), as the driver's reader said it, and where accepting would
  // put it: the review's own plan, asked only where there is something to land, under the review's key.
  const driverOffers = attended && here && grouping?.lands ? grouping.lands : null;
  // ACCEPTFOOT1 (D164's note): an open conversation's record stays `working` between turns, so the driver's look offers its tree
  // nothing; once its turn has ended, by the driver's word on its turns, the review's own answer says what is beyond the line.
  const talksInTree = !driverOffers && talking && attended
    ? ownTree(attended, (registry.data ?? []).find((row) => row.repository === attended.repository)?.root)
    : null;
  const turnHeld = talksInTree ? chatTurns[attended!.id] : undefined;
  const turnEnded = Boolean(turnHeld && !turnHeld.taking && turnHeld.queued.length === 0);
  // When this frame saw that turn end, or first saw the conversation at rest: its record need not move with a turn, so the review is
  // read again from then, and an answer read before it, another turn's, offers nothing meanwhile.
  const [restsSince, setRestsSince] = useState<{ session: string; at: number } | null>(null);
  useEffect(() => {
    setRestsSince(turnEnded && attended ? { session: attended.id, at: Date.now() } : null);
  }, [attended?.id, turnEnded]);
  const endedAt = restsSince && restsSince.session === attended?.id ? restsSince.at : null;
  // How the tree stands against its line, from the review's own answer (REVIEWBASE5): the same query the review reads, so the
  // head offers no *Accept…* where the review says nothing is beyond the line. Asked only where there is something offered, or a
  // conversation's turn has ended in a tree of its own, whose read is as fresh as that end.
  const reviewMoved = attended && endedAt !== null && endedAt > Date.parse(attended.updated) ? new Date(endedAt).toISOString() : attended?.updated;
  const reviewRead = useSessionDiff(driverOffers || turnEnded ? attended!.id : null, attended ? { updated: reviewMoved!, live: SESSION_ACTIVE.has(attended.state) } : null);
  const behind = reviewRead.data?.behind ?? null;
  const lands = driverOffers ?? conversationOffer({
    tree: talksInTree, turn: turnHeld, review: reviewRead.data, endedAt, readAt: reviewRead.dataUpdatedAt,
  });
  // The branch a moved tree is on (TREEFOLLOWWEB2): the head names it where the clean-up lists no `daoris/<folder>` row for it.
  const ownBranch = attended && here ? grouping?.work?.branch ?? null : null;
  // LANDNAME1b: the branch name typed in the head's land ask, so its plan is asked again with it and a refusal shows first.
  const [landName, setLandName] = useState('');
  const landing = useLanding(lands ? attended!.id : null, landName);
  // Another session's page is another ask: a name typed for one is not carried to the next.
  useEffect(() => setLandName(''), [attended?.id]);
  const land = useLandSessionTree();
  // CHATFLOW1d (the chat-flow design §5.2): a chat run's press refused for what the page can word says it in the reader's language; any
  // other refusal is the driver's sentence, as before.
  const pressRefusal = (result: { message: string; press?: string | null }) =>
    result.press && ['busy', 'elsewhere', 'moved', 'asked'].includes(result.press) ? t(`work.review.chatPress.${result.press}`) : result.message;
  // A landing's answer told back to the press that made it, *Accept…*'s and *Accept without the look…*'s alike.
  const landed = (answered: Answered) => ({
    onSuccess: (result: { done: boolean; message: string; withheld?: string | null; press?: string | null; carried?: { told: boolean } | null }) => {
      if (!result.done) {
        // LANDBACK1 part 1: the press carried the session on and kept the person's yes: it took effect, so it is said, never refused.
        if (result.carried?.told) {
          notify(result.message);
          answered.done();
          return;
        }

        answered.refused(pressRefusal(result));
        return;
      }
      // FABRIC-CLOSE1h9: a push held back is said as such, with the hand-off its review offers, never as an ordinary landing.
      if (result.withheld) notify(`${result.message} ${t('work.review.pushHeldReview')}`, 'error');
      else notify(result.message);
      answered.done();
    },
    onError: (error: unknown) => answered.refused(sentence(error)),
  });
  // The review's gate where it holds that work (REVIEWENV1g, design §3.1): the set-up step's record, whether Daoris still serves
  // its tab here, and the verdict's presses, each by the one owner; *Set it up* follows the session's own quest.
  const reviewActs = useReviewActs({ notify });
  const inReview = useDriver().data?.inReview;
  const gateHolds = reviewHolds(landing.data);
  const gateStep = gateHolds?.quest ? (quests.data ?? []).find((each) => each.id === gateHolds.quest) ?? null : null;
  const headReview: HeadReview | null = gateHolds && attended ? {
    step: gateStep,
    served: inReview && gateHolds.quest ? inReview.find((row) => row.quest === gateHolds.quest)?.served ?? false : null,
    busy: reviewActs.busy || runNodeWord.isPending,
    acts: {
      ...reviewActs.actsFor({
        state: reviewState(gateHolds.state),
        step: gateHolds.quest ?? null,
        record: gateStep,
        work: attended.quest ?? null,
        open: gateHolds.quest && onOpenQuest ? () => onOpenQuest(gateHolds.quest!) : undefined,
      }),
      // A chat run's look is a node of its graph: the person's skip of it, then the one Accept (WORKFLOWCLEAN1h).
      ...(gateHolds.chatRun && gateHolds.lookNode && here ? {
        withoutLook: (words: string | null, answered: Answered) => runNodeWord.mutate(
          { run: gateHolds.chatRun!, node: gateHolds.lookNode!, act: 'skip', words },
          {
            onSuccess: (skipped) => {
              if (!skipped.done) { answered.refused(skipped.message ?? skipped.refused ?? ''); return; }
              land.mutate({ id: attended.id }, landed(answered));
            },
            onError: (error: unknown) => answered.refused(sentence(error)),
          }),
      } : {}),
    },
  } : null;
  // The second opinion's gate before it (XAGENT1g, the second-agent design §7, §9): its findings beside their answers, read only
  // where a level asks one, and its presses by the one owner; a finding's place opens the side bar's preview.
  const opinionActs = useOpinionActs({ notify });
  const secondOpinion = opinionAsked(landing.data);
  const opinionRead = useOpinionGate(secondOpinion && attended ? attended.id : null);
  const headOpinion: HeadOpinion | null = secondOpinion && attended ? {
    detail: opinionRead.data?.detail ?? null,
    busy: opinionActs.busy,
    acts: opinionActs.actsFor({ session: attended.id, gate: secondOpinion, openSession: attend }),
    onOpenFile: openPreview,
  } : null;
  // SQUASHTIDY1b: where the line holds its commits by content, no landing but its discard, the review's own, unforced.
  const discards = attended && here && !lands && grouping?.discards ? grouping.discards : null;
  const discardTree = useDiscardSessionTree();
  // Its line of work's one Accept, or its ask's, lands it too (ACCEPTALL1h): the head's own press waits while either is under way.
  const lineLandsNow = useUnderWaySubject('LAND_ENTRY', attendedEntry?.entry ?? null);
  const askLandsNow = useUnderWaySubject('LAND_ASK', channelAsk);
  const landsNow = useUnderWay('LAND_SESSION_TREE', attended?.id ?? null) || lineLandsNow || askLandsNow;
  const discardsNow = useUnderWay('DISCARD_SESSION_TREE', attended?.id ?? null);
  // Its one press, for the head and, in Accept's place, the review (SQUASHTIDY1f): never forced from here, a refusal the
  // driver's sentence inside the ask, and the review's Discard the door that asks twice.
  const discardHeld = discards && attended ? (answered: Answered) => discardTree.mutate({ id: attended.id }, {
    onSuccess: (result) => {
      if (!result.done) {
        answered.refused(result.message);
        return;
      }
      notify(result.message);
      answered.done();
    },
    onError: (error) => answered.refused(sentence(error)),
  }) : undefined;
  // What waits on the person, handed to the head and to the strip at the foot alike (WAITFOOT1b, D168 point 3): one object, so
  // a press in either place is the same press with the same pending, and its answer, the record read again, settles both.
  const waits: WaitProps | null = attended ? {
    session: attended,
    resolving: resolve.isPending || parkGoAhead.isPending || personDone.isPending,
    onResolve: here ? onResolve : undefined,
    // Its card's answer (PARKANSWER1): through the box's say door, the one door that answers a park (ONEWAY1k).
    onAnswerSession: here ? answerFromCard : undefined,
    onAnswerChoice: here && answering && question ? choiceFromCard : undefined,
    // QUESTIONUI1a (the design §8.4): the head's card's rows are the frame's selection too, so a row chosen there is chosen below.
    answerPick: here && answering && question ? { selected: parkPicked, onSelect: onParkPick } : undefined,
    onAnswerAsk: here ? onAnswerAsk : undefined,
    // The go-aheads it asked, answerable only where this machine can answer the park (KNOWUSE1a2).
    goAheads,
    onGoAhead: here ? onGoAhead : undefined,
    // Its question (PARKVIEW1), and whether the box below is the park's answer box, which carries every press of what it asks: then
    // the strip draws none of the park's waits, and the head lists them with no press. A parked chat's composer draws none of it.
    question,
    parkBox: answering,
    // The rules it proposed (PROPOSEHERE1b): settled through the band's own door, then, on an *Allow*, the session told so as its
    // next turn, which is composed with the rule. One that takes no words now (its box's line, an intake) is not told, and says so.
    rules: {
      proposals: ruleState.data?.proposals,
      onAnswer: here
        ? (proposal, accept, answered) => ruleAnswer.answer(proposal, accept, answered, intake || box.kind === 'line'
          ? null
          : { session: attended.id, say: (text) => say.mutateAsync({ id: attended.id, text }) })
        : undefined,
      busy: ruleAnswer.busy,
      heard: !intake && box.kind !== 'line',
    },
    // The helpers it asked for (MIXAGENT2b), with the yes and the no: an agent's ask the look holds waiting on the person's word is
    // one of its waits, at the head and at the foot alike (MIXAGENT2c, D168).
    helpers: helperWaits,
    // Its agent's own ask to hand it over (MIXAGENT1c): one of its waits while the look holds it on the person's word, the strip's.
    handOver: handOverWait,
    // The tool calls it holds for the person's answer (AUTONOMY1b3c): one of its waits, at the head and at the foot alike.
    permissions: heldPermissions,
    // A third answer alone the bound holds for the person (COWORK1e): one of its waits, at the head and at the foot alike.
    answers: heldAnswers,
    // A git go-ahead its git door holds (GITAUTH1f): one of its waits, at the head and at the foot alike.
    gitAsks: heldGitAsks,
    // Its review, wherever the review stands (SESS2 H4): the move beside unlanded work.
    onReview: () => openView('review'),
    // LAND4: commits its tree holds, offered to land beside what it left; the press is the review's Accept, its sentence said
    // once it landed and a refusal said inside the ask (UXFIX2).
    lands,
    review: headReview,
    behind,
    opinion: headOpinion,
    landing: landing.data,
    planning: landing.isPlaceholderData,
    // The second opinion's token rides with the press that answers what it showed (XAGENT1g).
    onLand: lands ? (answered, answers, branch) => land.mutate({ id: attended.id, answers, branch }, landed(answered)) : undefined,
    onSayRebase: (text) => say.mutateAsync({ id: attended.id, text }).then((answer) => answer.sent),
    onName: lands ? setLandName : undefined,
    // SQUASHTIDY1b: its discard, never forced from here (`discardHeld`).
    discards,
    onDiscardTree: discardHeld,
    // Under way for this session from either place or another window (STATEHUB1c, D169 point 7), never another session's press.
    workBusy: landsNow || discardsNow,
  } : null;
  // Where the work's ask (*Accept…*, *Discard branch…*) is open: in the head or at the foot, never both (D168 point 3). The session's
  // own press (REVIEWFLOW1a) is that ask in the place of a part's.
  const [workAsk, setWorkAsk] = useState<{ session: string; at: 'head' | 'foot' | 'review' } | null>(null);
  const workAskAt = (at: 'head' | 'foot'): WorkAsk | undefined => (attended ? {
    open: workAsk?.session === attended.id && workAsk.at === at,
    onOpen: (open) => setWorkAsk((was) => (open
      ? { session: attended.id, at }
      : was?.session === attended.id && was.at === at ? null : was)),
  } : undefined);
  // ACCEPTALL1 (D167's 2026-10-15 amendment), REVIEWFLOW1a: its line of work's one Accept, where its parts offer work to land and
  // more than one does or the attended part has none of its own; the plan reads git for every part, so it is asked only once the ask
  // is open.
  const entryOffered = attendedEntry && here
    ? attendedEntry.parts.filter((part) => (groups.data ?? []).some((row) => row.session === part.session && row.lands)).length
    : 0;
  const entryAccepts = attendedEntry && (entryOffered > 1 || (entryOffered === 1 && !lands)) ? attendedEntry.entry : null;
  const entryPlan = useEntryLanding(entryAccepts, entryAccepts !== null && Boolean(attended && workAsk?.session === attended.id));
  const landEntry = useLandEntry();
  // Under way in any window (ACCEPTALL1e): this one's press from its send, another's from the driver's telling.
  const entryLandsNow = useUnderWaySubject('LAND_ENTRY', entryAccepts);
  const entryBase: ComponentProps<typeof EntryAccept> | undefined = entryAccepts && attendedEntry ? {
    scope: 'entry',
    offered: entryOffered,
    open: false,
    onOpen: () => {},
    plans: entryPlan.data ? [entryPlan.data] : null,
    reading: entryPlan.isFetching && !entryPlan.data,
    busy: entryLandsNow,
    numberOf: (session) => attendedEntry.parts.findIndex((part) => part.session === session) + 1 || null,
    // Told back to its ask: every part landed, or the driver's sentence of what landed and what was refused, said inside it.
    onLand: (leaveOut, answered) => landEntry.mutate({ entry: entryAccepts, parts: entryOffered, leaveOut }, {
      onSuccess: (result) => {
        if (!result.done) {
          answered.refused(result.message);
          return;
        }
        notify(result.message);
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
  } : undefined;
  // The press where it is drawn: its ask open there, and held where the ask is open in another place (D168 point 3).
  const entryAt = (at: 'head' | 'foot' | 'review'): ComponentProps<typeof EntryAccept> | undefined => {
    if (!entryBase || !attended) return undefined;
    const mine = workAsk?.session === attended.id ? workAsk.at : null;
    return {
      ...entryBase,
      open: mine === at,
      held: mine !== null && mine !== at,
      onOpen: (open) => setWorkAsk((was) => (open
        ? { session: attended.id, at }
        : was?.session === attended.id && was.at === at ? null : was)),
    };
  };
  const entryAccept = entryAt('head');
  // WAITFOOT1c (D168 point 4): the waits that stand where the strip draws them, and the events that raised them, which the
  // conversation finds and tells; the way to each is handed to the head and the strip alike, and a raising block's *To the wait*
  // leads to its line where it is on screen (WAITFOOT2b).
  const answerBelow = answering || (talking && box.kind === 'chat');
  const standing = attended && here && waits ? waitsOf({ ...waits, entry: entryAccept, answerBelow }) : [];
  // PARKVIEW1: where the box answers the park, its question and its go-aheads stand in the box, so *To the wait* leads there.
  const inTheBox = (wait: AnchorWait) => answering && (wait === 'question' || wait.startsWith('goAhead:'));
  const [anchored, setAnchored] = useState<(AnchorState & { session: string }) | null>(null);
  const toEvent = attended && anchored?.session === attended.id ? waitEvents(anchored.anchored, anchored.show) : undefined;
  // One wait in one place on the screen (WAITFOOT2a, D168's WAITFOOT2 note): while the head's waits are wholly on screen the
  // strip stands aside, and a raising block's *To the wait* leads to the head's line instead of the strip's.
  const waitsOnScreen = useWaitsOnScreen(centre);
  const [toWait, setToWait] = useState<{ session: string; wait: AnchorWait; n: number; at: 'head' | 'foot' | 'box' } | null>(null);
  // Spent once the person attends another: the strip drawn again on coming back takes no focus from it.
  useEffect(() => { setToWait(null); }, [attended?.id]);
  // To the head's line while it is on screen, where the strip stands aside (WAITFOOT2b), else the strip's; a park's question and
  // go-aheads to the box that answers them (PARKVIEW1), which is always drawn. The head's *next* leads the same way (REVIEWFLOW1b).
  const goToWait = (anchor: AnchorWait) => {
    if (!attended) return;
    const at = inTheBox(anchor) ? 'box' : waitsOnScreen === true ? 'head' : 'foot';
    setToWait((was) => ({ session: attended.id, wait: anchor, n: (was?.n ?? 0) + 1, at }));
  };
  const seekAt = (at: 'head' | 'foot' | 'box') => (attended && toWait?.session === attended.id && toWait.at === at ? toWait : null);
  // And a rule's *Allow…* (PROPOSEHERE1b), by the proposal it asks about: open in the head or at the foot, never both.
  const [ruleAsk, setRuleAsk] = useState<{ session: string; id: string; at: 'head' | 'foot' } | null>(null);
  const ruleAskAt = (at: 'head' | 'foot'): RuleAsk | undefined => (attended ? {
    open: ruleAsk?.session === attended.id && ruleAsk.at === at ? ruleAsk.id : null,
    onOpen: (id) => setRuleAsk((was) => (id !== null
      ? { session: attended.id, id, at }
      : was?.session === attended.id && was.at === at ? null : was)),
  } : undefined);
  // The waiting panel's height, kept per viewer (WAITPANEL1); and the conversation's presses drawn above it, so neither is ever
  // under it: *Back to bottom* told by the conversation (`onTail`), *To the top* once the reader is down from the head.
  const waitPanel = useWaitPanelHeight();
  const [tail, setTail] = useState<TailState | null>(null);
  const awayFromTop = useAwayFromTop(centre, attended?.id);
  // *To the top* (D168 point 1, above the waiting panel since WAITPANEL1): the record's head, where every wait stands with its
  // reading, the focus with it.
  const toTop = () => {
    const element = centre.current;
    if (!element) return;
    // A test's DOM has no scrollTo on elements; a property set is the same place without the glide.
    if (typeof element.scrollTo === 'function') element.scrollTo({ top: 0, behavior: 'smooth' });
    else element.scrollTop = 0;
    element.querySelector<HTMLElement>('[data-record-head]')?.focus({ preventScroll: true });
  };
  const attendedFacts: ActFacts | null = attended
    ? { session: attended, quest, grouping, root: rootOf(attended.repository), where: where[attended.id] }
    : null;
  const headActs = attendedFacts ? offeredActs(attendedFacts, 'header').filter(actions.can) : [];
  // LIMITRESUME1d (D206 §3.7): a driven line an account's limit cut off, every account it may run on cooling, says under its header
  // when it resumes by itself and how, with the person's ways out beside it: *Go on now with…* (the hand-over's ask), *Pause quest…*
  // and the account's *Try now*. Read from the accounts the page holds, never a guess: no walk that waits, no line.
  const attendedWorkspace = attended
    ? (registry.data ?? []).find((row) => row.repository === attended.repository)?.workspace || 'default'
    : 'default';
  const limitWait = attended && here && limitedLine(attended, quest?.status, Boolean(grouping?.pausedBy))
    ? limitWaitOf({ session: attended, workspace: attendedWorkspace, doors: roster, answer: accounts.data, now: new Date() })
    : null;
  // LIMITRESUME1d2 (D206 §3.5, §3.7): a conversation says its wait from its events alone (`chat.limit-waits`), worded by the walk
  // where the page holds it, else by the driver's own line. Its ways out: *Go on now with…*, *Stop…* while it runs or *Stop waiting*
  // once it ended, and *Try now*.
  const conversationHeld = attended && here && attended.kind === 'chat' && chatHeld?.session === attended.id ? chatHeld.held : null;
  const conversationWait = conversationHeld && attended
    ? limitWaitOf({ session: attended, workspace: attendedWorkspace, doors: roster, answer: accounts.data, now: new Date() })
    : null;
  const conversationLines = conversationHeld && attended
    ? chatLimitWaitSaid(t, {
      held: conversationHeld,
      wait: conversationWait,
      label: (account) => nameOf(attended.adapter, account),
      language: i18n.language,
      names: accountNames(roster),
      now: new Date(),
    })
    : null;
  const waitLines = limitWait && attended
    ? limitWaitSaid(t, limitWait, (account) => nameOf(attended.adapter, account))
    : conversationLines;
  const waitAccount = limitWait?.account ?? conversationWait?.account ?? attended?.profile ?? null;
  // The line, the same at the head and the foot (D206 §3.7): one element, its presses the same presses.
  const limitLine = (className: string) => (waitLines && attended ? (
    <LimitWaitLine
      // Keyed by the session and the place: a refusal said in one is never another's (RELSPAM1).
      key={`limit:${attended.id}`}
      className={className}
      lines={waitLines}
      busy={actions.handingOver || accountUse.isPending || stopWaiting.isPending}
      refusal={limitRefusal?.session === attended.id ? limitRefusal.sentence : null}
      onGoOnNow={headActs.includes('handOver') ? () => doors.handOver?.(attended.id) : undefined}
      onPause={limitWait && attended.quest ? () => doors.pause?.(attended.id, { scope: 'quest', id: attended.quest! }) : undefined}
      onStop={!limitWait && live && headActs.includes('stop') ? () => doors.stop?.(attended.id) : undefined}
      onStopWaiting={!limitWait && !live ? () => {
        setLimitRefusal(null);
        stopWaiting.mutate(attended.id, {
          onSuccess: (answer) => {
            if (!answer.stopped) setLimitRefusal({ session: attended.id, sentence: answer.message });
          },
          onError: (error) => setLimitRefusal({ session: attended.id, sentence: sentence(error) }),
        });
      } : undefined}
      onTryNow={() => {
        setLimitRefusal(null);
        accountUse.mutate(
          { harness: attended.adapter, action: 'ready', ...(waitAccount ? { profile: waitAccount } : {}) },
          { onError: (error) => setLimitRefusal({ session: attended.id, sentence: sentence(error) }) },
        );
      }}
    />
  ) : null);
  // Daoris's offer to hand over (MIXAGENT1b-ii, D163 point 3): on a conversation this page offers the press on, when its own
  // account is cut or near its limit by its scope's near (never the agent's pace warning alone, USAGEREAD3b) and another agent
  // has an account the walk would take. No driver read.
  // MODELROLE1e (D205 §3.5(e)): its order is the session's actor's, Build's for a driven line and Chat's for a conversation, where that
  // actor has an order of its own; the roster's order stands where it is as before.
  const attendedActor = attended ? (actors.data ?? []).find((row) => row.id === (attended.kind === 'chat' ? 'chat' : 'build')) : undefined;
  const offer = attended && headActs.includes('handOver') && !offerDismissed.has(attended.id) && !waitLines
    ? handOverOffer({
      session: attended,
      workspace: attendedWorkspace,
      doors: roster,
      answer: accounts.data,
      now: new Date(),
      actor: attendedActor,
    })
    : null;
  const offerTo = offer ? roster.find((row) => row.harness === offer.agent) : null;
  const asked = attended ? stopAsk(attended, quest) : null;
  // A pause's ask says what it stops from the driver's plan of the work (D132 §2.6); one that stops nothing applies at once.
  const pausing = pauseAsking && attended?.id === pauseAsking.session ? pauseAsking : null;
  const pausePlan = useWorkPlan(pausing?.target ?? null);
  const pauseWired = wiredFor(wiring, quest?.workspace ?? attended?.workspace);
  const pauseLines = pausePlan.plan ? pauseAsk(pausePlan.plan, { wired: pauseWired }) : null;
  const pauseQuiet = Boolean(pausing && pausePlan.plan && pauseLines === null);
  const pausedQuietly = useRef<string | null>(null);
  useEffect(() => {
    if (!pausing || !pauseQuiet) return;
    const key = `${pausing.target.scope}:${pausing.target.id}`;
    if (pausedQuietly.current === key) return;
    pausedQuietly.current = key;
    actions.work.pause(pausing.target, undefined, () => setPauseAsking(null));
  }, [pausing, pauseQuiet, actions.work]);
  useEffect(() => { if (!pausing) pausedQuietly.current = null; }, [pausing]);
  // Named as its row is (TITLEONE1): the entry's name, its ask's, where an ask began it, read under the rail's own key; otherwise
  // the record's own.
  const entryAsk = channelAsk ? (entryAsks.data ?? []).find((ask) => ask.id === channelAsk) : undefined;
  const attendedTitle = attended
    ? nameOfEntry(attendedEntry ?? undefined, entryAsk ? { [entryAsk.id]: askName(entryAsk) } : {})
      ?? sessionTitle(attended, quest, openings[attended.id])
    : '';
  // Whom it continues and whom a helper helps (MIXAGENT3a-i): the record's own links, joined to the sessions the frame holds, for
  // the head's facts and its *Details*. One the frame does not hold is said by its id, and its agent is not guessed.
  const linked = attended
    ? linkedSessions(attended, new Map((sessions.data ?? []).map((row): [string, Session] => [row.id, row])))
    : null;
  const headLink = (other: Session | null | undefined): HeadLink | null => (other ? {
    session: other,
    title: sessionTitle(other, other.quest ? (quests.data ?? []).find((row) => row.id === other.quest) : null, openings[other.id]),
  } : null);
  const continues = headLink(linked?.continues);
  const helps = headLink(linked?.helps);
  const headPrimary = primaryAct(headActs, grouping);
  // A right-click on the session's page offers its header's acts, in the order the header draws them (CTX1, D138 §4).
  const pageMenu = attended && attendedFacts
    ? {
      label: attendedTitle,
      acts: actMenu(headerActs(headActs, headPrimary).all, t, (act) => actions.run(act, attendedFacts), actions.busy, attended),
    }
    : null;
  const attendedShown = attended ? shownOf(attended, grouping, taking[attended.id]) : null;
  // Its facts line by its state (UX7c, D152 §7): the row's facts and the record's, so the head and the row agree.
  const headFactsOf: SessionFactsOf | null = attended && attendedShown ? {
    session: attended,
    shown: attendedShown,
    grouping,
    ownSignIn: roster.some((row) => row.harness === attended.adapter),
    branch,
    nameOf,
    // The agent it continues by the roster's name for it, as the hand-over's ask names agents (MIXAGENT3a-i).
    continuesAgent: continues
      ? roster.find((row) => row.harness === continues.session.adapter)?.product || continues.session.adapter
      : null,
    leadingAgent: leadingAgentOf(attendedEntry, sessions.data ?? [], roster),
    helping: helps?.title,
    // COAGENT1b: its partners, the agents its pieces went to by the roster's names, and how many work now.
    partners: partnersOf(partnerPieces.data ?? [], (agent) => agentRoster.names?.[agent] ?? agent),
    waitingSince: waiting?.session === attended.id ? waiting.since : undefined,
    calling: waiting?.session === attended.id ? waiting.calling : undefined,
    withIt: following?.id === attended.id ? withIt : 0,
    windowOpened: atWorkHeld && 'answer' in atWorkHeld ? atWorkHeld.answer.windowOpened : undefined,
    // AGENTERRORS1a2 (D210 §5.1, §8): *Trying again at* while a transient episode waits.
    retrying: waiting?.session === attended.id ? waiting.retrying : undefined,
    // HEAVYSLOT1: its heavy run said in place of the call, by its gate or whom it waits behind.
    heavy: heavyPart ? heavyLine(t, heavyPart, heavyTitleOf) : null,
    integration: integration.data ?? null,
    // REVIEWFLOW1b: where the work stands, from the same facts the head's waits are drawn from.
    flow: { parts: entryOffered, landing: landsNow },
  } : null;
  const nextWait = nextPress(standing);
  const nextAnchor = standing[0] ? anchorOf(standing[0]) : null;
  const pageHead = attended && attendedFacts && attendedShown && headFactsOf && (
    <SessionPageHead
      session={attended}
      title={attendedTitle}
      shown={attendedShown}
      facts={sessionFacts(t, headFactsOf)}
      // RAILLINE1: the line is one line, its running call said short; the whole line, the call's whole title in it, is its tip.
      factsTip={sessionFactsTip(t, headFactsOf)}
      next={nextWait && nextAnchor ? { press: nextWait, onPress: () => goToWait(nextAnchor) } : null}
      acts={headActs}
      primary={headPrimary}
      busy={actions.busy}
      onAct={(act) => actions.run(act, attendedFacts)}
      asking={stopAsking === attended.id && live && asked ? (
        <StopAsk
          // Keyed by the session: an ask open on one session, with its wait or its refusal, is never another's.
          key={attended.id}
          sentence={t(asked.key, asked.values)}
          busy={actions.stopping}
          onStop={(answered) => actions.stopNow(attended, answered)}
          // A stop that lands after the person moved to another session closes only the ask it was pressed in.
          onClose={() => setStopAsking((was) => (was === attended.id ? null : was))}
        />
      ) : deleteAsking === attended.id && headActs.includes('delete') ? (
        <DeleteAsk
          key={attended.id}
          busy={actions.deleting}
          onDelete={(answered) => actions.deleteNow(attended, answered)}
          onClose={() => setDeleteAsking((was) => (was === attended.id ? null : was))}
        />
      ) : handOverAsking === attended.id && headActs.includes('handOver') ? (
        <HandOverAsk
          key={attended.id}
          from={attended.adapter}
          first={[...new Set(nextChoices(attended, roster, attendedActor).map((step) => step.door.harness))]}
          working={SESSION_ACTIVE.has(attended.state)}
          roster={agentRoster}
          initial={handOverChosen?.choice}
          busy={actions.handingOver}
          onHandOver={(choice, told) => actions.handOverNow(attended, choice, told, handOverChosen?.why)}
          onClose={() => {
            setHandOverAsking((was) => (was === attended.id ? null : was));
            setHandOverChosen(null);
          }}
        />
      ) : helperAsking === attended.id && headActs.includes('askHelper') ? (
        <AskHelper
          key={attended.id}
          from={attended.adapter}
          roster={agentRoster}
          busy={actions.askingHelper}
          onAsk={(asked, answered) => actions.askHelperNow(attended, asked, answered)}
          onClose={() => setHelperAsking((was) => (was === attended.id ? null : was))}
        />
      ) : pausing && !pauseQuiet ? (
        <PauseAsk
          className="mt-2.5"
          target={pausing.target}
          lines={pauseLines}
          meanIt={t(pausing.target.scope === 'ask' ? 'asks.record.pauseMeanIt' : 'quests.detail.pauseMeanIt')}
          busy={actions.work.pausing}
          onPause={(answered) => actions.work.pause(pausing.target, answered)}
          onClose={() => setPauseAsking(null)}
        />
      ) : waitLines && attended ? (
        limitLine('mt-2.5')
      ) : offer && attended ? (
        <HandOverOfferLine
          // Keyed by the session: a refusal or a kept answer said in one is never another's.
          key={attended.id}
          offer={offer}
          actorName={attendedActor ? t(`work.role.name.${attendedActor.id}`) : undefined}
          agentName={offerTo?.product || offer.agent}
          accountName={offer.account ? nameOf(offerTo ? toolOf(offerTo) : offer.agent, offer.account) : null}
          busy={actions.handingOver}
          onGoOn={(told) => actions.handOverNow(
            attended,
            {
              agent: offer.agent, account: offer.account ?? '', name: offerTo?.product || offer.agent,
              model: offer.choice?.model, effort: offer.choice?.effort,
            },
            told,
            offer.why,
          )}
          onChoose={() => {
            chosen();
            setStopAsking(null);
            setDeleteAsking(null);
            setHelperAsking(null);
            setPauseAsking(null);
            setHandOverChosen({ why: offer.why, choice: { adapter: offer.agent, profile: offer.account ?? '' } });
            setHandOverAsking(attended.id);
          }}
          onDismiss={() => setOfferDismissed((was) => new Set(was).add(attended.id))}
        />
      ) : null}
    />
  );

  // Sessions' list (D118 §3a): the rail, handed to the list pane as every view with a list hands its own.
  // NEW is one control (D56): it was a permanent 287×200 form above the list, 27% of the rail, for
  // something a person does occasionally, and every reference in the study puts new behind a single affordance.
  // The list's own choices (D126 §4.1): by state, the default, or by repository; and whether archived is shown.
  const sessionsFilters = sessionFilters(listed.pane('sessions').filters);
  // A go to a group of the list where the person keeps it by repository shows it by state (ENTRY1g, D161's ENTRY1b note):
  // for the go only and never remembered, it lasts until the person chooses an arrangement in the ⋯, which says what is
  // shown, or leaves Sessions. Set as the go arrives, not after, so the rail draws the group the render it looks for it.
  const [goByState, setGoByState] = useState(false);
  if (!elsewhere && (group === 'you' || group === 'review') && sessionsFilters.group === 'repository' && !goByState) {
    setGoByState(true);
  }
  if (elsewhere && goByState) setGoByState(false);
  const arrangement = goByState ? 'state' : sessionsFilters.group;
  const sessionsList = (): ListSpec => ({
    view: 'sessions',
    name: t('work.rail.label'),
    labels: { open: t('work.rail.open'), close: t('work.rail.close'), resize: t('work.rail.resize') },
    make: { label: t('work.start.title'), onMake: newConversationPressed },
    // The list's ⋯, which it never had (SESSUX1c): how it is grouped, a choice of two, and whether archived sessions
    // are shown, both remembered (D118 §3f); then, below a rule, *Archive what ended…*, which lists first (SESSUX1e).
    more: (
      <ListMore
        label={t('work.list.more')}
        choice={{
          label: t('work.list.groupBy'),
          value: arrangement,
          options: [
            { value: 'state', label: t('work.list.byState') },
            { value: 'repository', label: t('work.list.byRepository') },
            // Each ask its entries (D167 point 5): offered where the reader answers entries, since it groups by them.
            ...(entries ? [{ value: 'ask', label: t('work.list.byAsk') }] : []),
          ],
          onChoose: (value) => {
            setGoByState(false);
            listed.setFilters('sessions', keptSessionFilters({
              ...sessionsFilters, group: value === 'repository' ? 'repository' : value === 'ask' ? 'ask' : 'state',
            }));
          },
        }}
        items={[
          { id: 'archived', label: t('work.list.showArchived'), checked: sessionsFilters.archived },
          { id: 'archiveEnded', label: t('work.list.archiveEnded'), rule: true },
        ]}
        onChoose={(id) => {
          if (id === 'archiveEnded') setArchivingEnded(true);
          else if (id === 'archived') {
            listed.setFilters('sessions', keptSessionFilters({ ...sessionsFilters, archived: !sessionsFilters.archived }));
          }
        }}
      />
    ),
    loading: sessions.isPending,
    resyncing: sessionsResyncing,
    chosen: selected,
    // What waits on the person, then what runs, in the open list's order (FRAME6; D126 §2.5).
    strip: (
      <SessionRail
        selected={selected}
        onSelect={(id) => { chosen(); attend(id); }}
        notify={notify}
        compact
        taking={taking}
        arrangement={arrangement}
        archived={sessionsFilters.archived}
        // The strip draws no group and is handed none: a go lays the list over beside it, and the list there brings the
        // group (ENTRY1g). Handed one, it answered first and let the go go before the list laid over had looked.
      />
    ),
    body: (
      <SessionRail
        selected={selected}
        onSelect={(id) => { chosen(); attend(id); }}
        notify={notify}
        taking={taking}
        lastTurns={lastTurns}
        arrangement={arrangement}
        archived={sessionsFilters.archived}
        archiveEnded={archivingEnded}
        onArchiveEnded={() => setArchivingEnded(false)}
        group={group}
        onGroupBrought={onGroupBrought}
        // A row's acts that are this frame's: answering, asking to stop or to delete, reviewing, a terminal there (SESSUX1d, SESSUX1f).
        doors={doors}
      />
    ),
  });
  const spec = elsewhere ? viewLayout.list : sessionsList();

  // The line the attended part continues (SWITCHCTX1c-i; the switch-context design §2.7): each part's record and place as the strip
  // reads them, the parts it continues by `from` (a part running alongside is not among them), and how each began.
  const partRows = Object.fromEntries((sessions.data ?? []).map((row) => [row.id, {
    session: row, root: rootOf(row.repository), where: where[row.id], spent: spent[row.id],
  }]));
  const beganOf = (id: string) => {
    const part = attendedEntry?.parts.find((each) => each.session === id);
    return attendedEntry && part && part.began !== 'opened' ? beganLine(t, part, attendedEntry.parts, partRows) : undefined;
  };
  const line: EarlierPart[] = attended && here
    ? ancestryOf(sessions.data ?? [], attended.id, attendedEntry ? new Set(attendedEntry.parts.map((part) => part.session)) : null)
      .flatMap((id) => {
        const row = (sessions.data ?? []).find((each) => each.id === id);
        return row
          ? [{ id, adapter: row.adapter, chat: row.kind === 'chat', tree: row.tree, live: SESSION_ACTIVE.has(row.state), began: beganOf(id) }]
          : [];
      })
    : [];
  // Pressing a part in the strip or on the flow's rail attends it and scrolls to it, so the part shown is the one in view.
  const [seeking, setSeeking] = useState(0);
  const partBox = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (seeking === 0) return undefined;
    const frame = requestAnimationFrame(() => partBox.current?.scrollIntoView?.({ block: 'start' }));
    return () => cancelAnimationFrame(frame);
  }, [seeking]);

  return (
    // Positioned, so a dock filling the frame (FRAME6) lies over exactly this and nothing more.
    // 🔴 `min-w-0` (USE1): a flex item is otherwise no narrower than its content, and every view but
    // Sessions is drawn in here, so a long title that should truncate widened the frame past the
    // window (seen on the install's Quests view; a browser draws the view outside the frame).
    // `isolate` (D118 §3d, audit F10): its layers stay its own — a full side bar's `z-20`, a list laid
    // over — so every overlay drawn at the page's root, a drawer among them, lies above all of it.
    <div ref={root} className="relative isolate flex min-h-0 min-w-0 flex-1">
      {spec && list && <ViewListPane spec={spec} layout={list} lists={listed} onOver={go.setOver} />}

      <div className="flex min-w-0 flex-1 flex-col">
        {elsewhere ? viewLayout.main : (<>
        <ViewMain
          ref={centre}
          gutters="session"
          // Pinned at the top while the conversation scrolls under it (D126 §3.2); none with nothing attended.
          header={pageHead || undefined}
          menu={pageMenu}
          // A remembered session is found in the list, so while the list first loads it is on its way, never
          // the composer (audit SE11). With nothing attended the main area is *New conversation* (NEW1b): no notice is
          // handed, so the state's body is the composer.
          state={attended ? 'chosen' : selected && sessions.isPending ? 'loading' : 'none'}
        >
          {/* *New conversation* (NEW1b, D162 point 1), the main area while nothing is attended. */}
          {!attended && (
            <StartPane
              start={start}
              registered={registry.data ?? []}
              sessions={sessions.data}
              agentRoster={agentRoster}
              defaultHarness={spawning?.harness}
              notify={notify}
              onMakeTask={onMakeTask}
              attend={attend}
            />
          )}
          {attended && waits && <AttendedSession
            // What waits on the person, and every press on it: the strip at the foot is handed the same (D168 point 3).
            {...waits}
            // Its pieces (COAGENT1b): what other agents write for it, and the ask of one; no wait of the person's.
            pieces={pieceSection}
            // COMMS1i: what its line waits on of others, At work's line from At work's read, a door to At work.
            waitingOn={following?.id === attended.id && atWorkHeld && 'answer' in atWorkHeld ? {
              answer: atWorkHeld.answer,
              nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
              onOpen: () => openView('atWork'),
            } : null}
            workAsk={workAskAt('head')}
            ruleAsk={ruleAskAt('head')}
            toEvent={toEvent}
            seek={seekAt('head')}
            quest={quest}
            opening={attended ? openings[attended.id] : null}
            taking={attended ? taking[attended.id] : undefined}
            lastTurn={attended ? lastTurns[attended.id] : undefined}
            // The page header above carries its state, its id and its stop (D126 §3.2): said once.
            headed
            // An agent on the roster has accounts, so a record naming none ran on the tool's own sign-in (D125 §3.7).
            ownSignIn={Boolean(attended && roster.some((row) => row.harness === attended.adapter))}
            nameOf={nameOf}
            // Its entry's parts (SESSONE1c, D167 point 5): every act on this page stays the part shown's, and the strip picks it.
            parts={attended && entryOfSession(entries, attended.id) ? {
              entry: entryOfSession(entries, attended.id)!,
              attended: attended.id,
              rows: partRows,
              onShow: (id: string) => {
                attend(id);
                setSeeking((count) => count + 1);
              },
            } : undefined}
            entryAccept={entryAccept}
            // Its ask's channel, kept to this line of work (SESSONE2c): a post is the person's words, and a refusal is the service's code.
            channel={attended && attendedEntry && channelAsk && channel.available && (channel.loading || channel.channel) ? {
              channel: channel.channel,
              only: { entry: attendedEntry.entry, repository: attendedEntry.repository },
              onPost: (post, answered) => postChannel.mutate({ ask: channelAsk, post }, {
                onSuccess: (posted) => (posted.posted ? answered.done() : answered.refused(posted.code ?? 'other')),
                onError: (error) => answered.refused(sentence(error)),
              }),
              // COWORK1d: *Send to its session*, the person's own press, as on the ask's page.
              onForward: (message, repository, answered) => forwardChannel.mutate({ ask: channelAsk, message, repository }, {
                onSuccess: (forwarded) => (forwarded.sent ? answered.done() : answered.refused(forwarded.code ?? 'other')),
                onError: (error) => answered.refused(sentence(error)),
              }),
              onOpenQuest,
              onOpenAsk: onAnswerAsk ? () => onAnswerAsk(channelAsk) : undefined,
              opened: channelOpened,
            } : undefined}
            chain={quest ? buildChain(quest.id, quests.data ?? [], sessions.data ?? []) : []}
            relations={attended ? relationsOf(attended, quest, quests.data ?? [], sessions.data ?? []) : undefined}
            related={related}
            relatedOwn={relatedOwn}
            onRelated={attend}
            together={attended ? {
              candidates: related.filter((row) => row.read !== 'not-read').map((row) => row.id),
              operation: together,
              busy: bringTogether.isPending || togetherAct.isPending || landEntry.isPending,
              said: bringTogether.data?.refused ?? togetherAct.data?.refused
                ?? (landEntry.variables?.entry === attendedEntry?.entry ? landEntry.data?.message ?? null : null),
              onBring: here ? (members) => bringTogether.mutate({ session: attended.id, members }) : undefined,
              onLetGo: () => togetherAct.mutate({ session: attended.id, act: 'let-go' }),
              onAskAgain: () => togetherAct.mutate({ session: attended.id, act: 'ask-again' }),
              onAccept: attendedEntry
                ? () => landEntry.mutate({ entry: attendedEntry.entry, parts: Math.max(1, entryOffered), leaveOut: [] })
                : undefined,
              onSession: attend,
            } : undefined}
            memory={attended ? {
              rows: memoryRows,
              ended: !live,
              busy: decideMemory.isPending,
              said: decideMemory.variables?.session === attended.id ? decideMemory.data?.message ?? null : null,
              onDecide: (op, approve) => decideMemory.mutate({ session: attended.id, op, approve }),
              onKeep: (keep) => decideMemory.mutate({ session: attended.id, keep }),
            } : undefined}
            continues={continues}
            helps={helps}
            heavy={attended && heavyPart ? heavyRow(t, heavySlot.data, attended.id, heavyTitleOf, attend) : null}
            process={told.data?.process ?? null}
            onSession={(session) => attend(session.id)}
            onQuest={onOpenQuest ? (row) => onOpenQuest(row.id) : undefined}
            branch={branch}
            ownBranch={ownBranch}
            // Only this machine's own record: the clean-up's list is this machine's, and so is the branch it names.
            // Told back to its ask (UXFIX2): a branch kept, or a refusal, is said inside it.
            onDiscardBranch={branch && here
              ? (answered) => discardBranch.mutate(branch, {
                onSuccess: sayDiscard(notify, t, answered),
                onError: (error) => answered.refused(sentence(error)),
              })
              : undefined}
            discardingBranch={discardBranch.isPending}
            milestone={milestone}
            record={sessionRecord}
            onOpenBranch={onOpenBranch}
            trace={attended && trace.available ? {
              open: tracing === attended.id,
              onToggle: () => setTracing(tracing === attended.id ? null : attended.id),
              answer: tracing === attended.id ? trace.query.data : undefined,
              reading: tracing === attended.id && trace.query.isPending,
              refusal: tracing === attended.id && trace.query.error ? sentence(trace.query.error) : null,
              onSession: attend,
              onQuest: onOpenQuest,
              onAsk: onAnswerAsk,
            } : undefined}
          />}
          {/* The conversation (D76): below the record, in the same scroll, so the head is read once
              and the words are what the region follows. Only where the session ran: a teammate's
              record came without its transcript, which stays on their machine (D47 §4). */}
          {attended && here && (
            <div className="mt-2 min-w-0">
              {/* A file a tool card names opens in the side bar's preview (PREVIEW1, D111). */}
              <FileOpener.Provider value={openPreview}>
                {/* The parts it continues, above it and read-only (SWITCHCTX1c-i); keyed by the part, so attending another starts the count again. */}
                {line.length > 0 && <EarlierParts key={attended.id} line={line} scroller={centre} />}
                <div ref={partBox} data-part={attended.id} data-attended className="min-w-0 scroll-mt-12">
                  <SessionConversation
                    session={attended.id}
                    began={beganOf(attended.id)}
                    adapter={attended.adapter}
                    chat={attended.kind === 'chat'}
                    tree={attended.tree}
                    live={live}
                    // A conversation the driver answers for is working while a turn is in flight, and not
                    // under words its agent said after the turn ended (found looking at CONSOLE2).
                    turnRunning={talking && chatTurns[attended.id] ? turns.taking : undefined}
                    // The driver's word that its call waits for the person's answer (AUTONOMY1b3e).
                    callMark={grouping?.calling ?? undefined}
                    scroller={centre}
                    // *Back to bottom* is the waiting panel's to draw, above it (WAITPANEL1), not the transcript's foot.
                    onTail={setTail}
                    onUsage={onUsage}
                    onWaiting={onWaiting}
                    // Its limit's wait, read from its events (LIMITRESUME1d2): the head and the foot say it.
                    onLimitWait={(session, held) => setChatHeld((was) => (held
                      ? { session, held }
                      : was?.session === session ? null : was))}
                    // Each wait's raising event, and the way between them (WAITFOOT1c, D168 point 4).
                    waits={waits ? {
                      waits: standing, goAheads: waits.goAheads, opinion: opinionAsked(waits.landing)?.opinion ?? null,
                      proposals: rulesAsked(waits.rules?.proposals, attended.id),
                      helpers: waits.helpers?.asks,
                      permissions: waits.permissions?.waits,
                    } : undefined}
                    onAnchors={(session, state) => setAnchored({ session, ...state })}
                    // COMMS1h: each helper its agent asked for, a card at its call, the answer folded in, the helper's door on it.
                    helpersAsked={helperWaits ? { asks: helperWaits.asks, nameOf: helperWaits.nameOf, onOpen: helperWaits.onOpen } : undefined}
                    onToWait={(wait) => {
                      const anchor = anchorOf(wait);
                      if (anchor) goToWait(anchor);
                    }}
                    // Where the person's words went, a door to that session; and words that cannot go on here, one press
                    // that starts a conversation with them, where a chat can start here (MSG1f, D137 §3.1, §2.2).
                    onSession={(id) => attend(id)}
                    onStartFrom={rootOf(attended.repository) ? onStartFrom : undefined}
                    // SESSCTX1c part B: and beside it, the same session going on in a new part handed Daoris's brief.
                    onGoOnBrief={rootOf(attended.repository) ? onGoOnBrief : undefined}
                    // *Say it again* (COWORK1c): the person's own words that may have reached the agent before a restart, said anew.
                    onSayAgain={onSayAgain}
                    // Words its cooling account holds, and *Go on in a new session* with what it came to (MSG1g2, D137 §2.2).
                    cooling={coolsUntil ? {
                      until: coolsUntil,
                      quest: attended.quest,
                      onGoOnNew,
                      pending: goOnNew.isPending,
                      answer: newSession?.session === attended.id ? newSession.answer : null,
                    } : undefined}
                    reasons={{
                      from: attended.adapter,
                      to: harnesses.data?.adapter,
                      adapter: harnesses.data?.adapter,
                      agent: roster.find((row) => row.harness === attended.adapter)?.product || attended.adapter,
                    }}
                  />
                </div>
              </FileOpener.Provider>
            </div>
          )}
        </ViewMain>

        {/* The waiting panel (WAITPANEL1): the strip and the box, its top edge a handle that sizes what waits, kept per viewer;
            and the conversation's *To the top* and *Back to bottom* above that edge, never under it. */}
        <WaitPanel
          height={waitPanel.height}
          onResize={waitPanel.resize}
          onReset={waitPanel.reset}
          controls={attended && here && (awayFromTop || (tail && !tail.atTail)) ? (
            <>
              {awayFromTop && <ToTheTop onPress={toTop} className="bg-overlay shadow-sm" />}
              {tail && !tail.atTail && <BackToBottom onPress={tail.toTail} className="bg-overlay shadow-sm" />}
            </>
          ) : undefined}
        >
          {/* What waits on the person, where they read (WAITFOOT1b, D168): above the box and outside the conversation's scroll,
              each wait with the head's own press. None on a teammate's record, which has no press here (D47 §4). Keyed by the
              session, so a fold is its own. 🔴 The strip, the settler and the composer each name their kind beside the id (RELSPAM1):
              keyed by the id alone, they took each other's fibers, and the one left over was never unmounted. */}
          {/* A limit's wait (LIMITRESUME1d2, D206 §3.7): the head's line again where the reader is, aside while the head is on screen. */}
          {waitsOnScreen === false && limitLine('mb-2')}
          {attended && here && waits && (
            <WaitStrip
              key={`strip:${attended.id}`}
              {...waits}
              // The box below takes the park's answer: a driven park's answer box, or a parked chat's composer.
              answerBelow={answerBelow}
              workAsk={workAskAt('foot')}
              entry={entryAt('foot')}
              ruleAsk={ruleAskAt('foot')}
              toEvent={toEvent}
              seek={seekAt('foot')}
              // Not yet told is aside too, for the frame the observer takes to say: no strip flashed over a head on screen.
              aside={waitsOnScreen !== false}
            />
          )}

          {sending.lateTo && <SentSettler key={`settler:${sending.lateTo}`} sending={sending} giveBack={giveBackSent} />}

          {talking && attended && box.kind === 'chat' && (
            <Composer
              // Per session: the files attached in one conversation never follow the person to another.
              key={`composer:${attended.id}`}
              live={live}
              sending={send.isPending}
              refusal={refusal?.session === attended.id ? refusal.text : null}
              // One owner for the moves at a time (D56): while the session is parked the card above holds
              // finish and decline, and this form keeps `send` alone. Its session stop is the page
              // header's at every state (D126 §3.3); this form keeps *Finish* and *Stop turn*.
              endings={attended.state !== 'awaiting-person'}
              // *Answer…* from its row puts the focus here, where a parked chat is answered.
              focus={answerFocus?.session === attended.id ? answerFocus.at : 0}
              draft={draft}
              onDraft={setDraft}
              queued={turns.queued}
              // A parked conversation's waiting words are its record's, which it goes on with (SAIDBACK1b): no turn, no door.
              queuedLabel={attended.state === 'awaiting-person' ? t('work.say.waiting') : undefined}
              fold={attended.state === 'awaiting-person'}
              going={sending.going}
              taking={turns.taking}
              opening={turns.opening}
              // Only a door that can see a turn end can stop one; the roster says which (CONV3b).
              stoppable={roster.find((row) => row.harness === attended.adapter)?.structured === true}
              stopping={cancelTurn.isPending}
              onStopTurn={onStopTurn}
              levels={levels}
              onLevel={editQueued.level}
              onKeepingStop={keepingStop ? onKeepingStop : undefined}
              onRemove={editQueued.remove}
              onEdit={editQueued.reword}
              mentions={{
                files: treeFiles.data?.files ?? null,
                unlisted: treeFiles.data?.unlisted ?? 0,
                refusal: treeFiles.error ? sentence(treeFiles.error) : null,
              }}
              onMentioning={setMentioning}
              context={{
                usage: reading?.session === attended.id ? reading.usage : undefined,
                door: door(roster.find((row) => row.harness === attended.adapter)?.structured),
              }}
              offered={offered}
              optionsBusy={setOption.isPending}
              // The driver's refusal, or the agent's, reaches the person in its own words.
              onOption={(option, value) => setOption.mutate({ id: attended.id, option, value }, { onError: failure(notify) })}
              onSend={onSend}
              onFinish={() => end.mutate(attended.id, {
                onSuccess: () => notify(t('work.composer.ending', { id: attended.id })),
                onError: failure(notify),
              })}
            />
          )}

          {/* Every other box (MSG1f, D137 §5.1): the running door, the park's answer, the box where the same session goes
              on, and the line where nothing takes words. Keyed by session and box, so a draft's form starts fresh. */}
          {attended && box.kind !== 'chat' && box.kind !== 'none' && (
            <SessionBox
              key={`${box.kind === 'say' ? box.mode : box.kind}:${attended.id}`}
              box={box}
              quest={attended.quest}
              draft={draft}
              onDraft={setDraft}
              sending={say.isPending}
              refusal={refusal?.session === attended.id ? refusal.text : null}
              held={held ? { queued: held.queued, taking: held.taking } : undefined}
              ending={ending?.session === attended.id ? ending.words : []}
              // What waits on a park's or an ended session's record (SAIDBACK1b), as the driver lists it.
              waiting={box.kind === 'say' ? chatTurns[attended.id]?.queued ?? [] : []}
              going={sending.going}
              stopping={cancelTurn.isPending}
              // *Answer…* from its row, and *Send back…* from its review, put the focus here (D126 §3.1, D137 §5.1).
              focus={answerFocus?.session === attended.id ? answerFocus.at : 0}
              accepts={boxAccepts}
              onSend={onSay}
              levels={box.kind === 'steer' ? levels : undefined}
              onLevel={editQueued.level}
              onKeepingStop={box.kind === 'steer' && keepingStop ? onKeepingStop : undefined}
              onRemove={editQueued.remove}
              onEdit={editQueued.reword}
              goes={goes}
              onMove={onMoveWord}
              toHead={onDraftToHead}
              // The conversation it went on in, by its record's title (HANDWORDS1-ii); one the frame does not hold is said by its id.
              continued={box.kind === 'line' && box.why === 'continued' && box.to
                ? headLink((sessions.data ?? []).find((row) => row.id === box.to))
                : null}
              onOpenSession={(session) => attend(session.id)}
              // What the park asks, in its box with every answer a press the box's words go with (PARKVIEW1, the design §3-§4): its
              // question as its agent asked it, its go-aheads still open, by when and what it holds. Only where the box answers a park.
              park={answering ? {
                quest: attended.quest,
                tree: ownTree(attended, rootOf(attended.repository)),
                since: attended.updated,
                question,
                goAheads: openGoAheads(goAheads),
                busy: say.isPending || parkGoAhead.isPending,
                selected: parkPicked,
                onSelect: question ? onParkPick : undefined,
                onGoAhead: parkAsk ? onBoxGoAhead : undefined,
                seek: seekAt('box'),
                toEvent,
              } : undefined}
            />
          )}
        </WaitPanel>

        </>)}

        {/* An emptied panel that is hidden is not drawn at all, unless a view is being dragged to it;
            opened, it says how to fill it. */}
        {(panelViews.length > 0 || !collapsed || dragging !== null) && (
          <OutputPanel
            console={consoleView ? withWhose(consoleView, true) : null}
            height={Math.max(PANEL_MIN, height)}
            collapsed={collapsed}
            onResize={resize}
            onToggle={toggle}
            tabs={streamTabs}
            selected={shown ?? undefined}
            onStop={stopStream}
            onSelect={(key) => {
              pickStream(key);
              // Picking what to read is asking to read it: a hidden panel opens.
              if (collapsed) toggle();
            }}
            views={panelViews}
            view={panelView}
            onView={setPanelPick}
            onMove={moveView}
            onReset={placed.moved ? placed.reset : undefined}
            onDrag={onDrag}
          >
            {panelView && panelView !== 'console' ? surface(panelView) : undefined}
          </OutputPanel>
        )}
      </div>

      {/* The third column, built now that it has a second occupant (components plan §3a). It is
          keyed to the attended session like every other region, and it is what gives the centre
          column its height back — the timeline used to share that space with the composer and the
          panel. An emptied one that is closed is not drawn at all (DOCK1b), unless a view is being
          dragged to it (DOCK1e). */}
      {(rightViews.length > 0 || !dockClosed || dragging !== null) && (
        <RightDock
          tab={dock}
          onTab={setDock}
          views={rightViews}
          onMove={moveView}
          onReset={placed.moved ? placed.reset : undefined}
          onDrag={onDrag}
          preview={previewing ? { name: fileName(previewing.path), path: previewing.path, onClose: closePreview } : null}
          mode={layout.dock.mode}
          width={layout.dock.width}
          range={dockRange(width.viewport, width.frame, list?.beside ?? 0)}
          // Full because the window is narrow, not because the person asked: only widening undoes it.
          autoFull={layout.dock.mode === 'full' && !dockFull}
          onResize={resizeDock}
          onResetWidth={() => resizeDock(null)}
          onClose={() => closeDock(true)}
          onOpen={openDock}
          onFull={setDockFull}
        >
          {dock === 'preview' ? previewSurface : dock ? surface(dock, 'right') : null}
        </RightDock>
      )}
    </div>
  );
}

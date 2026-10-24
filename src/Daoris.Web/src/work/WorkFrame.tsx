import { type ComponentProps, type ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SessionConsole } from './console/SessionConsole';
import { sentence } from '../locales/format';
import { buildChain } from '../map/chain';
import type { Compose, OpenGroup } from '../app/opener';
import { SESSIONS_SLICE, useAnswerSession, useAsks, usePersonDone, useQuests, useRegistry, useSessions } from '../data/queries';
import { usePushedResyncing } from '../bridge/state/slices';
import {
  type TurnStop, useCancelTurn, useEditQueued, useEndChat, useHarnesses, useResolveSession, useSendMessage,
  NO_TURNS, useChatTurns, useSessionEntries, useSessionGroups, useSessionOpenings, useSessionOptions, useSessionSpent, useSessionStreams, useSessionWhere,
  useSetSessionOption, useStartChat, useStopTask, useSweepPlan, useTreeFiles, useTreeFile, useReviewedPatch,
  logEvent, useTerminals, useRemotes, useWorkPlan, useSay, useSessionReach, useStartFrom, useGoOnBrief, type WordsAnswer,
  useParkGoAhead, useAccounts, useGoOnNew, useTrace, useDiscardSessionBranch, useLanding, useLandSessionTree, useLandWithoutLook, useSessionDiff,
  useDiscardSessionTree, useDriver, useOpinionGate, useWorkflowRun, useRules, useSessionEvents, stoppedWaiting, useUnderWay,
  useEntryLanding, useLandEntry, useSessionHelpers, useSessionPartners, useUnderWaySubject, useSessionProcess, useWorkState, usePushedCalls, withCall,
  useAnswerPermission, usePermissionWaits, useSessionHandOverAsk, useApproveHandOver, useDeclineHandOver, type HandOverWord,
  useAnswerWaits, useAnswerAlone, useRunNodeWord, useSessionQuestion, useStopWaiting,
} from '../bridge/shell';
import { handOverRefused, type HandOverAskWait } from './waits/HandOverAgentAsk';
import { permissionSaid, type PermissionWaits } from './waits/PermissionAsks';
import { answerAloneSaid, type AnswerWaits } from './waits/AnswerAlone';
import { type Sent, useSending, useSettled } from './conversation/sending';
import { WorkflowRunView } from '../workflow/WorkflowRunView';
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
import { doorOf, toolOf } from '../agents/tools';
import { agentOf, machineScope } from '../settings/accounts/accounts';
import { namer } from '../settings/accounts/namer';
import { accountNames } from './accountNames';
import { type ChatLimitHeld, chatLimitWaitSaid, limitedLine, limitWaitOf, limitWaitSaid } from './waits/limitWait';
import { LimitWaitLine } from './waits/LimitWaitLine';
import {
  boxOf, coolingFor, goOnBriefRefusal, levelsOf, NATIVE_WORDS_LIMIT, neverSentence, type NewSessionAnswer, startFromRefusal, takesWords, tooLong,
} from './say';
import { SessionBox } from './conversation/SessionBox';
import { askOf, pauseAsk, wiredFor, type WorkTarget } from './pausing/pausing';
import type { GoAheadAnswer } from '../asks/GoAheadList';
import { goAheadsAsked, goAheadToast } from './waits/parkGoAheads';
import { afterPress, choiceWords, openGoAheads } from './waits/parkQuestion';
import { PauseAsk } from './pausing/WorkAsks';
import { FilePreview } from './review/FilePreview';
import { type FileOpen, FileOpener, fileName } from './review/preview';
import { TerminalView } from './terminals/TerminalView';
import { answeredPark, failure, type Notify, SESSION_ACTIVE, useErrorNotify } from '../design/ui';
import type { FrameIntent } from '../menus/commands';
import { AttendedSession, noteIsInTheHead } from './AttendedSession';
import { type AnchorState, BackToBottom, SessionConversation, type TailState, ToTheTop } from './conversation/SessionConversation';
import { type EarlierPart, EarlierParts } from './conversation/EarlierParts';
import { ancestryOf } from './conversation/line';
import { useAwayFromTop } from './conversation/followTail';
import type { Level, RunningCall, Usage } from './conversation/conversation';
import { useNow } from './useNow';
import { useActors, useHeavySlot } from '../bridge/driver';
import { heavyPartOf } from './heavySlot';
import { heavyLine, heavyRow } from './HeavySlotLine';
import { askName, isHelper, isIntake, isOpinion, linkedSessions, ownTree, sessionOrigin, sessionTitle } from './identity';
import { doorLabel } from '../agents/tools';
import type { QuestClose, Resolution } from './waits/AwaitingPerson';
import { Composer } from './conversation/Composer';
import { DiffPane } from './review/DiffPane';
import { keepDraft, useDraft } from './conversation/drafts';
import { dockOpensOn, type DockTab, followed, NothingFollowed, RightDock } from './frame/RightDock';
import { SessionTimeline } from './head/SessionTimeline';
import { SessionRail } from './rail/SessionRail';
import { type ActFacts, actMenu, headerActs, offeredActs, primaryAct, stopAsk } from './acts';
import { askOfEntry, entryOfSession, isIntakeEntry, nameOfEntry } from './entries';
import { useAccountUse, useChannel, useForwardChannel, usePostChannel } from '../bridge/shell';
import { keptSessionFilters, sessionFilters, shownOf } from './groups';
import { leadingAgentOf, type SessionFactsOf, sessionFacts, sessionFactsTip } from './head/headFacts';
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
import { AddProjectDrawer, ImportFolderDrawer } from '../projects/ProjectManage';
import { useCheckoutBranch } from '../bridge/repositories/git';
import { type CheckoutReader, NewConversation, type NewStart } from './start/NewConversation';
import { keepChosen, keepRecent, mergeRecent, readChosen, readRecent } from './start/startRecent';
import { type Carry, NO_CARRY } from '../compose/carry';
import { OutputPanel, PANEL_MIN, StreamTabs } from './frame/frame';
import { panelTabs } from './console/streams';
import { DOCK, dockRange, frameLayout, type ListMode } from './frame/layout';
import { type FrameClosings, useFrameClosings } from './frame/closings';
import { type ListPanes, useListPanes } from './frame/listPanes';
import { listChoice, type ListSpec, useFrameWidth, useGoOver, useListMode, type ViewLayout, ViewListPane } from './frame/ViewFrame';
import { ViewMain } from './frame/ViewMain';
import { type Place, type Placements, usePlacements, type ViewId, viewsIn } from './frame/placements';
import { relationsOf } from './head/relations';
import { beganLine } from './head/partsLife';
import { store, stored } from '../lib/stored';
import { cn } from '../lib/cn';

// Per-viewer conveniences, like the language and the workspace scope (D42): a remembered layout is
// a preference, never machine wiring and never a tracked file. A view's list keeps its own since FRAME1c
// (`listPanes.ts`, D118 §3f).
const PANEL_HEIGHT = 'daoris.panelHeight';
// The dock's dragged width, as a SHARE of the window (LAYOUT1). `daoris.dockWidth` held pixels, and a
// dock kept in pixels stayed the same while the window grew; one held there is read once, as its share
// of the window it is read in, and then forgotten.
const DOCK_SHARE = 'daoris.dockShare';
const DOCK_WIDTH_PIXELS = 'daoris.dockWidth';

/** The dock's share of the window the person dragged it to, or null where they never did. */
function rememberedShare(): number | null {
  const share = Number(stored(DOCK_SHARE));
  if (Number.isFinite(share) && share > 0 && share < 1) return share;

  const pixels = rememberedWidth(DOCK_WIDTH_PIXELS);
  if (pixels === null || window.innerWidth <= 0) return null;
  const converted = Math.min(pixels / window.innerWidth, DOCK.cap);
  store(DOCK_SHARE, String(converted));
  store(DOCK_WIDTH_PIXELS, null);
  return converted;
}

function remembered(key: string, fallback: number): number {
  const held = Number(stored(key));
  return Number.isFinite(held) && held > 0 ? held : fallback;
}

/** A width the person dragged, or null where they never did — which is not the same as a default. */
function rememberedWidth(key: string): number | null {
  const held = Number(stored(key));
  return Number.isFinite(held) && held > 0 ? held : null;
}

/**
 * The composer's tree chip reads the checkout's branch through this (NEW1e-b): a refusal or a pending read is no answer, and
 * the chip says the checkout plain. A module-level hook, so the molecule's prop is stable.
 */
const readCheckout: CheckoutReader = (repository, workspace) => {
  const { query } = useCheckoutBranch({ repository, workspace });
  return { read: query.isError ? undefined : query.data, refresh: () => { void query.refetch(); } };
};

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
  const [refusal, setRefusal] = useState<{ session: string; text: string } | null>(null);
  // What *Go on in a new session* came to (MSG1g2), the session's own, as a refusal is: said under its words alone.
  const [newSession, setNewSession] = useState<{ session: string; answer: NewSessionAnswer } | null>(null);
  // *New conversation* (NEW1b, D162 point 1): bumped by each of its doors (the list's ＋, the menu, Ctrl+N, the palette),
  // which puts the attended session down and the focus in the composer's prompt.
  const [composing, setComposing] = useState(0);
  // Whether that press's focus is still owed: met once the prompt took it, so a composer that comes back by another path
  // (the attended session deleted) takes none.
  const [focusOwed, setFocusOwed] = useState(false);
  // The repository of the session attended at the press (NEW1f): the press puts the selection down, so the composer is told
  // where the person was. Each press sets it afresh, to null where nothing was attended.
  const [pressedFrom, setPressedFrom] = useState<string | null>(null);
  const newConversationPressed = () => {
    setPressedFrom(attended && sessionOrigin(attended) === null ? attended.repository : null);
    setComposing((count) => count + 1);
    setFocusOwed(true);
  };
  // The composer's words and files, held here so a visit to a session and back keeps them; a start that opened a
  // conversation lets them go. Its last refusal is said under Start until the next press (UX5 U68).
  // The drawers the composer's picker opens over Sessions (NEW1c): Projects' own, on this frame's state, so the draft stays where
  // it is and App's views are not asked to navigate. `pickNew` and `reveal` are the answers it hands the composer, each a counted
  // ask; `refocus` is bumped when a drawer goes down, so the prompt takes the focus Radix would return to a press that is gone.
  const [adding, setAdding] = useState(false);
  const [importing, setImporting] = useState(false);
  const [pickNew, setPickNew] = useState<{ repository: string; n: number } | null>(null);
  const [reveal, setReveal] = useState<{ workspace: string; n: number } | null>(null);
  const [refocus, setRefocus] = useState(0);
  // An import that opened the picker keeps the focus in its field: the drawer's closing must not take it back to the prompt.
  const listOpened = useRef(false);
  const [newDraft, setNewDraft] = useState('');
  const [newCarry, setNewCarry] = useState<Carry>(NO_CARRY);
  const [startRefusal, setStartRefusal] = useState<string | null>(null);
  // This machine's last starts and the repository last chosen (the entry design §1), read once and kept as they change.
  const [recentStarts, setRecentStarts] = useState(readRecent);
  const [chosenStart, setChosenStart] = useState(readChosen);
  // Kept in step with the storage, which `chosenStart` alone would leave stale within a mount (NEW1f).
  const chooseStart = (repository: string) => {
    setChosenStart(repository);
    keepChosen(repository);
  };
  // One press, one request id (NEW1a): a press again with the same words and choices after a failure carries the same id,
  // so a start that did open is answered rather than repeated; an answer lets it go.
  const pressed = useRef<{ key: string; id: string } | null>(null);
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
  // Which dock surface each session has up (FRAME6: tabs per session). Not remembered across launches:
  // unlike the attended session, this one is answered by what the person is doing in the next ten seconds.
  const [docked, setDocked] = useState<Record<string, DockTab>>({});
  const [height, setHeight] = useState(() => remembered(PANEL_HEIGHT, 200));

  // The frame's columns (FRAME6): what the person chose, and what the window leaves room for.
  const root = useRef<HTMLDivElement>(null);
  const width = useFrameWidth(root);
  const [dockShare, setDockShare] = useState(rememberedShare);
  const ownLists = useListPanes();
  const listed = lists ?? ownLists;
  // What the person closed (DOCK1c): the application's where it holds them, so the strip's toggles and
  // the View menu reach them from every view; this frame's own where it is rendered alone.
  const own = useFrameClosings();
  const closed = closings ?? own;
  // Where each view stands (DOCK1b): the application's where it holds them, this frame's own alone.
  const ownPlaces = usePlacements();
  const placed = placements ?? ownPlaces;
  const collapsed = closed.panel;
  const dockClosed = closed.dock;
  const [dockFull, setDockFull] = useState(false);
  // Another view in the frame (DOCK1a): its own list or none, and its main area. Absent, it is Sessions.
  const elsewhere = viewLayout !== undefined;
  const listView = elsewhere ? viewLayout.list?.view ?? null : 'sessions';
  // A go's group lays the view's strip over the main area while it is brought into view (ENTRY1g).
  const { listOver, setListOver } = closed;
  const go = useGoOver(group, listOver, setListOver, onGroupBrought);
  const layout = frameLayout(width.viewport, width.frame, {
    list: listView ? listChoice(listView, listed, go.over) : null,
    dockShare, dockClosed, dockFull,
  });
  const list = layout.list;

  // What the list is now, told to the application, whose doors toggle it and say whether it is shown; and
  // a list laid over the main area let go once the room makes it anything else (D118 §3f).
  useListMode(list?.mode ?? null, onListMode, listOver, setListOver, go.bringing);

  /** A choice in a list laid over closes it (D118 §3a), and lets a go still on its way go (ENTRY1g). */
  const chosen = () => { if (list?.mode === 'over') go.setOver(false); };
  /** A drag lands as the dock's share of the window it was dragged in; null is the default again. */
  const resizeDock = (next: number | null) => {
    const share = next === null || width.viewport <= 0 ? null : next / width.viewport;
    setDockShare(share);
    store(DOCK_SHARE, share === null ? null : String(share));
  };
  const closeDock = (shut: boolean) => {
    closed.setDock(shut);
    if (shut) setDockFull(false);
  };

  const sessions = useSessions(null, true);
  const sessionsResyncing = usePushedResyncing(SESSIONS_SLICE);
  // A conversation's name (RAIL1): the same answer the rail asks for, from the same list, so the head
  // and the row read one name and the bridge is asked once.
  const openings = useSessionOpenings(sessions.data);
  // Where the attended session is listed, and where its work is (SESSUX1d): the rail's own two answers, asked under the
  // same keys, so the page header and its row are offered one set of acts and the bridge is asked once.
  const groups = useSessionGroups();
  // The entries beside them (SESSONE1c, D167): the attended session's parts, and what an archive reaches. The same answer.
  const entries = useSessionEntries().data;
  const where = useSessionWhere(sessions.data);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const harnesses = useHarnesses();
  // Each account's cool-off (TOOL4g), which says when words a cooling account holds go on (MSG1g2).
  const accounts = useAccounts();

  const startChat = useStartChat();
  const resolve = useResolveSession();
  // The person's done on the quest a finish at a checkpoint leaves (QUESTCLOSE1): the service's own door for it.
  const personDone = usePersonDone();
  const answer = useAnswerSession();
  const send = useSendMessage();
  const end = useEndChat();
  const cancelTurn = useCancelTurn();
  // Which workspaces sync with a remote here: a pause's ask names an open quest another machine may still take (PAUSE1e).
  const wiring = useRemotes().data;

  useErrorNotify(sessions.error, notify);

  const attended = (sessions.data ?? []).find((session) => session.id === selected) ?? null;
  // Its ask's channel (SESSONE2c, D167 points 6 to 8): the attended line of work's panel, where an ask began it. The same read the
  // ask's page holds, under one key; a conversation, an intake and a line rooted in a quest alone have none.
  const attendedEntry = attended ? entryOfSession(entries, attended.id) : null;
  const channelAsk = attendedEntry && !isIntakeEntry(attendedEntry) ? askOfEntry(attendedEntry) : null;
  // What each of its parts spent (SESSCTX1d), on each part's own row of the strip, from this machine's record.
  const spent = useSessionSpent(attendedEntry?.parts.map((part) => part.session));
  const channel = useChannel(channelAsk);
  const postChannel = usePostChannel();
  const forwardChannel = useForwardChannel();
  const editQueued = useEditQueued(attended?.id ?? null);
  const quest = attended?.quest
    ? (quests.data ?? []).find((row) => row.id === attended.quest) ?? null
    : null;
  const live = attended ? SESSION_ACTIVE.has(attended.state) : false;
  // What the attended session's own tree left (SESS1 S10): its branch in the clean-up's list, found by
  // the name the trees give it — `daoris/` and the tree's folder.
  const sweep = useSweepPlan();
  const branch = attended?.tree && Array.isArray(sweep.data?.branches)
    ? sweep.data.branches.find((row) => row.repository === attended.repository
      && row.branch === `daoris/${attended.tree!.split(/[\\/]/).filter(Boolean).pop()}`) ?? null
    : null;
  // That branch discarded once its tree is gone, a failed attempt's (LAND3b): `daoris-driver trees remove … --force`.
  const discardBranch = useDiscardSessionBranch();

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

  // A conversation is the only thing there is anything to say to. A driven session also holds a
  // tree, but it was given its whole target at once and has no channel to speak into — an input
  // box nothing is listening to is worse than none (the reasoning Projects used to carry).
  const conversation = attended?.kind === 'chat';
  // A teammate's record came down with the sync (SYNC4) and its process is on THEIR machine: it is
  // read here, and nothing this window sends could reach it — so it gets no moves and no composer.
  const here = attended ? sessionOrigin(attended) === null : false;
  // An intake takes no messages at all (INT4b, INT4g, INT4h): it is one turn, framed as one prompt.
  // Parked, there is no process left to hear one, and its answer is on the ask. Running, the pipe
  // door gave it no stdin and on the protocol door its stdin is the driver's own frames — so a box
  // there sent a person's words into nothing, or into the middle of the JSON-RPC stream. The driver
  // refuses such a line too; the frame offers no box, and the page header carries the stop (D126 §3.3).
  const intake = attended ? isIntake(attended) : false;
  // So does a second opinion's reviewer (XAGENT1g), and a helper (MIXAGENT3a-i, D163): one turn and no words, which its
  // record's host refuses besides.
  const oneTurn = intake || (attended ? isOpinion(attended) || isHelper(attended) : false);
  // The process its own tree runs under, for its *Details* (PROCESS1e): this machine's own record, never an intake's, which is told
  // none; the driver answers none for a session with no tree of its own here.
  const told = useSessionProcess(attended && here && !intake ? attended.id : null);
  // The go-aheads a park asked on its quest's ask (KNOWUSE1a2, D135 §2), read only for a driven park whose quest an ask
  // asked: shown in its head, where answering the last one open sends it on (GOAHEAD2b). The asks are asked for nowhere
  // else here.
  const parkAsk = attended?.state === 'awaiting-person' && !intake ? askOf(quest) : null;
  const asks = useAsks(false, parkAsk !== null);
  // The ask its entry came from, for the head's name: the rail's read, so it costs nothing it does not already ask.
  const entryAsks = useAsks(true, channelAsk !== null);
  const goAheads = attended && parkAsk ? goAheadsAsked(asks.data, parkAsk, attended.id) : [];
  const parkGoAhead = useParkGoAhead();
  // PARKVIEW1 (the park's ask design §2-§3): its question as its agent asked it and the driver kept it for this park, read only for a
  // driven park of this machine's still waiting on the person. Drawn in its box with its answers as presses, and in its head's card.
  const parkWaits = Boolean(attended && here && !intake && attended.kind !== 'chat' && attended.state === 'awaiting-person'
    && !answeredPark(attended));
  const parkQuestion = useSessionQuestion(parkWaits ? attended!.id : null);
  const question = parkWaits ? parkQuestion.data ?? null : null;
  const talking = Boolean(attended && conversation && here && !oneTurn);
  // A driven session still working may be told something (SESS3): its words are held and are its next prompt. Offered only
  // where the driver says it listens, which is the protocol door; the pipe door has nothing to hear it.
  const steerable = Boolean(attended && here && !oneTurn && !conversation && attended.quest
    && SESSION_ACTIVE.has(attended.state) && attended.state !== 'awaiting-person');

  // What the person was typing to this session, kept per session and across a reload (CONV4b): one draft for whichever
  // box its page offers, so words typed as it ends are still there in the box that goes on with them (MSG1f).
  const [draft, setDraft] = useDraft(attended && here && !oneTurn ? attended.id : null);
  // Where each live conversation's turns stand, as the driver holds them: the attended one's stop and
  // queue follow it, not the record, which learns a turn began only when its first event lands
  // (CONV4a); and the rail and the head read a chat between turns as idle (UX5 U17).
  const liveChats = (sessions.data ?? [])
    .filter((session) => session.kind === 'chat' && SESSION_ACTIVE.has(session.state))
    .map((session) => session.id);
  // A driven session of this machine's that parked or ended, whose record the person's words may wait on (SAIDBACK1b): the
  // driver lists them as its queue, each by its id, for the box's *Edit* and *Remove*.
  const resting = Boolean(attended && here && !oneTurn && !conversation
    && (attended.state === 'awaiting-person' || !SESSION_ACTIVE.has(attended.state)));
  const chatTurns = useChatTurns(talking || steerable || resting ? [...liveChats, attended!.id] : liveChats);
  const turns = (talking && chatTurns[attended!.id]) || NO_TURNS;
  // What the driver holds for the attended driven session, and whether it hears anything at all.
  const held = steerable ? chatTurns[attended!.id] : undefined;
  const steering = held?.listening === true;
  // The box at the foot (MSG1f, D137 §5.1): on every session that takes words, by what a word said now would do, and the
  // line saying why on the rest. 🔴 A driven session parked to ask the person is answered from it, where a chat's is: the
  // door was a button at the top of a record of 1,800 events, and the question is read at its foot. The card above keeps
  // the endings and says the box carries it on: one owner for the answer (D56). Answered, a second word joins the first
  // (D137 §2.4), so the box stays.
  const reach = useSessionReach(attended && here && !oneTurn ? attended : null, steering);
  const box = boxOf({ session: attended, here, intake, listening: steering, reach });
  // What the attended session's door does with a word while a turn runs (COWORK1a, D188): the levels its box offers, and the
  // keeping stop where its door can stop a turn keeping the session. Nothing known, none: the box queues as it did.
  const levels = levelsOf(reach);
  const keepingStop = reach?.keepingStop === true;
  const answering = box.kind === 'say' && box.mode === 'answer';
  // QUESTIONUI1a (the supervisor design §8.4): the answer selected in the park's question, the frame's beside the draft, in memory, so a
  // reload starts with none and the words kept. Drawn selected only on the question it was selected on: a replaced question is drawn
  // with none, so a kept label never answers a question the park no longer asks.
  const [parkChoice, setParkChoice] = useState<{ session: string; question: string; label: string } | null>(null);
  const parkPicked = attended && answering && question && parkChoice?.session === attended.id && parkChoice.question === question.id
    && question.choices.some((choice) => choice.label === parkChoice.label) ? parkChoice.label : null;
  const onParkPick = (label: string | null) => {
    setParkChoice(label !== null && attended && question ? { session: attended.id, question: question.id, label } : null);
  };
  // Words said as the attended session winds up, held for its record to end (D137 §2.1), shown above its box until then.
  const [ending, setEnding] = useState<{ session: string; words: string[] } | null>(null);
  useEffect(() => {
    if (!attended || ending?.session !== attended.id || !SESSION_ACTIVE.has(attended.state)) setEnding(null);
  }, [attended, ending?.session]);
  // The person's words on their way to the attended session (SENDLOST1b, D137 §2.4): shown the moment they are sent, kept
  // where the page stopped waiting on their send, and settled by its own queue and record (`SentSettler`, which reads it
  // only while some wait). A chat that ended without them gives them back, said so; a driven session keeps words once they
  // arrive.
  const sending = useSending(attended && here && !oneTurn
    ? { id: attended.id, queued: chatTurns[attended.id]?.queued, endedChat: conversation && !live, refused: box.kind === 'line' }
    : null);
  const giveBackSent = (sent: Sent) => {
    setDraft((was) => [sent.words.text, was.trim()].filter(Boolean).join('\n\n'));
    if (sent.words.files.length > 0) {
      notify(t('work.composer.filesNotSent', { count: 1, names: sent.words.files.join(', ') }), 'error');
    }
    // SENDLOST1c: where the driver's refusal came after the page stopped waiting, the box is now a line, so the words are
    // said under it as well as kept for the box.
    setRefusal({
      session: sent.session,
      text: box.kind === 'line' ? t('work.composer.notSentLine', { text: sent.words.text }) : t('work.composer.notSent'),
    });
  };
  const taking = Object.fromEntries(Object.entries(chatTurns).map(([id, held]) => [id, held.taking]));
  // When each live chat's last turn ended here (RAIL2): its *moved*, which its record never says.
  const lastTurns = Object.fromEntries(Object.entries(chatTurns).flatMap(([id, held]) => (held.lastTurn ? [[id, held.lastTurn]] : [])));
  // The conversation's model and effort as its agent offered them (AGT6b, D98) — asked of a live
  // conversation only, since only a live process has options to change.
  const offered = useSessionOptions(talking && live ? attended!.id : null);
  const setOption = useSetSessionOption();
  // The tree's files for `@` (CONV4d), asked for only while the person is writing a mention.
  const [mentioning, setMentioning] = useState(false);
  const treeFiles = useTreeFiles(talking ? attended!.id : null, mentioning);
  // The conversation's context reading, as its record says (CONV5) — told by the conversation, and
  // kept with the session it came from, so switching never shows one session's ring under another.
  const [reading, setReading] = useState<{ session: string; usage?: Usage } | null>(null);
  const onUsage = useCallback((session: string, usage?: Usage) => setReading({ session, usage }), []);
  // When the attended session's agent was asked and has answered nothing (WAITSHOWN1), likewise told by the conversation.
  // AGENTERRORS1a2: and when a transient episode tries again, while one of its waits runs.
  const [waiting, setWaiting] = useState<{ session: string; since?: string; calling?: RunningCall; retrying?: string } | null>(null);
  const onWaiting = useCallback(
    (session: string, since?: string, calling?: RunningCall, retrying?: string) => setWaiting({ session, since, calling, retrying }), []);
  // The head's *running … for …* steps with the clock (WAITSAY1), only while a call is named; and *Trying again at* leaves once past.
  useNow(waiting?.calling || waiting?.retrying ? 15_000 : null);

  // Cleared only when the record it pointed at is gone entirely. A session that ENDED stays
  // attended, because the person is very likely reading exactly that. 🔴 And only a record this
  // frame has SEEN can be gone: a session just started is attended before the list has caught up
  // with it, and clearing on that first answer left "Nothing attended" beside a running chat (CONV3).
  const seen = useRef(new Set<string>());
  useEffect(() => {
    if (!sessions.data) return;
    for (const session of sessions.data) seen.current.add(session.id);
    if (selected && seen.current.has(selected) && !sessions.data.some((session) => session.id === selected)) {
      onSelect(null);
    }
  }, [selected, sessions.data, onSelect]);

  const attend = (id: string) => {
    onSelect(id);
    setRefusal(null);
  };
  // How the attended session came to be (TRACE1b): read only while its section is open, for the session it was opened
  // on, since the read takes every session record and quest. Attending another opens it folded.
  const [tracing, setTracing] = useState<string | null>(null);
  const trace = useTrace('session', tracing);
  // A stop's ask is a question asked now, of the session it was asked of (D126 §3.3): attending another by any door
  // puts it down, so coming back later does not find it still open.
  useEffect(() => {
    setStopAsking((asked) => (asked === selected ? asked : null));
    setDeleteAsking((asked) => (asked === selected ? asked : null));
    setHandOverAsking((asked) => (asked === selected ? asked : null));
    setHelperAsking((asked) => (asked === selected ? asked : null));
    setPauseAsking((asked) => (asked?.session === selected ? asked : null));
  }, [selected]);

  const resize = (next: number) => {
    setHeight(next);
    store(PANEL_HEIGHT, String(next));
  };

  const toggle = () => closed.setPanel(!collapsed);

  // *New conversation* puts the attended session down, so the composer stands in the main area (NEW1b).
  useEffect(() => {
    if (composing > 0 && selected !== null) onSelect(null);
    // Only a new press: the selection is read, not watched.
  }, [composing]);

  const onStart = (start: NewStart) => {
    setStartRefusal(null);
    const key = JSON.stringify([
      start.repository, start.text, start.adapter ?? '', start.profile ?? '', start.ownTree ?? null,
      start.files.map((file) => [file.name, file.size, file.lastModified]),
    ]);
    if (pressed.current?.key !== key) pressed.current = { key, id: crypto.randomUUID() };
    const requestId = pressed.current.id;
    startChat.mutate({ ...start, requestId }, {
      onSuccess: (result) => {
        pressed.current = null;
        // The ledger's own sentence: the tree is busy, the repository has no checkout here, the
        // harness is missing, the chosen account is logged out. Each names the action that fixes
        // it, which is why it is shown rather than summarised — whole, under Start where it was
        // pressed, with the words kept; it was a corner toast cut mid-sentence (UX5 U68).
        if (!result.sessionId) {
          setStartRefusal(result.message);
          return;
        }
        const session = result.sessionId;
        setRecentStarts(keepRecent(start.repository));
        setNewDraft('');
        setNewCarry(NO_CARRY);
        // Opened without the words (NEW1a's `sent: false`): they wait in its box, put there before it is attended so its
        // draft is read with them, and the driver's sentence stands beside them. The files are named, as a send handed
        // back names them: the page no longer holds their bytes.
        if (!result.sent) keepDraft(session, start.text);
        attend(session);
        if (!result.sent) {
          setRefusal({ session, text: result.message });
          if (start.files.length > 0) {
            notify(t('work.composer.filesNotSent', { count: 1, names: start.files.map((file) => file.name).join(', ') }), 'error');
          }
        }
      },
      // No answer came: the bridge's sentence where Start was pressed, and the press keeps its id for the next.
      onError: (error: unknown) => setStartRefusal(sentence(error)),
    });
  };

  // Counted into the machine log once the words went, never their words (LOG1b): their length, how many files they carried,
  // and where they reach the session as the driver answered (MSG1d, D137 §3.3).
  const counted = (session: string, kind: string, text: string, files: number, said: WordsAnswer) =>
    logEvent('message.sent', { session, kind, length: text.length, files, ...(said.reaches ? { reach: said.reaches } : {}) });

  // Words nothing took, in the page's words: by the driver's code where it gave one (MSG1d), else that nothing took them.
  const untaken = (said: WordsAnswer, fallback: string) =>
    (said.why ? neverSentence(t, said.why, { quest: attended?.quest, to: said.to }) : fallback);

  // 🔴 A send the page stopped waiting on (the bridge's TIMEOUT) may still be taken by the machine (SENDLOST1b): its words
  // are kept above the box, saying so, never put back in it, where a second send would say them twice, and the sessions are
  // read again; the session's own queue and record settle them. Any other failure lets them go, for the caller to give back.
  const stillTaking = (n: number, error: unknown) => {
    if (!stoppedWaiting(error)) {
      sending.answered(n);
      return false;
    }
    sending.unanswered(n);
    void sessions.refetch();
    return true;
  };

  // What words at *now* came to (STEER2b, COWORK1a), said where the person pressed it: one line that the door could not, and
  // where the words go instead. Taken at once in the running turn says nothing here, since the conversation shows the words
  // where they were taken. No turn is stopped for them (D188 point 3): the keeping stop is the person's own press.
  // `now: false` carries one of three codes (STEER2a): `no-interrupt`, said by where the words go instead, `behind` and
  // `opening`. A code not known here gets the generic line. `now: true` with no reach is an ordinary send (no turn was running).
  const nowSaid = (result: WordsAnswer) => {
    if (result.now !== false) return;
    notify(t(result.why === 'opening' ? 'work.steer.notNowOpening'
      : result.why === 'behind' ? 'work.steer.notNowBehind'
      : result.why === 'no-interrupt' ? (result.reaches === 'next-step' ? 'work.steer.notNowNextStep' : 'work.steer.notNowTurnEnd')
        : 'work.steer.notNow'));
  };

  const onSend = (text: string, files: File[] = [], level?: Level) => {
    if (!attended) return;
    const session = attended.id;
    // 🔴 The composer lets go of the words when it sends; a send that did not arrive hands them back,
    // into THIS session's draft (the setter is bound to it), and names the files — the page no longer
    // holds their bytes. Losing a paragraph to a refusal is the failure the composer exists to prevent (REV3).
    const giveBack = () => {
      if (text) setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
      if (files.length > 0) {
        notify(t('work.composer.filesNotSent', { count: 1, names: files.map((file) => file.name).join(', ') }), 'error');
      }
    };
    // Shown at once, as sending, until the answer says what came of them (SENDLOST1b).
    const n = sending.begin(session, { text, files: files.map((file) => file.name) });
    send.mutate({ id: session, text, files, ...(level ? { level } : {}) }, {
      // False is an answer: the session ended while they were typing. It lands on the composer
      // rather than in a toast, because that is where the person is looking — on THAT session's.
      onSuccess: (result) => {
        sending.answered(n);
        if (result.sent) {
          counted(session, 'chat', text, files.length, result);
          setRefusal(null);
          if (level === 'now') nowSaid(result);
          return;
        }
        giveBack();
        setRefusal({ session, text: untaken(result, t('work.composer.notListening')) });
      },
      onError: (error: unknown) => {
        if (stillTaking(n, error)) return;
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };

  // A door that takes fewer characters at once than these says so before anything is sent, and the words stay in the box
  // (D137 §2.4): a driven session on the native door, whose resumed run takes them as one argument.
  // Why these words are not sent, or null: the box's refusal and the card's alike (PARKANSWER1).
  const unaccepted = (text: string): string | null => {
    // PARKVIEW1 (the park's ask design §4): where its question waits beside go-aheads still open, the go-aheads come first, each
    // keeping the park for this answer, which then sends it on with them; the words stay in the box, said why.
    if (attended && answering && question && openGoAheads(goAheads).length > 0) return t('work.park.goAheadsFirst');
    if (!attended || !tooLong(text, { door: doorOf(attended.adapter, roster), kind: attended.kind })) return null;
    return t('work.say.tooLong', { count: NATIVE_WORDS_LIMIT });
  };
  const accepts = (text: string) => {
    const why = unaccepted(text);
    if (why && attended) setRefusal({ session: attended.id, text: why });
    return why === null;
  };
  // The box's judge (QUESTIONUI1a): its words with the answer selected, as they would go; refused alike for a choice and free words while
  // the question's go-aheads are open, through Enter and *Send* (Codex's round-2 finding 14).
  const boxAccepts = (text: string) => accepts(parkPicked ? choiceWords(parkPicked, text) : text);

  // The person's words through the box at the foot (MSG1f, D137 §5.1): a working driven session's running door (SESS3), a
  // park's answer, a session that ended, or one winding up. A send that did not arrive gives the words back to the box, as
  // a message does (REV3).
  const say = useSay();
  // The rules agents proposed (PERM2, D74), the band's own read, and their answer's one owner: the attended session's own open
  // ones are a wait where the person talks to it (PROPOSEHERE1b, D168 point 2). The machine's file, so a browser has none.
  const ruleState = useRules();
  const ruleAnswer = useRuleAnswer({ notify });
  const onSay = (text: string, level?: Level) => {
    if (!attended || box.kind === 'none' || box.kind === 'line' || box.kind === 'chat') return;
    // QUESTIONUI1a: the box's *Send* with an answer selected sends it, then the words, naming its question.
    if (parkPicked && question) { onChoice(parkPicked, question.id, text); return; }
    if (!text) return;
    const session = attended.id;
    const kind = box.kind === 'steer' ? 'steer' : box.mode === 'answer' ? 'answer' : 'say';
    const winding = box.kind === 'say' && box.mode === 'ending';
    const giveBack = () => setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
    const n = sending.begin(session, { text, files: [] });
    say.mutate({ id: session, text, ...(level ? { level } : {}) }, {
      onSuccess: (result) => {
        sending.answered(n);
        if (!result.sent) {
          giveBack();
          setRefusal({ session, text: untaken(result, t('work.say.notTaken')) });
          return;
        }
        counted(session, kind, text, 0, result);
        setRefusal(null);
        if (level === 'now') nowSaid(result);
        if (kind === 'answer') notify(t('work.awaiting.answered'));
        // Held until its record ends, and shown above the box meanwhile: the conversation shows them once kept.
        if (winding) setEnding((was) => ({ session, words: [...(was?.session === session ? was.words : []), text] }));
      },
      onError: (error: unknown) => {
        if (stillTaking(n, error)) return;
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };
  // *Say it again* (COWORK1c, the message-urgency design §6, §8.2): the person's own words that may have reached the agent before a
  // restart were never sent again; this says them anew, a new word, as the person's default. What the session would not take goes back
  // to the composer, said why, as a word said there does.
  const onSayAgain = (words: string[]) => {
    if (!attended || words.length === 0) return;
    const session = attended.id;
    const text = words.join('\n\n');
    const giveBack = () => setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
    say.mutate({ id: session, text }, {
      onSuccess: (result) => {
        if (result.sent) return;
        giveBack();
        setRefusal({ session, text: untaken(result, t('work.say.notTaken')) });
      },
      onError: (error: unknown) => {
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };
  // *Start a conversation with these words* (MSG1f, D137 §2.2): a new chat in the repository, its first message the words
  // this session could not go on with, attended once it opens. One act of the driver's (MSG1f2), which reads the words off
  // the record and takes them off it, so the words the page shows are not sent. A refusal is said by its code; the driver's
  // sentence when none could start is said whole.
  const startFrom = useStartFrom();
  const onStartFrom = () => {
    if (!attended || startFrom.isPending) return;
    startFrom.mutate({ session: attended.id }, {
      onSuccess: (started) => {
        if (started.why) {
          notify(startFromRefusal(t, started.why, { quest: attended.quest }), 'error');
          return;
        }
        if (!started.sessionId) {
          notify(started.message, 'error');
          return;
        }
        attend(started.sessionId);
        if (!started.sent) notify(t('work.say.notTaken'), 'error');
      },
      onError: failure(notify),
    });
  };
  // *Go on with Daoris's brief* (SESSCTX1c part B, D189 point 3): the same session goes on in a new part of itself, handed Daoris's brief
  // and then the words, attended once it opens. One act of the driver's; a refusal is said by its code, as the fresh start's is.
  const goOnBrief = useGoOnBrief();
  const onGoOnBrief = () => {
    if (!attended || goOnBrief.isPending) return;
    goOnBrief.mutate({ session: attended.id }, {
      onSuccess: (moved) => {
        if (moved.why) {
          notify(goOnBriefRefusal(t, moved.why, { quest: attended.quest }), 'error');
          return;
        }
        if (!moved.sessionId) {
          notify(moved.message, 'error');
          return;
        }
        attend(moved.sessionId);
        if (!moved.sent) notify(t('work.say.notTaken'), 'error');
      },
      onError: failure(notify),
    });
  };
  // *Go on in a new session* (MSG1g2, D137 §2.2): words a cooling account holds go on in a new session at the driver's next
  // look. The driver judges and keeps the choice; what it came to is said under the words, in the page's words.
  const goOnNew = useGoOnNew();
  const onGoOnNew = () => {
    if (!attended || goOnNew.isPending) return;
    const session = attended.id;
    goOnNew.mutate(session, {
      onSuccess: (answer) => setNewSession({ session, answer }),
      onError: failure(notify),
    });
  };

  // The keeping stop (COWORK1a, the message-urgency design §3.2): the person's own press beside words held for the turn's end,
  // asked once, `CANCEL_TURN` with `keep` for a conversation and a driven session alike, whose stop already keeps what is held.
  // The turn stops and what waits goes next; nothing comes back to the box. No word presses it for them (D188 point 3).
  const onKeepingStop = (answered: Answered) => {
    if (!attended) return;
    cancelTurn.mutate({ id: attended.id, keep: true }, {
      onSuccess: ({ cancelled }: TurnStop) => {
        answered.done();
        notify(cancelled ? t('work.composer.turnStopping') : t('work.composer.noTurn'));
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

  // Stopping the turn and keeping the conversation (CONV4a). What was waiting comes back into the box,
  // ahead of whatever is being typed now, since it was written first; and the notice claims only the
  // asking — a stop that lands after the words and before the turn's end leaves it ending normally.
  const onStopTurn = () => {
    if (!attended) return;
    cancelTurn.mutate(attended.id, {
      onSuccess: ({ cancelled, withdrawn }: TurnStop) => {
        if (withdrawn.length > 0) {
          setDraft((was) => [...withdrawn.map((message) => message.text), was.trim()].filter(Boolean).join('\n\n'));
        }
        // A file cannot come back into the box — the page no longer holds its bytes — so it is named,
        // for the person to attach again (CONV4c).
        const unsent = withdrawn.flatMap((message) => message.files);
        const said = [
          cancelled ? t('work.composer.turnStopping') : null,
          withdrawn.length > 0 ? t('work.composer.withdrawn', { count: withdrawn.length }) : null,
          unsent.length > 0 ? t('work.composer.filesNotSent', { count: withdrawn.length, names: unsent.join(', ') }) : null,
        ].filter((sentence): sentence is string => Boolean(sentence));
        // Sentences are joined the catalogue's way: English puts a space after a full stop, and
        // Chinese does not (seen on the window, CONV4b).
        notify(said.length > 0
          ? said.reduce((first, second) => t('work.composer.twoSentences', { first, second }))
          : t('work.composer.noTurn'));
      },
      onError: failure(notify),
    });
  };

  // The person's answer to a parked session (design §4). The refusal — a move that is not theirs,
  // a decline with nothing in it — is the host's own sentence and reaches them word for word.
  // A finish that closes its quest (QUESTCLOSE1) sends the person's done once the finish stood: the session first, as the
  // driver moves the process before the record, then the quest, whose refusal is the service's sentence, the finish kept.
  // The ask the finish or decline was pressed in hears how the session's move ended (UXFIX2b2b): the refusal is said inside
  // it, and it closes once the session landed, the quest's done being its own notice.
  const onResolve = (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => {
    if (!attended) return;
    const closing = state === 'completed' && close?.as === 'done' ? attended.quest ?? null : null;
    resolve.mutate({ id: attended.id, state, note: note ?? undefined }, {
      onSuccess: () => {
        answered?.done();
        notify(t('work.awaiting.resolved', { id: attended.id, state: t(`sessionState.${state}`) }));
        if (closing) {
          personDone.mutate({ id: closing, note: close!.note }, {
            onSuccess: (result) => notify(result.message),
            onError: failure(notify),
          });
        }
      },
      onError: answered ? (error) => answered.refused(sentence(error)) : failure(notify),
    });
  };

  // The answer to a driven session that parked to ask the person (STANDDOWN2): the record stays parked
  // with their words (ANSWER1b), and the same session goes on with them at the driver's next tick (D131).
  // The sessions are read again, so until then the frame shows it going on, not waiting (ANSWER1c).
  // PARKANSWER1: answered from its card, which keeps the words until it is told they arrived, and says a refusal itself.
  const onAnswerSession = (words: string | null) => new Promise<string | null>((settle) => {
    if (!attended) { settle(null); return; }
    answer.mutate({ id: attended.id, answer: words }, {
      onSuccess: () => {
        notify(t('work.awaiting.answered'));
        settle(null);
      },
      onError: (error) => settle(sentence(error)),
    });
  });

  // PARKANSWER1 (the owner report, 2026-10-23): where the box below answers the park, its card answers it too, through the box's
  // own say door (MSG1f) and as the box would (its refusals, a question changed under the page, words on their way): the card was
  // handed no answer there, and a park whose box the person did not have before them could only be finished or declined. A choice
  // names its question (PARKVIEW1). Settles with the sentence a refusal is said in, for the card to say beside its words.
  const sayFromCard = (text: string, choice?: { label: string; question: string }) => new Promise<string | null>((settle) => {
    if (!attended) { settle(null); return; }
    const why = unaccepted(text);
    if (why) { settle(why); return; }
    const session = attended.id;
    const n = sending.begin(session, { text, files: [] });
    say.mutate({ id: session, text, ...(choice ? { question: choice.question } : {}) }, {
      onSuccess: (result) => {
        sending.answered(n);
        if (!result.sent) {
          settle(result.why === 'question-changed' ? t('work.park.changed') : untaken(result, t('work.say.notTaken')));
          return;
        }
        counted(session, 'answer', text, 0, result);
        notify(choice ? t('work.park.chose', { label: choice.label }) : t('work.awaiting.answered'));
        settle(null);
      },
      // A send the page stopped waiting on may still land (SENDLOST1b): it is shown on its way, so it leaves the card.
      onError: (error: unknown) => settle(stillTaking(n, error) ? null : sentence(error)),
    });
  });
  const answerFromCard = (words: string | null) => (words ? sayFromCard(words) : onAnswerSession(null));
  const choiceFromCard = (label: string, words: string) =>
    (question ? sayFromCard(choiceWords(label, words), { label, question: question.id }) : Promise.resolve(null));

  // A go-ahead the park asked, answered on its page (KNOWUSE1a2, D135 §2): through one door, which sends the same session
  // on once none of the go-aheads it asked is open, as the ask's page does (GOAHEAD2b). The person's words are the
  // go-ahead's, never a message to the park, so nothing is counted as one; the toast names any go-ahead still open from the
  // list as it stood at the press.
  // A refusal is said beside the go-ahead that was answered (UXFIX2b3b), not toasted.
  // PARKVIEW1 (the park's ask design §4): where the park's question waits unanswered, a go-ahead keeps the park for that answer
  // (`goesOn: false`, GOAHEAD2's provision for a caller that answers the park itself), which then sends it on with them all.
  const onGoAhead: GoAheadAnswer = (number, approved, words, answered) => {
    if (!attended || !parkAsk) { answered.done(); return; }
    const sessionQuest = attended.quest;
    const asked = goAheads;
    parkGoAhead.mutate({ id: attended.id, ask: parkAsk, number, approved, words, ...(question ? { goesOn: false } : {}) }, {
      onSuccess: (answer) => { notify(goAheadToast(t, number, approved, answer, { quest: sessionQuest, asked })); answered.done(); },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

  // PARKVIEW1 (the park's ask design §4): a press in the park's box takes the box's words with it. They leave the box only once the
  // door kept them, and only where it still begins with them, so words typed while the press was on its way stay; a refusal is said
  // in the box, the words kept.
  const onBoxGoAhead = (number: number, approved: boolean) => {
    if (!attended) return;
    const session = attended.id;
    const words = draft;
    setRefusal(null);
    onGoAhead(number, approved, words.trim() || undefined, {
      done: () => setDraft((was) => afterPress(was, words)),
      refused: (said) => setRefusal({ session, text: said }),
    });
  };
  // One of the answers its question offered, selected (QUESTIONUI1a): its label, then the words the box's *Send* took, as the park's answer
  // through the box's one door, naming the question so a label never answers one the park no longer asks (`question-changed`). The
  // box has let its words go: a refusal gives them back with the selection kept; a question changed keeps the words and draws the new
  // one with none selected, as its id is another.
  const onChoice = (label: string, asked: string, words: string) => {
    if (!attended) return;
    const session = attended.id;
    const text = choiceWords(label, words);
    const giveBack = () => { if (words) setDraft((was) => [words, was.trim()].filter(Boolean).join('\n\n')); };
    setRefusal(null);
    const n = sending.begin(session, { text, files: [] });
    say.mutate({ id: session, text, question: asked }, {
      onSuccess: (result) => {
        sending.answered(n);
        if (!result.sent) {
          giveBack();
          setRefusal({ session, text: result.why === 'question-changed' ? t('work.park.changed') : untaken(result, t('work.say.notTaken')) });
          return;
        }
        setParkChoice(null);
        counted(session, 'answer', text, 0, result);
        notify(t('work.park.chose', { label }));
      },
      onError: (error: unknown) => {
        // A send the page stopped waiting on may still land (SENDLOST1b): its words are shown on their way, so they stay out of the box.
        if (stillTaking(n, error)) return;
        giveBack();
        setRefusal({ session, text: sentence(error) });
      },
    });
  };

  // 🔴 Consumed by IDENTITY, the way the quest composer's opening draft is (FIX-LOG 2026-09-23):
  // "if (intent) set…" during render made React re-run this frame with the same prop on every pass,
  // so the palette's ask never landed. The parent is told from an effect, not during this render.
  // The session a session's views follow (UX6b, D150 §8): on Sessions the attended one; off Sessions only while it runs or
  // waits on the person, so an ended one's note no longer runs down every other view's side bar.
  const following = followed(attended, elsewhere);
  // The followed session's dock surface: the one it had, or the tab it opens on — the timeline (FRAME6), or Ask Daoris
  // off Sessions with none followed.
  const dockKey = following?.id ?? '';
  // What stands where (DOCK1b): Ask Daoris only where a shell handed it in, and the terminal likewise.
  // *Notifications* likewise (NOTIFY2b): where the application handed it in.
  const present = (view: ViewId) => (view === 'ask' ? Boolean(ask) : view === 'terminal' ? terminal
    : view === 'notifications' ? Boolean(notifications) : true);
  const rightViews = viewsIn(placed.places, 'right', present);
  const panelViews = viewsIn(placed.places, 'panel', present);
  // Ask Daoris and *Notifications* are the machine's, not the attended session's: their tab stays whichever session is attended.
  const [machineTab, setMachineTab] = useState<'ask' | 'notifications' | null>(null);
  // A file's preview per session (PREVIEW1, D111): what a tool card or the review opened, and the tab it
  // covered, which its × goes back to. Kept while the window is open, like the dock's tab.
  const [previews, setPreviews] = useState<Record<string, FileOpen & { back: DockTab }>>({});
  const previewing = following && here ? previews[following.id] ?? null : null;
  const sessionTab = docked[dockKey] ?? dockOpensOn(elsewhere, following !== null);
  const dock: DockTab | undefined = machineTab && rightViews.includes(machineTab)
    ? machineTab
    : sessionTab === 'preview'
      ? (previewing ? 'preview' : rightViews[0])
      : rightViews.includes(sessionTab) ? sessionTab : rightViews[0];
  const setDock = (tab: DockTab) => {
    const machine = tab === 'ask' || tab === 'notifications' ? tab : null;
    setMachineTab(machine);
    if (!machine) setDocked((was) => ({ ...was, [dockKey]: tab }));
  };
  // The panel's shown view: the machine's, like its height, not the attended session's.
  const [panelPick, setPanelPick] = useState<ViewId>('console');
  // The view being dragged (DOCK1e), so a region with nothing in it is drawn to be dropped on.
  const [dragging, setDragging] = useState<ViewId | null>(null);
  // 🔴 Told a beat after the drag starts, never inside it: Chromium drops a drag whose page changes
  // under the pointer in the same task as `dragstart`, and drawing an emptied region is such a change.
  const onDrag = (view: ViewId | null) => { window.setTimeout(() => setDragging(view), 0); };
  const panelView = panelViews.includes(panelPick) ? panelPick : panelViews[0];
  // Where the followed session's work stands (WORKFLOW1c, the workflow design §7): asked only while the Workflow view shows,
  // in whichever region it stands, and again at every tick under the driver's key.
  const workflowShown = following !== null && (placed.places.workflow === 'panel'
    ? !collapsed && panelView === 'workflow'
    : !dockClosed && dock === 'workflow');
  const workflowRun = useWorkflowRun(following ? { session: following.id } : null, { enabled: workflowShown });
  // FABRIC-CUTOVER1f: the person's skip or stop of a graph run's node, pressed under the node in the view, kept for the driver's next look.
  const runNodeWord = useRunNodeWord();
  // The person opening the dock on a surface — from its strip.
  const openDock = (tab: DockTab) => {
    setDock(tab);
    closeDock(false);
  };
  // A door opening a file's preview (PREVIEW1): the side bar opens on it, over the tab it covers. Handed to
  // the conversation's cards through a context, so one stable function is handed and the latest state read.
  const previewNow = useRef<(file: FileOpen) => void>(() => {});
  previewNow.current = (file: FileOpen) => {
    if (!following) return;
    const covered = dock && dock !== 'preview' ? dock : previews[following.id]?.back ?? 'timeline';
    setPreviews((was) => ({ ...was, [following.id]: { path: file.path, lines: file.lines ?? null, back: covered } }));
    openDock('preview');
  };
  const openPreview = useCallback((file: FileOpen) => previewNow.current(file), []);
  const closePreview = () => {
    if (!following) return;
    const back = previews[following.id]?.back ?? 'timeline';
    setPreviews(({ [following.id]: _closed, ...rest }) => rest);
    if (dock === 'preview') setDock(back);
    // A side bar the preview leaves with nothing in it closes, as one a moved view leaves does (DOCK1b).
    if (rightViews.length === 0) closeDock(true);
  };
  const previewFile = useTreeFile(previewing ? following!.id : null, previewing?.path ?? null);
  const previewPatch = useReviewedPatch(previewing ? following!.id : null, previewing?.path ?? null);
  // The person asking for a view by a door that does not know where it stands — the palette's review,
  // Ask Daoris's `F1` — opens whichever region holds it, on it.
  const openView = (view: ViewId) => {
    if (placed.places[view] === 'panel') {
      setPanelPick(view);
      closed.setPanel(false);
    } else {
      openDock(view);
    }
  };
  // A view moved (DOCK1b): shown where it went, as VS Code opens the container a view lands in, and a
  // region it leaves with nothing closes, as VS Code hides an empty one.
  const moveView = (view: ViewId, to: Place) => {
    const from = placed.places[view];
    // A drop ends the drag here: the tab it started from is gone by the time `dragend` fires on it.
    setDragging(null);
    if (from === to) return;
    placed.move(view, to);
    logEvent('panel.moved', { view, region: to });
    // Not `openView`: this render's places still say where the view was.
    if (to === 'panel') {
      setPanelPick(view);
      closed.setPanel(false);
    } else {
      openDock(view);
    }
    if (viewsIn({ ...placed.places, [view]: to }, from, present).length === 0) {
      if (from === 'panel') closed.setPanel(true);
      else closeDock(true);
    }
  };

  // The person asked for Ask Daoris — the right side bar's toggle, `F1`, the palette: its region opens on it.
  useEffect(() => {
    if (askFocus <= 0) return;
    openView('ask');
    // Only a new ask for it; where it stands and the closings are read, not watched.
  }, [askFocus]);

  // NOTIFY2b (the notification design §2.1): *Notifications* shown where it stands, by a door that asks for it; and what its region
  // showed before the bell opened it, so the bell's next press gives the region back: the view it held, or closed where it was.
  const notificationsBack = useRef<{ place: Place; view: ViewId | DockTab | undefined; closed: boolean } | null>(null);
  const notificationsShown = placed.places.notifications === 'panel'
    ? !collapsed && panelView === 'notifications'
    : !dockClosed && dock === 'notifications';
  useEffect(() => {
    if (notificationsShow <= 0) return;
    if (!notificationsShown) {
      const place = placed.places.notifications;
      notificationsBack.current = { place, view: place === 'panel' ? panelView : dock, closed: place === 'panel' ? collapsed : dockClosed };
    }
    openView('notifications');
    // Only a new ask for it, as Ask Daoris's.
  }, [notificationsShow]);
  useEffect(() => {
    if (notificationsToggle <= 0) return;
    const place = placed.places.notifications;
    if (!notificationsShown) {
      notificationsBack.current = { place, view: place === 'panel' ? panelView : dock, closed: place === 'panel' ? collapsed : dockClosed };
      openView('notifications');
      return;
    }
    const back = notificationsBack.current?.place === place ? notificationsBack.current : null;
    notificationsBack.current = null;
    const others: (ViewId | DockTab)[] = (place === 'panel' ? panelViews : rightViews).filter((view) => view !== 'notifications');
    if (back?.closed) {
      // Opened from a closed region: the region closes, as it was.
      if (place === 'panel') closed.setPanel(true);
      else closeDock(true);
      return;
    }
    // The view the region held, where it still stands there (or the file's preview it covered); else the region's first other view.
    const held = back?.view && back.view !== 'notifications' && (others.includes(back.view) || back.view === 'preview') ? back.view : others[0];
    if (!held) {
      if (place === 'panel') closed.setPanel(true);
      else closeDock(true);
    } else if (place === 'panel') {
      setPanelPick(held as ViewId);
    } else {
      setDock(held);
    }
    // Only a new press of the bell; where it stands and what it showed are read, not watched.
  }, [notificationsToggle]);

  const [taken, setTaken] = useState<typeof intent>(null);
  if (intent && intent !== taken) {
    setTaken(intent);
    if (intent === 'start') newConversationPressed();
    else if (intent === 'review') openView('review');
    else if (intent === 'workflow') openView('workflow');
  }
  if (!intent && taken) setTaken(null);

  // A go to New conversation (NEW1d-b): consumed by its id, as `intent` is by identity. The repository is NEW1c's pick, which waits
  // until the registry lists it; the words go above what is typed, as `keepDraft` puts them. Files are not touched.
  const [composed, setComposed] = useState<number | null>(null);
  if (compose && compose.id !== composed) {
    setComposed(compose.id);
    newConversationPressed();
    if (compose.repository) setPickNew((last) => ({ repository: compose.repository!, n: (last?.n ?? 0) + 1 }));
    if (compose.sentence) {
      const sentence = compose.sentence;
      setNewDraft((was) => [sentence, was.trim()].filter(Boolean).join('\n\n'));
    }
  }

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
            />
          </div>,
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

  const roster = Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : [];
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
  // Where its work stands (FABRIC-MILESTONE1b): the driver's derivation for this session, read only for this machine's own record,
  // whose branch and tree are this machine's. A session no record names a unit for (a chat with no tree) shows none.
  const milestones = useWorkState(attended && here ? attended.id : null, Boolean(attended && here));
  const milestoneUnits = milestones.data?.units ?? [];
  const milestone = (attended && milestoneUnits.length === 1 ? milestoneUnits[0] : milestoneUnits.find((unit) => unit.session === attended?.id)) ?? null;
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
  const landWithoutLook = useLandWithoutLook();
  // CHATFLOW1d (the chat-flow design §5.2): a chat run's press refused for what the page can word says it in the reader's language; any
  // other refusal is the driver's sentence, as before.
  const pressRefusal = (result: { message: string; press?: string | null }) =>
    result.press && ['busy', 'elsewhere', 'moved', 'asked'].includes(result.press) ? t(`work.review.chatPress.${result.press}`) : result.message;
  // A landing's answer told back to the press that made it, *Accept…*'s and *Accept without the look…*'s alike.
  const landed = (answered: Answered) => ({
    onSuccess: (result: { done: boolean; message: string; withheld?: string | null; press?: string | null }) => {
      if (!result.done) {
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
    busy: reviewActs.busy || landWithoutLook.isPending,
    acts: {
      ...reviewActs.actsFor({
        state: reviewState(gateHolds.state),
        step: gateHolds.quest ?? null,
        record: gateStep,
        work: attended.quest ?? null,
        open: gateHolds.quest && onOpenQuest ? () => onOpenQuest(gateHolds.quest!) : undefined,
      }),
      // CHATFLOW1d2 (the chat-flow design §5.3): a chat run's way past the look no set-up step can show, the person's own press.
      ...(gateHolds.chatRun && here ? {
        withoutLook: (words: string | null, answered: Answered) => landWithoutLook.mutate({ id: attended.id, words }, landed(answered)),
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
  const withoutLookNow = useUnderWay('LAND_WITHOUT_LOOK', attended?.id ?? null);
  const landsNow = useUnderWay('LAND_SESSION_TREE', attended?.id ?? null) || withoutLookNow || lineLandsNow || askLandsNow;
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
    resolving: resolve.isPending || answer.isPending || parkGoAhead.isPending || personDone.isPending,
    onResolve: here ? onResolve : undefined,
    // Its card's answer (PARKANSWER1): through the box's say door where the box answers the park too, else the park's answer door.
    onAnswerSession: here ? (answering ? answerFromCard : onAnswerSession) : undefined,
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
  // ACCEPTALL1 (D167's 2026-10-15 amendment): its line of work's one Accept, where more than one of its parts offers work to land
  // here; the plan reads git for every part, so it is asked only once the ask is open.
  const entryOffered = attendedEntry && here
    ? attendedEntry.parts.filter((part) => (groups.data ?? []).some((row) => row.session === part.session && row.lands)).length
    : 0;
  const entryAccepts = attendedEntry && entryOffered > 1 ? attendedEntry.entry : null;
  const [entryAsking, setEntryAsking] = useState<string | null>(null);
  const entryPlan = useEntryLanding(entryAccepts, entryAccepts !== null && entryAsking === entryAccepts);
  const landEntry = useLandEntry();
  // Under way in any window (ACCEPTALL1e): this one's press from its send, another's from the driver's telling.
  const entryLandsNow = useUnderWaySubject('LAND_ENTRY', entryAccepts);
  const entryAccept: ComponentProps<typeof EntryAccept> | undefined = entryAccepts && attendedEntry ? {
    scope: 'entry',
    offered: entryOffered,
    open: entryAsking === entryAccepts,
    onOpen: (open) => setEntryAsking(open ? entryAccepts : null),
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
  // WAITFOOT1c (D168 point 4): the waits that stand where the strip draws them, and the events that raised them, which the
  // conversation finds and tells; the way to each is handed to the head and the strip alike, and a raising block's *To the wait*
  // leads to its line where it is on screen (WAITFOOT2b).
  const answerBelow = answering || (talking && box.kind === 'chat');
  const standing = attended && here && waits ? waitsOf({ ...waits, answerBelow }) : [];
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
  const seekAt = (at: 'head' | 'foot' | 'box') => (attended && toWait?.session === attended.id && toWait.at === at ? toWait : null);
  // Where the work's ask (*Accept…*, *Discard branch…*) is open: in the head or at the foot, never both (D168 point 3).
  const [workAsk, setWorkAsk] = useState<{ session: string; at: 'head' | 'foot' } | null>(null);
  const workAskAt = (at: 'head' | 'foot'): WorkAsk | undefined => (attended ? {
    open: workAsk?.session === attended.id && workAsk.at === at,
    onOpen: (open) => setWorkAsk((was) => (open
      ? { session: attended.id, at }
      : was?.session === attended.id && was.at === at ? null : was)),
  } : undefined);
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
    // AGENTERRORS1a2 (D210 §5.1, §8): *Trying again at* while a transient episode waits.
    retrying: waiting?.session === attended.id ? waiting.retrying : undefined,
    // HEAVYSLOT1: its heavy run said in place of the call, by its gate or whom it waits behind.
    heavy: heavyPart ? heavyLine(t, heavyPart, heavyTitleOf) : null,
  } : null;
  const pageHead = attended && attendedFacts && attendedShown && headFactsOf && (
    <SessionPageHead
      session={attended}
      title={attendedTitle}
      shown={attendedShown}
      facts={sessionFacts(t, headFactsOf)}
      // RAILLINE1: the line is one line, its running call said short; the whole line, the call's whole title in it, is its tip.
      factsTip={sessionFactsTip(t, headFactsOf)}
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
          actorName={attendedActor ? t(`work.actor.name.${attendedActor.id}`) : undefined}
          agentName={offerTo?.product || offer.agent}
          accountName={offer.account ? nameOf(offerTo ? toolOf(offerTo) : offer.agent, offer.account) : null}
          busy={actions.handingOver}
          onGoOn={(told) => actions.handOverNow(
            attended,
            { agent: offer.agent, account: offer.account ?? '', name: offerTo?.product || offer.agent },
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

  // *New conversation* (NEW1b, D162 point 1), the main area while nothing is attended: what the drawer it replaced knew is
  // its chips'.
  const registered = registry.data ?? [];
  const newConversation = (
    <NewConversation
      readCheckout={readCheckout}
      // A checkout on this machine is what makes a repository choosable: there is nowhere else to talk, and a teammate's
      // mirrored registration has no tree here (D48 §3/§7). One without is listed and says so.
      repositories={registered
        .map((row) => ({ name: row.repository, workspace: row.workspace || 'default', checkout: Boolean(row.root) }))
        .sort((a, b) => a.name.localeCompare(b.name))}
      // A checkout an active session on this machine holds refuses a second one (UX5 U68): with the session holding it.
      busy={Object.fromEntries((sessions.data ?? [])
        .filter((session) => SESSION_ACTIVE.has(session.state) && sessionOrigin(session) === null
          && !ownTree(session, registered.find((row) => row.repository === session.repository)?.root))
        .map((session) => [session.repository, session.id]))}
      recent={mergeRecent(recentStarts, sessions.data ?? [])}
      remembered={chosenStart}
      attended={pressedFrom}
      onChoose={chooseStart}
      harnesses={agentRoster.harnesses}
      labels={agentRoster.labels}
      names={agentRoster.names}
      defaultHarness={spawning?.harness}
      accounts={agentRoster.accounts}
      orderOf={agentRoster.orderOf}
      draft={newDraft}
      onDraft={setNewDraft}
      carried={newCarry}
      onCarry={setNewCarry}
      pending={startChat.isPending}
      refusal={startRefusal}
      focus={(focusOwed ? composing : 0) + refocus}
      onFocused={() => setFocusOwed(false)}
      onStart={onStart}
      onOpenSession={attend}
      onAdd={() => setAdding(true)}
      onImport={() => setImporting(true)}
      pick={pickNew}
      reveal={reveal}
      onMakeTask={onMakeTask}
    />
  );
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
  const drawerDown = (close: () => void) => () => {
    close();
    if (listOpened.current) listOpened.current = false;
    else setRefocus((count) => count + 1);
  };

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
          {!attended && (
            <>
              {newConversation}
              {adding && (
                // Once registered it is chosen, as Projects' list chooses what it registered, and the words stay (NEW1c).
                <AddProjectDrawer
                  onClose={drawerDown(() => setAdding(false))}
                  onAdded={(repository) => setPickNew((last) => ({ repository, n: (last?.n ?? 0) + 1 }))}
                  notify={notify}
                />
              )}
              {importing && (
                <ImportFolderDrawer
                  onClose={drawerDown(() => setImporting(false))}
                  onImported={({ repositories: landed, workspace }) => {
                    // One is chosen; several, or none, leave the choice to the person, in the list opened on where they landed.
                    if (landed.length === 1) {
                      setPickNew((last) => ({ repository: landed[0]!, n: (last?.n ?? 0) + 1 }));
                      return;
                    }
                    listOpened.current = true;
                    setReveal((last) => ({ workspace: landed.length > 1 ? workspace ?? '' : '', n: (last?.n ?? 0) + 1 }));
                  }}
                  notify={notify}
                />
              )}
            </>
          )}
          {attended && waits && <AttendedSession
            // What waits on the person, and every press on it: the strip at the foot is handed the same (D168 point 3).
            {...waits}
            // Its pieces (COAGENT1b): what other agents write for it, and the ask of one; no wait of the person's.
            pieces={pieceSection}
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
            } : undefined}
            chain={quest ? buildChain(quest.id, quests.data ?? [], sessions.data ?? []) : []}
            relations={attended ? relationsOf(attended, quest, quests.data ?? [], sessions.data ?? []) : undefined}
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
                    onToWait={(wait) => {
                      const anchor = anchorOf(wait);
                      if (!anchor) return;
                      // To the head's line while it is on screen, where the strip stands aside (WAITFOOT2b), else the strip's; a park's
                      // question and go-aheads to the box that answers them (PARKVIEW1), which is always drawn.
                      const at = inTheBox(anchor) ? 'box' : waitsOnScreen === true ? 'head' : 'foot';
                      setToWait((was) => ({ session: attended.id, wait: anchor, n: (was?.n ?? 0) + 1, at }));
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

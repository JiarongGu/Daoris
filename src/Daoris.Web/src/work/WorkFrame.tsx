import { type ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SessionConsole } from '../SessionConsole';
import { sentence } from '../format';
import { buildChain } from '../map/chain';
import { useAnswerSession, useAsks, useQuests, useRegistry, useSessions } from '../queries';
import {
  type TurnStop, useCancelTurn, useEndChat, useHarnesses, useResolveSession, useSendMessage,
  NO_TURNS, useChatTurns, useSessionGroups, useSessionOpenings, useSessionOptions, useSessionStreams, useSessionWhere,
  useSetSessionOption, useStartChat, useStopTask, useSweepPlan, useTreeFiles, useTreeFile, useReviewedPatch,
  logEvent, useTerminals, useRemotes, useWorkPlan, useSay, useSessionReach, useStartFrom, type WordsAnswer,
  useParkGoAhead, useAccounts, useGoOnNew,
} from '../shell';
import { doorOf } from '../tools';
import {
  boxOf, coolingFor, NATIVE_WORDS_LIMIT, neverSentence, type NewSessionAnswer, startFromRefusal, takesWords, tooLong,
} from './say';
import { SessionBox } from './SessionBox';
import { askOf, pauseAsk, wiredFor, type WorkTarget } from './pausing';
import { goAheadsAsked, goAheadToast } from './parkGoAheads';
import { PauseAsk } from './WorkAsks';
import { FilePreview } from './FilePreview';
import { type FileOpen, FileOpener, fileName } from './preview';
import { TerminalView } from './TerminalView';
import { Button, Drawer, failure, type Notify, SESSION_ACTIVE, useErrorNotify } from '../ui';
import { AttendedSession, noteIsInTheHead } from './AttendedSession';
import { SessionConversation } from './SessionConversation';
import type { Usage } from './conversation';
import { isIntake, ownTree, sessionOrigin, sessionTitle } from './identity';
import { doorLabel } from '../tools';
import type { Resolution } from './AwaitingPerson';
import { Composer } from './Composer';
import { DiffPane } from './DiffPane';
import { useDraft } from './drafts';
import { type DockTab, RightDock } from './RightDock';
import { SessionTimeline } from './SessionTimeline';
import { SessionRail } from './SessionRail';
import { type ActFacts, actMenu, headerActs, offeredActs, primaryAct, stopAsk } from './acts';
import { keptSessionFilters, sessionFilters, shownOf } from './groups';
import { DeleteAsk, SessionPageHead, StopAsk } from './SessionPageHead';
import { type SessionDoors, useSessionActs } from './sessionActs';
import { ListMore } from './ListPane';
import { StartSession, type StartChoice } from './StartSession';
import { OutputPanel, PANEL_MIN, StreamTabs } from './frame';
import { panelTabs } from './streams';
import { DOCK, dockRange, frameLayout, type ListMode } from './layout';
import { type FrameClosings, useFrameClosings } from './closings';
import { type ListPanes, useListPanes } from './listPanes';
import { listChoice, type ListSpec, useFrameWidth, useListMode, type ViewLayout, ViewListPane } from './ViewFrame';
import { ViewMain } from './ViewMain';
import { type Place, type Placements, usePlacements, type ViewId, viewsIn } from './placements';
import { relationsOf } from './relations';
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

/** Whether a door reports context, from the roster's word on it — undefined until the roster says. */
const door = (structured?: boolean): 'structured' | 'text' | undefined =>
  structured === true ? 'structured' : structured === false ? 'text' : undefined;

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
  lists, layout: viewLayout, onOpenSessions, terminal = false, onListMode,
}: {
  /**
   * What each view's list remembers (D118 §3f), held by the application so the list's doors reach it from
   * every view; this frame's own where it is rendered alone.
   */
  lists?: ListPanes;
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
   * What the command palette asked for (SURF9) — an event, consumed on arrival, because leaving it
   * set would reopen the drawer every time anything here re-rendered.
   */
  intent?: 'start' | 'review' | null;
  onIntentTaken?: () => void;
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
  const { t } = useTranslation();
  // A refusal belongs to the session that gave it: attending another by any door (a notification, the
  // palette, a quest's record) must not show one session's "went nowhere" on another's composer (REV3).
  const [refusal, setRefusal] = useState<{ session: string; text: string } | null>(null);
  // What *Go on in a new session* came to (MSG1g2), the session's own, as a refusal is: said under its words alone.
  const [newSession, setNewSession] = useState<{ session: string; answer: NewSessionAnswer } | null>(null);
  // Why the last start was refused, said in the start form until it closes or starts again (UX5 U68).
  const [startRefusal, setStartRefusal] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  // *Archive what ended…* pressed in the list's ⋯ (SESSUX1e): its first press stands under the list's header until it
  // archives or the person never minds. Not remembered: it is a question asked now.
  const [archivingEnded, setArchivingEnded] = useState(false);
  // The session whose stop asks under the header (SESSUX1d, D126 §3.3), from the header's *Stop…* or its row's: a
  // question asked now, of that session only, so attending another closes it.
  const [stopAsking, setStopAsking] = useState<string | null>(null);
  // The session whose delete asks under the header (SESSUX1f, D126 §5.4), the stop's way: of that session only.
  const [deleteAsking, setDeleteAsking] = useState<string | null>(null);
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
  const layout = frameLayout(width.viewport, width.frame, {
    list: listView ? listChoice(listView, listed, closed.listOver) : null,
    dockShare, dockClosed, dockFull,
  });
  const list = layout.list;

  // What the list is now, told to the application, whose doors toggle it and say whether it is shown; and
  // a list laid over the main area let go once the room makes it anything else (D118 §3f).
  const { listOver, setListOver } = closed;
  useListMode(list?.mode ?? null, onListMode, listOver, setListOver);

  /** A choice in a list laid over closes it (D118 §3a). */
  const chosen = () => { if (listOver) setListOver(false); };
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
  // A conversation's name (RAIL1): the same answer the rail asks for, from the same list, so the head
  // and the row read one name and the bridge is asked once.
  const openings = useSessionOpenings(sessions.data);
  // Where the attended session is listed, and where its work is (SESSUX1d): the rail's own two answers, asked under the
  // same keys, so the page header and its row are offered one set of acts and the bridge is asked once.
  const groups = useSessionGroups();
  const where = useSessionWhere(sessions.data);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const harnesses = useHarnesses();
  // Each account's cool-off (TOOL4g), which says when words a cooling account holds go on (MSG1g2).
  const accounts = useAccounts();

  const startChat = useStartChat();
  const resolve = useResolveSession();
  const answer = useAnswerSession();
  const send = useSendMessage();
  const end = useEndChat();
  const cancelTurn = useCancelTurn();
  // Which workspaces sync with a remote here: a pause's ask names an open quest another machine may still take (PAUSE1e).
  const wiring = useRemotes().data;

  useErrorNotify(sessions.error, notify);

  const attended = (sessions.data ?? []).find((session) => session.id === selected) ?? null;
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
  // The go-aheads a park asked on its quest's ask (KNOWUSE1a2, D135 §2), read only for a driven park whose quest an ask
  // asked: shown in its head, where answering one answers the park too. The asks are asked for nowhere else here.
  const parkAsk = attended?.state === 'awaiting-person' && !intake ? askOf(quest) : null;
  const asks = useAsks(false, parkAsk !== null);
  const goAheads = attended && parkAsk ? goAheadsAsked(asks.data, parkAsk, attended.id) : [];
  const parkGoAhead = useParkGoAhead();
  const talking = Boolean(attended && conversation && here && !intake);
  // A driven session still working may be told something (SESS3): its words are held and are its next prompt. Offered only
  // where the driver says it listens, which is the protocol door; the pipe door has nothing to hear it.
  const steerable = Boolean(attended && here && !intake && !conversation && attended.quest
    && SESSION_ACTIVE.has(attended.state) && attended.state !== 'awaiting-person');

  // What the person was typing to this session, kept per session and across a reload (CONV4b): one draft for whichever
  // box its page offers, so words typed as it ends are still there in the box that goes on with them (MSG1f).
  const [draft, setDraft] = useDraft(attended && here && !intake ? attended.id : null);
  // Where each live conversation's turns stand, as the driver holds them: the attended one's stop and
  // queue follow it, not the record, which learns a turn began only when its first event lands
  // (CONV4a); and the rail and the head read a chat between turns as idle (UX5 U17).
  const liveChats = (sessions.data ?? [])
    .filter((session) => session.kind === 'chat' && SESSION_ACTIVE.has(session.state))
    .map((session) => session.id);
  const chatTurns = useChatTurns(talking || steerable ? [...liveChats, attended!.id] : liveChats);
  const turns = (talking && chatTurns[attended!.id]) || NO_TURNS;
  // What the driver holds for the attended driven session, and whether it hears anything at all.
  const held = steerable ? chatTurns[attended!.id] : undefined;
  const steering = held?.listening === true;
  // The box at the foot (MSG1f, D137 §5.1): on every session that takes words, by what a word said now would do, and the
  // line saying why on the rest. 🔴 A driven session parked to ask the person is answered from it, where a chat's is: the
  // door was a button at the top of a record of 1,800 events, and the question is read at its foot. The card above keeps
  // the endings and says the box carries it on: one owner for the answer (D56). Answered, a second word joins the first
  // (D137 §2.4), so the box stays.
  const reach = useSessionReach(attended && here && !intake ? attended : null, steering);
  const box = boxOf({ session: attended, here, intake, listening: steering, reach });
  const answering = box.kind === 'say' && box.mode === 'answer';
  // Words said as the attended session winds up, held for its record to end (D137 §2.1), shown above its box until then.
  const [ending, setEnding] = useState<{ session: string; words: string[] } | null>(null);
  useEffect(() => {
    if (!attended || ending?.session !== attended.id || !SESSION_ACTIVE.has(attended.state)) setEnding(null);
  }, [attended, ending?.session]);
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
  // A stop's ask is a question asked now, of the session it was asked of (D126 §3.3): attending another by any door
  // puts it down, so coming back later does not find it still open.
  useEffect(() => {
    setStopAsking((asked) => (asked === selected ? asked : null));
    setDeleteAsking((asked) => (asked === selected ? asked : null));
    setPauseAsking((asked) => (asked?.session === selected ? asked : null));
  }, [selected]);

  const resize = (next: number) => {
    setHeight(next);
    store(PANEL_HEIGHT, String(next));
  };

  const toggle = () => closed.setPanel(!collapsed);

  const onStart = (choice: StartChoice) => {
    setRefusal(null);
    setStartRefusal(null);
    startChat.mutate(choice, {
      onSuccess: (result) => {
        // The ledger's own sentence: the tree is busy, the repository has no checkout here, the
        // harness is missing, the chosen account is logged out. Each names the action that fixes
        // it, which is why it is shown rather than summarised — in the form, whole, where start was
        // pressed; it was a corner toast cut mid-sentence (UX5 U68).
        if (!result.sessionId) setStartRefusal(result.message);
        else { setStarting(false); attend(result.sessionId); }
      },
      onError: failure(notify),
    });
  };

  // Counted into the machine log once the words went, never their words (LOG1b): their length, how many files they carried,
  // and where they reach the session as the driver answered (MSG1d, D137 §3.3).
  const counted = (session: string, kind: string, text: string, files: number, said: WordsAnswer) =>
    logEvent('message.sent', { session, kind, length: text.length, files, ...(said.reaches ? { reach: said.reaches } : {}) });

  // Words nothing took, in the page's words: by the driver's code where it gave one (MSG1d), else that nothing took them.
  const untaken = (said: WordsAnswer, fallback: string) =>
    (said.why ? neverSentence(t, said.why, { quest: attended?.quest }) : fallback);

  const onSend = (text: string, files: File[] = []) => {
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
    send.mutate({ id: session, text, files }, {
      // False is an answer: the session ended while they were typing. It lands on the composer
      // rather than in a toast, because that is where the person is looking — on THAT session's.
      onSuccess: (result) => {
        if (result.sent) {
          counted(session, 'chat', text, files.length, result);
          setRefusal(null);
          return;
        }
        giveBack();
        setRefusal({ session, text: untaken(result, t('work.composer.notListening')) });
      },
      onError: (error: unknown) => {
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };

  // A door that takes fewer characters at once than these says so before anything is sent, and the words stay in the box
  // (D137 §2.4): a driven session on the native door, whose resumed run takes them as one argument.
  const accepts = (text: string) => {
    if (!attended || !tooLong(text, { door: doorOf(attended.adapter, roster), kind: attended.kind })) return true;
    setRefusal({ session: attended.id, text: t('work.say.tooLong', { count: NATIVE_WORDS_LIMIT }) });
    return false;
  };

  // The person's words through the box at the foot (MSG1f, D137 §5.1): a working driven session's running door (SESS3), a
  // park's answer, a session that ended, or one winding up. A send that did not arrive gives the words back to the box, as
  // a message does (REV3).
  const say = useSay();
  const onSay = (text: string) => {
    if (!attended || !text || box.kind === 'none' || box.kind === 'line' || box.kind === 'chat') return;
    const session = attended.id;
    const kind = box.kind === 'steer' ? 'steer' : box.mode === 'answer' ? 'answer' : 'say';
    const winding = box.kind === 'say' && box.mode === 'ending';
    const giveBack = () => setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
    say.mutate({ id: session, text }, {
      onSuccess: (result) => {
        if (!result.sent) {
          giveBack();
          setRefusal({ session, text: untaken(result, t('work.say.notTaken')) });
          return;
        }
        counted(session, kind, text, 0, result);
        setRefusal(null);
        if (kind === 'answer') notify(t('work.awaiting.answered'));
        // Held until its record ends, and shown above the box meanwhile: the conversation shows them once kept.
        if (winding) setEnding((was) => ({ session, words: [...(was?.session === session ? was.words : []), text] }));
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

  // Its stop sends what is held now: the turn stops, and the words are its next prompt.
  const onSteerNow = () => {
    if (!attended) return;
    cancelTurn.mutate(attended.id, {
      onSuccess: ({ cancelled }: TurnStop) => { if (cancelled) notify(t('work.steer.sending')); },
      onError: failure(notify),
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
  const onResolve = (state: Resolution, note: string | null) => {
    if (!attended) return;
    resolve.mutate({ id: attended.id, state, note: note ?? undefined }, {
      onSuccess: () => notify(t('work.awaiting.resolved', {
        id: attended.id, state: t(`sessionState.${state}`),
      })),
      onError: failure(notify),
    });
  };

  // The answer to a driven session that parked to ask the person (STANDDOWN2): the record stays parked
  // with their words (ANSWER1b), and the same session goes on with them at the driver's next tick (D131).
  // The sessions are read again, so until then the frame shows it going on, not waiting (ANSWER1c).
  const onAnswerSession = (words: string | null) => {
    if (!attended) return;
    answer.mutate({ id: attended.id, answer: words }, {
      onSuccess: () => notify(t('work.awaiting.answered')),
      onError: (error) => {
        // 🔴 The box at the foot let go of the words when it sent; an answer that did not arrive hands
        // them back, as a message does (REV3).
        if (words) setDraft((was) => [words, was.trim()].filter(Boolean).join('\n\n'));
        failure(notify)(error);
      },
    });
  };

  // A go-ahead the park asked, answered on its page (KNOWUSE1a2, D135 §2): the go-ahead and the park through one door, so
  // the same session goes on with one press. The person's words, where they gave any, are the park's answer too, counted
  // as an answer's are once they went; with none, the park takes its blank answer and nothing is counted.
  const onGoAhead = (number: number, approved: boolean, words?: string) => {
    if (!attended || !parkAsk) return;
    const session = attended.id;
    const sessionQuest = attended.quest;
    parkGoAhead.mutate({ id: session, ask: parkAsk, number, approved, words }, {
      onSuccess: (answered) => {
        if (answered.sent && words) counted(session, 'answer', words, 0, answered);
        notify(goAheadToast(t, number, approved, answered, { quest: sessionQuest }));
      },
      onError: failure(notify),
    });
  };

  // 🔴 Consumed by IDENTITY, the way the quest composer's opening draft is (FIX-LOG 2026-09-23):
  // "if (intent) set…" during render made React re-run this frame with the same prop on every pass,
  // so the palette's ask never landed. The parent is told from an effect, not during this render.
  // The attended session's dock surface: the one it had, or the timeline it opens on (FRAME6).
  const dockKey = attended?.id ?? '';
  // What stands where (DOCK1b): Ask Daoris only where a shell handed it in, and the terminal likewise.
  const present = (view: ViewId) => (view === 'ask' ? Boolean(ask) : view === 'terminal' ? terminal : true);
  const rightViews = viewsIn(placed.places, 'right', present);
  const panelViews = viewsIn(placed.places, 'panel', present);
  // Ask Daoris is the machine's, not the attended session's: its tab stays whichever session is attended.
  const [asking, setAsking] = useState(false);
  // A file's preview per session (PREVIEW1, D111): what a tool card or the review opened, and the tab it
  // covered, which its × goes back to. Kept while the window is open, like the dock's tab.
  const [previews, setPreviews] = useState<Record<string, FileOpen & { back: DockTab }>>({});
  const previewing = attended && here ? previews[attended.id] ?? null : null;
  const sessionTab = docked[dockKey] ?? 'timeline';
  const dock: DockTab | undefined = asking && rightViews.includes('ask')
    ? 'ask'
    : sessionTab === 'preview'
      ? (previewing ? 'preview' : rightViews[0])
      : rightViews.includes(sessionTab) ? sessionTab : rightViews[0];
  const setDock = (tab: DockTab) => {
    setAsking(tab === 'ask');
    if (tab !== 'ask') setDocked((was) => ({ ...was, [dockKey]: tab }));
  };
  // The panel's shown view: the machine's, like its height, not the attended session's.
  const [panelPick, setPanelPick] = useState<ViewId>('console');
  // The view being dragged (DOCK1e), so a region with nothing in it is drawn to be dropped on.
  const [dragging, setDragging] = useState<ViewId | null>(null);
  // 🔴 Told a beat after the drag starts, never inside it: Chromium drops a drag whose page changes
  // under the pointer in the same task as `dragstart`, and drawing an emptied region is such a change.
  const onDrag = (view: ViewId | null) => { window.setTimeout(() => setDragging(view), 0); };
  const panelView = panelViews.includes(panelPick) ? panelPick : panelViews[0];
  // The person opening the dock on a surface — from its strip.
  const openDock = (tab: DockTab) => {
    setDock(tab);
    closeDock(false);
  };
  // A door opening a file's preview (PREVIEW1): the side bar opens on it, over the tab it covers. Handed to
  // the conversation's cards through a context, so one stable function is handed and the latest state read.
  const previewNow = useRef<(file: FileOpen) => void>(() => {});
  previewNow.current = (file: FileOpen) => {
    if (!attended) return;
    const covered = dock && dock !== 'preview' ? dock : previews[attended.id]?.back ?? 'timeline';
    setPreviews((was) => ({ ...was, [attended.id]: { path: file.path, lines: file.lines ?? null, back: covered } }));
    openDock('preview');
  };
  const openPreview = useCallback((file: FileOpen) => previewNow.current(file), []);
  const closePreview = () => {
    if (!attended) return;
    const back = previews[attended.id]?.back ?? 'timeline';
    setPreviews(({ [attended.id]: _closed, ...rest }) => rest);
    if (dock === 'preview') setDock(back);
    // A side bar the preview leaves with nothing in it closes, as one a moved view leaves does (DOCK1b).
    if (rightViews.length === 0) closeDock(true);
  };
  const previewFile = useTreeFile(previewing ? attended!.id : null, previewing?.path ?? null);
  const previewPatch = useReviewedPatch(previewing ? attended!.id : null, previewing?.path ?? null);
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

  const [taken, setTaken] = useState<typeof intent>(null);
  if (intent && intent !== taken) {
    setTaken(intent);
    if (intent === 'start') setStarting(true);
    else openView('review');
  }
  if (!intent && taken) setTaken(null);

  // The console and its streams (CONSOLE2c), for whichever region it stands in. A session with nothing
  // held here says so as a sentence, not as an empty bordered well, which read as a field on the
  // installed window.
  const consoleView = attended && shown
    ? <SessionConsole id={shown} fill quiet={t(shown === attended.id ? 'work.panel.silent' : 'work.panel.streamSilent')} />
    : null;
  const streamTabs = attended ? panelTabs(attended.id, streams) : undefined;
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
  const whose = (flush: boolean) => (elsewhere && attended ? (
    // Flush in the panel, whose body carries its own gutter; inset in the side bar, as its views are.
    <p className={cn('m-0 flex min-w-0 shrink-0 items-center gap-2 border-b border-line py-1.5 text-meta text-ink-faint', flush ? 'mb-2' : 'px-3')}>
      <span className="min-w-0 truncate">
        {t('work.frame.attending', { title: sessionTitle(attended, quest, openings[attended.id]) })}
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
  const withWhose = (surface: ReactNode, flush = false) => (elsewhere && attended ? <>{whose(flush)}{surface}</> : surface);

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
    if (view === 'terminal') return <TerminalView terminals={terminals} cwd={terminalCwd} />;
    if (view === 'review') {
      return withWhose(
        <DiffPane
          session={attended?.id ?? null}
          // What its frame says while git reads (REVIEW4); off Sessions the line above already names the session.
          record={attended}
          title={attended && !elsewhere ? sessionTitle(attended, quest, openings[attended.id]) : null}
          // A tree of its OWN: the repository's checkout is never merged or discarded (UX5 U66).
          hasTree={Boolean(attended && ownTree(attended, (registry.data ?? []).find((row) => row.repository === attended.repository)?.root))}
          onSendBack={attended && (takesWords(box) || onSendBack) ? sendBack : undefined}
          onPreview={attended && here ? (path) => openPreview({ path }) : undefined}
        />,
      );
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
    return attended
      ? withWhose(
        <div className="p-3">
          <SessionTimeline
            session={attended}
            quest={quest}
            // Off Sessions there is no head to carry the parked note, so the timeline keeps it.
            hideCurrentNote={!elsewhere && noteIsInTheHead(attended, here)}
            titled={false}
          />
        </div>,
      )
      : elsewhere
        // Off Sessions there is no session list beside it to point at (audit SE11): it says nothing is
        // attended, and offers the view where one is chosen.
        ? (
          <div className="grid justify-items-start gap-2 p-3">
            <p className="m-0 text-small text-ink-faint">{t('work.frame.none')}</p>
            {onOpenSessions && <Button onClick={onOpenSessions}>{t('work.frame.goSessions')}</Button>}
          </div>
        )
        : <p className="m-0 p-3 text-small text-ink-faint">{t('work.attended.none.body')}</p>;
  };
  // The file previewed for the attended session (PREVIEW1): the host's answer, or its sentence, and the
  // review's patch for that path where the review already holds one.
  const previewSurface = previewing && attended
    ? withWhose(
      <FilePreview
        key={`${attended.id}:${previewing.path}`}
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
  const spawning = roster.find((row) => row.harness === (harnesses.data?.adapter ?? ''));
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
      setPauseAsking(null);
      setStopAsking(id);
    },
    // *Delete…* (SESSUX1f): attended, its ask under its header, as a stop asks.
    delete: (id) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setPauseAsking(null);
      setDeleteAsking(id);
    },
    // *Pause quest…* and *Pause ask…* (PAUSE1e): attended, the pause's ask under its header, as a stop asks.
    pause: (id, target) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setDeleteAsking(null);
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
  const actions = useSessionActs({ notify, doors });

  // The attended session's page header (§3.2): its acts by the one rule, its stop asking under it (§3.3).
  const grouping = attended ? (groups.data ?? []).find((row) => row.session === attended.id) ?? null : null;
  const attendedFacts: ActFacts | null = attended
    ? { session: attended, quest, grouping, root: rootOf(attended.repository), where: where[attended.id] }
    : null;
  const headActs = attendedFacts ? offeredActs(attendedFacts, 'header').filter(actions.can) : [];
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
    actions.work.pause(pausing.target, () => setPauseAsking(null));
  }, [pausing, pauseQuiet, actions.work]);
  useEffect(() => { if (!pausing) pausedQuietly.current = null; }, [pausing]);
  const attendedTitle = attended ? sessionTitle(attended, quest, openings[attended.id]) : '';
  const headPrimary = primaryAct(headActs, grouping);
  // A right-click on the session's page offers its header's acts, in the order the header draws them (CTX1, D138 §4).
  const pageMenu = attended && attendedFacts
    ? {
      label: attendedTitle,
      acts: actMenu(headerActs(headActs, headPrimary).all, t, (act) => actions.run(act, attendedFacts), actions.busy),
    }
    : null;
  const pageHead = attended && attendedFacts && (
    <SessionPageHead
      session={attended}
      title={attendedTitle}
      shown={shownOf(attended, grouping, taking[attended.id])}
      acts={headActs}
      primary={headPrimary}
      busy={actions.busy}
      onAct={(act) => actions.run(act, attendedFacts)}
      asking={stopAsking === attended.id && live && asked ? (
        <StopAsk
          sentence={t(asked.key, asked.values)}
          busy={actions.stopping}
          onStop={() => actions.stopNow(attended, () => setStopAsking(null))}
          onCancel={() => setStopAsking(null)}
        />
      ) : deleteAsking === attended.id && headActs.includes('delete') ? (
        <DeleteAsk
          busy={actions.deleting}
          onDelete={() => actions.deleteNow(attended, () => setDeleteAsking(null))}
          onCancel={() => setDeleteAsking(null)}
        />
      ) : pausing && !pauseQuiet ? (
        <PauseAsk
          className="mt-2.5"
          target={pausing.target}
          lines={pauseLines}
          meanIt={t(pausing.target.scope === 'ask' ? 'asks.record.pauseMeanIt' : 'quests.detail.pauseMeanIt')}
          busy={actions.work.pausing}
          onPause={() => actions.work.pause(pausing.target, () => setPauseAsking(null))}
          onCancel={() => setPauseAsking(null)}
        />
      ) : null}
    />
  );

  // Sessions' list (D118 §3a): the rail, handed to the list pane as every view with a list hands its own.
  // NEW is one control (D56): it was a permanent 287×200 form above the list, 27% of the rail, for
  // something a person does occasionally, and every reference in the study puts new behind a single affordance.
  // The list's own choices (D126 §4.1): by state, the default, or by repository; and whether archived is shown.
  const sessionsFilters = sessionFilters(listed.pane('sessions').filters);
  const sessionsList = (): ListSpec => ({
    view: 'sessions',
    name: t('work.rail.label'),
    labels: { open: t('work.rail.open'), close: t('work.rail.close'), resize: t('work.rail.resize') },
    make: { label: t('work.start.title'), onMake: () => setStarting(true) },
    // The list's ⋯, which it never had (SESSUX1c): how it is grouped, a choice of two, and whether archived sessions
    // are shown, both remembered (D118 §3f); then, below a rule, *Archive what ended…*, which lists first (SESSUX1e).
    more: (
      <ListMore
        label={t('work.list.more')}
        choice={{
          label: t('work.list.groupBy'),
          value: sessionsFilters.group,
          options: [
            { value: 'state', label: t('work.list.byState') },
            { value: 'repository', label: t('work.list.byRepository') },
          ],
          onChoose: (value) => listed.setFilters('sessions', keptSessionFilters({
            ...sessionsFilters, group: value === 'repository' ? 'repository' : 'state',
          })),
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
    chosen: selected,
    // What waits on the person, then what runs, in the open list's order (FRAME6; D126 §2.5).
    strip: (
      <SessionRail
        selected={selected}
        onSelect={(id) => { chosen(); attend(id); }}
        notify={notify}
        compact
        taking={taking}
        arrangement={sessionsFilters.group}
        archived={sessionsFilters.archived}
      />
    ),
    body: (
      <SessionRail
        selected={selected}
        onSelect={(id) => { chosen(); attend(id); }}
        notify={notify}
        taking={taking}
        lastTurns={lastTurns}
        arrangement={sessionsFilters.group}
        archived={sessionsFilters.archived}
        archiveEnded={archivingEnded}
        onArchiveEnded={() => setArchivingEnded(false)}
        // A row's acts that are this frame's: answering, asking to stop or to delete, reviewing, a terminal there (SESSUX1d, SESSUX1f).
        doors={doors}
      />
    ),
  });
  const spec = elsewhere ? viewLayout.list : sessionsList();

  return (
    // Positioned, so a dock filling the frame (FRAME6) lies over exactly this and nothing more.
    // 🔴 `min-w-0` (USE1): a flex item is otherwise no narrower than its content, and every view but
    // Sessions is drawn in here, so a long title that should truncate widened the frame past the
    // window (seen on the install's Quests view; a browser draws the view outside the frame).
    // `isolate` (D118 §3d, audit F10): its layers stay its own — a full side bar's `z-20`, a list laid
    // over — so every overlay drawn at the page's root, a drawer among them, lies above all of it.
    <div ref={root} className="relative isolate flex min-h-0 min-w-0 flex-1">
      {spec && list && <ViewListPane spec={spec} layout={list} lists={listed} onOver={setListOver} />}

      {/* D41's single detail-and-form surface (§4), rather than a popover built for one form. */}
      {starting && (
        <Drawer title={t('work.start.title')} onClose={() => { setStarting(false); setStartRefusal(null); }}>
          <StartSession
            // A checkout on this machine is the whole question: there is nowhere else to talk, and
            // a teammate's mirrored registration has no tree here (D48 §3/§7).
            repositories={(registry.data ?? [])
              .filter((row) => Boolean(row.root))
              .map((row) => row.repository)
              .sort()}
            // A checkout an active session on this machine holds refuses a second one (UX5 U68).
            busy={(sessions.data ?? [])
              .filter((session) => SESSION_ACTIVE.has(session.state) && sessionOrigin(session) === null
                && !ownTree(session, (registry.data ?? []).find((row) => row.repository === session.repository)?.root))
              .map((session) => session.repository)}
            harnesses={roster.filter((row) => row.present).map((row) => row.harness)}
            labels={Object.fromEntries(roster.map((row) => [row.harness, doorLabel(t, row)]))}
            defaultHarness={spawning?.harness}
            accounts={Object.fromEntries(roster.map((row) => [row.harness, Array.isArray(row.profiles) ? row.profiles : []]))}
            pending={startChat.isPending}
            refusal={startRefusal}
            onStart={onStart}
          />
        </Drawer>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        {elsewhere ? viewLayout.main : (<>
        <ViewMain
          ref={centre}
          gutters="session"
          // Pinned at the top while the conversation scrolls under it (D126 §3.2); none with nothing attended.
          header={pageHead || undefined}
          menu={pageMenu}
          // A remembered session is found in the list, so while the list first loads it is on its way, never
          // *Nothing attended* (audit SE11).
          state={attended ? 'chosen' : selected && sessions.isPending ? 'loading' : 'none'}
          none={{
            headline: t('work.attended.none.headline'),
            body: t('work.attended.none.body'),
            action: <Button onClick={() => setStarting(true)}>{t('work.start.title')}</Button>,
          }}
        >
          <AttendedSession
            session={attended}
            quest={quest}
            opening={attended ? openings[attended.id] : null}
            taking={attended ? taking[attended.id] : undefined}
            lastTurn={attended ? lastTurns[attended.id] : undefined}
            // The page header above carries its state, its id and its stop (D126 §3.2): said once.
            headed
            resolving={resolve.isPending || answer.isPending || parkGoAhead.isPending}
            onResolve={here ? onResolve : undefined}
            onAnswerSession={here && !answering ? onAnswerSession : undefined}
            onAnswerAsk={here ? onAnswerAsk : undefined}
            // The go-aheads it asked, answerable only where this machine can answer the park (KNOWUSE1a2).
            goAheads={goAheads}
            onGoAhead={here ? onGoAhead : undefined}
            chain={quest ? buildChain(quest.id, quests.data ?? [], sessions.data ?? []) : []}
            relations={attended ? relationsOf(attended, quest, quests.data ?? [], sessions.data ?? []) : undefined}
            onSession={(session) => attend(session.id)}
            onQuest={onOpenQuest ? (row) => onOpenQuest(row.id) : undefined}
            // Its review, wherever the review stands (SESS2 H4): the head's move beside unlanded work.
            onReview={() => openView('review')}
            branch={branch}
          />
          {/* The conversation (D76): below the record, in the same scroll, so the head is read once
              and the words are what the region follows. Only where the session ran: a teammate's
              record came without its transcript, which stays on their machine (D47 §4). */}
          {attended && here && (
            <div className="mt-4 min-w-0">
              {/* A file a tool card names opens in the side bar's preview (PREVIEW1, D111). */}
              <FileOpener.Provider value={openPreview}>
                <SessionConversation
                  session={attended.id}
                  adapter={attended.adapter}
                  chat={attended.kind === 'chat'}
                  tree={attended.tree}
                  live={live}
                  // A conversation the driver answers for is working while a turn is in flight, and not
                  // under words its agent said after the turn ended (found looking at CONSOLE2).
                  turnRunning={talking && chatTurns[attended.id] ? turns.taking : undefined}
                  scroller={centre}
                  onUsage={onUsage}
                  // Where the person's words went, a door to that session; and words that cannot go on here, one press
                  // that starts a conversation with them, where a chat can start here (MSG1f, D137 §3.1, §2.2).
                  onSession={(id) => attend(id)}
                  onStartFrom={rootOf(attended.repository) ? onStartFrom : undefined}
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
              </FileOpener.Provider>
            </div>
          )}
        </ViewMain>

        {talking && attended && box.kind === 'chat' && (
          <Composer
            // Per session: the files attached in one conversation never follow the person to another.
            key={attended.id}
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
            taking={turns.taking}
            opening={turns.opening}
            // Only a door that can see a turn end can stop one; the roster says which (CONV3b).
            stoppable={roster.find((row) => row.harness === attended.adapter)?.structured === true}
            stopping={cancelTurn.isPending}
            onStopTurn={onStopTurn}
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
            stopping={cancelTurn.isPending}
            // *Answer…* from its row, and *Send back…* from its review, put the focus here (D126 §3.1, D137 §5.1).
            focus={answerFocus?.session === attended.id ? answerFocus.at : 0}
            accepts={accepts}
            onSend={onSay}
            onSendNow={onSteerNow}
          />
        )}

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

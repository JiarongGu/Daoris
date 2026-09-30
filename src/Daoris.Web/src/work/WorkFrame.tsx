import { type ReactNode, type RefObject, useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SessionConsole } from '../SessionConsole';
import { sentence } from '../format';
import { buildChain } from '../map/chain';
import { useAnswerSession, useQuests, useRegistry, useSessions } from '../queries';
import {
  stopNotice, type TurnStop, useCancelTurn, useEndChat, useHarnesses, useResolveSession, useSendMessage,
  NO_TURNS, useChatTurns, useSessionOpenings, useSessionOptions, useSessionStreams, useSetSessionOption, useStartChat,
  useStopSession, useStopTask, useSweepPlan, useTreeFiles, useTreeFile, useReviewedPatch,
  logEvent, useTerminals,
} from '../shell';
import { FilePreview } from './FilePreview';
import { type FileOpen, FileOpener, fileName } from './preview';
import { TerminalView } from './TerminalView';
import { Drawer, failure, type Notify, SESSION_ACTIVE, useErrorNotify } from '../ui';
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
import { StartSession, type StartChoice } from './StartSession';
import { OutputPanel, PANEL_MIN, StreamTabs } from './frame';
import { panelTabs } from './streams';
import { DOCK, dockRange, frameLayout, LIST_BOUNDS, type ListMode, listToggled } from './layout';
import { ListPane } from './ListPane';
import { type FrameClosings, useFrameClosings } from './closings';
import { type Place, type Placements, usePlacements, type ViewId, viewsIn } from './placements';
import { relationsOf } from './relations';
import { store, stored } from '../lib/stored';
import { cn } from '../lib/cn';

// Per-viewer conveniences, like the language and the workspace scope (D42): a remembered layout is
// a preference, never machine wiring and never a tracked file.
const PANEL_HEIGHT = 'daoris.panelHeight';
// The frame's columns (FRAME6): the widths the person dragged, and what they closed.
const RAIL_WIDTH = 'daoris.railWidth';
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
 * How wide the window is and how wide this frame is (FRAME6): the window decides the one threshold
 * left, a full dock under 768 px, and the frame decides the room, which is what makes the list a strip
 * (D118). Where nothing measures the frame (a unit test's DOM), it is the window less the 48px activity
 * bar beside it.
 */
function useFrameWidth(frame: RefObject<HTMLDivElement | null>) {
  const [viewport, setViewport] = useState(() => window.innerWidth);
  const [measured, setMeasured] = useState(0);

  useEffect(() => {
    const onResize = () => setViewport(window.innerWidth);
    window.addEventListener('resize', onResize);
    const element = frame.current;
    if (element) setMeasured(element.getBoundingClientRect().width);
    const observer = element && typeof ResizeObserver !== 'undefined'
      ? new ResizeObserver(([entry]) => { if (entry) setMeasured(entry.contentRect.width); })
      : null;
    if (element) observer?.observe(element);
    return () => {
      window.removeEventListener('resize', onResize);
      observer?.disconnect();
    };
  }, [frame]);

  return { viewport, frame: measured > 0 ? measured : Math.max(0, viewport - 48) };
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
 */
export function WorkFrame({
  selected, onSelect, notify, onSendBack, onAnswerAsk, onOpenQuest, intent, onIntentTaken, ask, askFocus = 0, closings, placements,
  content, onOpenSessions, terminal = false, onListMode,
}: {
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
   * Another view's content in the frame's centre (DOCK1a): Overview, Quests and the rest keep the right side
   * bar and the panel, with every view that stands in them, as VS Code's workbench is one frame whatever
   * its editor shows. No rail then: the session list is Sessions' own. Absent, the centre is Sessions'.
   */
  content?: ReactNode;
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
  // Why the last start was refused, said in the start form until it closes or starts again (UX5 U68).
  const [startRefusal, setStartRefusal] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  // The centre scrolls the head and the conversation together; the conversation follows its tail.
  const centre = useRef<HTMLDivElement>(null);
  // Which dock surface each session has up (FRAME6: tabs per session). Not remembered across launches:
  // unlike the attended session, this one is answered by what the person is doing in the next ten seconds.
  const [docked, setDocked] = useState<Record<string, DockTab>>({});
  const [height, setHeight] = useState(() => remembered(PANEL_HEIGHT, 200));

  // The frame's columns (FRAME6): what the person chose, and what the window leaves room for.
  const root = useRef<HTMLDivElement>(null);
  const width = useFrameWidth(root);
  const [railWidth, setRailWidth] = useState(() => rememberedWidth(RAIL_WIDTH));
  const [dockShare, setDockShare] = useState(rememberedShare);
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
  // Another view in the centre (DOCK1a): the frame without Sessions' rail.
  const elsewhere = content !== undefined;
  const layout = frameLayout(width.viewport, width.frame, {
    list: elsewhere ? null : { bounds: LIST_BOUNDS.sessions, width: railWidth, closed: closed.list, over: closed.listOver },
    dockShare, dockClosed, dockFull,
  });
  const list = layout.list;

  // What the list is now, told to the application, whose doors toggle it and say whether it is shown.
  const listMode = list?.mode ?? null;
  useEffect(() => { onListMode?.(listMode); }, [listMode, onListMode]);
  // A list laid over the main area is never kept: room returning, or another view, lets it go (D118 §3f).
  const { listOver, setListOver } = closed;
  useEffect(() => { if (listOver && listMode !== 'over') setListOver(false); }, [listOver, listMode, setListOver]);

  const resizeRail = (next: number | null) => {
    setRailWidth(next);
    store(RAIL_WIDTH, next === null ? null : String(next));
  };
  /** The strip's open: beside where there is room, over the main area where the window drew the strip. */
  const toggleList = () => {
    if (!list) return;
    const next = listToggled(list);
    closed.setList(next.closed);
    closed.setListOver(next.over);
  };
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
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const harnesses = useHarnesses();

  const startChat = useStartChat();
  const resolve = useResolveSession();
  const answer = useAnswerSession();
  const send = useSendMessage();
  const end = useEndChat();
  const stop = useStopSession();
  const cancelTurn = useCancelTurn();

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
  // refuses such a line too; the frame offers no box, and the head carries the stop.
  const intake = attended ? isIntake(attended) : false;
  const talking = Boolean(attended && conversation && here && !intake);
  // 🔴 A driven session parked to ask the person is answered from the box at the foot, where a chat's
  // is: the door was a button at the top of a record of 1,800 events, and the question is read at its
  // foot. The card above keeps the
  // endings and says the box carries it on: one owner for the answer (D56).
  const answering = Boolean(attended && here && !intake && !conversation && attended.quest
    && attended.state === 'awaiting-person');
  const [answerDraft, setAnswerDraft] = useState('');
  // A driven session still working may be told something (SESS3): its words are held and are its next prompt. Offered only
  // where the driver says it listens, which is the protocol door; the pipe door has nothing to hear it.
  const steerable = Boolean(attended && here && !intake && !conversation && attended.quest
    && SESSION_ACTIVE.has(attended.state) && attended.state !== 'awaiting-person');
  const [steerDraft, setSteerDraft] = useState('');

  // What the person was typing to this conversation, kept per session and across a reload (CONV4b).
  const [draft, setDraft] = useDraft(talking ? attended!.id : null);
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

  const onSend = (text: string, files: File[] = []) => {
    if (!attended) return;
    const session = attended.id;
    // Counted into the machine log, never its words (LOG1b): its length, and how many files it carried.
    logEvent('message.sent', { session, kind: 'chat', length: text.length, files: files.length });
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
          setRefusal(null);
          return;
        }
        giveBack();
        setRefusal({ session, text: t('work.composer.notListening') });
      },
      onError: (error: unknown) => {
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };

  // What the person adds to a driven session while it works (SESS3): held by the driver for its turn's
  // end. A send that did not arrive gives the words back to the box, as a message does (REV3).
  const onSteer = (text: string) => {
    if (!attended || !text) return;
    const session = attended.id;
    logEvent('message.sent', { session, kind: 'steer', length: text.length, files: 0 });
    send.mutate({ id: session, text, files: [] }, {
      onSuccess: (result) => {
        if (result.sent) {
          setRefusal(null);
          return;
        }
        setSteerDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
        setRefusal({ session, text: t('work.steer.gone') });
      },
      onError: (error: unknown) => {
        setSteerDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
        notify(sentence(error), 'error');
      },
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

  // Cutting a live session off — a conversation's, from its composer, or a running intake's, from
  // the head, since an intake has no composer to carry it (INT4h).
  const onStop = () => {
    if (!attended) return;
    stop.mutate(attended.id, {
      onSuccess: (answer) => notify(t(stopNotice(answer), { id: attended.id })),
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

  // The answer to a driven session that parked to ask the person (STANDDOWN2): the record ends with
  // their words, and its quest is carried on in the same tree at the driver's next tick.
  const onAnswerSession = (words: string | null) => {
    if (!attended) return;
    answer.mutate({ id: attended.id, answer: words }, {
      onSuccess: () => notify(t('work.awaiting.answered')),
      onError: (error) => {
        // 🔴 The box at the foot let go of the words when it sent; an answer that did not arrive hands
        // them back, as a message does (REV3).
        if (words) setAnswerDraft((was) => [words, was.trim()].filter(Boolean).join('\n\n'));
        failure(notify)(error);
      },
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

  /** A view's surface, drawn in whichever region it stands (DOCK1b). The panel draws the console itself. */
  const surface = (view: ViewId, where: Place = 'panel') => {
    if (view === 'ask') return ask;
    if (view === 'terminal') return <TerminalView terminals={terminals} cwd={terminalCwd} />;
    if (view === 'review') {
      return withWhose(
        <DiffPane
          session={attended?.id ?? null}
          // A tree of its OWN: the repository's checkout is never merged or discarded (UX5 U66).
          hasTree={Boolean(attended && ownTree(attended, (registry.data ?? []).find((row) => row.repository === attended.repository)?.root))}
          onSendBack={onSendBack && attended
            ? () => onSendBack(attended.repository)
            : undefined}
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

  return (
    // Positioned, so a dock filling the frame (FRAME6) lies over exactly this and nothing more.
    // 🔴 `min-w-0` (USE1): a flex item is otherwise no narrower than its content, and every view but
    // Sessions is drawn in here, so a long title that should truncate widened the frame past the
    // window (seen on the install's Quests view; a browser draws the view outside the frame).
    <div ref={root} className="relative flex min-h-0 min-w-0 flex-1">
      {list && (
        // Sessions' list (D118 §3a): the rail on the list pane every view with a list shares. NEW is one
        // control (D56): it was a permanent 287×200 form above the list, 27% of the rail, for something a
        // person does occasionally, and every reference in the study puts new behind a single affordance.
        <ListPane
          name={t('work.rail.label')}
          labels={{ open: t('work.rail.open'), close: t('work.rail.close'), resize: t('work.rail.resize') }}
          layout={list}
          bounds={LIST_BOUNDS.sessions}
          make={{ label: t('work.start.title'), onMake: () => { chosen(); setStarting(true); } }}
          loading={sessions.isPending}
          // Each running session's initial and mark, in the open rail's order (FRAME6).
          strip={<SessionRail selected={selected} onSelect={(id) => { chosen(); attend(id); }} notify={notify} compact taking={taking} />}
          onOpen={toggleList}
          onClose={() => closed.setList(true)}
          onDismiss={() => setListOver(false)}
          onResize={resizeRail}
        >
          <SessionRail
            selected={selected}
            onSelect={(id) => { chosen(); attend(id); }}
            notify={notify}
            taking={taking}
            lastTurns={lastTurns}
            // A row's menu reviews that session: attended, with the dock open on its work.
            onReview={(id) => {
              chosen();
              attend(id);
              setDocked((was) => ({ ...was, [id]: 'review' }));
              closeDock(false);
            }}
          />
        </ListPane>
      )}

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
        {elsewhere ? content : (<>
        <div ref={centre} data-region="main" className="flex min-h-0 flex-1 flex-col overflow-y-auto px-4 py-3">
          <AttendedSession
            session={attended}
            quest={quest}
            opening={attended ? openings[attended.id] : null}
            taking={attended ? taking[attended.id] : undefined}
            lastTurn={attended ? lastTurns[attended.id] : undefined}
            resolving={resolve.isPending || answer.isPending}
            stopping={stop.isPending}
            onResolve={here ? onResolve : undefined}
            onAnswerSession={here && !answering ? onAnswerSession : undefined}
            onAnswerAsk={here ? onAnswerAsk : undefined}
            onStop={here && intake ? onStop : undefined}
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
                />
              </FileOpener.Provider>
            </div>
          )}
        </div>

        {talking && attended && (
          <Composer
            // Per session: the files attached in one conversation never follow the person to another.
            key={attended.id}
            live={live}
            sending={send.isPending}
            refusal={refusal?.session === attended.id ? refusal.text : null}
            // One owner for the moves at a time (D56): while the session is parked the attention
            // band above holds finish, decline and stop, and this form keeps `send` alone.
            endings={attended.state !== 'awaiting-person'}
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
            onStop={onStop}
          />
        )}

        {steering && attended && held && (
          <Composer
            key={`steer:${attended.id}`}
            live
            endings={false}
            attachments={false}
            sending={send.isPending}
            refusal={refusal?.session === attended.id ? refusal.text : null}
            placeholder={t('work.steer.placeholder')}
            draft={steerDraft}
            onDraft={setSteerDraft}
            queued={held.queued}
            taking={held.taking}
            // Only something held can be sent now; with nothing held, stopping the turn would end the work.
            stoppable={held.queued.length > 0}
            stopping={cancelTurn.isPending}
            stopTurnLabel={t('work.steer.now')}
            stopTurnTip={t('work.steer.nowTip')}
            onStopTurn={onSteerNow}
            onSend={(text) => onSteer(text.trim())}
            onFinish={() => {}}
            onStop={() => {}}
          />
        )}

        {answering && attended && (
          <Composer
            key={`answer:${attended.id}`}
            live
            endings={false}
            attachments={false}
            sending={answer.isPending}
            placeholder={t('work.awaiting.answerPlaceholder')}
            sendLabel={t('work.awaiting.answerConfirm')}
            draft={answerDraft}
            onDraft={setAnswerDraft}
            onSend={(text) => onAnswerSession(text.trim() || null)}
            onFinish={() => {}}
            onStop={() => {}}
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

import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Compose, OpenGroup } from '../app/opener';
import { useAsks } from '../data/queries';
import {
  useSessionEvents, usePushedCalls, withCall, useRemotes, useRunNodeWord, useTerminals,
} from '../bridge/shell';
import { useAttendedSession } from './head/useAttendedSession';
import { useAttendedRecord } from './head/useAttendedRecord';
import { useAttendedBox } from './conversation/useAttendedBox';
import { type Refusal, useWords } from './conversation/useWords';
import { useBoxWords } from './conversation/useBoxWords';
import { useHeadAsks } from './head/useHeadAsks';
import { useSessionLanding } from './review/useSessionLanding';
import { useAttendedWaits } from './waits/useAttendedWaits';
import { useAttendedHead } from './head/useAttendedHead';
import { type Sent, useSending, useSettled } from './conversation/sending';
import { useSessionAtWork } from '../bridge/sessions/atWork';
import { WaitStrip } from './waits/WaitStrip';
import { WaitPanel } from './waits/WaitPanel';
import { namer } from '../settings/accounts/namer';
import { coolingFor } from './say';
import type { FrameIntent } from '../menus/commands';
import { BackToBottom, ToTheTop } from './conversation/SessionConversation';
import { useNow } from './useNow';
import { useActors, useHeavySlot } from '../bridge/driver';
import { heavyPartOf } from './heavySlot';
import { heavyRow } from './HeavySlotLine';
import { sessionTitle } from './identity';
import { RightDock } from './frame/RightDock';
import { SESSION_ACTIVE, type Notify } from '../design/ui';
import { useSessionActs } from './sessionActs';
import { useNewStart } from './start/useNewStart';
import { StartPane } from './start/StartPane';
import { agentRosterOf } from './start/agentRoster';
import { OutputPanel, PANEL_MIN } from './frame/frame';
import { dockRange, type ListMode } from './frame/layout';
import type { FrameClosings } from './frame/closings';
import type { ListPanes } from './frame/listPanes';
import { type ViewLayout, ViewListPane } from './frame/ViewFrame';
import { ViewMain } from './frame/ViewMain';
import type { Placements } from './frame/placements';
import { fileName } from './review/preview';
import { useFrameLayout } from './frame/useFrameLayout';
import { useDockViews } from './frame/useDockViews';
import { useRegionSurfaces } from './frame/regionSurface';
import { useSessionsList } from './rail/useSessionsList';
import { AttendedPane } from './head/AttendedPane';
import { partRowsOf } from './head/partRows';
import { AttendedConversation } from './conversation/AttendedConversation';
import { AttendedBoxes } from './conversation/AttendedBoxes';
import { useSeekPart } from './conversation/useSeekPart';

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
  // The centre scrolls the head and the conversation together; the conversation follows its tail.
  const centre = useRef<HTMLElement>(null);
  const frame = useFrameLayout({ lists, closings, placements, viewLayout, group, onGroupBrought, onListMode });
  const { root, width, listed, placed, collapsed, dockClosed, dockFull, setDockFull, go, layout, list, chosen, resizeDock, closeDock } = frame;
  // Another view in the frame (DOCK1a): its own list or none, and its main area. Absent, it is Sessions.
  const elsewhere = viewLayout !== undefined;

  const session = useAttendedSession({ selected, onSelect, notify });
  const {
    sessions, openings, groups, entries, where, quests, registry, harnesses, accounts, attended, attendedEntry, channelAsk, quest, live,
    conversation, here, intake, oneTurn,
  } = session;
  const start = useNewStart({ t, notify, attended, selected, onSelect, compose, attend, setRefusal });
  const { newConversationPressed } = start;

  // Which workspaces sync with a remote here: a pause's ask names an open quest another machine may still take (PAUSE1e).
  const wiring = useRemotes().data;

  const record = useAttendedRecord({ attended, attendedEntry, channelAsk, where, here, intake });
  const { spent, branch, integration } = record;

  // The person's own terminals (CONSOLE4b), held here rather than by their view, which unmounts whenever
  // it moves or another view of its region is shown: a shell outlives that, as VS Code's terminal does.
  const terminals = useTerminals();

  // The ask its entry came from, for the head's name: the rail's read, so it costs nothing it does not already ask.
  const entryAsks = useAsks(true, channelAsk !== null);
  const boxState = useAttendedBox({
    t, attended, here, intake, oneTurn, conversation, live, quest, sessions: sessions.data, attendedEntry, groups: groups.data,
  });
  const { goAheads, question, talking, draft, setDraft, chatTurns, box, stands, answering, taking, waiting, parkAsk } = boxState;
  const words = useWords({
    t, notify, attended, here, oneTurn, conversation, live, box, chatTurns, draft, setDraft, setRefusal, sessions, attendedEntry, stands,
    attend,
  });
  const { say, sending, giveBackSent } = words;
  const roster = Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : [];
  const boxWords = useBoxWords({ t, notify, attended, box, answering, question, goAheads, parkAsk, draft, setDraft, setRefusal, roster, words });

  // 🔴 Consumed by IDENTITY, the way the quest composer's opening draft is (FIX-LOG 2026-09-23):
  // "if (intent) set…" during render made React re-run this frame with the same prop on every pass,
  // so the palette's ask never landed. The parent is told from an effect, not during this render.
  const views = useDockViews({
    attended, elsewhere, here, ask, notifications, terminal, placed, closed: frame.closed, collapsed, dockClosed, closeDock, askFocus,
    notificationsShow, notificationsToggle,
  });
  const {
    setDocked, following, rightViews, panelViews, previewing, dock, setDock, setPanelPick, dragging, onDrag, panelView, openDock,
    openPreview, closePreview, openView, moveView,
  } = views;
  // Who is at work on the followed session (COWORKVIEW1a), asked whatever its state and whether *At work* shows (COMMS1i): the head's
  // *Waiting on* is this read's line, and *working with it* (§3.7) its count, at its mount and each `atWork` wake.
  const atWork = useSessionAtWork(following?.id ?? null);
  const atWorkHeld = atWork.data;
  const withIt = atWorkHeld && 'answer' in atWorkHeld
    ? atWorkHeld.answer.subagents.working + atWorkHeld.answer.parts.filter((part) => part.key !== atWorkHeld.answer.attended && part.state === 'working').length
    : 0;
  // FABRIC-CUTOVER1f: the person's skip or stop of a graph run's node, pressed under the node in the view, kept for the driver's next look.
  const runNodeWord = useRunNodeWord();

  const [taken, setTaken] = useState<typeof intent>(null);
  if (intent && intent !== taken) {
    setTaken(intent);
    if (intent === 'start') newConversationPressed();
    else if (intent === 'review') openView('review');
    else if (intent === 'workflow') openView('workflow');
    else if (intent === 'atWork') openView('atWork');
  }
  if (!intent && taken) setTaken(null);

  // Where a terminal opened here starts (D96): the attended session's tree (a record with none works in
  // its repository's root), else the first repository of the workspace in scope that has a checkout here.
  // Said by the page, which knows what is attended and what the viewer is scoped to; with nothing to say,
  // the module opens it in the home, and it opens there too when this folder is not on the machine.
  const rootOf = (repository: string) => (registry.data ?? []).find((row) => row.repository === repository)?.root;
  const terminalCwd = (attended && here ? attended.tree ?? rootOf(attended.repository) : undefined)
    ?? [...(registry.data ?? [])].filter((row) => row.root).sort((a, b) => a.repository.localeCompare(b.repository))[0]?.root
    ?? undefined;
  useEffect(() => { if (intent) onIntentTaken?.(); }, [intent, onIntentTaken]);

  // Each actor's order (MODELROLE1e): the hand-over's offer and its ask draw from the session's actor's, empty in a browser.
  const actors = useActors();
  // What a person calls an account (ACCTNAME1, D152 §4.2): the roster's one namer, so the head's facts, its Details, its
  // chain and its trace say the name the agent's page leads its row with.
  const nameOf = namer(t, roster);
  const agentRoster = agentRosterOf(t, roster, accounts.data);
  // Words a cooling account holds (MSG1g2, D137 §2.2): a driven record of this machine's between runs, parked or ended, whose
  // own account cools. A running one hears words at its door, and a chat's go on with the next word said to it.
  const between = attended && here && attended.kind !== 'chat'
    && !(SESSION_ACTIVE.has(attended.state) && attended.state !== 'awaiting-person');
  const coolsUntil = between ? coolingFor(attended, roster, accounts.data, new Date()) : null;

  const asks = useHeadAsks({ selected, chosen, attend, setDocked, closeDock, terminal, terminals, openView });
  const { setArchivingEnded, doors } = asks;
  const actions = useSessionActs({ notify, doors, entries });

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
  const work = useSessionLanding({
    t, notify, attended, here, grouping, talking, registry: registry.data, chatTurns, quests: quests.data, onOpenQuest, runNodeWord,
    attendedEntry, channelAsk, attend, openPreview, groups: groups.data,
  });
  const { entryAt, workAskAt, entryOffered, landsNow } = work;
  const waitsState = useAttendedWaits({
    t, notify, attended, here, intake, agentRoster, actions, chosen, attend, box, say, answering, question, goAheads, onAnswerAsk,
    boxWords, openView, work, talking, centre,
  });
  const {
    partnerPieces, waits, answerBelow, standing, toEvent, waitsOnScreen, goToWait, seekAt, ruleAskAt, waitPanel, tail, awayFromTop, toTop,
  } = waitsState;
  const headState = useAttendedHead({
    t, language: i18n.language, attended, here, live, quest, grouping, registry: registry.data, roster, accounts: accounts.data, nameOf,
    actions, doors, asks, actors: actors.data, wiring, entryAsks: entryAsks.data, channelAsk, attendedEntry, openings,
    sessions: sessions.data, quests: quests.data, branch, partnerPieces, agentRoster, waiting, following, withIt, atWorkHeld, heavyPart,
    heavyTitleOf, integration, entryOffered, landsNow, standing, goToWait, chosen, rootOf, where, taking,
  });
  const { limitLine, pageMenu, pageHead } = headState;

  const regions = useRegionSurfaces({
    t, notify, ask, notifications, elsewhere, onOpenSessions, onOpenQuest, onAnswerAsk, onOpenBranch, onSendBack, terminals, terminalCwd,
    session, record, work, views, frame, boxState, asks, agentRoster, attend, runNodeWord, atWork,
  });
  const { surface, previewSurface, consoleView, streamTabs, shown, pickStream, stopStream, withWhose } = regions;
  const spec = useSessionsList({
    t, notify, selected, elsewhere, viewLayout, group, onGroupBrought, session, frame, boxState, asks, chosen, attend, newConversationPressed,
  });
  const partRows = partRowsOf(sessions.data, rootOf, where, spent);
  const { partBox, seek } = useSeekPart();

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
              defaultHarness={roster.find((row) => row.harness === (harnesses.data?.adapter ?? ''))?.harness}
              notify={notify}
              onMakeTask={onMakeTask}
              attend={attend}
            />
          )}
          <AttendedPane
            t={t}
            notify={notify}
            session={session}
            record={record}
            work={work}
            boxState={boxState}
            waitsState={waitsState}
            headState={headState}
            roster={roster}
            nameOf={nameOf}
            agentRoster={agentRoster}
            following={following}
            atWorkHeld={atWorkHeld}
            heavy={attended && heavyPart ? heavyRow(t, heavySlot.data, attended.id, heavyTitleOf, attend) : null}
            partRows={partRows}
            onShowPart={(id) => { attend(id); seek(); }}
            openView={openView}
            attend={attend}
            onOpenQuest={onOpenQuest}
            onAnswerAsk={onAnswerAsk}
            onOpenBranch={onOpenBranch}
          />
          <AttendedConversation
            t={t}
            session={session}
            boxState={boxState}
            words={words}
            waitsState={waitsState}
            headState={headState}
            grouping={grouping}
            coolsUntil={coolsUntil}
            roster={roster}
            rootOf={rootOf}
            openPreview={openPreview}
            centre={centre}
            partRows={partRows}
            partBox={partBox}
            attend={attend}
          />
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

          <AttendedBoxes
            t={t}
            notify={notify}
            session={session}
            boxState={boxState}
            words={words}
            boxWords={boxWords}
            asks={asks}
            waitsState={waitsState}
            headState={headState}
            refusal={refusal}
            roster={roster}
            rootOf={rootOf}
            attend={attend}
          />
        </WaitPanel>

        </>)}

        {/* An emptied panel that is hidden is not drawn at all, unless a view is being dragged to it;
            opened, it says how to fill it. */}
        {(panelViews.length > 0 || !collapsed || dragging !== null) && (
          <OutputPanel
            console={consoleView ? withWhose(consoleView, true) : null}
            height={Math.max(PANEL_MIN, frame.height)}
            collapsed={collapsed}
            onResize={frame.resize}
            onToggle={frame.toggle}
            tabs={streamTabs}
            selected={shown ?? undefined}
            onStop={stopStream}
            onSelect={(key) => {
              pickStream(key);
              // Picking what to read is asking to read it: a hidden panel opens.
              if (collapsed) frame.toggle();
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

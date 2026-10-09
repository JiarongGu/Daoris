import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  useAsks, useQuests, useRefreshIndex, useRegistry, useRepositories, useSessions,
  useStatus, useSyncStanding, useWorkspaceHoldings, useWorkspaces,
} from './queries';
import { useScope } from './scope';
import { AskDaoris } from './help/AskDaoris';
import type { AskOpening } from './help/AskConversation';
import { QuickAsk } from './help/QuickAsk';
import { ContextMenus } from './menus/ContextMenu';
import { clipped, type ContextDoors, isTextField } from './menus/press';
import { MainOffer, offerStore, useMainOffer } from './menus/mainOffer';
import { fieldTracker, findTarget, runEdit } from './menus/editing';
import { opensAtStart, setupProgress, setupSteps } from './help/setup';
import { useMachine } from './help/useMachine';
import { useSetupAtStart } from './setupGuide';
import { useFrameClosings } from './work/closings';
import { type ListMode, type ListView, listToggled } from './work/layout';
import { useListPanes } from './work/listPanes';
import { usePlacements, viewsIn } from './work/placements';
import { type LayoutRegion, LayoutToggles } from './work/LayoutToggles';
import { commandForKey } from './shortcuts';
import { focusRegion } from './work/regions';
import type { StarterDoor } from './help/starters';
import {
  askItem, doorOpening, type Opening, type OpenPart, opening as plannedOpening, questsItem, workspaceItem,
} from './opener';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';
import { Drawer, failure, Prose, SESSION_ACTIVE, Toasts, useToasts } from './ui';
import { OverviewView } from './OverviewView';
import { useKnowledgeMode, useKnowledgeView } from './KnowledgeView';
import { MapView } from './MapView';
import { useQuestsView } from './QuestsView';
import { useProjectsView } from './ProjectsView';
import {
  type ProjectTab, readProjectTab, readWorkspaceTab, storeProjectTab, storeWorkspaceTab, type WorkspaceSection, type WorkspaceTab,
} from './projects/tabs';
import { type SettingsAnchor, type SettingsSection, useSettingsLayout } from './SettingsView';
import { ShellSignals } from './ShellSignals';
import {
  logEvent, useConsidered, useDismissUpdate, useDriver, useHarnesses, useLinkOpener, useOpenBrowser, useOpenWindow, useRemotes,
  useRules, useSayUpdate, useSessionGroups, useShowReviewAgain, useSyncNow, useUntrusted, useUpdateState,
} from './shell';
import { byTool } from './tools';
import { UpdateBanner } from './update/UpdateBanner';
import { LinkOpener } from './links';
import { BrowserDoor } from './work/BrowserDoor';
import { browserDrivers } from './work/browserDrivers';
import { ReviewChip } from './work/ReviewChip';
import { newestSetUp, setUpRef } from './work/review';
import { useReviewActs } from './work/reviewActs';
import { menuRows } from './work/appMenus';
import { KeyboardShortcuts } from './work/KeyboardShortcuts';
import { offeredActs } from './work/acts';
import { SyncStatus } from './work/SyncStatus';
import { MONITOR_WINDOW } from './work/window';
import { WorkFrame } from './work/WorkFrame';
import { ViewFrame, type ViewLayout } from './work/ViewFrame';
import { ViewMain } from './work/ViewMain';
import { type AttentionDoors, useAccountsKnown } from './work/AttentionBand';
import type { Attention } from './work/AttentionRow';
import { needsAPerson, waitingInSessions } from './work/attention';
import { ActivityBar, AppStrip, type DriverPresence, StatusBar } from './work/frame';
import { useWindowChrome } from './windowChrome';
import {
  type Command, type CommandDoors, type CommandState, commandTable, type FrameIntent, listOf, MENUS, paletteCommands, shortcutGroups,
  type View, VIEWS,
} from './commands';
import { CommandPalette } from './work/CommandPalette';
import { CommandCenter } from './work/CommandCenter';
import { AppMenuBar, type BarMenu, focusMenuBar, useMenuFold } from './work/AppMenu';
import { useThemeChoice } from './theme';
import { rememberedDomain, SETTINGS_DOMAINS } from './settings/domains';
import { store, stored } from './lib/stored';
import { figure } from './format';
import { HarnessRuns } from './harnessRuns';
import { usePluginsView } from './plugins/PluginsView';
import { useAgentsView, useAgentsWaiting } from './agents/AgentsView';
import type { AgentPart } from './agents/agents';

/** The views are `commands.ts`'s one list (D66); the activity bar and the palette read the same. */
type Tab = View;

/**
 * Whether the person was last watching Sessions — a per-browser preference like the language and the
 * scope, never machine wiring and never a tracked file. Only Sessions is worth returning to (D55's
 * reason for remembering a mode, kept when the mode went — D66): a relaunch lands on Overview, the
 * landing view (D40), unless the person was attending work.
 */
const VIEW = 'daoris.view';

// What each view's list has chosen is its list's memory since FRAME1c (`work/listPanes.ts`, D118 §3f),
// in the keys that predate it: the session attended (D56), since relaunching into Sessions landed on
// *Nothing attended* while a session sat parked, and Settings' domain (D75), so the gear returns to it.
// A session whose record is gone is let go by the Work frame, a remembered record that closed or went
// once its view reads it (UX6b), and a domain this window cannot show opens on Appearance, so a stale
// one costs nothing.

function rememberedView(): Tab {
  // Landing on Overview is the safe half of the choice.
  return stored(VIEW) === 'sessions' ? 'sessions' : 'overview';
}

// The activity bar: every view but Settings, which has its own gear. Sessions — what the Work frame
// was (D66) — is absent in a browser rather than disabled.
const NAV = VIEWS.filter(({ view }) => view !== 'settings');

/**
 * The views drawn with a list pane (D118 §2), each naming it in `layout.list.<view>` and
 * `layout.menu.list.<view>`. Sessions' first (FRAME1b), then Plugins, built on the frame (PLUGUI1b), then Quests
 * (FRAME1d), Repositories (FRAME1e), Settings (FRAME1g), and Convergence and Search (FRAME1f), one place since UX6i:
 * every view but Overview and Map, which have none (§4). Knowledge names its list by its mode's (`listOf`).
 */
const LISTED: ReadonlySet<ListView> = new Set<ListView>([
  'sessions', 'plugins', 'quests', 'projects', 'knowledge', 'agents', 'settings',
]);
const isListed = (view: View): view is View & ListView => (LISTED as ReadonlySet<string>).has(view);

/**
 * A command's id as the machine log takes it (LOG1b): a family's row by its family (`workspace.scope`, `go.agent`), so a
 * workspace's or an agent's name never reaches the log.
 */
const loggedId = (id: string) => id.split(':')[0]!;

/**
 * The strip's layout toggles, which leave it below the width the seven menus and the caption room need (UX7a, measured
 * on the stories at 680 px and above, `docs/2026-10-05-ux7-design.md` §3.1).
 */
const TOGGLES_ROOM = 'flex items-center max-[54rem]:hidden';

/** A palette command that says it ran, into the machine log (LOG1b): by its id, never what was typed. */
const counted = (command: Command): Command => ({
  ...command,
  run: () => {
    logEvent('command.run', { command: loggedId(command.id) });
    command.run();
  },
});

/**
 * The platform: the person's window over the family (D38), landing on management (D40), wearing the
 * console shell (D41), on properly-tooled foundations (D42). Doctrine stays unwritable from every
 * view (D31); the service's sentences render verbatim; UI chrome speaks the active language.
 */
export function App() {
  const { t, i18n } = useTranslation();
  const [tab, setTab] = useState<Tab>(rememberedView);
  // What each view's list remembers (D118 §3f): its closing, its width, its chosen item, its filters.
  const lists = useListPanes();
  const attending = lists.pane('sessions').chosen;
  // Knowledge's mode (UX6i, D150 §2.2): Search or Convergence, remembered for the place. Held here, where every door is
  // applied, since a door into either names it.
  const [knowledgeMode, chooseKnowledgeMode] = useKnowledgeMode(lists);
  // A domain remembered from before it left Settings reads as the one Settings opens on, so Ask Daoris is told the domain
  // shown: Plugins as Driver, which keeps its folder's row (UX6j); Agents, Workspace and Permissions as Appearance.
  const settingsSection: SettingsSection = rememberedDomain(lists.pane('settings').chosen);
  // The part of a Settings domain a menu item named, brought into view once it is drawn (UX5 U72).
  const [settingsAnchor, setSettingsAnchor] = useState<SettingsAnchor | null>(null);
  // And the part of an agent's page a door named (UX6e, D150 §2.4): its accounts, what it may do, its usage.
  const [agentPart, setAgentPart] = useState<AgentPart | null>(null);
  // A quest the review asked for (SURF6b): the repository whose work is being sent back, handed
  // to the composer as an opening draft. Held here because the door crosses two views.
  const [opening, setOpening] = useState<{ from?: string; to?: string } | null>(null);
  // The Workspace menu's *Add repository…* (D75), an event Repositories consumes, like the draft above.
  const [addRequested, setAddRequested] = useState(false);
  // And its *Import a folder…* (D77): the import drawer, which names the workspace it lands in.
  const [importRequested, setImportRequested] = useState(false);
  // A repository's page's tab (UX6f, D150 §4.2), remembered for the view: a door into a repository's own value names
  // Setup, so it is held here, where every door is applied.
  const [projectTab, setProjectTab] = useState<ProjectTab>(readProjectTab);
  const chooseProjectTab = useCallback((next: ProjectTab) => {
    setProjectTab(next);
    storeProjectTab(next);
  }, []);
  // A workspace's page's tab (UX6g, D150 §4.3), remembered apart, and the Setup section a door asked for: a door into a
  // workspace's remote or its defaults names both, so they are held here too.
  const [workspaceTab, setWorkspaceTab] = useState<WorkspaceTab>(readWorkspaceTab);
  const chooseWorkspaceTab = useCallback((next: WorkspaceTab) => {
    setWorkspaceTab(next);
    storeWorkspaceTab(next);
  }, []);
  const [workspaceSection, setWorkspaceSection] = useState<WorkspaceSection | null>(null);
  // The repository whose code map a door opened the Map on (MAP3a; FRAME1e): a repository's page names it. The Map
  // goes one level in on it, and back to the workspace from there by its own door.
  const [mapCode, setMapCode] = useState<string | null>(null);
  // The palette asked for the ask composer (INT4c) — an event Quests consumes, like the two above. A quest or an
  // ask a door names is Quests' chosen item since FRAME1d, and its page is the main area's.
  const [asking, setAsking] = useState(false);
  // The palette (SURF9), and what it asks the Work frame to do. Both are events consumed on arrival
  // rather than state, for the reason the quest composer's opening draft is.
  const [palette, setPalette] = useState(false);
  // The application's own card. It is where the NAME lives now that the strip carries only the mark.
  const [about, setAbout] = useState(false);
  // Help › Keyboard shortcuts (UX7a): every key the table holds, by menu.
  const [shortcuts, setShortcuts] = useState(false);
  // The menu bar's open menu, which moves with the pointer once one is open (D152 §3.3), and whether Alt is held, which
  // shows each menu's letter.
  const [openMenu, setOpenMenu] = useState<string | null>(null);
  const [mnemonics, setMnemonics] = useState(false);
  // Whether the window is narrower than the seven names need, so they fold into one menu (UX7a2).
  const foldMenus = useMenuFold();
  // What a menu, the palette or a key asked the Work frame to do with the session attended there (D118 §5): an event.
  const [workIntent, setWorkIntent] = useState<FrameIntent | null>(null);
  const { toasts, notify, dismiss } = useToasts();

  // The window this page is the chrome of (SURF7). In a browser `present` is false and the strip is
  // simply a strip: no caption room, no drag, nothing to command.
  const chrome = useWindowChrome();

  const status = useStatus();
  const repositories = useRepositories();
  // The badge shares the Quests view's cache — one fetch, two readers.
  const outstanding = useQuests(null, false);
  const refresh = useRefreshIndex();
  // The same "is a shell here" answer every control uses — one detection path, not two that drift.
  const driver = useDriver();
  const attached = driver.data !== undefined;
  // Ask Daoris (HELP1) is a view of the frame's regions on every view since DOCK1a: bumped each time the
  // person asks for it, and the frame opens whichever region holds it, on it.
  const [helpFocus, setHelpFocus] = useState(0);
  // What the person closed in the frame (DOCK1c): held here, so the strip's toggles, the View menu and
  // the keys reach them from every view.
  const closings = useFrameClosings();
  // What the view's list is now, as the frame measured it (D118 §3a): open, a strip, or laid over the
  // main area. A strip the window drew is not shown, and its toggle lays the list over.
  const [listMode, setListMode] = useState<ListMode | null>(null);
  const listShown = listMode === 'open' || listMode === 'over';
  // Where each view stands (DOCK1b): held here, so the View menu's reset reaches it.
  const placements = usePlacements();
  // Its doors open it, never close it — `F1`, `Ctrl+Alt+I`, the palette, Quick Ask's *Open in the side
  // bar*; the region's own close and toggle are what close it, on every view alike.
  const openHelp = useCallback(() => setHelpFocus((was) => was + 1), []);
  const onSessions = useRef(false);
  // A question handed to Ask Daoris gets an id no earlier one had, whichever door handed it: each is let
  // go once sent (SETUP1b), so an id counted from the last one held would repeat.
  const openings = useRef(0);
  // Quick Ask (DOCK1d): open or not, and the question the palette handed it, one id per question.
  const [quick, setQuick] = useState(false);
  const [quickOpening, setQuickOpening] = useState<AskOpening | null>(null);
  // Words a right-click asked Search for (CTX1), one id per ask, let go once in its box.
  const [searchAsked, setSearchAsked] = useState<{ text: string; id: number } | null>(null);
  // The side bar's question (SETUP1b): the setup guide's *Set up with Ask Daoris*, held until it is sent.
  const [helpOpening, setHelpOpening] = useState<{ text: string; id: number } | null>(null);
  const askSetup = (message: string) => {
    openings.current += 1;
    setHelpOpening({ text: message, id: openings.current });
    openHelp();
  };
  /** Quick Ask, with a question it sends, or with a draft in its box for the person to finish (CTX1's *Ask Daoris about it*). */
  const askQuickly = (question?: string, draft = false) => {
    if (question) {
      openings.current += 1;
      setQuickOpening({ text: question, id: openings.current, ...(draft ? { draft } : {}) });
    }
    // 🔴 A beat after the palette closes, never with it: closing, the palette hands focus back to where
    // it was, which pulled it out of a box opened in the same step, and the box's trap caught it on its
    // frame instead of its message box (found looking at DOCK1d).
    window.setTimeout(() => setQuick(true), 0);
  };
  // The status bar's other two facts. Both share caches the frames already fill, so neither is a
  // second fetch — and both are absent in a browser, which is what the bar then says.
  const running = useSessions(null, false);
  const remotes = useRemotes();
  const registry = useRegistry();
  // The asks still waiting (INT4d), from the cache Quests fills — the count below includes them.
  const asks = useAsks(false);
  // The folders the driver is holding for the agent's trust (D73), from the tick. The band's row asks the question in
  // place (UX6c); only the person's press grants it.
  const untrusted = useUntrusted();
  // The planner's verdicts as of the last tick: Sessions' badge counts the quests parked on their failed sessions
  // (SESSUX1c, D126 §2.5), and Overview's, which counts the band, does too (SESSUX1i). Empty in a browser.
  const considered = useConsidered();
  // What agents proposed about the rules (PERM2): the menus count those waiting, and the band lists them.
  const rules = useRules();
  const proposals = Array.isArray(rules.data?.proposals) ? rules.data.proposals : [];
  // The agents this machine has, for the Agents menu (UX6e): the roster the frame holds, asked once and kept.
  const roster = useHarnesses();
  // Where Sessions' list places each session (D126): Overview's badge counts the work to review the band lists (UX6c).
  // The frame reads the same answer under the same key on every view, so this asks nothing more; a browser has none.
  const sessionGroups = useSessionGroups();
  // The install's update (UPDATE1, D139): what is staged, the drain, and the person's word on it, as the banner's.
  const update = useUpdateState();
  const sayUpdate = useSayUpdate();
  const dismissUpdate = useDismissUpdate();
  // Opening a window is the shell's act, not the page's (SURF8). In a browser it simply rejects,
  // which is why the commands that use it are gated on a shell being here.
  const openWindow = useOpenWindow();
  const openBrowser = useOpenBrowser();
  // *Show it again* on the strip's review chip (REVIEWENV1d): a set-up's build served to its tab again, and the tab in front.
  const showReviewAgain = useShowReviewAgain();
  // The chip's *Reviewed* (REVIEWENV1g): the review's one owner, as the step's page presses it.
  const reviewActs = useReviewActs({ notify });
  // Where the page's links open (BRW7): Daoris's browser where the person chose it and a shell is here
  // to open one, and otherwise null — a link then opens as a link always has.
  const linkOpener = useLinkOpener(notify);

  // Sessions does not exist in a browser (D55): no stream, no tree path, nothing honest to show. A
  // remembered `sessions` where no shell answers falls back rather than rendering an empty view, and so does
  // any shell-only view a door asked for: Plugins too, which are this machine's (D119 §3.7).
  const view: Tab = !attached && VIEWS.some((entry) => entry.view === tab && entry.shellOnly) ? 'overview' : tab;
  onSessions.current = view === 'sessions';

  // What is used, and what never is (LOG1b): each view the person lands on, into the machine log — once
  // the shell has answered, so a relaunch into Sessions is not first logged as the Overview it stands in for.
  const settling = driver.isLoading;
  // Knowledge says its mode too (UX6i), so the log still tells Search's use from Convergence's, as their views did.
  const loggedMode = view === 'knowledge' ? knowledgeMode : null;
  useEffect(() => {
    if (!settling) logEvent('view.opened', loggedMode ? { view, mode: loggedMode } : { view });
  }, [view, loggedMode, settling]);

  // The Plugins view (PLUGUI1b, D119): held on every view, so what its pages hold — a trial's report, an update's
  // plan — stays while Daoris is open; it asks the driver for nothing until it is in front.
  const plugins = usePluginsView({
    active: view === 'plugins',
    chosen: lists.pane('plugins').chosen,
    onChoose: (item) => lists.choose('plugins', item),
    notify,
    onAsk: attached ? askSetup : undefined,
  });

  // The Agents place (UX6e, D150 §5): held on every view, as Plugins is; it reads the roster the frame holds and the
  // accounts' files, and opening it asks no account. A sign-in started there outlives leaving it (SIGNIN1).
  const agentsPane = lists.pane('agents');
  const agents = useAgentsView({
    active: view === 'agents',
    chosen: agentsPane.chosen,
    onChoose: (item) => lists.choose('agents', item),
    notify,
    part: agentPart,
    onAnchored: () => setAgentPart(null),
  });
  // Its badge (D150 §2.1): the accounts a list or a default holds that read signed out, in open's hue. A browser has none.
  const agentsWaiting = useAgentsWaiting();
  // The same accounts as *What needs you* reads them, with the tick's waits (UX6d), for Overview's badge.
  const accountsKnown = useAccountsKnown();

  // Whether the view in front has a list pane (D118 §3a), whose four doors — the strip's toggle, the View
  // menu's item, Ctrl+B and a press on its place — are absent where it has none, never disabled. A browser
  // keeps a view's list too (D118 §4); Sessions, the one view that needs a shell, is never in front there.
  const listed = isListed(view);

  // A region toggled (DOCK1c): the side bar and the panel are the frame's on every view since DOCK1a, and
  // the list is the view's own, so where the view has none its key does nothing. True when it did. A
  // browser has the view's list and never the side bar or the panel (D47 §4: they hold this machine's sessions).
  const toggleRegion = (region: LayoutRegion): boolean => {
    if (!attached && region !== 'list') return false;
    if (region === 'right') closings.setDock(!closings.dock);
    else if (region === 'panel') closings.setPanel(!closings.panel);
    else if (isListed(view) && listMode) {
      // By what the room made of it: an open list closes, one laid over goes, and a strip opens — over
      // the main area where the window drew it (D118 §3a). The closing is this view's own (§3f).
      const next = listToggled({ mode: listMode });
      lists.setClosed(view, next.closed);
      closings.setListOver(next.over);
    }
    else return false;
    return true;
  };

  // The scope (WSP5): which circle this window is looking at. The roster comes from the registry
  // unscoped — the one reader that must see every workspace — and a remembered choice the deployment
  // no longer holds falls back to every, out loud (the switcher then says so), rather than scoping
  // every query to a circle nobody is in any more.
  const scope = useScope();
  const workspaces = useWorkspaces();
  const holdings = useWorkspaceHoldings();
  // 🔴 The scope in words, whatever the machine holds (D75 §3): the one chosen, the one there is,
  // every one among several, or none yet. With none registered the top bar and the status bar both
  // said "every workspace", of nothing, and the switcher that would have said more is absent below
  // two by WSP5's rule. That rule still hides the control; it no longer hides the fact.
  const scopeNamed = scope.workspace ?? (workspaces.data === undefined
    ? null
    : workspaces.data.length === 0
      ? t('scope.none')
      : workspaces.data.length === 1
        ? workspaces.data[0]!
        : t('scope.every', { count: workspaces.data.length }));
  useEffect(() => {
    if (workspaces.data && scope.workspace && !workspaces.data.includes(scope.workspace)) {
      scope.setWorkspace(null);
    }
  }, [workspaces.data, scope]);

  const onRefresh = () => refresh.mutate(undefined, {
    onSuccess: (report) => {
      // What the semantic half embedded is said beside the count, in the one notice (SEM3b, D123); a
      // refresh it did not run in says nothing of it, since absent is never zero.
      const counted = t('sidebar.refreshed', report);
      const made = report.embedded;
      notify(made
        ? t('sidebar.join', {
          first: counted,
          second: t('sidebar.embedded', {
            count: made.split,
            split: figure(made.split),
            entries: figure(made.entries),
            pieces: figure(made.pieces),
            window: figure(made.window),
          }),
        })
        : counted);
      // A registered checkout that was not where the registry says contributed nothing, and the count
      // still looks healthy — so it is named, as the failure it is (D48 §3; REV3).
      if (report.absent?.length) notify(t('sidebar.absent', { names: report.absent.join(', ') }), 'error');
      // The semantic half's failure is the service's own sentence — dropped nowhere (D24).
      if (report.semanticError) notify(report.semanticError, 'error');
    },
    onError: failure(notify),
  });

  const indexed = (repositories.data ?? []).reduce((sum, r) => sum + r.total, 0);
  const outstandingCount = outstanding.data?.length ?? 0;

  // Ready once the driver's service is up, not merely its file (LOOK2a); a shell older than the field says nothing, and reads ready.
  const presence: DriverPresence = driver.data
    ? (driver.data.ready === false ? 'starting' : 'running')
    : driver.isError ? 'stopped' : 'absent';
  const liveSessions = (running.data ?? []).filter((s) => SESSION_ACTIVE.has(s.state)).length;
  // Overview's badge, from the one derivation the band uses and every input it reads, the rule proposals, the parked
  // quests (SESSUX1i) and the work to review (UX6c) among them — two answers to "how many need me" would disagree the
  // first time either was edited. Sessions' badge is its own sessions only (U20). The accounts' rows too (UX6d), from the
  // roster and the accounts' files the Agents badge reads, and the tick's waits.
  const waiting = needsAPerson(
    running.data ?? [], outstanding.data ?? [], registry.data ?? [], asks.data ?? [], untrusted.data ?? [],
    proposals, considered.data ?? [], sessionGroups.data ?? [], accountsKnown).length;
  const sessionsWaiting = waitingInSessions(running.data ?? [], considered.data ?? []);
  // Where this circle stands with its remote (SYNC6b), from this machine's own host — so a browser on
  // the machine reads it too, and it says for itself whether the circle is wired. Before it answers,
  // the shell's map says; a browser with neither is not asked.
  // 🔴 WHICH circle: the one chosen, or the only one there is. "Every circle" has no single standing,
  // and assuming `default` both asked a door for a scope nobody chose and named the wrong circle in a
  // family whose one circle is called something else.
  const circle = scope.workspace ?? (workspaces.data?.length === 1 ? workspaces.data[0] : null);
  const standing = useSyncStanding(circle);
  const syncNow = useSyncNow();
  // Titles for the quests in conflict, from the cache the Quests view fills anyway; one it has not
  // loaded is named by its id alone.
  const everything = useQuests(null, true);
  // Who is driving Daoris's browser (BRW8), named from the caches the frame already fills: the driver's
  // ids, the sessions, and the quests they serve. Said beside the browser's door and in its Settings.
  const driving = browserDrivers(driver.data?.drivingBrowser, running.data ?? [], everything.data ?? []);
  const wired = standing.data
    ? standing.data.wired
    : remotes.data && circle
      ? remotes.data.remotes.some((row) => row.workspace === circle)
      : null;

  const onSyncNow = (workspace: string) => syncNow.mutate(workspace, {
    // The pass's own words, verbatim: the wall it hit, and what the remote understood and did not
    // take. A clean pass that said nothing is the one sentence this page writes.
    onSuccess: (report) => {
      if (report.problem) notify(report.problem, 'error');
      for (const note of report.notes) notify(note);
      if (!report.problem && report.notes.length === 0) notify(t('work.sync.done', { workspace }));
    },
    onError: failure(notify),
  });

  /**
   * **The application's one opener** (D118 §3i): every door into a view calls it, naming the item it opens,
   * and the view's list has that item chosen (§3f). `opener.ts` plans it as a value; this applies it.
   * Settings opens at the domain a door names, at the part it names (D75, UX5 U72): the tier opens Daoris's
   * own AI, the remote Workspace, the driver Driver, where each once opened the whole page at its top.
   */
  const apply = (plan: Opening) => {
    // Knowledge's mode first (UX6i): a switch reads that mode's remembered choice again, and an item a door names in it
    // is then chosen now, never a remembered choice.
    if (plan.knowledge) chooseKnowledgeMode(plan.knowledge);
    if (plan.chosen) lists.choose(plan.chosen.view, plan.chosen.item);
    // A view opened by a door that names nothing reopens what its list chose only while it still waits (UX6b, D150 §8):
    // the list reads it again, and lets go of one that closed or went. A door that names an item has it chosen above.
    // Knowledge's chosen item is its mode's list's.
    else if (plan.view !== view && isListed(plan.view)) {
      lists.reopen(plan.view === 'knowledge' ? plan.knowledge ?? knowledgeMode : plan.view);
    }
    if (plan.anchor !== undefined) setSettingsAnchor(plan.anchor);
    setAgentPart(plan.agentPart ?? null);
    if (plan.drawer === 'add') setAddRequested(true);
    if (plan.drawer === 'import') setImportRequested(true);
    if (plan.tab) chooseProjectTab(plan.tab);
    if (plan.workspaceTab) chooseWorkspaceTab(plan.workspaceTab);
    setWorkspaceSection(plan.workspaceSection ?? null);
    // A door naming a code map opens the Map on it; any other way onto the Map opens the workspace, as it always has.
    if (plan.code !== undefined) setMapCode(plan.code);
    else if (plan.view !== view) setMapCode(null);
    // A list laid over the main area is never kept: another view lets it go (§3f).
    if (plan.view !== view) closings.setListOver(false);
    setTab(plan.view);
    store(VIEW, plan.view === 'sessions' ? 'sessions' : null);
  };
  // A door naming a workspace's page and no workspace opens the one in view (UX6g).
  const open = (target: View, item?: string | null, part?: OpenPart) => apply(plannedOpening(target, item, part, circle));

  /** A domain chosen in Settings' own list opens at its top: an anchor its part never answered is dropped. */
  const chooseSettings = (section: SettingsSection) => {
    lists.choose('settings', section);
    setSettingsAnchor(null);
  };
  const openSettings = (section: SettingsSection, anchor?: SettingsAnchor) => open('settings', section, { anchor });

  // The setup guide (SETUP1b, D97), from the reading Ask Daoris's starters share: the status bar's count
  // until the required steps are done, and Get started opened once at start on a machine missing any of
  // the first three. Both wait for the machine to be read, and neither is a browser's (D47 §4).
  const setupReading = useMachine();
  const setupStepsNow = setupSteps(setupReading.machine);
  const setupCount = setupReading.settled ? setupProgress(setupStepsNow) ?? undefined : undefined;
  useSetupAtStart({
    settled: setupReading.settled,
    needed: opensAtStart(setupStepsNow),
    // The view chosen, not the one shown: a remembered Sessions stands in as Overview until the shell
    // answers, and that is not the person going anywhere.
    view: tab,
    open: () => openSettings('start'),
  });

  // The record in the main area, as its page offers it to a right-click (CTX1), read by Run's record groups and Edit's
  // *Copy ID* (UX7a, D152 §2): each act has one owner, whichever door pressed it.
  const copyText = useRef<(text: string) => void>(() => {});
  const offers = useMemo(() => offerStore((text) => copyText.current(text)), []);
  const offered = useMainOffer(offers);
  // The field the person was last in, which Edit's acts go back to once a menu has taken the focus (D152 §3.4).
  const fields = useRef<ReturnType<typeof fieldTracker> | null>(null);
  useEffect(() => {
    const tracker = fieldTracker(document);
    fields.current = tracker;
    return () => {
      tracker.stop();
      fields.current = null;
    };
  }, []);
  const [theme, setTheme] = useThemeChoice();
  const attendedSession = (running.data ?? []).find((session) => session.id === attending) ?? null;
  const chosenQuest = lists.pane('quests').chosen;

  /**
   * What is true now, which decides what the menus, the palette and the keys offer (D152 §2). The box Find goes to and
   * the field Edit acts on are read from the page as it stands, so a key reads them at the press.
   */
  const commandState = (): CommandState => {
    const live = (offered?.acts ?? []).filter((act) => !act.disabled).map((act) => act.id);
    return {
      attached,
      view,
      list: listed ? { shown: listShown } : null,
      panelShown: !closings.panel,
      sideShown: !closings.dock,
      moved: placements.moved,
      workspaces: holdings.data ?? [],
      scope: scope.workspace,
      circle,
      wired: Boolean(wired),
      theme,
      language: i18n.language.startsWith('zh') ? 'zh' : 'en',
      // Each agent installed here, as the Agents place lists it (UX6e): the palette goes to each one's page.
      agents: byTool(Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [])
        .filter((tool) => tool.present)
        .map((tool) => ({ name: tool.name, label: tool.product ?? tool.name })),
      domains: SETTINGS_DOMAINS.filter((domain) => attached || !domain.machine).map(({ id, label }) => ({ id, label })),
      knowledge: knowledgeMode,
      // The session attended on Sessions, with what its header offers; *Answer…* is its card's, offered where its row's is.
      session: view === 'sessions' && offered
        ? { acts: live, answer: attached && attendedSession !== null && offeredActs({ session: attendedSession }, 'row').includes('answer') }
        : null,
      // A quest open on Quests (an ask's page is not a quest's), with what its header and its body offer.
      quest: view === 'quests' && offered && chosenQuest && 'quest' in questsItem(chosenQuest) ? { acts: live } : null,
      record: live.includes('copy'),
      find: findTarget(document, view) !== null,
      field: fields.current?.field() != null,
      refreshing: refresh.isPending,
    };
  };

  /** What the Work frame does with the session attended there; answering, starting and archiving are Sessions' own. */
  const frameIntent = (intent: FrameIntent) => {
    if (intent === 'start' || intent === 'answer' || intent === 'archiveEnded' || intent === 'review') open('sessions');
    setWorkIntent(intent);
  };

  /** What each verb runs: the application's doors, handed to the table (D152 §2). */
  const commandDoors: CommandDoors = {
    // Asking lives at the head of Quests (INT4c); the menu goes there and opens the composer, and a quest's composer too.
    ask: () => { open('quests'); setAsking(true); },
    quest: () => { open('quests'); setOpening({}); },
    scope: scope.setWorkspace,
    add: () => open('projects', null, { drawer: 'add' }),
    // `daoris import <folder> --workspace <name>`'s screen door (D77): the drawer chooses the folder and names the workspace.
    import: () => open('projects', null, { drawer: 'import' }),
    // The workspace in view's page at Setup (UX6g), where its defaults, its remote and its rules are.
    workspaceSetup: () => open('projects', null, { workspaceTab: 'setup' }),
    sync: () => { if (circle) onSyncNow(circle); },
    wire: () => { if (circle) open('projects', workspaceItem(circle), { workspaceSection: 'remote' }); },
    // At a domain the row names, or at the one Settings' list remembers (D75).
    settings: (section) => (section ? openSettings(section as SettingsSection) : open('settings')),
    edit: (act) => {
      void runEdit(act, fields.current?.field() ?? null).then((done) => {
        if (!done && act === 'paste') notify(t('menu.pasteRefused'), 'error');
      });
    },
    find: () => findTarget(document, view)?.focus(),
    searchKnowledge: () => {
      // Knowledge's Search (UX6i), whatever mode the place was left in.
      open('knowledge', null, { knowledge: 'search' });
      // A beat after the view is drawn, as Quick Ask's box is opened (DOCK1d): its box is in its list.
      window.setTimeout(() => findTarget(document, 'knowledge')?.focus(), 0);
    },
    copyId: () => { offers.run('copy'); },
    palette: () => setPalette(true),
    toggle: (region: LayoutRegion) => { toggleRegion(region); },
    reset: () => placements.reset(),
    frame: frameIntent,
    // A window is the shell's to open (SURF8), so neither is in a browser's table.
    monitor: () => openWindow.mutate(MONITOR_WINDOW),
    browser: () => openBrowser.mutate(),
    theme: setTheme,
    language: (language) => void i18n.changeLanguage(language),
    refresh: onRefresh,
    go: (target) => open(target),
    knowledge: (mode) => open('knowledge', null, { knowledge: mode }),
    agent: (name) => open('agents', name),
    agentPart: (part) => open('agents', null, { agentPart: part }),
    region: (previous) => focusRegion(document, previous),
    // A record's act by its page's own press (D152 §2): the session's through `sessionActs.ts`, the quest's by its page.
    sessionAct: (act) => { offers.run(act); },
    questAct: (act) => { offers.run(act); },
    help: openHelp,
    quickAsk: () => askQuickly(),
    setup: () => openSettings('start'),
    shortcuts: () => setShortcuts(true),
    // Settings → Driver, where the install's update is (UPDATE1b): nothing here checks a release channel.
    update: () => openSettings('driver', 'update'),
    about: () => setAbout(true),
  };
  const translate = (key: string, values?: Record<string, unknown>) => (values ? t(key, values) : t(key));
  const entries = commandTable(commandState(), commandDoors, translate);
  // The menus as the bar draws them; a browser has no Terminal (D152 §3.6), and claims no Alt (§3.4), so shows no letter.
  const barMenus: BarMenu[] = MENUS.filter((menu) => !menu.shell || attached).map((menu) => ({
    id: menu.id,
    label: t(menu.label),
    ...(attached ? { letter: menu.letter } : {}),
    items: menuRows(entries, menu.id),
  }));
  const onMenu = (_menu: string, item: string) => {
    const entry = entries.find((each) => each.id === item);
    if (!entry) return;
    logEvent('command.run', { command: loggedId(entry.id) });
    entry.run();
  };

  // The keys (D152 §3.4), read from the one table at the press, so a key runs what its menu row runs and only while it
  // applies: a field keeps what a field means, the terminal that and a shell's next line, and a browser what it keeps.
  const tableNow = useRef<() => ReturnType<typeof commandTable>>(() => []);
  tableNow.current = () => commandTable(commandState(), commandDoors, translate);
  const menuOpen = useRef(openMenu);
  menuOpen.current = openMenu;
  useEffect(() => {
    // Alt pressed and let go with nothing between reaches the bar, as Windows' menu bars do (the design §3.3).
    let altAlone = false;
    const onKey = (event: KeyboardEvent) => {
      // A shell's alone: a browser keeps Alt and F10 for its own menu.
      if (attached && event.key === 'Alt' && !event.ctrlKey && !event.shiftKey && !event.metaKey) {
        altAlone = true;
        setMnemonics(true);
        return;
      }
      altAlone = false;
      if (attached && event.altKey && !event.ctrlKey && !event.shiftKey && !event.metaKey) {
        const menu = MENUS.find((each) => (!each.shell || attached) && event.code === `Key${each.letter}`);
        if (menu) {
          event.preventDefault();
          setMnemonics(false);
          setOpenMenu(menu.id);
          return;
        }
      }
      if (attached && event.key === 'F10' && !event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) {
        event.preventDefault();
        focusMenuBar(document);
        return;
      }
      if (event.defaultPrevented) return;
      const target = event.target instanceof Element ? event.target : null;
      const entry = commandForKey(tableNow.current(), event, {
        field: isTextField(target), terminal: Boolean(target?.closest('.xterm')),
      });
      if (!entry) return;
      event.preventDefault();
      logEvent('command.run', { command: loggedId(entry.id) });
      entry.run();
    };
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key !== 'Alt') return;
      setMnemonics(false);
      if (!altAlone) return;
      altAlone = false;
      event.preventDefault();
      // With a menu open, Alt closes it, as Windows' does; with none, it reaches the bar.
      if (menuOpen.current !== null) setOpenMenu(null);
      else focusMenuBar(document);
    };
    const reset = () => {
      altAlone = false;
      setMnemonics(false);
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('keyup', onKeyUp);
    window.addEventListener('blur', reset);
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('keyup', onKeyUp);
      window.removeEventListener('blur', reset);
    };
  }, [attached]);

  // The attended session is remembered alongside the view (D56), so a relaunch into Sessions reopens
  // what the person was watching rather than an empty column.
  const { choose } = lists;
  const setAttending = useCallback((next: string | null) => choose('sessions', next), [choose]);

  // A door from a record into the session itself. The selection lives here rather than inside the
  // view precisely so a door can name which session it is opening (D55: one selection, every
  // region) — the view change alone would land the person on whatever they last attended.
  const openInWork = (session: string) => open('sessions', session);
  // A door into a run (WORKFLOW1c, the workflow design §7): its session attended in Sessions, the Workflow view opened on it.
  const openRun = (session: string) => { open('sessions', session); setWorkIntent('workflow'); };
  // A quest's record and an ask's, where quests are read: an ask is named as one, since Quests' list holds both.
  const openQuest = (id: string) => open('quests', id);
  // A row in Overview's band is a door into whatever is waiting, wherever that exists. A parked
  // session opens in Sessions, which only a shell has. An ask opens its record, where it is answered
  // (INT4d), and a quest nobody can take opens its own page. Both of those a browser has too.
  const openAsk = (id: string) => open('quests', askItem(id));
  // Where a starter's, a setup step's or Ask Daoris's door leads (HELP1d, SETUP1a, HELP6): a view and the item
  // it names, a domain of Settings at the part it names, or one of the Workspace menu's drawers on Repositories.
  const go = (door: StarterDoor) => apply(doorOpening(door, circle));

  // The Quests view (FRAME1d, D118 §2): held on every view, as Plugins is, so what its composers hold lasts while
  // Daoris is open; its list and its main area read the list's memory, which every door into it names (§3i).
  const questsPane = lists.pane('quests');
  const quests = useQuestsView({
    active: view === 'quests',
    chosen: questsPane.chosen,
    onChoose: (item) => lists.choose('quests', item),
    filters: questsPane.filters,
    onFilters: (filters) => lists.setFilters('quests', filters),
    notify,
    onAttend: attached ? openInWork : undefined,
    onOpenRun: attached ? openRun : undefined,
    opening,
    onOpened: () => setOpening(null),
    asking,
    onAsked: () => setAsking(false),
  });
  // The Repositories view (FRAME1e, D118 §2): held on every view, as Quests is; its list is the registry and its main
  // area the chosen repository's page, which every door into it names (§3i). Its drawers are the Workspace menu's too.
  const projects = useProjectsView({
    active: view === 'projects',
    chosen: lists.pane('projects').chosen,
    onChoose: (item) => lists.choose('projects', item),
    tab: projectTab,
    onTab: chooseProjectTab,
    workspaceTab,
    onWorkspaceTab: chooseWorkspaceTab,
    workspaceSection,
    notify,
    onOpenCode: (repository) => open('map', null, { code: repository }),
    // A workspace's Details names each agent's accounts there, set on the agent's page (UX6e).
    onOpenAgent: (agent) => open('agents', agent),
    onSyncNow,
    syncing: syncNow.isPending,
    addRequested,
    onAddOpened: () => setAddRequested(false),
    importRequested,
    onImportOpened: () => setImportRequested(false),
    // A workspace's clear names what it keeps, each with the page that frees it (HIST1e, D153 §5).
    onOpenQuest: openQuest,
    onOpenAsk: openAsk,
    onAttend: attached ? openInWork : undefined,
  });
  // Knowledge (UX6i, D150 §2.2): Search and Convergence (FRAME1f, D118 §2) as one place, held on every view as Quests is
  // and asking the service nothing until in front. Each mode's list memory is its chosen item and its one filter, *local
  // only* and the similarity (§3f), as it was; a mode chosen at the list's head is a door into the place in that mode.
  const knowledge = useKnowledgeView({
    active: view === 'knowledge',
    mode: knowledgeMode,
    onMode: (mode) => open('knowledge', null, { knowledge: mode }),
    lists,
    notify,
    semantic: status.data?.semantic ?? false,
    handed: searchAsked,
    onHanded: () => setSearchAsked(null),
  });
  // What Ask Daoris is handed wherever it stands: what is on the screen (HELP1b) — the view, the scope,
  // the settings domain on Settings, and the attended session on Sessions — and its two ways out.
  const askProps = {
    where: {
      view, workspace: scope.workspace ?? null, settings: settingsSection, knowledge: knowledgeMode,
      // Where Sessions' views stand (HELP2): the helper cannot see the window, and guessed without it.
      layout: {
        right: viewsIn(placements.places, 'right'),
        panel: viewsIn(placements.places, 'panel'),
        rightShown: !closings.dock,
        panelShown: !closings.panel,
      },
    },
    attending,
    // Unframed in the side bar, whose own close is the region's; nothing here closes on its own.
    onClose: () => closings.setDock(true),
    onGo: go,
  };

  // Each row's door (UX6c, design §6.2): where its record is, beside the acts the row settles in place.
  const attentionDoors: AttentionDoors = {
    ...(attached ? { parked: (item) => openInWork(item.id) } : {}),
    proposal: (item) => openAsk(item.id),
    intake: (item) => openAsk(item.id),
    unanswerable: (item) => openQuest(item.id),
    // A quest parked on its failed sessions (SESSUX1i) opens its page, and its session from there.
    'parked-quest': (item) => openQuest(item.id),
    // A go-ahead opens the ask it was asked on, where every go-ahead of it is listed (KNOWUSE1a).
    'go-ahead': (item) => { if (item.ask) openAsk(item.ask); },
    // A departure opens its quest's page, where what was required and how its done answered are quoted (DRIFT1d2).
    departure: (item) => openQuest(item.id),
    // A set-up shown for review opens its step's page, where every set-up it said and the review's presses are (REVIEWENV1g).
    'set-up': (item) => openQuest(item.id),
    // A folder waiting on the person's trust (D73) opens what it holds: the quest's page, or the ask whose intake it is.
    // The row asks the trust question itself. Only a shell has one.
    ...(attached ? {
      trust: (item: Attention) => {
        if (item.quest) openQuest(item.quest);
        else if (item.ask) openAsk(item.ask);
      },
    } : {}),
    // An agent's proposal to widen the rules (PERM2) opens the rules it would change, where it is answered beside them:
    // what the agent may do, on its page since UX6e (D150 §3.1). Only a shell reads the rules.
    ...(attached ? { rule: () => open('agents', null, { agentPart: 'rules' }) } : {}),
    // Work to review opens in Sessions with its review open (D126, D113): accepting needs looking. A shell's alone.
    ...(attached ? { review: (item: Attention) => { open('sessions', item.id); setWorkIntent('review'); } } : {}),
    // A second opinion waiting on the person opens the session whose work it read, where its gate and every press are
    // (XAGENT1g, the second-agent design §9). A shell's alone: the opinion is this machine's.
    ...(attached ? { opinion: (item: Attention) => open('sessions', item.id) } : {}),
    // An account's row opens its agent's page at its accounts (UX6d, D150 §6.2), where every account's act is. A shell's alone.
    ...(attached ? {
      'account-wait': (item: Attention) => open('agents', item.account?.agent ?? null, { agentPart: 'accounts' }),
      'signed-out': (item: Attention) => open('agents', item.account?.agent ?? null, { agentPart: 'accounts' }),
    } : {}),
  };

  // Settings on the frame (FRAME1g): its domains are its list pane and the domain chosen its main area.
  const settings = useSettingsLayout({
    notify,
    section: settingsSection,
    onSection: chooseSettings,
    anchor: settingsAnchor,
    onAnchored: () => setSettingsAnchor(null),
    onGo: go,
    // Ask Daoris is the shell's (HELP1): a browser's guide offers no hand-off.
    onAskSetup: attached ? askSetup : undefined,
    // Who is driving Daoris's browser (BRW8), for its domain, each a door into Sessions.
    browserDrivers: driving,
    onAttend: attached ? openInWork : undefined,
  });

  /**
   * Every view but Sessions, as it hands itself to the frame (D118 §5): its list pane where it has one, and
   * its main area, in a shell's frame beside the side bar and the panel, and in a browser's without them.
   * Plugins was built on the frame (PLUGUI1b), and Quests (FRAME1d), Repositories (FRAME1e), Settings (FRAME1g),
   * Convergence and Search (FRAME1f, Knowledge since UX6i) moved onto it: each hands its list and its pages whole.
   * Overview and Map have no list (§4), and their page is their main area.
   */
  const listedLayouts: Partial<Record<View, ViewLayout>> = { plugins, quests, projects, knowledge, agents, settings };

  // The right-click menu's doors (CTX1, D138 §3): a copy, said once copied, since nothing on the screen shows it; Search
  // with the words in its box; and, a shell's, Quick Ask with them quoted and Daoris's browser at the address.
  const contextDoors: ContextDoors = {
    copy: (text) => {
      void navigator.clipboard?.writeText(text).then(() => notify(t('contextMenu.copied', { what: clipped(text) })), () => {});
    },
    search: (text) => {
      openings.current += 1;
      setSearchAsked({ text, id: openings.current });
      open('knowledge', null, { knowledge: 'search' });
    },
    ...(attached ? {
      ask: (text: string) => askQuickly(text, true),
      openInBrowser: (address: string) => openBrowser.mutate(address, { onError: failure(notify) }),
    } : {}),
  };
  // Edit › Copy ID copies what a page's own *Copy … ID* would, and says so the same way (UX7a).
  copyText.current = contextDoors.copy;
  const renderView = (): ViewLayout => listedLayouts[view] ?? {
    main: (
      // No cap: content follows the window (UX5 U59), as the session's centre does since U16. It was 72rem,
      // and a maximized window left every view a third empty. Every block wraps at the column's edge, prose
      // included (D141), and a form keeps its own size. The main area is the container a view's split
      // follows (§3b).
      <ViewMain>
        {view === 'overview' && (
          <OverviewView
            onNavigate={(target) => open(target)}
            onOpenQuest={openQuest}
            doors={attentionDoors}
            notify={notify}
            onSessions={attached ? () => open('sessions') : undefined}
            onRun={attached ? openRun : undefined}
          />
        )}
        {view === 'map' && (
          <MapView
            // Drawn anew on a code map a door names, so the door's repository is what it opens on.
            key={mapCode ?? ''}
            code={mapCode}
            notify={notify}
            onOpenConvergence={() => open('knowledge', null, { knowledge: 'convergence' })}
            onOpenQuest={openQuest}
          />
        )}
      </ViewMain>
    ),
  };

  return (
    // A tool's running action — a sign-in above all — outlives the view it started on (SIGNIN1).
    <HarnessRuns notify={notify}>
    {/* Every link on the page opens through one place, told here where to (BRW7). */}
    <LinkOpener.Provider value={linkOpener}>
    {/* What the main area's page offers, which the menu bar's record groups read (UX7a). */}
    <MainOffer.Provider value={offers}>
    {/* A window, not a page (D55): the viewport IS the frame, every region scrolls inside it, and
        the status bar is therefore always where it was. Page scrolling would put the output panel
        below the fold exactly when a session is producing output. */}
    <div className="flex h-screen flex-col overflow-hidden">
      {/* The application's one global row (D56). It holds what is true everywhere, which is exactly
          why none of it belongs in a sidebar owned by one view. Since SURF7 it is the window's own
          title bar. */}
      <AppStrip
        captionRoom={chrome.present}
        stripRef={chrome.stripRef}
        onDragStart={chrome.present ? chrome.onDragStart : undefined}
        onToggleMaximize={chrome.present ? chrome.onToggleMaximize : undefined}
        onResizeTop={chrome.present ? chrome.onResizeTop : undefined}
        onSystemMenu={chrome.present ? chrome.onSystemMenu : undefined}
        // 🔴 The APPLICATION's menus, which is what a title bar holds in an IDE, and since D152 they are
        // its verbs and places, as VS Code's bar holds them: Workspace, Edit, View, Go, Run, Terminal,
        // Help, every row a row of the one table (`commands.ts`) that the palette and the keys read too.
        // The activity bar is still the one navigation region (D66); Go is its keyboard twin.
        menus={(
          <AppMenuBar
            label={t('menu.bar')}
            menus={barMenus}
            open={openMenu}
            onOpen={setOpenMenu}
            onChoose={onMenu}
            mnemonics={mnemonics}
            // Narrower than the seven names need, they fold into one menu (UX7a2), which the keys reach as they reach the bar.
            compact={foldMenus}
            foldLabel={t('menu.fold')}
          />
        )}
        // The palette's way in is the command center now, not a 14px glyph wedged against the
        // caption buttons — same dialog, a target a person can find.
        center={(
          <CommandCenter
            scope={scopeNamed ?? t('palette.scope')}
            shortcut="Ctrl K"
            onOpen={() => setPalette(true)}
            label={t('palette.open')}
          />
        )}
        // The region toggles (DOCK1c, SURF11) at the strip's right, beside the window controls, as VS
        // Code's sit: the panel and the right side bar on every view since DOCK1a, and the view's list where
        // it has one, named for it (D118 §3a). Ask Daoris has no button of its own up here: it is a tab of
        // the side bar, so the right toggle, F1 and Ctrl+Alt+I are its doors. Before them, Daoris's browser
        // (BRW7): an act of the application's, one press from every view, where View → Browser was the only one.
        // 🔴 On a narrow window the toggles leave the strip before any menu does (UX7a, D152 §3.1): each is View's,
        // with its key, and seven menus beside a command center reduced to its glyph need the room.
        trailing={attached ? (
          <div className="flex items-center gap-2">
            <BrowserDoor onOpen={() => openBrowser.mutate()} drivers={driving} onAttend={openInWork} />
            {/* A set-up waiting for the person's review (REVIEWENV1d): said beside the browser, never inside it, which an
                agent drives. */}
            <ReviewChip
              // Each with the newest set-up its step's record holds, which *Reviewed* names (REVIEWENV1g, REVIEWENV1b3).
              reviews={driver.data?.inReview?.map((row) => ({
                ...row,
                setUp: setUpRef(newestSetUp(outstanding.data?.find((quest) => quest.id === row.quest) ?? {})),
              }))}
              onShowAgain={(quest) => showReviewAgain.mutate({ quest }, {
                onSuccess: () => notify(t('browser.review.shownAgain', { quest })),
                onError: failure(notify),
              })}
              onReviewed={(quest, setUp) => reviewActs.actsFor({ state: 'shown', step: quest }).reviewed?.(setUp, {
                done: () => {},
                refused: (said) => notify(said, 'error'),
              })}
              // A not yet needs words, which the step's page asks for: a menu holds no box.
              onNotYet={(quest) => openQuest(quest)}
            />
            <div className={TOGGLES_ROOM}>
              <LayoutToggles
                regions={['list', 'panel', 'right']}
                list={listed ? t(`layout.list.${listOf(view, knowledgeMode)}`) : undefined}
                closed={{ list: !listShown, panel: closings.panel, right: closings.dock }}
                onToggle={toggleRegion}
              />
            </div>
          </div>
        ) : listed ? (
          // A browser's strip holds the list's toggle alone (D118 §4): it has no side bar and no panel.
          <div className={TOGGLES_ROOM}>
            <LayoutToggles
              regions={['list']}
              list={t(`layout.list.${listOf(view, knowledgeMode)}`)}
              closed={{ list: !listShown, panel: true, right: true }}
              onToggle={toggleRegion}
            />
          </div>
        ) : undefined}
      />

      {/* The install's update, under the strip and above every view (UPDATE1, D139): quiet, one line, and only while
          there is something to say. The shell's alone: a browser has no install to update. */}
      {attached && (
        <UpdateBanner
          update={update.data}
          busy={sayUpdate.isPending || dismissUpdate.isPending}
          onSay={(mode) => sayUpdate.mutate(mode, { onError: failure(notify) })}
          onDismiss={() => dismissUpdate.mutate(undefined, { onError: failure(notify) })}
        />
      )}

      {/* No narrow-window stacking. The 15rem sidebar this replaced had to become a top bar under
          768px (D41 §2) — a 48px icon rail does not, and stacking it was actively wrong: it put a
          276px-tall column of icons ABOVE the content and left the page 105px, measured in a real
          browser at 686px. One layout at every width, which is also what keeps D55's "a window, not
          a page" true on a narrow screen instead of only on a wide one. */}
      <div className="flex min-h-0 flex-1">
        {/* The application's one navigation (D66): every view is one click from every other, and
            nothing is gated behind a mode. Its foot holds Settings alone (UX6j, D150 §2.1): the state
            it once carried went to the status bar, and its two actions, refreshing the index and the
            language, to View, the palette and Settings → Appearance. */}
        <ActivityBar
          label={t('nav.label')}
          items={NAV.filter(({ shellOnly }) => !shellOnly || attached).map(({ view: target, icon }) => ({
            tab: target,
            label: t(`nav.${target}`),
            icon,
            // 🔴 A badge counts what its place holds (UX5 U20, the owner's choice): Overview the whole
            // of What needs you, beside its band, and Sessions its own sessions waiting on the person.
            // Both are that status and wear its hue; outstanding quests are a quantity, in the accent.
            badge: target === 'quests' ? outstandingCount
              : target === 'overview' ? waiting
                : target === 'sessions' ? sessionsWaiting
                  : target === 'agents' ? agentsWaiting : undefined,
            tone: target === 'overview' || target === 'sessions' || target === 'agents' ? 'open' as const : undefined,
            // Its key from the table, as Go prints it (UX7a); none in a browser, which keeps Ctrl+1–8.
            keys: entries.find((entry) => entry.id === `go.${target}`)?.keys[0]?.combo,
          }))}
          // Settings is everywhere now (D66) — a browser has appearance to set, if nothing of a machine.
          end={[{
            tab: 'settings', label: t('nav.settings'), icon: 'settings',
            keys: entries.find((entry) => entry.id === 'workspace.settings')?.keys[0]?.combo,
          }]}
          active={view}
          onSelect={(target) => open(target)}
          // The list's fourth door (D118 §3a): the place you are on, pressed again, toggles its list.
          onToggleCurrent={listed ? () => { toggleRegion('list'); } : undefined}
        />

        {/* 🔴 ONE frame on every view (DOCK1a): the right side bar and the panel stay whatever the
            centre shows, as VS Code's workbench does. One element in one place, so what is open, selected and sized survives a change
            of view. Sessions' list and main area are its own; every other view hands in its layout. A browser
            keeps the view's list and main area and never the side bar or the panel, which hold this machine's
            sessions (D47 §4; D118 §4). */}
        {attached
          ? (
            <WorkFrame
              selected={attending}
              onSelect={setAttending}
              notify={notify}
              intent={workIntent}
              onIntentTaken={() => setWorkIntent(null)}
              // Sending work back is publishing a request, which is the platform's own door — so
              // this goes to the composer rather than growing a second one here.
              onSendBack={(repository) => {
                setOpening({ from: repository });
                open('quests');
              }}
              // A parked intake's answer is on its ask (INT4g): the same door the band's ask rows use.
              onAnswerAsk={openAsk}
              // A quest on the chain, or one the session asked (SESS1), opens where quests are read.
              onOpenQuest={openQuest}
              // Ask Daoris as a view of the frame's regions: one right region, never a second column.
              ask={<AskDaoris {...askProps} framed={false} opening={helpOpening} onOpened={() => setHelpOpening(null)} />}
              askFocus={helpFocus}
              // The person's own shell (CONSOLE4b): a frame is only drawn where a shell is attached.
              terminal
              closings={closings}
              lists={lists}
              onListMode={setListMode}
              placements={placements}
              layout={view === 'sessions' ? undefined : renderView()}
              onOpenSessions={() => open('sessions')}
            />
          )
          : (
            <ViewFrame
              layout={renderView()}
              lists={lists}
              over={closings.listOver}
              onOver={closings.setListOver}
              onListMode={setListMode}
            />
          )}
      </div>

      {/* Ambient truth, true on every view without being looked at (D55). Which circle and whether
          it syncs are as true on Quests as on Sessions, and a bar that appeared and vanished with a
          view would be chrome. Since D56 it also carries the tier — D24's "stated on every screen"
          is better served by a bar that is on every screen by construction — and what the index
          holds. */}
      <StatusBar
        // The scope lives here now, not in the strip: this bar is what is TRUE, and a circle is a
        // fact about what you are looking at rather than an action.
        //
        // 🔴 Passed ONLY when there is a choice to make. `WorkspaceSwitcher` renders null below two
        // circles (WSP5), so handing it over unconditionally replaced the bar's sentence with
        // nothing and the scope disappeared from the window entirely — caught by photographing the
        // bar, which is the only place it would ever have shown.
        scope={(workspaces.data?.length ?? 0) >= 2
          ? (
            <WorkspaceSwitcher
              bar
              workspaces={workspaces.data ?? []}
              value={scope.workspace}
              onChange={scope.setWorkspace}
            />
          )
          : undefined}
        driver={presence}
        sessions={liveSessions}
        workspace={scopeNamed}
        remote={wired}
        sync={circle && standing.data?.wired
          ? (
            <SyncStatus
              workspace={circle}
              standing={standing.data}
              conflicts={standing.data.conflicts.map((id) => ({
                id, title: everything.data?.find((quest) => quest.id === id)?.title,
              }))}
              syncing={syncNow.isPending}
              // The pass is the shell's (D50): a browser reads where the circle stands and is not
              // offered a button that could not run it.
              onSyncNow={attached ? () => onSyncNow(circle) : undefined}
              onOpenQuest={openQuest}
              // Named for the part (NAME1b, UX5 U72): the workspace's page at its remote, where it is wired (UX6g).
              onRemotes={attached ? () => open('projects', workspaceItem(circle), { workspaceSection: 'remote' }) : undefined}
            />
          )
          : undefined}
        /* Where each fact leads, and the rule is that a status item goes where the fact is SET
           rather than where it is merely repeated. The driver and the remote are both machine wiring,
           so both land on Settings; sessions land on Sessions; the index count leads to the
           repositories it was built from.

           🔴 Handed over unconditionally and refused per-item inside the bar. A browser has no
           machine settings at all, and `driver === 'absent'` is exactly that case — the bar drops
           the target itself rather than making every caller remember to. */
        onDriver={() => openSettings('driver')}
        // Where the workspace in view syncs is its page's (UX6g): at its remote on a shell, its Details in a browser.
        onRemote={() => open('projects', null, attached ? { workspaceSection: 'remote' } : { workspaceTab: 'details' })}
        onSessions={attached ? () => open('sessions') : undefined}
        onIndex={() => open('projects')}
        // Settings holds Daoris's own AI (AGT6) in a browser too, so the tier leads there everywhere.
        onTier={() => openSettings('ai')}
        // Where the setup is done (SETUP1b): absent in a browser, and once the required steps are.
        setup={setupCount}
        onSetup={() => openSettings('start')}
        tier={status.data
          ? { label: status.data.tier, note: status.data.note ?? '', semantic: status.data.semantic }
          : undefined}
        indexed={indexed > 0
          ? t('sidebar.count', {
            entries: figure(indexed),
            repositories: repositories.data?.length ?? 0,
          })
          : undefined}
      />

      {/* Every action, by name (SURF9). The registry is a pure function of what is true right now,
          so a browser's list and a shell's list differ by OMISSION rather than by a disabled row —
          a palette is a promise that what it lists can be done. */}
      {/* 🔴 Where the NAME lives now that the strip carries only the mark. It is also the honest
          home for it: a title bar in an IDE says what you can do, and the one place a person looks
          for "what is this and which version" is About. */}
      {about && (
        <Drawer title={t('menu.about')} onClose={() => setAbout(false)}>
          <div className="flex items-baseline gap-2">
            <strong className="font-serif text-wordmark font-semibold tracking-[-0.01em]">Daoris</strong>
            <span className="text-small text-ink-faint">道衍</span>
          </div>
          <Prose className="mt-2">{t('about.what')}</Prose>
          <dl className="m-0 mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-small">
            {/* What the index can answer with, not a version number nobody serves: `Status` carries
                the tier and no canon version, and inventing a field to fill a row is how a card
                starts lying. */}
            <dt className="text-ink-faint">{t('about.index')}</dt>
            <dd className="m-0 font-mono text-meta">{status.data?.tier ?? '—'}</dd>
            <dt className="text-ink-faint">{t('about.surface')}</dt>
            <dd className="m-0">{attached ? t('about.shell') : t('about.browser')}</dd>
          </dl>
        </Drawer>
      )}

      {/* Help › Keyboard shortcuts (UX7a): the table's keys by menu, as the menus print them. */}
      {shortcuts && (
        <KeyboardShortcuts groups={shortcutGroups(entries, translate)} browser={!attached} onClose={() => setShortcuts(false)} />
      )}

      <CommandPalette
        open={palette}
        onClose={() => setPalette(false)}
        // The menus' rows that apply now, under their menu's name (D152 §2): one table, so a palette row is a menu row.
        commands={paletteCommands(entries, translate).map(counted)}
        // The command center's one question (DOCK1d): what was typed, asked in Quick Ask.
        onAsk={attached ? (question) => { logEvent('command.run', { command: 'ask' }); askQuickly(question); } : undefined}
      />

      {attached && (
        <QuickAsk
          open={quick}
          onClose={() => setQuick(false)}
          onExpand={() => { setQuick(false); openHelp(); }}
        >
          <AskDaoris
            {...askProps}
            framed={false}
            opening={quickOpening}
            // The box is drawn anew each time it opens: a question still held would be asked again.
            onOpened={() => setQuickOpening(null)}
            // A starter's door leads away from the box, so the box goes with it.
            onGo={(door) => { setQuick(false); askProps.onGo(door); }}
            onClose={() => setQuick(false)}
          />
        </QuickAsk>
      )}

      <Toasts items={toasts} onClose={dismiss} />
      {/* The window's one right-click handler (CTX1, D138): each surface offers its acts, and it draws the menu. */}
      <ContextMenus doors={contextDoors} shell={chrome.present} />
      {/* A notification is a door (design §4): clicking the OS balloon lands on that session
          rather than on whatever was last open. */}
      <ShellSignals notify={notify} onAttend={openInWork} />
    </div>
    </MainOffer.Provider>
    </LinkOpener.Provider>
    </HarnessRuns>
  );
}

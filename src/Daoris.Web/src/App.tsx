import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  useAsks, useEntry, useQuests, useRefreshIndex, useRegistry, useRepositories, useSessions,
  useStatus, useSyncStanding, useWorkspaceHoldings, useWorkspaces,
} from './queries';
import { useScope } from './scope';
import { AskDaoris } from './help/AskDaoris';
import { QuickAsk } from './help/QuickAsk';
import { opensAtStart, setupProgress, setupSteps } from './help/setup';
import { useMachine } from './help/useMachine';
import { useSetupAtStart } from './setupGuide';
import { useFrameClosings } from './work/closings';
import { type ListMode, type ListView, listToggled } from './work/layout';
import { useListPanes } from './work/listPanes';
import { usePlacements, viewsIn } from './work/placements';
import { LAYOUT_KEYS, type LayoutRegion, LayoutToggles } from './work/LayoutToggles';
import { frameShortcut } from './shortcuts';
import { focusRegion } from './work/regions';
import type { StarterDoor } from './help/starters';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';
import {
  Button, Drawer, failure, Icon, LanguageSwitcher, Prose, SESSION_ACTIVE, Tip, Toasts,
  useErrorNotify, useToasts,
} from './ui';
import { OverviewView } from './OverviewView';
import { ConvergenceView } from './ConvergenceView';
import { MapView } from './MapView';
import { SearchView } from './SearchView';
import { QuestsView } from './QuestsView';
import { ProjectsView } from './ProjectsView';
import { type SettingsAnchor, type SettingsSection, SettingsView } from './SettingsView';
import { Reader } from './Reader';
import { ShellSignals } from './ShellSignals';
import {
  logEvent, useDriver, useLinkOpener, useOpenBrowser, useOpenWindow, useRemotes, useRules, useSyncNow, useTrustFolder,
  useUntrusted,
} from './shell';
import { LinkOpener } from './links';
import { BrowserDoor } from './work/BrowserDoor';
import { browserDrivers } from './work/browserDrivers';
import { appMenus, menuAction } from './work/appMenus';
import type { TrustHold } from './signals';
import { TrustAsk } from './work/TrustAsk';
import { SyncStatus } from './work/SyncStatus';
import { MONITOR_WINDOW, sessionWindowName } from './work/window';
import { WorkFrame } from './work/WorkFrame';
import type { AttentionDoors } from './work/AttentionBand';
import { needsAPerson, waitingInSessions } from './work/attention';
import { ActivityBar, AppStrip, type DriverPresence, StatusBar } from './work/frame';
import { useWindowChrome } from './windowChrome';
import { type Command, commands, type View, VIEWS } from './commands';
import { CommandPalette } from './work/CommandPalette';
import { CommandCenter } from './work/CommandCenter';
import { AppMenu, AppMenuBar } from './work/AppMenu';
import { store, stored } from './lib/stored';
import { figure } from './format';
import { HarnessRuns } from './harnessRuns';

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
// An id that no longer names a record is cleared by the view's own effect, and a domain this window
// cannot show opens on Appearance, so a stale one costs nothing.

function rememberedView(): Tab {
  // Landing on Overview is the safe half of the choice.
  return stored(VIEW) === 'sessions' ? 'sessions' : 'overview';
}

// The activity bar: every view but Settings, which has its own gear. Sessions — what the Work frame
// was (D66) — is absent in a browser rather than disabled.
const NAV = VIEWS.filter(({ view }) => view !== 'settings');

/**
 * The views drawn with a list pane (D118 §2), each naming it in `layout.list.<view>` and
 * `layout.menu.list.<view>`. Sessions' first (FRAME1b); each view joins as its row moves it onto the frame.
 */
const LISTED: ReadonlySet<ListView> = new Set<ListView>(['sessions']);
const isListed = (view: View): view is View & ListView => (LISTED as ReadonlySet<string>).has(view);

/** A palette command that says it ran, into the machine log (LOG1b): by its id, never what was typed. */
const counted = (command: Command): Command => ({
  ...command,
  run: () => {
    logEvent('command.run', { command: command.id });
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
  const settingsSection = (lists.pane('settings').chosen as SettingsSection | null) ?? 'appearance';
  // The part of a Settings domain a menu item named, brought into view once it is drawn (UX5 U72).
  const [settingsAnchor, setSettingsAnchor] = useState<SettingsAnchor | null>(null);
  const [readingId, setReadingId] = useState<string | null>(null);
  // A quest the review asked for (SURF6b): the repository whose work is being sent back, handed
  // to the composer as an opening draft. Held here because the door crosses two views.
  const [opening, setOpening] = useState<{ from?: string; to?: string } | null>(null);
  // The Workspace menu's *Add repository…* (D75), an event Projects consumes, like the draft above.
  const [addRequested, setAddRequested] = useState(false);
  // And its *Import a folder…* (D77): the import drawer, which names the workspace it lands in.
  const [importRequested, setImportRequested] = useState(false);
  // A quest a door asked Quests to open in its drawer — the sync item's conflict list (SYNC6b). An
  // event like the opening draft: Quests consumes it and says so.
  const [questFocus, setQuestFocus] = useState<string | null>(null);
  // The palette asked for the ask composer (INT4c) — an event Quests consumes, like the two above.
  const [asking, setAsking] = useState(false);
  // An ask a door asked Quests to open in its record — Overview's band (INT4d). The same kind of event.
  const [askFocus, setAskFocus] = useState<string | null>(null);
  // The palette (SURF9), and what it asks the Work frame to do. Both are events consumed on arrival
  // rather than state, for the reason the quest composer's opening draft is.
  const [palette, setPalette] = useState(false);
  // The application's own card. It is where the NAME lives now that the strip carries only the mark.
  const [about, setAbout] = useState(false);
  const [workIntent, setWorkIntent] = useState<'start' | 'review' | null>(null);
  const { toasts, notify, dismiss } = useToasts();

  // The window this page is the chrome of (SURF7). In a browser `present` is false and the strip is
  // simply a strip: no caption room, no drag, nothing to command.
  const chrome = useWindowChrome();

  const status = useStatus();
  const repositories = useRepositories();
  // The badge shares the Quests view's cache — one fetch, two readers.
  const outstanding = useQuests(null, false);
  // The Reader's document rides the same cache as every other read — re-opening an entry is free.
  const reading = useEntry(readingId);
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
  const [quickOpening, setQuickOpening] = useState<{ text: string; id: number } | null>(null);
  // The side bar's question (SETUP1b): the setup guide's *Set up with Ask Daoris*, held until it is sent.
  const [helpOpening, setHelpOpening] = useState<{ text: string; id: number } | null>(null);
  const askSetup = (message: string) => {
    openings.current += 1;
    setHelpOpening({ text: message, id: openings.current });
    openHelp();
  };
  const askQuickly = (question?: string) => {
    if (question) {
      openings.current += 1;
      setQuickOpening({ text: question, id: openings.current });
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
  // The folders the driver is holding for the agent's trust (D73), from the tick, and the one being
  // asked about. The band's row opens the question; only the person's press grants it.
  const untrusted = useUntrusted();
  const grantTrust = useTrustFolder();
  const [trusting, setTrusting] = useState<TrustHold | null>(null);
  // Opening a window is the shell's act, not the page's (SURF8). In a browser it simply rejects,
  // which is why the commands that use it are gated on a shell being here.
  const openWindow = useOpenWindow();
  const openBrowser = useOpenBrowser();
  // Where the page's links open (BRW7): Daoris's browser where the person chose it and a shell is here
  // to open one, and otherwise null — a link then opens as a link always has.
  const linkOpener = useLinkOpener(notify);

  // Sessions does not exist in a browser (D55): no stream, no tree path, nothing honest to show. A
  // remembered `sessions` where no shell answers falls back rather than rendering an empty view.
  const view: Tab = tab === 'sessions' && !attached ? 'overview' : tab;
  onSessions.current = view === 'sessions';

  // What is used, and what never is (LOG1b): each view the person lands on, into the machine log — once
  // the shell has answered, so a relaunch into Sessions is not first logged as the Overview it stands in for.
  const settling = driver.isLoading;
  useEffect(() => {
    if (!settling) logEvent('view.opened', { view });
  }, [view, settling]);

  // Whether the view in front has a list pane (D118 §3a), whose four doors — the strip's toggle, the View
  // menu's item, Ctrl+B and a press on its place — are absent where it has none, never disabled.
  const listed = attached && isListed(view);

  // A region toggled (DOCK1c): the side bar and the panel are the frame's on every view since DOCK1a, and
  // the list is the view's own, so where the view has none its key does nothing. True when it did. In a
  // browser there is no frame, only the view (D47 §4: the regions hold this machine's sessions).
  const toggleRegion = (region: LayoutRegion): boolean => {
    if (!attached) return false;
    if (region === 'right') closings.setDock(!closings.dock);
    else if (region === 'panel') closings.setPanel(!closings.panel);
    else if (listed && isListed(view) && listMode) {
      // By what the room made of it: an open list closes, one laid over goes, and a strip opens — over
      // the main area where the window drew it (D118 §3a). The closing is this view's own (§3f).
      const next = listToggled({ mode: listMode });
      lists.setClosed(view, next.closed);
      closings.setListOver(next.over);
    }
    else return false;
    return true;
  };
  const toggleRegionRef = useRef(toggleRegion);
  toggleRegionRef.current = toggleRegion;

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

  useErrorNotify(reading.error, notify);

  const onRefresh = () => refresh.mutate(undefined, {
    onSuccess: (report) => {
      notify(t('sidebar.refreshed', report));
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
  // Overview's badge, from the one derivation the band uses — two answers to "how many need me" would
  // disagree the first time either was edited. Sessions' badge is its own sessions only (U20).
  const waiting = needsAPerson(
    running.data ?? [], outstanding.data ?? [], registry.data ?? [], asks.data ?? [], untrusted.data ?? []).length;
  const sessionsWaiting = waitingInSessions(running.data ?? []);
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

  const setView = (next: Tab) => {
    setTab(next);
    store(VIEW, next === 'sessions' ? 'sessions' : null);
  };

  /**
   * Settings, open at one domain (D75). Every way in names the domain its fact is set in: the tier
   * opens Daoris's own AI, the remote opens Workspace, the driver opens Driver. Before this each one
   * opened the whole page at its top.
   */
  const chooseSettings = (section: SettingsSection) => {
    lists.choose('settings', section);
    // A domain chosen from the list opens at its top: an anchor its part never answered is dropped.
    setSettingsAnchor(null);
  };
  const openSettings = (section: SettingsSection, anchor?: SettingsAnchor) => {
    chooseSettings(section);
    setSettingsAnchor(anchor ?? null);
    setView('settings');
  };

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

  // The menus by domain (D75), as data. The count waiting is the rules' own, where they are answered.
  const rules = useRules();
  const menus = appMenus({
    attached,
    workspaces: holdings.data ?? [],
    scope: scope.workspace,
    waiting: (Array.isArray(rules.data?.proposals) ? rules.data.proposals : [])
      .filter((proposal) => proposal.state === 'waiting').length,
  });
  const onMenu = (_menu: string, item: string) => {
    const action = menuAction(item);
    switch (action.kind) {
      case 'settings': openSettings(action.section, action.anchor); return;
      case 'scope': scope.setWorkspace(action.workspace); return;
      case 'add': setAddRequested(true); setView('projects'); return;
      // `daoris import <folder> --workspace <name>`'s screen door (D77): the drawer chooses the folder
      // and names the workspace, and the service's sentence says what it registered.
      case 'import': setImportRequested(true); setView('projects'); return;
      case 'refresh': onRefresh(); return;
      case 'language': void i18n.changeLanguage(i18n.language.startsWith('zh') ? 'en' : 'zh'); return;
      case 'about': setAbout(true); return;
      default:
    }
  };

  // Ctrl/Cmd+K, the one this class of application has agreed on. Captured on the window so it works
  // wherever focus is — except inside a text field, where a person typing is typing. F1 is help's key
  // everywhere, a field included, since it types nothing (HELP1).
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      // The frame's own keys (`shortcuts.ts`), the one list the terminal leaves to the frame too: Quick
      // Ask (DOCK1d), Ask Daoris (HELP1), and the region toggles (DOCK1c). Anywhere, a field included:
      // none of them types anything there.
      const shortcut = frameShortcut(event);
      // The regions in turn (D118 §3e): in a browser too, whose window has its bar, its view and its status.
      if (shortcut === 'nextRegion' || shortcut === 'previousRegion') {
        event.preventDefault();
        focusRegion(document, shortcut === 'previousRegion');
        return;
      }
      if (shortcut === 'quickAsk' || shortcut === 'help') {
        if (!attached) return;
        event.preventDefault();
        if (shortcut === 'quickAsk') setQuick(true);
        else openHelp();
        return;
      }
      if (shortcut) {
        const region: LayoutRegion = shortcut;
        if (toggleRegionRef.current(region)) event.preventDefault();
        return;
      }
      if (event.key !== 'k' || !(event.ctrlKey || event.metaKey)) return;
      const inside = event.target as HTMLElement | null;
      if (inside?.tagName === 'INPUT' || inside?.tagName === 'TEXTAREA') return;
      event.preventDefault();
      setPalette((was) => !was);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [attached, openHelp]);

  // The attended session is remembered alongside the view (D56), so a relaunch into Sessions reopens
  // what the person was watching rather than an empty column.
  const { choose } = lists;
  const setAttending = useCallback((next: string | null) => choose('sessions', next), [choose]);

  // A door from a record into the session itself. The selection lives here rather than inside the
  // view precisely so a door can name which session it is opening (D55: one selection, every
  // region) — the view change alone would land the person on whatever they last attended.
  const openInWork = (session: string) => {
    setAttending(session);
    setView('sessions');
  };

  // A row in Overview's band is a door into whatever is waiting, wherever that exists. A parked
  // session opens in Sessions, which only a shell has. An ask opens its record, where it is answered
  // (INT4d), and a quest nobody can take opens its own drawer. Both of those a browser has too.
  const openAsk = (id: string) => { setAskFocus(id); setView('quests'); };
  // Where a starter's or a setup step's door leads (HELP1d, SETUP1a): a domain of Settings at the part it
  // names, a view, or one of the Workspace menu's drawers, opened on Projects as the menu opens them.
  const go = (door: StarterDoor) => {
    if (door.view === 'settings' && door.section) {
      openSettings(door.section, door.anchor);
      return;
    }
    if (door.drawer === 'add') setAddRequested(true);
    if (door.drawer === 'import') setImportRequested(true);
    setView(door.view);
  };
  // What Ask Daoris is handed wherever it stands: what is on the screen (HELP1b) — the view, the scope,
  // the settings domain on Settings, and the attended session on Sessions — and its two ways out.
  const askProps = {
    where: {
      view, workspace: scope.workspace ?? null, settings: settingsSection,
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

  const attentionDoors: AttentionDoors = {
    ...(attached ? { parked: (item) => openInWork(item.id) } : {}),
    proposal: (item) => openAsk(item.id),
    intake: (item) => openAsk(item.id),
    unanswerable: (item) => { setQuestFocus(item.id); setView('quests'); },
    // A folder waiting on the person's trust (D73) opens the question itself. Only a shell has one.
    ...(attached ? { trust: (item) => item.trust && setTrusting(item.trust) } : {}),
    // An agent's proposal to widen the rules (PERM2) opens the rules it would change, where it is
    // answered beside them. Only a shell reads the rules.
    ...(attached ? { rule: () => openSettings('permissions') } : {}),
  };

  /** Every view but Sessions, as the frame's centre in a shell and the whole window in a browser (DOCK1a). */
  const renderView = () => (
    // `relative`: the containing block for what is positioned inside the column. Without it an
    // `sr-only` label far down a long page took the viewport as its block and stretched the
    // document, which grew a second scrollbar beside this one (seen on the window, PERM1).
    <main data-region="main" className="relative min-h-0 min-w-0 flex-1 overflow-y-auto px-6 pb-12 pt-5 max-md:px-3 max-md:pb-8 max-md:pt-4">
      {/* No cap: content follows the window (UX5 U59, the owner), as the session's centre does
          since U16. It was 72rem, and a maximized window left every view a third empty.
          Prose keeps its own measure (`Prose`), and a form its own size. */}
      <div>
        {view === 'overview' && (
          <OverviewView
            onNavigate={setView}
            onOpenQuest={(id) => { setQuestFocus(id); setView('quests'); }}
            doors={attentionDoors}
            notify={notify}
          />
        )}
        {view === 'quests' && (
          <QuestsView
            notify={notify}
            onAttend={attached ? openInWork : undefined}
            opening={opening}
            onOpened={() => setOpening(null)}
            focus={questFocus}
            onFocused={() => setQuestFocus(null)}
            asking={asking}
            onAsked={() => setAsking(false)}
            askFocus={askFocus}
            onAskFocused={() => setAskFocus(null)}
          />
        )}
        {view === 'projects' && (
          <ProjectsView
            notify={notify}
            addRequested={addRequested}
            onAddOpened={() => setAddRequested(false)}
            importRequested={importRequested}
            onImportOpened={() => setImportRequested(false)}
          />
        )}
        {view === 'map' && (
          <MapView
            notify={notify}
            onOpenConvergence={() => setView('convergence')}
            onOpenQuest={(id) => { setQuestFocus(id); setView('quests'); }}
          />
        )}
        {view === 'convergence' && (
          <ConvergenceView semantic={status.data?.semantic ?? false} onOpen={setReadingId} notify={notify} />
        )}
        {view === 'search' && (
          <SearchView
            onOpen={setReadingId}
            notify={notify}
            semantic={status.data?.semantic ?? false}
            onConverge={() => setView('convergence')}
          />
        )}
        {view === 'settings' && (
          <SettingsView
            notify={notify}
            section={settingsSection}
            onSection={chooseSettings}
            anchor={settingsAnchor}
            onAnchored={() => setSettingsAnchor(null)}
            onGo={go}
            // Ask Daoris is the shell's (HELP1): a browser's guide offers no hand-off.
            onAskSetup={attached ? askSetup : undefined}
            // Who is driving Daoris's browser (BRW8), for its domain, each a door into Sessions.
            browserDrivers={driving}
            onAttend={attached ? openInWork : undefined}
          />
        )}
      </div>
    </main>
  );

  return (
    // A tool's running action — a sign-in above all — outlives the view it started on (SIGNIN1).
    <HarnessRuns notify={notify}>
    {/* Every link on the page opens through one place, told here where to (BRW7). */}
    <LinkOpener.Provider value={linkOpener}>
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
        // 🔴 The APPLICATION's menus, which is what a title bar holds in an IDE, and since D75 they
        // are the setup domains: Daoris, Workspace, Agents, then View. Each setup item opens its own
        // domain of Settings. Navigating is NOT here: that is the rail's, and since D66 the rail is
        // the only navigation there is. The items are `appMenus`'s, built as data.
        menus={(
          <AppMenuBar>
            <AppMenu label={t('menu.app')} trigger="app" items={menus.daoris} onChoose={onMenu} />
            <AppMenu label={t('menu.workspace')} trigger="workspace" items={menus.workspace} onChoose={onMenu} />
            <AppMenu label={t('menu.agents')} trigger="agents" items={menus.agents} onChoose={onMenu} />
            <AppMenu
              label={t('menu.view')}
              trigger="view"
              items={[
                { id: 'palette', label: t('palette.title'), icon: 'search' },
                // A window is the shell's to open, so a browser is offered none — the item was there
                // and did nothing (REV3), which the machine items above already knew not to do.
                ...(attached ? [
                  { id: 'monitor', label: t('work.menu.monitor'), icon: 'monitor' as const, separated: true },
                  // Daoris's own browser (D78): where the person signs in, and watches a session use it.
                  { id: 'browser', label: t('work.menu.browser'), icon: 'browser' as const },
                ] : []),
                // The region toggles' second door (DOCK1c, SURF11), with their keys, ticked while shown: the
                // view's list, named for the view (D118 §3a), and the panel and the side bar on every view (DOCK1a).
                ...(listed ? [
                  {
                    id: 'layout:list', label: t(`layout.menu.list.${view}`), icon: LAYOUT_KEYS.list.icon, shortcut: LAYOUT_KEYS.list.keys,
                    checked: listShown, separated: true,
                  },
                ] : []),
                ...(attached ? [
                  {
                    id: 'layout:panel', label: t('layout.menu.panel'), icon: LAYOUT_KEYS.panel.icon, shortcut: LAYOUT_KEYS.panel.keys,
                    checked: !closings.panel, separated: !listed,
                  },
                  { id: 'layout:right', label: t('layout.menu.right'), icon: LAYOUT_KEYS.right.icon, shortcut: LAYOUT_KEYS.right.keys, checked: !closings.dock },
                  // VS Code's *Reset View Locations* (DOCK1b): every view back where it started. Said, and
                  // not choosable, while nothing has moved.
                  { id: 'views:reset', label: t('work.views.reset'), icon: 'refresh' as const, disabled: !placements.moved, separated: true },
                ] : []),
              ]}
              onChoose={(_, item) => {
                if (item === 'palette') { setPalette(true); return; }
                if (item === 'views:reset') { placements.reset(); return; }
                if (item.startsWith('layout:')) { toggleRegion(item.slice('layout:'.length) as LayoutRegion); return; }
                if (item === 'monitor' && attached) openWindow.mutate(MONITOR_WINDOW);
                if (item === 'browser' && attached) openBrowser.mutate();
              }}
            />
          </AppMenuBar>
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
        trailing={attached ? (
          <div className="flex items-center gap-2">
            <BrowserDoor onOpen={() => openBrowser.mutate()} drivers={driving} onAttend={openInWork} />
            <LayoutToggles
              regions={['list', 'panel', 'right']}
              list={listed ? t(`layout.list.${view}`) : undefined}
              closed={{ list: !listShown, panel: closings.panel, right: closings.dock }}
              onToggle={toggleRegion}
            />
          </div>
        ) : undefined}
      />

      {/* No narrow-window stacking. The 15rem sidebar this replaced had to become a top bar under
          768px (D41 §2) — a 48px icon rail does not, and stacking it was actively wrong: it put a
          276px-tall column of icons ABOVE the content and left the page 105px, measured in a real
          browser at 686px. One layout at every width, which is also what keeps D55's "a window, not
          a page" true on a narrow screen instead of only on a wide one. */}
      <div className="flex min-h-0 flex-1">
        {/* The application's one navigation (D66): every view is one click from every other, and
            nothing is gated behind a mode. Its foot holds ACTIONS, then Settings — the state it used
            to carry went to the status bar, where ambient state belongs. */}
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
                : target === 'sessions' ? sessionsWaiting : undefined,
            tone: target === 'overview' || target === 'sessions' ? 'open' as const : undefined,
          }))}
          // Settings is everywhere now (D66) — a browser has appearance to set, if nothing of a machine.
          end={[{ tab: 'settings', label: t('nav.settings'), icon: 'settings' }]}
          active={view}
          onSelect={setView}
          // The list's fourth door (D118 §3a): the place you are on, pressed again, toggles its list.
          onToggleCurrent={listed ? () => { toggleRegion('list'); } : undefined}
          footer={(
            <>
              <Tip content={refresh.isPending ? t('sidebar.refreshing') : t('sidebar.refresh')}>
                <Button
                  variant="ghost"
                  aria-label={t('sidebar.refresh')}
                  disabled={refresh.isPending}
                  onClick={onRefresh}
                  className="h-9 w-9 justify-center px-0"
                >
                  <Icon name="refresh" size={15} />
                </Button>
              </Tip>
              <LanguageSwitcher compact />
            </>
          )}
        />

        {/* 🔴 ONE frame on every view (DOCK1a): the right side bar and the panel stay whatever the
            centre shows, as VS Code's workbench does. One element in one place, so what is open, selected and sized survives a change
            of view. Sessions' centre is its own; every other view is handed in. A browser has no frame:
            its regions hold this machine's sessions (D47 §4), so it shows the view alone. */}
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
                setView('quests');
              }}
              // A parked intake's answer is on its ask (INT4g): the same door the band's ask rows use.
              onAnswerAsk={openAsk}
              // A quest on the chain, or one the session asked (SESS1), opens where quests are read.
              onOpenQuest={(id) => { setQuestFocus(id); setView('quests'); }}
              // Ask Daoris as a view of the frame's regions: one right region, never a second column.
              ask={<AskDaoris {...askProps} framed={false} opening={helpOpening} onOpened={() => setHelpOpening(null)} />}
              askFocus={helpFocus}
              // The person's own shell (CONSOLE4b): a frame is only drawn where a shell is attached.
              terminal
              closings={closings}
              lists={lists}
              onListMode={setListMode}
              placements={placements}
              content={view === 'sessions' ? undefined : renderView()}
              onOpenSessions={() => setView('sessions')}
            />
          )
          : renderView()}
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
              onOpenQuest={(id) => { setQuestFocus(id); setView('quests'); }}
              // Named for the part (NAME1b, UX5 U72), so it opens at Wiring, as *Wire to a remote…* does.
              onRemotes={attached ? () => openSettings('workspace', 'wiring') : undefined}
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
        onRemote={() => openSettings('workspace')}
        onSessions={attached ? () => setView('sessions') : undefined}
        onIndex={() => setView('projects')}
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

      <CommandPalette
        open={palette}
        onClose={() => setPalette(false)}
        commands={commands({
          label: (id) => t(`command.${id}`),
          group: (id) => t(`palette.group.${id}`),
          attached,
          current: view,
          go: setView,
          refresh: onRefresh,
          toggleLanguage: () => void i18n.changeLanguage(
            i18n.language.startsWith('zh') ? 'en' : 'zh'),
          startSession: () => { setView('sessions'); setWorkIntent('start'); },
          review: () => { setView('sessions'); setWorkIntent('review'); },
          // The second screen (SURF8). Opening a window is the shell's act, so both of these are
          // absent in a browser by the same omission every other shell-only command uses.
          monitor: () => openWindow.mutate(MONITOR_WINDOW),
          browser: () => openBrowser.mutate(),
          detach: attending
            ? () => openWindow.mutate(sessionWindowName(attending))
            : undefined,
          // Asking lives at the head of Quests (INT4c); the palette goes there and opens the composer.
          ask: () => { setView('quests'); setAsking(true); },
          help: openHelp,
          quickAsk: () => askQuickly(),
          setup: () => openSettings('start'),
        }).map(counted)}
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

      {reading.data && <Reader entry={reading.data} onClose={() => setReadingId(null)} />}
      {trusting && (
        <Drawer title={t('trust.title')} onClose={() => setTrusting(null)}>
          <TrustAsk
            hold={{
              ...trusting,
              // What it holds, named: the band groups one folder's holds into one row.
              ...(untrusted.data ?? []).find((hold) =>
                hold.folder === trusting.folder && hold.trustFile === trusting.trustFile),
            }}
            busy={grantTrust.isPending}
            onCancel={() => setTrusting(null)}
            onGrant={() => grantTrust.mutate(trusting, {
              onSuccess: (granted) => {
                notify(granted.message, granted.verified ? 'ok' : 'error');
                setTrusting(null);
              },
              onError: failure(notify),
            })}
          />
        </Drawer>
      )}
      <Toasts items={toasts} onClose={dismiss} />
      {/* A notification is a door (design §4): clicking the OS balloon lands on that session
          rather than on whatever was last open. */}
      <ShellSignals notify={notify} onAttend={openInWork} />
    </div>
    </LinkOpener.Provider>
    </HarnessRuns>
  );
}

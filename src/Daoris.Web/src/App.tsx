import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  useAsks, useEntry, useImportFolder, useQuests, useRefreshIndex, useRegistry, useRepositories, useSessions,
  useStatus, useSyncStanding, useWorkspaceHoldings, useWorkspaces,
} from './queries';
import { useScope } from './scope';
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
import { type SettingsSection, SettingsView } from './SettingsView';
import { Reader } from './Reader';
import { ShellSignals } from './ShellSignals';
import {
  useDriver, useOpenWindow, usePickFolder, useRemotes, useRules, useSyncNow, useTrustFolder, useUntrusted,
} from './shell';
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
import { commands, type View, VIEWS } from './commands';
import { CommandPalette } from './work/CommandPalette';
import { CommandCenter } from './work/CommandCenter';
import { AppMenu, AppMenuBar } from './work/AppMenu';
import { store, stored } from './lib/stored';
import { figure } from './format';

/** The views are `commands.ts`'s one list (D66); the activity bar and the palette read the same. */
type Tab = View;

/**
 * Whether the person was last watching Sessions — a per-browser preference like the language and the
 * scope, never machine wiring and never a tracked file. Only Sessions is worth returning to (D55's
 * reason for remembering a mode, kept when the mode went — D66): a relaunch lands on Overview, the
 * landing view (D40), unless the person was attending work.
 */
const VIEW = 'daoris.view';

/**
 * And which session they were attending (D56). Sessions survived a restart and the selection did
 * not, so relaunching into it landed on *Nothing attended* while a session sat parked — the one
 * arrangement SURF5a's whole attention half exists to prevent. An id that no longer names a record
 * is cleared by the view's own effect, so a stale one costs nothing.
 */
const ATTENDING = 'daoris.attending';

/**
 * And which domain of Settings they last had open (D75), so the gear returns to it. A domain this
 * window cannot show is Settings' own business: it opens on Appearance instead.
 */
const SETTINGS_SECTION = 'daoris.settings';

function rememberedView(): Tab {
  // Landing on Overview is the safe half of the choice.
  return stored(VIEW) === 'sessions' ? 'sessions' : 'overview';
}

// The activity bar: every view but Settings, which has its own gear. Sessions — what the Work frame
// was (D66) — is absent in a browser rather than disabled.
const NAV = VIEWS.filter(({ view }) => view !== 'settings');

/**
 * The platform: the person's window over the family (D38), landing on management (D40), wearing the
 * console shell (D41), on properly-tooled foundations (D42). Doctrine stays unwritable from every
 * view (D31); the service's sentences render verbatim; UI chrome speaks the active language.
 */
export function App() {
  const { t, i18n } = useTranslation();
  const [tab, setTab] = useState<Tab>(rememberedView);
  const [attending, setAttendingState] = useState<string | null>(() => stored(ATTENDING));
  const [settingsSection, setSettingsSection] = useState<SettingsSection>(
    () => (stored(SETTINGS_SECTION) as SettingsSection | null) ?? 'appearance');
  const [readingId, setReadingId] = useState<string | null>(null);
  // A quest the review asked for (SURF6b): the repository whose work is being sent back, handed
  // to the composer as an opening draft. Held here because the door crosses two views.
  const [opening, setOpening] = useState<{ from?: string; to?: string } | null>(null);
  // The Workspace menu's *Add repository…* (D75), an event Projects consumes, like the draft above.
  const [addRequested, setAddRequested] = useState(false);
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

  // Sessions does not exist in a browser (D55): no stream, no tree path, nothing honest to show. A
  // remembered `sessions` where no shell answers falls back rather than rendering an empty view.
  const view: Tab = tab === 'sessions' && !attached ? 'overview' : tab;

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

  const presence: DriverPresence = driver.data ? 'running' : driver.isError ? 'stopped' : 'absent';
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
    setSettingsSection(section);
    store(SETTINGS_SECTION, section);
  };
  const openSettings = (section: SettingsSection) => {
    chooseSettings(section);
    setView('settings');
  };

  // The menus by domain (D75), as data. The count waiting is the rules' own, where they are answered.
  const rules = useRules();
  const menus = appMenus({
    attached,
    workspaces: holdings.data ?? [],
    scope: scope.workspace,
    waiting: (Array.isArray(rules.data?.proposals) ? rules.data.proposals : [])
      .filter((proposal) => proposal.state === 'waiting').length,
  });
  const pickFolder = usePickFolder();
  const importFolder = useImportFolder();
  const onMenu = (_menu: string, item: string) => {
    const action = menuAction(item);
    switch (action.kind) {
      case 'settings': openSettings(action.section); return;
      case 'scope': scope.setWorkspace(action.workspace); return;
      case 'add': setAddRequested(true); setView('projects'); return;
      // `daoris import <folder>`'s screen door: choose the folder, and the service's sentence says
      // what it registered.
      case 'import':
        pickFolder.mutate(undefined, {
          onSuccess: (folder) => folder && importFolder.mutate(folder.path, {
            onSuccess: (result) => notify(result.message),
            onError: failure(notify),
          }),
          onError: failure(notify),
        });
        return;
      case 'refresh': onRefresh(); return;
      case 'language': void i18n.changeLanguage(i18n.language.startsWith('zh') ? 'en' : 'zh'); return;
      case 'about': setAbout(true); return;
      default:
    }
  };

  // Ctrl/Cmd+K, the one this class of application has agreed on. Captured on the window so it works
  // wherever focus is — except inside a text field, where a person typing is typing.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'k' || !(event.ctrlKey || event.metaKey)) return;
      const inside = event.target as HTMLElement | null;
      if (inside?.tagName === 'INPUT' || inside?.tagName === 'TEXTAREA') return;
      event.preventDefault();
      setPalette((was) => !was);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  // The attended session is remembered alongside the view (D56), so a relaunch into Sessions reopens
  // what the person was watching rather than an empty column.
  const setAttending = useCallback((next: string | null) => {
    setAttendingState(next);
    store(ATTENDING, next);
  }, []);

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

  return (
    // A window, not a page (D55): the viewport IS the frame, every region scrolls inside it, and
    // the status bar is therefore always where it was. Page scrolling would put the output panel
    // below the fold exactly when a session is producing output.
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
                ...(attached ? [{ id: 'monitor', label: t('work.menu.monitor'), icon: 'monitor' as const, separated: true }] : []),
              ]}
              onChoose={(_, item) => {
                if (item === 'palette') { setPalette(true); return; }
                if (item === 'monitor' && attached) openWindow.mutate(MONITOR_WINDOW);
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

        {/* Sessions fills the window because it is for WATCHING; every other view keeps the reading
            cap, because that is what the cap is for (D55). */}
        {view === 'sessions'
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
            />
          )
          : (
            // `relative`: the containing block for what is positioned inside the column. Without it an
            // `sr-only` label far down a long page took the viewport as its block and stretched the
            // document, which grew a second scrollbar beside this one (seen on the window, PERM1).
            <main className="relative min-w-0 flex-1 overflow-y-auto px-6 pb-12 pt-5 max-md:px-3 max-md:pb-8 max-md:pt-4">
              <div className="max-w-6xl">
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
                  />
                )}
                {view === 'map' && <MapView notify={notify} onOpenConvergence={() => setView('convergence')} />}
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
                  <SettingsView notify={notify} section={settingsSection} onSection={chooseSettings} />
                )}
              </div>
            </main>
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
              onOpenQuest={(id) => { setQuestFocus(id); setView('quests'); }}
              onRemotes={attached ? () => openSettings('workspace') : undefined}
            />
          )
          : undefined}
        /* Where each fact leads, and the rule is that a status item goes where the fact is SET
           rather than where it is merely repeated (owner, 2026-09-22: *"better design with display
           and action (on click or on hover)"*). The driver and the remote are both machine wiring,
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
          waiting,
          go: setView,
          refresh: onRefresh,
          toggleLanguage: () => void i18n.changeLanguage(
            i18n.language.startsWith('zh') ? 'en' : 'zh'),
          startSession: () => { setView('sessions'); setWorkIntent('start'); },
          review: () => { setView('sessions'); setWorkIntent('review'); },
          // The second screen (SURF8). Opening a window is the shell's act, so both of these are
          // absent in a browser by the same omission every other shell-only command uses.
          monitor: () => openWindow.mutate(MONITOR_WINDOW),
          detach: attending
            ? () => openWindow.mutate(sessionWindowName(attending))
            : undefined,
          // Asking lives at the head of Quests (INT4c); the palette goes there and opens the composer.
          ask: () => { setView('quests'); setAsking(true); },
        })}
      />

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
  );
}

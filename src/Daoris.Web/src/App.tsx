import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import {
  useAsks, useEntry, useQuests, useRefreshIndex, useRegistry, useRepositories, useSessions, useStatus,
  useSyncStanding, useWorkspaces,
} from './queries';
import { useScope } from './scope';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';
import {
  Button, Drawer, Icon, type IconName, LanguageSwitcher, Prose, SESSION_ACTIVE, Tip, Toasts,
  type ToastItem, useErrorNotify,
} from './ui';
import { OverviewView } from './OverviewView';
import { ConvergenceView } from './ConvergenceView';
import { MapView } from './MapView';
import { SearchView } from './SearchView';
import { QuestsView } from './QuestsView';
import { ProjectsView } from './ProjectsView';
import { SettingsView } from './SettingsView';
import { Reader } from './Reader';
import { ShellSignals } from './ShellSignals';
import { useDriver, useOpenWindow, useRemotes, useSyncNow } from './shell';
import { SyncStatus } from './work/SyncStatus';
import { MONITOR_WINDOW, sessionWindowName } from './work/window';
import { WorkFrame } from './work/WorkFrame';
import type { AttentionDoors } from './work/AttentionBand';
import { needsAPerson } from './work/attention';
import { ActivityBar, AppStrip, type DriverPresence, StatusBar } from './work/frame';
import { useWindowChrome } from './windowChrome';
import { commands } from './commands';
import { capped } from './signals';
import { CommandPalette } from './work/CommandPalette';
import { CommandCenter } from './work/CommandCenter';
import { AppMenu, AppMenuBar } from './work/AppMenu';

type Tab = 'overview' | 'sessions' | 'quests' | 'projects' | 'map' | 'convergence' | 'search' | 'settings';

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

function remembered(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    // A private window, a blocked origin: not remembering is a lesser failure than not working.
    return null;
  }
}

function remember(key: string, value: string | null): void {
  try {
    if (value === null) window.localStorage.removeItem(key);
    else window.localStorage.setItem(key, value);
  } catch {
    // As above.
  }
}

function rememberedView(): Tab {
  // Landing on Overview is the safe half of the choice.
  return remembered(VIEW) === 'sessions' ? 'sessions' : 'overview';
}

const NAV: { tab: Tab; icon: IconName; shellOnly?: boolean }[] = [
  { tab: 'overview', icon: 'overview' },
  // Sessions is a view (D66), what the Work frame was: the rail, the attended session, the dock and
  // the console. Absent in a browser rather than disabled — a stream never leaves the machine that
  // produced it (D47 §4), so there is nothing a browser could be shown there.
  { tab: 'sessions', icon: 'frameWork', shellOnly: true },
  { tab: 'quests', icon: 'quests' },
  { tab: 'projects', icon: 'projects' },
  // The workspace map (MAP2, D67 §3) — a view of its own, the owner's choice: how the repositories
  // are wired, read at a glance.
  { tab: 'map', icon: 'map' },
  { tab: 'convergence', icon: 'convergence' },
  { tab: 'search', icon: 'search' },
];

/**
 * The platform: the person's window over the family (D38), landing on management (D40), wearing the
 * console shell (D41), on properly-tooled foundations (D42). Doctrine stays unwritable from every
 * view (D31); the service's sentences render verbatim; UI chrome speaks the active language.
 */
export function App() {
  const { t, i18n } = useTranslation();
  const [tab, setTab] = useState<Tab>(rememberedView);
  const [attending, setAttendingState] = useState<string | null>(() => remembered(ATTENDING));
  const [readingId, setReadingId] = useState<string | null>(null);
  // A quest the review asked for (SURF6b): the repository whose work is being sent back, handed
  // to the composer as an opening draft. Held here because the door crosses two views.
  const [opening, setOpening] = useState<{ from?: string; to?: string } | null>(null);
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
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextToast = useRef(1);

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
  useEffect(() => {
    if (workspaces.data && scope.workspace && !workspaces.data.includes(scope.workspace)) {
      scope.setWorkspace(null);
    }
  }, [workspaces.data, scope]);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const notify = useCallback((text: string, kind: 'ok' | 'error' = 'ok') => {
    const id = nextToast.current;
    nextToast.current += 1;
    // Capped, so a burst cannot climb the window — the newest are what a corner can promise to
    // show. The number has a name and a test now rather than being a `-3` nobody could search for.
    setToasts((current) => [...capped(current), { id, text, kind }]);
  }, []);

  useErrorNotify(reading.error, notify);

  const onRefresh = () => refresh.mutate(undefined, {
    onSuccess: (report) => {
      notify(t('sidebar.refreshed', report));
      // The semantic half's failure is the service's own sentence — dropped nowhere (D24).
      if (report.semanticError) notify(report.semanticError, 'error');
    },
    onError: (e) => notify(sentence(e), 'error'),
  });

  const indexed = (repositories.data ?? []).reduce((sum, r) => sum + r.total, 0);
  const outstandingCount = outstanding.data?.length ?? 0;

  const presence: DriverPresence = driver.data ? 'running' : driver.isError ? 'stopped' : 'absent';
  const liveSessions = (running.data ?? []).filter((s) => SESSION_ACTIVE.has(s.state)).length;
  // The second of design §4's two counts, from the one derivation the band uses — two answers to
  // "how many need me" would disagree the first time either was edited.
  const waiting = needsAPerson(
    running.data ?? [], outstanding.data ?? [], registry.data ?? [], asks.data ?? []).length;
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
    onError: (e) => notify(sentence(e), 'error'),
  });

  const setView = (next: Tab) => {
    setTab(next);
    remember(VIEW, next === 'sessions' ? 'sessions' : null);
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
    remember(ATTENDING, next);
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
        // 🔴 The APPLICATION's menus, which is what a title bar holds in an IDE: settings, this
        // machine's wiring, the accounts sessions run as, help. Navigating is NOT here — that is the
        // rail's, and since D66 the rail is the only navigation there is.
        menus={(
          <AppMenuBar>
            <AppMenu
              label={t('menu.app')}
              trigger="app"
              active={false}
              items={[
                { id: 'settings', label: t('menu.settings'), icon: 'settings' },
                // The machine's own pages are absent where there is no machine to have them — in
                // a browser they would open Settings on appearance, which is not what they name.
                ...(attached
                  ? [
                    { id: 'harnesses', label: t('menu.harnesses'), icon: 'inbox' as const },
                    { id: 'remotes', label: t('menu.remotes'), icon: 'convergence' as const },
                  ]
                  : []),
                { id: 'refresh', label: t('menu.refresh'), icon: 'refresh', separated: true },
                { id: 'language', label: t('menu.language'), icon: 'languages' },
                { id: 'about', label: t('menu.about'), icon: 'check', separated: true },
              ]}
              onChoose={(_, item) => {
                if (item === 'about') { setAbout(true); return; }
                if (item === 'refresh') { onRefresh(); return; }
                if (item === 'language') {
                  void i18n.changeLanguage(i18n.language.startsWith('zh') ? 'en' : 'zh');
                  return;
                }
                // Every setting lives in one view; the menu is how you reach it by name instead of
                // by remembering which icon it is.
                setView('settings');
              }}
            />
            <AppMenu
              label={t('menu.view')}
              trigger="view"
              active={false}
              items={[
                { id: 'palette', label: t('palette.title'), icon: 'search' },
                { id: 'monitor', label: t('work.menu.monitor'), icon: 'monitor', separated: true },
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
            scope={scope.workspace ?? t('palette.scope')}
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
          items={NAV.filter(({ shellOnly }) => !shellOnly || attached).map(({ tab: target, icon }) => ({
            tab: target,
            label: t(`nav.${target}`),
            icon,
            // Two counts, and only one is a status: what is waiting on a person wears the status
            // hue; how many quests are outstanding is a quantity and wears the accent.
            badge: target === 'quests' ? outstandingCount : target === 'sessions' ? waiting : undefined,
            tone: target === 'sessions' ? 'open' as const : undefined,
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
            <main className="min-w-0 flex-1 overflow-y-auto px-6 pb-12 pt-5 max-md:px-3 max-md:pb-8 max-md:pt-4">
              <div className="max-w-6xl">
                {view === 'overview' && (
                  <OverviewView
                    onNavigate={setView}
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
                {view === 'projects' && <ProjectsView notify={notify} />}
                {view === 'map' && <MapView notify={notify} onOpenConvergence={() => setView('convergence')} />}
                {view === 'convergence' && (
                  <ConvergenceView semantic={status.data?.semantic ?? false} onOpen={setReadingId} notify={notify} />
                )}
                {view === 'search' && <SearchView onOpen={setReadingId} notify={notify} />}
                {view === 'settings' && <SettingsView notify={notify} />}
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
        workspace={scope.workspace}
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
              onRemotes={attached ? () => setView('settings') : undefined}
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
        onDriver={() => setView('settings')}
        onRemote={() => setView('settings')}
        onSessions={attached ? () => setView('sessions') : undefined}
        onIndex={() => setView('projects')}
        // Settings holds Daoris's own AI (AGT6) in a browser too, so the tier leads there everywhere.
        onTier={() => setView('settings')}
        tier={status.data
          ? { label: status.data.tier, note: status.data.note ?? '', semantic: status.data.semantic }
          : undefined}
        indexed={indexed > 0
          ? t('sidebar.count', {
            entries: indexed.toLocaleString(),
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
      <Toasts items={toasts} onClose={dismiss} />
      {/* A notification is a door (design §4): clicking the OS balloon lands on that session
          rather than on whatever was last open. */}
      <ShellSignals notify={notify} onAttend={openInWork} />
    </div>
  );
}

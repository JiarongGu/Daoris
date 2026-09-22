import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import {
  useEntry, useQuests, useRefreshIndex, useRegistry, useRepositories, useSessions, useStatus,
  useWorkspaces,
} from './queries';
import { useScope } from './scope';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';
import {
  Button, Drawer, Icon, type IconName, LanguageSwitcher, Prose, SESSION_ACTIVE, Tip, Toasts,
  type ToastItem, useErrorNotify,
} from './ui';
import { OverviewView } from './OverviewView';
import { ConvergenceView } from './ConvergenceView';
import { SearchView } from './SearchView';
import { QuestsView } from './QuestsView';
import { ProjectsView } from './ProjectsView';
import { SettingsView } from './SettingsView';
import { Reader } from './Reader';
import { ShellSignals } from './ShellSignals';
import { useDriver, useOpenWindow, useRemotes } from './shell';
import { MONITOR_WINDOW, sessionWindowName } from './work/window';
import { WorkFrame } from './work/WorkFrame';
import type { Attention } from './work/AttentionRow';
import { needsAPerson } from './work/attention';
import { ActivityBar, AppStrip, type DriverPresence, type Mode, StatusBar } from './work/frame';
import { useWindowChrome } from './windowChrome';
import { commands } from './commands';
import { CommandPalette } from './work/CommandPalette';
import { CommandCenter } from './work/CommandCenter';
import { AppMenu, AppMenuBar } from './work/AppMenu';

type Tab = 'overview' | 'quests' | 'projects' | 'convergence' | 'search' | 'settings';

/**
 * Which frame the person was last in — a per-browser preference like the language and the scope
 * (D55), never machine wiring and never a tracked file. It replaces the remembered *view* the
 * design asked for, because a mode is the thing worth returning to and a view inside Manage is not.
 */
const MODE = 'daoris.mode';

/**
 * And which session they were attending (D56). The mode survived a restart and the selection did
 * not, so relaunching into Work landed on *Nothing attended* while a session sat parked — the one
 * arrangement SURF5a's whole attention half exists to prevent. An id that no longer names a record
 * is cleared by the frame's own effect, so a stale one costs nothing.
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

function rememberedMode(): Mode {
  // Landing on Manage is the safe half of the choice.
  return remembered(MODE) === 'work' ? 'work' : 'manage';
}

const NAV: { tab: Tab; icon: IconName; shellOnly?: boolean }[] = [
  { tab: 'overview', icon: 'overview' },
  { tab: 'quests', icon: 'quests' },
  { tab: 'projects', icon: 'projects' },
  { tab: 'convergence', icon: 'convergence' },
  { tab: 'search', icon: 'search' },
  // The machine's own settings, where there is a machine to have them (D50). In a browser this is
  // not a disabled tab but an absent one: the wiring is machine-local state with a credential in it,
  // and the service has no route onto it — an empty view would imply one exists.
  { tab: 'settings', icon: 'settings', shellOnly: true },
];

/**
 * The platform: the person's window over the family (D38), landing on management (D40), wearing the
 * console shell (D41), on properly-tooled foundations (D42). Doctrine stays unwritable from every
 * view (D31); the service's sentences render verbatim; UI chrome speaks the active language.
 */
export function App() {
  const { t, i18n } = useTranslation();
  const [tab, setTab] = useState<Tab>('overview');
  const [mode, setMode] = useState<Mode>(rememberedMode);
  const [attending, setAttendingState] = useState<string | null>(() => remembered(ATTENDING));
  const [readingId, setReadingId] = useState<string | null>(null);
  // A quest the review asked for (SURF6b): the repository whose work is being sent back, handed
  // to the composer as an opening draft. Held here because the door crosses the two frames.
  const [opening, setOpening] = useState<{ from?: string; to?: string } | null>(null);
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
  // Opening a window is the shell's act, not the page's (SURF8). In a browser it simply rejects,
  // which is why the commands that use it are gated on a shell being here.
  const openWindow = useOpenWindow();

  // Work does not exist over a keyed remote (D55): no stream, no tree path, nothing honest to show.
  // A remembered `work` on a machine with no shell falls back rather than rendering an empty frame.
  const frame: Mode = attached ? mode : 'manage';

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
    setToasts((current) => [...current.slice(-3), { id, text, kind }]);
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
    running.data ?? [], outstanding.data ?? [], registry.data ?? []).length;
  // Only the machine that holds the map can answer this, so elsewhere the question is not asked.
  const wired = remotes.data
    ? remotes.data.remotes.some((row) => row.workspace === (scope.workspace ?? 'default'))
    : null;

  const chooseMode = (next: Mode) => {
    setMode(next);
    remember(MODE, next);
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

  // The attended session is remembered alongside the mode (D56), so a relaunch into Work reopens
  // what the person was watching rather than an empty column.
  const setAttending = useCallback((next: string | null) => {
    setAttendingState(next);
    remember(ATTENDING, next);
  }, []);

  // A door from a record into the session itself. The selection lives here rather than inside the
  // frame precisely so a door can name which session it is opening (D55: one selection, every
  // region) — the mode change alone would land the person on whatever they last attended.
  const openInWork = (session: string) => {
    setAttending(session);
    chooseMode('work');
  };

  // A row in Overview's band is a door into whatever is waiting — a parked session opens in Work,
  // and a quest nobody can take opens where quests are answered.
  const openAttention = (item: Attention) => {
    if (item.kind === 'parked') openInWork(item.id);
    else setTab('quests');
  };

  return (
    // A window, not a page (D55): the viewport IS the frame, every region scrolls inside it, and
    // the status bar is therefore always where it was. Page scrolling would put the output panel
    // below the fold exactly when a session is producing output.
    <div className="flex h-screen flex-col overflow-hidden">
      {/* The application's one global row (D56). It holds what is true in BOTH frames, which is
          exactly why none of it belonged in a sidebar owned by one of them. SURF7 makes this strip
          the window's own chrome; until then it sits below an OS title bar, which D56 records as a
          known interim. */}
      <AppStrip
        mode={frame}
        modeAvailable={attached}
        attention={waiting}
        onMode={chooseMode}
        captionRoom={chrome.present}
        stripRef={chrome.stripRef}
        onDragStart={chrome.present ? chrome.onDragStart : undefined}
        onToggleMaximize={chrome.present ? chrome.onToggleMaximize : undefined}
        onResizeTop={chrome.present ? chrome.onResizeTop : undefined}
        // Each frame as a MENU of what is inside it (VS Code's menu bar), which the two-button
        // toggle could not be: a view in the other frame was switch-then-hunt-an-unlabelled-icon,
        // and is now one click with a name on it.
        // 🔴 The APPLICATION's menus, which is what a title bar holds in an IDE: settings, this
        // machine's wiring, the accounts sessions run as, help. Switching frames is NOT here — that
        // went to the rail, where switching belongs.
        menus={(
          <AppMenuBar>
            <AppMenu
              label={t('menu.app')}
              trigger="app"
              active={false}
              items={[
                { id: 'settings', label: t('menu.machine'), icon: 'settings' },
                { id: 'harnesses', label: t('menu.harnesses'), icon: 'inbox' },
                { id: 'remotes', label: t('menu.remotes'), icon: 'convergence' },
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
                // Every configuration surface this machine has lives in one view; the menu is how
                // you reach it by name instead of by remembering which icon it is.
                chooseMode('manage');
                setTab('settings');
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
        {/* The same bar in both frames, which is what makes them peers (D56). It replaces a 15rem
            labelled sidebar that, in Work, was six items belonging to the other frame above ~440px
            of empty column. Its foot holds ACTIONS; the state it used to carry went to the status
            bar, where ambient state belongs. */}
        <ActivityBar
          label={t('nav.label')}
          // 🔴 The frames live HERE now, at the top of the rail (owner: *"those mode/tab switches
          // can be at left menu"*). Work is ABSENT in a browser rather than disabled — the rule this
          // bar already follows for the Machine domain.
          frames={[
            { id: 'manage', label: t('work.mode.manage'), icon: 'frameManage', active: frame === 'manage' },
            ...(attached
              ? [{
                  id: 'work',
                  label: t('work.mode.work'),
                  icon: 'frameWork' as const,
                  badge: waiting,
                  active: frame === 'work',
                }]
              : []),
          ]}
          items={NAV.filter(({ shellOnly }) => !shellOnly || attached).map(({ tab: target, icon }) => ({
            tab: target,
            label: t(`nav.${target}`),
            icon,
            badge: target === 'quests' ? outstandingCount : undefined,
          }))}
          // Nothing is current in Work: the current thing is the other frame, and selecting a
          // domain here is a door back into it.
          active={frame === 'manage' ? tab : null}
          onFrame={(id) => chooseMode(id as Mode)}
          onSelect={(target) => {
            setTab(target);
            chooseMode('manage');
          }}
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

        {/* The two frames (D55). Work fills the window because it is for WATCHING; Manage keeps the
            reading cap, because that is what the cap is for. Neither carries the other's navigator
            any more — the bar above is shared and belongs to the application (D56). */}
        {frame === 'work'
          ? (
            <WorkFrame
              selected={attending}
              onSelect={setAttending}
              notify={notify}
              intent={workIntent}
              onIntentTaken={() => setWorkIntent(null)}
              // Sending work back is publishing a request, which is the platform's own door — so
              // this switches frames onto the composer rather than growing a second one here.
              onSendBack={(repository) => {
                setOpening({ from: repository });
                setTab('quests');
                chooseMode('manage');
              }}
            />
          )
          : (
            <main className="min-w-0 flex-1 overflow-y-auto px-6 pb-12 pt-5 max-md:px-3 max-md:pb-8 max-md:pt-4">
              <div className="max-w-6xl">
                {tab === 'overview' && (
                  <OverviewView
                    onNavigate={setTab}
                    onAttend={attached ? openAttention : undefined}
                    notify={notify}
                  />
                )}
                {tab === 'quests' && (
                  <QuestsView
                    notify={notify}
                    onAttend={attached ? openInWork : undefined}
                    opening={opening}
                    onOpened={() => setOpening(null)}
                  />
                )}
                {tab === 'projects' && <ProjectsView notify={notify} />}
                {tab === 'convergence' && (
                  <ConvergenceView semantic={status.data?.semantic ?? false} onOpen={setReadingId} notify={notify} />
                )}
                {tab === 'search' && <SearchView onOpen={setReadingId} notify={notify} />}
                {tab === 'settings' && attached && <SettingsView notify={notify} />}
              </div>
            </main>
          )}
      </div>

      {/* Ambient truth, true in both frames without being looked at (D55). It belongs to the
          application rather than to Work: which circle and whether it syncs are as true on a
          management screen, and a bar that appeared and vanished with a mode would be chrome.
          Since D56 it also carries the tier — D24's "stated on every screen" is better served by a
          bar that is on every screen by construction — and what the index holds. */}
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
        /* Where each fact leads, and the rule is that a status item goes where the fact is SET
           rather than where it is merely repeated (owner, 2026-09-22: *"better design with display
           and action (on click or on hover)"*). The driver and the remote are both machine wiring,
           so both land on Machine; sessions are Work's whole subject; the index count leads to the
           repositories it was built from.

           🔴 Handed over unconditionally and refused per-item inside the bar. A browser has no
           Machine view at all, and `driver === 'absent'` is exactly that case — the bar drops the
           target itself rather than making every caller remember to. */
        onDriver={() => { setTab('settings'); chooseMode('manage'); }}
        onRemote={() => { setTab('settings'); chooseMode('manage'); }}
        onSessions={() => chooseMode('work')}
        onIndex={() => { setTab('projects'); chooseMode('manage'); }}
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
          mode: frame,
          waiting,
          go: (target) => { setTab(target); chooseMode('manage'); },
          setMode: chooseMode,
          refresh: onRefresh,
          toggleLanguage: () => void i18n.changeLanguage(
            i18n.language.startsWith('zh') ? 'en' : 'zh'),
          startSession: () => { chooseMode('work'); setWorkIntent('start'); },
          review: () => { chooseMode('work'); setWorkIntent('review'); },
          // The second screen (SURF8). Opening a window is the shell's act, so both of these are
          // absent in a browser by the same omission every other shell-only command uses.
          monitor: () => openWindow.mutate(MONITOR_WINDOW),
          detach: attending
            ? () => openWindow.mutate(sessionWindowName(attending))
            : undefined,
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

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
  Button, Icon, type IconName, LanguageSwitcher, SESSION_ACTIVE, Tip, Toasts, type ToastItem,
  useErrorNotify,
} from './ui';
import { cn } from './lib/cn';
import { OverviewView } from './OverviewView';
import { ConvergenceView } from './ConvergenceView';
import { SearchView } from './SearchView';
import { QuestsView } from './QuestsView';
import { ProjectsView } from './ProjectsView';
import { SettingsView } from './SettingsView';
import { Reader } from './Reader';
import { ShellSignals } from './ShellSignals';
import { useDriver, useRemotes } from './shell';
import { WorkFrame } from './work/WorkFrame';
import type { Attention } from './work/AttentionRow';
import { needsAPerson } from './work/attention';
import { type DriverPresence, type Mode, ModeSwitch, StatusBar } from './work/frame';

type Tab = 'overview' | 'quests' | 'projects' | 'convergence' | 'search' | 'settings';

/**
 * Which frame the person was last in — a per-browser preference like the language and the scope
 * (D55), never machine wiring and never a tracked file. It replaces the remembered *view* the
 * design asked for, because a mode is the thing worth returning to and a view inside Manage is not.
 */
const MODE = 'daoris.mode';

function rememberedMode(): Mode {
  try {
    return window.localStorage.getItem(MODE) === 'work' ? 'work' : 'manage';
  } catch {
    // A private window, a blocked origin: landing on Manage is the safe half of the choice.
    return 'manage';
  }
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
  const { t } = useTranslation();
  const [tab, setTab] = useState<Tab>('overview');
  const [mode, setMode] = useState<Mode>(rememberedMode);
  const [attending, setAttending] = useState<string | null>(null);
  const [readingId, setReadingId] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextToast = useRef(1);

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
    try {
      window.localStorage.setItem(MODE, next);
    } catch {
      // Not remembering is a lesser failure than not switching.
    }
  };

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
      <div className="flex min-h-0 flex-1 max-md:flex-col max-md:overflow-y-auto">
        <aside className="flex h-full w-60 shrink-0 flex-col overflow-y-auto border-r border-line px-3.5 pb-4 pt-5 max-md:h-auto max-md:w-full max-md:flex-row max-md:items-center max-md:gap-2 max-md:border-b max-md:border-r-0 max-md:px-3.5 max-md:py-2.5">
        <div className="flex items-baseline gap-2 px-2.5 pb-4 max-md:p-0 max-md:pr-2">
          <strong className="font-serif text-[1.5rem] font-semibold tracking-[-0.01em]">Daoris</strong>
          <span className="text-[0.9rem] text-ink-faint">道衍</span>
        </div>

        {/* Manage ⇄ Work as peers (D55) — above the nav, because the nav belongs to one of them.
            SURF7 moves this into the window's own top strip, where every reference puts it. */}
        <div className="pb-3 max-md:pb-0 max-md:pr-2">
          <ModeSwitch mode={frame} available={attached} attention={waiting} onChange={chooseMode} />
        </div>

        <nav aria-label={t('nav.label')} className="grid gap-0.5 max-md:flex max-md:overflow-x-auto">
          {NAV.filter(({ shellOnly }) => !shellOnly || attached).map(({ tab: target, icon }) => (
            <button
              key={target}
              onClick={() => setTab(target)}
              className={cn(
                'flex w-full items-center gap-1.5 rounded-control px-2.5 py-2 text-[0.9rem] transition-colors duration-(--speed) max-md:w-auto max-md:whitespace-nowrap',
                tab === target
                  ? 'border-l-2 border-l-accent bg-accent-soft text-ink max-md:border-b-2 max-md:border-l-0 max-md:border-b-accent'
                  : 'border-l-2 border-l-transparent text-ink-soft hover:bg-raised hover:text-ink max-md:border-b-2 max-md:border-l-0 max-md:border-b-transparent',
              )}
            >
              <Icon name={icon} />
              {t(`nav.${target}`)}
              {target === 'quests' && outstandingCount > 0 && (
                <span className="ml-auto rounded-full border border-accent bg-accent-soft px-1.5 font-mono text-[0.68rem] tabular-nums text-accent max-md:ml-1">
                  {outstandingCount}
                </span>
              )}
            </button>
          ))}
        </nav>

        <div className="mt-auto grid gap-2 px-2.5 max-md:ml-auto max-md:mt-0 max-md:grid-flow-col max-md:items-center max-md:p-0">
          {/* Global state, in its one place (D41 §2): the scope first — it decides what every number
              below and every view beside means — then the tier, the count, the refresh. Absent while
              the deployment holds one workspace. */}
          <WorkspaceSwitcher
            workspaces={workspaces.data ?? []}
            value={scope.workspace}
            onChange={scope.setWorkspace}
          />
          {status.data && (
            /* The tier is stated on every screen, never implied (D24) — here, in the one global spot.
               The sentence itself is the service's, verbatim. */
            <Tip content={status.data.note ?? ''}>
              <span className={cn(
                'justify-center rounded-full border px-2 py-1 text-center font-mono text-[0.72rem]',
                status.data.semantic
                  ? 'border-accent bg-accent-soft text-accent'
                  : 'border-warn text-warn',
              )}
              >
                {status.data.tier}
              </span>
            </Tip>
          )}
          {indexed > 0 && (
            <span className="text-[0.75rem] tabular-nums text-ink-faint max-md:hidden">
              {t('sidebar.count', { entries: indexed.toLocaleString(), repositories: repositories.data?.length ?? 0 })}
            </span>
          )}
          <Button onClick={onRefresh} disabled={refresh.isPending} className="w-full justify-center max-md:w-auto">
            <Icon name="refresh" size={14} />
            <span className="max-md:hidden">{refresh.isPending ? t('sidebar.refreshing') : t('sidebar.refresh')}</span>
          </Button>
          <LanguageSwitcher />
        </div>
        </aside>

        {/* The two frames (D55). Work fills the window because it is for WATCHING; Manage keeps the
            reading cap, because that is what the cap is for. The nav above belongs to Manage, and
            Work borrows the sidebar rather than growing a second one. */}
        {frame === 'work'
          ? <WorkFrame selected={attending} onSelect={setAttending} notify={notify} />
          : (
            <main className="min-w-0 flex-1 overflow-y-auto px-8 pb-20 pt-7 max-md:px-4 max-md:pb-12 max-md:pt-5">
              <div className="max-w-6xl">
                {tab === 'overview' && (
                  <OverviewView
                    onNavigate={setTab}
                    onAttend={attached ? openAttention : undefined}
                    notify={notify}
                  />
                )}
                {tab === 'quests' && <QuestsView notify={notify} onAttend={attached ? openInWork : undefined} />}
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
          management screen, and a bar that appeared and vanished with a mode would be chrome. */}
      <StatusBar driver={presence} sessions={liveSessions} workspace={scope.workspace} remote={wired} />

      {reading.data && <Reader entry={reading.data} onClose={() => setReadingId(null)} />}
      <Toasts items={toasts} onClose={dismiss} />
      <ShellSignals notify={notify} />
    </div>
  );
}

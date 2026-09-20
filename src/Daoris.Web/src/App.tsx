import { useCallback, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import { useEntry, useQuests, useRefreshIndex, useRepositories, useStatus } from './queries';
import {
  Button, Icon, type IconName, LanguageSwitcher, Tip, Toasts, type ToastItem, useErrorNotify,
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
import { useDriver } from './shell';

type Tab = 'overview' | 'quests' | 'projects' | 'convergence' | 'search' | 'settings';

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
  const attached = useDriver().data !== undefined;

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

  return (
    <div className="flex min-h-screen max-md:flex-col">
      <aside className="sticky top-0 flex h-screen w-60 shrink-0 flex-col border-r border-line px-3.5 pb-4 pt-5 max-md:static max-md:h-auto max-md:w-full max-md:flex-row max-md:items-center max-md:gap-2 max-md:border-b max-md:border-r-0 max-md:px-3.5 max-md:py-2.5">
        <div className="flex items-baseline gap-2 px-2.5 pb-4 max-md:p-0 max-md:pr-2">
          <strong className="font-serif text-[1.5rem] font-semibold tracking-[-0.01em]">Daoris</strong>
          <span className="text-[0.9rem] text-ink-faint">道衍</span>
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

      <main className="min-w-0 flex-1 px-8 pb-20 pt-7 max-md:px-4 max-md:pb-12 max-md:pt-5">
        <div className="max-w-6xl">
          {tab === 'overview' && <OverviewView onNavigate={setTab} notify={notify} />}
          {tab === 'quests' && <QuestsView notify={notify} />}
          {tab === 'projects' && <ProjectsView notify={notify} />}
          {tab === 'convergence' && (
            <ConvergenceView semantic={status.data?.semantic ?? false} onOpen={setReadingId} notify={notify} />
          )}
          {tab === 'search' && <SearchView onOpen={setReadingId} notify={notify} />}
          {tab === 'settings' && attached && <SettingsView notify={notify} />}
        </div>
      </main>

      {reading.data && <Reader entry={reading.data} onClose={() => setReadingId(null)} />}
      <Toasts items={toasts} onClose={dismiss} />
      <ShellSignals notify={notify} />
    </div>
  );
}

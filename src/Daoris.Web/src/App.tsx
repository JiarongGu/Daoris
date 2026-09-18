import { useCallback, useEffect, useRef, useState } from 'react';
import { api, type Convergence, type Entry, type Hit, type Repository, type Status } from './api';
import { Icon, Toasts, type ToastItem } from './ui';
import { OverviewView } from './OverviewView';
import { ConvergenceView } from './ConvergenceView';
import { SearchView } from './SearchView';
import { QuestsView } from './QuestsView';
import { ProjectsView } from './ProjectsView';
import { Reader } from './Reader';

type Tab = 'overview' | 'quests' | 'projects' | 'convergence' | 'search';

const NAV: { tab: Tab; label: string; icon: string }[] = [
  { tab: 'overview', label: 'Overview', icon: 'overview' },
  { tab: 'quests', label: 'Quests', icon: 'quests' },
  { tab: 'projects', label: 'Projects', icon: 'projects' },
  { tab: 'convergence', label: 'Convergence', icon: 'convergence' },
  { tab: 'search', label: 'Search', icon: 'search' },
];

/**
 * The platform: the person's window over the family (D38), landing on management (D40), wearing the
 * console shell (D41): a sidebar that stays put, global state stated once at its foot, one page
 * header per view, drawers for detail, toasts for outcomes. Convergence remains the lead of the
 * knowledge half (D30), and doctrine stays unwritable from every view (D31).
 */
export function App() {
  const [tab, setTab] = useState<Tab>('overview');
  const [status, setStatus] = useState<Status | null>(null);
  const [repositories, setRepositories] = useState<Repository[]>([]);
  const [outstanding, setOutstanding] = useState(0);
  const [reading, setReading] = useState<Entry | null>(null);
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const nextToast = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  // Every outcome and every error speaks from one corner, verbatim — nothing shifts the layout.
  const notify = useCallback((text: string, kind: 'ok' | 'error' = 'ok') => {
    const id = nextToast.current;
    nextToast.current += 1;
    setToasts((current) => [...current.slice(-3), { id, text, kind }]);
    window.setTimeout(() => dismiss(id), 7000);
  }, [dismiss]);

  const fail = useCallback((text: string) => notify(text, 'error'), [notify]);

  // The one number that follows the person across views: how much is outstanding.
  const countOutstanding = useCallback((signal?: AbortSignal) => {
    api.quests(null, false, signal)
      .then((quests) => setOutstanding(quests.length))
      .catch(() => { /* the Quests view will say what is wrong; a badge stays quiet */ });
  }, []);

  useEffect(() => {
    const abort = new AbortController();
    Promise.all([api.status(abort.signal), api.repositories(abort.signal)])
      .then(([s, r]) => { setStatus(s); setRepositories(r); })
      .catch((e: Error) => { if (e.name !== 'AbortError') fail(e.message); });
    countOutstanding(abort.signal);
    return () => abort.abort();
  }, [countOutstanding, fail]);

  const open = useCallback((id: string) => {
    api.entry(id).then(setReading).catch((e: Error) => fail(e.message));
  }, [fail]);

  const refresh = useCallback(async () => {
    setRefreshing(true);
    try {
      const report = await api.refresh() as { entries: number; repositories: number };
      setRepositories(await api.repositories());
      countOutstanding();
      notify(`Indexed ${report.entries} entries from ${report.repositories} repositories.`);
    } catch (e) {
      fail((e as Error).message);
    } finally {
      setRefreshing(false);
    }
  }, [countOutstanding, notify, fail]);

  const navigate = useCallback((target: Tab) => setTab(target), []);
  const indexed = repositories.reduce((sum, r) => sum + r.total, 0);

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="wordmark">
          <strong>Daoris</strong>
          <span className="hanzi">道衍</span>
        </div>

        <nav aria-label="views">
          {NAV.map(({ tab: target, label, icon }) => (
            <button
              key={target}
              className={tab === target ? 'active' : ''}
              onClick={() => navigate(target)}
            >
              <Icon name={icon} />
              {label}
              {target === 'quests' && outstanding > 0 && <span className="badge">{outstanding}</span>}
            </button>
          ))}
        </nav>

        <div className="side-foot">
          {status && (
            /* The tier is stated on every screen, never implied (D24) — here, in the one global spot. */
            <span className={status.semantic ? 'tier on' : 'tier off'} title={status.note ?? ''}>
              {status.tier}
            </span>
          )}
          {indexed > 0 && (
            <span className="count">{indexed.toLocaleString()} entries · {repositories.length} repositories</span>
          )}
          <button onClick={refresh} disabled={refreshing}>
            <Icon name="refresh" />
            {refreshing ? 'reading…' : 'refresh index'}
          </button>
        </div>
      </aside>

      <main className="content">
        {tab === 'overview' && (
          <OverviewView repositories={repositories} onNavigate={navigate} onError={fail} />
        )}
        {tab === 'quests' && <QuestsView notify={notify} onChanged={countOutstanding} />}
        {tab === 'projects' && <ProjectsView repositories={repositories} onError={fail} />}
        {tab === 'convergence' && (
          <ConvergenceView semantic={status?.semantic ?? false} onOpen={open} onError={fail} />
        )}
        {tab === 'search' && <SearchView onOpen={open} onError={fail} />}
      </main>

      {reading && <Reader entry={reading} onClose={() => setReading(null)} />}
      <Toasts items={toasts} onClose={dismiss} />
    </div>
  );
}

export type { Convergence, Hit };

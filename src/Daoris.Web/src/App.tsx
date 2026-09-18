import { useCallback, useEffect, useState } from 'react';
import { api, type Convergence, type Entry, type Hit, type Repository, type Status } from './api';
import { OverviewView } from './OverviewView';
import { ConvergenceView } from './ConvergenceView';
import { SearchView } from './SearchView';
import { QuestsView } from './QuestsView';
import { ProjectsView } from './ProjectsView';
import { Reader } from './Reader';

type Tab = 'overview' | 'quests' | 'projects' | 'convergence' | 'search';

/**
 * The platform: the person's window over the family (D38), landing on management (D40).
 *
 * The first question a person opens this window to answer is "is anything sitting" — so Overview
 * leads, with the family's health and its outstanding work. Convergence remains the lead of the
 * knowledge half (D30's finding stands: search must not lead, because a convergence cannot be
 * searched for — to search for it you would have to know it exists). Doctrine stays unwritable from
 * every view (D31).
 */
export function App() {
  const [tab, setTab] = useState<Tab>('overview');
  const [status, setStatus] = useState<Status | null>(null);
  const [repositories, setRepositories] = useState<Repository[]>([]);
  const [outstanding, setOutstanding] = useState(0);
  const [reading, setReading] = useState<Entry | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  // The nav badge is the one number that follows the person across tabs: how much is outstanding.
  const countOutstanding = useCallback((signal?: AbortSignal) => {
    api.quests(null, false, signal)
      .then((quests) => setOutstanding(quests.length))
      .catch(() => { /* the Quests view will say what is wrong; a badge stays quiet */ });
  }, []);

  useEffect(() => {
    const abort = new AbortController();
    Promise.all([api.status(abort.signal), api.repositories(abort.signal)])
      .then(([s, r]) => { setStatus(s); setRepositories(r); })
      .catch((e: Error) => { if (e.name !== 'AbortError') setError(e.message); });
    countOutstanding(abort.signal);
    return () => abort.abort();
  }, [countOutstanding]);

  const open = useCallback((id: string) => {
    api.entry(id).then(setReading).catch((e: Error) => setError(e.message));
  }, []);

  const refresh = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      await api.refresh();
      setRepositories(await api.repositories());
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setRefreshing(false);
    }
  }, []);

  const navigate = useCallback((target: Tab) => {
    setError(null);
    setTab(target);
  }, []);

  const indexed = repositories.reduce((sum, r) => sum + r.total, 0);

  return (
    <div className="app">
      <header>
        <div className="title">
          <h1>Daoris</h1>
          <p>The family's health, its open work, and what it has learned.</p>
        </div>
        <div className="meta">
          {status && (
            /* The tier is stated on every screen, never implied. A reader looking at results has no
               way to know the semantic half was absent, and would read them as complete rather than as
               complete-for-word-overlap (D24). */
            <span className={status.semantic ? 'tier on' : 'tier off'} title={status.note ?? ''}>
              {status.tier}
            </span>
          )}
          {indexed > 0 && <span className="count">{indexed} entries · {repositories.length} repositories</span>}
          <button onClick={refresh} disabled={refreshing}>
            {refreshing ? 'reading…' : 'refresh index'}
          </button>
        </div>
      </header>

      {status && !status.semantic && <p className="note">{status.note}</p>}
      {error && <p className="error">{error}</p>}

      <nav>
        <button className={tab === 'overview' ? 'active' : ''} onClick={() => navigate('overview')}>
          Overview
        </button>
        <button className={tab === 'quests' ? 'active' : ''} onClick={() => navigate('quests')}>
          Quests{outstanding > 0 && <span className="badge">{outstanding}</span>}
        </button>
        <button className={tab === 'projects' ? 'active' : ''} onClick={() => navigate('projects')}>
          Projects
        </button>
        <button className={tab === 'convergence' ? 'active' : ''} onClick={() => navigate('convergence')}>
          Convergence
        </button>
        <button className={tab === 'search' ? 'active' : ''} onClick={() => navigate('search')}>
          Search
        </button>
      </nav>

      <main>
        {tab === 'overview' && (
          <OverviewView repositories={repositories} onNavigate={navigate} onError={setError} />
        )}
        {tab === 'quests' && <QuestsView onError={setError} onChanged={countOutstanding} />}
        {tab === 'projects' && <ProjectsView repositories={repositories} onError={setError} />}
        {tab === 'convergence' && (
          <ConvergenceView semantic={status?.semantic ?? false} onOpen={open} onError={setError} />
        )}
        {tab === 'search' && <SearchView onOpen={open} onError={setError} />}
      </main>

      {reading && <Reader entry={reading} onClose={() => setReading(null)} />}
    </div>
  );
}

export type { Convergence, Hit };

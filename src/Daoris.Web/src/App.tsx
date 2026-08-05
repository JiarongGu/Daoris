import { useCallback, useEffect, useState } from 'react';
import { api, type Convergence, type Entry, type Hit, type Repository, type Status } from './api';
import { ConvergenceView } from './ConvergenceView';
import { SearchView } from './SearchView';
import { Reader } from './Reader';

type Tab = 'convergence' | 'search';

/**
 * Convergence is the landing view, not search (D30).
 *
 * Search answers a question you already have. The findings that built this canon were comparisons —
 * two repositories saying the same thing in different words — and to search for one of those you would
 * have to already know it exists. Search is here, as the second tab, because once you know what you are
 * looking for it is the faster route.
 */
export function App() {
  const [tab, setTab] = useState<Tab>('convergence');
  const [status, setStatus] = useState<Status | null>(null);
  const [repositories, setRepositories] = useState<Repository[]>([]);
  const [reading, setReading] = useState<Entry | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => {
    const abort = new AbortController();
    Promise.all([api.status(abort.signal), api.repositories(abort.signal)])
      .then(([s, r]) => { setStatus(s); setRepositories(r); })
      .catch((e: Error) => { if (e.name !== 'AbortError') setError(e.message); });
    return () => abort.abort();
  }, []);

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

  const indexed = repositories.reduce((sum, r) => sum + r.total, 0);

  return (
    <div className="app">
      <header>
        <div className="title">
          <h1>Daoris</h1>
          <p>What the family has learned, and where it agrees with itself.</p>
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
        <button className={tab === 'convergence' ? 'active' : ''} onClick={() => setTab('convergence')}>
          Convergence
        </button>
        <button className={tab === 'search' ? 'active' : ''} onClick={() => setTab('search')}>
          Search
        </button>
      </nav>

      <main>
        {tab === 'convergence'
          ? <ConvergenceView semantic={status?.semantic ?? false} onOpen={open} onError={setError} />
          : <SearchView onOpen={open} onError={setError} />}
      </main>

      {reading && <Reader entry={reading} onClose={() => setReading(null)} />}
    </div>
  );
}

export type { Convergence, Hit };

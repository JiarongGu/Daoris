import { useEffect, useState } from 'react';
import { api, type Hit } from './api';

/**
 * The supporting view. Useful once you know what you are looking for — which is exactly the case
 * convergence cannot help with, and vice versa.
 */
export function SearchView(
  { onOpen, onError }: { onOpen: (id: string) => void; onError: (message: string) => void },
) {
  const [query, setQuery] = useState('');
  const [localOnly, setLocalOnly] = useState(true);
  const [hits, setHits] = useState<Hit[] | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const trimmed = query.trim();
    if (trimmed.length < 2) { setHits(null); return; }

    const abort = new AbortController();
    // Debounced: every keystroke is an index query, and the early ones are answers to a question the
    // person had not finished asking.
    const timer = setTimeout(() => {
      setLoading(true);
      api.search(trimmed, localOnly, abort.signal)
        .then((found) => { setHits(found); setLoading(false); })
        .catch((e: Error) => {
          if (e.name === 'AbortError') return;
          onError(e.message);
          setLoading(false);
        });
    }, 250);

    return () => { clearTimeout(timer); abort.abort(); };
  }, [query, localOnly, onError]);

  return (
    <section className="search">
      <div className="controls">
        <input
          type="search" value={query} autoFocus
          placeholder="a decision, a trap, a rule — in your own words"
          onChange={(e) => setQuery(e.target.value)}
        />
        <label className="toggle">
          <input type="checkbox" checked={localOnly} onChange={(e) => setLocalOnly(e.target.checked)} />
          {/* On by default: canonical content is byte-identical in every adopter, so including it
              returns a dozen copies of one rule and calls that a corpus. */}
          each repository's own only
        </label>
      </div>

      {loading && <p className="loading">searching…</p>}
      {!loading && hits?.length === 0 && (
        <p className="empty">
          No matches. Lexical search matches words — a repository that reached the same conclusion in
          different vocabulary will not appear here. That is what the convergence view is for.
        </p>
      )}

      <ul className="hits">
        {hits?.map((hit) => (
          <li key={hit.id}>
            <button className="link" onClick={() => onOpen(hit.id)}>{hit.title}</button>
            <span className="where">{hit.repository} · {hit.kind} · {hit.path}</span>
            {hit.excerpt && <p className="excerpt">{hit.excerpt}</p>}
          </li>
        ))}
      </ul>
    </section>
  );
}

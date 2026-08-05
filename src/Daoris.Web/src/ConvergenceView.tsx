import { useEffect, useState } from 'react';
import { api, type Convergence } from './api';

/** How each group was found, which is also how much confidence it carries. */
const METHOD: Record<Convergence['method'], { label: string; hint: string }> = {
  Convergent: {
    label: 'Same lesson, different words',
    hint: 'The finding no text comparison can make — and the reason the semantic tier exists.',
  },
  Restatement: {
    label: 'Substantially the same words',
    hint: 'Usually a copy that has since drifted.',
  },
  Identical: {
    label: 'The same document, pasted',
    hint: 'A copy, not a coincidence.',
  },
};

/**
 * The landing view (D30).
 *
 * The threshold is a control rather than a constant, and deliberately so: the useful value depends on
 * the embedder and the corpus. Measured on this family, 0.82 returns nothing, 0.70 returns exactly the
 * true pairs, and 0.60 begins pulling in unrelated documents — so a default nobody can move would be
 * wrong for someone.
 */
export function ConvergenceView(
  { semantic, onOpen, onError }:
  { semantic: boolean; onOpen: (id: string) => void; onError: (message: string) => void },
) {
  const [threshold, setThreshold] = useState(0.75);
  const [groups, setGroups] = useState<Convergence[] | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const abort = new AbortController();
    setLoading(true);
    api.convergence(threshold, abort.signal)
      .then((found) => { setGroups(found); setLoading(false); })
      .catch((e: Error) => {
        if (e.name === 'AbortError') return;
        onError(e.message);
        setLoading(false);
      });
    return () => abort.abort();
  }, [threshold, onError]);

  return (
    <section className="convergence">
      <div className="controls">
        <label>
          Similarity ≥ <strong>{threshold.toFixed(2)}</strong>
          <input
            type="range" min={0.5} max={0.95} step={0.01} value={threshold}
            onChange={(e) => setThreshold(Number(e.target.value))}
          />
        </label>
        <p className="hint">
          {semantic
            ? 'Lower it to see weaker overlaps. Worth sweeping rather than trusting one number — the right value depends on the corpus.'
            : 'Without an embedding endpoint this finds copies and restatements only. Two repositories that reached the same conclusion in different words will not appear.'}
        </p>
      </div>

      {loading && <p className="loading">comparing…</p>}
      {!loading && groups?.length === 0 && <p className="empty">Nothing converges above {threshold.toFixed(2)}.</p>}

      {groups?.map((group, index) => (
        <article key={index} className={`group ${group.method.toLowerCase()}`}>
          <header>
            <span className="method">{METHOD[group.method].label}</span>
            <span className="score">{group.similarity.toFixed(3)}</span>
          </header>
          <p className="repos">{group.repositories.join(' ↔ ')}</p>
          <ul>
            {group.entries.map((entry) => (
              <li key={entry.id}>
                <button className="link" onClick={() => onOpen(entry.id)}>{entry.title}</button>
                <span className="where">{entry.repository} · {entry.kind} · {entry.path}</span>
              </li>
            ))}
          </ul>
          {/* The suggestion is a command to run where the file lives — never a button that applies it.
              A candidate is a prompt to look, not a merge, and doctrine that appeared without anyone
              choosing it is the failure this project exists to prevent (D21, D31). */}
          <p className="suggestion">{group.suggestion}</p>
          <p className="method-hint">{METHOD[group.method].hint}</p>
        </article>
      ))}
    </section>
  );
}

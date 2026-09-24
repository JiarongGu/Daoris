import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Convergence } from './api';
import { useConvergence } from './queries';
import { Card, Inline, type Notify, PageHeader, SkeletonRows, useErrorNotify } from './ui';
import { cn } from './lib/cn';
import { useDebounced } from './lib/useDebounced';
import { page } from './results';

/**
 * The knowledge half's lead view (D30). The threshold is a control rather than a constant,
 * deliberately: the useful value depends on the embedder and the corpus — measured on this family,
 * 0.82 returns nothing, 0.70 the true pairs, 0.60 begins pulling in unrelated documents.
 *
 * The suggestion under each group is the SERVICE's sentence, verbatim — a command to run where the
 * file lives, never a button that applies it (D21, D31).
 */
export function ConvergenceView({ semantic, onOpen, notify }: {
  semantic: boolean;
  onOpen: (id: string) => void;
  notify: Notify;
}) {
  const { t } = useTranslation();
  const [threshold, setThreshold] = useState(0.75);
  const debounced = useDebounced(threshold, 200);

  const groups = useConvergence(debounced);
  // The service caps; the client asks for one more than it shows, so "there are more" is a fact.
  const { shown, more } = page(groups.data ?? []);
  useErrorNotify(groups.error, notify);

  return (
    <section>
      <PageHeader title={t('convergence.title')} description={t('convergence.description')} />

      <div className="mb-4 flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2.5 text-body text-ink-soft">
          {t('convergence.threshold')} <strong className="tabular-nums">{threshold.toFixed(2)}</strong>
          <input
            type="range" min={0.5} max={0.95} step={0.01} value={threshold}
            onChange={(e) => setThreshold(Number(e.target.value))}
            className="w-56 accent-accent"
          />
        </label>
        <p className="m-0 basis-full text-body text-ink-soft">
          {semantic ? t('convergence.hintSemantic') : t('convergence.hintLexical')}
        </p>
      </div>

      {/* A first load is skeleton rows (D41 §4), with the words in the line the count takes, so
          nothing moves when the answer lands. On the first real index this was seconds of a bare
          "comparing…" on an otherwise empty page (POLISH3). */}
      {groups.isPending && (
        <>
          <p className="mb-2 text-small text-ink-faint">{t('convergence.comparing')}</p>
          <SkeletonRows rows={4} />
        </>
      )}
      {groups.data?.length === 0 && (
        <p className="text-body text-ink-soft">
          {t('convergence.empty', { value: threshold.toFixed(2) })}
        </p>
      )}

      {/* How many, and whether that is all of them — the same promise the search makes, for the
          same reason: a capped list with nothing saying so reads as the whole answer. */}
      {!groups.isPending && shown.length > 0 && (
        <p className="mb-2 text-small text-ink-faint">
          {more
            ? t('convergence.cappedAt', { count: shown.length })
            : t('convergence.count', { count: shown.length })}
        </p>
      )}

      <div className={cn(groups.isFetching && groups.data && 'opacity-60 transition-opacity duration-(--speed)')}>
        {shown.map((group: Convergence, index: number) => (
          <Card key={index} className="mb-3.5" accent={group.method === 'Convergent'}>
            <header className="flex items-baseline justify-between gap-4">
              <span className="text-body font-semibold">
                {t(`convergence.methods.${group.method}.label`)}
              </span>
              <span className="font-mono text-small tabular-nums text-ink-faint">
                {group.similarity.toFixed(3)}
              </span>
            </header>
            <p className="mb-2 mt-1 text-body text-accent">{group.repositories.join(' ↔ ')}</p>
            <ul className="m-0 list-none p-0">
              {group.entries.map((entry) => (
                <li key={entry.id} className="border-t border-line py-1.5 first:border-t-0">
                  <button
                    className="border-0 bg-transparent p-0 text-left text-body font-medium text-ink underline decoration-line-strong underline-offset-[3px] hover:decoration-accent"
                    onClick={() => onOpen(entry.id)}
                  >
                    {entry.title}
                  </button>
                  <span className="block font-mono text-meta text-ink-faint">
                    {entry.repository} · {t(`kind.${entry.kind}`)} · {entry.path}
                  </span>
                </li>
              ))}
            </ul>
            {/* The service's sentence is the contract and already says what kind of finding this is;
                the italic gloss beneath it said the same thing in the UI's words. Seen twice per
                card on the deployed application, forty cards deep. One sentence — the service's. */}
            <p className="mt-3 rounded-control bg-accent-soft px-3 py-2.5 text-body"><Inline text={group.suggestion} /></p>
          </Card>
        ))}
      </div>
    </section>
  );
}

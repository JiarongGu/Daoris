import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api, type Convergence } from './api';
import { Card, PageHeader, useErrorNotify } from './ui';
import { cn } from './lib/cn';

/**
 * The knowledge half's lead view (D30). The threshold is a control rather than a constant,
 * deliberately: the useful value depends on the embedder and the corpus — measured on this family,
 * 0.82 returns nothing, 0.70 the true pairs, 0.60 begins pulling in unrelated documents.
 *
 * The suggestion under each group is the SERVICE's sentence, verbatim — a command to run where the
 * file lives, never a button that applies it (D21, D31).
 */
export function ConvergenceView({ semantic, onOpen, onError }: {
  semantic: boolean;
  onOpen: (id: string) => void;
  onError: (message: string) => void;
}) {
  const { t } = useTranslation();
  const [threshold, setThreshold] = useState(0.75);
  const [debounced, setDebounced] = useState(threshold);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(threshold), 200);
    return () => clearTimeout(timer);
  }, [threshold]);

  const groups = useQuery({
    queryKey: ['convergence', debounced],
    queryFn: ({ signal }) => api.convergence(debounced, signal),
  });
  useErrorNotify(groups.error, onError);

  return (
    <section>
      <PageHeader title={t('convergence.title')} description={t('convergence.description')} />

      <div className="mb-4 flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2.5 text-[0.85rem] text-ink-soft">
          {t('convergence.threshold')} <strong className="tabular-nums">{threshold.toFixed(2)}</strong>
          <input
            type="range" min={0.5} max={0.95} step={0.01} value={threshold}
            onChange={(e) => setThreshold(Number(e.target.value))}
            className="w-56 accent-accent"
          />
        </label>
        <p className="m-0 basis-full text-[0.875rem] text-ink-soft">
          {semantic ? t('convergence.hintSemantic') : t('convergence.hintLexical')}
        </p>
      </div>

      {groups.isPending && <p className="text-[0.875rem] text-ink-soft">{t('convergence.comparing')}</p>}
      {groups.data?.length === 0 && (
        <p className="text-[0.875rem] text-ink-soft">
          {t('convergence.empty', { value: threshold.toFixed(2) })}
        </p>
      )}

      <div className={cn(groups.isFetching && groups.data && 'opacity-60 transition-opacity duration-(--speed)')}>
        {groups.data?.map((group: Convergence, index: number) => (
          <Card key={index} className="mb-3.5" accent={group.method === 'Convergent'}>
            <header className="flex items-baseline justify-between gap-4">
              <span className="text-[0.9rem] font-semibold">
                {t(`convergence.methods.${group.method}.label`)}
              </span>
              <span className="font-mono text-[0.8rem] tabular-nums text-ink-faint">
                {group.similarity.toFixed(3)}
              </span>
            </header>
            <p className="mb-2 mt-1 text-[0.85rem] text-accent">{group.repositories.join(' ↔ ')}</p>
            <ul className="m-0 list-none p-0">
              {group.entries.map((entry) => (
                <li key={entry.id} className="border-t border-line py-1.5 first:border-t-0">
                  <button
                    className="border-0 bg-transparent p-0 text-left text-[0.95rem] font-medium text-ink underline decoration-line-strong underline-offset-[3px] hover:decoration-accent"
                    onClick={() => onOpen(entry.id)}
                  >
                    {entry.title}
                  </button>
                  <span className="block font-mono text-[0.72rem] text-ink-faint">
                    {entry.repository} · {t(`kind.${entry.kind}`)} · {entry.path}
                  </span>
                </li>
              ))}
            </ul>
            <p className="mt-3 rounded-control bg-accent-soft px-3 py-2.5 text-[0.85rem]">{group.suggestion}</p>
            <p className="mt-1.5 text-[0.78rem] italic text-ink-faint">
              {t(`convergence.methods.${group.method}.hint`)}
            </p>
          </Card>
        ))}
      </div>
    </section>
  );
}

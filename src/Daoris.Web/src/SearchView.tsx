import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from './api';
import { CheckField, PageHeader, useErrorNotify } from './ui';

/**
 * The supporting view. Useful once you know what you are looking for — which is exactly the case
 * convergence cannot help with, and vice versa.
 */
export function SearchView({ onOpen, onError }: {
  onOpen: (id: string) => void;
  onError: (message: string) => void;
}) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [localOnly, setLocalOnly] = useState(true);
  const [debounced, setDebounced] = useState('');

  useEffect(() => {
    // Debounced: every keystroke is an index query, and the early ones are answers to a question the
    // person had not finished asking.
    const timer = setTimeout(() => setDebounced(query.trim()), 250);
    return () => clearTimeout(timer);
  }, [query]);

  const hits = useQuery({
    queryKey: ['search', debounced, localOnly],
    queryFn: ({ signal }) => api.search(debounced, localOnly, signal),
    enabled: debounced.length >= 2,
  });
  useErrorNotify(hits.error, onError);

  return (
    <section>
      <PageHeader title={t('search.title')} description={t('search.description')} />

      <div className="mb-4 flex flex-wrap items-center gap-4">
        <input
          type="search" value={query} autoFocus
          placeholder={t('search.placeholder')}
          onChange={(e) => setQuery(e.target.value)}
          className="min-h-[2.2rem] flex-1 basis-88 rounded-control border border-line bg-raised px-3 py-2 text-[0.95rem] text-ink"
        />
        {/* Local-only by default: canonical content is byte-identical in every adopter, so including
            it returns a dozen copies of one rule and calls that a corpus. */}
        <CheckField checked={localOnly} onChange={setLocalOnly} label={t('search.localOnly')} />
      </div>

      {hits.isFetching && <p className="text-[0.875rem] text-ink-soft">{t('search.searching')}</p>}
      {!hits.isFetching && hits.data?.length === 0 && (
        <p className="max-w-xl text-[0.875rem] text-ink-soft">{t('search.empty')}</p>
      )}

      <ul className="m-0 list-none p-0">
        {hits.data?.map((hit) => (
          <li key={hit.id} className="border-t border-line py-2 first:border-t-0">
            <button
              className="border-0 bg-transparent p-0 text-left text-[0.95rem] font-medium text-ink underline decoration-line-strong underline-offset-[3px] hover:decoration-accent"
              onClick={() => onOpen(hit.id)}
            >
              {hit.title}
            </button>
            <span className="block font-mono text-[0.72rem] text-ink-faint">
              {hit.repository} · {t(`kind.${hit.kind}`)} · {hit.path}
            </span>
            {hit.excerpt && <p className="mt-1 text-[0.85rem] text-ink-soft">{hit.excerpt}</p>}
          </li>
        ))}
      </ul>
    </section>
  );
}

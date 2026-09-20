import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearch } from './queries';
import { CheckField, type Notify, PageHeader, useErrorNotify } from './ui';
import { useDebounced } from './lib/useDebounced';

/**
 * The supporting view. Useful once you know what you are looking for — which is exactly the case
 * convergence cannot help with, and vice versa.
 */
export function SearchView({ onOpen, notify }: {
  onOpen: (id: string) => void;
  notify: Notify;
}) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [localOnly, setLocalOnly] = useState(true);
  const debounced = useDebounced(query, 250).trim();

  const hits = useSearch(debounced, localOnly);
  useErrorNotify(hits.error, notify);

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

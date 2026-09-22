import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearch } from './queries';
import { page } from './results';
import { mark } from './highlight';
import { Button, CheckField, Icon, type Notify, PageHeader, useErrorNotify } from './ui';
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
  // The service caps; the client asks for one more than it shows, so "there are more" is a fact.
  const { shown, more } = page(hits.data ?? []);

  return (
    <section>
      <PageHeader title={t('search.title')} description={t('search.description')} />

      <div className="mb-4 flex flex-wrap items-center gap-4">
        {/* The clearing control is the platform's own. `type="search"` brings Chromium's native one,
            which WebView2 paints in WINDOWS' accent colour — a blue ✕ in a palette that has no blue,
            seen on the deployed application in both themes. The native button is hidden in
            tokens.css; this one is drawn in the field's own language and exists only while there
            is something to clear. */}
        <div className="relative flex-1 basis-88">
          <input
            type="search" value={query} autoFocus
            placeholder={t('search.placeholder')}
            onChange={(e) => setQuery(e.target.value)}
            className="min-h-[2.2rem] w-full rounded-control border border-line-strong bg-raised py-2 pl-3 pr-9 text-body text-ink"
          />
          {query && (
            <Button
              variant="ghost"
              aria-label={t('search.clear')}
              onClick={() => setQuery('')}
              className="absolute right-1 top-1/2 -translate-y-1/2"
            >
              <Icon name="x" size={14} />
            </Button>
          )}
        </div>
        {/* Local-only by default: canonical content is byte-identical in every adopter, so including
            it returns a dozen copies of one rule and calls that a corpus. */}
        <CheckField checked={localOnly} onChange={setLocalOnly} label={t('search.localOnly')} />
      </div>

      {hits.isFetching && <p className="text-body text-ink-soft">{t('search.searching')}</p>}
      {!hits.isFetching && hits.data?.length === 0 && (
        <p className="max-w-xl text-body text-ink-soft">{t('search.empty')}</p>
      )}

      {/* 🔴 How many, and whether that is all of them. A broad query on a real index comes back
          capped by the service, and a truncated list with nothing saying so is the one answer a
          search must never give quietly — "what has the family learned about this" would be
          answered with a number the person had no reason to doubt. */}
      {!hits.isFetching && shown.length > 0 && (
        <p className="mb-1 text-small text-ink-faint">
          {more ? t('search.cappedAt', { count: shown.length }) : t('search.count', { count: shown.length })}
        </p>
      )}

      <ul className="m-0 list-none p-0">
        {shown.map((hit) => (
          <li key={hit.id} className="border-t border-line py-2 first:border-t-0">
            <button
              className="border-0 bg-transparent p-0 text-left text-body font-medium text-ink underline decoration-line-strong underline-offset-[3px] hover:decoration-accent"
              onClick={() => onOpen(hit.id)}
            >
              {hit.title}
            </button>
            <span className="block font-mono text-meta text-ink-faint">
              {hit.repository} · {t(`kind.${hit.kind}`)} · {hit.path}
            </span>
            {/* The matched terms, MARKED. The service centres the excerpt on the first match and
                says why in its own source — a result that cannot show its reasoning gets treated as
                an oracle — and the view was rendering that reasoning as plain text. The wash is
                `accent-soft`, the same token every other "this is the one" surface here uses. */}
            {hit.excerpt && (
              <p className="mt-1 text-body text-ink-soft">
                {mark(hit.excerpt, debounced).map((run, index) => (run.hit
                  ? <mark key={index} className="rounded-[2px] bg-accent-soft px-0.5 text-ink">{run.text}</mark>
                  : <span key={index}>{run.text}</span>))}
              </p>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

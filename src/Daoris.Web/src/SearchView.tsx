import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import { EntryMainNotice, EntryPage } from './knowledge/EntryPage';
import { HitList, type HitsAnswer } from './knowledge/HitList';
import { answeredBy, readingOf, searchFilters } from './knowledge/records';
import { useEntry, useSearch } from './queries';
import { page } from './results';
import { type Notify, useErrorNotify } from './ui';
import { useDebounced } from './lib/useDebounced';
import type { ViewLayout } from './work/ViewFrame';

/**
 * **The Search view** (FRAME1f, D118 §2): what it hands the frame (D118 §5), its list pane and its main area. The list
 * is the box, *each repository's own only*, and the hits; the main area is the entry a hit names, read as it is
 * written, where it was the reader drawer over the side bar and the panel (audit SR4). Useful once you know what you
 * are looking for, which is exactly the case Convergence cannot help with, and the other way round.
 *
 * @remarks
 * **A hook, because a view hands the frame a value** (`ViewLayout`), as Quests' and Repositories' are. The application
 * holds it on every view, so what was typed lasts while Daoris is open; it asks the service nothing until it is in
 * front, and says its errors only then.
 *
 * **What it remembers is its list's** (`listPanes.ts`, §3f): the entry chosen, by its id, and *local only*. What was
 * typed is not kept: it is what the person is typing now.
 */
export function useSearchView({ active, chosen, onChoose, filters: kept, onFilters, notify, semantic, onConverge, handed, onHanded }: {
  /** The view is in front: only then does it ask, and say its errors. */
  active: boolean;
  /** The list's chosen item, an entry's id, which the application remembers (`daoris.list.search.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  /** The list's filters as kept (`daoris.list.search.filters`). */
  filters: Record<string, unknown>;
  onFilters: (filters: Record<string, unknown> | null) => void;
  notify: Notify;
  /** Whether this deployment's recall matches meaning too (D24): the empty answer says which. */
  semantic: boolean;
  /** Where an empty answer points: the view that finds a conclusion reached in other words. */
  onConverge: () => void;
  /**
   * Words another door asked Search for, once per `id`: a right-click's *Search Daoris for it* (CTX1). They replace what
   * the box held, and the holder lets them go once in the box.
   */
  handed?: { text: string; id: number } | null;
  onHanded?: () => void;
}): ViewLayout {
  const { t } = useTranslation();
  const { localOnly } = searchFilters(kept);
  const [query, setQuery] = useState('');
  // From the effect, never the render (frontend-architecture §4b): the holder clears what was taken.
  useEffect(() => {
    if (!handed) return;
    setQuery(handed.text);
    onHanded?.();
  }, [handed?.id]);
  const debounced = useDebounced(query, 250).trim();
  const asked = debounced.length >= 2;

  // The last answer is held while a newer search is on its way (platform language §4), so the list never jumps.
  const hits = useSearch(debounced, localOnly, { enabled: active, holding: true });
  const read = useEntry(chosen, active);
  useErrorNotify(active ? hits.error ?? read.error : null, notify);

  // The service caps; the client asks for one more than it shows, so "there are more" is a fact.
  const { shown, more } = page(hits.data?.hits ?? []);
  // 🔴 The tier that ANSWERED this search (TIER1, D24), not the one configured: with the embedder down, a deployment
  // that matches meaning answered by words and said it had matched meaning.
  const { byMeaning, nothing } = answeredBy(hits.data?.tier ?? null, semantic);
  const answer: HitsAnswer = !asked
    ? { state: 'idle' }
    : hits.data
      ? { state: 'answered', hits: shown, more, byMeaning, nothing, searching: hits.isPlaceholderData }
      : hits.error
        ? { state: 'unanswered', sentence: sentence(hits.error) }
        : { state: 'first' };

  const reading = chosen ? readingOf(read) : null;
  const main = !reading
    ? <EntryMainNotice state="none" />
    : reading.state === 'read'
      ? <EntryPage key={reading.entry.id} entry={reading.entry} />
      : reading.state === 'unanswered'
        ? <EntryMainNotice state="unanswered" sentence={reading.sentence} />
        : <EntryMainNotice state={reading.state} />;

  return {
    list: {
      view: 'search',
      name: t('nav.search'),
      labels: { open: t('search.list.open'), close: t('search.list.close'), resize: t('search.list.resize') },
      // Search makes nothing, so its list has no ＋, and its one filter is beside its box, where it changes what the
      // box finds (D118 §2); its strip holds its controls alone.
      chosen,
      body: (
        <HitList
          query={query}
          onQuery={setQuery}
          localOnly={localOnly}
          // Local only is the default, so only everything chosen is kept as a change.
          onLocalOnly={(next) => onFilters(next ? null : { localOnly: false })}
          answer={answer}
          marked={debounced}
          semantic={semantic}
          chosen={chosen}
          onChoose={onChoose}
          onConverge={onConverge}
        />
      ),
    },
    main,
  };
}

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import { FindingList, type FindingsAnswer } from './knowledge/FindingList';
import { FindingMainNotice, FindingPage } from './knowledge/FindingPage';
import { convergenceFilters, type EntryReading, findingId, readingOf } from './knowledge/records';
import { useConvergence, useEntries } from './queries';
import { page } from './results';
import { type Notify, useErrorNotify } from './ui';
import { useDebounced } from './lib/useDebounced';
import type { ViewLayout } from './work/ViewFrame';

/**
 * **The Convergence view** (D30; FRAME1f, D118 §2): what it hands the frame (D118 §5), its list pane and its main area.
 * The list is the similarity, then the findings; the main area is the finding chosen, the service's sentence and then
 * its entries read whole, where each entry was the reader drawer over the side bar and the panel (audit CO4). The
 * knowledge half's lead view: where two repositories reached the same conclusion independently, to read and decide.
 *
 * @remarks
 * **A hook, because a view hands the frame a value** (`ViewLayout`), as Quests' and Repositories' are. The application
 * holds it on every view, and it asks the service nothing until it is in front: a comparison over a real index takes
 * seconds, and nobody on another view asked for one.
 *
 * **What it remembers is its list's** (`listPanes.ts`, §3f): the finding chosen, by its entries (`findingId`), and the
 * similarity, once it has held still. The suggestion is the SERVICE's sentence, verbatim: a command to run where the
 * file lives, never a button that applies it (D21, D31).
 */
export function useConvergenceView({ active, chosen, onChoose, filters: kept, onFilters, notify, semantic }: {
  /** The view is in front: only then does it ask, and say its errors. */
  active: boolean;
  /** The list's chosen item, a finding's name, which the application remembers (`daoris.list.convergence.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  /** The list's filters as kept (`daoris.list.convergence.filters`). */
  filters: Record<string, unknown>;
  onFilters: (filters: Record<string, unknown> | null) => void;
  notify: Notify;
  /** Whether this deployment matches meaning too (D24), which the note under the similarity says. */
  semantic: boolean;
}): ViewLayout {
  const { t } = useTranslation();
  const remembered = convergenceFilters(kept).threshold;
  const [threshold, setThreshold] = useState(remembered);
  const debounced = useDebounced(threshold, 200);
  // Kept once it has held still, not at every step of a drag; the start is kept as nothing.
  useEffect(() => {
    if (debounced !== remembered) onFilters(debounced === convergenceFilters({}).threshold ? null : { threshold: debounced });
  }, [debounced, remembered, onFilters]);

  // The last findings are held while a moved similarity's are on their way (platform language §4).
  const groups = useConvergence(debounced, { enabled: active, holding: true });
  // The service caps; the client asks for one more than it shows, so "there are more" is a fact.
  const { shown, more } = page(groups.data ?? []);
  const finding = chosen ? (groups.data ?? []).find((group) => findingId(group) === chosen) : undefined;
  const ids = finding ? finding.entries.map((entry) => entry.id) : [];
  const reads = useEntries(ids, active);
  useErrorNotify(active ? groups.error : null, notify);

  const answer: FindingsAnswer = groups.data
    // While held, the findings are the last similarity's, and the list says nothing of `at` until the new ones land.
    ? { state: 'answered', findings: shown, at: debounced, more, comparing: groups.isPlaceholderData }
    : groups.error
      ? { state: 'unanswered', sentence: sentence(groups.error) }
      : { state: 'first' };

  const readings: Record<string, EntryReading> = Object.fromEntries(ids.map((id, index) => [id, readingOf(reads[index] ?? { error: null })]));
  const main = !chosen
    ? <FindingMainNotice state="none" />
    : finding
      ? <FindingPage key={chosen} finding={finding} readings={readings} />
      : groups.data && !groups.isPlaceholderData
        ? <FindingMainNotice state="gone" at={debounced} />
        : groups.error
          ? <FindingMainNotice state="unanswered" sentence={sentence(groups.error)} />
          : <FindingMainNotice state="loading" />;

  return {
    list: {
      view: 'convergence',
      name: t('nav.convergence'),
      labels: { open: t('convergence.list.open'), close: t('convergence.list.close'), resize: t('convergence.list.resize') },
      // Convergence makes nothing, so its list has no ＋; its similarity heads the list, where it changes what the list
      // holds (D118 §2), and its strip holds its controls alone.
      chosen,
      body: (
        <FindingList
          threshold={threshold}
          onThreshold={setThreshold}
          semantic={semantic}
          answer={answer}
          chosen={chosen}
          onChoose={onChoose}
        />
      ),
    },
    main,
  };
}

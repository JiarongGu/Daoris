import { useTranslation } from 'react-i18next';
import type { Convergence } from '../api';
import { Inline, Prose, SkeletonRows } from '../ui';
import { type MainNotice, PageHead, ViewMain } from '../work/ViewMain';
import { EntryPills, entryPlace, EntryText } from './EntryPage';
import { type EntryReading, findingTitle } from './records';

/**
 * **A finding's page** (FRAME1f, D118 §2): Convergence's main area, the finding chosen in its list. Its header is its
 * entries' titles, how alike they are, what kind of likeness, and the repositories that reached it; then **the
 * service's sentence**, verbatim; then **each entry read whole**, as it is written, so the two are read side by side
 * on one page where each was a drawer of its own (audit CO4).
 *
 * @remarks
 * **A molecule**: the finding and each entry's reading arrive, and the page holds nothing.
 *
 * - **The service's sentence is the contract** (D21, D31): a command to run where the file lives, never a button
 *   that applies it, and it already says what kind of finding this is, so no gloss of the UI's sits beside it.
 * - **An entry not read yet is skeleton rows in its place**; one the index no longer holds says so in its place, and
 *   a read that failed says its sentence, so one entry's trouble never blanks the others.
 */
export function FindingPage({ finding, readings }: {
  finding: Convergence;
  /** Each entry's reading, by its id: its text, on its way, gone, or failed. */
  readings: Record<string, EntryReading>;
}) {
  const { t } = useTranslation();
  const head = (
    <PageHead
      title={findingTitle(finding)}
      pills={<span className="font-mono text-small tabular-nums text-ink-faint">{finding.similarity.toFixed(3)}</span>}
      id={finding.repositories.join(' ↔ ')}
      line={t(`convergence.methods.${finding.method}.label`)}
    />
  );

  return (
    <ViewMain header={head}>
      <p className="m-0 max-w-prose rounded-control bg-accent-soft px-3 py-2.5 text-body"><Inline text={finding.suggestion} /></p>
      {finding.entries.map((entry) => {
        const reading = readings[entry.id] ?? { state: 'loading' };
        const read = reading.state === 'read' ? reading.entry : null;
        return (
          <section key={entry.id} aria-label={entry.title} className="mt-6 border-t border-line pt-4">
            <h2 className="m-0 text-title font-semibold wrap-anywhere">{entry.title}</h2>
            <div className="mb-3 mt-1 flex flex-wrap items-baseline gap-x-2 gap-y-1">
              <EntryPills kind={entry.kind} provenance={read?.provenance} />
              <span className="font-mono text-meta text-ink-faint wrap-anywhere">{entryPlace(entry)}</span>
            </div>
            {reading.state === 'read' && <EntryText body={reading.entry.body} />}
            {reading.state === 'loading' && <SkeletonRows rows={4} />}
            {reading.state === 'gone' && <Prose>{t('convergence.page.entryGone')}</Prose>}
            {reading.state === 'unanswered' && <Prose><Inline text={reading.sentence} /></Prose>}
          </section>
        );
      })}
    </ViewMain>
  );
}

/**
 * Convergence's main area with no finding to read (D118 §3b): **nothing chosen** says how to choose; **gone** says the
 * finding chosen is not among the findings at this similarity; **loading** is skeleton rows, never the empty state; and
 * a comparison that never answered says its sentence in place.
 */
export function FindingMainNotice({ state, at, sentence }: {
  state: 'none' | 'gone' | 'loading' | 'unanswered';
  /** The similarity the findings were asked at, which a finding gone no longer reaches. */
  at?: number;
  /** The sentence of a comparison that has never answered. */
  sentence?: string;
}) {
  const { t } = useTranslation();
  if (state === 'unanswered') return <ViewMain><Prose><Inline text={sentence ?? ''} /></Prose></ViewMain>;
  const none: MainNotice = { icon: 'convergence', headline: t('convergence.none.headline'), body: t('convergence.none.body') };
  const gone: MainNotice = {
    icon: 'convergence',
    headline: t('convergence.gone.headline'),
    body: t('convergence.gone.body', { value: (at ?? 0).toFixed(2) }),
  };
  return <ViewMain state={state} none={none} gone={gone} />;
}

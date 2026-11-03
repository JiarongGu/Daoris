import { useTranslation } from 'react-i18next';
import { type KeptReads, useKeptReads } from '../../bridge/shell';
import { Button, Inline, SkeletonRows } from '../../design/ui';
import { sentence } from '../../locales/format';
import { Refused } from '../../work/InlineConfirm';

/**
 * **This machine's Old records** (ONEWAY1ad-ii; the oneway design §1.3): each conversion part's census first (*Workflows: nothing old here*, or the
 * records waiting with the review's door), then every old way Daoris still reads, each said ended or kept until its condition holds, the row
 * that deletes it and the build it waits out, and the home's floor. The driver's words are its own; the page judges nothing.
 *
 * A molecule: the list arrives, and the review's door goes out.
 */
export function OldRecordsView({ read, onReview }: { read: KeptReads; onReview?: () => void }) {
  const { t } = useTranslation();
  const partName = (part: string) => t(`agents.convert.part.${part}`, { defaultValue: part });
  const parts = read.lines.filter((each) => each.kind === 'part');
  const reads = read.lines.filter((each) => each.kind === 'read');
  return (
    <div className="grid min-w-0 gap-3 text-small">
      <p className="m-0 text-ink-soft">{t('agents.setup.oldRecords.lead')}</p>
      {parts.length > 0 && (
        <ul aria-label={t('agents.setup.oldRecords.parts')} className="m-0 grid list-none gap-1 p-0">
          {parts.map((each) => (
            <li key={each.id} className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-ink">
              <span className="[overflow-wrap:anywhere]">
                {each.ended
                  ? t('agents.setup.oldRecords.partEnded', { part: partName(each.id) })
                  : t('agents.setup.oldRecords.partWaits', { part: partName(each.id), count: each.count ?? 0 })}
              </span>
              {!each.ended && onReview && (
                <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={onReview}>{t('agents.setup.oldRecords.review')}</Button>
              )}
            </li>
          ))}
        </ul>
      )}
      <ul aria-label={t('agents.setup.oldRecords.reads')} className="m-0 grid list-none gap-1.5 p-0">
        {reads.map((each) => (
          <li key={each.id} data-ended={each.ended || undefined} className="m-0 grid min-w-0 gap-0.5">
            <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
              <code className="font-mono text-meta text-ink [overflow-wrap:anywhere]">{each.id}</code>
              <span className={each.ended ? 'text-ink-soft' : 'text-ink'}>
                {each.ended ? t('agents.setup.oldRecords.ended') : t('agents.setup.oldRecords.kept')}
              </span>
            </span>
            {!each.ended && (
              <span className="text-ink-soft [overflow-wrap:anywhere]">
                {each.ends && <Inline text={each.ends} />}
                {each.row && ` ${t('agents.setup.oldRecords.row', { row: each.row })}`}
              </span>
            )}
            {!each.ended && each.waits && (
              <span className="text-ink-faint [overflow-wrap:anywhere]">{t('agents.setup.oldRecords.waits')} <Inline text={each.waits} /></span>
            )}
          </li>
        ))}
      </ul>
      <p className="m-0 text-ink-soft [overflow-wrap:anywhere]">
        {read.floor.build ? t('agents.setup.oldRecords.floor', { build: read.floor.build }) : t('agents.setup.oldRecords.noFloor')}
      </p>
      {read.floorNeeded && <p className="m-0 text-ink [overflow-wrap:anywhere]">{t('agents.setup.oldRecords.floorNeeded', { build: read.floorNeeded })}</p>}
    </div>
  );
}

/** The section's organism: the list read where the section is open, and the review's door handed in. */
export function OldRecords({ onReview }: { onReview?: () => void }) {
  const { t } = useTranslation();
  const read = useKeptReads();
  if (read.error) return <Refused sentence={sentence(read.error)} />;
  if (!read.data) return <div className="grid gap-1"><p className="m-0 text-small text-ink-soft">{t('agents.setup.oldRecords.reading')}</p><SkeletonRows rows={3} /></div>;
  return <OldRecordsView read={read.data} onReview={onReview} />;
}

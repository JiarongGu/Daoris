import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { dockKeyOf, rowMark, type StepRow } from './passage';

/**
 * **A run's steps as rows** in the timeline, each one line between the turns it fell between: a step's attempt started, and how it
 * ended, with what failed it. A failed step's press is the dock's: its row leads there and offers none of its own. Under the standing
 * step's start, `acts` draws what its holder journaled (`StepActRows`).
 *
 * Presentational: the rows (`stepRowsOf`, `rowsWithin`), the dock's keys and the presses arrive as props.
 */
export function StepRows({ rows, docked, onToDock, acts }: {
  rows: readonly StepRow[];
  /** The keys of the dock's lines, so a failed step's row leads to its own. */
  docked: readonly string[];
  onToDock: (key: string) => void;
  /** What a step's holder journaled, drawn under its newest start; absent, none is drawn. */
  acts?: (row: StepRow) => ReactNode;
}) {
  const { t } = useTranslation();
  if (rows.length === 0) return null;
  const words = (row: StepRow) => [
    t(`work.track.row.${row.edge === 'start' ? 'start' : row.mark}`, { node: row.node }),
    row.attempt ? t('work.track.row.attempt', { attempt: row.attempt }) : null,
    row.edge === 'end' ? row.code : null,
  ].filter(Boolean).join(' · ');
  return (
    <ol aria-label={t('work.track.rows')} className="m-0 grid list-none gap-1 p-0">
      {rows.map((row) => {
        const key = dockKeyOf(row.run, row.node);
        return (
          <li key={row.key} data-step-row={rowMark(row.run, row.node)} data-mark={row.mark} className="m-0 grid min-w-0 gap-1">
            <p className="m-0 flex min-w-0 items-center gap-2 text-small text-ink-soft">
              <span aria-hidden="true" className="h-px w-4 shrink-0 bg-line-strong" />
              <span className={cn('min-w-0 truncate', row.mark === 'failed' && 'text-ink')}>{words(row)}</span>
              {row.edge === 'end' && row.mark === 'failed' && docked.includes(key) && (
                <button
                  type="button"
                  onClick={() => onToDock(key)}
                  className="shrink-0 cursor-pointer rounded-control border-0 bg-transparent p-0 text-small text-ink-open underline-offset-2 hover:underline"
                >
                  {t('work.track.row.toDock')}
                </button>
              )}
              <span aria-hidden="true" className="h-px min-w-4 flex-1 bg-line-strong" />
            </p>
            {row.edge === 'start' && acts?.(row)}
          </li>
        );
      })}
    </ol>
  );
}

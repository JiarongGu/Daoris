import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ReviewOperation, ReviewRow } from './runReviewWire';
import { CheckField, Icon, Inline, PathText } from '../../../design/ui';
import { cn } from '../../../lib/cn';
import { contentOf } from './viewedMarks';

/** The reader's marks, asked and pressed by a file's path and content. */
export type ViewedMarks = { isViewed: (path: string, content: string) => boolean; mark: (path: string, content: string, viewed: boolean) => void };

const short = (hash: string | null | undefined, none: string) => (hash ? hash.slice(0, 7) : none);

/**
 * **A folder's or a copy's files, as the record keeps them**: one row each, its change, its versions before and after by their hashes,
 * the marks the record says of it, and *Viewed*, kept by its content; a row opens in place on what `body` draws of it.
 *
 * A molecule: the rows, the marks and the body arrive as props.
 */
export function RecordRows({ rows, viewed, body }: { rows: ReviewRow[]; viewed: ViewedMarks; body?: (row: ReviewRow) => ReactNode }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const done = rows.filter((row) => viewed.isViewed(row.path, contentOf(row))).length;
  return (
    <section aria-label={t('work.runReview.changes')}>
      <header className="flex flex-wrap items-baseline gap-x-2 border-b border-line px-3 py-1.5">
        <span className="text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.runReview.files', { count: rows.length })}</span>
        {done > 0 && <span className="text-meta tabular-nums text-ink-faint">{t('work.runReview.viewedCount', { done, total: rows.length })}</span>}
      </header>
      <ul className="m-0 list-none p-0">
        {rows.map((row) => {
          const content = contentOf(row);
          const seen = viewed.isViewed(row.path, content);
          return (
            <li key={row.path} className={cn('border-b border-line last:border-b-0', seen && 'opacity-55')}>
              <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5 px-3 py-1">
                <button
                  type="button"
                  aria-expanded={open[row.path] ?? false}
                  disabled={!body}
                  onClick={() => setOpen((was) => ({ ...was, [row.path]: !was[row.path] }))}
                  className="flex min-w-0 flex-1 cursor-pointer items-center gap-2 border-0 bg-transparent p-0 text-left text-ink disabled:cursor-default"
                >
                  {body && <Icon name={open[row.path] ? 'chevronDown' : 'chevronRight'} size={12} />}
                  <PathText path={row.path} className="min-w-0 truncate text-small" />
                  <span className="shrink-0 text-meta text-ink-faint">{t(`work.runReview.change.${row.change}`, { defaultValue: row.change })}</span>
                </button>
                <span className="font-mono text-meta text-ink-faint">
                  {t('work.runReview.versions', { before: short(row.before, t('work.runReview.versionNone')), after: short(row.after, t('work.runReview.versionNone')) })}
                </span>
                <CheckField
                  className="text-meta"
                  label={t('work.runReview.viewed')}
                  checked={seen}
                  onChange={(next) => viewed.mark(row.path, content, next)}
                />
              </div>
              {row.marks.length > 0 && (
                <p className="m-0 px-3 pb-1 text-meta text-warn">
                  {row.marks.map((mark) => t(`work.runReview.mark.${mark}`, { defaultValue: mark })).join(' · ')}
                </p>
              )}
              {open[row.path] && body?.(row)}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

/**
 * **A plan's operations**: one row per resource, its operation in the person's words, the system's name for it, and its value before
 * and after. Values are the system's own and drawn as written.
 */
export function PlanOperations({ system, operations }: { system: string; operations: ReviewOperation[] | null | undefined }) {
  const { t } = useTranslation();
  return (
    <section aria-label={t('work.runReview.plan', { system })} className="px-3 py-2">
      <h3 className="m-0 mb-1.5 text-small font-semibold"><Inline text={t('work.runReview.plan', { system })} /></h3>
      {operations && operations.length > 0 ? (
        <ul className="m-0 grid list-none gap-1 p-0">
          {operations.map((operation) => (
            <li key={operation.resource} className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-2 text-small">
              <span className="text-ink-soft">{t(`work.runReview.op.${operation.op}`, { defaultValue: operation.op })}</span>
              <span className="min-w-0 font-mono text-ink">{operation.resource}</span>
              {(operation.before != null || operation.after != null) && (
                <span className="col-start-2 font-mono text-meta text-ink-faint">
                  {operation.before ?? '∅'} → {operation.after ?? '∅'}
                </span>
              )}
            </li>
          ))}
        </ul>
      ) : <p className="m-0 text-small text-ink-faint">{t('work.runReview.planUnread')}</p>}
    </section>
  );
}

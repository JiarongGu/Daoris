import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../ui';
import { cn } from '../lib/cn';

/** One fact a record's *Details* holds: its name, its value (absent values are left out), and whether it is copied. */
export type DetailRow = {
  label: string;
  value: ReactNode | null | undefined;
  /** Set in the mono face: an id, a path's name. */
  mono?: boolean;
  /** The text a press copies, beside the value: an id is for copying (D152 §7). */
  copy?: string;
};

/**
 * **A record's *Details*** (UX7c, D152 §7, the UX7 design §5.2–§5.3): what its head leaves out, folded to one line that
 * names a few of them, and open as a list of facts, each named. A session's id, its agent and its clock; a quest's id,
 * its full times and its lanes. An id is its one copyable row.
 *
 * @remarks
 * **Folded by default, for every viewer**: a head says the facts that matter, and these are the ones that are looked up,
 * which is the same reason the trace folds (TRACE1b). A molecule: its one state is whether it is open, and whether it
 * just copied.
 */
export function DetailsFold({ summary, rows, className }: {
  /** The folded line: a few of the facts, joined, so the fold says what it holds. */
  summary: string;
  rows: readonly DetailRow[];
  className?: string;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState<string | null>(null);
  const shown = rows.filter((row) => row.value !== null && row.value !== undefined && row.value !== '');

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(text);
      setTimeout(() => setCopied(null), 1500);
    } catch {
      // No clipboard here: the value is on the page to select by hand.
    }
  };

  return (
    <section aria-label={t('work.head.details')} className={cn('min-w-0', className)}>
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
        <span className="min-w-0 truncate">
          <span className="font-semibold">{t('work.head.details')}</span>
          {summary && <span className="ml-2">{summary}</span>}
        </span>
      </button>
      {open && (
        <dl className="m-0 mt-2 grid min-w-0 grid-cols-[max-content_1fr] gap-x-4 gap-y-1 pl-[18px] text-small">
          {shown.map((row) => (
            <div key={row.label} className="contents">
              <dt className="text-ink-faint">{row.label}</dt>
              <dd className={cn('m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 text-ink-soft', row.mono && 'font-mono text-meta')}>
                <span className="min-w-0 [overflow-wrap:anywhere]">{row.value}</span>
                {row.copy && (
                  <button
                    type="button"
                    onClick={() => void copy(row.copy!)}
                    className="cursor-pointer border-0 bg-transparent p-0 font-sans text-small text-accent hover:underline"
                  >
                    {copied === row.copy ? t('work.head.copied') : t('work.head.copyId')}
                  </button>
                )}
              </dd>
            </div>
          ))}
        </dl>
      )}
    </section>
  );
}

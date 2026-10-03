import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { figure, list } from '../format';
import { cn } from '../lib/cn';
import { CodeText, Icon, Inline } from '../ui';
import type { InstructionAccount } from './conversation';
import { type HandedLine, type HandedRow, handedView } from './handed';

/**
 * What a driven session was handed, section by section (CONTEXT1, D143 point 1): the account the driver kept beside the
 * instruction it composed, under that instruction on the session's page. Not the words again, which the instruction above
 * holds: each section's size, where it came from, and what its bound left out and why; then what was handed beside it (the
 * permission rules) and what could have been handed and was not.
 *
 * @remarks
 * - **Folded to one line**: the instruction's size, its sections and how many had a part left out. A press opens the table.
 * - **Worded from codes, in the reader's language** (LANG1a's rule): a code this page does not know, or a value its entry
 *   needs that the record lacks, shows the driver's own English, marked *shown as recorded*.
 * - **A target handed before the account was kept says so** (D143 point 3), rather than showing nothing.
 * - **No measure of its own** (D141): the table takes the card's width, and its last column wraps.
 *
 * A molecule: handed the account, it reads the catalogue and nothing else.
 */
export function HandedAccount({ account, defaultOpen = false }: {
  /** The account the target's event keeps, or null for one handed before the driver kept one. */
  account?: InstructionAccount | null;
  /** Whether the table starts open: a story's or a test's. The person's press opens it on the page. */
  defaultOpen?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);

  if (!account) {
    return <p className="m-0 mt-1.5 border-t border-line pt-1.5 text-meta text-ink-faint">{t('work.handed.notKept')}</p>;
  }

  const view = handedView(t, (key) => i18n.exists(key), account, i18n.language);
  const summary = [
    t('work.handed.summary', { count: view.sections, chars: figure(view.chars) }),
    ...(view.cut > 0 ? [t('work.handed.cuts', { count: view.cut })] : []),
  ].join(' · ');

  return (
    <section aria-label={t('work.handed.label')} className="mt-1.5 border-t border-line pt-1.5">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-meta text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-0.5 shrink-0" />
        <span className="min-w-0">
          <span className="text-ink-soft">{t('work.handed.label')}</span>
          {` · ${summary}`}
        </span>
      </button>
      {open && (
        <>
          <table className="mt-1.5 w-full border-collapse text-small">
            <thead>
              <tr className="text-meta text-ink-faint">
                <th scope="col" className="pb-0.5 pr-3 text-left font-normal">{t('work.handed.column.section')}</th>
                <th scope="col" className="pb-0.5 pr-3 text-right font-normal">{t('work.handed.column.size')}</th>
                <th scope="col" className="pb-0.5 text-left font-normal">{t('work.handed.column.source')}</th>
              </tr>
            </thead>
            <tbody>
              {view.rows.map((row) => (
                <Row key={row.key} row={row} />
              ))}
            </tbody>
          </table>
          {view.absent.length > 0 && (
            <p className="m-0 mt-1.5 text-meta text-ink-faint">
              {view.absent.some((line) => line.recorded)
                ? view.absent.map((line, index) => (
                  <span key={index} className="mr-2 inline">
                    {line.recorded ? <Said line={line} /> : line.text}
                  </span>
                ))
                : t('work.handed.notHanded', { items: list(view.absent.map((line) => line.text), i18n.language) })}
            </p>
          )}
        </>
      )}
    </section>
  );
}

/** One section's row, and beneath it a line for each part its bound left out. */
function Row({ row }: { row: HandedRow }) {
  const { t } = useTranslation();
  if (row.name.recorded) {
    return (
      <tr className="border-t border-line align-baseline">
        <td colSpan={3} className="py-1 text-ink-soft"><Said line={row.name} /></td>
      </tr>
    );
  }

  return (
    <>
      <tr className="border-t border-line align-baseline">
        <th scope="row" className="py-1 pr-3 text-left font-normal text-ink">
          {row.name.text}
          {row.beside && (
            <span className="ml-1.5 inline-block rounded-control border border-line px-1 text-meta leading-[1.4] text-ink-faint">
              {t('work.handed.beside')}
            </span>
          )}
        </th>
        {/* A character count is a figure set in the column's mono; the rules handed beside it are counted in words. */}
        <td className={cn('whitespace-nowrap py-1 pr-3 text-right text-meta text-ink-soft', !row.beside && 'font-mono tabular-nums')}>
          {row.size ?? '—'}
        </td>
        <td className="py-1 text-ink-faint wrap-anywhere">
          {row.source}
          {row.from && <>{' · '}<CodeText text={row.from} className="text-ink-soft" /></>}
          {row.facts.map((fact) => <span key={fact}>{` · ${fact}`}</span>)}
        </td>
      </tr>
      {row.cuts.map((cut, index) => (
        <tr key={index} className="align-baseline">
          <td colSpan={3} className="pb-1">
            <span className="ml-3 block border-l-2 border-line-strong pl-2 text-meta text-ink-soft">
              {cut.recorded ? <Said line={cut} /> : cut.text}
            </span>
          </td>
        </tr>
      ))}
    </>
  );
}

/** The driver's own English where the page could not word a code, marked as the note's are (D142 points 4, 5). */
function Said({ line }: { line: HandedLine }) {
  const { t } = useTranslation();
  return (
    <>
      <span className="mr-1.5 inline-block rounded-control border border-line px-1 align-baseline text-meta leading-[1.4] text-ink-faint">
        {t('work.handed.recorded')}
      </span>
      <Inline text={line.text} />
    </>
  );
}

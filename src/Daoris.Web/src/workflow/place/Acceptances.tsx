import type { ReactNode } from 'react';
import { CheckField, Inline } from '../../design/ui';

/** One acceptance of a declaration: its words, its standing and consequence, and whether the person can tick it now. */
export type AcceptanceRow = {
  /** What a tick is keyed by: the purpose, with the repository, line and blob it was read at. */
  id: string;
  label: string;
  /** What the acceptance is for, where the caller says it apart from the standing. */
  why?: string;
  /** Its standing and consequence, in the driver's or the page's words. */
  said: string;
  /** Whether it waits for the person: a tick is drawn only for what does. */
  offered: boolean;
  /** The declaration's text it is of, shown once however many acceptances share it. */
  text?: string | null;
  /** What names the text: the line and commit it was read at. */
  textLabel?: string;
  /** Presses beside the standing (a withdrawal). */
  extra?: ReactNode;
};

/**
 * **Acceptances** (SETUPUX1k, the setup-UX design §2.2; Gates draws it too): the declaration at a line's tip shown once, and each purpose's
 * acceptance as its own tick, with the standing it leaves. A tick starts empty and is the person's alone: `ticked` is the ids chosen, and
 * `cleared` the reasons for ticks the caller dropped because the text read changed since.
 *
 * @remarks
 * A molecule: it keeps no tick and judges nothing; the caller binds each id to what was read and says what changed.
 */
export function Acceptances({ rows, ticked, onTick, disabled = false, title, fold, cleared = [] }: {
  rows: readonly AcceptanceRow[];
  ticked: readonly string[];
  onTick: (id: string, on: boolean) => void;
  disabled?: boolean;
  /** A line over the rows, naming what they are of (a repository and the blob). */
  title?: string;
  /** The words that fold the declaration's text under a line; without them the text is drawn open. */
  fold?: string;
  cleared?: readonly string[];
}) {
  const texts = [...new Map(rows.filter((row) => row.offered && row.text).map((row) => [row.text!, row.textLabel ?? ''])).entries()];
  return (
    <div className="grid min-w-0 gap-2">
      {title && <p className="m-0 text-small font-medium text-ink [overflow-wrap:anywhere]">{title}</p>}
      {texts.map(([text, label]) => {
        const pre = (
          <pre aria-label={label} className="m-0 max-h-48 overflow-auto rounded-control border border-line bg-raised px-2 py-1 font-mono text-meta text-ink">{text}</pre>
        );
        return fold
          ? (
            <details key={text} className="m-0 min-w-0 text-small">
              <summary className="cursor-pointer text-ink-soft">{fold}</summary>
              <div className="mt-1">{pre}</div>
            </details>
          )
          : <div key={text}>{pre}</div>;
      })}
      <ul className="m-0 grid list-none gap-2 p-0">
        {rows.map((row) => (
          <li key={row.id} data-acceptance={row.id} className="grid min-w-0 gap-0.5 rounded-control border border-line px-2.5 py-1.5">
            {row.offered
              ? <CheckField checked={ticked.includes(row.id)} onChange={(on) => onTick(row.id, on)} label={row.label} disabled={disabled} className="whitespace-normal font-medium text-ink" />
              : <span className="text-body font-medium text-ink">{row.label}</span>}
            {row.why && <span className="text-small text-ink-soft"><Inline text={row.why} /></span>}
            <span className="text-small text-ink [overflow-wrap:anywhere]"><Inline text={row.said} /></span>
            {row.extra}
          </li>
        ))}
      </ul>
      {cleared.length > 0 && (
        <div role="status" className="grid gap-0.5 border-l-[3px] border-warn pl-2.5 text-small text-ink-soft">
          {cleared.map((each) => <p key={each} className="m-0 [overflow-wrap:anywhere]">{each}</p>)}
        </div>
      )}
    </div>
  );
}

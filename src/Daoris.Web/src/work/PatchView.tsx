import { useMemo } from 'react';
import { cn } from '../lib/cn';
import { highlightLines, languageOf } from './codeLines';
import { type PatchLine, pairRows, parsePatch } from './patch';
import './code.css';

/** How a patch is laid out: one column of lines, or the old side beside the new. */
export type DiffLayout = 'unified' | 'split';

/**
 * A file's patch drawn (REVIEW2): git's own lines, numbered on the side each belongs to, highlighted
 * in the file's language, and unified or side by side.
 *
 * @remarks
 * **Reading, never re-deriving**: the lines, their order and their numbers are git's (`patch.ts`).
 *
 * **Highlighted a side at a time.** A hunk's old side (unchanged and removed lines) and its new side
 * (unchanged and added) are each highlighted as one text, so a comment or a string that spans lines is
 * coloured on each (`codeLines.ts`). A hunk begins mid-file, so a comment opened above it is not
 * known to be one — the highlighter cannot see what the patch does not carry. A language it does not
 * know is shown plain, never guessed.
 *
 * **Colour is never the only mark** (D41 §6): a changed line carries its sign, in a column of its own.
 */
export function PatchView({ patch, path, layout }: { patch: string; path: string; layout: DiffLayout }) {
  const parsed = useMemo(() => parsePatch(patch), [patch]);
  const language = languageOf(path);

  // A patch with no hunks is a rename or a mode change: what git said is the whole of it.
  if (parsed.hunks.length === 0) {
    return (
      <div className="px-3 py-2 font-mono text-meta leading-[1.5] text-ink-faint">
        {parsed.preamble.map((line, index) => <div key={index}>{line}</div>)}
      </div>
    );
  }

  return (
    <div className="font-mono text-meta leading-[1.5]">
      {parsed.hunks.map((hunk, index) => (
        <section key={index}>
          <div className="border-y border-line bg-page px-3 py-0.5 text-ink-faint">{hunk.header}</div>
          <DiffLines lines={hunk.lines} language={language} layout={layout} />
        </section>
      ))}
    </div>
  );
}

const TONE = {
  add: { row: 'bg-st-done/10', sign: 'text-ink-done', mark: '+' },
  del: { row: 'bg-st-declined/10', sign: 'text-ink-danger', mark: '−' },
  same: { row: '', sign: 'text-ink-faint', mark: ' ' },
} as const;

const NUMBER = 'w-[1%] select-none whitespace-nowrap px-1.5 text-right align-top text-ink-faint';
const CODE = 'whitespace-pre px-2 align-top text-ink-soft';
// Side by side, each half is held at half and its lines wrap: a long line on one side pushed the
// other out of view (seen on the window). A unified line stays whole, and scrolls.
const WRAPPED = 'whitespace-pre-wrap px-2 align-top text-ink-soft [overflow-wrap:anywhere]';

/**
 * Lines of a diff as rows — a hunk's in a review, or an edit's in a conversation's tool card, which
 * numbers nothing because its lines are not the file's (`numbered`).
 */
export function DiffLines({ lines, language, layout = 'unified', numbered = true }: {
  lines: readonly PatchLine[];
  language: string | null;
  layout?: DiffLayout;
  numbered?: boolean;
}) {
  const html = useMemo(() => {
    // Each side highlighted whole; an unchanged line takes its new side's markup, which is the same.
    const oldSide = lines.filter((line) => line.kind === 'same' || line.kind === 'del');
    const newSide = lines.filter((line) => line.kind === 'same' || line.kind === 'add');
    const byLine = new Map<PatchLine, string>();
    highlightLines(oldSide.map((line) => line.text), language).forEach((markup, i) => byLine.set(oldSide[i]!, markup));
    highlightLines(newSide.map((line) => line.text), language).forEach((markup, i) => byLine.set(newSide[i]!, markup));
    return byLine;
  }, [lines, language]);

  const code = (line: PatchLine | undefined) => (
    <td className={CODE}>
      {/* The highlighter's own escaped markup, or the escaped text — never the file's text as markup. */}
      {line && <code className="hljs" dangerouslySetInnerHTML={{ __html: html.get(line) ?? '' }} />}
    </td>
  );
  const note = (line: PatchLine, span: number, key: number) => (
    <tr key={key}>
      <td colSpan={span} className="px-3 italic text-ink-faint">{line.text}</td>
    </tr>
  );

  if (layout === 'split') {
    return (
      <table className="w-full table-fixed border-collapse">
        <colgroup>
          <col className="w-11" />
          <col />
          <col className="w-11" />
          <col />
        </colgroup>
        <tbody>
          {pairRows(lines).map((row, index) => {
            if ('note' in row) return note(row.note, 4, index);
            const { left, right } = row;
            return (
              <tr key={index}>
                <td className={cn(NUMBER, left && TONE[left.kind].row)}>{numbered && left ? left.old : ''}</td>
                <td className={cn(WRAPPED, left ? TONE[left.kind].row : 'bg-page', 'border-r border-line')}>
                  {left && <code className="hljs" dangerouslySetInnerHTML={{ __html: html.get(left) ?? '' }} />}
                </td>
                <td className={cn(NUMBER, right && TONE[right.kind].row)}>{numbered && right ? right.new : ''}</td>
                <td className={cn(WRAPPED, right ? TONE[right.kind].row : 'bg-page')}>
                  {right && <code className="hljs" dangerouslySetInnerHTML={{ __html: html.get(right) ?? '' }} />}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse">
        <tbody>
          {lines.map((line, index) => {
            if (line.kind === 'note') return note(line, numbered ? 4 : 2, index);
            const tone = TONE[line.kind];
            return (
              <tr key={index} data-kind={line.kind} className={tone.row}>
                {numbered && <td className={NUMBER}>{line.kind === 'add' ? '' : line.old}</td>}
                {numbered && <td className={NUMBER}>{line.kind === 'del' ? '' : line.new}</td>}
                <td className={cn('w-[1%] select-none pl-1.5', tone.sign)}>{tone.mark}</td>
                {code(line)}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

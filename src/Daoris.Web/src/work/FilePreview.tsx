import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { size } from '../format';
import { cn } from '../lib/cn';
import { Button, Icon, Inline, Segmented, SkeletonRows, Tip } from '../ui';
import { highlightLines, languageOf } from './codeLines';
import { PatchView } from './PatchView';
import { fileLines, fileName, type LineRange, type TreeFile } from './preview';
import './code.css';

/** What the preview shows: the file as it is now, or the review's patch for it. */
type Showing = 'file' | 'changes';

const NUMBER = 'w-[1%] select-none whitespace-nowrap border-l-2 px-1.5 text-right align-top';

/**
 * One file, read-only, in the side bar (PREVIEW1, D111): what a tool card or the review named, read
 * without leaving the window.
 *
 * @remarks
 * **A preview, never an editor** (D55): no caret, no save, nothing that writes. The file is the disk's,
 * as it is now, named relative to its tree — never the machine's path (platform language §4).
 *
 * **Line-numbered and highlighted in its language**, where the highlighter ships it (REVIEW2), a whole
 * file at once so what spans lines is coloured on each. A language it does not know is shown plain.
 *
 * **The lines a tool call named are marked and scrolled to**, where the card knew them: a bar beside
 * their numbers and a wash across them, and a sentence saying which — never the hue alone (D41 §6).
 *
 * **Every other state is a sentence**: reading, the host's refusal as it said it, a binary file with its
 * size, an empty file, and the bound — how much the file holds and that the disk has the rest.
 *
 * **The file's changes are the review's own patch**, where the review holds one: one press away, drawn
 * by `PatchView`, and never a second diff (D111).
 *
 * A molecule: the file arrives as props, and the one state it owns is which of the two it shows.
 */
export function FilePreview({ path, file, pending = false, refusal, lines, patch, onReload }: {
  /** Relative to the tree, as the door asked for it: named while it is read, and after a refusal. */
  path: string;
  /** The host's answer, once it has one. */
  file?: TreeFile | null;
  pending?: boolean;
  /** The host's sentence for why it did not read the file, verbatim. */
  refusal?: string | null;
  /** The lines the tool call named, where it knew them. */
  lines?: LineRange | null;
  /** The review's patch for this file, where the review holds one. */
  patch?: string | null;
  onReload?: () => void;
}) {
  const { t } = useTranslation();
  const [showing, setShowing] = useState<Showing>('file');
  const shown: Showing = patch ? showing : 'file';

  const text = file && !file.binary ? file.text ?? '' : null;
  const rows = useMemo(() => (text === null ? [] : fileLines(text)), [text]);
  const html = useMemo(() => highlightLines(rows, languageOf(path)), [rows, path]);
  const marked = (line: number) => Boolean(lines && line >= lines.from && line <= lines.to);

  // The named lines are what the person came for: brought into view whenever the file or the range changes.
  const first = useRef<HTMLTableRowElement>(null);
  useEffect(() => {
    if (shown === 'file') first.current?.scrollIntoView?.({ block: 'center' });
  }, [lines?.from, lines?.to, text, shown]);

  const measure = file && !file.binary && text !== null
    ? `${size(file.size)} · ${t('work.preview.lines', { count: rows.length })}`
    : null;
  const named = lines && rows.length >= lines.from
    ? (lines.to > lines.from
      ? t('work.preview.named', { from: lines.from, to: lines.to })
      : t('work.preview.namedOne', { from: lines.from }))
    : null;

  let body;
  if (pending) {
    body = (
      <div className="px-3 py-2">
        <p role="status" className="m-0 text-meta text-ink-faint">{t('work.preview.reading')}</p>
        <SkeletonRows rows={4} />
      </div>
    );
  } else if (refusal) {
    body = <p className="m-0 px-3 py-4 text-small text-ink-soft"><Inline text={refusal} /></p>;
  } else if (!file) {
    body = null;
  } else if (file.binary) {
    body = (
      <p className="m-0 px-3 py-4 text-small text-ink-soft">
        {t('work.preview.binary', { name: fileName(file.path), size: size(file.size) })}
      </p>
    );
  } else if (shown === 'changes' && patch) {
    body = <PatchView patch={patch} path={path} layout="unified" />;
  } else if (rows.length === 0) {
    body = <p className="m-0 px-3 py-4 text-small text-ink-faint">{t('work.preview.empty')}</p>;
  } else {
    body = (
      <table className="w-full border-collapse font-mono text-meta leading-[1.5]">
        <tbody>
          {rows.map((_, index) => {
            const line = index + 1;
            const mark = marked(line);
            return (
              <tr
                key={line}
                ref={lines && line === lines.from ? first : undefined}
                data-marked={mark}
                className={cn(mark && 'bg-accent/10')}
              >
                <td className={cn(NUMBER, mark ? 'border-l-accent text-accent' : 'border-l-transparent text-ink-faint')}>{line}</td>
                <td className="whitespace-pre px-2 align-top text-ink-soft">
                  {/* The highlighter's own escaped markup — never the file's text as markup. */}
                  <code className="hljs" dangerouslySetInnerHTML={{ __html: html[index] ?? '' }} />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    );
  }

  return (
    <section aria-label={t('work.preview.label', { path })} className="flex min-h-0 flex-1 flex-col">
      <header className="flex shrink-0 items-center gap-1 border-b border-line py-1 pl-3 pr-1">
        {/* The path is the identity, so it gives way at its FRONT: the name is what the person looks for. */}
        <span dir="rtl" className="min-w-0 flex-1 truncate text-left font-mono text-small text-ink">
          <span dir="ltr">{path}</span>
        </span>
        {onReload && (
          <Tip content={t('work.preview.reload')}>
            <Button
              variant="ghost"
              aria-label={t('work.preview.reload')}
              onClick={onReload}
              className="h-6 w-6 shrink-0 justify-center px-0"
            >
              <Icon name="refresh" size={13} />
            </Button>
          </Tip>
        )}
      </header>

      {(measure || named || patch) && (
        // Wraps rather than squeezing: at the side bar's floor the switch goes under what it follows.
        <div className="flex shrink-0 flex-wrap items-center gap-x-2 gap-y-1 border-b border-line px-3 py-1">
          {measure && <span className="text-meta tabular-nums text-ink-faint">{measure}</span>}
          {named && shown === 'file' && <span className="text-meta text-accent">{named}</span>}
          {patch && file && !file.binary && (
            <span className="ml-auto">
              <Segmented
                label={t('work.preview.show')}
                value={shown}
                options={[
                  { value: 'file', label: t('work.preview.file') },
                  { value: 'changes', label: t('work.preview.changes') },
                ]}
                onChange={setShowing}
              />
            </span>
          )}
        </div>
      )}

      {/* Once a landing tidied the tree away, the file is the landed branch's copy (REVIEW2, D113), said in words. */}
      {file?.branch && !pending && !refusal && (
        <p className="m-0 shrink-0 border-b border-line px-3 py-1 text-meta text-ink-faint [overflow-wrap:anywhere]">
          <Inline text={t('work.preview.fromBranch', { branch: file.branch })} />
        </p>
      )}

      <div className="min-h-0 flex-1 overflow-auto">{body}</div>

      {/* The bound is stated, never hidden (design §5) — and where the rest is: the disk, or the landed branch. */}
      {file?.truncated && !file.binary && shown === 'file' && (
        <p className="m-0 shrink-0 border-t border-line px-3 py-2 text-meta text-ink-faint">
          {t(file.branch ? 'work.preview.truncatedBranch' : 'work.preview.truncated', { size: size(file.size), count: rows.length })}
        </p>
      )}
    </section>
  );
}

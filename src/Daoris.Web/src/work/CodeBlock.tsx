import { useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import hljs from 'highlight.js/lib/common';
import { cn } from '../lib/cn';
import { useWidth } from '../map/useWidth';
import { Icon } from '../ui';
import './code.css';

/** Text as HTML with nothing interpreted — what a block in no known language shows. */
const escape = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/**
 * Code as HTML: highlighted when its language is one the highlighter knows, escaped otherwise.
 *
 * @remarks
 * **Never guessed.** Auto-detection is slow on a long block and wrong often enough to colour prose
 * as code, so a block that names no language, or one the highlighter lacks, is shown plain. The
 * highlighter escapes what it reads, so neither path can put the agent's text into the page as markup.
 */
export function highlighted(code: string, language?: string | null): string {
  if (language && hljs.getLanguage(language)) {
    return hljs.highlight(code, { language, ignoreIllegals: true }).value;
  }
  return escape(code);
}

/**
 * Below this width of its own pane a block's long lines wrap until the person says otherwise (ASKHIST1c): the container
 * query's number, written whole in each class that reads it. 48rem holds a hundred characters of the code face, so a
 * conversation in Ask Daoris's dock (300 px to about 700) wraps, and one in Sessions' centre at a wide window scrolls as
 * before; Sessions' centre narrowed by an open side bar wraps by the same rule.
 */
export const WRAP_BELOW = '48rem';
const WRAP_BELOW_REM = 48;

/** The page's rem in pixels, which a container query's `rem` reads. */
const remPx = () =>
  (typeof document === 'undefined' ? 16 : parseFloat(getComputedStyle(document.documentElement).fontSize)) || 16;

/**
 * One code block in a conversation (D76, CONV2): its language named, highlighted in the paper's own
 * inks (`code.css`), and a copy button — the reference console's shape.
 *
 * @remarks
 * **The colours are D41's tokens**, not an imported theme: a keyword wears the accent, a string and a
 * number the two cool status hues, a comment the faint ink. So it follows the chosen theme with no
 * second palette to keep in step.
 *
 * **Its long lines wrap where its pane is narrow** (ASKHIST1c): in Ask Daoris's dock a line of a hundred characters scrolled
 * sideways, cut off at the edge. *Wrap* beside *Copy* says which it is and switches it; until pressed, the block's own width
 * decides (`WRAP_BELOW`), by a container query on the block before it is measured and by its measure after, so the press
 * and the lines agree. Never the window's width: the dock is narrow on a wide window. A table is not code and keeps its own
 * scrolling box (`Markdown`). Copy takes the original text, whatever is shown.
 *
 * A molecule: props in; its state is whether it was just copied and whether the person chose to wrap.
 */
export function CodeBlock({ code, language }: { code: string; language?: string | null }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  // The person's choice, or null to leave it to the pane.
  const [chosen, setChosen] = useState<boolean | null>(null);
  const figure = useRef<HTMLElement>(null);
  const width = useWidth(figure);
  const html = useMemo(() => highlighted(code, language), [code, language]);
  const narrow = width === undefined ? undefined : width < WRAP_BELOW_REM * remPx();
  const wrapped = chosen ?? narrow;

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // No clipboard here: the code is on the page to select by hand.
    }
  };

  const control = 'inline-flex min-h-7 cursor-pointer items-center gap-1 border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink';
  return (
    <figure ref={figure} className="@container/code my-2.5 overflow-hidden rounded-control border border-line bg-raised">
      <figcaption className="flex items-center justify-between gap-2 border-b border-line px-3 font-mono text-meta text-ink-faint">
        <span className="min-w-0 truncate">{language || t('work.code.plain')}</span>
        <span className="flex shrink-0 items-center gap-3">
          <button
            type="button"
            aria-pressed={wrapped ?? false}
            onClick={() => setChosen(!(wrapped ?? false))}
            className={cn(control, wrapped && 'text-ink-soft')}
          >
            <Icon name="wrap" size={12} />
            {t('work.code.wrap')}
          </button>
          <button type="button" onClick={() => void copy()} className={control}>
            <Icon name={copied ? 'check' : 'copy'} size={12} />
            {copied ? t('work.code.copied') : t('work.code.copy')}
          </button>
        </span>
      </figcaption>
      <pre
        className={cn(
          'm-0 px-3 py-2.5 font-mono text-small leading-relaxed',
          wrapped === undefined
            // Not measured yet: the block's own width decides, written whole for the stylesheet to find (`WRAP_BELOW`).
            ? 'overflow-x-auto @max-[48rem]/code:whitespace-pre-wrap @max-[48rem]/code:wrap-anywhere'
            : wrapped ? 'whitespace-pre-wrap wrap-anywhere' : 'overflow-x-auto',
        )}
      >
        {/* The highlighter's own escaped output, or the escaped text: never the agent's markup. */}
        <code className="hljs" dangerouslySetInnerHTML={{ __html: html }} />
      </pre>
    </figure>
  );
}

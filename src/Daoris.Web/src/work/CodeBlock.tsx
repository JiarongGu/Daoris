import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import hljs from 'highlight.js/lib/common';
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
 * One code block in a conversation (D76, CONV2): its language named, highlighted in the paper's own
 * inks (`code.css`), and a copy button — the reference console's shape.
 *
 * @remarks
 * **The colours are D41's tokens**, not an imported theme: a keyword wears the accent, a string and a
 * number the two cool status hues, a comment the faint ink. So it follows the chosen theme with no
 * second palette to keep in step.
 *
 * A molecule: props in, and the one piece of state it owns is whether it was just copied.
 */
export function CodeBlock({ code, language }: { code: string; language?: string | null }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const html = useMemo(() => highlighted(code, language), [code, language]);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // No clipboard here: the code is on the page to select by hand.
    }
  };

  return (
    <figure className="my-2.5 overflow-hidden rounded-control border border-line bg-raised">
      <figcaption className="flex items-center justify-between gap-2 border-b border-line px-3 py-1 font-mono text-meta text-ink-faint">
        <span>{language || t('work.code.plain')}</span>
        <button
          type="button"
          onClick={() => void copy()}
          className="inline-flex cursor-pointer items-center gap-1 border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
        >
          <Icon name={copied ? 'check' : 'copy'} size={12} />
          {copied ? t('work.code.copied') : t('work.code.copy')}
        </button>
      </figcaption>
      <pre className="m-0 overflow-x-auto px-3 py-2.5 font-mono text-small leading-relaxed">
        {/* The highlighter's own escaped output, or the escaped text: never the agent's markup. */}
        <code className="hljs" dangerouslySetInnerHTML={{ __html: html }} />
      </pre>
    </figure>
  );
}

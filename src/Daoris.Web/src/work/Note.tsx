import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import type { NotePart } from '../api';
import { cn } from '../lib/cn';
import { Inline } from '../ui';
import { noteBlocks, noteLines } from './noteLines';

const CLAMP = { 2: 'line-clamp-2', 3: 'line-clamp-3', 4: 'line-clamp-4' } as const;

/**
 * A session's note, in the reader's language where it is Daoris's and as written where it is someone's (LANG1b, D142
 * points 1, 4, 5; the language design §2, §5, §6, §9).
 *
 * @remarks
 * **Daoris's lines are chrome**: each coded part is worded from the catalogue with its values, and they run together as
 * one paragraph, joined the reader's way. **Someone's words are content**: the agent's question, the person's answer, a
 * program's message, each set apart beneath the line that leads into it, as written, with its own line breaks; never
 * marked, since they are theirs in whatever language they wrote.
 *
 * **What the page cannot word is shown as its record keeps it, and says so**: a code this page does not know (a newer
 * driver's), a value its sentence needs that is absent, and a record from before parts, whose English is every record on an
 * install before LANG1a. The mark, *shown as recorded*, leads the line, so a clamped note still says it.
 *
 * A molecule: handed the record's note and parts, it reads the catalogue and nothing else.
 */
export function Note({ note, parts, className, clamp, compact = false }: {
  /** The record's English, shown as kept where it has no parts. */
  note?: string | null;
  /** The note's lines, as the record answers them (LANG1a). */
  parts?: readonly NotePart[] | null;
  /** The type step and ink its words are set in, which the place showing it chooses. */
  className?: string;
  /** How many lines show before the rest is cut: the timeline's four (UX6b), the head's three, a row's two. Absent, the whole note. */
  clamp?: 2 | 3 | 4;
  /** One run of words, for a row too narrow for blocks: someone's words inline rather than set apart. */
  compact?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const lines = noteLines(t, { note, parts }, i18n.language);
  if (lines.length === 0) return null;

  if (compact) {
    // The catalogue's gap between two sentences: a space after an English full stop, none after a Chinese one.
    const gap = t('work.note.join', { first: '', second: '' });
    return (
      <span className={cn('min-w-0', clamp && CLAMP[clamp], className)}>
        {noteBlocks(t, lines).map((block, index) => (
          <Fragment key={index}>
            {index > 0 && gap}
            {block.kind === 'recorded' && <Recorded />}
            {block.kind === 'words' ? block.text : <Inline text={block.text} />}
          </Fragment>
        ))}
      </span>
    );
  }

  return (
    <div className={cn('min-w-0 space-y-1.5', clamp && CLAMP[clamp], className)}>
      {noteBlocks(t, lines).map((block, index) => {
        if (block.kind === 'words') {
          return (
            <blockquote key={index} className="m-0 whitespace-pre-wrap border-l-2 border-line-strong pl-3" data-by={block.by}>
              {block.text}
            </blockquote>
          );
        }
        return (
          <p key={index} className="m-0 whitespace-pre-wrap">
            {block.kind === 'recorded' && <Recorded />}
            <span><Inline text={block.text} /></span>
          </p>
        );
      })}
    </div>
  );
}

/** The mark on a line the page could not word: its record's own words, shown as kept (D142 points 4, 5). */
function Recorded() {
  const { t } = useTranslation();
  return (
    <span className="mr-1.5 inline-block rounded-control border border-line px-1 align-baseline text-meta not-italic leading-[1.4] text-ink-faint">
      {t('work.note.recorded')}
    </span>
  );
}

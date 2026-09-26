import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Icon } from '../ui';

/**
 * The files a mention could mean (CONV4d), floating over the conversation above the composer's box.
 *
 * @remarks
 * **The box keeps the focus.** The list is the textbox's, told by `aria-controls` and followed by
 * `aria-activedescendant`, so the arrows, Enter and Tab stay where the person is typing — and a click
 * on a row is taken on `mousedown` without moving the focus, so the box never loses the caret.
 *
 * **Never an empty panel** (the palette's rule): listing, no match, an empty tree and the host's refusal
 * are each a sentence, and a bound that left files out says so beneath what it did offer.
 */
export function MentionList({ id, options, active, query, listing, refusal, empty, unlisted, onPick, onActive }: {
  /** The listbox's id, for the box's `aria-controls`; each row is `${id}-${index}`. */
  id: string;
  /** The files to offer, best first. */
  options: string[];
  active: number;
  /** What was typed after the `@`. */
  query: string;
  /** The tree's files have been asked for and not answered yet. */
  listing: boolean;
  /** The host's sentence when it cannot list the tree, verbatim. */
  refusal: string | null;
  /** The tree holds no files at all. */
  empty: boolean;
  /** How many files the host's bound left out of the list. */
  unlisted: number;
  onPick: (path: string) => void;
  onActive: (index: number) => void;
}) {
  const { t } = useTranslation();
  const list = useRef<HTMLUListElement>(null);

  useEffect(() => {
    list.current?.querySelector('[data-active="true"]')?.scrollIntoView?.({ block: 'nearest' });
  }, [active, options]);

  const said = refusal
    ?? (listing ? t('work.composer.mention.listing')
      : empty ? t('work.composer.mention.empty')
        : options.length === 0 ? t('work.composer.mention.none', { query })
          : null);

  return (
    // Sized to its rows, within bounds: across the box it ran 1,900px on a maximized window for rows
    // about 450px wide (UX5 U64). It opens from the box's left edge, where the writing starts.
    <div className="absolute bottom-full left-0 z-10 mb-1 w-max min-w-[min(20rem,100%)] max-w-[min(36rem,100%)] overflow-hidden rounded-overlay border border-line bg-overlay shadow-[0_8px_32px_rgb(15_12_8/0.18)]">
      {options.length > 0 && (
        <ul
          id={id}
          ref={list}
          role="listbox"
          aria-label={t('work.composer.mention.label')}
          className="m-0 max-h-64 list-none overflow-y-auto p-1"
        >
          {options.map((path, index) => {
            const slash = path.lastIndexOf('/');
            return (
              <li
                key={path}
                id={`${id}-${index}`}
                role="option"
                aria-label={path}
                aria-selected={index === active}
                data-active={index === active}
                // Pointer, not hover-with-focus: the row under the cursor is the one Enter takes.
                onMouseMove={() => onActive(index)}
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => onPick(path)}
                className={cn(
                  'flex min-w-0 cursor-default items-baseline gap-2 rounded-control px-2 py-1 text-small',
                  index === active ? 'bg-accent-soft text-ink' : 'text-ink-soft',
                )}
              >
                <Icon name="read" size={13} className="shrink-0 self-center text-ink-faint" />
                {/* Both truncate, and the folder gives way a hundred times faster than the name: a very
                    long name keeps most of the row and a hint of where it lives (seen on the window). */}
                <span className="min-w-0 truncate">{path.slice(slash + 1)}</span>
                {slash > 0 && (
                  <span className="min-w-0 shrink-[100] truncate text-meta text-ink-faint">{path.slice(0, slash)}</span>
                )}
              </li>
            );
          })}
        </ul>
      )}
      {said && <p className="m-0 px-2.5 py-2 text-small text-ink-faint">{said}</p>}
      {unlisted > 0 && !refusal && (
        <p className="m-0 border-t border-line px-2.5 py-1.5 text-meta text-ink-faint">
          {t('work.composer.mention.unlisted', { count: unlisted })}
        </p>
      )}
    </div>
  );
}

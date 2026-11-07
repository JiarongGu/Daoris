import { type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';

/** The results a find lists beside stepping. */
const LISTED = 50;

/**
 * **Find on a canvas**: a field in the toolbar, worded by the feature, that says *n of m*.
 * Enter and Shift+Enter step the matches (the focus stays in the field: the next Enter would open the node), ↓ hands the focus to the
 * match shown and Escape clears the find. The matches are also a list of named results to choose from. A molecule: all of it is given.
 */
export function CanvasFind({ label, query, onQuery, matches, at, onStep, onClear, onDown, onPick }: {
  label: string;
  query: string;
  onQuery: (query: string) => void;
  /** The matches, in order, each with the words a person knows it by. */
  matches: readonly { id: string; title: string }[];
  /** The match shown, or -1. */
  at: number;
  onStep: (back: boolean) => void;
  onClear: () => void;
  onDown: () => void;
  onPick: (id: string) => void;
}) {
  const { t } = useTranslation();
  const searching = query.trim() !== '';
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (isComposing(event)) return;
    if (event.key === 'Enter') { event.preventDefault(); onStep(event.shiftKey); }
    else if (event.key === 'Escape' && (searching || at >= 0)) { event.preventDefault(); event.stopPropagation(); onClear(); }
    else if (event.key === 'ArrowDown' && at >= 0) { event.preventDefault(); onDown(); }
  };
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
      <input
        type="search"
        value={query}
        aria-label={label}
        placeholder={label}
        onChange={(event) => onQuery(event.target.value)}
        onKeyDown={onKeyDown}
        className="h-7 w-44 min-w-0 rounded-control border border-line bg-page px-2 text-small text-ink placeholder:text-ink-faint focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent"
      />
      <span role="status" aria-live="polite" className={cn('font-mono text-meta tabular-nums text-ink-soft', !searching && 'sr-only')}>
        {searching ? (matches.length === 0 ? t('canvas.findNone') : t('canvas.findAt', { at: Math.max(at, 0) + (at >= 0 ? 1 : 0), count: matches.length })) : ''}
      </span>
      {searching && matches.length > 0 && (
        <ul aria-label={t('canvas.findResults')} className="max-h-32 basis-full overflow-auto rounded-control border border-line bg-page text-small">
          {matches.slice(0, LISTED).map((match, index) => (
            <li key={match.id}>
              <button
                type="button"
                aria-current={index === at ? 'true' : undefined}
                onClick={() => onPick(match.id)}
                className={cn('block w-full truncate px-2 py-0.5 text-left text-ink hover:bg-raised', index === at && 'bg-raised font-medium')}
              >
                {match.title}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

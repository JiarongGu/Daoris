import { useTranslation } from 'react-i18next';
import { isComposing } from '../lib/composing';
import { searchable } from '../searchable';
import { Button, Icon } from '../ui';

/**
 * The way through a long run (SESS1 S9): a jump to where it first failed and to its last words, and a
 * search within the session — its words and its calls by their titles — stepped through in order.
 *
 * @remarks
 * Written from the first real workspace, whose driven sessions ran to 1,855 events: a reader came for
 * the failure or the conclusion, and scrolled for them. **A jump opens what it lands in** — the fold, and
 * the pages before — which is the organism's; this is the row, props in and presses out.
 */
export function ConversationFind({
  hasFailure, onFirstFailure, hasWords, onLastWords, query, onQuery, hits, at = 0, cut = false, onStep,
}: {
  /** Whether a call failed anywhere in the run: the jump is offered only then. */
  hasFailure: boolean;
  onFirstFailure: () => void;
  /** Whether the agent has said anything to end on. */
  hasWords: boolean;
  onLastWords: () => void;
  query: string;
  onQuery: (query: string) => void;
  /** How many places the search found, once it has answered; undefined while nothing is asked. */
  hits?: number;
  /** Which of them is shown, from 0. */
  at?: number;
  /** Whether the host left places out beyond its bound. */
  cut?: boolean;
  onStep: (by: 1 | -1) => void;
}) {
  const { t } = useTranslation();
  const found = hits ?? 0;

  return (
    <div role="toolbar" aria-label={t('work.find.label')} className="flex flex-wrap items-center gap-2">
      {hasFailure && (
        <Button variant="ghost" onClick={onFirstFailure} className="text-small">
          <Icon name="failure" size={12} />{t('work.find.firstFailure')}
        </Button>
      )}
      {hasWords && (
        <Button variant="ghost" onClick={onLastWords} className="text-small">
          <Icon name="toBottom" size={12} />{t('work.find.lastWords')}
        </Button>
      )}
      <div className="ml-auto flex items-center gap-1.5">
        <input
          type="search"
          aria-label={t('work.find.placeholder')}
          value={query}
          placeholder={t('work.find.placeholder')}
          onChange={(event) => onQuery(event.target.value)}
          onKeyDown={(event) => {
            // Words found through an input method: its Enter accepts a candidate and steps nowhere (IME1).
            if (isComposing(event) || event.key !== 'Enter' || found === 0) return;
            event.preventDefault();
            onStep(event.shiftKey ? -1 : 1);
          }}
          className="w-56 rounded-control border border-line-strong bg-raised px-2.5 py-1 text-small text-ink placeholder:text-ink-faint"
        />
        {hits !== undefined && searchable(query) && (
          <>
            <span aria-live="polite" className="whitespace-nowrap text-meta tabular-nums text-ink-faint">
              {found === 0
                ? t('work.find.none')
                : t(cut ? 'work.find.countMore' : 'work.find.count', { at: at + 1, count: found })}
            </span>
            <Button variant="ghost" aria-label={t('work.find.previous')} disabled={found === 0} onClick={() => onStep(-1)}>
              <Icon name="chevronUp" size={12} />
            </Button>
            <Button variant="ghost" aria-label={t('work.find.next')} disabled={found === 0} onClick={() => onStep(1)}>
              <Icon name="chevronDown" size={12} />
            </Button>
          </>
        )}
      </div>
    </div>
  );
}

import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest } from '../api';
import { Button } from '../ui';
import { cn } from '../lib/cn';
import { questName } from '../work/identity';
import type { ChainStep } from './chain';

/** A quest's status, in the words and the hue its pill wears (D41: never hue alone). */
const WORD: Record<Quest['status'], string> = {
  Open: 'text-st-open', Taken: 'text-st-taken', Done: 'text-st-done', Declined: 'text-ink-danger',
};

/**
 * How the work ran, as one line of stops (SESS2 H7): the ask, each quest with how it stands, the
 * attended one as *this quest*, and what is still to come — with the whole strip on a press.
 *
 * @remarks
 * Above a session's conversation the strip gave 350 to 450px to the attended quest's title and status
 * again, and to every session of a quest its tool and version again. The head already says this
 * quest's title and state, so here it is *this quest*; a quest's sessions are the strip's, a press away.
 *
 * A molecule: the chain arrives built, and a press goes out.
 */
export function ChainLine({ chain, onQuest, onExpand }: {
  chain: ChainStep[];
  onQuest?: (quest: Quest) => void;
  /** Show the whole strip. */
  onExpand: () => void;
}) {
  const { t } = useTranslation();

  return (
    <nav aria-label={t('chain.title')} className="flex min-w-0 flex-wrap items-center gap-x-1.5 gap-y-1 text-small">
      <span className="shrink-0 text-meta text-ink-faint">{t('chain.title')}</span>
      {chain.map((step, index) => (
        <Fragment key={step.kind === 'quest' ? step.quest.id : `${step.kind}-${index}`}>
          {index > 0 && <span aria-hidden className="text-ink-faint">›</span>}
          <Stop step={step} onQuest={onQuest} />
        </Fragment>
      ))}
      <Button variant="ghost" className="px-1.5 py-0 text-small" onClick={onExpand}>{t('chain.show')}</Button>
    </nav>
  );
}

function Stop({ step, onQuest }: { step: ChainStep; onQuest?: (quest: Quest) => void }) {
  const { t } = useTranslation();

  if (step.kind === 'ask') return <span className="font-mono text-meta text-ink-soft">{t('chain.ask', { id: step.id })}</span>;
  if (step.kind === 'unseen') return <span className="font-mono text-meta text-ink-faint">#{step.id}</span>;
  if (step.kind === 'pending') {
    return <span className="max-w-64 truncate text-ink-faint" title={step.step.title}>{t('chain.next', { to: step.step.to })}</span>;
  }

  const { quest, current } = step;
  const status = <span className={cn('shrink-0 text-meta', WORD[quest.status])}>{t(`status.${quest.status}`)}</span>;
  if (current) {
    return <span className="inline-flex items-baseline gap-1.5"><span className="font-semibold text-ink">{t('chain.current')}</span>{status}</span>;
  }
  return (
    <span className="inline-flex min-w-0 items-baseline gap-1.5">
      {onQuest
        ? (
          <button
            type="button"
            onClick={() => onQuest(quest)}
            title={quest.title}
            className="max-w-64 cursor-pointer truncate border-0 bg-transparent p-0 text-left text-small text-ink underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
          >
            {questName(quest)}
          </button>
        )
        : <span className="max-w-64 truncate text-ink" title={quest.title}>{questName(quest)}</span>}
      {status}
    </span>
  );
}

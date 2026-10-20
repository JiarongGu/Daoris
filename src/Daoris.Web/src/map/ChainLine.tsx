import { Fragment } from 'react';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import type { Quest } from '../data/api';
import { Button } from '../design/ui';
import { cn } from '../lib/cn';
import { shownStatus } from '../quests/records';
import { questName } from '../work/identity';
import type { ChainStep } from './chain';

/** A quest's status, in the words and the ink its pill's word wears (D41: never hue alone; UXFIX5c). */
const WORD: Record<Quest['status'], string> = {
  Open: 'text-ink-open', Taken: 'text-ink-taken', Done: 'text-ink-done', Declined: 'text-ink-danger',
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
 * **One line, whatever the width** (HEADFIT1b, D185): the stops truncate with an ellipsis, whole in their tip, and *Show how it
 * ran* keeps its place at the line's end. Wrapped, the press took a row of its own at the main area's floor, 22 px of a record
 * head's 200.
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

  const whole = [t('chain.title'), chain.map((step) => stepText(step, t)).join(' › ')].join(' ');
  return (
    <nav aria-label={t('chain.title')} className="flex min-w-0 items-center gap-x-1.5 text-small">
      <span className="min-w-0 flex-1 truncate" title={whole}>
        <span className="text-meta text-ink-faint">{t('chain.title')}</span>
        {chain.map((step, index) => (
          <Fragment key={step.kind === 'quest' ? step.quest.id : `${step.kind}-${index}`}>
            {index > 0 && <span aria-hidden className="ml-1.5 text-ink-faint">›</span>}
            <span className="ml-1.5"><Stop step={step} onQuest={onQuest} /></span>
          </Fragment>
        ))}
      </span>
      <Button variant="ghost" className="shrink-0 px-1.5 py-0 text-small" onClick={onExpand}>{t('chain.show')}</Button>
    </nav>
  );
}

/** A stop in words, as the line's tip says it whole. */
function stepText(step: ChainStep, t: TFunction): string {
  if (step.kind === 'ask') return t('chain.ask', { id: step.id });
  if (step.kind === 'unseen') return `#${step.id}`;
  if (step.kind === 'pending') return t('chain.next', { to: step.step.to });
  const status = t(`status.${shownStatus(step.quest)}`);
  const held = step.quest.hold === 'source-reopened' ? ` ${t('quests.card.reopened')}` : '';
  return `${step.current ? t('chain.current') : questName(step.quest)} ${status}${held}`;
}

function Stop({ step, onQuest }: { step: ChainStep; onQuest?: (quest: Quest) => void }) {
  const { t } = useTranslation();

  if (step.kind === 'ask') return <span className="font-mono text-meta text-ink-soft">{t('chain.ask', { id: step.id })}</span>;
  if (step.kind === 'unseen') return <span className="font-mono text-meta text-ink-faint">#{step.id}</span>;
  if (step.kind === 'pending') {
    return <span className="inline-block max-w-64 truncate align-bottom text-ink-faint" title={step.step.title}>{t('chain.next', { to: step.step.to })}</span>;
  }

  const { quest, current } = step;
  // The words the quest's page reads (FABRIC-CLOSE1d2), in the same plain ink: submitted is a taken quest's, and a step held for
  // its source reopened says so after its status.
  const status = (
    <>
      <span className={cn('ml-1.5 text-meta', WORD[quest.status])}>{t(`status.${shownStatus(quest)}`)}</span>
      {quest.hold === 'source-reopened' && <span className="ml-1.5 text-meta text-ink-open">{t('quests.card.reopened')}</span>}
    </>
  );
  if (current) {
    return <span><span className="font-semibold text-ink">{t('chain.current')}</span>{status}</span>;
  }
  return (
    <span>
      {onQuest
        ? (
          <button
            type="button"
            onClick={() => onQuest(quest)}
            title={quest.title}
            className="inline-block max-w-64 cursor-pointer truncate border-0 align-bottom bg-transparent p-0 text-left text-small text-ink underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
          >
            {questName(quest)}
          </button>
        )
        : <span className="inline-block max-w-64 truncate align-bottom text-ink" title={quest.title}>{questName(quest)}</span>}
      {status}
    </span>
  );
}

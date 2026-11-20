import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Dot, type DOT_TONE } from '../../design/ui';
import { phaseTime } from '../../locales/format';
import { cn } from '../../lib/cn';

/** The state the head names, as the driver's reader gives it: never the record's state word. */
export type PanelState = 'loading' | 'live' | 'idle' | 'held' | 'waiting' | 'waitsOn' | 'failed' | 'finished' | 'stopped' | 'stale';

/**
 * What the head says: the state with its time in state and what it does now (for a wait on the person, the oldest wait); the title
 * as *ask › quest*; where and who: repository, branch, role, the maker and the model that ran (with the model asked where it
 * differs), the account.
 */
export type HeadFacts = {
  state: PanelState;
  since: string;
  doing?: string | null;
  /** Whom it waits on, for `waitsOn`. */
  waitsOn?: string | null;
  title: string;
  ask?: string | null;
  /** A quest's or an ask's title, which takes two lines; any other is cut on one. */
  wraps?: boolean;
  repository: string;
  branch?: string | null;
  role?: string | null;
  maker: string;
  /** The model that ran, as the record acknowledged it; null where the record reports none, never the asked one. */
  model: string | null;
  /** The model asked, where it differs from the one that ran. */
  asked?: string | null;
  account?: string | null;
};

/** The facts its state makes matter (`sessionFacts`: an attempt, a retry, its integration, the agent leading its line, whom it continues), and the line's tip. */
export type HeadFactsLine = { said: readonly string[]; tip?: string | null };

const TONE: Readonly<Record<PanelState, keyof typeof DOT_TONE>> = {
  loading: 'idle', live: 'live', idle: 'idle', held: 'idle', waiting: 'parked', waitsOn: 'waits', failed: 'failed', finished: 'done',
  stopped: 'ended', stale: 'idle',
};

/** The model that ran, said once: "asked X, ran Y" where they differ, "model not reported" where the record names none. */
export function useModelWords() {
  const { t } = useTranslation();
  return (head: Pick<HeadFacts, 'model' | 'asked'>) => {
    if (!head.model) return t('work.session.head.notReported');
    return head.asked && head.asked !== head.model ? t('work.session.head.asked', { asked: head.asked, ran: head.model }) : head.model;
  };
}

/**
 * **The head**: the state, its time in state and what it does now, with the acts at its right (a wait on the person names the oldest,
 * a press to it in the dock, `onWait`); the title as *ask › quest*; where and who, ending in the head's chips (`chips`: a supervisor's
 * count, a later choice, full access) where the host hands them, cut before them; and, where its state makes any matter, the facts
 * line (`facts`), faint on one line, whole in its tip. Narrow, the where line ends with the changes as one fact (`changes`).
 */
export function PanelHead({ head, now, acts, changes, chips, onWait, facts }: {
  head: HeadFacts; now: number; acts?: ReactNode; changes?: ReactNode; chips?: ReactNode;
  /** The oldest wait's press: to its card in the dock. */
  onWait?: () => void;
  facts?: HeadFactsLine | null;
}) {
  const { t } = useTranslation();
  const modelWords = useModelWords();
  const word = head.state === 'waitsOn' ? t('work.session.state.waitsOn', { who: head.waitsOn ?? '' }) : t(`work.session.state.${head.state}`);
  const where = [head.repository, head.branch, head.role, head.maker, modelWords(head), head.account].filter(Boolean).join(' · ');
  return (
    <header data-region="head" className="grid gap-0.5 border-b border-line pb-2">
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        <Dot tone={TONE[head.state]} label={word} className="text-small font-semibold" />
        {head.state !== 'loading' && <span className="text-meta text-ink-faint">{phaseTime(now - Date.parse(head.since))}</span>}
        {head.doing && (onWait ? (
          <button
            type="button"
            onClick={onWait}
            className="min-w-0 flex-1 cursor-pointer truncate border-0 bg-transparent p-0 text-left text-small text-ink-open underline-offset-2 hover:underline"
          >
            {head.doing}
          </button>
        ) : <span className="min-w-0 flex-1 truncate text-small text-ink-soft">{head.doing}</span>)}
        {acts && <span className="ml-auto flex shrink-0 items-center gap-1">{acts}</span>}
      </div>
      <h2 className={cn('m-0 min-w-0 text-view font-semibold text-ink', head.wraps ? 'line-clamp-2' : 'truncate')}>
        {head.ask && <span className="font-normal text-ink-soft">{head.ask} › </span>}
        {head.title}
      </h2>
      <p className="m-0 flex min-w-0 items-baseline gap-2 text-small text-ink-soft">
        <span className="min-w-0 truncate">{where}</span>
        {chips}
        {changes}
      </p>
      {facts && facts.said.length > 0 && (
        <p data-head-facts className="m-0 min-w-0 truncate text-meta text-ink-faint" title={facts.tip ?? facts.said.join(' · ')}>
          {facts.said.join(' · ')}
        </p>
      )}
    </header>
  );
}

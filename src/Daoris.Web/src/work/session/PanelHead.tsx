import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Dot, type DOT_TONE } from '../../design/ui';
import { phaseTime } from '../../locales/format';

/** The state the head names, as the driver's reader gives it: never the record's state word. */
export type PanelState = 'loading' | 'live' | 'idle' | 'waiting' | 'waitsOn' | 'failed' | 'finished' | 'stopped' | 'stale';

/**
 * What the head says: the state with its time in state and what it does now; the title as *ask › quest*; where and who: repository,
 * branch, role, the maker and the model that ran (with the model asked where it differs), the account.
 */
export type HeadFacts = {
  state: PanelState;
  since: string;
  doing?: string | null;
  /** Whom it waits on, for `waitsOn`. */
  waitsOn?: string | null;
  title: string;
  ask?: string | null;
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

const TONE: Readonly<Record<PanelState, keyof typeof DOT_TONE>> = {
  loading: 'idle', live: 'live', idle: 'idle', waiting: 'parked', waitsOn: 'waits', failed: 'failed', finished: 'done',
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
 * **The head** (at most three lines): the state, its time in state and what it does now, with the acts at its right; the title as
 * *ask › quest*; where and who, ending in the head's chips (`chips`: a supervisor's count, a later choice, full access) where the
 * host hands them, cut before them. Narrow, the third line ends with the changes as one fact (`changes`).
 */
export function PanelHead({ head, now, acts, changes, chips }: {
  head: HeadFacts; now: number; acts?: ReactNode; changes?: ReactNode; chips?: ReactNode;
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
        {head.doing && <span className="min-w-0 flex-1 truncate text-small text-ink-soft">{head.doing}</span>}
        {acts && <span className="ml-auto flex shrink-0 items-center gap-1">{acts}</span>}
      </div>
      <h2 className="m-0 min-w-0 truncate text-view font-semibold text-ink">
        {head.ask && <span className="font-normal text-ink-soft">{head.ask} › </span>}
        {head.title}
      </h2>
      <p className="m-0 flex min-w-0 items-baseline gap-2 text-small text-ink-soft">
        <span className="min-w-0 truncate">{where}</span>
        {chips}
        {changes}
      </p>
    </header>
  );
}

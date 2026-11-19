import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Dot, type DOT_TONE } from '../../design/ui';
import { phaseTime } from '../../locales/format';
import { cn } from '../../lib/cn';
import type { Track } from '../track/passage';

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
  /** The model that ran, as the record acknowledged it; null where none was reported, never the asked one. */
  model: string | null;
  /** The model asked, where it differs from the one that ran. */
  asked?: string | null;
  account?: string | null;
};

const TONE: Readonly<Record<PanelState, keyof typeof DOT_TONE>> = {
  loading: 'idle', live: 'live', idle: 'idle', waiting: 'parked', waitsOn: 'waits', failed: 'failed', finished: 'done',
  stopped: 'ended', stale: 'idle',
};

const CHIP_GLYPH = { passed: '✓', current: '●', failed: '▲', next: '·' } as const;

/** The model that ran, said once: "asked X, ran Y" where they differ, "model not reported" where none was. */
export function useModelWords() {
  const { t } = useTranslation();
  return (head: Pick<HeadFacts, 'model' | 'asked'>) => {
    if (!head.model) return t('work.session.head.notReported');
    return head.asked && head.asked !== head.model ? t('work.session.head.asked', { asked: head.asked, ran: head.model }) : head.model;
  };
}

/**
 * **The head** (at most three lines): the state, its time in state and what it does now, with the acts at its right; the title as
 * *ask › quest*; where and who. Narrow, the third line ends with the changes as one fact (`changes`).
 */
export function PanelHead({ head, now, acts, changes }: { head: HeadFacts; now: number; acts?: ReactNode; changes?: ReactNode }) {
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
        {changes}
      </p>
    </header>
  );
}

/** **The track**: each step of the run a chip with its mark, the standing one with its attempt; a press scrolls to its rule. */
export function PanelTrack({ track, onStep }: { track: Track; onStep?: (node: string) => void }) {
  const { t } = useTranslation();
  return (
    <nav data-region="track" aria-label={t('work.session.track.label')} className="flex min-w-0 flex-wrap items-center gap-1 py-1.5">
      {track.chips.map((chip, index) => (
        <span key={chip.node} className="flex items-center gap-1">
          {index > 0 && <span aria-hidden="true" className="text-ink-faint">›</span>}
          <button
            type="button"
            onClick={() => onStep?.(chip.node)}
            className={cn(
              'inline-flex cursor-pointer items-center gap-1 rounded-control border border-line bg-transparent px-1.5 py-0.5 text-small',
              chip.mark === 'passed' && 'text-ink-done',
              chip.mark === 'current' && 'border-st-taken text-ink-taken',
              chip.mark === 'failed' && 'border-st-declined text-ink-danger',
              chip.mark === 'next' && 'text-ink-faint',
            )}
          >
            <span aria-hidden="true">{CHIP_GLYPH[chip.mark]}</span>
            {chip.node}
            {chip.attempt && chip.attempt > 1 && <span className="text-meta">{t('work.session.state.retry', { attempt: chip.attempt })}</span>}
            {chip.wait && <span className="text-meta text-ink-open">◆ {t('work.session.track.waitsOnYou')}</span>}
          </button>
        </span>
      ))}
      <span className="ml-1 text-meta text-ink-faint">{t('work.session.track.of', { at: track.at, of: track.of })}</span>
    </nav>
  );
}

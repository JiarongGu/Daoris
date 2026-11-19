import { useTranslation } from 'react-i18next';
import { ExternalLink } from '../design/links';
import type { RunStep } from './run';
import { roleSaid } from './stepRoles';

const STATES: Record<string, true> = { reached: true, waiting: true, failed: true };

/**
 * The acts journaled on a step while its holder acts, one row each under it: the fact its receipt names and its state, its
 * words, its credit and page, and *waiting on `<role>`: <holder>* where the receipt waits on another role. Nothing where none is journaled.
 */
export function StepActRows({ step }: { step: RunStep }) {
  const { t, i18n } = useTranslation();
  if (!step.journal || step.journal.length === 0) return null;
  const holderSaid = (holder: string) => holder === 'person'
    ? t('workflow.run.holder.you')
    : holder.startsWith('agent:') ? holder.slice('agent:'.length) : holder === 'agent' ? t('workflow.run.holder.anAgent') : holder;
  return (
    <ul aria-label={t('workflow.run.acts.title')} className="m-0 grid list-none gap-1 p-0">
      {step.journal.map((act, at) => (
        <li key={`${act.fact}:${at}`} data-act={act.fact} className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small text-ink-soft">
          <span className={act.state === 'failed' ? 'text-ink' : undefined}>
            {t('workflow.run.acts.row', { fact: act.fact, state: STATES[act.state] ? t(`workflow.run.acts.state.${act.state}`) : act.state })}
          </span>
          {act.message && <span className="text-ink">{act.message}</span>}
          {act.by && <span className="text-ink-faint">{t('workflow.run.acts.by', { by: act.by })}</span>}
          {act.waitsOn && (
            <span className="text-ink-faint">
              {t('workflow.run.state.waitingOn', { role: roleSaid(t, i18n, act.waitsOn), holder: act.holder ? holderSaid(act.holder) : t('workflow.run.holder.you') })}
            </span>
          )}
          {act.url && <ExternalLink href={act.url} className="text-small text-accent">{t('workflow.run.acts.page')}</ExternalLink>}
        </li>
      ))}
    </ul>
  );
}

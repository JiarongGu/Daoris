import { useTranslation } from 'react-i18next';
import { Inline } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';
import type { WorkflowUseAnswer } from '../edit/answers';

/** The plan of one choice as the page holds it: on its way, refused to be read, or read. */
export type PlanState =
  | { key: string; state: 'reading' }
  | { key: string; state: 'failed'; why: string }
  | { key: string; state: 'read'; answer: WorkflowUseAnswer };

/** The token a press carries: the plan's, where it was read for this very ask and the driver did not refuse it. */
export function planToken(plan: PlanState | null, key: string): string | null {
  if (plan === null || plan.key !== key || plan.state !== 'read') return null;
  return plan.answer.refused ? null : plan.answer.expected ?? null;
}

/**
 * **The plan of a choice** (SETUPUX1p; the Setup design's §2.3), in the driver's words: what a cleared default's kinds keep, the part of the
 * person the choice lowers, the places it changes and the driver's own account. Nothing is judged here: a refusal is the driver's sentence.
 *
 * A molecule: the plan arrives as a prop.
 */
export function ChangePlan({ plan }: { plan: PlanState }) {
  const { t } = useTranslation();

  if (plan.state === 'reading') {
    return <p aria-busy="true" className="m-0 text-small text-ink-soft">{t('workflow.place.change.plan.reading')}</p>;
  }
  if (plan.state === 'failed') return <Refused sentence={plan.why} />;

  const { answer } = plan;
  if (answer.refused) return <Refused sentence={answer.message ?? answer.refused} />;

  const lowers = answer.lowers ?? [];
  const places = answer.places ?? [];
  return (
    <div className="grid min-w-0 gap-2">
      {answer.keeps && (
        <p className="m-0 rounded-control border border-line px-3 py-2 text-small text-ink"><Inline text={answer.keeps.said} /></p>
      )}
      {lowers.length > 0 && (
        <div className="grid gap-0.5">
          <h5 className="m-0 text-small font-medium text-ink">{t('workflow.place.change.plan.part')}</h5>
          <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
            {lowers.map((each) => <li key={each} className="[overflow-wrap:anywhere]"><Inline text={each} /></li>)}
          </ul>
        </div>
      )}
      {places.length > 0 && (
        <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
          {places.map((each) => (
            <li key={each.repository} className="[overflow-wrap:anywhere]">
              {t('workflow.place.change.plan.place', { repository: each.repository, was: each.was, now: each.now })}
            </li>
          ))}
        </ul>
      )}
      {answer.message && (
        <details className="min-w-0 text-small text-ink-soft">
          <summary className="cursor-pointer">{t('workflow.place.change.plan.driver')}</summary>
          <p className="m-0 mt-1 whitespace-pre-line [overflow-wrap:anywhere]"><Inline text={answer.message} /></p>
        </details>
      )}
    </div>
  );
}

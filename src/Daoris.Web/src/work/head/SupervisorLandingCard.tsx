import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type LandingRead, landingSays } from './lineSupervision';

/**
 * The supervisor's accept, approve or reject of a step waiting for the person's press, with the time it takes effect and *Overrule* while
 * the decision is held, the window in which it is theirs to take back.
 *
 * @remarks
 * The card stands only while it can be pressed: a decision no longer held is not drawn, so it never lingers as a notice.
 * A molecule: handed the decision and its door; where nothing can act it is handed none, and still says the decision.
 */
export function SupervisorLandingCard({ landing, onOverrule, busy = false, said = null }: {
  landing: LandingRead | null;
  /** Take the decision back, by its id. Absent where nothing can. */
  onOverrule?: (decision: string) => void;
  busy?: boolean;
  /** Why the last overrule did not take, in the bridge's words. */
  said?: string | null;
}) {
  const { t } = useTranslation();
  if (!landing || landing.state !== 'held' || landing.do === 'hold') return null;
  const title = t('work.supervision.landing.title');
  return (
    <section aria-label={title} className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{title}</h3>
      <p className="m-0 mt-1.5 text-small text-ink-soft">{landingSays(t, landing)}</p>
      {onOverrule && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          <Button disabled={busy} onClick={() => onOverrule(landing.id)}>{t('work.supervision.overrule')}</Button>
        </div>
      )}
      {said && <p role="status" className="m-0 mt-1.5 text-small text-warn">{said}</p>}
    </section>
  );
}

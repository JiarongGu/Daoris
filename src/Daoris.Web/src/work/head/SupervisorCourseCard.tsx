import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type CourseRead, courseWhole } from './lineSupervision';

/**
 * The supervisor's decision on a point a person's rule referred: which rule referred it, what the supervisor chose and when,
 * and *Overrule* while the decision is held, the window in which it is theirs to take back.
 *
 * @remarks
 * **The card stands only while it can be pressed.** A decision that went on, is overruled, did not go on or is left to the person is
 * *Details*' line alone: a card that said it for as long as the line lives would be a notice that never went away.
 *
 * A molecule: handed the decision and its door. Where nothing can act it is handed none, and still says the decision.
 */
export function SupervisorCourseCard({ course, onOverrule, busy = false, said = null }: {
  course: CourseRead | null;
  /** Take the decision back, by its id. Absent where nothing can. */
  onOverrule?: (decision: string) => void;
  busy?: boolean;
  /** Why the last overrule did not take, in the bridge's words. */
  said?: string | null;
}) {
  const { t } = useTranslation();
  if (!course || course.state !== 'held') return null;
  return (
    <section aria-label={t('work.supervision.cardTitle')} className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{t('work.supervision.cardTitle')}</h3>
      <p className="m-0 mt-1.5 text-small text-ink-soft">{courseWhole(t, course)}</p>
      {onOverrule && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          <Button disabled={busy} onClick={() => onOverrule(course.id)}>{t('work.supervision.overrule')}</Button>
        </div>
      )}
      {said && <p role="status" className="m-0 mt-1.5 text-small text-warn">{said}</p>}
    </section>
  );
}

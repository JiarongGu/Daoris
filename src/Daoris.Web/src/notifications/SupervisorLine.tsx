import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { moment } from '../locales/format';
import { type AppointmentNow, OVERRULE_WINDOW } from '../person/standing';
import { Refused } from '../work/InlineConfirm';

/**
 * The supervisor's line in *Notifications* (SUPERVISOR1a-ii, D208; the supervisor design §5, §6.2, §6.4): one per appointment the record
 * holds, at the head of the presses' group beside the standing confirmation's. Appointed, for which repository or workspace (SUPERVISOR1e)
 * and since when, that its answers are said here and held for the overrule window the owner set, five where the record says none; found as Daoris started, dormant until *Appoint again*, never said as how it
 * was written. **Turn off** at once, with no ask. **A line, not a row of *What needs you***: nothing waits on the owner, so it counts
 * toward no group's count and never the bell's. Nothing where there is no appointment.
 *
 * A molecule: drawn from props; the application holds the press.
 */
export function SupervisorLine({ appointments, busy = false, refused = null, onTurnOff }: {
  appointments: readonly AppointmentNow[];
  /** *Turn off* on its way. */
  busy?: boolean;
  /** What *Turn off* was refused with, in words, said under the lines. */
  refused?: string | null;
  onTurnOff: (appointment: AppointmentNow) => void;
}) {
  const { t } = useTranslation();
  const supervisors = appointments.filter((each) => each.role === 'supervisor');
  if (supervisors.length === 0) return null;
  return (
    <section data-supervisor-line="" aria-label={t('notifications.supervisor.label')} className="grid gap-2 rounded-control border border-line border-l-[3px] border-l-warn bg-sunken px-3 py-2">
      {supervisors.map((each) => {
        const where = each.workspace ? t('notifications.supervisor.workspace', { workspace: each.workspace }) : each.repository;
        const held = t('notifications.supervisor.held', { count: each.window ?? OVERRULE_WINDOW.default });
        return (
          <div key={each.scope} className="flex flex-wrap items-center justify-between gap-2">
            <p className="m-0 min-w-0 text-small text-ink-soft">
              {each.dormant
                ? t('notifications.supervisor.dormant', { where })
                : each.since
                  ? t('notifications.supervisor.line', { where, held, since: moment(each.since), count: each.answers })
                  : t('notifications.supervisor.lineUndated', { where, held, count: each.answers })}
            </p>
            <Button disabled={busy} onClick={() => onTurnOff(each)}>{t('notifications.supervisor.off')}</Button>
          </div>
        );
      })}
      {refused && <Refused sentence={refused} />}
    </section>
  );
}

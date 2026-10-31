import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { moment } from '../../locales/format';
import type { AppointmentNow } from '../../person/standing';

/**
 * The steward's line under This Daoris's *Roles* (COMPANYVIEW1e): where it stands in one sentence, and a door to Agents → Roles →
 * Appointed roles, where it is appointed. It appoints nothing itself.
 *
 * A molecule: drawn from props; the application holds the door.
 */
export function StewardDoor({ appointment, acts, onOpen }: { appointment: AppointmentNow | null; acts: number; onOpen?: () => void }) {
  const { t } = useTranslation();
  const id = useId();
  const status = !appointment
    ? t('settings.daoris.steward.off')
    : appointment.dormant
      ? t('settings.daoris.steward.dormant')
      : t('settings.daoris.steward.appointed', {
        since: appointment.since ? moment(appointment.since) : t('settings.daoris.steward.thisRun'),
        count: acts,
      });
  return (
    <div role="group" aria-labelledby={`${id}-label`} data-steward-row="" className="grid gap-1.5 rounded-control border border-line px-3 py-2">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p id={`${id}-label`} className="m-0 text-body font-medium text-ink">{t('settings.daoris.steward.label')}</p>
          <p className="m-0 text-small text-ink-faint">{t('settings.daoris.steward.hint')}</p>
        </div>
        {onOpen && <Button variant="ghost" onClick={onOpen}>{t('agents.roles.appointed.open')}</Button>}
      </div>
      <p data-steward-status="" className="m-0 text-small text-ink-soft">{status}</p>
    </div>
  );
}

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, SettingRow } from '../../design/ui';
import { moment } from '../../locales/format';
import type { AppointmentNow } from '../../person/standing';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';

/** The supervisor's appointment for one repository, as Setup → Driving draws it (SUPERVISOR1a-ii, D208; the supervisor design §5, §6.4). */
export type SupervisorSetup = {
  /** Whether this build offers appointments: until then the row says it is not offered yet, and nothing appoints. */
  offered: boolean;
  /** The record's appointment for this repository, live or dormant; null where none. */
  appointment: AppointmentNow | null;
  /** That the window started the holder's terminal at the last *Appoint* of this page. */
  started?: boolean;
  /** A press on its way. */
  busy?: boolean;
  /** What *Turn off* was refused with, in words. */
  refused?: string | null;
  /** *Appoint* in the ask: the window starts the holder's terminal, and the ask is told how it ended. */
  onAppoint: (answered: Answered) => void;
  onTurnOff: () => void;
};

/**
 * The supervisor's row in a repository's Setup → Driving (SUPERVISOR1a-ii, D208; the supervisor design §5, §6.4): off, *Appoint…*;
 * appointed, since when, held by the terminal Daoris started at the appointment and how many answers this run, with *Appoint again…*
 * and *Turn off*; found as Daoris started, dormant until *Appoint again*, never said as how it was written. *Appoint…* and *Appoint
 * again…* open one inline ask naming the scope, with its plain sentences; *Appoint* there is the owner's own press, and the window then
 * starts the holder's terminal with the key in its environment. **The key is never shown, copied or sent here**: this row never holds
 * one. *Turn off* asks nothing.
 *
 * A molecule: drawn from props; the application holds the presses.
 */
export function SupervisorRow({ repository, supervisor, open: opened = false }: {
  repository: string;
  supervisor: SupervisorSetup;
  /** The ask drawn open, as a story draws it. */
  open?: boolean;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(opened);
  const { appointment, offered } = supervisor;
  const status = !appointment
    ? t(offered ? 'projects.supervisor.off' : 'projects.supervisor.notOffered')
    : appointment.dormant
      ? t('projects.supervisor.dormant')
      : t('projects.supervisor.appointed', {
        since: appointment.since ? moment(appointment.since) : t('projects.supervisor.thisRun'),
        count: appointment.answers,
      });
  return (
    <SettingRow
      label={t('projects.supervisor.label')}
      hint={t('projects.supervisor.hint', { repository })}
      control={(
        <div className="flex flex-wrap gap-2">
          {offered && !asking && (
            <Button disabled={supervisor.busy} onClick={() => setAsking(true)}>
              {t(appointment ? 'projects.supervisor.again' : 'projects.supervisor.appoint')}
            </Button>
          )}
          {appointment && (
            <Button variant="ghost" disabled={supervisor.busy} onClick={supervisor.onTurnOff}>{t('projects.supervisor.turnOff')}</Button>
          )}
        </div>
      )}
    >
      <p data-supervisor-status="" className="m-0 text-small text-ink-soft">{status}</p>
      {supervisor.started && appointment && !appointment.dormant && (
        <p role="status" className="m-0 text-small text-ink-soft">{t('projects.supervisor.started')}</p>
      )}
      {supervisor.refused && <Refused sentence={supervisor.refused} />}
      {asking && offered && (
        <InlineConfirm
          tone="warn"
          block
          busy={supervisor.busy}
          label={t('projects.supervisor.ask.label', { repository })}
          meanIt={t('projects.supervisor.ask.appoint')}
          says={(
            <div data-supervisor-ask="" className="grid gap-1.5 text-small text-ink">
              <p className="m-0 font-medium">{t('projects.supervisor.ask.scope', { repository })}</p>
              <p className="m-0">{t('projects.supervisor.ask.answers')}</p>
              <p className="m-0">{t('projects.supervisor.ask.never')}</p>
              <p className="m-0">{t('projects.supervisor.ask.recorded')}</p>
              <p className="m-0">{t('projects.supervisor.ask.holder')}</p>
              <p className="m-0">{t('projects.supervisor.ask.admits')}</p>
              <p className="m-0">{t('projects.supervisor.ask.terminal')}</p>
              <p className="m-0">{t('projects.supervisor.ask.limit')}</p>
              <p className="m-0">{t('projects.supervisor.ask.restart')}</p>
            </div>
          )}
          onConfirm={supervisor.onAppoint}
          onClose={() => setAsking(false)}
        />
      )}
    </SettingRow>
  );
}

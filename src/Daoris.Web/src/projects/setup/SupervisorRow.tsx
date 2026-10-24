import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, ChoiceRow, FormField, SettingRow } from '../../design/ui';
import { moment } from '../../locales/format';
import { type AppointmentNow, type HolderNow, inWindow, WINDOW } from '../../person/standing';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';

/** The supervisor's appointment for one repository or one workspace, as its Setup draws it (SUPERVISOR1a-ii, 1e, D208; the supervisor design §5, §6.4). */
export type SupervisorSetup = {
  /** Whether this build offers appointments: until then the row says it is not offered yet, and nothing appoints. */
  offered: boolean;
  /** The record's appointment for this scope, live or dormant; null where none. */
  appointment: AppointmentNow | null;
  /**
   * A repository's row only (SUPERVISOR1e, §5): its workspace's appointment, which answers for it while it has none of its own. The
   * repository's own appointment, once made, wins over it.
   */
  covering?: AppointmentNow | null;
  /** That the window started the holder's terminal at the last *Appoint* of this page. */
  started?: boolean;
  /** A press on its way. */
  busy?: boolean;
  /** What *Turn off* was refused with, in words. */
  refused?: string | null;
  /**
   * The holders living this run (STEWARDROLE1a-i, D209 §2.2): while one lives, the ask offers adding the role to its terminal or a
   * terminal of its own, neither preselected.
   */
  holders?: HolderNow[];
  /**
   * *Appoint* in the ask: the window starts the holder's terminal, or adds the role to the one chosen, and the ask is told how it
   * ended. `share` is the ask's choice while a holder lives: `own`, or that holder's id; `window` the minutes its answers are held
   * for the owner's overrule, 1 to 60 (SUPERVISOR1e).
   */
  onAppoint: (answered: Answered, share: string | undefined, window: number) => void;
  onTurnOff: () => void;
};

/**
 * The supervisor's row in a repository's or a workspace's Setup (SUPERVISOR1a-ii, SUPERVISOR1e, D208; the supervisor design §5, §6.4):
 * off, *Appoint…*; appointed, since when, held by the terminal Daoris started at the appointment, how many answers this run and how
 * long its answers are held for the owner's overrule, with *Appoint again…* and *Turn off*; found as Daoris started, dormant until
 * *Appoint again*, never said as how it was written. A repository with none of its own says the workspace's that covers it.
 * *Appoint…* and *Appoint again…* open one inline ask naming the scope, with its plain sentences and the overrule window, 1 to 60
 * minutes, five unless the owner sets another; *Appoint* there is the owner's own press, and the window then starts the holder's
 * terminal with the key in its environment. **The key is never shown, copied or sent here**: this row never holds one. *Turn off*
 * asks nothing.
 *
 * A molecule: drawn from props; the application holds the presses.
 */
export function SupervisorRow({ repository, workspace, supervisor, open: opened = false }: {
  /** The repository the row appoints for; or, with `workspace`, none. */
  repository?: string;
  /** The workspace the row appoints for, every repository of it (SUPERVISOR1e). */
  workspace?: string;
  supervisor: SupervisorSetup;
  /** The ask drawn open, as a story draws it. */
  open?: boolean;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(opened);
  const [share, setShare] = useState<string | null>(null);
  const [minutes, setMinutes] = useState(String(WINDOW.standing));
  const shareId = useId();
  const { appointment, offered } = supervisor;
  const covering = workspace ? null : supervisor.covering ?? null;
  const holders = supervisor.holders ?? [];
  const held = Number(minutes);
  const windowed = minutes.trim() !== '' && inWindow(held);
  const ofWorkspace = Boolean(workspace);
  const status = !appointment
    ? covering && offered
      ? covering.dormant
        ? t('projects.supervisor.coveredDormant', { workspace: covering.workspace })
        : t('projects.supervisor.covered', { workspace: covering.workspace, count: covering.window })
      : t(offered ? 'projects.supervisor.off' : 'projects.supervisor.notOffered')
    : appointment.dormant
      ? t('projects.supervisor.dormant')
      : t('projects.supervisor.appointed', {
        since: appointment.since ? moment(appointment.since) : t('projects.supervisor.thisRun'),
        count: appointment.answers,
      });
  const close = () => {
    setAsking(false);
    setShare(null);
    setMinutes(String(WINDOW.standing));
  };
  return (
    <SettingRow
      label={t('projects.supervisor.label')}
      hint={ofWorkspace ? t('projects.supervisor.hintWorkspace', { workspace }) : t('projects.supervisor.hint', { repository })}
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
      {appointment && (
        <p data-supervisor-window="" className="m-0 text-small text-ink-soft">
          {t('projects.supervisor.window', { count: appointment.window })}
        </p>
      )}
      {supervisor.started && appointment && !appointment.dormant && (
        <p role="status" className="m-0 text-small text-ink-soft">{t('projects.supervisor.started')}</p>
      )}
      {supervisor.refused && <Refused sentence={supervisor.refused} />}
      {asking && offered && (
        <InlineConfirm
          tone="warn"
          block
          busy={supervisor.busy}
          ready={windowed && (holders.length === 0 || share !== null)}
          label={ofWorkspace
            ? t('projects.supervisor.ask.labelWorkspace', { workspace })
            : t('projects.supervisor.ask.label', { repository })}
          meanIt={t('projects.supervisor.ask.appoint')}
          says={(
            <div data-supervisor-ask="" className="grid gap-1.5 text-small text-ink">
              <p className="m-0 font-medium">
                {ofWorkspace
                  ? t('projects.supervisor.ask.scopeWorkspace', { workspace })
                  : t('projects.supervisor.ask.scope', { repository })}
              </p>
              <p className="m-0">{t('projects.supervisor.ask.answers')}</p>
              <p className="m-0">{t('projects.supervisor.ask.never')}</p>
              <p className="m-0">{t('projects.supervisor.ask.recorded', { count: windowed ? held : WINDOW.standing })}</p>
              <p className="m-0">{t('projects.supervisor.ask.holder')}</p>
              <p className="m-0">{t('projects.supervisor.ask.admits')}</p>
              <p className="m-0">{t('projects.supervisor.ask.terminal')}</p>
              <p className="m-0">{t('projects.supervisor.ask.limit')}</p>
              <p className="m-0">{t('projects.supervisor.ask.restart')}</p>
            </div>
          )}
          onConfirm={(answered) => supervisor.onAppoint(answered, holders.length > 0 ? share ?? undefined : undefined, held)}
          onClose={close}
        >
          <div data-supervisor-window-field="" className="grid gap-1 text-small text-ink">
            <FormField label={t('projects.supervisor.ask.windowField', { shortest: WINDOW.shortest, longest: WINDOW.longest })}>
              <input
                type="number"
                inputMode="numeric"
                min={WINDOW.shortest}
                max={WINDOW.longest}
                step={1}
                value={minutes}
                disabled={supervisor.busy}
                aria-invalid={!windowed}
                onChange={(event) => setMinutes(event.target.value)}
                className="w-24 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
              />
            </FormField>
            {!windowed && (
              <p role="alert" className="m-0 text-small text-ink-soft">
                {t('projects.supervisor.ask.windowBounds', { shortest: WINDOW.shortest, longest: WINDOW.longest })}
              </p>
            )}
          </div>
          {holders.length > 0 && (
            <div data-supervisor-share="" className="grid gap-1.5 text-small text-ink">
              <p id={`${shareId}-legend`} className="m-0 font-medium">{t('projects.supervisor.ask.share.legend')}</p>
              <p className="m-0">{t('projects.supervisor.ask.share.means')}</p>
              <div role="radiogroup" aria-labelledby={`${shareId}-legend`} className="grid gap-1.5">
                {holders.map((holder) => (
                  <ChoiceRow
                    key={holder.id}
                    name={shareId}
                    value={holder.id}
                    checked={share === holder.id}
                    onChange={() => setShare(holder.id)}
                    labelledBy={`${shareId}-${holder.id}`}
                  >
                    <span id={`${shareId}-${holder.id}`}>
                      {t('projects.supervisor.ask.share.add', { time: holder.since ? moment(holder.since) : t('projects.supervisor.thisRun') })}
                    </span>
                  </ChoiceRow>
                ))}
                <ChoiceRow name={shareId} value="own" checked={share === 'own'} onChange={() => setShare('own')} labelledBy={`${shareId}-own`}>
                  <span id={`${shareId}-own`}>{t('projects.supervisor.ask.share.own')}</span>
                </ChoiceRow>
              </div>
            </div>
          )}
        </InlineConfirm>
      )}
    </SettingRow>
  );
}

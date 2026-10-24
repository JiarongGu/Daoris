import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, ChoiceRow, SettingRow } from '../../design/ui';
import { list, moment } from '../../locales/format';
import { type AppointmentNow, type HolderNow, OVERRULE_WINDOW, windowAllowed } from '../../person/standing';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';

/**
 * The supervisor's appointment for one repository, as Setup → Driving draws it, or for one workspace, as its Setup draws it
 * (SUPERVISOR1a-ii, SUPERVISOR1e, D208; the supervisor design §5, §6.4).
 */
export type SupervisorSetup = {
  /** Whether this build offers appointments: until then the row says it is not offered yet, and nothing appoints. */
  offered: boolean;
  /** The record's appointment for this scope, live or dormant; null where none. */
  appointment: AppointmentNow | null;
  /**
   * A repository's row only (SUPERVISOR1e, §5): its workspace's appointment, which answers here while this repository has none of its
   * own; one made here wins over it, and a decision it holds here then goes stale.
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
   * ended. `share` is the ask's choice while a holder lives: `own`, or that holder's id; `window` the overrule window in minutes the
   * owner set in the ask, 1 to 60 (SUPERVISOR1e).
   */
  onAppoint: (answered: Answered, share: string | undefined, window: number) => void;
  onTurnOff: () => void;
};

/**
 * The scope a row appoints for (SUPERVISOR1e): one repository, or one workspace with the repositories the registry places in it now
 * (one added later is covered too, which the ask says).
 */
export type SupervisorScope = { repository: string } | { workspace: string; repositories: string[] };

/**
 * The supervisor's row in a repository's Setup → Driving, or a workspace's Setup (SUPERVISOR1a-ii, SUPERVISOR1e, D208; the supervisor
 * design §5, §6.4): off, *Appoint…*;
 * appointed, since when, held by the terminal Daoris started at the appointment and how many answers this run, with *Appoint again…*
 * and *Turn off*; found as Daoris started, dormant until *Appoint again*, never said as how it was written. *Appoint…* and *Appoint
 * again…* open one inline ask naming the scope, with its plain sentences and the overrule window in minutes, 1 to 60, the record's or
 * five; *Appoint* there is the owner's own press, and the window then starts the holder's terminal with the key in its environment.
 * **The key is never shown, copied or sent here**: this row never holds one. *Turn off* asks nothing. A repository its workspace's
 * appointment covers says so, and that one made here wins over it.
 *
 * A molecule: drawn from props; the application holds the presses.
 */
export function SupervisorRow({ supervisor, open: opened = false, ...scope }: SupervisorScope & {
  supervisor: SupervisorSetup;
  /** The ask drawn open, as a story draws it. */
  open?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const [asking, setAsking] = useState(opened);
  const [share, setShare] = useState<string | null>(null);
  const shareId = useId();
  const windowId = useId();
  const { appointment, offered } = supervisor;
  const covering = 'repository' in scope && !appointment ? supervisor.covering ?? null : null;
  const found = appointment?.window ?? covering?.window ?? null;
  const unset = String(appointment?.window ?? OVERRULE_WINDOW.default);
  const [minutes, setMinutes] = useState(unset);
  const window = Number(minutes);
  const windowSet = minutes.trim() !== '' && windowAllowed(window);
  const holders = supervisor.holders ?? [];
  const words = 'repository' in scope
    ? {
        hint: t('projects.supervisor.hint', { repository: scope.repository }),
        label: t('projects.supervisor.ask.label', { repository: scope.repository }),
        scope: t('projects.supervisor.ask.scope', { repository: scope.repository }),
      }
    : {
        hint: t('projects.supervisor.workspace.hint', { workspace: scope.workspace }),
        label: t('projects.supervisor.workspace.label', { workspace: scope.workspace }),
        scope: scope.repositories.length > 0
          ? t('projects.supervisor.workspace.scope', { workspace: scope.workspace, repositories: list(scope.repositories, i18n.language) })
          : t('projects.supervisor.workspace.scopeNone', { workspace: scope.workspace }),
      };
  const status = !appointment
    ? covering
      ? t(covering.dormant ? 'projects.supervisor.coveredDormant' : 'projects.supervisor.covered', { workspace: covering.workspace ?? '' })
      : t(offered ? 'projects.supervisor.off' : 'projects.supervisor.notOffered')
    : appointment.dormant
      ? t('projects.supervisor.dormant')
      : t('projects.supervisor.appointed', {
        since: appointment.since ? moment(appointment.since) : t('projects.supervisor.thisRun'),
        count: appointment.answers,
      });
  return (
    <SettingRow
      label={t('projects.supervisor.label')}
      hint={words.hint}
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
      {found !== null && (
        <p data-supervisor-window="" className="m-0 text-small text-ink-soft">{t('projects.supervisor.window.held', { count: found })}</p>
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
          ready={windowSet && (holders.length === 0 || share !== null)}
          label={words.label}
          meanIt={t('projects.supervisor.ask.appoint')}
          says={(
            <div data-supervisor-ask="" className="grid gap-1.5 text-small text-ink">
              <p className="m-0 font-medium">{words.scope}</p>
              {covering && <p className="m-0">{t('projects.supervisor.ask.wins', { workspace: covering.workspace ?? '' })}</p>}
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
          onConfirm={(answered) => supervisor.onAppoint(answered, holders.length > 0 ? share ?? undefined : undefined, window)}
          onClose={() => {
            setAsking(false);
            setShare(null);
            setMinutes(unset);
          }}
        >
          <div data-supervisor-window-field="" className="grid gap-1 text-small text-ink">
            <label htmlFor={windowId} className="font-medium">{t('projects.supervisor.window.label')}</label>
            <div className="flex items-center gap-2">
              {/* Sized to what it holds, one or two digits: the owner's own number, 1 to 60, never 0 (the design's §12). */}
              <input
                id={windowId}
                type="number"
                min={OVERRULE_WINDOW.least}
                max={OVERRULE_WINDOW.most}
                step={1}
                value={minutes}
                disabled={supervisor.busy}
                aria-describedby={`${windowId}-means`}
                onChange={(event) => setMinutes(event.target.value)}
                className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
              />
              <span>{t('projects.supervisor.window.unit', { count: windowSet ? window : OVERRULE_WINDOW.default })}</span>
            </div>
            <p id={`${windowId}-means`} className={`m-0 ${windowSet ? 'text-ink-soft' : 'text-warn'}`}>
              {t(windowSet ? 'projects.supervisor.window.means' : 'projects.supervisor.window.bounds', {
                least: OVERRULE_WINDOW.least, most: OVERRULE_WINDOW.most,
              })}
            </p>
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

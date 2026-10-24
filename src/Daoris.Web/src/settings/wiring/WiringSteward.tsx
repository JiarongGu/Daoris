import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, ChoiceRow } from '../../design/ui';
import { moment } from '../../locales/format';
import { type AppointmentNow, type HolderNow, stewardActKnown } from '../../person/standing';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';

/** The six the steward never does (the steward design §3.2), a line each, in that order. */
const NEVER = ['goAhead', 'signIn', 'access', 'publish', 'spend', 'delete'] as const;

/** Its bounds, its holder and what that does not close, its record and its end (§5.3), a line each, in that order. */
const BOUNDS = ['pushes', 'checkout', 'yours', 'registered', 'holder', 'limit', 'recorded', 'restart'] as const;

/** The steward's appointment as This Daoris's *Roles* draws it (STEWARDROLE1b, D209; the steward design §5.3, §2.3). */
export type StewardSetup = {
  /** The record's steward appointment, live or dormant; null where none. */
  appointment: AppointmentNow | null;
  /** The acts this build offers, in its order: the ask names these, never the record's `acts`. */
  offered: readonly string[];
  /** How many acts the steward made this run. */
  acts: number;
  /** The holders living this run: while one lives, the ask offers adding the steward to its terminal or one of its own. */
  holders: readonly HolderNow[];
  /** That the window started the holder's terminal at the last *Appoint* here. */
  started?: boolean;
  /** A press on its way. */
  busy?: boolean;
  /** What *Turn off* was refused with, in words. */
  refused?: string | null;
  /** *Appoint* in the ask: its set-up tick and, while a holder lives, the choice (`own` or that holder's id). */
  onAppoint: (answered: Answered, setup: boolean, share?: string) => void;
  onTurnOff: () => void;
};

/**
 * The steward's row under *Roles* (STEWARDROLE1b, D209; the steward design §5.3): not appointed, *Appoint…*; appointed, since when and
 * its acts this run, **Turn off**; found as Daoris started, dormant, *Appoint again…* and **Turn off**. *Appoint…* and *Appoint again…*
 * open one ordinary ask: a line per act this build offers, the six it never does, its bounds, its holder and what that does not close,
 * its record and its end at a restart, the set-up tick **always shown off** (never read from the record, §2.3), and while a holder
 * lives the choice of terminal, neither preselected. *Turn off* asks nothing (§5.4). The key is never on the page.
 *
 * A molecule: drawn from props; the domain holds the presses (`WiringDomain`).
 */
export function WiringSteward({ steward, open: opened = false }: {
  steward: StewardSetup;
  /** The ask drawn open, as a story draws it. */
  open?: boolean;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(opened);
  const [setup, setSetup] = useState(false);
  const [share, setShare] = useState<string | null>(null);
  const id = useId();
  const { appointment, holders } = steward;
  const offered = steward.offered.filter(stewardActKnown);

  const open = () => {
    // The ordinary ask each time: the tick off and no terminal chosen, whatever the record holds.
    setSetup(false);
    setShare(null);
    setAsking(true);
  };
  const status = !appointment
    ? t('settings.daoris.steward.off')
    : appointment.dormant
      ? t('settings.daoris.steward.dormant')
      : t('settings.daoris.steward.appointed', {
        since: appointment.since ? moment(appointment.since) : t('settings.daoris.steward.thisRun'),
        count: steward.acts,
      });

  return (
    <div role="group" aria-labelledby={`${id}-label`} data-steward-row="" className="grid gap-1.5 rounded-control border border-line px-3 py-2">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p id={`${id}-label`} className="m-0 text-body font-medium text-ink">{t('settings.daoris.steward.label')}</p>
          <p className="m-0 text-small text-ink-faint">{t('settings.daoris.steward.hint')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {(!appointment || appointment.dormant) && !asking && (
            <Button disabled={steward.busy} onClick={open}>
              {t(appointment ? 'settings.daoris.steward.again' : 'settings.daoris.steward.appoint')}
            </Button>
          )}
          {appointment && (
            <Button variant="ghost" disabled={steward.busy} onClick={steward.onTurnOff}>{t('settings.daoris.steward.turnOff')}</Button>
          )}
        </div>
      </div>
      <p data-steward-status="" className="m-0 text-small text-ink-soft">{status}</p>
      {steward.started && appointment && !appointment.dormant && (
        <p role="status" className="m-0 text-small text-ink-soft">{t('settings.daoris.steward.started')}</p>
      )}
      {steward.refused && <Refused sentence={steward.refused} />}
      {asking && (
        <InlineConfirm
          tone="warn"
          block
          busy={steward.busy}
          ready={holders.length === 0 || share !== null}
          label={t('settings.daoris.steward.ask.label')}
          meanIt={t('settings.daoris.steward.ask.appoint')}
          className="min-w-0"
          says={(
            <div data-steward-ask="" className="grid gap-1.5 text-small text-ink">
              <p className="m-0 font-medium">{t('settings.daoris.steward.ask.scope')}</p>
              <p className="m-0">{t('settings.daoris.steward.ask.does')}</p>
              <ul className="m-0 grid list-disc gap-0.5 pl-5">
                {offered.map((act) => <li key={act}>{t(`settings.daoris.steward.act.${act}`)}</li>)}
              </ul>
              <p className="m-0">{t('settings.daoris.steward.ask.never')}</p>
              <ul className="m-0 grid list-disc gap-0.5 pl-5">
                {NEVER.map((never) => <li key={never}>{t(`settings.daoris.steward.never.${never}`)}</li>)}
              </ul>
              {BOUNDS.map((bound) => <p key={bound} className="m-0">{t(`settings.daoris.steward.ask.${bound}`)}</p>)}
            </div>
          )}
          onConfirm={(answered) => steward.onAppoint(answered, setup, holders.length > 0 ? share ?? undefined : undefined)}
          onClose={() => {
            setAsking(false);
            setSetup(false);
            setShare(null);
          }}
        >
          <div className="grid gap-2">
            <CheckField
              checked={setup}
              disabled={steward.busy}
              onChange={setSetup}
              label={t('settings.daoris.steward.ask.setup')}
              className="whitespace-normal text-small"
            />
            {holders.length > 0 && (
              <div data-steward-share="" className="grid gap-1.5 text-small text-ink">
                <p id={`${id}-legend`} className="m-0 font-medium">{t('settings.daoris.steward.ask.share.legend')}</p>
                <p className="m-0">{t('projects.supervisor.ask.share.means')}</p>
                <div role="radiogroup" aria-labelledby={`${id}-legend`} className="grid gap-1.5">
                  {holders.map((holder) => (
                    <ChoiceRow
                      key={holder.id}
                      name={`${id}-share`}
                      value={holder.id}
                      checked={share === holder.id}
                      onChange={() => setShare(holder.id)}
                      labelledBy={`${id}-${holder.id}`}
                    >
                      <span id={`${id}-${holder.id}`}>
                        {t('projects.supervisor.ask.share.add', { time: holder.since ? moment(holder.since) : t('settings.daoris.steward.thisRun') })}
                      </span>
                    </ChoiceRow>
                  ))}
                  <ChoiceRow name={`${id}-share`} value="own" checked={share === 'own'} onChange={() => setShare('own')} labelledBy={`${id}-own`}>
                    <span id={`${id}-own`}>{t('projects.supervisor.ask.share.own')}</span>
                  </ChoiceRow>
                </div>
              </div>
            )}
          </div>
        </InlineConfirm>
      )}
    </div>
  );
}

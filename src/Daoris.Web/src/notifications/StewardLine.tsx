import { useId, useState } from 'react';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { clockOf } from '../locales/format';
import { type DeskRequestNow, type StandingConfirmationNow, type StewardActNow, stewardActKnown, stewardReverse } from '../person/standing';
import { Refused } from '../work/InlineConfirm';

/** An act's target in words: its repository, or its quest and session, as the act names them. */
function targetOf(t: TFunction, act: StewardActNow): string {
  if (act.repository) return act.repository;
  if (act.quest && act.session) return t('notifications.steward.target.session', { quest: act.quest, session: act.session });
  if (act.quest) return t('notifications.steward.target.quest', { quest: act.quest });
  if (act.session) return t('notifications.steward.target.sessionOnly', { session: act.session });
  return '';
}

/** One act's line: what it did, to what, when, the mark, and the owner's reverse press while it stands. */
function ActLine({ act, busy, onReverse }: { act: StewardActNow; busy: boolean; onReverse: (act: StewardActNow) => void }) {
  const { t, i18n } = useTranslation();
  const press = stewardReverse(act);
  return (
    <li className="flex flex-wrap items-center justify-between gap-2 text-small">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
        <span data-steward-act="" className="text-ink">
          {stewardActKnown(act.act) ? t(`notifications.steward.did.${act.act}`) : act.act}
        </span>
        <span className="text-ink-soft">{targetOf(t, act)}</span>
        <time dateTime={act.at} className="text-ink-faint">{clockOf(act.at, undefined, i18n.language)}</time>
        <span className="text-ink-faint">{t('notifications.steward.mark')}</span>
      </span>
      {press && (
        <Button variant="ghost" disabled={busy} onClick={() => onReverse(act)}>{t(`notifications.steward.reverse.${press}`)}</Button>
      )}
    </li>
  );
}

/**
 * One request at the steward's desk (STEWARDROLE1c): the act it asks for in words, on what, when, the caller's why as written with the
 * mark that it is their words and grants nothing, and where it stands: waiting, answered by the steward's act, or declined with the
 * steward's reason, also as written.
 */
function DeskLine({ request }: { request: DeskRequestNow }) {
  const { t, i18n } = useTranslation();
  return (
    <li data-desk-request="" className="grid gap-0.5 text-small">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
        <span className="text-ink">
          {stewardActKnown(request.act) ? t(`notifications.steward.desk.act.${request.act}`) : request.act}
        </span>
        <span className="text-ink-soft">{request.target}</span>
        <time dateTime={request.filed} className="text-ink-faint">{clockOf(request.filed, undefined, i18n.language)}</time>
        <span data-desk-state={request.state} className="text-ink-faint">{t(`notifications.steward.desk.state.${request.state}`)}</span>
      </span>
      <q data-desk-why="" className="m-0 min-w-0 break-words text-ink-soft">{request.why}</q>
      <span className="text-ink-faint">{t('notifications.steward.desk.claims')}</span>
      {request.state === 'declined' && request.reason && (
        <span className="min-w-0 break-words text-ink-soft">{t('notifications.steward.desk.reason', { reason: request.reason })}</span>
      )}
    </li>
  );
}

/**
 * The steward's line in *Notifications* (STEWARDROLE1b, D209; the steward design §5.3, §5.4, §8): at the head of the presses' group
 * beside the standing confirmation's and the supervisor's, where the build offers the steward and it is appointed or acted this run.
 * *Steward appointed · n acts since Daoris started* with **Turn off**, at once and with no ask, opening to the run's acts newest first:
 * each its act in words, its target, its time and the mark *By the steward, on your behalf*, and only while it stands, the owner's
 * own reverse press (§5.4), none for a finish or a Try again. A stop whose quest still waits on a *Try again* is said above them as
 * *Stopped by the steward*, with its press, as *What needs you*'s `steward-stopped` row also says it. **A line, not a row**: no notice
 * per act, so it counts toward no group's count and never the bell's.
 *
 * Under it, the steward's desk (STEWARDROLE1c, §6.2): what any caller asked the steward to do this run, *n waiting*, folded until
 * opened, each request its caller's words, which grant nothing, and how the steward closed it. It offers no press: the owner's own
 * presses are elsewhere, and a request is the steward's to answer.
 *
 * A molecule: drawn from props; the application holds the presses.
 */
export function StewardLine({ standing, busy = false, refused = null, onTurnOff, onReverse }: {
  standing: StandingConfirmationNow | null;
  /** A press of the line on its way. */
  busy?: boolean;
  /** What a press was refused with, in words, said under the line. */
  refused?: string | null;
  onTurnOff: () => void;
  /** The owner's reverse press for this act, the route `stewardReverse` names. */
  onReverse: (act: StewardActNow) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [deskOpen, setDeskOpen] = useState(false);
  const id = useId();
  if (!standing?.stewardOffered) return null;
  const appointment = standing.appointments.find((each) => each.role === 'steward') ?? null;
  const acts = standing.stewardActs;
  const desk = standing.stewardDesk;
  if (!appointment && acts.length === 0 && desk.length === 0) return null;
  const deskWaiting = desk.filter((each) => each.state === 'open').length;
  const waiting = acts.filter((each) => each.act === 'stop' && each.waiting && stewardReverse(each) === 'retry');
  const said = !appointment
    ? t('notifications.steward.lineOff', { count: acts.length })
    : appointment.dormant
      ? t('notifications.steward.dormant')
      : t('notifications.steward.line', { count: acts.length });
  return (
    <section data-steward-line="" aria-label={t('notifications.steward.label')} className="grid gap-2 rounded-control border border-line border-l-[3px] border-l-warn bg-sunken px-3 py-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="m-0 min-w-0 text-small text-ink-soft">{said}</p>
        <div className="flex flex-wrap gap-2">
          {acts.length > 0 && (
            <Button variant="ghost" aria-expanded={open} aria-controls={`${id}-acts`} onClick={() => setOpen((was) => !was)}>
              {t(open ? 'notifications.steward.hide' : 'notifications.steward.show')}
            </Button>
          )}
          {appointment && <Button disabled={busy} onClick={onTurnOff}>{t('notifications.steward.off')}</Button>}
        </div>
      </div>
      {waiting.length > 0 && (
        <div className="grid gap-1">
          <p id={`${id}-stopped`} className="m-0 text-small font-medium text-ink">{t('notifications.steward.stopped')}</p>
          <ul aria-labelledby={`${id}-stopped`} className="m-0 grid list-none gap-1 p-0">
            {waiting.map((each) => (
              <li key={each.id} className="flex flex-wrap items-center justify-between gap-2 text-small">
                <span className="text-ink-soft">{targetOf(t, each)}</span>
                <Button disabled={busy} onClick={() => onReverse(each)}>{t('notifications.steward.reverse.retry')}</Button>
              </li>
            ))}
          </ul>
        </div>
      )}
      {open && (
        <ul id={`${id}-acts`} aria-label={t('notifications.steward.list')} className="m-0 grid list-none gap-1 p-0">
          {acts.map((each) => <ActLine key={each.id} act={each} busy={busy} onReverse={onReverse} />)}
        </ul>
      )}
      {desk.length > 0 && (
        <div className="grid gap-1">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="m-0 min-w-0 text-small text-ink-soft">{t('notifications.steward.desk.line', { count: deskWaiting })}</p>
            <Button variant="ghost" aria-expanded={deskOpen} aria-controls={`${id}-desk`} onClick={() => setDeskOpen((was) => !was)}>
              {t(deskOpen ? 'notifications.steward.desk.hide' : 'notifications.steward.desk.show')}
            </Button>
          </div>
          {deskOpen && (
            <ul id={`${id}-desk`} aria-label={t('notifications.steward.desk.list')} className="m-0 grid list-none gap-2 p-0">
              {desk.map((each) => <DeskLine key={each.id} request={each} />)}
            </ul>
          )}
        </div>
      )}
      {refused && <Refused sentence={refused} />}
    </section>
  );
}

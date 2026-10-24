import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Prose, SettingRow } from '../../design/ui';
import { moment } from '../../locales/format';
import { type StandingConfirmationNow, standingPressList, standingPressWord } from '../../person/standing';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';

/**
 * The standing confirmation's row (TERMTRUST1a, D201; the terminal-trust design §6.1), in This Daoris's *Terminal* card beside the
 * launchers and the address file: its label and hint, *Off* or *On since* with the presses granted, and only those, a record not
 * honoured said under it, and *Turn on…* or *Turn off*.
 *
 * - **Turn on… asks once, under the row** (`InlineConfirm` in its warning tone, as Ask Daoris's full access asks: it loosens what runs
 *   unasked): the design's paragraph and each press this build names as a tick, every one ticked at first; *Turn on* sends the ticked
 *   ones alone and waits in the ask until the driver answers, a refusal said inside it. With none ticked it cannot be pressed.
 * - **Turn off asks nothing** (design §4.3): its refusal, or a revoke whose record could not be written, is said under the row (R3).
 * - **Not offered yet** (D201's note on order): no *Turn on…*, and the row says why; one on anyway still offers *Turn off*.
 * - ***Roles*** (STEWARDROLE1b, D209 §2.4, §5.3): where the build offers the steward, the row is renamed *Roles* and holds the
 *   steward's row (`roles`). D201's grant stays not offered and is not said as a missing feature there; one on anyway still says
 *   so and offers *Turn off*, as before.
 *
 * A molecule: drawn from props; the domain holds the press (`WiringDomain`).
 */
export function WiringStanding({ standing, busy = false, refused = null, unwritten = false, onTurnOn, onTurnOff, roles }: {
  standing: StandingConfirmationNow;
  /** A press of it on its way: both buttons wait. */
  busy?: boolean;
  /** What the last *Turn off* was refused with, in words. */
  refused?: string | null;
  /** That the last *Turn off* turned it off for this run alone, its record unwritten (beside the state's own `unwritten`). */
  unwritten?: boolean;
  /** *Turn on*'s press: the presses ticked, and how to tell the ask how it ended. */
  onTurnOn: (presses: string[], answered: Answered) => void;
  onTurnOff: () => void;
  /** The roles this build offers, the steward's row (`WiringSteward`): drawn under the row, which is then named *Roles*. */
  roles?: ReactNode;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const [ticked, setTicked] = useState<ReadonlySet<string>>(() => new Set(standing.built));

  const open = () => {
    setTicked(new Set(standing.built));
    setAsking(true);
  };
  const tick = (press: string, on: boolean) => setTicked((was) => {
    const next = new Set(was);
    if (on) next.add(press);
    else next.delete(press);
    return next;
  });

  const presses = standingPressList(t, standing.presses);
  const now = standing.on
    ? standing.since
      ? t('settings.daoris.standing.on', { since: moment(standing.since), presses })
      : t('settings.daoris.standing.onUndated', { presses })
    : t('settings.daoris.standing.off');

  // Under *Roles*, D201's own grant is said only while it is on: off and not offered, it is no longer a thing the owner can turn on.
  const grant = !roles || standing.on;
  const notOffered = !standing.offered && !roles;
  return (
    <SettingRow
      label={t(roles ? 'settings.daoris.label.roles' : 'settings.daoris.label.standing')}
      hint={t(roles ? 'settings.daoris.roles.hint' : 'settings.daoris.standing.hint')}
      control={grant && (
        <>
          <span className="text-small text-ink-soft">{now}</span>
          {standing.on
            ? <Button disabled={busy} onClick={onTurnOff}>{t('settings.daoris.button.standingOff')}</Button>
            : standing.offered && !asking && (
              <Button disabled={busy} onClick={open}>{t('settings.daoris.button.standingOn')}</Button>
            )}
        </>
      )}
    >
      {(roles || standing.unread || notOffered || standing.unwritten || unwritten || refused || asking) && (
        <div className="grid gap-1.5">
          {roles}
          {grant && standing.unread && <p className="m-0 text-small text-ink-soft">{t(`settings.daoris.standing.unread.${standing.unread}`)}</p>}
          {notOffered && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.standing.notOffered')}</p>}
          {(standing.unwritten || unwritten) && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.standing.unwritten')}</p>}
          {refused && <Refused sentence={refused} />}
          {asking && (
            <InlineConfirm
              tone="warn"
              block
              label={t('settings.daoris.standing.ask.label')}
              meanIt={t('settings.daoris.button.standingConfirm')}
              busy={busy}
              ready={standing.built.some((press) => ticked.has(press))}
              onConfirm={(answered) => onTurnOn(standing.built.filter((press) => ticked.has(press)), answered)}
              onClose={() => setAsking(false)}
              className="min-w-0"
              says={<Prose className="m-0 text-small text-ink">{t('settings.daoris.standing.ask.says')}</Prose>}
            >
              <ul className="m-0 grid list-none gap-1 p-0">
                {standing.built.map((press) => (
                  <li key={press}>
                    <CheckField
                      checked={ticked.has(press)}
                      disabled={busy}
                      onChange={(on) => tick(press, on)}
                      label={standingPressWord(t, press)}
                      className="whitespace-normal text-small"
                    />
                  </li>
                ))}
              </ul>
            </InlineConfirm>
          )}
        </div>
      )}
    </SettingRow>
  );
}

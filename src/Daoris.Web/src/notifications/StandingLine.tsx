import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { moment } from '../locales/format';
import { type StandingConfirmationNow, standingPressList } from '../person/standing';
import { Refused } from '../work/InlineConfirm';

/**
 * The standing confirmation's line (TERMTRUST1a, D201 point 7; the terminal-trust design §6.2, amending D196's view): at the head of
 * *Notifications*' presses' group while it is on, the presses granted and only those, since when, and how many it covered since Daoris
 * started, with **Turn off** and *Open This Daoris*. **A line, not a row of *What needs you***: nothing waits on the person, so it
 * counts toward no group's count and never the bell's. Nothing while it is off.
 *
 * A molecule: drawn from props; the application holds the press.
 */
export function StandingLine({ standing, busy = false, refused = null, onTurnOff, onOpen }: {
  standing: StandingConfirmationNow | null;
  /** *Turn off* on its way. */
  busy?: boolean;
  /** What *Turn off* was refused with, in words, said under the line. */
  refused?: string | null;
  onTurnOff: () => void;
  /** Settings → This Daoris, at its *Terminal* card. */
  onOpen: () => void;
}) {
  const { t } = useTranslation();
  if (!standing?.on) return null;
  const presses = standingPressList(t, standing.presses);
  const said = standing.since
    ? t('notifications.standing.line', { presses, since: moment(standing.since), count: standing.used })
    : t('notifications.standing.lineUndated', { presses, count: standing.used });
  return (
    <section data-standing-line="" aria-label={t('settings.daoris.label.standing')} className="grid gap-2 rounded-control border border-line border-l-[3px] border-l-warn bg-sunken px-3 py-2">
      <p className="m-0 text-small text-ink-soft">{said}</p>
      <div className="flex flex-wrap gap-2">
        <Button disabled={busy} onClick={onTurnOff}>{t('notifications.standing.off')}</Button>
        <Button variant="ghost" onClick={onOpen}>{t('settings.daoris.button.here')}</Button>
      </div>
      {refused && <Refused sentence={refused} />}
    </section>
  );
}

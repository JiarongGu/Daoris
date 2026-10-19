import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../../data/wiring';
import { Button, Pill, SettingRow } from '../../design/ui';
import { moment } from '../../locales/format';
import { Fact, WiringCard } from './WiringParts';

/** The holders the catalogue words; one it does not (`unknown`) says it is something else. */
const HOLDERS = new Set(['desktop', 'headless', 'verb', 'migration', 'swap']);

/**
 * Driver loop (the this-Daoris design §3.3): who holds the home, when the loop last looked, what holds its starts, and how
 * often it looks, each with the door to Driver, where `driver.json`'s dials are set. Shows; the dials stay in Driver.
 *
 * @remarks
 * A molecule: drawn from props. The holder is the lease's (HOMELEASE1): this window, another window, a terminal's drive, or an
 * act holding the home for its length (a command, a move of its files, an update's swap), with its process and since when.
 */
export function WiringLoop({ loop, onDriver }: { loop: SystemWiring['loop']; onDriver: () => void }) {
  const { t } = useTranslation();
  const { holder } = loop;
  const door = <Button variant="ghost" onClick={onDriver}>{t('settings.daoris.button.driver')}</Button>;

  const who = !holder ? t('settings.daoris.loop.nobody')
    : holder.thisWindow ? t('settings.daoris.loop.thisWindow')
      : t(`settings.daoris.loop.${HOLDERS.has(holder.kind) ? holder.kind : 'unknown'}`);

  return (
    <WiringCard id="settings-daoris-loop" title={t('settings.daoris.card.loop')}>
      <SettingRow
        label={t('settings.daoris.label.holder')}
        hint={holder ? t('settings.daoris.loop.since', { pid: holder.pid, when: moment(holder.since) }) : undefined}
        control={<span className="text-small text-ink-soft">{who}</span>}
      >
        {holder?.what && <p className="m-0 font-mono text-small text-ink-faint">{holder.what}</p>}
      </SettingRow>
      <SettingRow
        label={t('settings.daoris.label.looked')}
        control={<Fact value={loop.looked} why={loop.lookedWhy}><span className="text-small text-ink-soft">{loop.looked ? moment(loop.looked) : ''}</span></Fact>}
      />
      <SettingRow
        label={t('settings.daoris.label.holds')}
        control={(
          <>
            <Fact value={loop.holds} why={loop.holdsWhy}>
              <span className="text-small text-ink-soft">
                {loop.holds ? t('settings.daoris.loop.held', { count: loop.holds }) : t('settings.daoris.loop.noneHeld')}
              </span>
            </Fact>
            {door}
          </>
        )}
      >
        {loop.draining && (
          <p className="m-0 text-small text-ink-soft"><Pill tone="taken">{t('settings.daoris.pill.draining')}</Pill>{' '}{t('settings.daoris.loop.draining')}</p>
        )}
      </SettingRow>
      <SettingRow
        label={t('settings.daoris.label.poll')}
        control={(
          <>
            <Fact value={loop.pollSeconds} why={loop.pollSecondsWhy}>
              <span className="text-small text-ink-soft">{t('settings.daoris.loop.seconds', { count: loop.pollSeconds ?? 0 })}</span>
            </Fact>
            {door}
          </>
        )}
      />
    </WiringCard>
  );
}

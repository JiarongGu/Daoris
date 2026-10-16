import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../data/wiring';
import { Button, PathText, Pill, SettingRow } from '../design/ui';
import { moment, size } from '../locales/format';
import { Door, Fact, WiringCard } from './WiringParts';

/**
 * Logs (the this-Daoris design §3.9): the machine log's folder, its size, the days it keeps, its newest day, with the press
 * that opens the folder and the door to the Machine log; and whether the window's engine log is there.
 *
 * @remarks
 * A molecule: drawn from props. *Open the folder* is the Machine log's own press, kept where it stood (LOG1c); no other press
 * of this page opens a folder (that, and a copied line, are WIRING1e's).
 */
export function WiringLogs({ logs, onOpenFolder, onLog }: {
  logs: SystemWiring['logs'];
  onOpenFolder: () => void;
  onLog: () => void;
}) {
  const { t } = useTranslation();
  return (
    <WiringCard id="settings-daoris-logs" title={t('settings.daoris.card.logs')}>
      <SettingRow
        label={t('settings.daoris.label.logFolder')}
        control={(
          <>
            <Fact value={logs.path} why={logs.pathWhy}><PathText path={logs.path ?? ''} className="text-small text-ink-soft" /></Fact>
            {logs.path && <Button variant="ghost" onClick={onOpenFolder}>{t('settings.logs.open')}</Button>}
          </>
        )}
      />
      <SettingRow
        label={t('settings.daoris.label.logSize')}
        control={<Fact value={logs.bytes} why={logs.bytesWhy}><span className="text-small text-ink-soft">{size(logs.bytes ?? 0)}</span></Fact>}
      />
      <SettingRow
        label={t('settings.daoris.label.logKept')}
        control={<span className="text-small text-ink-soft">{t('settings.daoris.logs.days', { count: logs.days })}</span>}
      />
      <SettingRow
        label={t('settings.daoris.label.logNewest')}
        control={<Fact value={logs.newest} why={logs.newestWhy}><span className="text-small text-ink-soft">{logs.newest ? moment(logs.newest) : ''}</span></Fact>}
      />
      <SettingRow
        label={t('settings.daoris.label.engineLog')}
        hint={t('settings.daoris.logs.engineWhere')}
        control={<Pill tone={logs.engineLog ? 'done' : 'neutral'}>{t(logs.engineLog ? 'settings.daoris.pill.present' : 'settings.daoris.pill.absent')}</Pill>}
      />
      <SettingRow control={<Door label={t('settings.daoris.button.log')} onPress={onLog} />} label={t('settings.daoris.label.machineLog')} />
    </WiringCard>
  );
}

import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../../data/wiring';
import { SettingRow } from '../../design/ui';
import { Door, Fact, WiringCard } from './WiringParts';

/** The rows after the program's, in the order the design's table lists them (§3.6). */
const PARTS = ['kit', 'cli', 'driver', 'service', 'connector', 'dotnet', 'node'] as const;

/**
 * Versions: the program with its build as the release workflow stamped it, the UI kit, the doctrine tool, the driver's verbs,
 * the service and its connector, .NET and Node.js, each its version or why there is none; Node is Tools' to say, so its row
 * carries that door. The update is the card above it on Updates; a development run, which has none, says so here.
 */
export function WiringVersions({ versions, development = false, onTools }: {
  versions: SystemWiring['versions'];
  development?: boolean;
  onTools: () => void;
}) {
  const { t } = useTranslation();
  const program = versions.program
    ? `${versions.program}${versions.build ? ` (${versions.build})` : ''}`
    : null;

  return (
    <WiringCard id="settings-daoris-versions" title={t('settings.daoris.card.versions')}>
      <SettingRow
        label={t('settings.daoris.label.versions.program')}
        control={<Fact value={program} why={versions.programWhy}><span className="text-small text-ink-soft">{program}</span></Fact>}
      />
      {PARTS.map((part) => (
        <SettingRow
          key={part}
          label={t(`settings.daoris.label.versions.${part}`)}
          control={(
            <>
              <Fact value={versions[part]} why={versions[`${part}Why`]}><span className="text-small text-ink-soft">{versions[part]}</span></Fact>
              {part === 'node' && versions.node === null && <Door label={t('settings.daoris.button.tools')} onPress={onTools} />}
            </>
          )}
        />
      ))}
      {development && (
        <SettingRow
          label={t('settings.daoris.label.versions.update')}
          control={<span className="text-small text-ink-soft">{t('settings.daoris.versions.notUpdated')}</span>}
        />
      )}
    </WiringCard>
  );
}

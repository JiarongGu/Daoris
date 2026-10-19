import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../../data/wiring';
import { SettingRow } from '../../design/ui';
import type { UpdateState } from '../../update/UpdateBanner';
import { Door, Fact, WiringCard } from './WiringParts';

/** The rows after the program's, in the order the design's table lists them (§3.6). */
const PARTS = ['kit', 'cli', 'driver', 'service', 'connector', 'dotnet', 'node'] as const;

/**
 * Versions and the update (the this-Daoris design §3.6): the program with its build as the release workflow stamped it, the UI
 * kit, the doctrine tool, the driver's verbs, the service and its connector, .NET and Node.js, each its version or why there
 * is none; Node is Tools' to say, so its row carries that door. The update is one line: what is staged and how the last swap
 * ended, with the door to Driver, where it is taken. A development run has none to take.
 *
 * @remarks
 * A molecule: drawn from props. The update is not in the wiring's answer (it is the shell's `UPDATE_STATE`), so the organism
 * hands it in; nothing until it answers.
 */
export function WiringVersions({ versions, update, development = false, onTools, onDriver }: {
  versions: SystemWiring['versions'];
  update: UpdateState | undefined;
  development?: boolean;
  onTools: () => void;
  onDriver: () => void;
}) {
  const { t } = useTranslation();
  const program = versions.program
    ? `${versions.program}${versions.build ? ` (${versions.build})` : ''}`
    : null;

  const line = (): string | null => {
    if (development) return t('settings.daoris.versions.notUpdated');
    if (!update) return null;
    const standing = update.staged
      ? t('settings.daoris.versions.staged', { version: update.staged.version })
      : t('settings.update.none');
    return update.last ? t('settings.daoris.versions.last', { standing, last: t(`settings.daoris.versions.ended.${update.last.phase}`, { defaultValue: update.last.phase }) }) : standing;
  };
  const updateLine = line();

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
      {updateLine && (
        <SettingRow
          label={t('settings.daoris.label.versions.update')}
          control={<><span className="text-small text-ink-soft">{updateLine}</span><Door label={t('settings.daoris.button.driver')} onPress={onDriver} /></>}
        />
      )}
    </WiringCard>
  );
}

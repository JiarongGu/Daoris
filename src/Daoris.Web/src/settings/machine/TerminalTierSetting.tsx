import { useTranslation } from 'react-i18next';
import { CheckField, SettingRow } from '../../design/ui';
import { type TerminalTierChange, tierSetWords } from '../../person/terminalActs';

/** The tier of a terminal's presses as the shell answers it: `ask`, the default, or `act`. */
export type TerminalTier = 'ask' | 'act';

/** The tier the shell's state names; any other value reads as ask, as the driver reads it. */
export const terminalTierOf = (raw: unknown): TerminalTier | null =>
  raw === 'act' ? 'act' : raw === 'ask' ? 'ask' : null;

/**
 * Whether a press from a terminal on this machine that no Daoris session owns runs at once as the person's, drawn in its key's words
 * (`settings.terminal.*`): the same `terminal` `daoris settings terminal ask|act` sets. The box says what checking it does, the line
 * under it the tier held and who set it and when, as *Notifications* says it. A molecule: the tier and its changes by props, the change out.
 */
export function TerminalTierSetting({ held, changes = [], onSet }: {
  held: TerminalTier;
  /** Who changed the tier, newest first. */
  changes?: TerminalTierChange[];
  onSet: (tier: TerminalTier) => void;
}) {
  const { t, i18n } = useTranslation();
  const set = tierSetWords(t, i18n.language, held === 'act', changes);
  return (
    <SettingRow
      label={t('settings.terminal.label')}
      twin={t('settings.terminal.terminal')}
      why={t('settings.terminal.body')}
      control={<CheckField checked={held === 'act'} onChange={(on) => onSet(on ? 'act' : 'ask')} label={t('settings.terminal.act')} />}
    >
      <p className="m-0 text-small text-ink-soft">{t(held === 'act' ? 'settings.terminal.state.act' : 'settings.terminal.state.ask')}</p>
      {set && <p className="m-0 text-small text-ink-faint">{set}</p>}
    </SettingRow>
  );
}

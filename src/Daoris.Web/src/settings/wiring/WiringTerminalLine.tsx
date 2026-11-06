import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Segmented, SettingRow } from '../../design/ui';
import { TERMINAL_SHELLS, type TerminalFacts, type TerminalShell, terminalLine } from './terminalLine';

/**
 * *Copy a terminal line* (WIRING1e, the this-Daoris design §4), in the Terminal card: the line for PowerShell, Command Prompt or Git
 * Bash, setting `DAORIS_HOME` and `DAORIS_SERVICE_URL` for that terminal only and putting the install's launchers first on its `PATH`.
 * It is shown, so the person reads what they paste; it names this machine's paths and goes nowhere but the clipboard, and changes
 * nothing on the account, so it asks nothing first.
 *
 * @remarks
 * A molecule: drawn from props. `onCopy` is the domain's clipboard.
 */
export function WiringTerminalLine({ facts, onCopy }: { facts: TerminalFacts; onCopy: (line: string) => void }) {
  const { t } = useTranslation();
  const [shell, setShell] = useState<TerminalShell>('powershell');
  const line = terminalLine(shell, facts);
  return (
    <SettingRow
      label={t('settings.daoris.label.terminalLine')}
      hint={t('settings.daoris.setup.line.hint')}
      control={<Button onClick={() => onCopy(line)} disabled={line === ''}>{t('settings.daoris.button.copyLine')}</Button>}
    >
      <div className="grid gap-1.5">
        <Segmented
          label={t('settings.daoris.label.terminalLine')}
          value={shell}
          options={TERMINAL_SHELLS.map((each) => ({ value: each, label: t(`settings.daoris.setup.line.shell.${each}`) }))}
          onChange={setShell}
        />
        <code className="block break-all rounded-control border border-line bg-page/60 px-3 py-2 font-mono text-small text-ink-soft">{line}</code>
      </div>
    </SettingRow>
  );
}

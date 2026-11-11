import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SettingRow } from '../../design/ui';

const FIELD = 'w-[6.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink';

/**
 * The copy bound, the `manifest` setting's third number: how many megabytes a folder may hold for its department's sessions to work
 * apart in copies of it. A row of its own beside the manifest's bounds, in its key's words; written alone, only when it changes to a
 * whole number of at least one, and held to the driver's own refusal above that, which the page says.
 */
export function CopyBoundSetting({ megabytes, onCopy }: {
  /** The megabytes the shell answered: the file's, or the default. */
  megabytes: number;
  onCopy: (megabytes: number) => void;
}) {
  const { t } = useTranslation();
  // Held as text while typed: a number input mid-edit passes through the empty string, which is no value to write.
  const [typed, setTyped] = useState<string | undefined>();
  const leave = () => {
    const value = typed === undefined || typed.trim() === '' ? megabytes : Number(typed);
    setTyped(undefined);
    if (!Number.isInteger(value) || value < 1 || value === megabytes) return;
    onCopy(value);
  };

  return (
    <SettingRow
      label={t('settings.manifest.copyLabel')}
      twin={t('settings.manifest.copyTerminal')}
      why={t('settings.manifest.copyBody')}
      control={(
        <span className="inline-flex items-center justify-end gap-1.5">
          <input
            type="number"
            min={1}
            aria-label={t('settings.manifest.copy')}
            value={typed ?? String(megabytes)}
            onChange={(event) => setTyped(event.target.value)}
            onBlur={leave}
            className={FIELD}
          />
          <span className="text-small text-ink-soft">{t('settings.manifest.copyUnit')}</span>
        </span>
      )}
    />
  );
}

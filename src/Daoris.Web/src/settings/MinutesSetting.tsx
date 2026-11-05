import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type MachineSetting, SETTINGS } from '../bridge/generated/routes';
import { SettingRow } from '../design/ui';

/** A machine setting as the driver's one list declares it. */
export function settingOf(key: string): MachineSetting {
  const setting = SETTINGS.find((each) => each.key === key);
  if (!setting) throw new Error(`no machine setting is named ${key}`);
  return setting;
}

/**
 * A machine setting held in whole minutes (the cool-off, the quiet bound), drawn in its one key's words (`.label`, `.terminal`,
 * `.body`, `.minutes`) and held to its bounds as the list declares them. Passing through is not a write (REV3), and the driver
 * refuses a value out of its bounds, so nothing typed, a value out of them, one not whole, or the one held puts the held value back.
 */
export function MinutesSetting({ setting, held, onSet }: {
  /** The setting's key. */
  setting: string;
  /** The minutes the shell answered: the file's, or the default. */
  held: number;
  onSet: (minutes: number) => void;
}) {
  const { t } = useTranslation();
  const { words, least, most } = settingOf(setting);
  // Held as text while typed: a number input mid-edit passes through the empty string, which is no value to write.
  const [typed, setTyped] = useState<string | null>(null);
  return (
    <SettingRow
      label={t(`${words}.label`)}
      hint={t(`${words}.terminal`)}
      why={t(`${words}.body`)}
      control={(
        <span className="inline-flex items-center gap-1.5">
          <input
            type="number"
            min={least ?? undefined}
            max={most ?? undefined}
            aria-label={t(`${words}.label`)}
            value={typed ?? String(held)}
            onChange={(event) => setTyped(event.target.value)}
            onBlur={() => {
              const value = typed === null || typed.trim() === '' ? held : Number(typed);
              setTyped(null);
              if (!Number.isInteger(value) || value === held) return;
              if ((least !== null && value < least) || (most !== null && value > most)) return;
              onSet(value);
            }}
            className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
          />
          <span className="text-small text-ink-soft">{t(`${words}.minutes`)}</span>
        </span>
      )}
    />
  );
}

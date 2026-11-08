import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SettingRow } from '../../design/ui';

/** What a setting held in minutes declares of itself: its key's words, and its bounds (null where it has none). */
export type MinutesDeclared = { words: string; least: number | null; most: number | null };

/**
 * A machine setting held in whole minutes (the cool-off, the quiet bound), drawn in its one key's words (`.label`, `.terminal`,
 * `.body`, `.minutes`) and held to its bounds as the list declares them. Passing through is not a write, and the driver
 * refuses a value out of its bounds, so nothing typed, a value out of them, one not whole, or the one held puts the held value back.
 */
export function MinutesSetting({ setting, held, onSet }: {
  /** The setting as the driver's list declares it, which the page that draws this reads from the list. */
  setting: MinutesDeclared;
  /** The minutes the shell answered: the file's, or the default. */
  held: number;
  onSet: (minutes: number) => void;
}) {
  const { t } = useTranslation();
  const { words, least, most } = setting;
  // Held as text while typed: a number input mid-edit passes through the empty string, which is no value to write.
  const [typed, setTyped] = useState<string | null>(null);
  return (
    <SettingRow
      label={t(`${words}.label`)}
      twin={t(`${words}.terminal`)}
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

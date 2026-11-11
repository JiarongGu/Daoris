import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SettingRow } from '../../design/ui';
import type { MinutesDeclared } from './MinutesSetting';

const FIELD = 'w-[5.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink';

/**
 * The machine's one `manifest` setting: how many files and how many minutes a folder's manifest read spends before the folder is
 * too large to follow, two numbers on one row in its key's words. Each is written alone, only when it changes to a whole number;
 * the minutes are held to the bounds the driver's list declares, and the files to the driver's own refusal, which the page says.
 */
export function ManifestBoundsSetting({ setting, files, minutes, onFiles, onMinutes }: {
  /** The setting as the driver's list declares it: its words, and the bounds of its minutes. */
  setting: MinutesDeclared;
  /** The files the shell answered: the file's, or the default. */
  files: number;
  /** The minutes the shell answered: the file's, or the default. */
  minutes: number;
  onFiles: (files: number) => void;
  onMinutes: (minutes: number) => void;
}) {
  const { t } = useTranslation();
  const { words, least, most } = setting;
  // Held as text while typed: a number input mid-edit passes through the empty string, which is no value to write.
  const [typed, setTyped] = useState<{ files?: string; minutes?: string }>({});
  const leave = (which: 'files' | 'minutes', held: number, send: (value: number) => void) => {
    const text = typed[which];
    const value = text === undefined || text.trim() === '' ? held : Number(text);
    setTyped({ ...typed, [which]: undefined });
    if (!Number.isInteger(value) || value < 1 || value === held) return;
    if (which === 'minutes' && ((least !== null && value < least) || (most !== null && value > most))) return;
    send(value);
  };

  return (
    <SettingRow
      label={t(`${words}.label`)}
      twin={t(`${words}.terminal`)}
      why={t(`${words}.body`)}
      control={(
        <span className="inline-flex flex-wrap items-center justify-end gap-1.5">
          <input
            type="number"
            min={1}
            aria-label={t(`${words}.files`)}
            value={typed.files ?? String(files)}
            onChange={(event) => setTyped({ ...typed, files: event.target.value })}
            onBlur={() => leave('files', files, onFiles)}
            className={FIELD}
          />
          <span className="text-small text-ink-soft">{t(`${words}.filesUnit`)}</span>
          <input
            type="number"
            min={least ?? undefined}
            max={most ?? undefined}
            aria-label={t(`${words}.minutes`)}
            value={typed.minutes ?? String(minutes)}
            onChange={(event) => setTyped({ ...typed, minutes: event.target.value })}
            onBlur={() => leave('minutes', minutes, onMinutes)}
            className={FIELD}
          />
          <span className="text-small text-ink-soft">{t(`${words}.minutesUnit`)}</span>
        </span>
      )}
    />
  );
}

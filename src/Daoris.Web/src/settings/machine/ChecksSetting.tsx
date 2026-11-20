import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField, SettingRow } from '../../design/ui';
import type { MinutesDeclared } from './MinutesSetting';

/**
 * How many checks the machine runs at once (`checks`): auto, as many as its memory admits, or a pinned number in the setting's bounds.
 * Auto is a switch; off, the number beside it is the one held. A molecule: the held value by props, every change out.
 */
export function ChecksSetting({ setting, held, onSet }: {
  /** The setting as the driver's list declares it: its bounds. */
  setting: MinutesDeclared;
  /** The pinned number, or null for auto. */
  held: number | null;
  /** A change the person made: a whole number in bounds and not the one held, or null for auto. */
  onSet: (checks: number | null) => void;
}) {
  const { t } = useTranslation();
  const least = setting.least ?? 1;
  const most = setting.most ?? 16;
  // The number held as text while typed: mid-edit passes through the empty string, which the setting refuses.
  const [typed, setTyped] = useState<string | null>(null);
  const shown = held ?? 2;

  return (
    <SettingRow
      label={t('settings.checks.label')}
      twin={t('settings.checks.terminal')}
      why={t('settings.checks.body')}
      control={(
        <>
          <CheckField checked={held === null} onChange={(auto) => onSet(auto ? null : shown)} label={t('settings.checks.auto')} />
          <input
            type="number"
            min={least}
            max={most}
            disabled={held === null}
            aria-label={t('settings.checks.label')}
            value={typed ?? String(shown)}
            onChange={(event) => setTyped(event.target.value)}
            onBlur={() => {
              const value = typed === null || typed.trim() === '' ? shown : Number(typed);
              setTyped(null);
              if (!Number.isInteger(value) || value < least || value > most || value === held) return;
              onSet(value);
            }}
            className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink disabled:opacity-50"
          />
        </>
      )}
    >
      <p className="m-0 text-small text-ink-soft">{held === null ? t('settings.checks.state.auto') : t('settings.checks.state.pinned', { count: held })}</p>
    </SettingRow>
  );
}

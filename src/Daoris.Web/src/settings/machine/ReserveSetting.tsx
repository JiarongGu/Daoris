import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField, SettingRow } from '../../design/ui';
import type { MinutesDeclared } from './MinutesSetting';

/** What the reserve is and how the machine stands beside it, in GB: the state's own `reserve`. */
export type ReserveFacts = {
  /** The GB the person pinned, or null for auto. */
  gb: number | null;
  /** The GB in force: the pinned number, or a third of the machine's memory. */
  kept: number;
  total?: number | null;
  freeCommit?: number | null;
  /** What Daoris's own processes hold. */
  daoris?: number | null;
};

const said = (gb: number | null | undefined) => (gb === null || gb === undefined ? '?' : String(gb));

/**
 * The memory the machine keeps free for the person's own work (`reserve`): auto, a third of its memory, or a pinned number of GB, 0 for none,
 * shown with how the machine's memory stands and what Daoris's own processes hold. A molecule: the held value by props, every change out.
 */
export function ReserveSetting({ setting, held, onSet, busy = false }: {
  /** The setting as the driver's list declares it: its bounds. */
  setting: MinutesDeclared;
  held: ReserveFacts;
  /** A change the person made: whole GB in bounds and not the one held, or null for auto. */
  onSet: (gb: number | null) => void;
  /** A change is being written: both controls wait for it, so a second press never races the first. */
  busy?: boolean;
}) {
  const { t } = useTranslation();
  const least = setting.least ?? 0;
  const most = setting.most ?? 1024;
  const [typed, setTyped] = useState<string | null>(null);
  const shown = held.gb ?? Math.round(held.kept);
  const auto = held.gb === null;
  const values = { kept: held.kept, total: said(held.total), free: said(held.freeCommit), daoris: said(held.daoris) };

  return (
    <SettingRow
      label={t('settings.reserve.label')}
      twin={t('settings.reserve.terminal')}
      why={t('settings.reserve.body')}
      control={(
        <>
          <CheckField checked={auto} disabled={busy} onChange={(on) => onSet(on ? null : shown)} label={t('settings.reserve.auto')} />
          <input
            type="number"
            min={least}
            max={most}
            disabled={auto || busy}
            aria-label={t('settings.reserve.label')}
            value={typed ?? String(shown)}
            onChange={(event) => setTyped(event.target.value)}
            onBlur={() => {
              const value = typed === null || typed.trim() === '' ? shown : Number(typed);
              setTyped(null);
              if (!Number.isInteger(value) || value < least || value > most || value === held.gb) return;
              onSet(value);
            }}
            className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink disabled:opacity-50"
          />
          <span className="text-body text-ink-soft">{t('settings.reserve.gb')}</span>
        </>
      )}
    >
      <p className="m-0 text-small text-ink-soft">{held.kept === 0 ? t('settings.reserve.stateNone', values) : t('settings.reserve.state', values)}</p>
    </SettingRow>
  );
}

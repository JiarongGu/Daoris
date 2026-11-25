import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField, SettingRow } from '../../design/ui';
import type { MinutesDeclared } from './MinutesSetting';

/** Why the room is what it is, in GB: the person's share and its swing, what keeps the room, the limit and its signal, and what a gate needs. */
export type ReserveWhy = {
  person: number;
  swing: number;
  margin: number;
  floor: number;
  /** What sets the memory kept free: `floor` (the person's number) or `headroom` (their own work's measured swing). */
  keepBy: 'floor' | 'headroom';
  limit: number;
  ceiling: number;
  /** The signal behind the limit: `start`, `memory`, `cpu`, `timeouts` or `clear`. */
  signal: string;
  /** The processors' busy, 0 to 1. */
  busy?: number | null;
  need: number;
  /** `learned` from its runs on this machine, or `declared` until it has run. */
  needSource: string;
  needRuns: number;
};

/** What the reserve is and how the machine stands beside it, in GB: the state's own `reserve`. */
export type ReserveFacts = {
  /** The GB the person pinned as a floor, or null for auto (no floor). */
  gb: number | null;
  /** The GB kept free now: the larger of the floor and the measured headroom. */
  kept: number;
  total?: number | null;
  freeCommit?: number | null;
  /** What Daoris's own processes hold. */
  daoris?: number | null;
  why?: ReserveWhy | null;
};

const said = (gb: number | null | undefined) => (gb === null || gb === undefined ? '?' : String(gb));

/**
 * The floor of the memory the machine keeps free for the person's own work (`reserve`): auto, none beyond what their own work is measured to
 * take back, or a pinned number of GB, shown with how the machine's memory stands, why the room is what it is and what Daoris's own
 * processes hold. A molecule: the held value by props, every change out.
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
  const why = held.why;

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
      {why ? (
        <>
          <p className="m-0 text-small text-ink-soft">
            {t('settings.reserve.why', { person: why.person, swing: why.swing, margin: why.margin, by: t(`settings.reserve.by.${why.keepBy}`) })}
          </p>
          <p className="m-0 text-small text-ink-soft">
            {t('settings.reserve.room', {
              limit: why.limit,
              ceiling: why.ceiling,
              signal: t(`settings.reserve.signal.${why.signal}`, { defaultValue: t('settings.reserve.signal.start') }),
              busy: why.busy === null || why.busy === undefined ? '' : t('settings.reserve.busy', { percent: Math.round(why.busy * 100) }),
              need: why.need,
              source: t(`settings.reserve.source.${why.needSource}`, { runs: why.needRuns }),
            })}
          </p>
        </>
      ) : null}
    </SettingRow>
  );
}

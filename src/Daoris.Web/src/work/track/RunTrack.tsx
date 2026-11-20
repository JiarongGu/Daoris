import { useTranslation } from 'react-i18next';
import { Button, Tip } from '../../design/ui';
import { cn } from '../../lib/cn';
import { roleSaid } from '../../workflow/stepRoles';
import type { Track, TrackChip } from './passage';

/** Each mark's glyph, beside its words: a mark is never said by colour alone. A step the person settles is marked ◆. */
const GLYPH: Record<TrackChip['mark'], string> = { passed: '✓', current: '●', next: '○', failed: '✗' };

/**
 * **The workflow's track** under a session's head: its run's steps in its graph's order, each a chip with its mark, the standing one
 * with its attempt and, where it waits, whom it waits on ("waiting on `<role>`: `<holder>`"); how far it came; and *Flowchart*,
 * which opens the run's whole view. A press on a chip leads to its step's rows in the timeline.
 *
 * Presentational: the track (`trackOf`), the holder's words and the presses arrive as props.
 */
export function RunTrack({ track, holderOf, onChip, onFlowchart }: {
  track: Track;
  /** Whom a chip's wait is held by, in the window's words (`stepHolder`): *you*, or an agent by its name. */
  holderOf: (chip: TrackChip) => string;
  onChip: (node: string) => void;
  onFlowchart: () => void;
}) {
  const { t, i18n } = useTranslation();
  const said = (chip: TrackChip) => {
    const role = chip.wait?.role ?? chip.role;
    const state = chip.wait && role
      ? t('work.track.waitingOn', { role: roleSaid(t, i18n, role), holder: holderOf(chip) })
      : t(`work.track.state.${chip.mark}`);
    return chip.attempt
      ? t('work.track.chipAttempt', { node: chip.node, attempt: chip.attempt, state })
      : t('work.track.chip', { node: chip.node, state });
  };
  return (
    <nav aria-label={t('work.track.label', { workflow: track.workflow })} className="mt-1.5 flex min-w-0 items-center gap-2 text-small">
      <ol className="m-0 flex min-w-0 flex-1 list-none items-center gap-1 overflow-hidden p-0">
        {track.chips.map((chip, index) => (
          <li key={chip.node} className="flex min-w-0 shrink items-center gap-1">
            {index > 0 && <span aria-hidden="true" className="text-ink-faint">›</span>}
            <Tip content={said(chip)}>
              <button
                type="button"
                data-track-chip={chip.node}
                data-mark={chip.mark}
                aria-label={said(chip)}
                onClick={() => onChip(chip.node)}
                className={cn(
                  'inline-flex min-h-7 min-w-0 cursor-pointer items-center gap-1 rounded-control border-0 bg-transparent px-1 text-small hover:text-accent-ink',
                  chip.mark === 'next' ? 'text-ink-faint' : 'text-ink',
                  (chip.mark === 'current' || chip.mark === 'failed') && 'font-medium',
                )}
              >
                <span aria-hidden="true">{chip.wait?.why && chip.wait.why !== 'actor' && chip.mark === 'current' ? '◆' : GLYPH[chip.mark]}</span>
                <span className="truncate">{chip.node}</span>
                {chip.attempt && chip.attempt > 1 && <span aria-hidden="true" className="text-ink-faint">{t('work.track.attempt', { attempt: chip.attempt })}</span>}
              </button>
            </Tip>
          </li>
        ))}
      </ol>
      <span className="shrink-0 tabular-nums text-ink-faint">{`${track.at}/${track.of}`}</span>
      <Button variant="ghost" className="min-h-7 shrink-0 px-1.5 py-0 text-small" onClick={onFlowchart}>{t('work.track.flowchart')}</Button>
    </nav>
  );
}

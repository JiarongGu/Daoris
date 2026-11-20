import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { holderSaid } from '../../settings/place/PlaceCast';

/** What `WORKFLOW_HOLD` is sent: the run, its step, the act, whom it is handed to, the reason and the epoch of the tenure its row read. */
export type StepHoldAsk = { run: string; node: string; act: string; holder?: string | null; why?: string | null; epoch?: number | null };

/** A press's ask, carrying the epoch its row read so a step passed on since moves nothing; none where the row read none. */
export const stepHoldAsk = (run: string, node: string, act: string, holder: string | null, epoch?: number | null): StepHoldAsk =>
  ({ run, node, act, holder, ...(epoch != null ? { epoch } : {}) });

/** What it came to: done, or refused by its code with the driver's sentence. */
export type StepHoldAnswer = { done: boolean; refused: string | null; message: string | null };

/**
 * The bridge's calls a step's holder presses go through, handed down by the organism that holds the bridge (`stepHoldBridge`), so the door
 * holds none: the roster read from the step's place (only while *Pass to…* is offered) and the press itself.
 */
export type StepHoldBridge = {
  useCast: (workspace: string, repository: string, options: { enabled: boolean }) => { data?: { roster: string[] } };
  useHold: () => { mutateAsync: (ask: StepHoldAsk) => Promise<StepHoldAnswer>; isPending: boolean };
};

/**
 * A standing step's **Take**, **Pass to…** and **Give back**, as the step offers them (`holds`): *Pass to…* opens the roster to pick from. Each
 * press is a new epoch of the step's tenure, restarting nothing; a refusal says the driver's sentence. Props only, so the run view and *What
 * needs you* draw the same presses.
 */
export function StepHoldPresses({ holds, heldBy, roster, busy = false, onHold, said }: {
  holds: string[];
  heldBy?: string | null;
  /** The holders *Pass to…* offers; none drawn until it is opened. */
  roster: string[];
  busy?: boolean;
  onHold: (act: string, holder?: string) => void;
  said?: string | null;
}) {
  const { t } = useTranslation();
  const [handing, setHanding] = useState(false);
  if (holds.length === 0) return null;
  return (
    <div className="grid min-w-0 gap-1">
      <div className="flex flex-wrap items-center gap-1.5" aria-label={t('workflow.hold.label')}>
        {heldBy && <span className="text-caption text-ink-faint">{t('workflow.hold.heldBy', { holder: holderSaid(t, heldBy) })}</span>}
        {holds.map((act) => (
          <Button key={act} variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" disabled={busy}
            onClick={() => (act === 'hand' ? setHanding((open) => !open) : onHold(act))}>
            {t(`workflow.hold.act.${act}`)}
          </Button>
        ))}
      </div>
      {handing && (
        <ul aria-label={t('workflow.hold.handTo')} className="m-0 flex list-none flex-wrap gap-1 p-0">
          {roster.filter((holder) => holder !== heldBy).map((holder) => (
            <li key={holder}>
              <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" disabled={busy}
                onClick={() => { setHanding(false); onHold('hand', holder); }}>
                {holderSaid(t, holder)}
              </Button>
            </li>
          ))}
        </ul>
      )}
      {said && <p className="m-0 text-small text-ink-soft">{said}</p>}
    </div>
  );
}

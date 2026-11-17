import { useState } from 'react';
import { usePlaceCast, useStepHold } from '../../bridge/repositories/cast';
import { StepHoldPresses, stepHoldAsk } from './StepHold';

/**
 * A standing step's holder presses, wired: the roster read from its place's cast, each press `WORKFLOW_HOLD` with the epoch its row read. The
 * run's view and *What needs you* both draw it, each from what its step carries (`holds`, `heldBy`, `epoch`).
 */
export function StepHold({ run, workspace, repository, node, holds, heldBy, epoch }: {
  run: string; workspace: string; repository: string; node: string; holds?: string[] | null; heldBy?: string | null; epoch?: number | null;
}) {
  const hold = useStepHold();
  const cast = usePlaceCast(workspace, repository, { enabled: (holds ?? []).includes('hand') });
  const [said, setSaid] = useState<string | null>(null);
  if (!holds || holds.length === 0) return null;
  return (
    <StepHoldPresses
      holds={holds}
      heldBy={heldBy}
      roster={cast.data?.roster ?? []}
      busy={hold.isPending}
      said={said}
      onHold={(act, holder) => {
        hold.mutateAsync(stepHoldAsk(run, node, act, holder ?? null, epoch))
          .then((answer) => setSaid(answer.done ? null : answer.message))
          .catch((error: unknown) => setSaid(error instanceof Error ? error.message : String(error)));
      }}
    />
  );
}

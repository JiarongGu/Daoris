import { useState } from 'react';
import { usePlaceCast, useStepHold } from '../../bridge/repositories/cast';
import { StepHoldPresses } from './StepHold';

/**
 * A standing step's holder presses, wired: the roster read from its place's cast, each press `WORKFLOW_HOLD`. The run's view and *What needs
 * you* both draw it, each from what its step carries (`holds`, `heldBy`).
 */
export function StepHold({ run, workspace, repository, node, holds, heldBy }: {
  run: string; workspace: string; repository: string; node: string; holds?: string[] | null; heldBy?: string | null;
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
        hold.mutateAsync({ run, node, act, holder: holder ?? null })
          .then((answer) => setSaid(answer.done ? null : answer.message))
          .catch((error: unknown) => setSaid(error instanceof Error ? error.message : String(error)));
      }}
    />
  );
}

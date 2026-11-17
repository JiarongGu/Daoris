import { useState } from 'react';
import { usePlaceCast, useStepHold } from '../../bridge/repositories/cast';
import type { RunStep } from '../run';
import { StepHoldPresses } from './StepHold';

/** A run's standing step's holder presses, wired: the roster read from its place's cast, each press `WORKFLOW_HOLD`. */
export function StepHold({ run, workspace, repository, step }: { run: string; workspace: string; repository: string; step: RunStep }) {
  const hold = useStepHold();
  const cast = usePlaceCast(workspace, repository, { enabled: (step.holds ?? []).includes('hand') });
  const [said, setSaid] = useState<string | null>(null);
  if (!step.holds || step.holds.length === 0) return null;
  return (
    <StepHoldPresses
      holds={step.holds}
      heldBy={step.heldBy}
      roster={cast.data?.roster ?? []}
      busy={hold.isPending}
      said={said}
      onHold={(act, holder) => {
        hold.mutateAsync({ run, node: step.step.id, act, holder: holder ?? null })
          .then((answer) => setSaid(answer.done ? null : answer.message))
          .catch((error: unknown) => setSaid(error instanceof Error ? error.message : String(error)));
      }}
    />
  );
}

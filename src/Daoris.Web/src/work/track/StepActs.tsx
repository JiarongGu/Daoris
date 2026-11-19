import { useWorkflowRun } from '../../bridge/repositories/workflow';
import { StepActRows } from '../../workflow/acts/StepActRows';

/**
 * What the holder of a session's standing step journaled, under that step's start in the timeline: the run view's act rows
 * (`StepActRows`, ACTORFLOW1c), read from the session's run as that view reads it. Nothing where none is journaled.
 */
export function StepActs({ session, node }: { session: string; node: string }) {
  const read = useWorkflowRun({ session });
  const step = (Array.isArray(read.data?.runs) ? read.data.runs : []).flatMap((run) => run.steps).find((each) => each.step.id === node);
  return step ? <StepActRows step={step} /> : null;
}

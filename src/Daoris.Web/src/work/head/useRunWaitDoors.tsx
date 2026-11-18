import { useAttention } from '../../bridge/attention';
import { stepHoldBridge } from '../../bridge/repositories/cast';
import { useResolveHeld } from '../../bridge/repositories/lines';
import { useRunNodeWord, useWorkflowRun } from '../../bridge/repositories/workflow';
import { useSessionGroups } from '../../bridge/sessions/sessionGroups';
import { ReleaseStepDoors } from '../../workflow/release/ReleaseStepDoors';
import type { WorkflowRun } from '../../workflow/run';
import { StepHold } from '../../workflow/wait/StepHoldDoor';
import type { StepWaitRow } from '../attention/attentionPushed';
import type { StepWait } from '../waits/dock';

/**
 * A wait's release step doors, the run's view's own (`ReleaseStepDoors`), drawn on its step as the session's run read answers it:
 * *What needs you*'s row names no step kind, so the step's own facts (its kind and its wait's actor, words and page) come from the run.
 */
export const releaseDoorsOf = (runs: readonly WorkflowRun[]) => (wait: StepWait) => {
  const run = runs.find((each) => each.graph?.run === wait.run);
  const step = run?.steps.find((each) => each.step.id === wait.node);
  return run && step ? <ReleaseStepDoors run={run} step={step} /> : null;
};

/**
 * The dock's run waits of `session`: its placing in Sessions and *What needs you*'s rows, as the shell pushes them, with their doors.
 * Its run is read only while a step waits on a plugin's actor, the wait a release's approval is.
 */
export function useRunWaitDoors(session: string | null) {
  const groups = useSessionGroups();
  const attention = useAttention();
  const word = useRunNodeWord();
  const held = useResolveHeld();
  const waits = (session ? groups.data?.find((row) => row.session === session)?.waits : null) ?? [];
  const runRead = useWorkflowRun(session ? { session } : null, { enabled: waits.some((wait) => wait.why === 'actor') });
  const rows = attention.state === 'known' ? attention.rows.filter((row) => row.kind === 'step' && row.step) : [];
  const steps = rows.flatMap((row) => (row.step ? [row.step] : []));
  const hold = (step: StepWaitRow) => {
    const row = rows.find((each) => each.step === step);
    return <StepHold bridge={stepHoldBridge} run={step.run} workspace={row?.workspace ?? ''} repository={row?.where ?? ''} node={step.node} holds={step.holds} />;
  };
  const release = releaseDoorsOf(Array.isArray(runRead.data?.runs) ? runRead.data.runs : []);
  return { waits, steps, onWord: word.mutateAsync, onHeld: held.mutateAsync, hold, release };
}

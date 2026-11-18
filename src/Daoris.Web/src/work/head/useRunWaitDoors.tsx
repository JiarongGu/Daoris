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

export const releaseDoorsOf = (runs: readonly WorkflowRun[]) => (wait: StepWait) => {
  const run = runs.find((each) => each.graph?.run === wait.run);
  const step = run?.steps.find((each) => each.step.id === wait.node);
  return run && step ? <ReleaseStepDoors run={run} step={step} /> : null;
};

/** The dock's run waits of `session` with their doors; a release step's are its run's view's (`releaseDoorsOf`), its run read while a step waits on a plugin's actor. */
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

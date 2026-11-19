import { useAttention } from '../../bridge/attention';
import { stepHoldBridge } from '../../bridge/repositories/cast';
import { useResolveHeld } from '../../bridge/repositories/lines';
import { useRunNodeWord, useWorkflowRun } from '../../bridge/repositories/workflow';
import { useSessionGroups } from '../../bridge/sessions/sessionGroups';
import { ReleaseStepDoors } from '../../workflow/release/ReleaseStepDoors';
import type { RunScope, WorkflowRun } from '../../workflow/run';
import { StepHold } from '../../workflow/wait/StepHoldDoor';
import type { StepWaitRow } from '../attention/attentionPushed';
import type { StepWait } from '../waits/dock';

type WaitAt = Pick<StepWait, 'run' | 'node'>;

export const releaseDoorsOf = (runs: readonly WorkflowRun[]) => (wait: WaitAt) => {
  const run = runs.find((each) => each.graph?.run === wait.run);
  const step = run?.steps.find((each) => each.step.id === wait.node);
  return run && step ? <ReleaseStepDoors run={run} step={step} /> : null;
};

export function RunReleaseDoors({ scope, wait }: { scope: RunScope; wait: WaitAt }) {
  const read = useWorkflowRun(scope);
  return releaseDoorsOf(Array.isArray(read.data?.runs) ? read.data.runs : [])(wait);
}

export const actorWait = (wait: { why: string }) => wait.why === 'actor';

/**
 * The dock's run waits of `session` with their doors; a plugin's actor wait's are its run's view's, read from the run `scope` names. While
 * the session awaits its step, its runs' reading too, whose ask's parts the dock names (what its part waits on, its joint checks).
 */
export function useRunWaitDoors(session: string | null, { awaitingStep = false, onQuest }: { awaitingStep?: boolean; onQuest?: (id: string) => void } = {}) {
  const runs = useWorkflowRun(session ? { session } : null, { enabled: awaitingStep });
  const groups = useSessionGroups();
  const attention = useAttention();
  const word = useRunNodeWord();
  const held = useResolveHeld();
  const waits = (session ? groups.data?.find((row) => row.session === session)?.waits : null) ?? [];
  const rows = attention.state === 'known' ? attention.rows.filter((row) => row.kind === 'step' && row.step) : [];
  const steps = rows.flatMap((row) => (row.step ? [row.step] : []));
  const hold = (step: StepWaitRow) => {
    const row = rows.find((each) => each.step === step);
    return <StepHold bridge={stepHoldBridge} run={step.run} workspace={row?.workspace ?? ''} repository={row?.where ?? ''} node={step.node} holds={step.holds} />;
  };
  const release = (wait: StepWait) => (session && actorWait(wait) ? <RunReleaseDoors scope={{ session }} wait={wait} /> : null);
  const parts = awaitingStep && Array.isArray(runs.data?.runs) ? { runs: runs.data.runs, onQuest } : null;
  return { waits, steps, onWord: word.mutateAsync, onHeld: held.mutateAsync, hold, release, parts };
}

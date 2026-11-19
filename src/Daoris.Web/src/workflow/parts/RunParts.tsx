import type { RunJoint, RunPartReceipt, WorkflowRun } from '../run';
import { JointCheckRows, PartWaitRows } from './PartRows';

type Drawn = { key: string; waits: readonly RunPartReceipt[] } | { key: string; joint: RunJoint };

/** What runs say of their ask's parts: each run's part's waits, then each joint check of its steps, in the run's order. */
export const partsOf = (runs: readonly WorkflowRun[]): Drawn[] => runs.flatMap((run) => {
  const name = run.graph?.run ?? run.repository;
  const waits: Drawn[] = run.graph?.partWaits && run.graph.partWaits.length > 0 ? [{ key: `${name}:waits`, waits: run.graph.partWaits }] : [];
  return [...waits, ...run.steps.flatMap((step): Drawn[] => (step.joint ? [{ key: `${name}:${step.step.id}`, joint: step.joint }] : []))];
});

/**
 * **What a session's runs wait on of their ask's other parts**, in its page's dock: each run's part's waits on another part's receipt and
 * each joint check's parts held and waited for, read from the runs' one reading, never counted as a wait on the person. Nothing where
 * none. Presentational: the runs and the quest's door arrive as props.
 */
export function RunParts({ runs, onQuest }: { runs: readonly WorkflowRun[]; onQuest?: (id: string) => void }) {
  const drawn = partsOf(runs);
  if (drawn.length === 0) return null;
  return (
    <div className="grid min-w-0 gap-1.5">
      {drawn.map((each) => ('waits' in each
        ? <PartWaitRows key={each.key} waits={each.waits} onQuest={onQuest} />
        : <JointCheckRows key={each.key} joint={each.joint} onQuest={onQuest} />))}
    </div>
  );
}

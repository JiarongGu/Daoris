import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Inline } from '../../design/ui';
import { NodeWord } from '../../workflow/NodeWord';
import type { CheckHeldAnswer, CheckHeldAsk, NodeWordAnswer, NodeWordAsk, RunStep } from '../../workflow/run';
import { FailedStepHeld } from '../../workflow/wait/FailedStepHeld';
import type { StepWaitRow } from '../attention/attentionPushed';
import { stepHolder } from '../attention/taskRead';
import type { SessionGrouping } from '../groups';

type SessionWait = NonNullable<SessionGrouping['waits']>[number];

/** A step row as the run view's presses read it, *What needs you*'s; a press its held attempt offers is said on that attempt alone. */
const stepOf = (wait: SessionWait, row: StepWaitRow | undefined): RunStep => ({
  step: { id: wait.node, kind: '' } as RunStep['step'], state: 'waiting', detail: '',
  acts: (row?.presses ?? []).filter((act) => !row?.held?.words.includes(act)), held: row?.held ?? null, epoch: row?.epoch ?? null,
});

/**
 * **The steps of the session's run that wait on a role the person holds**, on its page's head: each reads
 * "<node>: waiting on `<role>`: <holder>", its holder as its row in *What needs you* says it, with the presses that settle it beside it, the
 * ones that row offers (the step's own words, *Not needed this run*, *Done outside Daoris*, a left step's *Try again* and stop), each
 * passing the epoch the row read, and the row's *Take*, *Pass to…* and *Give back*. A release step's own doors stand first, as its run's
 * view draws them: on its approval, the card with *Approve* and *Reject* where the plugin makes it, else the stage's page. A notification
 * about the step opens this session, so it opens here. Nothing where no step waits on the person.
 *
 * A molecule: the waits, the rows and the doors arrive as props, the holder presses as `hold`, a release step's doors as `release`.
 */
export function RunWaitsHead({ waits, steps, onWord, onHeld, hold, release }: {
  /** The session's run's waits on the person, as Sessions placed it (`SessionGrouping.waits`). */
  waits: readonly SessionWait[];
  /** *What needs you*'s step rows, whose presses each wait offers. */
  steps: readonly StepWaitRow[];
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
  /** A row's holder presses, drawn where it offers any. */
  hold?: (row: StepWaitRow) => ReactNode;
  /** A wait's release step doors (`ReleaseStepDoors`, on its step as the run's read answers it); nothing on any other step. */
  release?: (wait: SessionWait) => ReactNode;
}) {
  const { t } = useTranslation();
  if (waits.length === 0) return null;
  return (
    <div className="grid gap-1.5">
      {waits.map((wait) => {
        const row = steps.find((step) => step.run === wait.run && step.node === wait.node);
        const step = stepOf(wait, row);
        const said = t('work.head.runWait', { node: wait.node, role: wait.role ?? '', holder: row ? stepHolder(row) : t('workflow.wait.holder.person') });
        return (
          <div key={`${wait.run}/${wait.node}`} role="group" aria-label={said.replace(/`/g, '')} className="grid min-w-0 gap-1">
            <p className="m-0 text-small text-ink"><Inline text={said} /></p>
            {release?.(wait)}
            {row && <NodeWord run={wait.run} step={step} onWord={onWord} />}
            {row?.held && <FailedStepHeld step={step} onHeld={onHeld} />}
            {row?.holds && row.holds.length > 0 && hold?.(row)}
          </div>
        );
      })}
    </div>
  );
}

import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useAttention } from '../../bridge/attention';
import { stepHoldBridge } from '../../bridge/repositories/cast';
import { useResolveHeld } from '../../bridge/repositories/lines';
import { useRunNodeWord } from '../../bridge/repositories/workflow';
import { useSessionGroups } from '../../bridge/sessions/sessionGroups';
import { Inline } from '../../design/ui';
import { NodeWord } from '../../workflow/NodeWord';
import type { CheckHeldAnswer, CheckHeldAsk, NodeWordAnswer, NodeWordAsk, RunStep } from '../../workflow/run';
import { FailedStepHeld } from '../../workflow/wait/FailedStepHeld';
import { StepHold } from '../../workflow/wait/StepHoldDoor';
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
 * passing the epoch the row read, and the row's *Take*, *Pass to…* and *Give back*. A notification about the step opens this session, so it
 * opens here. Nothing where no step waits on the person.
 *
 * A molecule: the waits, the rows and the doors arrive as props, the holder presses as `hold`.
 */
export function RunWaitsHead({ waits, steps, onWord, onHeld, hold }: {
  /** The session's run's waits on the person, as Sessions placed it (`SessionGrouping.waits`). */
  waits: readonly SessionWait[];
  /** *What needs you*'s step rows, whose presses each wait offers. */
  steps: readonly StepWaitRow[];
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
  /** A row's holder presses, drawn where it offers any. */
  hold?: (row: StepWaitRow) => ReactNode;
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
            {row && <NodeWord run={wait.run} step={step} onWord={onWord} />}
            {row?.held && <FailedStepHeld step={step} onHeld={onHeld} />}
            {row?.holds && row.holds.length > 0 && hold?.(row)}
          </div>
        );
      })}
    </div>
  );
}

/**
 * The dock's run waits of `session`: its placing in Sessions and *What needs you*'s rows, as the shell pushes them, with their
 * doors (`WaitStrip`'s `runWaits`). None where nothing is attended.
 */
export function useRunWaitDoors(session: string | null) {
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
  return { waits, steps, onWord: word.mutateAsync, onHeld: held.mutateAsync, hold };
}

import { useTranslation } from 'react-i18next';
import { useAttention } from '../../bridge/attention';
import { useResolveHeld } from '../../bridge/repositories/lines';
import { useRunNodeWord } from '../../bridge/repositories/workflow';
import { useSessionGroups } from '../../bridge/sessions/sessionGroups';
import { Inline } from '../../design/ui';
import { NodeWord } from '../../workflow/NodeWord';
import type { CheckHeldAnswer, CheckHeldAsk, NodeWordAnswer, NodeWordAsk, RunStep } from '../../workflow/run';
import { FailedStepHeld } from '../../workflow/wait/FailedStepHeld';
import type { StepWaitRow } from '../attention/attentionPushed';
import type { SessionGrouping } from '../groups';

type SessionWait = NonNullable<SessionGrouping['waits']>[number];

/** A step row as the run view's presses read it: its node, and the presses and held attempt *What needs you* carries. */
const stepOf = (wait: SessionWait, row: StepWaitRow | undefined): RunStep => ({
  step: { id: wait.node, kind: '' } as RunStep['step'], state: 'waiting', detail: '', acts: row?.presses ?? [], held: row?.held ?? null,
});

/**
 * **The steps of the session's run that wait on a role the person holds**, on its page's head:each reads
 * "<node>: waiting on `<role>`: you", with the presses that settle it beside it, the ones its row in *What needs you* offers (the step's
 * own words, its *Not needed this run*, a plugin wait's *Done outside Daoris*), never its stop. A notification about the step opens
 * this session, so it opens here. Nothing where no step waits on the person.
 *
 * A molecule: the waits, the rows and the two doors arrive as props.
 */
export function RunWaitsHead({ waits, steps, onWord, onHeld }: {
  /** The session's run's waits on the person, as Sessions placed it (`SessionGrouping.waits`). */
  waits: readonly SessionWait[];
  /** *What needs you*'s step rows, whose presses each wait offers. */
  steps: readonly StepWaitRow[];
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
}) {
  const { t } = useTranslation();
  if (waits.length === 0) return null;
  return (
    <div className="grid gap-1.5">
      {waits.map((wait) => {
        const row = steps.find((step) => step.run === wait.run && step.node === wait.node);
        const step = stepOf(wait, row);
        const said = t('work.head.runWait', { node: wait.node, role: wait.role ?? '', holder: t('workflow.wait.holder.person') });
        return (
          <div key={`${wait.run}/${wait.node}`} role="group" aria-label={said.replace(/`/g, '')} className="grid min-w-0 gap-1">
            <p className="m-0 text-small text-ink"><Inline text={said} /></p>
            {row && <NodeWord run={wait.run} step={step} onWord={onWord} />}
            {row?.held && <FailedStepHeld step={step} onHeld={onHeld} />}
          </div>
        );
      })}
    </div>
  );
}

/** The head's run waits of `session`: its placing in Sessions and *What needs you*'s rows, as the shell pushes them, with their doors. */
export function SessionRunWaits({ session }: { session: string }) {
  const groups = useSessionGroups();
  const attention = useAttention();
  const word = useRunNodeWord();
  const held = useResolveHeld();
  const waits = groups.data?.find((row) => row.session === session)?.waits ?? [];
  const steps = attention.state === 'known' ? attention.rows.flatMap((row) => (row.kind === 'step' && row.step ? [row.step] : [])) : [];
  return <RunWaitsHead waits={waits} steps={steps} onWord={word.mutateAsync} onHeld={held.mutateAsync} />;
}

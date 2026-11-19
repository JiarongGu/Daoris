import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import { NodeWord } from '../../workflow/NodeWord';
import { type CheckHeldAnswer, type CheckHeldAsk, heldByAgent, type NodeWordAnswer, type NodeWordAsk, type RunStep } from '../../workflow/run';
import { FailedStepHeld } from '../../workflow/wait/FailedStepHeld';
import type { StepWaitRow } from '../attention/attentionPushed';
import { stepHolder } from '../attention/taskRead';
import type { SessionGrouping } from '../groups';

type SessionWait = NonNullable<SessionGrouping['waits']>[number];

/** A step row as the run view's presses read it, *What needs you*'s; a press its held attempt offers is said on that attempt alone. */
const stepOf = (wait: SessionWait, row: StepWaitRow | undefined): RunStep => ({
  step: { id: wait.node, kind: '' } as RunStep['step'], state: 'waiting', detail: '',
  acts: (row?.presses ?? []).filter((act) => !row?.held?.words.includes(act)), held: row?.held ?? null, epoch: row?.epoch ?? null,
  holder: row ? { kind: row.holder === 'agent' ? 'agent' : 'person', name: row.holderName ?? null } : null, holds: row?.holds ?? null,
});

/** A wait of the session's dock, by its line's key and its name, that the step's agent may be blocked on. */
export type BlockedWait = { key: string; name: string };

/**
 * **The steps of the session's run that wait on a role the person holds**, on its page's head: each reads
 * "<node>: waiting on `<role>`: <holder>", its holder as its row in *What needs you* says it, with the presses that settle it beside it, the
 * ones that row offers (the step's own words, *Not needed this run*, *Done outside Daoris*, a left step's *Try again* and stop), each
 * passing the epoch the row read, and the row's *Take*, *Pass to…* and *Give back*, after a release step's own doors (`release`: its approval's
 * card). A notification about the step opens this session, so it opens here. Nothing where no step waits on the person.
 *
 * A molecule: the waits, the rows and the doors arrive as props, the holder presses as `hold`.
 */
export function RunWaitsHead({ waits, steps, onWord, onHeld, hold, release, blocked, onTo }: {
  /** The session's run's waits on the person, as Sessions placed it (`SessionGrouping.waits`). */
  waits: readonly SessionWait[];
  /** *What needs you*'s step rows, whose presses each wait offers. */
  steps: readonly StepWaitRow[];
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
  /** A row's holder presses, drawn where it offers any. */
  hold?: (row: StepWaitRow) => ReactNode;
  release?: (wait: SessionWait) => ReactNode;
  /** The other waits on the person this session's dock holds: a step its agent holds says it waits on them, each a link to its line. */
  blocked?: readonly BlockedWait[];
  /** Leads to the dock line a `blocked` wait names. */
  onTo?: (key: string) => void;
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
            {row && heldByAgent(step) && blocked && blocked.length > 0 && onTo && (
              <p className="m-0 flex flex-wrap items-center gap-x-1.5 text-small text-ink-soft">
                <Inline text={t('work.head.runWaitBlocked', { holder: stepHolder(row) })} />
                {blocked.map((each) => (
                  <Button key={each.key} variant="ghost" className="min-h-0 px-1.5 py-0 text-small" onClick={() => onTo(each.key)}>{each.name}</Button>
                ))}
              </p>
            )}
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

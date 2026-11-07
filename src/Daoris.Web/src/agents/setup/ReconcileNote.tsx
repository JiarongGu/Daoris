import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useReconcileApply, useReconcilePlan } from '../../bridge/repositories/setup';
import { Button, Inline } from '../../design/ui';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';
import type { ReconcilePlanned } from './reconcilePlan';

/**
 * **What the shared doctrine asks of an adopted repository**: where the doctrine tool's status says
 * the canon moved, or a file the repository keeps as its own is passed by the canon's, a note between the place's head and its sections says
 * how, with the counts a sync would move, and offers *Reconcile…*, which asks the repository's own session to decide each difference. The
 * press is the person's, confirmed in the page; a refusal is the driver's sentence. Nothing is drawn where nothing is offered.
 *
 * A molecule: the plan arrives, and the press goes out.
 */
export function ReconcileNoteView({ plan, busy = false, onReconcile }: {
  plan: ReconcilePlanned | null;
  busy?: boolean;
  onReconcile: (answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  if (!plan?.offered) return null;
  const { repository, status } = plan;
  const lead = status?.update === 'behind'
    ? t('agents.setup.reconcile.behind', { repository, locked: status.locked ?? '', carried: status.carried ?? '' })
    : status?.update === 'differs'
      ? t('agents.setup.reconcile.differs', { repository, carried: status.carried ?? '' })
      : t('agents.setup.reconcile.kept', { repository });
  const counts = status
    ? ([['changed', status.changed], ['added', status.added], ['retired', status.retired], ['reopened', status.reopened]] as const)
      .filter(([, count]) => count > 0)
      .map(([what, count]) => t(`agents.setup.reconcile.${what}`, { count }))
    : [];

  return (
    <section aria-label={t('agents.setup.reconcile.label')} data-reconcile="" className="mb-3 grid min-w-0 gap-1 border-l-[3px] border-accent pl-2.5 text-small text-ink-soft">
      <p className="m-0 text-ink [overflow-wrap:anywhere]">{lead}</p>
      {counts.length > 0 && <p className="m-0">{counts.join(t('agents.setup.reconcile.join'))}</p>}
      {!plan.pressable && plan.refusals.map((each) => (
        <p key={`${each.code}${each.sentence}`} data-refusal={each.code} className="m-0 [overflow-wrap:anywhere]"><Inline text={each.sentence} /></p>
      ))}
      <div>
        <Button disabled={!plan.pressable || busy} onClick={() => setAsking(true)}>{t('agents.setup.reconcile.open')}</Button>
      </div>
      {asking && (
        <InlineConfirm
          tone="primary"
          label={t('agents.setup.reconcile.confirmLabel', { repository })}
          says={t('agents.setup.reconcile.confirmSays', { repository })}
          meanIt={t('agents.setup.reconcile.confirm')}
          busy={busy}
          onConfirm={onReconcile}
          onClose={() => setAsking(false)}
        />
      )}
    </section>
  );
}

/** The note of an adopted repository's page, from its reconcile's plan, and the press that asks it. */
export function ReconcileNote({ repository }: { repository: string }) {
  const plan = useReconcilePlan(repository);
  const apply = useReconcileApply();
  const onReconcile = (answered: Answered) => apply.mutate(repository, {
    onSuccess: (outcome) => (outcome.done ? answered.done() : answered.refused(outcome.message)),
    onError: (error) => answered.refused(error instanceof Error ? error.message : String(error)),
  });
  return <ReconcileNoteView plan={plan.data ?? null} busy={apply.isPending} onReconcile={onReconcile} />;
}

import { useState } from 'react';
import { useConvertApply, useConvertPlan, useConvertUndo, useConvertUndoPlan } from '../../bridge/shell';
import { sentence } from '../../locales/format';
import { type ConvertApplied, type ConvertChoices, DEFAULT_CHOICES } from './convertPlan';
import { ConvertReview } from './ConvertReview';

/**
 * **A home's *Convert…*** (WORKFLOWCLEAN1g-i; the cleanup design §4.3-§4.5), the organism over `ConvertReview`: it holds the two choices, reads the
 * plan they make and presses *Apply* and *Undo the conversion* as the person's own (`CONVERT_APPLY`, `CONVERT_UNDO`), straight to the routes.
 *
 * @remarks
 * One plan over every place, whichever place opened it. The undo's plan is read only once a conversion is done here or the census says the home
 * has ended, and *Undo* is offered only where the driver's plan carries no refusal. A `moved` apply says the driver's sentence, and the plan is
 * read again.
 */
export function ConvertPanel({ onClose }: { onClose: () => void }) {
  const [choices, setChoices] = useState<ConvertChoices>(DEFAULT_CHOICES);
  const [result, setResult] = useState<ConvertApplied | null>(null);
  const [undone, setUndone] = useState<ConvertApplied | null>(null);
  const planned = useConvertPlan(choices);
  const apply = useConvertApply();
  const undo = useConvertUndo();
  const plan = planned.plan;
  const wantsUndo = result?.done === true || (plan?.census.ended === true && !planned.stale);
  const undoPlanned = useConvertUndoPlan({ enabled: wantsUndo });
  const undoPlan = wantsUndo && undone?.done !== true && undoPlanned.plan && !undoPlanned.plan.refusal && undoPlanned.plan.digest ? undoPlanned.plan : null;
  const failed = planned.error ?? apply.error ?? undo.error;

  const onChoose = (patch: Partial<ConvertChoices>) => {
    setChoices((was) => ({ ...was, ...patch }));
    setResult(null);
    setUndone(null);
  };
  const onApply = () => {
    if (!plan) return;
    apply.mutate({ ...choices, digest: plan.digest }, { onSuccess: (answer) => { setResult(answer); setUndone(null); } });
  };
  const onUndo = () => {
    if (!undoPlan?.digest) return;
    undo.mutate({ digest: undoPlan.digest }, { onSuccess: (answer) => { setUndone(answer); if (answer.done) setResult(null); } });
  };

  return (
    <ConvertReview
      plan={plan}
      choices={choices}
      reading={planned.reading}
      stale={planned.stale}
      problem={failed ? sentence(failed) : null}
      applying={apply.isPending}
      undoing={undo.isPending}
      result={result}
      undo={undoPlan}
      undone={undone}
      onChoose={onChoose}
      onApply={onApply}
      onUndo={onUndo}
      onClose={onClose}
    />
  );
}

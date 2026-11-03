import { useState } from 'react';
import { useConvertApply, useConvertPlan, useConvertUndo, useConvertUndoPlan } from '../../bridge/shell';
import { sentence } from '../../locales/format';
import { type ConvertApplied, type ConvertApplyAsk, type ConvertChoices, DEFAULT_CHOICES } from './convertPlan';
import { ConvertReview } from './ConvertReview';

/**
 * **A home's *Convert…*** (WORKFLOWCLEAN1g-i, ONEWAY1ae; the oneway design §1.1, the cleanup design §4.3-§4.5), the organism over `ConvertReview`: it
 * holds the two choices, reads the plan they make over every part and presses *Apply*, the receipt again, *Leave it* and *Undo the conversion* as
 * the person's own (`CONVERT_APPLY`, `CONVERT_UNDO`), straight to the routes.
 *
 * @remarks
 * One plan over every place and part, whichever place opened it. Pressing again and *Leave it* name the receipt the last press answered. The
 * undo's plan is read only once a conversion is done here or the plan says the home has ended, and *Undo* is offered only where the driver's plan
 * carries no refusal. A `moved` apply says the driver's sentence, and the plan is read again.
 */
export function ConvertPanel({ onClose }: { onClose: () => void }) {
  const [choices, setChoices] = useState<ConvertChoices>(DEFAULT_CHOICES);
  const [result, setResult] = useState<ConvertApplied | null>(null);
  const [undone, setUndone] = useState<ConvertApplied | null>(null);
  const planned = useConvertPlan(choices);
  const apply = useConvertApply();
  const undo = useConvertUndo();
  const plan = planned.plan;
  const wantsUndo = result?.done === true || (plan?.ended === true && !planned.stale);
  const undoPlanned = useConvertUndoPlan({ enabled: wantsUndo });
  const undoPlan = wantsUndo && undone?.done !== true && undoPlanned.plan && !undoPlanned.plan.refusal && undoPlanned.plan.digest ? undoPlanned.plan : null;
  const failed = planned.error ?? apply.error ?? undo.error;

  const press = (ask: ConvertApplyAsk) => apply.mutate(ask, { onSuccess: (answer) => { setResult(answer); setUndone(null); } });
  const onChoose = (patch: Partial<ConvertChoices>) => {
    setChoices((was) => ({ ...was, ...patch }));
    setResult(null);
    setUndone(null);
  };
  const onApply = () => {
    if (plan) press({ ...choices, digest: plan.digest });
  };
  const onAgain = () => {
    if (result) press({ ...choices, digest: result.digest });
  };
  const onLeave = (part: string) => {
    if (result) press({ digest: result.digest, leave: part });
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
      onAgain={onAgain}
      onLeave={onLeave}
      onUndo={onUndo}
      onClose={onClose}
    />
  );
}

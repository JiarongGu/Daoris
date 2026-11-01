import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import type { RefActs } from './NewRef';

type Step =
  | { is: 'renamed'; act: string }
  | { is: 'partial'; act: string }
  | { is: 'working' }
  | { is: 'undone' }
  | { is: 'said'; sentence: string }
  | { is: 'unknown' };

/**
 * **What a rename left, with its Finish and Undo** (GITAPP2e, D204; the git-writes design §5, §6): kept by the list beyond the renamed
 * row. A rename that stopped between the branch and its settings (its upstream and the like) says so and offers *Finish*, which moves the
 * settings alone, and *Undo*, which moves the branch back alone; the driver allows each only where the branch and the settings stand as
 * the act left them, and refuses otherwise in words. A done rename offers *Undo*, which checks both names. A result the driver cannot
 * tell is said and never retried.
 *
 * @remarks
 * **A molecule**: the acts arrive as a prop.
 */
export function Renamed({ name, to, act, partial = false, acts, onClose, initial }: {
  name: string;
  /** The name the branch took. */
  to: string;
  /** The journal's act the rename ran as, which Finish and Undo name. */
  act: string;
  /** The rename stopped before the branch's settings moved. */
  partial?: boolean;
  acts: RefActs;
  onClose: () => void;
  /** Open at a step already, as its stories show each. */
  initial?: { step: 'undone' | 'said' | 'unknown'; sentence?: string };
}) {
  const { t } = useTranslation();
  const heading = useId();
  const [step, setStep] = useState<Step>(
    initial?.step === 'undone' ? { is: 'undone' }
      : initial?.step === 'said' ? { is: 'said', sentence: initial.sentence ?? '' }
        : initial?.step === 'unknown' ? { is: 'unknown' } : partial ? { is: 'partial', act } : { is: 'renamed', act },
  );
  // A refusal leaves the notice as it stood, so the person can press the other one.
  const [last, setLast] = useState<Step>(partial ? { is: 'partial', act } : { is: 'renamed', act });

  const run = async (call: () => Promise<{ act?: string | null; outcome?: string | null; refused: boolean; said: string }>, then: 'undone' | 'renamed') => {
    setStep({ is: 'working' });
    try {
      const outcome = await call();
      if (!outcome.refused && outcome.outcome === 'done') {
        if (then === 'undone') setStep({ is: 'undone' });
        else {
          const finished: Step = { is: 'renamed', act: outcome.act ?? act };
          setLast(finished);
          setStep(finished);
        }
      } else if (outcome.refused || outcome.outcome === 'not-done') {
        setStep({ is: 'said', sentence: outcome.said });
      } else setStep({ is: 'unknown' });
    } catch (error) {
      setStep({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  const open = step.is === 'renamed' || step.is === 'partial' || step.is === 'working';
  const settled = step.is === 'renamed' || step.is === 'partial' ? step : last;
  const words = step.is === 'undone' ? t('projects.git.ref.rename.undone', { name, to })
    : step.is === 'said' ? step.sentence
      : step.is === 'unknown' ? t('projects.git.ref.unknown')
        : t(settled.is === 'partial' ? 'projects.git.ref.rename.partial' : 'projects.git.ref.rename.renamed', { name, to });
  const working = step.is === 'working';
  const pressable = settled.is === 'renamed' || settled.is === 'partial' ? settled : null;
  return (
    <div role="group" aria-labelledby={heading} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      <h3 id={heading} className="sr-only">{t('projects.git.ref.rename.openLabel', { name })}</h3>
      <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{words}</p>
      <div className="flex flex-wrap gap-2">
        {(open || step.is === 'said') && pressable && (
          <>
            {pressable.is === 'partial' && (
              <Button variant="primary" disabled={working} onClick={() => { void run(() => acts.finish(pressable.act), 'renamed'); }}>
                {t('projects.git.ref.rename.finish')}
              </Button>
            )}
            <Button variant="ghost" disabled={working} onClick={() => { void run(() => acts.undo(pressable.act), 'undone'); }}>{t('projects.git.ref.undo')}</Button>
          </>
        )}
        <Button variant="ghost" onClick={onClose}>{t('projects.git.ref.done')}</Button>
      </div>
    </div>
  );
}

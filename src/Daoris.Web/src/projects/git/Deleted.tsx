import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import type { RefActs } from './NewRef';

type Step = { is: 'deleted' } | { is: 'undoing' } | { is: 'undone' } | { is: 'said'; sentence: string } | { is: 'unknown' };

/**
 * **What a delete left, and its Undo**: kept by the list beyond the deleted row, which the
 * refreshed list does not draw. *Undo* makes the branch again only where its name is free and puts its configuration back; the driver
 * refuses it otherwise, in words, and a result it cannot tell is said and never retried.
 *
 * @remarks
 * **A molecule**: the acts arrive as a prop.
 */
export function Deleted({ name, act, acts, onClose, initial }: {
  name: string;
  /** The journal's act the delete ran as, which the undo names. */
  act: string;
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
        : initial?.step === 'unknown' ? { is: 'unknown' } : { is: 'deleted' },
  );

  const undo = async () => {
    setStep({ is: 'undoing' });
    try {
      const outcome = await acts.undo(act);
      if (!outcome.refused && outcome.outcome === 'done') setStep({ is: 'undone' });
      else if (outcome.refused || outcome.outcome === 'not-done') setStep({ is: 'said', sentence: outcome.said });
      else setStep({ is: 'unknown' });
    } catch (error) {
      setStep({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  const words = step.is === 'undone' ? t('projects.git.ref.delete.undone', { name })
    : step.is === 'said' ? step.sentence
      : step.is === 'unknown' ? t('projects.git.ref.unknown')
        : t('projects.git.ref.delete.deleted', { name });
  return (
    <div role="group" aria-labelledby={heading} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      <h3 id={heading} className="sr-only">{t('projects.git.ref.delete.openLabel', { name })}</h3>
      <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{words}</p>
      <div className="flex flex-wrap gap-2">
        {(step.is === 'deleted' || step.is === 'undoing') && (
          <Button variant="ghost" disabled={step.is === 'undoing'} onClick={() => { void undo(); }}>{t('projects.git.ref.undo')}</Button>
        )}
        <Button variant="ghost" onClick={onClose}>{t('projects.git.ref.done')}</Button>
      </div>
    </div>
  );
}

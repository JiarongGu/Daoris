import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose } from '../../design/ui';
import type { RefActs } from './NewRef';

const short = (commit: string) => commit.slice(0, 7);

type Step = { is: 'idle' } | { is: 'confirm' } | { is: 'working' } | { is: 'said'; sentence: string };

/**
 * **Delete a branch** (GITAPP2d, D204; the git-writes design §6): a press on one of *Yours*' rows that names what will go and where its
 * commits stay, and sends nothing until it is confirmed. The press carries the commit the row showed, so the driver refuses a branch that
 * has moved. A refusal is said in the driver's own sentence; a branch that went is handed to the list (`onDeleted`), whose notice outlives
 * this row and holds the *Undo* (`Deleted`).
 *
 * @remarks
 * **A molecule**: the acts arrive as a prop. It is absent, never disabled, where the row has no commit.
 */
export function DeleteRef({ name, commit, acts, onDeleted, initial }: {
  name: string;
  /** The branch's whole commit id, as the list showed it. */
  commit: string;
  acts: RefActs;
  onDeleted: (deleted: { act: string; name: string }) => void;
  /** Open at a step already, as its stories show each. */
  initial?: { step: 'confirm' | 'said'; sentence?: string };
}) {
  const { t } = useTranslation();
  const [step, setStep] = useState<Step>(
    initial?.step === 'confirm' ? { is: 'confirm' } : initial?.step === 'said' ? { is: 'said', sentence: initial.sentence ?? '' } : { is: 'idle' },
  );

  const press = async () => {
    setStep({ is: 'working' });
    try {
      const outcome = await acts.remove({ kind: 'branch', name, at: commit });
      if (outcome.refused) setStep({ is: 'said', sentence: outcome.said });
      else if (outcome.outcome === 'not-done') setStep({ is: 'said', sentence: t('projects.git.ref.delete.notDone', { said: outcome.said }) });
      else if (outcome.act && (outcome.outcome === 'done' || outcome.outcome === 'partial')) onDeleted({ act: outcome.act, name });
      else setStep({ is: 'said', sentence: t('projects.git.ref.unknown') });
    } catch (error) {
      setStep({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  if (step.is === 'idle') {
    return (
      <div>
        <Button variant="ghost" className="text-small" aria-label={t('projects.git.ref.delete.openLabel', { name })} onClick={() => setStep({ is: 'confirm' })}>
          {t('projects.git.ref.delete.open')}
        </Button>
      </div>
    );
  }

  return (
    <div role="group" aria-label={t('projects.git.ref.delete.openLabel', { name })} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      {step.is === 'said'
        ? <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{step.sentence}</p>
        : <Prose className="text-small [overflow-wrap:anywhere]">{t('projects.git.ref.delete.confirm', { name, id: short(commit) })}</Prose>}
      <div className="flex flex-wrap gap-2">
        {step.is !== 'said' && (
          <Button variant="primary" disabled={step.is === 'working'} onClick={() => { void press(); }}>{t('projects.git.ref.delete.press')}</Button>
        )}
        <Button variant="ghost" disabled={step.is === 'working'} onClick={() => setStep({ is: 'idle' })}>{t('projects.git.ref.cancel')}</Button>
      </div>
    </div>
  );
}

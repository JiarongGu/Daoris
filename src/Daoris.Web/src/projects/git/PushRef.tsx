import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose } from '../../design/ui';
import type { PushActs, RefKind } from './NewRef';

const short = (commit: string) => commit.slice(0, 7);

type Step = { is: 'idle' } | { is: 'confirm' } | { is: 'working' } | { is: 'said'; sentence: string; pushed?: boolean };

/**
 * **Push a branch or a tag**: a press on one of *Yours*' rows, or on a tag just made, that says what
 * will move on origin and sends nothing until it is confirmed. The press carries the commit the page showed, so the driver refuses a ref
 * that has moved. A branch moves only to its own commit, never forced; a tag is a Dangerous push, which the place's Dangerous value
 * decides at the door, and the confirmation says which. A push has no *Undo*: Daoris never deletes a ref on origin.
 *
 * @remarks
 * **A molecule**: the push arrives as a prop. It is absent where the driver does not offer a push.
 */
export function PushRef({ kind, name, commit, push, initial }: {
  kind: RefKind;
  name: string;
  /** The ref's whole commit id, as the page showed it. */
  commit: string;
  push: PushActs;
  /** Open at a step already, as its stories show each. */
  initial?: { step: 'confirm' | 'said'; sentence?: string };
}) {
  const { t } = useTranslation();
  const [step, setStep] = useState<Step>(
    initial?.step === 'confirm' ? { is: 'confirm' } : initial?.step === 'said' ? { is: 'said', sentence: initial.sentence ?? '' } : { is: 'idle' },
  );
  if (!push.offered) return null;

  const press = async () => {
    setStep({ is: 'working' });
    try {
      const outcome = await push.press({ kind, name, at: commit });
      if (outcome.refused) setStep({ is: 'said', sentence: outcome.said });
      else if (outcome.outcome === 'done') setStep({ is: 'said', sentence: t(`projects.git.push.done.${kind}`, { name }), pushed: true });
      else if (outcome.outcome === 'not-done') setStep({ is: 'said', sentence: t('projects.git.push.notDone', { said: outcome.notes ?? outcome.said }) });
      else setStep({ is: 'said', sentence: t('projects.git.ref.unknown') });
    } catch (error) {
      setStep({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  const label = t(`projects.git.push.openLabel.${kind}`, { name });
  if (step.is === 'idle') {
    return (
      <div>
        <Button variant="ghost" className="text-small" aria-label={label} onClick={() => setStep({ is: 'confirm' })}>
          {t(`projects.git.push.open.${kind}`)}
        </Button>
      </div>
    );
  }

  const where = push.address ? t('projects.git.push.to', { address: push.address }) : t('projects.git.push.toOrigin');
  return (
    <div role="group" aria-label={label} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      {step.is === 'said'
        ? <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{step.sentence}</p>
        : (
          <Prose className="text-small [overflow-wrap:anywhere]">
            {t(`projects.git.push.confirm.${kind}`, { name, id: short(commit), where })}
            {kind === 'tag' && ` ${t(push.dangerous === 'allow' ? 'projects.git.push.dangerous.allow' : 'projects.git.push.dangerous.deny')}`}
          </Prose>
        )}
      <div className="flex flex-wrap gap-2">
        {step.is !== 'said' && (
          <Button variant="primary" disabled={step.is === 'working'} onClick={() => { void press(); }}>{t(`projects.git.push.press.${kind}`)}</Button>
        )}
        <Button variant="ghost" disabled={step.is === 'working'} onClick={() => setStep({ is: 'idle' })}>
          {step.is === 'said' && step.pushed ? t('projects.git.ref.done') : t('projects.git.ref.cancel')}
        </Button>
      </div>
    </div>
  );
}

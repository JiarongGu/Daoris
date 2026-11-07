import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose } from '../../design/ui';
import type { FetchActs } from './NewRef';

type Step =
  | { is: 'idle' }
  | { is: 'confirm' }
  | { is: 'working' }
  | { is: 'said'; sentence: string };

/**
 * **Fetch origin**: a disclosure on a repository's Branches that says what will move
 * (only the `origin/…` branches here; tags, the person's own branches and their last fetch's record are left) and sends nothing until it
 * is confirmed. The driver answers as data: what it made, moved and removed in its own words, refused in its sentence, or not known,
 * which is said and never retried. It draws nothing where the driver does not offer the fetch, and says why only where origin's settings
 * refuse it.
 *
 * @remarks
 * **A molecule**: the fetch arrives as a prop.
 */
export function FetchOrigin({ fetch, initial }: {
  fetch: FetchActs;
  /** Open at a step already, as its stories show each. */
  initial?: { step: 'confirm' | 'said'; sentence?: string };
}) {
  const { t } = useTranslation();
  const heading = useId();
  const [step, setStep] = useState<Step>(
    initial?.step === 'confirm' ? { is: 'confirm' } : initial?.step === 'said' ? { is: 'said', sentence: initial.sentence ?? '' } : { is: 'idle' },
  );

  if (!fetch.offered) {
    return fetch.reason === 'widened' && fetch.why
      ? <Prose className="text-small text-ink-soft [overflow-wrap:anywhere]">{t('projects.git.fetch.widened', { why: fetch.why })}</Prose>
      : null;
  }

  const press = async () => {
    setStep({ is: 'working' });
    try {
      const outcome = await fetch.press();
      if (outcome.refused) setStep({ is: 'said', sentence: outcome.said });
      else if (outcome.outcome === 'done') setStep({ is: 'said', sentence: t('projects.git.fetch.done', { notes: outcome.notes ?? '' }).trim() });
      else if (outcome.outcome === 'not-done') setStep({ is: 'said', sentence: t('projects.git.fetch.notDone', { said: outcome.notes ?? outcome.said }) });
      else setStep({ is: 'said', sentence: t('projects.git.ref.unknown') });
    } catch (error) {
      setStep({ is: 'said', sentence: error instanceof Error ? error.message : t('projects.git.ref.failed') });
    }
  };

  if (step.is === 'idle') {
    return <div><Button variant="ghost" onClick={() => setStep({ is: 'confirm' })}>{t('projects.git.fetch.open')}</Button></div>;
  }

  return (
    <div role="group" aria-labelledby={heading} className="grid min-w-0 gap-2 rounded-control border border-line p-3">
      <h3 id={heading} className="m-0 text-small font-semibold text-ink-faint">{t('projects.git.fetch.heading')}</h3>
      {step.is === 'said'
        ? <p role="status" className="m-0 text-small text-ink [overflow-wrap:anywhere]">{step.sentence}</p>
        : <Prose className="text-small [overflow-wrap:anywhere]">{t('projects.git.fetch.confirm', { address: fetch.address ?? 'origin' })}</Prose>}
      <div className="flex flex-wrap gap-2">
        {step.is !== 'said' && (
          <Button variant="primary" disabled={step.is === 'working'} onClick={() => { void press(); }}>{t('projects.git.fetch.press')}</Button>
        )}
        <Button variant="ghost" disabled={step.is === 'working'} onClick={() => setStep({ is: 'idle' })}>
          {t(step.is === 'said' ? 'projects.git.ref.done' : 'projects.git.ref.cancel')}
        </Button>
      </div>
    </div>
  );
}

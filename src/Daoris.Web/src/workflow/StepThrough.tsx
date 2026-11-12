import { useTranslation } from 'react-i18next';
import type { RunStep } from './run';

/**
 * A run's step the runtime passed on the person's through words: *Accepted on your words* on a review, *Passed on your words* on an
 * unchecked done, with the quote and the ask it is kept on, so the person reads which of their words moved it with no press of theirs.
 * Nothing for a step any other word moved.
 */
export function StepThrough({ step }: { step: RunStep }) {
  const { t } = useTranslation();
  if (!step.through) return null;
  const words = { quote: step.through.quote, ask: step.through.ask };
  return (
    <p data-step-through="" className="m-0 text-small text-ink">
      {t(step.state === 'skipped' ? 'workflow.run.through.passed' : 'workflow.run.through.accepted', words)}
    </p>
  );
}

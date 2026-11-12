import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../locales/format';
import { Button } from '../design/ui';
import type { CheckHeldAnswer, CheckHeldAsk, RunStep } from './run';

/**
 * **A failed plugin step held for the person**: under its node, *Try again*, the held attempt's `again` (`WORK_HELD`), which
 * What needs you presses too, so a failed step is tried again one way. The word given is said until the driver's next look applies it.
 *
 * A molecule: the step and the press arrive as props; the driver's refusal is said where it is pressed.
 */
export function FailedStepHeld({ step, onHeld }: {
  step: RunStep;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const { held } = step;
  if (!held) return null;
  if (held.resolution === 'again') return <p className="m-0 text-small text-ink-soft">{t('workflow.run.step.said.again')}</p>;
  if (!onHeld || !held.words.includes('again')) return null;

  const tryAgain = () => {
    setSending(true);
    setRefused(null);
    onHeld({ unit: held.unit, node: step.step.id, attempt: held.attempt, resolution: 'again' })
      .then((answer) => { if (!answer.done) setRefused(answer.message ?? answer.refused ?? ''); })
      .catch((error: unknown) => setRefused(sentence(error)))
      .finally(() => setSending(false));
  };

  return (
    <div className="grid min-w-0 gap-1">
      <div className="flex flex-wrap items-center gap-1.5">
        <Button variant="ghost" disabled={sending} className="min-h-0 px-1.5 py-0.5 text-small" onClick={tryAgain}>
          {t('workflow.run.step.tryAgain')}
        </Button>
      </div>
      {refused && <p role="alert" className="m-0 text-small text-ink">{refused}</p>}
    </div>
  );
}

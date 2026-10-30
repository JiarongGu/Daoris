import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../locales/format';
import { Button } from '../design/ui';
import type { CheckHeldAnswer, CheckHeldAsk, RunStep } from './run';

/**
 * **A check held unknown for the person** (FABRIC-PIPELINE1d, the pipeline design §4.1, §4.5): under its node, *Run again* (a new attempt at
 * the same tip) and *Not now* (nothing more is asked of it), the words the driver offers on the held attempt (`held.words`). No person's word
 * settles a check's verdict, so these are the only two. A word given and not applied yet is drawn by the node's own words instead.
 *
 * A molecule: the step and the press arrive as props; the driver's refusal is said where it was pressed.
 */
export function CheckHeld({ step, onHeld }: {
  step: RunStep;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const { held } = step;
  if (!onHeld || !held || held.resolution) return null;
  const words = held.words.filter((word): word is 'again' | 'stop' => word === 'again' || word === 'stop');
  if (words.length === 0) return null;

  const send = (resolution: 'again' | 'stop') => {
    setSending(true);
    setRefused(null);
    onHeld({ unit: held.unit, node: step.step.id, attempt: held.attempt, resolution })
      .then((answer) => { if (!answer.done) setRefused(answer.message ?? answer.refused ?? ''); })
      .catch((error: unknown) => setRefused(sentence(error)))
      .finally(() => setSending(false));
  };

  return (
    <div className="grid min-w-0 gap-1">
      <div className="flex flex-wrap items-center gap-1.5">
        {words.map((word) => (
          <Button key={word} variant="ghost" disabled={sending} className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => send(word)}>
            {t(`workflow.run.check.${word}`)}
          </Button>
        ))}
      </div>
      {refused && <p role="alert" className="m-0 text-small text-ink">{refused}</p>}
    </div>
  );
}

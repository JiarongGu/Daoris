import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../../locales/format';
import { Button } from '../../design/ui';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';
import type { CheckHeldAnswer, CheckHeldAsk, RunStep } from '../run';

/**
 * **A plugin step held for its holder**: under its node, on a failed step *Try again*, the held attempt's `again` (`WORK_HELD`),
 * which What needs you presses too, so a failed step is tried again one way; and on a failed step or one under way *Done outside
 * Daoris…* (`done-outside`), its link required and a note if given, which settles the step on the holder's word, stands its attempt
 * down and lets the run go on (DONEBYHOLDER1). The word given is said until the driver's next look applies it.
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
  const [asking, setAsking] = useState(false);
  const [link, setLink] = useState('');
  const [note, setNote] = useState('');
  const { held } = step;
  if (!held) return null;
  if (held.resolution === 'again') return <p className="m-0 text-small text-ink-soft">{t('workflow.run.step.said.again')}</p>;
  if (held.resolution === 'done-outside') return <p className="m-0 text-small text-ink-soft">{t('workflow.run.step.said.done-outside')}</p>;
  const again = held.words.includes('again');
  const outside = held.words.includes('done-outside');
  if (!onHeld || (!again && !outside)) return null;

  const tryAgain = () => {
    setSending(true);
    setRefused(null);
    onHeld({ unit: held.unit, node: step.step.id, attempt: held.attempt, resolution: 'again' })
      .then((answer) => { if (!answer.done) setRefused(answer.message ?? answer.refused ?? ''); })
      .catch((error: unknown) => setRefused(sentence(error)))
      .finally(() => setSending(false));
  };

  const doneOutside = (answered: Answered) => {
    const said = note.trim();
    onHeld({ unit: held.unit, node: step.step.id, attempt: held.attempt, resolution: 'done-outside', link: link.trim(), ...(said ? { note: said } : {}) })
      .then((answer) => (answer.done ? answered.done() : answered.refused(answer.message ?? answer.refused ?? '')))
      .catch((error: unknown) => answered.refused(sentence(error)));
  };

  return (
    <div className="grid min-w-0 gap-1">
      <div className="flex flex-wrap items-center gap-1.5">
        {again && (
          <Button variant="ghost" disabled={sending} className="min-h-0 px-1.5 py-0.5 text-small" onClick={tryAgain}>
            {t('workflow.run.step.tryAgain')}
          </Button>
        )}
        {outside && !asking && (
          <Button variant="ghost" disabled={sending} className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => setAsking(true)}>
            {t('workflow.run.step.doneOutside')}
          </Button>
        )}
      </div>
      {asking && (
        <InlineConfirm
          block
          tone="primary"
          label={t('workflow.run.step.doneOutsideMeanIt')}
          says={t('workflow.run.step.said.done-outside')}
          meanIt={t('workflow.run.step.doneOutsideMeanIt')}
          ready={link.trim().length > 0}
          onConfirm={doneOutside}
          onClose={() => setAsking(false)}
        >
          <input
            aria-label={t('workflow.run.step.doneOutsideLink')}
            placeholder={t('workflow.run.step.doneOutsideLink')}
            value={link}
            onChange={(event) => setLink(event.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
          <input
            aria-label={t('workflow.run.step.doneOutsideNote')}
            placeholder={t('workflow.run.step.doneOutsideNote')}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            className="min-h-[1.9rem] min-w-0 basis-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
        </InlineConfirm>
      )}
      {refused && <p role="alert" className="m-0 text-small text-ink">{refused}</p>}
    </div>
  );
}

import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type Answered, Refused } from '../../work/InlineConfirm';

/** The person's answer to an open question of an ask's delivery plan, or their waiver of a release order with a reason. */
export type PlanSettle = { key: string; words: string } | { deployable: string; on: string; reason: string };

/** A settle, and how it ended. */
export type PlanSettling = (settle: PlanSettle, answered: Answered) => void;

/**
 * **One word and one press** on a delivery plan: the person's words and the press that gives them, to answer a question or to waive an
 * order with their reason. The press waits in place, and a refusal is the service's sentence said beside it, the words kept.
 *
 * @remarks
 * A molecule, props only. The words are needed: the press stays disabled until there are some, since an answer or a waiver with no
 * words is refused by the service and says nothing the lead could use.
 */
export function PlanSettleForm({ label, press, onSettle }: {
  /** What the words are: the field's name, and its placeholder. */
  label: string;
  /** What the press is called. */
  press: string;
  /** Gives the words; `answered` tells how it ended. */
  onSettle: (words: string, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [words, setWords] = useState('');
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);
  const refusalId = useId();
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);
  const give = () => {
    setRefusal(null);
    setPending(true);
    onSettle(words.trim(), {
      done: () => { if (alive.current) { setPending(false); setWords(''); } },
      refused: (sentence) => { if (alive.current) { setPending(false); setRefusal(sentence); } },
    });
  };
  return (
    <div className="mt-1 flex flex-wrap items-center gap-2">
      <input
        aria-label={label}
        placeholder={label}
        value={words}
        onChange={(event) => setWords(event.target.value)}
        className="min-h-[1.9rem] flex-1 basis-56 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
      />
      <Button disabled={pending || words.trim() === ''} aria-describedby={refusal ? refusalId : undefined} onClick={give}>{press}</Button>
      <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
        {pending ? t('work.confirm.pending') : null}
      </span>
      {refusal && <Refused id={refusalId} sentence={refusal} />}
    </div>
  );
}

import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TrustHold } from '../signals';
import { Button, PathText, WaitingCard } from '../ui';
import { type Answered, Refused } from './InlineConfirm';

/**
 * The agent's trust question, asked by Daoris for a folder the driver is holding (D73).
 *
 * @remarks
 * **The grant is the person's, and this is where they give it.** The agent ignores a folder's own
 * `permissions.allow` until someone has trusted it there (DEPLOY1, both doors, measured), so the
 * driver holds rather than spend a session that could not take its quest. The flag is the person's
 * grant, so this asks exactly what the agent would ask, names the one file that is written, and
 * grants only on the press. Nothing grants it at adoption, sync or spawn.
 *
 * **Never wider than the hold.** It is handed a hold the driver produced: the folder and the file,
 * as a pair. The shell refuses any other pair, and the terminal's `daoris agent trust … --yes` is
 * the door that names any folder.
 *
 * A molecule: handed the hold, it reports the grant and the putting-down. It sits inline on a
 * quest's page and inside the drawer the band's row opens.
 *
 * **It keeps its card and answers in it** (UXFIX2b3b): it is the agent's own question, not a destructive edit's second
 * press, so it is not an `InlineConfirm`. It takes the same `Answered`: it waits saying so while the grant is written, and a
 * refusal is said inside it (not toasted) with the grant pressable again. The caller puts it down once the grant landed.
 * Trust is outward and loses nothing, so the grant stays the primary's hue.
 */
export function TrustAsk({ hold, busy = false, onGrant, onCancel }: {
  hold: TrustHold;
  /** The grant is being written: the press is held so it cannot be given twice. */
  busy?: boolean;
  /** The grant, and how to tell the question how it ended (UXFIX2b3b): a refusal is said inside it, not toasted. */
  onGrant: (answered: Answered) => void;
  /** Put the question down. Absent where there is nothing to return to. */
  onCancel?: () => void;
}) {
  const { t } = useTranslation();
  const refusalId = useId();
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);
  const turn = useRef(0);
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);

  const grant = () => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    onGrant({
      // The caller puts the question down; here the wait only lets go.
      done: () => { if (mine === turn.current && alive.current) setPending(false); },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };
  const waiting = busy || pending;

  return (
    <WaitingCard title={t('trust.title')}>
      <p className="m-0 mt-1.5 text-meta text-ink"><PathText path={hold.folder} /></p>
      <p className="m-0 mt-2 text-body leading-relaxed">{t('trust.what')}</p>
      <p className="m-0 mt-1.5 text-small text-ink-soft">
        {hold.quest
          ? t('trust.holding.quest', { id: hold.quest })
          : t('trust.holding.ask', { id: hold.ask ?? '' })}
      </p>
      <p className="m-0 mt-2 text-small text-ink-faint">{t('trust.file')}</p>
      <p className="m-0 mt-0.5 text-meta text-ink-faint"><PathText path={hold.trustFile} /></p>
      <div className="mt-3 flex flex-wrap gap-2">
        <Button
          variant="primary"
          disabled={waiting}
          aria-describedby={refusal ? refusalId : undefined}
          onClick={grant}
        >
          {t('trust.grant')}
        </Button>
        {onCancel && <Button variant="ghost" disabled={pending} onClick={onCancel}>{t('trust.cancel')}</Button>}
        {/* Drawn from the first, so it is a live region before it speaks (UXFIX2c). */}
        <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
          {pending ? t('work.confirm.pending') : null}
        </span>
        {refusal && <Refused id={refusalId} sentence={refusal} />}
      </div>
    </WaitingCard>
  );
}

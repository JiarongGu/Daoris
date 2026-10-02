import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, WaitingCard } from '../ui';

/** What the ledger lets a person do from `awaiting-person` — and nothing this surface invented. */
export type Resolution = 'completed' | 'declined' | 'stopped';

/**
 * A session parked at a checkpoint, and the person's answer to it (design §4).
 *
 * @remarks
 * **`AwaitingPerson` has meant "only the person can clear this" since D46 and had no surface at
 * all.** This is it: the analysis at the top, verbatim — `autonomous-development` asks a park for
 * options, a recommendation and a reason, and none of those survive rewording — and beneath it
 * exactly the three moves the ledger already allows.
 *
 * **Three, not four, and no new states.** The ledger also allows `awaiting-person → working`; that
 * one is the driver observing a session that carried on, which a person causes by *answering* it
 * in the composer. A button for it would be a second way to say the same thing, and the surface
 * says so instead. **The third, `stopped`, is the page header's** (SESSUX1d, D126 §3.3): a session's stop has one
 * owner, so this card keeps *Answer and carry on…*, *Finish* and *Decline…*.
 *
 * **Unless nothing is left to take a message (STANDDOWN2).** A driven session that took its quest and
 * ended its turn to ask the person has no process: its answer is a move here, handed `onAnswer`. The
 * record stays parked with the words (ANSWER1b), and the same session goes on with them at the driver's
 * next look (D131); until then the head shows `AnsweredPark` in this card's place (ANSWER1c). Then the
 * composer hint would send the person nowhere, and it is not shown.
 *
 * **Declining asks for its reason in place**, the same two-step a quest's page uses and for the
 * same reason: the note is the only part whoever reads the record later can act on, and a decline
 * that slipped out on one click would routinely carry nothing.
 *
 * A molecule: it is handed the note and reports a move, so a parked session with a three-paragraph
 * analysis and one with none are both reachable by passing them.
 */
export function AwaitingPerson({ note, pending = false, onResolve, onAnswer }: {
  /** The session's own analysis, rendered word for word. */
  note?: string | null;
  pending?: boolean;
  /** The ledger's moves this card makes, finish and decline, where this surface can make them (the shell). */
  onResolve?: (state: Resolution, note: string | null) => void;
  /** The answer to a session with no process left — its quest carried on with the words (STANDDOWN2). */
  onAnswer?: (answer: string | null) => void;
}) {
  const { t } = useTranslation();
  const [declining, setDeclining] = useState(false);
  const [answering, setAnswering] = useState(false);
  const [reason, setReason] = useState('');
  const [words, setWords] = useState('');

  const field = 'min-h-14 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink';

  return (
    <WaitingCard title={t('work.head.waiting')}>

      {note && (
        <p className="m-0 mt-1.5 whitespace-pre-wrap text-body leading-relaxed">{note}</p>
      )}

      <p className="m-0 mt-2 text-small text-ink-faint">{t('work.awaiting.hint')}</p>

      {answering && onAnswer
        ? (
          <div className="mt-2.5 grid gap-2">
            <label className="grid gap-1 text-small text-ink-faint">
              <span className="sr-only">{t('work.awaiting.answerPlaceholder')}</span>
              <textarea
                autoFocus
                value={words}
                aria-label={t('work.awaiting.answerPlaceholder')}
                onChange={(event) => setWords(event.target.value)}
                placeholder={t('work.awaiting.answerPlaceholder')}
                className={field}
              />
            </label>
            <div className="flex flex-wrap gap-2">
              <Button variant="primary" disabled={pending} onClick={() => onAnswer(words.trim() || null)}>
                {t('work.awaiting.answerConfirm')}
              </Button>
              <Button variant="ghost" onClick={() => setAnswering(false)}>{t('common.cancel')}</Button>
            </div>
          </div>
        )
        : declining && onResolve
        ? (
          <div className="mt-2.5 grid gap-2">
            <label className="grid gap-1 text-small text-ink-faint">
              <span className="sr-only">{t('work.awaiting.declinePlaceholder')}</span>
              <textarea
                autoFocus
                value={reason}
                aria-label={t('work.awaiting.declinePlaceholder')}
                onChange={(event) => setReason(event.target.value)}
                placeholder={t('work.awaiting.declinePlaceholder')}
                className={field}
              />
            </label>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="danger"
                disabled={!reason.trim() || pending}
                onClick={() => onResolve('declined', reason.trim())}
              >
                {t('work.awaiting.declineConfirm')}
              </Button>
              <Button variant="ghost" onClick={() => setDeclining(false)}>{t('common.cancel')}</Button>
            </div>
          </div>
        )
        : (
          <div className="mt-2.5 flex flex-wrap gap-2">
            {onAnswer && (
              <Button variant="primary" disabled={pending} onClick={() => setAnswering(true)}>
                {t('work.awaiting.carryOn')}
              </Button>
            )}
            {onResolve && (
              <>
                <Button variant={onAnswer ? 'default' : 'primary'} disabled={pending} onClick={() => onResolve('completed', null)}>
                  {t('work.awaiting.finish')}
                </Button>
                <Button disabled={pending} onClick={() => setDeclining(true)}>
                  {t('work.awaiting.decline')}
                </Button>
              </>
            )}
          </div>
        )}

      {!onAnswer && <p className="m-0 mt-2 text-small text-ink-faint">{t('work.awaiting.answer')}</p>}
    </WaitingCard>
  );
}

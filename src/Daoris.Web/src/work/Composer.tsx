import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Tip } from '../ui';

/**
 * The input a person talks to a session through (SES2), with its **two distinct endings**.
 *
 * @remarks
 * **Finish and stop mean different things and are never one button.** Finishing closes the input
 * and lets the harness wind up on its own — the record ends `completed`. Stopping cuts it off, and
 * the record says the person did (`stopped`). Collapsing them would make the ledger's two endings
 * indistinguishable in the one place a person chooses between them.
 *
 * **A session that ends mid-sentence keeps what was typed.** The draft stays, disabled, with the
 * ending stated — because the alternative is a box that silently swallows a paragraph somebody was
 * halfway through, and they have no way to get it back.
 *
 * **A refusal renders verbatim**, as every service and driver sentence does: "nothing is listening"
 * is the ledger's answer, and rewriting it here would be the second copy of a sentence.
 *
 * **`endings` is the verb-ownership rule** (D56). A parked session used to render the attention
 * band's *finish it · decline… · stop it* and this form's *send · finish · stop* at the same time,
 * 400px apart — two owners for one set of moves, which is worse than either. So the state decides:
 * while a session is `awaiting-person` the band owns the endings and this form keeps `send` alone,
 * under the band's own sentence saying that answering is a message rather than one of those moves.
 * No capability is lost; every move stays reachable wherever it is legal, from exactly one place.
 */
export function Composer({ live, sending = false, refusal, endings = true, onSend, onFinish, onStop }: {
  /** Whether anything is listening. False is an ending, not a disabled state. */
  live: boolean;
  sending?: boolean;
  /** The last refusal, shown inline and word for word. */
  refusal?: string | null;
  /** Whether this form owns the two endings, or something else on screen does. */
  endings?: boolean;
  onSend: (text: string) => void;
  onFinish: () => void;
  onStop: () => void;
}) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState('');

  const say = () => {
    const text = draft.trim();
    if (!text || !live || sending) return;
    onSend(text);
    setDraft('');
  };

  return (
    <form
      className="grid gap-2 border-t border-line px-4 py-3"
      onSubmit={(event) => { event.preventDefault(); say(); }}
    >
      {refusal && <p className="m-0 text-small text-st-declined">{refusal}</p>}
      {!live && <p className="m-0 text-small text-ink-soft">{t('work.composer.over')}</p>}

      <label className="grid gap-1 text-small text-ink-faint">
        <span className="sr-only">{t('work.composer.label')}</span>
        <textarea
          value={draft}
          disabled={!live}
          aria-label={t('work.composer.label')}
          onChange={(event) => setDraft(event.target.value)}
          // Enter sends, because this is a conversation; a newline needs the modifier, because a
          // paste of several lines is one message and should stay one.
          onKeyDown={(event) => {
            if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); say(); }
          }}
          placeholder={t('work.composer.placeholder')}
          className="min-h-14 resize-y rounded-control border border-line bg-raised px-2.5 py-1.5 text-body text-ink disabled:opacity-55"
        />
      </label>

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="primary" disabled={!live || !draft.trim() || sending} onClick={say}>
          {t('work.composer.send')}
        </Button>
        {live && endings && (
          <>
            <Tip content={t('work.composer.finishTip')}>
              <Button type="button" onClick={onFinish}>{t('work.composer.finish')}</Button>
            </Tip>
            <Tip content={t('work.composer.stopTip')}>
              <Button type="button" variant="danger" onClick={onStop}>{t('work.composer.stop')}</Button>
            </Tip>
          </>
        )}
      </div>
    </form>
  );
}

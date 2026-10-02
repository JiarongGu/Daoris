import { useTranslation } from 'react-i18next';

/**
 * A park the person answered, until the driver takes it up (ANSWER1c; D131, answer-continues design §5).
 *
 * @remarks
 * **The answer keeps the park** (ANSWER1b): the record stays `awaiting-person` with the answer set, and at the driver's
 * next look the same session goes on, its own conversation resumed with the answer as its next prompt. For up to one
 * look the record still says it waits on the person, and the card that asked would offer the answer box again. This
 * card says what is true instead: what they answered, word for word, and that the same session goes on with it.
 *
 * **It asks nothing.** No box, no *Finish* or *Decline…*, and not the waiting hue, which is the person's alone (D126
 * §2.3): nothing here waits on them. Its stop is the page header's, as every session's is (SESSUX1d, D126 §3.3). Where
 * the driver cannot resume the conversation it carries the quest on in a new session and says why (D131 §3); the
 * record that follows says so, and this card is gone by then.
 *
 * A molecule: handed the answer, so a long one, a blank one the service kept as *carry on.*, and a 中文 one are each
 * reached by passing it.
 */
export function AnsweredPark({ answer }: {
  /** The person's words as the record keeps them: content, never translated. */
  answer: string;
}) {
  const { t } = useTranslation();

  return (
    <section className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{t('work.awaiting.yourAnswer')}</h3>
      <p className="m-0 mt-1.5 whitespace-pre-wrap text-body leading-relaxed">{answer}</p>
      <p className="m-0 mt-2 text-small text-ink-faint">{t('work.awaiting.goesOn')}</p>
    </section>
  );
}

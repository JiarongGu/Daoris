import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Icon } from '../../design/ui';
import { cn } from '../../lib/cn';
import { clockOf } from '../../locales/format';
import type { ExchangeCard } from './exchanges';

/** The helper's own state words the card says (`HelperAskRow.state`); another is said as asked. */
const STATES = ['waiting', 'approved', 'started', 'answered', 'refused', 'cooling'] as const;
type State = (typeof STATES)[number];
const stateOf = (state: string | null): State | 'asked' => (STATES.includes(state as State) ? (state as State) : 'asked');

/**
 * A request drawn whole once, at the call that asked it: who is asked and as what, when, where it
 * stands, the question as written, and where its answer goes. Its answer folds in once the page holds the turn that took it, a claim
 * to check and never the person's words; *Open its conversation* opens the helper's session once it has one.
 */
export function ExchangeCardView({ card, revealed, onOpen }: {
  card: ExchangeCard;
  /** The page is brought here from the turn that took its answer: the card takes the focus, so the keyboard goes on from it. */
  revealed?: boolean;
  onOpen?: (session: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const [all, setAll] = useState(false);
  const box = useRef<HTMLElement>(null);
  useEffect(() => {
    if (revealed) box.current?.focus({ preventScroll: true });
  }, [revealed]);

  const clock = (at: string) => clockOf(at, new Date(), i18n.language);
  const state = stateOf(card.state);
  const said = [
    clock(card.at),
    card.answer
      ? `${t('work.conversation.asked.answeredAt', { time: clock(card.answer.at) })} · ${t('work.conversation.asked.takenNext')}`
      : t(`work.conversation.asked.state.${state}`),
  ].join(' · ');
  const answerHead = card.agent
    ? t('work.conversation.asked.answer', { agent: card.agent })
    : t('work.conversation.asked.answerAny');

  return (
    <section
      ref={box}
      tabIndex={-1}
      data-exchange={card.id}
      aria-label={card.agent ? t('work.conversation.asked.helper', { agent: card.agent }) : t('work.conversation.asked.anyHelper')}
      className="rounded-card border border-line border-l-[3px] border-l-line-strong bg-raised px-3 py-2 outline-none focus-visible:ring-2 focus-visible:ring-accent/60"
    >
      <div className="flex flex-wrap items-baseline justify-between gap-x-2">
        <span className="text-meta text-ink-soft">
          {card.agent ? t('work.conversation.asked.helper', { agent: card.agent }) : t('work.conversation.asked.anyHelper')}
        </span>
        <span data-exchange-state className="text-meta text-ink-faint">{said}</span>
      </div>
      <p className="m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body text-ink">{card.question}</p>
      {card.answer ? (
        <div className="mt-1.5 border-l-2 border-line pl-2">
          <p className="m-0 text-meta text-ink-faint">{answerHead}</p>
          <p data-exchange-answer className={cn('m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body text-ink-soft', !all && 'line-clamp-3')}>
            {card.answer.text}
          </p>
          <button
            type="button"
            aria-expanded={all}
            onClick={() => setAll((was) => !was)}
            className="border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
          >
            {all ? t('work.conversation.hide') : t('work.conversation.show')}
          </button>
        </div>
      ) : state !== 'refused' && (
        <p className="m-0 mt-1 text-meta text-ink-faint">{t('work.conversation.asked.comesBack')}</p>
      )}
      {card.helper && onOpen && (
        <div className="mt-1.5 flex justify-end">
          <Button variant="ghost" onClick={() => onOpen(card.helper!)} className="text-small">
            {t('work.conversation.asked.open')}
          </Button>
        </div>
      )}
    </section>
  );
}

/**
 * The turn that took a card's answer: one line that opens the card, where the answer is drawn, rather than the answer
 * drawn a second time.
 */
export function TakenLine({ card, onShow }: { card: ExchangeCard; onShow?: (call: string) => void }) {
  const { t } = useTranslation();
  const words = card.agent ? t('work.conversation.asked.began', { agent: card.agent }) : t('work.conversation.asked.beganAny');
  if (!onShow) return <p data-taken={card.id} className="m-0 text-meta text-ink-faint">{words}</p>;
  return (
    <button
      type="button"
      data-taken={card.id}
      onClick={() => onShow(card.call)}
      className="inline-flex items-center gap-1 justify-self-start border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
    >
      {words}
      <Icon name="chevronUp" size={12} />
    </button>
  );
}

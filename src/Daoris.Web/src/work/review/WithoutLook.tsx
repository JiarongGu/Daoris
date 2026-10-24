import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type Answered, InlineConfirm } from '../InlineConfirm';

/**
 * *Accept without the look…* on a chat run's work (CHATFLOW1d2, the chat-flow design §5.3): a conversation publishes no set-up step, so
 * no look can be set up for its work, and the person's way past the look its rule requires is a press of their own with their words.
 * The press is the Accept itself, judged and landed at the tip it captures, the words kept on that tip; it asks for them before it goes.
 */
export function WithoutLook({ onPress, busy = false }: {
  /** The Accept with the person's words, told back to the ask as every press is. */
  onPress: (answered: Answered, words: string) => void;
  /** A landing is on its way: the press waits for it. */
  busy?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [words, setWords] = useState('');
  if (!open) {
    return (
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 border-l-[3px] border-st-open pl-2 text-small text-ink-soft">
        <span>{t('work.review.withoutLookWaits')}</span>
        <Button className="px-2 py-0.5 text-small" disabled={busy} onClick={() => setOpen(true)}>{t('work.review.withoutLook')}</Button>
      </p>
    );
  }

  return (
    <InlineConfirm
      tone="primary"
      block
      label={t('work.review.withoutLookPress')}
      says={<p className="m-0 text-small text-ink-soft">{t('work.review.withoutLookLimit')}</p>}
      meanIt={t('work.review.withoutLookPress')}
      busy={busy}
      ready={words.trim().length > 0}
      onConfirm={(answered) => onPress(answered, words.trim())}
      onClose={() => {
        setOpen(false);
        setWords('');
      }}
    >
      <label className="flex min-w-0 flex-1 basis-64 flex-col gap-1 text-small text-ink-soft">
        <span>{t('work.review.withoutLookWords')}</span>
        <input
          value={words}
          onChange={(event) => setWords(event.target.value)}
          className="min-h-[1.9rem] min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
        />
      </label>
    </InlineConfirm>
  );
}

import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChannelAnswered } from '../../asks/ChannelSection';
import { Button } from '../../design/ui';
import { type ChannelPost, channelRefusal } from '../channel';
import { Refused } from '../InlineConfirm';

/**
 * The person's answer to a channel message, in place under its row: their words, to the repository that posted it (everyone where
 * it names none), answering that message. A refusal is the service's code, said under the box in the reader's language; the words
 * stay for another try. Sent, it closes.
 */
export function ChannelReply({ to, re, onPost, onClose }: {
  to: string | null;
  re: string;
  onPost: (post: ChannelPost, answered: ChannelAnswered) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [words, setWords] = useState('');
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);

  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);

  const send = () => {
    setRefusal(null);
    setPending(true);
    onPost({ text: words.trim(), to, re }, {
      done: () => {
        if (!alive.current) return;
        setPending(false);
        onClose();
      },
      refused: (code) => {
        if (!alive.current) return;
        setPending(false);
        setRefusal(code);
      },
    });
  };

  return (
    <div className="ml-4 grid gap-2">
      <textarea
        aria-label={t('asks.channel.compose.words')}
        placeholder={t('asks.channel.compose.words')}
        rows={2}
        value={words}
        onChange={(e) => setWords(e.target.value)}
        className="min-w-0 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
      />
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <Button variant="primary" disabled={pending || words.trim() === ''} onClick={send}>{t('asks.channel.compose.send')}</Button>
        <Button variant="ghost" onClick={onClose}>{t('work.exchange.channel.cancel')}</Button>
        <span className="text-small text-ink-faint">
          {to ? t('work.exchange.channel.note', { to }) : t('asks.channel.compose.note')}
        </span>
      </div>
      {refusal !== null && <Refused sentence={channelRefusal(t, refusal)} />}
    </div>
  );
}

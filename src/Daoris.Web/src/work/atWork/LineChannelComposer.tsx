import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChannelAnswered } from '../../asks/ChannelSection';
import type { ChannelPost } from '../channel';
import { Refused } from '../InlineConfirm';
import { Button, SelectField } from '../../design/ui';

/** Everyone: the one value that names no repository (a repository's name has no `*`). */
const EVERYONE = '*';

/** The codes the service refuses a post on a line's own channel with, which this page has words for; another is said as it was written. */
const REFUSALS = new Set(['empty', 'too-long', 'bad-to']);

/**
 * The person's words on a line's own channel (COMMS1k, the comms design §4.4), under the channel's last words on *At work*: to everyone on
 * it or one repository its messages name, which a part of the line takes as their words at its look. A refusal is the service's code,
 * said under the box in the reader's language; the words stay for another try.
 */
export function LineChannelComposer({ onPost, recipients }: {
  onPost: (post: ChannelPost, answered: ChannelAnswered) => void;
  recipients: readonly string[];
}) {
  const { t } = useTranslation();
  const [to, setTo] = useState(EVERYONE);
  const [words, setWords] = useState('');
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const turn = useRef(0);
  const alive = useRef(true);

  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);

  const send = () => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    onPost({ text: words.trim(), to: to === EVERYONE ? null : to }, {
      done: () => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setWords('');
      },
      refused: (code) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(code);
      },
    });
  };

  const refusalWords = refusal === null ? null : REFUSALS.has(refusal) ? t(`asks.channel.refused.${refusal}`) : t('asks.channel.refused.other', { code: refusal });

  return (
    <div className="grid gap-2">
      {recipients.length > 0 && (
        <label className="flex items-center gap-2 text-small text-ink-soft">
          {t('asks.channel.compose.to')}
          <SelectField
            ariaLabel={t('asks.channel.compose.to')}
            value={to}
            onChange={setTo}
            options={[{ value: EVERYONE, label: t('asks.channel.everyone') }, ...recipients.map((name) => ({ value: name, label: name }))]}
          />
        </label>
      )}
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
        <span className="text-small text-ink-faint">{t('work.atWork.channel.postNote')}</span>
      </div>
      {refusalWords && <Refused sentence={refusalWords} />}
    </div>
  );
}

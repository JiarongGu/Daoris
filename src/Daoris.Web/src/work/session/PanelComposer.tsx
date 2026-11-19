import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';

/**
 * When the agent reads the person's words: now (between turns), after the current step (while it works), when the session goes on
 * (parked or held), or never again in this session (finished: the words go on with the work).
 */
export type Reads = 'now' | 'next' | 'back' | 'finished';

const READS_LINE: Readonly<Record<Reads, string>> = {
  now: 'work.session.composer.readsNow',
  next: 'work.session.composer.readsNext',
  back: 'work.session.composer.readsWhenBack',
  finished: 'work.session.composer.readsWhenBack',
};

/**
 * **The composer** at the panel's foot: the person's words to the session, a chip naming the model a default resolves to (`chip`,
 * the caller's agent label), the line saying when the agent reads them, and *Send* (Ctrl Enter). Finished, it reads *Go on with this
 * work…*. Sent words leave the box; what becomes of them is the story's to say.
 */
export function PanelComposer({ reads, chip, onSend, disabled = false }: {
  reads: Reads;
  chip: string;
  onSend: (words: string) => void;
  disabled?: boolean;
}) {
  const { t } = useTranslation();
  const [words, setWords] = useState('');
  const send = () => {
    const said = words.trim();
    if (!said || disabled) return;
    onSend(said);
    setWords('');
  };
  return (
    <form
      className="grid gap-1 rounded-card border border-line-strong bg-raised px-2.5 py-2 focus-within:border-accent"
      onSubmit={(event) => {
        event.preventDefault();
        send();
      }}
    >
      <textarea
        aria-label={t('work.session.composer.label')}
        placeholder={t(reads === 'finished' ? 'work.session.composer.goOn' : 'work.session.composer.placeholder')}
        value={words}
        rows={2}
        disabled={disabled}
        onChange={(event) => setWords(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && (event.ctrlKey || event.metaKey) && !event.nativeEvent.isComposing) {
            event.preventDefault();
            send();
          }
        }}
        className="w-full resize-none border-0 bg-transparent p-0 text-body text-ink outline-none placeholder:text-ink-faint"
      />
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <span className="rounded-control border border-line px-1.5 py-0.5 text-meta text-ink-soft">{chip}</span>
        <span className="min-w-0 flex-1 truncate text-meta text-ink-faint">{t(READS_LINE[reads])}</span>
        <Button type="submit" variant="primary" disabled={disabled || words.trim() === ''}>
          {t('work.session.composer.send')}
          <kbd className="text-meta opacity-80">{t('work.session.composer.sendKey')}</kbd>
        </Button>
      </div>
    </form>
  );
}

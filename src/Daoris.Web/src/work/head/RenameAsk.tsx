import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { isComposing } from '../../lib/composing';
import { HELP_NAME_LIMIT } from '../../help/history';
import { type Answered, Refused } from '../InlineConfirm';

/**
 * **A conversation's name**, asked under the page header as a stop's ask is: it starts from the title shown, selected, so typing replaces
 * it; *Use its first words* returns a named conversation to the title it had, since a name cleared to nothing is a press nobody could
 * read; the wait is said aloud and Escape changes nothing while it waits. The name is the conversation's, whichever part is attended.
 */
export function RenameAsk({ title, named, onSave, onClose }: {
  /** The title shown now: the name, or the first words. */
  title: string;
  /** Whether the title is a name the person gave, which can be given up. */
  named: boolean;
  /** The name, or null to give it up; told how it ended. */
  onSave: (name: string | null, answered: Answered) => void;
  /** Put down, or the rename landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  // A title longer than a name may be (a first question) starts cut to the longest name.
  const start = title.slice(0, HELP_NAME_LIMIT);
  const [name, setName] = useState(start);
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const field = useRef<HTMLInputElement>(null);
  const kept = name.trim();
  const unchanged = kept === start.trim();

  useEffect(() => {
    field.current?.focus();
    field.current?.select();
  }, []);

  const save = (value: string | null) => {
    setRefusal(null);
    setPending(true);
    onSave(value, {
      done: () => { setPending(false); onClose(); },
      refused: (sentence) => { setPending(false); setRefusal(sentence); },
    });
  };

  return (
    <form
      aria-label={t('work.rename.title')}
      className="mt-2.5 grid grid-cols-[minmax(0,1fr)] gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (!pending && kept && !unchanged) save(kept);
      }}
      onKeyDown={(event) => {
        if (isComposing(event) || event.key !== 'Escape') return;
        event.preventDefault();
        event.stopPropagation();
        if (!pending) onClose();
      }}
    >
      <input
        ref={field}
        value={name}
        readOnly={pending}
        maxLength={HELP_NAME_LIMIT}
        aria-label={t('work.rename.name')}
        onChange={(event) => setName(event.target.value.replace(/[\r\n]+/g, ' '))}
        spellCheck={false}
        autoComplete="off"
        className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2 py-1 text-small text-ink outline-none focus:border-accent read-only:text-ink-soft"
      />
      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" variant="primary" disabled={pending || unchanged || !kept}>{t('work.rename.save')}</Button>
        {named && <Button disabled={pending} onClick={() => save(null)}>{t('work.rename.original')}</Button>}
        <Button variant="ghost" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
        {/* There from the first, so a reader hears it speak when the wait begins. */}
        <span role="status" className="text-small text-ink-soft">{pending ? t('work.rename.saving') : ''}</span>
      </div>
      {refusal && <Refused sentence={refusal} />}
    </form>
  );
}

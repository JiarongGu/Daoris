import { useTranslation } from 'react-i18next';

/** The attended part's boundary where it began by no move: the *Parts* strip's began words, as a line of Daoris's above its story. */
export function PartBegan({ words }: { words: string }) {
  const { t } = useTranslation();
  return (
    <p data-began="" className="m-0 flex min-w-0 items-baseline gap-1 text-small text-ink-soft">
      <span aria-hidden="true">◇</span>
      <span className="font-medium">{t('work.session.story.daoris')}</span>
      <span className="min-w-0 wrap-anywhere">{words}</span>
    </p>
  );
}

/** The line's state where another part or a co-worker decides it (*Working: its helper*), in the head, with a door to that session. */
export function HeadLineOf({ said, onOpen }: { said: string; onOpen?: (() => void) | null }) {
  const { t } = useTranslation();
  return (
    <span data-line-state="" className="flex shrink-0 items-baseline gap-1.5 text-small text-ink-soft">
      {said}
      {onOpen && (
        <button type="button" onClick={onOpen} className="cursor-pointer border-0 bg-transparent p-0 text-small text-accent hover:underline">
          {t('work.parts.line.open')}
        </button>
      )}
    </span>
  );
}

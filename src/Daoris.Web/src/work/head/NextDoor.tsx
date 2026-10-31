import { useTranslation } from 'react-i18next';

/** *next: Answer* on a head's facts line (REVIEWFLOW1b): the press that moves the work on, a door to its step. */
export function NextDoor({ press, onPress }: { press: string; onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <button
      type="button"
      onClick={onPress}
      className="cursor-pointer rounded-control border-0 bg-transparent p-0 text-small text-ink-open underline-offset-2 hover:underline"
    >
      {t('work.facts.nextIs', { press })}
    </button>
  );
}

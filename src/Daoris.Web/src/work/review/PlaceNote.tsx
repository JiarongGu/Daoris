import { useTranslation } from 'react-i18next';

/**
 * Above a folder's change set: what the files are measured against, and that what only git offers (history, branches, a landing)
 * is not this department's, said where Review would otherwise show it.
 */
export function PlaceNote() {
  const { t } = useTranslation();
  return (
    <p className="m-0 shrink-0 border-b border-line bg-raised px-3 py-2 text-small text-ink-soft">
      {t('work.review.place.note')}
    </p>
  );
}

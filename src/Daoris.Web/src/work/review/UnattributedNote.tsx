import { useTranslation } from 'react-i18next';

/**
 * Above a turn in place's files: the paths it changed that no tool of its run named, changed while it ran by the person or another
 * program, so the person reads them as possibly not the session's. Nothing where every path is its own.
 */
export function UnattributedNote({ paths }: { paths?: string[] | null }) {
  const { t } = useTranslation();
  if (!paths?.length) return null;
  return (
    <p className="m-0 shrink-0 border-b border-line bg-raised px-3 py-2 text-small text-ink-soft">
      {t('work.review.unattributed', { paths: paths.join(', ') })}
    </p>
  );
}

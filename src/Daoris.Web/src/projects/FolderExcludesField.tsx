import { useTranslation } from 'react-i18next';

/** The globs a person typed, one to a line or separated by commas: trimmed, empty ones dropped, each once. */
export const parseExcludes = (text: string): string[] =>
  [...new Set(text.split(/[\n,]/).map((glob) => glob.trim()).filter(Boolean))];

/**
 * A folder's own excludes, set where it is added: the files its manifest passes by beside the defaults (editors' lock files,
 * thumbnails, temporary files). A glob with a slash is read from the folder's root, one without names a file wherever it stands.
 */
export function FolderExcludesField({ value, onChange }: { value: string; onChange: (text: string) => void }) {
  const { t } = useTranslation();
  return (
    <>
      <label className="mt-4 block text-small text-ink-faint" htmlFor="add-excludes">
        {t('projects.manage.excludes')}
      </label>
      <textarea
        id="add-excludes"
        value={value}
        rows={3}
        onChange={(event) => onChange(event.target.value)}
        placeholder={t('projects.manage.excludesPlaceholder')}
        className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body"
      />
      <p className="mt-1.5 text-small text-ink-faint">{t('projects.manage.excludesNote')}</p>
    </>
  );
}

import { useTranslation } from 'react-i18next';

/**
 * Where a repository's page shows what only git offers (its branches, its line's history, its integration), a folder's says it has
 * none: absent, never an error, and nothing a folder needs to be driven.
 */
export function FolderNoGit() {
  const { t } = useTranslation();
  return <p className="m-0 px-4 py-3 text-body text-ink-soft">{t('projects.folder.noGit')}</p>;
}

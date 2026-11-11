import { useTranslation } from 'react-i18next';
import type { Registration } from '../data/api';
import { PageSection } from '../work/frame/ViewMain';
import { FolderExcludesRow } from './FolderExcludesRow';

/** The folder department's excludes on its page, the same list `daoris repository excludes` edits, saved by the view that holds the
 * driver. Keyed by the list held, so a change the registry answers draws the new text. */
export function FolderExcludesSection({ registration, busy, onSave }: {
  registration: Registration;
  busy: boolean;
  onSave: (change: { add: string[]; remove: string[] }) => void;
}) {
  const { t } = useTranslation();
  const held = registration.excludes ?? [];
  return (
    <PageSection title={t('projects.manage.excludes')}>
      <FolderExcludesRow key={held.join('\n')} held={held} busy={busy} onSave={onSave} />
    </PageSection>
  );
}

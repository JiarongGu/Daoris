import { useTranslation } from 'react-i18next';
import { useSetFolderExcludes } from '../bridge/shell';
import type { Registration } from '../data/api';
import { failure, type Notify } from '../design/ui';
import { PageSection } from '../work/frame/ViewMain';
import { FolderExcludesRow } from './FolderExcludesRow';

/**
 * The folder department's excludes on its page, the same list `daoris repository excludes` edits: pressed through the driver, which
 * changes the registration and reads the folder's manifest again with the new list. Keyed by the list held, so a change the registry
 * answers draws the new text.
 */
export function FolderExcludesSection({ registration, notify }: { registration: Registration; notify: Notify }) {
  const { t } = useTranslation();
  const set = useSetFolderExcludes();
  const held = registration.excludes ?? [];
  return (
    <PageSection title={t('projects.manage.excludes')}>
      <FolderExcludesRow
        key={held.join('\n')}
        held={held}
        busy={set.isPending}
        onSave={(change) => set.mutate({ repository: registration.repository, add: change.add, remove: change.remove }, {
          onSuccess: () => notify(t('projects.folder.excludesSaved', { name: registration.repository })),
          onError: failure(notify),
        })}
      />
    </PageSection>
  );
}

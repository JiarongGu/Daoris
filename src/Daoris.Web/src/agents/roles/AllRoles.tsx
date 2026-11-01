import { useTranslation } from 'react-i18next';
import { useRegistry } from '../../data/queries';
import { workspaceOf } from '../../data/workspaces';
import { type Notify, Segmented } from '../../design/ui';
import { MACHINE, repositoryId, workspaceId } from '../modes';
import type { PlaceAt } from '../setup/placeShell';
import { type RoleOpeners, type RolesPage, useRolesMode } from './RolesView';

/**
 * **This machine's *All roles*** (SETUPUX1m, the setup-UX design §2.4): every role once, by where it is placed, as a list or as the
 * diagram, with what Daoris suggests and the staffing changes found above the rows. A role placed elsewhere is read here and opened at its
 * place; one placed on this machine is edited in place by the one role editor.
 *
 * @remarks
 * It is the Roles mode's Working roles and diagram under the page's section, with the doors re-pointed: a press that led to another of the
 * mode's sections stays here, and a press that led to a place's setting opens that place's Roles with *Back* to this section. It is mounted
 * only while the section is open, so a folded page reads nothing.
 */
export function AllRoles({ notify, page, doors, onGo }: {
  notify: Notify;
  page: RolesPage;
  /** Where the roles' other doors lead: an agent, a plugin, Repositories. */
  doors?: RoleOpeners;
  /** A door to a place at a section, which names this section as the way back. */
  onGo: (place: string, at: PlaceAt) => void;
}) {
  const { t } = useTranslation();
  const registry = useRegistry('machine');
  const back = { place: MACHINE, name: t('agents.setup.machine'), section: 'allRoles' as const };
  const openAtPlace: RoleOpeners = {
    ...doors,
    // The mode's sections are this one, so a node of another kind is chosen where it stands.
    section: () => {},
    repository: (repository) => {
      const row = (registry.data ?? []).find((each) => each.repository === repository);
      onGo(repositoryId(workspaceOf(row ?? {}), repository), { section: 'roles', back });
    },
    workspace: (workspace) => onGo(workspaceId(workspace), { section: 'roles', back }),
  };
  const mode = useRolesMode({ notify, section: 'working', doors: openAtPlace, page, placed: true });
  const body = page.view === 'diagram' ? mode.diagram : mode.card;
  return (
    <div className="grid min-w-0 gap-3">
      <div>
        <Segmented
          label={t('agents.setup.allRoles.view')}
          value={page.view}
          options={[
            { value: 'list', label: t('agents.setup.allRoles.list') },
            { value: 'diagram', label: t('agents.setup.allRoles.diagram') },
          ]}
          onChange={page.onView}
        />
      </div>
      {body ?? <p className="m-0 text-small text-ink-soft">{t('agents.roles.reading')}</p>}
    </div>
  );
}

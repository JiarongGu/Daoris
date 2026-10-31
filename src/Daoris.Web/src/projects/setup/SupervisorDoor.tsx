import { useTranslation } from 'react-i18next';
import { Button, SettingRow } from '../../design/ui';
import { type SupervisorLine, supervisorStatus } from './SupervisorRow';

/**
 * The supervisor's line in a repository's or a workspace's Setup (COMPANYVIEW1e): where it stands in one sentence, and a door to
 * Agents → Roles → Appointed roles, where it is appointed. It appoints nothing itself.
 *
 * A molecule: drawn from props; the application holds the door.
 */
export function SupervisorDoor({ repository, workspace, supervisor, onOpen }: {
  repository?: string;
  workspace?: string;
  supervisor: SupervisorLine;
  onOpen?: () => void;
}) {
  const { t } = useTranslation();
  return (
    <SettingRow
      label={t('projects.supervisor.label')}
      hint={workspace ? t('projects.supervisor.hintWorkspace', { workspace }) : t('projects.supervisor.hint', { repository })}
      control={onOpen && <Button variant="ghost" onClick={onOpen}>{t('agents.roles.appointed.open')}</Button>}
    >
      <p data-supervisor-status="" className="m-0 text-small text-ink-soft">{supervisorStatus(t, supervisor)}</p>
    </SettingRow>
  );
}

import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { SupervisorRow, type SupervisorSetup } from '../../projects/setup/SupervisorRow';
import { WiringSteward, type StewardSetup } from '../../settings/wiring/WiringSteward';

/** A scope's group: its role and scope named, so a screen reader hears which appointment a press belongs to. */
function AppointedRow({ name, children }: { name: string; children: ReactNode }) {
  return <div role="group" aria-label={name} data-appointed-row="" className="grid gap-1.5">{children}</div>;
}

/** A heading and the rows under it. */
function Group({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <section aria-labelledby={id} className="grid gap-2">
      <h3 id={id} className="m-0 text-body font-medium text-ink">{title}</h3>
      {children}
    </section>
  );
}

/**
 * The Appointed roles section's rows (COMPANYVIEW1e, the company-view design §3.2): the steward for This Daoris, the supervisor of each
 * workspace and of each repository. Each is the molecule its home drew, so the asks are the ones they were; a steward this build does not
 * offer is drawn saying so, with nothing to press.
 *
 * A molecule: drawn from props; the section's container holds the presses.
 */
export function AppointedList({ steward, machine = true, workspaces, repositories }: {
  /** The steward's molecule's props, or null where this build does not offer the steward. */
  steward: StewardSetup | null;
  /** Whether *This Daoris*, the steward's group, is drawn; a workspace's or a repository's page has none (AGENTSPLACE1g1). */
  machine?: boolean;
  workspaces: { workspace: string; supervisor: SupervisorSetup }[];
  repositories: { repository: string; supervisor: SupervisorSetup }[];
}) {
  const { t } = useTranslation();
  const supervisor = t('projects.supervisor.label');
  return (
    <div className="grid min-w-0 gap-5" data-appointed-roles="">
      {machine && (
        <Group id="appointed-machine" title={t('agents.roles.appointed.machine')}>
          <AppointedRow name={`${t('settings.daoris.steward.label')} · ${t('settings.domain.wiring')}`}>
            {steward
              ? <WiringSteward steward={steward} />
              : (
                <div data-steward-row="" className="grid gap-1 rounded-control border border-line px-3 py-2">
                  <p className="m-0 text-body font-medium text-ink">{t('settings.daoris.steward.label')}</p>
                  <p className="m-0 text-small text-ink-faint">{t('settings.daoris.steward.hint')}</p>
                  <p data-steward-status="" className="m-0 text-small text-ink-soft">{t('agents.roles.appointed.notOffered')}</p>
                </div>
              )}
          </AppointedRow>
        </Group>
      )}
      {workspaces.length > 0 && (
        <Group id="appointed-workspaces" title={t('agents.roles.appointed.workspaces')}>
          {workspaces.map((each) => (
            <AppointedRow key={each.workspace} name={`${supervisor} · ${t('agents.roles.appointed.workspace', { workspace: each.workspace })}`}>
              <SupervisorRow workspace={each.workspace} supervisor={each.supervisor} />
            </AppointedRow>
          ))}
        </Group>
      )}
      {repositories.length > 0 && (
        <Group id="appointed-repositories" title={t('agents.roles.appointed.repositories')}>
          {repositories.map((each) => (
            <AppointedRow key={each.repository} name={`${supervisor} · ${each.repository}`}>
              <SupervisorRow repository={each.repository} supervisor={each.supervisor} />
            </AppointedRow>
          ))}
        </Group>
      )}
    </div>
  );
}

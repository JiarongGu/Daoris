import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { StartWiringList } from '../map/StartWiring';
import type { StartWiring } from '../map/wiring';
import { Button, Icon, Prose } from '../ui';
import { PageHead, PageSection, PageTabs, ViewMain } from '../work/ViewMain';
import type { WorkspaceTab } from './tabs';
import type { AccountsHere } from './workspace';

/**
 * **A workspace's page** (UX6g, D150 §4.3): in Repositories' main area, opened from its row at the head of its group. Its
 * header names it, how many repositories it holds and where it syncs, with *Sync now* where it is wired or *Wire to a
 * remote…* where it is not. Beneath, four tabs, the chosen one remembered for the view: **Details** (its repositories,
 * what a start runs on, the accounts its work may run on), **Branches** (the clean-up and bringing up to date across its
 * repositories), **Workflow** (how work moves in its repositories, drawn from its rules, WORKFLOW1b) and **Setup** (its
 * defaults, its remote and its rules).
 *
 * @remarks
 * **A molecule**: the chosen tab's content arrives as `children`, drawn by whoever holds the data, and every press goes
 * out. **A browser gets the page with what it may know** (D47 §4): its repositories and whether it syncs, with no tab row,
 * since branches, setup and accounts are this machine's. Where it syncs is a host only where this machine's wiring says
 * so: a browser is never told where a machine syncs (D48 §5).
 */
export function WorkspacePage({ workspace, repositories, remote, tab = 'details', onTab, syncing = false, onSync, onWire, children }: {
  workspace: string;
  /** How many repositories it holds in the list's registry. */
  repositories: number;
  /** Where it syncs: a deployment's host, a deployment with no host told (a browser), none (null), or unknown (undefined). */
  remote?: { host?: string } | null;
  /** The tab shown, its holder's to remember; Details where it is not handed. */
  tab?: WorkspaceTab;
  /** Choose a tab; absent, the page is Details alone, with no tab row (a browser). */
  onTab?: (tab: WorkspaceTab) => void;
  syncing?: boolean;
  /** Sync it with its remote now, the status bar's pass (SYNC6b): a shell's, where it is wired. */
  onSync?: () => void;
  /** Open its Setup at its remote, the wiring form open: a shell's, where it is not wired. */
  onWire?: () => void;
  /** The chosen tab's content. */
  children: ReactNode;
}) {
  const { t } = useTranslation();
  const where = remote === undefined
    ? null
    : remote
      ? remote.host ? t('projects.workspace.page.syncs', { host: remote.host }) : t('projects.workspace.page.syncsRemote')
      : t('projects.workspace.page.local');
  const line = [t('projects.workspace.repositories', { count: repositories }), ...(where ? [where] : [])].join(' · ');
  const acts = onSync
    ? <Button disabled={syncing} onClick={onSync}><Icon name="refresh" size={14} />{t('projects.workspace.page.sync')}</Button>
    : onWire
      ? <Button variant="ghost" onClick={onWire}><Icon name="cloud" size={14} />{t('projects.workspace.page.wire')}</Button>
      : undefined;
  const menu = {
    label: workspace,
    acts: [
      ...(onSync ? [{ id: 'sync', label: t('projects.workspace.page.sync'), icon: 'refresh' as const, onSelect: onSync }] : []),
      ...(onWire ? [{ id: 'wire', label: t('projects.workspace.page.wire'), icon: 'cloud' as const, onSelect: onWire }] : []),
      { id: 'copy', label: t('contextMenu.act.copyWorkspace'), icon: 'copy' as const, copy: workspace },
    ],
  };

  return (
    <ViewMain header={<PageHead title={workspace} line={line} acts={acts} />} menu={menu}>
      {onTab
        ? (
          <PageTabs<WorkspaceTab>
            label={t('projects.workspace.tab.list', { workspace })}
            tabs={[
              { id: 'details', label: t('projects.tab.details') },
              { id: 'branches', label: t('projects.tab.branches') },
              { id: 'workflow', label: t('projects.tab.workflow') },
              { id: 'setup', label: t('projects.tab.setup') },
            ]}
            chosen={tab}
            onChoose={onTab}
          >
            {children}
          </PageTabs>
        )
        : children}
    </ViewMain>
  );
}

/**
 * **A workspace's Details** (§4.3): its repositories, each a door to its page; what a start in it runs on, a row per job
 * with the setting that chose each part and a held start's sentence whole (MAP1b, moved from Settings → Workspace); and
 * the accounts each agent's work may run on here, read-only, each with a door to its agent (D130 §3.2, set on the agent's
 * page since UX6e). A browser is handed the repositories alone.
 */
export function WorkspaceDetails({ repositories, onOpen, starts, accounts, onOpenAgent }: {
  repositories: string[];
  onOpen: (repository: string) => void;
  /** What a start in it would run on, as the driver answered, and what a person calls each account: a shell's. */
  starts?: { list: StartWiring[]; nameOf: (owner: string, profile?: string | null) => string } | null;
  /** The accounts each installed agent's work may run on here: a shell's. */
  accounts?: AccountsHere[] | null;
  onOpenAgent?: (agent: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <>
      <PageSection title={t('projects.workspace.section.repositories')}>
        <ul aria-label={t('projects.workspace.section.repositories')} className="m-0 flex list-none flex-wrap gap-1.5 p-0">
          {repositories.map((repository) => (
            <li key={repository}>
              <Button
                variant="ghost"
                className="border border-line px-2.5 py-0.5 text-small"
                aria-label={t('projects.workspace.open', { repository })}
                onClick={() => onOpen(repository)}
              >
                {repository}
              </Button>
            </li>
          ))}
        </ul>
      </PageSection>
      {starts && starts.list.length > 0 && (
        <PageSection title={t('wiring.title')}>
          <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('projects.workspace.startsBody')}</Prose>
          <StartWiringList starts={starts.list} nameOf={starts.nameOf} />
        </PageSection>
      )}
      {accounts && accounts.length > 0 && (
        <PageSection title={t('projects.workspace.section.accounts')}>
          <ul className="m-0 grid list-none gap-1.5 p-0">
            {accounts.map((each) => (
              <li key={each.agent} aria-label={each.product} className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <span className="text-body font-medium text-ink">{each.product}</span>
                <span className="min-w-0 flex-1 text-small text-ink-soft">
                  {t(each.own ? 'projects.workspace.accounts.own' : 'projects.workspace.accounts.machine', {
                    accounts: each.accounts.join(t('projects.setup.summary.listJoin')),
                  })}
                </span>
                {onOpenAgent && (
                  <Button variant="ghost" className="text-small" onClick={() => onOpenAgent(each.agent)}>
                    {t('projects.workspace.accounts.door', { agent: each.product })}
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </PageSection>
      )}
    </>
  );
}

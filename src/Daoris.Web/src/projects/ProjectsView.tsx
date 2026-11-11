import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { isFolder, type Registration } from '../data/api';
import { FolderExcludesSection } from './FolderExcludesSection';
import { FolderNoGit } from './FolderNoGit';
import { type PluginEntry, usePlugins } from '../bridge/plugins';
import { sentence } from '../locales/format';
import { AddProjectDrawer, ImportFolderDrawer, ManageProjectDrawer } from './ProjectManage';
import { projectsItem, workspaceItem } from '../app/opener';
import { ProjectList, type RepositoryRowFacts, type WorkspaceGroup } from './ProjectList';
import { ProjectPage, ProjectsMainNotice } from './ProjectPage';
import { RepositoryBranch, RepositoryBranches, RepositoryChanges, RepositoryHistory } from './git/RepositoryGitView';
import { RepositoryIntegration } from './integration/RepositoryIntegration';
import { RepositoryFiles } from './files/RepositoryFiles';
import type { ProjectTab, WorkspaceSection, WorkspaceTab } from './tabs';
import { WorkspaceView } from './workspace/WorkspaceView';
import { useQuests, useRegistry, useRepositories, useSessions } from '../data/queries';
import { sessionTitle } from '../work/identity';
import { useScope } from '../data/scope';
import {
  useDriver, useLines, useHeavySlot, useSetFolderExcludes, useSweepPlan,
} from '../bridge/shell';
import { useOpenDepartmentFolder } from '../bridge/repositories/registry';
import { failure, type Notify, useErrorNotify } from '../design/ui';
import { ListMore } from '../work/frame/ListPane';
import type { ViewLayout } from '../work/frame/ViewFrame';
import { workspaceOf, workspacesOf } from '../data/workspaces';
import { buttonNamed, returnFocus } from '../lib/returnFocus';

/** The systems, `<plugin>/<system>`, whose adapter declares no precondition: the departments the list says are unguarded. */
export const unguardedSystems = (plugins: readonly PluginEntry[]): ReadonlySet<string> =>
  new Set(plugins.flatMap((plugin) => (plugin.systems ?? []).filter((each) => each.preconditions === 'none').map((each) => `${plugin.id}/${each.id}`)));

/**
 * **The Repositories view**: what it hands the frame, its list pane and its main
 * area. The list holds a group per workspace, its row a door to the workspace's page, then its adopted repositories and
 * *Registered, not adopted*; its `＋` adds a repository and its ⋯ imports a folder. The main area holds
 * the chosen workspace's page (`WorkspaceView`, §4.3), or the chosen repository's, with *Manage* and the door to its code
 * map in its header. A record is the main area and a form a drawer (§3d): adding, importing and managing stay drawers.
 *
 * @remarks
 * **A hook, because a view hands the frame a value** (`ViewLayout`), as Quests' and Plugins' are: the list and the main
 * area are drawn in two places the frame decides. The application holds it on every view, and says its errors only
 * while it is in front. Every query it holds the frame already holds, so holding it everywhere asks nothing more.
 *
 * **What it remembers is its list's** (`listPanes.ts`): the chosen repository, by name, which a door into the view
 * names through the opener; and its page's tab, Details, Branches, History, Workflow or Setup, one for the view,
 * which its holder keeps so a door can open a repository at Setup.
 *
 * **Git is inside Repositories**: a repository's Branches and History are parts of their own
 * (`RepositoryGitView`), handed to its page and drawn only while their tab shows, so Git's reads are asked only then. A
 * branch's name in a repository's Branches, or a workspace's, opens the branch's page in the main area in its repository's
 * place; it is page state of the view's, not remembered, and its way back is the repository's Branches.
 *
 * **A repository's Setup is every setting it holds on this machine**, each the screen's half of `daoris place
 * <verb> <repository>` or `daoris agent rules … --repository`, from the answers the driver gives every door: the
 * driver's state, the lines, reading across, the rules, and the plugins that land work. What only Setup reads (reading
 * across, the plugins) is asked only while Setup shows, so holding the view on every view still asks nothing more.
 *
 * **Membership is a repository's own act**: Daoris never writes into a sibling, so nothing joins by being seen,
 * and the join steps are proposed as text, never a button. The management surfaces exist where a shell
 * does: managing repositories means touching machine paths, and a browser has none.
 */
export function useProjectsView({
  active, chosen, onChoose, tab = 'details', onTab, workspaceTab = 'details', onWorkspaceTab, workspaceSection = null, notify,
  onOpenCode, onOpenAgent, onOpenSetup, onOpenWorkspaceSetup, onSyncNow, syncing = false, addRequested = false, onAddOpened, importRequested = false, onImportOpened,
  drawerWorkspace = null, onOpenQuest, onOpenAsk, onAttend, branchAsked = null, onBranchOpened,
}: {
  /** The view is in front: only then are its errors said. */
  active: boolean;
  /**
   * The list's chosen item, which the application remembers (`daoris.list.projects.chosen`): a repository's name, or a
   * workspace's item (`workspaceItem`).
   */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  /** The page's tab, which the application remembers for the view (`daoris.list.projects.tab`). */
  tab?: ProjectTab;
  /** Choose a tab; absent, the page is Details alone. */
  onTab?: (tab: ProjectTab) => void;
  /** A workspace's page's tab, remembered apart (`daoris.list.projects.workspaceTab`). */
  workspaceTab?: WorkspaceTab;
  onWorkspaceTab?: (tab: WorkspaceTab) => void;
  /** The section of a workspace's Setup a door asked to see open. */
  workspaceSection?: WorkspaceSection | null;
  notify: Notify;
  /** Open an agent's page, where a workspace's accounts are set: a workspace's Details names each agent's. */
  onOpenAgent?: (agent: string) => void;
  /** Open a plugin's page, from the plugin a landing rule names in a Setup. */
  onOpenPlugin?: (plugin: string) => void;
  /** Open Settings → Git, where a remote nothing serves is given an identity. */
  onOpenGit?: (host: string) => void;
  /** Open a repository's Setup, which is the Agents place's page once its Setup mode is lit: the door its page offers in place of the tab. */
  onOpenSetup?: (repository: string) => void;
  /** Open a section of a workspace's Setup, which is the workspace place's once the Setup mode is lit: the door its Workflow tab's steps offer. */
  onOpenWorkspaceSetup?: (workspace: string, section: WorkspaceSection) => void;
  /** Sync a workspace with its remote, the status bar's pass, which the application says. */
  onSyncNow?: (workspace: string) => void;
  syncing?: boolean;
  /** Its code map, one level into the Map: the door a repository's page offers. */
  onOpenCode?: (repository: string) => void;
  /**
   * The Workspace menu's *Add repository…*, an EVENT like Quests' opening draft: consumed once,
   * and cleared by its holder through `onAddOpened`, or the drawer would reopen on every render.
   */
  addRequested?: boolean;
  onAddOpened?: () => void;
  /** And its *Import a folder…*: the same kind of event, for the import drawer. */
  importRequested?: boolean;
  onImportOpened?: () => void;
  /**
   * The workspace the drawer an event asks for opens filled with: Ask Daoris's go's, cleared with the event.
   * The folder is never asked for; it stays the person's pick.
   */
  drawerWorkspace?: string | null;
  /**
   * A quest's page, an ask's, or a session in Sessions: the doors a workspace's clear offers beside a unit it keeps, where
   * the page that frees it is. Absent where there is no such page to open.
   */
  onOpenQuest?: (id: string) => void;
  onOpenAsk?: (id: string) => void;
  onAttend?: (session: string) => void;
  /**
   * A door into how a landed branch landed (the work's milestone, a run's landing step), an EVENT like the drawers':
   * the branch's page opens on its repository's Branches with that section open, and its holder lets the word go through
   * `onBranchOpened`. The door's opening chose the repository and the tab already.
   */
  branchAsked?: { repository: string; branch: string } | null;
  onBranchOpened?: () => void;
}): ViewLayout {
  const { t } = useTranslation();
  // A chosen item names a workspace or a repository.
  const item = chosen ? projectsItem(chosen) : null;
  const chosenWorkspace = item && 'workspace' in item ? item.workspace : null;
  const chosenRepository = item && 'repository' in item ? item.repository : null;
  const registry = useRegistry();
  const { workspace } = useScope();
  const repositories = useRepositories();
  const driver = useDriver();
  // Each repository's line as the driver resolves it — shell-only, and absent in a browser.
  const lines = useLines();
  const lineOf = (repository: string) => {
    const line = (Array.isArray(lines.data?.lines) ? lines.data.lines : []).find((one) => one.repository === repository);
    return line?.branch ? line : undefined;
  };
  // Who holds its heavy-run slot and who waits, for its Details' row, asked while its Details shows; the sessions and
  // quests that name them are the rail's own reads, asked only while someone holds or waits.
  const detailsShown = active && driver.data !== undefined && tab === 'details' && chosenRepository !== null;
  const heavySlot = useHeavySlot(chosenRepository ?? '', { enabled: detailsShown });
  const heavyBusy = Boolean(heavySlot.data && (heavySlot.data.holder || heavySlot.data.waiters.length > 0));
  const heavySessions = useSessions(null, true, heavyBusy);
  const heavyQuests = useQuests(null, true, heavyBusy);
  const heavyTitleOf = (id: string) => {
    const session = (heavySessions.data ?? []).find((row) => row.id === id);
    return session ? sessionTitle(session, session.quest ? (heavyQuests.data ?? []).find((row) => row.id === session.quest) : null) : id;
  };
  // Session branches holding work no branch of the person's holds — named on the repository,
  // so work is not lost in a pile nobody reads. A workspace's Branches lists them one by one.
  const sweep = useSweepPlan();
  const openFolder = useOpenDepartmentFolder();
  const setExcludes = useSetFolderExcludes();
  const unlandedIn = (repository: string) => (Array.isArray(sweep.data?.branches) ? sweep.data.branches : [])
    .filter((branch) => branch.repository === repository && branch.kind === 'unlanded').length;
  // And every branch of Daoris's it holds, sessions' and landings' (§4.1): its row says how many. Absent where the driver
  // has not answered, so a browser's row says nothing of it.
  const branchesIn = (repository: string) => (sweep.data
    ? [...(Array.isArray(sweep.data.branches) ? sweep.data.branches : []), ...(Array.isArray(sweep.data.landed) ? sweep.data.landed : [])]
      .filter((branch) => branch.repository === repository).length
    : undefined);
  // The driver bridge included: a STATE that fails silently reads as a machine with no driver. Said once, while the
  // view is in front.
  useErrorNotify(active ? registry.error ?? repositories.error ?? driver.error : null, notify);

  const attached = driver.data !== undefined;
  // An open drawer, and the workspace a go filled it with; the list's own doors fill none.
  const [adding, setAdding] = useState<{ workspace?: string } | null>(null);
  useEffect(() => {
    if (!addRequested || !attached) return;
    setAdding(drawerWorkspace ? { workspace: drawerWorkspace } : {});
    onAddOpened?.();
  }, [addRequested, attached, onAddOpened, drawerWorkspace]);
  const [importing, setImporting] = useState<{ workspace?: string } | null>(null);
  useEffect(() => {
    if (!importRequested || !attached) return;
    setImporting(drawerWorkspace ? { workspace: drawerWorkspace } : {});
    onImportOpened?.();
  }, [importRequested, attached, onImportOpened, drawerWorkspace]);
  const [managing, setManaging] = useState<Registration | null>(null);
  // The branch open on a repository's page: page state, as the Map holds its code map, never remembered across a
  // restart. It shows while its repository is chosen at Branches, whose page it is, and its way back clears it.
  // A door into how it landed opens it with that section open; the list's presses open the page alone.
  const [branchOpen, setBranchOpen] = useState<{ repository: string; branch: string; landing?: boolean } | null>(null);
  useEffect(() => {
    if (!branchAsked) return;
    setBranchOpen({ ...branchAsked, landing: true });
    onBranchOpened?.();
  }, [branchAsked, onBranchOpened]);
  // The list's own press opens the item it names, never a branch left open on it.
  const choose = (item: string | null) => {
    setBranchOpen(null);
    onChoose(item);
  };

  const rows = registry.data ?? [];
  // Only a system department asks the plugins, to say whether its adapter guards its writes.
  const plugins = usePlugins({ enabled: rows.some((row) => row.kind === 'system') });
  const indexed = (name: string) => (repositories.data ?? []).find((r) => r.name === name);
  const named = (names: string[] | undefined, repository: string) =>
    (names ?? []).some((name) => name.toLowerCase() === repository.toLowerCase());
  /** Its standing on this machine: the driver's choices and whether a checkout is here, where a shell answers. */
  const standing = (registration: Registration) => driver.data && {
    drivable: named(driver.data.drivable, registration.repository),
    held: named(driver.data.holds, registration.repository),
    // Only a local host names a checkout, and only to its own page.
    here: Boolean(registration.root),
  };
  const unguarded = unguardedSystems(plugins.data?.plugins ?? []);
  const facts = (registration: Registration): RepositoryRowFacts => ({
    registration,
    ...standing(registration),
    entries: indexed(registration.repository)?.total ?? null,
    branches: branchesIn(registration.repository),
    unguarded: registration.kind === 'system' && unguarded.has(registration.system ?? ''),
  });
  /** The list's groups: a workspace each, by name, its adopted repositories then those not adopted. */
  const groups: WorkspaceGroup[] = workspacesOf(rows).map((name) => {
    const held = rows.filter((row) => workspaceOf(row) === name);
    return { workspace: name, adopted: held.filter((row) => row.adopted).map(facts), outside: held.filter((row) => !row.adopted).map(facts) };
  });

  const shown = chosenRepository ? rows.find((row) => row.repository === chosenRepository) : undefined;
  const workspaceHeld = chosenWorkspace !== null && groups.some((group) => group.workspace === chosenWorkspace);
  const branchShown = attached && shown && tab === 'branches' && branchOpen?.repository === shown.repository ? branchOpen.branch : null;
  const add = { label: t('projects.manage.add'), onAct: () => setAdding({}) };
  const goneOrLoading = (of: 'repository' | 'workspace') => (registry.data === undefined && registry.error
    ? <ProjectsMainNotice state="unanswered" sentence={sentence(registry.error)} />
    : <ProjectsMainNotice state={registry.data === undefined ? 'loading' : 'gone'} of={of} />);
  const main = chosenWorkspace !== null
    ? workspaceHeld
      ? (
        <WorkspaceView
          key={chosenWorkspace}
          workspace={chosenWorkspace}
          repositories={rows.filter((row) => workspaceOf(row) === chosenWorkspace).map((row) => row.repository).sort()}
          attached={attached}
          tab={workspaceTab}
          onTab={(next) => onWorkspaceTab?.(next)}
          asked={workspaceSection}
          notify={notify}
          onOpenRepository={onChoose}
          // A repository that sets rules of its own opens at its own workflow.
          // That is the repository's place: the application opens it, as it does for a Setup door.
          onOpenRepositoryWorkflow={(repository) => onOpenSetup?.(repository)}
          // A branch in its Branches opens on its repository's page, so its way back is that repository's Branches.
          onOpenBranch={(repository, branch) => { setBranchOpen({ repository, branch }); onChoose(repository); onTab?.('branches'); }}
          onOpenRepositoryHistory={(repository) => { onChoose(repository); onTab?.('history'); }}
          onOpenAgent={onOpenAgent}
          onOpenSetupSection={onOpenWorkspaceSetup}
          onSyncNow={onSyncNow}
          syncing={syncing}
          onOpenQuest={onOpenQuest}
          onOpenAsk={onOpenAsk}
          onAttend={onAttend}
        />
      )
      : goneOrLoading('workspace')
    : chosen
    ? shown && branchShown
      ? (
        <RepositoryBranch
          key={`${shown.repository}\n${branchShown}`}
          repository={shown.repository}
          workspace={workspaceOf(shown)}
          here={Boolean(shown.root)}
          branch={branchShown}
          landingOpen={branchOpen?.landing === true}
          onBack={() => {
            // The focus goes back to the branch's name button; a branch no longer listed leaves it on the page's heading.
            const label = t('projects.git.branches.open', { branch: branchShown });
            returnFocus({
              find: () => buttonNamed(label),
              // The repository's page is drawn again (its tabs are back) and nothing in it is still reading.
              settled: () => document.querySelector('main [role="tab"]') !== null && document.querySelector('main [aria-busy]') === null,
              fallback: () => document.querySelector<HTMLElement>('main h1'),
            });
            setBranchOpen(null);
          }}
        />
      )
    : shown
      ? (
        <ProjectPage
          key={shown.repository}
          registration={shown}
          counts={indexed(shown.repository)}
          line={lineOf(shown.repository)}
          unlanded={unlandedIn(shown.repository)}
          heavy={heavySlot.data && heavySlot.data.repository === shown.repository
            ? { slot: heavySlot.data, titleOf: heavyTitleOf, onOpen: onAttend }
            : null}
          here={attached ? Boolean(shown.root) : undefined}
          drivable={driver.data ? named(driver.data.drivable, shown.repository) : undefined}
          held={driver.data ? named(driver.data.holds, shown.repository) : undefined}
          onOpenSetup={onOpenSetup ? () => onOpenSetup(shown.repository) : undefined}
          excludes={attached && isFolder(shown) ? (
            <FolderExcludesSection
              registration={shown}
              busy={setExcludes.isPending}
              onSave={(change) => setExcludes.mutate({ repository: shown.repository, add: change.add, remove: change.remove }, {
                onSuccess: () => notify(t('projects.folder.excludesSaved', { name: shown.repository })),
                onError: failure(notify),
              })}
            />
          ) : undefined}
          // Its branches and its line's history: a shell's, each read only while its tab shows.
          branches={attached && isFolder(shown)
            ? <FolderNoGit />
            : attached
            ? (
              <RepositoryBranches
                repository={shown.repository}
                workspace={workspaceOf(shown)}
                here={Boolean(shown.root)}
                // A branch's name opens its page over Branches; the line's opens History, the line's own page.
                onOpenBranch={(branch) => setBranchOpen({ repository: shown.repository, branch })}
                onOpenHistory={onTab && (() => onTab('history'))}
              />
            )
            : undefined}
          history={attached && isFolder(shown)
            ? <FolderNoGit />
            : attached
            ? <RepositoryHistory repository={shown.repository} workspace={workspaceOf(shown)} here={Boolean(shown.root)} />
            : undefined}
          // Its checkout's changes, where the person let Daoris change its files (Setup's Reach), read while its tab shows.
          changes={attached && named(driver.data?.checkoutWrites, shown.repository)
            ? <RepositoryChanges repository={shown.repository} workspace={workspaceOf(shown)} here={Boolean(shown.root)} />
            : undefined}
          // Its queue, candidates and history, read while its tab shows.
          integration={attached && isFolder(shown)
            ? <FolderNoGit />
            : attached ? <RepositoryIntegration repository={shown.repository} notify={notify} /> : undefined}
          files={attached && Boolean(shown.root) ? <RepositoryFiles repository={shown.repository} /> : undefined}
          tab={tab}
          onTab={onTab}
          // Adoption's own acts are an adopter's: managing writes its declaration into it.
          onManage={attached && shown.adopted ? () => setManaging(shown) : undefined}
          onOpenCode={onOpenCode ? () => onOpenCode(shown.repository) : undefined}
          onOpenFolder={attached && Boolean(shown.root) ? () => openFolder.mutate(shown.repository, { onError: failure(notify) }) : undefined}
        />
      )
      : goneOrLoading('repository')
    : <ProjectsMainNotice state="none" action={attached ? add : undefined} />;

  return {
    list: {
      view: 'projects',
      name: t('nav.projects'),
      labels: { open: t('projects.list.open'), close: t('projects.list.close'), resize: t('projects.list.resize') },
      // Adding and importing touch machine paths, so they are a shell's: a browser's list makes nothing.
      make: attached ? { label: add.label, onMake: add.onAct } : undefined,
      more: attached
        ? <ListMore label={t('projects.list.more')} items={[{ id: 'import', label: t('projects.list.import') }]} onChoose={() => setImporting({})} />
        : undefined,
      loading: registry.isPending,
      // 🔴 The first thing a new installation shows, and it must not be a header over a blank page: the fixture always holds
      // a repository, so no test renders this by default. It names the workspace when the scope is one, because the
      // machine may hold repositories in another.
      empty: registry.data?.length === 0
        ? {
            headline: workspace ? t('projects.empty.headlineIn', { workspace }) : t('projects.empty.headline'),
            body: t(attached ? 'projects.empty.body' : 'projects.empty.bodyBrowser'),
          }
        : undefined,
      chosen,
      // A remembered repository reopens only while the registry still holds it: one retired opens nothing chosen. A
      // workspace, while a repository registered here is still in it.
      standing: !chosen ? undefined : shown || workspaceHeld ? 'live' : registry.data !== undefined ? 'gone' : 'unread',
      body: (
        <ProjectList
          groups={groups}
          chosen={chosenRepository}
          chosenWorkspace={chosenWorkspace}
          unanswered={registry.data === undefined && registry.error ? sentence(registry.error) : null}
          onChoose={choose}
          onChooseWorkspace={(workspace) => choose(workspaceItem(workspace))}
        />
      ),
    },
    main: (
      <>
        {main}
        {adding && (
          // Once registered, the list has it chosen, as an installed plugin is.
          <AddProjectDrawer onClose={() => setAdding(null)} onAdded={onChoose} notify={notify} initialWorkspace={adding.workspace} />
        )}
        {importing && (
          <ImportFolderDrawer onClose={() => setImporting(null)} notify={notify} initialWorkspace={importing.workspace} />
        )}
        {managing && (
          <ManageProjectDrawer
            project={managing}
            onClose={() => setManaging(null)}
            // Retired by the person's own press, so the page goes back to choosing, not to *gone*.
            onRetired={() => onChoose(null)}
            notify={notify}
          />
        )}
      </>
    ),
  };
}

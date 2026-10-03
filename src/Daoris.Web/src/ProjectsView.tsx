import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { canBeAsked, type Registration } from './api';
import { sentence } from './format';
import { AddProjectDrawer, ImportFolderDrawer, ManageProjectDrawer } from './ProjectManage';
import { ProjectList, type RepositoryRowFacts } from './projects/ProjectList';
import { type Driving, ProjectPage, ProjectsMainNotice } from './projects/ProjectPage';
import { useRegistry, useRepositories } from './queries';
import { useScope } from './scope';
import { useDriver, useHarnesses, useLines, useSetDrivable, useSetHold, useSetStanding, useSetTrees, useSweepPlan } from './shell';
import { doorOf } from './tools';
import { failure, type Notify, useErrorNotify } from './ui';
import { ListMore } from './work/ListPane';
import type { ViewLayout } from './work/ViewFrame';

const list = <T,>(value: unknown): T[] => (Array.isArray(value) ? value as T[] : []);

/**
 * **The Repositories view** (D38; FRAME1e, D118 §2): what it hands the frame (D118 §5), its list pane and its main
 * area. The list holds the adopted repositories, then *Registered, not adopted*; its `＋` adds a repository and its ⋯
 * imports a folder. The main area holds the chosen repository's page, with *Manage* and the door to its code map in its
 * header. A record is the main area and a form a drawer (§3d): adding, importing and managing stay drawers.
 *
 * @remarks
 * **A hook, because a view hands the frame a value** (`ViewLayout`), as Quests' and Plugins' are: the list and the main
 * area are drawn in two places the frame decides. The application holds it on every view, and says its errors only
 * while it is in front. Every query it holds the frame already holds, so holding it everywhere asks nothing more.
 *
 * **What it remembers is its list's** (`listPanes.ts`, §3f): the chosen repository, by name, which a door into the view
 * names through the opener (§3i).
 *
 * **Membership is a repository's own act** (D32): Daoris never writes into a sibling, so nothing joins by being seen,
 * and the join steps are proposed as text, never a button (D31's shape). The management surfaces exist where a shell
 * does (D48 §7): managing repositories means touching machine paths, and a browser has none.
 */
export function useProjectsView({
  active, chosen, onChoose, notify, onOpenCode, addRequested = false, onAddOpened, importRequested = false, onImportOpened,
}: {
  /** The view is in front: only then are its errors said. */
  active: boolean;
  /** The list's chosen item, a repository's name, which the application remembers (`daoris.list.projects.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  notify: Notify;
  /** Its code map, one level into the Map (MAP3a): the door a repository's page offers. */
  onOpenCode?: (repository: string) => void;
  /**
   * The Workspace menu's *Add repository…* (D75), an EVENT like Quests' opening draft: consumed once,
   * and cleared by its holder through `onAddOpened`, or the drawer would reopen on every render.
   */
  addRequested?: boolean;
  onAddOpened?: () => void;
  /** And its *Import a folder…* (D77): the same kind of event, for the import drawer. */
  importRequested?: boolean;
  onImportOpened?: () => void;
}): ViewLayout {
  const { t } = useTranslation();
  const registry = useRegistry();
  const { workspace } = useScope();
  const repositories = useRepositories();
  const driver = useDriver();
  const roster = useHarnesses();
  const setDrivable = useSetDrivable();
  const setHold = useSetHold();
  const setTrees = useSetTrees();
  const setStanding = useSetStanding();
  // Each repository's line as the driver resolves it (WSR2) — shell-only, and absent in a browser.
  const lines = useLines();
  const lineOf = (repository: string) => (Array.isArray(lines.data?.lines) ? lines.data.lines : [])
    .find((line) => line.repository === repository && line.branch);
  // Session branches holding work no branch of the person's holds (WSR3, D88) — named on the repository,
  // so work is not lost in a pile nobody reads. Settings → Workspace lists them one by one.
  const sweep = useSweepPlan();
  const unlandedIn = (repository: string) => (Array.isArray(sweep.data?.branches) ? sweep.data.branches : [])
    .filter((branch) => branch.repository === repository && branch.kind === 'unlanded').length;
  // The driver bridge included: a STATE that fails silently reads as a machine with no driver. Said once, while the
  // view is in front (D118 §3h).
  useErrorNotify(active ? registry.error ?? repositories.error ?? driver.error : null, notify);

  const attached = driver.data !== undefined;
  const [adding, setAdding] = useState(false);
  useEffect(() => {
    if (!addRequested || !attached) return;
    setAdding(true);
    onAddOpened?.();
  }, [addRequested, attached, onAddOpened]);
  const [importing, setImporting] = useState(false);
  useEffect(() => {
    if (!importRequested || !attached) return;
    setImporting(true);
    onImportOpened?.();
  }, [importRequested, attached, onImportOpened]);
  const [managing, setManaging] = useState<Registration | null>(null);

  const rows = registry.data ?? [];
  const indexed = (name: string) => (repositories.data ?? []).find((r) => r.name === name);
  const named = (names: string[] | undefined, repository: string) =>
    (names ?? []).some((name) => name.toLowerCase() === repository.toLowerCase());
  const onDriverError = failure(notify);
  // Which door this machine's starts ride (INT3c): an unadopted repository is carried by the protocol door only
  // (D70), so on a direct one its page says a quest there will sit. Unknown says nothing.
  const door = doorOf(roster.data?.adapter, list(roster.data?.harnesses));

  /** Its standing on this machine: the driver's choices and whether a checkout is here, where a shell answers. */
  const standing = (registration: Registration) => driver.data && {
    drivable: named(driver.data.drivable, registration.repository),
    held: named(driver.data.holds, registration.repository),
    // Only a local host names a checkout, and only to its own page (D47 §4).
    here: Boolean(registration.root),
  };
  const facts = (registration: Registration): RepositoryRowFacts => ({
    registration,
    ...standing(registration),
    entries: indexed(registration.repository)?.total ?? null,
  });

  /**
   * This machine's driving row for one repository — the same row wherever it can be driven: every adopter, and since
   * INT3c one not adopted with a root here (D70). One with no root has nowhere to start, and gets none.
   */
  const drivingOf = (registration: Registration): Driving | null => {
    if (!driver.data || (!registration.adopted && !canBeAsked(registration))) return null;
    const { repository } = registration;
    return {
      drivable: named(driver.data.drivable, repository),
      held: named(driver.data.holds, repository),
      ownTree: named(driver.data.trees, repository),
      onDrive: (next) => setDrivable.mutate({ repository, drivable: next }, { onError: onDriverError }),
      onHold: (next) => setHold.mutate({ repository, held: next }, { onError: onDriverError }),
      onTrees: (next) => setTrees.mutate({ repository, ownTree: next }, { onError: onDriverError }),
      note: !registration.adopted && door === 'pipe' ? t('projects.outside.directDoor') : undefined,
      // Its standing answer (KNOWUSE1b), matched without case as the driver matches it; a shell older than it answers no
      // `standing`, and nothing is offered rather than a field whose save would be refused.
      standing: (driver.data.standing ?? []).find((row) => row.repository.toLowerCase() === repository.toLowerCase()) ?? null,
      onStanding: driver.data.standing === undefined
        ? undefined
        : (says) => setStanding.mutate(says === null ? { repository } : { repository, says }, { onError: onDriverError }),
    };
  };

  const shown = chosen ? rows.find((row) => row.repository === chosen) : undefined;
  const add = { label: t('projects.manage.add'), onAct: () => setAdding(true) };
  const main = chosen
    ? shown
      ? (
        <ProjectPage
          key={shown.repository}
          registration={shown}
          counts={indexed(shown.repository)}
          line={lineOf(shown.repository)}
          unlanded={unlandedIn(shown.repository)}
          here={attached ? Boolean(shown.root) : undefined}
          driving={drivingOf(shown)}
          // Adoption's own acts are an adopter's (INT3c): managing writes its declaration into it.
          onManage={attached && shown.adopted ? () => setManaging(shown) : undefined}
          onOpenCode={onOpenCode ? () => onOpenCode(shown.repository) : undefined}
        />
      )
      : registry.data === undefined && registry.error
        ? <ProjectsMainNotice state="unanswered" sentence={sentence(registry.error)} />
        : <ProjectsMainNotice state={registry.data === undefined ? 'loading' : 'gone'} />
    : <ProjectsMainNotice state="none" action={attached ? add : undefined} />;

  return {
    list: {
      view: 'projects',
      name: t('nav.projects'),
      labels: { open: t('projects.list.open'), close: t('projects.list.close'), resize: t('projects.list.resize') },
      // Adding and importing touch machine paths, so they are a shell's (D48 §7): a browser's list makes nothing.
      make: attached ? { label: add.label, onMake: add.onAct } : undefined,
      more: attached
        ? <ListMore label={t('projects.list.more')} items={[{ id: 'import', label: t('projects.list.import') }]} onChoose={() => setImporting(true)} />
        : undefined,
      loading: registry.isPending,
      // 🔴 The first thing a new installation shows, and it was a header over a blank page: the fixture always holds
      // a repository, so nothing had ever rendered this. It names the workspace when the scope is one, because the
      // machine may hold repositories in another.
      empty: registry.data?.length === 0
        ? {
            headline: workspace ? t('projects.empty.headlineIn', { workspace }) : t('projects.empty.headline'),
            body: t(attached ? 'projects.empty.body' : 'projects.empty.bodyBrowser'),
          }
        : undefined,
      chosen,
      body: (
        <ProjectList
          adopted={rows.filter((row) => row.adopted).map(facts)}
          outside={rows.filter((row) => !row.adopted).map(facts)}
          chosen={chosen}
          unanswered={registry.data === undefined && registry.error ? sentence(registry.error) : null}
          onChoose={onChoose}
        />
      ),
    },
    main: (
      <>
        {main}
        {adding && (
          // Once registered, the list has it chosen, as an installed plugin is (D119 §3.1).
          <AddProjectDrawer onClose={() => setAdding(false)} onAdded={onChoose} notify={notify} />
        )}
        {importing && <ImportFolderDrawer onClose={() => setImporting(false)} notify={notify} />}
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

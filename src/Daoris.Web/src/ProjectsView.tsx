import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { canBeAsked, type Registration } from './api';
import { sentence } from './format';
import { AddProjectDrawer, ImportFolderDrawer, ManageProjectDrawer } from './ProjectManage';
import { ProjectList, type RepositoryRowFacts } from './projects/ProjectList';
import { ProjectPage, ProjectsMainNotice } from './projects/ProjectPage';
import type { Driving, RepositorySetupProps } from './projects/RepositorySetup';
import type { ProjectTab } from './projects/tabs';
import { useRegistry, useRepositories } from './queries';
import { useScope } from './scope';
import type { LandingRule } from './settings/Landings';
import {
  useAcross, useDriver, useHarnesses, useLines, usePlugins, useRuleAction, useRules, useSetDrivable, useSetHold, useSetLanding,
  useSetLanguage, useSetLine, useSetReadAcross, useSetStanding, useSetTrees, useSetWriteAcross, useSweepPlan,
} from './shell';
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
 * names through the opener (§3i); and its page's tab, Details or Setup, one for the view (UX6f, D150 §4.2), which its
 * holder keeps so a door can open a repository at Setup.
 *
 * **A repository's Setup is every setting it holds on this machine** (UX6f), each the screen's half of `daoris driver
 * <verb> <repository>` or `daoris agent rules … --repository` (D50), from the answers the driver gives every door: the
 * driver's state, the lines, reading across, the rules, and the plugins that land work. What only Setup reads (reading
 * across, the plugins) is asked only while Setup shows, so holding the view on every view still asks nothing more.
 *
 * **Membership is a repository's own act** (D32): Daoris never writes into a sibling, so nothing joins by being seen,
 * and the join steps are proposed as text, never a button (D31's shape). The management surfaces exist where a shell
 * does (D48 §7): managing repositories means touching machine paths, and a browser has none.
 */
export function useProjectsView({
  active, chosen, onChoose, tab = 'details', onTab, notify, onOpenCode, addRequested = false, onAddOpened, importRequested = false,
  onImportOpened,
}: {
  /** The view is in front: only then are its errors said. */
  active: boolean;
  /** The list's chosen item, a repository's name, which the application remembers (`daoris.list.projects.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  /** The page's tab, which the application remembers for the view (`daoris.list.projects.tab`). */
  tab?: ProjectTab;
  /** Choose a tab; absent, the page is Details alone. */
  onTab?: (tab: ProjectTab) => void;
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
  const setLanguage = useSetLanguage();
  const setLine = useSetLine();
  const setLanding = useSetLanding();
  const setRead = useSetReadAcross();
  const setWrite = useSetWriteAcross();
  const ruleAction = useRuleAction();
  // Each repository's line as the driver resolves it (WSR2) — shell-only, and absent in a browser.
  const lines = useLines();
  const resolvedLine = (repository: string) => (Array.isArray(lines.data?.lines) ? lines.data.lines : [])
    .find((line) => line.repository === repository);
  const lineOf = (repository: string) => {
    const line = resolvedLine(repository);
    return line?.branch ? line : undefined;
  };
  // And its session language (LANG1c), the driver's resolution, read rather than recomputed.
  const languageOf = (repository: string) => (Array.isArray(lines.data?.languages) ? lines.data.languages : [])
    .find((one) => one.repository === repository);
  // And how its work lands (WSR1), the driver's choice.
  const landingOf = (repository: string) => (Array.isArray(lines.data?.landings) ? lines.data.landings : [])
    .find((one) => one.repository === repository);
  // What only Setup reads is asked only while it shows (UX6f): reading across, and the plugins that land work. The rules
  // are the application's on every view already.
  const setupShown = active && driver.data !== undefined && tab === 'setup' && chosen !== null;
  const across = useAcross({ enabled: setupShown });
  const rules = useRules();
  const catalog = usePlugins({ enabled: setupShown });
  const landers = (catalog.data?.plugins ?? [])
    .filter((plugin) => plugin.enabled && !plugin.problem && plugin.points.includes('work/land'))
    .map((plugin) => plugin.id);
  // Session branches holding work no branch of the person's holds (WSR3, D88) — named on the repository,
  // so work is not lost in a pile nobody reads. Settings → Workspace lists them one by one.
  const sweep = useSweepPlan();
  const unlandedIn = (repository: string) => (Array.isArray(sweep.data?.branches) ? sweep.data.branches : [])
    .filter((branch) => branch.repository === repository && branch.kind === 'unlanded').length;
  // The driver bridge included: a STATE that fails silently reads as a machine with no driver. Said once, while the
  // view is in front (D118 §3h).
  useErrorNotify(active ? registry.error ?? repositories.error ?? driver.error : null, notify);
  // What Setup alone asks is said while Setup shows.
  useErrorNotify(setupShown ? across.error : null, notify);

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
   * This machine's driving for one repository — the same wherever it can be driven: every adopter, and since INT3c one
   * not adopted with a root here (D70). One with no root has nowhere to start, and gets none.
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
    };
  };

  /** Which of the driver's rows by workspace is this repository's workspace's, matched without case as the driver does. */
  const ofWorkspace = <R extends { workspace: string }>(rows: R[] | undefined, workspace: string) =>
    (rows ?? []).find((row) => row.workspace.toLowerCase() === workspace.toLowerCase());

  /**
   * Its session language (LANG1c, D142 point 7): the driver's resolution from the lines, its workspace's by the table's name,
   * and the table. A shell older than it answers no table, and nothing is offered rather than a field whose save is refused.
   */
  const languageOfPage = (registration: Registration): NonNullable<RepositorySetupProps['sessions']>['language'] => {
    const table = driver.data?.languageTable;
    if (!Array.isArray(table)) return undefined;
    const { repository } = registration;
    const resolved = languageOf(repository) ?? null;
    const shared = ofWorkspace(driver.data?.workspaceLanguages, resolved?.workspace ?? registration.workspace ?? 'default')?.language;
    return {
      resolved: resolved?.language ? resolved : null,
      inherited: table.find((row) => row.code === shared)?.name,
      table,
      onSet: (language) => setLanguage.mutate(language === null ? { repository } : { repository, language }, {
        onSuccess: () => {
          const name = table.find((row) => row.code === language)?.name;
          notify(name
            ? t('settings.sessionLanguage.saved', { name: repository, language: name })
            : t('settings.sessionLanguage.cleared', { name: repository }));
        },
        onError: onDriverError,
      }),
    };
  };

  /**
   * **Its Setup** (UX6f, D150 §4.2): every setting it holds on this machine, each with the press its terminal twin makes,
   * and each change said as the toast Settings' lists said it. A section the driver did not answer is absent, never a
   * control whose save would be refused; Reach waits for reading across, so it opens on what it holds.
   */
  const setupOf = (registration: Registration): RepositorySetupProps | null => {
    if (!driver.data) return null;
    const { repository } = registration;
    const same = (name: string) => name.toLowerCase() === repository.toLowerCase();
    const driving = drivingOf(registration);
    const line = resolvedLine(repository);
    const landing = landingOf(repository);
    const workspace = line?.workspace ?? landing?.workspace ?? registration.workspace ?? 'default';
    const above = ofWorkspace(driver.data.workspaceLandings, workspace);

    const work: RepositorySetupProps['work'] = lines.data === undefined ? null : {
      line: line ?? null,
      onLine: (branch) => setLine.mutate(branch ? { repository, branch } : { repository }, {
        onSuccess: () => notify(branch
          ? t('settings.lines.saved', { name: repository, branch })
          : t('settings.lines.cleared', { name: repository })),
        onError: onDriverError,
      }),
      landing: landing ?? null,
      landingAbove: above ? { ...above } : { form: 'merge' },
      landers,
      onLanding: (rule?: LandingRule) => setLanding.mutate(rule ? { repository, ...rule } : { repository }, {
        onSuccess: () => notify(!rule
          ? t('settings.landing.cleared', { name: repository })
          : rule.form === 'branch'
            ? rule.plugin
              ? t('settings.landing.savedBranchPlugin', { name: repository, pattern: rule.pattern, plugin: rule.plugin })
              : t('settings.landing.savedBranch', { name: repository, pattern: rule.pattern })
            : t('settings.landing.savedMerge', { name: repository })),
        onError: onDriverError,
      }),
    };

    // Its standing answer (KNOWUSE1b), matched without case as the driver matches it; a shell older than it answers no
    // `standing`, and nothing is offered rather than a field whose save would be refused.
    const sessions: RepositorySetupProps['sessions'] = driving && {
      language: languageOfPage(registration),
      standing: (driver.data.standing ?? []).find((row) => same(row.repository)) ?? null,
      onStanding: driver.data.standing === undefined
        ? undefined
        : (says) => setStanding.mutate(says === null ? { repository } : { repository, says }, { onError: onDriverError }),
    };

    const acrossRows = Array.isArray(across.data?.repositories) ? across.data.repositories : null;
    const mine = acrossRows?.find((row) => same(row.repository)) ?? null;
    const scope = rules.data && Array.isArray(rules.data.scopes)
      ? rules.data.scopes.find((row) => row.scope === 'repository' && row.name !== undefined && same(row.name))
      : undefined;
    const where = t('settings.rules.where.repository', { name: repository });
    const reach: RepositorySetupProps['reach'] = across.data === undefined && !across.error ? null : {
      across: mine,
      candidates: mine && acrossRows
        ? acrossRows
          .filter((other) => other.workspace === mine.workspace && !same(other.repository)
            && !mine.writesTo.some((to) => to.toLowerCase() === other.repository.toLowerCase()))
          .map((other) => other.repository)
        : [],
      onRead: (read) => setRead.mutate(read === undefined ? { repository } : { repository, read }, {
        onSuccess: () => notify(read === undefined
          ? t('settings.across.cleared', { name: repository })
          : t(read ? 'settings.across.savedOn' : 'settings.across.savedOff', { name: repository })),
        onError: onDriverError,
      }),
      onWrite: (to, allow) => setWrite.mutate({ repository, to, allow }, {
        onSuccess: () => notify(t(allow ? 'settings.across.declared' : 'settings.across.withdrawn', { repository, to })),
        onError: onDriverError,
      }),
      // The rules this repository's scope holds, by the name the registry holds it under; none where it holds none, and
      // the row absent where the rules were not answered.
      rules: rules.data && Array.isArray(rules.data.scopes)
        ? { allow: scope?.allow ?? [], ask: scope?.ask ?? [], deny: scope?.deny ?? [] }
        : null,
      onAddRule: (list, rule, added) => ruleAction.mutate({ action: 'add', list, rule, scope: 'repository', name: repository }, {
        onSuccess: () => {
          notify(t('settings.rules.added', { rule, list: t(`settings.rules.list.${list}`), where }));
          added();
        },
        onError: onDriverError,
      }),
      onRemoveRule: (rule) => ruleAction.mutate({ action: 'remove', rule, scope: 'repository', name: repository }, {
        onSuccess: () => notify(t('settings.rules.removed', { rule, where })),
        onError: onDriverError,
      }),
    };

    return {
      repository,
      driving,
      work,
      sessions,
      reach,
      busy: setLine.isPending || setLanding.isPending || setLanguage.isPending || setStanding.isPending
        || setRead.isPending || setWrite.isPending || ruleAction.isPending,
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
          drivable={driver.data ? named(driver.data.drivable, shown.repository) : undefined}
          held={driver.data ? named(driver.data.holds, shown.repository) : undefined}
          setup={setupOf(shown)}
          tab={tab}
          onTab={onTab}
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
      // A remembered repository reopens only while the registry still holds it (UX6b): one retired opens nothing chosen.
      standing: !chosen ? undefined : shown ? 'live' : registry.data !== undefined ? 'gone' : 'unread',
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

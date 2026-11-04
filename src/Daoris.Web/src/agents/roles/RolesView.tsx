import { type ComponentType, type Dispatch, type SetStateAction, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  helpModeOf, standingOf, useAccounts, useActors, useDriver, useHarnesses, useLines, usePlugins, useRoles,
  useSetRole, useSetRoles, useStarts, useWorkflowCurrents, type RoleChangePress,
} from '../../bridge/shell';
import { useRegistry } from '../../data/queries';
import { workspacesOf } from '../../data/workspaces';
import type { Notify } from '../../design/ui';
import { store, stored } from '../../lib/stored';
import { sentence } from '../../locales/format';
import { namer } from '../../settings/accounts/namer';
import { AskDaorisAbout, IntakeAbout } from '../../settings/ai/ActorAbout';
import { byTool, doorLabel } from '../tools';
import type { RoleSection } from '../modes';
import { type Editing, newRoleEditing, RolesCard, roleName, type RoleScope } from './Roles';
import { AppointedRoles } from './AppointedRoles';
import { DiagramSection } from './StructureCanvas';
import { RoleSectionMain, type RolesView } from './RoleSections';
import { type RoleDoors, AccountsSection, DiagramDetail, RepositorySection, WorkSection } from './StructureSections';
import {
  buildStructure, NO_FILTERS, nodeOfRow, placeId, roleId, sectionOf, type StructureFilters, type WorkflowRead, workflowReadOf,
} from './roleStructure';
import { useStructureRead } from './structureRead';

/** Where the application leads a press of these sections: the Agents mode at an agent, a repository's tab, a workspace's Setup, a plugin. */
export type RoleOpeners = {
  agent?: (agent: string) => void;
  repository?: (repository: string, tab: 'setup' | 'workflow' | 'integration') => void;
  workspace?: (workspace: string) => void;
  repositories?: () => void;
  plugin?: (plugin: string) => void;
  section?: (section: RoleSection) => void;
};

type Answered = { done: () => void; refused: (sentence: string) => void };

/**
 * **The role editor's reads and presses** (COMPANYVIEW1a, AGENTSPLACE1g1): the catalogue and each order over `ROLES`, the agents
 * this machine has and what each offers, and the presses `SET_ROLE` and `SET_ROLES` with their toasts. Held once, so the Roles mode's
 * card and a place's rows press the same acts.
 */
export function useRoleEditing(notify: Notify) {
  const { t, i18n } = useTranslation();
  const roster = useHarnesses();
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  // An agent a person can name here is a way in this machine HAS: one not installed would hold every start, and a choice whose only
  // outcome is a hold is worse than none.
  const agents = harnesses.filter((door) => door.present).map((door) => ({ value: door.harness, label: doorLabel(t, door) }));
  // What each agent itself offers for a model and an effort (the account editor's source), so a choice is picked and not typed.
  const offers = Object.fromEntries(harnesses.map((door) => [door.harness, door.settingsChoices ?? null]));

  // Each order as the driver reads it; every change of it is a role's, over SET_ROLE (ONEWAY1a).
  const actors = useActors();
  const staffing = actors.data ?? [];

  // The role catalogue, each change the person's press over SET_ROLE, judged by the driver's role door.
  const roles = useRoles();
  // Why there is no catalogue: a refusal, or an answer that does not read, is a failed read.
  const read = roles.data ? 'ok' : roles.isError || roles.isSuccess ? 'failed' : 'loading';
  const setRole = useSetRole();
  const pressRole = (press: RoleChangePress, answered: Answered) => {
    const row = roles.data?.roles.find((each) => each.id === press.id);
    const named = press.role
      ? roleName(t, i18n.language, { id: press.id, seeded: row?.seeded ?? false, name: press.role.name })
      : roleName(t, i18n.language, row ?? { id: press.id, seeded: false });
    setRole.mutate(press, {
      onSuccess: () => {
        const said = { set: 'saved', remove: 'removedSaved', default: 'defaulted', 'default-all': 'defaultedAll' }[press.change];
        notify(t(`settings.ai.roles.${said}`, { role: named }));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };
  // Several changes as one over SET_ROLES (the suggested choices): the driver judges them whole, so one toast says them and a refusal writes none.
  const setRoles = useSetRoles();
  const pressRoles = (presses: RoleChangePress[], answered: Answered) => {
    const named = presses.map((press) => {
      const row = roles.data?.roles.find((each) => each.id === press.id);
      return roleName(t, i18n.language, {
        id: press.id, seeded: row?.seeded ?? false, name: press.role?.name ?? row?.name,
      });
    });
    setRoles.mutate({ changes: presses }, {
      onSuccess: () => {
        notify(t('settings.ai.roles.savedAll', { roles: named.join(t('settings.ai.role.and')) }));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };
  return {
    harnesses, agents, offers, staffing, catalogue: roles.data ?? null, read: read as 'ok' | 'loading' | 'failed',
    busy: setRole.isPending || setRoles.isPending, onSet: pressRole, onSetAll: pressRoles,
  };
}

/**
 * **The Roles mode of the Agents place** (COMPANYVIEW1b, the company-view design §1, §3.1): its chosen section's page and every press on
 * it. Today only Working roles is built, the catalogue and its staffing of COMPANYVIEW1a moved here from AI features.
 *
 * @remarks
 * **The mode's one owner** (D168 point 3): this hook holds the open editor and every pending press, and hands the molecule its
 * answers, so `RolesCard` imports nothing of the bridge. Each press goes out as its route takes it (`SET_ROLE`, `SET_ROLES`)
 * and the driver judges; a refusal is its sentence under the press. It is mounted only while the mode is in
 * front, so opening the place asks the driver nothing of roles (D150 point 5).
 */
export function useRolesMode({ notify, section = 'working', doors = {}, page: given, placed = false }: {
  notify: Notify; section?: RoleSection; doors?: RoleOpeners; page?: RolesPage;
  /** Drawn in a section of This machine's page (SETUPUX1m), which names it: the catalogue's card has no title of its own. */
  placed?: boolean;
}) {
  const { t } = useTranslation();
  const own = useRolesPage();
  const page = given ?? own;
  const driver = useDriver();
  const registry = useRegistry('machine');
  const workspaces = workspacesOf(registry.data ?? []);
  const starts = useStarts(workspaces);
  const [editing, setEditing] = useState<Editing | null>(null);
  const { harnesses, agents, offers, staffing, catalogue, read, busy: editBusy, onSet: pressRole, onSetAll: pressRoles } = useRoleEditing(notify);
  const startRows = Array.isArray(starts.data?.starts) ? starts.data.starts : [];

  // Intake's and Ask Daoris's rows say what they do now under their order: Intake's per circle, the driver's own resolution.
  const first = (id: string) => staffing.find((row) => row.id === id)?.choices[0]?.agent ?? null;
  const helpMode = helpModeOf(driver.data?.helpMode);
  const about = {
    intake: {
      why: t('settings.ai.intake.why'),
      below: <IntakeAbout agent={first('intake')} starts={startRows.filter((start) => start.job === 'intake')} nameOf={namer(t, harnesses)} />,
    },
    'ask-daoris': {
      why: t('settings.ai.helper.why'),
      below: <AskDaorisAbout agent={first('ask-daoris')} full={driver.data?.helpMode !== undefined && helpMode === 'full'} />,
    },
  };

  // Work, Accounts and By repository (COMPANYVIEW1d) and the diagram (COMPANYVIEW1f): one read of the driver's structure, the shared
  // filters, the rows open, the chosen node and each opened repository's workflow, all held by the page; the sections draw and press.
  const asked = section === 'work' || section === 'accounts' || section === 'repository' || page.view === 'diagram';
  const { answer, read: structureRead } = useStructureRead(asked);
  const { filters, onFilters: setFilters, opened, onOpened: setOpened } = page;
  const toggle = (id: string) => {
    const opening = !opened.has(id);
    setOpened((was) => {
      const next = new Set(was);
      if (!next.delete(id)) next.add(id);
      return next;
    });
    // A row opened is the one chosen, so the diagram shows it chosen.
    const node = opening && structure ? nodeOfRow(structure, id) : null;
    if (node) page.onSelected(node);
  };
  const accounts = useAccounts();
  const plugins = usePlugins({ enabled: asked });
  const lines = useLines();
  const registryRows = registry.data ?? [];
  const sought = section === 'repository'
    ? registryRows.filter((row) => opened.has(placeId({ workspace: row.workspace ?? 'default', repository: row.repository }))).map((row) => row.repository)
    : [];
  const currents = useWorkflowCurrents(sought);
  const workflows: Record<string, WorkflowRead> = {};
  for (const { name, result } of currents) {
    workflows[name] = result.data ? workflowReadOf(result.data) : result.isError ? { read: 'failed' } : { read: 'loading' };
  }
  // The appointments as STATE says them, which the diagram draws; Appointed roles keeps their presses.
  const standing = standingOf(driver.data?.standingConfirmation);
  const structure = answer
    ? buildStructure({
      structure: answer, registry: registryRows, tools: byTool(harnesses), accounts: accounts.data, plugins: plugins.data?.plugins, lines: lines.data,
      driver: driver.data ? { drivable: driver.data.drivable ?? [], holds: driver.data.holds ?? [] } : null, workflows, filters,
      appointed: standing ? { stewardOffered: standing.stewardOffered, supervisorOffered: standing.supervisorOffered, appointments: standing.appointments } : null,
    })
    : null;

  // Choosing a node (COMPANYVIEW1f, §4 *Moving between them*): it is the one chosen, its row is opened so *Show as list* lands on it, and
  // one of another kind moves the section to its row's.
  const choose = (id: string) => {
    page.onSelected(id);
    const rows = id.startsWith('place:') && id.includes('/') ? [`place:${id.slice('place:'.length, id.indexOf('/'))}`, id] : [id];
    setOpened((was) => new Set([...was, ...rows]));
    const home = sectionOf(id);
    if (home !== section) doors.section?.(home);
  };
  const toDiagram = (row: string) => {
    const node = structure ? nodeOfRow(structure, row) : null;
    if (node) choose(node);
    page.onView('diagram');
  };

  const shown = catalogue || staffing.length > 0 || read === 'failed';
  const cardOf = (only?: string) => (
    <RolesCard
      catalogue={catalogue} read={read} staffing={staffing} agents={agents} about={about}
      busy={editBusy}
      onSet={pressRole} onSetAll={pressRoles}
      editor={editing} onEditor={(next) => {
        setEditing(next);
        if (next && !next.isNew) page.onSelected(roleId(next.id));
      }}
      only={only} onDiagram={(role) => toDiagram(roleId(role))}
      offers={offers} scope={page.scope} onScope={page.onScope} search={page.search} onSearch={page.onSearch} bare={placed && only === undefined}
      onOpenPlace={(kind, id) => {
        // The selection and the scope stay on the page; a repository role opens its own tab, a workspace role its Setup.
        if (kind === 'repository') doors.repository?.(id, 'setup');
        else doors.workspace?.(id);
      }}
    />
  );
  const card = shown ? cardOf() : null;

  // Where each press leads; "Open its setting" is the workspace's Setup under a workspace filter, Working roles for the two works whose
  // *Off* is there, and Repositories otherwise.
  const rowDoors: RoleDoors = {
    ...(doors.agent ? { agent: doors.agent } : {}),
    ...(doors.repository ? { repository: doors.repository } : {}),
    ...(doors.plugin ? { plugin: doors.plugin } : {}),
    // *Add a role here*: Working roles' editor for a new role, the place filled; it saves over SET_ROLE as any role does.
    addRole: (place) => {
      setEditing(newRoleEditing(catalogue?.works ?? [], agents, place, -1));
      doors.section?.('working');
      // The new role's editor is the catalogue's, which the list holds: from a node's detail, the list.
      page.onView('list');
    },
    // The page's state is the place's (RolesPage), so the selection, the scope and the filters are as they were on coming back.
    setting: (work) => {
      if (work === 'intake' || work === 'ask-daoris') doors.section?.('working');
      else if (filters.workspace && doors.workspace) doors.workspace(filters.workspace);
      else doors.repositories?.();
    },
    diagram: toDiagram,
  };
  const shared = { structure, read: structureRead, filters, onFilters: setFilters, open: opened, onToggle: toggle, doors: rowDoors };
  const selected = page.selected;
  const detail = selected && structure && (
    <DiagramDetail
      structure={structure} id={selected} open={opened} onToggle={toggle} doors={rowDoors}
      working={selected.startsWith('role:') && shown ? cardOf(selected.slice('role:'.length)) : undefined}
      appointed={selected.startsWith('appointed:') ? <AppointedRoles /> : undefined}
    />
  );
  return {
    card,
    work: <WorkSection {...shared} />,
    accounts: <AccountsSection {...shared} />,
    repository: <RepositorySection {...shared} />,
    diagram: (
      <DiagramSection
        structure={structure} read={structureRead} filters={filters} onFilters={setFilters} selected={selected} onSelect={choose} detail={detail}
      />
    ),
  };
}

/** What the Roles mode's page holds across its sections and both views: the view, the node chosen, the filters and the rows open. */
export type RolesPage = {
  view: RolesView;
  onView: (view: RolesView) => void;
  selected: string | null;
  onSelected: (id: string | null) => void;
  filters: StructureFilters;
  onFilters: (filters: StructureFilters) => void;
  opened: ReadonlySet<string>;
  onOpened: Dispatch<SetStateAction<ReadonlySet<string>>>;
  /** Which of All roles' rows are shown, by where the role is placed. */
  scope: RoleScope;
  onScope: (scope: RoleScope) => void;
  /** The words All roles' rows are narrowed by. */
  search: string;
  onSearch: (search: string) => void;
};

/** The person's choice of the diagram over the list, remembered on this machine. */
const VIEW_KEY = 'daoris.roles.view';

/**
 * The page's state, held by the place beside its list (`useAgentsView`) and not by the mode's main area, which is drawn anew each time
 * the place is in front: opening a policy, an account or a session from here and coming back finds the selection and the scope as left.
 */
export function useRolesPage(): RolesPage {
  const [scope, onScope] = useState<RoleScope>('all');
  const [search, onSearch] = useState('');
  const [view, setView] = useState<RolesView>(() => (stored(VIEW_KEY) === 'diagram' ? 'diagram' : 'list'));
  const [selected, onSelected] = useState<string | null>(null);
  const [filters, onFilters] = useState<StructureFilters>(NO_FILTERS);
  const [opened, onOpened] = useState<ReadonlySet<string>>(new Set());
  const onView = (next: RolesView) => {
    store(VIEW_KEY, next === 'diagram' ? 'diagram' : null);
    setView(next);
  };
  return { view, onView, selected, onSelected, filters, onFilters, opened, onOpened, scope, onScope, search, onSearch };
}

/**
 * The Roles mode's main area: the chosen section, and for Working roles the catalogue and its staffing; or the diagram, whichever section
 * is chosen (COMPANYVIEW1f). The page's state is held here, so the toggle and a section that holds its own keep it.
 */
export function RolesMode({ section, notify, doors = {}, page: held }: { section: RoleSection; notify: Notify; doors?: RoleOpeners; page?: RolesPage }) {
  const own = useRolesPage();
  const page = held ?? own;
  const Own = OWN_MAINS[section];
  return Own && page.view === 'list'
    ? <RoleSectionMain section={section} view={page.view} onView={page.onView}><Own /></RoleSectionMain>
    : <SharedMain section={section} notify={notify} doors={doors} page={page} />;
}

/** The sections that hold their own reads and presses, so opening another asks nothing of them. */
const OWN_MAINS: Partial<Record<RoleSection, ComponentType>> = {
  appointed: AppointedRoles,
};

/** Working roles, Work, Accounts and By repository, and the diagram: one hook's card, read and doors. */
function SharedMain({ section, notify, doors, page }: { section: RoleSection; notify: Notify; doors: RoleOpeners; page: RolesPage }) {
  const mode = useRolesMode({ notify, section, doors, page });
  const body = page.view === 'diagram' ? mode.diagram
    : section === 'working' ? mode.card : section === 'work' ? mode.work : section === 'accounts' ? mode.accounts : section === 'repository' ? mode.repository : null;
  // Back on the list, the chosen node's row is brought into view.
  const { view, selected } = page;
  useEffect(() => {
    if (view !== 'list' || !selected) return;
    document.querySelector(`[data-structure-row="${selected}"]`)?.scrollIntoView?.({ block: 'nearest' });
  }, [view, selected, section]);
  return <RoleSectionMain section={section} view={page.view} onView={page.onView}>{body}</RoleSectionMain>;
}

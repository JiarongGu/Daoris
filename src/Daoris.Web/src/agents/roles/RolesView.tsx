import { type ComponentType, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  helpModeOf, refusedNoRoute, useAccounts, useActors, useAnswerStaffingFound, useDriver, useHarnesses, useLines, usePlugins, useRoles, useSetActor,
  useSetRole, useStaffingFound, useStarts, useWorkflowCurrents, type RoleChangePress,
} from '../../bridge/shell';
import { useRegistry } from '../../data/queries';
import { workspacesOf } from '../../data/workspaces';
import type { Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { namer } from '../../settings/accounts/namer';
import { AskDaorisAbout, IntakeAbout } from '../../settings/ai/ActorAbout';
import { byTool, doorLabel } from '../tools';
import type { RoleSection } from '../modes';
import { type ActorChoice, type Editing, newRoleEditing, RolesCard, roleName } from './Roles';
import { AppointedRoles } from './AppointedRoles';
import { RoleSectionMain } from './RoleSections';
import { type RoleDoors, AccountsSection, RepositorySection, WorkSection } from './StructureSections';
import { buildStructure, NO_FILTERS, placeId, type StructureFilters, type WorkflowRead, workflowReadOf } from './structure';
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
 * **The Roles mode of the Agents place** (COMPANYVIEW1b, the company-view design §1, §3.1): its chosen section's page and every press on
 * it. Today only Working roles is built, the catalogue and its staffing of COMPANYVIEW1a moved here from AI features.
 *
 * @remarks
 * **The mode's one owner** (D168 point 3): this hook holds the open editor and every pending press, and hands the molecule its
 * answers, so `RolesCard` imports nothing of the bridge. Each press goes out as its route takes it (`SET_ROLE`, `SET_ACTOR`,
 * `STAFFING_FOUND_ANSWER`) and the driver judges; a refusal is its sentence under the press. It is mounted only while the mode is in
 * front, so opening the place asks the driver nothing of roles (D150 point 5).
 */
export function useRolesMode({ notify, section = 'working', doors = {} }: { notify: Notify; section?: RoleSection; doors?: RoleOpeners }) {
  const { t, i18n } = useTranslation();
  const driver = useDriver();
  const roster = useHarnesses();
  const registry = useRegistry('machine');
  const workspaces = workspacesOf(registry.data ?? []);
  const starts = useStarts(workspaces);
  const [editing, setEditing] = useState<Editing | null>(null);

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  // An agent a person can name here is a way in this machine HAS: one not installed would hold every start, and a choice whose only
  // outcome is a hold is worse than none.
  const agents = harnesses.filter((door) => door.present).map((door) => ({ value: door.harness, label: doorLabel(t, door) }));
  const startRows = Array.isArray(starts.data?.starts) ? starts.data.starts : [];

  // Each order as the driver reads it, set through SET_ACTOR, the person's press. A change of Build's agent keeps Chat and Brainstorm,
  // still as before, on the agent they ran, written for them, and says so.
  const actors = useActors();
  const setActor = useSetActor();
  const staffing = actors.data ?? [];
  const saveActor = (actor: string, choices: ActorChoice[] | null, answered: Answered) => {
    const build = staffing.find((row) => row.id === 'build')?.choices[0]?.agent;
    const followers = staffing.filter((row) => (row.id === 'chat' || row.id === 'brainstorm') && row.state === 'before')
      .map((row) => t(`settings.ai.actors.name.${row.id}`));
    const named = t(`settings.ai.actors.name.${actor}`);
    setActor.mutate({ actor, choices }, {
      onSuccess: () => {
        notify(t(choices === null ? 'settings.ai.actors.cleared' : 'settings.ai.actors.saved', { actor: named }));
        if (actor === 'build' && build && choices?.[0] && choices[0].agent !== build && followers.length > 0) {
          notify(t('settings.ai.actors.kept', { agent: build, actors: followers.join(t('settings.ai.actors.and')) }));
        }
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

  // The changes of driver.json's actors another program made, which the driver kept for the person.
  const found = useStaffingFound();
  const answerFound = useAnswerStaffingFound();
  const answerStaffingFound = (id: number, answer: 'take' | 'dismiss', answered: Answered) =>
    answerFound.mutate({ found: id, answer }, {
      onSuccess: () => {
        notify(t(answer === 'take' ? 'settings.ai.actors.found.taken' : 'settings.ai.actors.found.dismissed'));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });

  // The role catalogue, each change the person's press over SET_ROLE, judged by the driver's role door.
  const roles = useRoles();
  // Why there is no catalogue: a shell older than the route says so by NO_ROUTE or by answering nothing; any other refusal is a failed read.
  const read = roles.data ? 'ok' : roles.isError ? (refusedNoRoute(roles.error) ? 'notOffered' : 'failed') : roles.isSuccess ? 'notOffered' : 'loading';
  const setRole = useSetRole();
  const pressRole = (press: RoleChangePress, answered: Answered) => {
    const row = roles.data?.roles.find((each) => each.id === press.id);
    const named = press.role
      ? roleName(t, i18n.language, { id: press.id, seeded: row?.seeded ?? false, nameEn: press.role.name.en, nameZh: press.role.name.zh })
      : roleName(t, i18n.language, row ?? { id: press.id, seeded: false });
    setRole.mutate(press, {
      onSuccess: () => {
        notify(t(press.change === 'set' ? 'settings.ai.roles.saved' : press.change === 'remove' ? 'settings.ai.roles.removedSaved' : 'settings.ai.roles.defaulted', { role: named }));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

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

  const shown = roles.data || staffing.length > 0 || read === 'failed';
  const card = shown
    ? (
      <RolesCard
        catalogue={roles.data ?? null} read={read} staffing={staffing} agents={agents} about={about}
        busy={setRole.isPending || setActor.isPending || answerFound.isPending}
        onSet={pressRole} onSetActor={saveActor} found={found.data ?? []} onAnswerFound={answerStaffingFound}
        editor={editing} onEditor={setEditing}
      />
    )
    : null;

  // Work, Accounts and By repository (COMPANYVIEW1d): one read of the driver's structure, the shared filters, the rows open and each
  // opened repository's workflow, all held here; the sections draw rows and press doors.
  const asked = section === 'work' || section === 'accounts' || section === 'repository';
  const { answer, read: structureRead } = useStructureRead(asked);
  const [filters, setFilters] = useState<StructureFilters>(NO_FILTERS);
  const [opened, setOpened] = useState<ReadonlySet<string>>(new Set());
  const toggle = (id: string) => setOpened((was) => {
    const next = new Set(was);
    if (!next.delete(id)) next.add(id);
    return next;
  });
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
  const structure = answer
    ? buildStructure({
      structure: answer, registry: registryRows, tools: byTool(harnesses), accounts: accounts.data, plugins: plugins.data?.plugins, lines: lines.data,
      driver: driver.data ? { drivable: driver.data.drivable ?? [], holds: driver.data.holds ?? [] } : null, workflows, filters,
    })
    : null;
  // Where each press leads; "Open its setting" is the workspace's Setup under a workspace filter, Working roles for the two works whose
  // *Off* is there, and Repositories otherwise.
  const rowDoors: RoleDoors = {
    ...(doors.agent ? { agent: doors.agent } : {}),
    ...(doors.repository ? { repository: doors.repository } : {}),
    ...(doors.plugin ? { plugin: doors.plugin } : {}),
    // *Add a role here*: Working roles' editor for a new role, the place filled; it saves over SET_ROLE as any role does.
    addRole: (place) => {
      setEditing(newRoleEditing(roles.data?.works ?? [], agents, place, -1));
      doors.section?.('working');
    },
    setting: (work) => {
      if (work === 'intake' || work === 'ask-daoris') doors.section?.('working');
      else if (filters.workspace && doors.workspace) doors.workspace(filters.workspace);
      else doors.repositories?.();
    },
  };
  const shared = { structure, read: structureRead, filters, onFilters: setFilters, open: opened, onToggle: toggle, doors: rowDoors };
  return {
    card,
    work: <WorkSection {...shared} />,
    accounts: <AccountsSection {...shared} />,
    repository: <RepositorySection {...shared} />,
  };
}

/** The Roles mode's main area: the chosen section, and for Working roles the catalogue and its staffing. */
export function RolesMode({ section, notify, doors = {} }: { section: RoleSection; notify: Notify; doors?: RoleOpeners }) {
  const Own = OWN_MAINS[section];
  return Own ? <RoleSectionMain section={section}><Own /></RoleSectionMain> : <SharedMain section={section} notify={notify} doors={doors} />;
}

/** The sections that hold their own reads and presses, so opening another asks nothing of them. */
const OWN_MAINS: Partial<Record<RoleSection, ComponentType>> = {
  appointed: AppointedRoles,
};

/** Working roles, Work, Accounts and By repository: one hook's card, read and doors. */
function SharedMain({ section, notify, doors }: { section: RoleSection; notify: Notify; doors: RoleOpeners }) {
  const mode = useRolesMode({ notify, section, doors });
  const body = section === 'working' ? mode.card : section === 'work' ? mode.work : section === 'accounts' ? mode.accounts : section === 'repository' ? mode.repository : null;
  return <RoleSectionMain section={section}>{body}</RoleSectionMain>;
}

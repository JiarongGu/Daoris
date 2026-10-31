/**
 * **The roles' structure as rows** (COMPANYVIEW1d, the company-view design §3.3 to §3.5, §4's *One read, one function, one owner*):
 * `ROLE_STRUCTURE`'s answer, the roster, the accounts, the plugins, the lines and each opened repository's workflow, made into the
 * rows of Work, Accounts and By repository, one id each as `map/topology.ts` gives its nodes.
 *
 * @remarks
 * Pure: it asks nothing, resolves nothing (the driver's `RoleFor` answered every place already, so a second resolution here would be a
 * second truth) and words nothing; a row carries codes and names, and the molecule says them. A failed part is its row's own field,
 * never an empty list. The ids are shaped for the diagram of row 1f (`place:`, `role:`, `agent:`), which builds nodes and edges over
 * the same answer; this builds rows only.
 */
import type { RoleStructure, StructureChoice, StructureWork } from '../../data/wiring';
import type { AccountsAnswer } from '../../settings/accounts/accounts';
import type { RepositoryLine } from '../../settings/repository/Lines';
import type { RepositoryLanding } from '../../settings/repository/Landings';
import type { CurrentWorkflow } from '../../workflow/current';
import { accountStates, agentRows, latestRead } from '../agents';
import type { Tool } from '../tools';

/** The shared filters of the three sections (§3): the workspace, one work, one agent, a name, and *Needs a look*. */
export type StructureFilters = { workspace: string | null; work: string | null; agent: string | null; name: string; needsLook: boolean };

export const NO_FILTERS: StructureFilters = { workspace: null, work: null, agent: null, name: '', needsLook: false };

/** A place a structure is asked of, which is also a node of the diagram. */
export type StructurePlace = { workspace?: string; repository?: string };

export const placeId = (place: StructurePlace): string =>
  place.repository ? `place:${place.workspace ?? ''}/${place.repository}` : place.workspace ? `place:${place.workspace}` : 'place:machine';
export const roleId = (role: string): string => `role:${role}`;
export const agentId = (agent: string): string => `agent:${agent}`;
export const workId = (work: string): string => `work:${work}`;

/** The places to ask: this machine, each workspace and each repository (with its workspace), once each. */
export function placesToAsk(registry: readonly { repository: string; workspace?: string | null }[]): StructurePlace[] {
  const workspaces = [...new Set(registry.map((row) => row.workspace ?? 'default'))].sort();
  const repositories = [...registry].sort((a, b) => a.repository.localeCompare(b.repository));
  return [
    {},
    ...workspaces.map((workspace) => ({ workspace })),
    ...repositories.map((row) => ({ workspace: row.workspace ?? 'default', repository: row.repository })),
  ];
}

/** The codes of a choice that cannot run by its last reading; `unknown` and `not-read` run as any unknown does. */
export const CANNOT_RUN: ReadonlySet<string> = new Set(['unavailable', 'not-offered', 'cooling', 'signed-out', 'held']);

export type Reason =
  | 'catalogue' | 'held' | 'no-role' | 'policy-failed' | 'choices-failed' | 'cannot-run' | 'not-installed' | 'not-known' | 'integration' | 'not-driven-held';

export type RoleRef = { id: string; nameEn: string; nameZh: string; scope: { kind: string; id?: string }; revision: number };

/** A work's rule, as the driver resolved it for the place with where it was set. */
export type RuleRead =
  | { kind: 'rule'; consent: string; agents: string[]; concurrency?: number; minutes?: number; source: string }
  | { kind: 'not-a-rule' }
  | { kind: 'failed'; why: string };

export type WorkRow = {
  id: string;
  work: string;
  role: RoleRef | null;
  /** Design with no role of its own takes building's. */
  takesBuilding: boolean;
  held?: string;
  choices: StructureChoice[];
  choicesFailed?: string;
  /** Places whose answer for this work differs from the scope's. */
  own: number;
  rule: RuleRead | null;
  /** Agents the rule permits that no choice names; they run at account default. */
  atDefault: string[];
  reasons: Reason[];
};

export type UsedBy = {
  /** The role and the work it is used for here. */
  role: RoleRef;
  works: string[];
  /** The choice's position: `1` to `4`, `before`, or `rule-default`. */
  from: string;
  model?: string;
  effort?: string;
  state: StructureChoice['state'];
};

export type AccountRow = {
  id: string;
  agent: string;
  /** What a person calls it; the id where the roster has none. */
  product: string;
  /** The roster's tool this agent is, where the roster has it. */
  tool: string | null;
  known: boolean;
  /** Whether its binary is there by the last answer; null where it was not asked yet. */
  installed: boolean | null;
  version?: string;
  /** The accounts in the Agents list's phrase; null where the roster does not have the agent. */
  phrase: string | null;
  /** When any of its accounts was last read. */
  read: string | null;
  usedBy: UsedBy[];
  /** The plugin that declared it. */
  plugin: string | null;
  reasons: Reason[];
};

export type PluginLine = { id: string; name: string; via: ('agent' | 'landing')[] };

export type IntegrationRead = 'none' | 'proposed' | 'accepted' | 'unread' | string;

export type WorkflowRead =
  | { read: 'loading' }
  | { read: 'failed' }
  | { read: 'ok'; steps: number; integration: IntegrationRead | null };

export type RepositoryWork = {
  work: string;
  role: RoleRef | null;
  held?: string;
  first: StructureChoice | null;
  more: number;
  choicesFailed?: string;
  policyFailed?: string;
  /** Whether it differs from what this machine's roles give. */
  differs: boolean;
  /** Every choice cannot run by its last reading. */
  cannotRun: boolean;
};

export type RepositoryRow = {
  id: string;
  workspace: string;
  repository: string;
  /** Works that differ from this machine's roles; none: it takes this machine's roles. */
  differs: number;
  works: RepositoryWork[];
  line: { branch?: string; source: string } | null;
  landing: { form: string; plugin?: string; source: string } | null;
  driving: 'driven' | 'held' | 'not-driven' | null;
  workflow: WorkflowRead | null;
  plugins: PluginLine[];
  reasons: Reason[];
};

export type WorkspaceGroup = { id: string; workspace: string; repositories: RepositoryRow[]; differs: number };

export type Structure = {
  /** The scope Work and Accounts say: this machine, or the filtered workspace. */
  scope: StructurePlace;
  /** Why the store's roles do not read: every work then holds. */
  catalogueFailed?: string;
  works: WorkRow[];
  accounts: AccountRow[];
  repositories: WorkspaceGroup[];
  /** The filters' options. */
  options: { workspaces: string[]; works: string[]; agents: string[] };
};

/** What a plugin is to the structure: its id and name, and the agents it declares (the catalogue's entry says more). */
export type PluginRef = { id: string; name: string; harnesses: readonly string[] };

export type StructureInput = {
  structure: RoleStructure;
  registry: readonly { repository: string; workspace?: string | null }[];
  tools: readonly Tool[];
  accounts: AccountsAnswer | null | undefined;
  plugins: readonly PluginRef[] | null | undefined;
  lines: { lines: readonly RepositoryLine[]; landings?: readonly RepositoryLanding[] } | null | undefined;
  driver: { drivable: readonly string[]; holds: readonly string[] } | null | undefined;
  /** The workflow of each repository opened, by name. */
  workflows: Readonly<Record<string, WorkflowRead>>;
  filters: StructureFilters;
};

/**
 * A repository's Current workflow as the one line By repository says: its steps, and its integration declaration as none, proposed
 * (waiting for acceptance, which stays at the Integration tab) or accepted. A driver before the declaration leaves it out.
 */
export function workflowReadOf(current: CurrentWorkflow): WorkflowRead {
  const integration = current.start?.integration;
  const state: IntegrationRead | null = integration === undefined ? null
    : integration === null ? 'none'
      : integration.state === 'accepted' ? (integration.gates === 'none' ? 'none' : 'accepted')
        : integration.state === 'changed' || integration.state === 'unaccepted' ? 'proposed' : integration.state;
  return { read: 'ok', steps: current.steps.length, integration: state };
}

const sameAnswer =(a: StructureWork | undefined, b: StructureWork | undefined): boolean =>
  (a?.role?.id ?? null) === (b?.role?.id ?? null) && (a?.role?.revision ?? 0) === (b?.role?.revision ?? 0) && (a?.held ?? null) === (b?.held ?? null);

const toRef = (role: NonNullable<StructureWork['role']>): RoleRef => ({ ...role, scope: { ...role.scope } });

/** The agent a choice names, folded to the roster's tool where one has that door. */
function toolOf(tools: readonly Tool[], agent: string): Tool | undefined {
  return tools.find((tool) => tool.name === agent || tool.doors.some((door) => door.harness === agent));
}

const lower = (value: string | undefined | null) => (value ?? '').toLowerCase();

/** Whether every choice of a work cannot run by its last reading: one that can, or may, is a start that goes. */
const allCannot = (choices: readonly StructureChoice[]) => choices.length > 0 && choices.every((choice) => CANNOT_RUN.has(choice.state.code));

export function buildStructure(input: StructureInput): Structure {
  const { structure, filters } = input;
  const placeAt = (place: StructurePlace) =>
    structure.places.find((each) => (each.workspace ?? null) === (place.workspace ?? null) && (each.repository ?? null) === (place.repository ?? null));
  const machine = placeAt({});
  const scope: StructurePlace = filters.workspace ? { workspace: filters.workspace } : {};
  const scoped = placeAt(scope) ?? machine;
  const workAt = (place: typeof machine, work: string) => place?.works.find((each) => each.work === work);

  const registryWorkspaces = [...new Set(input.registry.map((row) => row.workspace ?? 'default'))].sort();
  const workCodes = structure.works.length > 0 ? structure.works : [...new Set(structure.places.flatMap((place) => place.works.map((each) => each.work)))];

  const pluginName = (id: string) => input.plugins?.find((plugin) => plugin.id === id)?.name ?? id;
  const pluginOfAgent = (agent: string): string | null => {
    const tool = toolOf(input.tools, agent);
    const declared = tool?.doors.find((door) => door.plugin)?.plugin;
    if (declared) return declared;
    return input.plugins?.find((plugin) => plugin.harnesses.includes(agent))?.id ?? null;
  };

  // ---- Work: the scope's answer per work, and how many places have their own.
  const works: WorkRow[] = workCodes.map((work) => {
    const found = workAt(scoped, work);
    const role = found?.role ? toRef(found.role) : null;
    const roleRow = role ? structure.roles.find((each) => each.id === role.id) : undefined;
    const takesBuilding = work === 'design' && role !== null && roleRow !== undefined && !roleRow.takes.includes('design');
    // Beneath the scope: every other place for this machine, a workspace's repositories for a workspace.
    const own = structure.places.filter((place) => (filters.workspace === null
      ? place.workspace !== undefined || place.repository !== undefined
      : place.workspace === filters.workspace && place.repository !== undefined)
      && !sameAnswer(workAt(place, work), found)).length;
    const policy = found?.policy;
    const rule: RuleRead | null = found?.policyFailed
      ? { kind: 'failed', why: found.policyFailed }
      : !policy
        ? null
        : policy.consent === 'not-a-rule'
          ? { kind: 'not-a-rule' }
          : { kind: 'rule', consent: policy.consent, agents: policy.agents, concurrency: policy.concurrency, minutes: policy.minutes, source: policy.source };
    const named = new Set((found?.choices ?? []).filter((choice) => choice.from !== 'rule-default').map((choice) => choice.agent));
    const atDefault = rule?.kind === 'rule' ? rule.agents.filter((agent) => !named.has(agent)) : [];
    const reasons: Reason[] = [];
    if (structure.catalogue) reasons.push('catalogue');
    if (found?.held) reasons.push('held');
    if (found && !found.role && !found.held) reasons.push('no-role');
    if (found?.policyFailed) reasons.push('policy-failed');
    if (found?.choicesFailed) reasons.push('choices-failed');
    if (found && allCannot(found.choices)) reasons.push('cannot-run');
    return {
      id: workId(work), work, role, takesBuilding, held: found?.held, choices: found?.choices ?? [], choicesFailed: found?.choicesFailed,
      own, rule, atDefault, reasons,
    };
  });

  // ---- Accounts: each agent a choice names, or that has accounts.
  const named = new Map<string, { installed: boolean | null; known: boolean; version?: string }>();
  for (const agent of structure.agents) named.set(agent.agent, { installed: agent.installed ?? null, known: agent.known, version: agent.version });
  const keys = new Map<string, { agent: string; tool: Tool | undefined }>();
  for (const agent of named.keys()) {
    const tool = toolOf(input.tools, agent);
    keys.set(tool?.name ?? agent, keys.get(tool?.name ?? agent) ?? { agent, tool });
  }
  for (const tool of input.tools) if (tool.accounts.length > 0 && !keys.has(tool.name)) keys.set(tool.name, { agent: tool.name, tool });
  const rowsOf = agentRows(input.tools, input.accounts);
  const accounts: AccountRow[] = [...keys.entries()].map(([key, { agent, tool }]) => {
    const names = new Set([agent, key, ...(tool?.doors.map((door) => door.harness) ?? [])]);
    const info = [...names].map((name) => named.get(name)).find((each) => each !== undefined);
    const usedBy = new Map<string, UsedBy>();
    for (const row of works) {
      row.choices.forEach((choice) => {
        if (!names.has(choice.agent)) return;
        const index = `${row.role?.id ?? ''}|${choice.from}|${choice.agent}|${choice.model ?? ''}`;
        const held = usedBy.get(index);
        if (held) held.works.push(row.work);
        else if (row.role) usedBy.set(index, { role: row.role, works: [row.work], from: choice.from, model: choice.model, effort: choice.effort, state: choice.state });
      });
    }
    const use = input.accounts?.agents.find((each) => each.agent === (tool?.name ?? agent));
    const reasons: Reason[] = [];
    if (info && !info.known) reasons.push('not-known');
    else if (info?.installed === false || (tool && !tool.present && info)) reasons.push('not-installed');
    if ([...usedBy.values()].some((used) => CANNOT_RUN.has(used.state.code))) reasons.push('cannot-run');
    const phrase = tool ? rowsOf.find((row) => row.name === tool.name)?.phrase ?? null : null;
    return {
      id: agentId(key), agent: key, product: tool?.product ?? agent, tool: tool?.name ?? null,
      known: info?.known ?? tool !== undefined, installed: info?.installed ?? (tool ? tool.present : null), version: info?.version,
      phrase, read: tool ? latestRead([...accountStates(tool, use).values()]) : null,
      usedBy: [...usedBy.values()], plugin: pluginOfAgent(agent), reasons,
    };
  });

  // ---- By repository: each workspace's group, its repositories inside.
  const repositories: WorkspaceGroup[] = registryWorkspaces.map((workspace) => {
    const rows: RepositoryRow[] = input.registry
      .filter((row) => (row.workspace ?? 'default') === workspace)
      .sort((a, b) => a.repository.localeCompare(b.repository))
      .map((row): RepositoryRow => {
        const place = placeAt({ workspace, repository: row.repository });
        const line = input.lines?.lines.find((each) => each.repository === row.repository);
        const landing = input.lines?.landings?.find((each) => each.repository === row.repository);
        const driven = input.driver ? (input.driver.holds.includes(row.repository) ? 'held' : input.driver.drivable.includes(row.repository) ? 'driven' : 'not-driven') : null;
        const flow = input.workflows[row.repository] ?? null;
        const rowWorks: RepositoryWork[] = (place?.works ?? []).map((each) => {
          const base = workAt(machine, each.work);
          return {
            work: each.work, role: each.role ? toRef(each.role) : null, held: each.held, first: each.choices[0] ?? null,
            more: Math.max(0, each.choices.length - 1), choicesFailed: each.choicesFailed, policyFailed: each.policyFailed,
            differs: !sameAnswer(each, base), cannotRun: allCannot(each.choices),
          };
        });
        const agents = new Set((place?.works ?? []).flatMap((each) => each.choices.map((choice) => choice.agent)));
        const via = new Map<string, Set<'agent' | 'landing'>>();
        for (const agent of agents) {
          const id = pluginOfAgent(agent);
          if (id) via.set(id, (via.get(id) ?? new Set()).add('agent'));
        }
        if (landing?.plugin) via.set(landing.plugin, (via.get(landing.plugin) ?? new Set()).add('landing'));
        const reasons: Reason[] = [];
        if (rowWorks.some((each) => each.held || each.choicesFailed || each.policyFailed)) reasons.push('held');
        if (rowWorks.some((each) => each.cannotRun)) reasons.push('cannot-run');
        if (driven === 'held') reasons.push('not-driven-held');
        if (flow?.read === 'ok' && flow.integration === 'proposed') reasons.push('integration');
        return {
          id: placeId({ workspace, repository: row.repository }), workspace, repository: row.repository,
          differs: rowWorks.filter((each) => each.differs).length, works: rowWorks,
          line: line && !line.notReadYet ? { branch: line.branch, source: line.source } : null,
          landing: landing ? { form: landing.form, plugin: landing.plugin, source: landing.source } : null,
          driving: driven, workflow: flow,
          plugins: [...via.entries()].map(([id, kinds]) => ({ id, name: pluginName(id), via: [...kinds] })), reasons,
        };
      });
    return { id: placeId({ workspace }), workspace, repositories: rows, differs: rows.filter((each) => each.differs > 0).length };
  });

  const options = {
    workspaces: registryWorkspaces,
    works: workCodes,
    agents: [...new Set(accounts.map((row) => row.agent))].sort(),
  };

  // ---- The shared filters; a work filter narrows each section to that work.
  const needle = lower(filters.name.trim());
  const hasAgent = (names: Iterable<string>) => filters.agent === null || [...names].some((name) => name === filters.agent);
  const agentNames = (row: AccountRow) => [row.agent, row.id.slice('agent:'.length), ...(toolOf(input.tools, row.agent)?.doors.map((door) => door.harness) ?? [])];
  const workShown = works.filter((row) => (filters.work === null || row.work === filters.work)
    && hasAgent([...row.choices.map((choice) => choice.agent), ...(row.rule?.kind === 'rule' ? row.rule.agents : [])].flatMap((agent) => {
      const tool = toolOf(input.tools, agent);
      return [agent, ...(tool ? [tool.name] : [])];
    }))
    && (needle === '' || [row.work, row.role?.id, row.role?.nameEn, row.role?.nameZh, ...row.choices.map((choice) => choice.agent)].some((text) => lower(text).includes(needle)))
    && (!filters.needsLook || row.reasons.length > 0));
  const accountsShown = accounts.filter((row) => hasAgent(agentNames(row))
    && (filters.work === null || row.usedBy.some((used) => used.works.includes(filters.work!)))
    && (needle === '' || [row.agent, row.product, ...row.usedBy.flatMap((used) => [used.role.id, used.role.nameEn, used.role.nameZh])].some((text) => lower(text).includes(needle)))
    && (!filters.needsLook || row.reasons.length > 0))
    .map((row) => (filters.work === null ? row : { ...row, usedBy: row.usedBy.filter((used) => used.works.includes(filters.work!)) }));
  const groups = repositories
    .filter((group) => filters.workspace === null || group.workspace === filters.workspace)
    .map((group) => ({
      ...group,
      repositories: group.repositories.filter((row) => (filters.work === null || row.works.some((each) => each.work === filters.work))
        && hasAgent(row.works.flatMap((each) => (each.first ? [each.first.agent, toolOf(input.tools, each.first.agent)?.name ?? ''] : [])))
        && (needle === '' || [row.repository, group.workspace, ...row.works.flatMap((each) => [each.role?.nameEn, each.role?.nameZh, each.work])].some((text) => lower(text).includes(needle)))
        && (!filters.needsLook || row.reasons.length > 0)),
    }))
    .filter((group) => group.repositories.length > 0 || (!filters.needsLook && needle === '' && filters.work === null && filters.agent === null));

  return { scope, catalogueFailed: structure.catalogue?.why, works: workShown, accounts: accountsShown, repositories: groups, options };
}

/** Each role that leans on an agent at this machine, for the agent's page (*Used by*): the roles at their choices' positions. */
export function usedByAgent(structure: RoleStructure, tool: Pick<Tool, 'name' | 'doors'>): UsedBy[] {
  const names = new Set([tool.name, ...tool.doors.map((door) => door.harness)]);
  const machine = structure.places.find((place) => place.workspace === undefined && place.repository === undefined);
  const used = new Map<string, UsedBy>();
  for (const work of machine?.works ?? []) {
    if (!work.role) continue;
    for (const choice of work.choices) {
      if (!names.has(choice.agent)) continue;
      const index = `${work.role.id}|${choice.from}|${choice.model ?? ''}`;
      const held = used.get(index);
      if (held) held.works.push(work.work);
      else used.set(index, { role: toRef(work.role), works: [work.work], from: choice.from, model: choice.model, effort: choice.effort, state: choice.state });
    }
  }
  return [...used.values()];
}

/** The sections whose rows a held start marks, from the read alone (the list's row wears it). */
export function heldMarks(structure: RoleStructure): { work: boolean; repository: boolean } {
  const held = (place: RoleStructure['places'][number]) => place.works.some((each) => each.held);
  return {
    work: Boolean(structure.catalogue) || structure.places.some((place) => place.repository === undefined && held(place)),
    repository: structure.places.some((place) => place.repository !== undefined && held(place)),
  };
}

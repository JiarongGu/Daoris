/**
 * **The roles' structure as rows** (COMPANYVIEW1d, the company-view design §3.3 to §3.5, §4's *One read, one function, one owner*):
 * `ROLE_STRUCTURE`'s answer, the roster, the accounts, the plugins, the lines and each opened repository's workflow, made into the
 * rows of Work, Accounts and By repository, one id each as `map/topology.ts` gives its nodes.
 *
 * @remarks
 * Pure: it asks nothing, resolves nothing (the driver's `RoleFor` answered every place already, so a second resolution here would be a
 * second truth) and words nothing; a row carries codes and names, and the molecule says them. A failed part is its row's own field,
 * never an empty list. The diagram's nodes and edges (COMPANYVIEW1f, §4) are built here too, over the same answer and filters, a node
 * with the id its row has (`place:`, `role:`, `agent:`), so the two views are one selection.
 */
import type { RoleStructure, StructureChoice, StructureWork } from '../../data/wiring';
import { type Authored, authoredTexts } from '../../locales/authored';
import type { AccountsAnswer } from '../../settings/accounts/accounts';
import type { RepositoryLine } from '../../settings/repository/Lines';
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

/**
 * The places to ask: this machine, each workspace and each *driven* repository (with its workspace), once each. A repository the
 * machine does not drive is left out (AGENTSPLACE1a): it takes no work, and the driver counts it for its workspace's fold.
 */
export function placesToAsk(
  registry: readonly { repository: string; workspace?: string | null }[], drivable: readonly string[],
): StructurePlace[] {
  const workspaces = [...new Set(registry.map((row) => row.workspace ?? 'default'))].sort();
  const driven = new Set(drivable.map((name) => name.toLowerCase()));
  const repositories = registry.filter((row) => driven.has(row.repository.toLowerCase())).sort((a, b) => a.repository.localeCompare(b.repository));
  return [
    {},
    ...workspaces.map((workspace) => ({ workspace })),
    ...repositories.map((row) => ({ workspace: row.workspace ?? 'default', repository: row.repository })),
  ];
}

/** The codes of a choice that cannot run by its last reading; `unknown` and `not-read` run as any unknown does. */
export const CANNOT_RUN: ReadonlySet<string> = new Set(['unavailable', 'not-offered', 'cooling', 'signed-out', 'held']);

export type Reason =
  | 'catalogue' | 'held' | 'no-role' | 'policy-failed' | 'choices-failed' | 'cannot-run' | 'not-installed' | 'not-known' | 'not-driven-held';

export type RoleRef = { id: string; name: Authored; scope: { kind: string; id?: string }; revision: number };

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

/** A plugin one of the repository's agents is declared by. */
export type PluginLine = { id: string; name: string };

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
  driving: 'driven' | 'held' | 'not-driven' | null;
  plugins: PluginLine[];
  reasons: Reason[];
};

export type WorkspaceGroup = { id: string; workspace: string; repositories: RepositoryRow[]; differs: number };

/** A node of the diagram (§4): a place, a working role, an appointed role or an agent, each with the id its row has. */
export type DiagramNode =
  | {
    id: string; kind: 'place'; workspace: string | null; repository: string | null;
    /** The workspace it folds into past 24 nodes; none for this machine. */
    group: string | null;
    /** False where the registry does not have it and only a role's scope names it: drawn as written. */
    known: boolean;
    /** Whether it sets something of its own: a role placed here, an answer that differs from the one it would take, an appointment. */
    own: boolean;
    /** Whether its roles are its own: a role placed here, or an answer that differs. */
    ownRoles: boolean;
    /** The repositories in this workspace that set nothing of their own, counted here rather than drawn. */
    counted: number;
    /** Per work, what the driver answered here, which choosing the place lights and its detail says. */
    works: RepositoryWork[];
    reasons: Reason[];
  }
  | { id: string; kind: 'role'; role: RoleRef; takes: string[]; first: StructureChoice | null; more: number; reasons: Reason[] }
  | { id: string; kind: 'appointed'; role: 'steward' | 'supervisor'; offered: boolean }
  | { id: string; kind: 'agent'; agent: string; product: string; phrase: string | null; known: boolean; installed: boolean | null; reasons: Reason[] };

/** An edge of the diagram, each from a record: a role placed in a scope, a role's choice, an appointment. */
export type DiagramEdge =
  | { id: string; kind: 'placed'; from: string; to: string }
  | {
    id: string; kind: 'staffed'; from: string; to: string; agent: string; model?: string; effort?: string;
    /** `1` to `4`, or `before`. */
    position: string;
    state: StructureChoice['state'];
    /** The last reading says this choice cannot run. */
    dashed: boolean;
  }
  | { id: string; kind: 'appointed'; from: string; to: string; dashed: boolean };

export type Diagram = { nodes: DiagramNode[]; edges: DiagramEdge[] };

/** The appointments as `STATE` says them: which roles this build offers, and each appointment's scope and whether it is dormant. */
export type AppointedRead = {
  stewardOffered: boolean;
  supervisorOffered: boolean;
  appointments: readonly { role: string; scope: string; workspace: string; repository: string; dormant: boolean }[];
};

export type Structure = {
  /** The scope Work and Accounts say: this machine, or the filtered workspace. */
  scope: StructurePlace;
  /** Why the store's roles do not read: every work then holds. */
  catalogueFailed?: string;
  works: WorkRow[];
  accounts: AccountRow[];
  repositories: WorkspaceGroup[];
  /** The diagram's nodes and edges, under the same filters. */
  diagram: Diagram;
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
  lines: { lines: readonly RepositoryLine[] } | null | undefined;
  driver: { drivable: readonly string[]; holds: readonly string[] } | null | undefined;
  /** The appointments from `STATE`; absent, the diagram draws no appointed role. */
  appointed?: AppointedRead | null;
  filters: StructureFilters;
};

const sameAnswer =(a: StructureWork | undefined, b: StructureWork | undefined): boolean =>
  (a?.role?.id ?? null) === (b?.role?.id ?? null) && (a?.role?.revision ?? 0) === (b?.role?.revision ?? 0) && (a?.held ?? null) === (b?.held ?? null);

const toRef = (role: NonNullable<StructureWork['role']>): RoleRef => ({ ...role, scope: { ...role.scope } });

/** The agent a choice names, folded to the roster's tool where one has that door. */
function toolOf(tools: readonly Tool[], agent: string): Tool | undefined {
  return tools.find((tool) => tool.name === agent || tool.doors.some((door) => door.door === agent));
}

const lower = (value: string | undefined | null) => (value ?? '').toLowerCase();

/** Whether every choice of a work cannot run by its last reading: one that can, or may, is a start that goes. */
const allCannot = (choices: readonly StructureChoice[]) => choices.length > 0 && choices.every((choice) => CANNOT_RUN.has(choice.state.code));

type AnsweredPlace = RoleStructure['places'][number] | undefined;

/** A place's answer per work as a row says it: the role, why it holds, its first choice, and whether it differs from this machine's. */
function placeWorks(place: AnsweredPlace, machine: AnsweredPlace): RepositoryWork[] {
  return (place?.works ?? []).map((each) => ({
    work: each.work, role: each.role ? toRef(each.role) : null, held: each.held, first: each.choices[0] ?? null,
    more: Math.max(0, each.choices.length - 1), choicesFailed: each.choicesFailed, policyFailed: each.policyFailed,
    differs: !sameAnswer(each, machine?.works.find((other) => other.work === each.work)), cannotRun: allCannot(each.choices),
  }));
}

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
    const names = new Set([agent, key, ...(tool?.doors.map((door) => door.door) ?? [])]);
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
        const driven = input.driver ? (input.driver.holds.includes(row.repository) ? 'held' : input.driver.drivable.includes(row.repository) ? 'driven' : 'not-driven') : null;
        const rowWorks = placeWorks(place, machine);
        const agents = new Set((place?.works ?? []).flatMap((each) => each.choices.map((choice) => choice.agent)));
        const pluginIds = new Set<string>();
        for (const agent of agents) {
          const id = pluginOfAgent(agent);
          if (id) pluginIds.add(id);
        }
        const reasons: Reason[] = [];
        if (rowWorks.some((each) => each.held || each.choicesFailed || each.policyFailed)) reasons.push('held');
        if (rowWorks.some((each) => each.cannotRun)) reasons.push('cannot-run');
        if (driven === 'held') reasons.push('not-driven-held');
        return {
          id: placeId({ workspace, repository: row.repository }), workspace, repository: row.repository,
          differs: rowWorks.filter((each) => each.differs).length, works: rowWorks,
          line: line && !line.notReadYet ? { branch: line.branch, source: line.source } : null,
          driving: driven,
          plugins: [...pluginIds].map((id) => ({ id, name: pluginName(id) })), reasons,
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
  const agentNames = (row: AccountRow) => [row.agent, row.id.slice('agent:'.length), ...(toolOf(input.tools, row.agent)?.doors.map((door) => door.door) ?? [])];
  const workShown = works.filter((row) => (filters.work === null || row.work === filters.work)
    && hasAgent([...row.choices.map((choice) => choice.agent), ...(row.rule?.kind === 'rule' ? row.rule.agents : [])].flatMap((agent) => {
      const tool = toolOf(input.tools, agent);
      return [agent, ...(tool ? [tool.name] : [])];
    }))
    && (needle === '' || [row.work, row.role?.id, ...authoredTexts(row.role?.name), ...row.choices.map((choice) => choice.agent)].some((text) => lower(text).includes(needle)))
    && (!filters.needsLook || row.reasons.length > 0));
  const accountsShown = accounts.filter((row) => hasAgent(agentNames(row))
    && (filters.work === null || row.usedBy.some((used) => used.works.includes(filters.work!)))
    && (needle === '' || [row.agent, row.product, ...row.usedBy.flatMap((used) => [used.role.id, ...authoredTexts(used.role.name)])].some((text) => lower(text).includes(needle)))
    && (!filters.needsLook || row.reasons.length > 0))
    .map((row) => (filters.work === null ? row : { ...row, usedBy: row.usedBy.filter((used) => used.works.includes(filters.work!)) }));
  const groups = repositories
    .filter((group) => filters.workspace === null || group.workspace === filters.workspace)
    .map((group) => ({
      ...group,
      repositories: group.repositories.filter((row) => (filters.work === null || row.works.some((each) => each.work === filters.work))
        && hasAgent(row.works.flatMap((each) => (each.first ? [each.first.agent, toolOf(input.tools, each.first.agent)?.name ?? ''] : [])))
        && (needle === '' || [row.repository, group.workspace, ...row.works.flatMap((each) => [...authoredTexts(each.role?.name), each.work])].some((text) => lower(text).includes(needle)))
        && (!filters.needsLook || row.reasons.length > 0)),
    }))
    .filter((group) => group.repositories.length > 0 || (!filters.needsLook && needle === '' && filters.work === null && filters.agent === null));

  const diagram = diagramOf(input, accounts, needle);
  return { scope, catalogueFailed: structure.catalogue?.why, works: workShown, accounts: accountsShown, repositories: groups, diagram, options };
}

const APPOINTED_ROLES = ['steward', 'supervisor'] as const;

/**
 * The diagram (§4): three columns, places, roles and agents, and the edges between them, each from a record. A place is drawn where it
 * sets something of its own, and counted in its workspace's node otherwise; an agent the roster lacks is drawn as the choice wrote it.
 * Never drawn: a rule, an account, a line, a plugin, a resolution, a role over a role.
 */
function diagramOf(input: StructureInput, accounts: readonly AccountRow[], needle: string): Diagram {
  const { structure, filters } = input;
  const registryWorkspaces = [...new Set(input.registry.map((row) => row.workspace ?? 'default'))].sort();
  const repositories = [...input.registry].sort((a, b) => a.repository.localeCompare(b.repository));
  const workspaceOfRepository = (repository: string) => input.registry.find((row) => row.repository === repository)?.workspace ?? 'default';
  const placeAt = (place: StructurePlace) =>
    structure.places.find((each) => (each.workspace ?? null) === (place.workspace ?? null) && (each.repository ?? null) === (place.repository ?? null));
  const machine = placeAt({});
  const differs = (place: typeof machine, parent: typeof machine) =>
    (place?.works ?? []).some((each) => !sameAnswer(each, parent?.works.find((other) => other.work === each.work)));

  // ---- Roles: the catalogue's, and any the driver answered that the catalogue lacks, each once.
  type RoleSeed = { ref: RoleRef; takes: string[]; stored: StructureChoice[] };
  const roles = new Map<string, RoleSeed>();
  for (const row of structure.roles) {
    if (row.state === 'removed') continue;
    roles.set(row.id, {
      ref: {
        id: row.id, name: row.name ?? '', revision: row.revision,
        scope: { kind: row.scopeKind ?? 'machine', ...(row.scopeId ? { id: row.scopeId } : {}) },
      },
      takes: row.takes,
      stored: (row.choices ?? []).map((each, index) => ({
        agent: each.agent, ...(each.model ? { model: each.model } : {}), ...(each.effort ? { effort: each.effort } : {}),
        from: String(index + 1), state: { code: 'not-read' },
      })),
    });
  }
  // A role's staffing is its own, so the first place the driver answered it at says its choices and their states.
  const answered = new Map<string, StructureChoice[]>();
  for (const place of structure.places) {
    for (const work of place.works) {
      if (!work.role) continue;
      const catalogued = structure.roles.some((row) => row.id === work.role!.id);
      if (!roles.has(work.role.id)) roles.set(work.role.id, { ref: toRef(work.role), takes: [], stored: [] });
      const seed = roles.get(work.role.id)!;
      if (!catalogued && !seed.takes.includes(work.work)) seed.takes.push(work.work);
      if (!answered.has(work.role.id) && !work.choicesFailed) answered.set(work.role.id, work.choices);
    }
  }

  // ---- Places: this machine, each workspace and repository that sets something of its own, and any a role's scope names as written.
  const appointments = input.appointed?.appointments ?? [];
  const placedAt = (kind: string, id: string) => [...roles.values()].some((seed) => seed.ref.scope.kind === kind && seed.ref.scope.id === id);
  const appointedAt = (scope: string) => appointments.some((each) => each.scope.toLowerCase() === scope.toLowerCase());
  const reasonsOf = (place: typeof machine): Reason[] => {
    const reasons: Reason[] = [];
    if ((place?.works ?? []).some((each) => each.held || each.choicesFailed || each.policyFailed)) reasons.push('held');
    if ((place?.works ?? []).some((each) => allCannot(each.choices))) reasons.push('cannot-run');
    return reasons;
  };
  type PlaceNode = Extract<DiagramNode, { kind: 'place' }>;
  const places: PlaceNode[] = [{
    id: placeId({}), kind: 'place', workspace: null, repository: null, group: null, known: true, own: true, ownRoles: true, counted: 0,
    works: placeWorks(machine, machine), reasons: reasonsOf(machine),
  }];
  for (const workspace of registryWorkspaces) {
    const here = placeAt({ workspace });
    const inside = repositories.filter((row) => (row.workspace ?? 'default') === workspace).map((row) => {
      const place = placeAt({ workspace, repository: row.repository });
      const ownRoles = placedAt('repository', row.repository) || differs(place, here ?? machine);
      return { repository: row.repository, place, ownRoles, own: ownRoles || appointedAt(`repository:${row.repository}`) };
    });
    const ownRoles = placedAt('workspace', workspace) || differs(here, machine);
    const own = ownRoles || appointedAt(`workspace:${workspace}`);
    const counted = inside.filter((each) => !each.own).length;
    if (own || counted > 0) {
      places.push({
        id: placeId({ workspace }), kind: 'place', workspace, repository: null, group: workspace, known: true, own, ownRoles, counted,
        works: placeWorks(here, machine), reasons: reasonsOf(here),
      });
    }
    for (const each of inside.filter((one) => one.own)) {
      places.push({
        id: placeId({ workspace, repository: each.repository }), kind: 'place', workspace, repository: each.repository, group: workspace,
        known: true, own: true, ownRoles: each.ownRoles, counted: 0, works: placeWorks(each.place, machine), reasons: reasonsOf(each.place),
      });
    }
  }
  const placeOfScope = (kind: string, id: string | undefined): string => {
    if (kind === 'workspace' && id) return placeId({ workspace: id });
    if (kind === 'repository' && id) return placeId({ workspace: workspaceOfRepository(id), repository: id });
    return placeId({});
  };
  const asWritten = (kind: string, id: string | undefined, ownRoles: boolean) => {
    const at = placeOfScope(kind, id);
    if (places.some((place) => place.id === at)) return;
    const workspace = kind === 'workspace' ? id! : workspaceOfRepository(id!);
    places.push({
      id: at, kind: 'place', workspace, repository: kind === 'repository' ? id! : null, group: workspace, known: false, own: true, ownRoles,
      counted: 0, works: [], reasons: [],
    });
  };
  for (const seed of roles.values()) {
    if (seed.ref.scope.kind === 'workspace' || seed.ref.scope.kind === 'repository') asWritten(seed.ref.scope.kind, seed.ref.scope.id, true);
  }

  // ---- The edges: each role placed in its scope, staffed choice by choice; each appointment.
  const edges: DiagramEdge[] = [];
  const roleNodes: Extract<DiagramNode, { kind: 'role' }>[] = [];
  const agentKeys: string[] = [];
  for (const [id, seed] of roles) {
    const node = roleId(id);
    const from = placeOfScope(seed.ref.scope.kind, seed.ref.scope.id);
    edges.push({ id: `placed:${from}>${node}`, kind: 'placed', from, to: node });
    const choices = (answered.get(id) ?? seed.stored).filter((choice) => choice.from !== 'rule-default');
    for (const choice of choices) {
      const key = toolOf(input.tools, choice.agent)?.name ?? choice.agent;
      const edge = `staffed:${node}>${agentId(key)}:${choice.from}`;
      if (edges.some((each) => each.id === edge)) continue;
      if (!agentKeys.includes(key)) agentKeys.push(key);
      edges.push({
        id: edge, kind: 'staffed', from: node, to: agentId(key), agent: choice.agent, position: choice.from, state: choice.state,
        dashed: CANNOT_RUN.has(choice.state.code), ...(choice.model ? { model: choice.model } : {}), ...(choice.effort ? { effort: choice.effort } : {}),
      });
    }
    roleNodes.push({
      id: node, kind: 'role', role: seed.ref, takes: seed.takes, first: choices[0] ?? null, more: Math.max(0, choices.length - 1),
      reasons: allCannot(choices) ? ['cannot-run'] : [],
    });
  }
  const appointedNodes: Extract<DiagramNode, { kind: 'appointed' }>[] = [];
  for (const role of APPOINTED_ROLES) {
    const offered = role === 'steward' ? input.appointed?.stewardOffered === true : input.appointed?.supervisorOffered === true;
    const these = appointments.filter((each) => each.role === role);
    if (!offered && these.length === 0) continue;
    const node = `appointed:${role}`;
    appointedNodes.push({ id: node, kind: 'appointed', role, offered });
    for (const each of these) {
      const [kind, id] = each.scope === 'machine' ? ['machine', undefined] : [each.scope.slice(0, each.scope.indexOf(':')), each.scope.slice(each.scope.indexOf(':') + 1)];
      if (kind === 'workspace' || kind === 'repository') asWritten(kind, id, false);
      const from = placeOfScope(kind!, id);
      edges.push({ id: `appointed:${from}>${node}`, kind: 'appointed', from, to: node, dashed: each.dormant });
    }
  }
  const agentNodes: Extract<DiagramNode, { kind: 'agent' }>[] = agentKeys.map((key) => {
    const row = accounts.find((each) => each.id === agentId(key));
    return row
      ? { id: row.id, kind: 'agent', agent: key, product: row.product, phrase: row.phrase, known: row.known, installed: row.installed, reasons: row.reasons }
      : { id: agentId(key), kind: 'agent', agent: key, product: key, phrase: null, known: false, installed: null, reasons: ['not-known'] };
  });

  // ---- The shared filters, as the sections take them: a node is kept by them, and an edge where both its ends are.
  const titleOf = (place: PlaceNode) => [place.workspace, place.repository].filter(Boolean).join('/');
  const inScope = (place: PlaceNode) => filters.workspace === null || place.group === null || place.group === filters.workspace;
  const answering = (work: string) => new Set(structure.places.flatMap((place) => place.works.filter((each) => each.work === work && each.role).map((each) => roleId(each.role!.id))));
  const byWork = filters.work === null ? null : answering(filters.work);
  const narrowed = filters.work !== null || filters.agent !== null || needle !== '' || filters.needsLook;
  const placeById = new Map(places.map((place) => [place.id, place]));
  const agentById = new Map(agentNodes.map((agent) => [agent.id, agent]));
  const out = (from: string) => edges.filter((edge) => edge.from === from);
  const into = (to: string) => edges.filter((edge) => edge.to === to);
  const keptRoles = new Set(roleNodes.filter((node) => {
    const from = placeById.get(into(node.id)[0]?.from ?? '');
    const agents = out(node.id).map((edge) => agentById.get(edge.to)!);
    return (from === undefined || inScope(from))
      && (byWork === null || byWork.has(node.id))
      && (filters.agent === null || agents.some((agent) => agent.agent === filters.agent))
      && (needle === '' || [node.role.id, ...authoredTexts(node.role.name), ...(from ? [titleOf(from)] : []), ...agents.flatMap((agent) => [agent.agent, agent.product])]
        .some((text) => lower(text).includes(needle)))
      && (!filters.needsLook || node.reasons.length > 0);
  }).map((node) => node.id));
  const keptAppointed = new Set(appointedNodes.filter((node) => filters.work === null && filters.agent === null && needle === ''
    && (!filters.needsLook || into(node.id).some((edge) => edge.kind === 'appointed' && edge.dashed))).map((node) => node.id));
  const keptPlaces = new Set(places.filter((place) => inScope(place) && (
    place.id === placeId({})
    || !narrowed
    || out(place.id).some((edge) => keptRoles.has(edge.to) || keptAppointed.has(edge.to))
    || (needle !== '' && lower(titleOf(place)).includes(needle))
  )).map((place) => place.id));
  const keptAgents = new Set(agentNodes.filter((agent) => (filters.agent === null || agent.agent === filters.agent)
    && into(agent.id).some((edge) => keptRoles.has(edge.from))).map((agent) => agent.id));
  const kept = new Set([...keptPlaces, ...keptRoles, ...keptAppointed, ...keptAgents]);
  return {
    nodes: [...places, ...roleNodes, ...appointedNodes, ...agentNodes].filter((node) => kept.has(node.id)),
    edges: edges.filter((edge) => kept.has(edge.from) && kept.has(edge.to)),
  };
}

/** What choosing a node lights (§4, *inheritance is lit, never drawn*): never a new edge, only what the records already draw. */
export function litOf(diagram: Diagram, id: string | null): { nodes: Set<string>; edges: Set<string> } {
  const nodes = new Set<string>();
  const edges = new Set<string>();
  const node = id === null ? undefined : diagram.nodes.find((each) => each.id === id);
  if (!node) return { nodes, edges };
  const light = (edge: DiagramEdge) => { edges.add(edge.id); nodes.add(edge.from); nodes.add(edge.to); };
  const around = (role: string) => {
    nodes.add(role);
    for (const edge of diagram.edges) if ((edge.to === role && edge.kind === 'placed') || (edge.from === role && edge.kind === 'staffed')) light(edge);
  };
  nodes.add(node.id);
  if (node.kind === 'place') {
    // Per work, the role the driver answered here, where that role is placed and its staffing; and this place's appointments.
    for (const work of node.works) if (work.role && diagram.nodes.some((each) => each.id === roleId(work.role!.id))) around(roleId(work.role.id));
    for (const edge of diagram.edges) if (edge.kind === 'appointed' && edge.from === node.id) light(edge);
  } else if (node.kind === 'agent') {
    for (const edge of diagram.edges) if (edge.kind === 'staffed' && edge.to === node.id) light(edge);
  } else if (node.kind === 'role') {
    around(node.id);
  } else {
    for (const edge of diagram.edges) if (edge.to === node.id) light(edge);
  }
  return { nodes, edges };
}

/** The node a row is drawn as: a work's by its role, a place that sets nothing of its own by its workspace's; none where none is drawn. */
export function nodeOfRow(structure: Structure, id: string): string | null {
  const drawn = (node: string) => structure.diagram.nodes.some((each) => each.id === node);
  if (id.startsWith('work:')) {
    const role = structure.works.find((row) => row.id === id)?.role;
    return role && drawn(roleId(role.id)) ? roleId(role.id) : null;
  }
  if (drawn(id)) return id;
  if (id.startsWith('place:') && id.includes('/')) {
    const workspace = `place:${id.slice('place:'.length, id.indexOf('/'))}`;
    return drawn(workspace) ? workspace : null;
  }
  return null;
}

/** Each role that leans on an agent at this machine, for the agent's page (*Used by*): the roles at their choices' positions. */
export function usedByAgent(structure: RoleStructure, tool: Pick<Tool, 'name' | 'doors'>): UsedBy[] {
  const names = new Set([tool.name, ...tool.doors.map((door) => door.door)]);
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

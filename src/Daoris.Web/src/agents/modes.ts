import { store, stored } from '../lib/stored';

/**
 * The Agents place's two modes (the Agents-place design §3), in the order its list's head draws them: Agents, each agent this machine
 * knows, and Setup, the tree of places. One place whose list switches between them, as Knowledge's does.
 */
export const AGENTS_MODES = ['agents', 'setup'] as const;

export type AgentsMode = (typeof AGENTS_MODES)[number];

/**
 * The Roles mode's five sections (§1), in the order its list draws them. Working roles, Work, Accounts and By repository are built; Appointed
 * roles is listed and says it comes later, built by its own row.
 */
export const ROLE_SECTIONS = ['working', 'appointed', 'work', 'accounts', 'repository'] as const;

export type RoleSection = (typeof ROLE_SECTIONS)[number];

/** Where the mode chosen is kept: one for the place, as Knowledge's is. */
const KEY = 'daoris.list.agents.mode';

export const isAgentsMode = (value: string | null | undefined): value is AgentsMode =>
  AGENTS_MODES.some((mode) => mode === value);

export const isRoleSection = (value: string | null | undefined): value is RoleSection =>
  ROLE_SECTIONS.some((section) => section === value);

/** The mode the place last showed, or Agents, the first, where nothing readable is kept. */
export function readAgentsMode(): AgentsMode {
  const kept = stored(KEY);
  return isAgentsMode(kept) ? kept : 'agents';
}

/**
 * Whether Setup shows its tree of places. Dark until the places' pages carry the Roles, Workflow and Sessions sections (AGENTSPLACE1g):
 * until then the mode shows the Roles sections, so nothing usable is lost. Lit by `daoris.agents.setup` set to `on`.
 */
export const setupLit = (): boolean => stored('daoris.agents.setup') === 'on';

/** The ids of Setup's places, as its list chooses them and a door names them (AGENTSPLACE1e, 1f): this machine, a workspace, a repository. */
export const MACHINE = 'machine';
export const workspaceId = (workspace: string) => `workspace:${workspace}`;
export const repositoryId = (workspace: string | null | undefined, repository: string) => `repo:${workspace ?? ''}/${repository}`;

/** Remember the mode chosen; Agents, the first, is what nothing kept means. */
export function storeAgentsMode(mode: AgentsMode): void {
  store(KEY, mode === 'agents' ? null : mode);
}

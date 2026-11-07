import { store, stored } from '../lib/stored';

/**
 * The Agents place's two modes, in the order its list's head draws them: Agents, each agent this machine
 * knows, and Setup, the tree of places. One place whose list switches between them, as Knowledge's does.
 */
export const AGENTS_MODES = ['agents', 'setup'] as const;

export type AgentsMode = (typeof AGENTS_MODES)[number];

/** Where the mode chosen is kept: one for the place, as Knowledge's is. */
const KEY = 'daoris.list.agents.mode';

export const isAgentsMode = (value: string | null | undefined): value is AgentsMode =>
  AGENTS_MODES.some((mode) => mode === value);

/** The mode the place last showed, or Agents, the first, where nothing readable is kept. */
export function readAgentsMode(): AgentsMode {
  const kept = stored(KEY);
  return isAgentsMode(kept) ? kept : 'agents';
}

/** The ids of Setup's places, as its list chooses them and a door names them: this machine, a workspace, a repository. */
export const MACHINE = 'machine';
export const workspaceId = (workspace: string) => `workspace:${workspace}`;
export const repositoryId = (workspace: string | null | undefined, repository: string) => `repo:${workspace ?? ''}/${repository}`;

/** Remember the mode chosen; Agents, the first, is what nothing kept means. */
export function storeAgentsMode(mode: AgentsMode): void {
  store(KEY, mode === 'agents' ? null : mode);
}

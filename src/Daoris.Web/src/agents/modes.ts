import { store, stored } from '../lib/stored';

/**
 * The Agents place's two modes (COMPANYVIEW1b, the company-view design §1), in the order its list's head draws them: Agents, each
 * agent this machine knows, and Roles, the positions those agents fill. One place whose list switches between them, as Knowledge's does.
 */
export const AGENTS_MODES = ['agents', 'roles'] as const;

export type AgentsMode = (typeof AGENTS_MODES)[number];

/**
 * The Roles mode's five sections (§1), in the order its list draws them. Only the first is built; the other four are listed and say they
 * come later, and each is built by its own row.
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

/** Remember the mode chosen; Agents, the first, is what nothing kept means. */
export function storeAgentsMode(mode: AgentsMode): void {
  store(KEY, mode === 'agents' ? null : mode);
}

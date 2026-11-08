import { store, stored } from '../lib/stored';

/**
 * A repository's page's tabs, in the order the page draws them: what the page holds, its branches and its
 * line's history, its checkout's changes where the person let Daoris change its files
 *  its integration and the files git does not manage. Its setup and workflow are its place's, in the Agents place's Setup mode.
 */
export const PROJECT_TABS = ['details', 'branches', 'history', 'changes', 'integration', 'files'] as const;

export type ProjectTab = (typeof PROJECT_TABS)[number];

/**
 * A workspace's page's tabs, in the order the page draws them: its repositories and what a start runs on,
 * the clean-up and bringing up to date across its repositories, and how work moves in them. Its defaults, access
 * and rules are its place's, in the Agents place's Setup mode; its remote's wiring is a row of Details.
 */
export const WORKSPACE_TABS = ['details', 'branches', 'workflow'] as const;

export type WorkspaceTab = (typeof WORKSPACE_TABS)[number];

/**
 * The parts of a workspace's setup a door may name, among them *Sessions' access* and its *Supervisor*.
 * Each opens on the workspace's place (`WORKSPACE_HOMES`), the remote on Details' Remote row. *Reach* is the place's Sessions row that
 * holds its rules and the machines its remote counts undeclared.
 */
export type WorkspaceSection = 'defaults' | 'access' | 'git' | 'supervisor' | 'remote' | 'reach';

/** Where the tab chosen is kept: one for the view, whichever repository it shows. */
const KEY = 'daoris.list.projects.tab';

/**
 * And a workspace's page's, kept apart: its tabs are not a repository's, so one chosen on either kind of page leaves the
 * other kind's where the person left it.
 */
const WORKSPACE_KEY = 'daoris.list.projects.workspaceTab';

const isTab = (value: string | null): value is ProjectTab => PROJECT_TABS.some((tab) => tab === value);

const isWorkspaceTab = (value: string | null): value is WorkspaceTab => WORKSPACE_TABS.some((tab) => tab === value);

/** The tab Repositories last showed, or Details where nothing readable is kept. */
export function readProjectTab(): ProjectTab {
  const kept = stored(KEY);
  return isTab(kept) ? kept : 'details';
}

/** Remember the tab chosen; Details, the first, is what nothing kept means. */
export function storeProjectTab(tab: ProjectTab): void {
  store(KEY, tab === 'details' ? null : tab);
}

/** The tab a workspace's page last showed, or Details where nothing readable is kept. */
export function readWorkspaceTab(): WorkspaceTab {
  const kept = stored(WORKSPACE_KEY);
  return isWorkspaceTab(kept) ? kept : 'details';
}

/** Remember a workspace's page's tab; Details, the first, is what nothing kept means. */
export function storeWorkspaceTab(tab: WorkspaceTab): void {
  store(WORKSPACE_KEY, tab === 'details' ? null : tab);
}

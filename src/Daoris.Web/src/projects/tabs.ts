import { store, stored } from '../lib/stored';

/**
 * A repository's page's tabs (UX6f, D150 §4.2), in the order the page draws them: what the page holds, and every setting
 * the repository holds on this machine. Branches and History join with UX6h.
 */
export const PROJECT_TABS = ['details', 'setup'] as const;

export type ProjectTab = (typeof PROJECT_TABS)[number];

/**
 * A workspace's page's tabs (UX6g, D150 §4.3), in the order the page draws them: its repositories and what a start runs on,
 * the clean-up and bringing up to date across its repositories, and every default and wiring it holds on this machine.
 */
export const WORKSPACE_TABS = ['details', 'branches', 'setup'] as const;

export type WorkspaceTab = (typeof WORKSPACE_TABS)[number];

/** The two sections of a workspace's Setup (UX6g, §4.3), which a door may ask to see open. */
export type WorkspaceSection = 'defaults' | 'remote';

/** Where the tab chosen is kept: one for the view, whichever repository it shows (§4.2, *remembered per view*). */
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

import { store, stored } from '../lib/stored';

/**
 * A repository's page's tabs (UX6f, D150 §4.2), in the order the page draws them: what the page holds, and every setting
 * the repository holds on this machine. Branches and History join with UX6h.
 */
export const PROJECT_TABS = ['details', 'setup'] as const;

export type ProjectTab = (typeof PROJECT_TABS)[number];

/** Where the tab chosen is kept: one for the view, whichever repository it shows (§4.2, *remembered per view*). */
const KEY = 'daoris.list.projects.tab';

const isTab = (value: string | null): value is ProjectTab => PROJECT_TABS.some((tab) => tab === value);

/** The tab Repositories last showed, or Details where nothing readable is kept. */
export function readProjectTab(): ProjectTab {
  const kept = stored(KEY);
  return isTab(kept) ? kept : 'details';
}

/** Remember the tab chosen; Details, the first, is what nothing kept means. */
export function storeProjectTab(tab: ProjectTab): void {
  store(KEY, tab === 'details' ? null : tab);
}

import { workspaceOf } from '../data/workspaces';
import { homeFocus, type Place, placeSaid, samePlace } from './focus';
import { type HelpConversationRow } from './history';

/**
 * Which conversations the history shows (`docs/2026-11-11-ask-scope-ux-design.md` §3): those homed in the window's workspace, every
 * workspace's, or those about a place, whose home or any focus it held is there or inside it.
 */
export type Showing = { kind: 'here' } | { kind: 'all' } | { kind: 'about'; place: Place };

/** One home's conversations, the window's own first. */
export type HomeGroup = { home: string; here: boolean; rows: HelpConversationRow[] };

const lower = (text: string) => text.toLowerCase();

/**
 * The rows grouped by the workspace each started in: the window's workspace first, then the others by name, each group in the
 * order the driver gave (the pinned, then the newest). A row that names no workspace is `default`'s, and a workspace is one group
 * whatever its letters' case; a window on every workspace marks none.
 */
export function homeGroups(rows: readonly HelpConversationRow[], window: string | null): HomeGroup[] {
  const groups = new Map<string, HomeGroup>();
  for (const row of rows) {
    const home = workspaceOf(row);
    const key = lower(home);
    const group = groups.get(key) ?? { home, here: window !== null && key === lower(window), rows: [] };
    group.rows.push(row);
    groups.set(key, group);
  }
  return [...groups.values()].sort((a, b) => Number(b.here) - Number(a.here) || a.home.localeCompare(b.home));
}

/** The row's focus where it is not its home, which is the only case its chip says; null for the usual quiet row. */
export function strayFocus(row: HelpConversationRow): Place | null {
  const focus = row.focus ?? null;
  return focus !== null && !samePlace(focus, homeFocus(workspaceOf(row))) ? focus : null;
}

/** A filter as one string: part of the query's key, so one filter's pages never answer another's. */
export const showingKey = (showing: Showing): string => (showing.kind === 'about' ? `about:${placeSaid(showing.place)}` : showing.kind);

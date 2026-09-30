import type { Session } from '../api';
import type { View } from '../commands';
import type { SettingsSection } from '../SettingsView';
import type { ViewId } from '../work/placements';

/**
 * What is on the screen, as Ask Daoris is told it (HELP1b, D89): the view, the workspace in scope, the
 * settings domain on Settings, and the session attended on Sessions with what a parked one asks.
 */
export type HelpWhere = {
  view: View;
  /** The workspace in scope, or null for every workspace. */
  workspace: string | null;
  settings?: SettingsSection | null;
  session?: { id: string; repository: string; state: string; note?: string | null } | null;
  /** Where Sessions' views stand and which region is showing (HELP2): the helper cannot see the window. */
  layout?: { right: readonly ViewId[]; panel: readonly ViewId[]; rightShown: boolean; panelShown: boolean } | null;
};

const VIEW_NAMES: Record<ViewId, string> = {
  timeline: 'the timeline', review: 'the review', ask: 'Ask Daoris', console: 'the console', terminal: 'the terminal',
};

/** "the timeline, the review and Ask Daoris", or "nothing". */
function listed(views: readonly ViewId[]): string {
  const names = views.map((view) => VIEW_NAMES[view]);
  if (names.length === 0) return 'nothing';
  return names.length === 1 ? names[0]! : `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}

// The window's own names (NAME1b), so the agent names a place the person can find.
const VIEWS: Record<View, string> = {
  overview: 'Overview', sessions: 'Sessions', quests: 'Quests', projects: 'Repositories',
  map: 'Map', convergence: 'Convergence', search: 'Search', settings: 'Settings',
};

const DOMAINS: Record<SettingsSection, string> = {
  start: 'Setup', appearance: 'Appearance', ai: 'AI features', workspace: 'Workspace', driver: 'Driver',
  agents: 'Agents', permissions: 'Permissions', plugins: 'Plugins', browser: 'Browser', logs: 'Machine log',
};

/** The attended session as the preface names it — or null when none is attended, or it is not in the list. */
export function attendedOf(id: string | null, sessions: readonly Session[]): HelpWhere['session'] {
  const session = id ? sessions.find((row) => row.id === id) : undefined;
  return session ? { id: session.id, repository: session.repository, state: session.state, note: session.note ?? null } : null;
}

/** How much of what a session says the preface carries: enough to name the question, not the analysis. */
const SAYS = 280;

/**
 * Where the person is, as one line the agent is handed ahead of their words.
 *
 * @remarks
 * **Agent-facing, so it is not translated**: the person never reads it as chrome, and one spelling is
 * what the agent learns to read. **Only what the screen already shows** (D47 §4): chrome the page
 * renders, never a machine path. Pure, so every screen is an argument.
 */
export function prefaceOf(where: HelpWhere): string {
  const place = where.view === 'settings' && where.settings
    ? `Settings → ${DOMAINS[where.settings]}`
    : `the ${VIEWS[where.view]} view`;
  const parts = [place, where.workspace ? `workspace \`${where.workspace}\`` : 'every workspace'];

  const session = where.view === 'sessions' ? where.session : null;
  if (session) {
    const state = session.state === 'awaiting-person' ? 'waiting on the person' : session.state;
    parts.push(`attending session \`${session.id}\` in \`${session.repository}\`, which is ${state}`);
  }

  let said = `Where the person is now: ${parts.join(', ')}.`;
  const note = session?.note?.replace(/\s+/g, ' ').trim();
  if (note) said += ` It says: "${note.length > SAYS ? `${note.slice(0, SAYS).trimEnd()}…` : note}".`;

  // Where the views stand (HELP2), on every view since the frame is on every view (DOCK1a). Absent where
  // there is no frame at all — a browser — which is the caller's to say by leaving it out.
  const layout = where.layout;
  if (layout) {
    const region = (views: readonly ViewId[], shown: boolean, open: string, shut: string) =>
      views.length === 0 ? listed(views) : `${listed(views)}, and is ${shown ? open : shut}`;
    said += ` The right side bar holds ${region(layout.right, layout.rightShown, 'open', 'closed')};`
      + ` the panel holds ${region(layout.panel, layout.panelShown, 'showing', 'hidden')}.`;
  }
  return said;
}

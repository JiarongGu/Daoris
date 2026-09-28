import type { Session } from '../api';
import type { View } from '../commands';
import type { SettingsSection } from '../SettingsView';

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
};

const VIEWS: Record<View, string> = {
  overview: 'Overview', sessions: 'Sessions', quests: 'Quests', projects: 'Projects',
  map: 'Map', convergence: 'Convergence', search: 'Search', settings: 'Settings',
};

const DOMAINS: Record<SettingsSection, string> = {
  appearance: 'Appearance', ai: "Daoris's own AI", workspace: 'Workspace', driver: 'Driver',
  agents: 'Agents & accounts', permissions: 'Permissions', plugins: 'Plugins', browser: 'Browser',
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
  return said;
}

import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId, HelpGrantState } from './wired';

// Restricted mode's configured access, read from the code: each
// grant's operations, how each is chosen, its default, and where an agent cannot be held to it. `ConfiguredAccess` draws from
// it, and the driver answers what is held.

/** One grant of the table. */
export type GrantSpec = {
  id: HelpGrantId;
  /** Its operations' codes in the table's order, each chosen on its own; a group the table takes whole is one code. */
  operations: readonly string[];
  /** What it starts as: off, waiting for approval (the browser), or on, inherited (the checkouts). */
  byDefault: 'off' | 'approval' | 'inherited';
  /**
   * Held only by an agent that takes Claude's permission rules: the browser's chosen writes are exact MCP rules. On another agent it
   * is unavailable until Daoris mediates the tools. The checkouts are held on any agent: Daoris serves their read and judges it.
   */
  needsRules: boolean;
  /**
   * Its operations are the driver's to name, as `built`: the checkouts, one box a readable checkout by its repository's
   * name as written, never translated; none where the driver serves none.
   */
  served?: boolean;
  /** Not built yet: nothing to choose (the confined shell). */
  later: boolean;
  /**
   * Set on a plugin's own tab, the one place its record is changed: drawn here
   * read only from that record, its door leading to the tab, never a second set of boxes. The browser's.
   */
  onPlugin: boolean;
  /** Its door's words, or none. */
  door: 'operations' | 'manage' | 'settings' | null;
};

export const GRANTS: readonly GrantSpec[] = [
  // Typed tools Daoris serves and enforces, so they hold on every agent. Inspection is one group; each removal its own.
  {
    id: 'housekeeping', operations: ['inspect', 'clean-up', 'discard-branch', 'drop-recovery'],
    byDefault: 'off', needsRules: false, later: false, onPlugin: false, door: 'operations',
  },
  // The server approval takes its reads together and each write on its own, on the plugin's page: nothing is
  // chosen here. No page code, ever.
  { id: 'browser', operations: [], byDefault: 'approval', needsRules: true, later: false, onPlugin: true, door: 'manage' },
  // The workspace's checkouts within the readable set, each on until turned off here, read through one tool Daoris judges at each
  // call, so held on every agent: a file, a folder, `git status` and `git branch --list`.
  {
    id: 'checkouts', operations: [], byDefault: 'inherited', needsRules: false, served: true, later: false, onPlugin: false,
    door: 'manage',
  },
  // The ordinary settings, each through the existing validation and writer.
  {
    id: 'settings', operations: ['notify', 'language', 'cap', 'cooloff', 'quiet'],
    byDefault: 'off', needsRules: false, later: false, onPlugin: false, door: 'settings',
  },
  { id: 'acts', operations: ['pause'], byDefault: 'off', needsRules: false, later: false, onPlugin: false, door: 'operations' },
  { id: 'shell', operations: [], byDefault: 'off', needsRules: false, later: true, onPlugin: false, door: null },
];

/**
 * A grant's state on this agent (§4.1). One the agent cannot be held to is unavailable whatever is chosen, so the screen never
 * shows it in force; the browser is on only once its reads are approved, and a change since approval is said apart.
 */
export function grantState(spec: GrantSpec, choice: HelpGrantChoice | undefined, agent: HelpGrantAgent): HelpGrantState {
  if (spec.later) return 'later';
  if (spec.needsRules && !agent.rules) return 'unavailable';
  const chosen = choice?.chosen ?? [];
  if (spec.id === 'browser') {
    if (choice?.changed) return 'changed';
    return chosen.includes('reads') ? 'on' : 'needs-approval';
  }
  return chosen.length > 0 ? 'on' : 'off';
}

import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId, HelpGrantState } from './wired';

// Restricted mode's configured access: each grant's operations, default, and where an agent cannot be held to it.

/** One grant of the table. */
export type GrantSpec = {
  id: HelpGrantId;
  /** Its operations' codes in the table's order, each chosen on its own; a group the table takes whole is one code. */
  operations: readonly string[];
  /** What it starts as: off, waiting for approval (the browser), or on, inherited (the checkouts). */
  byDefault: 'off' | 'approval' | 'inherited';
  /** Held only by an agent that takes Claude's permission rules (the browser's chosen writes); Daoris judges the rest itself. */
  needsRules: boolean;
  /** Its operations are the driver's to name, as `built`: a box per readable checkout, by its repository's name as written. */
  served?: boolean;
  /** Not built yet: nothing to choose (the confined shell). */
  later: boolean;
  /** Set on a plugin's own tab, drawn here read only from that record, its door leading there (the browser's). */
  onPlugin: boolean;
  /** Its door's words, or none. */
  door: 'operations' | 'manage' | null;
};

export const GRANTS: readonly GrantSpec[] = [
  // The server approval takes its reads together and each write on its own, on the plugin's page: nothing is
  // chosen here. No page code, ever.
  { id: 'browser', operations: [], byDefault: 'approval', needsRules: true, later: false, onPlugin: true, door: 'manage' },
  // The workspace's readable checkouts, each on until turned off here, read through one tool Daoris judges at each call.
  {
    id: 'checkouts', operations: [], byDefault: 'inherited', needsRules: false, served: true, later: false, onPlugin: false,
    door: 'manage',
  },
  // The acts Ask Daoris presses with no card, granted by family; a family names the driver's routes it covers (`ActFamilies`).
  {
    id: 'acts', operations: ['pause', 'clean-up', 'discard', 'settings'], byDefault: 'off', needsRules: false, later: false,
    onPlugin: false, door: 'operations',
  },
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

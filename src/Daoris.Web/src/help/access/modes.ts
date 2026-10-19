import type { StarterDoor } from '../starters';
import type { HelpMode, HelpPluginOffer, HelpReceipt, HelpWiredLack } from './wired';

// Ask Daoris's access modes on the page (HELPMODE1b, D183 points 2-4; the modes design §1, §5, §6), as values: the chip, what full
// access would add, and the door each lack names.

/**
 * What the chip says (design §1): the mode the conversation in view runs under, its record's, or the mode chosen before any start;
 * and, where the two differ, the chosen one as what the next start takes (*Restricted · full access next start*). The chosen mode
 * alone never stands for a conversation's own: a switch takes its next start (D183's HELPMODE1a2 note).
 */
export function chipOf(selected: HelpMode, running: HelpMode | null): { now: HelpMode; next: HelpMode | null } {
  if (running === null || running === selected) return { now: selected, next: null };
  return { now: running, next: selected };
}

/** Why a server restricted mode withheld is one full access hands (D183 as it amends D170): an approval, or the mode itself. */
const FULL_HANDS = new Set(['plugins', 'mode']);

/**
 * The servers full access would hand that restricted mode withholds (design §5's confirmation), each once: from the newest receipt,
 * each server it lacks whose door is its plugin's approval or the mode (not one a host or another agent would bring); and from the
 * plugins, each server one that contributes offers Ask Daoris and the person has not approved. Full access brings no host, no
 * other agent's door and no server that is not running (design §3).
 */
export function fullAdds(newest: HelpReceipt | null | undefined, plugins: readonly HelpPluginOffer[] | undefined): string[] {
  const names = new Set<string>();
  for (const lack of newest?.lacks ?? []) {
    if (lack.kind === 'server' && FULL_HANDS.has(lack.door)) names.add(lack.name);
  }
  for (const plugin of plugins ?? []) {
    if (!plugin.enabled || plugin.problem) continue;
    for (const server of plugin.helpServers ?? []) {
      if (server.state !== 'approved') names.add(server.name);
    }
  }
  return [...names];
}

/**
 * Where a lack's door is (design §6): its plugin's page for an approval, the mode picker for what full access grants, Settings'
 * Browser for a browser the host would bring and *This Daoris* for Daoris's own connector, and AI features for another agent. A door
 * a newer driver names that this page does not know is null: the lack is said, with no press.
 */
export function lackDoor(lack: HelpWiredLack): StarterDoor | 'mode' | null {
  switch (lack.door) {
    case 'plugins': return lack.plugin ? { view: 'plugins', item: lack.plugin } : { view: 'plugins' };
    case 'mode': return 'mode';
    case 'host': return { view: 'settings', section: lack.why.startsWith('browser.') ? 'browser' : 'wiring' };
    case 'harness': return { view: 'settings', section: 'ai' };
    default: return null;
  }
}

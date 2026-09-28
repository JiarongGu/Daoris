import type { View } from '../commands';
import type { SettingsAnchor, SettingsSection } from '../SettingsView';
import type { Tool } from '../tools';

/** Where the screen that fixes a starter is: a view, and in Settings its domain and the card in it. */
export type StarterDoor = { view: View; section?: SettingsSection; anchor?: SettingsAnchor };

/**
 * One thing this machine lacks (HELP1d, D89): the sentence's key and its values, the screen that fixes
 * it, and the terminal command that does the same (D50) where one does.
 */
export type Starter = {
  id: 'waiting' | 'nothing-registered' | 'nothing-driven' | 'agent-signed-out' | 'no-line' | 'helper-off';
  values: Record<string, string | number>;
  door: StarterDoor;
  command?: string;
};

/**
 * Ask Daoris's starters: what this machine lacks, in the order a setup goes — what waits on the person
 * first, since it is waiting now. Pure, so every machine is an argument (D24's no-model tier: the
 * useful part with no agent at all).
 *
 * @remarks
 * A tool is signed out only on a **definite** `out` from the tool, with no named account signed in
 * either — the roster's own rule (SES3): `unknown` is not a lack anyone can act on.
 */
export function starters(machine: {
  /** The repositories registered here. */
  repositories: readonly string[];
  drivable: readonly string[];
  tools: readonly Tool[];
  /** How many sessions wait on the person. */
  waiting: number;
  /** Repositories with no line set and none git can name. */
  unnamedLines: readonly string[];
  /** The agent Ask Daoris runs on, or null. */
  helper: string | null;
}): Starter[] {
  const found: Starter[] = [];

  if (machine.waiting > 0) found.push({ id: 'waiting', values: { count: machine.waiting }, door: { view: 'sessions' } });

  if (machine.repositories.length === 0) {
    found.push({ id: 'nothing-registered', values: {}, door: { view: 'projects' }, command: 'daoris connect' });
  } else if (machine.drivable.length === 0) {
    found.push({ id: 'nothing-driven', values: {}, door: { view: 'projects' }, command: 'daoris driver drive <repository>' });
  }

  for (const tool of machine.tools) {
    if (tool.present && tool.ownLogin === 'out' && !tool.accounts.some((account) => account.login === 'in')) {
      found.push({
        id: 'agent-signed-out', values: { tool: tool.product ?? tool.name },
        door: { view: 'settings', section: 'agents' }, command: `daoris agent login ${tool.name}`,
      });
    }
  }

  if (machine.unnamedLines.length > 0) {
    found.push({
      id: 'no-line', values: { count: machine.unnamedLines.length, first: machine.unnamedLines[0]! },
      door: { view: 'settings', section: 'workspace', anchor: 'lines' },
      command: `daoris driver line ${machine.unnamedLines[0]} <branch>`,
    });
  }

  if (!machine.helper) {
    found.push({ id: 'helper-off', values: {}, door: { view: 'settings', section: 'ai' }, command: 'daoris driver helper <agent>' });
  }

  return found;
}

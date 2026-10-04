import type { AgentPart } from '../agents/agents';
import type { View } from '../commands';
import type { ProjectTab } from '../projects/tabs';
import type { SettingsAnchor, SettingsSection } from '../SettingsView';
import type { Machine } from './machine';

/**
 * Where the screen that fixes a starter, or does a setup step, is: a view, and in Settings its domain
 * and the card in it — or on Projects, the Workspace menu's drawer that adds a repository or imports a
 * folder (SETUP1a). Since FRAME1c it may name the item it opens in its view — a session, a quest, a
 * repository, a plugin — which the application's one opener chooses there (D118 §3i); since UX6e an agent
 * and the part of its page.
 */
export type StarterDoor = {
  view: View; item?: string; section?: SettingsSection; anchor?: SettingsAnchor; drawer?: 'add' | 'import';
  /** A repository page's tab (UX6f): a door into a repository's own value opens its Setup. */
  tab?: ProjectTab;
  agentPart?: AgentPart;
};

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
 *
 * The machine is `readMachine`'s, the one reading the setup guide's steps are made from too (D97).
 */
export function starters(
  machine: Pick<Machine, 'repositories' | 'drivable' | 'tools' | 'waiting' | 'unnamedLines' | 'helper'>,
): Starter[] {
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
        // The agent's page, at its accounts (UX6e, D150 §5): where an account is signed in.
        door: { view: 'agents', item: tool.name, agentPart: 'accounts' }, command: `daoris agent login ${tool.name}`,
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

import { type AccountsAnswer, agentOf, machineScope, workspaceScope } from '../settings/accounts';
import type { Tool } from '../tools';

// What a workspace's page reads (UX6g, D150 §4.3), worked out from answers the page already holds. Pure, so every
// machine is an argument.

/** The accounts one agent's work in a workspace may run on: its own list, or this machine's, named as the roster names them. */
export type AccountsHere = {
  agent: string;
  product: string;
  /** The workspace names a default or a list of its own (D130 §3.2); else it runs on this machine's. */
  own: boolean;
  /** Each account by what a person calls it, in the list's order; the agent's own sign-in where nothing is named. */
  accounts: string[];
};

/**
 * **The accounts each agent may run here** (§4.3, read-only): for each installed agent, the workspace's own list, else its
 * own default, else this machine's list, default or the agent's own sign-in, as a start resolves them (D130 §3.2). A shell
 * older than the accounts' answer still names a workspace's default, which the roster carries.
 */
export function accountsHere(
  tools: readonly Tool[],
  answer: AccountsAnswer | undefined | null,
  workspace: string,
  nameOf: (owner: string, profile?: string | null) => string,
): AccountsHere[] {
  return tools.filter((tool) => tool.present).map((tool) => {
    const use = agentOf(answer, tool.name);
    const scope = use ? workspaceScope(use, workspace) : null;
    const rosterDefault = tool.workspaceDefaults.find((circle) => circle.workspace === workspace)?.profile ?? null;
    const ownNames = scope
      ? (scope.list.length > 0 ? scope.list : scope.default ? [scope.default] : [])
      : rosterDefault ? [rosterDefault] : [];
    const machine = use ? machineScope(use) : null;
    const machineNames = machine && machine.list.length > 0
      ? machine.list
      : (machine?.default ?? tool.machineDefault) ? [(machine?.default ?? tool.machineDefault)!] : [null];
    const names: (string | null)[] = ownNames.length > 0 ? ownNames : machineNames;
    return {
      agent: tool.name,
      product: tool.product ?? tool.name,
      own: ownNames.length > 0,
      accounts: names.map((name) => nameOf(tool.name, name)),
    };
  });
}

/** Where a deployment is, as a person reads it: its host, or the address whole where it is not a URL. */
export function hostOf(url: string): string {
  try {
    return new URL(url).host || url;
  } catch {
    return url;
  }
}

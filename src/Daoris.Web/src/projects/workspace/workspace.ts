import { type AccountsAnswer, agentOf, machineScope } from '../../settings/accounts/accounts';
import type { Tool } from '../../agents/tools';

// What a workspace's page reads (UX6g, D150 §4.3), worked out from answers the page already holds. Pure, so every
// machine is an argument.

/** The accounts one agent's work in a workspace may run on: Daoris's accounts, one set for every workspace (D197). */
export type AccountsHere = {
  agent: string;
  product: string;
  /** Each account by what a person calls it, in the set's order; none where the agent has no account of Daoris's. */
  accounts: string[];
};

/**
 * **The accounts each agent may run here** (§4.3, read-only; POOL1c, D197 point 1): for each installed agent, every account of
 * Daoris's, in the set's order, since one set serves every workspace; one the order does not name yet goes after, as the next
 * change would place it. None where the agent has no account of Daoris's: the tool's own sign-in is never what a start runs on,
 * and the page says no Daoris account is signed in.
 */
export function accountsHere(
  tools: readonly Tool[],
  answer: AccountsAnswer | undefined | null,
  nameOf: (owner: string, profile?: string | null) => string,
): AccountsHere[] {
  return tools.filter((tool) => tool.present).map((tool) => {
    const use = agentOf(answer, tool.name);
    const order = use ? machineScope(use).list.filter((name) => tool.accounts.some((account) => account.name === name)) : [];
    const names = [...order, ...tool.accounts.map((account) => account.name).filter((name) => !order.includes(name))];
    return {
      agent: tool.name,
      product: tool.product ?? tool.name,
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

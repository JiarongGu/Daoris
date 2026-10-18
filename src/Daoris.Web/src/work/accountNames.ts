import { accountLabel } from '../agents/agents';
import { byTool, type ToolDoor } from '../agents/tools';

/** What a person calls each account, by its id: the names a conversation's notes say an account by (NOTENAME1). */
export type AccountNames = ReadonlyMap<string, string>;

/**
 * The names a note says an account id by, read from the roster the page already holds, by the rule the agent's page leads its
 * row with (`accountName`, ACCTNAME1), told apart from another sign-in of its email by its organisation (`accountLabel`,
 * ACCOUNTID1b). A note carries an id and no tool for it, so an id is named only where the roster gives it one name everywhere
 * it appears and that name is not the id: an id nobody named, or two tools name differently, stays as the record wrote it.
 */
export function accountNames(harnesses: readonly ToolDoor[]): AccountNames {
  const names = new Map<string, string>();
  const unsure = new Set<string>();
  for (const tool of byTool(harnesses as ToolDoor[])) {
    for (const account of tool.accounts) {
      const name = accountLabel(account, tool);
      if (names.has(account.name) && names.get(account.name) !== name) unsure.add(account.name);
      names.set(account.name, name);
    }
  }
  for (const [id, name] of [...names]) if (unsure.has(id) || name === id) names.delete(id);
  return names;
}

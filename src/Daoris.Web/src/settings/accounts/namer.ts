import type { useTranslation } from 'react-i18next';
import { accountLabel, ownWho } from '../../agents/agents';
import { type AccountNamer, byTool, type ToolDoor } from '../../agents/tools';

/**
 * What a person calls an account, looked up on the TOOL, because a door's accounts are its owner's. One copy, for
 * every card that names an account from the driver's answer.
 *
 * @remarks
 * The rule is the agent's page's own (`accountName`), so a card says what the
 * account's row leads with: the person's name, else a key's handle, else, for a fresh `acct-` id nobody named, who signed
 * in, else its id. It said who signed in first, so an account the person named read as their email everywhere but its row.
 */
export function namer(t: ReturnType<typeof useTranslation>['t'], harnesses: ToolDoor[]): AccountNamer {
  const tools = byTool(harnesses);
  // By the tool, or by a door onto it: usage is counted per door, and a door's accounts are its
  // owner's.
  const toolFor = (owner: string) =>
    tools.find((tool) => tool.name === owner) ?? tools.find((tool) => tool.doors.some((door) => door.door === owner));
  return (owner: string, profile?: string | null) => {
    const tool = toolFor(owner);
    // The tool's own home is named as its row names it: who signed in, else this machine's own, so every
    // card says the same name for it.
    if (!profile) return (tool ? ownWho(tool) : null) ?? t('harness.own');
    const row = tool?.accounts.find((account) => account.name === profile);
    // Told apart from another sign-in of its email by its organisation, else its plan.
    return row ? accountLabel(row, tool) : profile;
  };
}

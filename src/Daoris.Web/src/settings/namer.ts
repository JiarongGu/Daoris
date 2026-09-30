import type { useTranslation } from 'react-i18next';
import { byTool, type ToolDoor } from '../tools';

/**
 * What a person calls an account — the roster's own rule (who signed in, a key's handle, the name),
 * looked up on the TOOL, because a door's accounts are its owner's (AGT7). One copy, for every card
 * that names an account from the driver's answer.
 */
export function namer(t: ReturnType<typeof useTranslation>['t'], harnesses: ToolDoor[]) {
  const tools = byTool(harnesses);
  // By the tool, or by a door onto it: usage is counted per door, and a door's accounts are its
  // owner's (AGT7).
  const toolFor = (owner: string) =>
    tools.find((tool) => tool.name === owner) ?? tools.find((tool) => tool.doors.some((door) => door.harness === owner));
  return (owner: string, profile?: string | null) => {
    const tool = toolFor(owner);
    // The tool's own home is named as its row names it: who signed in, else this machine's own. It
    // was "the agent's own sign-in" in one card and "its own home" in another (UX5 U53).
    if (!profile) return tool?.ownAccount ?? t('harness.own');
    const row = tool?.accounts.find((account) => account.name === profile);
    return row?.account ?? (row?.key ? t('harness.profile.keyName', { handle: row.key }) : profile);
  };
}

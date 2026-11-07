import type { AgentSheet } from './tools';

/** What a door's agent is read from: the roster's sheet half, each agent with its doors' sheets. */
export type AgentDoors = readonly Pick<AgentSheet, 'id' | 'doors'>[] | null | undefined;

/**
 * Which agent a door is (AGENTPROVIDER1 s-vii; the agent-provider design §7 row 10), as the roster's sheet answers it: the agent whose
 * doors hold it, so a move between one agent's two doors (`claude-code`, `claude-code-acp`) is one agent. A door no sheet names, or a
 * roster with no sheet, is its own agent.
 */
export function agentOfDoor(door: string, agents: AgentDoors): string {
  const sheets: readonly Pick<AgentSheet, 'id' | 'doors'>[] = Array.isArray(agents) ? agents : [];
  return sheets.find((agent) => (agent.doors ?? []).some((each) => each.door === door))?.id ?? door;
}

/**
 * One tool's shares of the usage answer (s-vii): the answer counts each account by agent, its provider's id, so a share is the tool's
 * where its agent is the tool or one of its doors (a plugin's door keyed by another door's pool is an agent of its own).
 */
export function usageOfTool<Share extends { agent: string }>(
  tool: { name: string; doors: readonly { door: string }[] },
  used: readonly Share[],
): Share[] {
  return used.filter((share) => share.agent === tool.name || tool.doors.some((each) => each.door === share.agent));
}

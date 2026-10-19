import type { Session } from '../../data/api';
import { type AccountsAnswer, type AccountScope, agentOf, machineScope, workspaceScope } from '../../settings/accounts/accounts';
import { toolOf, type ToolDoor } from '../../agents/tools';
import { coolingFor } from '../say';

/** Daoris's offer to hand a conversation over (MIXAGENT1b-ii, D163): why, when it is offered again, and where it would go. */
export type HandOverOffer = {
  /** `limit`: its account is cut; `near`: its account is near its limit by its scope's own near. */
  why: 'near' | 'limit';
  /** When a cut account is offered again; null for `near`. */
  until: string | null;
  /** The agent the conversation would go on with: the first other present agent that has an account its next start takes. */
  agent: string;
  /** That account; null where the walk takes the agent's own sign-in. */
  account: string | null;
};

/**
 * Whether Daoris offers to go on with another agent (MIXAGENT1b-ii, D163 point 3), read only from facts the page already holds:
 * the conversation's own account is cut (`coolingFor`), or near its limit (the scope's `near`: a limit reached, usage credits
 * or its number, and only where its agent says how near its sessions are), and another present agent, in the roster's order,
 * has an account its next start would take (`scope.next`). The agent's pace warning alone is never near, so it offers nothing
 * and passes no account (USAGEREAD3b, D165 point 2). A conversation on the tool's own sign-in can be cut and is never near,
 * since a sign-in that says nothing is never offered as near. No driver read: the offer is a line, and taken only by the
 * person's press (D163, `ask`).
 *
 * @remarks
 * The scope is the conversation's workspace's own where it names one, else the machine's, as the start's chip reads it, for the
 * agent that owns the door's accounts (AGT7). A shell older than the facts answers none, and then there is no offer.
 */
export function handOverOffer({ session, workspace, doors, answer, now }: {
  session: Pick<Session, 'adapter' | 'profile'>;
  /** The conversation's workspace, as the registry names it (`default` where it names none). */
  workspace: string;
  doors: readonly ToolDoor[];
  answer: AccountsAnswer | null | undefined;
  now: Date;
}): HandOverOffer | null {
  const scopeOf = (agent: NonNullable<ReturnType<typeof agentOf>>): AccountScope =>
    workspaceScope(agent, workspace) ?? machineScope(agent);

  const until = coolingFor(session, doors, answer, now);
  let why: HandOverOffer['why'] | null = until ? 'limit' : null;
  if (!why && session.profile) {
    const door = doors.find((each) => each.harness === session.adapter);
    const own = agentOf(answer, door ? toolOf(door) : session.adapter);
    const near = own?.speaks && scopeOf(own).near.some((each) => each.account === session.profile);
    if (near) why = 'near';
  }
  if (!why) return null;

  // Candidates are other tools (MIXAGENT1b-iii), each once by its first present door; a tool's other door is the same tool.
  for (const door of otherToolDoors(session, doors)) {
    const agent = agentOf(answer, toolOf(door));
    const scope = agent ? scopeOf(agent) : null;
    const next = scope?.next;
    if (!agent || !scope || !next || next.reason === 'waits') continue;
    const roomy = (account: string | null | undefined): boolean => {
      if (!account) return !live(agent.own?.cooling, now);
      const facts = (Array.isArray(agent.accounts) ? agent.accounts : []).find((each) => each.name === account);
      return !scope.near.some((each) => each.account === account)
        && !live(facts?.cooling, now)
        && !facts?.said?.windows?.some((window) => window.standing === 'refused');
    };
    // The walk's account first, else the next ready one of the order it tried; none with room, the tool is not offered.
    const taken = [next.account ?? null, ...(next.others ?? []).filter((each) => each.hold === 'ready').map((each) => each.account ?? null)]
      .find(roomy);
    if (taken === undefined) continue;
    return { why, until: why === 'limit' ? until : null, agent: door.harness, account: taken };
  }
  return null;
}

const live = (cooling: { until: string } | null | undefined, now: Date): boolean =>
  Boolean(cooling) && Date.parse(cooling!.until) > now.getTime();

/**
 * The present doors of tools other than the conversation's own, one door per tool, in the roster's order (MIXAGENT1b-iii): the
 * candidates Daoris's offer draws from and the hand-over's ask lists first.
 */
export function otherToolDoors(session: Pick<Session, 'adapter'>, doors: readonly ToolDoor[]): ToolDoor[] {
  const own = doors.find((each) => each.harness === session.adapter);
  const ownTool = own ? toolOf(own) : session.adapter;
  const seen = new Set<string>([ownTool]);
  const found: ToolDoor[] = [];
  for (const door of doors) {
    if (!door.present || seen.has(toolOf(door))) continue;
    seen.add(toolOf(door));
    found.push(door);
  }
  return found;
}

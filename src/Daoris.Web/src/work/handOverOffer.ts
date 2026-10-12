import type { Session } from '../api';
import { type AccountsAnswer, type AccountScope, agentOf, machineScope, workspaceScope } from '../settings/accounts';
import { toolOf, type ToolDoor } from '../tools';
import { coolingFor } from './say';

/** Daoris's offer to hand a conversation over (MIXAGENT1b-ii, D163): why, when it is offered again, and where it would go. */
export type HandOverOffer = {
  /** `limit`: its account is cut; `near`: its account says it is near its limit. */
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
 * the conversation's own account is cut (`coolingFor`), or near its limit by its own word (the scope's `near`, and only where
 * its agent says how near its sessions are), and another present agent, in the roster's order, has an account its next start
 * would take (`scope.next`). A conversation on the tool's own sign-in can be cut and is never near, since a sign-in that says
 * nothing is never offered as near. No driver read: the offer is a line, and taken only by the person's press (D163, `ask`).
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

  for (const door of doors) {
    if (!door.present || door.harness === session.adapter) continue;
    const agent = agentOf(answer, toolOf(door));
    const next = agent ? scopeOf(agent).next : null;
    if (!next || next.reason === 'waits') continue;
    return { why, until: why === 'limit' ? until : null, agent: door.harness, account: next.account ?? null };
  }
  return null;
}

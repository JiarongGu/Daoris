import type { Session } from '../../data/api';
import type { ActorRow } from '../../data/wiring';
import { type AccountsAnswer, type AccountScope, agentOf, machineScope, switchOf, workspaceScope } from '../../settings/accounts/accounts';
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
  /**
   * Where the session's actor has an order of its own (MODELROLE1e, D205 §3.5(e)), the choice the offer names: its number in that order
   * and the model it asks (null for account default). Absent where the roster's order stands because the actor is as before.
   */
  choice?: { n: string; model: string | null };
};

/**
 * Whether Daoris offers to go on with another agent (MIXAGENT1b-ii, D163 point 3), read only from facts the page already holds:
 * the conversation's own account is cut (`coolingFor`), or near its limit (the scope's `near`: a limit reached, usage credits
 * or its number, and only where its agent says how near its sessions are), and another present agent, in the roster's order,
 * has an account its next start would take (`scope.next`). The agent's pace warning alone is never near, so it offers nothing
 * and passes no account (USAGEREAD3b, D165 point 2). A conversation on the tool's own sign-in can be cut and is never near,
 * since a sign-in that says nothing is never offered as near. No driver read: the offer is a line, and taken only by the
 * person's press (D163, `ask`). It is the fallback (SWITCH1e, D165 point 4): where the switch moves the conversation to another
 * account of its own agent at its turn's end, there is no offer.
 *
 * @remarks
 * The scope is the conversation's workspace's own where it names one, else the machine's, as the start's chip reads it, for the
 * agent that owns the door's accounts (AGT7). A shell older than the facts answers none, and then there is no offer.
 */
export function handOverOffer({ session, workspace, doors, answer, now, actor }: {
  session: Pick<Session, 'adapter' | 'profile'>;
  /** The conversation's workspace, as the registry names it (`default` where it names none). */
  workspace: string;
  doors: readonly ToolDoor[];
  answer: AccountsAnswer | null | undefined;
  now: Date;
  /** The session's actor (Build's for a driven line, Chat's for a conversation) as `ACTORS` answers it; its order is the offer's. */
  actor?: ActorRow | null;
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
  if (switchesAway(session, doors, answer, scopeOf)) return null;

  // Candidates are the actor's entries on other tools (MODELROLE1e, D205 §3.5(e)), in its order; where the actor is as before they are
  // the roster's other tools (MIXAGENT1b-iii), each once by its first present door. A tool's other door is the same tool.
  for (const { door, model, from } of nextChoices(session, doors, actor)) {
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
    return {
      why, until: why === 'limit' ? until : null, agent: door.harness, account: taken,
      ...(from === 'roster' ? {} : { choice: { n: from, model } }),
    };
  }
  return null;
}

/**
 * Whether the switch moves the conversation to another account of its own agent at its turn's end, so the offer, the fallback,
 * stays down (SWITCH1e, D165 point 4, the switch design §4): its scope's switch is not `off`, its account is over the scope's curve
 * (`overCurve`, what `SwitchDecision` moves by), and another account of that scope has room (`room`). Each is .NET's verdict
 * (D182 point 6); a shell older than SWITCH1e answers no `room`, and the offer stands as before.
 */
function switchesAway(
  session: Pick<Session, 'adapter' | 'profile'>,
  doors: readonly ToolDoor[],
  answer: AccountsAnswer | null | undefined,
  scopeOf: (agent: NonNullable<ReturnType<typeof agentOf>>) => AccountScope,
): boolean {
  if (!session.profile) return false;
  const door = doors.find((each) => each.harness === session.adapter);
  const own = agentOf(answer, door ? toolOf(door) : session.adapter);
  if (!own) return false;
  const scope = scopeOf(own);
  return switchOf(scope.use) !== 'off'
    && (scope.overCurve ?? []).some((each) => each.account === session.profile)
    && (scope.room ?? []).some((account) => account !== session.profile);
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

/** One step of a hand-over's order: the door it goes to, the model the actor's entry asks of it, and the choice it is. */
export type NextChoice = { door: ToolDoor; model: string | null; from: string };

/**
 * The order a hand-over goes in (MODELROLE1e, D205 §3.5(e), round 4's Opus N1): every path takes the session's actor's effective
 * order, so it is the actor's entries whose agent is a present door on another tool than the session's own, each with its model and its
 * number. No maker outside the actor's entries is a step. Where the actor is as before, or has no entries, the roster's order stands
 * (`otherToolDoors`) with no model, its `from` being `roster`. A TWIN with the driver's `HandOverOrder.Steps`, both held to
 * `fixtures/hand-over-order.json`'s `actorRows`.
 */
export function nextChoices(
  session: Pick<Session, 'adapter'>,
  doors: readonly ToolDoor[],
  actor?: Pick<ActorRow, 'choices'> | null,
): NextChoice[] {
  const entries = actor?.choices ?? [];
  if (entries.length === 0 || entries.every((entry) => entry.from === 'before')) {
    return otherToolDoors(session, doors).map((door) => ({ door, model: null, from: 'roster' }));
  }
  const own = doors.find((each) => each.harness === session.adapter);
  const ownTool = own ? toolOf(own) : session.adapter;
  const found: NextChoice[] = [];
  for (const entry of entries) {
    const door = doors.find((each) => each.present && each.harness === entry.agent);
    if (!door || toolOf(door) === ownTool) continue;
    found.push({ door, model: entry.model ?? null, from: entry.from });
  }
  return found;
}

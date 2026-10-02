// The order in `harnesses.json` (TOOL4e, D125 §3.1): `rotation` and `workspaceRotation`, the accounts rotation may use
// per agent — the account's owner — in the person's order, for the machine and for one workspace:
//
//   { "rotation": { "claude-code": ["account-1", "account-2"] },
//     "workspaceRotation": { "work": { "claude-code": ["account-2", "account-3"] } } }
//
// It is the CLI's twin of the driver's `HarnessSettings` (`Harnesses.cs`: `ResolveRotationFrom`, `WithRotation`,
// `OrderProblem`, `WithoutAccount`), and `rotation.test.ts` holds the tables `RotationTwinTests.cs` holds, cell for cell.
// The rules both keep:
//
//   1. Read: each name trimmed; a blank, or a name that is not text, skipped; a name written twice, in any case, read
//      once where first written. A list that is not one, or names nobody, is none; so is a workspace naming none.
//   2. Resolved as a default is: the workspace's order for the agent, else the machine's, else none — and none is no
//      rotation at all, a cooling account holding its starts as it did before (D48 §2a).
//   3. Written only when set: an order replaced whole, cleared by naming nobody, and a workspace left with none dropped.
//      Agents and workspaces go out in order of name; each list in the person's order.
//   4. Refused at a door: a name that is no account here (the directories, compared exactly, as `profile default`
//      compares one) or one named twice in any case — the first problem in the order is the one said.
//
// Absence from the order is do-not-rotate (§3.1): one list, not a list and a mark. Nothing reads an order to choose an
// account until TOOL4f; this is the wiring and its editor.

import type { HarnessSettings } from './toolchain.ts';

/** Agent → the accounts rotation may use, in order. */
export type Orders = Record<string, string[]>;

/** One agent → order map, read by rule 1; anything that is not one is none. */
export function readOrders(value: unknown): Orders {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const orders: Orders = {};
  for (const [agent, list] of Object.entries(value as Record<string, unknown>)) {
    if (!Array.isArray(list)) continue;
    const order: string[] = [];
    for (const item of list) {
      const name = typeof item === 'string' ? item.trim() : '';
      if (name.length > 0 && !order.some((held) => held.toLowerCase() === name.toLowerCase())) order.push(name);
    }
    if (order.length > 0) orders[agent] = order;
  }
  return orders;
}

/** One workspace → agent → order map, each read by rule 1; a workspace naming none is none. */
export function readOrderCircles(value: unknown): Record<string, Orders> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const circles: Record<string, Orders> = {};
  for (const [workspace, orders] of Object.entries(value as Record<string, unknown>)) {
    const read = readOrders(orders);
    if (Object.keys(read).length > 0) circles[workspace] = read;
  }
  return circles;
}

/** An agent → order map as rule 3 writes it: agents in order of name, a list naming nobody left out. */
export function writtenOrders(orders: Orders): Orders {
  return Object.fromEntries(Object.entries(orders)
    .filter(([, order]) => order.length > 0)
    .sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** The workspaces' orders as rule 3 writes them: workspaces in order of name, one naming nobody left out. */
export function writtenOrderCircles(circles: Record<string, Orders>): Record<string, Orders> {
  return Object.fromEntries(Object.entries(circles)
    .map(([workspace, orders]) => [workspace, writtenOrders(orders)] as const)
    .filter(([, orders]) => Object.keys(orders).length > 0)
    .sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** Rule 2: the workspace's order for the agent, else the machine's, else none — saying which answered. */
export function resolveRotation(
  settings: HarnessSettings, agent: string, workspace?: string | null,
): { order: string[]; from: 'workspace' | 'machine' | 'unset' } {
  const own = workspace?.trim() ? settings.workspaceRotation[workspace.trim()]?.[agent] : undefined;
  if (own && own.length > 0) return { order: own, from: 'workspace' };
  const machine = settings.rotation[agent];
  return machine && machine.length > 0 ? { order: machine, from: 'machine' } : { order: [], from: 'unset' };
}

/**
 * Rule 3: an agent's order set, the machine's or one workspace's, replacing it whole; null or nobody clears it. The
 * names are kept trimmed; whether each is an account here, once, is `rotationProblem`'s question, asked first.
 */
export function withRotation(
  settings: HarnessSettings, agent: string, order: string[] | null, workspace?: string | null,
): HarnessSettings {
  const kept = (order ?? []).map((name) => name.trim()).filter((name) => name.length > 0);
  const set = (orders: Orders): Orders => {
    const next = { ...orders };
    if (kept.length > 0) next[agent] = kept;
    else delete next[agent];
    return next;
  };

  if (!workspace?.trim()) return { ...settings, rotation: set(settings.rotation) };

  const circle = set(settings.workspaceRotation[workspace.trim()] ?? {});
  const workspaceRotation = { ...settings.workspaceRotation, [workspace.trim()]: circle };
  if (Object.keys(circle).length === 0) delete workspaceRotation[workspace.trim()];
  return { ...settings, workspaceRotation };
}

/** Rule 4: the first name that is no account here, or the first named twice, or null when the order can be written. */
export function rotationProblem(accounts: string[], order: string[]): { account: string; twice: boolean } | null {
  const named = new Set<string>();
  for (const raw of order) {
    const name = raw.trim();
    if (!accounts.includes(name)) return { account: name, twice: false };
    if (named.has(name.toLowerCase())) return { account: name, twice: true };
    named.add(name.toLowerCase());
  }
  return null;
}

/** The refusal a person reads for a rule-4 problem: which account, and what is there to name instead. */
export function rotationRefusal(agent: string, problem: { account: string; twice: boolean }, accounts: string[]): string {
  return problem.twice
    ? `\`${problem.account}\` is named twice — an order names each account once, in the order rotation tries them.`
    : `\`${agent}\` has no account \`${problem.account}\` on this machine, so an order cannot name it — accounts there: `
      + `${accounts.length > 0 ? accounts.join(', ') : '(none)'}.`;
}

/**
 * The wiring with a removed account gone from it (D66 §3): no default names it, the machine's or a workspace's, and no
 * order does. The rest of each order keeps its place, and an order or a workspace left naming none goes. The driver's
 * `WithoutAccount` is the twin, which the screen's *Remove* is to use (TOOL4g).
 */
export function withoutAccount(settings: HarnessSettings, agent: string, profile: string): HarnessSettings {
  const defaults = { ...settings.defaults };
  if (defaults[agent] === profile) delete defaults[agent];

  const workspaces = Object.fromEntries(Object.entries(settings.workspaces).map(([workspace, map]) => {
    if (map[agent] !== profile) return [workspace, map];
    const copy = { ...map };
    delete copy[agent];
    return [workspace, copy];
  }).filter(([, map]) => Object.keys(map as Record<string, string>).length > 0));

  let next: HarnessSettings = { ...settings, defaults, workspaces };
  const machine = settings.rotation[agent];
  if (machine) next = withRotation(next, agent, machine.filter((name) => name !== profile));
  for (const [workspace, orders] of Object.entries(settings.workspaceRotation)) {
    const own = orders[agent];
    if (own) next = withRotation(next, agent, own.filter((name) => name !== profile), workspace);
  }
  return next;
}

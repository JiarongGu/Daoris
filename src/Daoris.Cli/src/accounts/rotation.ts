// The order in `harnesses.json` (TOOL4e, D125 §3.1): `rotation` and `workspaceRotation`, the accounts rotation may use
// per agent — the account's owner — in the person's order, for the machine and for one workspace:
//
//   { "rotation": { "claude-code": ["account-1", "account-2"] },
//     "workspaceRotation": { "work": { "claude-code": ["account-2", "account-3"] } } }
//
// It is the CLI's twin of the driver's `HarnessSettings` (`HarnessSettings.cs`: `ResolveRotationFrom`, `WithRotation`,
// `OrderProblem`, `WithoutAccount`), and `rotation.test.ts` holds the tables `RotationTwinTests.cs` holds, cell for cell.
// The rules both keep:
//
//   1. Read: each name trimmed; a blank, or a name that is not text, skipped; a name written twice, in any case, read
//      once where first written. A list that is not one, or names nobody, is none; so is a workspace naming none. A
//      workspace is found, edited and read in any case, as the driver's dictionaries hold one: written twice, it is one,
//      spelled as first written and holding the later's (CASEFOLD1c). "In any case" here and below is the driver's
//      `OrdinalIgnoreCase` (`casefold.ts`, CASEFOLD1): `straße` is not `STRASSE`.
//   2. Resolved as a default is: the workspace's order for the agent, else the machine's, else none — and none is no
//      rotation at all, a cooling account holding its starts as it did before (D48 §2a).
//   3. Written only when set: an order replaced whole, cleared by naming nobody, and a workspace left with none dropped.
//      Agents and workspaces go out in order of name; each list in the person's order.
//   4. Refused at a door: a name that is no account here (the directories, compared exactly, as `profile default`
//      compares one) or one named twice in any case — the first problem in the order is the one said.
//
// Absence from the order is do-not-rotate (§3.1): one list, not a list and a mark. TOOL4f's walk reads it.
//
// How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14 as §16.6 amends them; D165 point 2 and the switch
// design §2 and §6, SWITCH1a) sits beside it: `rotationUse` for the machine and `workspaceRotationUse` for one workspace,
// per agent — *use accounts* (`use`: `goal`, make the most of them, or `order`, one by one in order), *keep for
// conversations* (`keep`), and when to switch (`switch`: `smart`, Daoris's curve, `curve`, the person's own, or `off`;
// and `curve`, the person's points per window):
//
//   { "rotationUse": { "claude-code": { "switch": "curve", "curve": { "weekly": [[0, 30], [50, 70], [100, 100]] } } },
//     "workspaceRotationUse": { "work": { "claude-code": { "use": "order", "switch": "off" } } } }
//
// The driver's twin is `RotationUse.cs` (`RotationUse`, `UseCurve`, `UseEntry`, `ScopeProblem`) with `HarnessSettings.cs`
// (`ResolveScope`, `WithUse`), and both hold the table in `test/fixtures/rotation-use.json`, row for row. The rules both keep:
//
//   5. Read: an entry is an object, kept whole but for `prefer` and `parallel`, which §16.6 retired and nothing released
//      wrote: skipped, and gone at the next write. Each other field is a value this build knows — `use` one of
//      `USE_MODES`, `keep` a name (trimmed), `switch` one of `SWITCHES`, `curve` one `curveProblem` passes — or one it
//      does not, which reads as today's default and is SAID; a field this build has no name for is kept and said too, so a
//      newer build's setting outlives this one's save (twins: an editor keeps what it has no field for).
//   6. The old names read on (D165 §6): `early` true or false and `near` a whole percent from 50 to 99, which only an
//      older build writes, are taken out and applied as a change would apply them (`UseChange`), over the entry's own
//      switch and curve: `early: false` is `switch: off`, and `near: N` other than 90 the session window flat at N with
//      `switch: curve` unless it is off. A value of theirs that does not read is said, and goes.
//   7. Resolved as one scope (§2 rule 1): the workspace's, when it names a default or a list of its own for the agent,
//      else the machine's. A scope's settings are read only with its list; with none it is its default alone. A kept
//      account not in the list is none, and a default outside it leaves the list to win: it begins at its first.
//   8. Written as chosen: a choice equal to today's default is still written, so a later default never overturns what a
//      person chose; absence alone means today's default (`USE_DEFAULTS`, the one place they live). Known fields go out
//      in one order, a value they know normalised (a curve's windows in `CURVE_WINDOWS`' order, a curve naming none not
//      at all), then whatever this build does not know as it was read; `early` and `near` never. A list cleared takes its
//      scope's settings with it; an account removed is kept nowhere.
//   9. Refused at a door (§3.1, §4.6): a scope's default and its kept account are of its list, and a kept account leaves
//      driven work another — the first problem said. Nothing counts accounts: no list is refused for its length. A curve
//      that breaks its rules is refused in `curveProblem`'s sentence, the driver's word for word.
//
// The driver's walk reads them (TOOL6b, TOOL6c): `use` and `keep` choose a start's account, and since SWITCH1b-ii the switch
// and the curve pass an account over its curve, where the agent's door carries its windows or its own server is asked them
// (`windows.json`, the toolchain's `windows`; CODEXUSE1). The curve is measured by `usagecurves.ts`, the driver's
// `UsageCurves`' twin, which `profile use`'s said line reads (SWITCH1b-iii). `early` and `near` stay derived, as the
// driver's `RotationUse` keeps them for the doors that still speak them, and nothing here reads them.

import { atName, byName, findName, foldName } from '../platform/casefold.ts';
import { shellWord } from '../platform/shellword.ts';
import type { HarnessSettings } from '../agents/toolchain.ts';

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
      if (name.length > 0 && findName(order, name) === null) order.push(name);
    }
    if (order.length > 0) orders[agent] = order;
  }
  return orders;
}

/**
 * One workspace → agent → order map, each read by rule 1; a workspace naming none is none, and so replaces nothing where its
 * name was written before in another case (the driver's `ReadOrderCircles` sets only a workspace that names one).
 */
export function readOrderCircles(value: unknown): Record<string, Orders> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  return byName(Object.entries(value as Record<string, unknown>)
    .map(([workspace, orders]) => [workspace, readOrders(orders)] as const)
    .filter(([, read]) => Object.keys(read).length > 0));
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
  const own = workspace?.trim() ? atName(settings.workspaceRotation, workspace.trim())?.[agent] : undefined;
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
  // Rule 8 (TOOL6a): a list's settings come with it, so a list cleared takes them too.
  const cleared = (next: HarnessSettings) => (kept.length > 0 ? next : withUse(next, agent, null, workspace));

  if (!workspace?.trim()) return cleared({ ...settings, rotation: set(settings.rotation) });

  // CASEFOLD1c: under the spelling the workspace was first written in, as the driver's dictionary keeps it.
  const key = findName(Object.keys(settings.workspaceRotation), workspace.trim()) ?? workspace.trim();
  const circle = set(settings.workspaceRotation[key] ?? {});
  const workspaceRotation = { ...settings.workspaceRotation, [key]: circle };
  if (Object.keys(circle).length === 0) delete workspaceRotation[key];
  return cleared({ ...settings, workspaceRotation });
}

/** Rule 4: the first name that is no account here, or the first named twice, or null when the order can be written. */
export function rotationProblem(accounts: string[], order: string[]): { account: string; twice: boolean } | null {
  const named = new Set<string>();
  for (const raw of order) {
    const name = raw.trim();
    if (!accounts.includes(name)) return { account: name, twice: false };
    if (named.has(foldName(name))) return { account: name, twice: true };
    named.add(foldName(name));
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
 * The wiring with a removed account gone from it (D66 §3): no default names it, the machine's or a workspace's, no order
 * does, and no scope keeps it (TOOL6a). The rest of each order keeps its place, and an order or a workspace left naming
 * none goes, its settings with it. The driver's `WithoutAccount` is the twin, which the screen's *Remove* is to use
 * (TOOL4g).
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

  const keeps = (entry: UseEntry | undefined) => typeof entry?.keep === 'string' && entry.keep.trim() === profile;
  if (keeps(next.rotationUse[agent])) next = withUse(next, agent, { keep: null });
  for (const [workspace, uses] of Object.entries(next.workspaceRotationUse)) {
    if (keeps(uses[agent])) next = withUse(next, agent, { keep: null }, workspace);
  }
  return next;
}

// ——— An account put into a scope's list, and where each account runs (ACCT1, D125's ACCT1 note; D130 §3.1). The driver's
// twin is `HarnessSettings.cs` (`JoinProblemOf`, `WithJoined`, `PlacesOf`), and `account-join.test.ts` holds the tables
// `AccountJoinTwinTests.cs` holds, cell for cell. Accounts compare exactly, as the wiring compares a name with a folder.

/** One scope an account runs in: a workspace, or null for this machine; whether its own list holds it, and its own default names it. */
export interface AccountPlace {
  workspace: string | null;
  list: boolean;
  default: boolean;
}

/**
 * The workspace an account cannot join, or null where it can: a workspace that names no default and no list of its own for
 * the agent takes this machine's scope (rule 7), so its list is this machine's, and a list of its own would move its starts
 * off every account the machine's holds. Null is this machine's list, which any account may join.
 */
export function joinProblem(settings: HarnessSettings, agent: string, workspace: string | null): { workspace: string } | null {
  const circle = workspace?.trim();
  return circle && resolveScope(settings, agent, circle).from !== 'workspace' ? { workspace: circle } : null;
}

/** The refusal a person reads for a workspace that takes this machine's list, as the driver's `JoinProblem.Sentence` says it. */
export function joinRefusal(agent: string, workspace: string): string {
  return `\`${workspace}\` names no \`${agent}\` account or list of its own, so its starts take this machine's list — join this `
    + `machine's list, or give \`${workspace}\` a list of its own first (\`daoris agent profile order ${agent} <account>… `
    + `--workspace ${shellWord(workspace, '<workspace>')}\`).`;
}

/**
 * The wiring with `account` in the scope's own list: appended where the list lacks it, a list begun at the scope's default
 * where it has none, or of the account alone where the scope names nobody. The list's settings stay. Whether the scope may
 * be joined is `joinProblem`'s question, which a door asks first.
 */
export function withJoined(settings: HarnessSettings, agent: string, account: string, workspace: string | null): HarnessSettings {
  const scope = resolveScope(settings, agent, workspace);
  const list = scope.list.length > 0
    ? (scope.list.includes(account) ? scope.list : [...scope.list, account])
    : (scope.default !== null && scope.default !== account ? [scope.default, account] : [account]);
  return withRotation(settings, agent, list, workspace?.trim() || null);
}

/**
 * Where `account` runs: each scope whose own list holds it or whose own default names it, this machine first and then each
 * workspace by name. None is an account no start runs on, which both doors say.
 */
export function placesOf(settings: HarnessSettings, agent: string, account: string): AccountPlace[] {
  const places: AccountPlace[] = [];
  const add = (workspace: string | null, list: string[] | undefined, named: string | undefined) => {
    const listed = list?.includes(account) === true;
    const defaulted = named?.trim() === account;
    if (listed || defaulted) places.push({ workspace, list: listed, default: defaulted });
  };

  add(null, settings.rotation[agent], settings.defaults[agent]);
  const seen = new Set<string>();
  const workspaces = [...Object.keys(settings.workspaces), ...Object.keys(settings.workspaceRotation)]
    .filter((workspace) => !seen.has(foldName(workspace)) && seen.add(foldName(workspace)))
    .sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
  for (const workspace of workspaces) {
    add(workspace, atName(settings.workspaceRotation, workspace)?.[agent], atName(settings.workspaces, workspace)?.[agent]);
  }
  return places;
}

// ——— How a scope's list is used (TOOL6a; D130 §2, §14, §16.6; D165 §6): rules 5 to 9 above.

/**
 * *Use accounts*' choices (§16.6): `goal`, make the most of them (§16.3, the driver's walk), and `order`, one by one in
 * the list's order (D125's walk). A new choice is a row here, and its words in the terminal's `USE_WORDS` (`toolchain.ts`).
 */
export const USE_MODES = ['goal', 'order'] as const;
export type UseMode = (typeof USE_MODES)[number];

/**
 * When to switch (D165 point 2): `smart`, Daoris's curve; `curve`, the person's own points; `off`, no switch while a
 * session runs. The driver's `RotationUse.Switches`.
 */
export const SWITCHES = ['smart', 'curve', 'off'] as const;
export type Switch = (typeof SWITCHES)[number];

/** A curve's windows, as `windows.json` names them (the switch design §5): the five-hour window and the week. */
export const CURVE_WINDOWS = ['session', 'weekly'] as const;
export type CurveWindow = (typeof CURVE_WINDOWS)[number];

/** A window's curve has one point to this many (the switch design §6). The driver's `UseCurve.MostPoints`. */
export const CURVE_MOST_POINTS = 12;

/** A point of a curve: how much of the window's time has passed, and the most of it an account should have used by then. */
export type CurvePoint = [elapsed: number, used: number];

/**
 * The person's curve (D165 point 2): each window it names, its points joined by straight lines; a window left out takes
 * the smart curve's. Kept whatever `switch` says, and read only under `curve`.
 */
export type Curve = Partial<Record<CurveWindow, CurvePoint[]>>;

/**
 * The smart curve's five-hour window, flat (D165 point 2; D130 §6's 90): what *near* meant where none was set, and the
 * old name's value that reads as no curve. The driver's `RotationUse.SmartSession`.
 */
export const SMART_SESSION = 90;

/** How one scope's list is used, each setting's value as a start reads it. */
export interface RotationUse {
  use: UseMode;
  /** The account kept for conversations, or none. */
  keep: string | null;
  /** When to switch. */
  switch: Switch;
  /** The person's curve, its windows in `CURVE_WINDOWS`' order; none is `{}`. */
  curve: Curve;
  /**
   * Derived, as the old name reads (D165 §6): *switch before the limit*, the switch not `off`. The walk reads the switch
   * since SWITCH1b-ii; the twin table keeps this for the doors that still speak it.
   */
  early: boolean;
  /**
   * Derived, as the old name reads: the session window's one point under `curve`, else the smart curve's 90. The walk reads
   * the curve since SWITCH1b-ii (`usagecurves.ts`); the twin table keeps this for the doors that still speak it.
   */
  near: number;
}

/** The settings as chosen, the old names' two derived beside them (D165 §6). */
function useOf(chosen: Pick<RotationUse, UseField>): RotationUse {
  const session = chosen.curve.session;
  return {
    ...chosen,
    early: chosen.switch !== 'off',
    near: chosen.switch === 'curve' && session?.length === 1 ? session[0]![1] : SMART_SESSION,
  };
}

/** Today's defaults (D130 §16.6, D165 point 2), and the one place they live: what absence means for each setting. */
export const USE_DEFAULTS: Readonly<RotationUse> = Object.freeze(
  useOf({ use: 'goal', keep: null, switch: 'smart', curve: Object.freeze({}) }));

/** *Near*'s range (§14), which the old name `--near` still takes: a whole percent from the one to the other. */
export const NEAR_LOWEST = 50;
export const NEAR_HIGHEST = 99;

/** One scope's settings as the file holds them: by name, in the order read, a value this build knows or not. */
export type UseEntry = Record<string, unknown>;

/** Agent → its settings. */
export type Uses = Record<string, UseEntry>;

/** The settings this build knows, in the order they are written. */
export const USE_FIELDS = ['use', 'keep', 'switch', 'curve'] as const;
type UseField = (typeof USE_FIELDS)[number];

/** The old names (D130 §16.6), which D165 §6 replaced: read on (rule 6), and never written. */
export const OLD_FIELDS = ['early', 'near'] as const;
type OldField = (typeof OLD_FIELDS)[number];

/**
 * The settings §16.6 retired, which nothing released wrote: read as nothing and said as nothing, and gone at the next write, which
 * is measured from the entry as read (KEEPALL1b).
 */
export const RETIRED_FIELDS: readonly string[] = ['prefer', 'parallel'];

/** Each setting's reading (rule 5): its value, or `undefined` where this build does not know the value. */
const READ_FIELD: { [F in UseField]: (raw: unknown) => RotationUse[F] | undefined } = {
  use: (raw) => (typeof raw === 'string' && (USE_MODES as readonly string[]).includes(raw) ? raw as UseMode : undefined),
  keep: (raw) => (typeof raw === 'string' && raw.trim().length > 0 ? raw.trim() : undefined),
  switch: (raw) => (typeof raw === 'string' && (SWITCHES as readonly string[]).includes(raw) ? raw as Switch : undefined),
  curve: (raw) => readCurve(raw),
};

/** Each old name's reading (rule 6): its value, or `undefined` where it does not read. */
const READ_OLD: { [F in OldField]: (raw: unknown) => UseChange[F] } = {
  early: (raw) => (typeof raw === 'boolean' ? raw : undefined),
  near: (raw) => (typeof raw === 'number' && Number.isInteger(raw) && raw >= NEAR_LOWEST && raw <= NEAR_HIGHEST ? raw : undefined),
};

function isField(name: string): name is UseField {
  return (USE_FIELDS as readonly string[]).includes(name);
}

function isOld(name: string): name is OldField {
  return (OLD_FIELDS as readonly string[]).includes(name);
}

/**
 * Rule 9's curve: the first problem with a curve a door is handed, in the sentence both twins say (the shared table's
 * `refusal` rows, the driver's `UseCurve.Problem`), or null. Window by window in the order written, then point by point:
 * its shape, elapsed from 0 to 100, used from 0 to 100, elapsed rising, used never falling.
 */
export function curveProblem(curve: unknown): string | null {
  if (!isObject(curve)) return 'A curve is an object of windows, `session` or `weekly`, each a list of rows [elapsed, used].';
  for (const [window, points] of Object.entries(curve)) {
    if (!(CURVE_WINDOWS as readonly string[]).includes(window)) {
      return `\`${window}\` is not a window — a curve's windows are \`session\` and \`weekly\`.`;
    }
    if (!Array.isArray(points)) return `\`${window}\` is not a list of rows [elapsed, used].`;
    if (points.length < 1 || points.length > CURVE_MOST_POINTS) {
      return `\`${window}\` has ${points.length} rows — a window's curve has one to twelve.`;
    }
    let before: CurvePoint | null = null;
    for (const [index, point] of points.entries()) {
      const at = index + 1;
      if (!Array.isArray(point) || point.length !== 2 || !point.every((n) => typeof n === 'number' && Number.isInteger(n))) {
        return `\`${window}\`'s row ${at} is not two whole numbers [elapsed, used].`;
      }
      const [elapsed, used] = point as CurvePoint;
      if (elapsed < 0 || elapsed > 100) return `\`${window}\`'s row ${at} is at ${elapsed}% of the window's time — elapsed is from 0 to 100.`;
      if (used < 0 || used > 100) return `\`${window}\`'s row ${at} has used ${used}% — used is from 0 to 100.`;
      if (before !== null && elapsed <= before[0]) {
        return `\`${window}\`'s row ${at} is at ${elapsed}% of the window's time, not after row ${at - 1}'s ${before[0]}% — `
          + 'elapsed rises from row to row.';
      }
      if (before !== null && used < before[1]) {
        return `\`${window}\`'s row ${at} has used ${used}%, less than row ${at - 1}'s ${before[1]}% — used never falls.`;
      }
      before = [elapsed, used];
    }
  }
  return null;
}

/** A curve as read: its windows in `CURVE_WINDOWS`' order, or `undefined` where it breaks a rule (`curveProblem`). */
export function readCurve(raw: unknown): Curve | undefined {
  if (curveProblem(raw) !== null) return undefined;
  const windows = raw as Record<string, CurvePoint[]>;
  const curve: Curve = {};
  for (const window of CURVE_WINDOWS) {
    if (Object.hasOwn(windows, window)) curve[window] = windows[window]!.map(([elapsed, used]) => [elapsed, used]);
  }
  return curve;
}

/** Rule 6: the old names taken out, and what reads of them applied as a change applies them. */
function foldedUse(entry: UseEntry): UseEntry {
  if (!OLD_FIELDS.some((name) => Object.hasOwn(entry, name))) return entry;
  const { early, near, ...rest } = entry;
  const change: UseChange = {};
  const flag = READ_OLD.early(early);
  const percent = READ_OLD.near(near);
  if (flag !== undefined) change.early = flag;
  if (percent !== undefined) change.near = percent;
  return appliedUse(rest, change);
}

/** Rules 5 and 6: an entry's settings, today's default where it says nothing or what this build does not know, and those named. */
export function readUse(entry: UseEntry | undefined): { use: RotationUse; unknown: string[] } {
  const chosen: Pick<RotationUse, UseField> = { use: USE_DEFAULTS.use, keep: USE_DEFAULTS.keep, switch: USE_DEFAULTS.switch, curve: {} };
  const unknown: string[] = [];
  const folded = entry ? foldedUse(entry) : {};
  for (const field of USE_FIELDS) {
    if (!Object.hasOwn(folded, field)) continue;
    const value = READ_FIELD[field](folded[field]);
    if (value === undefined) unknown.push(field);
    else (chosen as Record<string, unknown>)[field] = value;
  }
  for (const name of OLD_FIELDS) {
    if (entry && Object.hasOwn(entry, name) && READ_OLD[name](entry[name]) === undefined) unknown.push(name);
  }
  for (const name of Object.keys(entry ?? {})) if (!isField(name) && !isOld(name) && !RETIRED_FIELDS.includes(name)) unknown.push(name);
  return { use: useOf(chosen), unknown };
}

/**
 * One agent → settings map, each entry an object kept whole, the retired settings too, so a save's edit takes them out (KEEPALL1b);
 * anything else, or an entry naming nothing, is none.
 */
export function readUses(value: unknown): Uses {
  if (!isObject(value)) return {};
  return Object.fromEntries(Object.entries(value)
    .filter((entry): entry is [string, UseEntry] => isObject(entry[1]))
    .map(([agent, entry]) => [agent, { ...entry }] as const)
    .filter(([, entry]) => Object.keys(entry).length > 0));
}

/** One workspace → agent → settings map; a workspace naming none is none, and replaces nothing, as with the orders. */
export function readUseCircles(value: unknown): Record<string, Uses> {
  if (!isObject(value)) return {};
  return byName(Object.entries(value)
    .map(([workspace, uses]) => [workspace, readUses(uses)] as const)
    .filter(([, uses]) => Object.keys(uses).length > 0));
}

/**
 * Rule 8: an entry as written — the old names read on, the settings this build knows in their order, normalised (a curve
 * naming no window not at all), then the rest as read.
 */
export function writtenUse(entry: UseEntry): UseEntry {
  const folded = foldedUse(entry);
  const out: UseEntry = {};
  for (const field of USE_FIELDS) {
    if (!Object.hasOwn(folded, field)) continue;
    const value = READ_FIELD[field](folded[field]);
    if (value === undefined) out[field] = folded[field];
    else if (field !== 'curve' || Object.keys(value as Curve).length > 0) out[field] = value;
  }
  for (const [name, value] of Object.entries(folded)) if (!isField(name) && !RETIRED_FIELDS.includes(name)) out[name] = value;
  return out;
}

/** An agent → settings map as written: agents in order of name, an entry written as naming nothing left out. */
export function writtenUses(uses: Uses): Record<string, UseEntry> {
  return Object.fromEntries(Object.entries(uses)
    .map(([agent, entry]) => [agent, writtenUse(entry)] as const)
    .filter(([, entry]) => Object.keys(entry).length > 0)
    .sort(([a], [b]) => (a < b ? -1 : 1)));
}

/**
 * An agent → settings map as the file held it, for the text a save's edit is measured from (KEEPALL1b): each value as read, the
 * settings this build knows in their written order, then every other field in the order read, the old and retired names among them,
 * so an entry the writer would write alike measures as no edit, and its old and retired names as ones the edit takes out. Agents in
 * order of name, as `writtenUses`; the driver's `UseEntry.WriteAsRead`.
 */
export function usesAsRead(uses: Uses): Record<string, UseEntry> {
  return Object.fromEntries(Object.entries(uses)
    .filter(([, entry]) => Object.keys(entry).length > 0)
    .map(([agent, entry]) => [agent, {
      ...Object.fromEntries(USE_FIELDS.filter((field) => Object.hasOwn(entry, field)).map((field) => [field, entry[field]])),
      ...Object.fromEntries(Object.entries(entry).filter(([name]) => !isField(name))),
    }] as const)
    .sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** The workspaces' settings as written: workspaces in order of name, one naming nobody left out. */
export function writtenUseCircles(circles: Record<string, Uses>): Record<string, Record<string, UseEntry>> {
  return Object.fromEntries(Object.entries(circles)
    .map(([workspace, uses]) => [workspace, writtenUses(uses)] as const)
    .filter(([, uses]) => Object.keys(uses).length > 0)
    .sort(([a], [b]) => (a < b ? -1 : 1)));
}

/**
 * A change to one scope's settings: a field absent is left as it was, `keep: null` keeps none, `curve: null` takes the
 * curve out, and `curve` sets each window it names, keeping the others. Each value is written as given — whether it is a
 * setting at all is the door's question, asked first. `early` and `near` are the old names (D165 §6), which a door may
 * still take: `early` false is `switch: off`, and true is `smart` where it was off; `near` N other than 90 is the session
 * window flat at N, and `switch: curve` unless it is off, and 90 takes the session window back to the smart curve's. They
 * are applied before `switch` and `curve`, so a new name beside an old one is the one written.
 */
export interface UseChange {
  use?: UseMode;
  keep?: string | null;
  switch?: Switch;
  curve?: Curve | null;
  early?: boolean;
  near?: number;
}

/** One window of an entry's curve set, or taken out where `points` is none; a curve left naming none is taken out. */
function setWindow(entry: UseEntry, window: string, points: CurvePoint[] | null | undefined): void {
  const curve: Record<string, unknown> = isObject(entry.curve) ? { ...entry.curve } : {};
  if (points) curve[window] = points.map(([elapsed, used]) => [elapsed, used]);
  else delete curve[window];
  if (Object.keys(curve).length > 0) entry.curve = curve;
  else delete entry.curve;
}

/** A change applied to an entry whose old names are read on already: the old names first, then the new. */
function appliedUse(entry: UseEntry, change: UseChange): UseEntry {
  const next: UseEntry = { ...entry };
  if (change.use !== undefined) next.use = change.use;
  if (change.keep === null) delete next.keep;
  else if (change.keep !== undefined && change.keep.trim().length > 0) next.keep = change.keep.trim();

  const off = () => READ_FIELD.switch(next.switch) === 'off';
  if (change.early === false) next.switch = 'off';
  else if (change.early === true && off()) next.switch = 'smart';
  if (change.near !== undefined && change.near !== SMART_SESSION) {
    setWindow(next, 'session', [[0, change.near]]);
    if (!off()) next.switch = 'curve';
  } else if (change.near === SMART_SESSION) {
    setWindow(next, 'session', null);
  }

  if (change.switch !== undefined) next.switch = change.switch;
  if (change.curve === null) delete next.curve;
  else if (change.curve !== undefined) {
    for (const window of CURVE_WINDOWS) if (Object.hasOwn(change.curve, window)) setWindow(next, window, change.curve[window]);
  }
  return next;
}

/**
 * Rule 8: an agent's settings changed, the machine's or one workspace's; `null` clears them, and a scope or a workspace
 * left with none is dropped. What this build does not know is kept, unless the change sets that field; the old names are
 * read on first, so a change is made over what they meant.
 */
export function withUse(
  settings: HarnessSettings, agent: string, change: UseChange | null, workspace?: string | null,
): HarnessSettings {
  const edit = (entry: UseEntry | undefined): UseEntry => (change === null ? {} : appliedUse(foldedUse(entry ?? {}), change));
  const place = (uses: Uses): Uses => {
    const next = { ...uses };
    const entry = edit(uses[agent]);
    if (Object.keys(entry).length > 0) next[agent] = entry;
    else delete next[agent];
    return next;
  };

  if (!workspace?.trim()) return { ...settings, rotationUse: place(settings.rotationUse) };

  const key = findName(Object.keys(settings.workspaceRotationUse), workspace.trim()) ?? workspace.trim();
  const circle = place(settings.workspaceRotationUse[key] ?? {});
  const workspaceRotationUse = { ...settings.workspaceRotationUse, [key]: circle };
  if (Object.keys(circle).length === 0) delete workspaceRotationUse[key];
  return { ...settings, workspaceRotationUse };
}

/** The scope a start reads (rule 7): whose it is, its own default, its list, where it begins, and how it is used. */
export interface Scope {
  from: 'workspace' | 'machine';
  /** The scope's own default, trimmed, or none. */
  default: string | null;
  list: string[];
  /** Its default where its list holds it, else its list's first; with no list, its default alone. */
  begins: string | null;
  use: RotationUse;
  /** The settings it holds that this build does not know, by name: known ones first, in their order. */
  unknown: string[];
}

/** Rule 7: the workspace's scope when it names a default or a list of its own for the agent, else the machine's. */
export function resolveScope(settings: HarnessSettings, agent: string, workspace?: string | null): Scope {
  const circle = workspace?.trim();
  if (circle) {
    const own = atName(settings.workspaces, circle)?.[agent]?.trim() || null;
    const list = atName(settings.workspaceRotation, circle)?.[agent] ?? [];
    if (own !== null || list.length > 0) {
      return scopeOf('workspace', own, list, atName(settings.workspaceRotationUse, circle)?.[agent]);
    }
  }
  return scopeOf('machine', settings.defaults[agent]?.trim() || null, settings.rotation[agent] ?? [], settings.rotationUse[agent]);
}

function scopeOf(from: Scope['from'], own: string | null, list: string[], entry: UseEntry | undefined): Scope {
  if (list.length === 0) return { from, default: own, list: [], begins: own, use: { ...USE_DEFAULTS }, unknown: [] };
  const { use, unknown } = readUse(entry);
  if (use.keep !== null && !list.includes(use.keep)) use.keep = null;
  return { from, default: own, list, begins: own !== null && list.includes(own) ? own : list[0]!, use, unknown };
}

/** What binds a scope (rule 9): its default outside its list, its kept account outside it, or kept and alone in it. */
export interface ScopeProblem {
  kind: 'default' | 'keep' | 'alone';
  account: string;
}

/**
 * Rule 9: the first problem with a scope's default, list and kept account, or null. Names compare exactly, as a door
 * compares a name with its directory; a scope with no list is its default alone, so only a kept account is refused there.
 */
export function scopeProblem(scope: { default: string | null; list: string[]; keep: string | null }): ScopeProblem | null {
  const own = scope.default?.trim();
  if (own && scope.list.length > 0 && !scope.list.includes(own)) return { kind: 'default', account: own };
  const keep = scope.keep?.trim();
  if (keep) {
    if (!scope.list.includes(keep)) return { kind: 'keep', account: keep };
    if (!scope.list.some((name) => name !== keep)) return { kind: 'alone', account: keep };
  }
  return null;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

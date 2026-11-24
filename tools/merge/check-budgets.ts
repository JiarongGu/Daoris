/**
 * What this machine has learned of a repository's gates, kept in the Daoris home (`check-budgets`), never in git: each gate's
 * green wall times, a timeout derived from them, a pin the owner set over it, and the load settings tuned to this machine (`memoryGb`,
 * `alone`). The product's numbers are here: the factor, the floor, how many greens teach and how many are kept, and the bound a rise of the
 * learned timeout is said past. With no home, or nothing recorded for a gate, the lane's default budget applies (`timeout.ts`).
 *
 * The file is `{ schema: 1, repositories: { <name>: { gates: { <gate>: GateKept }, sets, treeBaseline, … } } }`. The driver's door
 * (`daoris settings checks`, Setup) writes the same file in C# (`CheckBudgets`), and a shared fixture holds the two to one derivation.
 * Each write reads the file again first and replaces it whole through `writeAtomic`, so the tools' greens and the door's values are neither lost.
 */
import { existsSync, readFileSync } from 'node:fs';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';
import { writeAtomic } from '../fsx.mjs';

/** The product's derivation: the timeout is the slowest ninetieth percentile of the recent greens times the factor, rounded up to a whole minute, never under the floor. */
export const BUDGET_FACTOR = 1.5;
export const BUDGET_FLOOR_MINUTES = 5;
/** Greens kept per gate (the newest), and how many teach a timeout at all. */
export const GREENS_KEPT = 20;
export const GREENS_NEEDED = 3;
/** A learned timeout that rises past this many times the one last said is said again, once. */
export const RISE_BOUND = 1.25;

const MERGE_DEFAULT_MINUTES = 5;
const FULL_DEFAULT_MINUTES = 10;

export type Green = { ms: number; at: string };
export type Pin = { minutes: number; why: string };

/** One gate's record: what the machine learned, and what its door set. */
export type GateKept = {
  green?: Green[];
  pin?: Pin;
  memoryGb?: number;
  alone?: boolean;
  /** The learned timeout last said, or the first one learned: a rise is judged against it. */
  said?: number;
};

/** One repository's record in the home. */
export type RepositoryKept = {
  gates?: Record<string, GateKept>;
  /** A test set's per-test timeout in milliseconds, by `<gate>/<set>`. */
  sets?: Record<string, { timeoutMs: number }>;
  treeBaseline?: string;
  devkit?: string;
  localInputs?: string[];
  patternsFile?: string;
};

export type Budgets = { schema: 1; repositories: Record<string, RepositoryKept> };

/** The home's file; throws where no home is named. */
export const budgetsFile = (home: string): string => homeEntry(home, 'check-budgets');

/** The record, empty where there is no home or no file; a file that does not read is refused by its name, never read as empty. */
export function readBudgets(home: string | null | undefined): Budgets {
  const empty: Budgets = { schema: 1, repositories: {} };
  if (!home) return empty;
  const file = budgetsFile(home);
  if (!existsSync(file)) return empty;
  let parsed: unknown;
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Error(`${file} does not read (${(error as Error).message}): the machine's check budgets are not used until it does`);
  }
  const repositories = (parsed as { repositories?: unknown } | null)?.repositories;
  if (!repositories || typeof repositories !== 'object' || Array.isArray(repositories)) throw new Error(`${file} has no repositories object: the machine's check budgets are not used until it does`);
  return { schema: 1, repositories: repositories as Record<string, RepositoryKept> };
}

const positive = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value) && value > 0;

/** The nearest-rank ninetieth percentile, in milliseconds, of the newest greens; null where fewer than `GREENS_NEEDED` are kept. */
function slowestOf(greens: readonly Green[] | undefined): number | null {
  const recent = (greens ?? []).filter((each) => positive(each?.ms)).slice(-GREENS_KEPT).map((each) => each.ms).sort((a, b) => a - b);
  if (recent.length < GREENS_NEEDED) return null;
  return recent[Math.ceil(0.9 * recent.length) - 1]!;
}

export type Budget = { minutes: number; source: 'default' | 'learned' | 'pin'; why?: string; rise?: { from: number; to: number } };

/**
 * A gate's budget in minutes: its pin, else what its green times teach, else the lane's default (`merge` is five minutes, any other lane ten).
 * A learned value that stands past `RISE_BOUND` times the one last said carries the `rise`, which the caller says and then marks (`saidRise`);
 * a gate with nothing said yet rises from nothing.
 */
export function budgetOf(kept: GateKept | undefined, lane: string | null): Budget {
  if (kept?.pin && positive(kept.pin.minutes)) return { minutes: kept.pin.minutes, source: 'pin', why: kept.pin.why };
  const slowest = slowestOf(kept?.green);
  if (slowest === null) return { minutes: lane === null || lane === 'merge' ? MERGE_DEFAULT_MINUTES : FULL_DEFAULT_MINUTES, source: 'default' };
  const minutes = Math.max(BUDGET_FLOOR_MINUTES, Math.ceil((slowest * BUDGET_FACTOR) / 60_000));
  const said = kept?.said;
  return positive(said) && minutes > said * RISE_BOUND ? { minutes, source: 'learned', rise: { from: said, to: minutes } } : { minutes, source: 'learned' };
}

/** One write: the file read again, `change` applied to the repository's record, the file replaced whole. Writes nothing where there is no home. */
function write(home: string | null | undefined, repository: string, change: (kept: RepositoryKept) => void): void {
  if (!home) return;
  const budgets = readBudgets(home);
  const kept = (budgets.repositories[repository] ??= {});
  change(kept);
  writeAtomic(budgetsFile(home), `${JSON.stringify(budgets, null, 2)}\n`);
}

const gateOf = (kept: RepositoryKept, gate: string): GateKept => ((kept.gates ??= {})[gate] ??= {});

/**
 * A green wall time recorded for a gate that ran whole and replayed nothing. The first learning (the third green) sets what is said, silently,
 * so a rise is judged from there.
 */
export function recordGreen(home: string | null | undefined, repository: string, gate: string, ms: number, at: string): void {
  if (!positive(ms)) return;
  write(home, repository, (kept) => {
    const each = gateOf(kept, gate);
    each.green = [...(each.green ?? []), { ms, at }].slice(-GREENS_KEPT);
    const learned = budgetOf({ green: each.green }, 'merge');
    if (each.said === undefined && learned.source === 'learned') each.said = learned.minutes;
  });
}

/** A rise said: the value told is kept, so it is not told again until the timeout rises past the bound once more. */
export function saidRise(home: string | null | undefined, repository: string, gate: string, minutes: number): void {
  write(home, repository, (kept) => { gateOf(kept, gate).said = minutes; });
}

/** The door's values for a gate. `pin: null` takes a pin away; a member left out is left as it is. */
export function setGate(home: string | null | undefined, repository: string, gate: string, values: { memoryGb?: number | null; alone?: boolean | null; pin?: Pin | null }): void {
  write(home, repository, (kept) => {
    const each = gateOf(kept, gate);
    if (values.memoryGb !== undefined) { if (values.memoryGb === null) delete each.memoryGb; else each.memoryGb = values.memoryGb; }
    if (values.alone !== undefined) { if (values.alone === null || values.alone === false) delete each.alone; else each.alone = true; }
    if (values.pin !== undefined) { if (values.pin === null) delete each.pin; else each.pin = values.pin; }
  });
}

/** A test set's per-test timeout in milliseconds: the home's for the set, else the declaration's, else none. */
export function setTimeoutOf(kept: RepositoryKept, name: string, declared: number | undefined): number | undefined {
  const own = kept.sets?.[name]?.timeoutMs;
  return positive(own) ? own : declared;
}

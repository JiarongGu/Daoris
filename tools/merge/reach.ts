/**
 * Which gates a change's paths can reach, by the lane table, and why each gate runs or is skipped.
 */
import { fileURLToPath } from 'node:url';
import { readAdmission } from './admission.ts';
import type { PathCheck } from './admission.ts';
import { laneMatcher } from './lanes-file.ts';
import { REACH } from './reach-table.ts';
import type { Rule } from './reach-table.ts';
import { isLongGate } from './plan.ts';
import type { Planned } from './plan.ts';
import { pad } from './format.ts';
import type { Lane } from './lanes.ts';
import type { Reach } from './verification.ts';

// test-map: strings name paths as data — the path checks name the paths they place, which the tool does not read

// The line's admission is the declaration's one list: the driver's landing reads the same key, so the tool keeps none of its own.
const ADMISSION = readAdmission(fileURLToPath(new URL('../..', import.meta.url)));

export type { PathCheck };

/** The gates every merge runs, whatever it changed: the blocking ones and the suites (`admission.always`, `admission.suites`). */
export const BASELINE: readonly string[] = Object.freeze([...ADMISSION.always, ...ADMISSION.suites]);

/**
 * What a merge blocks on by default (`admission.always`). Every other gate is listed and skipped, "in the CI beside" (`--ci`);
 * `--gated` runs what the merge's paths reach, `--full` every gate, `--rerun <gate>` a named one.
 */
export const BLOCKING: readonly string[] = ADMISSION.always;

/**
 * The light checks a merge blocks on when one of its paths reaches what they read (`admission.byPaths`). Each is reached by its own
 * `paths`, and by a path the lane table's every-gate rule places, never by the rest of the table: a path no rule of `REACH` places
 * reaches every other gate, and a typecheck stays a minute of the merges that change what it reads. `--gated` runs it where
 * reached, `--full` always.
 */
export const PATH_CHECKS: readonly PathCheck[] = ADMISSION.pathChecks;

/** The suites a merge runs whatever it changed (`admission.suites`): the CLI's verify holds the records, lists and documents, which no path places. */
export const MERGE_SUITES: readonly string[] = ADMISSION.suites;

/** The lanes the table places by: each lane's id and its paths. */
type Placing = readonly Pick<Lane, 'id' | 'paths'>[];

const pathCheckOf = (gate: string, pathChecks: readonly PathCheck[] = PATH_CHECKS): PathCheck | null => pathChecks.find((check) => check.gate === gate) ?? null;

/** The first rule that places a path, or null: by glob, or by a lane whose paths own it. */
export function ruleFor(path: string, rules: readonly Rule[], lanes: Placing): Rule | null {
  return ruleReader(rules, lanes)(path);
}

/** Compile a reached rule once within this read; a later read sees the configuration afresh. */
function ruleReader(rules: readonly Rule[], lanes: Placing): (path: string) => Rule | null {
  const matchers = new Map<Rule, (path: string) => boolean>();
  return (path) => rules.find((rule) => {
    let matches = matchers.get(rule);
    if (!matches) {
      const paths = rule.paths;
      const lane = paths ? null : lanes.find((candidate) => candidate.id === rule.lane);
      matches = paths ? laneMatcher([...paths]) : lane ? laneMatcher(lane.paths) : () => false;
      matchers.set(rule, matches);
    }
    return matches(path);
  }) ?? null;
}

const andMore = (paths: readonly string[]): string => `${paths[0]}${paths.length > 1 ? ` and ${paths.length - 1} more` : ''}`;

/** Every gate some rule of the table names: a gate none names is reached by every path. */
const namedGates = (reach: readonly Rule[]): Set<string> => new Set(reach.flatMap((rule) => (rule.gates === '*' ? [] : rule.gates)));

/** One gate of the plan as a merge reads it: whether it runs, whether the table reaches it, and why. */
export type Selection = { gate: Planned; run: boolean; reached: boolean; why: string };

/** A change's reach at a merge: of the gates the lane table can skip, those reached; the rest reach every change, so are not counted. */
export function gateReach(selection: readonly Selection[], reach: readonly Rule[] = REACH): Reach {
  const named = namedGates(reach);
  const always = new Set([...BLOCKING, ...BASELINE, ...MERGE_SUITES]);
  const skippable = selection.filter((entry) => named.has(entry.gate.name) && !always.has(entry.gate.name));
  return { reached: skippable.filter((entry) => entry.reached).length, of: skippable.length, what: 'gates the lane table can skip' };
}

type Reading = { lanes?: Placing; reach?: readonly Rule[]; pathChecks?: readonly PathCheck[] };

/**
 * Which of the plan's gates a merge runs, and why each runs or is skipped, in the plan's order. A gate is
 * `reached` when it is the baseline, a changed path's rule names it, a path has no rule, or no rule names
 * the gate at all. A merge runs what is reached but the long gates, which say they are in the full
 * set before staging; `full` runs every gate. `reached` is the lane table's answer, run or not, which is what
 * its tests hold to the code. `unplaced` is the paths no rule placed.
 */
export function selectGates(
  plan: readonly Planned[],
  changed: readonly string[],
  { lanes = [], full = false, gated = false, reach = REACH, pathChecks = PATH_CHECKS }: Reading & { full?: boolean; gated?: boolean } = {},
): { gates: Selection[]; unplaced: string[] } {
  const names = new Set(plan.map((gate) => gate.name));
  const named = namedGates(reach);
  const reachedBy = new Map<string, { paths: string[]; rule: Rule }>();
  const unplaced: string[] = [];
  const readRule = ruleReader(reach, lanes);
  for (const path of changed) {
    const rule = readRule(path);
    if (!rule) {
      unplaced.push(path);
      continue;
    }
    for (const gate of rule.gates === '*' ? names : rule.gates) {
      if (!names.has(gate)) continue;
      let by = reachedBy.get(gate);
      if (!by) reachedBy.set(gate, by = { paths: [], rule });
      by.paths.push(path);
    }
  }
  const reachOf = (gate: Planned): { reached: boolean; why: string } => {
    if (unplaced.length) return { reached: true, why: `everything runs: no rule of the lane table places ${andMore(unplaced)}` };
    const by = reachedBy.get(gate.name);
    if (by) return { reached: true, why: `${andMore(by.paths)} (${by.rule.lane ? `${by.rule.lane}: ` : ''}${by.rule.why})` };
    if (!named.has(gate.name)) return { reached: true, why: 'the lane table does not name it, so it runs at every merge' };
    return { reached: false, why: 'no changed path reaches it' };
  };
  const gates = plan.map((gate): Selection => {
    if (BLOCKING.includes(gate.name) || (gated && BASELINE.includes(gate.name))) return { gate, run: true, reached: true, why: 'runs at every merge' };
    if (!full && !gated && MERGE_SUITES.includes(gate.name)) return { gate, run: true, reached: true, why: 'runs at every merge: the lists, records and documents it holds a branch cannot place by its paths' };
    const check = pathCheckOf(gate.name, pathChecks);
    if (check) {
      // A path check blocks the merges whose paths reach what it reads, and only those.
      const by = reachingOf({ lanes, reach, pathChecks })(gate.name, changed);
      if (full) return { gate, run: true, reached: by.length > 0, why: '--full runs every gate' };
      if (by.length === 0) return { gate, run: false, reached: false, why: gated ? 'no changed path reaches it' : 'in the CI beside' };
      return { gate, run: true, reached: true, why: `blocks the merge: ${andMore(by)} (${check.why})` };
    }
    const { reached, why } = BASELINE.includes(gate.name) ? { reached: true, why: 'the baseline' } : reachOf(gate);
    if (full) return { gate, run: true, reached, why: '--full runs every gate' };
    const detail = reached ? `; ${why.replace(/^everything runs: /, '')}` : '';
    if (isLongGate(gate)) return { gate, run: false, reached, why: `in the full set before staging${detail}` };
    // A merge blocks on the checks alone; the CI beside (--ci) runs the rest, and --gated runs what is reached.
    if (!gated) return { gate, run: false, reached, why: `in the CI beside${detail}` };
    return { gate, run: reached, reached, why };
  });
  return { gates, unplaced };
}

/**
 * Of `paths`, the ones that reach `gate` by the lane table, read as `selectGates` reads it: every path reaches
 * a baseline gate and a gate no rule names, a path no rule places reaches every gate, and any other reaches the gates
 * its rule names. A verdict stays good until one of these changes, so the stage and `--rerun` ask this of the paths
 * changed since it is given. The test of the table checks that it and `selectGates` agree on every case.
 */
export function pathsReaching(gate: string, paths: readonly string[], reading: Reading = {}): string[] {
  return reachingOf(reading)(gate, paths);
}

/** `pathsReaching` asked many times, as a judgement of every stored verdict asks it: the rules compiled and each path placed once. */
export function reachingOf({ lanes = [], reach = REACH, pathChecks = PATH_CHECKS }: Reading = {}): (gate: string, paths: readonly string[]) => string[] {
  const readRule = ruleReader(reach, lanes);
  const placed = new Map<string, Rule | null>();
  const ruleOf = (path: string): Rule | null => {
    let rule = placed.get(path);
    if (rule === undefined) placed.set(path, (rule = readRule(path)));
    return rule;
  };
  const named = namedGates(reach);
  const owns = new Map<PathCheck, (path: string) => boolean>();
  return (gate, paths) => {
    const check = pathCheckOf(gate, pathChecks);
    if (check) {
      // A path check's own paths, and those the lane table sends to every gate.
      if (!owns.has(check)) owns.set(check, laneMatcher([...check.paths]));
      const own = owns.get(check)!;
      return paths.filter((path) => own(path) || ruleOf(path)?.gates === '*');
    }
    if (BASELINE.includes(gate) || !named.has(gate)) return [...paths];
    return paths.filter((path) => {
      const rule = ruleOf(path);
      return !rule || rule.gates === '*' || rule.gates.includes(gate);
    });
  };
}

/** The selection as lines: each gate run, numbered, with why; then each skipped, with why. */
export function selectionLines(gates: readonly Selection[]): string[] {
  const run = gates.filter((entry) => entry.run);
  const skipped = gates.filter((entry) => !entry.run);
  const out = run.map((entry, i) => `  ${pad(`${i + 1}.`, 4)}${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  if (skipped.length) {
    out.push(`  skipped (${skipped.length}):`);
    for (const entry of skipped) out.push(`      ${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  }
  return out;
}

/**
 * The reached checks of a merge: of the census of test units, the units its paths reach in the suites a merge can run in
 * minutes, as the commands that run them. The CI beside runs the whole suites; this is what keeps a red from landing in the
 * meantime. A unit the bounds leave out is counted and named, never dropped silently.
 */
import { posix } from 'node:path';
import { changedCode } from './tiers.ts';
import type { Tiers } from './tiers.ts';
import { isProcessGate } from './plan.ts';

// test-map: strings name paths as data — the suites' folders are what the commands run in, not reads

/** What runs a unit: `dotnet test`, Vitest or `node --test`. */
export type Runner = 'dotnet' | 'vitest' | 'node';

/** A test unit as the census of test units answers it for a merge: where it lives, what runs it, and the path that reached it. */
export type ReachedUnit = {
  suite: string; kind: string; unit: string; name: string; file: string; by?: string;
  project?: string; settings?: string | null; needs?: string[];
};

/** The units of one suite and runner that one command runs. */
export type ReachedGroup = { runner: Runner; suite: string; project?: string; settings?: string | null; units: ReachedUnit[] };

/** What a plan decides: the groups to run, the count each suite left out, every unit reached, and the units replayed from a standing pass. */
export type ReachedPlan = { groups: ReachedGroup[]; left: Record<string, number>; reached: number; replayed: Record<string, number> };

/** A command with the folder it runs in and the label its log says. */
export type Command = { label: string; cwd: string; command: string; args: string[] };

/**
 * The suites a merge runs units of, by the runner of each: the CLI's and the tools' unit tests are reached by the census like the
 * rest (the merge's `cli` gate holds the checks, `cli-tests` the whole run). Not the censuses (a path check of their own) nor the
 * browser suites. The Process halves and the rehearsals' classes are the heavy tier (`heavy-tier.ts`): the run after the landing.
 */
const SUITES: Readonly<Record<string, Runner>> = Object.freeze({
  driver: 'dotnet', modules: 'dotnet', service: 'dotnet', 'web-unit': 'vitest', 'tool-tests': 'node', cli: 'node',
  'driver-process': 'dotnet', 'driver-process-git': 'dotnet', 'driver-process-git-pinned': 'dotnet', 'modules-process': 'dotnet', 'service-process': 'dotnet',
  'driver-rehearsals': 'dotnet',
});

/** The suites whose gate runs the Process category (real processes, a real git): the declared gates that run with the Process settings. */
export const processSuitesOf = (gates: readonly { name: string; run: string }[]): Set<string> => new Set(gates.filter(isProcessGate).map((gate) => gate.name));

/** The runner of a unit: its suite's, but a Vitest file of a suite that also holds `node --test` files is Vitest's. */
const runnerOf = (unit: ReachedUnit): Runner | undefined => (unit.kind === 'vitest' ? 'vitest' : SUITES[unit.suite]);

/** A Process half's class is a unit of a merge only where `unitsOfMerge` kept it for a dependency whose code changed. */
const isDependencyUnit = (unit: ReachedUnit): boolean => Array.isArray(unit.needs) && unit.needs.length > 0;

/**
 * How many units of one suite a merge runs, bounded by the minutes they cost (median of a CI run's gates, 2026-11-05 to
 * 2026-11-11: the driver's 16, the modules' 7, the page's unit run 11, the tool-tests 22, each whole): a bound is a fraction of a
 * suite's time, and what it leaves is counted in `left`.
 */
export const REACHED_BOUNDS: Readonly<Record<Runner, number>> = Object.freeze({ dotnet: 40, vitest: 80, node: 12 });

const RUNNER_ORDER: readonly Runner[] = ['dotnet', 'vitest', 'node'];
const WEB = 'src/Daoris.Web';
const CLI = 'src/Daoris.Cli';
const VITEST = 'node_modules/vitest/vitest.mjs';

/** A command names at most this many units, so a bound lifted never meets the command line's length limit. */
const COMMAND_UNITS = 120;
const chunked = (units: ReachedUnit[]): ReachedUnit[][] => Array.from({ length: Math.ceil(units.length / COMMAND_UNITS) }, (_, at) => units.slice(at * COMMAND_UNITS, (at + 1) * COMMAND_UNITS));

const stemOf = (path: string): string => posix.basename(path).replace(/\.[^.]+$/, '').replace(/\.(test|spec)$/, '').replace(/(Tests?)$/, '');

/**
 * How near a unit is to the change, for choosing among the units a bound leaves out: 3 where the change is in the unit's own
 * file, 2 where the unit is named for a changed file, 1 where it is in a changed file's folder (a class reaches by area, and
 * an area is wide), else 0.
 */
function nearness(unit: ReachedUnit, changed: readonly string[]): number {
  let best = 0;
  const own = stemOf(unit.file);
  for (const path of changed) {
    if (path === unit.file) return 3;
    const stem = stemOf(path);
    if (own.length >= 6 && stem.length >= 6 && (own.startsWith(stem) || stem.startsWith(own))) best = Math.max(best, 2);
    else if (posix.basename(posix.dirname(path)) === posix.basename(posix.dirname(unit.file))) best = Math.max(best, 1);
  }
  return best;
}

/**
 * The units a merge considers of those its paths reach: every unit class, and of the Process halves' classes (which each name
 * the dependency they need) only those a changed path of that dependency's declared code reaches (`reaches(unit, path)`). Each
 * other dependency class is counted by its first dependency in `night`, left to the nightly run and the stage. A Process suite is
 * one whose gate runs with the Process settings (`processSuites`), whatever it is called.
 */
export function unitsOfMerge<U extends ReachedUnit>(
  units: readonly U[],
  paths: readonly string[],
  { tiers, reaches, processSuites }: { tiers: Tiers; reaches: (unit: U, path: string) => boolean; processSuites: ReadonlySet<string> },
): { units: U[]; night: Record<string, number> } {
  const kept: U[] = [];
  const night: Record<string, number> = {};
  for (const unit of units) {
    if (!SUITES[unit.suite] || !processSuites.has(unit.suite)) {
      kept.push(unit);
      continue;
    }
    const needs = unit.needs ?? [];
    if (changedCode(needs, [...paths], tiers).some((path) => reaches(unit, path))) kept.push(unit);
    else {
      const first = needs[0] ?? 'unnamed';
      night[first] = (night[first] ?? 0) + 1;
    }
  }
  return { units: kept, night };
}

/**
 * Which units of `units` (the census's `unitsReached`) a merge runs. A suite over its bound keeps the units nearest the
 * change first (`nearness`), then the census's order; `left` counts what each suite left out and `reached` every unit of
 * the suites a merge runs that the paths reach.
 */
export function reachedPlan(
  units: readonly ReachedUnit[],
  changed: readonly string[],
  { bounds = {}, replayed = {}, processSuites }: { bounds?: Partial<Record<Runner, number>>; replayed?: Record<string, number>; processSuites: ReadonlySet<string> },
): ReachedPlan {
  const limit = { ...REACHED_BOUNDS, ...bounds };
  const bySuite = new Map<string, { suite: string; runner: Runner; all: ReachedUnit[] }>();
  for (const unit of units) {
    const runner = runnerOf(unit);
    if (!runner || !SUITES[unit.suite] || (processSuites.has(unit.suite) && !isDependencyUnit(unit))) continue;
    const key = `${unit.suite}|${runner}`;
    const group = bySuite.get(key) ?? { suite: unit.suite, runner, all: [] };
    group.all.push(unit);
    bySuite.set(key, group);
  }
  const groups: ReachedGroup[] = [];
  const left: Record<string, number> = {};
  let reached = Object.values(replayed).reduce((sum, count) => sum + count, 0);
  for (const { suite, runner, all } of bySuite.values()) {
    reached += all.length;
    const ordered = all.map((unit, index) => ({ unit, index, near: nearness(unit, changed) }))
      .sort((a, b) => b.near - a.near || a.index - b.index).map((entry) => entry.unit);
    const kept = ordered.slice(0, limit[runner]);
    if (kept.length < all.length) left[suite] = (left[suite] ?? 0) + all.length - kept.length;
    if (runner === 'dotnet') {
      for (const project of [...new Set(kept.map((unit) => unit.project))]) {
        const among = kept.filter((unit) => unit.project === project);
        for (const units of chunked(among)) groups.push({ runner, suite, ...(project === undefined ? {} : { project }), settings: among[0]?.settings ?? null, units });
      }
    } else {
      for (const units of chunked(kept)) groups.push({ runner, suite, units });
    }
  }
  groups.sort((a, b) => RUNNER_ORDER.indexOf(a.runner) - RUNNER_ORDER.indexOf(b.runner));
  return { groups, left, reached, replayed };
}

/** The commands that run a plan's groups, each with the folder it runs in and the label its log says. */
export function reachedCommands(plan: Pick<ReachedPlan, 'groups'>): Command[] {
  return plan.groups.map((group): Command => {
    if (group.runner === 'dotnet') {
      const names = group.units.map((unit) => `FullyQualifiedName~${unit.unit}`).join('|');
      const project = group.project ?? '.';
      if (group.settings) {
        // The settings' own filter makes it the half, and VSTest ANDs it with the one added here.
        return { label: group.suite, cwd: '.', command: 'dotnet', args: ['test', project, '--settings', group.settings, '--filter', `(${names})`] };
      }
      return { label: group.suite, cwd: '.', command: 'dotnet', args: ['test', project, '--filter', `(Category!=Process)&(${names})`] };
    }
    // Vitest's own entry under node: npm's script is a shell script on Windows. The page's install is its own; the tools' is the root's.
    if (group.runner === 'vitest' && group.suite === 'web-unit') {
      return { label: group.suite, cwd: WEB, command: 'node', args: [VITEST, 'run', ...group.units.map((unit) => posix.relative(WEB, unit.file))] };
    }
    if (group.runner === 'vitest') {
      return { label: group.suite, cwd: '.', command: 'node', args: [VITEST, 'run', '--config', 'tools/vitest.config.ts', ...group.units.map((unit) => unit.file)] };
    }
    return {
      label: group.suite, cwd: CLI, command: 'node',
      args: ['--test', '--test-timeout=1800000', '--test-force-exit', ...group.units.map((unit) => posix.relative(CLI, unit.file))],
    };
  });
}

/** The plan as lines: what each suite runs, what it leaves to the CI beside, or that no unit is reached. */
export function reachedLines(plan: ReachedPlan): string[] {
  if (plan.reached === 0) return ['reached: no reached unit in a suite a merge runs; the CI beside runs the whole suites'];
  const lines: string[] = [];
  const replayed = plan.replayed;
  for (const suite of new Set([...plan.groups.map((group) => group.suite), ...Object.keys(replayed)])) {
    const ran = plan.groups.filter((group) => group.suite === suite).reduce((sum, group) => sum + group.units.length, 0);
    const left = plan.left[suite] ?? 0;
    const standing = replayed[suite] ? `; ${replayed[suite]} replayed from a standing pass of the same inputs` : '';
    lines.push(left
      ? `reached: ${suite}: ${ran} of ${ran + left} reached units run; ${left} are left to the CI beside${standing}`
      : `reached: ${suite}: ${ran} reached unit${ran === 1 ? '' : 's'} run${standing}`);
  }
  return lines;
}

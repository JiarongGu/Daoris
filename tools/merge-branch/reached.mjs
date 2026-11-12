/**
 * The reached checks of a merge: of the census of test units, the units its paths reach in the suites a merge can run in
 * minutes, as the commands that run them. The CI beside runs the whole suites; this is what keeps a red from landing in the
 * meantime. A unit the bounds leave out is counted and named, never dropped silently.
 */
import { posix } from 'node:path';

// test-map: strings name paths as data — the suites' folders are what the commands run in, not reads

/**
 * The suites a merge runs units of, by the runner of each. The CLI's suite is not here (its verify runs whole at every
 * merge), nor the censuses (a path check of their own), nor the Process halves, the browser suites and the rehearsals,
 * which only the full set runs.
 */
const SUITES = Object.freeze({
  driver: 'dotnet', modules: 'dotnet', service: 'dotnet', 'web-unit': 'vitest', 'tool-tests': 'node',
});

/**
 * How many units of one suite a merge runs, bounded by the minutes they cost (median of a CI run's gates, 2026-11-05 to
 * 2026-11-11: the driver's 16, the modules' 7, the page's unit run 11, the tool-tests 22, each whole): a bound is a fraction of a
 * suite's time, and what it leaves is counted in `left`.
 */
export const REACHED_BOUNDS = Object.freeze({ dotnet: 80, vitest: 80, node: 30 });

const RUNNER_ORDER = ['dotnet', 'vitest', 'node'];
const WEB = 'src/Daoris.Web';
const CLI = 'src/Daoris.Cli';

const stemOf = (path) => posix.basename(path).replace(/\.[^.]+$/, '').replace(/\.(test|spec)$/, '').replace(/(Tests?)$/, '');

/**
 * How near a unit is to the change, for choosing among the units a bound leaves out: 3 where the change is in the unit's own
 * file, 2 where the unit is named for a changed file, 1 where it is in a changed file's folder (a class reaches by area, and
 * an area is wide), else 0.
 */
function nearness(unit, changed) {
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
 * Which units of `units` (the census's `unitsReached`) a merge runs. A suite over its bound keeps the units nearest the
 * change first (`nearness`), then the census's order; `left` counts what each suite left out and `reached` every unit of
 * the suites a merge runs that the paths reach.
 */
export function reachedPlan(units, changed, { bounds = {} } = {}) {
  const limit = { ...REACHED_BOUNDS, ...bounds };
  const bySuite = new Map();
  for (const unit of units) {
    if (!SUITES[unit.suite]) continue;
    bySuite.set(unit.suite, [...(bySuite.get(unit.suite) ?? []), unit]);
  }
  const groups = [];
  const left = {};
  let reached = 0;
  for (const [suite, all] of bySuite) {
    reached += all.length;
    const runner = SUITES[suite];
    const ordered = all.map((unit, index) => ({ unit, index, near: nearness(unit, changed) }))
      .sort((a, b) => b.near - a.near || a.index - b.index).map((entry) => entry.unit);
    const kept = ordered.slice(0, limit[runner]);
    if (kept.length < all.length) left[suite] = all.length - kept.length;
    if (runner === 'dotnet') {
      for (const project of [...new Set(kept.map((unit) => unit.project))]) {
        groups.push({ runner, suite, project, units: kept.filter((unit) => unit.project === project) });
      }
    } else {
      groups.push({ runner, suite, units: kept });
    }
  }
  groups.sort((a, b) => RUNNER_ORDER.indexOf(a.runner) - RUNNER_ORDER.indexOf(b.runner));
  return { groups, left, reached };
}

/** The commands that run a plan's groups, each with the folder it runs in and the label its log says. */
export function reachedCommands(plan) {
  return plan.groups.map((group) => {
    if (group.runner === 'dotnet') {
      const names = group.units.map((unit) => `FullyQualifiedName~${unit.unit}`).join('|');
      return { label: group.suite, cwd: '.', command: 'dotnet', args: ['test', group.project, '--filter', `(Category!=Process)&(${names})`] };
    }
    if (group.runner === 'vitest') {
      return { label: group.suite, cwd: '.', command: 'npm', args: ['--prefix', WEB, 'run', 'test', '--', ...group.units.map((unit) => posix.relative(WEB, unit.file))] };
    }
    return {
      label: group.suite, cwd: CLI, command: 'node',
      args: ['--test', '--test-timeout=1800000', '--test-force-exit', ...group.units.map((unit) => posix.relative(CLI, unit.file))],
    };
  });
}

/** The plan as lines: what each suite runs, what it leaves to the CI beside, or that no unit is reached. */
export function reachedLines(plan) {
  if (plan.reached === 0) return ['reached: no reached unit in a suite a merge runs; the CI beside runs the whole suites'];
  const lines = [];
  for (const suite of new Set(plan.groups.map((group) => group.suite))) {
    const ran = plan.groups.filter((group) => group.suite === suite).reduce((sum, group) => sum + group.units.length, 0);
    const left = plan.left[suite] ?? 0;
    lines.push(left
      ? `reached: ${suite}: ${ran} of ${ran + left} reached units run; ${left} are left to the CI beside`
      : `reached: ${suite}: ${ran} reached unit${ran === 1 ? '' : 's'} run`);
  }
  return lines;
}

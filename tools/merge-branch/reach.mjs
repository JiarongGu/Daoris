/**
 * Which gates a change's paths can reach, by the lane table, and why each gate runs or is skipped.
 */
import { laneMatcher } from './lanes-file.mjs';
import { REACH } from './reach-table.mjs';
import { isLongGate } from './plan.mjs';
import { pad } from './format.mjs';

// test-map: strings name paths as data — the path checks name the paths they place, which the tool does not read

/**
 * The gates every merge runs, whatever it changed: the universal gates scan every file, the code map
 * maps all the code, the census of test units reads every suite's
 * sources, and the CLI's `verify` holds the records, the lanes, the canon and the tools' tests.
 */
export const BASELINE = Object.freeze(['universal', 'code-map', 'test-map', 'cli']);

/**
 * What a merge blocks on by default: the universal gates (which hold the sensitive-patterns scan, never to reach a
 * commit), the code map, and the census of test units' check (`tools/test-map.mjs check`, about 20 s), which reads every
 * suite's sources and holds the lane table's hand rows, the whole-suite files and the skips to them. Every other gate is
 * listed and skipped, "in the CI beside" (`--ci`); `--gated` runs what the merge's paths reach, `--full` every gate,
 * `--rerun <gate>` a named one.
 */
export const BLOCKING = Object.freeze(['universal', 'code-map', 'test-map']);

/**
 * The light checks a merge blocks on when one of its paths reaches what they read, beside `BLOCKING`. Two sides that each
 * typecheck may not together, and the CI beside finds that hours after the commit; the web's `tsc --noEmit` takes about a
 * minute. Each is reached by its own `paths`, and by a path the lane table's every-gate rule places (the gates, the
 * scripts, the lanes), never by the rest of the table: a path no rule of `REACH` places reaches every other gate, and a
 * typecheck stays a minute of the merges that change what it reads. `--gated` runs it where reached, `--full` always.
 *
 * The web's design and structure tests are a second such check, reached the same way: a story can break
 * `design/tokens.test.ts` (a line's measure, a native control), which only the CI beside's `web-unit` would run; the two
 * files run in a minute or two, most of it jsdom starting. The desktop's censuses are a third.
 */
export const PATH_CHECKS = Object.freeze([
  {
    gate: 'web-types',
    paths: ['src/Daoris.Web/src/**', 'src/Daoris.Web/tsconfig.json', 'src/Daoris.Web/vite.config.ts', 'src/Daoris.Web/package.json', 'src/Daoris.Web/package-lock.json'],
    why: "the web's typecheck: the page's sources, what configures its compiler, or what changes how every gate runs (MERGETSC1)",
  },
  {
    gate: 'web-rules',
    paths: ['src/Daoris.Web/src/**', 'src/Daoris.Web/vite.config.ts', 'src/Daoris.Web/package.json', 'src/Daoris.Web/package-lock.json'],
    why: "the web's design and structure checks: the page's sources, what configures its tests, or what changes how every gate runs (MERGEWEBRULES1)",
  },
  {
    gate: 'censuses',
    paths: ['src/Daoris.Desktop/Daoris.Desktop.{Driver,Driver.Tests,Modules,Modules.Tests,App}/**/*.cs', 'src/Daoris.Service/Daoris.Service.Tests/fixtures/participant-work-readers.json'],
    why: 'the desktop\'s censuses, every test class that reads the sources by text (`--filter Category=Census`, REVIEW2 T6): a source they walk, or a table they hold it to; the CI beside ran them hours after a merge',
  },
  {
    gate: 'reached',
    paths: ['src/**', 'tools/**', 'examples/**', 'canon/**', 'daoris.gates.json', 'daoris.lanes.json', 'package.json', 'package-lock.json'],
    why: 'the test units the merge\'s paths reach in the suites a merge can run in minutes (the driver\'s, the modules\', the service\'s, the page\'s unit tests and the tool-tests, by the census of test units, bounded per suite, DEVCYCLE1): a list or a text a test holds the change to, which the CI beside found hours after it landed',
  },
]);

/** The suites a merge runs whatever it changed: the CLI's verify holds the records, lists and documents, which no path places; about three minutes in a CI run. */
export const MERGE_SUITES = Object.freeze(['cli']);

const pathCheckOf = (gate, pathChecks = PATH_CHECKS) => pathChecks.find((check) => check.gate === gate) ?? null;

/** Of `paths`, the ones that reach a path check: its own, and those the lane table sends to every gate. */
const pathCheckPaths = (check, paths, reach, lanes) => {
  const own = laneMatcher(check.paths);
  const readRule = ruleReader(reach, lanes);
  return paths.filter((path) => own(path) || readRule(path)?.gates === '*');
};

/** The first rule that places a path, or null: by glob, or by a lane whose paths own it. */
export function ruleFor(path, rules, lanes) {
  return ruleReader(rules, lanes)(path);
}

/** Compile a reached rule once within this read; a later read sees the configuration afresh. */
function ruleReader(rules, lanes) {
  const matchers = new Map();
  return (path) => rules.find((rule) => {
    if (!matchers.has(rule)) {
      const paths = rule.paths;
      const lane = paths ? null : lanes.find((candidate) => candidate.id === rule.lane);
      matchers.set(rule, paths ? laneMatcher(paths) : lane ? laneMatcher(lane.paths) : () => false);
    }
    return matchers.get(rule)(path);
  }) ?? null;
}

const andMore = (paths) => `${paths[0]}${paths.length > 1 ? ` and ${paths.length - 1} more` : ''}`;

/** Every gate some rule of the table names: a gate none names is reached by every path. */
const namedGates = (reach) => new Set(reach.flatMap((rule) => (rule.gates === '*' ? [] : rule.gates)));

/**
 * Which of the plan's gates a merge runs, and why each runs or is skipped, in the plan's order. A gate is
 * `reached` when it is the baseline, a changed path's rule names it, a path has no rule, or no rule names
 * the gate at all. A merge runs what is reached but the long gates, which say they are in the full
 * set before staging; `full` runs every gate. `reached` is the lane table's answer, run or not, which is what
 * its tests hold to the code. `unplaced` is the paths no rule placed.
 */
export function selectGates(plan, changed, { lanes = [], full = false, gated = false, reach = REACH, pathChecks = PATH_CHECKS } = {}) {
  const names = new Set(plan.map((gate) => gate.name));
  const named = namedGates(reach);
  const reachedBy = new Map();
  const unplaced = [];
  const readRule = ruleReader(reach, lanes);
  for (const path of changed) {
    const rule = readRule(path);
    if (!rule) {
      unplaced.push(path);
      continue;
    }
    for (const gate of rule.gates === '*' ? names : rule.gates) {
      if (!names.has(gate)) continue;
      if (!reachedBy.has(gate)) reachedBy.set(gate, { paths: [], rule });
      reachedBy.get(gate).paths.push(path);
    }
  }
  const reachOf = (gate) => {
    if (unplaced.length) return { reached: true, why: `everything runs: no rule of the lane table places ${andMore(unplaced)}` };
    const by = reachedBy.get(gate.name);
    if (by) return { reached: true, why: `${andMore(by.paths)} (${by.rule.lane ? `${by.rule.lane}: ` : ''}${by.rule.why})` };
    if (!named.has(gate.name)) return { reached: true, why: 'the lane table does not name it, so it runs at every merge' };
    return { reached: false, why: 'no changed path reaches it' };
  };
  const gates = plan.map((gate) => {
    if (BLOCKING.includes(gate.name) || (gated && BASELINE.includes(gate.name))) return { gate, run: true, reached: true, why: 'runs at every merge' };
    if (!full && !gated && MERGE_SUITES.includes(gate.name)) return { gate, run: true, reached: true, why: 'runs at every merge: the lists, records and documents it holds a branch cannot place by its paths' };
    const check = pathCheckOf(gate.name, pathChecks);
    if (check) {
      // A path check blocks the merges whose paths reach what it reads, and only those.
      const by = pathCheckPaths(check, changed, reach, lanes);
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
export function pathsReaching(gate, paths, { lanes = [], reach = REACH, pathChecks = PATH_CHECKS } = {}) {
  const check = pathCheckOf(gate, pathChecks);
  if (check) return pathCheckPaths(check, paths, reach, lanes);
  if (BASELINE.includes(gate) || !namedGates(reach).has(gate)) return [...paths];
  const readRule = ruleReader(reach, lanes);
  return paths.filter((path) => {
    const rule = readRule(path);
    return !rule || rule.gates === '*' || rule.gates.includes(gate);
  });
}

/** The selection as lines: each gate run, numbered, with why; then each skipped, with why. */
export function selectionLines(gates) {
  const run = gates.filter((entry) => entry.run);
  const skipped = gates.filter((entry) => !entry.run);
  const out = run.map((entry, i) => `  ${pad(`${i + 1}.`, 4)}${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  if (skipped.length) {
    out.push(`  skipped (${skipped.length}):`);
    for (const entry of skipped) out.push(`      ${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  }
  return out;
}

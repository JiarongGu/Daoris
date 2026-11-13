/**
 * This repository as the lane table's tests read it: its declared plan, its lanes, its tracked files, and what a change to some
 * paths runs, reaches or makes stale. The gates are read from `daoris.gates.json`; a test names one only through `declared`,
 * which refuses a name the file does not declare.
 */
import { spawnSync } from 'node:child_process';
import { posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readLanes } from './lanes.ts';
import { isLongGate, readPlan } from './plan.ts';
import { BLOCKING, PATH_CHECKS, pathsReaching, selectGates } from './reach.ts';
import type { Rule } from './reach-table.ts';

// test-map: strings name paths as data — the tables and sources named here are what the lane table's tests hold it to

export const repoRoot = fileURLToPath(new URL('../..', import.meta.url));
export const repoPlan = readPlan(repoRoot);
export const repoLanes = readLanes(repoRoot)!.lanes;
export const ALL: string[] = repoPlan.map((gate) => gate.name);
/** The gates only the full set runs: the Process halves and the deployment rehearsal. */
export const LONG: string[] = repoPlan.filter(isLongGate).map((gate) => gate.name);

/** Names of declared gates, each checked against the declaration, so a renamed or dropped gate fails where it is named. */
export function declared<const T extends readonly string[]>(...names: T): { -readonly [K in keyof T]: string } {
  const missing = names.filter((name) => !ALL.includes(name));
  if (missing.length) throw new Error(`not declared in daoris.gates.json: ${missing.join(', ')}`);
  return [...names] as unknown as { -readonly [K in keyof T]: string };
}

/** The gates of these sets together, in the plan's order. */
export const inPlanOrder = (...sets: string[][]): string[] => ALL.filter((gate) => sets.some((set) => set.includes(gate)));

/** The gates no rule of the table names and no path check holds: whatever changed reaches them. */
export const unnamedGates = (reach: readonly Rule[]): string[] => ALL.filter((name) => !PATH_CHECKS.some((check) => check.gate === name)
  && !reach.some((rule) => rule.gates !== '*' && rule.gates.includes(name)));

/** What `--gated` runs, per gate: the lane table's own answer for these paths, which its tests hold to the code. */
export const selected = (paths: string[], full = false, gated = true) => selectGates(repoPlan, paths, { lanes: repoLanes, full, gated }).gates;
/** What a merge runs by default: the blocking checks and the suites it always runs. */
export const mergeRuns = (paths: string[], full = false): string[] => selected(paths, full, false).filter((entry) => entry.run).map((entry) => entry.gate.name);
/** What a merge runs when asked for what its paths reach. */
export const runs = (paths: string[], full = false): string[] => selected(paths, full).filter((entry) => entry.run).map((entry) => entry.gate.name);
/** What the lane table says can see the change, run or not: what holds the table to the code. */
export const reaches = (paths: string[]): string[] => selected(paths).filter((entry) => entry.reached).map((entry) => entry.gate.name);
/** The gates whose verdicts a change to these paths makes stale, in the plan's order. */
export const stalens = (paths: string[]): string[] => ALL.filter((gate) => pathsReaching(gate, paths, { lanes: repoLanes }).length > 0);
export { BLOCKING };

/** The kept names' table: among the driver's tests, and the page's twin reads it too. */
export const KEPT_NAMES = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/kept-names.json';
/** The backlog merges' table: among the driver's tests, and the merge tool's twin reads it too. */
export const BACKLOG_MERGES = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/backlog-merges.json';
/** The joins union leaves and the merge sets apart: among the driver's tests, and the merge tool's twin reads it too. */
export const UNION_JOINS = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json';
/** The integration declarations' table: among the driver's tests, and the merge tool's twin reads it too. */
export const INTEGRATION_DECLARATIONS = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json';
/**
 * The driver's sources whose declarations the page's twin vitests parse. Each by its
 * name, at whatever depth of the driver's folder it is tracked.
 */
export const PAGE_PARSED_NAMES = ['NoteCodes.cs', 'EndNoteCodes.cs', 'StartNoteCodes.cs', 'HeldNoteCodes.cs', 'ChatNoteCodes.cs', 'AccountNoteCodes.cs', 'PeerNoteCodes.cs', 'SessionEvents.cs', 'HandedCodes.cs', 'TraceCodes.cs', 'LandingCode.cs', 'WorkplaceFacts.cs', 'SweepKind.cs', 'BackgroundState.cs', 'TraceChains.cs', 'LandedKind.cs'];
export const DRIVER_FOLDER = 'src/Daoris.Desktop/Daoris.Desktop.Driver/';

/** Every tracked file of the repository. */
export function trackedFiles(): string[] {
  const listed = spawnSync('git', ['ls-files'], { cwd: repoRoot, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
  if (listed.status !== 0) throw new Error(listed.stderr);
  return listed.stdout.split('\n').filter(Boolean);
}

export const PAGE_PARSED = PAGE_PARSED_NAMES
  .map((name) => trackedFiles().find((path) => path.startsWith(DRIVER_FOLDER) && posix.basename(path) === name) ?? `${DRIVER_FOLDER}${name}`);
/** The page-parsed rule, by the names it holds wherever the driver keeps them. */
export const isPageParsedRule = (rule: Rule): boolean => rule.paths?.some((path) => PAGE_PARSED_NAMES.some((name) => path === `${DRIVER_FOLDER}**/${name}`)) ?? false;

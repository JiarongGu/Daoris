/**
 * The six reds that landed on main on 2026-11-17 and 18 through merges whose gates all passed, each replayed as the
 * change that caused it: the merge's plan now picks the check that would have stopped it. Each case names its cause and its fix
 * on main; the e2e specs' reads are the census's of the tree at 7e1a18e882.
 */
import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { splitTier } from './heavy-tier.ts';
import { reachedCommands, reachedPlan } from './reached.ts';
import { reuseContext } from './reached-reuse.ts';
import type { ReachedUnit } from './reached.ts';
import type { TestMap } from './reached-reuse.ts';
// @ts-expect-error — untyped workspace tooling, read through the seam its own tests use
import * as stepsModule from '../verify-checks/steps.mjs';
// @ts-expect-error — untyped workspace tooling, read through the seam its own tests use
import * as testMap from '../test-map.mjs';

type Step = { run: string };
const { CHECK_STEPS, selectSteps } = stepsModule as { CHECK_STEPS: Step[]; selectSteps: (steps: Step[], changed: string[]) => { run: Step[] } };
type Unit = ReachedUnit & { reach?: unknown };
type Map = TestMap & { units: Unit[] };
const { TS_SUITES, census, declaresRead, tsReaches, unitHops, unitReaches } = testMap as {
  TS_SUITES: { suite: string; whole?: string[] }[];
  census: (files: string[], read: (path: string) => string, options?: { suites?: string[] }) => Map;
  tsReaches: (reach: unknown, path: string) => boolean;
  declaresRead: (reach: unknown, path: string) => boolean;
  unitHops: (unit: Unit, paths: string[], map: Map) => number | undefined;
  unitReaches: (unit: ReachedUnit, path: string, map: TestMap) => boolean;
};

const ROOT = fileURLToPath(new URL('../..', import.meta.url));
const real = (path: string): string => readFileSync(`${ROOT}${path}`, 'utf8');
const LAYERS_COUNTS = 'node tools/layers-census/counts.mjs';

/** The merge's reach over a census of `files` (`read` gives each text): the key context `reached-checks` plans by. */
function mergeReach(files: string[], read: (path: string) => string) {
  const map = census(files, read);
  const context = reuseContext({
    map, files, read, toolchain: '', gates: [], readers: { tsSuites: TS_SUITES, tsReaches, declaresRead, unitReaches, pathReach: () => ({}) },
  });
  return { map, reaches: (unit: Unit, path: string) => context.reaches(unit, path) };
}

const treeOf = (texts: Record<string, string>) => ({ files: Object.keys(texts), read: (path: string) => texts[path] ?? '' });

test('1: a test helper split out of a census class (848804aeac) runs the layers census, whose git allowance moved with it (9e402f39d7)', () => {
  const changed = [
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/ReviewGraphCensusTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/ReviewGraphPlace.cs', 'tools/size-ratchet.json',
  ];
  assert.ok(selectSteps(CHECK_STEPS, changed).run.some((step) => step.run === LAYERS_COUNTS));
});

test('5: a private record named like a type the boundary counts (8d4cc2c363), or the boundary list (d9e9c13e0d), runs the layers census (0af22c3ecf)', () => {
  assert.ok(selectSteps(CHECK_STEPS, ['src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/Cast/PlaceCastVerb.cs']).run.some((step) => step.run === LAYERS_COUNTS));
  assert.ok(selectSteps(CHECK_STEPS, ['tools/layers-census.json', 'daoris.lanes.json']).run.some((step) => step.run === LAYERS_COUNTS));
  assert.ok(!selectSteps(CHECK_STEPS, ['docs/development.md']).run.some((step) => step.run === LAYERS_COUNTS), 'a document reaches no source count');
});

test('6: a declaration beside a script (b4a8c80f7b) runs the declarations test, which lists them (7e1a18e882)', () => {
  const { files, read } = treeOf({
    'tools/merge/declarations.test.ts': real('tools/merge/declarations.test.ts'),
    'tools/time-load.mjs': 'export const stampLoad = 1;\n', 'tools/time-load.d.mts': 'export declare const stampLoad: 1;\n',
    'tools/fsx.mjs': '', 'tools/fsx.d.mts': '', 'tools/merge-branch/dependencies.d.mts': '', 'tools/merge-branch/dependencies.mjs': '',
    'docs/development.md': '',
  });
  const { map, reaches } = mergeReach(files, read);
  const unit = map.units.find((each) => each.file === 'tools/merge/declarations.test.ts')!;
  assert.ok(reaches(unit, 'tools/time-load.d.mts') && reaches(unit, 'tools/time-load.mjs') && reaches(unit, 'tools/merge-branch/dependencies.d.mts'));
  assert.ok(!reaches(unit, 'docs/development.md'));
  assert.equal(unitHops(unit, ['tools/time-load.d.mts'], map), 1, 'a path a unit declares it reads is one read away, so no bound leaves it out');
  const bounded = { declared: ['src/fixtures/a.json', 'tools/merge'], within: ['tools/merge/'] };
  assert.ok(!declaresRead(bounded, 'src/fixtures/a.json') && declaresRead(bounded, 'tools/merge/x.ts'), "an end-to-end spec's declared reads stay within its gate's paths");
});

test('4: a build-output site added to the merge tool (4c89cc5988) runs the tree-base census, a node test the CLI\'s verify lists (e22e8884e8)', () => {
  const { files, read } = treeOf({
    'package.json': JSON.stringify({ scripts: { 'verify:tests': 'npm test -w daoris && node --test tools/orient-index.test.mjs tools/tree-base.test.mjs' } }),
    'daoris.gates.json': JSON.stringify({ gates: [{ name: 'cli-tests', run: 'npm run verify:tests' }] }),
    'src/Daoris.Cli/package.json': JSON.stringify({ scripts: { test: 'node --test test/**/*.test.ts' } }),
    'tools/tree-base.test.mjs': real('tools/tree-base.test.mjs'), 'tools/tree-base.mjs': '', 'tools/orient-index.test.mjs': '',
    'src/Daoris.Cli/test/_fixture.ts': '', 'tools/merge/reached-build.ts': 'export const onBuilt = 1;\n', 'docs/development.md': '',
  });
  const { map, reaches } = mergeReach(files, read);
  const unit = map.units.find((each) => each.file === 'tools/tree-base.test.mjs')!;
  assert.equal(unit?.suite, 'cli', 'the census counts the CLI\'s verify run as cli, its other units\' suite');
  assert.ok(reaches(unit, 'tools/merge/reached-build.ts') && !reaches(unit, 'docs/development.md'));
  const changed = ['tools/merge/reached-build.ts'];
  const hops = unitHops(unit, changed, map);
  assert.equal(hops, 1, 'a declared listing is one read away, so no bound leaves the census out');
  const plan = reachedPlan([{ ...unit, hops: 1 }], changed, { processSuites: new Set() });
  const [command] = reachedCommands(plan);
  assert.deepEqual([command?.cwd, command?.args.at(-1)], ['.', 'tools/tree-base.test.mjs'], 'a root test runs from the root, as its gate runs it');
});

/** The tool-e2e specs within two reads of each case's changed paths, by the census at 7e1a18e882 (`unitHops`), in its order. */
const e2e = (near: Record<string, number>) => Object.entries(near).map(([file, hops]): ReachedUnit => ({ suite: 'tool-e2e', kind: 'vitest', unit: file, name: file, file, hops }));
const FAR = ['merge-backlog', 'merge-branch-beside', 'merge-branch-ci-more', 'merge-branch-ci', 'merge-branch-merge', 'merge-branch-prune', 'merge-branch-rerun', 'merge-branch-watch']
  .map((name) => `src/Daoris.Cli/tool-test/${name}.e2e.ts`);

test('2: moving the e2e specs to a tier of their own (7fb9a4de0d) runs the spec that read the old tier name (9e402f39d7)', () => {
  const changed = ['tools/merge/heavy-tier.ts', 'tools/merge/reached.ts', 'tools/merge/plan.ts', 'tools/test-map.mjs', 'tools/merge/reach-table.ts'];
  const units = e2e({
    'tools/merge/preflight.e2e.ts': 1, 'tools/merge/reach-census.e2e.ts': 1, ...Object.fromEntries(FAR.map((file) => [file, 2])),
    'tools/merge/full-set.e2e.ts': 2, 'tools/merge/merge.e2e.ts': 2, 'tools/merge/reached-base.e2e.ts': 2, 'tools/merge/reached.e2e.ts': 2,
  });
  const { kept, deferred } = splitTier(units, 'fast', new Set(), { changed, hopsOf: (unit) => unit.hops });
  assert.ok(kept.some((unit) => unit.file === 'tools/merge/reached.e2e.ts'), 'the spec named for a changed file runs');
  assert.equal(kept.length + deferred.length, units.length, 'what the bound leaves is deferred, never dropped');
});

test('3: the census scope written under _fixtures (7d821b629b) runs the reach census spec it broke (9679302c50)', () => {
  const changed = ['tools/merge/census-scope.ts', 'tools/merge-branch/ci-steps.mjs', 'tools/merge/reach.ts', 'tools/merge/reach-table.ts'];
  const units = e2e({
    'tools/merge/reach-census.e2e.ts': 1, ...Object.fromEntries(FAR.map((file) => [file, 2])),
    'tools/merge/merge.e2e.ts': 2, 'tools/merge/preflight.e2e.ts': 2, 'tools/merge/reached-base.e2e.ts': 2, 'tools/merge/reached.e2e.ts': 2,
  });
  const { kept } = splitTier(units, 'fast', new Set(), { changed, hopsOf: (unit) => unit.hops });
  assert.ok(kept.some((unit) => unit.file === 'tools/merge/reach-census.e2e.ts'));
  assert.ok(splitTier(e2e({ 'tools/merge/reached.e2e.ts': 3 }), 'fast', new Set(), { changed, hopsOf: (unit) => unit.hops }).kept.length === 0, 'a spec far from the change waits for the run after the landing');
});

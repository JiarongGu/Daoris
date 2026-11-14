import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { readDeclared } from './plan.ts';
import { processSuitesOf, reachedCommands, reachedLines, reachedPlan, unitsOfMerge } from './reached.ts';
import type { ReachedUnit } from './reached.ts';
import { withReport } from './reached-reports.ts';
import { tiersOf } from './tiers.ts';
import { declared, repoRoot } from './repo-gates.ts';

// test-map: strings name paths as data — the units' files are the census's answers, given here as data

const processSuites = processSuitesOf(readDeclared(repoRoot));
const tiers = tiersOf(readFileSync(join(repoRoot, 'daoris.gates.json'), 'utf8'));
const [driverProcess, driverRehearsals] = declared('driver-process', 'driver-rehearsals');

// The units a merge's paths reach, as the census of test units answers (`unitsReached`): each with the path that reached it.
const DRIVER = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests';
const dotnet = (suite: string, name: string, by: string, project = DRIVER): ReachedUnit => ({
  suite, kind: 'dotnet', unit: `Daoris.Desktop.${name}`, name, file: `${project}/${name}.cs`, project, by,
});
const web = (file: string, by: string): ReachedUnit => ({ suite: 'web-unit', kind: 'vitest', unit: file, name: file.split('/').pop()!, file, by });
const node = (suite: string, file: string, by: string): ReachedUnit => ({ suite, kind: 'node', unit: file, name: file.split('/').pop()!, file, by });
const plan = (units: ReachedUnit[], changed: string[], options: { bounds?: { dotnet?: number }; replayed?: Record<string, number> } = {}) =>
  reachedPlan(units, changed, { ...options, processSuites });

test("the reached suites are the unit ones: the CLI's and the tools' tests are, the censuses, the Process halves, the browser suites and the rehearsals are not", () => {
  const units = [
    dotnet('driver', 'BacklogMergeTests', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'),
    dotnet('censuses', 'SourceCensusTests', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'),
    dotnet(driverProcess, 'IntegrateProcessTests', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'),
    web('src/Daoris.Web/src/work/WorkFrame.test.tsx', 'src/Daoris.Web/src/work/WorkFrame.tsx'),
    node('cli', 'src/Daoris.Cli/test/check.test.ts', 'src/Daoris.Cli/src/cli.ts'),
    node('tool-tests', 'src/Daoris.Cli/tool-test/merge-queue.test.ts', 'tools/merge-queue.mjs'),
    { suite: 'web', kind: 'playwright', unit: 'src/Daoris.Web/e2e/platform.spec.ts', name: 'platform.spec.ts', file: 'src/Daoris.Web/e2e/platform.spec.ts', by: 'x' },
    { suite: 'deployment', kind: 'rehearsal', unit: 'tools/deployment-rehearsal.mjs', name: 'deployment-rehearsal.mjs', file: 'tools/deployment-rehearsal.mjs', by: 'x' },
  ];
  const planned = plan(units, []);
  assert.deepEqual(planned.groups.map((group) => [group.runner, group.units.map((unit) => unit.name)]), [
    ['dotnet', ['BacklogMergeTests']],
    ['vitest', ['WorkFrame.test.tsx']],
    ['node', ['check.test.ts']],
    ['node', ['merge-queue.test.ts']],
  ]);
  assert.deepEqual(planned.left, {});
});

test('a suite reached past its bound runs the units nearest the change first and names the count left to the CI beside, never silently', () => {
  const changed = ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/CTests.cs'];
  const units = ['ATests', 'BTests', 'CTests', 'DTests'].map((name) => dotnet('driver', name, 'src/Daoris.Desktop/Daoris.Desktop.Driver/X.cs'));
  const planned = plan(units, changed, { bounds: { dotnet: 2 } });
  // The test file the change itself is in comes first, then the order the census gave.
  assert.deepEqual(planned.groups[0]!.units.map((unit) => unit.name), ['CTests', 'ATests']);
  assert.deepEqual(planned.left, { driver: 2 });
  assert.equal(planned.reached, 4);
  assert.match(reachedLines(planned).join('\n'), /driver: 2 of 4 reached units run; 2 are left to the CI beside/);
});

test('past its bound a suite keeps first the units named for the change, then those in its folder, since a class reaches by area and an area is wide', () => {
  const changed = ['src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'];
  const at = (name: string, folder: string): ReachedUnit => ({ ...dotnet('driver', name, changed[0]!), file: `${DRIVER}/${folder}/${name}.cs` });
  const units = [at('ATests', 'Agents'), at('BTests', 'Workplace'), at('BacklogMergeTests', 'Review'), at('CTests', 'Agents')];
  const planned = plan(units, changed, { bounds: { dotnet: 2 } });
  // The class named for the changed file, then the first of the changed file's own folder, then the census's order.
  assert.deepEqual(planned.groups[0]!.units.map((unit) => unit.name), ['BacklogMergeTests', 'BTests']);
});

test('a .NET suite runs as one filtered command per test project, its fast half only, and a page or tool suite by its own runner from its own folder', () => {
  const modules = 'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests';
  const units = [
    dotnet('driver', 'ATests', 'x'), dotnet('driver', 'BTests', 'x'), dotnet('modules', 'MTests', 'x', modules),
    web('src/Daoris.Web/src/work/WorkFrame.test.tsx', 'x'),
    node('tool-tests', 'src/Daoris.Cli/tool-test/merge-queue.test.ts', 'x'),
    { ...node('tool-tests', 'tools/merge/merge.e2e.ts', 'x'), kind: 'vitest' },
  ];
  const planned = plan(units, []);
  assert.deepEqual(planned.groups.map((group) => `${group.suite}/${group.runner}`), ['driver/dotnet', 'modules/dotnet', 'web-unit/vitest', 'tool-tests/vitest', 'tool-tests/node'], 'a suite of two runners is a group of each');
  const commands = reachedCommands(planned);
  assert.deepEqual(commands.map((each) => each.label), ['driver', 'modules', 'web-unit', 'tool-tests', 'tool-tests']);
  assert.deepEqual(commands[0], {
    label: 'driver', cwd: '.', command: 'dotnet',
    args: ['test', DRIVER, '--filter', '(Category!=Process)&(FullyQualifiedName~Daoris.Desktop.ATests|FullyQualifiedName~Daoris.Desktop.BTests)'],
  });
  assert.deepEqual(commands[1]!.args.slice(0, 2), ['test', modules]);
  assert.deepEqual(commands[2], {
    label: 'web-unit', cwd: 'src/Daoris.Web', command: 'node', args: ['node_modules/vitest/vitest.mjs', 'run', 'src/work/WorkFrame.test.tsx'],
  });
  assert.deepEqual(commands[3], {
    label: 'tool-tests', cwd: '.', command: 'node', args: ['node_modules/vitest/vitest.mjs', 'run', '--config', 'tools/vitest.config.ts', 'tools/merge/merge.e2e.ts'],
  });
  assert.deepEqual(commands[4], {
    label: 'tool-tests', cwd: 'src/Daoris.Cli', command: 'node',
    args: ['--test', '--test-timeout=1800000', '--test-force-exit', 'tool-test/merge-queue.test.ts'],
  });
});

test("a Process half's class is a unit of a merge only where a changed path is the code of the dependency it needs; every other is left to the night", () => {
  const needing = (name: string, needs: string[]): ReachedUnit => ({ ...dotnet(driverProcess, name, 'x'), needs, settings: 'src/Daoris.Desktop/process.runsettings' });
  const git = needing('GitProcessTests', ['git']);
  const harness = needing('HarnessProcessTests', ['harness']);
  const changed = ['src/Ports/Git/IGit.cs'];
  const kept = unitsOfMerge([dotnet('driver', 'ATests', 'x'), git, harness], changed, {
    tiers: { git: ['src/Ports/Git/'], harness: ['src/Ports/Agents/'] }, reaches: () => true, processSuites,
  });
  assert.deepEqual(kept.units.map((unit) => unit.name), ['ATests', 'GitProcessTests']);
  assert.deepEqual(kept.night, { harness: 1 });
  const planned = plan(kept.units, changed, { replayed: { driver: 5 } });
  const commands = reachedCommands(planned);
  assert.deepEqual(commands.map((each) => each.label), ['driver', driverProcess]);
  assert.deepEqual(commands[1]!.args, ['test', DRIVER, '--settings', 'src/Daoris.Desktop/process.runsettings', '--filter', '(FullyQualifiedName~Daoris.Desktop.GitProcessTests)']);
  assert.match(reachedLines(planned).join('\n'), /driver: 1 reached unit run; 5 replayed from a standing pass of the same inputs/);
});

test('a Process class with no dependency named is never a unit of a merge', () => {
  assert.deepEqual(plan([dotnet(driverProcess, 'XProcessTests', 'x')], []).groups, []);
});

test('the rehearsals project is a Process suite by what its gate runs: a git adapter change selects its class, an unrelated change does not', () => {
  assert.ok(processSuites.has(driverRehearsals), 'the declared gate runs with the Process settings');
  const rehearsals = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Rehearsals';
  const settings = 'src/Daoris.Desktop/process.runsettings';
  const unit: ReachedUnit = { ...dotnet(driverRehearsals, 'GitRehearsalTests', 'x', rehearsals), needs: ['git'], settings };
  const plain: ReachedUnit = dotnet(driverRehearsals, 'NoNeedTests', 'x', rehearsals);
  // The rule is the same one the Process halves follow: a unit of the suite is kept where a changed path is its dependency's code.
  const reaches = () => true;
  const adapter = unitsOfMerge([unit], ['src/Daoris.Ports/Git/IGitRunner.cs'], { tiers, reaches, processSuites });
  assert.deepEqual(adapter.units.map((each) => each.name), ['GitRehearsalTests']);
  const chosen = plan(adapter.units, ['src/Daoris.Ports/Git/IGitRunner.cs']);
  assert.deepEqual(reachedCommands(chosen).map((each) => [each.label, each.args.slice(0, 4)]), [[driverRehearsals, ['test', rehearsals, '--settings', settings]]]);
  const unrelated = unitsOfMerge([unit], ['docs/x.md'], { tiers, reaches, processSuites });
  assert.deepEqual(unrelated.units, []);
  assert.deepEqual(unrelated.night, { git: 1 });
  // A class naming no dependency is never selected, as for the Process halves.
  assert.deepEqual(plan([plain], []).groups, []);
});

test('a merge that changes nothing reaches nothing, and says the CI beside runs the whole suites', () => {
  const planned = plan([], []);
  assert.deepEqual(planned.groups, []);
  assert.match(reachedLines(planned).join('\n'), /no reached unit/);
});

test('a Vitest command writes its JSON report where the repository keeps timings, read from the folder it runs in', () => {
  const page = withReport({ cwd: 'src/Daoris.Web', command: 'node', args: ['node_modules/vitest/vitest.mjs', 'run', 'a.test.ts'] }, 'r-1');
  assert.deepEqual(page.args.slice(3), ['--reporter=default', '--reporter=json', '--outputFile.json=../../_fixtures/test-timing/r-1.json']);
  assert.equal(page.report, '_fixtures/test-timing/r-1.json');
  const tools = withReport({ cwd: '.', command: 'node', args: ['node_modules/vitest/vitest.mjs', 'run', '--config', 'tools/vitest.config.ts', 'x.e2e.ts'] }, 'r-2');
  assert.equal(tools.args.at(-1), '--outputFile.json=_fixtures/test-timing/r-2.json');
});

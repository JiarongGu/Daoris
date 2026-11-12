import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { join } from 'node:path';
import { repoRoot, tool } from '../support/merge-branch.ts';

// The units a merge's paths reach, as the census of test units answers (`unitsReached`): each with the path that reached it.
const DRIVER = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests';
const dotnet = (suite: string, name: string, by: string, project = DRIVER) => ({
  suite, kind: 'dotnet', unit: `Daoris.Desktop.${name}`, name, file: `${project}/${name}.cs`, project, by,
});
const web = (file: string, by: string) => ({ suite: 'web-unit', kind: 'vitest', unit: file, name: file.split('/').pop()!, file, by });
const node = (suite: string, file: string, by: string) => ({ suite, kind: 'node', unit: file, name: file.split('/').pop()!, file, by });

test('the reached suites are the light ones a merge can run in minutes: the CLI\'s verify, the censuses, the Process halves and the rehearsals are not among them', () => {
  const units = [
    dotnet('driver', 'BacklogMergeTests', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'),
    dotnet('censuses', 'SourceCensusTests', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'),
    dotnet('driver-process', 'IntegrateProcessTests', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'),
    web('src/Daoris.Web/src/work/WorkFrame.test.tsx', 'src/Daoris.Web/src/work/WorkFrame.tsx'),
    node('cli', 'src/Daoris.Cli/test/check.test.ts', 'src/Daoris.Cli/src/cli.ts'),
    node('tool-tests', 'src/Daoris.Cli/tool-test/merge-branch/reach.test.ts', 'tools/merge-branch/reach.mjs'),
    { suite: 'web', kind: 'playwright', unit: 'src/Daoris.Web/e2e/platform.spec.ts', name: 'platform.spec.ts', file: 'src/Daoris.Web/e2e/platform.spec.ts', by: 'x' },
    { suite: 'deployment', kind: 'rehearsal', unit: 'tools/deployment-rehearsal.mjs', name: 'deployment-rehearsal.mjs', file: 'tools/deployment-rehearsal.mjs', by: 'x' },
  ];
  const plan = tool.reachedPlan(units, []);
  assert.deepEqual(plan.groups.map((group) => [group.runner, group.units.map((unit) => unit.name)]), [
    ['dotnet', ['BacklogMergeTests']],
    ['vitest', ['WorkFrame.test.tsx']],
    ['node', ['reach.test.ts']],
  ]);
  assert.deepEqual(plan.left, {});
});

test('a suite reached past its bound runs the units nearest the change first and names the count left to the CI beside, never silently', () => {
  const changed = ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/CTests.cs'];
  const units = ['ATests', 'BTests', 'CTests', 'DTests'].map((name) => dotnet('driver', name, 'src/Daoris.Desktop/Daoris.Desktop.Driver/X.cs'));
  const plan = tool.reachedPlan(units, changed, { bounds: { dotnet: 2 } });
  // The test file the change itself is in comes first, then the order the census gave.
  assert.deepEqual(plan.groups[0]!.units.map((unit) => unit.name), ['CTests', 'ATests']);
  assert.deepEqual(plan.left, { driver: 2 });
  assert.equal(plan.reached, 4);
  assert.match(tool.reachedLines(plan).join('\n'), /driver: 2 of 4 reached units run; 2 are left to the CI beside/);
});

test('past its bound a suite keeps first the units named for the change, then those in its folder, since a class reaches by area and an area is wide', () => {
  const changed = ['src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/BacklogMerge.cs'];
  const at = (name: string, folder: string) => ({ ...dotnet('driver', name, changed[0]!), file: `${DRIVER}/${folder}/${name}.cs` });
  const units = [at('ATests', 'Agents'), at('BTests', 'Workplace'), at('BacklogMergeTests', 'Review'), at('CTests', 'Agents')];
  const plan = tool.reachedPlan(units, changed, { bounds: { dotnet: 2 } });
  // The class named for the changed file, then the first of the changed file's own folder, then the census's order.
  assert.deepEqual(plan.groups[0]!.units.map((unit) => unit.name), ['BacklogMergeTests', 'BTests']);
});

test('a .NET suite runs as one filtered command per test project, its fast half only, and a page or tool suite by its own runner from its own folder', () => {
  const modules = 'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests';
  const units = [
    dotnet('driver', 'ATests', 'x'), dotnet('driver', 'BTests', 'x'), dotnet('modules', 'MTests', 'x', modules),
    web('src/Daoris.Web/src/work/WorkFrame.test.tsx', 'x'),
    node('tool-tests', 'src/Daoris.Cli/tool-test/merge-branch/reach.test.ts', 'x'),
  ];
  const commands = tool.reachedCommands(tool.reachedPlan(units, []));
  assert.deepEqual(commands.map((each) => each.label), ['driver', 'modules', 'web-unit', 'tool-tests']);
  assert.deepEqual(commands[0], {
    label: 'driver', cwd: '.', command: 'dotnet',
    args: ['test', DRIVER, '--filter', '(Category!=Process)&(FullyQualifiedName~Daoris.Desktop.ATests|FullyQualifiedName~Daoris.Desktop.BTests)'],
  });
  assert.deepEqual(commands[1]!.args.slice(0, 2), ['test', modules]);
  assert.deepEqual(commands[2], {
    label: 'web-unit', cwd: '.', command: 'npm', args: ['--prefix', 'src/Daoris.Web', 'run', 'test', '--', 'src/work/WorkFrame.test.tsx'],
  });
  assert.deepEqual(commands[3], {
    label: 'tool-tests', cwd: 'src/Daoris.Cli', command: 'node',
    args: ['--test', '--test-timeout=1800000', '--test-force-exit', 'tool-test/merge-branch/reach.test.ts'],
  });
});

test('a merge that changes nothing reaches nothing, and says the CI beside runs the whole suites', () => {
  const plan = tool.reachedPlan([], []);
  assert.deepEqual(plan.groups, []);
  assert.match(tool.reachedLines(plan).join('\n'), /no reached unit/);
});

test('the tool reads the census of this repository: a changed tool reaches the tool-test that holds it, --plan runs nothing, and a clean checkout reaches nothing', () => {
  const run = (...args: string[]) => spawnSync(process.execPath, [join(repoRoot, 'tools', 'reached-checks.mjs'), ...args], { cwd: repoRoot, encoding: 'utf8', windowsHide: true });
  const planned = run('--plan', '--paths', 'tools/merge-branch/reached.mjs');
  assert.equal(planned.status, 0, planned.stderr);
  assert.match(planned.stdout, /reached: tool-tests: (\d+ of )?\d+ reached units? run/);
  assert.match(planned.stdout, /tool-tests: node --test .*tool-test\/merge-branch\/reached\.test\.ts/);
  // No path changed, none reached.
  const none = run('--plan', '--paths', '');
  assert.equal(none.status, 0, none.stderr);
  assert.match(none.stdout, /no reached unit/);
});

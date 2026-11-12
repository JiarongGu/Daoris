import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { repoRoot, tool, refusal, trackedFiles } from './support/merge-branch.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


// ---------------------------------------------------------------------------------------------------
// The lane map

test('a lane path is a glob: * stays in a folder, ** crosses folders, {a,b} is either', () => {
  const deep = tool.globToRegExp('src/Daoris.Web/src/settings/**');
  assert.ok(deep.test('src/Daoris.Web/src/settings/PluginsDomain.tsx'));
  assert.ok(deep.test('src/Daoris.Web/src/settings/deeper/x.ts'));
  assert.ok(!deep.test('src/Daoris.Web/src/settingsX/y.ts'));

  const shallow = tool.globToRegExp('src/Daoris.Web/src/SettingsView*.tsx');
  assert.ok(shallow.test('src/Daoris.Web/src/SettingsView.shell.test.tsx'));
  assert.ok(!shallow.test('src/Daoris.Web/src/settings/SettingsView.tsx'));

  const either = tool.globToRegExp('src/App{,.test}.tsx');
  assert.ok(either.test('src/App.tsx') && either.test('src/App.test.tsx') && !either.test('src/App.stories.tsx'));
  assert.ok(tool.globToRegExp('a.b').test('a.b') && !tool.globToRegExp('a.b').test('axb'), 'a dot is a dot');
});


test("a lane's `!` path carves a narrower lane's paths out of a wider one (LEFT1)", () => {
  const shell = tool.laneMatcher(['src/Daoris.Web/**', '!src/Daoris.Web/src/settings/**', '!src/Daoris.Web/src/SettingsView*.tsx']);
  assert.ok(shell('src/Daoris.Web/src/queries.ts'));
  assert.ok(shell('src/Daoris.Web/vite.config.ts'));
  assert.ok(shell('src/Daoris.Web/src/settingsX/y.ts'), 'a carve-out is its own glob, not a prefix');
  assert.ok(!shell('src/Daoris.Web/src/settings/DriverDomain.tsx'));
  assert.ok(!shell('src/Daoris.Web/src/SettingsView.tsx'));
  assert.ok(!shell('src/Daoris.Cli/src/cli.ts'));
  // A lane of carve-outs alone owns nothing.
  assert.ok(!tool.laneMatcher(['!src/**'])('README.md'));
});


test("a branch's files are placed in lanes, the steward's lane, the shared records, no lane, or outside every lane", () => {
  const map = tool.readLanes(repoRoot)!;
  const placed = tool.classify([
    'src/Daoris.Web/src/settings/PluginsDomain.tsx',
    'src/Daoris.Web/src/locales/zh/settings.ai.json',
    'src/Daoris.Web/src/locales/en/work.panel.json',
    'src/Daoris.Web/src/queries.ts',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/HelpRoom.cs',
    'src/Daoris.Devkit/Daoris.Devkit.Core/Gates.cs',
    'TASKS.md',
    'docs/task-archive.md',
    'daoris.lanes.json',
    'CHANGELOG.md',
    'docs/2026-09-30-parallel-development-design.md',
    'src/Daoris.Somewhere/Program.cs',
  ], { ...map, union: tool.unionRecords(repoRoot) });

  assert.deepEqual(placed.lanes, [
    { id: 'web-shell', title: 'Web shell', files: ['src/Daoris.Web/src/locales/en/work.panel.json', 'src/Daoris.Web/src/queries.ts'] },
    { id: 'web-settings', title: 'Web settings', files: ['src/Daoris.Web/src/settings/PluginsDomain.tsx', 'src/Daoris.Web/src/locales/zh/settings.ai.json'] },
    { id: 'driver', title: 'Driver library', files: ['src/Daoris.Desktop/Daoris.Desktop.Driver/Help/HelpRoom.cs'] },
    { id: 'modules', title: 'Desktop modules', files: ['src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs', 'src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs'] },
    { id: 'tools', title: 'Tools', files: ['src/Daoris.Devkit/Daoris.Devkit.Core/Gates.cs'] },
  ]);
  // The steward's lane holds the parent's list and the map itself. The archive merges
  // by union too, but it is the steward's, and that is what the parent needs to hear.
  assert.deepEqual(placed.steward, ['TASKS.md', 'docs/task-archive.md', 'daoris.lanes.json']);
  assert.deepEqual(placed.shared, ['CHANGELOG.md']);
  // A document belongs to no lane on purpose; a new source tree is a path nothing placed.
  assert.deepEqual(placed.laneless, ['docs/2026-09-30-parallel-development-design.md']);
  assert.deepEqual(placed.outside, ['src/Daoris.Somewhere/Program.cs']);
});


test("the lane map's lanes are the design's §5 lanes, by id and title", () => {
  const design = readFileSync(join(repoRoot, 'docs', '2026-09-30-parallel-development-design.md'), 'utf8').replace(/\r\n/g, '\n');
  const section = design.slice(design.indexOf('## 5. The lane map'), design.indexOf('## 6.'));
  const rows = section.split('\n')
    .filter((line) => line.startsWith('|') && !/^\|\s*-/.test(line))
    .map((line) => line.split('|').slice(1, 3).map((cell) => cell.trim().replace(/^`|`$/g, '')))
    .filter(([id]) => id !== 'Lane');
  assert.ok(rows.length >= 8, `the design's lane table was not found: ${rows.map(([id]) => id).join(', ')}`);
  assert.deepEqual(tool.readLanes(repoRoot)!.lanes.map((lane) => [lane.id, lane.title]), rows);
});


/**
 * The map is the repository's declaration (`docs/2026-10-01-self-development-design.md` §2.1),
 * and what makes it unreadable is a rule, so the driver's reader can hold the same table.
 */
test('the lanes file is unreadable for a bad or repeated id, a lane with no paths, two stewards, or an undeclared gate', () => {
  const lane = (id: string, more: Record<string, unknown> = {}) => ({ id, title: id, summary: `the ${id} lane`, paths: [`${id}/**`], ...more });
  assert.deepEqual(tool.lanesProblems({ lanes: [lane('web-shell'), lane('records', { steward: true, gates: ['cli'] })] }, ['cli']), []);
  assert.deepEqual(tool.lanesProblems({ lanes: [] }, []), [], 'an empty list is no lanes, not a fault');
  assert.deepEqual(tool.lanesProblems({ laneless: [] }, []), [], 'no list is no lanes, not a fault');

  const cases: [unknown, RegExp][] = [
    [[], /not a JSON object/],
    [{ lanes: {} }, /'lanes' is not a list/],
    [{ lanes: [lane('Web')] }, /lane 'Web': its id must be lower-case letters, digits and dashes, starting with a letter/],
    [{ lanes: [lane('2d')] }, /lane '2d': its id must/],
    [{ lanes: [{ title: 'No id', paths: ['x/**'] }] }, /lane 1: its id must/],
    [{ lanes: [lane('cli'), lane('cli')] }, /lane 'cli': the id is used twice/],
    [{ lanes: [lane('cli', { paths: [] })] }, /lane 'cli': it has no paths/],
    [{ lanes: [lane('cli', { paths: ['!src/**'] })] }, /lane 'cli': it has no paths/],
    [{ lanes: [lane('a', { steward: true }), lane('b', { steward: true })] }, /two stewards \(a, b\)/],
    [{ lanes: [lane('records', { gates: ['cli', 'nope'] })] }, /lane 'records': its gate 'nope' is not declared in daoris\.gates\.json/],
    [{ lanes: [lane('records', { gates: 'cli' })] }, /lane 'records': 'gates' is not a list/],
    [{ lanes: [lane('cli')], laneless: {} }, /'laneless' is not a list/],
  ];
  for (const [parsed, expected] of cases) {
    const problems = tool.lanesProblems(parsed, ['cli']);
    assert.ok(problems.some((problem) => expected.test(problem)), `${JSON.stringify(parsed)}: ${problems.join('; ') || 'no problem found'}`);
  }
});


test('no lanes file is no lanes; an unreadable one is refused with exit 2, naming each problem', () => {
  const fx = makeFixture('merge-branch-lanes-file');
  assert.equal(tool.readLanes(fx.root), null);
  fx.write('daoris.gates.json', JSON.stringify({ gates: [{ name: 'cli', run: 'npm test' }] }));
  fx.write(tool.LANES_FILE, JSON.stringify({ lanes: [] }));
  assert.deepEqual(tool.readLanes(fx.root), { lanes: [], laneless: [] });

  fx.write(tool.LANES_FILE, '{ "lanes": [');
  let refused = refusal(() => tool.readLanes(fx.root));
  assert.equal(refused.exitCode, 2);
  assert.match(refused.message, /daoris\.lanes\.json is not valid JSON/);

  fx.write(tool.LANES_FILE, JSON.stringify({ lanes: [
    { id: 'cli', title: 'CLI', summary: 's', paths: [] },
    { id: 'records', title: 'Records', summary: 's', steward: true, paths: ['TASKS.md'], gates: ['cli', 'web'] },
  ] }));
  refused = refusal(() => tool.readLanes(fx.root));
  assert.equal(refused.exitCode, 2);
  assert.match(refused.message, /daoris\.lanes\.json cannot be read:\n\s+lane 'cli': it has no paths\n\s+lane 'records': its gate 'web' is not declared/);
  fx.cleanup();
});


test("this repository's lanes each carry an id, a title and a one-line summary, and one steward keeps the records", () => {
  const { lanes } = tool.readLanes(repoRoot)!;
  for (const lane of lanes) {
    assert.ok(lane.title.trim(), `${lane.id}: no title`);
    assert.ok(lane.summary.trim() && !lane.summary.includes('\n'), `${lane.id}: its summary is one line, for the people and agents that address the lane`);
  }
  assert.deepEqual(lanes.map((lane) => lane.id), ['web-shell', 'web-settings', 'driver', 'modules', 'service', 'cli', 'tools', 'records']);
  const stewards = lanes.filter((lane) => lane.steward);
  assert.deepEqual(stewards.map((lane) => [lane.id, lane.paths, lane.gates]),
    [['records', ['TASKS.md', 'docs/task-archive.md', 'daoris.lanes.json'], ['universal', 'cli']]]);
  // A code lane never narrows its gates: a "web only" batch skips the .NET suites.
  assert.deepEqual(lanes.filter((lane) => lane.gates !== undefined).map((lane) => lane.id), ['records']);
});


test('every lane path and every laneless path matches a tracked file, and no tracked file has two places', () => {
  const files = trackedFiles();
  const { lanes, laneless } = tool.readLanes(repoRoot)!;

  // A carve-out (`!`) that matches nothing carves nothing, and is as stale as a path that points at nothing.
  for (const [id, globs] of [...lanes.map((lane) => [lane.id, lane.paths] as const), ['laneless', laneless] as const]) {
    for (const glob of globs) {
      const pattern = tool.globToRegExp(glob.replace(/^!/, ''));
      assert.ok(files.some((file) => pattern.test(file)), `${id}: '${glob}' matches no tracked file — the map points at nothing`);
    }
  }
  // The steward's lane is a lane like the rest here: its records are carved out of the docs, never
  // left to the classification order to settle.
  const owns = lanes.map((lane) => ({ id: lane.id, owns: tool.laneMatcher(lane.paths) }));
  const declared = tool.laneMatcher(laneless);
  for (const file of files) {
    const owners = owns.filter((lane) => lane.owns(file)).map((lane) => lane.id);
    assert.ok(owners.length <= 1, `${file} is in two lanes: ${owners.join(', ')}`);
    assert.ok(!(owners.length && declared(file)), `${file} is in ${owners[0]} and declared laneless`);
  }
});


/**
 * A path outside every lane would sit there unremarked, and a new source tree with it.
 * Every tracked file has a place: a lane (the steward's among them), a record that
 * merges by union, or the map's `laneless` list (the docs, the doctrine, the harness's settings). A new
 * top-level path fails here until the map places it on purpose.
 */
test('every tracked file has a place, so a new path is placed on purpose, never left outside silently', () => {
  const map = tool.readLanes(repoRoot)!;
  const placed = tool.classify(trackedFiles(), { ...map, union: tool.unionRecords(repoRoot) });
  assert.deepEqual(placed.outside, [],
    `outside every lane: ${placed.outside.slice(0, 12).join(', ')}${placed.outside.length > 12 ? ', …' : ''}\n`
    + '  Give each a lane in daoris.lanes.json, or list it under "laneless" with the reason no lane owns it.');
  // The laneless list names files, never a source tree: a `src/**` there would place everything and
  // guard nothing.
  for (const glob of map.laneless.filter((path) => /^(src|tools)\//.test(path))) {
    assert.ok(!glob.includes('*'), `laneless: '${glob}' is a glob over a source tree; give that tree a lane`);
  }
});
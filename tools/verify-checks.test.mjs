import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { CHANGED_VARIABLE, changedEnvironment, changedPaths, withChanged } from './merge/gate-hands.ts';
import { treeOf, tsReader } from './test-map.mjs';
import { verifyChecks } from './verify-checks.mjs';
import { GATE_START, withGateStart } from './time-ratchet.mjs';
import { stepReadFailures } from './verify-checks/reads.mjs';
import { runSteps } from './verify-checks/run.mjs';
import { CHECK_STEPS, selectSteps } from './verify-checks/steps.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const GATES = ['cli', 'universal'];
const runs = (steps) => steps.map((step) => step.run);
const named = (part) => CHECK_STEPS.filter((step) => step.run.includes(part)).map((step) => step.run);
const DEVKIT = named('Daoris.Devkit.Cli')[0];

test('with no changed paths every step runs, the devkit\'s universal gates among them', () => {
  const chosen = selectSteps(CHECK_STEPS, null, { gates: GATES });
  assert.deepEqual(runs(chosen.run), runs(CHECK_STEPS));
  assert.deepEqual(chosen.left, []);
});

test('a docs-only change runs the documents\' checks and what reads every path, and leaves the code\'s', () => {
  const chosen = runs(selectSteps(CHECK_STEPS, ['docs/development.md'], { gates: GATES }).run);
  for (const doc of ['doc-budgets', 'doc-current', 'doc-shapes', 'doc-duplicates', 'doc-system', 'doc-links', 'release-prep']) {
    assert.ok(chosen.some((run) => run.includes(`tools/${doc}.mjs`)), doc);
  }
  for (const left of ['typecheck', 'comment-gate', 'size-ratchet', 'partials', 'read-shares', 'optional-locks', 'live-logins', 'console-windows', 'tool-test-census', 'contracts.mjs']) {
    assert.ok(!chosen.some((run) => run.includes(left)), left);
  }
  assert.ok(!chosen.includes(DEVKIT), 'the universal gate runs the devkit\'s check in the merge');
});

test('the gate makes the generated contracts current itself before it checks them, whichever driver runs it', () => {
  const writers = CHECK_STEPS.filter((step) => step.writes).map((step) => step.run);
  assert.deepEqual(writers, ['node tools/orient-index.mjs', 'node tools/contracts.mjs --if-stale']);
  assert.ok(runs(selectSteps(CHECK_STEPS, ['src/Daoris.Contracts/a.cs'], { gates: GATES }).run).includes('node tools/contracts.mjs --if-stale'));
});

test('a web-only change runs the source checks that read the page, and leaves the typechecks and the documents\' checks', () => {
  const chosen = runs(selectSteps(CHECK_STEPS, ['src/Daoris.Web/src/pages/Home.tsx'], { gates: GATES }).run);
  for (const reads of ['comment-gate.mjs', 'size-ratchet', 'console-windows', 'test-patience', 'orient-index', 'doc-current', 'tool-test-census']) {
    assert.ok(chosen.some((run) => run.includes(reads)), reads);
  }
  for (const left of ['typecheck', 'doc-budgets', 'doc-shapes', 'doc-duplicates', 'doc-system', 'partials', 'read-shares', 'optional-locks', 'contracts.mjs']) {
    assert.ok(!chosen.some((run) => run.includes(left)), left);
  }
});

test('a step whose aside gate the repository does not declare runs whatever the paths', () => {
  assert.ok(runs(selectSteps(CHECK_STEPS, ['docs/development.md'], { gates: ['cli'] }).run).includes(DEVKIT));
});

test('the writers run first and alone, then the rest side by side up to the width, each started in its order', async () => {
  const events = [];
  let open = 0;
  let most = 0;
  const run = async (command) => {
    open += 1;
    most = Math.max(most, open);
    events.push(`start ${command}`);
    await new Promise((resolve) => setTimeout(resolve, 5));
    events.push(`end ${command}`);
    open -= 1;
    return { status: command === 'c' ? 1 : 0, output: '' };
  };
  const steps = [{ run: 'a' }, { run: 'w', writes: true }, { run: 'b' }, { run: 'c' }, { run: 'd' }];
  const ran = await runSteps(steps, { run, width: 2 });
  assert.deepEqual(events.slice(0, 2), ['start w', 'end w'], 'the writer ends before any other step starts');
  assert.deepEqual(events.filter((each) => each.startsWith('start')).slice(1), ['start a', 'start b', 'start c', 'start d']);
  assert.equal(most, 2, 'two at once, never more');
  assert.deepEqual(ran.map((each) => [each.step.run, each.status]), [['a', 0], ['w', 0], ['b', 0], ['c', 1], ['d', 0]]);
});

test('the gate runs what the handed paths reach, says every red at the end and exits 1', async () => {
  const seen = [];
  const lines = [];
  const steps = [
    { run: 'node tools/doc-x.mjs', reads: ['docs/'] },
    { run: 'node tools/code-x.mjs', reads: ['tools/'] },
    { run: 'node tools/all-x.mjs', reads: '*' },
  ];
  const env = changedEnvironment(['docs/a.md']);
  const run = (command) => {
    seen.push(command);
    return { status: command.includes('all') ? 1 : 0, output: `${command} said\n` };
  };
  const code = await verifyChecks({ root, env, steps, run, say: (line) => lines.push(line) });
  assert.equal(code, 1);
  assert.deepEqual(seen.sort(), ['node tools/all-x.mjs', 'node tools/doc-x.mjs']);
  const said = lines.join('\n');
  assert.match(said, /2 of 3 steps, by the 1 path the merge changes/);
  assert.match(said, /left: node tools\/code-x\.mjs: no changed path is one it reads/);
  assert.match(said, /RED node tools\/all-x\.mjs \(exit 1\)$/);
});

test('handed no paths the gate runs every step', async () => {
  const seen = [];
  const steps = [{ run: 'one', reads: ['docs/'] }, { run: 'two', reads: ['tools/'] }];
  const code = await verifyChecks({ root, env: {}, steps, run: (command) => (seen.push(command), { status: 0, output: '' }), say: () => {} });
  assert.equal(code, 0);
  assert.deepEqual(seen, ['one', 'two']);
});

test('a gate run hands its steps its own start, so a time ratchet judges only the records since it began', () => {
  assert.deepEqual(withGateStart({ A: '1' }, 5000), { A: '1', [GATE_START]: '5000' });
  const outer = { [GATE_START]: '1000' };
  assert.equal(withGateStart(outer, 5000), outer, 'an enclosing gate run\'s earlier start stands');
});

test('the changed paths reach a step through its environment, and a list too long for one variable is not handed', async () => {
  assert.deepEqual(changedPaths(changedEnvironment(['a/b.md', 'c.ts'])), ['a/b.md', 'c.ts']);
  assert.deepEqual(changedEnvironment(null), {});
  assert.deepEqual(changedEnvironment(Array.from({ length: 2000 }, (_, i) => `src/Daoris.Desktop/a/long/path/to/file-${i}.cs`)), {});
  assert.equal(changedPaths({ [CHANGED_VARIABLE]: '{"not":"a list"}' }), null);
  const handed = [];
  const step = async (command, cwd, fd, options = {}) => handed.push(options.env ?? null);
  await withChanged(step, ['docs/a.md'])('x', '.', 1, { env: { KEPT: '1' } });
  await withChanged(step, null)('x', '.', 1, {});
  assert.deepEqual(handed, [{ KEPT: '1', [CHANGED_VARIABLE]: '["docs/a.md"]' }, null]);
});

test('every step declares what its code reads, and a step that reads an undeclared path fails the census', () => {
  const tree = treeOf(root);
  assert.deepEqual(stepReadFailures(CHECK_STEPS, { ...tree, reader: tsReader(tree.files, tree.read) }), []);
  const files = ['tools/a.mjs', 'tools/b.json', 'tools/fsx.mjs'];
  const texts = { 'tools/a.mjs': "import { x } from './fsx.mjs';\nexport const y = readFileSync('tools/b.json');\n", 'tools/fsx.mjs': 'export const x = 1;\n', 'tools/b.json': '{}' };
  const read = (file) => texts[file] ?? '';
  const context = { files, read, reader: tsReader(files, read) };
  const failures = stepReadFailures([
    { run: 'node tools/a.mjs', reads: ['tools/a.mjs', 'tools/fsx.mjs'] },
    { run: 'node tools/a.mjs --all', reads: ['tools/'] },
    { run: 'dotnet run --project x', reads: ['x/'] },
  ], context);
  assert.equal(failures.length, 2, failures.join('\n'));
  assert.match(failures[0], /node tools\/a\.mjs reads 1 path\(s\) it does not declare: tools\/b\.json/);
  assert.match(failures[1], /dotnet run --project x: a command no census here reads, so it declares '\*'/);
});

test('npm run verify:checks is the runner, so the gate and quick read one list', () => {
  const scripts = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).scripts;
  assert.equal(scripts['verify:checks'], 'node tools/verify-checks.mjs');
});

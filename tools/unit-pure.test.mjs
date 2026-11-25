/**
 * The unit-purity census (a unit test starts no process, reads no git and waits on no wall clock, unless a list that only falls
 * names the file with its row).
 *
 *   node --test tools/unit-pure.test.mjs
 *
 * `readActs` reads one test file's text; `checkUnitPure` reads a tree. Each tree case builds a small tree in its own folder of
 * `_fixtures/` with a `daoris.gates.json` declaring the sets and a list, and holds the verdict; the real tree passes.
 */
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkUnitPure, measureUnitPure, readActs, shrinkUnitPure, unitPureFinds } from './unit-pure.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, '_fixtures', 'unit-pure-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const call = (name, args = '') => `${name}(${args})`;

const UNIT = { name: 'tools', gate: 'cli-tests', runner: 'node', files: ['tools/*.test.mjs'] };
const WEB = { name: 'web-unit', gate: 'web-unit', runner: 'vitest', cwd: 'src/Daoris.Web', files: ['src/Daoris.Web/src/**/*.test.{ts,tsx}'] };
const E2E = { name: 'tool-e2e', gate: 'tool-e2e', runner: 'node', files: ['src/Daoris.Cli/tool-test/**/*.e2e.ts'] };
const SPEC = { name: 'web', gate: 'web', runner: 'playwright', files: ['src/Daoris.Web/e2e/**/*.spec.ts'] };

const STARTS = [
  `import { ${'execFileSync'}, spawnSync } from 'node:child_process';`,
  `${call('spawnSync', "'git', ['init']")}; // ${call('spawnSync')}`,
  `const text = "${call('execFileSync')}";`,
  `const out = ${call('execFileSync', "'git', ['status']")};`,
  `const made = fixture.${call('spawnGit', "['init']")};`,
  `function ${call('local')} {}`,
].join('\n');

let n = 0;
/** A fresh tree holding `files` (path to text), the declared `sets` and the `list` file's text; returns its root. */
function tree({ files = {}, sets = [UNIT, WEB, E2E, SPEC], list = { files: {} } }) {
  const dir = join(scratch, `t${n++}`);
  const put = (rel, text) => {
    mkdirSync(dirname(join(dir, rel)), { recursive: true });
    writeFileSync(join(dir, rel), text);
  };
  for (const [file, text] of Object.entries(files)) put(file, text);
  put('daoris.gates.json', JSON.stringify({ gates: [], tests: { sets } }));
  put('tools/unit-pure.json', `${JSON.stringify(list, null, 2)}\n`);
  return dir;
}

test('the tree passes: every unit test that starts, reads git or waits is on the list at no less than it does', () => {
  assert.deepEqual(checkUnitPure(root).problems, []);
});

test('a process start is read by name and line: a call by its bare name, not a method, a string, a comment or a name the file declares', () => {
  const acts = readActs(STARTS);
  assert.deepEqual(acts.process.map((site) => [site.line, site.name]), [[2, 'spawnSync'], [4, 'execFileSync']]);
  assert.deepEqual(acts.git.map((site) => [site.line, site.name]), [], 'a method call is no start of the file\'s');
  assert.equal(readActs(`const made = ${call('spawnSync', "'x'")};`).process.length, 0, 'a file that never imports child_process starts nothing by that name');
});

test('git is read in the helpers that start it, a local helper of that name counting only where the file itself starts processes', () => {
  const read = [
    `import { repositoryFiles } from './fsx.mjs';`,
    `const files = ${call('repositoryFiles', 'root')};`,
    `const top = ${call('git', "root, ['rev-parse']")};`,
    `const sight = ${call('gitSight', 'root')};`,
    `const reader = ${call('sourceReader', 'root')};`,
  ].join('\n');
  assert.deepEqual(readActs(read).git.map((site) => site.name), ['repositoryFiles', 'git', 'gitSight', 'sourceReader']);
  const local = [`function ${call('git', 'args')} { return args; }`, `const x = ${call('git', "['a']")};`].join('\n');
  assert.deepEqual(readActs(local).git, [], 'a graph layout\'s own git() starts nothing');
  const real = [`import { execFileSync } from 'node:child_process';`, `const git = (...a) => ${call('execFileSync', "'git', a")};`, `${call('git', "['init']")};`].join('\n');
  assert.deepEqual(readActs(real).git.map((site) => site.line), [3], 'the helper that does start counts at its calls');
});

test('a wall-clock wait is a timer call outside fake timers', () => {
  assert.deepEqual(readActs(`await ${call('sleep', '50')};\n${call('setTimeout', 'done, 5')};`).clock.map((site) => site.name), ['sleep', 'setTimeout']);
  assert.deepEqual(readActs(`vi.useFakeTimers();\n${call('setTimeout', 'done, 5')};`).clock, []);
  assert.deepEqual(readActs(`await new Promise((r) => ${'setTimeout'}(r, 0));
await new Promise((r) => ${'setTimeout'}(r));`).clock, [], 'a timer for no time is a yield to the loop, not a wait');
  assert.deepEqual(readActs(`await new Promise((r) => ${'setTimeout'}(r, 20));`).clock.map((site) => site.name), ['setTimeout'], 'a timer for time is');
  assert.deepEqual(readActs(`const note = "${call('sleep')}"; // ${call('setTimeout')}`).clock, []);
});

test('a unit test with a start the list does not name is named by file, line and act; the same text in a real-dependency set is not', () => {
  const dir = tree({ files: { 'tools/a.test.mjs': STARTS, 'tools/b.test.mjs': '', 'src/Daoris.Cli/tool-test/c.e2e.ts': STARTS, 'src/Daoris.Web/e2e/d.spec.ts': STARTS } });
  const { problems } = checkUnitPure(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^tools\/a\.test\.mjs:2: starts a process 2 times \(lines 2, 4\), over its allowance of 0 in a unit test/);
  assert.match(problems[0], /move the case to a real-dependency set \(a `\.e2e\.ts` of tool-e2e, a Playwright spec\), or hand the code under test a seam of its own/);
});

test('a count over the list is named, a count under it says to lower it, and an entry on a file with none is named too', () => {
  const list = { files: { 'tools/a.test.mjs': { process: 1, row: 'UNITPURE2' }, 'tools/gone.test.mjs': { process: 3, row: 'UNITPURE2' }, 'tools/b.test.mjs': { clock: 2, row: 'UNITPURE2' } } };
  const dir = tree({ files: { 'tools/a.test.mjs': STARTS, 'tools/b.test.mjs': `${call('sleep', '1')};` }, list });
  const { problems } = checkUnitPure(dir);
  assert.deepEqual(problems.map((each) => each.split(':')[0]), ['tools/a.test.mjs', 'tools/b.test.mjs', 'tools/gone.test.mjs']);
  assert.match(problems[0], /starts a process 2 times .* over its allowance of 1/);
  assert.match(problems[1], /^tools\/b\.test\.mjs: clock fell to 1 from its allowance of 2: lower the list/);
  assert.match(problems[2], /no longer a unit test that does any of it.*--shrink/);
});

test('a list entry names its row, and a row that is missing or not one is named', () => {
  const list = { files: { 'tools/a.test.mjs': { process: 2 }, 'tools/b.test.mjs': { clock: 1, row: 'someday' } } };
  const dir = tree({ files: { 'tools/a.test.mjs': STARTS, 'tools/b.test.mjs': `${call('sleep', '1')};` }, list });
  const { problems } = checkUnitPure(dir);
  assert.equal(problems.length, 2);
  assert.match(problems[0], /^tools\/unit-pure\.json: tools\/a\.test\.mjs names no row/);
  assert.match(problems[1], /^tools\/unit-pure\.json: tools\/b\.test\.mjs's row someday is no row id/);
});

test('a row the backlog no longer holds is named, so a finished row leaves no entry behind', () => {
  const list = { files: { 'tools/a.test.mjs': { process: 2, row: 'UNITPURE2' } } };
  const dir = tree({ files: { 'tools/a.test.mjs': STARTS, 'TASKS.md': '- [ ] **OTHERROW1 — something** (tools)\n' }, list });
  const { problems } = checkUnitPure(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /tools\/a\.test\.mjs's row UNITPURE2 is not in TASKS\.md/);
  writeFileSync(join(dir, 'TASKS.md'), '- [ ] **UNITPURE2 — the rest** (tools)\n');
  assert.deepEqual(checkUnitPure(dir).problems, []);
});

test('--shrink lowers a count that fell and drops an entry that fell to none, and raises nothing', () => {
  const list = { files: { 'tools/a.test.mjs': { process: 5, git: 1, row: 'UNITPURE2' }, 'tools/b.test.mjs': { clock: 4, row: 'UNITPURE2' }, 'tools/c.test.mjs': { process: 1, row: 'UNITPURE2' } } };
  const dir = tree({ files: { 'tools/a.test.mjs': STARTS, 'tools/b.test.mjs': '', 'tools/c.test.mjs': `import { spawn } from 'node:child_process';\n${call('spawn', "'a'")};\n${call('spawn', "'b'")};` }, list });
  const lowered = shrinkUnitPure(dir);
  assert.deepEqual(lowered, ['tools/a.test.mjs process: 5 to 2', 'tools/a.test.mjs git: 1 to none', 'tools/b.test.mjs: no longer any of it'].sort());
  const after = JSON.parse(readFileSync(join(dir, 'tools', 'unit-pure.json'), 'utf8'));
  assert.deepEqual(after.files, { 'tools/a.test.mjs': { process: 2, row: 'UNITPURE2' }, 'tools/c.test.mjs': { process: 1, row: 'UNITPURE2' } }, 'c rose to 2 and is left at 1: a rise is a breach for a person');
  assert.deepEqual(shrinkUnitPure(dir), [], 'a second shrink changes nothing');
});

test('the finds are each unit test with what it did, largest first, by domain and act', () => {
  const dir = tree({ files: { 'tools/a.test.mjs': STARTS, 'tools/b.test.mjs': `${call('sleep', '1')}; ${call('sleep', '2')};`, 'src/Daoris.Web/src/x/x.test.ts': `import { spawnSync } from 'node:child_process';\n${call('spawnSync', "'a'")};` } });
  const finds = unitPureFinds(dir);
  assert.deepEqual(finds.map((each) => [each.file, each.act, each.count]), [
    ['tools/a.test.mjs', 'process', 2], ['tools/b.test.mjs', 'clock', 2], ['src/Daoris.Web/src/x/x.test.ts', 'process', 1],
  ]);
  const measured = measureUnitPure(dir);
  assert.deepEqual(measured.groups.map((each) => [each.domain, each.act, each.files, each.sites]), [['tools', 'process', 1, 2], ['tools', 'clock', 1, 2], ['src/Daoris.Web/src/x', 'process', 1, 1]]);
});

test('the merge tool\'s unit project is held as it was: a git helper it imports and calls is named, the same call in its end-to-end project is not', () => {
  const unit = { name: 'merge-unit', gate: 'cli-tests', runner: 'vitest', config: 'tools/vitest.config.ts', project: 'unit', files: ['tools/merge/**/*.test.ts'] };
  const e2e = { name: 'merge-e2e', gate: 'tool-e2e', runner: 'vitest', config: 'tools/vitest.config.ts', project: 'e2e', files: ['tools/merge/**/*.e2e.ts'] };
  const text = [`import { git, gitSight } from './git.ts';`, `const top = ${call('git', "root, ['rev-parse']")};`, `const sight = ${call('gitSight', 'root')};`].join('\n');
  const dir = tree({ files: { 'tools/merge/a.test.ts': text, 'tools/merge/b.e2e.ts': text }, sets: [unit, e2e] });
  const { problems } = checkUnitPure(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^tools\/merge\/a\.test\.ts:2: reads git through a helper that starts it 2 times \(lines 2, 3\)/);
});

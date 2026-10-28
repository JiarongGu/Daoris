/**
 * TESTPATIENCE3: the test-patience census (FIX-LOG 2026-10-28, TESTPATIENCE3).
 *
 *   node --test tools/test-patience.test.mjs
 *
 * `checkTestPatience` reads the three test trees under a root. Each case builds a small tree in a gitignored scratch folder of the
 * repository and holds the verdict: a fixed wait of a second or more and under the patience is refused in every shape a wait is
 * written, naming its line; one at the patience or past it, one under a second, one that is no number, a word in a comment or a
 * string, and a marked one pass. The cases after hold the repository's own trees clean, read in all three, and the patience two minutes.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { PATIENCE } from '../src/Daoris.Cli/test/_fixture.ts';
import { checkTestPatience, TREES, waits } from './test-patience.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'test-patience-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

let n = 0;
/** A fresh tree from `{ 'src/Daoris.Cli/tool-test/x.test.ts': 'source' }`; returns its root. */
function tree(files) {
  const dir = join(scratch, `t${n++}`);
  for (const [rel, text] of Object.entries(files)) {
    const full = join(dir, rel);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, text);
  }
  return dir;
}

const TOOL_TEST = 'src/Daoris.Cli/tool-test/x.test.ts';
const verdict = (source, rel = TOOL_TEST) => checkTestPatience(tree({ [rel]: source }));

test('a short fixed wait is refused in each shape a wait is written, naming its line', () => {
  const { problems } = verdict("import { test } from 'node:test';\n\nconst timer = setTimeout(() => fail(), 30_000);\n");
  assert.equal(problems.length, 1);
  assert.match(problems[0], /src\/Daoris\.Cli\/tool-test\/x\.test\.ts:3: a fixed wait of 30000 ms, under the shared patience of 120000 ms/);
  for (const source of [
    "test('a', { timeout: 30_000 }, () => {});",
    'await speak({ cwd, wait: 10_000 });',
    'await speak({ cwd, start: 60_000 });',
    'const run = (ms = 5000) => ms;',
    'const STUB_WAIT_MS = 10_000;',
    'const HANG_LIMIT = 90_000;',
    'await until(() => done, 20_000);',
    "await waitFor(() => done, 'it', 15_000);",
    'await within(promise, 1500);',
    'const t = treePids(at, 20_000);',
    'const end = Date.now() + 30_000;',
    'for (let waited = 0; waited < 10_000; waited += 100) {}',
    'await wait(() => ready, { within: 5_000, every: 5 });',
    'const t = new Promise((done) => setTimeout(done, 2 * 60_000 - 1));'.replace('2 * 60_000 - 1', '1_500'),
    'const t = setTimeout(() => { fail(new Error(`no answer`)); }, 30 * 1000);',
    'const bounded = AbortSignal.timeout(30_000);',
  ]) {
    assert.equal(verdict(source).problems.length, 1, source);
  }
});

test('a product of numbers is read: two minutes passes and a minute does not', () => {
  assert.equal(verdict('await until(() => done, 60 * 1000);').problems.length, 1);
  assert.equal(verdict('await until(() => done, 2 * 60 * 1000);').problems.length, 0);
  assert.deepEqual(waits('const end = Date.now() + 2 * minute;'), []);
});

test('a wait at the patience or past it, under a second, with no number, or by PATIENCE passes', () => {
  for (const source of [
    'await until(() => done, 120_000);',
    "test('a', { timeout: 300_000 }, () => {});",
    'const HANG_MS = 5 * PATIENCE;',
    'await until(() => done, PATIENCE);',
    'await speak({ cwd, wait: PATIENCE });',
    'await sleep(250);',
    'await speak({ cwd, wait: 900 });',
    'await new Promise((done) => setTimeout(done, 5));',
    'const run = (result) => { result.ms = 1000; };',
    'await speak({ cwd, wait: limit });',
    'const row = { name: "Red", ms: 9 * minute, turnMs: 60000, start: 0 };',
    'assert.equal(outcome.waitedMs, 30_000);',
  ]) {
    assert.deepEqual(verdict(source).problems, [], source);
  }
});

test('a word in a comment, a string or a template is no wait', () => {
  const source = [
    '// await until(() => done, 20_000);',
    '/* { timeout: 30_000 } */',
    "const script = 'setTimeout(() => {}, 60_000)';",
    'const said = `the stub answered no prompt within ${ms}s, wait: 30_000`;',
    "export const a = /timeout: 30_000/.test(text) ? 1 : 2 / 3;",
    '',
  ].join('\n');
  assert.deepEqual(verdict(source).problems, []);
});

test('a marked wait passes and is counted by its kind; a mark with no kind, or no word of why, is none', () => {
  const marked = [
    "await speak({ cwd, wait: 1_000 }); // patience: claim — the bound the row fails at is what it tests",
    '// patience: staging — a window in which nothing should happen',
    'await new Promise((done) => setTimeout(done, 1500));',
    'const run = capture(command, at, { timeout: 5_000 }); // patience: input — the command is ended at its own timeout',
    'await speak({ cwd, wait: 1_000 }); // patience: claim — a second one',
    '',
  ].join('\n');
  const found = verdict(marked);
  assert.deepEqual(found.problems, []);
  assert.deepEqual(found.marked, { claim: 2, staging: 1, input: 1 });
  for (const mark of ['// patience:', '// patience: claim', '// patience: whatever — it stays', '// patience claim — missing colon']) {
    assert.equal(verdict(`await speak({ cwd, wait: 1_000 }); ${mark}\n`).problems.length, 1, mark);
  }
  assert.equal(verdict('// patience: claim — for the one below\nconst a = 1;\nawait speak({ wait: 1_000 });\n').problems.length, 1, 'a mark two lines up reaches nothing');
});

test('a wait on the PATIENCE is counted, its import and its definition are not', () => {
  const found = checkTestPatience(tree({
    [TOOL_TEST]: "import { PATIENCE } from '../test/_fixture.ts';\nawait until(() => done, PATIENCE);\nconst t = setTimeout(stop, PATIENCE);\n",
    'src/Daoris.Cli/test/_fixture.ts': 'export const PATIENCE = 120_000;\n',
  }));
  assert.equal(found.uses, 2);
  assert.deepEqual(found.files.get(TOOL_TEST), { uses: 2, marked: 0 });
});

test('the three trees are read, and a folder of fixtures, node_modules and a script that is no test are not', () => {
  const short = 'await until(() => done, 20_000);\n';
  const planted = {
    'src/Daoris.Cli/test/a/b.test.ts': short,
    'src/Daoris.Cli/tool-test/c.test.ts': short,
    'tools/d.test.mjs': short,
  };
  assert.equal(checkTestPatience(tree(planted)).problems.length, 3);
  const skipped = {
    'src/Daoris.Cli/test/fixtures/e.ts': short,
    'src/Daoris.Cli/test/node_modules/f.ts': short,
    'tools/g.mjs': short,
    'tools/node_modules/h.test.mjs': short,
    'src/Daoris.Cli/src/i.ts': short,
  };
  assert.deepEqual(checkTestPatience(tree(skipped)).problems, []);
});

test('the patience a root is read against is its own: a wait at it passes, one under it does not', () => {
  const source = 'await until(() => done, 30_000);\n';
  assert.equal(checkTestPatience(tree({ [TOOL_TEST]: source }), 60_000).problems.length, 1);
  assert.deepEqual(checkTestPatience(tree({ [TOOL_TEST]: source }), 30_000).problems, []);
});

test('the shared patience is two minutes, as the desktop tests read theirs', () => {
  assert.equal(PATIENCE, 120_000);
});

test("the repository's own tests wait by the patience or say why they do not, and each tree is read", () => {
  const found = checkTestPatience(root);
  assert.deepEqual(found.problems, []);
  for (const { dir } of TREES) {
    assert.ok([...found.files.keys()].some((rel) => rel.startsWith(`${dir}/`)), `${dir} is read: the census found no test file in it`);
  }
  assert.ok(found.uses > 0, 'the tests name PATIENCE');
});

test('a short wait planted in a copy of a real test is refused, and the patience in its place is clean', () => {
  // The merge-queue test's own guard, as TOOLTESTHANG1 wrote it before the shared patience: the wait the census exists to refuse.
  const guard = 'const HANG_MS = 10_000;\nasync function until(holds, ms = HANG_MS) { for (const end = Date.now() + ms; !holds() && Date.now() < end;) await pause(100); }\n';
  const red = checkTestPatience(tree({ 'src/Daoris.Cli/tool-test/merge-queue.test.ts': guard }));
  assert.equal(red.problems.length, 1);
  assert.match(red.problems[0], /merge-queue\.test\.ts:1: a fixed wait of 10000 ms/);
  assert.deepEqual(checkTestPatience(tree({ 'src/Daoris.Cli/tool-test/merge-queue.test.ts': guard.replace('10_000', 'PATIENCE') })).problems, []);
});

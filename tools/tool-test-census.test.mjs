/**
 * TOOLTESTCENSUS1: the tool-test census (every tool test file is run by exactly one declared gate).
 *
 *   node --test tools/tool-test-census.test.mjs
 *
 * `checkToolTests` reads a source tree. Each case builds a small tree in a gitignored scratch folder of the repository, with a
 * root `verify` script that lists `node --test` files and a `tool-tests` gate that globs the CLI package's `tool-test/` folder,
 * and holds the verdict: a file in neither, in both or listed and gone is refused by name; the real tree passes.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkToolTests, coverage, expand } from './tool-test-census.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'tool-test-census-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

let n = 0;
/** A fresh tree with `tools` test files and `tool-test` test files on disk and `verify` listing `listed`; returns its root. */
function tree({ tools = [], toolTests = [], listed = [], extraGate = '' }) {
  const dir = join(scratch, `t${n++}`);
  const put = (rel, text) => {
    mkdirSync(dirname(join(dir, rel)), { recursive: true });
    writeFileSync(join(dir, rel), text);
  };
  for (const name of tools) put(`tools/${name}`, '');
  for (const name of toolTests) put(`src/Daoris.Cli/tool-test/${name}`, '');
  put('package.json', JSON.stringify({ scripts: {
    verify: `node x.mjs && node --test ${listed.join(' ')}`,
    tooltests: 'npm run tooltests -w daoris',
  } }));
  put('src/Daoris.Cli/package.json', JSON.stringify({ scripts: { tooltests: 'node --test "tool-test/**/*.ts"', test: 'node --test "test/**/*.ts"' } }));
  put('daoris.gates.json', JSON.stringify({ gates: [
    { name: 'cli', run: 'npm run verify' },
    { name: 'tool-tests', run: 'npm run tooltests' },
    ...(extraGate ? [{ name: 'extra', run: extraGate }] : []),
  ] }));
  return dir;
}

test('the tree passes', () => {
  assert.deepEqual(checkToolTests(root).problems, []);
});

test('a file listed by verify and a file the tool-tests glob reaches are each owned once', () => {
  const dir = tree({ tools: ['a.test.mjs'], toolTests: ['b.test.ts', 'deep/c.test.ts'], listed: ['tools/a.test.mjs'] });
  assert.deepEqual(checkToolTests(dir), { problems: [], count: 3 });
});

test('a tools test no list names is an orphan, named', () => {
  const dir = tree({ tools: ['a.test.mjs', 'b.test.mjs'], listed: ['tools/a.test.mjs'] });
  const { problems } = checkToolTests(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^tools\/b\.test\.mjs: no gate runs it/);
});

test('a file two gates run is refused, naming both', () => {
  const dir = tree({ tools: ['a.test.mjs'], listed: ['tools/a.test.mjs'], extraGate: 'node --test tools/a.test.mjs' });
  const { problems } = checkToolTests(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^tools\/a\.test\.mjs: run by 2 gates \(cli, extra\)/);
});

test('a file named twice in one gate is still one gate', () => {
  const dir = tree({ tools: ['a.test.mjs'], listed: ['tools/a.test.mjs', 'tools/a.test.mjs'] });
  assert.deepEqual(checkToolTests(dir).problems, []);
});

test('a listed test that is gone is refused', () => {
  const dir = tree({ tools: ['a.test.mjs'], listed: ['tools/a.test.mjs', 'tools/gone.test.mjs'] });
  const { problems } = checkToolTests(dir);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^tools\/gone\.test\.mjs: listed by gate cli and not there/);
});

test('npm scripts are followed into the CLI package, and a glob is read relative to it', () => {
  const scripts = { root: { tooltests: 'npm run tooltests -w daoris' }, cli: { tooltests: 'node --test "tool-test/**/*.ts"' } };
  const { listed, globs } = coverage(expand('npm run tooltests', scripts));
  assert.equal(listed.size, 0);
  assert.equal(globs.length, 1);
  assert.ok(globs[0].test('src/Daoris.Cli/tool-test/x.test.ts'));
  assert.ok(globs[0].test('src/Daoris.Cli/tool-test/deep/x.test.ts'));
  assert.ok(!globs[0].test('src/Daoris.Cli/test/x.test.ts'));
});

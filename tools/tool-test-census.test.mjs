/**
 * The tool-test census (every test file is in a declared test set, every set holds a file and is run by a declared gate, and no
 * script names a test file).
 *
 *   node --test tools/tool-test-census.test.mjs
 *
 * `checkToolTests` reads a source tree. Each case builds a small tree in its own folder of `_fixtures/`, with a
 * `daoris.gates.json` declaring gates and test sets, and holds the verdict; the real tree passes.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkToolTests, liveLoadReads } from './tool-test-census.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, '_fixtures', 'tool-test-census-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const GATES = [{ name: 'cli-tests', run: 'npm run verify:tests' }, { name: 'tool-tests', run: 'npm run tooltests' }];
const TOOLS = { name: 'tools', gate: 'cli-tests', suite: 'cli', runner: 'node', files: ['tools/*.test.mjs'] };
const TOOL_TESTS = { name: 'tool-tests', gate: 'tool-tests', runner: 'node', cwd: 'src/Daoris.Cli', files: ['src/Daoris.Cli/tool-test/**/*.test.ts'] };

let n = 0;
/** A fresh tree holding `files` (empty), the declared `sets` and `scripts`; returns its root. */
function tree({ files = [], sets = [TOOLS, TOOL_TESTS], scripts = { 'verify:tests': 'node tools/test-sets.mjs run --gate cli-tests' }, cliScripts = {} }) {
  const dir = join(scratch, `t${n++}`);
  const put = (rel, text) => {
    mkdirSync(dirname(join(dir, rel)), { recursive: true });
    writeFileSync(join(dir, rel), text);
  };
  for (const file of files) put(file, '');
  put('package.json', JSON.stringify({ scripts }));
  put('src/Daoris.Cli/package.json', JSON.stringify({ scripts: cliScripts }));
  put('daoris.gates.json', JSON.stringify({ gates: GATES, tests: { sets } }));
  return dir;
}

test('the tree passes', () => {
  assert.deepEqual(checkToolTests(root).problems, []);
});

test('a test file under a set\'s pattern is held with no list edited, nested folders included', () => {
  const dir = tree({ files: ['tools/a.test.mjs', 'tools/brand-new.test.mjs', 'src/Daoris.Cli/tool-test/deep/c.test.ts'] });
  assert.deepEqual(checkToolTests(dir), { problems: [], count: 3 });
});

test('a test file no set holds is named', () => {
  const dir = tree({ files: ['tools/a.test.mjs', 'tools/merge/b.test.ts', 'src/Daoris.Cli/tool-test/c.e2e.ts', 'src/Daoris.Cli/tool-test/d.test.ts', 'src/Daoris.Cli/tool-test/support/kit.ts'] });
  const { problems } = checkToolTests(dir);
  assert.deepEqual(problems.map((each) => each.split(':')[0]), ['src/Daoris.Cli/tool-test/c.e2e.ts', 'tools/merge/b.test.ts']);
  assert.match(problems[0], /no test set holds it/);
});

test('a set no file falls under is named, an earlier set taking its every file included', () => {
  const shadowed = { name: 'later', gate: 'cli-tests', runner: 'node', files: ['tools/a.test.mjs'] };
  const { problems } = checkToolTests(tree({ files: ['tools/a.test.mjs', 'src/Daoris.Cli/tool-test/c.test.ts'], sets: [TOOLS, TOOL_TESTS, shadowed] }));
  assert.deepEqual(problems, ['set later: holds no test file (no file matches its patterns, or an earlier set holds each)']);
});

test('a set whose gate is not declared is named', () => {
  const stray = { ...TOOL_TESTS, gate: 'gone' };
  const { problems } = checkToolTests(tree({ files: ['tools/a.test.mjs', 'src/Daoris.Cli/tool-test/c.test.ts'], sets: [TOOLS, stray] }));
  assert.deepEqual(problems, ['set tool-tests: run by gate gone, which daoris.gates.json does not declare']);
});

test('a set run by its own config whose patterns hold an earlier set\'s file is named, since that file would run twice', () => {
  const unit = { name: 'merge-unit', gate: 'cli-tests', runner: 'vitest', config: 'tools/vitest.config.ts', project: 'unit', files: ['tools/merge/**/*.test.ts'] };
  const early = { name: 'early', gate: 'cli-tests', runner: 'node', files: ['tools/merge/a.test.ts'] };
  const { problems } = checkToolTests(tree({ files: ['tools/merge/a.test.ts', 'tools/merge/b.test.ts'], sets: [early, unit] }));
  assert.deepEqual(problems, ['set merge-unit: runs by its config, which also runs tools/merge/a.test.ts of set early']);
});

test('a pick of another set names files that set holds', () => {
  const unit = { name: 'web-unit', gate: 'cli-tests', runner: 'vitest', cwd: 'src/Daoris.Web', files: ['src/Daoris.Web/src/**/*.test.ts'] };
  const pick = { name: 'rules', gate: 'tool-tests', runner: 'vitest', cwd: 'src/Daoris.Web', of: 'web-unit', files: ['src/Daoris.Web/src/a.test.ts', 'src/Daoris.Web/src/gone.test.ts'] };
  const { problems } = checkToolTests(tree({ files: ['src/Daoris.Web/src/a.test.ts'], sets: [pick, unit] }));
  assert.deepEqual(problems, ['set rules: picks src/Daoris.Web/src/gone.test.ts, which set web-unit does not hold']);
});

test('a script that names a test file is named: the declaration is where a test file is placed', () => {
  const { problems } = checkToolTests(tree({
    files: ['tools/a.test.mjs', 'src/Daoris.Cli/tool-test/c.test.ts'],
    scripts: { 'verify:tests': 'node --test tools/a.test.mjs' }, cliScripts: { tooltests: 'node --test "tool-test/**/*.test.ts" ../../tools/a.test.mjs' },
  }));
  assert.deepEqual(problems, [
    'package.json: script verify:tests names tools/a.test.mjs; place a test file by a set\'s pattern in daoris.gates.json\'s tests',
    'src/Daoris.Cli/package.json: script tooltests names ../../tools/a.test.mjs; place a test file by a set\'s pattern in daoris.gates.json\'s tests',
  ]);
});

const call = (name, args = '') => `${name}(${args})`;

test('a tools test that reads the live machine\'s load is named by line; one that hands a seam or a home of its own is not', () => {
  const source = [
    `const flow = { load: ${call('loadSeam', 'home, root')} };`,
    `const held = ${call('heldNow')};`,
    `const again = ${call('lookAt', 'process.env.DAORIS_HOME')};`,
    `const trees = ${call('foreignTrees', 'own')};`,
    `const sampler = ${call('loadSampler')};`,
    `const procs = ${call('readProcesses')};`,
    `const ok = [${call('heldNow', 'fixtureHome')}, ${call('foreignTrees', 'own, seamOf([])')}, ${call('loadSampler', '{ every: 1, count: () => 0 }')}, ${call('holdersSentence', 'null')}];`,
    `// ${call('heldNow')} in a comment reads nothing`,
    `function ${call('heldNow')} {}`,
    `const nested = ${call('foreignTrees', `own, { processes: () => ${call('list')} }`)};`,
  ].join('\n');
  assert.deepEqual(liveLoadReads(source).map((read) => [read.line, read.name]), [[1, 'loadSeam'], [2, 'heldNow'], [3, 'lookAt'], [4, 'foreignTrees'], [5, 'loadSampler'], [6, 'readProcesses']]);
  const dir = tree({ files: ['tools/a.test.mjs', 'tools/b.test.mjs', 'src/Daoris.Cli/tool-test/c.test.ts'] });
  writeFileSync(join(dir, 'tools', 'b.test.mjs'), source);
  writeFileSync(join(dir, 'src/Daoris.Cli/tool-test/c.test.ts'), source);
  const { problems } = checkToolTests(dir);
  assert.deepEqual(problems.map((each) => each.split(' ')[0]), ['tools/b.test.mjs:1:', 'tools/b.test.mjs:2:', 'tools/b.test.mjs:3:', 'tools/b.test.mjs:4:', 'tools/b.test.mjs:5:', 'tools/b.test.mjs:6:'], 'only a test under tools/ is read, and each call is named');
  assert.match(problems[1], /heldNow\( reads the live machine/);
});

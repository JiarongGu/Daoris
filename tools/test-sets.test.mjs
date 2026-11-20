/**
 * The declared test sets (`daoris.gates.json`'s `tests`), read as pure data: which set holds a file, which sets a changed path
 * reaches, which sets a gate runs and in what order, and the command that runs one.
 *
 *   node --test tools/test-sets.test.mjs
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { filesBySet, selectSets, setCommand, setOf, setsReaching, testSets } from './test-sets.mjs';

const declaration = {
  gates: [
    { name: 'cli-tests', run: 'npm run verify:tests' },
    { name: 'tool-e2e', run: 'npm run tooltests:e2e', paths: ['daoris.gates.json', 'tools/merge/', 'src/Daoris.Cli/tool-test/'] },
    { name: 'web-unit', run: 'npm run test:web:unit' },
    { name: 'web-rules', run: 'npm run check:web-rules' },
  ],
  tests: {
    sets: [
      { name: 'layers', gate: 'cli-tests', runner: 'node', files: ['tools/layers-*.test.mjs'] },
      { name: 'e2e', gate: 'tool-e2e', runner: 'node', cwd: 'src/Daoris.Cli', bounded: true, files: ['src/Daoris.Cli/tool-test/**/*.e2e.ts', 'tools/as-merged.test.mjs'], args: ['--test-concurrency=4'] },
      { name: 'merge-unit', gate: 'cli-tests', suite: 'cli', runner: 'vitest', config: 'tools/vitest.config.ts', project: 'unit', files: ['tools/merge/**/*.test.ts'] },
      { name: 'alone', gate: 'cli-tests', suite: 'cli', runner: 'node', last: true, files: ['tools/alone.test.mjs'] },
      { name: 'tools', gate: 'cli-tests', suite: 'cli', runner: 'node', files: ['tools/*.test.mjs'] },
      { name: 'web-rules', gate: 'web-rules', runner: 'vitest', cwd: 'src/Daoris.Web', of: 'web-unit', files: ['src/Daoris.Web/src/design/tokens.test.ts'] },
      { name: 'web-unit', gate: 'web-unit', runner: 'vitest', cwd: 'src/Daoris.Web', files: ['src/Daoris.Web/src/**/*.test.{ts,tsx}'] },
    ],
  },
};
const sets = testSets(declaration);
const names = (list) => list.map((set) => set.name);

test('a file is in the first set whose patterns hold it, and a pick of another set takes none', () => {
  assert.equal(setOf(sets, 'tools/layers-census.test.mjs')?.name, 'layers');
  assert.equal(setOf(sets, 'tools/as-merged.test.mjs')?.name, 'e2e');
  assert.equal(setOf(sets, 'tools/fsx.test.mjs')?.name, 'tools');
  assert.equal(setOf(sets, 'src/Daoris.Web/src/design/tokens.test.ts')?.name, 'web-unit');
  assert.equal(setOf(sets, 'tools/merge/deep/plan.test.ts')?.name, 'merge-unit');
  assert.equal(setOf(sets, 'docs/README.md'), null);
});

test('a new test file under a pattern is in its set, and in the command that runs it, with no list edited', () => {
  const files = ['tools/fsx.test.mjs', 'tools/brand-new.test.mjs', 'tools/layers-census.test.mjs', 'tools/fsx.mjs', 'src/Daoris.Cli/tool-test/x.e2e.ts', 'tools/as-merged.test.mjs'];
  const held = filesBySet(sets, files);
  assert.deepEqual(held.get('tools'), ['tools/brand-new.test.mjs', 'tools/fsx.test.mjs']);
  assert.deepEqual(held.get('layers'), ['tools/layers-census.test.mjs']);
  const tools = setCommand(sets.find((set) => set.name === 'tools'), held.get('tools'));
  assert.deepEqual(tools, { label: 'tools', cwd: '.', command: 'node', args: ['--test', 'tools/brand-new.test.mjs', 'tools/fsx.test.mjs'] });
  const e2e = setCommand(sets.find((set) => set.name === 'e2e'), held.get('e2e'));
  assert.deepEqual(e2e.args, ['--test', '--test-concurrency=4', 'tool-test/x.e2e.ts', '../../tools/as-merged.test.mjs'], 'a set run from its own folder names its files from there');
});

test('a changed path reaches every set but a bounded one, which it reaches only within its gate\'s paths', () => {
  assert.deepEqual(names(setsReaching(sets, declaration.gates, 'docs/README.md')), ['layers', 'merge-unit', 'alone', 'tools', 'web-rules', 'web-unit']);
  assert.deepEqual(names(setsReaching(sets, declaration.gates, 'tools/merge/plan.ts')), names(sets));
  assert.ok(names(setsReaching(sets, declaration.gates, 'src/Daoris.Cli/tool-test/x.e2e.ts')).includes('e2e'));
});

test("a gate runs its sets in the declaration's order, a last set after the rest", () => {
  assert.deepEqual(names(selectSets(sets, { gates: ['cli-tests'] })), ['layers', 'merge-unit', 'tools', 'alone']);
  assert.deepEqual(names(selectSets(sets, { names: ['tools', 'layers'] })), ['layers', 'tools']);
  assert.throws(() => selectSets(sets, { names: ['gone'] }), /no test set is named gone/);
  assert.throws(() => selectSets(sets, { gates: ['nobody'] }), /no test set is run by gate nobody/);
});

test('a Vitest set runs its project by its config, a pick runs its files, and a whole set its config alone', () => {
  const merge = setCommand(sets.find((set) => set.name === 'merge-unit'), ['tools/merge/a.test.ts']);
  assert.deepEqual(merge, { label: 'merge-unit', cwd: '.', command: 'node', args: ['node_modules/vitest/vitest.mjs', 'run', '--config', 'tools/vitest.config.ts', '--project', 'unit'] });
  const pick = setCommand(sets.find((set) => set.name === 'web-rules'), ['src/Daoris.Web/src/design/tokens.test.ts']);
  assert.deepEqual(pick.args, ['node_modules/vitest/vitest.mjs', 'run', 'src/design/tokens.test.ts']);
  assert.equal(pick.cwd, 'src/Daoris.Web');
  const whole = setCommand(sets.find((set) => set.name === 'web-unit'), ['src/Daoris.Web/src/a.test.ts']);
  assert.deepEqual(whole.args, ['node_modules/vitest/vitest.mjs', 'run']);
});

test('a declaration is refused where a set names no files, no runner, or a name another set has', () => {
  assert.throws(() => testSets({ tests: { sets: [{ name: 'a', gate: 'g', runner: 'node', files: [] }] } }), /set a names no files/);
  assert.throws(() => testSets({ tests: { sets: [{ name: 'a', gate: 'g', files: ['x/*.test.mjs'] }] } }), /set a names no runner/);
  assert.throws(() => testSets({ tests: { sets: [{ name: 'a', gate: 'g', runner: 'node', files: ['x'] }, { name: 'a', gate: 'g', runner: 'node', files: ['y'] }] } }), /two sets are named a/);
  assert.deepEqual(testSets({}), []);
});

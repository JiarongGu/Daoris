import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { budgetsFile, setTimeoutOf } from './check-budgets.ts';
import { privateGateFiles } from '../private-gate-files.mjs';
import { declaredPrep } from '../tree-prep.mjs';

const scratch: string[] = [];
const fixture = (gates: object, kept?: object): { checkout: string; source: { home: string; repository: string } } => {
  const base = mkdtempSync(join(tmpdir(), 'home-members-'));
  scratch.push(base);
  const checkout = join(base, 'checkout');
  mkdirSync(checkout, { recursive: true });
  writeFileSync(join(checkout, 'daoris.gates.json'), JSON.stringify(gates));
  const home = join(base, 'home');
  mkdirSync(join(home, 'data'), { recursive: true });
  if (kept) writeFileSync(budgetsFile(home), JSON.stringify({ schema: 1, repositories: { Fixture: kept } }));
  return { checkout, source: { home, repository: 'Fixture' } };
};
test.afterAll(() => { for (const each of scratch) rmSync(each, { recursive: true, force: true }); });

test('the private files a tree is handed are the home\'s local inputs and patterns file, the declaration\'s only where the home has none', () => {
  const { checkout, source } = fixture({ gates: [], localInputs: ['local/old.props'], sensitive: { patternsFile: 'local/old-patterns.txt' } }, { localInputs: ['local/a.props', 'local/b.props'], patternsFile: 'local/mine.txt' });
  assert.deepEqual(privateGateFiles(checkout, source), ['local/a.props', 'local/b.props', 'local/mine.txt']);
  const bare = fixture({ gates: [], localInputs: ['local/old.props'], sensitive: { patternsFile: 'local/old-patterns.txt' } }, {});
  assert.deepEqual(privateGateFiles(bare.checkout, bare.source), ['local/old.props', 'local/old-patterns.txt'], 'a member the home does not keep is the declaration\'s');
  assert.deepEqual(privateGateFiles(bare.checkout, { home: null, repository: 'Fixture' }), ['local/old.props', 'local/old-patterns.txt']);
});

test('with no home and no declared member there are no local inputs and the devkit\'s patterns file', () => {
  const { checkout } = fixture({ gates: [] });
  assert.deepEqual(privateGateFiles(checkout, { home: null, repository: 'Fixture' }), ['local/sensitive-patterns.txt']);
});

test('only what is under local/ is handed, whichever side names it', () => {
  const { checkout, source } = fixture({ gates: [] }, { localInputs: ['secrets/x.txt', 'local/../x.txt', 'local/ok.txt'], patternsFile: 'elsewhere.txt' });
  assert.deepEqual(privateGateFiles(checkout, source), ['local/ok.txt']);
});

test('the tree baseline is the home\'s, the declaration\'s where the home keeps none', () => {
  const { checkout, source } = fixture({ gates: [{ name: 'a', run: 'x', regenerate: 'node r.cjs' }], treeBaseline: 'node old.cjs' }, { treeBaseline: 'node mine.cjs' });
  assert.deepEqual(declaredPrep(checkout, source), { regenerate: ['node r.cjs'], baseline: 'node mine.cjs' });
  const bare = fixture({ gates: [], treeBaseline: 'node old.cjs' }, {});
  assert.equal(declaredPrep(bare.checkout, bare.source).baseline, 'node old.cjs');
  assert.equal(declaredPrep(bare.checkout, { home: null, repository: 'Fixture' }).baseline, 'node old.cjs');
  const none = fixture({ gates: [] });
  assert.equal(declaredPrep(none.checkout, { home: null, repository: 'Fixture' }).baseline, null);
});

test('a test set\'s per-test timeout is the home\'s, the declaration\'s where the home keeps none, none where neither does', () => {
  assert.equal(setTimeoutOf({ sets: { 'merge-unit': { timeoutMs: 123_456 } } }, 'merge-unit', 9_999), 123_456);
  assert.equal(setTimeoutOf({}, 'merge-unit', 9_999), 9_999);
  assert.equal(setTimeoutOf({ sets: { other: { timeoutMs: 1 } } }, 'merge-unit', undefined), undefined);
  assert.equal(setTimeoutOf({ sets: { 'merge-unit': { timeoutMs: -1 } } }, 'merge-unit', 7), 7, 'a value that is no positive number is none');
});

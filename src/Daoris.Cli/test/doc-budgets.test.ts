import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture } from './_fixture.ts';

// DOC4 (D122 §2.7): a document the manifest declares with a ceiling is measured at the manifest's number,
// so a ceiling is written once. `tools/doc-budgets.json` keeps only what no role names.
// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const { ceilings, words } = await import('../../../tools/doc-budgets.mjs') as {
  words: (text: string) => number;
  ceilings: (root: string) => {
    budgets: { document: string; ceiling: number; from: string }[];
    twice: string[];
  };
};

const BUDGETS = 'tools/doc-budgets.json';

test('a word is a whitespace-separated token, as check counts one', () => {
  // #, Tasks, -, [, ], **ROW1, —, a, row**, done: a bracket and a dash are tokens, as a reader sees them.
  assert.equal(words('# Tasks\n\n- [ ] **ROW1 — a  row**\tdone\r\n'), 10);
  assert.equal(words('  '), 0);
});

test('a declared document is measured at the manifest\'s ceiling, and the rest at the tool\'s', () => {
  const fx = makeFixture('doc-budgets-both');
  fx.write('daoris.json', JSON.stringify({
    documents: { backlog: { path: 'TASKS.md', words: 6600 }, decisions: 'docs/DECISIONS.md' },
  }));
  fx.write(BUDGETS, JSON.stringify({ _why: 'prose', 'README.md': 2600 }));

  const { budgets, twice } = ceilings(fx.root);

  assert.deepEqual(budgets, [
    { document: 'TASKS.md', ceiling: 6600, from: 'daoris.json (backlog)' },
    { document: 'README.md', ceiling: 2600, from: BUDGETS },
  ]);
  assert.deepEqual(twice, []);
  fx.cleanup();
});

test('a role bound to a ceiling alone is check\'s to measure, not this tool\'s', () => {
  const fx = makeFixture('doc-budgets-brief');
  fx.write('daoris.json', JSON.stringify({ documents: { brief: { words: 1300 }, room: { words: 600 } } }));
  fx.write(BUDGETS, JSON.stringify({}));

  assert.deepEqual(ceilings(fx.root).budgets, []);
  fx.cleanup();
});

test('a document given a ceiling in both lists is named, since two numbers for one file is a fact', () => {
  const fx = makeFixture('doc-budgets-twice');
  fx.write('daoris.json', JSON.stringify({ documents: { backlog: { path: 'TASKS.md', words: 6600 } } }));
  fx.write(BUDGETS, JSON.stringify({ 'TASKS.md': 7000 }));

  const { budgets, twice } = ceilings(fx.root);

  assert.deepEqual(twice, ['TASKS.md']);
  assert.deepEqual(budgets.map((b) => b.ceiling), [6600]);
  fx.cleanup();
});

test('a repository with no manifest, or one that declares nothing, keeps the tool\'s list alone', () => {
  const fx = makeFixture('doc-budgets-none');
  fx.write(BUDGETS, JSON.stringify({ 'ROADMAP.md': 3600 }));

  assert.deepEqual(ceilings(fx.root).budgets, [{ document: 'ROADMAP.md', ceiling: 3600, from: BUDGETS }]);
  fx.cleanup();
});

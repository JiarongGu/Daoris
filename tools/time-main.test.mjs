import assert from 'node:assert/strict';
import test from 'node:test';
import { KEEP, mainRecord, withRun } from './time-main.mjs';

const KEY = 'tools/merge/flow.test.ts::a case';

test('a run\'s alone times go first under each test, one entry a commit, the newest few kept', () => {
  let tests = {};
  for (let i = 1; i <= KEEP + 2; i += 1) tests = withRun(tests, { [KEY]: i * 1000 }, `c${i}`, `2026-11-20T00:0${i}:00.000Z`);
  assert.equal(tests[KEY].length, KEEP);
  assert.deepEqual(tests[KEY][0], { ms: (KEEP + 2) * 1000, commit: `c${KEEP + 2}`, at: `2026-11-20T00:0${KEEP + 2}:00.000Z` });
  const again = withRun(tests, { [KEY]: 500, 'other::b': 9 }, `c${KEEP + 2}`, '2026-11-20T01:00:00.000Z');
  assert.equal(again[KEY].length, KEEP, 'a rerun on the same commit replaces its entry');
  assert.deepEqual(again[KEY].map((each) => each.ms).slice(0, 2), [500, (KEEP + 1) * 1000]);
  assert.deepEqual(again['other::b'], [{ ms: 9, commit: `c${KEEP + 2}`, at: '2026-11-20T01:00:00.000Z' }]);
});

test('main\'s record of a test is the slowest of its kept entries whose commit is in main\'s history; none, null', () => {
  const tests = { [KEY]: [{ ms: 31_000, commit: 'failed-branch', at: 'c' }, { ms: 12_000, commit: 'landed-2', at: 'b' }, { ms: 20_000, commit: 'landed-1', at: 'a' }] };
  const onMain = (commit) => commit.startsWith('landed');
  assert.equal(mainRecord(tests, KEY, onMain), 20_000, 'a branch that never landed sets no record of main\'s');
  assert.equal(mainRecord(tests, 'none::x', onMain), null);
  assert.equal(mainRecord(tests, KEY, () => false), null);
});

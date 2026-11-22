import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { declaredAdmission, readAdmission } from './admission.ts';
import { BASELINE, BLOCKING, MERGE_SUITES, PATH_CHECKS } from './reach.ts';
import { ALL, repoRoot } from './repo-gates.ts';

// test-map: strings name paths as data — the cases name a gates file's keys and gates, which nothing reads as a path

const GOOD = {
  admission: {
    always: ['one', 'two'],
    suites: ['three'],
    byPaths: [{ gate: 'four', paths: ['a/**', 'b.json'], why: 'a and b' }],
  },
};

test("the admission is read from the declaration's own key: the gates every landing runs, its suites, and the checks its paths reach", () => {
  const read = declaredAdmission(JSON.stringify(GOOD));
  assert.deepEqual([...read.always], ['one', 'two']);
  assert.deepEqual([...read.suites], ['three']);
  assert.deepEqual(read.pathChecks.map((check) => ({ ...check, paths: [...check.paths] })), [{ gate: 'four', paths: ['a/**', 'b.json'], why: 'a and b' }]);
});

test('an admission that is missing or mistyped fails where it is read, said at once, never a list the landing silently shortens', () => {
  const broken: unknown[] = [
    undefined,
    {},
    { always: 'one', suites: [], byPaths: [] },
    { always: [], suites: [3], byPaths: [] },
    { always: [], suites: [], byPaths: [{ gate: 'four', paths: [], why: 'x' }] },
    { always: [], suites: [], byPaths: [{ gate: 'four', paths: ['a'], why: '' }] },
    { always: [], suites: [], byPaths: [{ paths: ['a'], why: 'x' }] },
    { always: ['one'], suites: ['one'], byPaths: [] },
    { always: [], suites: [], byPaths: [{ gate: 'four', paths: ['a'], why: 'x' }, { gate: 'four', paths: ['b'], why: 'y' }] },
  ];
  for (const admission of broken) {
    assert.throws(() => declaredAdmission(JSON.stringify({ admission })), /daoris\.gates\.json/, JSON.stringify(admission));
  }
  assert.throws(() => declaredAdmission(JSON.stringify({ gates: [] })), /admission/);
  assert.throws(() => declaredAdmission('not json'), /daoris\.gates\.json/);
});

test("the merge tool's lists are the declaration's: one list, which the driver's landing reads from the same key", () => {
  const text = readFileSync(join(repoRoot, 'daoris.gates.json'), 'utf8');
  const declared = declaredAdmission(text);
  assert.deepEqual([...BLOCKING], [...declared.always]);
  assert.deepEqual([...MERGE_SUITES], [...declared.suites]);
  assert.deepEqual([...BASELINE], [...declared.always, ...declared.suites]);
  assert.deepEqual(PATH_CHECKS, declared.pathChecks);
  assert.deepEqual(readAdmission(repoRoot), declared);
  const names = new Set(ALL);
  for (const gate of [...declared.always, ...declared.suites, ...declared.pathChecks.map((check) => check.gate)]) {
    assert.ok(names.has(gate), `the admission names ${gate}, which daoris.gates.json does not declare`);
  }
});

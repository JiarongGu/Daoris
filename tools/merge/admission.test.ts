import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { declaredAdmission, readAdmission, readAdmissionOr } from './admission.ts';
import { BASELINE, BLOCKING, MERGE_SUITES, PATH_CHECKS, selectGates } from './reach.ts';
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

const PLAN = ['universal', 'comments', 'types'].map((name) => ({ name, run: name, kind: 'check' })) as unknown as Parameters<typeof selectGates>[0];

const admissionOf = (always: string[], byPaths: object[] = []) => declaredAdmission(JSON.stringify({ admission: { always, suites: [], byPaths } }));

test("a selection reads the admission of the tree it selects for, so two trees' policies are judged in one process", () => {
  const before = admissionOf(['universal']);
  const after = admissionOf(['universal', 'comments']);
  const run = (admission: ReturnType<typeof admissionOf>) => selectGates(PLAN, ['a.txt'], { admission }).gates.filter((entry) => entry.run).map((entry) => entry.gate.name);

  assert.deepEqual(run(before), ['universal']);
  assert.deepEqual(run(after), ['universal', 'comments']);
  assert.deepEqual(run(before), ['universal'], 'the first policy is not kept by the second');
});

test('a tree that declares no admission is judged by the fallback, one that declares it badly is an error', () => {
  const fallback = admissionOf(['universal']);
  const folder = join(repoRoot, '_fixtures', `admission-${process.pid}`);
  mkdirSync(folder, { recursive: true });
  try {
    assert.equal(readAdmissionOr(folder, fallback), fallback, 'no file');
    writeFileSync(join(folder, 'daoris.gates.json'), JSON.stringify({ gates: [] }));
    assert.equal(readAdmissionOr(folder, fallback), fallback, 'a file with no admission');
    writeFileSync(join(folder, 'daoris.gates.json'), JSON.stringify({ admission: { always: ['comments'], suites: [], byPaths: [] } }));
    assert.deepEqual([...readAdmissionOr(folder, fallback).always], ['comments']);
    writeFileSync(join(folder, 'daoris.gates.json'), JSON.stringify({ admission: { always: 3 } }));
    assert.throws(() => readAdmissionOr(folder, fallback), /admission/);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('a byPaths glob with a bang excludes the path it names, as the lanes file reads it', () => {
  const admission = admissionOf([], [{ gate: 'types', paths: ['web/**', '!web/generated/**'], why: 'w' }]);
  const reaching = (...paths: string[]) => selectGates(PLAN, paths, { admission }).gates.find((entry) => entry.gate.name === 'types')!.run;

  assert.equal(reaching('web/src/a.ts'), true);
  assert.equal(reaching('web/generated/a.ts'), false);
  assert.equal(reaching('web/generated/a.ts', 'web/b.ts'), true);
});

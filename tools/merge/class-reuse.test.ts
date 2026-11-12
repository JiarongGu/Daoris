import { test } from 'vitest';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { PassedClasses, classKey, splitByReuse, unitId, type KeyContext, type Unit } from './class-reuse.ts';

const a: Unit = { suite: 'driver', unit: 'A.Tests.OneTests' };
const b: Unit = { suite: 'driver', unit: 'A.Tests.TwoTests' };

// OneTests reads src/Ports and its own file; TwoTests reads src/Core and its own file.
const reads: Record<string, string[]> = { [a.unit]: ['src/Ports/P.cs', 'T/OneTests.cs'], [b.unit]: ['src/Core/C.cs', 'T/TwoTests.cs'] };
const files = ['src/Ports/P.cs', 'src/Core/C.cs', 'T/OneTests.cs', 'T/TwoTests.cs', 'docs/x.md'];

const context = (text: Record<string, string>, over: Partial<KeyContext> = {}): KeyContext => ({
  candidates: () => files,
  reaches: (unit, path) => (reads[unit.unit] ?? []).includes(path),
  digestOf: (path) => text[path] ?? path,
  declaration: () => 'cmd',
  toolchain: 'node24',
  ...over,
});

test('a change to a widely referenced project invalidates the classes that name it and no others', () => {
  const before = context({});
  const after = context({ 'src/Ports/P.cs': 'changed' });
  assert.notEqual(classKey(a, after), classKey(a, before));
  assert.equal(classKey(b, after), classKey(b, before));
});

test('a file no class reads, the declaration and the toolchain: only the last two are inputs', () => {
  assert.equal(classKey(a, context({ 'docs/x.md': 'other' })), classKey(a, context({})));
  assert.notEqual(classKey(a, context({}, { declaration: () => 'cmd --other' })), classKey(a, context({})));
  assert.notEqual(classKey(a, context({}, { toolchain: 'node25' })), classKey(a, context({})));
});

test('only the classes whose key has no standing pass are run, and a pass is kept per class across a save', () => {
  const { dir: folder, remove } = fixtureFolder('class-reuse');
  try {
    const file = join(folder, 'store.json');
    const store = new PassedClasses(file);
    const first = splitByReuse([a, b], (unit) => classKey(unit, context({})), store);
    assert.deepEqual(first.run.map(unitId), [unitId(a), unitId(b)]);
    for (const unit of first.run) store.add(unitId(unit), first.keys.get(unitId(unit)) as string, '2026-11-13T00:00:00Z');
    store.save();
    const second = splitByReuse([a, b], (unit) => classKey(unit, context({ 'src/Ports/P.cs': 'changed' })), new PassedClasses(file));
    assert.deepEqual(second.run, [a]);
    assert.deepEqual(second.replayed, [b]);
  } finally {
    remove();
  }
});

test('a store that does not read is empty, never a failure', () => {
  const { dir: folder, remove } = fixtureFolder('class-reuse');
  try {
    assert.equal(new PassedClasses(join(folder, 'missing.json')).has('x', 'y'), false);
  } finally {
    remove();
  }
});

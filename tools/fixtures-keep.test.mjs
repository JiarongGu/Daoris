import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, rmSync, symlinkSync, utimesSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { KEPT, removeStale, stale } from './fixtures-keep.mjs';

const REPO = dirname(dirname(fileURLToPath(import.meta.url)));
mkdirSync(join(REPO, '_fixtures'), { recursive: true });

const HOUR = 3600_000;
const NOW = Date.parse('2026-11-12T12:00:00Z');

/** A checkout of its own whose `_fixtures/` holds `entries`, each written `hours` before NOW. */
function checkout(entries) {
  const root = mkdtempSync(join(REPO, '_fixtures', 'fixtures-keep-'));
  const fixtures = join(root, '_fixtures');
  for (const { name, hours, child, file } of entries) {
    const at = join(fixtures, name);
    if (file) {
      mkdirSync(fixtures, { recursive: true });
      writeFileSync(at, '');
    } else {
      mkdirSync(join(at, 'deep'), { recursive: true });
      writeFileSync(join(at, 'deep', 'left.txt'), 'scratch\n');
    }
    const when = new Date(NOW - hours * HOUR);
    if (!file) utimesSync(join(at, 'deep'), when, when);
    utimesSync(at, when, when);
    if (child !== undefined) {
      const fresh = new Date(NOW - child * HOUR);
      utimesSync(join(at, 'deep'), fresh, fresh);
    }
  }
  return root;
}

test('TESTDIET1: an entry older than the window goes; a recent one, one with a recent child and a cache a run keeps stay', () => {
  const root = checkout([
    { name: 'old-test-home', hours: 72 },
    { name: 'deployment-rehearsal', hours: 30 },
    { name: 'today', hours: 2 },
    { name: 'old-but-written-in', hours: 72, child: 1 },
    { name: KEPT[0], hours: 400 },
    { name: 'contracts-build.lock', hours: 50, file: true },
  ]);
  try {
    assert.deepEqual(stale(root, { now: NOW, hours: 24 }).map((each) => each.name).sort(),
      ['contracts-build.lock', 'deployment-rehearsal', 'old-test-home']);
    assert.deepEqual(stale(root, { now: NOW, hours: 48 }).map((each) => each.name).sort(), ['contracts-build.lock', 'old-test-home']);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('TESTDIET1: a link is never followed nor removed, and what it names outside is untouched', () => {
  const root = checkout([{ name: 'old', hours: 72 }]);
  const outside = join(root, 'outside');
  mkdirSync(outside, { recursive: true });
  writeFileSync(join(outside, 'kept.txt'), 'not scratch\n');
  try {
    symlinkSync(outside, join(root, '_fixtures', 'linked'), 'junction');
    const plan = stale(root, { now: NOW + 100 * HOUR, hours: 24 });
    assert.deepEqual(plan.map((each) => each.name), ['old']);
    const { removed, held } = removeStale(root, plan);
    assert.deepEqual([removed, held], [['old'], []]);
    assert.equal(existsSync(join(root, '_fixtures', 'old')), false);
    assert.equal(existsSync(join(root, '_fixtures', 'linked')), true);
    assert.equal(existsSync(join(outside, 'kept.txt')), true);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('TESTDIET1: a target that resolves outside the folder is refused; one something still holds is said and left', () => {
  const root = checkout([{ name: 'held', hours: 72 }]);
  try {
    assert.throws(() => removeStale(root, [{ name: '..', path: join(root, '_fixtures', '..') }]), /outside/);
    const refuse = () => { throw Object.assign(new Error('busy'), { code: 'EBUSY' }); };
    const { removed, held } = removeStale(root, stale(root, { now: NOW, hours: 24 }), { remove: refuse });
    assert.deepEqual([removed, held.map((each) => each.name)], [[], ['held']]);
    assert.equal(existsSync(join(root, '_fixtures', 'held')), true);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

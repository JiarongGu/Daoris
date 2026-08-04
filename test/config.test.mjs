import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, captureError } from './_fixture.mjs';
import { readManifest, writeManifest, readLock, writeLock, lockIndex } from '../src/config.mjs';
import { DaorisError } from '../src/errors.mjs';

test('a missing manifest is a tool error that says how to fix it', () => {
  const fx = makeFixture('config-missing');
  const error = captureError(() => readManifest(fx.root));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /daoris init/);
  fx.cleanup();
});

test('the manifest fills in defaults for everything but source', () => {
  const fx = makeFixture('config-defaults');
  fx.write('daoris.json', '{"source":"github:OWNER/daoris#v0.1.0"}');
  const manifest = readManifest(fx.root);
  assert.deepEqual(manifest.packs, []);
  assert.equal(manifest.target, '.claude');
  assert.equal(manifest.coreBudgetBytes, 24000);
  fx.cleanup();
});

test('a manifest without a source is a tool error', () => {
  const fx = makeFixture('config-nosource');
  fx.write('daoris.json', '{"packs":[]}');
  const error = captureError(() => readManifest(fx.root));
  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /source/);
  fx.cleanup();
});

test('the lock round-trips with entries sorted by target', () => {
  const fx = makeFixture('config-lock');
  writeLock(fx.root, {
    canonVersion: '0.1.0',
    source: 'github:OWNER/daoris#v0.1.0',
    entries: [
      { pack: 'core', source: 'core/z.md', target: 'rules/z.md', canonVersion: '0.1.0', sha256: 'bb' },
      { pack: 'core', source: 'core/a.md', target: 'rules/a.md', canonVersion: '0.1.0', sha256: 'aa' },
    ],
  });
  assert.deepEqual(readLock(fx.root).entries.map((e) => e.target), ['rules/a.md', 'rules/z.md']);
  assert.match(fx.read('daoris.lock'), /\n$/);
  fx.cleanup();
});

test('an absent lock reads as null, and lockIndex keys by target', () => {
  const fx = makeFixture('config-nolock');
  assert.equal(readLock(fx.root), null);
  const index = lockIndex({ entries: [{ target: 'rules/a.md', sha256: 'aa' }] });
  assert.equal(index.get('rules/a.md').sha256, 'aa');
  assert.equal(lockIndex(null).size, 0);
  fx.cleanup();
});

test('writeManifest produces re-readable JSON', () => {
  const fx = makeFixture('config-write');
  writeManifest(fx.root, { source: 's', packs: ['p'], target: '.claude', coreBudgetBytes: 100 });
  assert.deepEqual(readManifest(fx.root).packs, ['p']);
  fx.cleanup();
});

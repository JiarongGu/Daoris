import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, captureError } from './_fixture.ts';
import { readManifest, writeManifest, readLock, writeLock, lockIndex } from '../src/config.ts';
import { DaorisError } from '../src/errors.ts';

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
  assert.equal(manifest.coreBudgetBytes, 30000);
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
  assert.deepEqual(readLock(fx.root)!.entries.map((e) => e.target), ['rules/a.md', 'rules/z.md']);
  assert.match(fx.read('daoris.lock'), /\n$/);
  fx.cleanup();
});

test('an absent lock reads as null, and lockIndex keys by target', () => {
  const fx = makeFixture('config-nolock');
  assert.equal(readLock(fx.root), null);
  const index = lockIndex({ entries: [
    { pack: 'core', source: 'core/rules/a.md', target: 'rules/a.md', canonVersion: '0.1.0', sha256: 'aa' },
  ] });
  assert.equal(index.get('rules/a.md')!.sha256, 'aa');
  assert.equal(lockIndex(null).size, 0);
  fx.cleanup();
});

/**
 * What may leave this machine for a remote deployment is declared in the MANIFEST — tracked and
 * reviewed, because disclosure is the repository's call, not one person's local toggle (D47 §4).
 * Silence means local: the cost of the wrong default is asymmetric.
 */
test('the remote declaration is silent by default and normalizes to booleans', () => {
  const fx = makeFixture('config-remote');
  fx.write('daoris.json', '{"source":"s"}');
  assert.equal(readManifest(fx.root).remote, undefined);

  fx.write('daoris.json', '{"source":"s","remote":{"join":true}}');
  const manifest = readManifest(fx.root);
  assert.equal(manifest.remote!.join, true);
  assert.equal(manifest.remote!.knowledge, false);
  fx.cleanup();
});

test('declaring knowledge without join is refused, naming the fix', () => {
  const fx = makeFixture('config-remote-orphan');
  fx.write('daoris.json', '{"source":"s","remote":{"knowledge":true}}');
  const error = captureError(() => readManifest(fx.root));
  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /join/);
  fx.cleanup();
});

/** A JSON `null` means what absence means — local, silently — not a crash on the dereference. */
test('a null remote declaration reads as silence', () => {
  const fx = makeFixture('config-remote-null');
  fx.write('daoris.json', '{"source":"s","remote":null}');
  assert.equal(readManifest(fx.root).remote, undefined);
  fx.cleanup();
});

/**
 * A corrupt file is a TOOL error (exit 2), never a bare SyntaxError: unhandled, Node exits 1, which a
 * build gate reads as policy failure — the one thing a broken file is not.
 */
test('a manifest or lock that is not JSON fails as a tool error naming the file', () => {
  const fx = makeFixture('config-corrupt');
  fx.write('daoris.json', '{ not json');
  const manifest = captureError(() => readManifest(fx.root));
  assert.ok(manifest instanceof DaorisError);
  assert.equal(manifest.exitCode, 2);
  assert.match(manifest.message, /daoris\.json/);

  fx.write('daoris.json', '{"source":"s"}');
  fx.write('daoris.lock', '{ also not json');
  const lock = captureError(() => readLock(fx.root));
  assert.ok(lock instanceof DaorisError);
  assert.equal(lock.exitCode, 2);
  fx.cleanup();
});

test('writeManifest produces re-readable JSON', () => {
  const fx = makeFixture('config-write');
  writeManifest(fx.root, { source: 's', packs: ['p'], target: '.claude', coreBudgetBytes: 100 });
  assert.deepEqual(readManifest(fx.root).packs, ['p']);
  fx.cleanup();
});

/** The path any future `daoris declare --join` takes: the declaration must survive the round trip. */
test('a remote declaration survives writeManifest and readManifest intact', () => {
  const fx = makeFixture('config-remote-roundtrip');
  writeManifest(fx.root, {
    source: 's', packs: [], target: '.claude', coreBudgetBytes: 100,
    remote: { join: true, knowledge: true },
  });
  assert.deepEqual(readManifest(fx.root).remote, { join: true, knowledge: true });
  fx.cleanup();
});

import { test } from 'vitest';
import assert from 'node:assert/strict';
import { configDigest, inputsDigest, keyOf, replayFor, type StoredVerdict, type VerdictKey } from './verdict-store.ts';

const gate = { name: 'censuses', run: 'dotnet test A --filter Category=Census', reuse: 'inputs' };
const tool = { node: 'v24.1.0', dotnet: '10.0.100' };
const entries = [{ path: 'src/a.cs', blob: 'aaa' }, { path: 'docs/x.md', blob: 'ddd' }, { path: 'src/b.cs', blob: 'bbb' }];
const reaches = (paths: string[]) => paths.filter((path) => path.startsWith('src/'));

const keyFor = (over: Partial<{ blobs: Record<string, string>; run: string; node: string; extra: string }> = {}): VerdictKey => {
  const changed = entries.map((entry) => ({ ...entry, blob: over.blobs?.[entry.path] ?? entry.blob }));
  return keyOf({ gate: { ...gate, run: over.run ?? gate.run }, entries: changed, reaching: reaches, toolchain: { ...tool, node: over.node ?? tool.node }, extra: over.extra ?? '' });
};

const stored = (key: VerdictKey, over: Partial<StoredVerdict> = {}): StoredVerdict => ({ gate: 'censuses', verdict: 'PASS', tree: 'T1', at: '2026-11-13T10:00:00Z', key, ...over });

test('a path the gate cannot reach never changes its inputs digest; one it reaches does', () => {
  const same = inputsDigest(entries, reaches);
  assert.equal(inputsDigest(entries.map((entry) => (entry.path === 'docs/x.md' ? { ...entry, blob: 'other' } : entry)), reaches), same);
  assert.notEqual(inputsDigest(entries.map((entry) => (entry.path === 'src/a.cs' ? { ...entry, blob: 'other' } : entry)), reaches), same);
  assert.equal(inputsDigest([...entries].reverse(), reaches), same, 'the order the tree lists is no input');
});

test('the configuration is the gate declaration, so a changed command is a changed key', () => {
  assert.notEqual(configDigest(gate), configDigest({ ...gate, run: `${gate.run} --no-build` }));
  assert.equal(configDigest(gate), configDigest({ ...gate }));
});

test('an unchanged reached input replays a standing pass, saying from which tree and when', () => {
  const found = replayFor([stored(keyFor())], keyFor());
  assert.equal(found?.from, 'T1');
  assert.equal(found?.at, '2026-11-13T10:00:00Z');
  assert.match(found!.note, /replayed from tree T1 at 2026-11-13T10:00:00Z: its reached inputs, command and toolchain are unchanged/);
});

test('a changed input, command, toolchain or diff voids the replay', () => {
  const standing = [stored(keyFor())];
  assert.equal(replayFor(standing, keyFor({ blobs: { 'src/a.cs': 'new' } })), null);
  assert.equal(replayFor(standing, keyFor({ run: 'dotnet test B' })), null);
  assert.equal(replayFor(standing, keyFor({ node: 'v24.2.0' })), null);
  assert.equal(replayFor(standing, keyFor({ extra: 'other changed paths' })), null);
});

test('a failure, an ended run and a verdict with no key are never replayed, and a matching command alone is nothing', () => {
  const key = keyFor();
  assert.equal(replayFor([stored(key, { verdict: 'FAIL' })], key), null);
  assert.equal(replayFor([stored(key, { verdict: 'FLAKE' })], key), null);
  const { key: _dropped, ...unkeyed } = stored(key);
  assert.equal(replayFor([unkeyed], key), null);
  assert.equal(replayFor([stored(keyFor({ blobs: { 'src/a.cs': 'x' } }))], key), null);
});

test('the newest standing pass answers, and a newer failure of the same key withdraws it', () => {
  const key = keyFor();
  const older = stored(key, { tree: 'T1', at: '2026-11-13T09:00:00Z' });
  const newer = stored(key, { tree: 'T2', at: '2026-11-13T10:00:00Z' });
  assert.equal(replayFor([older, newer], key)?.from, 'T2');
  assert.equal(replayFor([older, newer, stored(key, { verdict: 'FAIL', tree: 'T3' })], key), null);
});

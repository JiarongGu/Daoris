import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, captureError } from './_fixture.ts';
import { readCanon, PACK_API } from '../src/canon.ts';
import { DaorisError } from '../src/errors.ts';

/**
 * 🔴 **A pack says which canon it needs, or it does not say anything** (PLUG1).
 *
 * Taken from a neighbouring application's plugin manifests, which declare an integer `apiVersion`
 * the host reads **before loading anything**. `pack.json` carried a name and a description, so a
 * pack written against a newer canon and installed by an older CLI failed in whatever way it
 * happened to fail — a missing frontmatter field, a tier that is now a region, a skill layout that
 * moved. Every one of those is a confusing error about the wrong thing.
 *
 * **Absent means 1**, so every pack that exists keeps working: the field is how a pack opts into
 * saying something, never a wall put in front of packs written before it.
 */
const pack = (fx: ReturnType<typeof makeFixture>, name: string, manifest: object) => {
  fx.write(`packs/${name}/pack.json`, JSON.stringify(manifest));
  fx.write(`packs/${name}/rules/x.md`, '---\nname: x\napplies_when: w\nenforces: e\n---\nBody.\n');
};

const canon = () => {
  const fx = makeFixture('packapi');
  fx.write('canon.json', '{"version":"0.1.0"}');
  fx.write('core/rules/a.md', '---\nname: a\napplies_when: w\nenforces: e\n---\nBody.\n');
  return fx;
};

test('a pack that names no api version is read as the first one', () => {
  const fx = canon();
  pack(fx, 'old', { name: 'old', description: 'written before the field existed' });

  assert.equal(readCanon(fx.root).packs.get('old')!.api, 1);
  fx.cleanup();
});

test('a pack may name the api version it was written against', () => {
  const fx = canon();
  pack(fx, 'current', { name: 'current', description: 'd', apiVersion: PACK_API });

  assert.equal(readCanon(fx.root).packs.get('current')!.api, PACK_API);
  fx.cleanup();
});

/**
 * The whole point: refused **before** anything is materialized, and naming BOTH numbers — a refusal
 * that says only "incompatible" sends somebody to guess which side is behind.
 */
test('a pack from a newer canon is refused naming both versions', () => {
  const fx = canon();
  pack(fx, 'future', { name: 'future', description: 'd', apiVersion: PACK_API + 1 });

  const error = captureError(() => readCanon(fx.root));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /future/);
  assert.match(error.message, new RegExp(String(PACK_API + 1)));
  assert.match(error.message, new RegExp(String(PACK_API)));
  fx.cleanup();
});

/**
 * An `apiVersion` that is not a number is a malformed manifest, not an old one — treating it as 1
 * would silently install a pack whose author meant something this build cannot know.
 */
test('an api version that is not a number is a tool error, never a default', () => {
  const fx = canon();
  pack(fx, 'odd', { name: 'odd', description: 'd', apiVersion: 'latest' });

  const error = captureError(() => readCanon(fx.root));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /odd/);
  fx.cleanup();
});

test('every pack this canon ships declares the current api', () => {
  const canonRoot = new URL('../../../canon/', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
  for (const [name, held] of readCanon(canonRoot).packs) {
    if (name === 'core') continue;
    assert.equal(held.api, PACK_API, `${name} should declare apiVersion ${PACK_API}`);
  }
});

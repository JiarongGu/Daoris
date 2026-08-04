import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText } from '../src/fsx.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (rel) => readText(join(repoRoot, rel));
const version = () => JSON.parse(read('package.json')).version;

/**
 * The version appears in four live places, and a bump that misses one is
 * invisible until a consumer's provenance header disagrees with the canon that
 * wrote it. Dated design and plan documents are deliberately excluded: they are
 * records of what was decided then, not statements about what ships now.
 */
test('the canon version tracks the package version', () => {
  assert.equal(JSON.parse(read('canon/canon.json')).version, version());
});

test("daoris's own manifest pins the version it is", () => {
  assert.equal(JSON.parse(read('daoris.json')).source.endsWith(`#v${version()}`), true);
});

test('the README install lines name the current version', () => {
  const refs = [...read('README.md').matchAll(/daoris#v(\d+\.\d+\.\d+)/g)].map((m) => m[1]);
  assert.ok(refs.length >= 3, 'the README should show the pinned install reference');
  for (const ref of refs) assert.equal(ref, version());
});

/**
 * Pre-release, so nothing is published and nothing may claim to be. The first
 * release is 0.1.0; everything before it is 0.0.x.
 */
test('development versions are 0.0.x', () => {
  assert.match(version(), /^0\.0\.\d+$/);
});

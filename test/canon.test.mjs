import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, captureError } from './_fixture.mjs';
import { readCanon, selectFiles, resolveCanonRoot } from '../src/canon.mjs';
import { DaorisError } from '../src/errors.mjs';

function seedCanon() {
  const fx = makeFixture('canon');
  fx.write('canon.json', '{"version":"0.1.0"}');
  fx.write('core/sensitive-info.md', 'core rule\n');
  fx.write('core/task-lifecycle.md', 'core rule\n');
  fx.write('packs/dotnet-library/pack.json', '{"name":"dotnet-library","description":"NuGet library"}');
  fx.write('packs/dotnet-library/rules/dev-conventions.md', 'pack rule\n');
  fx.write('packs/dotnet-library/knowledge/storage.md', 'pack knowledge\n');
  fx.write('packs/dotnet-library/README.txt', 'ignored\n');
  return fx;
}

test('readCanon reads the version, its own root, core, and packs', () => {
  const fx = seedCanon();
  const canon = readCanon(fx.root);
  assert.equal(canon.version, '0.1.0');
  assert.equal(canon.root, fx.root);
  assert.equal(canon.packs.get('core').files.length, 2);
  assert.equal(canon.packs.get('dotnet-library').description, 'NuGet library');
  fx.cleanup();
});

test('the source directory is the target directory', () => {
  const fx = seedCanon();
  const canon = readCanon(fx.root);
  const targets = selectFiles(canon, ['dotnet-library']).map((f) => f.target);
  assert.deepEqual(targets, [
    'knowledge/storage.md',
    'rules/dev-conventions.md',
    'rules/sensitive-info.md',
    'rules/task-lifecycle.md',
  ]);
  fx.cleanup();
});

test('core is selected without being asked for', () => {
  const fx = seedCanon();
  const files = selectFiles(readCanon(fx.root), []);
  assert.deepEqual(files.map((f) => f.target), ['rules/sensitive-info.md', 'rules/task-lifecycle.md']);
  assert.ok(files.every((f) => f.pack === 'core'));
  fx.cleanup();
});

test('an unknown pack is a tool error naming the available packs', () => {
  const fx = seedCanon();
  const canon = readCanon(fx.root);
  const error = captureError(() => selectFiles(canon, ['nope']));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /nope/);
  assert.match(error.message, /dotnet-library/);
  fx.cleanup();
});

test('a missing canon is a tool error naming the path', () => {
  const error = captureError(() => readCanon('no/such/canon'));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /no.such.canon/);
});

test('DAORIS_CANON overrides the packaged canon root', () => {
  assert.equal(resolveCanonRoot('/pkg').endsWith('canon'), true);
  process.env.DAORIS_CANON = 'D:/elsewhere/canon';
  assert.equal(resolveCanonRoot('/pkg'), 'D:/elsewhere/canon');
  delete process.env.DAORIS_CANON;
});

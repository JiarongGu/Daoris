import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, captureError } from './_fixture.mjs';
import { readManifest } from '../src/config.mjs';
import { commandInit, commandStatus } from '../src/commands.mjs';
import { DaorisError } from '../src/errors.mjs';

const doc = (name) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\nx\n`;

function canonFixture() {
  const fx = makeFixture('cmd-canon');
  fx.write('canon.json', '{"version":"0.1.0"}');
  fx.write('core/sensitive-info.md', doc('sensitive-info'));
  fx.write('packs/win/pack.json', '{"name":"win","description":"Windows machine traps"}');
  fx.write('packs/win/rules/gotchas.md', doc('gotchas'));
  return fx;
}

test('init writes a manifest with no packs and reports what is available', () => {
  const canonFx = canonFixture();
  const repoFx = makeFixture('cmd-init');
  repoFx.write('.claude/knowledge/storage.md', '# Storage\n');
  process.env.DAORIS_CANON = canonFx.root;

  const out = [];
  assert.equal(commandInit({ root: repoFx.root, argv: [], write: (s) => out.push(s), packageRoot: '' }), 0);
  const manifest = readManifest(repoFx.root);
  assert.deepEqual(manifest.packs, []);
  assert.equal(manifest.target, '.claude');
  assert.match(out.join('\n'), /win\s+—\s+Windows machine traps/);
  assert.match(out.join('\n'), /knowledge\/storage\.md/);

  delete process.env.DAORIS_CANON;
  canonFx.cleanup();
  repoFx.cleanup();
});

test('init refuses to overwrite an existing manifest', () => {
  const canonFx = canonFixture();
  const repoFx = makeFixture('cmd-init-twice');
  repoFx.write('daoris.json', '{"source":"s"}');
  process.env.DAORIS_CANON = canonFx.root;

  const error = captureError(() =>
    commandInit({ root: repoFx.root, argv: [], write: () => {}, packageRoot: '' }),
  );
  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /already/);

  delete process.env.DAORIS_CANON;
  canonFx.cleanup();
  repoFx.cleanup();
});

test('status reports packs, drift, and local files without failing', () => {
  const canonFx = canonFixture();
  const repoFx = makeFixture('cmd-status');
  repoFx.write('daoris.json', '{"source":"github:OWNER/daoris#v0.1.0","packs":["win"]}');
  repoFx.write('.claude/rules/house-style.md', '# local\n');
  process.env.DAORIS_CANON = canonFx.root;

  const out = [];
  assert.equal(commandStatus({ root: repoFx.root, write: (s) => out.push(s), packageRoot: '' }), 0);
  const text = out.join('\n');
  assert.match(text, /never synced/);
  assert.match(text, /house-style/);

  delete process.env.DAORIS_CANON;
  canonFx.cleanup();
  repoFx.cleanup();
});

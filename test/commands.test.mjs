import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, captureError } from './_fixture.mjs';
import { readManifest } from '../src/config.mjs';
import { commandInit, commandStatus } from '../src/commands.mjs';
import { readCanon } from '../src/canon.mjs';
import { planSync, applySync } from '../src/materialize.mjs';
import { DaorisError } from '../src/errors.mjs';

const doc = (name) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\nx\n`;

function canonFixture() {
  const fx = makeFixture('cmd-canon');
  fx.write('canon.json', '{"version":"0.1.0"}');
  fx.write('core/rules/sensitive-info.md', doc('sensitive-info'));
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

test('status says when the shipped canon is newer than the lock', () => {
  const canonFx = canonFixture();
  const repoFx = makeFixture('cmd-status-stale');
  repoFx.write('daoris.json', '{"source":"s","packs":[]}');
  repoFx.write(
    'daoris.lock',
    JSON.stringify({ version: 1, canonVersion: '0.0.9', source: 's', entries: [] }),
  );
  process.env.DAORIS_CANON = canonFx.root;

  const out = [];
  assert.equal(commandStatus({ root: repoFx.root, write: (s) => out.push(s), packageRoot: '' }), 0);
  const text = out.join('\n');
  assert.match(text, /0\.0\.9/);
  assert.match(text, /0\.1\.0/);
  assert.match(text, /update available|daoris sync/i);

  delete process.env.DAORIS_CANON;
  canonFx.cleanup();
  repoFx.cleanup();
});

test('status is silent about updates when the lock matches the canon', () => {
  const canonFx = canonFixture();
  const repoFx = makeFixture('cmd-status-current');
  repoFx.write('daoris.json', '{"source":"s","packs":[]}');
  repoFx.write(
    'daoris.lock',
    JSON.stringify({ version: 1, canonVersion: '0.1.0', source: 's', entries: [] }),
  );
  process.env.DAORIS_CANON = canonFx.root;

  const out = [];
  commandStatus({ root: repoFx.root, write: (s) => out.push(s), packageRoot: '' });
  assert.equal(/update available/i.test(out.join('\n')), false);

  delete process.env.DAORIS_CANON;
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * "A newer canon exists" is not actionable on its own. The lock already records
 * a per-file hash, so what actually changed is computable offline — no network
 * and no git. The provenance header is excluded deliberately: a version bump
 * rewrites every header, and reporting all of them as changes is noise that
 * trains people to stop reading the list.
 */
test('status names what changed, ignoring a pure version bump', () => {
  const canonFx = canonFixture();
  const repoFx = makeFixture('cmd-status-changes');
  repoFx.write('daoris.json', '{"source":"s","packs":["win"]}');
  process.env.DAORIS_CANON = canonFx.root;

  // Sync, then move the canon on: one rule reworded, one rule added.
  const manifest = readManifest(repoFx.root);
  const before = readCanon(canonFx.root);
  applySync({
    root: repoFx.root,
    manifest,
    canonVersion: before.version,
    force: false,
    plan: planSync({ root: repoFx.root, manifest, canon: before, lock: null }),
  });

  canonFx.write('canon.json', '{"version":"0.2.0"}');
  canonFx.write('core/rules/sensitive-info.md', `${doc('sensitive-info')}REWORDED\n`);
  canonFx.write('core/rules/task-lifecycle.md', doc('task-lifecycle'));

  const out = [];
  commandStatus({ root: repoFx.root, write: (s) => out.push(s), packageRoot: '' });
  const text = out.join('\n');
  assert.match(text, /changed\s+rules\/sensitive-info\.md/);
  assert.match(text, /new\s+rules\/task-lifecycle\.md/);
  // gotchas.md is untouched — only its header version moved.
  assert.equal(/gotchas/.test(text), false, 'a header-only difference is not a change');

  delete process.env.DAORIS_CANON;
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * The other half of the same problem: knowing sensitive-info changed does not
 * tell you whether to care. Only the author of the change can say that, so the
 * canon carries it and ships it in the package.
 */
test('status prints why the canon changed, for the versions being skipped', () => {
  const canonFx = canonFixture();
  const repoFx = makeFixture('cmd-status-why');
  repoFx.write('daoris.json', '{"source":"s","packs":[]}');
  repoFx.write(
    'daoris.lock',
    JSON.stringify({ version: 1, canonVersion: '0.1.0', source: 's', entries: [] }),
  );
  canonFx.write('canon.json', '{"version":"0.3.0"}');
  canonFx.write(
    'CHANGELOG.md',
    '# Canon changelog\n\n## 0.3.0\n\n- Now covers commit messages.\n\n## 0.2.0\n\n- Retired the legacy layout rule.\n\n## 0.1.0\n\n- The first canon.\n',
  );
  process.env.DAORIS_CANON = canonFx.root;

  const out = [];
  commandStatus({ root: repoFx.root, write: (s) => out.push(s), packageRoot: '' });
  const text = out.join('\n');
  assert.match(text, /why 0\.2\.0/);
  assert.match(text, /Retired the legacy layout rule/);
  assert.match(text, /why 0\.3\.0/);
  assert.match(text, /Now covers commit messages/);
  assert.equal(/why 0\.1\.0/.test(text), false, 'the version already installed is not news');

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

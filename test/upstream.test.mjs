import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { makeFixture, captureError } from './_fixture.mjs';
import { readCanon } from '../src/canon.mjs';
import { readManifest, readLock } from '../src/config.mjs';
import { planSync, applySync } from '../src/materialize.mjs';
import { upstreamFile } from '../src/upstream.mjs';
import { readText } from '../src/fsx.mjs';
import { DaorisError } from '../src/errors.mjs';

const doc = (name) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\nBody of ${name}.\n`;

function synced() {
  const canonFx = makeFixture('up-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/sensitive-info.md', doc('sensitive-info'));

  const repoFx = makeFixture('up-repo');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));
  repoFx.write('.claude/rules/house-style.md', doc('house-style'));
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  applySync({
    root: repoFx.root,
    manifest,
    canonVersion: canon.version,
    force: false,
    plan: planSync({ root: repoFx.root, manifest, canon, lock: null }),
  });
  return { canonFx, repoFx };
}

const promote = ({ canonFx, repoFx }, file) =>
  upstreamFile({
    root: repoFx.root,
    manifest: readManifest(repoFx.root),
    lock: readLock(repoFx.root),
    canonRoot: canonFx.root,
    file,
  });

const edited = () =>
  `<!-- daoris: core/core/sensitive-info.md @ 0.1.0 -->\n${doc('sensitive-info')}IMPROVED.\n`;

test('a local edit lands in the canon without the provenance header', () => {
  const fx = synced();
  fx.repoFx.write('.claude/rules/sensitive-info.md', edited());
  const result = promote(fx, 'rules/sensitive-info.md');
  const canonText = readText(join(fx.canonFx.root, 'core/sensitive-info.md'));
  assert.equal(result.source, 'core/sensitive-info.md');
  assert.match(canonText, /IMPROVED\./);
  assert.equal(canonText.startsWith('---\n'), true);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a bare filename resolves to its locked target', () => {
  const fx = synced();
  assert.equal(promote(fx, 'sensitive-info.md').target, 'rules/sensitive-info.md');
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a target-dir-prefixed path resolves too', () => {
  const fx = synced();
  assert.equal(promote(fx, '.claude/rules/sensitive-info.md').target, 'rules/sensitive-info.md');
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a local file has nothing to upstream and says so', () => {
  const fx = synced();
  const error = captureError(() => promote(fx, 'rules/house-style.md'));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /local/i);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('after upstreaming, a re-sync reports no drift', () => {
  const fx = synced();
  fx.repoFx.write('.claude/rules/sensitive-info.md', edited());
  promote(fx, 'rules/sensitive-info.md');
  const canon = readCanon(fx.canonFx.root);
  const manifest = readManifest(fx.repoFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest,
    canon,
    lock: readLock(fx.repoFx.root),
  });
  applySync({ root: fx.repoFx.root, manifest, plan, canonVersion: canon.version, force: true });
  const after = planSync({
    root: fx.repoFx.root,
    manifest,
    canon,
    lock: readLock(fx.repoFx.root),
  });
  assert.deepEqual(after.drifted, []);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from './_fixture.mjs';
import { readCanon } from '../src/canon.mjs';
import { readManifest, readLock } from '../src/config.mjs';
import { planSync, applySync } from '../src/materialize.mjs';
import { inspect, commandCheck } from '../src/drift.mjs';

const doc = (name) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\nBody of ${name}.\n`;

function synced(packs = [], coreBudgetBytes = 24000) {
  const canonFx = makeFixture('check-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  canonFx.write('packs/win/pack.json', '{"name":"win","description":"Windows"}');
  canonFx.write('packs/win/rules/gotchas.md', doc('gotchas'));

  const repoFx = makeFixture('check-repo');
  repoFx.write(
    'daoris.json',
    JSON.stringify({ source: 'github:OWNER/daoris#v0.1.0', packs, coreBudgetBytes }),
  );
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  const plan = planSync({ root: repoFx.root, manifest, canon, lock: null });
  applySync({ root: repoFx.root, manifest, plan, canonVersion: canon.version, force: false });
  return { canonFx, repoFx };
}

const look = (repoFx) =>
  inspect({ root: repoFx.root, manifest: readManifest(repoFx.root), lock: readLock(repoFx.root) });

test('a clean repo checks clean and exits 0', () => {
  const { canonFx, repoFx } = synced();
  assert.equal(look(repoFx).ok, true);
  assert.equal(commandCheck({ root: repoFx.root, write: () => {} }), 0);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('check passes with the canon source completely absent', () => {
  const { canonFx, repoFx } = synced();
  canonFx.cleanup(); // the canon is gone; check must not care
  assert.equal(commandCheck({ root: repoFx.root, write: () => {} }), 0);
  repoFx.cleanup();
});

test('a hand-edited vendored file is drift, named, exit 1', () => {
  const { canonFx, repoFx } = synced();
  repoFx.write('.claude/rules/sensitive-info.md', 'hand-edited\n');
  const report = look(repoFx);
  assert.deepEqual(report.drifted, ['rules/sensitive-info.md']);
  assert.equal(report.ok, false);
  const out = [];
  assert.equal(commandCheck({ root: repoFx.root, write: (s) => out.push(s) }), 1);
  assert.match(out.join('\n'), /sensitive-info/);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('a deleted vendored file is missing', () => {
  const { canonFx, repoFx } = synced();
  rmSync(join(repoFx.root, '.claude/rules/sensitive-info.md'));
  assert.deepEqual(look(repoFx).missing, ['rules/sensitive-info.md']);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('a pack added to the manifest but not yet synced is stale', () => {
  const { canonFx, repoFx } = synced();
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: ['win'] }));
  assert.deepEqual(look(repoFx).stalePacks, ['win']);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('an over-budget core fails the check', () => {
  const { canonFx, repoFx } = synced([], 10);
  const report = look(repoFx);
  assert.equal(report.overBudget, true);
  assert.ok(report.coreBytes > 10);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('a stale index fails the check', () => {
  const { canonFx, repoFx } = synced();
  repoFx.write('.claude/rules/RULES_INDEX.md', '# stale\n');
  assert.equal(look(repoFx).indexStale, true);
  canonFx.cleanup();
  repoFx.cleanup();
});

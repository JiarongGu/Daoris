import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { rmSync } from 'node:fs';
import { makeFixture, captureError } from './_fixture.mjs';
import { readCanon } from '../src/canon.mjs';
import { readText } from '../src/fsx.mjs';
import { readManifest, readLock } from '../src/config.mjs';
import { planSync, applySync } from '../src/materialize.mjs';
import { DaorisError } from '../src/errors.mjs';

const doc = (name) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\nBody of ${name}.\n`;

function seed(packs = []) {
  const canonFx = makeFixture('sync-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  canonFx.write('packs/win/pack.json', '{"name":"win","description":"Windows"}');
  canonFx.write('packs/win/rules/gotchas.md', doc('gotchas'));

  const repoFx = makeFixture('sync-repo');
  repoFx.write('daoris.json', JSON.stringify({ source: 'github:OWNER/daoris#v0.1.0', packs }));
  repoFx.write('.claude/rules/house-style.md', doc('house-style'));
  return { canonFx, repoFx };
}

function run({ canonFx, repoFx }, force = false) {
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  const plan = planSync({ root: repoFx.root, manifest, canon, lock: readLock(repoFx.root) });
  return applySync({ root: repoFx.root, manifest, plan, canonVersion: canon.version, force });
}

test('a fresh sync writes the tree, the header, and the lock', () => {
  const fx = seed(['win']);
  run(fx);
  const written = fx.repoFx.read('.claude/rules/sensitive-info.md');
  // Frontmatter first, provenance under it — never above (see document.mjs).
  assert.equal(written.startsWith('---\n'), true);
  assert.match(written, /---\n<!-- daoris: core\/core\/rules\/sensitive-info\.md @ 0\.1\.0 /);
  assert.match(written, /Body of sensitive-info/);
  assert.equal(fx.repoFx.exists('.claude/rules/gotchas.md'), true);
  assert.deepEqual(
    readLock(fx.repoFx.root).entries.map((e) => e.target).sort(),
    ['rules/gotchas.md', 'rules/sensitive-info.md'],
  );
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a local file is never touched and never enters the lock', () => {
  const fx = seed();
  const before = fx.repoFx.read('.claude/rules/house-style.md');
  run(fx);
  assert.equal(fx.repoFx.read('.claude/rules/house-style.md'), before);
  assert.equal(readLock(fx.repoFx.root).entries.some((e) => e.target.includes('house-style')), false);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('retiring a canonical file removes it from the repo on the next sync', () => {
  const fx = seed(['win']);
  run(fx);
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  run(fx);
  assert.equal(fx.repoFx.exists('.claude/rules/gotchas.md'), false);
  assert.equal(readLock(fx.repoFx.root).entries.some((e) => e.target.includes('gotchas')), false);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a locally-drifted file is refused without --force and overwritten with it', () => {
  const fx = seed();
  run(fx);
  fx.repoFx.write('.claude/rules/sensitive-info.md', 'hand-edited\n');
  const error = captureError(() => run(fx));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /sensitive-info/);
  assert.match(error.message, /--force/);
  run(fx, true);
  assert.match(fx.repoFx.read('.claude/rules/sensitive-info.md'), /Body of sensitive-info/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a canon improvement reaches an untouched repo without --force', () => {
  const fx = seed();
  run(fx);
  // The rule got better upstream. The repo did nothing at all.
  fx.canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info').replace('Body of', 'IMPROVED body of'));
  run(fx);
  assert.match(fx.repoFx.read('.claude/rules/sensitive-info.md'), /IMPROVED body of sensitive-info/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('a canon version bump alone is not drift', () => {
  const fx = seed();
  run(fx);
  fx.canonFx.write('canon.json', '{"version":"0.2.0"}');
  const canon = readCanon(fx.canonFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon,
    lock: readLock(fx.repoFx.root),
  });
  // Only the provenance header moved. Reporting that as "you edited this" would
  // accuse every consumer of an edit nobody made.
  assert.deepEqual(plan.drifted, []);
  assert.deepEqual(plan.collisions, []);
  run(fx);
  assert.match(fx.repoFx.read('.claude/rules/sensitive-info.md'), /@ 0\.2\.0 /);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

/**
 * A canonical file renamed upstream reaches consumers as a delete plus an add,
 * which loses nothing and explains nothing. Detected by content rather than
 * declared in metadata: a ledger can claim a rename that never happened, and
 * this cannot — it is reading what actually moved, the way version control does.
 */
test('a renamed canonical file is reported as a rename, not a delete plus an add', () => {
  const fx = seed(['win']);
  run(fx);
  const body = readText(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  fx.canonFx.write('packs/win/rules/windows-traps.md', body);

  const canon = readCanon(fx.canonFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon,
    lock: readLock(fx.repoFx.root),
  });
  assert.deepEqual(plan.renames, [{ from: 'rules/gotchas.md', to: 'rules/windows-traps.md' }]);

  // The outcome is unchanged — only the explanation improves.
  run(fx);
  assert.equal(fx.repoFx.exists('.claude/rules/gotchas.md'), false);
  assert.equal(fx.repoFx.exists('.claude/rules/windows-traps.md'), true);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('an unrelated retirement and addition are not called a rename', () => {
  const fx = seed(['win']);
  run(fx);
  rmSync(join(fx.canonFx.root, 'packs/win/rules/gotchas.md'));
  fx.canonFx.write('packs/win/rules/something-else.md', doc('something-else'));

  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon: readCanon(fx.canonFx.root),
    lock: readLock(fx.repoFx.root),
  });
  assert.deepEqual(plan.renames, []);
  assert.deepEqual(plan.deletes, ['rules/gotchas.md']);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('an unchanged file is planned as unchanged, not rewritten', () => {
  const fx = seed();
  run(fx);
  const canon = readCanon(fx.canonFx.root);
  const plan = planSync({
    root: fx.repoFx.root,
    manifest: readManifest(fx.repoFx.root),
    canon,
    lock: readLock(fx.repoFx.root),
  });
  assert.ok(plan.writes.every((w) => w.state === 'unchanged'));
  assert.deepEqual(plan.deletes, []);
  assert.deepEqual(plan.drifted, []);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('adopting a repo that already owns a canonical filename refuses rather than clobbering', () => {
  const fx = seed();
  // The repo wrote its own sensitive-info.md long before it ever heard of daoris.
  fx.repoFx.write('.claude/rules/sensitive-info.md', 'our own hard-won rule\n');

  const error = captureError(() => run(fx));
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /sensitive-info/);
  assert.match(error.message, /already/i);
  assert.equal(fx.repoFx.read('.claude/rules/sensitive-info.md'), 'our own hard-won rule\n');

  run(fx, true); // --force is the deliberate "yes, take the canonical one"
  assert.match(fx.repoFx.read('.claude/rules/sensitive-info.md'), /Body of sensitive-info/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

test('an adopted file identical to the canon is not a collision', () => {
  const fx = seed();
  run(fx);
  const vendored = fx.repoFx.read('.claude/rules/sensitive-info.md');
  const fresh = seed();
  fresh.repoFx.write('.claude/rules/sensitive-info.md', vendored);
  run(fresh); // byte-identical: nothing to warn about
  assert.deepEqual(
    readLock(fresh.repoFx.root).entries.map((e) => e.target),
    ['rules/sensitive-info.md'],
  );
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
  fresh.canonFx.cleanup();
  fresh.repoFx.cleanup();
});

test('sync writes the index too, so a synced repo is immediately consistent', () => {
  const fx = seed();
  run(fx);
  const index = fx.repoFx.read('.claude/rules/RULES_INDEX.md');
  assert.match(index, /sensitive-info/);
  assert.match(index, /house-style.*\(local\)/);
  fx.canonFx.cleanup();
  fx.repoFx.cleanup();
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { Fixture } from './_fixture.ts';
import { makeFixture } from './_fixture.ts';
import { readCanon } from '../src/canon.ts';
import { readManifest, readLock } from '../src/config.ts';
import { planSync, applySync } from '../src/materialize.ts';
import { inspect, commandCheck } from '../src/drift.ts';

const doc = (name: string) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\nBody of ${name}.\n`;

function synced(packs = [], coreBudgetBytes = 30000) {
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

const look = (repoFx: Fixture) =>
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
  repoFx.write('AGENTS.md', repoFx.read('AGENTS.md').replace('Body of sensitive-info.', 'hand-edited'));
  const report = look(repoFx);
  assert.deepEqual(report.drifted, ['rules/sensitive-info.md']);
  assert.equal(report.ok, false);
  const out: string[] = [];
  assert.equal(commandCheck({ root: repoFx.root, write: (s: string) => out.push(s) }), 1);
  assert.match(out.join('\n'), /sensitive-info/);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('a deleted vendored file is missing', () => {
  const { canonFx, repoFx } = synced();
  // The whole region gone is every rule in it missing — the span's equivalent of a deleted file.
  rmSync(join(repoFx.root, 'AGENTS.md'));
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

/**
 * The budget reports and does not fail (D54). Size is a judgement, not a fact the tool established:
 * one byte over a number somebody chose is not wrong, and a gate that stops a build over a judgement
 * gets its number raised rather than read. Everything else `check` reports IS a fact, and those still
 * fail — which is the distinction worth pinning, because the tempting simplification is to make the
 * whole report advisory.
 */
test('an over-budget core is reported but does not fail the check', () => {
  const { canonFx, repoFx } = synced([], 10);
  const report = look(repoFx);
  assert.equal(report.overBudget, true);
  assert.ok(report.coreBytes > 10);
  assert.equal(report.ok, true, 'over budget must not fail the check — it is advisory');

  const out: string[] = [];
  assert.equal(commandCheck({ root: repoFx.root, write: (s: string) => out.push(s) }), 0);
  const text = out.join('\n');
  assert.match(text, /advisory/, 'the run must SAY it is advisory rather than staying silent');
  assert.match(text, /over by/, 'and say by how much — an unquantified warning is one nobody acts on');
  canonFx.cleanup();
  repoFx.cleanup();
});

/** The other half of the same rule: a fact the tool established still stops the run. */
test('drift still fails the check — only the budget is advisory', () => {
  const { canonFx, repoFx } = synced();
  repoFx.write('AGENTS.md', repoFx.read('AGENTS.md').replace('Body of sensitive-info.', '# locally edited'));
  const report = look(repoFx);
  assert.equal(report.ok, false);
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * 🔴 The roster's on-demand rows, rebuilt from disk and compared — offline and canon-free, which is
 * what `check` inside a build gate needs (D8). It catches the case that actually happens: a document
 * added to a tier that IS still files, and the region never re-synced.
 *
 * The rules rows are deliberately not checked. They come from frontmatter the span strips on the way
 * in, so nothing without the canon can say what they ought to be — and a canon change is `status`'s
 * report, which is where a canon change belongs.
 */
test('a roster that no longer matches the on-demand tiers fails the check', () => {
  const { canonFx, repoFx } = synced();
  assert.equal(look(repoFx).indexStale, false);

  repoFx.write('.claude/knowledge/ours.md', doc('ours'));

  assert.equal(look(repoFx).indexStale, true);
  canonFx.cleanup();
  repoFx.cleanup();
});

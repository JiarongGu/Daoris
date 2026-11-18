import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { Fixture } from '../_fixture.ts';
import { makeFixture } from '../_fixture.ts';
import { readCanon } from '../../src/doctrine/canon.ts';
import { readManifest, readLock } from '../../src/doctrine/config.ts';
import { planSync, applySync } from '../../src/doctrine/materialize.ts';
import { inspect, commandCheck } from '../../src/doctrine/drift.ts';

const doc = (name: string) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\nBody of ${name}.\n`;

function synced(packs = [], coreBudget: object = { words: 4500 }, documents?: object) {
  const canonFx = makeFixture('check-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  canonFx.write('packs/win/pack.json', '{"name":"win","description":"Windows"}');
  canonFx.write('packs/win/rules/gotchas.md', doc('gotchas'));

  const repoFx = makeFixture('check-repo');
  repoFx.write(
    'daoris.json',
    JSON.stringify({ source: 'github:OWNER/daoris#v0.1.0', packs, coreBudget, ...(documents ? { documents } : {}) }),
  );
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  const plan = planSync({ root: repoFx.root, manifest, canon, lock: null });
  applySync({ root: repoFx.root, manifest, plan, canon, force: false });
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
 * A ceiling the manifest does not declare failing reports and does not fail: a repository that has not chosen to
 * hold a size hears it on every run, and its build is not stopped by a number it never agreed to hold.
 */
test('an over-budget core is reported but does not fail the check', () => {
  const { canonFx, repoFx } = synced([], { words: 3 });
  const report = look(repoFx);
  assert.equal(report.overBudget, true);
  assert.ok(report.coreWords > 3);
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
 * 🔴 The index's rows, rebuilt from disk and compared — offline and canon-free, which is what `check`
 * inside a build gate needs. It catches the case that actually happens: a document added to a
 * tier that IS files, and the index never re-synced.
 *
 * The rules rows are deliberately not checked. They come from frontmatter the span strips on the way
 * in, so nothing without the canon can say what they ought to be — and a canon change is `status`'s
 * report, which is where a canon change belongs.
 */
test('an index that no longer matches the on-demand tiers fails the check', () => {
  const { canonFx, repoFx } = synced();
  assert.equal(look(repoFx).indexStale, false);

  repoFx.write('.claude/knowledge/ours.md', doc('ours'));

  assert.equal(look(repoFx).indexStale, true);
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * The brief is paid for by every session, so a repository may hold it to its ceiling: declared `fails`, the root
 * instruction file over its words or its bytes fails the check and names the cut, and under it the check is clean.
 */
test('a brief over a ceiling declared failing fails the check, and under it passes', () => {
  const { canonFx, repoFx } = synced([], { words: 4500 }, { brief: { words: 5000, bytes: 40, fails: true } });
  const out: string[] = [];
  assert.equal(commandCheck({ root: repoFx.root, write: (s: string) => out.push(s) }), 1);
  const report = look(repoFx);
  assert.equal(report.factsOk, true);
  assert.deepEqual(report.documentsOver.map((over) => `${over.unit} ${over.fails}`), ['bytes true']);
  assert.match(out.join('\n'), /the brief \(AGENTS\.md\) is \d+ bytes of 40 .*fails, as daoris\.json declares/);
  assert.doesNotMatch(out.join('\n'), /run 'daoris sync'/);

  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [], documents: { brief: { words: 5000, bytes: 40 } } }));
  assert.equal(commandCheck({ root: repoFx.root, write: () => {} }), 0, 'undeclared failing, a ceiling only reports');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [], documents: { brief: { words: 5000, bytes: 9000, fails: true } } }));
  assert.equal(commandCheck({ root: repoFx.root, write: () => {} }), 0, 'under its ceiling, the brief passes');
  canonFx.cleanup();
  repoFx.cleanup();
});

test('a core over its words declared failing fails the check', () => {
  const { canonFx, repoFx } = synced([], { words: 3, fails: true });
  assert.equal(look(repoFx).sizeFails, true);
  assert.equal(commandCheck({ root: repoFx.root, write: () => {} }), 1);
  canonFx.cleanup();
  repoFx.cleanup();
});

/** A hand-written manifest: the byte ceiling the words replaced is refused with the line that replaces it, never ignored. */
test('a manifest still carrying coreBudgetBytes is refused with its replacement', () => {
  const { canonFx, repoFx } = synced();
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [], coreBudgetBytes: 30000 }));
  assert.throws(() => readManifest(repoFx.root), /coreBudgetBytes, which is retired.*"coreBudget": \{ "words": 4500 \}/);
  canonFx.cleanup();
  repoFx.cleanup();
});


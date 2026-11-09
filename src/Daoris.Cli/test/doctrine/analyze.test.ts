import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Fixture } from '../_fixture.ts';
import { makeFixture } from '../_fixture.ts';
import { readCanon } from '../../src/doctrine/canon.ts';
import { readManifest, readLock } from '../../src/doctrine/config.ts';
import { planSync, applySync } from '../../src/doctrine/materialize.ts';
import type { Lock } from '../../src/doctrine/records.ts';
import { analyze, commandAnalyze } from '../../src/doctrine/analyze.ts';
import { DaorisError } from '../../src/platform/errors.ts';

const doc = (name: string, body = 'Body.') =>
  `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\n${body}\n`;

function seedCanon() {
  const fx = makeFixture('analyze-canon');
  fx.write('canon.json', '{"version":"0.1.0"}');
  fx.write('core/rules/file-tool-discipline.md', doc('file-tool-discipline',
    'Inspect files with the dedicated read and search tools rather than shell equivalents. '
    + 'Reserve the shell for genuine shell work; destructive commands deserve care.'));
  // A knowledge document too, because that tier is files — which is where a collision can
  // happen, the always-loaded tier being a span.
  fx.write('core/knowledge/reaching-in.md', doc('reaching-in', 'Never repair another repository.'));
  fx.write('packs/win/pack.json', '{"name":"win","description":"Windows"}');
  fx.write('packs/win/rules/windows-machine.md', doc('windows-machine', 'PowerShell and encoding traps.'));
  return fx;
}

const run = (repoFx: Fixture, canonFx: Fixture, packs: string[] = [], lock: Lock | null = null) =>
  analyze({
    root: repoFx.root,
    canon: readCanon(canonFx.root),
    packs,
    target: '.claude',
    budgetLimit: 30000,
    lock,
  });

test('a repository with no doctrine reports a fresh adoption', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-fresh');

  const report = run(repoFx, canonFx);

  assert.deepEqual(report.existing.rules, []);
  assert.deepEqual(report.collisions, []);
  assert.deepEqual(report.twins, []);
  assert.equal(report.budget.current, 0);
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * The question the command exists to answer, which must not take a sync to find out.
 */
/**
 * A collision is still a collision for every tier that is FILES. The always-loaded tier is a span
 * and a repository's own `.claude/rules/x.md` is not at a path the canon claims —
 * `doctor` is what reports one that restates a canonical rule.
 */
test('a file the repository wrote at a canonical path is a collision', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-collide');
  repoFx.write('.claude/knowledge/reaching-in.md', '# Ours, written first\n');

  const report = run(repoFx, canonFx);

  assert.deepEqual(report.collisions, ['knowledge/reaching-in.md']);
  assert.deepEqual(report.updates, []);
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * Provenance decides which of the two it is. Without the lock, an already-adopted repository
 * reports every canonical file as a collision — which is both alarming and wrong.
 */
test('a file daoris already owns is an update, not a collision', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-owned');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));

  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  applySync({
    root: repoFx.root,
    manifest,
    canonVersion: canon.version,
    force: false,
    plan: planSync({ root: repoFx.root, manifest, canon, lock: null }),
  });

  // The canon moves on underneath it.
  canonFx.write('core/rules/file-tool-discipline.md', doc('file-tool-discipline', 'Reworded upstream.'));

  const report = run(repoFx, canonFx, [], readLock(repoFx.root));

  assert.deepEqual(report.collisions, []);
  assert.deepEqual(report.updates, ['rules/file-tool-discipline.md']);
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * `doctor` cannot answer this before adoption, because it compares against the lock and there is
 * none — which is exactly when knowing is most useful. A twin found now is a deliberate decision; one
 * found afterwards is a duplicate already living in the tree.
 */
test('a renamed twin is found BEFORE adoption, against the canon', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-twin');
  repoFx.write('.claude/rules/shell-discipline.md',
    '# Keep inspection off the shell\n\n'
    + 'Reach for the dedicated read and search tools when inspecting files. Reserve the shell for '
    + 'genuine shell work, and treat destructive commands with care.\n');

  const report = run(repoFx, canonFx);

  const twin = report.twins.find((t) => t.local === 'rules/shell-discipline.md');
  assert.ok(twin, `expected a twin, got ${JSON.stringify(report.twins)}`);
  assert.equal(twin.canonical, 'rules/file-tool-discipline.md');
  canonFx.cleanup();
  repoFx.cleanup();
});

/** As `doctor`, only a skill's entry file is compared; a template beside it is not a document. */
test("a skill's templates are never a twin candidate before adoption either", () => {
  const canonFx = seedCanon();
  const shell = 'Inspect files with the dedicated read and search tools rather than shell equivalents. '
    + 'Reserve the shell for genuine shell work; destructive commands deserve care.';
  canonFx.write('core/skills/set-up/SKILL.md', '---\nname: set-up\ndescription: d\n---\n\nArrange the records.\n');
  canonFx.write('core/skills/set-up/templates/row.md', `# A row\n\n${shell}\n`);
  const repoFx = makeFixture('analyze-template');
  repoFx.write('.claude/skills/dispatch/SKILL.md', `---\nname: dispatch\ndescription: d\n---\n\n${shell}\n`);
  repoFx.write('.claude/skills/dispatch/templates/notes.md', '# Notes\n\nArrange the records.\n');

  const report = run(repoFx, canonFx);

  assert.deepEqual(report.twins.filter((twin) => twin.local.startsWith('skills/')), []);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('the projected budget counts the rules a pack would add', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-budget');
  repoFx.write('.claude/rules/house.md', doc('house'));

  const withoutPack = run(repoFx, canonFx, []);
  const withPack = run(repoFx, canonFx, ['win']);

  assert.equal(withoutPack.budget.current, withPack.budget.current);
  assert.ok(
    withPack.budget.projected > withoutPack.budget.projected,
    'adding a pack must raise the always-loaded projection',
  );
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * 🔴 After adoption a rule's body lives in the region, so the budget is measured there and a rule the region already
 * holds adds nothing: counting every selected rule again showed a repository over its budget by what it already carried.
 */
test('on an adopted repository the budget is the region, and a rule it holds is not added again', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-adopted-budget');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: ['win'] }));
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  applySync({
    root: repoFx.root, manifest, canonVersion: canon.version, force: false,
    plan: planSync({ root: repoFx.root, manifest, canon, lock: null }),
  });

  const adopted = run(repoFx, canonFx, ['win'], readLock(repoFx.root));
  const wider = (() => {
    canonFx.write('packs/more/pack.json', '{"name":"more","description":"More"}');
    canonFx.write('packs/more/rules/more-things.md', doc('more-things', 'Another rule the region does not hold yet.'));
    return run(repoFx, canonFx, ['win', 'more'], readLock(repoFx.root));
  })();

  assert.ok(adopted.budget.current > 0, 'the region is measured');
  assert.equal(adopted.budget.projected, adopted.budget.current);
  assert.ok(wider.budget.projected > wider.budget.current, 'a rule the region lacks is added');
  canonFx.cleanup();
  repoFx.cleanup();
});

test('a repository with no manifest is analysable — that is the point', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-no-manifest');
  repoFx.write('.claude/rules/house.md', doc('house'));

  const report = run(repoFx, canonFx, ['win']);

  assert.equal(report.existing.rules.length, 1);
  assert.equal(report.budget.projected > 0, true);
  canonFx.cleanup();
  repoFx.cleanup();
});

// The claim is the sentence, not the line breaks it wraps at, so the assertions read a
// whitespace-collapsed copy — otherwise re-wrapping one line silently breaks a check about meaning.
const printed = (repoFx: Fixture, canonFx: Fixture): string => {
  process.env.DAORIS_CANON = canonFx.root;
  const out: string[] = [];
  commandAnalyze({ root: repoFx.root, argv: [], write: (s: string) => out.push(s), packageRoot: '' });
  delete process.env.DAORIS_CANON;
  return out.join('\n').replace(/\s+/g, ' ');
};

test('a manifest that exists and cannot be read is refused, not read as no adoption', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-bad-manifest');
  repoFx.write('daoris.json', '{ not json');
  process.env.DAORIS_CANON = canonFx.root;
  try {
    assert.throws(
      () => commandAnalyze({ root: repoFx.root, argv: [], write: () => {}, packageRoot: '' }),
      (error: Error) => error instanceof DaorisError && /daoris.json is not valid JSON/.test(error.message),
    );
  } finally {
    delete process.env.DAORIS_CANON;
    canonFx.cleanup();
    repoFx.cleanup();
  }
});

/**
 * The verdict a person adopting reads before anything else: the
 * always-loaded tier lands IN `AGENTS.md`, so a repository already on that convention shares the
 * file rather than being blind to what daoris writes. Only the on-demand tiers stay unreachable.
 *
 * This is checked on the PRINTED output because that is where the claim lives — the report object
 * carries the evidence and says nothing about reach.
 */
test('the AGENTS.md convention is told the always-loaded tier lands in its own file', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-agents-md');
  repoFx.write('AGENTS.md', '# house doctrine\n');

  const text = printed(repoFx, canonFx);

  assert.match(text, /ALSO SEEN.*AGENTS\.md convention/);
  assert.match(text, /always-loaded tier lands IN AGENTS\.md/);
  assert.doesNotMatch(text, /invisible to them/);
  canonFx.cleanup();
  repoFx.cleanup();
});

/**
 * The other half, unchanged: a harness that reads neither the region's file nor the target sees
 * nothing daoris writes, and the silence of that failure is the reason this line exists.
 */
test('a harness that reads neither file is still told what it installs is invisible', () => {
  const canonFx = seedCanon();
  const repoFx = makeFixture('analyze-cursor-only');
  repoFx.write('.cursorrules', 'house rules\n');

  const text = printed(repoFx, canonFx);

  assert.match(text, /ALSO SEEN.*Cursor/);
  assert.match(text, /invisible to them/);
  assert.doesNotMatch(text, /always-loaded tier lands IN/);
  canonFx.cleanup();
  repoFx.cleanup();
});

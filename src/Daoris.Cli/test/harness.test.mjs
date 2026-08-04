import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture } from './_fixture.mjs';
import {
  detectHarnesses, verifyHarnessContract, harnessVerdict,
  resolveHarness, HARNESSES, DEFAULT_HARNESS, alwaysLoadedTiers, tierNames,
} from '../src/harness.mjs';
import { readManifest } from '../src/config.mjs';
import { captureError } from './_fixture.mjs';
import { DaorisError } from '../src/errors.mjs';

test('the supported harness is recognised by its own layout', () => {
  const fx = makeFixture('harness-claude');
  fx.write('CLAUDE.md', '# project\n');
  fx.write('.claude/rules/a.md', '# a\n');

  const detected = detectHarnesses(fx.root);

  const claude = detected.find((h) => h.id === 'claude-code');
  assert.ok(claude);
  assert.equal(claude.supported, true);
  fx.cleanup();
});

/**
 * The failure this guards against is silent. Install the Claude layout in a repository whose agent
 * reads AGENTS.md and every file is present, correct, and never loaded — there is no error anywhere.
 */
test('a repository built for another harness is reported, not guessed at', () => {
  const fx = makeFixture('harness-other');
  fx.write('AGENTS.md', '# instructions\n');
  fx.write('.cursorrules', 'rules\n');

  const { supported, others } = harnessVerdict(fx.root);

  assert.deepEqual(supported, []);
  assert.deepEqual(others.map((h) => h.id).sort(), ['agents-md', 'cursor']);
  assert.ok(others.every((h) => h.evidence.length > 0), 'a report must name what it saw');
  fx.cleanup();
});

test('a repository with no agent setup at all is not a problem', () => {
  const fx = makeFixture('harness-none');
  fx.write('README.md', '# just code\n');

  assert.deepEqual(detectHarnesses(fx.root), []);
  fx.cleanup();
});

/**
 * Every contract checked here fails SILENTLY. Anything that would fail loudly does not need a check,
 * because the failure is its own report.
 */
test('a skill without frontmatter is reported — it installs but never fires', () => {
  const fx = makeFixture('harness-skill-nofm');
  fx.write('.claude/skills/broken/SKILL.md', 'Steps, but no frontmatter.\n');

  const problems = verifyHarnessContract(fx.root, '.claude');

  assert.equal(problems.length, 1);
  assert.match(problems[0], /does not begin with frontmatter/);
  fx.cleanup();
});

test('a skill missing name or description is reported', () => {
  const fx = makeFixture('harness-skill-partial');
  fx.write('.claude/skills/partial/SKILL.md', '---\nname: partial\n---\n\nSteps.\n');

  const problems = verifyHarnessContract(fx.root, '.claude');

  assert.equal(problems.length, 1);
  assert.match(problems[0], /never fires/);
  fx.cleanup();
});

/** The tier is the directory (D7), so a rule filed one level down is simply never loaded. */
test('a nested rule is reported, because only the top level is read', () => {
  const fx = makeFixture('harness-nested');
  fx.write('.claude/rules/area/deep.md', '# deep\n');

  const problems = verifyHarnessContract(fx.root, '.claude');

  assert.equal(problems.length, 1);
  assert.match(problems[0], /only the top level/);
  fx.cleanup();
});

test('a correct tree reports no problems', () => {
  const fx = makeFixture('harness-ok');
  fx.write('.claude/rules/a.md', '---\nname: a\napplies_when: w\nenforces: e\n---\n\nBody.\n');
  fx.write('.claude/skills/good/SKILL.md', '---\nname: good\ndescription: does a thing\n---\n\nSteps.\n');
  fx.write('.claude/skills/good/reference.md', 'Supporting detail.\n');

  assert.deepEqual(verifyHarnessContract(fx.root, '.claude'), []);
  fx.cleanup();
});

/**
 * The seam a second harness grows from. One implementation ships (D23); what these pin is that the
 * facts live in ONE descriptor rather than scattered as constants, so adding another is an addition
 * and not an excavation.
 */
test('the descriptor holds every fact that used to be a constant', () => {
  const harness = resolveHarness('claude-code');

  assert.equal(harness.defaultTarget, '.claude');
  assert.equal(harness.indexPath, 'rules/RULES_INDEX.md');
  assert.equal(harness.headerPlacement, 'below-frontmatter');
  assert.deepEqual(tierNames(harness), ['rules', 'knowledge', 'skills']);
  assert.deepEqual(alwaysLoadedTiers(harness), ['rules']);
  assert.equal(harness.tiers.skills.entryFile, 'SKILL.md');
  assert.deepEqual(harness.tiers.skills.frontmatter, ['name', 'description']);
});

test('an omitted harness resolves to the default, so old manifests keep working', () => {
  const fx = makeFixture('harness-default');
  fx.write('daoris.json', '{"source":"s","packs":[]}');

  const manifest = readManifest(fx.root);

  assert.equal(manifest.harness, DEFAULT_HARNESS);
  assert.equal(manifest.target, '.claude', 'the target defaults from the descriptor');
  assert.equal(manifest.harnessDescriptor.id, DEFAULT_HARNESS);
  fx.cleanup();
});

/**
 * A silent fallback would be the exact failure this seam exists to prevent: a repository that asked
 * for one harness and quietly got another's layout.
 */
test('an unknown harness is a tool error naming what exists', () => {
  const fx = makeFixture('harness-unknown');
  fx.write('daoris.json', '{"source":"s","harness":"nonesuch"}');

  const error = captureError(() => readManifest(fx.root));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /nonesuch/);
  assert.match(error.message, /claude-code/);
  fx.cleanup();
});

test('a recognised but ungenerated harness says so, rather than pretending', () => {
  const error = captureError(() => resolveHarness('agents-md'));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /recognised but not generated/);
  assert.match(error.message, /D23/);
});

test('an explicit target still overrides the descriptor default', () => {
  const fx = makeFixture('harness-target');
  fx.write('daoris.json', '{"source":"s","target":".agent"}');

  assert.equal(readManifest(fx.root).target, '.agent');
  fx.cleanup();
});

test('the contract check follows the descriptor, not hard-coded names', () => {
  const fx = makeFixture('harness-contract-desc');
  fx.write('.claude/skills/x/SKILL.md', '---\nname: x\ndescription: d\n---\n\nSteps.\n');

  assert.deepEqual(verifyHarnessContract(fx.root, '.claude', HARNESSES['claude-code']), []);
  fx.cleanup();
});

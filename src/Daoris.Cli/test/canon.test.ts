import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Fixture } from './_fixture.ts';
import { makeFixture, captureError } from './_fixture.ts';
import { readCanon, selectFiles, resolveCanonRoot } from '../src/canon.ts';
import { DaorisError } from '../src/errors.ts';

function seedCanon() {
  const fx = makeFixture('canon');
  fx.write('canon.json', '{"version":"0.1.0"}');
  fx.write('core/rules/sensitive-info.md', 'core rule\n');
  fx.write('core/rules/task-lifecycle.md', 'core rule\n');
  fx.write('core/skills/doc-loader/SKILL.md', 'core skill\n');
  fx.write('packs/dotnet-library/pack.json', '{"name":"dotnet-library","description":"NuGet library"}');
  fx.write('packs/dotnet-library/rules/dev-conventions.md', 'pack rule\n');
  fx.write('packs/dotnet-library/knowledge/storage.md', 'pack knowledge\n');
  fx.write('packs/dotnet-library/skills/add-provider/SKILL.md', 'pack skill\n');
  fx.write('packs/dotnet-library/README.txt', 'ignored\n');
  return fx;
}

/**
 * A skill is a directory, and the platform lets it carry supporting files: a
 * reference document, a template, a script invoked via ${CLAUDE_SKILL_DIR}.
 * Materializing only the SKILL.md would install a skill whose first action is
 * to run a file that is not there — broken in the consumer, working here.
 */
test("a skill's supporting files travel with it", () => {
  const fx = seedCanon();
  fx.write('core/skills/doc-loader/reference.md', 'detail\n');
  fx.write('core/skills/doc-loader/scripts/probe.sh', 'echo hi\n');

  const targets = readCanon(fx.root).packs.get('core')!.files.map((f) => f.target);
  assert.ok(targets.includes('skills/doc-loader/reference.md'), 'reference doc was dropped');
  assert.ok(targets.includes('skills/doc-loader/scripts/probe.sh'), 'script was dropped');
  // Non-markdown outside a skill is still not canon material.
  assert.equal(targets.some((t) => t.endsWith('README.txt')), false);
  fx.cleanup();
});

test('readCanon reads the version, its own root, core, and packs', () => {
  const fx = seedCanon();
  const canon = readCanon(fx.root);
  assert.equal(canon.version, '0.1.0');
  assert.equal(canon.root, fx.root);
  assert.equal(canon.packs.get('core')!.files.length, 3);
  assert.equal(canon.packs.get('dotnet-library')!.description, 'NuGet library');
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
    'skills/add-provider/SKILL.md',
    'skills/doc-loader/SKILL.md',
  ]);
  fx.cleanup();
});

/**
 * Core is laid out exactly like a pack, so the tier is the directory there too
 * (D7) and one code path reads both.
 */
test('core carries tiers of its own, skills included', () => {
  const fx = seedCanon();
  const core = readCanon(fx.root).packs.get('core')!.files;
  assert.deepEqual(core.map((f) => `${f.source} -> ${f.target}`).sort(), [
    'core/rules/sensitive-info.md -> rules/sensitive-info.md',
    'core/rules/task-lifecycle.md -> rules/task-lifecycle.md',
    'core/skills/doc-loader/SKILL.md -> skills/doc-loader/SKILL.md',
  ]);
  fx.cleanup();
});

test('core is selected without being asked for', () => {
  const fx = seedCanon();
  const files = selectFiles(readCanon(fx.root), []);
  assert.deepEqual(files.map((f) => f.target), [
    'rules/sensitive-info.md',
    'rules/task-lifecycle.md',
    'skills/doc-loader/SKILL.md',
  ]);
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

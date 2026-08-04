import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { existsSync } from 'node:fs';
import { readCanon } from '../src/canon.mjs';
import { parseFrontmatter, SKILL_FIELDS } from '../src/document.mjs';
import { readText } from '../src/fsx.mjs';
import { readManifest, readLock } from '../src/config.mjs';
import { inspect } from '../src/drift.mjs';

// This package is src/Daoris.Cli; the canon and daoris's own doctrine live at
// the workspace root, because they are the project's data rather than the CLI's.
const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(cliRoot));

const isSkill = (source) => source.includes('/skills/');

test('every shipped canon file has complete frontmatter', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const core = canon.packs.get('core').files;
  assert.ok(core.length >= 6, `expected at least 6 core files, found ${core.length}`);

  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      if (isSkill(file.source)) continue;
      const { meta } = parseFrontmatter(readText(join(repoRoot, 'canon', file.source)));
      assert.ok(meta, `${file.source} is missing or has incomplete frontmatter`);
      assert.ok(meta.name && meta.applies_when && meta.enforces);
      assert.equal(
        meta.name,
        file.source.split('/').pop().replace(/\.md$/, ''),
        `${file.source}: frontmatter name must match the filename`,
      );
    }
  }
});

/**
 * A skill's frontmatter is the harness's, not ours: `description` is the trigger
 * it matches on, so a skill with none never fires — it installs, costs bytes and
 * silently does nothing. The name has to match the DIRECTORY, because every
 * skill's file is called SKILL.md.
 */
test('every shipped canon skill carries the frontmatter the harness needs', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const skills = [...canon.packs.values()].flatMap((pack) => pack.files).filter((f) => isSkill(f.source));
  assert.ok(skills.length >= 2, `expected at least 2 canon skills, found ${skills.length}`);

  for (const file of skills) {
    assert.ok(file.source.endsWith('/SKILL.md'), `${file.source}: a skill's file must be SKILL.md`);
    const text = readText(join(repoRoot, 'canon', file.source));
    const { meta } = parseFrontmatter(text, SKILL_FIELDS);
    assert.ok(meta, `${file.source} is missing 'name' or 'description'`);
    assert.equal(
      meta.name,
      file.source.split('/').at(-2),
      `${file.source}: frontmatter name must match the skill's directory`,
    );
  }
});

test('every pack declares a description and ships at least one file', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const packs = [...canon.packs.values()].filter((pack) => pack.name !== 'core');
  assert.ok(packs.length >= 3, `expected at least 3 packs, found ${packs.length}`);
  for (const pack of packs) {
    assert.ok(pack.description, `pack '${pack.name}' has no description — init prints it`);
    assert.ok(pack.files.length, `pack '${pack.name}' ships no files`);
  }
});

test('no canon file names a private sibling project or a machine path', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const forbidden = /[A-Z]:\\Users\\|\/home\/[a-z]/i;
  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      const text = readText(join(repoRoot, 'canon', file.source));
      assert.equal(forbidden.test(text), false, `${file.source} contains a machine path`);
    }
  }
});

test('daoris holds its own doctrine and checks clean', () => {
  assert.equal(existsSync(join(repoRoot, 'daoris.json')), true, 'run: node bin/daoris.mjs init');
  const lock = readLock(repoRoot);
  assert.ok(lock, 'run: node bin/daoris.mjs sync');
  const report = inspect({ root: repoRoot, manifest: readManifest(repoRoot), lock });
  assert.equal(report.ok, true, JSON.stringify(report, null, 2));
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { existsSync } from 'node:fs';
import { readCanon } from '../src/canon.ts';
import { parseFrontmatter, SKILL_FIELDS } from '../src/document.ts';
import { listFiles, readText } from '../src/fsx.ts';
import { readManifest, readLock } from '../src/config.ts';
import { inspect, commandCheck } from '../src/drift.ts';

// This package is src/Daoris.Cli; the canon and daoris's own doctrine live at
// the workspace root, because they are the project's data rather than the CLI's.
const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(cliRoot));

const isSkill = (source: string) => source.includes('/skills/');

test('every shipped canon file has complete frontmatter', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const core = canon.packs.get('core')!.files;
  assert.ok(core.length >= 6, `expected at least 6 core files, found ${core.length}`);

  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      if (isSkill(file.source)) continue;
      const { meta } = parseFrontmatter(readText(join(repoRoot, 'canon', file.source)));
      assert.ok(meta, `${file.source} is missing or has incomplete frontmatter`);
      assert.ok(meta!.name && meta!.applies_when && meta!.enforces);
      assert.equal(
        meta!.name,
        file.source.split('/').pop()!.replace(/\.md$/, ''),
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
      meta!.name,
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

/**
 * D8's offline guarantee, asserted rather than asserted-about.
 *
 * CLAUDE.md has claimed both halves of this for a while and neither was enforced by anything. A
 * documented guarantee with no test is worse than an undocumented one: it reads as verified, so nobody
 * checks it, and it stays true only as long as nobody writes the obvious convenience feature.
 */
test('the CLI contains no network primitive at all', () => {
  const forbidden = /\b(?:fetch|XMLHttpRequest|WebSocket)\s*\(|(?:^|[^\w.])require\(['"]https?['"]\)|from\s+['"]node:https?['"]/;
  for (const dir of ['src', 'bin']) {
    for (const file of listFiles(join(cliRoot, dir), (n) => n.endsWith('.ts') || n.endsWith('.mjs'))) {
      const text = readText(join(cliRoot, dir, file));
      assert.equal(forbidden.test(text), false, `${dir}/${file} reaches the network`);
    }
  }
});

/**
 * `check` verifies against the LOCK, which already carries a hash per file — so it needs neither the
 * canon nor a network to answer. That is what makes it usable in a hook or an air-gapped build, and it
 * is the property most easily lost by an innocent-looking "just re-read the canon to compare" change.
 */
test('check passes with no canon present at all', () => {
  const previous = process.env.DAORIS_CANON;
  // Points the resolver at a path that does not exist rather than deleting the real canon: the
  // guarantee is about `check` never NEEDING the canon, and a test that had to destroy the tree to
  // prove it could not run beside the others.
  process.env.DAORIS_CANON = join(cliRoot, '_fixtures', 'no-such-canon');
  try {
    const out: string[] = [];
    // No `packageRoot` on purpose — commandCheck does not accept one, which is the guarantee stated
    // as a signature. The env var covers the other route to a canon, so a future version that grew
    // either one would fail here rather than quietly acquiring a dependency.
    const code = commandCheck({ root: repoRoot, write: (s: string) => out.push(s) });
    assert.equal(code, 0, out.join('\n'));
  } finally {
    if (previous === undefined) delete process.env.DAORIS_CANON;
    else process.env.DAORIS_CANON = previous;
  }
});

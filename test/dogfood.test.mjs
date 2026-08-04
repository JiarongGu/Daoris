import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { existsSync } from 'node:fs';
import { readCanon } from '../src/canon.mjs';
import { parseFrontmatter } from '../src/document.mjs';
import { readText } from '../src/fsx.mjs';
import { readManifest, readLock } from '../src/config.mjs';
import { inspect } from '../src/drift.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));

test('every shipped canon file has complete frontmatter', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const core = canon.packs.get('core').files;
  assert.ok(core.length >= 6, `expected at least 6 core rules, found ${core.length}`);

  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
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

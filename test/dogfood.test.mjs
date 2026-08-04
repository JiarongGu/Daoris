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

test('the shipped canon is readable and every core file has frontmatter', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const core = canon.packs.get('core').files;
  assert.ok(core.length >= 5, `expected at least 5 core rules, found ${core.length}`);
  for (const file of core) {
    const { meta } = parseFrontmatter(readText(join(repoRoot, 'canon', file.source)));
    assert.ok(meta, `${file.source} is missing or has incomplete frontmatter`);
    assert.ok(meta.name && meta.applies_when && meta.enforces);
  }
});

test('daoris holds its own doctrine and checks clean', () => {
  assert.equal(existsSync(join(repoRoot, 'daoris.json')), true, 'run: node bin/daoris.mjs init');
  const lock = readLock(repoRoot);
  assert.ok(lock, 'run: node bin/daoris.mjs sync');
  const report = inspect({ root: repoRoot, manifest: readManifest(repoRoot), lock });
  assert.equal(report.ok, true, JSON.stringify(report, null, 2));
});

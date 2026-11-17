import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CENSUS_SCOPE, mergeGateEnvironment, scopeFile } from './census-scope.ts';

const repo = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const scratch = (name: string): string => {
  const dir = join(repo, '_fixtures', 'census-scope-test', `${name}-${process.pid}`);
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  return dir;
};

test("a merge gate's environment names the merge's changed paths in a file, one a line, beside the ratchet's standing aside", () => {
  const root = scratch('named');
  try {
    const env = mergeGateEnvironment({ PATH: 'p' }, root, () => ['src/A.cs', 'docs/b.md']);
    assert.equal(env.MERGE_GATES_RUN, '1');
    assert.equal(env.PATH, 'p');
    const file = env[CENSUS_SCOPE] as string;
    assert.equal(readFileSync(file, 'utf8'), 'src/A.cs\ndocs/b.md\n');
    assert.equal(scopeFile(root, ['src/A.cs', 'docs/b.md']), file, 'every gate of the merge names the same file');
    assert.notEqual(scopeFile(root, ['src/A.cs']), file, 'another merge, another file: none is rewritten while a census reads it');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a checkout whose changes cannot be read names no scope, so its censuses judge the whole tree', () => {
  const env = mergeGateEnvironment({}, scratch('unread'), () => { throw new Error('not a repository'); });
  assert.equal(env.MERGE_GATES_RUN, '1');
  assert.equal(env[CENSUS_SCOPE], undefined);
});

test('the variable is the one the desktop census scan reads', () => {
  const scan = readFileSync(join(repo, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'CensusScope.cs'), 'utf8');
  assert.match(scan, new RegExp(`Variable = "${CENSUS_SCOPE}"`));
});

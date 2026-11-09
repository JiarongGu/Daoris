import { test } from 'node:test';
import assert from 'node:assert/strict';
import { tool } from '../support/merge-branch.ts';

// The census of test units reads this file's strings as globs under test, not as reads.
// test-map: strings name paths as data
// reaches: tools/merge-branch/lanes-file.mjs — the grammar under test

test('a wildcard inside {a,b} keeps its meaning, and a group nests', () => {
  const stars = tool.globToRegExp('src/*.{ts,*.tsx}');
  assert.ok(stars.test('src/a.ts') && stars.test('src/a.test.tsx') && !stars.test('src/a/b.ts'));
  const nested = tool.globToRegExp('src/{a,b/**/*.{ts,tsx}}');
  assert.ok(nested.test('src/a') && nested.test('src/b/x/y.tsx') && !nested.test('src/b/x/y.cs'));
  assert.ok(tool.globToRegExp('a{b').test('a{b'), 'a brace with no close is a brace');
});

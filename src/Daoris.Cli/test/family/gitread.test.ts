import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../_fixture.ts';
import { openRepository } from '../../src/family/gitread.ts';
import { commit, git, history, initWithCommit } from './_git.ts';

/**
 * The family check's git reading: refs and commits read from git's own files, never by starting git (the CLI's one spawn is the
 * desktop program's, `dogfood.test.ts`). Real git lays each repository out; the reader is held to what git itself answers.
 */

test('refs are read loose, packed and through a symbolic ref, and a missing one is null', () => {
  const fixture = makeFixture('family-gitread/refs');
  const root = fixture.root;
  const first = initWithCommit(root);
  git(root, 'tag', 'v1');
  const second = commit(root, 'second');
  git(root, 'pack-refs', '--all');
  const third = commit(root, 'third');

  const repository = openRepository(root)!;

  assert.equal(repository.ref('refs/heads/main'), third, 'the loose ref wins over its packed line');
  assert.equal(repository.ref('refs/tags/v1'), first, 'a packed tag');
  assert.equal(repository.ref('HEAD'), third, 'HEAD through its symbolic ref');
  assert.equal(repository.ref('refs/heads/nothing'), null);
  assert.ok(repository.isAncestor(second, [third]));
  assert.ok(!repository.isAncestor(third, [first]));
  fixture.cleanup();
});

test('an annotated tag is peeled to its commit', () => {
  const fixture = makeFixture('family-gitread/annotated');
  const first = initWithCommit(fixture.root);
  git(fixture.root, 'tag', '-a', 'v1', '-m', 'a release');

  const repository = openRepository(fixture.root)!;

  assert.equal(repository.commitOf(repository.ref('refs/tags/v1')!), first);
  fixture.cleanup();
});

test('a linked worktree reads its own HEAD and the common refs and objects', () => {
  const fixture = makeFixture('family-gitread/worktree');
  const main = join(fixture.root, 'main');
  mkdirSync(main, { recursive: true });
  const first = initWithCommit(main);
  const tree = join(fixture.root, 'tree');
  git(main, 'worktree', 'add', '-q', '-b', 'side', tree);
  const side = commit(tree, 'on the side');

  const repository = openRepository(tree)!;

  assert.equal(repository.ref('HEAD'), side);
  assert.equal(repository.ref('refs/heads/main'), first);
  assert.ok(repository.isAncestor(first, [side]));
  fixture.cleanup();
});

test('commits are walked through a pack, commits stored as deltas included', () => {
  const fixture = makeFixture('family-gitread/pack');
  const root = fixture.root;
  const ids = history(root, 30);
  git(root, 'repack', '-q', '-a', '-d', '-f', '--window=50', '--depth=50');
  const pack = readdirSync(join(root, '.git', 'objects', 'pack')).find((name) => name.endsWith('.idx'))!;
  const listed = git(root, 'verify-pack', '-v', join('.git', 'objects', 'pack', pack));
  // A delta's line names its depth and its base after the commit's sizes; without one this test would prove the plain path only.
  assert.match(listed, /^[0-9a-f]{40} commit +\d+ \d+ \d+ \d+ [0-9a-f]{40}$/m, 'the repack stores some commit as a delta');

  const repository = openRepository(root)!;

  for (const id of ids) assert.equal(repository.commitOf(id), id, id);
  assert.ok(repository.isAncestor(ids[0]!, [ids.at(-1)!]));
  assert.ok(!repository.isAncestor(ids.at(-1)!, [ids[0]!]));
  assert.equal(repository.commitOf('d'.repeat(40)), null);
  fixture.cleanup();
});

test('a folder that is no repository opens as null', () => {
  const fixture = makeFixture('family-gitread/none');
  assert.equal(openRepository(fixture.root), null);
  fixture.cleanup();
});

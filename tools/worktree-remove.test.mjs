/**
 * A worktree removed without reaching through its links:
 *
 *   node --test tools/worktree-remove.test.mjs
 *
 * Scratch repositories live in a gitignored folder of this one. Each worktree holds a junction (a directory
 * symlink off Windows) to a scratch folder that stands for the main checkout's `node_modules`.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, lstatSync, mkdirSync, readdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { removeWorktree } from './worktree-remove.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'worktree-remove-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const env = { ...process.env, GIT_CONFIG_GLOBAL: join(scratch, 'gitconfig'), GIT_CONFIG_SYSTEM: join(scratch, 'gitconfig'), GIT_CONFIG_NOSYSTEM: '1' };
for (const key of Object.keys(env)) if (/^GIT_(DIR|WORK_TREE|INDEX_FILE|COMMON_DIR)$/.test(key)) delete env[key];
writeFileSync(join(scratch, 'gitconfig'), '[user]\n\tname = t\n\temail = t@example.invalid\n[commit]\n\tgpgsign = false\n');

function git(cwd, ...args) {
  const run = spawnSync('git', args, { cwd, env, encoding: 'utf8', windowsHide: true });
  assert.equal(run.status, 0, `git ${args.join(' ')}: ${run.stderr}`);
  return run.stdout;
}

let counter = 0;
/** A repository with one tracked file and a worktree of it; `shared` is a folder with two files the worktree links to. */
function fixture() {
  const base = join(scratch, `case${++counter}`);
  const repo = join(base, 'main');
  mkdirSync(repo, { recursive: true });
  git(repo, 'init', '-q', '-b', 'main');
  writeFileSync(join(repo, 'tracked.txt'), 'kept\n');
  git(repo, 'add', '-A');
  git(repo, 'commit', '-q', '-m', 'one');
  const tree = join(base, 'tree');
  git(repo, 'worktree', 'add', '-q', '-b', 'side', tree);
  const shared = join(base, 'shared');
  mkdirSync(join(shared, 'deep'), { recursive: true });
  writeFileSync(join(shared, 'a.txt'), 'a');
  writeFileSync(join(shared, 'deep', 'b.txt'), 'b');
  return { repo, tree, shared };
}

const link = (target, path) => symlinkSync(target, path, 'junction');

test('a worktree holding a link to a folder is removed and the folder keeps every file', () => {
  const { repo, tree, shared } = fixture();
  link(shared, join(tree, 'node_modules'));
  mkdirSync(join(tree, 'web'));
  link(shared, join(tree, 'web', 'node_modules'));
  const result = removeWorktree(repo, tree);
  assert.equal(result.ok, true, result.why);
  assert.equal(result.links.length, 2);
  assert.equal(existsSync(tree), false);
  assert.deepEqual(readdirSync(shared).sort(), ['a.txt', 'deep']);
  assert.equal(existsSync(join(shared, 'deep', 'b.txt')), true);
  assert.equal(existsSync(join(repo, 'tracked.txt')), true);
});

test('a plain forced git removal follows the same link and empties the folder, which is why the helper exists', (t) => {
  const { repo, tree, shared } = fixture();
  link(shared, join(tree, 'node_modules'));
  git(repo, 'worktree', 'remove', '--force', tree);
  // Newer git removes a link as a link; where it does not, the folder is emptied. Either way the helper's result is the one asserted above.
  if (existsSync(join(shared, 'a.txt'))) t.skip('this git does not follow links');
  assert.equal(existsSync(join(shared, 'a.txt')), false);
});

test('a dangling link is removed', () => {
  const { repo, tree, shared } = fixture();
  const gone = join(scratch, `gone${counter}`);
  mkdirSync(gone);
  link(gone, join(tree, 'node_modules'));
  rmSync(gone, { recursive: true });
  const result = removeWorktree(repo, tree);
  assert.equal(result.ok, true, result.why);
  assert.equal(existsSync(tree), false);
  assert.equal(existsSync(join(shared, 'a.txt')), true);
});

test('a link that cannot be removed stops the removal before git removes anything', () => {
  const { repo, tree, shared } = fixture();
  link(shared, join(tree, 'node_modules'));
  writeFileSync(join(tree, 'untouched.txt'), 'x');
  const result = removeWorktree(repo, tree, { removeLink: () => { throw new Error('held'); } });
  assert.equal(result.ok, false);
  assert.match(result.why, /node_modules/);
  assert.equal(existsSync(join(tree, 'untouched.txt')), true);
  assert.equal(lstatSync(join(tree, 'node_modules')).isSymbolicLink(), true);
  assert.equal(existsSync(join(shared, 'a.txt')), true);
  assert.match(git(repo, 'worktree', 'list'), /tree/);
});

test('a tracked file of the main checkout deleted by the removal is reported', () => {
  const { repo, tree } = fixture();
  const result = removeWorktree(repo, tree, { afterRemove: () => rmSync(join(repo, 'tracked.txt')) });
  assert.equal(result.ok, false);
  assert.match(result.why, /tracked\.txt/);
});

test('a worktree with changes is kept unless forced', () => {
  const { repo, tree } = fixture();
  writeFileSync(join(tree, 'tracked.txt'), 'changed\n');
  const refused = removeWorktree(repo, tree);
  assert.equal(refused.ok, false);
  assert.equal(existsSync(tree), true);
  const forced = removeWorktree(repo, tree, { force: true });
  assert.equal(forced.ok, true, forced.why);
  assert.equal(existsSync(tree), false);
});

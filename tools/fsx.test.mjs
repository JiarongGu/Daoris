/**
 * Shared atomic writes, repository inventory and staged-tree snapshots:
 *
 *   node --test tools/fsx.test.mjs
 *
 * `npm run verify` runs it beside the orientation index's. The snapshot cases build scratch repositories in a
 * gitignored folder of this one, and start git with every git location the run inherited taken out, so a run under
 * `tools/as-merged.mjs` never points them at the repository around them.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { repositoryFiles, stagedTree, writeAtomic } from './fsx.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'fsx-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

// Git's settings for the fixtures ride the environment, not a `git config` per repository: each git start costs a
// noticeable fraction of a second on Windows, and this file runs in verify.
const settings = [['user.name', 'Fixture'], ['user.email', 'fixture@example.invalid'], ['core.autocrlf', 'false']];
const inherited = {
  ...Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !/^GIT_(DIR|WORK_TREE|INDEX_FILE|COMMON_DIR|OBJECT_DIRECTORY|CONFIG_COUNT|CONFIG_KEY_\d+|CONFIG_VALUE_\d+)$/i.test(key))),
  GIT_CONFIG_COUNT: String(settings.length),
  ...Object.fromEntries(settings.flatMap(([key, value], i) => [[`GIT_CONFIG_KEY_${i}`, key], [`GIT_CONFIG_VALUE_${i}`, value]])),
};

/** A rename that refuses with `code` for its first `refusals` calls, then moves the file, recording each call. */
function refusing(code, refusals) {
  const calls = [];
  return {
    calls,
    rename: (from, to) => {
      calls.push([from, to]);
      if (calls.length <= refusals) throw Object.assign(new Error(`${code}: held, rename`), { code });
      renameSync(from, to);
    },
  };
}

const folder = (name) => {
  const at = join(scratch, name);
  rmSync(at, { recursive: true, force: true });
  mkdirSync(at, { recursive: true });
  return at;
};

// ---------------------------------------------------------------------------------------------------------
// writeAtomic

test('a file is written whole into a folder made for it, replacing what was there, with nothing left beside it', () => {
  const at = folder('write');
  const file = join(at, 'deep', 'record.json');
  writeAtomic(file, '{"a":1}\n');
  assert.equal(readFileSync(file, 'utf8'), '{"a":1}\n');

  writeAtomic(file, 'x — 灵台\n');
  const bytes = readFileSync(file);
  assert.equal(bytes.toString('utf8'), 'x — 灵台\n');
  assert.equal(bytes[0], 0x78, 'UTF-8 with no BOM');
  writeAtomic(file, Buffer.from([0, 1, 2]));
  assert.deepEqual([...readFileSync(file)], [0, 1, 2], 'bytes as they are');
  assert.deepEqual(readdirSync(dirname(file)), ['record.json']);
});

test('a rename refused while something holds the file is tried again until it gives way', () => {
  const at = folder('held');
  const file = join(at, 'state.json');
  writeFileSync(file, 'old\n');
  for (const code of ['EPERM', 'EACCES', 'EBUSY']) {
    const held = refusing(code, 2);
    writeAtomic(file, `${code}\n`, { waitMs: 1, rename: held.rename });
    assert.equal(held.calls.length, 3, code);
    assert.equal(readFileSync(file, 'utf8'), `${code}\n`);
    assert.deepEqual(readdirSync(at), ['state.json'], code);
  }
});

test('each write goes beside under a name of its own, never a name another write would share', () => {
  const at = folder('names');
  const file = join(at, 'log.json');
  const seen = [];
  for (let i = 0; i < 3; i++) writeAtomic(file, `${i}\n`, { rename: (from, to) => { seen.push(from); renameSync(from, to); } });
  assert.equal(new Set(seen).size, 3);
  for (const beside of seen) {
    assert.equal(dirname(beside), at, 'beside the file, so the rename never crosses a volume');
    assert.notEqual(beside, `${file}.partial`);
  }
});

test('a failed rename leaves the file as it was and removes what was written beside it', () => {
  const at = folder('failed');
  const file = join(at, 'D1.md');
  writeFileSync(file, 'as it was\n');

  // Held past its tries: the last refusal is thrown.
  const held = refusing('EPERM', 10);
  assert.throws(() => writeAtomic(file, 'new\n', { tries: 3, waitMs: 1, rename: held.rename }), /EPERM/);
  assert.equal(held.calls.length, 3);
  assert.equal(readFileSync(file, 'utf8'), 'as it was\n');
  assert.deepEqual(readdirSync(at), ['D1.md'], 'nothing beside it for `git add -A` to stage');

  // Any other refusal: thrown at once.
  const other = refusing('EXDEV', 10);
  assert.throws(() => writeAtomic(file, 'new\n', { waitMs: 1, rename: other.rename }), /EXDEV/);
  assert.equal(other.calls.length, 1);
  assert.equal(readFileSync(file, 'utf8'), 'as it was\n');
  assert.deepEqual(readdirSync(at), ['D1.md']);
});

// ---------------------------------------------------------------------------------------------------------
// stagedTree

function git(cwd, args, env = {}) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', env: { ...inherited, ...env }, windowsHide: true });
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

function repository(name) {
  const repo = folder(name);
  git(repo, ['init', '-q', '-b', 'main']);
  const write = (path, text) => {
    mkdirSync(dirname(join(repo, path)), { recursive: true });
    writeFileSync(join(repo, path), text);
  };
  return { repo, write };
}

/**
 * What a snapshot must leave as it found: the index's bytes, an open merge, the status with HEAD in it, and the git
 * folder. The status is asked without optional locks: a plain `git status` may refresh the index it judges, and write it.
 */
function state(repo) {
  const [gitDir, index] = git(repo, ['rev-parse', '--path-format=absolute', '--absolute-git-dir', '--git-path', 'index']).split(/\r?\n/);
  return {
    index: existsSync(index) ? readFileSync(index).toString('base64') : null,
    mergeHead: existsSync(join(gitDir, 'MERGE_HEAD')) ? readFileSync(join(gitDir, 'MERGE_HEAD'), 'utf8') : null,
    status: git(repo, ['--no-optional-locks', 'status', '--porcelain=v2', '--branch', '--untracked-files=all']),
    gitDir: readdirSync(gitDir).sort(),
  };
}

test('repositoryFiles reads tracked and new regular files, once, without changing Git state', () => {
  const { repo, write } = repository('inventory');
  write('.gitignore', 'ignored/\n');
  write('tracked.md', 'tracked\n');
  write('gone.md', 'deleted after staging\n');
  write('directory.md', 'replaced by a directory after staging\n');
  git(repo, ['add', '-A']);
  rmSync(join(repo, 'gone.md'));
  rmSync(join(repo, 'directory.md'));
  write('directory.md/child.txt', 'regular child\n');
  write('ignored/hidden.md', 'ignored\n');
  write('ignored/tracked.md', 'tracked even though its directory is now ignored\n');
  git(repo, ['add', '-f', 'ignored/tracked.md']);
  write('new file.md', 'new\n');
  write('Z.md', 'sort before lowercase\n');
  write('é.md', 'sort after ASCII\n');
  const before = state(repo);

  assert.deepEqual(repositoryFiles(repo, { env: inherited }), [
    '.gitignore', 'Z.md', 'directory.md/child.txt', 'ignored/tracked.md', 'new file.md', 'tracked.md', 'é.md',
  ]);
  assert.deepEqual(state(repo), before, 'inventory must leave the index, refs and working tree alone');
});

test('repositoryFiles refuses a nested directory instead of inventorying its parent repository', () => {
  const { repo, write } = repository('inventory-root');
  write('nested/file.md', 'nested\n');
  assert.throws(() => repositoryFiles(join(repo, 'nested'), { env: inherited }), /not the top of a git work tree/);
});

/** The oracle, taken after the snapshot has been judged: the tree `git add -A` then `git write-tree` write. */
function addedTree(repo) {
  git(repo, ['add', '-A']);
  return git(repo, ['write-tree']);
}

const blob = (repo, tree, path) => git(repo, ['cat-file', 'blob', `${tree}:${path}`]);
const paths = (repo, tree) => git(repo, ['ls-tree', '-r', '--name-only', tree]).split('\n').filter(Boolean);

test('outside a merge the snapshot is the tree `git add -A` would stage, and the checkout is left as it was', () => {
  const { repo, write } = repository('plain');
  write('.gitignore', 'ignored/\n');
  write('kept.txt', 'one\n');
  write('edited.txt', 'one\n');
  write('gone.txt', 'one\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'one']);
  write('edited.txt', 'two\n');
  rmSync(join(repo, 'gone.txt'));
  write('new/added.txt', 'new\n');
  write('ignored/secret.txt', 'never\n');
  write('staged.txt', 'staged\n');
  git(repo, ['add', 'staged.txt']);

  const before = state(repo);
  const tree = stagedTree(repo, { env: inherited });
  assert.deepEqual(state(repo), before, "the person's index, HEAD and git folder are untouched");

  assert.deepEqual(paths(repo, tree), ['.gitignore', 'edited.txt', 'kept.txt', 'new/added.txt', 'staged.txt']);
  assert.equal(blob(repo, tree, 'edited.txt'), 'two', 'an unstaged edit is in it');
  assert.equal(tree, addedTree(repo));
});

test('with a merge open the snapshot is the commit-to-be: both sides, and an unstaged fix in the merge, the merge left open', () => {
  const { repo, write } = repository('merge');
  write('a.txt', 'one\n');
  write('README.md', 'what a does\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'one']);
  git(repo, ['checkout', '-q', '-b', 'work']);
  write('a.txt', 'one\ntwo\n');
  git(repo, ['commit', '-q', '-am', 'two']);
  git(repo, ['checkout', '-q', 'main']);
  write('main.txt', 'main\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'three']);
  git(repo, ['merge', '-q', '--no-ff', '--no-commit', 'work']);
  write('README.md', 'what a does, both lines\n');

  const before = state(repo);
  assert.ok(before.mergeHead, 'a merge is open');
  const tree = stagedTree(repo, { env: inherited });
  assert.deepEqual(state(repo), before, 'the merge, its index and the git folder are untouched');

  assert.equal(blob(repo, tree, 'a.txt'), 'one\ntwo', "the branch's side");
  assert.equal(blob(repo, tree, 'main.txt'), 'main', "main's side");
  assert.equal(blob(repo, tree, 'README.md'), 'what a does, both lines', 'the fix not yet staged');
  assert.equal(tree, addedTree(repo));
});

test("in a linked worktree the snapshot is that worktree's, staged from its own index and leaving nothing in either git folder", () => {
  const { repo, write } = repository('linked-main');
  write('a.txt', 'one\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'one']);
  const linked = join(scratch, 'linked-tree');
  rmSync(linked, { recursive: true, force: true });
  git(repo, ['worktree', 'add', '-q', linked, '-b', 'side']);
  writeFileSync(join(linked, 'a.txt'), 'side\n');
  writeFileSync(join(linked, 'side.txt'), 'only here\n');
  writeFileSync(join(repo, 'main-only.txt'), 'only in main\n');
  // Only this worktree's index holds an ignored file staged by force: HEAD's tree or main's index would drop it.
  writeFileSync(join(linked, '.gitignore'), '*.log\n');
  writeFileSync(join(linked, 'forced.log'), 'staged by force\n');
  git(linked, ['add', '-f', 'forced.log']);

  const [mainBefore, linkedBefore] = [state(repo), state(linked)];
  const tree = stagedTree(linked, { env: inherited });
  assert.deepEqual([state(repo), state(linked)], [mainBefore, linkedBefore]);

  assert.deepEqual(paths(linked, tree), ['.gitignore', 'a.txt', 'forced.log', 'side.txt']);
  assert.equal(blob(linked, tree, 'a.txt'), 'side');
  assert.equal(tree, addedTree(linked));
});

test('with no index the snapshot starts from HEAD, so a tracked file that is now ignored stays; with no HEAD either, from nothing', () => {
  const { repo, write } = repository('no-index');
  write('tracked.log', 'kept although ignored\n');
  write('a.txt', 'one\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'one']);
  write('.gitignore', '*.log\n');
  rmSync(join(repo, '.git', 'index'));

  const tree = stagedTree(repo, { env: inherited });
  assert.deepEqual(paths(repo, tree), ['.gitignore', 'a.txt', 'tracked.log']);
  assert.equal(existsSync(join(repo, '.git', 'index')), false, 'no index is written where there was none');
  assert.deepEqual(readdirSync(join(repo, '.git')).filter((name) => name.startsWith('daoris-')), []);

  const unborn = repository('unborn');
  unborn.write('first.txt', 'first\n');
  const first = stagedTree(unborn.repo, { env: inherited });
  assert.deepEqual(paths(unborn.repo, first), ['first.txt']);
});

test("an inherited index is read and never written, and an inherited git location is followed as the caller's own git follows it", () => {
  const { repo, write } = repository('inherited');
  write('a.txt', 'one\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'one']);
  write('a.txt', 'two\n');
  const plain = stagedTree(repo, { env: inherited });

  // A hook runs with GIT_INDEX_FILE naming the index it commits: the snapshot copies it and stages into its own.
  const index = join(repo, '.git', 'index');
  const bytes = readFileSync(index);
  const modified = statSync(index).mtimeMs;
  for (const named of [index, join('.git', 'index')]) {
    assert.equal(stagedTree(repo, { env: { ...inherited, GIT_INDEX_FILE: named } }), plain, named);
    assert.deepEqual(readFileSync(index), bytes, named);
    assert.equal(statSync(index).mtimeMs, modified, named);
  }

  // Started elsewhere with GIT_DIR and GIT_WORK_TREE naming the checkout, as tools/as-merged.mjs starts a command.
  const elsewhere = folder('elsewhere');
  const named = stagedTree(elsewhere, { env: { ...inherited, GIT_DIR: join(repo, '.git'), GIT_WORK_TREE: repo } });
  assert.equal(named, plain);
});

test('outside a checkout git cannot say, and the snapshot throws for its caller to decide', () => {
  const outside = folder('outside');
  assert.throws(() => stagedTree(outside, { env: { ...inherited, GIT_CEILING_DIRECTORIES: scratch } }), /git /);
});

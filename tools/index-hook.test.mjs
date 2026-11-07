/**
 * The checkout hooks write the ignored `docs/index/` where a branch's work tree moves, and the notes driver merges the
 * decisions.
 *
 *   node --test tools/index-hook.test.mjs
 *
 * Run by `npm run verify`. Each case works in a scratch repository under `_fixtures/` with the hooks installed by
 * `--install` and pointed at this checkout's tools.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { install } from './index-hook.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', '_fixtures', 'index-hook-test');
const script = join(here, 'index-hook.mjs');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const clean = () => {
  const env = { ...process.env };
  for (const key of ['GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'DAORIS_INDEX_HOOK']) delete env[key];
  return env;
};
const git = (cwd, ...args) => spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@t', '-c', 'core.autocrlf=false', ...args],
  { cwd, encoding: 'utf8', env: { ...clean(), ...(git.env ?? {}) }, windowsHide: true });
const show = (cwd, spec) => git(cwd, 'show', spec).stdout;
const indexOf = (dir) => join(dir, 'docs', 'index', 'README.md');
const drop = (dir) => rmSync(join(dir, 'docs', 'index'), { recursive: true, force: true });

let serial = 0;
function repository(hook = true) {
  const dir = join(scratch, `r${serial += 1}`);
  mkdirSync(join(dir, 'src'), { recursive: true });
  writeFileSync(join(dir, 'daoris.json'), '{}\n');
  writeFileSync(join(dir, '.gitignore'), 'docs/index/\n');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() {}\n');
  git(dir, 'init', '-q');
  if (hook) assert.equal(install({ out() {}, err() {}, cwd: dir, env: clean(), script }), 0);
  return dir;
}
const committed = () => {
  const dir = repository();
  git(dir, 'add', '-A');
  assert.equal(git(dir, 'commit', '-q', '-m', 'first').status, 0);
  git(dir, 'branch', '-M', 'main');
  return dir;
};

test('install writes the checkout hooks, removes its own pre-commit, and leaves a hook that is not its own', () => {
  const dir = repository(false);
  const hooks = join(dir, '.git', 'hooks');
  mkdirSync(hooks, { recursive: true });
  writeFileSync(join(hooks, 'pre-commit'), '#!/bin/sh\n# daoris index-hook\nexec node tools/index-hook.mjs\n');
  writeFileSync(join(hooks, 'post-merge'), '#!/bin/sh\necho mine\n');
  const lines = [];
  assert.equal(install({ out: (line) => lines.push(line), err() {}, cwd: dir, env: clean(), script }), 0);
  assert.match(readFileSync(join(hooks, 'post-checkout'), 'utf8'), /index-fresh\.mjs" --root "\$root"/);
  assert.equal(existsSync(join(hooks, 'pre-commit')), false);
  assert.equal(readFileSync(join(hooks, 'post-merge'), 'utf8'), '#!/bin/sh\necho mine\n');
  assert.ok(lines.some((line) => /post-merge is not its own/.test(line)), lines.join('\n'));
});

test('install outside a work tree exits 0 and writes nothing', () => {
  const lines = [];
  assert.equal(install({ out: (line) => lines.push(line), err() {}, cwd: scratch, env: { ...clean(), GIT_CEILING_DIRECTORIES: dirname(scratch) }, script }), 0);
  assert.match(lines[0], /not a git work tree/);
});

test('install writes the index where none stands, and git ignores it', () => {
  const dir = committed();
  assert.match(readFileSync(indexOf(dir), 'utf8'), /index/i);
  assert.equal(git(dir, 'status', '--porcelain').stdout.trim(), '');
  assert.equal(git(dir, 'ls-files', 'docs').stdout.trim(), '');
});

test("a branch's checkout and a new worktree write the index; a detached or a file checkout, and the opt-out, write none", () => {
  const dir = committed();
  drop(dir);
  assert.equal(git(dir, 'checkout', '-q', '-b', 'side').status, 0);
  assert.ok(existsSync(indexOf(dir)), "a branch's checkout writes it");

  drop(dir);
  assert.equal(git(dir, 'checkout', '-q', '--', 'src/a.ts').status, 0);
  assert.ok(!existsSync(indexOf(dir)), 'a file checkout writes none');
  assert.equal(git(dir, 'checkout', '-q', '--detach').status, 0);
  assert.ok(!existsSync(indexOf(dir)), "a detached checkout is a tool's tree, and writes none");

  const tree = join(scratch, `w${serial}`);
  const added = git(dir, 'worktree', 'add', '-q', '-b', 'tree', tree);
  assert.equal(added.status, 0, added.stderr);
  assert.ok(existsSync(indexOf(tree)), 'a new worktree starts with its index');
  git(dir, 'worktree', 'remove', '--force', tree);

  git.env = { DAORIS_INDEX_HOOK: '0' };
  try {
    assert.equal(git(dir, 'checkout', '-q', 'main').status, 0);
    assert.ok(!existsSync(indexOf(dir)), 'the opt-out writes none');
  } finally {
    git.env = undefined;
  }
});

test('a merge into a branch writes the index', () => {
  const dir = committed();
  git(dir, 'checkout', '-q', '-b', 'side');
  writeFileSync(join(dir, 'src', 'b.ts'), 'export function b() {}\n');
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '-m', 'b');
  git(dir, 'checkout', '-q', 'main');
  drop(dir);
  assert.equal(git(dir, 'merge', '-q', '--ff-only', 'side').status, 0);
  assert.ok(existsSync(indexOf(dir)));
});

// A union merge coalesces the blank line two branches each add before their note.
const noteRepository = () => {
  const dir = repository();
  mkdirSync(join(dir, 'docs', 'decisions'), { recursive: true });
  writeFileSync(join(dir, '.gitattributes'), 'docs/decisions/*.md merge=notes\n');
  writeFileSync(join(dir, 'docs', 'decisions', 'D1.md'), '# D1\n\nBody.\n');
  git(dir, 'add', '-A');
  assert.equal(git(dir, 'commit', '-q', '-m', 'first').status, 0);
  git(dir, 'branch', '-M', 'main');
  return dir;
};
const addNote = (dir, branch, label) => {
  git(dir, 'checkout', '-q', '-b', branch, 'main');
  const file = join(dir, 'docs', 'decisions', 'D1.md');
  writeFileSync(file, `${readFileSync(file, 'utf8')}\n**Note, ${label}**\nText ${label}.\n`);
  git(dir, 'commit', '-q', '-am', label);
};
const glued = /[^\n]\n\*\*Note, /;

test('a clean union merge commits the blank line before each note', () => {
  const dir = noteRepository();
  addNote(dir, 'a', 'a');
  addNote(dir, 'b', 'b');
  git(dir, 'checkout', '-q', 'a');
  const merged = git(dir, 'merge', '-q', '--no-edit', 'b');
  assert.equal(merged.status, 0, merged.stderr);
  assert.equal(git(dir, 'rev-list', '--parents', '-n', '1', 'HEAD').stdout.trim().split(' ').length, 3);
  const text = show(dir, 'HEAD:docs/decisions/D1.md');
  assert.match(text, /\*\*Note, a\*\*/);
  assert.match(text, /\*\*Note, b\*\*/);
  assert.doesNotMatch(text, glued);
  assert.equal(git(dir, 'status', '--porcelain').stdout.trim(), '');
});

test('a conflicted merge finished by git commit keeps the blank line the driver set', () => {
  const dir = noteRepository();
  addNote(dir, 'a', 'a');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() { return 1; }\n');
  git(dir, 'commit', '-q', '-am', 'a change');
  addNote(dir, 'b', 'b');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() { return 2; }\n');
  git(dir, 'commit', '-q', '-am', 'b change');
  git(dir, 'checkout', '-q', 'a');
  assert.notEqual(git(dir, 'merge', '-q', '--no-edit', 'b').status, 0);
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() { return 3; }\n');
  git(dir, 'add', '-A');
  const made = git(dir, 'commit', '-q', '--no-edit');
  assert.equal(made.status, 0, made.stderr);
  assert.doesNotMatch(show(dir, 'HEAD:docs/decisions/D1.md'), glued);
});

test('install configures the notes driver, and the merge commit is the one git made', () => {
  const dir = noteRepository();
  assert.match(git(dir, 'config', '--local', 'merge.notes.driver').stdout, /index-hook\.mjs" --merge-notes %O %A %B/);
  addNote(dir, 'a', 'a');
  addNote(dir, 'b', 'b');
  git(dir, 'checkout', '-q', 'a');
  assert.equal(git(dir, 'merge', '-q', '--no-edit', 'b').status, 0);
  assert.deepEqual(git(dir, 'reflog', 'HEAD', '--format=%gs').stdout.trim().split('\n').filter((line) => /index-hook|update-ref/.test(line)), []);
  assert.match(git(dir, 'log', '-1', '--format=%s').stdout, /^Merge branch 'b'/);
});

test('a clone without the driver merges the decisions as plain text and conflicts rather than gluing', () => {
  const dir = noteRepository();
  git(dir, 'config', '--local', '--unset', 'merge.notes.driver');
  addNote(dir, 'a', 'a');
  addNote(dir, 'b', 'b');
  git(dir, 'checkout', '-q', 'a');
  assert.notEqual(git(dir, 'merge', '-q', '--no-edit', 'b').status, 0);
  assert.doesNotMatch(readFileSync(join(dir, 'docs', 'decisions', 'D1.md'), 'utf8'), glued);
});

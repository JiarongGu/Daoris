/**
 * INDEXAUTO1: the pre-commit hook regenerates `docs/index/` in the commit that stales it.
 *
 *   node --test tools/index-hook.test.mjs
 *
 * Run by `npm run verify`. Each case commits in a scratch repository under `_fixtures/` with the hook installed by
 * `--install` and pointed at this checkout's tool.
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

let serial = 0;
function repository(hook = true) {
  const dir = join(scratch, `r${serial += 1}`);
  mkdirSync(join(dir, 'src'), { recursive: true });
  writeFileSync(join(dir, 'daoris.json'), '{}\n');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() {}\n');
  git(dir, 'init', '-q');
  if (hook) assert.equal(install({ out() {}, err() {}, cwd: dir, env: clean(), script }), 0);
  return dir;
}

test('install writes the hook, and leaves a hook that is not its own', () => {
  const dir = repository();
  const hook = join(dir, '.git', 'hooks', 'pre-commit');
  assert.match(readFileSync(hook, 'utf8'), /index-hook/);
  const lines = [];
  assert.equal(install({ out: (line) => lines.push(line), err() {}, cwd: dir, env: clean(), script }), 0);
  writeFileSync(hook, '#!/bin/sh\necho mine\n');
  install({ out: (line) => lines.push(line), err() {}, cwd: dir, env: clean(), script });
  assert.equal(readFileSync(hook, 'utf8'), '#!/bin/sh\necho mine\n');
  assert.match(lines.at(-1), /not its own/);
});

test('install outside a work tree exits 0 and writes nothing', () => {
  const lines = [];
  assert.equal(install({ out: (line) => lines.push(line), err() {}, cwd: scratch, env: { ...clean(), GIT_CEILING_DIRECTORIES: dirname(scratch) }, script }), 0);
  assert.match(lines[0], /not a git work tree/);
});

test('a source commit carries its regenerated outline', () => {
  const dir = repository();
  git(dir, 'add', '-A');
  const made = git(dir, 'commit', '-q', '-m', 'first');
  assert.equal(made.status, 0, made.stderr);
  assert.match(show(dir, 'HEAD:docs/index/README.md'), /index/i);
  assert.equal(git(dir, 'status', '--porcelain').stdout.trim(), '');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() {}\nexport function b() {}\n');
  git(dir, 'add', 'src/a.ts');
  assert.equal(git(dir, 'commit', '-q', '-m', 'second').status, 0);
  const check = spawnSync('node', [join(here, 'orient-index.mjs'), '--check', '--root', dir], { encoding: 'utf8', env: clean(), windowsHide: true });
  assert.equal(check.status, 0, check.stderr);
  assert.equal(git(dir, 'status', '--porcelain').stdout.trim(), '');
});

test('a docs-only commit does no work', () => {
  const dir = repository();
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '-m', 'first');
  const before = show(dir, 'HEAD:docs/index/README.md');
  writeFileSync(join(dir, 'docs', 'index', 'README.md'), `${before}\nhand note\n`);
  git(dir, 'add', 'docs/index/README.md');
  assert.equal(git(dir, 'commit', '-q', '-m', 'docs').status, 0);
  assert.match(show(dir, 'HEAD:docs/index/README.md'), /hand note/);
});

test('a partially staged file skips with a note, and the opt-out skips silently', () => {
  const dir = repository();
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '-m', 'first');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() {}\nexport function b() {}\n');
  git(dir, 'add', 'src/a.ts');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() {}\nexport function b() {}\nexport function c() {}\n');
  const made = git(dir, 'commit', '-m', 'partial');
  assert.equal(made.status, 0);
  assert.match(made.stdout + made.stderr, /partially staged/);
  git.env = { DAORIS_INDEX_HOOK: '0' };
  try {
    git(dir, 'add', '-A');
    const off = git(dir, 'commit', '-m', 'off');
    assert.equal(off.status, 0);
    assert.doesNotMatch(off.stdout + off.stderr, /partially staged|orient-index/);
  } finally {
    git.env = undefined;
  }
});

test('a tool failure aborts the commit with its message', () => {
  const dir = repository();
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '-m', 'first');
  writeFileSync(join(dir, 'src', 'a.ts'), 'export function a() {}\nexport function b() {}\n');
  git(dir, 'add', 'src/a.ts');
  // A file where the index folder's subfolder must go makes the write fail.
  rmSync(join(dir, 'docs', 'index'), { recursive: true, force: true });
  writeFileSync(join(dir, 'docs', 'index'), 'not a folder\n');
  const made = git(dir, 'commit', '-q', '-m', 'second');
  assert.notEqual(made.status, 0);
  assert.match(made.stderr, /index-hook|orient-index/);
  assert.equal(existsSync(join(dir, 'docs', 'index')), true);
});

/**
 * The seeds of the hook tests (`tools/hook-seeds.mjs`): a scratch repository built once for each shape and copied for each case, so a
 * case no longer pays `git init`, a base commit and the hook's install again.
 *
 *   node --test tools/hook-seeds.test.mjs
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { seeds } from './hook-seeds.mjs';

const scratch = join(dirname(fileURLToPath(import.meta.url)), '..', '_fixtures', 'hook-seeds-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const git = (cwd, ...args) => spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@t', ...args], { cwd, encoding: 'utf8', windowsHide: true });
const build = (log) => (dir) => {
  log.push(dir);
  git(dir, 'init', '-q', '-b', 'main');
  writeFileSync(join(dir, 'a.txt'), 'one\n');
  mkdirSync(join(dir, '.git', 'hooks'), { recursive: true });
  writeFileSync(join(dir, '.git', 'hooks', 'pre-commit'), '#!/bin/sh\nexit 0\n');
  git(dir, 'add', '-A');
  assert.equal(git(dir, 'commit', '-q', '-m', 'base').status, 0);
};

test('a shape is built once however many cases copy it, and a different shape is built again', () => {
  const log = [];
  const kit = seeds(scratch);
  const first = kit.copy('one', build(log));
  const second = kit.copy('one', build(log));
  const other = kit.copy('two', build(log));
  assert.equal(log.length, 2, 'two shapes, two builds');
  assert.equal(new Set([first, second, other]).size, 3, 'each case has a folder of its own');
  assert.ok(![first, second, other].some((dir) => log.includes(dir)), 'a case never works in the seed itself');
});

test('a copy is a working repository with the seed\'s commit and hooks, and a change in it reaches no other copy', () => {
  const kit = seeds(scratch);
  const log = [];
  const first = kit.copy('shared', build(log));
  const second = kit.copy('shared', build(log));
  assert.equal(git(first, 'rev-parse', 'HEAD').stdout, git(second, 'rev-parse', 'HEAD').stdout);
  assert.ok(existsSync(join(first, '.git', 'hooks', 'pre-commit')));
  assert.equal(git(first, 'status', '--porcelain').stdout.trim(), '');
  writeFileSync(join(first, 'a.txt'), 'two\n');
  git(first, 'add', '-A');
  assert.equal(git(first, 'commit', '-q', '-m', 'second').status, 0);
  assert.equal(git(first, 'rev-list', '--count', 'HEAD').stdout.trim(), '2');
  assert.equal(git(second, 'rev-list', '--count', 'HEAD').stdout.trim(), '1');
  assert.equal(readFileSync(join(second, 'a.txt'), 'utf8'), 'one\n');
});

test('the folders a kit hands out count on, and two kits over one scratch never share one', () => {
  const log = [];
  const one = seeds(scratch);
  const other = seeds(scratch);
  const dirs = [one.copy('x', build(log)), other.copy('x', build(log)), one.copy('x', build(log)), other.copy('x', build(log))];
  assert.equal(new Set(dirs).size, 4);
  assert.equal(one.count, 2);
});

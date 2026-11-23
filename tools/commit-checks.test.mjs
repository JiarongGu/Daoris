/**
 * The commit hook's checks (`tools/commit-checks.mjs`, installed as `pre-commit` by `tools/index-hook.mjs`): planted commits in a scratch
 * repository under `_fixtures/` with the hook installed and pointed at this checkout's tools. This file holds the comment gate, the size
 * ratchet and the contracts; `commit-checks-layers.test.mjs` holds the layers' counts and shrink.
 *
 *   node --test tools/commit-checks.test.mjs
 */
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { code, commitKit, plain } from './commit-checks-kit.mjs';
import { BUDGET } from './size-ratchet.mjs';

const { git, put, repository, commit } = commitKit('commit-checks-test');
const bigAt = (size) => `${'x'.repeat(size - 1)}\n`;
const CONTRACT = 'src/Daoris.Contracts/Routes.cs';
const listAt = (dir, spec) => JSON.parse(git(dir, 'show', spec).stdout);
const STUB = "import { writeFileSync } from 'node:fs';\nwriteFileSync('contracts-ran', process.argv.slice(2).join(' '));\n";

test('a comment rise is refused at the commit, naming the file, and the commit does not happen', () => {
  const dir = repository({ 'src/a.ts': plain });
  put(dir, { 'src/a.ts': code(Array.from({ length: 20 }, () => 'explains the line')) });
  const done = commit(dir);
  assert.notEqual(done.status, 0);
  assert.match(done.stderr, /comment-gate: src\/a\.ts: comments rose/);
  assert.equal(git(dir, 'rev-list', '--count', 'HEAD').stdout.trim(), '1');
});

test('a commit that adds no comment passes, and says what it ran', () => {
  const dir = repository({ 'src/a.ts': plain });
  put(dir, { 'src/a.ts': `${plain}export const more = 1;\n` });
  const done = commit(dir);
  assert.equal(done.status, 0, done.stderr);
  assert.match(`${done.stdout}${done.stderr}`, /commit checks: comments clean \(1 file\)/);
});

test('the comment gate reads only the staged files, not the branch\'s earlier commits', () => {
  const dir = repository({ 'src/a.ts': plain, 'src/b.ts': plain });
  assert.equal(git(dir, 'checkout', '-q', '-b', 'work').status, 0);
  put(dir, { 'src/a.ts': `${plain}export const more = 1;\n`, 'src/b.ts': `${plain}export const more = 2;\n` });
  git(dir, 'add', '-A');
  assert.equal(git(dir, 'commit', '-q', '--no-verify', '-m', 'earlier').status, 0);
  put(dir, { 'src/c.ts': plain });
  const done = commit(dir);
  assert.equal(done.status, 0, done.stderr);
  assert.match(`${done.stdout}${done.stderr}`, /commit checks: comments clean \(1 file\)/);
});

test('a size shrink is written to the list and staged into the same commit', () => {
  const list = { 'src/big.ts': BUDGET + 500 };
  const dir = repository({ 'src/big.ts': bigAt(BUDGET + 500), 'tools/size-ratchet.json': `${JSON.stringify(list)}\n` });
  put(dir, { 'src/big.ts': bigAt(BUDGET + 300) });
  const done = commit(dir);
  assert.equal(done.status, 0, done.stderr);
  assert.deepEqual(listAt(dir, 'HEAD:tools/size-ratchet.json'), { 'src/big.ts': BUDGET + 300 });
  assert.equal(git(dir, 'status', '--porcelain').stdout.trim(), '');
});

test('a file that falls under the budget leaves the list', () => {
  const dir = repository({ 'src/big.ts': bigAt(BUDGET + 500), 'tools/size-ratchet.json': `${JSON.stringify({ 'src/big.ts': BUDGET + 500 })}\n` });
  put(dir, { 'src/big.ts': 'export const small = 1;\n' });
  assert.equal(commit(dir).status, 0);
  assert.deepEqual(listAt(dir, 'HEAD:tools/size-ratchet.json'), {});
});

test('size growth is refused with the file named, and so is a new file over the budget', () => {
  const dir = repository({ 'src/big.ts': bigAt(BUDGET + 500), 'tools/size-ratchet.json': `${JSON.stringify({ 'src/big.ts': BUDGET + 500 })}\n` });
  put(dir, { 'src/big.ts': bigAt(BUDGET + 600), 'src/new.ts': bigAt(BUDGET + 10) });
  const done = commit(dir);
  assert.notEqual(done.status, 0);
  assert.match(done.stderr, /size-ratchet: src\/big\.ts grew from \d+ to \d+ bytes/);
  assert.match(done.stderr, /size-ratchet: src\/new\.ts is \d+ bytes, over the/);
  assert.equal(git(dir, 'rev-list', '--count', 'HEAD').stdout.trim(), '1');
});

test('a stale contract is regenerated where a contract source is staged, and not for any other path', () => {
  const dir = repository({ 'tools/contracts.mjs': STUB, 'src/a.ts': plain });
  put(dir, { 'src/a.ts': `${plain}export const more = 1;\n` });
  assert.equal(commit(dir).status, 0);
  assert.ok(!existsSync(join(dir, 'contracts-ran')), 'a path that declares no contract runs nothing');
  put(dir, { [CONTRACT]: 'namespace Daoris.Contracts;\n' });
  const done = commit(dir);
  assert.equal(done.status, 0, done.stderr);
  assert.equal(readFileSync(join(dir, 'contracts-ran'), 'utf8'), '--if-stale');
  assert.match(`${done.stdout}${done.stderr}`, /contracts regenerated if stale/);
});


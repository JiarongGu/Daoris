/**
 * The commit hook's layers checks (`tools/commit-checks.mjs`): the counts that run for a staged `.cs` or `.csproj` only, the list's shrink
 * staged into the commit, and a tool error that never blocks one. The rest of the hook's checks are in `commit-checks.test.mjs`.
 *
 *   node --test tools/commit-checks-layers.test.mjs
 */
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { layersShrink, runCommitChecks } from './commit-checks.mjs';
import { code, commitKit, plain } from './commit-checks-kit.mjs';

const { env, git, put, repository } = commitKit('commit-checks-layers-test');

test('the layers counts run for a staged .cs or .csproj only, and a breach refuses the commit with the census\'s words', () => {
  const dir = repository({ 'src/a.ts': plain });
  const ran = [];
  const checks = (problems) => ({ counts: () => { ran.push('counts'); return { code: problems.length ? 1 : 0, problems }; } });
  put(dir, { 'src/a.ts': `${plain}export const more = 1;\n` });
  git(dir, 'add', '-A');
  assert.equal(runCommitChecks({ root: dir, env: env(), say() {}, err() {}, ...checks(['x']) }), 0);
  assert.deepEqual(ran, []);
  put(dir, { 'src/B.cs': 'namespace Daoris;\n' });
  git(dir, 'add', '-A');
  const said = [];
  assert.equal(runCommitChecks({ root: dir, env: env(), say() {}, err: (line) => said.push(line), ...checks(['the ambient count rose']) }), 1);
  assert.deepEqual(ran, ['counts']);
  assert.match(said.join('\n'), /layers-census: the ambient count rose/);
});

test('a layers shrink is written and staged into the commit before the counts judge, for a staged .cs or .csproj only', () => {
  const LIST = 'tools/layers-census.json';
  const dir = repository({ 'src/a.ts': plain, [LIST]: '{\n  "clock": {\n    "src/B.cs": 3\n  },\n  "slowTests": {}\n}\n' });
  const order = [];
  const checks = {
    shrink: (root) => {
      order.push('shrink');
      writeFileSync(join(root, LIST), '{\n  "clock": {\n    "src/B.cs": 1\n  },\n  "slowTests": {}\n}\n');
      return ['clock src/B.cs: 3 to 1'];
    },
    counts: () => { order.push('counts'); return { code: 0, problems: [] }; },
  };
  put(dir, { 'src/a.ts': `${plain}export const more = 1;\n` });
  git(dir, 'add', '-A');
  assert.equal(runCommitChecks({ root: dir, env: env(), say() {}, err() {}, ...checks }), 0);
  assert.deepEqual(order, [], 'a commit with no .cs runs neither');
  put(dir, { 'src/B.cs': 'namespace Daoris;\n' });
  git(dir, 'add', '-A');
  const said = [];
  assert.equal(runCommitChecks({ root: dir, env: env(), say: (line) => said.push(line), err() {}, ...checks }), 0);
  assert.deepEqual(order, ['shrink', 'counts']);
  assert.match(said.join('\n'), /layers list lowered \(1\)/);
  assert.equal(git(dir, 'show', `:${LIST}`).stdout, '{\n  "clock": {\n    "src/B.cs": 1\n  },\n  "slowTests": {}\n}\n', 'the lowered list is staged');
});

test('the layers list is lowered only where the working tree is the index, so an unstaged removal never lowers what a staged source needs', () => {
  const LIST = 'tools/layers-census.json';
  const DRIVER = 'src/Daoris.Desktop/Daoris.Desktop.Driver';
  const A = `${DRIVER}/A.cs`;
  const listOf = (clock) => `${JSON.stringify({ clock, slowTests: {} }, null, 2)}\n`;
  const dir = repository({
    [`${DRIVER}/Daoris.Desktop.Driver.csproj`]: '<Project Sdk="Microsoft.NET.Sdk"></Project>',
    [A]: 'var a = DateTime.UtcNow;\n',
    [LIST]: listOf({ [A]: 1 }),
  });
  put(dir, { [A]: 'var a = 1;\n', [`${DRIVER}/B.cs`]: 'var b = 1;\n' });
  git(dir, 'add', '--', `${DRIVER}/B.cs`);
  assert.deepEqual(layersShrink(dir), [], 'A is changed and not staged, so the list is not lowered to it');
  assert.equal(readFileSync(join(dir, LIST), 'utf8'), listOf({ [A]: 1 }));
  git(dir, 'add', '--', A);
  assert.deepEqual(layersShrink(dir), [`clock ${A}: 1 to none`], 'staged, the same removal lowers it');
  assert.equal(readFileSync(join(dir, LIST), 'utf8'), listOf({}));
});

test('a guard read that fails lowers nothing, since it cannot tell the working tree is the index', () => {
  const LIST = 'tools/layers-census.json';
  const DRIVER = 'src/Daoris.Desktop/Daoris.Desktop.Driver';
  const A = `${DRIVER}/A.cs`;
  const listOf = (clock) => `${JSON.stringify({ clock, slowTests: {} }, null, 2)}\n`;
  const dir = repository({
    [`${DRIVER}/Daoris.Desktop.Driver.csproj`]: '<Project Sdk="Microsoft.NET.Sdk"></Project>',
    [A]: 'var a = DateTime.UtcNow;\n',
    [LIST]: listOf({ [A]: 1 }),
  });
  put(dir, { [A]: 'var a = 1;\n' });
  git(dir, 'add', '--', A);
  const failing = (root, args) => (args.includes('diff') || args.includes('--others') ? null : git(root, ...args).stdout);
  assert.deepEqual(layersShrink(dir, { read: failing }), [], 'the diff read failed, so nothing says A is the index');
  assert.equal(readFileSync(join(dir, LIST), 'utf8'), listOf({ [A]: 1 }));
  assert.deepEqual(layersShrink(dir), [`clock ${A}: 1 to none`], 'read, the same staged removal lowers it');
});

test('a tool error never blocks a commit, and the opt-out runs nothing', () => {
  const dir = repository({ 'src/a.ts': plain });
  put(dir, { 'src/B.cs': 'namespace Daoris;\n' });
  const said = [];
  git(dir, 'add', '-A');
  const broke = { counts: () => { throw new Error('no list'); } };
  assert.equal(runCommitChecks({ root: dir, env: env(), say() {}, err: (line) => said.push(line), ...broke }), 0);
  assert.match(said.join('\n'), /not run: no list/);
  put(dir, { 'src/a.ts': code(Array.from({ length: 20 }, () => 'explains the line')) });
  git(dir, 'add', '-A');
  assert.equal(runCommitChecks({ root: dir, env: { ...env(), DAORIS_COMMIT_HOOK: '0' }, say() {}, err() {} }), 0);
});

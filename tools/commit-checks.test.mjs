/**
 * The commit hook's checks (`tools/commit-checks.mjs`, installed as `pre-commit` by `tools/index-hook.mjs`): planted commits in a scratch
 * repository under `_fixtures/` with the hook installed and pointed at this checkout's tools.
 *
 *   node --test tools/commit-checks.test.mjs
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { layersShrink, runCommitChecks } from './commit-checks.mjs';
import { install } from './index-hook.mjs';
import { BUDGET } from './size-ratchet.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', '_fixtures', 'commit-checks-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const env = () => {
  const out = { ...process.env, DAORIS_INDEX_HOOK: '0' };
  for (const key of ['GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'DAORIS_COMMIT_HOOK']) delete out[key];
  return out;
};
const git = (cwd, ...args) => spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@t', '-c', 'core.autocrlf=false', ...args],
  { cwd, encoding: 'utf8', env: env(), windowsHide: true });
const put = (dir, files) => {
  for (const [rel, text] of Object.entries(files)) {
    mkdirSync(dirname(join(dir, rel)), { recursive: true });
    writeFileSync(join(dir, rel), text);
  }
};
const code = (lines) => `${lines.map((line, i) => `// ${line}\nexport const v${i} = ${i};`).join('\n')}\n`;
const plain = Array.from({ length: 20 }, (_, i) => `export const v${i} = ${i};`).join('\n') + '\n';
const bigAt = (size) => `${'x'.repeat(size - 1)}\n`;
const CONTRACT = 'src/Daoris.Contracts/Routes.cs';

let serial = 0;
/** A repository on `main` holding `files`, the hook installed from this checkout's tools. */
function repository(files) {
  const dir = join(scratch, `r${serial += 1}`);
  mkdirSync(dir, { recursive: true });
  git(dir, 'init', '-q', '-b', 'main');
  put(dir, files);
  git(dir, 'add', '-A');
  assert.equal(git(dir, 'commit', '-q', '--no-verify', '-m', 'base').status, 0);
  assert.equal(install({ out() {}, err() {}, cwd: dir, env: env(), script: join(here, 'index-hook.mjs') }), 0);
  return dir;
}
const commit = (dir, ...more) => {
  git(dir, 'add', '-A');
  return git(dir, 'commit', '-q', '-m', 'change', ...more);
};
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

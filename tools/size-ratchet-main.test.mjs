/**
 * The size ratchet's check of a merged tree, in scratch repositories on `main` (`checkTree` in `tools/size-ratchet.mjs`): a landing
 * that leaves a file over its entry is refused, and a red main itself carries is told to a branch begun before it as main's.
 *
 *   node --test tools/size-ratchet-main.test.mjs
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { commitKit, plain } from './commit-checks-kit.mjs';
import { BUDGET, checkTree, scanSizes } from './size-ratchet.mjs';

const { git, put, repository, commit } = commitKit('size-ratchet-main-test');
const bigAt = (size) => `${'x'.repeat(size - 1)}\n`;
const list = (entries) => `${JSON.stringify(entries)}\n`;
const ok = (done) => assert.equal(done.status, 0, done.stderr);

/** The check of the tree at `dir`: its exit and what it said. */
function check(dir) {
  const said = [];
  const refused = [];
  const code = checkTree(dir, scanSizes(dir), { say: (line) => said.push(line), err: (line) => refused.push(line) });
  return { code, said: said.join('\n'), refused: refused.join('\n') };
}

/** A repository whose main stands over its own entry for `src/big.ts` (the landing that broke a budget), and a branch begun before it. */
function brokenMain() {
  const dir = repository({
    'src/big.ts': bigAt(BUDGET + 200), 'src/two.ts': bigAt(BUDGET + 300), 'src/other.ts': plain,
    'tools/size-ratchet.json': list({ 'src/big.ts': BUDGET + 200, 'src/two.ts': BUDGET + 300 }),
  });
  ok(git(dir, 'checkout', '-q', '-b', 'work'));
  put(dir, { 'src/other.ts': `${plain}export const more = 1;\n` });
  ok(commit(dir, '--no-verify'));
  ok(git(dir, 'checkout', '-q', 'main'));
  put(dir, { 'src/big.ts': bigAt(BUDGET + 260) });
  ok(commit(dir, '--no-verify'));
  return dir;
}

test('a branch begun before a landing that broke a budget is told the red is main\'s, and is not failed for it', () => {
  const dir = brokenMain();
  ok(git(dir, 'checkout', '-q', 'work'));
  ok(git(dir, 'merge', '-q', '--no-edit', 'main'));
  const done = check(dir);
  assert.equal(done.code, 0, done.refused);
  assert.match(done.said, /main's red, not this tree's: src\/big\.ts/);
  assert.equal(done.refused, '');
});

test('a branch that grows a file of its own beside main\'s red is failed for its own, with main\'s said apart', () => {
  const dir = brokenMain();
  ok(git(dir, 'checkout', '-q', 'work'));
  put(dir, { 'src/two.ts': bigAt(BUDGET + 350) });
  ok(commit(dir, '--no-verify'));
  ok(git(dir, 'merge', '-q', '--no-edit', 'main'));
  const done = check(dir);
  assert.equal(done.code, 1);
  assert.match(done.refused, /src\/two\.ts grew from \d+ to \d+/);
  assert.doesNotMatch(done.refused, /big\.ts/);
  assert.match(done.said, /main's red, not this tree's: src\/big\.ts/);
});

test('a run on main itself never excuses its red', () => {
  const dir = brokenMain();
  const done = check(dir);
  assert.equal(done.code, 1);
  assert.match(done.refused, /src\/big\.ts grew from \d+ to \d+/);
  assert.equal(done.said, '');
});

test('the landing that leaves a file over its entry is refused, though it stays within the size main\'s list allows', () => {
  const dir = repository({ 'src/big.ts': bigAt(BUDGET + 200), 'tools/size-ratchet.json': list({ 'src/big.ts': BUDGET + 200 }) });
  ok(git(dir, 'checkout', '-q', '-b', 'breaker'));
  put(dir, { 'src/big.ts': bigAt(BUDGET + 100), 'tools/size-ratchet.json': list({ 'src/big.ts': BUDGET + 100 }) });
  ok(commit(dir, '--no-verify'));
  put(dir, { 'src/big.ts': bigAt(BUDGET + 150) });
  ok(commit(dir, '--no-verify'));
  ok(git(dir, 'checkout', '-q', 'main'));
  ok(git(dir, 'merge', '-q', '--no-ff', '--no-commit', 'breaker'));
  const done = check(dir);
  assert.equal(done.code, 1);
  assert.match(done.refused, new RegExp(`src/big\\.ts is ${BUDGET + 150} bytes, over the ${BUDGET + 100} its entry holds \\(main's list allows ${BUDGET + 200}\\)`));
  assert.equal(done.said, '');
});

test('a landing on a red main that adds nothing to it passes with main\'s red said', () => {
  const dir = brokenMain();
  ok(git(dir, 'merge', '-q', '--no-ff', '--no-commit', 'work'));
  const done = check(dir);
  assert.equal(done.code, 0, done.refused);
  assert.match(done.said, /main's red, not this tree's: src\/big\.ts/);
});

test('a tree begun on a red main, with no commit of its own, is told the red is main\'s too', () => {
  const dir = brokenMain();
  ok(git(dir, 'checkout', '-q', '-b', 'fresh'));
  const done = check(dir);
  assert.equal(done.code, 0, done.refused);
  assert.match(done.said, /main's red, not this tree's: src\/big\.ts/);
});

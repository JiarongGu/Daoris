import assert from 'node:assert/strict';
import test from 'node:test';
import { foreignTrees, loadSampler } from './time-load.mjs';

const MAIN = 'D:\\Work\\Repo';
const own = `${MAIN}\\.claude\\worktrees\\mine`;
const other = `${MAIN}\\.claude\\worktrees\\theirs`;
const testhost = (tree, name = 'testhost.exe') => ({ name, commandLine: `"C:\\sdk\\${name}" --port 1 "${tree}\\src\\X\\bin\\X.Tests.dll"` });
const seamOf = (rows) => ({ processes: () => rows });

test('a foreign tree\'s testhost counts as one place', () => {
  assert.equal(foreignTrees(own, seamOf([testhost(other)])), 1);
  assert.equal(foreignTrees(own, seamOf([testhost(MAIN)])), 1, 'the main checkout is a tree of its own');
  assert.equal(foreignTrees(own, seamOf([testhost(`${MAIN}\\local\\gate-tree`), testhost(`${MAIN}\\local\\heavy-tree`)])), 2);
});

test('two processes of one tree count once', () => {
  const node = { name: 'node.exe', commandLine: `node --test ${other}\\tools\\a.test.mjs` };
  assert.equal(foreignTrees(own, seamOf([testhost(other), testhost(other, 'vstest.console.exe'), node])), 1);
});

test('the run\'s own tree counts nothing, nor do processes that are no test run', () => {
  assert.equal(foreignTrees(own, seamOf([testhost(own)])), 0);
  assert.equal(foreignTrees(own, seamOf([{ name: 'node.exe', commandLine: `node ${other}\\tools\\build.mjs` }])), 0);
  assert.equal(foreignTrees(own, seamOf([{ name: 'node.exe', commandLine: `node ${other}\\node_modules\\vitest\\vitest.mjs run` }])), 1);
  assert.equal(foreignTrees(MAIN, seamOf([testhost(MAIN)])), 0);
  assert.equal(foreignTrees(MAIN, seamOf([testhost(own)])), 1, 'a worktree is foreign to the main checkout');
});

test('a failed read counts nothing and never throws', () => {
  assert.equal(foreignTrees(own, { processes: () => { throw new Error('no powershell'); } }), 0);
  assert.equal(foreignTrees(own, seamOf([{ name: 'testhost.exe', commandLine: undefined }])), 0);
});

test('the sampler\'s peak takes the foreign count', () => {
  const counts = [1, 3, 2];
  const sampler = loadSampler({ every: 1_000_000, count: () => counts.shift() ?? 0 });
  try {
    assert.equal(sampler.peak(), 3);
  } finally {
    sampler.stop();
  }
});

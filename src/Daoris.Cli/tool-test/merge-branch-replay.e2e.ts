import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { git, scratch, branch } from './support/merge-branch-kit.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them

describe('the full set as a replay of passes already proved, in a scratch repository (e2e-replay)', { concurrency: true }, () => {
  test('a second --full replays every gate whose inputs are unchanged and says so; a change its gates reach runs them again', async () => {
    const repo = scratch('replay', [
      { name: 'first', run: 'node gate.mjs first' },
      { name: 'second', run: 'node gate.mjs second' },
    ]);
    let result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual([...repo.ran()].sort(), ['first', 'second']);
    assert.match(result.out, /replayed: 0 of 2 gates/);

    result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().length, 2, 'nothing ran again');
    assert.match(result.out, /replayed: 2 of 2 gates on a standing pass of the same inputs \(first, second\)/);
    assert.match(result.out, /replayed from tree [0-9a-f]+ at \d{4}-\d{2}-\d{2}T/);

    branch(repo.root, 'edit', { 'edit.txt': 'x\n' });
    git(repo.root, 'merge', '--quiet', '--ff-only', 'edit');
    result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);
    assert.equal(repo.ran().length, 4, 'a gate no rule of the table names reaches every path, so both run on the new tree');
    repo.cleanup();
  });

  test('a red never replays: the gate that failed runs again beside the one that passed, which is replayed', async () => {
    const repo = scratch('replay-red', [
      { name: 'first', run: 'node gate.mjs first' },
      // Fails the first time only.
      { name: 'second', run: 'node gate.mjs second 3 once' },
    ]);
    let result = await repo.run(['--full', '--keep-going']);
    assert.equal(result.status, 1, result.out);
    const before = repo.ran().length;
    result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().slice(before), ['second'], 'only the gate with no standing pass runs');
    assert.match(result.out, /replayed: 1 of 2 gates .*\(first\)/);
    repo.cleanup();
  });
});

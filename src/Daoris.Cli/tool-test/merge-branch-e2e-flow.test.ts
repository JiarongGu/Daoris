import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { git, scratch, branch } from './support/merge-branch-kit.ts';

// The merge's flow: a standing pass is replayed, a gate that ends abnormally does not end the run, and each merge leaves a record.
// test-map: strings name paths as data

const records = (root: string): Array<Record<string, any>> => {
  const file = join(root, 'local', 'merge-record.jsonl');
  return existsSync(file) ? readFileSync(file, 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line)) : [];
};

describe('the merge tool\'s flow, end to end in a scratch repository', { concurrency: true }, () => {
  test('a gate declaring reuse is replayed on the same inputs, says from which tree, and runs nothing', async () => {
    const repo = scratch('flow-replay', [
      { name: 'first', run: 'node gate.mjs first', reuse: 'inputs' } as { name: string; run: string },
      { name: 'second', run: 'node gate.mjs second' },
    ]);
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n' });
    git(repo.root, 'checkout', '--quiet', 'main');

    const first = await repo.run(['feature/one']);
    assert.equal(first.status, 0, first.out);
    assert.deepEqual(repo.ran().sort(), ['first', 'second']);
    git(repo.root, 'merge', '--abort');

    const again = await repo.run(['feature/one']);
    assert.equal(again.status, 0, again.out);
    assert.match(again.out, /first\s.*replayed from tree [0-9a-f]{40} at /);
    assert.deepEqual(repo.ran().sort(), ['first', 'second', 'second'], 'the replayed gate ran nothing; the gate that declares no reuse ran again');
  });

  test('a gate that ends with a signal\'s status is said abnormal and the next gate still runs', async () => {
    const repo = scratch('flow-abnormal', [
      { name: 'killed', run: 'node gate.mjs killed 255' },
      { name: 'after', run: 'node gate.mjs after' },
    ]);
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n' });
    git(repo.root, 'checkout', '--quiet', 'main');

    const result = await repo.run(['feature/one']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /ended abnormally \(exit 255/);
    assert.deepEqual(repo.ran(), ['killed', 'after'], 'the run went on to the next gate');
    assert.match(result.out, /1 gate failed: killed/);
  });

  test('each merge appends a record: its branch, each gate with its time, the time ratchet last, a first pass, and a second run is not one', async () => {
    const repo = scratch('flow-record', [{ name: 'first', run: 'node gate.mjs first' }]);
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n' });
    git(repo.root, 'checkout', '--quiet', 'main');

    assert.equal((await repo.run(['feature/one'])).status, 0);
    const [one] = records(repo.root);
    assert.equal(one!['branch'], 'feature/one');
    assert.equal(one!['firstPass'], true);
    assert.deepEqual(one!['gates'].map((gate: { name: string }) => gate.name), ['first', 'time-ratchet']);
    assert.equal(typeof one!['gates'][0].ms, 'number');

    git(repo.root, 'merge', '--abort');
    assert.equal((await repo.run(['feature/one'])).status, 0);
    assert.deepEqual(records(repo.root).map((each) => [each['attempt'], each['firstPass']]), [[1, true], [2, false]]);
  });
});

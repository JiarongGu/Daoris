import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
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

  test('git merge temp files left at the root are removed when the merge is made, so they do not dirty the checkout', async () => {
    const repo = scratch('flow-merge-temps', [{ name: 'first', run: 'node gate.mjs first' }]);
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n' });
    git(repo.root, 'checkout', '--quiet', 'main');
    writeFileSync(join(repo.root, '.merge_file_a1B2c3'), 'left by a git that was stopped');

    const result = await repo.run(['feature/one']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /removed 1 stray git merge temp file at the root: \.merge_file_a1B2c3/);
    assert.equal(existsSync(join(repo.root, '.merge_file_a1B2c3')), false);
  });

  test('--rerun time-ratchet judges the merge\'s timing records again, and runs no gate of the plan', async () => {
    // A stand-in ratchet that is red while its flag stands and says what window it was asked to judge.
    const ratchet = [
      "import { existsSync, appendFileSync } from 'node:fs';",
      "appendFileSync('local/ratchet-asked.txt', `${process.argv.slice(2).join(' ')}\\n`);",
      "if (existsSync('local/slow.flag')) { console.log('time-ratchet: A.Slow took 9000 ms, longer than its listed 1000 ms'); process.exit(1); }",
      "console.log('time-ratchet: clean');",
    ].join('\n');
    const repo = scratch('flow-ratchet-rerun', [{ name: 'first', run: 'node gate.mjs first' }], { 'tools/time-ratchet.mjs': `${ratchet}\n` });
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n' });
    git(repo.root, 'checkout', '--quiet', 'main');
    mkdirSync(join(repo.root, 'local'), { recursive: true });
    writeFileSync(join(repo.root, 'local', 'slow.flag'), '');

    let result = await repo.run(['feature/one']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /1 gate failed: time-ratchet/);
    assert.deepEqual(repo.ran(), ['first']);

    // Still red: the check ran again and said so.
    result = await repo.run(['--rerun', 'time-ratchet']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /failed: time-ratchet/);

    rmSync(join(repo.root, 'local', 'slow.flag'));
    result = await repo.run(['--rerun', 'time-ratchet']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first'], 'the gate that passed is kept, not run again');
    assert.match(result.out, /kept\s+first\s+PASS/);
    const asked = readFileSync(join(repo.root, 'local', 'ratchet-asked.txt'), 'utf8').split('\n').filter(Boolean).map((line) => Number(line.split(' ')[1]));
    assert.equal(asked.length, 3);
    assert.equal(asked[2], asked[0], 'the rerun judges the records since the merge began, as the first judgement did');
  });
});

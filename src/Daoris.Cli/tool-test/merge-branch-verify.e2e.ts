import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { git, scratch, branch, merging } from './support/merge-branch-kit.ts';
import { CASES_AT_ONCE } from './support/e2e-repository.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: tools/merge/merge-verification.ts — the wiring of the workspace's verification rule these cases hold
// reaches: tools/merge/verification.ts — the rule as the gates file declares it

/** A scratch repository whose gates file declares this workspace's verification rule, `build.props` its one wide path. */
function verifying(name: string) {
  const gates = [{ name: 'first', run: 'node gate.mjs first' }];
  const verification = { budgetMinutes: 10, fullRunReasons: { widePaths: ['build.props'], reachShare: 0.5, runs: ['after-landing', 'release'] } };
  const repo = scratch(name, gates, { 'daoris.gates.json': `${JSON.stringify({ verification, gates }, null, 2)}\n`, 'build.props': 'a\n' });
  // What each merge's record keeps of its verification: what ran, the scope its change calls for, and its reason's kind.
  const kept = () => readFileSync(join(repo.root, 'local', 'merge-record.jsonl'), 'utf8').trim().split('\n')
    .map((line) => JSON.parse(line).verification).map((each) => [each.ran, each.scope, each.kind ?? null]);
  return { repo, kept };
}

describe('the verification rule, end to end in a scratch repository (e2e-verify)', { concurrency: CASES_AT_ONCE }, () => {

  test('a merge\'s --full is judged on its batch, by the plan and the merge alike, and each record keeps what ran beside its own reason (D349)', async () => {
    const { repo, kept } = verifying('verify10-batch');
    try {
      branch(repo.root, 'text', { 'notes.txt': 'x\n' });
      branch(repo.root, 'wide', { 'build.props': 'b\n' });

      // The plan judges --full on the batch, as the merge does: refused for the text change alone, taken beside a wide path.
      let result = await repo.run(['--plan', 'text', '--full']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /--full would be refused: --full needs a reason the change bears \(D349\)/);
      assert.match(result.out, /verification: scoped to its reach: 1 path reaches/);
      assert.doesNotMatch(result.out, /\(--full\)/);
      result = await repo.run(['--plan', 'text', '--batch', 'wide', '--full']);
      assert.equal(result.status, 0, result.out);
      assert.doesNotMatch(result.out, /would be refused/);
      assert.match(result.out, /verification: full: its batch's --full, for a reason another of its branches bears/);
      assert.match(result.out, /verification: full: build\.props is a declared wide path/);

      // A misspelt branch is named before --full is judged; a text change bears no reason, and nothing is merged or run.
      result = await repo.run(['nope', '--full']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, /no branch 'nope'/);
      result = await repo.run(['text', '--full']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, /--full needs a reason the change bears \(D349\), and its 1 path bears none/);
      assert.equal(merging(repo.root), false);
      assert.deepEqual(repo.ran(), []);

      // A batch whose one branch bears a reason runs full for both, and each record keeps what ran beside its own reason.
      result = await repo.run(['text', '--batch', 'wide', '--full']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /verification: full: its batch's --full/);
      git(repo.root, 'commit', '--quiet', '--no-edit');
      result = await repo.run(['--continue']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /verification: full: build\.props is a declared wide path/);
      assert.deepEqual(kept(), [['full', 'reach', null], ['full', 'full', 'wide-path']]);
    } finally {
      repo.cleanup();
    }
  });

  test('an open merge\'s --continue --full is judged on what it changes, a fix made in it included, and a plain merge of a wide path records its reach (D349)', async () => {
    const { repo, kept } = verifying('verify10-open');
    try {
      // An open merge: --full on the checkout is refused before its scope is said, and --continue --full is judged on what
      // the open merge changes, a fix made during it included.
      branch(repo.root, 'text', { 'notes.txt': 'y\n' });
      let result = await repo.run(['text']);
      assert.equal(result.status, 0, result.out);
      result = await repo.run(['--full']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, /a merge is open here: node tools\/merge-branch\.mjs --continue gates it \(--full where its changes bear a reason, D349\)/);
      assert.doesNotMatch(result.out, /verification:/);
      result = await repo.run(['--continue', '--full']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, /--full needs a reason the change bears \(D349\)/);
      writeFileSync(join(repo.root, 'build.props'), 'c\n');
      git(repo.root, 'add', 'build.props');
      result = await repo.run(['--continue', '--full']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /verification: full: build\.props is a declared wide path/);
      git(repo.root, 'commit', '--quiet', '--no-edit');

      // A plain merge touching a wide path runs its reach, and says and records the reason it bears and did not take. A
      // batch of one whose merge is committed holds no next merge back, so it starts without a --continue.
      branch(repo.root, 'wide', { 'build.props': 'd\n' });
      result = await repo.run(['wide']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /verification: scoped to its reach: 1 path reaches [^\n]*; it bears a full run's reason \(build\.props is a declared wide path\)/);
      git(repo.root, 'commit', '--quiet', '--no-edit');
      assert.deepEqual(kept(), [['reach', 'reach', null], ['full', 'full', 'wide-path'], ['reach', 'full', 'wide-path']]);

      // --full on the checkout is a release's run by its kind, said before it runs.
      result = await repo.run(['--full']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /verification: full: a release \(--full on the checkout, --ci --stage\)/);

      // A branch's preview says the scope its merge would run.
      branch(repo.root, 'later', { 'build.props': 'e\n' });
      result = await repo.run(['later', '--preview'], { BY_DEFAULT: '1' });
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /verification: scoped to its reach: 1 path reaches [^\n]*; it bears a full run's reason \(build\.props is a declared wide path\)/);
    } finally {
      repo.cleanup();
    }
  });
});

import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { type Verdict, publish } from './support/merge-branch.ts';
import { git, TRAILER, unordered, receipt, scratch, branch, merging } from './support/merge-branch-kit.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row

describe('the tool, end to end in a scratch repository (e2e-rerun)', { concurrency: true }, () => {

  test('--rerun runs a fixed gate again on the merge in place, and names the verdicts it kept and from when', async () => {
    const repo = scratch('rerun', [
      { name: 'first', run: 'node gate.mjs first' },
      // Fails the first time only, as a gate does once the parent has fixed what it found.
      { name: 'second', run: 'node gate.mjs second 3 once' },
      { name: 'third', run: 'node gate.mjs third' },
    ]);
    branch(repo.root, 'fix', { 'fix.txt': 'x\n' });
    // A merge this tool did not leave is not one it re-gates; with none open, --rerun gates the checkout.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-commit', 'fix');
    let result = await repo.run(['--rerun', 'first']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /a merge is open here that this tool did not leave/);
    assert.deepEqual(repo.ran(), []);
    git(repo.root, 'merge', '--abort');

    // One at a time, which the batch keeps, so the failure leaves the third gate unstarted: side by side, the three
    // build nothing in common and would start together.
    result = await repo.run(['fix', '--one-at-a-time']);
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(repo.ran(), ['first', 'second']);
    assert.match(result.out, /NOT RUN\s+third/);
    assert.match(result.out, /--rerun second/, 'the failure says how to run the fixed gate alone');

    result = await repo.run(['--rerun', 'nope']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /no gate 'nope' in the plan: first, second, third/);

    result = await repo.run(['--rerun', 'second']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first', 'second', 'second', 'third'], 'the fixed gate, then the one its failure stopped; never the one that passed');
    assert.match(result.out, /kept\s+first\s+PASS\s+from \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC/);
    assert.match(result.out, /2 gates run and passed; 1 verdict kept \(first, from \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC\)/);
    assert.equal(merging(repo.root), true, 'still merged in place, and never committed');

    // --continue keeps its meaning: every gate again.
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().slice(4), ['first', 'second', 'third']);
    assert.doesNotMatch(result.out, /kept/);
    repo.cleanup();
  });

  test('--commit lands a merge whose gates all passed with no budget, prunes, and refuses one a gate failed or never ran', async () => {
    const repo = scratch('commit', [{ name: 'first', run: 'node gate.mjs first' }, { name: 'second', run: 'node gate.mjs second 3 once' }]);
    branch(repo.root, 'fix', { 'fix.txt': 'x\n' });
    assert.equal((await repo.run(['--commit'])).status, 2, 'no merge is open');
    let result = await repo.run(['fix', '--one-at-a-time', '--no-timeout']);
    assert.equal(result.status, 1, result.out);
    result = await repo.run(['--commit']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /not committed: failed: second/);
    assert.equal(merging(repo.root), true);

    result = await repo.run(['--rerun', 'second', '--no-timeout']);
    assert.equal(result.status, 0, result.out);
    result = await repo.run(['--commit']);
    assert.equal(result.status, 0, result.out);
    assert.equal(merging(repo.root), false, 'committed by the tool');
    assert.match(result.out, /committed fix as [0-9a-f]{7} \(no gate had a time budget in this run\)/);
    assert.match(git(repo.root, 'log', '-1', '--format=%B'), /2 gates passed on the merge with no time budget on a gate \(--no-timeout\)/);
    assert.match(result.out, /prune:|branches merged into main|removed|kept/, 'the prune ran after the commit');
    assert.match(git(repo.root, 'log', '-1', '--format=%B'), /Co-Authored-By:/);
    repo.cleanup();
  });

  // At --rerun the gates are named as this repository names them, so the tool's own lane table places
  // the paths (a scratch repository has no lanes file, and the table's path rules need none).
  test('--rerun keeps a verdict no fixed path reaches, and runs again each one a fixed path reaches, saying which path', async () => {
    const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
    const driverTest = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs';
    const repo = scratch('rerun-reach', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'driver', run: 'node gate.mjs driver' },
      { name: 'driver-process', run: 'node gate.mjs driver-process' },
      // Fails the first time only: the browser test the parent fixes.
      { name: 'web', run: 'node gate.mjs web 3 once' },
    ]);
    branch(repo.root, 'both', { [spec]: 'one\n', [driverTest]: 'one\n' });
    let result = await repo.run(['both']);
    assert.equal(result.status, 1, result.out);
    // Side by side, as they build nothing: which ran counts here, not in what order.
    assert.deepEqual(unordered(repo.ran()), ['cli', 'driver', 'driver-process', 'web']);

    // The browser test fixed in the merge: the web gate, and the baseline every path reaches; the driver's verdicts are kept.
    writeFileSync(join(repo.root, spec), 'two\n');
    result = await repo.run(['--rerun', 'web']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(unordered(repo.ran().slice(4)), ['cli', 'web']);
    assert.match(result.out, /re-run on the merge of both: web, then cli, whose verdict a changed path reaches; 2 verdicts kept/);
    assert.match(result.out, /stale\s+cli\s+PASS\s+from \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC: src\/Daoris\.Web\/e2e\/platform\.spec\.ts changed since, which reaches it/);
    assert.match(result.out, /kept\s+driver\s+PASS\s+from .*no path changed since reaches it/);
    assert.match(result.out, /kept\s+driver-process\s+PASS/);
    assert.doesNotMatch(result.out, /a stage does not count it/, 'every verdict kept is one a stage counts');

    // The driver's tests fixed too: the driver's gates run again, and the web gate's verdict stands.
    writeFileSync(join(repo.root, driverTest), 'two\n');
    result = await repo.run(['--rerun', 'cli']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(unordered(repo.ran().slice(6)), ['cli', 'driver', 'driver-process']);
    assert.match(result.out, /stale\s+driver\s+PASS\s+from .*: src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\/LandingTests\.cs changed since, which reaches it/);
    assert.match(result.out, /kept\s+web\s+PASS/);
    repo.cleanup();
  });

  test('--full gates the checkout as it stands, and --passed says whether the full set passed it, forgiving the records alone', async () => {
    const repo = scratch('full', [
      { name: 'first', run: 'node gate.mjs first' },
      { name: 'second', run: 'node gate.mjs second' },
    ], { 'code.txt': 'base\n' });
    let result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /the full set has NOT passed this checkout/);
    assert.match(result.out, /first\s+none\s+no verdict on this tree/);
    // No background run finished on it either, so the stage's own run is what it names.
    assert.match(result.out, /not staged\s+no background run's receipt on its commit's tree \([0-9a-f]{7}\): no background run finished on it/);
    assert.match(result.out, /\n {2}Run the stage's own CI run on it: node tools\/merge-branch\.mjs --ci --stage [0-9a-f]{7}\n/);

    result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);
    // Side by side, as they build nothing, so each verdict is recorded as its gate ends, in either order.
    assert.deepEqual(unordered(repo.ran()), ['first', 'second']);
    assert.match(result.out, /PASS\s+first\s.*local\/scratch\/full-[0-9a-f]{7}\/first\.log/);
    assert.equal(merging(repo.root), false, '--full alone merges nothing');
    const record = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { schema: number; verdicts: Verdict[] };
    assert.equal(record.schema, 1);
    assert.deepEqual(unordered(record.verdicts.map((verdict) => `${verdict.gate} ${verdict.verdict}`)), ['first PASS', 'second PASS']);
    assert.equal(record.verdicts[0]!.tree, git(repo.root, 'rev-parse', 'HEAD^{tree}'), 'the tree that was gated, the clean checkout\'s');
    assert.equal(record.verdicts[0]!.commit, git(repo.root, 'rev-parse', 'HEAD'));

    // `--full` on the checkout is no background run, so its passes alone do not stage it.
    assert.match(result.out, /A stage still needs its evidence: no background run's receipt on its commit's tree/);
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /the full set has passed this checkout \(.*\): 2 of 2 gates, but the stage's evidence is missing \(CISCOPE1d\)/);
    // A sweep-only run's receipt, and a run that is stopped, are said and never counted.
    receipt(repo.root, { kind: 'sweep' });
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /only a sweep-only run finished on it, which ran one half and is no receipt a stage counts/);
    receipt(repo.root, { kind: 'ordinary', finished: false });
    assert.equal((await repo.run(['--passed'])).status, 1);
    receipt(repo.root);
    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /the full set has passed this checkout/);
    assert.match(result.out, /first\s+PASS\s+\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC\s+on this tree/);
    assert.match(result.out, /receipt\s+a ci run finished on this commit's tree/);

    // The parent writes the records after the gates: a tree that differs only by them is forgiven, once committed.
    writeFileSync(join(repo.root, 'CHANGELOG.md'), '# Changelog\n\n## Unreleased\n\n- **A** line, long enough.\n');
    // Uncommitted, it is a dirty checkout whose HEAD has a receipt and passing verdicts, refused all the same.
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /not staged\s+its content \(tree [0-9a-f]{7}\) is not its commit's \(tree [0-9a-f]{7}\): a stage builds a commit's tree, never uncommitted changes/);
    git(repo.root, 'commit', '--quiet', '-am', `the records${TRAILER}`);
    receipt(repo.root);
    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /on a tree that differs from this one only by the records \(CHANGELOG\.md\)/);
    // Code is not: no rule of the lane table places it, so it reaches every gate, and each says it made its verdict stale.
    writeFileSync(join(repo.root, 'code.txt'), 'changed\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /second\s+stale\s+PASS \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC, on a tree before code\.txt changed, which reaches it/);
    repo.cleanup();
  });

  // At the stage: a browser test fixed after the full set, and re-gated by the web gate alone.
  test('--passed counts a verdict no path changed since reaches: a browser test re-gated alone stages, a driver change does not', async () => {
    const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
    const driverTest = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs';
    const repo = scratch('passed-reach', [
      { name: 'driver', run: 'node gate.mjs driver' },
      { name: 'driver-process', run: 'node gate.mjs driver-process' },
      { name: 'web', run: 'node gate.mjs web' },
    ], { [spec]: 'one\n', [driverTest]: 'one\n' });
    let result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);

    // Changed since the full set: the web gate's verdict is stale, and the refusal names the path; the driver's stand.
    writeFileSync(join(repo.root, spec), 'two\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /2 of 3 gates/);
    assert.match(result.out, /web\s+stale\s+PASS .* UTC, on a tree before src\/Daoris\.Web\/e2e\/platform\.spec\.ts changed, which reaches it/);
    assert.match(result.out, /driver\s+PASS .* UTC\s+on a tree that differs from this one only by paths it does not reach \(src\/Daoris\.Web\/e2e\/platform\.spec\.ts\)/);
    assert.match(result.out, /driver-process\s+PASS/);

    // The fix landed by a merge, which runs the web gate alone: the tree stages, with no second full run.
    git(repo.root, 'checkout', '--', spec);
    branch(repo.root, 'spec-fix', { [spec]: 'two\n' });
    result = await repo.run(['spec-fix']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().slice(3), ['web']);
    git(repo.root, 'commit', '--quiet', '--no-edit');
    writeFileSync(join(repo.root, 'CHANGELOG.md'), '# Changelog\n\n## Unreleased\n\n- **A** browser test, fixed.\n');
    // The records committed, and a background run finished on that commit, as the watch's would.
    git(repo.root, 'commit', '--quiet', '-am', `the records${TRAILER}`);
    receipt(repo.root);
    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /the full set has passed this checkout/);
    assert.match(result.out, /web\s+PASS .* UTC\s+on a tree that differs from this one only by the records \(CHANGELOG\.md\)/);
    assert.match(result.out, /driver\s+PASS .* UTC\s+on a tree that differs from this one only by paths it does not reach \(src\/Daoris\.Web\/e2e\/platform\.spec\.ts\) and the records \(CHANGELOG\.md\)/);

    // A driver change reaches the driver's gates: those are stale, naming it, and the web gate's verdict stands.
    writeFileSync(join(repo.root, driverTest), 'two\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /driver\s+stale\s+PASS .*, on a tree before src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\/LandingTests\.cs changed, which reaches it/);
    assert.match(result.out, /driver-process\s+stale/);
    assert.match(result.out, /web\s+PASS .*only by paths it does not reach \(src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\/LandingTests\.cs\)/);
    repo.cleanup();
  });

  // A fix committed on the checkout after the full set stages once the gates it reaches run again.
  test('--stale runs exactly the gates a fix committed past the full set reaches, and the stage then accepts the tree', async () => {
    const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
    const driverTest = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs';
    const repo = scratch('stale', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'driver', run: 'node gate.mjs driver' },
      { name: 'driver-process', run: 'node gate.mjs driver-process' },
      { name: 'web', run: 'node gate.mjs web' },
    ], { [spec]: 'one\n', [driverTest]: 'one\n' });
    let result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);

    // The refusal names --stale, which runs two gates, not a second full run.
    writeFileSync(join(repo.root, spec), 'two\n');
    git(repo.root, 'commit', '--quiet', '-am', `fix the spec${TRAILER}`);
    // With no background run on the fix, the stage's own run is what it names.
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /\n {2}Run the stage's own CI run on it: node tools\/merge-branch\.mjs --ci --stage [0-9a-f]{7}\n/);
    // One finished on it (and failed the stale gates): the smaller command is what is left.
    receipt(repo.root);
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /\n {2}Run the 2 stale gates on it: node tools\/merge-branch\.mjs --stale\n/);
    assert.doesNotMatch(result.out, /--full/);

    // Work left uncommitted is refused, by name, and nothing runs.
    writeFileSync(join(repo.root, 'stray.txt'), 'left here\n');
    result = await repo.run(['--stale']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not clean:\s+\?\? stray\.txt/);
    rmSync(join(repo.root, 'stray.txt'));
    assert.deepEqual(repo.ran().slice(4), [], 'a refusal ran a gate');

    result = await repo.run(['--stale']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(unordered(repo.ran().slice(4)), ['cli', 'web'], "the baseline and the web gate; the driver's verdicts stand");
    assert.match(result.out, /stale\s+web\s+PASS\s+from .* UTC: src\/Daoris\.Web\/e2e\/platform\.spec\.ts changed since, which reaches it/);
    assert.match(result.out, /driver-process\s+PASS .* UTC\s+on a tree that differs from this one only by paths it does not reach/);
    assert.match(result.out, /2 gates run and passed on [0-9a-f]{7}; the full set has passed this checkout: 4 of 4 gates\. A stage of this tree is allowed\./);
    assert.equal(merging(repo.root), false, 'nothing is merged');
    // What publish:desktop asks before it builds.
    assert.equal(publish.ungatedRefusal(join(dirname(repo.root), 'install'), repo.root).refusal, null);

    // Nothing is stale now: --stale runs nothing, and says the stage passes.
    result = await repo.run(['--stale']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /nothing is stale on the checkout/);
    assert.deepEqual(repo.ran().slice(6), []);
    repo.cleanup();
  });

  test('a flaked gate re-run alone on the checkout makes the stage accept it, and a dirty tree is refused, naming the files', async () => {
    const repo = scratch('flake-rerun', [
      { name: 'first', run: 'node gate.mjs first' },
      // Fails the first time only: a test that timed out under load.
      { name: 'second', run: 'node gate.mjs second 3 once' },
      { name: 'third', run: 'node gate.mjs third' },
    ], { 'code.txt': 'base\n' });
    let result = await repo.run(['--full', '--keep-going']);
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(unordered(repo.ran()), ['first', 'second', 'third']);
    assert.match(result.out, /\n {2}Run the gate it lacks on it: node tools\/merge-branch\.mjs --rerun second\n/, 'the failed full set names the smaller command');

    // The background run finished on this commit too, its flake failing there as here.
    receipt(repo.root);
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /second\s+FAIL\s+.* UTC\s+on this tree/);
    assert.match(result.out, /\n {2}Run the gate it lacks on it: node tools\/merge-branch\.mjs --rerun second\n/);

    writeFileSync(join(repo.root, 'code.txt'), 'changed\n');
    result = await repo.run(['--rerun', 'second']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not clean:\s+\.M code\.txt/);
    git(repo.root, 'checkout', '--', 'code.txt');
    result = await repo.run(['--rerun', 'nope']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /no gate 'nope' in the plan: first, second, third/);
    assert.deepEqual(repo.ran().slice(3), [], 'a refusal ran a gate');

    result = await repo.run(['--rerun', 'second']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().slice(3), ['second'], 'the flaked gate alone: nothing changed, so no other verdict is stale');
    assert.match(result.out, /re-run on the checkout \(HEAD [0-9a-f]{7}\): second; 2 verdicts kept/);
    assert.match(result.out, /PASS\s+second\s.*local\/scratch\/full-[0-9a-f]{7}\/second\.log/);
    assert.match(result.out, /A stage of this tree is allowed\./);
    assert.equal(merging(repo.root), false, 'nothing is merged');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /second\s+PASS .* UTC\s+on this tree/);
    repo.cleanup();
  });
});

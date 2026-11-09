import { test } from 'node:test';
import assert from 'node:assert/strict';
import { tool, refusal } from '../support/merge-branch.ts';

test('the arguments name one branch, or several after --batch, and the flags', () => {
  assert.deepEqual(tool.parseArgs(['mod9']), {
    branches: ['mod9'], keepGoing: false, commitCheck: true, resume: false, dropBatch: false, plan: false, prune: false, autoPrune: true,
    full: false, gated: false, rerun: [], passed: false, stale: false, oneAtATime: false, beside: false, besideCommit: null,
    ci: false, ciCommit: null, watch: false, stop: false, ciStage: false, ciNight: false,
    preview: false, messageFile: null, queue: false, recheck: false, queueRun: false, daorisVerify: false,
  });
  // The queue's verbs, each alone with what it takes.
  assert.equal(tool.parseArgs(['a', '--preview']).preview, true);
  assert.equal(tool.parseArgs(['a', '--message', 'local/a.msg']).messageFile, 'local/a.msg');
  assert.equal(tool.parseArgs(['a', '--message', 'local/a.msg', '--one-at-a-time']).oneAtATime, true);
  assert.equal(tool.parseArgs(['--queue', '--recheck']).recheck, true);
  assert.equal(tool.parseArgs(['--queue-run']).queueRun, true);
  assert.deepEqual(tool.parseArgs(['--drop-batch', 'a']).branches, ['a']);
  assert.throws(() => tool.parseArgs(['a', '--message']), /--message names its file/);
  assert.throws(() => tool.parseArgs(['a', '--message', 'm', '--full']), /takes only --one-at-a-time and --no-commit-check/);
  assert.throws(() => tool.parseArgs(['a', '--preview', '--gated']), /--preview names one branch and nothing else/);
  assert.throws(() => tool.parseArgs(['--recheck']), /--queue --recheck/);
  assert.throws(() => tool.parseArgs(['--queue', 'a']), /--queue takes only --recheck/);
  assert.equal(tool.parseArgs(['a', '--gated']).gated, true);
  assert.equal(tool.parseArgs(['--continue', '--gated']).gated, true);
  assert.throws(() => tool.parseArgs(['--rerun', 'web', '--gated']), /--rerun takes gate names/);
  // Every verb that runs gates runs them one at a time when told to, as on a loaded machine.
  for (const argv of [['a', '--one-at-a-time'], ['--continue', '--one-at-a-time'], ['--rerun', 'web', '--one-at-a-time'],
    ['--stale', '--one-at-a-time'], ['--full', '--one-at-a-time'], ['--full', '--keep-going', '--one-at-a-time']]) {
    assert.equal(tool.parseArgs(argv).oneAtATime, true, argv.join(' '));
  }
  // Each stale gate run again on the checkout as it stands.
  assert.equal(tool.parseArgs(['--stale']).stale, true);
  assert.equal(tool.parseArgs(['--stale', '--keep-going']).keepGoing, true);
  // Every gate for one merge, or for the checkout as it stands; and whether the full set passed it.
  assert.equal(tool.parseArgs(['a', '--full']).full, true);
  assert.equal(tool.parseArgs(['--plan', 'a', '--full']).full, true);
  assert.equal(tool.parseArgs(['--continue', '--full']).full, true);
  const alone = tool.parseArgs(['--full']);
  assert.deepEqual([alone.full, alone.branches], [true, []]);
  assert.equal(tool.parseArgs(['--passed']).passed, true);
  // The named gates again, on the merge in place.
  assert.deepEqual(tool.parseArgs(['--rerun', 'web', 'driver-process']).rerun, ['web', 'driver-process']);
  assert.equal(tool.parseArgs(['--rerun', 'web', '--keep-going']).keepGoing, true);
  assert.deepEqual(tool.parseArgs(['a', '--batch', 'b', 'c']).branches, ['a', 'b', 'c']);
  const flagged = tool.parseArgs(['a', '--keep-going', '--no-commit-check']);
  assert.equal(flagged.keepGoing, true);
  assert.equal(flagged.commitCheck, false);
  assert.equal(tool.parseArgs(['--continue']).resume, true);
  assert.equal(tool.parseArgs(['--continue', '--keep-going']).keepGoing, true);
  assert.equal(tool.parseArgs(['--drop-batch']).dropBatch, true);
  assert.equal(tool.parseArgs(['--plan', 'a', '--batch', 'b']).plan, true);
  // The prune on its own, its plan, and a merge that leaves the merged branches standing.
  const prune = tool.parseArgs(['--prune']);
  assert.deepEqual([prune.prune, prune.plan, prune.branches], [true, false, []]);
  const planned = tool.parseArgs(['--prune', '--plan']);
  assert.deepEqual([planned.prune, planned.plan], [true, true]);
  assert.equal(tool.parseArgs(['a', '--no-prune']).autoPrune, false);
  assert.equal(tool.parseArgs(['--continue', '--no-prune']).autoPrune, false);
});

test('the arguments refuse what would merge the wrong thing, with exit 2', () => {
  const cases: [string[], RegExp][] = [
    [[], /name the branch/],
    [['a', 'b'], /--batch/],
    [['a', '--batch'], /at least one branch/],
    [['--batch', 'b'], /follows the first branch/],
    [['a', '--batch', 'a'], /named twice/],
    [['a', '--bogus'], /unknown option '--bogus'/],
    [['--continue', 'a'], /takes no branch/],
    [['--continue', '--drop-batch'], /one of/],
    [['--plan', '--continue'], /one of/],
    [['--prune', 'a'], /--prune takes no branch/],
    [['--prune', '--continue'], /one of/],
    [['--prune', '--drop-batch'], /one of/],
    [['--prune', '--no-prune'], /--no-prune/],
    [['--rerun'], /--rerun names the gates to run again/],
    [['--rerun', 'web', '--continue'], /one of/],
    [['--rerun', 'web', '--full'], /--rerun takes gate names, --keep-going and --one-at-a-time/],
    [['--rerun', 'web', 'web'], /'web' is named twice/],
    [['--passed', 'a'], /--passed takes no branch/],
    [['--passed', '--full'], /one of/],
    [['--passed', '--keep-going'], /--passed takes nothing else/],
    [['--passed', '--gated'], /--passed takes nothing else/],
    [['a', '--full', '--gated'], /--full and --gated are alternatives/],
    [['--prune', '--full'], /--full is for a merge or the checkout/],
    [['--stale', 'a'], /--stale takes no branch/],
    [['--stale', '--rerun', 'web'], /one of/],
    [['--stale', '--passed'], /one of/],
    [['--stale', '--full'], /--stale takes only --keep-going and --one-at-a-time/],
    [['--stale', '--no-prune'], /--stale takes only --keep-going and --one-at-a-time/],
    [['--prune', '--one-at-a-time'], /--one-at-a-time is for a run of gates; --prune runs no gate/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

test('--full --beside gates a commit in a worktree of its own, main\'s tip when it names none, and takes only the run\'s flags', () => {
  const plain = tool.parseArgs(['--full', '--beside']);
  assert.deepEqual([plain.full, plain.beside, plain.besideCommit, plain.branches], [true, true, null, []]);
  const named = tool.parseArgs(['--full', '--beside', 'abc1234', '--keep-going']);
  assert.deepEqual([named.beside, named.besideCommit, named.keepGoing, named.branches], [true, 'abc1234', true, []]);
  assert.equal(tool.parseArgs(['--beside', '--full', '--one-at-a-time']).oneAtATime, true);
  const cases: [string[], RegExp][] = [
    [['--beside'], /--beside runs the full set: --full --beside \[<commit>\]/],
    [['a', '--full', '--beside'], /--beside takes no branch/],
    [['--full', '--beside', 'abc1234', 'b'], /--beside takes no branch/],
    [['--full', '--beside', '--no-commit-check'], /--full --beside takes only a commit, --keep-going and --one-at-a-time/],
    [['--full', '--beside', '--beside'], /--beside is given once/],
    [['--prune', '--beside'], /one of|--beside/],
    [['--passed', '--beside'], /one of|--beside/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

// The flag that makes the tool the declared runner, valid with `--full --beside` alone, taking its commit from the environment.
test('--daoris-verify is the declared runner: only with --full --beside, naming no commit, taking only the run\'s flags', () => {
  const asked = tool.parseArgs(['--full', '--beside', '--daoris-verify', '--one-at-a-time']);
  assert.deepEqual([asked.full, asked.beside, asked.daorisVerify, asked.besideCommit, asked.oneAtATime], [true, true, true, null, true]);
  assert.equal(tool.parseArgs(['--full', '--beside']).daorisVerify, false);
  const cases: [string[], RegExp][] = [
    [['--daoris-verify'], /--daoris-verify is the declared runner's: --full --beside --daoris-verify/],
    [['--full', '--daoris-verify'], /--daoris-verify is the declared runner's/],
    [['--beside', '--daoris-verify'], /--daoris-verify is the declared runner's|--beside runs the full set/],
    [['--full', '--beside', 'abc1234', '--daoris-verify'], /takes its commit from DAORIS_VERIFY_COMMIT/],
    [['--full', '--beside', '--daoris-verify', '--no-commit-check'], /--full --beside takes only/],
    [['--passed', '--daoris-verify'], /--daoris-verify is the declared runner's/],
    [['--ci', '--daoris-verify'], /--ci takes only/],
    [['--queue', '--daoris-verify'], /--queue takes only/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

test("--ci gates a commit in the gate tree, main's tip unless named; --watch follows main's tip, --stop ends the watch", () => {
  const plain = tool.parseArgs(['--ci']);
  assert.deepEqual([plain.ci, plain.ciCommit, plain.watch, plain.stop, plain.branches, plain.full], [true, null, false, false, [], false]);
  const named = tool.parseArgs(['--ci', 'abc1234', '--one-at-a-time']);
  assert.deepEqual([named.ci, named.ciCommit, named.oneAtATime], [true, 'abc1234', true]);
  assert.throws(() => tool.parseArgs(['--ci', '--keep-going']), /--keep-going is not taken beside --ci/, 'a CI run keeps going anyway: the flag would change nothing, so it is refused');
  const watch = tool.parseArgs(['--ci', '--watch']);
  assert.deepEqual([watch.ci, watch.watch, watch.ciCommit], [true, true, null]);
  assert.equal(tool.parseArgs(['--watch', '--ci']).watch, true);
  assert.deepEqual([tool.parseArgs(['--ci', '--stop']).stop], [true]);
  assert.equal(plain.ciStage, false, 'a plain run is no stage\'s');
  // The stage's path, its commit after --stage or after --ci.
  for (const argv of [['--ci', '--stage', 'abc1234'], ['--ci', 'abc1234', '--stage'], ['--stage', '--ci', 'abc1234']]) {
    const stage = tool.parseArgs(argv);
    assert.deepEqual([stage.ci, stage.ciStage, stage.ciCommit, stage.branches], [true, true, 'abc1234', []], argv.join(' '));
  }
  assert.deepEqual([tool.parseArgs(['--ci', '--stage']).ciStage, tool.parseArgs(['--ci', '--stage']).ciCommit], [true, null]);
  // The nightly run by hand, every gate whole, its commit after --night or after --ci.
  for (const argv of [['--ci', '--night', 'abc1234'], ['--ci', 'abc1234', '--night']]) {
    const night = tool.parseArgs(argv);
    assert.deepEqual([night.ci, night.ciNight, night.ciStage, night.ciCommit], [true, true, false, 'abc1234'], argv.join(' '));
  }
  const cases: [string[], RegExp][] = [
    [['--night'], /--night is the CI's: --ci --night \[<commit>\]/],
    [['--ci', '--night', '--stage'], /--ci --night is a run of its own/],
    [['--ci', '--night', '--watch'], /--ci --night is a run of its own/],
    [['--watch'], /--watch is the CI's: --ci --watch/],
    [['--stop'], /--stop is the CI watch's: --ci --stop/],
    [['--ci', 'abc1234', 'b'], /--ci takes no branch/],
    [['a', '--ci'], /--ci takes no branch/],
    [['--ci', '--watch', 'abc1234'], /--ci --watch gates main's tip as it moves: it takes no commit/],
    [['--ci', '--stop', 'abc1234'], /--ci --stop takes nothing else/],
    [['--ci', '--stop', '--watch'], /--ci --stop takes nothing else/],
    [['--ci', '--full'], /--ci takes only a commit, --stage, --night, --watch, --stop and --one-at-a-time/],
    [['--stage'], /--stage is the CI's: --ci --stage \[<commit>\]/],
    [['--ci', '--watch', '--stage'], /--ci --watch owes the stage's rehearsals/],
    [['--ci', '--stop', '--stage'], /--ci --stop takes nothing else/],
    [['--ci', '--stage', '--stage'], /--stage is given once/],
    [['--ci', '--stage', 'abc1234', 'b'], /--ci takes no branch/],
    [['--ci', '--beside'], /--ci takes only/],
    [['--ci', '--no-prune'], /--ci takes only/],
    [['--ci', '--ci'], /--ci is given once/],
    [['--passed', '--ci'], /one of|--ci takes only/],
    [['--prune', '--ci'], /one of|--ci takes only/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { census } from './layers-census.mjs';
import { counts } from './layers-census/counts.mjs';
import { quickSteps } from './quick/steps.mjs';
import { CHECK_STEPS } from './verify-checks/steps.mjs';
import { checkTimes, scanRecords } from './time-ratchet.mjs';
import { judge, readVerdicts, say, writeVerdicts } from './quick/verdicts.mjs';
import { baseVerdicts } from './quick/base.mjs';
import { runQuick } from './quick.mjs';

const scratch = () => {
  mkdirSync('_fixtures', { recursive: true });
  return mkdtempSync(join('_fixtures', 'quick-'));
};

test('the steps are the index, the contracts, and each check of the cli gate once but the time ratchet, the census\'s counts among them', () => {
  const checks = CHECK_STEPS.map((step) => step.run).filter((run) => run !== 'node tools/orient-index.mjs' && run !== 'node tools/contracts.mjs --if-stale' && run !== 'node tools/time-ratchet.mjs');
  assert.deepEqual(quickSteps(), ['node tools/orient-index.mjs', 'node tools/contracts.mjs --if-stale', ...checks]);
  assert.ok(checks.includes('node tools/layers-census/counts.mjs'));
});

test('the writers run alone before the rest, which run side by side', async () => {
  const root = scratch();
  try {
    const events = [];
    const run = async (command) => {
      events.push(`start ${command}`);
      await new Promise((resolve) => setTimeout(resolve, 5));
      events.push(`end ${command}`);
      return { status: 0, output: '' };
    };
    const steps = ['node tools/orient-index.mjs', 'node tools/contracts.mjs --if-stale', 'one', 'two'];
    assert.equal(await runQuick({ root, steps, run, say: () => {}, width: 2 }), 0);
    assert.deepEqual(events.slice(0, 4), ['start node tools/orient-index.mjs', 'end node tools/orient-index.mjs', 'start node tools/contracts.mjs --if-stale', 'end node tools/contracts.mjs --if-stale']);
    assert.deepEqual(events.slice(4, 6), ['start one', 'start two'], 'the checks start together');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a red time record on disk leaves quick green, and the gates that judge time still read it red', async () => {
  const root = scratch();
  try {
    mkdirSync(join(root, 'tools'), { recursive: true });
    writeFileSync(join(root, 'tools', 'layers-census.json'), '{}');
    const records = join(root, '_fixtures', 'test-timing');
    mkdirSync(records, { recursive: true });
    writeFileSync(join(records, 'driver.trx'), '<TestRun><Results><UnitTestResult testName="Ns.Loaded.slow" duration="00:01:40.0000000" outcome="Passed" /></Results></TestRun>');
    const tree = { files: [], read: () => '' };
    const exits = {
      'node tools/time-ratchet.mjs': () => (checkTimes(scanRecords(records, root).over, {}).length > 0 ? 1 : 0),
      'node tools/layers-census.mjs': () => census({ root, argv: ['--records', records], tree }),
      'node tools/layers-census/counts.mjs': () => counts({ root, tree }),
    };
    const steps = async (list) => Object.fromEntries(await Promise.all(list.map(async (step) => [step, (await exits[step]?.()) ?? 0])));
    const ran = await steps(quickSteps());
    const run = (command) => ({ status: ran[command], output: '' });
    const lines = [];
    assert.equal(await runQuick({ root, steps: quickSteps(), run, say: (line) => lines.push(line), now: () => 0 }), 0, lines.join('\n'));
    assert.match(lines.join('\n'), /judges no test time/);
    const gates = await steps(['node tools/time-ratchet.mjs', 'node tools/layers-census.mjs']);
    assert.deepEqual(gates, { 'node tools/time-ratchet.mjs': 1, 'node tools/layers-census.mjs': 1 }, 'the record is red to both time judgements');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a red step does not stop the steps after it, and every red is said', async () => {
  const root = scratch();
  try {
    const seen = [];
    const lines = [];
    const run = (command) => {
      seen.push(command);
      return command === 'one' || command === 'three' ? { status: 1, output: `${command} broke\n` } : { status: 0, output: '' };
    };
    const code = await runQuick({ root, steps: ['one', 'two', 'three'], run, say: (line) => lines.push(line), now: () => 0 });
    assert.equal(code, 1);
    assert.deepEqual(seen, ['one', 'two', 'three']);
    assert.match(lines.join('\n'), /one broke/);
    assert.match(lines.join('\n'), /3 steps, 2 red/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a red the base already stood in is said as the base\'s, and any other as new', () => {
  const base = { head: 'b', at: 't', steps: { one: 'fail', two: 'pass' } };
  const verdicts = { one: 'fail', two: 'fail', three: 'fail', four: 'pass' };
  assert.deepEqual(judge(verdicts, base), { new: ['two', 'three'], base: ['one'], unknown: [] });
  assert.deepEqual(judge({ one: 'fail' }, null), { new: [], base: [], unknown: ['one'] });
  const text = say({ new: ['two'], base: ['one'], unknown: [] }).join('\n');
  assert.match(text, /NEW +two/);
  assert.match(text, /RED ON THE BASE +one/);
});

function branchedRepo() {
  const root = scratch();
  const git = (...args) => execFileSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@t', ...args], { cwd: root, stdio: 'pipe', windowsHide: true });
  git('init', '-q', '-b', 'main');
  writeFileSync(join(root, '.gitignore'), 'local/\n');
  writeFileSync(join(root, 'check.mjs'), 'process.exit(0);\n');
  git('add', '-A');
  git('commit', '-q', '-m', 'main');
  git('checkout', '-q', '-b', 'branch');
  writeFileSync(join(root, 'check.mjs'), 'process.exit(1);\n');
  git('commit', '-q', '-am', 'the branch breaks the check');
  return { root, git };
}

test('a red the branch introduced is its own even where a base was recorded after the branch\'s commit', async () => {
  const { root } = branchedRepo();
  try {
    writeVerdicts(join(root, 'local', 'quick-base.json'), { head: 'the branch tip', at: 't', steps: { 'node check.mjs': 'fail' } });
    const lines = [];
    assert.equal(await runQuick({ root, steps: ['node check.mjs'], say: (line) => lines.push(line), now: () => 0 }), 1);
    assert.match(lines.join('\n'), /NEW +node check\.mjs/);
    assert.doesNotMatch(lines.join('\n'), /RED ON THE BASE/);
    assert.equal(readVerdicts(join(root, 'local', 'quick-base.json'))?.steps['node check.mjs'], 'pass', 'the base is main at the merge base');
    assert.equal(existsSync(join(root, 'local', 'quick-base-tree')), false, 'the base worktree is gone');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a red main stands in is the base\'s, the record is reused for the same merge base, and --record-base records every step there', async () => {
  const { root, git } = branchedRepo();
  try {
    git('checkout', '-q', 'main');
    writeFileSync(join(root, 'check.mjs'), 'process.exit(1);\n');
    git('commit', '-q', '-am', 'main is red');
    git('checkout', '-q', 'branch');
    git('merge', '-q', '-s', 'ours', 'main', '-m', 'merge main');
    const lines = [];
    assert.equal(await runQuick({ root, steps: ['node check.mjs'], say: (line) => lines.push(line), now: () => 0 }), 1);
    assert.match(lines.join('\n'), /RED ON THE BASE +node check\.mjs/);
    const asked = [];
    const base = baseVerdicts(root, ['node check.mjs'], { runAt: () => (command) => (asked.push(command), 0) });
    assert.deepEqual(asked, [], 'the held verdict of the same merge base is not run again');
    assert.equal(base?.steps['node check.mjs'], 'fail');
    assert.equal(await runQuick({ root, steps: ['node check.mjs', 'node -e 0'], say: () => {}, now: () => 0, recordBase: true }), 1);
    assert.deepEqual(readVerdicts(join(root, 'local', 'quick-base.json'))?.steps, { 'node check.mjs': 'fail', 'node -e 0': 'pass' });
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a verdicts file that does not read is no base', () => {
  const root = scratch();
  try {
    writeFileSync(join(root, 'bad.json'), '{');
    assert.equal(readVerdicts(join(root, 'bad.json')), null);
    assert.equal(readVerdicts(join(root, 'absent.json')), null);
    writeVerdicts(join(root, 'ok.json'), { head: 'h', at: 'a', steps: { x: 'pass' } });
    assert.equal(readVerdicts(join(root, 'ok.json'))?.steps.x, 'pass');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

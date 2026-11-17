/**
 * The declared runner's probes and the queue's drains end to end (TOOLTIER1's tier).
 */
import { describe, test } from 'node:test';
import { join, delimiter, dirname } from 'node:path';
import { mkdirSync, writeFileSync, readFileSync, existsSync, rmSync } from 'node:fs';
import assert from 'node:assert/strict';
import { spawnSync, spawn } from 'node:child_process';
import {
  queueScratch, pollutedDotnet, pathOf, commitOnMain, verifier, modes, type Answered, VERIFY, envGate, treeGate, TREE, pidsIn, alive, pause,
  TRAILER,
} from './support/merge-queue-scratch.ts';
import { type Candidate, type Failure, q, type Drained, sig } from './support/merge-queue-kit.ts';
import { PATIENCE } from '../test/_fixture.ts';

describe('INTEGRATE1j and QUEUEDRAIN1: a probe repeats a failure as composed, and a drop ends it, end to end in a scratch repository', { concurrency: true }, () => {
  test('INTEGRATE1j: a probe repeats a failure as composed, so a test its own class pollutes fails again with its signature where alone it passes; a composition that would differ, or is not the one kept, is refused', async () => {
    const repo = queueScratch('verify-probe');
    const shims = join(repo.root, 'local', 'shims');
    mkdirSync(shims, { recursive: true });
    try {
      const calls = join(repo.root, 'local', 'dotnet-calls.jsonl');
      writeFileSync(join(shims, 'dotnet.mjs'), pollutedDotnet(calls));
      if (process.platform === 'win32') writeFileSync(join(shims, 'dotnet.cmd'), '@node "%~dp0dotnet.mjs" %*\r\n');
      else writeFileSync(join(shims, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/dotnet.mjs" "$@"\n', { mode: 0o755 });
      // The stand-in is first on the runner's own PATH as it starts: the test process's environment is never changed.
      const PATH = `${shims}${delimiter}${pathOf(repo.env)}`;
      const base = repo.git('rev-parse', 'HEAD');
      const settings = '<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration></RunSettings>\n';
      const commit = commitOnMain(repo, { 'fixture.runsettings': settings, 'Fixture.csproj': '<Project />\n' }, 'a test project');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'The fixture', run: 'dotnet test Fixture.csproj --settings fixture.runsettings --filter "Category!=Process"' }] });
      const asked = modes(repo, run, base);
      const ran = await asked('run', commit, { PATH });
      assert.equal(ran.status, 1, ran.out);
      const [gate] = run.read()!.gates as Answered[];
      assert.equal(gate!.verdict, 'fail');
      assert.deepEqual(gate!.failures.map((each: Answered) => [each.unit, each.test]), [['Fixture.PollutedTests', 'Fixture.PollutedTests.B_Reads']]);
      const kept = JSON.parse(readFileSync(gate!.composition.settings, 'utf8')) as Answered;
      assert.deepEqual(kept.settings.runsettings, { path: 'fixture.runsettings', text: settings }, 'the run settings as the commit holds them');
      // Alone, the failing test passes: it fails only after the test before it in its class.
      const alone = spawnSync(process.execPath, [join(shims, 'dotnet.mjs'), 'test', 'Fixture.csproj', '--filter', 'FullyQualifiedName~Fixture.PollutedTests.B_Reads'], { encoding: 'utf8', windowsHide: true });
      assert.equal(alone.status, 0, alone.stdout);
      // The probe runs its class as the gate composed it, and it fails again with its signature.
      const failure = join(repo.root, 'local', 'verify', 'failure.json');
      const named = (composition: object) => writeFileSync(failure, JSON.stringify({ gate: gate!.name, ...gate!.failures[0], composition }));
      named(gate!.composition);
      const probe = (at: string) => asked('probe', at, { DAORIS_VERIFY_FAILURE: failure, PATH });
      const again = await probe(commit);
      assert.equal(again.status, 1, again.out);
      const answer = run.read()!;
      assert.deepEqual(Object.keys(answer).sort(), ['base', 'commit', 'declaration', 'log', 'mode', 'result', 'schema', 'signature']);
      assert.deepEqual([answer.schema, answer.mode, answer.commit, answer.base, answer.result], [2, 'probe', commit, base, 'fail']);
      assert.deepEqual(answer.signature, gate!.failures[0].signature);
      assert.ok(existsSync(answer.log), answer.log);
      const last = readFileSync(calls, 'utf8').trim().split('\n').at(-1)!;
      assert.match(last, /\(Category!=Process\)&\(FullyQualifiedName~Fixture\.PollutedTests\)/, 'the probe ran the failing class, not the test alone');
      assert.match(last, /fixture\.runsettings/, 'with the gate\'s own run settings');
      // At a commit whose run settings differ, the composition would differ: refused, nothing written, nothing run.
      const later = commitOnMain(repo, { 'fixture.runsettings': settings.replace('>1<', '>4<') }, 'the tests run four at once');
      const before = readFileSync(calls, 'utf8');
      const differs = await probe(later);
      assert.equal(differs.status, 2, differs.out);
      assert.match(differs.out, new RegExp(`the composition at ${later.slice(0, 7)} differs from the run's \\(the run settings fixture\\.runsettings\\)`));
      assert.equal(run.read(), null);
      assert.equal(readFileSync(calls, 'utf8'), before, 'nothing ran');
      // A digest that is not the kept composition's is refused too.
      named({ ...gate!.composition, digest: `sha256:${'0'.repeat(64)}` });
      const forged = await probe(commit);
      assert.equal(forged.status, 2, forged.out);
      assert.match(forged.out, /the composition handed is not the one kept/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: a run that started and did not finish (npm ci failing in its tree) answers so, each gate not run, and exits 2; D192\'s schema 1 does the same', async () => {
    const repo = queueScratch('verify-unfinished');
    try {
      const base = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'package.json': '{ "name": "fixture", "version": "1.0.0" }\n', 'package-lock.json': '{ not json\n' }, 'a lockfile npm cannot read');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check' }] });
      const stopped = await modes(repo, run, base)('run', commit);
      assert.equal(stopped.status, 2, stopped.out);
      const answer = run.read()!;
      assert.deepEqual([answer.schema, answer.mode, answer.commit, answer.base], [2, 'run', commit, base]);
      assert.match(answer.unfinished, /^the run did not finish: npm ci failed in the gate tree's \./);
      assert.deepEqual(answer.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'not-run'], ['check', 'not-run']]);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');
      const plain = await repo.run(VERIFY, run.env(commit));
      assert.equal(plain.status, 2, plain.out);
      const said = run.read()!;
      assert.equal(said.schema, 1);
      assert.match(said.unfinished, /^the run did not finish: npm ci failed/);
      assert.deepEqual(said.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'not-run'], ['check', 'not-run']]);
    } finally {
      await repo.cleanup();
    }
  });

  test('QUEUETESTLEAK1: a probe the runner starts gets the environment its process had at its start: a PATH and a variable set in it later reach no probe', async () => {
    const repo = queueScratch('probe-env');
    const dir = join(repo.root, 'local', 'probe');
    const shims = join(dirname(repo.root), 'shims');
    mkdirSync(dir, { recursive: true });
    mkdirSync(shims, { recursive: true });
    try {
      const gate = join(dirname(repo.root), 'envgate.mjs');
      const record = join(dirname(repo.root), 'probe-env.json');
      writeFileSync(gate, envGate(record));
      const base = repo.git('rev-parse', 'main');
      const candidate: Candidate = { base, tip: base, members: [], state: 'running', reforms: 0, recordsOnly: false };
      const failure: Failure = { unit: 'check', test: 'check', gate: 'check', signature: { gate: 'check', step: 'run' }, names: 'gate' };
      const after = { PATH: `${shims}${delimiter}${pathOf(repo.env)}`, QUEUE_DRAIN_DIR: dir };
      const probed = await repo.probe({ candidate, plan: [{ name: 'check', run: `node "${gate}" check` }], dir, failure, ks: [0], after });
      assert.equal(probed.status, 0, probed.out);
      assert.deepEqual(probed.lines.map((line) => line.answer?.result), ['pass'], probed.out);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { drain: string | null; path: string };
      assert.equal(seen.drain, null, 'a variable set in the runner after its start is not passed on');
      assert.ok(!seen.path.split(delimiter).includes(shims), 'a PATH set in the runner after its start is not passed on');
    } finally {
      await repo.cleanup();
    }
  });

  // A waiter whose request has left the queue (dropped while queued, or merged in another branch's
  // batch) must not start a runner every second.
  test('QUEUEWAITEND1: a request dropped while queued is told to its waiter, which ends saying so; a request gone from every record ends its wait too', async () => {
    const repo = queueScratch('wait-ends');
    const hold = join(repo.root, 'local', 'go');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      repo.branch('b2', { 'b.txt': 'b\n' });
      for (const name of ['b1', 'b2']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      // The runner holds before it forms, so both requests stay queued; a waiter that never ends is ended at the shared patience.
      const first = repo.run(['b1', '--message', 'local/b1.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1 && Boolean(repo.queue()?.runner));
      const second = repo.run(['b2', '--message', 'local/b2.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      assert.equal((await repo.run(['--drop-batch', 'b1'])).status, 0);
      const dropped = await first;
      assert.equal(dropped.status, 1, dropped.out);
      assert.match(dropped.out, /b1's request was taken off the queue \(--drop-batch, .*\): nothing of it was merged by this request/);
      // b2's request leaves every record with no outcome (a record written by hand): its waiter says so and ends.
      q.writeQueue(repo.root, { ...repo.queue()!, queued: [] });
      const gone = await second;
      assert.equal(gone.status, 1, gone.out);
      assert.match(gone.out, /b2's request \([0-9a-f]{8}\) is no longer in the queue's records/);
      assert.deepEqual(repo.queue()!.handedBack.map((each) => [each.branch, each.handedBack?.how]), [['b1', 'dropped']]);
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('QUEUEDRAIN1: a candidate dropped while its gate runs ends the gate, its child and grandchild; the scoped tier is released after; what is not its own survives; the next candidate forms at once', async () => {
    const repo = queueScratch('drain-gate');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    const bystander = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore', windowsHide: true });
    try {
      repo.branch('b1', { 'hold-check': 'x\n', 'qgate.mjs': treeGate(dir, "existsSync('hold-check')") });
      repo.branch('b2', { 'b.txt': 'b\n' });
      for (const name of ['b1', 'b2']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const first = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.ok(pids.every(alive), 'the gate and what it started run');
      await repo.until(() => repo.admission()?.scopedTier?.state === 'running');
      assert.equal(repo.admission()?.scopedTier?.state, 'running');
      const second = repo.run(['b2', '--message', 'local/b2.msg']);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      // The tier must never read released while a process of the dropped run still lives: read the tier first, then the processes.
      const unsafe: string[] = [];
      const sampled = (async () => {
        while (pids.some(alive)) {
          const record = repo.admission();
          const released = record !== undefined && !record.scopedTier;
          if (released && pids.some(alive)) unsafe.push('the scoped tier was released while a process of the dropped run lived');
          await pause(10);
        }
      })();
      const dropped = await repo.run(['--drop-batch', 'b1']);
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /taken off the queue: b1/);
      assert.match(dropped.out, /the runner \(pid \d+\) ended gates .*check.*each with everything it started/);
      await sampled;
      assert.deepEqual(unsafe, []);
      assert.deepEqual(pids.filter(alive), [], 'the gate, its child and its grandchild are gone, though nothing released them');
      assert.equal(alive(bystander.pid!), true, 'a process the run did not start is left alone');
      assert.equal((await first).status, 1);
      const promoted = await second;
      assert.equal(promoted.status, 0, `${promoted.out}\n${repo.log()}`);
      assert.match(promoted.out, /b2 is in main/);
      assert.equal(existsSync(join(repo.root, 'b.txt')), true);
      assert.equal(existsSync(join(repo.root, 'hold-check')), false, 'the dropped branch never lands');
      assert.match(repo.log(), /was dropped or replaced: ended gates .*check/);
      const runs = q.readScopeRuns(repo.root) as { state: string; members: { branch: string }[]; drained?: Drained }[];
      const ended = runs.find((each) => each.members.some((member) => member.branch === 'b1'));
      assert.equal(ended?.state, 'ended');
      assert.ok(ended?.drained?.ended.includes('check'));
      assert.equal(runs.at(-1)?.state, 'green');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      bystander.kill();
      await repo.cleanup();
    }
  });

  test('QUEUEDRAIN1: a candidate dropped during a failure probe ends the probe with its tree, starts no more, and the runner is free', async () => {
    const repo = queueScratch('drain-probe');
    const dir = join(repo.root, 'local', 'drain');
    const hold = join(repo.root, 'local', 'go');
    mkdirSync(dir, { recursive: true });
    try {
      // The first run of `check` (the candidate's) fails, and so does its second, the confirm run; every run after
      // them is a probe, and a probe holds.
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, 'count >= 3'));
      repo.git('commit', '--quiet', '-am', `a gate that holds its probes${TRAILER}`);
      repo.branch('good', { 'good.txt': 'fine\n' });
      repo.branch('bad', { 'break-check': 'x\n' });
      for (const name of ['good', 'bad']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const env = { MERGE_BRANCH_QUEUE_HOLD: hold };
      const good = repo.run(['good', '--message', 'local/good.msg'], env);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const bad = repo.run(['bad', '--message', 'local/bad.msg'], env);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 3, 'the candidate ran once, its confirm run once, and its first probe is the one held');
      assert.ok(pids.every(alive));
      const dropped = await repo.run(['--drop-batch', 'good']);
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /the probe check\.prefix-1/);
      assert.deepEqual(pids.filter(alive), [], 'the probe, its child and its grandchild are gone');
      assert.equal((await good).status, 1);
      assert.equal((await bad).status, 1);
      // A runner that went on probing would hold at the next probe for ever; it is idle, and no probe started after the drop.
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(repo.queue()?.runner, null, repo.log());
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 3);
      assert.match(repo.log(), /was dropped or replaced: ended no gate running, the probe check\.prefix-1/);
      const runs = q.readScopeRuns(repo.root) as { state: string; judged?: unknown }[];
      assert.equal(runs.at(-1)?.state, 'ended');
      assert.equal(runs.at(-1)?.judged, undefined, 'a judgement cut short is not recorded');
      assert.equal(existsSync(join(repo.root, 'good.txt')), false);
    } finally {
      writeFileSync(join(dir, 'release'), '');
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('QUEUEDRAIN1: a class probe, which runs the gate narrowed to the failing class, is ended with its tree when the candidate is dropped, and none starts after', async () => {
    const repo = queueScratch('drain-class');
    const dir = join(repo.root, 'local', 'drain');
    const shims = join(repo.root, 'local', 'shims');
    mkdirSync(dir, { recursive: true });
    mkdirSync(shims, { recursive: true });
    const live = join(dir, 'live');
    try {
      // A `dotnet` that records what it is asked and holds, with a child and a grandchild, as a test host does.
      writeFileSync(join(shims, 'dotnet.mjs'), treeGate(dir, 'true', 'args'));
      if (process.platform === 'win32') writeFileSync(join(shims, 'dotnet.cmd'), `@node "%~dp0dotnet.mjs" check %*\r\n`);
      else {
        writeFileSync(join(shims, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/dotnet.mjs" check "$@"\n', { mode: 0o755 });
      }
      repo.branch('b1', { 'a.txt': 'a\n' });
      const base = repo.git('rev-parse', 'main');
      const commit = repo.git('rev-parse', 'b1');
      const candidate: Candidate = { base, tip: commit, members: [{ branch: 'b1', tip: commit, commit, request: 'first' }], state: 'running', reforms: 0, recordsOnly: false };
      const plan = [{ name: 'driver', run: 'dotnet test Fixture.csproj --filter "Category!=Process"' }];
      const failure: Failure = { unit: 'SessionTests', test: 'Driver.Tests.SessionTests.Resumes', gate: 'driver', signature: sig('x'), names: 'test' };
      // The stand-in `dotnet` is first on the probe's own PATH, as its process starts; the test process's environment,
      // which every other case's children are spawned from, is never changed.
      writeFileSync(live, '');
      const probing = repo.probe({ candidate, plan, dir, failure, ks: [1, 0], live }, { PATH: `${shims}${delimiter}${pathOf(repo.env)}` });
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3);
      assert.match(readFileSync(join(dir, 'args'), 'utf8'), /\(Category!=Process\)&\(FullyQualifiedName~SessionTests\)/, 'the probe is the gate narrowed to the failing class');
      rmSync(live);
      const probed = await probing;
      assert.equal(probed.status, 0, probed.out);
      assert.match(probed.lines[0]?.error ?? '', /was dropped/, probed.out);
      assert.deepEqual(pids.filter(alive), [], 'the class probe, its child and its grandchild are gone');
      assert.match(probed.lines[1]?.error ?? '', /was dropped/, 'a probe asked after the drop is refused');
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 1, 'no probe started after the drop');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      await repo.cleanup();
    }
  });
});

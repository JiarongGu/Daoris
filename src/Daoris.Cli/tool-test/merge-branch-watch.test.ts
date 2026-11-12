import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { EventEmitter } from 'node:events';
import os from 'node:os';
import { closeSync, existsSync, mkdirSync, openSync, readFileSync, writeFileSync, writeSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { tool, type Gate, type Step, type GateResult, type Trees } from './support/merge-branch.ts';
import { PROCESS_FAILURE, planned, within, laneRun, sleep, alive, TREE, treePids, stillRunning, cleanupAfterKill, PARENT, SHELL_PARENT } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


// ---------------------------------------------------------------------------------------------------
// A full set beside the merges is a guest on the machine

test('a full set beside lowers its own priority to below normal at its start, and says so when it cannot', () => {
  const asked: [number, number][] = [];
  const said: string[] = [];
  const env: Record<string, string | undefined> = {};
  tool.startBesideRun({ setPriority: (pid, priority) => { asked.push([pid, priority]); }, env, say: (line) => said.push(line) });
  assert.deepEqual(asked, [[0, os.constants.priority.PRIORITY_BELOW_NORMAL]]);
  assert.deepEqual(said, []);

  // A failure to lower is said and the run goes on: the environment is still set.
  const failed: string[] = [];
  const bare: Record<string, string | undefined> = {};
  tool.startBesideRun({ setPriority: () => { throw new Error('access denied'); }, env: bare, say: (line) => failed.push(line) });
  assert.match(failed.join('\n'), /could not lower its priority \(access denied\)/);
  assert.equal(bare.MSBUILDDISABLENODEREUSE, '1');
});


test('the gates beside get their own build servers: none reused, none borrowed, none left for the owner\'s builds', () => {
  assert.deepEqual({ ...tool.BESIDE_ENVIRONMENT }, { MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false' });
  const env: Record<string, string | undefined> = { KEEP: 'x' };
  tool.startBesideRun({ setPriority: () => {}, env, say: () => {} });
  assert.deepEqual(env, { KEEP: 'x', MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false' });
});


test('a gate step started after the run lowered itself runs at the lowered priority', async () => {
  const fx = makeFixture('merge-branch-priority');
  const before = os.getPriority(0);
  try {
    assert.equal(tool.lowerPriority(), null);
    const lowered = os.getPriority(0);
    // At least below normal: a run beside already lowered itself, so its tool-tests start there (the CI of 2026-10-19).
    assert.ok(lowered >= os.constants.priority.PRIORITY_BELOW_NORMAL, `lowered from ${before} to ${lowered}`);
    const log = join(fx.root, 'priority.log');
    writeFileSync(log, '');
    const fd = openSync(log, 'a');
    const code = await tool.runStep('node -e "console.log(\'priority=\' + require(\'os\').getPriority())"', fx.root, fd);
    closeSync(fd);
    assert.equal(code, 0);
    assert.match(readFileSync(log, 'utf8'), new RegExp(`priority=${lowered}\\b`));
  } finally {
    os.setPriority(0, before);
    fx.cleanup();
  }
});


// The rehearsals' bounds are wall-clock, so in a run beside they start at normal priority

test('a run beside raises a rehearsal step\'s child to normal, touches no suite\'s, and says once when it cannot', async () => {
  const fx = makeFixture('merge-branch-raise');
  try {
    const asked: [number, number][] = [];
    tool.startBesideRun({ setPriority: (pid, priority) => { asked.push([pid, priority]); }, env: {}, say: () => {} });
    const raise = tool.rehearsalRaise();
    assert.ok(raise);
    raise(4242);
    assert.deepEqual(asked.slice(-1), [[4242, os.constants.priority.PRIORITY_NORMAL]]);

    // runGate hands a rehearsal's step the raise and a suite's none.
    const given: Record<string, boolean> = {};
    const step: Step = async (command, _cwd, _fd, options) => { given[command] = Boolean(options?.raise); return 0; };
    await tool.runGate(fx.root, planned('rf', 'npm run rehearse:family'), fx.root, { step });
    await tool.runGate(fx.root, planned('s', 'dotnet test src/S'), fx.root, { step });
    assert.deepEqual(given, { 'npm run rehearse:family': true, 'dotnet test src/S': false });

    // A Process half's bounds are wall-clock too, so its full run and each test it runs alone get the raise;
    // the fast half beside them does not.
    const raised: string[] = [];
    const flaky = 'Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked';
    const alone = 'Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)\n';
    const processStep: Step = async (command, _cwd, fd, options) => {
      if (options?.raise) raised.push(command.includes('--filter') ? 'alone' : 'full');
      writeSync(fd, command.includes('--filter') ? alone : PROCESS_FAILURE);
      return command.includes('--filter') ? 0 : 1;
    };
    const half = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
    assert.equal((await tool.runGate(fx.root, half, fx.root, { step: processStep })).verdict, 'FLAKE');
    assert.deepEqual(raised, ['full', 'alone'], `${flaky} was run alone without the raise`);
    let fastRaised = false;
    await tool.runGate(fx.root, planned('fast', 'dotnet test src/D --filter Category!=Process'), fx.root, {
      step: async (_command, _cwd, _fd, options) => { fastRaised = Boolean(options?.raise); return 0; },
    });
    assert.equal(fastRaised, false);

    // A gate the repository marks clock-bound (`"clock": true`) is raised too, though it is no Process half:
    // the CLI's suites hold real processes on wall-clock bounds. The mark is read from daoris.gates.json and planned.
    writeFileSync(join(fx.root, 'daoris.gates.json'), JSON.stringify({ gates: [
      { name: 'cli', run: 'npm run verify', clock: true },
      { name: 'light', run: 'node tools/check.mjs' },
    ] }));
    const plan = tool.readPlan(fx.root);
    assert.equal(plan.find((gate) => gate.name === 'cli')?.clock, true);
    assert.equal('clock' in (plan.find((gate) => gate.name === 'light') ?? {}), false);
    let clockRaised = false;
    await tool.runGate(fx.root, plan.find((gate) => gate.name === 'cli')!, fx.root, {
      step: async (_command, _cwd, _fd, options) => { clockRaised = Boolean(options?.raise); return 0; },
    });
    assert.equal(clockRaised, true);

    // A run that could not lower itself raises nothing; one whose raise fails says so once.
    tool.startBesideRun({ setPriority: () => { throw new Error('denied'); }, env: {}, say: () => {} });
    assert.equal(tool.rehearsalRaise(), null);
    const said: string[] = [];
    let lowered = false;
    tool.startBesideRun({ setPriority: (pid) => { if (pid !== 0) throw new Error('denied'); lowered = true; }, env: {}, say: (line) => said.push(line) });
    assert.ok(lowered);
    const again = tool.rehearsalRaise();
    assert.ok(again);
    again(1);
    again(2);
    assert.equal(said.length, 1);
    assert.match(said[0] ?? '', /could not raise a rehearsal to normal priority \(denied\)/);
  } finally {
    tool.startBesideRun({ setPriority: () => { throw new Error('reset'); }, env: {}, say: () => {} });
    fx.cleanup();
  }
});


test('in a real spawn, a rehearsal step\'s grandchild starts at normal and a suite step\'s stays lowered', async () => {
  const fx = makeFixture('merge-branch-raise-real');
  const before = os.getPriority(0);
  try {
    assert.equal(tool.lowerPriority(), null);
    tool.startBesideRun({ setPriority: os.setPriority, env: {}, say: () => {} });
    const command = `node -e "require('child_process').execFileSync(process.execPath, ['-e', 'console.log(\\"grand=\\" + require(\\"os\\").getPriority())'], {stdio: 'inherit'})"`;
    const grand = async (run: string) => {
      const gate = planned('g', run);
      await tool.runGate(fx.root, gate, fx.root, {
        step: (_cmd, cwd, fd, options) => tool.runStep(command, cwd, fd, options),
      });
      return Number(/grand=(-?\d+)/.exec(readFileSync(join(fx.root, 'g.log'), 'utf8'))?.[1]);
    };
    // The CI itself runs below normal, so the grandchild is compared with the constants, not the starting value.
    assert.equal(await grand('npm run rehearse'), os.constants.priority.PRIORITY_NORMAL);
    assert.equal(await grand('dotnet test src/S'), os.constants.priority.PRIORITY_BELOW_NORMAL);
  } finally {
    os.setPriority(0, before);
    tool.startBesideRun({ setPriority: () => { throw new Error('reset'); }, env: {}, say: () => {} });
    fx.cleanup();
  }
});


test('a run of main\'s tip stops before its next gate once a newer tip supersedes it, and is recorded superseded, never passed', async () => {
  let tip: string | null = 'aaaaaaa1';
  const watch = tool.watchTip('aaaaaaa1', () => tip);
  const gates = ['a', 'b', 'c'].map((name) => planned(name, `dotnet build src/S/${name}`));
  const lanes = laneRun(['a', 'b', 'c']);
  const printed: string[] = [];
  const results = await tool.runGates('root', gates, 'logs', {
    keepGoing: true,
    run: async (root, gate, at, beside) => {
      const result = await lanes.run(root, gate, at, beside);
      tip = 'bbbbbbb2';
      return result;
    },
    halt: watch.check,
    print: (line) => printed.push(line),
  });
  assert.deepEqual(results.map((result) => result.gate.name), ['a'], 'gate b never ran');
  assert.deepEqual(lanes.events, ['start a', 'end a']);
  assert.deepEqual(watch.superseded, { from: 'aaaaaaa1', to: 'bbbbbbb2' });
  assert.match(printed.at(-1)!, /NOT RUN\s+c/);
  const lines = tool.supersededLines(watch.superseded!, ['a'], ['b', 'c']).join('\n');
  assert.match(lines, /superseded/);
  assert.match(lines, /aaaaaaa.*bbbbbbb/);
  assert.match(lines, /ran: a/);
  assert.match(lines, /left: b, c/);
  assert.deepEqual(tool.besideOutcome(1, watch.superseded), { passed: false, superseded: 'bbbbbbb2' });
  assert.deepEqual(tool.besideOutcome(0, null), { passed: true, superseded: null });
  // Even a run whose gates all passed is not passed once it is superseded.
  assert.equal(tool.besideOutcome(0, watch.superseded).passed, false);
});


test('a run given a named commit is never superseded: no commit to watch, no tip read, none polled', async () => {
  let reads = 0;
  let moved = 0;
  const watch = tool.watchTip(null, () => { reads += 1; return 'bbbbbbb2'; }, { onMove: () => { moved += 1; } });
  assert.equal(watch.check(), false);
  // Nor polled while its gates run.
  const unwatch = watch.poll(5);
  await sleep(50);
  unwatch();
  assert.equal(reads, 0);
  assert.equal(moved, 0);
  assert.equal(watch.superseded, null);
  // A tip that cannot be read is not a newer one.
  assert.equal(tool.watchTip('aaaaaaa1', () => null).check(), false);
  // The same tip is not newer.
  assert.equal(tool.watchTip('aaaaaaa1', () => 'aaaaaaa1').check(), false);
});


test('a run that ends its trees ends a running step\'s whole tree, its shell, child and grandchild, and the step answers ended', async () => {
  const fx = makeFixture('merge-branch-end-tree');
  const script = join(fx.root, 'tree.mjs');
  writeFileSync(script, TREE);
  const log = join(fx.root, 'step.log');
  writeFileSync(log, '');
  const fd = openSync(log, 'a');
  let trees: Trees | null = null;
  const pids: number[] = [];
  try {
    trees = tool.stepTrees();
    const ran = tool.runStep(`node "${script}" "${fx.root}"`, fx.root, fd, { trees });
    pids.push(...await treePids(fx.root));
    trees.end('main moved from aaaaaaa to bbbbbbb');
    let code = 0;
    await within(ran.then((answer) => { code = answer; }), 'the ended step answering');
    assert.equal(code, tool.STEP_ENDED, 'a code of its own, never a failure\'s');
    assert.notEqual(tool.STEP_ENDED, 0);
    assert.deepEqual(await stillRunning(pids), [], 'every process the step started has ended');
    assert.equal(trees.ended, 'main moved from aaaaaaa to bbbbbbb');
  } finally {
    trees?.end('the test ended');
    for (const pid of pids) if (alive(pid)) process.kill(pid);
    await stillRunning(pids);
    closeSync(fd);
    await cleanupAfterKill(fx);
  }
});


test('a run killed outright leaves no gate tree: the next run taking its lock ends what the dead pid started (CIPRIO1d)', { skip: process.platform !== 'win32' }, async () => {
  const fx = makeFixture('merge-branch-killed-run');
  const tree = join(fx.root, 'tree.mjs');
  const parent = join(fx.root, 'parent.mjs');
  const at = join(fx.root, 'pids');
  mkdirSync(at, { recursive: true });
  writeFileSync(tree, TREE);
  writeFileSync(parent, PARENT);
  const run = spawn(process.execPath, [parent, fx.root, tree, at], { stdio: 'ignore', windowsHide: true });
  const pids: number[] = [];
  try {
    pids.push(...await treePids(at));
    assert.ok(alive(run.pid!));
    process.kill(run.pid!, 'SIGKILL');
    await stillRunning([run.pid!]);
    const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40) });
    assert.equal(taken.stale?.pid, run.pid);
    assert.ok((taken.stale?.ended.length ?? 0) > 0, 'the shell the dead run started was ended');
    assert.deepEqual(await stillRunning(pids), [], 'the shell, child and grandchild have ended');
  } finally {
    for (const pid of [run.pid!, ...pids]) if (alive(pid)) process.kill(pid);
    await stillRunning([run.pid!, ...pids]);
    await cleanupAfterKill(fx);
  }
});


test('a run killed outright leaves no orphan naming the gate tree: the takeover ends what runs on under a dead parent (CIPRIO1e)', { skip: process.platform !== 'win32' }, async () => {
  const fx = makeFixture('merge-branch-killed-shell');
  const tree = join(fx.root, 'tree.mjs');
  const parent = join(fx.root, 'parent.mjs');
  const at = join(fx.root, 'local', 'gate-tree', 'pids');
  mkdirSync(at, { recursive: true });
  writeFileSync(tree, TREE);
  writeFileSync(parent, SHELL_PARENT);
  const run = spawn(process.execPath, [parent, fx.root, tree, at], { stdio: 'ignore', windowsHide: true });
  const pids: number[] = [];
  try {
    pids.push(...await treePids(at));
    process.kill(run.pid!, 'SIGKILL');
    await stillRunning([run.pid!]);
    assert.ok(pids.every(alive), 'the shell\'s child and grandchild outlive the killed run');
    const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40) });
    assert.equal(taken.stale?.pid, run.pid);
    // The shell itself may be the one stranded (its parent dead, its command line naming the tree), the child going with it.
    assert.ok((taken.stale?.ended.length ?? 0) > 0, 'the orphan was ended');
    assert.deepEqual(await stillRunning(pids), [], 'the orphan and its grandchild have ended');
  } finally {
    for (const pid of [run.pid!, ...pids]) if (alive(pid)) process.kill(pid);
    await stillRunning([run.pid!, ...pids]);
    await cleanupAfterKill(fx);
  }
});


test('a gate whose step the run ended reads ENDED, never FAIL, and nothing of it starts after: no step before it, no rehearsal run again, no test run alone', async () => {
  const fx = makeFixture('merge-branch-ended-gate');
  const why = 'main moved from aaaaaaa to bbbbbbb';
  try {
    const gateOf = async (gate: Gate, step: (trees: Trees) => Step) => {
      const trees = tool.stepTrees({ end: () => {} });
      const ran: string[] = [];
      const handed: unknown[] = [];
      const inner = step(trees);
      const result = await tool.runGate(fx.root, gate, fx.root, {
        trees,
        step: (command, cwd, fd, options) => { ran.push(command); handed.push(options?.trees); return inner(command, cwd, fd, options); },
      });
      assert.ok(handed.every((given) => given === trees), 'every step is handed the run\'s trees, so its child is held');
      return { result, ran, log: readFileSync(result.log, 'utf8') };
    };
    const endedAt = (at: string, code = tool.STEP_ENDED) => (trees: Trees): Step => async (command) => {
      if (command !== at) return 0;
      trees.end(why);
      return code;
    };
    const isEnded = ({ result, log }: { result: GateResult; log: string }) => {
      assert.equal(result.verdict, 'ENDED');
      assert.match(result.note, /ended, not failed: main moved from aaaaaaa to bbbbbbb/);
      assert.match(log, /\(ended, not failed: main moved from aaaaaaa to bbbbbbb\)/);
      assert.doesNotMatch(log, /\(exited -?\d+\)\s*$/);
    };

    // Ended while it ran: a rehearsal is not run again.
    const running = await gateOf(planned('rf', 'npm run rehearse:family'), endedAt('npm run rehearse:family'));
    isEnded(running);
    assert.deepEqual(running.ran, ['npm run rehearse:family']);
    assert.equal(existsSync(join(fx.root, 'rf.rerun.log')), false);

    // A step that answered a failure of its own once the run had ended it (a person's Ctrl+C reaches the console's
    // processes too) is ended, not failed, and not run again: it printed nothing, which alone would run it again.
    const own = await gateOf(planned('rf', 'npm run rehearse:family'), endedAt('npm run rehearse:family', 1));
    isEnded(own);
    assert.deepEqual(own.ran, ['npm run rehearse:family']);
    assert.equal(existsSync(join(fx.root, 'rf.rerun.log')), false);

    // A Process half that failed on its own, then waited to run its failure alone while main moved: nothing runs alone.
    const half = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
    const trees = tool.stepTrees({ end: () => {} });
    const alone: string[] = [];
    const waited = await tool.runGate(fx.root, half, fx.root, {
      trees,
      settled: async () => { trees.end(why); },
      step: async (command, _cwd, fd) => {
        if (command.includes('--filter')) alone.push(command);
        writeSync(fd, PROCESS_FAILURE);
        return 1;
      },
    });
    assert.equal(waited.verdict, 'ENDED');
    assert.match(waited.note, /exit 1.*ended, not failed: main moved/);
    assert.deepEqual(alone, []);
    assert.equal(existsSync(join(fx.root, 'driver-process.rerun.log')), false);

    // Once the run has ended its trees, a gate asked to run starts no step at all.
    const late = await gateOf(planned('s', 'dotnet build src/S'), (ended) => { ended.end(why); return async () => 0; });
    assert.equal(late.result.verdict, 'ENDED');
    assert.deepEqual(late.ran, []);
    assert.match(late.log, /not started: dotnet build src\/S/);
  } finally {
    fx.cleanup();
  }
});


test('a run of main\'s tip polls the tip while a gate runs and, on the first move, ends what it is running and starts nothing more', async () => {
  const fx = makeFixture('merge-branch-poll');
  let tip = 'aaaaaaa1';
  const endedPids: number[] = [];
  let release = () => {};
  const trees = tool.stepTrees({ end: (pid) => { endedPids.push(pid); release(); } });
  const moves: string[] = [];
  const watch = tool.watchTip('aaaaaaa1', () => tip, {
    onMove: (moved) => {
      moves.push(moved.to);
      trees.end(`main moved from ${moved.from.slice(0, 7)} to ${moved.to.slice(0, 7)}`);
    },
  });
  const unwatch = watch.poll(20);
  try {
    // b holds a pid as a real step holds its child, and main moves while it runs, so only the poll can see it.
    const step: Step = (command, _cwd, _fd, options) => new Promise((done) => {
      if (!command.endsWith(' b')) return done(0);
      const held = options!.trees!;
      held.hold(4242);
      release = () => done(held.release(4242) ? tool.STEP_ENDED : 0);
      tip = 'bbbbbbb2';
    });
    const gates = ['a', 'b', 'c'].map((name) => planned(name, `node gate.mjs ${name}`));
    const printed: string[] = [];
    let results: GateResult[] = [];
    await within(tool.runGates(fx.root, gates, fx.root, {
      oneAtATime: true,
      run: (root, gate, dir, beside) => tool.runGate(root, gate, dir, { ...beside, step, trees }),
      halt: () => Boolean(trees.ended) || watch.check(),
      print: (line) => printed.push(line),
    }).then((answer) => { results = answer; }), 'the gate running ended by the poll');
    assert.deepEqual(results.map((result) => `${result.gate.name} ${result.verdict}`), ['a PASS', 'b ENDED']);
    assert.deepEqual(endedPids, [4242]);
    assert.deepEqual(moves, ['bbbbbbb2'], 'said once, however often it is asked after');
    assert.deepEqual(watch.superseded, { from: 'aaaaaaa1', to: 'bbbbbbb2' });
    assert.match(printed.join('\n'), /ENDED\s+b\s.*ended, not failed: main moved from aaaaaaa to bbbbbbb/);
    assert.match(printed.at(-1)!, /NOT RUN\s+c/);
    assert.doesNotMatch(printed.join('\n'), /FAIL/);

    const lines = tool.supersededLines(watch.superseded!, ['a'], ['c'], ['b']).join('\n');
    assert.match(lines, /superseded: main moved from aaaaaaa to bbbbbbb, so this run ended the gates it was running and started no more; it is not a pass/);
    assert.match(lines, /ran: a\n\s+ended: b\n\s+left: c/);
    // The cadence is a few seconds: a gate on an old tip runs that long at most, for a git read every few seconds.
    assert.ok(tool.TIP_POLL_MS >= 1000 && tool.TIP_POLL_MS <= 10_000, `${tool.TIP_POLL_MS} ms`);
  } finally {
    unwatch();
    fx.cleanup();
  }
});


test('a person\'s stop (Ctrl+C) ends the trees the run has running and is heard once; a second exits as it always did', () => {
  const ended: number[] = [];
  const trees = tool.stepTrees({ end: (pid) => ended.push(pid) });
  trees.hold(7);
  const said: string[] = [];
  const terminal = new EventEmitter();
  const unhook = tool.stopOnInterrupt(trees, { on: terminal, say: (line) => said.push(line) });
  assert.equal(terminal.listenerCount('SIGINT'), 1);
  terminal.emit('SIGINT');
  assert.deepEqual(ended, [7]);
  assert.match(trees.ended ?? '', /a person stopped the run/);
  assert.match(said.join('\n'), /stopped: ending the gates running/);
  assert.equal(terminal.listenerCount('SIGINT'), 0, 'once: a second Ctrl+C is the default exit');
  unhook();

  // A run that ends with no stop leaves no listener behind.
  const quiet = new EventEmitter();
  const off = tool.stopOnInterrupt(tool.stepTrees({ end: () => {} }), { on: quiet, say: () => {} });
  off();
  assert.equal(quiet.listenerCount('SIGINT'), 0);

  const lines = tool.stoppedLines(['a'], ['c'], ['b']).join('\n');
  assert.match(lines, /stopped: a person stopped this run, so it ended the gates it was running and started no more; it is not a pass/);
  assert.match(lines, /ran: a\n\s+ended: b\n\s+left: c/);
});
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, writeSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { tool, refusal, type Gate, type Step, type GateResult, type MemoryWait, processRun, repoPlan } from './support/merge-branch.ts';
import { PROCESS_FAILURE, TIMED_SPEC, PLAIN_SPEC, measuredLog, planned, fakeStep, signal, within, laneRun, summary, OK_LINES, GiB, standIn, MEMORY_SIGNS, measuredRun, NODE_TITLE_A, NODE_TITLE_B, nodeLog, nodeRun } from './support/merge-branch-kit.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


test('MEMGATE1: the memory a gate needs is its class\'s, or what daoris.gates.json declares; a light gate needs none', () => {
  const needs = Object.fromEntries(repoPlan.map((gate) => [gate.name, tool.memoryNeed(gate)]));
  const { suite, process: half, rehearsal } = tool.MEMORY_NEEDS;
  // Each declared gate's need is read from its own declaration and class, so a gate added to the declaration is held to the same rule.
  const expected = (gate: Gate): number | undefined => {
    if (typeof gate.memoryGb === 'number') return gate.memoryGb;
    if (tool.isProcessGate(gate)) return half;
    if (tool.isHeavyGate(gate)) return suite;
    return tool.gateKind(gate.run) === 'rehearsal' ? rehearsal : 0;
  };
  assert.deepEqual(needs, Object.fromEntries(repoPlan.map((gate) => [gate.name, expected(gate)])));
  assert.equal(needs['universal'], 0, 'a blocking check is light');
  assert.equal(needs['driver'], suite, 'a .NET suite needs a suite\'s memory');
  assert.equal(needs['driver-process'], half, 'a Process half needs its class\'s');
  assert.equal(needs['deployment'], rehearsal, 'a rehearsal needs its class\'s');
  assert.ok(suite! > 0 && half! >= suite! && rehearsal! >= half!, 'a heavier class needs more');
  assert.ok(tool.MEMORY_NEEDS['again']! > 0, 'a light gate that died of memory needs some');

  // `"memoryGb"` on a declared gate is its need, whatever its class: more, less, or none at all.
  const fx = makeFixture('merge-branch-memory-declared');
  fx.write('daoris.gates.json', JSON.stringify({ gates: [
    { name: 'deployment', run: 'npm run rehearse:deploy', memoryGb: 14 },
    { name: 'scan', run: 'node scan.mjs', memoryGb: 2 },
    { name: 'service', run: 'dotnet test src/S', memoryGb: 0 },
    { name: 'plain', run: 'dotnet test src/T' },
  ] }));
  const plan = tool.readPlan(fx.root);
  assert.deepEqual(Object.fromEntries(plan.map((gate) => [gate.name, tool.memoryNeed(gate)])), { scan: 2, service: 0, plain: suite, deployment: 14 });
  assert.equal('memoryGb' in plan.find((gate) => gate.name === 'plain')!, false);
  for (const wrong of ['lots', -1, null]) {
    fx.write('daoris.gates.json', JSON.stringify({ gates: [{ name: 'x', run: 'node x.mjs', memoryGb: wrong }] }));
    const refused = refusal(() => tool.readPlan(fx.root));
    assert.equal(refused.exitCode, 2);
    assert.match(refused.message, /gate 'x'.*memoryGb.*GB/);
  }
  fx.cleanup();
});


test('MEMGATE1: a stand-in machine short of memory delays a heavy gate and says why; it starts once the memory is free', async () => {
  const lanes = laneRun(['u', 'p']);
  const machine = standIn([3, 3, 12], { events: lanes.events });
  const printed: string[] = [];
  const gates = [planned('u', 'node u.mjs'), planned('p', processRun('fake/P.Tests'))];
  await tool.runGates('root', gates, 'logs', { run: lanes.run, print: (line) => printed.push(line), memory: machine });
  // Read before it, again every few seconds while short, and started only once the reading had its need.
  const at = (event: string) => lanes.events.indexOf(event);
  assert.ok(at('start p') > lanes.events.lastIndexOf('read 12'), lanes.events.join(', '));
  assert.ok(at('start p') > at('read 3'), lanes.events.join(', '));
  assert.deepEqual(machine.reads, [3, 3, 12], 'the light gate beside it was never read for');
  assert.deepEqual(machine.slept, [tool.MEMORY_POLL_MS, tool.MEMORY_POLL_MS]);
  const said = printed.join('\n');
  assert.match(said, /waiting for memory before p: 3\.0 GB free commit, the gate needs 8 GB; read again every 5 s, for up to 10 m/);
  assert.match(said, /memory for p: 12\.0 GB free commit and 32\.0 GB free physical memory after 10 s; it starts/);
  assert.deepEqual(summary(printed), ['PASS u', 'PASS p']);
});


test('MEMGATE1: past its bound a heavy gate runs anyway, saying so; a commit that cannot be read is said once, and physical memory judges alone', async () => {
  const deploy = planned('deployment', 'npm run rehearse:deploy');
  // Short the whole time: read every poll until the bound, then it runs, saying so.
  let machine = standIn([2]);
  let said: string[] = [];
  await tool.memoryGate({ ...machine, say: (line) => said.push(line) })(deploy);
  assert.equal(machine.slept.reduce((sum, ms) => sum + ms, 0), tool.MEMORY_WAIT_MS);
  assert.equal(machine.reads.length, tool.MEMORY_WAIT_MS / tool.MEMORY_POLL_MS + 1);
  assert.match(said.join('\n'), /waiting for memory before deployment: 2\.0 GB free commit, the gate needs 10 GB/);
  assert.match(said.at(-1)!, /memory for deployment: still 2\.0 GB free commit after 10 m, the gate needs 10 GB; it runs anyway, past the bound/);

  // A run that stops while a gate waits stops the wait.
  machine = standIn([2]);
  let reads = 0;
  await tool.memoryGate({ ...machine, say: () => {} })(deploy, { halted: () => ++reads > 3 });
  assert.ok(machine.slept.length <= 3, 'the wait ends with the run');

  // A machine smaller than the needs were set on waits for a quarter of its memory at most, never for what it has not got.
  machine = standIn([5], { total: 16 });
  said = [];
  await tool.memoryGate({ ...machine, say: (line) => said.push(line) })(deploy);
  assert.deepEqual(said, [], 'a quarter of 16 GB is free');

  // Where the commit cannot be read, it is said once a run, and the physical memory alone is waited for.
  let physical = [1, 1, 20];
  said = [];
  let clock = 0;
  const unread = tool.memoryGate({
    read: () => ({ physical: physical.shift()! * GiB, commit: null, total: 64 * GiB, why: 'this platform keeps none' }),
    sleep: async (ms) => { clock += ms; }, now: () => clock, say: (line) => said.push(line),
  });
  await unread(deploy);
  physical = [30];
  await unread(planned('rehearse', 'npm run rehearse'));
  assert.equal(said.filter((line) => /cannot be read/.test(line)).length, 1, said.join('\n'));
  assert.match(said[0]!, /memory: free commit cannot be read here \(this platform keeps none\); the heavy gates wait on free physical memory alone/);
  assert.match(said.join('\n'), /waiting for memory before deployment: 1\.0 GB free physical memory, the gate needs 10 GB/);
  // Nothing readable at all: said once, and no gate waits.
  said = [];
  const blind = tool.memoryGate({ read: () => { throw new Error('no query'); }, sleep: async () => { throw new Error('never sleeps'); }, say: (line) => said.push(line) });
  await blind(deploy);
  await blind(deploy);
  assert.deepEqual(said, ['  memory: it cannot be read here (the reading failed: no query); the heavy gates run without waiting for it']);
});


test('MEMGATE1: a light gate never waits: its memory is not even read, in two lanes or one at a time', async () => {
  for (const oneAtATime of [false, true]) {
    const lanes = laneRun(['a', 'b']);
    let asked = 0;
    const memory = { read: () => { asked += 1; return { physical: 0, commit: 0, total: 64 * GiB }; }, sleep: async () => {}, now: () => 0 };
    await tool.runGates('root', [planned('a', 'node a.mjs'), planned('b', 'npm run check:web-rules')], 'logs', { run: lanes.run, print: () => {}, memory, oneAtATime });
    assert.equal(asked, 0, `oneAtATime ${oneAtATime}`);
    assert.deepEqual(lanes.events.filter((event) => event.startsWith('start')).sort(), ['start a', 'start b']);
  }
  // The same in turn for a heavy one: it waits there too.
  const lanes = laneRun(['h']);
  const machine = standIn([1, 9]);
  await tool.runGates('root', [planned('h', 'npm run verify')], 'logs', { run: lanes.run, print: () => {}, memory: machine, oneAtATime: true });
  assert.deepEqual(machine.reads, [1, 9]);
});


test('MEMGATE1: each out-of-memory sign in a log is a death of memory, and the platform\'s exit for it; a quoted one, a failed check and another crash are not', () => {
  for (const sign of MEMORY_SIGNS) {
    const died = tool.memoryDeath(`$ npm run check:web-rules\nsome output\n${sign}\n    at somewhere\n\n(exited 1)\n`, 1);
    assert.ok(died, sign);
    assert.match(died.said, /memory|paging file/i, sign);
  }
  // Windows' statuses for an allocation it refused, unsigned as cmd and npm pass them on, and signed.
  for (const code of [0xC0000017, 0xC000012D, -1073741801, -1073741523]) {
    assert.match(tool.memoryDeath('', code)?.said ?? '', /exit 0xC000(0017|012D)/, `exit ${code}`);
  }
  // An assertion that shows the phrase as a value, a failed check, and a process that crashed some other way.
  assert.equal(tool.memoryDeath('  Error Message:\n   Assert.Equal() Failure: Collections differ\nExpected: ["out of memory"]\nActual:   ["full"]\n', 1), null);
  assert.equal(tool.memoryDeath(PROCESS_FAILURE, 1), null);
  assert.equal(tool.memoryDeath('', 0xC0000142), null);
  assert.equal(tool.memoryDeath('', 127), null);
});


test('MEMGATE1: a gate that died of memory is classed as memory and run again once, alone, after waiting: a pass is a FLAKE of memory', async () => {
  const fx = makeFixture('merge-branch-memory-again');
  for (const sign of MEMORY_SIGNS) {
    const died = planned('web-rules', 'npm run check:web-rules');
    const beside = planned('u', 'node u.mjs');
    const printed: string[] = [];
    const firstLine = signal();
    const asked: string[] = [];
    let runs = 0;
    // u ends only once the first run's line is printed, so a run again that came before u ended would show here.
    const step: Step = async (command, _cwd, fd) => {
      asked.push(command);
      if (command === beside.run) {
        await within(firstLine.promise, "the dead gate's first line");
        asked.push('u ended');
      }
      const dies = command === died.run && ++runs === 1;
      writeSync(fd, `$ ${command}\n${dies ? `${sign}\n` : 'ok\n'}`);
      return dies ? 1 : 0;
    };
    const machine = standIn([2, 9]);
    const results = await tool.runGates(fx.root, [died, beside], fx.root, {
      run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step }),
      print: (line) => {
        printed.push(line);
        if (/MEMORY\s+web-rules/.test(line)) firstLine.resolve();
      },
      memory: machine,
    });
    assert.deepEqual(asked, [died.run, beside.run, 'u ended', died.run], `${sign}: run again once, after every other gate ended`);
    const [again] = results;
    assert.equal(again!.verdict, 'FLAKE', sign);
    assert.equal(again!.cause, 'memory', sign);
    const said = printed.join('\n');
    assert.match(said, /MEMORY\s+web-rules\s.*\(exit 1; died of memory \(.+\): run again alone once every other gate has ended, after waiting for memory\)/, sign);
    // A light gate waits for memory only to run again, for what a light gate that died of memory needs.
    assert.deepEqual(machine.reads, [2, 9], sign);
    assert.match(said, new RegExp(`waiting for memory before web-rules: 2\\.0 GB free commit, the gate needs ${tool.MEMORY_NEEDS['again']} GB to run again`), sign);
    assert.match(said, /FLAKE\s+web-rules\s.*died of memory \(.+\), and passed run again alone; web-rules\.rerun\.log/, sign);
    assert.deepEqual(summary(printed), ['FLAKE web-rules', 'PASS u'], sign);
    assert.ok(readFileSync(join(fx.root, 'web-rules.log'), 'utf8').includes(sign), 'the first run keeps its own log');
  }
  fx.cleanup();
});


test('MEMGATE1: a second death of memory is a failure still named as memory; a second failure of another kind is a plain one', async () => {
  const fx = makeFixture('merge-branch-memory-twice');
  const died = planned('web-rules', 'npm run check:web-rules');
  const gateOn = (answer: (run: number) => { out: string; code: number }) => {
    let runs = 0;
    const fake = fakeStep(() => answer(++runs));
    const printed: string[] = [];
    return {
      fake, printed,
      results: tool.runGates(fx.root, [died], fx.root, {
        run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step: fake.step }), print: (line) => printed.push(line),
      }),
    };
  };
  let run = gateOn(() => ({ out: 'DataCloneError: Data cannot be cloned, out of memory.\n', code: 1 }));
  let [result] = await run.results;
  assert.equal(run.fake.asked.length, 2, 'run again once, not until it passes');
  assert.equal(result!.verdict, 'FAIL', 'a failure all the same: the gate did not pass');
  assert.equal(result!.cause, 'memory');
  assert.match(result!.note, /died of memory twice \(.*out of memory.*; then .*out of memory.*\), not of a check: run it again when the machine has memory to spare; web-rules\.rerun\.log/);
  assert.match(run.printed.at(-1)!, /^\s+MEMORY\s+web-rules\s/, 'one at a time and alone, its line is its verdict, said as memory');

  // The platform's exit for it counts the same, with nothing printed.
  run = gateOn(() => ({ out: '', code: 0xC000012D }));
  [result] = await run.results;
  assert.equal(result!.cause, 'memory');
  assert.match(result!.note, /died of memory twice \(exit 0xC000012D/);

  // Died of memory, then failed a check: a plain failure, and it says so.
  run = gateOn((n) => (n === 1 ? { out: 'Out of memory.\n', code: 1 } : { out: 'AssertionError: expected 1 to equal 2\n', code: 1 }));
  [result] = await run.results;
  assert.equal(result!.verdict, 'FAIL');
  assert.equal(result!.cause, undefined);
  assert.match(result!.note, /exit 1; died of memory \("Out of memory\."\), then failed run again alone, not of memory; web-rules\.rerun\.log/);
  assert.match(run.printed.at(-1)!, /^\s+FAIL\s+web-rules\s/);

  // How a summary says them: a flake of memory is the machine's, never recorded as a flake; a failure names it.
  const as = (name: string, verdict: string, cause?: string): GateResult => ({ gate: planned(name, 'x'), code: 0, log: '', verdict, note: '', ms: 0, ...(cause ? { cause } : {}) });
  assert.equal(tool.flakeNote([as('driver', 'FLAKE'), as('rehearse', 'FLAKE')]), ', with 2 flakes (driver, rehearse): record it under FLAKE1');
  assert.equal(tool.flakeNote([as('driver', 'FLAKE'), as('web-rules', 'FLAKE', 'memory')]),
    ', with 1 flake (driver): record it under FLAKE1; and 1 flake of memory (web-rules): it died of memory and passed run again alone, the machine\'s and not the change\'s');
  assert.equal(tool.flakeNote([]), '');
  assert.equal(tool.memoryLine([as('cli', 'FAIL')]), null);
  assert.equal(tool.memoryLine([as('cli', 'FAIL'), as('web-rules', 'FAIL', 'memory')]),
    '  Of memory, not of a check: web-rules died of memory; run it again when the machine has memory to spare.');
  fx.cleanup();
});


test('MEASUREDALONE1: a timed spec that failed beside other gates runs again alone, once the others have ended: a pass is a FLAKE', async () => {
  const passed = { out: '  1 passed (7.0s)\n', code: 0 };
  const { results, asked, printed, measured } = await measuredRun('merge-branch-measured-flake', measuredLog([TIMED_SPEC]), passed);
  assert.deepEqual(asked, [measured.run, 'node u.mjs', 'u ended', `${measured.run} -- -- measured/large.spec.ts:73`], 'asked once, after the other light gate ended');
  const [result] = results;
  assert.equal(result!.verdict, 'FLAKE');
  assert.deepEqual(result!.flakes, ['measured/large.spec.ts:73']);
  assert.match(result!.note, /timed spec.*failed beside other gates and passed alone: measured\/large\.spec\.ts:73; web-measured\.rerun\.log/);
  assert.deepEqual(summary(printed), ['FLAKE web-measured', 'PASS u']);
  assert.match(printed.join('\n'), /run again alone, every other gate having ended: web-measured/);
  assert.equal(tool.endsStageRun(result!), false);
});


test('MEASUREDALONE1: a timed spec that fails again alone is a FAIL naming it', async () => {
  const { results, asked, measured } = await measuredRun('merge-branch-measured-twice', measuredLog([TIMED_SPEC]), { out: `  1 failed\n    ${TIMED_SPEC} \n`, code: 1 });
  assert.equal(asked.filter((command) => command.startsWith(measured.run)).length, 2, 're-run once, not until it passes');
  const [result] = results;
  assert.equal(result!.verdict, 'FAIL');
  assert.equal(result!.flakes, undefined);
  assert.match(result!.note, /failed again alone: measured\/large\.spec\.ts:73; web-measured\.rerun\.log/);
});


test('MEASUREDALONE1: a mixed failure, or a count that does not match the names, is a FAIL and never re-run, and says why', async () => {
  for (const [log, why] of [
    [measuredLog([TIMED_SPEC, PLAIN_SPEC]), /not a timed spec: canvas\.spec\.ts:108/],
    [measuredLog([TIMED_SPEC], 2), /2 failed but 1 are named/],
  ] as const) {
    const { results, asked, measured } = await measuredRun('merge-branch-measured-mixed', log, { out: '  1 passed\n', code: 0 });
    assert.equal(asked.filter((command) => command.startsWith(measured.run)).length, 1);
    assert.equal(results[0]!.verdict, 'FAIL');
    assert.match(results[0]!.note, why);
    assert.match(results[0]!.note, /not re-run/);
  }
});


test('MEASUREDALONE1: a Playwright gate that is not a clock gate, and a rehearsal, keep their old failure', async () => {
  const log = measuredLog([TIMED_SPEC]);
  let run = await measuredRun('merge-branch-measured-noclock', log, { out: '  1 passed\n', code: 0 }, { clock: false });
  assert.equal(run.asked.filter((command) => command.startsWith('npm run test:web:measured')).length, 1);
  assert.equal(run.results[0]!.verdict, 'FAIL');
  assert.match(run.results[0]!.note, /reported failed checks, so it is never run again/);
  run = await measuredRun('merge-branch-measured-rehearse', log, { out: '  1 passed\n', code: 0 }, { run: 'npm run rehearse:family', hold: false });
  assert.equal(run.asked.filter((command) => command.startsWith('npm run rehearse:family')).length, 1);
  assert.equal(run.results[0]!.verdict, 'FAIL');
});


test('MEASUREDALONE1: one gate in turn runs the timed spec alone after the last gate', async () => {
  const fx = makeFixture('merge-branch-measured-turn');
  const measured = { ...planned('web-measured', 'npm run test:web:measured'), clock: true } as Gate;
  const after = planned('u', 'node u.mjs');
  let runs = 0;
  const fake = fakeStep((command) => (command === measured.run && ++runs === 1 ? { out: measuredLog([TIMED_SPEC]), code: 1 } : { out: '  1 passed\n', code: 0 }));
  const results = await tool.runGates(fx.root, [measured, after], fx.root, {
    oneAtATime: true, run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step: fake.step }), print: () => {},
  });
  assert.deepEqual(fake.asked, [measured.run, after.run, `${measured.run} -- -- measured/large.spec.ts:73`]);
  assert.deepEqual(results.map((each) => each.verdict), ['FLAKE', 'PASS']);
  fx.cleanup();
});


test('NODEALONE1: a node test run names its failed tests by file and title, and only a list that adds up to its count is re-run', () => {
  const log = nodeLog([['tool-test\\merge-queue.test.ts', NODE_TITLE_A], ['..\\..\\tools\\as-merged.test.mjs', NODE_TITLE_B]]);
  const decision = tool.nodeTestDecision(log);
  assert.equal(decision.node, true);
  assert.deepEqual(decision.rerun, [
    { file: 'tool-test/merge-queue.test.ts', tests: [NODE_TITLE_A] },
    { file: '../../tools/as-merged.test.mjs', tests: [NODE_TITLE_B] },
  ]);
  // A title is named once, and a file's tests stay together.
  const twice = tool.nodeTestDecision(nodeLog([['tool-test\\a.test.ts', 'one'], ['tool-test\\a.test.ts', 'two'], ['tool-test\\a.test.ts', 'one']], 3));
  assert.deepEqual(twice.rerun, [{ file: 'tool-test/a.test.ts', tests: ['one', 'two'] }]);

  const counted = tool.nodeTestDecision(nodeLog([['tool-test\\a.test.ts', 'one']], 2));
  assert.deepEqual(counted.rerun, []);
  assert.match(counted.reason!, /2 failed but 1 are named/);
  const none = tool.nodeTestDecision('$ npm run verify\nerror: the docs are stale\n(exited 1)\n');
  assert.equal(none.node, false);
  assert.deepEqual(none.rerun, []);
  assert.match(none.reason!, /no failed node test is named/);
});


test('NODEALONE1: a test is named for node by its own title, anchored, with every character that means something escaped', () => {
  const command = tool.nodeTestRerunCommand('tool-test/a.test.ts', NODE_TITLE_B);
  assert.equal(command, `node --test --test-name-pattern="^INTEGRATE1b: a gate that fails once beside a held load and passes quiet holding the whole admission is a sighting; under split, the default with no policy written, the candidate splits \\(a\\+b\\) \\[x\\]$" tool-test/a.test.ts`);
  // What a shell would read inside double quotes is matched by a dot, never passed through.
  assert.match(tool.nodeTestRerunCommand('f.ts', 'a "b" 100% $x'), /^node --test --test-name-pattern="\^a \.b\. 100\. \.x\$" f\.ts$/);
  // The pattern reaches that test alone: a nested test, by the title it prints, and no sibling.
  const fx = makeFixture('merge-branch-node-pattern');
  fx.write('n.test.mjs', "import { test, describe } from 'node:test';\ndescribe('outer', () => {\n  test('inner one; a+b', () => { console.log('RAN one'); });\n  test('inner two', () => { console.log('RAN two'); });\n});\ntest('top', () => { console.log('RAN top'); });\n");
  const { NODE_TEST_CONTEXT: _inherited, ...env } = process.env;
  const ran = spawnSync(tool.nodeTestRerunCommand('n.test.mjs', 'inner one; a+b'), { cwd: fx.root, shell: true, encoding: 'utf8', env, windowsHide: true });
  assert.equal(ran.status, 0, ran.stdout + ran.stderr);
  assert.match(ran.stdout, /RAN one/);
  assert.doesNotMatch(ran.stdout, /RAN two|RAN top/);
  assert.match(ran.stdout, /ℹ pass 1\b/);
  fx.cleanup();
});


test('NODEALONE1: a test that failed beside another gate runs alone in its package folder once the others have ended: a pass is a FLAKE', async () => {
  const firstLog = nodeLog([['tool-test\\merge-queue.test.ts', NODE_TITLE_A]]);
  const { results, asked, printed, root } = await nodeRun('merge-branch-node-flake', firstLog, { out: 'ℹ tests 1\nℹ pass 1\nℹ fail 0\n', code: 0 });
  assert.deepEqual(asked.map((each) => each.command), ['npm run tooltests', 'node u.mjs', tool.nodeTestRerunCommand('tool-test/merge-queue.test.ts', NODE_TITLE_A)], 'asked once, after the other gate ended');
  assert.equal(asked[2]!.cwd, join(root, 'pkg'), 'in the folder its script runs in');
  const [result] = results;
  assert.equal(result!.verdict, 'FLAKE');
  assert.deepEqual(result!.flakes, [NODE_TITLE_A]);
  assert.match(result!.note, /1 test failed beside other gates and passed alone: .*run's tree.*tool-tests\.rerun\.log/);
  assert.deepEqual(summary(printed), ['FLAKE tool-tests', 'PASS u']);
  assert.match(printed.join('\n'), /run again alone, every other gate having ended: tool-tests/);
  assert.equal(tool.endsStageRun(result!), false);
});


test('NODEALONE1: a test that fails again alone is a FAIL naming it, and a test that matched nothing is no pass', async () => {
  const firstLog = nodeLog([['tool-test\\merge-queue.test.ts', NODE_TITLE_A]]);
  for (const second of [{ out: 'ℹ pass 0\nℹ fail 1\n', code: 1 }, { out: 'ℹ tests 0\nℹ pass 0\nℹ fail 0\n', code: 0 }]) {
    const { results, asked } = await nodeRun('merge-branch-node-twice', firstLog, second);
    assert.equal(asked.filter((each) => each.command.startsWith('node --test')).length, 1, 're-run once, not until it passes');
    const [result] = results;
    assert.equal(result!.verdict, 'FAIL');
    assert.equal(result!.flakes, undefined);
    assert.match(result!.note, /failed again alone: INTEGRATE1j: a tree the caller hands.*tool-tests\.rerun\.log/);
  }
});


test('NODEALONE1: a failure that names no test, or whose count does not match its list, is a FAIL, never re-run, and says why', async () => {
  const unnamed = '$ npm run verify\n> daoris@0.0.1 typecheck\nerror TS2322: nothing\n(exited 1)\n';
  for (const [log, run, why] of [
    [unnamed, 'npm run verify', /^exit 1; no failed node test is named.*; not re-run$/],
    [nodeLog([['tool-test\\a.test.ts', 'one']], 2), 'npm run tooltests', /^exit 1; 2 failed but 1 are named; not re-run$/],
    [nodeLog([['tool-test\\a.test.ts', 'one']]), 'npm run other', /^exit 1$/],
  ] as const) {
    const { results, asked } = await nodeRun('merge-branch-node-unnamed', log, { out: 'ℹ pass 1\n', code: 0 }, { run });
    assert.equal(asked.filter((each) => each.command !== 'node u.mjs').length, 1, 'the gate ran once');
    assert.equal(results[0]!.verdict, 'FAIL');
    assert.match(results[0]!.note, why);
  }
});


test('NODEALONE1: which gates run node tests is read from the scripts they run, through npm and its workspace', () => {
  const fx = makeFixture('merge-branch-node-scripts');
  fx.write('package.json', JSON.stringify({ workspaces: ['pkg'], scripts: { tooltests: 'npm run tooltests -w cli', verify: 'npm run check -w cli && node --test tools/x.test.mjs && node tools/doc.mjs', other: 'node tools/other.mjs', loop: 'npm run loop' } }));
  fx.write('pkg/package.json', JSON.stringify({ name: 'cli', scripts: { tooltests: 'node --test "tool-test/**/*.ts"', check: 'tsc --noEmit' } }));
  const dirs = (run: string) => tool.nodeTestDirs(fx.root, planned('g', run));
  assert.deepEqual(dirs('npm run tooltests'), [join(fx.root, 'pkg')]);
  assert.deepEqual(dirs('npm run verify'), [fx.root]);
  assert.deepEqual(dirs('npm run other'), []);
  assert.deepEqual(dirs('npm run loop'), []);
  assert.deepEqual(dirs('dotnet test src/Daoris.Service'), []);
  assert.deepEqual(tool.nodeTestDirs(fx.root, { ...planned('g', 'npm run tooltests'), cwd: 'pkg' } as Gate), [join(fx.root, 'pkg')]);
  fx.cleanup();
});


test('NODEALONE1: one gate in turn runs the failed test alone after the last gate', async () => {
  const fx = makeFixture('merge-branch-node-turn');
  fx.write('package.json', JSON.stringify({ scripts: { verify: 'node --test t/x.test.mjs' } }));
  fx.write('t/x.test.mjs', '');
  const verify = { ...planned('cli', 'npm run verify'), clock: true } as Gate;
  const after = planned('u', 'node u.mjs');
  let runs = 0;
  const fake = fakeStep((command) => (command === verify.run && ++runs === 1 ? { out: nodeLog([['t\\x.test.mjs', 'one']]), code: 1 } : { out: 'ℹ pass 1\nℹ fail 0\n', code: 0 }));
  const results = await tool.runGates(fx.root, [verify, after], fx.root, {
    oneAtATime: true, run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step: fake.step }), print: () => {},
  });
  assert.deepEqual(fake.asked, [verify.run, after.run, tool.nodeTestRerunCommand('t/x.test.mjs', 'one')]);
  assert.deepEqual(results.map((each) => each.verdict), ['FLAKE', 'PASS']);
  fx.cleanup();
});


test('MEMGATE1: a rehearsal that died of memory is run again for memory, though its checks failed with it', async () => {
  const fx = makeFixture('merge-branch-memory-rehearsal');
  const family = planned('rehearse-family', 'npm run rehearse:family');
  // The 2026-10-22 landing step: two checks failed, each saying Out of memory.
  const landing = `${OK_LINES}  FAIL  the landing plugin lands\n          Out of memory.\n  FAIL  the landing is kept\n          Out of memory.\n\n  299/301 checks passed\n`;
  const fake = fakeStep(() => ({ out: landing, code: 1 }));
  const result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(fake.asked.length, 1, 'not run again in place, beside the gates still running');
  assert.equal(result.cause, 'memory');
  assert.equal(result.again, true);
  assert.equal(result.verdict, 'FAIL', 'a verdict recorded before it runs again is no pass');
  assert.match(result.note, /died of memory \("Out of memory\."\)/);
  fx.cleanup();
});


test("MEMGATE1: a Process half's tests that died of memory run alone once its memory is free; a whole half that died runs again whole", async () => {
  const fx = makeFixture('merge-branch-memory-half');
  const half = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  const flaky = 'Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked';
  const oom = PROCESS_FAILURE.replace('   Assert.True() Failure', '   System.OutOfMemoryException: Insufficient memory to continue the execution of the program.');
  const alone = 'Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)\n';
  const order: string[] = [];
  const memory: MemoryWait = async (gate, options) => { order.push(`memory ${gate.name}${options?.again ? ' again' : ''}`); };
  const settled = async () => { order.push('settled'); };

  let fake = fakeStep((command) => (command.includes('--filter') ? { out: alone, code: 0 } : { out: oom, code: 1 }));
  let result = await tool.runGate(fx.root, half, fx.root, { step: fake.step, settled, memory });
  assert.equal(result.verdict, 'FLAKE');
  assert.equal(result.cause, 'memory');
  assert.deepEqual(order, ['settled', 'memory driver-process again'], 'alone first, then its memory');
  assert.equal(fake.asked.at(-1), tool.rerunCommand(half.run, flaky));
  assert.match(result.note, /died of memory in the full run \(.*OutOfMemoryException.*\) and passed alone: ProcessJobTests\.A_child.*driver-process\.rerun\.log/);

  // Alone, it died of memory again: a failure, named as memory.
  fake = fakeStep((command) => (command.includes('--filter') ? { out: `${oom}\n`, code: 1 } : { out: oom, code: 1 }));
  result = await tool.runGate(fx.root, half, fx.root, { step: fake.step, settled, memory });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(result.cause, 'memory');
  assert.match(result.note, /died of memory again alone \(.*OutOfMemoryException.*\): ProcessJobTests\.A_child/);

  // The test host itself died of memory, naming no test: the half runs again whole, at the run's end.
  fake = fakeStep(() => ({ out: 'The active test run was aborted. Reason: Test host process crashed : Out of memory.\n', code: 1 }));
  result = await tool.runGate(fx.root, half, fx.root, { step: fake.step, settled, memory });
  assert.equal(result.again, true);
  assert.equal(result.cause, 'memory');
  assert.equal(fake.asked.length, 1);
  fx.cleanup();
});


test('MEMGATE1: a run a failure stopped runs no gate again for memory, and says why; --keep-going runs it again', async () => {
  const fx = makeFixture('merge-branch-memory-stopped');
  // Of two components, so they run side by side.
  const gates = [planned('a', 'dotnet build src/A/A'), planned('b', 'dotnet build src/B/B')];
  for (const keepGoing of [false, true]) {
    const fake = fakeStep((command) => (command === gates[0]!.run ? { out: 'Out of memory.\n', code: 1 } : { out: 'error CS0103\n', code: 1 }));
    const printed: string[] = [];
    const results = await tool.runGates(fx.root, gates, fx.root, {
      keepGoing, run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step: fake.step }), print: (line) => printed.push(line),
    });
    assert.equal(fake.asked.filter((command) => command === gates[0]!.run).length, keepGoing ? 2 : 1, `keepGoing ${keepGoing}`);
    assert.deepEqual(summary(printed), ['MEMORY a', 'FAIL b'], `keepGoing ${keepGoing}`);
    if (!keepGoing) assert.match(results[0]!.note, /not run again: b failed, and the run stopped there/);
  }
  fx.cleanup();
});


test("MEMGATE1: the machine's memory is read from the platform: Windows' free physical memory and free commit, Linux's commit only where it is held", () => {
  // Win32_OperatingSystem's fields are in KB, as the CIM query answers them.
  assert.deepEqual(tool.parseWindowsMemory('{"FreePhysicalMemory":22818728,"FreeVirtualMemory":15787368}'),
    { physical: 22818728 * 1024, commit: 15787368 * 1024 });
  assert.deepEqual(tool.parseWindowsMemory('﻿{"FreePhysicalMemory":null,"FreeVirtualMemory":1024}\r\n'), { physical: null, commit: 1024 * 1024 });
  const asked: string[] = [];
  const windows = tool.readMemory({
    platform: 'win32', freemem: () => 7, totalmem: () => 64 * GiB,
    run: (command, args) => { asked.push(`${command} ${args.join(' ')}`); return { status: 0, stdout: '{"FreePhysicalMemory":1048576,"FreeVirtualMemory":2097152}' }; },
  });
  assert.deepEqual(windows, { physical: GiB, commit: 2 * GiB, total: 64 * GiB });
  assert.match(asked[0]!, /^powershell\.exe .*Win32_OperatingSystem.*FreePhysicalMemory.*FreeVirtualMemory/);
  // A query that fails leaves Node's free memory, and says why the commit is not read.
  const failed = tool.readMemory({ platform: 'win32', freemem: () => 7, totalmem: () => 8, run: () => ({ status: 1, stdout: '', stderr: 'denied' }) });
  assert.equal(failed.physical, 7);
  assert.equal(failed.commit, null);
  assert.match(failed.why!, /the memory query failed: denied/);

  // Linux holds a process to a commit limit only under strict overcommit (mode 2); otherwise there is none to read.
  const meminfo = 'MemTotal:       16000000 kB\nMemAvailable:    8000000 kB\nCommitLimit:    12000000 kB\nCommitted_AS:    9000000 kB\n';
  assert.deepEqual(tool.parseMeminfo(meminfo), { commit: 3000000 * 1024 });
  const files = (mode: string) => (path: string) => (path.endsWith('overcommit_memory') ? `${mode}\n` : meminfo);
  assert.deepEqual(tool.readMemory({ platform: 'linux', freemem: () => 5, totalmem: () => 9, read: files('2') }), { physical: 5, commit: 3000000 * 1024, total: 9 });
  const loose = tool.readMemory({ platform: 'linux', freemem: () => 5, totalmem: () => 9, read: files('0') });
  assert.deepEqual([loose.physical, loose.commit], [5, null]);
  assert.match(loose.why!, /overcommit/);
  const mac = tool.readMemory({ platform: 'darwin', freemem: () => 5, totalmem: () => 9 });
  assert.deepEqual([mac.physical, mac.commit], [5, null]);
  assert.match(mac.why!, /darwin/);
});
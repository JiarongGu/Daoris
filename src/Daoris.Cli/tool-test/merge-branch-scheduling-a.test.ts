import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { repoRoot, tool, type Gate, type GateResult, type Beside, processRun } from './support/merge-branch.ts';
import { planned, fakeStep, laneRun, summary, laneGates } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


test('the first failing gate stops the run and names every gate after it; --keep-going runs them all', async () => {
  const root = join(repoRoot, 'scratch-root');
  const dir = join(root, 'local', 'scratch', 'merge-x');
  // Three builds of one component, so the light lane runs them in turn and the third is not yet started.
  const gates = ['a', 'b', 'c'].map((name) => planned(name, `dotnet build src/S/${name}`));
  const run = async (_root: string, gate: Gate, at: string): Promise<GateResult> => {
    const failed = gate.name === 'b';
    return { gate, code: failed ? 3 : 0, log: join(at, `${gate.name}.log`), verdict: failed ? 'FAIL' : 'PASS', note: failed ? 'exit 3' : '', ms: 1000 };
  };

  for (const oneAtATime of [false, true]) {
    const printed: string[] = [];
    const results = await tool.runGates(root, gates, dir, { run, oneAtATime, print: (line) => printed.push(line) });
    assert.deepEqual(results.map((result) => result.gate.name), ['a', 'b']);
    assert.ok(printed.some((line) => /PASS\s+a\s+1 s\s+local\/scratch\/merge-x\/a\.log$/.test(line)), printed.join('\n'));
    assert.ok(printed.some((line) => /FAIL\s+b\s.*local\/scratch\/merge-x\/b\.log\s+\(exit 3\)/.test(line)), printed.join('\n'));
    assert.match(printed.at(-1)!, /NOT RUN\s+c/);
  }

  const all = await tool.runGates(root, gates, dir, { run, keepGoing: true, print: () => {} });
  assert.deepEqual(all.map((result) => result.gate.name), ['a', 'b', 'c']);
});


test("the light lane runs beside the Process lane: both at once, each verdict its own, the summary in the plan's order", async () => {
  const lanes = laneRun(['a', 'b', 'p', 'q']);
  // a ends only once p has started, and p only once b has: run in turn, neither would.
  lanes.until['a'] = () => lanes.started['p']!.promise;
  lanes.until['p'] = () => lanes.started['b']!.promise;
  const printed: string[] = [];
  const results = await tool.runGates('root', laneGates(), join('root', 'local', 'scratch', 'merge-x'), { run: lanes.run, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => result.gate.name), ['a', 'b', 'p', 'q'], "the results in the plan's order");
  assert.ok(lanes.events.indexOf('start p') < lanes.events.indexOf('end a'), `p started while a ran: ${lanes.events.join(', ')}`);
  assert.ok(lanes.events.indexOf('start b') < lanes.events.indexOf('end p'), `b started while p ran: ${lanes.events.join(', ')}`);
  assert.ok(lanes.events.indexOf('start q') > lanes.events.indexOf('end p'), 'the Process lane runs one gate at a time');
  assert.match(printed.join('\n'), /light\s+a, b\n.*Process\s+p, q/);
  assert.match(printed.join('\n'), /--one-at-a-time/);
  assert.deepEqual(summary(printed), ['PASS a', 'PASS b', 'PASS p', 'PASS q']);
  assert.equal(lanes.heard['a']!.settled, undefined, 'a light gate waits for nothing beside it');
  assert.equal(typeof lanes.heard['p']!.settled, 'function', "a Process gate is handed the light lane's end, for its re-run alone");
});


test('scheduled by memory, a Process gate starts beside a heavy light gate once the gate that builds its component has ended (CI1)', async () => {
  // a builds p's component; h is a heavy light gate of none, which a merge's Process gate waits for.
  const gates = [planned('a', 'dotnet build fake/P.Tests'), planned('h', 'npm run verify'), planned('p', processRun('fake/P.Tests'))];
  const lanes = laneRun(['a', 'h', 'p']);
  lanes.until['h'] = () => lanes.started['p']!.promise;
  const printed: string[] = [];
  await tool.runGates('root', gates, 'logs', { run: lanes.run, print: (line) => printed.push(line), byMemory: true });
  assert.ok(lanes.events.indexOf('start p') > lanes.events.indexOf('end a'), `p waits for the gate that builds its component: ${lanes.events.join(', ')}`);
  assert.ok(lanes.events.indexOf('start p') < lanes.events.indexOf('end h'), `p runs beside the heavy light gate: ${lanes.events.join(', ')}`);
  assert.match(printed.join('\n'), /p starts once a has ended: a builds fake\/P\.Tests first/);
  assert.match(printed.join('\n'), /the heavy light gates run beside the Process lane, each once its free memory allows \(CI1, MEMGATE1\)/);
});


test('a failure in either lane starts nothing more in either; the gate running beside it ends and keeps its verdict', async () => {
  // The light lane fails while p runs: p ends and keeps its pass, and neither b nor q starts.
  let lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('a');
  const light = lanes;
  lanes.until['a'] = () => light.started['p']!.promise;
  lanes.until['p'] = () => light.ended['a']!.promise;
  let printed: string[] = [];
  let results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => `${result.verdict} ${result.gate.name}`), ['FAIL a', 'PASS p']);
  assert.deepEqual(summary(printed), ['FAIL a', 'NOT RUN b', 'PASS p', 'NOT RUN q']);

  // The Process lane fails while a runs: a ends and keeps its pass, and neither b nor q starts.
  lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('p');
  const first = lanes;
  lanes.until['a'] = () => first.ended['p']!.promise;
  printed = [];
  results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => `${result.verdict} ${result.gate.name}`), ['PASS a', 'FAIL p']);
  assert.deepEqual(summary(printed), ['PASS a', 'NOT RUN b', 'FAIL p', 'NOT RUN q']);

  // --keep-going: a failure in each lane, and every gate still runs.
  lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('a');
  lanes.failing.add('p');
  printed = [];
  results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, keepGoing: true, print: (line) => printed.push(line) });
  assert.deepEqual(summary(printed), ['FAIL a', 'PASS b', 'FAIL p', 'PASS q']);
  assert.equal(results.length, 4);
});


test("a Process gate starts once the light gates of its component have ended, which run first; a rehearsal once the light lane has", async () => {
  const gates = [
    planned('u', 'node u.mjs'),
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'),
    planned('m', 'dotnet test src/X/M.Tests --filter Category!=Process'),
    planned('h', 'npm run test:web'),
    planned('dp', processRun('src/X/X.Tests')),
    planned('r', 'npm run rehearse:family'),
  ];
  const schedule = tool.laneSchedule(gates);
  assert.deepEqual(schedule.light.map((gate) => gate.name), ['d', 'm', 'u', 'h']);
  // The heavy light gate h is waited for too; the cheap one, u, is not.
  assert.deepEqual(schedule.waits['dp'], { after: ['d', 'm', 'h'], component: 'src/X' });
  assert.deepEqual(schedule.waits['r'], { after: ['d', 'm', 'u', 'h'], component: null });

  const lanes = laneRun(['u', 'd', 'm', 'h', 'dp', 'r']);
  // dp waits, as its re-run alone would, for the light lane's end that it is handed.
  lanes.until['dp'] = async () => {
    await lanes.heard['dp']!.settled!();
    lanes.events.push('settled');
  };
  const printed: string[] = [];
  await tool.runGates('root', gates, 'logs', { run: lanes.run, print: (line) => printed.push(line) });
  const at = (event: string) => lanes.events.indexOf(event);
  assert.equal(lanes.events[0], 'start d', 'the light gates the Process lane waits for go first');
  assert.ok(at('start dp') > at('end m'), `dp starts after the light gates of src/X: ${lanes.events.join(', ')}`);
  assert.ok(at('start dp') > at('end h'), `dp starts after the heavy light gate: ${lanes.events.join(', ')}`);
  assert.ok(at('settled') > at('end u'), 'the light lane ended before what dp was handed resolved');
  assert.ok(at('start r') > at('end u'), 'the rehearsal starts once the light lane has ended');
  assert.match(printed.join('\n'), /dp starts once d, m, h have ended: d, m build src\/X first, h loads the machine/);
  assert.match(printed.join('\n'), /r starts once the light lane has ended/);
  assert.deepEqual(summary(printed), ['PASS u', 'PASS d', 'PASS m', 'PASS h', 'PASS dp', 'PASS r']);
});


test('--one-at-a-time runs every gate in turn, in the plan\'s order, as before the lanes', async () => {
  const lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('b');
  const printed: string[] = [];
  const results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, oneAtATime: true, print: (line) => printed.push(line) });
  assert.deepEqual(lanes.events, ['start a', 'end a', 'start b', 'end b']);
  assert.deepEqual(results.map((result) => result.gate.name), ['a', 'b']);
  assert.match(printed[2]!, /NOT RUN\s+p/);
  assert.match(printed[3]!, /NOT RUN\s+q/);
  assert.equal(printed.length, 4, 'no lanes and no summary: the lines are already in order');
  assert.equal(lanes.heard['a']!.settled, undefined);
});


// ---------------------------------------------------------------------------------------------------
// The light lane's gates of different components side by side; a Process half reuses its fast half's build

test("light gates of different components run at once, up to the width, two of one component never; the summary keeps the plan's order", async () => {
  const gates = [
    planned('a1', 'dotnet test src/A/A1.Tests'), planned('a2', 'dotnet test src/A/A2.Tests'),
    planned('b', 'dotnet test src/B/B.Tests'), planned('c', 'node c.mjs'), planned('d', 'node d.mjs'),
  ];
  const counted = (lanes: ReturnType<typeof laneRun>) => {
    const seen = { now: 0, most: 0 };
    const run = async (root: string, gate: Gate, at: string, beside?: Beside) => {
      seen.now += 1;
      seen.most = Math.max(seen.most, seen.now);
      try {
        return await lanes.run(root, gate, at, beside);
      } finally {
        seen.now -= 1;
      }
    };
    return { seen, run };
  };
  const lanes = laneRun(['a1', 'a2', 'b', 'c', 'd']);
  // a1 ends only once b has ended with c beside it, b once c has started: one gate at a time, none of them would.
  lanes.until['a1'] = () => Promise.all([lanes.ended['b']!.promise, lanes.started['c']!.promise]);
  lanes.until['b'] = () => lanes.started['c']!.promise;
  lanes.until['c'] = () => lanes.ended['a1']!.promise;
  const printed: string[] = [];
  const { seen, run } = counted(lanes);
  const results = await tool.runGates('root', gates, 'logs', { run, scripts: {}, print: (line) => printed.push(line) });
  const at = (event: string) => lanes.events.indexOf(event);
  assert.equal(seen.most, 3, `three at once, and never more: ${lanes.events.join(', ')}`);
  assert.ok(at('start a2') > at('end a1'), `a1 and a2 build src/A, so never at once: ${lanes.events.join(', ')}`);
  assert.ok(at('start d') > at('end b'), `d waited for a place: ${lanes.events.join(', ')}`);
  assert.match(printed[0]!, /the light lane, up to 3 gates at once \(--one-at-a-time runs every gate in turn/);
  assert.match(printed.join('\n'), /light\s+a1, a2, b, c, d\n/);
  assert.match(printed.join('\n'), /a1, a2 build src\/A: one at a time/);
  assert.match(printed.find((line) => /^\s+(PASS|FAIL)/.test(line))!, /PASS\s+b\s/, 'each line is printed as its gate ends');
  assert.deepEqual(summary(printed), ['PASS a1', 'PASS a2', 'PASS b', 'PASS c', 'PASS d']);
  assert.deepEqual(results.map((result) => result.gate.name), ['a1', 'a2', 'b', 'c', 'd'], "the results in the plan's order");

  // A narrower lane holds to its width.
  const narrow = laneRun(['a1', 'a2', 'b', 'c', 'd']);
  for (const name of ['a1', 'a2', 'b', 'c', 'd']) narrow.until[name] = () => new Promise((done) => setTimeout(done, 5));
  const two = counted(narrow);
  await tool.runGates('root', gates, 'logs', { run: two.run, width: 2, scripts: {}, print: () => {} });
  assert.equal(two.seen.most, 2, narrow.events.join(', '));
});


test('a failure starts no more light gates; the gates running beside it end and keep their verdicts', async () => {
  const gates = [planned('x1', 'dotnet test src/X/X1.Tests'), planned('y', 'dotnet test src/Y/Y.Tests'), planned('x2', 'dotnet test src/X/X2.Tests')];
  const lanes = laneRun(['x1', 'y', 'x2']);
  lanes.failing.add('x1');
  lanes.until['x1'] = () => lanes.started['y']!.promise;
  lanes.until['y'] = () => lanes.ended['x1']!.promise;
  const printed: string[] = [];
  const results = await tool.runGates('root', gates, 'logs', { run: lanes.run, scripts: {}, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => `${result.verdict} ${result.gate.name}`), ['FAIL x1', 'PASS y']);
  assert.deepEqual(summary(printed), ['FAIL x1', 'PASS y', 'NOT RUN x2']);
  assert.equal(lanes.events.includes('start x2'), false);
});


test("a Process half is handed its fast half's pass in this run and the tree it ran on; nothing when that failed or did not run", async () => {
  const gates = [
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'), planned('m', 'dotnet test src/X/M.Tests --filter Category!=Process'),
    planned('dp', processRun('src/X/X.Tests')), planned('dg', 'dotnet test src/X/X.Tests --settings src/X/git.process.runsettings'),
    planned('mp', processRun('src/X/M.Tests')),
  ];
  assert.deepEqual(tool.laneSchedule(gates).reuses, { dp: 'd', dg: 'd', mp: 'm' });
  const names = gates.map((gate) => gate.name);
  const treed = (lanes: ReturnType<typeof laneRun>) => async (root: string, gate: Gate, at: string, beside?: Beside) => ({ ...(await lanes.run(root, gate, at, beside)), tree: 't1' });

  for (const oneAtATime of [false, true]) {
    const lanes = laneRun(names);
    await tool.runGates('root', gates, 'logs', { run: treed(lanes), oneAtATime, scripts: {}, print: () => {} });
    assert.deepEqual(lanes.heard['dp']!.built, { gate: 'd', tree: 't1' }, `one at a time: ${oneAtATime}`);
    assert.deepEqual(lanes.heard['dg']!.built, { gate: 'd', tree: 't1' });
    assert.deepEqual(lanes.heard['mp']!.built, { gate: 'm', tree: 't1' });
    assert.equal(lanes.heard['d']!.built, undefined, 'a fast half builds');
  }

  // A fast half that failed: its Process halves build (--keep-going ran them), the other component's reuse.
  let lanes = laneRun(names);
  lanes.failing.add('d');
  await tool.runGates('root', gates, 'logs', { run: treed(lanes), keepGoing: true, scripts: {}, print: () => {} });
  assert.equal(lanes.heard['dp']!.built, undefined);
  assert.equal(lanes.heard['dg']!.built, undefined);
  assert.deepEqual(lanes.heard['mp']!.built, { gate: 'm', tree: 't1' });

  // --rerun or --stale naming the Process halves alone: no fast half ran in this run, so they build.
  lanes = laneRun(['dp', 'mp']);
  await tool.runGates('root', gates.filter((gate) => ['dp', 'mp'].includes(gate.name)), 'logs', { run: treed(lanes), scripts: {}, print: () => {} });
  assert.equal(lanes.heard['dp']!.built, undefined);
  assert.equal(lanes.heard['mp']!.built, undefined);
});


test("a Process half told not to build runs its command with --no-build and says whose build it ran; its re-run alone is as before", async () => {
  const fx = makeFixture('merge-branch-no-build');
  const { asked, step } = fakeStep(() => ({ out: 'Passed!\n', code: 0 }));
  const dp = planned('dp', processRun('src/X/X.Tests'));
  const result = await tool.runGate(fx.root, dp, fx.root, { step, noBuild: true, built: { gate: 'd', tree: 't1' } });
  assert.equal(asked[0], tool.trxCommand(tool.noBuildCommand(dp.run), join(fx.root, 'dp.trx')));
  assert.equal(result.verdict, 'PASS');
  assert.match(result.note, /no build: d built this tree in this run/);
  assert.match(readFileSync(join(fx.root, 'dp.log'), 'utf8'), /^\$ dotnet test src\/X\/X\.Tests --settings \S+ --no-build --logger/);

  const built = await tool.runGate(fx.root, dp, fx.root, { step });
  assert.equal(asked[1], tool.trxCommand(dp.run, join(fx.root, 'dp.trx')));
  assert.equal(built.note, '');
  // Only a dotnet test command is told so.
  await tool.runGate(fx.root, planned('r', 'npm run rehearse'), fx.root, { step, noBuild: true, built: { gate: 'd', tree: 't1' } });
  assert.equal(asked[2], 'npm run rehearse');
  fx.cleanup();
});
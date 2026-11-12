import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { repoRoot, tool, rehearsalKit, trackedFiles, processRun, rootScripts, repoPlan, gitRun } from './support/merge-branch.ts';
import { PROCESS_FAILURE, planned, fakeStep, laneRun, summary, rehearsalProjects, NPM_BANNER, OK_LINES } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


/**
 * A rehearsal that runs beside the git half builds a project of a component that half builds only into its
 * own output (`ownBuild`), never into that project's folders, which the git half builds into and runs from; and it shuts
 * no build server down. Read from the tool its root script runs, as REACH's rows for the rehearsals are. Or it runs
 * no `dotnet` at all, as the release rehearsal does. And every publish of the desktop it runs is told an output of
 * its own (`tools/desktop-publish.mjs`'s `--artifacts-path`, whose test holds every `dotnet publish` that tool runs to the
 * output it is told), and no such gate is planned a step before it, as a build-server shutdown would be.
 */
test('a rehearsal that runs beside the git half builds what that half builds into its own output, or no .NET at all, and shuts no build server down', () => {
  const scripts = rootScripts();
  const processBuilds = new Set(repoPlan.filter((gate) => tool.isGitHalf(gate)).map((gate) => tool.dotnetComponent(gate.run)));
  assert.deepEqual([...processBuilds], ['src/Daoris.Desktop']);
  const building: string[] = [];
  const publishing: string[] = [];
  for (const script of tool.BESIDE_GIT_HALF) {
    const file = /^node (tools\/\S+\.mjs)$/.exec(scripts[script] ?? '')?.[1];
    assert.ok(file, `${script} runs one tool`);
    const text = readFileSync(join(repoRoot, file), 'utf8');
    assert.doesNotMatch(text, /build-server/, `${file} shuts a build server down, which a build beside it would lose`);
    const roots = new Set([...text.matchAll(/const (\w+) = ownBuildRoot\(repoRoot, '[^']+'\);/g)].map((match) => match[1]!));
    const publishes = [...text.matchAll(/desktop-publish\.mjs'\)\}"([^`]*)`/g)].map((match) => match[1]!);
    if (publishes.length) publishing.push(script);
    for (const publish of publishes) {
      const told = /--artifacts-path "\$\{(\w+)\}"/.exec(publish)?.[1];
      assert.ok(told && roots.has(told), `${file} publishes the desktop into the projects' own folders: ${publish.trim()}`);
    }
    if (!/\bdotnet\s+(?:build|publish|test|run)\b|\bownBuild\(/.test(text)) continue;
    building.push(script);
    const projects = rehearsalProjects(text);
    const own = [...text.matchAll(/\bownBuild\((\w+),/g)].map((match) => match[1]!);
    assert.ok(own.length >= 1, `${file} builds nothing into its own output: this test has stopped reading it`);
    for (const name of own) assert.ok(projects[name], `${file} builds \${${name}} into its own output, a project this test cannot place`);
    for (const [, name] of text.matchAll(/dotnet (?:build|publish|test|run)\s+"\$\{(\w+)\}"/g)) {
      const project = projects[name!];
      assert.ok(project, `${file} builds \${${name}}, a project this test cannot place`);
      assert.ok(!processBuilds.has(tool.dotnetComponent(`dotnet build ${project}`)),
        `${file} builds ${project} into its own folders, which the git half builds into and runs from`);
    }
  }
  // The family's and the deployment's tools are read for their builds: a rehearsal that names no `dotnet` is not, so those
  // that do are named here, and the one that publishes the desktop.
  assert.deepEqual(building, ['rehearse:family', 'rehearse:deploy']);
  assert.deepEqual(publishing, ['rehearse:deploy']);
  // In place of that shutdown, every build the deployment rehearsal makes keeps build servers of its own, as a run beside's
  // do: it joins none a build before it left, one of which can carry a broken environment into a publish.
  const deploy = readFileSync(join(repoRoot, 'tools', 'deployment-rehearsal.mjs'), 'utf8');
  assert.match(deploy, /\n {2}Object\.assign\(process\.env, OWN_BUILD_SERVERS\);/, 'the deployment rehearsal builds on servers it may share');
  assert.deepEqual({ ...rehearsalKit.OWN_BUILD_SERVERS }, { ...tool.BESIDE_ENVIRONMENT });
});


/**
 * The release rehearsal (the tool's header says why) runs beside the git half only while no desktop test reads the
 * CLI package's folder outside its `src/` and `test/`, which no pack writes, nor runs its bin. Read from every tracked
 * desktop source but its raw strings, and from the stager's own list.
 */
test('the release rehearsal stages nothing a desktop test reads: they read the CLI package only under src/ and test/, which no pack writes', () => {
  const stager = readFileSync(join(repoRoot, 'tools', 'stage-package.mjs'), 'utf8');
  const staged = [...(/const STAGED = \[([^\]]+)\]/.exec(stager)?.[1] ?? '').matchAll(/'([^']+)'/g)].map((match) => match[1]!);
  assert.deepEqual(staged, ['canon', 'LICENSE', 'README.md', 'CHANGELOG.md'], 'the stager has stopped saying what it stages: read it again');
  assert.match(stager, /\[\.\.\.STAGED, 'dist'\]/, 'the stager no longer cleans dist/ with them: read it again');
  // `bin` (read beside no pack, above) and `tool-test` are tracked: no pack stages or removes them.
  const unwritten = new Set(['src', 'test', 'bin', 'tool-test']);
  for (const folder of [...staged, 'dist']) assert.ok(!unwritten.has(folder), `a pack writes the CLI's ${folder}/`);
  const sources = trackedFiles().filter((path) => path.startsWith('src/Daoris.Desktop/') && path.endsWith('.cs'));
  assert.ok(sources.length >= 100, 'the desktop sources left src/Daoris.Desktop');
  let read = 0;
  for (const path of sources) {
    const text = readFileSync(join(repoRoot, path), 'utf8').replace(/"""[^]*?"""/g, '');
    // The install's own CLI is no package of this checkout; the one test running this checkout's bin is a modules Process test, never beside a pack.
    assert.ok(path.endsWith('Bridge/ReconcileStatusProcessTests.cs') || !/Daoris\.Cli"[^;]*daoris\.mjs/.test(text), `${path} names the CLI's bin, which prefers a staged dist/`);
    for (const match of text.matchAll(/Daoris\.Cli"\s*,\s*"([^"]+)"|Daoris\.Cli[\\/]+([^\\/\s"`'<]+)/g)) {
      const folder = match[1] ?? match[2]!;
      read += 1;
      assert.ok(unwritten.has(folder), `${path} reads the CLI package's ${folder}, which a pack beside it may stage or remove`);
    }
  }
  assert.ok(read >= 10, 'no desktop source reads the CLI package any more: this test has stopped reading them');
});


test('the release, family and deployment rehearsals run beside the git half, one after the other, never beside another Process half', async () => {
  const gates = [
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'),
    planned('dp', processRun('src/X/X.Tests')), planned('dg', gitRun('src/X/X.Tests')), planned('mp', processRun('src/X/M.Tests')),
    planned('r', 'npm run rehearse'), planned('f', 'npm run rehearse:family'), planned('dep', 'npm run rehearse:deploy'),
  ];
  const lanes = laneRun(gates.map((gate) => gate.name));
  // dg ends only once dep has started beside it, and r, f and dep only once dg has: one at a time, none would.
  lanes.until['dg'] = () => lanes.started['dep']!.promise;
  for (const name of ['r', 'f', 'dep']) lanes.until[name] = () => lanes.started['dg']!.promise;
  const printed: string[] = [];
  const results = await tool.runGates('root', gates, 'logs', { run: lanes.run, scripts: {}, print: (line) => printed.push(line) });
  const at = (event: string) => lanes.events.indexOf(event);
  const order = lanes.events.join(', ');
  assert.ok(['r', 'f', 'dep'].every((name) => at(`start ${name}`) > at('end dp')), `none runs beside the driver's other half: ${order}`);
  assert.ok(at('start f') > at('end r'), `the family never runs beside the release rehearsal, whose staged dist/ its CLI would run: ${order}`);
  // The deployment rehearsal packs the CLI's folder as the release rehearsal does, and runs its bin as the family does.
  assert.ok(at('start dep') > at('end r') && at('start dep') > at('end f'), `the deployment rehearsal runs beside neither: ${order}`);
  assert.ok(at('start dep') < at('end dg'), `the deployment rehearsal runs beside the git half: ${order}`);
  assert.ok(['dg', 'r', 'f', 'dep'].every((name) => at('start mp') > at(`end ${name}`)), `the next Process half waits for all four: ${order}`);
  // Two Process-lane gates at once at most, the measured ceiling.
  let running = 0;
  let most = 0;
  for (const event of lanes.events) {
    if (event === 'start d' || event === 'end d') continue;
    running += event.startsWith('start ') ? 1 : -1;
    most = Math.max(most, running);
  }
  assert.equal(most, 2, order);
  assert.match(printed.join('\n'), /Process\s+dp, dg, r, f, dep, mp\b/);
  assert.match(printed.join('\n'), /r, f, dep run beside dg, one after the other: none builds into a folder a Process half runs from, or shuts a build server down/);
  assert.deepEqual(summary(printed), ['PASS d', 'PASS dp', 'PASS dg', 'PASS mp', 'PASS r', 'PASS f', 'PASS dep'], "the summary in the plan's order");
  assert.deepEqual(results.map((result) => result.gate.name), gates.map((gate) => gate.name));

  // --one-at-a-time runs every gate in turn, in the plan's order, as before.
  const turn = laneRun(gates.map((gate) => gate.name));
  await tool.runGates('root', gates, 'logs', { run: turn.run, oneAtATime: true, scripts: {}, print: () => {} });
  assert.deepEqual(turn.events.filter((event) => event.startsWith('start')), gates.map((gate) => `start ${gate.name}`));
});


test("the git half's failures are re-run alone: after the rehearsal beside it has ended, and none starts meanwhile", async () => {
  const gates = [planned('u', 'node u.mjs'), planned('dg', gitRun('src/X/X.Tests')), planned('f', 'npm run rehearse:family')];
  const names = gates.map((gate) => gate.name);

  // f runs when dg wants to be alone: what dg is handed resolves once f has ended.
  const running = laneRun(names);
  running.until['f'] = () => running.started['dg']!.promise;
  running.until['dg'] = async () => {
    await running.started['f']!.promise;
    await running.heard['dg']!.settled!();
    running.events.push('alone');
  };
  await tool.runGates('root', gates, 'logs', { run: running.run, scripts: {}, print: () => {} });
  assert.ok(running.events.indexOf('alone') > running.events.indexOf('end f'), running.events.join(', '));

  // f has not started when dg wants to be alone: it starts once dg has ended.
  const waiting = laneRun(names);
  waiting.until['u'] = () => waiting.started['dg']!.promise;
  waiting.until['dg'] = async () => {
    await waiting.heard['dg']!.settled!();
    waiting.events.push('alone');
  };
  await tool.runGates('root', gates, 'logs', { run: waiting.run, scripts: {}, print: () => {} });
  assert.ok(waiting.events.indexOf('start f') > waiting.events.indexOf('end dg'), waiting.events.join(', '));
  assert.ok(waiting.events.indexOf('alone') > waiting.events.indexOf('end u'), 'the light lane ended first, as before');

  // The release rehearsal runs beside dg when dg wants to be alone, and the family waits for it: dg's re-run waits
  // for the release rehearsal to end, and the family, next beside dg, starts only once dg has ended.
  const chain = [...gates.slice(0, 2), planned('r', 'npm run rehearse'), gates[2]!];
  const chained = laneRun(chain.map((gate) => gate.name));
  chained.until['r'] = () => chained.started['dg']!.promise;
  chained.until['dg'] = async () => {
    await chained.started['r']!.promise;
    await chained.heard['dg']!.settled!();
    chained.events.push('alone');
  };
  await tool.runGates('root', chain, 'logs', { run: chained.run, scripts: {}, print: () => {} });
  const at = (event: string) => chained.events.indexOf(event);
  assert.ok(at('alone') > at('end r'), chained.events.join(', '));
  assert.ok(at('start f') > at('end dg'), chained.events.join(', '));
});


test("the web gate's unit run starts beside service, and the web gate's build after it, as before", async () => {
  const scripts = { 'test:web:e2e': 'npm --prefix w run build && dotnet build src/S/S.Http && npm --prefix w run e2e', 'test:web:unit': 'npm --prefix w run test' };
  const gates = [planned('service', 'dotnet test src/S'), planned('k', 'dotnet test src/K'), planned('web', 'npm run test:web:e2e'), planned('web-unit', 'npm run test:web:unit')];
  const lanes = laneRun(gates.map((gate) => gate.name));
  lanes.until['service'] = () => lanes.started['web-unit']!.promise;
  await tool.runGates('root', gates, 'logs', { run: lanes.run, scripts, print: () => {} });
  const at = (event: string) => lanes.events.indexOf(event);
  assert.ok(at('start web-unit') < at('end service'), lanes.events.join(', '));
  assert.ok(at('start web') > at('end service'), lanes.events.join(', '));
});


test("a gate's whole output is kept in its log", async () => {
  const fx = makeFixture('merge-branch-gate-log');
  const long = Array.from({ length: 5000 }, (_, i) => `line ${i + 1}`).join('\n');
  const { asked, step } = fakeStep(() => ({ out: `${long}\n`, code: 0 }));

  const result = await tool.runGate(fx.root, planned('deployment', 'npm run rehearse:deploy'), fx.root, { step });
  assert.equal(result.verdict, 'PASS');
  assert.deepEqual(asked, ['npm run rehearse:deploy']);
  const log = readFileSync(join(fx.root, 'deployment.log'), 'utf8');
  assert.match(log, /\nline 1\n/);
  assert.match(log, /\nline 5000\n/);
  assert.equal(existsSync(join(fx.root, 'deployment.log.partial')), false, 'the log is renamed into place when its gate ends');
  fx.cleanup();
});


test('a real-process failure is re-run alone once: a pass alone is a FLAKE, a second failure a FAIL, and nothing else is re-run', async () => {
  const fx = makeFixture('merge-branch-flake-runs');
  const driver = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  const flaky = 'Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked';
  const alone = 'Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)\n';

  let fake = fakeStep((command) => (command.includes('--filter') ? { out: alone, code: 0 } : { out: PROCESS_FAILURE, code: 1 }));
  // The re-run alone waits for the light lane beside it to end, so a pass alone is still a pass with nothing beside.
  const first = fake;
  let result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step, settled: async () => { first.asked.push('the light lane ended'); } });
  assert.equal(result.verdict, 'FLAKE');
  // The full run leaves its trx; the re-run of one test adds none.
  assert.deepEqual(fake.asked, [tool.trxCommand(driver.run, join(fx.root, 'driver-process.trx')), 'the light lane ended', tool.rerunCommand(driver.run, flaky)]);
  assert.match(result.note, /failed in the full run and passed alone: ProcessJobTests\.A_child.*driver-process\.rerun\.log/);
  assert.match(readFileSync(join(fx.root, 'driver-process.rerun.log'), 'utf8'), /Passed!/);

  fake = fakeStep(() => ({ out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.match(result.note, /failed again alone/);

  // Two failed in the full run, one of them passing alone: the note names only the one that failed again, and says the
  // other passed alone, so a reader chases the right test (a parent once briefed a worker on three that had passed).
  const second = 'Daoris.Desktop.Driver.Tests.StopHoldTickTests.A_take_the_person_stopped_is_held';
  const twoFailed = PROCESS_FAILURE.replace('Failed!  - Failed:     1', `  Failed ${second} [9 s]\r\nFailed!  - Failed:     2`);
  fake = fakeStep((command) => (!command.includes('--filter') ? { out: twoFailed, code: 1 }
    : command.includes('StopHoldTickTests') ? { out: alone, code: 0 } : { out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.match(result.note, /failed again alone: ProcessJobTests\.A_child[^;]*; passed alone: StopHoldTickTests\.A_take/);
  assert.doesNotMatch(result.note, /failed again alone: [^;]*StopHoldTickTests/);

  // A re-run whose filter matched nothing exits 0, and is still not a pass.
  fake = fakeStep((command) => (command.includes('--filter')
    ? { out: 'No test matches the given testcase filter\n', code: 0 } : { out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');

  // Outside the Process category a failure is real: the fast half, the service, any gate without the settings.
  const fast = planned('driver', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process');
  fake = fakeStep(() => ({ out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, fast, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.deepEqual(fake.asked, [fast.run], 'a failure outside the Process category is never re-run');
  assert.match(result.note, /outside the Process category, a failure is real and never re-run/);

  // A rehearsal is not read for tests: one that said why it stopped has failed, and is not run again.
  fake = fakeStep(() => ({ out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, planned('family', 'npm run rehearse:family'), fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 1);
  fx.cleanup();
});


test('a process-level exit is the shell, a signal or a Windows crash status, never an ordinary failure', () => {
  // 127 and 126 are a POSIX shell's, 9009 is cmd's for a command it cannot find; 128 up is a signal (this
  // tool reads one as 128); a crash status comes through cmd and npm unsigned, and in its signed form elsewhere.
  for (const code of [126, 127, 9009, 128, 134, 255, 0xC0000142, 0xC0000409, -1073741819]) {
    assert.equal(tool.processExit(code), true, `exit ${code}`);
  }
  for (const code of [1, 2, 3, 100]) assert.equal(tool.processExit(code), false, `exit ${code}`);
});


test('a failed rehearsal is run again only when it reached no verdict: its process ended, or it said nothing', () => {
  // 2026-09-30: the family rehearsal exited 127 straight after building the HTTP host, with no transcript.
  const died = tool.rehearsalDecision(`${NPM_BANNER}${OK_LINES}\n(exited 127)\n`, 127);
  assert.equal(died.rerun, true);
  assert.match(died.reason, /exit 127/);
  assert.equal(tool.rehearsalDecision(`${NPM_BANNER}(exited 3221225794)\n`, 0xC0000142).rerun, true);

  const silent = tool.rehearsalDecision(`${NPM_BANNER}\n(exited 1)\n`, 1);
  assert.equal(silent.rerun, true);
  assert.match(silent.reason, /nothing of its own/);
  // A step before the gate is not the gate speaking: the build servers' shutdown says "shut down".
  const deploy = '$ dotnet build-server shutdown\nshut down\n\n(exited 0)\n\n$ npm run rehearse:deploy\n\n'
    + '> daoris-workspace@0.0.1 rehearse:deploy\n> node tools/deployment-rehearsal.mjs\n\n\n(exited 1)\n';
  assert.equal(tool.rehearsalDecision(deploy, 1, 'npm run rehearse:deploy').rerun, true);

  // A report is a verdict, whatever the exit that follows it.
  const kit = `${NPM_BANNER}${OK_LINES}  FAIL  a quest is refused where it should be\n          409\n\n  299/301 checks passed\n`;
  for (const code of [1, 127]) {
    const failed = tool.rehearsalDecision(kit, code);
    assert.equal(failed.rerun, false, `exit ${code}`);
    assert.match(failed.reason, /reported failed checks/);
  }
  // The platform's own suites inside `test:web`: vitest's summary and a failed file, and Playwright's.
  assert.equal(tool.rehearsalDecision(`${NPM_BANNER} FAIL  src/App.test.tsx > the frame\n      Tests  1 failed | 1712 passed (1713)\n`, 1).rerun, false);
  assert.equal(tool.rehearsalDecision(`${NPM_BANNER}  1 failed\n    [chromium] › e2e/platform.spec.ts:10:5 › a quest\n  20 passed (1.2m)\n`, 1).rerun, false);

  // Anything else said why it stopped: a thrown error is a failure, not a flake.
  const thrown = tool.rehearsalDecision(`${NPM_BANNER}${OK_LINES}Error: take failed: 500\n    at file:///x.mjs:630:9\n`, 1);
  assert.equal(thrown.rerun, false);
  assert.match(thrown.reason, /said why it stopped/);
});


test('a rehearsal that died is run again once, with its steps: a pass is a FLAKE, a second death a FAIL', async () => {
  const fx = makeFixture('merge-branch-rehearsal-reruns');
  const family = planned('family', 'npm run rehearse:family');
  let runs = 0;
  let fake = fakeStep(() => (++runs === 1
    ? { out: OK_LINES, code: 127 }
    : { out: `${OK_LINES}\n  301/301 checks passed\n`, code: 0 }));
  let result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FLAKE');
  assert.deepEqual(fake.asked, [family.run, family.run]);
  assert.match(result.note, /exit 127.*passed when run again.*family\.rerun\.log/);
  assert.match(readFileSync(join(fx.root, 'family.rerun.log'), 'utf8'), /301\/301 checks passed/);
  assert.match(readFileSync(join(fx.root, 'family.log'), 'utf8'), /\(exited 127\)/, 'the first run keeps its own log');

  fake = fakeStep(() => ({ out: '', code: 127 }));
  result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 2, 'run again once, not until it passes');
  assert.match(result.note, /exit 127.*failed again.*family\.rerun\.log/);

  // A rehearsal that reported failed checks is never run again.
  fake = fakeStep(() => ({ out: `${OK_LINES}  FAIL  a quest closes done\n`, code: 1 }));
  result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 1);
  assert.match(result.note, /reported failed checks/);

  // The rule is the rehearsals': a suite that dies is read the same way.
  fake = fakeStep(() => ({ out: '', code: 127 }));
  result = await tool.runGate(fx.root, planned('cli', 'npm run verify'), fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 1);
  fx.cleanup();
});
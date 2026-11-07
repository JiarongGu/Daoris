import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join, posix } from 'node:path';
import { tool, repoRoot, repoPlan, LONG, processRun, trackedFiles, contracts, rootScripts, gitRun } from '../support/merge-branch.ts';

// The census of test units reads this file's strings as the plan's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the plan's tests hold it to every declared gate and every project the gates build

test('a gate is a check, a suite or a rehearsal by what it runs', () => {
  assert.equal(tool.gateKind('dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- map --check'), 'check');
  // A tool's own --check is a check too, run with the fast ones (ORIENT1a); a tool run otherwise is not.
  assert.equal(tool.gateKind('node tools/orient-index.mjs --check'), 'check');
  assert.equal(tool.gateKind('node tools/orient-index.mjs'), 'suite');
  assert.equal(tool.gateKind('npm run verify'), 'suite');
  assert.equal(tool.gateKind('dotnet test src/Daoris.Service'), 'suite');
  assert.equal(tool.gateKind('npm run test:web'), 'rehearsal');
  assert.equal(tool.gateKind('npm run rehearse'), 'rehearsal');
  assert.equal(tool.gateKind('npm run rehearse:deploy'), 'rehearsal');
  // Anything it cannot place still runs, as a suite: the kind orders a gate, it never drops one.
  assert.equal(tool.gateKind('make everything'), 'suite');
});

test('the plan is the declared gates plus the rehearsals only the workflow runs, fast first, the declared order kept within a kind', () => {
  const declared = [
    { name: 'cli', run: 'npm run verify' },
    { name: 'universal', run: 'dotnet run --project devkit -- verify --universal-only' },
    { name: 'service', run: 'dotnet test src/Daoris.Service' },
    { name: 'web', run: 'npm run test:web' },
    { name: 'deployment', run: 'npm run rehearse:deploy' },
  ];
  const workflow = [
    '# run: npm run a-comment-is-not-a-step',
    '    steps:',
    '      - name: Deployment rehearsal',
    '        run: npm run rehearse:deploy',
    '      - run: npm run verify',
    '        run: npm run rehearse',
    '        run: npm run rehearse:family',
    '        run: |',
    '          npm run inside-a-block',
    '        run: npm run rehearse',
  ].join('\n');

  const plan = tool.gatePlan(declared, workflow);
  assert.deepEqual(plan.map((gate) => gate.name), ['universal', 'cli', 'service', 'rehearse', 'rehearse-family', 'web', 'deployment']);
  assert.deepEqual(plan.map((gate) => gate.kind), ['check', 'suite', 'suite', 'rehearsal', 'rehearsal', 'rehearsal', 'rehearsal']);
  // A long-lived build server carried a broken environment into a publish on 2026-09-30, and the deployment rehearsal ran
  // after a shutdown of every build server; since REHLANE2b its builds keep servers of their own, and BEFORESTEP1 left a gate no
  // step before it at all: a planned gate has no `before`.
  assert.ok(plan.every((gate) => !('before' in gate)));
});

test("this repository's plan runs every declared gate once, fast first, and the rehearsals and the web gates last", () => {
  const declared = (JSON.parse(readFileSync(join(repoRoot, 'daoris.gates.json'), 'utf8')) as { gates: { run: string }[] }).gates;
  const plan = tool.readPlan(repoRoot);

  for (const gate of declared) {
    assert.equal(plan.filter((planned) => planned.run === gate.run).length, 1, `${gate.run} must run exactly once`);
  }
  // The batch that broke main skipped these as "web only": every .NET suite is in the plan, the full set a stage needs.
  assert.ok(plan.filter((gate) => /^dotnet test /.test(gate.run)).length >= 4, 'the .NET suites left the plan');

  const rank = ['check', 'suite', 'rehearsal'];
  plan.forEach((gate, i) => {
    if (i > 0) assert.ok(rank.indexOf(gate.kind) >= rank.indexOf(plan[i - 1]!.kind), `${gate.name} runs before a faster kind`);
  });
  assert.deepEqual(plan.filter((gate) => gate.kind === 'rehearsal').map((gate) => gate.run),
    ['npm run rehearse', 'npm run rehearse:family', 'npm run test:web:e2e', 'npm run test:web:unit', 'npm run test:web:measured', 'npm run rehearse:deploy']);
  assert.ok(!('before' in plan.at(-1)!));
});

test('the long gates are the real-process halves and the deployment rehearsal, read from what each runs', () => {
  assert.deepEqual(repoPlan.filter((gate) => tool.isLongGate(gate)).map((gate) => gate.name), LONG);
  assert.equal(tool.isLongGate({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/process.runsettings' }), true);
  assert.equal(tool.isLongGate({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/git.process.runsettings' }), true);
  assert.equal(tool.isLongGate({ name: 'renamed', run: 'npm run rehearse:deploy' }), true);
  assert.equal(tool.isLongGate({ name: 'driver', run: 'dotnet test x --filter Category!=Process' }), false);
  assert.equal(tool.isLongGate({ name: 'rehearse-family', run: 'npm run rehearse:family' }), false);
});

test("this repository's Process lane is the real-process halves and the rehearsals; every other gate is light, the web gate too", () => {
  assert.deepEqual(repoPlan.filter((gate) => tool.isProcessLane(gate)).map((gate) => gate.name),
    ['service-process', 'driver-process', 'driver-process-git', 'modules-process', 'rehearse', 'rehearse-family', 'deployment']);
  assert.deepEqual(repoPlan.filter((gate) => !tool.isProcessLane(gate)).map((gate) => gate.name),
    ['universal', 'code-map', 'test-map', 'web-types', 'web-rules', 'cli', 'tool-tests', 'service', 'devkit', 'driver', 'modules', 'censuses', 'web', 'web-unit', 'web-measured']);
  // By what each runs, as the long gates are: a renamed gate, a new Process half or a new rehearsal is still one.
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/git.process.runsettings' }), true);
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'npm run rehearse:anything' }), true);
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'dotnet test x --filter Category!=Process' }), false);
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'npm run test:web' }), false);
});

test("a .NET gate's component is the first two folders of the project its command names", () => {
  assert.equal(tool.dotnetComponent('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process'), 'src/Daoris.Desktop');
  assert.equal(tool.dotnetComponent(processRun('src/Daoris.Desktop/Daoris.Desktop.Modules.Tests')), 'src/Daoris.Desktop');
  assert.equal(tool.dotnetComponent('dotnet test src/Daoris.Service'), 'src/Daoris.Service');
  assert.equal(tool.dotnetComponent('node tools/as-merged.mjs dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- verify --universal-only'), 'src/Daoris.Devkit');
  assert.equal(tool.dotnetComponent('node tools/orient-index.mjs && node tools/as-merged.mjs dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- verify --universal-only'), 'src/Daoris.Devkit');
  assert.equal(tool.dotnetComponent('dotnet build "src\\Daoris.Service\\Daoris.Service.Http"'), 'src/Daoris.Service');
  assert.equal(tool.dotnetComponent('dotnet build-server shutdown'), null);
  assert.equal(tool.dotnetComponent('dotnet test'), null);
  assert.equal(tool.dotnetComponent('npm run test:web'), null);
});

// What makes a component the unit of the build rule: two gates of two components never build one project.
test('every project a .NET project references is in its own component', () => {
  const projects = trackedFiles().filter((path) => path.startsWith('src/') && path.endsWith('.csproj'));
  assert.ok(projects.length >= 10, 'the .NET projects left src/');
  for (const project of projects) {
    const text = readFileSync(join(repoRoot, project), 'utf8');
    for (const [, include] of text.matchAll(/<ProjectReference\s+Include="([^"]+)"/g)) {
      const referenced = posix.normalize(posix.join(posix.dirname(project), include!.replace(/\\/g, '/')));
      const own = tool.dotnetComponent(`dotnet build ${project}`);
      const there = tool.dotnetComponent(`dotnet build ${referenced}`);
      // A shared project is built by each referrer's gates as well (`SHARED_PROJECTS`), so none builds it beside another.
      if (there !== own) assert.ok(tool.SHARED_PROJECTS.get(own)?.includes(there), `${project} references ${referenced}`);
    }
  }
});

// The schedule reads a gate's command; a root script that builds behind its name is read here, one level deep.
test("a light gate whose script builds a Process gate's component runs before it", () => {
  const scripts = (JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
  const lane = repoPlan.filter((gate) => tool.isProcessLane(gate) && tool.dotnetComponent(gate.run));
  assert.deepEqual([...new Set(lane.map((gate) => tool.dotnetComponent(gate.run)))], ['src/Daoris.Service', 'src/Daoris.Desktop']);
  const { waits } = tool.laneSchedule(repoPlan, { scripts });
  for (const gate of repoPlan.filter((planned) => !tool.isProcessLane(planned))) {
    const script = /^npm run (\S+)$/.exec(gate.run)?.[1];
    if (!script) continue;
    assert.ok(scripts[script], `${gate.name} runs a script the workspace declares`);
    for (const command of scripts[script]!.split('&&')) {
      const component = tool.dotnetComponent(command.trim());
      for (const half of lane.filter((each) => tool.dotnetComponent(each.run) === component)) assert.ok(waits[half.name]!.after.includes(gate.name), `${half.name} after ${gate.name}`);
    }
  }
});

test("this repository's light lane runs the suites' fast halves first, and the Process lane waits for what it shares", () => {
  const schedule = tool.laneSchedule(repoPlan);
  assert.deepEqual(schedule.light.map((gate) => gate.name),
    ['service', 'driver', 'modules', 'censuses', 'universal', 'code-map', 'test-map', 'web-types', 'web-rules', 'cli', 'tool-tests', 'devkit', 'web', 'web-unit', 'web-measured']);
  // REHLANE1, REHLANE2, REHLANE2b: the rehearsals right after the git half, beside it, one after the other.
  assert.deepEqual(schedule.process.map((gate) => gate.name),
    ['service-process', 'driver-process', 'driver-process-git', 'rehearse', 'rehearse-family', 'deployment', 'modules-process']);
  for (const name of ['service-process', 'driver-process', 'driver-process-git', 'modules-process']) {
    // GATEORDER1: and for the heavy light gates, so no Process half runs under their load.
    assert.deepEqual(schedule.waits[name], { after: ['service', 'driver', 'modules', 'censuses', 'cli', 'devkit', 'web', 'web-unit', 'web-measured'], component: name === 'service-process' ? 'src/Daoris.Service' : 'src/Daoris.Desktop' }, name);
  }
  assert.deepEqual(repoPlan.filter((gate) => tool.isHeavyGate(gate)).map((gate) => gate.name), ['cli', 'service', 'devkit', 'driver', 'modules', 'censuses', 'web', 'web-unit', 'web-measured']);
  assert.equal(tool.isHeavyGate({ name: 'x', run: 'node tools/orient-index.mjs --check' }), false);
  // A rehearsal builds what its command does not name. REHLANE2: and the release and deployment rehearsals' packs stage the
  // CLI's dist/, which the bin the cli gate's `daoris check` runs prefers.
  for (const name of ['rehearse', 'rehearse-family', 'deployment']) {
    assert.deepEqual(schedule.waits[name], { after: schedule.light.map((gate) => gate.name), component: null }, name);
  }
});

test('a CI run schedules by memory: a Process half waits only for the gates that build its component, and the heavy light gates run beside it (CI1)', () => {
  const schedule = tool.laneSchedule(repoPlan, { byMemory: true });
  for (const name of ['driver-process', 'driver-process-git', 'modules-process']) {
    assert.deepEqual(schedule.waits[name], { after: ['driver', 'modules', 'censuses'], component: 'src/Daoris.Desktop' }, name);
  }
  // The rehearsals still wait for the whole light lane: what they build and stage is state, not load.
  for (const name of ['rehearse', 'rehearse-family', 'deployment']) {
    assert.deepEqual(schedule.waits[name]!.after, schedule.light.map((gate) => gate.name), name);
  }
  // The rest is the merge's and the full set's schedule: the lanes, the pairs beside the git half, the builds reused.
  const plain = tool.laneSchedule(repoPlan);
  assert.deepEqual([schedule.light, schedule.process, schedule.beside, schedule.reuses, schedule.width], [plain.light, plain.process, plain.beside, plain.reuses, plain.width]);
});

test("a gate's components are what its command builds, and what the root scripts it names build", () => {
  const scripts = {
    verify: 'npm test -w x && node tools/as-merged.mjs dotnet run --project src/K/K.Cli -- verify',
    'test:web': 'npm --prefix w run build && dotnet build src/S/S.Http && npm --prefix w run e2e',
    lint: 'node lint.mjs',
    all: 'npm run lint && npm run -s verify',
    loop: 'npm run loop',
  };
  assert.deepEqual(tool.gateComponents({ name: 'cli', run: 'npm run verify' }, scripts), ['src/K']);
  assert.deepEqual(tool.gateComponents({ name: 'web', run: 'npm run test:web' }, scripts), ['src/S']);
  assert.deepEqual(tool.gateComponents({ name: 'service', run: 'dotnet test src/S' }, scripts), ['src/S']);
  assert.deepEqual(tool.gateComponents({ name: 'universal', run: 'node tools/as-merged.mjs dotnet run --project src/K/K.Cli -- verify' }, scripts), ['src/K']);
  assert.deepEqual(tool.gateComponents({ name: 'all', run: 'npm run all' }, scripts), ['src/K'], 'a script that names another is read through');
  assert.deepEqual(tool.gateComponents({ name: 'loop', run: 'npm run loop' }, scripts), [], 'a script that names itself is read once');
  assert.deepEqual(tool.gateComponents({ name: 'lint', run: 'npm run lint' }, scripts), []);
  assert.deepEqual(tool.gateComponents({ name: 'index', run: 'node tools/orient-index.mjs --check' }, scripts), []);
  assert.deepEqual(tool.gateComponents({ name: 'gone', run: 'npm run gone' }, scripts), [], 'npm refuses a script the workspace does not declare');
});

test("this repository's light gates run three at once, never two that build one component, the CLI's verify and web read behind their names", () => {
  const scripts = (JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
  const schedule = tool.laneSchedule(repoPlan, { scripts });
  assert.equal(tool.LIGHT_WIDTH, 3);
  assert.equal(schedule.width, tool.LIGHT_WIDTH);
  const building = (component: string) => schedule.light.filter((gate) => schedule.components[gate.name]!.includes(component)).map((gate) => gate.name);
  assert.deepEqual(building('src/Daoris.Desktop'), ['driver', 'modules', 'censuses']);
  // The CLI's verify ends in the devkit's universal gates, and the web gate builds the HTTP host its own build writes into.
  assert.deepEqual(building('src/Daoris.Devkit'), ['universal', 'code-map', 'cli', 'devkit']);
  assert.deepEqual(building('src/Daoris.Service'), ['service', 'web']);
  // WEBSPLIT1: the web gate's unit run builds none, so it runs beside any.
  assert.deepEqual(schedule.components['web-unit'], []);
  // FLOWCHART1e: nor does the measured-box check, which serves the page's source with Vite.
  assert.deepEqual(schedule.components['web-measured'], []);
  // GATELANES1's light order and GATEORDER1's waits hold: the gates that build a Process half's component run first, the service's
  // and the desktop's alike.
  assert.deepEqual(schedule.light.map((gate) => gate.name),
    ['service', 'driver', 'modules', 'censuses', 'web', 'universal', 'code-map', 'test-map', 'web-types', 'web-rules', 'cli', 'tool-tests', 'devkit', 'web-unit', 'web-measured']);
  assert.deepEqual(schedule.waits['driver-process']!.after, ['service', 'driver', 'modules', 'censuses', 'web', 'cli', 'devkit', 'web-unit', 'web-measured']);
  // GATEBUILD1: each Process half reuses the build of its own test project's fast half.
  assert.deepEqual(schedule.reuses, { 'service-process': 'service', 'driver-process': 'driver', 'driver-process-git': 'driver', 'modules-process': 'modules' });
});

test("this repository's web gates write the page's types with a generator built into an output of its own, so they build no component beside the .NET gates", () => {
  // Each web gate's pre-script runs the generator, which references the contracts and the desktop modules: built in their own
  // folders it wrote the contracts' obj/ while the service's or the desktop's gate built them there (CS2012).
  const webScripts = (JSON.parse(readFileSync(join(repoRoot, 'src', 'Daoris.Web', 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
  for (const pre of ['pretypecheck', 'pretest', 'prebuild']) assert.match(webScripts[pre]!, /^node \.\.\/\.\.\/tools\/contracts\.mjs --if-stale$/, pre);
  const { build, built } = contracts.generatorBuild(join(repoRoot, contracts.OWN_OUTPUT));
  assert.deepEqual(build.slice(0, 4), ['build', 'tools/contracts', '--artifacts-path', join(repoRoot, '_fixtures', 'contracts-build')]);
  assert.ok(built.startsWith(join(repoRoot, '_fixtures', 'contracts-build')), built);
  const text = readFileSync(join(repoRoot, 'tools', 'contracts.mjs'), 'utf8');
  assert.doesNotMatch(text, /'run',\s*'--project'/, 'the generator is run from its own build, never by `dotnet run` in its project');
  // So the scheduler counts no component for the web gates' types and rules, and they run beside any .NET gate.
  const schedule = tool.laneSchedule(repoPlan, { scripts: rootScripts() });
  assert.deepEqual(schedule.components['web-types'], []);
  assert.deepEqual(schedule.components['web-rules'], []);
});

test("a Process half's build is its own test project's, with the same build options; --no-build only when its fast half passed on this tree", () => {
  const fast = 'dotnet test src/X/X.Tests --filter Category!=Process';
  assert.ok(tool.testBuild(fast));
  assert.equal(tool.testBuild(processRun('src/X/X.Tests')), tool.testBuild(fast));
  assert.equal(tool.testBuild('dotnet test "src\\X\\X.Tests\\" -s src/X/git.process.runsettings --logger "trx;LogFileName=a b.trx"'), tool.testBuild(fast));
  assert.notEqual(tool.testBuild(processRun('src/X/M.Tests')), tool.testBuild(fast));
  assert.notEqual(tool.testBuild('dotnet test src/X/X.Tests -c Release --settings src/X/process.runsettings'), tool.testBuild(fast), 'a build option one side lacks');
  for (const run of ['npm run test:web', 'dotnet build src/X/X.Tests', 'dotnet test', 'dotnet test --filter Category!=Process']) {
    assert.equal(tool.testBuild(run), null, run);
  }
  assert.equal(tool.noBuildCommand(`${processRun('src/X/X.Tests')} `), `${processRun('src/X/X.Tests')} --no-build`);
  // A changed tree always builds, and so does a tree git could not name.
  assert.equal(tool.reuseBuild({ gate: 'd', tree: 't1' }, 't1'), true);
  assert.equal(tool.reuseBuild({ gate: 'd', tree: 't1' }, 't2'), false);
  assert.equal(tool.reuseBuild({ gate: 'd', tree: null }, null), false);
  assert.equal(tool.reuseBuild(undefined, 't1'), false);
});

test('the git half is the Process half run with the git settings, and the rehearsals that may run beside it are named by their scripts', () => {
  assert.equal(tool.isGitHalf({ name: 'renamed', run: gitRun('src/X/X.Tests') }), true);
  assert.equal(tool.isGitHalf({ name: 'driver-process', run: processRun('src/X/X.Tests') }), false);
  assert.equal(tool.isGitHalf({ name: 'driver', run: 'dotnet test src/X/X.Tests --filter Category!=Process' }), false);
  // REHLANE2: the release rehearsal too, which builds no .NET project at all. REHLANE2b: and the deployment rehearsal, whose
  // publish and build go into an output of its own.
  assert.deepEqual([...tool.BESIDE_GIT_HALF], ['rehearse', 'rehearse:family', 'rehearse:deploy']);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run rehearse:family' }), true);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run -s rehearse:family' }), true);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run rehearse' }), true);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run rehearse:deploy' }), true);
  for (const run of ['npm run rehearse-x', 'npm run rehearse:family-x', 'npm run rehearse:deploy-x', processRun('src/X/X.Tests')]) {
    assert.equal(tool.runsBesideGitHalf({ name: 'x', run }), false, run);
  }
});

test("this repository's Process lane runs the release, family and deployment rehearsals beside the git half, one after the other, and every other Process-lane gate alone", () => {
  const schedule = tool.laneSchedule(repoPlan, { scripts: rootScripts() });
  assert.deepEqual(schedule.process.map((gate) => gate.name),
    ['service-process', 'driver-process', 'driver-process-git', 'rehearse', 'rehearse-family', 'deployment', 'modules-process']);
  // Never beside each other (REHLANE2): the family runs the CLI's bin, which prefers the dist/ the release rehearsal's pack
  // stages in the CLI's folder and removes again; the deployment rehearsal packs that folder too, and runs the bin
  // (REHLANE2b); and two Process-lane gates at once is the measured ceiling.
  assert.deepEqual(schedule.beside, {
    'driver-process-git': ['rehearse', 'rehearse-family', 'deployment'],
    rehearse: ['driver-process-git'], 'rehearse-family': ['driver-process-git'], deployment: ['driver-process-git'],
  });
  // They still start once the light lane has ended: the service's hosts the family builds are the light lane's service and web
  // gates' too, and the cli gate runs the CLI's bin, which a rehearsal's staged dist/ would shadow.
  for (const name of ['rehearse', 'rehearse-family', 'deployment']) {
    assert.deepEqual(schedule.waits[name], { after: schedule.light.map((gate) => gate.name), component: null }, name);
  }
  // No gate is planned a step before it: the deployment rehearsal's builds keep build servers of their own in place of the
  // shutdown it once ran after, which would stop the builds of every gate beside it (REHLANE2b).
  assert.deepEqual(repoPlan.filter((gate) => 'before' in gate).map((gate) => gate.name), []);
});

test("this repository's web gate runs its unit tests as a light gate of its own, which builds nothing, with WEBGATE1's cap", () => {
  const scripts = rootScripts();
  const web = repoPlan.find((gate) => gate.name === 'web')!;
  const unit = repoPlan.find((gate) => gate.name === 'web-unit');
  assert.ok(unit, "the web gate's unit run is a declared gate of its own");
  assert.equal(unit.run, 'npm run test:web:unit');
  assert.equal(scripts['test:web:unit'], 'npm --prefix src/Daoris.Web run test');
  assert.equal(web.run, 'npm run test:web:e2e');
  assert.equal(scripts['test:web:e2e'],
    'npm --prefix src/Daoris.Web run build && dotnet build src/Daoris.Service/Daoris.Service.Http && npm --prefix src/Daoris.Web run e2e');
  // A person's whole web run is both, its unit tests first, as it was.
  assert.equal(scripts['test:web'], 'npm run test:web:unit && npm run test:web:e2e');
  const schedule = tool.laneSchedule(repoPlan, { scripts });
  assert.deepEqual(schedule.components['web-unit'], []);
  assert.deepEqual(schedule.components['web'], ['src/Daoris.Service', 'src/Daoris.Contracts']);
  assert.equal(tool.isProcessLane(unit), false);
  assert.equal(tool.isHeavyGate(unit), true, 'vitest loads the machine, so a Process half waits for it (GATEORDER1)');
  // The web gate goes before its unit run, so the place service frees goes to the build the light lane's floor waits on.
  const light = schedule.light.map((gate) => gate.name);
  assert.ok(light.indexOf('web') < light.indexOf('web-unit'), light.join(', '));
  // WEBGATE1: vitest's workers, a quarter of the machine, set in the page's own config, which the unit run reads wherever it runs.
  assert.match(readFileSync(join(repoRoot, 'src', 'Daoris.Web', 'vite.config.ts'), 'utf8'), /maxWorkers: process\.env\.DAORIS_WEB_WORKERS \?\? '25%'/);
});

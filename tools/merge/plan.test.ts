import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { git } from './git.ts';
import {
  BESIDE_GIT_HALF, LIGHT_WIDTH, SHARED_PROJECTS, dotnetComponent, gateComponents, gateKind, gatePlan, isGitHalf, isHeavyGate, isLongGate, isProcessGate,
  isProcessLane, isStageRehearsal, laneSchedule, noBuildCommand, planLines, readPlan, rerunPlan, reuseBuild, rootScripts, runsBesideGitHalf, stagePlan,
  testBuild, type Planned,
} from './plan.ts';

// test-map: strings name paths as data
// reaches: * — the plan's tests hold it to every declared gate and every project the gates build

const repoRoot = fileURLToPath(new URL('../..', import.meta.url));
const declared = (JSON.parse(readFileSync(join(repoRoot, 'daoris.gates.json'), 'utf8')) as { gates: { name: string; run: string }[] }).gates;
const repoPlan = readPlan(repoRoot);
const scripts = rootScripts(repoRoot);
const names = (gates: readonly { name: string }[]): string[] => gates.map((gate) => gate.name);
const byRun = (pattern: RegExp): Planned[] => repoPlan.filter((gate) => pattern.test(gate.run));
const processRun = (project: string): string => `dotnet test ${project} --settings src/Daoris.Desktop/process.runsettings`;
const gitRun = (project: string): string => `dotnet test ${project} --settings src/Daoris.Desktop/git.process.runsettings`;
const planned = (name: string, run: string): Planned => ({ name, run, kind: 'suite' });
const light = repoPlan.filter((gate) => !isProcessLane(gate));
const heavy = light.filter(isHeavyGate);

test('a gate is a check, a suite or a rehearsal by what it runs', () => {
  assert.equal(gateKind('dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- map --check'), 'check');
  // A tool's own --check is a check too, run with the fast ones; a tool run otherwise is not.
  assert.equal(gateKind('node tools/orient-index.mjs --check'), 'check');
  assert.equal(gateKind('node tools/orient-index.mjs'), 'suite');
  assert.equal(gateKind('npm run verify'), 'suite');
  assert.equal(gateKind('dotnet test src/Daoris.Service'), 'suite');
  assert.equal(gateKind('npm run test:web'), 'rehearsal');
  assert.equal(gateKind('npm run rehearse'), 'rehearsal');
  assert.equal(gateKind('npm run rehearse:deploy'), 'rehearsal');
  // Anything it cannot place still runs, as a suite: the kind orders a gate, it never drops one.
  assert.equal(gateKind('make everything'), 'suite');
});

test('the plan is the declared gates plus the rehearsals only the workflow runs, fast first, the declared order kept within a kind', () => {
  const gates = [
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

  const plan = gatePlan(gates, workflow);
  assert.deepEqual(names(plan), ['universal', 'cli', 'service', 'rehearse', 'rehearse-family', 'web', 'deployment']);
  assert.deepEqual(plan.map((gate) => gate.kind), ['check', 'suite', 'suite', 'rehearsal', 'rehearsal', 'rehearsal', 'rehearsal']);
  // A gate has no step before it: a planned gate has no `before`.
  assert.ok(plan.every((gate) => !('before' in gate)));
});

test("this repository's plan runs every declared gate once, fast first, and the rehearsals and the web gates last", () => {
  for (const gate of declared) {
    assert.equal(repoPlan.filter((each) => each.run === gate.run).length, 1, `${gate.run} must run exactly once`);
  }
  // The batch that broke main skipped these as "web only": every .NET suite is in the plan, the full set a stage needs.
  assert.equal(repoPlan.filter((gate) => /^dotnet test /.test(gate.run)).length, declared.filter((gate) => /^dotnet test /.test(gate.run)).length);

  const rank = ['check', 'suite', 'rehearsal'];
  repoPlan.forEach((gate, i) => {
    if (i > 0) assert.ok(rank.indexOf(gate.kind) >= rank.indexOf(repoPlan[i - 1]!.kind), `${gate.name} runs before a faster kind`);
  });
  const last = repoPlan.findLastIndex((gate) => gate.kind !== 'rehearsal');
  assert.ok(repoPlan.slice(last + 1).every((gate) => gate.kind === 'rehearsal'));
  assert.ok(!('before' in repoPlan.at(-1)!));
});

test('the long gates are the real-process halves and the deployment rehearsal, read from what each runs', () => {
  assert.deepEqual(names(repoPlan.filter(isLongGate)), names(byRun(/process\.runsettings|rehearse:deploy/)));
  assert.equal(isLongGate({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/process.runsettings' }), true);
  assert.equal(isLongGate({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/git.process.runsettings' }), true);
  assert.equal(isLongGate({ name: 'renamed', run: 'npm run rehearse:deploy' }), true);
  assert.equal(isLongGate({ name: 'driver', run: 'dotnet test x --filter Category!=Process' }), false);
  assert.equal(isLongGate({ name: 'rehearse-family', run: 'npm run rehearse:family' }), false);
});

test("this repository's Process lane is the real-process halves and the rehearsals; every other gate is light, the web gates too", () => {
  assert.deepEqual(names(repoPlan.filter(isProcessLane)), names(byRun(/process\.runsettings|^npm run rehearse/)));
  assert.deepEqual([...names(light)].sort(), names(declared.filter((gate) => !/process\.runsettings|^npm run rehearse/.test(gate.run))).sort());
  assert.ok(byRun(/^npm run test:web/).length > 0 && byRun(/^npm run test:web/).every((gate) => light.includes(gate)));
  // By what each runs, as the long gates are: a renamed gate, a new Process half or a new rehearsal is still one.
  assert.equal(isProcessLane({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/git.process.runsettings' }), true);
  assert.equal(isProcessLane({ name: 'renamed', run: 'npm run rehearse:anything' }), true);
  assert.equal(isProcessLane({ name: 'renamed', run: 'dotnet test x --filter Category!=Process' }), false);
  assert.equal(isProcessLane({ name: 'renamed', run: 'npm run test:web' }), false);
  assert.equal(isProcessGate({ name: 'x', run: processRun('src/X/X.Tests') }), true);
});

test("a .NET gate's component is the first two folders of the project its command names", () => {
  assert.equal(dotnetComponent('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process'), 'src/Daoris.Desktop');
  assert.equal(dotnetComponent(processRun('src/Daoris.Desktop/Daoris.Desktop.Modules.Tests')), 'src/Daoris.Desktop');
  assert.equal(dotnetComponent('dotnet test src/Daoris.Service'), 'src/Daoris.Service');
  assert.equal(dotnetComponent('node tools/as-merged.mjs dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- verify --universal-only'), 'src/Daoris.Devkit');
  assert.equal(dotnetComponent('node tools/orient-index.mjs && node tools/as-merged.mjs dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- verify --universal-only'), 'src/Daoris.Devkit');
  assert.equal(dotnetComponent('dotnet build "src\\Daoris.Service\\Daoris.Service.Http"'), 'src/Daoris.Service');
  assert.equal(dotnetComponent('dotnet build-server shutdown'), null);
  assert.equal(dotnetComponent('dotnet test'), null);
  assert.equal(dotnetComponent('npm run test:web'), null);
});

// What makes a component the unit of the build rule: two gates of two components never build one project.
test('every project a .NET project references is in its own component', () => {
  const projects = git(repoRoot, ['ls-files']).out.split('\n').filter((path) => path.startsWith('src/') && path.endsWith('.csproj'));
  assert.ok(projects.length >= 10, 'the .NET projects left src/');
  for (const project of projects) {
    const text = readFileSync(join(repoRoot, project), 'utf8');
    for (const [, include] of text.matchAll(/<ProjectReference\s+Include="([^"]+)"/g)) {
      const referenced = posix.normalize(posix.join(posix.dirname(project), include!.replace(/\\/g, '/')));
      const own = dotnetComponent(`dotnet build ${project}`);
      const there = dotnetComponent(`dotnet build ${referenced}`);
      // A shared project is built by each referrer's gates as well (`SHARED_PROJECTS`), so none builds it beside another.
      if (there !== own) assert.ok(SHARED_PROJECTS.get(own!)?.includes(there!), `${project} references ${referenced}`);
    }
  }
});

// The schedule reads a gate's command; a root script that builds behind its name is read here, one level deep.
test("a light gate whose script builds a Process gate's component runs before it", () => {
  const lane = repoPlan.filter((gate) => isProcessLane(gate) && dotnetComponent(gate.run));
  assert.ok(lane.length > 0);
  const { waits } = laneSchedule(repoPlan, { scripts });
  for (const gate of light) {
    const script = /^npm run (\S+)$/.exec(gate.run)?.[1];
    if (!script) continue;
    assert.ok(scripts[script], `${gate.name} runs a script the workspace declares`);
    for (const command of scripts[script]!.split('&&')) {
      const component = dotnetComponent(command.trim());
      for (const half of lane.filter((each) => dotnetComponent(each.run) === component)) assert.ok(waits[half.name]!.after.includes(gate.name), `${half.name} after ${gate.name}`);
    }
  }
});

test("a Process gate starts once the light gates of its component have ended, which run first; a rehearsal once the light lane has", () => {
  const gates = [
    planned('u', 'node u.mjs'),
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'),
    planned('m', 'dotnet test src/X/M.Tests --filter Category!=Process'),
    planned('h', 'npm run test:web'),
    planned('dp', processRun('src/X/X.Tests')),
    planned('r', 'npm run rehearse:family'),
  ];
  const schedule = laneSchedule(gates);
  assert.deepEqual(names(schedule.light), ['d', 'm', 'u', 'h']);
  // The heavy light gate h is waited for too; the cheap one, u, is not.
  assert.deepEqual(schedule.waits['dp'], { after: ['d', 'm', 'h'], component: 'src/X' });
  assert.deepEqual(schedule.waits['r'], { after: ['d', 'm', 'u', 'h'], component: null });
});

test("a Process half reuses the build of the fast half of its own test project", () => {
  const gates = [
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'), planned('m', 'dotnet test src/X/M.Tests --filter Category!=Process'),
    planned('dp', processRun('src/X/X.Tests')), planned('dg', gitRun('src/X/X.Tests')), planned('mp', processRun('src/X/M.Tests')),
  ];
  assert.deepEqual(laneSchedule(gates).reuses, { dp: 'd', dg: 'd', mp: 'm' });
});

test("this repository's light lane runs the gates that build a Process half's component first, and the Process lane waits for what it shares", () => {
  const schedule = laneSchedule(repoPlan, { scripts });
  const processComponents = new Set(repoPlan.filter(isProcessGate).map((gate) => dotnetComponent(gate.run)));
  const first = light.filter((gate) => schedule.components[gate.name]!.some((component) => processComponents.has(component)));
  assert.ok(first.length > 0);
  assert.deepEqual(names(schedule.light), [...names(first), ...names(light.filter((gate) => !first.includes(gate)))]);
  const lane = schedule.light.map((gate) => gate.name);
  for (const gate of repoPlan.filter(isProcessGate)) {
    // After the heavy light gates too, so no Process half runs under their load.
    const own = names(first).filter((name) => schedule.components[name]!.includes(dotnetComponent(gate.run)!));
    assert.deepEqual(schedule.waits[gate.name]!.after, lane.filter((name) => own.includes(name) || names(heavy).includes(name)), gate.name);
  }
  assert.ok(heavy.length > 0 && heavy.every((gate) => /^dotnet test\b|^npm run (verify|test:web)\b/.test(gate.run)));
  assert.equal(isHeavyGate({ name: 'x', run: 'node tools/orient-index.mjs --check' }), false);
  // A rehearsal builds what its command does not name, and the release and deployment rehearsals' packs stage the
  // CLI's dist/, which the bin the cli gate's `daoris check` runs prefers.
  for (const gate of repoPlan.filter(runsBesideGitHalf)) {
    assert.deepEqual(schedule.waits[gate.name], { after: lane, component: null }, gate.name);
  }
});

test('a CI run schedules by memory: a Process half waits only for the gates that build its component, and the heavy light gates run beside it', () => {
  const schedule = laneSchedule(repoPlan, { scripts, byMemory: true });
  const plain = laneSchedule(repoPlan, { scripts });
  for (const gate of repoPlan.filter((each) => isProcessGate(each) && dotnetComponent(each.run))) {
    const component = dotnetComponent(gate.run)!;
    assert.deepEqual(schedule.waits[gate.name]!.after, schedule.light.filter((other) => schedule.components[other.name]!.includes(component)).map((other) => other.name), gate.name);
  }
  // The rehearsals still wait for the whole light lane: what they build and stage is state, not load.
  for (const gate of repoPlan.filter(runsBesideGitHalf)) assert.deepEqual(schedule.waits[gate.name]!.after, names(schedule.light), gate.name);
  // The rest is the merge's and the full set's schedule: the lanes, the pairs beside the git half, the builds reused.
  assert.deepEqual([schedule.light, schedule.process, schedule.beside, schedule.reuses, schedule.width], [plain.light, plain.process, plain.beside, plain.reuses, plain.width]);
});

test("a gate's components are what its command builds, and what the root scripts it names build", () => {
  const rootScript = {
    verify: 'npm test -w x && node tools/as-merged.mjs dotnet run --project src/K/K.Cli -- verify',
    'test:web': 'npm --prefix w run build && dotnet build src/S/S.Http && npm --prefix w run e2e',
    lint: 'node lint.mjs',
    all: 'npm run lint && npm run -s verify',
    loop: 'npm run loop',
  };
  const components = (run: string): string[] => gateComponents({ name: 'g', run }, rootScript);
  assert.deepEqual(components('npm run verify'), ['src/K']);
  assert.deepEqual(components('npm run test:web'), ['src/S']);
  assert.deepEqual(components('dotnet test src/S'), ['src/S']);
  assert.deepEqual(components('node tools/as-merged.mjs dotnet run --project src/K/K.Cli -- verify'), ['src/K']);
  assert.deepEqual(components('npm run all'), ['src/K'], 'a script that names another is read through');
  assert.deepEqual(components('npm run loop'), [], 'a script that names itself is read once');
  assert.deepEqual(components('npm run lint'), []);
  assert.deepEqual(components('node tools/orient-index.mjs --check'), []);
  assert.deepEqual(components('npm run gone'), [], 'npm refuses a script the workspace does not declare');
  assert.deepEqual(components('node census.mjs && node --test a.test.mjs && dotnet test src/T/T.Tests'), ['src/T']);
  assert.deepEqual(components('node tools/reached-checks.mjs --check'), ['src/Daoris.Desktop', ...SHARED_PROJECTS.get('src/Daoris.Desktop')!, 'src/Daoris.Service']);
  assert.deepEqual(components('node tools/layers-census.mjs && dotnet test src/Daoris.Testing.Tests'), ['src/Daoris.Testing.Tests', 'src/Daoris.Testing', 'src/Daoris.Ports']);
});

test("this repository's light gates run three at once, never two that build one component, the CLI's verify and web read behind their names", () => {
  const schedule = laneSchedule(repoPlan, { scripts });
  assert.equal(LIGHT_WIDTH, 3);
  assert.equal(schedule.width, LIGHT_WIDTH);
  const building = (component: string): string[] => schedule.light.filter((gate) => schedule.components[gate.name]!.includes(component)).map((gate) => gate.name);
  // Every .NET suite of a component builds it, and each host's gates build the shared projects too.
  for (const component of ['src/Daoris.Desktop', 'src/Daoris.Service', 'src/Daoris.Adapters']) {
    const suites = light.filter((gate) => /^dotnet test\b/.test(gate.run) && dotnetComponent(gate.run) === component).map((gate) => gate.name);
    assert.ok(suites.length > 0, component);
    for (const name of suites) assert.ok(building(component).includes(name), `${name} builds ${component}`);
    for (const shared of SHARED_PROJECTS.get(component) ?? []) for (const name of suites) assert.ok(building(shared).includes(name), `${name} builds ${shared}`);
  }
  // The CLI's verify ends in the devkit's universal gates, and the web gate builds the HTTP host its own build writes into.
  const cli = byRun(/^npm run verify$/);
  const web = byRun(/^npm run test:web:e2e$/);
  assert.ok(cli.length === 1 && web.length === 1);
  assert.ok(building('src/Daoris.Devkit').includes(cli[0]!.name));
  assert.ok(building('src/Daoris.Service').includes(web[0]!.name));
  // The web gate's unit run builds none, so it runs beside any; nor does the measured-box check, which serves the page's source with Vite.
  for (const gate of byRun(/^npm run test:web:(unit|measured)$/)) assert.deepEqual(schedule.components[gate.name], [], gate.name);
  // Each Process half reuses the build of its own test project's fast half.
  for (const gate of repoPlan.filter(isProcessGate)) {
    const build = testBuild(gate.run);
    const fast = light.find((other) => build !== null && testBuild(other.run) === build);
    if (fast) assert.equal(schedule.reuses[gate.name], fast.name, gate.name);
    else assert.equal(schedule.reuses[gate.name], undefined, gate.name);
  }
});

test("this repository's web gates build no component beside the .NET gates", () => {
  const schedule = laneSchedule(repoPlan, { scripts });
  for (const gate of byRun(/^npm run (typecheck:web|check:web-rules)$/)) assert.deepEqual(schedule.components[gate.name], [], gate.name);
});

test("a Process half's build is its own test project's, with the same build options; --no-build only when its fast half passed on this tree", () => {
  const fast = 'dotnet test src/X/X.Tests --filter Category!=Process';
  assert.ok(testBuild(fast));
  assert.equal(testBuild(processRun('src/X/X.Tests')), testBuild(fast));
  assert.equal(testBuild('dotnet test "src\\X\\X.Tests\\" -s src/X/git.process.runsettings --logger "trx;LogFileName=a b.trx"'), testBuild(fast));
  assert.notEqual(testBuild(processRun('src/X/M.Tests')), testBuild(fast));
  assert.notEqual(testBuild('dotnet test src/X/X.Tests -c Release --settings src/X/process.runsettings'), testBuild(fast), 'a build option one side lacks');
  for (const run of ['npm run test:web', 'dotnet build src/X/X.Tests', 'dotnet test', 'dotnet test --filter Category!=Process']) {
    assert.equal(testBuild(run), null, run);
  }
  assert.equal(noBuildCommand(`${processRun('src/X/X.Tests')} `), `${processRun('src/X/X.Tests')} --no-build`);
  // A changed tree always builds, and so does a tree git could not name.
  assert.equal(reuseBuild({ tree: 't1' }, 't1'), true);
  assert.equal(reuseBuild({ tree: 't1' }, 't2'), false);
  assert.equal(reuseBuild({ tree: null }, null), false);
  assert.equal(reuseBuild(undefined, 't1'), false);
});

test('the git half is the Process half run with the git settings, and the rehearsals that may run beside it are named by their scripts', () => {
  assert.equal(isGitHalf({ name: 'renamed', run: gitRun('src/X/X.Tests') }), true);
  assert.equal(isGitHalf({ name: 'driver-process', run: processRun('src/X/X.Tests') }), false);
  assert.equal(isGitHalf({ name: 'driver', run: 'dotnet test src/X/X.Tests --filter Category!=Process' }), false);
  // The release rehearsal too, which builds no .NET project at all, and the deployment rehearsal, whose
  // publish and build go into an output of its own.
  assert.deepEqual([...BESIDE_GIT_HALF], ['rehearse', 'rehearse:family', 'rehearse:deploy']);
  for (const run of ['npm run rehearse:family', 'npm run -s rehearse:family', 'npm run rehearse', 'npm run rehearse:deploy']) {
    assert.equal(runsBesideGitHalf({ name: 'renamed', run }), true, run);
  }
  for (const run of ['npm run rehearse-x', 'npm run rehearse:family-x', 'npm run rehearse:deploy-x', processRun('src/X/X.Tests')]) {
    assert.equal(runsBesideGitHalf({ name: 'x', run }), false, run);
  }
});

test("this repository's Process lane runs the rehearsals beside the git half, one after the other, and every other Process-lane gate alone", () => {
  const schedule = laneSchedule(repoPlan, { scripts });
  const lane = repoPlan.filter(isProcessLane);
  const git = lane.filter(isGitHalf);
  const beside = lane.filter(runsBesideGitHalf);
  assert.ok(git.length > 0 && beside.length === BESIDE_GIT_HALF.length);
  assert.deepEqual([...names(schedule.process)].sort(), names(lane).sort());
  // Each rehearsal comes right after the last git half, in the plan's order, so none is beside another.
  const at = schedule.process.findLastIndex(isGitHalf);
  assert.deepEqual(names(schedule.process.slice(at + 1, at + 1 + beside.length)), names(beside));
  assert.deepEqual(schedule.beside, Object.fromEntries([
    ...git.map((gate) => [gate.name, names(beside)]),
    ...beside.map((gate) => [gate.name, names(git)]),
  ]));
  // No gate is planned a step before it.
  assert.deepEqual(repoPlan.filter((gate) => 'before' in gate), []);
});

test("this repository's web gate runs its unit tests as a light gate of its own, which builds nothing, with a cap", () => {
  const unit = byRun(/^npm run test:web:unit$/)[0];
  const web = byRun(/^npm run test:web:e2e$/)[0];
  assert.ok(unit && web, "the web gate's unit and browser runs are declared gates of their own");
  assert.match(scripts['test:web:unit']!, /^npm --prefix src\/Daoris\.Web run test /);
  assert.match(scripts['test:web:e2e']!, /^npm --prefix src\/Daoris\.Web run build && node tools\/tree-base\.mjs build src\/Daoris\.Service\/Daoris\.Service\.Http && npm --prefix src\/Daoris\.Web run e2e$/);
  // A person's web run is both, its unit tests first.
  assert.equal(scripts['test:web'], 'npm run test:web:unit && npm run test:web:e2e');
  const schedule = laneSchedule(repoPlan, { scripts });
  assert.deepEqual(schedule.components[unit.name], []);
  assert.deepEqual(schedule.components[web.name], ['src/Daoris.Service', ...SHARED_PROJECTS.get('src/Daoris.Service')!]);
  assert.equal(isProcessLane(unit), false);
  assert.equal(isHeavyGate(unit), true, 'vitest loads the machine, so a Process half waits for it');
  // The web gate goes first, so the place service frees goes to the build the light lane's floor waits on.
  const order = names(schedule.light);
  assert.ok(order.indexOf(web.name) < order.indexOf(unit.name), order.join(', '));
  // Vitest's workers, a quarter of the machine, set in the page's config.
  assert.match(readFileSync(join(repoRoot, 'src', 'Daoris.Web', 'vite.config.ts'), 'utf8'), /maxWorkers: process\.env\.DAORIS_WEB_WORKERS \?\? '25%'/);
});

test("the light stage requires every gate of the plan but the stage's own rehearsals, which the CI runs after the install; the full stage requires all", () => {
  const stage = (light: boolean): string[] => names(stagePlan(repoPlan, { light }));
  assert.deepEqual(stage(false), names(repoPlan));
  assert.deepEqual(names(repoPlan).filter((name) => !stage(true).includes(name)), names(repoPlan.filter(isStageRehearsal)));
  assert.ok(names(repoPlan.filter(isStageRehearsal)).length > 0);
  // Every Process half and every light gate stay required.
  for (const gate of repoPlan.filter((each) => isProcessGate(each) || !isProcessLane(each))) assert.ok(stage(true).includes(gate.name), gate.name);
});

test('a re-run runs the named gates, the chosen ones with no verdict and the ones a changed path reaches, and keeps the rest', () => {
  const plan = ['universal', 'cli', 'driver', 'driver-process', 'web'];
  const chosen = ['universal', 'cli', 'driver', 'driver-process', 'web'];
  const results = {
    universal: { verdict: 'PASS', at: '2026-10-04T10:00:00Z' }, cli: { verdict: 'PASS', at: '2026-10-04T10:03:00Z' }, driver: { verdict: 'FAIL', at: '2026-10-04T10:08:00Z' },
  };
  assert.deepEqual(rerunPlan(plan, chosen, results, ['driver']), {
    run: ['driver', 'driver-process', 'web'],
    kept: [{ name: 'universal', verdict: 'PASS', at: '2026-10-04T10:00:00Z' }, { name: 'cli', verdict: 'PASS', at: '2026-10-04T10:03:00Z' }],
    fresh: ['driver-process', 'web'],
    stale: [],
  });
  const again = rerunPlan(plan, chosen, results, [], { cli: ['a.ts'] });
  assert.deepEqual(again.run, ['cli', 'driver-process', 'web']);
  assert.deepEqual(again.stale, [{ name: 'cli', verdict: 'PASS', at: '2026-10-04T10:03:00Z', paths: ['a.ts'] }]);
});

test('the plan lists each gate numbered with its name, kind and command', () => {
  const lines = planLines([planned('a', 'node a.mjs'), { name: 'b', run: 'npm run rehearse', kind: 'rehearsal' }]);
  assert.equal(lines.length, 2);
  assert.match(lines[0]!, /^ {2}1\. {1,2}a +suite +node a\.mjs$/);
  assert.match(lines[1]!, /^ {2}2\. {1,2}b +rehearsal +npm run rehearse$/);
});

test('a gate declared alone holds every component another gate builds, so it never runs beside one; the declared census gate is alone', () => {
  const gates = [
    planned('d', 'dotnet test src/X/X.Tests'),
    planned('s', 'dotnet test src/Y/Y.Tests'),
    { ...planned('c', 'dotnet test src/X/X.Tests --filter Category=Census'), alone: true },
  ];
  const { components } = laneSchedule(gates);
  assert.deepEqual([...components['c']!].sort(), ['src/X', 'src/Y']);
  assert.deepEqual(components['d'], ['src/X']);
  assert.equal(repoPlan.find((gate) => gate.name === 'censuses')?.alone, true);
  const every = laneSchedule(repoPlan, { scripts }).components;
  for (const gate of light) for (const component of every[gate.name]!) assert.ok(every['censuses']!.includes(component), `${gate.name}'s ${component}`);
});

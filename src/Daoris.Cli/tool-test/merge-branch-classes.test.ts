import { test } from 'node:test';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { tool, type Verdict, type Judging, type Reached, type ClassVerdict, type Standing, type Last, type Judged } from './support/merge-branch.ts';
import { planned, fakeStep, TRX } from './support/merge-branch-kit.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


test('the slowest classes are read from a trx: each class\'s tests summed, slowest first, at most ten', () => {
  assert.deepEqual(tool.slowestClasses(TRX), [
    { name: 'SlowTests', ms: 240_500, tests: 2 },
    { name: 'Outer+InnerTests', ms: 120_000, tests: 1 },
    { name: 'QuickTests', ms: 30_250, tests: 1 },
    { name: 'SkippedTests', ms: 0, tests: 1 },
  ]);
  assert.deepEqual(tool.slowestClasses(TRX, 2).map((slow) => slow.name), ['SlowTests', 'Outer+InnerTests']);
  assert.deepEqual(tool.slowestClasses('not a trx'), []);
  // Thirteen classes: ten are named.
  const many = Array.from({ length: 13 }, (_, i) => `<UnitTestResult testId="m${i}" testName="N.C${i}Tests.T" duration="00:00:${String(10 + i).padStart(2, '0')}" />`
    + `<UnitTest id="m${i}"><TestMethod className="N.C${i}Tests" name="T" /></UnitTest>`).join('\n');
  assert.equal(tool.slowestClasses(many).length, 10);
  assert.equal(tool.slowestClasses(many)[0]!.name, 'C12Tests');
});


test('QUEUEFILTER1: prefix isolation combines an existing filter with its class in one argument', () => {
  const failure = { names: 'test', unit: 'SessionTests' };
  const command = 'dotnet test src/Driver.Tests --filter Category!=Process';
  const narrowed = tool.prefixCommand({ run: command }, failure);
  assert.equal(narrowed.match(/--filter\b/g)?.length, 1, narrowed);
  assert.match(narrowed, /--filter "\(Category!=Process\)&\(FullyQualifiedName~SessionTests\)"$/);
  for (const filter of ['"Category!=Process|Priority=1"', "'Category!=Process'", '=Category!=Process']) {
    const run = `dotnet test src/Driver.Tests --filter${filter.startsWith('=') ? '' : ' '}${filter} --no-restore`;
    const selected = tool.prefixCommand({ run }, failure);
    assert.equal(selected.match(/--filter\b/g)?.length, 1, selected);
    assert.ok(selected.endsWith(' --no-restore'), selected);
    assert.ok(selected.includes(`(${filter.replace(/^["'=]|["']$/g, '')})&(FullyQualifiedName~SessionTests)`), selected);
  }
  const process = 'dotnet test src/Driver.Tests --settings process.runsettings';
  assert.equal(tool.prefixCommand({ run: process }, failure), `${process} --filter "FullyQualifiedName~SessionTests"`);
  assert.equal(tool.prefixCommand({ run: 'dotnet test src/Driver.Tests --filter ""' }, failure),
    'dotnet test src/Driver.Tests --filter "FullyQualifiedName~SessionTests"');
  assert.equal(tool.prefixCommand({ run: command }, { names: 'gate' }), command);
});


test('a Process gate writes its trx beside its log, and the slowest classes are printed after its line', async () => {
  const fx = makeFixture('merge-branch-trx');
  const processGate = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  assert.equal(tool.trxCommand(processGate.run, join(fx.root, 'driver-process.trx')),
    `${processGate.run} --logger "trx;LogFileName=${join(fx.root, 'driver-process.trx')}"`);

  const fake = fakeStep((command) => {
    const file = /LogFileName=([^"]+)"/.exec(command)?.[1];
    if (file) writeFileSync(file, TRX);
    return { out: 'Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 7 m - D.dll (net10.0)\n', code: 0 };
  });
  const result = await tool.runGate(fx.root, processGate, fx.root, { step: fake.step });
  assert.match(fake.asked[0]!, /--logger "trx;LogFileName=.*driver-process\.trx"$/);
  assert.equal(result.trx, join(fx.root, 'driver-process.trx'));
  assert.deepEqual(result.slowest!.map((slow) => slow.name), ['SlowTests', 'Outer+InnerTests', 'QuickTests', 'SkippedTests']);

  // Its re-run of one test adds no logger: the full run's trx is the one measured.
  assert.doesNotMatch(tool.rerunCommand(processGate.run, 'N.SlowTests.A'), /trx/);
  // A gate outside the Process category writes none.
  const plain = fakeStep(() => ({ out: '', code: 0 }));
  const fast = await tool.runGate(fx.root, planned('driver', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process'), fx.root, { step: plain.step });
  assert.doesNotMatch(plain.asked[0]!, /trx/);
  assert.equal(fast.slowest, undefined);

  const printed: string[] = [];
  result.ms = 1000;
  await tool.runGates(fx.root, [processGate], fx.root, { run: async () => result, print: (line) => printed.push(line) });
  assert.match(printed[0]!, /PASS\s+driver-process/);
  // The classes run at once, so the wall is the run's own (result.ms), never the classes' sum.
  assert.match(printed[1]!, /slowest classes of driver-process, by their own times; the half's wall was 1 s \(its classes ran at once, so the times do not add up to it\), from driver-process\.trx:/);
  assert.match(printed[2]!, /^\s+1\.\s+4 m 1 s\s+2 tests\s+SlowTests$/);
  assert.match(printed[3]!, /^\s+2\.\s+2 m 0 s\s+1 test\s+Outer\+InnerTests$/);
  fx.cleanup();
});


// ---------------------------------------------------------------------------------------------------
// A Process half run by the classes a change reaches, each class's verdict kept with its tree

test("a Process half's trx gives each class's verdict: a failed test fails its class, and one that passed alone is its class's flake", () => {
  assert.deepEqual([...tool.classResults(TRX)].map(([name, result]) => `${name} ${result.tests} ${result.failed.join(',')}`),
    ['N.SlowTests 2 N.SlowTests.B', 'N.QuickTests 1 ', 'N.Outer+InnerTests 1 ', 'N.SkippedTests 1 ']);
  assert.deepEqual(tool.classVerdictsOf(TRX, { verdict: 'FAIL' }), [
    { name: 'N.SlowTests', verdict: 'FAIL' }, { name: 'N.QuickTests', verdict: 'PASS' },
    { name: 'N.Outer+InnerTests', verdict: 'PASS' }, { name: 'N.SkippedTests', verdict: 'PASS' },
  ]);
  assert.deepEqual(tool.classVerdictsOf(TRX, { verdict: 'FLAKE', flakes: ['N.SlowTests.B'] })[0], { name: 'N.SlowTests', verdict: 'FLAKE' });
  assert.deepEqual(tool.classVerdictsOf(TRX, { verdict: 'ENDED' }), [], 'a gate the run ended gives no verdict');
  assert.deepEqual(tool.classVerdictsOf('not a trx', { verdict: 'PASS' }), []);
});


test("each class's verdicts are kept beside the gates', keyed by gate and class with the tree each ran on, the newest last and a few each", () => {
  const fx = makeFixture('merge-branch-class-verdicts');
  const at = (hour: number) => `2026-10-22T${String(hour).padStart(2, '0')}:00:00Z`;
  assert.equal(tool.CLASS_VERDICTS_FILE, 'local/class-verdicts.json', "under local/, outside local/scratch/: it outlives a run's logs");
  assert.deepEqual(tool.readClassVerdicts(fx.root), { schema: 1, gates: {} });
  tool.addClassVerdicts(fx.root, 'driver-process', [
    { name: 'N.ATests', verdict: 'PASS', tree: 't1', at: at(1), commit: 'c1' },
    { name: 'N.BTests', verdict: 'FAIL', tree: 't1', at: at(1), commit: 'c1' },
  ]);
  tool.addClassVerdicts(fx.root, 'driver-process', [{ name: 'N.ATests', verdict: 'PASS', tree: 't2', at: at(2), commit: 'c2' }]);
  tool.addClassVerdicts(fx.root, 'driver-process', [{ name: 'N.ATests', verdict: 'FLAKE', tree: 't3', at: at(3), commit: 'c3' }], { keep: 2 });
  tool.addClassVerdicts(fx.root, 'modules-process', [{ name: 'N.ATests', verdict: 'PASS', tree: 't3', at: at(3), commit: 'c3' }]);
  const store = tool.readClassVerdicts(fx.root);
  assert.deepEqual(store.gates['driver-process']!['N.ATests']!.map((each) => `${each.verdict} ${each.tree}`), ['PASS t2', 'FLAKE t3']);
  assert.deepEqual(store.gates['driver-process']!['N.BTests'], [{ verdict: 'FAIL', tree: 't1', at: at(1), commit: 'c1' }]);
  assert.equal(store.gates['modules-process']!['N.ATests']!.length, 1, 'one gate\'s class is not another\'s');
  writeFileSync(join(fx.root, tool.CLASS_VERDICTS_FILE), '{ not json');
  assert.deepEqual(tool.readClassVerdicts(fx.root), { schema: 1, gates: {} }, 'a store that does not read is none: every class runs');
  fx.cleanup();
});


test('a half the CI owes runs the classes a change reaches and keeps the rest; a path every class reads runs it whole; a class with no reach, a failure or none runs', () => {
  const at = (hour: number) => `2026-10-22T${String(hour).padStart(2, '0')}:00:00Z`;
  const now = Date.parse(at(12));
  const classes = ['N.AlphaTests', 'N.BetaTests', 'N.LonelyTests', 'N.ToolTests'];
  const drifts: Record<string, string[]> = {
    t1: ['src/Core/Alpha/AlphaThing.cs'],
    t0: ['src/Core/Alpha/AlphaThing.cs', 'src/Tests/GlobalUsings.cs'],
  };
  // The census, stood in: Alpha's class reads Alpha, Beta's and the tool's read Beta, the lonely class's sources place nothing,
  // and the global usings act on every test.
  const reached = (paths: string[]): Reached => {
    const whole = paths.filter((path) => path.endsWith('GlobalUsings.cs')).map((path) => ({ path, why: `${path} acts on every test unnamed` }));
    const by = new Map<string, string>();
    for (const path of paths) {
      for (const [name, area] of [['N.AlphaTests', 'Alpha'], ['N.BetaTests', 'Beta'], ['N.ToolTests', 'Beta'], ['N.LonelyTests', '']] as const) {
        if (path.startsWith(`src/Core/${area}`) && !by.has(name)) by.set(name, path);
      }
    }
    return { whole, classes: whole.length ? classes : [...by.keys()], by };
  };
  const facts = { classes, tree: 't2', drift: (from: string) => drifts[from] ?? null, reached, now };
  const pass = (tree: string, hour = 10, verdict = 'PASS'): ClassVerdict => ({ verdict, tree, at: at(hour), commit: `${tree}-commit` });
  const every = (entry: ClassVerdict) => Object.fromEntries(classes.map((name) => [name, [entry]]));

  // One module changed since every class passed: its class and the planted class with no reach run, the rest are kept.
  let plan = tool.classPlan({ ...facts, verdicts: every(pass('t1')) });
  assert.equal(plan.whole, null);
  assert.deepEqual(plan.run, [
    { name: 'N.AlphaTests', why: 'src/Core/Alpha/AlphaThing.cs changed since its pass, which reaches it' },
    { name: 'N.LonelyTests', why: 'src/Core/Alpha/AlphaThing.cs changed since its pass, which reaches it' },
  ]);
  assert.deepEqual(plan.kept.map((kept) => `${kept.name} ${kept.verdict.verdict} ${kept.verdict.tree} ${kept.standing}`),
    ['N.BetaTests PASS t1 unreached', 'N.ToolTests PASS t1 unreached']);
  assert.equal(plan.sweep, at(10), 'the oldest verdict it keeps');

  // A file every class reads without naming it changed since: the whole half.
  plan = tool.classPlan({ ...facts, verdicts: every(pass('t0')) });
  assert.match(plan.whole ?? '', /src\/Tests\/GlobalUsings\.cs acts on every test unnamed/);
  assert.deepEqual([plan.run.length, plan.kept, plan.sweep], [4, [], null]);

  // A failure stays owed whatever changed; a class with no verdict runs; a pass on this very tree is kept.
  plan = tool.classPlan({ ...facts, verdicts: {
    'N.AlphaTests': [pass('t1')],
    'N.BetaTests': [pass('t1', 9), pass('t1', 10, 'FAIL')],
    'N.LonelyTests': [pass('t2', 11, 'FLAKE')],
  } });
  assert.deepEqual(plan.run.map((run) => `${run.name}: ${run.why}`), [
    'N.AlphaTests: src/Core/Alpha/AlphaThing.cs changed since its pass, which reaches it',
    'N.BetaTests: it failed on 2026-10-22 10:00 UTC, and a failure runs until it passes',
    'N.ToolTests: it has no verdict of its own yet',
  ]);
  assert.deepEqual(plan.kept.map((kept) => `${kept.name} ${kept.standing}`), ['N.LonelyTests exact']);

  // Past the bound, the oldest kept verdict makes the run a sweep: the whole half.
  plan = tool.classPlan({ ...facts, verdicts: every(pass('t1')), now: Date.parse(at(10)) + tool.SWEEP_BOUND_MS + 60_000 });
  assert.match(plan.whole ?? '', /its oldest kept class verdict \(N\.BetaTests, 2026-10-22 10:00 UTC\) is older than 24 hours: a sweep runs the whole half/);
  assert.equal(tool.SWEEP_BOUND_MS, 24 * 60 * 60 * 1000);
  assert.match(tool.classPlan({ ...facts, verdicts: every(pass('t1')), now: Date.parse(at(11)), bound: 30 * 60_000 }).whole ?? '', /older than 0\.5 hours/);

  // Every class kept with no verdict of the half's own to compose from: the whole half, for one (the refusals below).
  plan = tool.classPlan({ ...facts, verdicts: every(pass('t2')) });
  assert.match(plan.whole ?? '', /every class is kept, but the half has no verdict of its own: it runs whole for a verdict of its own/);
  // No class with a pass that stands (a tree git cannot compare): the whole half.
  plan = tool.classPlan({ ...facts, verdicts: every(pass('gone')) });
  assert.match(plan.whole ?? '', /no class has a pass that stands on this tree/);
});


test('CISCOPE1b: a half whose every class stands passes without running only when its last verdict was a pass gone stale by reach, complete and matching the census', () => {
  const at = (hour: number) => `2026-10-22T${String(hour).padStart(2, '0')}:00:00Z`;
  const classes = ['N.AlphaTests', 'N.BetaTests', 'N.LonelyTests'];
  // Since t1, a path reached the gate but none of its classes: a source no class's area holds.
  const facts = {
    classes, tree: 't2', drift: (from: string) => (from === 't1' ? ['src/Shell/Window.cs'] : null),
    reached: (): Reached => ({ whole: [], classes: [], by: new Map() }), now: Date.parse(at(12)),
  };
  const ran = (name: string, verdict = 'PASS', hour = 10): [string, ClassVerdict[]] => [name, [{ verdict, tree: 't1', at: at(hour), commit: 'c1' }]];
  const whole = Object.fromEntries(classes.map((name) => ran(name)));
  const stale: Standing = { standing: 'stale', paths: ['src/Shell/Window.cs'], records: [] };
  const last = (verdict = 'PASS', classesOf: Verdict['classes'] | null = { ran: 3, kept: 0, sweep: at(10) }, standing = stale): Last => ({
    verdict: { gate: 'desk-process', verdict, tree: 't1', at: at(10), commit: 'c1', ...(classesOf ? { classes: classesOf } : {}) }, standing,
  });

  // Its last run passed whole, every class with it; only paths no class reads changed since: it passes by its classes.
  let plan = tool.classPlan({ ...facts, verdicts: whole, last: last() });
  assert.deepEqual([plan.whole, plan.run, plan.kept.length, plan.sweep], [null, [], 3, at(10)]);
  assert.equal(plan.composed, 'its last verdict, a pass on 2026-10-22 10:00 UTC, went stale by reach alone (src/Shell/Window.cs), which reaches none of its classes');
  // So does a half whose last pass is itself composed (none ran, every class kept): its classes' own passes stand.
  plan = tool.classPlan({ ...facts, verdicts: whole, last: last('PASS', { ran: 0, kept: 3, sweep: at(10) }) });
  assert.equal(plan.whole, null);
  assert.ok(plan.composed);

  // The refusals: each runs the whole half, saying what may be hidden.
  const refused = (why: RegExp, verdicts: Record<string, ClassVerdict[]>, given: Last | null) => {
    const refusal = tool.classPlan({ ...facts, verdicts, last: given });
    assert.equal(refusal.composed, undefined);
    assert.deepEqual([refusal.run.length, refusal.kept, refusal.sweep], [3, [], null]);
    assert.match(refusal.whole ?? '', new RegExp(`^every class is kept, but ${why.source}: it runs whole for a verdict of its own$`));
  };
  // A half that failed outside a test, every class kept: a build, a host or a discovery failure stays owed.
  refused(/its last verdict \(FAIL on 2026-10-22 10:00 UTC\) failed outside a test/, whole, last('FAIL'));
  // A trx missing: the run's results are not complete, whatever the classes' own verdicts.
  refused(/its last pass \(2026-10-22 10:00 UTC\) has no results of its classes \(no trx was read\)/, whole, last('PASS', { ran: null, kept: 0, sweep: at(10) }));
  refused(/its last pass .* has no results of its classes \(no trx was read\)/, whole, last('PASS', null));
  refused(/its last pass .* has no results of its classes \(no trx was read\)/, whole, last('PASS', { ran: 0, kept: 0, sweep: at(10) }));
  // A class the census lacks: the runner ran one the census never saw.
  refused(/its last run ran N\.GhostTests, which the census lacks/, { ...whole, ...Object.fromEntries([ran('N.GhostTests')]) }, last());
  // And the converse: its last run, whole, did not run a class the census holds (its pass is from an earlier run).
  refused(/its last run, whole, did not run N\.LonelyTests, which the census holds/, { ...whole, ...Object.fromEntries([ran('N.LonelyTests', 'PASS', 9)]) }, last());
  // A pass that did not go stale by reach (its tree cannot be compared), and a half with no verdict at all.
  refused(/its last pass \(2026-10-22 10:00 UTC\) did not go stale by reach/, whole, last('PASS', { ran: 3, kept: 0, sweep: at(10) }, { standing: 'stale', paths: [], records: [] }));
  refused(/the half has no verdict of its own/, whole, null);

  // A class run by reach in the last run counts only for the census it agrees with; one kept then is not asked to have run.
  assert.equal(tool.composeRefusal({ classes, verdicts: { ...whole, ...Object.fromEntries([ran('N.LonelyTests', 'PASS', 9)]) }, last: last('PASS', { ran: 2, kept: 1, sweep: at(9) }) }), null);

  // The sweep is unchanged: past its bound the half runs whole, however its last pass went stale.
  plan = tool.classPlan({ ...facts, verdicts: whole, last: last(), now: Date.parse(at(10)) + tool.SWEEP_BOUND_MS + 60_000 });
  assert.match(plan.whole ?? '', /a sweep runs the whole half/);
  // A class a change reaches still runs, and the rest are kept: the narrowed rule asks only of a half with nothing to run.
  plan = tool.classPlan({ ...facts, reached: () => ({ whole: [], classes: ['N.BetaTests'], by: new Map([['N.BetaTests', 'src/Shell/Window.cs']]) }), verdicts: whole, last: last() });
  assert.deepEqual([plan.whole, plan.run.map((each) => each.name), plan.kept.length, plan.composed], [null, ['N.BetaTests'], 2, undefined]);
});


test("CISCOPE1b: f296996's git half replayed: 73c1fe0 failed on cli and driver-process, its git half ran whole and passed, so at f296996 it composes and runs none of its 86 classes", () => {
  // The records of 2026-10-23, as `local/gate-verdicts.json` and `local/class-verdicts.json` held them when f296996's run began:
  // 73c1fe0's git half ran whole (86 classes, 1,966 s) and passed; its run failed on `cli` and `driver-process`
  // (`DriverHostTests.A_bare_invocation_prints_the_usage_and_starts_nothing`). f296996 changed four paths since: three records
  // and `DriverHostTests.cs`, which the lane table sends to the driver's Process gates and no git-half class reads.
  const [from, to] = ['c7293367a5be4b906346b7b6458d5ab57e5330c4', '1b70f53efb1b69c99463f1a1db44d8e69f0fc031'];
  const at = '2026-10-23T00:32:12Z';
  const classes = Array.from({ length: 86 }, (_, i) => `Daoris.Desktop.Driver.Tests.Git${String(i).padStart(2, '0')}Tests`);
  const changed = ['TASKS.md', 'docs/decisions/D204.md', 'docs/index/decisions.md', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/DriverHostTests.cs'];
  const judging: Judging = {
    tree: to, drift: (tree: string) => (tree === from ? changed : null), isRecord: (path: string) => !path.startsWith('src/'),
    reaching: (_gate: string, paths: string[]) => paths.filter((path) => path.startsWith('src/Daoris.Desktop/')),
  };
  const verdicts: Verdict[] = [
    { gate: 'driver-process', verdict: 'FAIL', tree: from, at: '2026-10-23T00:24:43Z', commit: '73c1fe0', classes: { ran: 114, kept: 0, sweep: '2026-10-23T00:24:43Z' } },
    { gate: 'driver-process-git', verdict: 'PASS', tree: from, at, commit: '73c1fe0', classes: { ran: 86, kept: 0, sweep: at } },
  ];
  const last = tool.lastVerdict('driver-process-git', verdicts, judging);
  assert.deepEqual(last?.standing, { standing: 'stale', paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/DriverHostTests.cs'], records: changed.slice(0, 3) });
  assert.equal(tool.lastVerdict('driver-process', verdicts, judging)?.verdict.verdict, 'FAIL');
  assert.equal(tool.lastVerdict('modules-process', verdicts, judging), null);
  const plan = tool.classPlan({
    classes, tree: to, drift: judging.drift, last, now: Date.parse('2026-10-23T02:23:28Z'),
    verdicts: Object.fromEntries(classes.map((name) => [name, [{ verdict: 'PASS', tree: from, at, commit: '73c1fe0' }]])),
    // No git-half class reads the other half's test file: the census places it nowhere in this half.
    reached: (): Reached => ({ whole: [], classes: [], by: new Map() }),
  });
  // Zero classes run, where the run as it went ran all 86 whole for 1,888,153 ms: 31.5 minutes.
  assert.deepEqual([plan.whole, plan.run.length, plan.kept.length, plan.sweep], [null, 0, 86, at]);
  assert.match(plan.composed ?? '', /a pass on 2026-10-23 00:32 UTC, went stale by reach alone \(src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\/DriverHostTests\.cs\)/);
  assert.equal(Math.round(1_888_153 / 6_000) / 10, 31.5);
});


test('the stage refuses a Process half that kept classes on verdicts older than the sweep bound, and the CI owes it; a whole pass of any age stands', () => {
  const now = Date.parse('2026-10-23T12:00:00Z');
  const verdicts: Verdict[] = [
    // A pass that kept 75 classes, the oldest of them from 27 hours before: past the bound.
    { gate: 'driver-process-git', verdict: 'PASS', tree: 'T', at: '2026-10-23T11:00:00Z', classes: { ran: 5, kept: 75, sweep: '2026-10-22T09:00:00Z' } },
    // One whose oldest kept class is 11 hours old: within it.
    { gate: 'driver-process', verdict: 'PASS', tree: 'T', at: '2026-10-23T11:00:00Z', classes: { ran: 5, kept: 95, sweep: '2026-10-23T01:00:00Z' } },
    // The whole half, three days ago, on a tree this one differs from by paths it does not reach: a sweep stands whatever its age.
    { gate: 'modules-process', verdict: 'PASS', tree: 'S', at: '2026-10-20T11:00:00Z', classes: { ran: 19, kept: 0, sweep: '2026-10-20T11:00:00Z' } },
    // A whole pass, then a newer one that kept classes past the bound: the whole one still stands and counts.
    { gate: 'other-process', verdict: 'PASS', tree: 'S', at: '2026-10-20T11:00:00Z' },
    { gate: 'other-process', verdict: 'PASS', tree: 'T', at: '2026-10-23T10:00:00Z', classes: { ran: 1, kept: 3, sweep: '2026-10-21T10:00:00Z' } },
  ];
  const judging = { tree: 'T', drift: (from: string) => (from === 'S' ? ['docs/a.md'] : null), isRecord: () => false, reaching: () => [] };
  const plan = ['driver-process-git', 'driver-process', 'modules-process', 'other-process'];
  const judged = tool.stageJudgement({ ...judging, plan, verdicts, now });
  assert.deepEqual(judged.missing, ['driver-process-git']);
  const git = judged.gates[0] as Judged & { swept?: { verdict: Verdict; sweep: string } };
  assert.deepEqual([git.verdict, git.swept?.sweep, git.swept?.verdict.at], [null, '2026-10-22T09:00:00Z', '2026-10-23T11:00:00Z']);
  assert.equal(judged.gates[3]!.verdict!.tree, 'S', 'the older whole pass counts');
  assert.deepEqual(tool.stageRemedy(judged.gates), { command: 'node tools/merge-branch.mjs --rerun driver-process-git', what: 'the gate it lacks', runs: ['driver-process-git'] });
  assert.deepEqual(tool.ciPlan({ ...judging, plan, verdicts, now }).run, ['driver-process-git'], 'the CI owes it: its next run sweeps it');
  // Within the bound it passes; with no clock given, the stage reads the machine's.
  assert.equal(tool.stageJudgement({ ...judging, plan, verdicts, now: Date.parse('2026-10-23T08:00:00Z') }).passed, true);
  const old: Verdict = { gate: 'old-process', verdict: 'PASS', tree: 'T', at: '2020-01-02T00:00:00Z', classes: { ran: 1, kept: 1, sweep: '2020-01-01T00:00:00Z' } };
  assert.equal(tool.stageJudgement({ ...judging, plan: ['old-process'], verdicts: [old] }).passed, false);
  assert.equal(tool.stageJudgement({ ...judging, plan: ['old-process'], verdicts: [{ ...old, classes: { ran: 1, kept: 1, sweep: 'never' } }] }).passed, false,
    'a sweep whose time does not read is no sweep within the bound');
});


// A standing whole pass's age, before a stage only.
test('before a stage a whole Process pass standing by reach counts only within the bound, an older one behind it neither; on its own tree at any age', () => {
  const now = Date.parse('2026-10-24T12:00:00Z');
  const judging = { tree: 'T', drift: (from: string) => (from === 'T' ? [] : ['docs/a.md']), isRecord: () => false, reaching: () => [] };
  const verdicts: Verdict[] = [
    // Two whole passes on older trees, by reach alone: 30 hours and three days old.
    { gate: 'driver-process-git', verdict: 'PASS', tree: 'R', at: '2026-10-21T12:00:00Z', classes: { ran: 86, kept: 0, sweep: '2026-10-21T12:00:00Z' } },
    { gate: 'driver-process-git', verdict: 'PASS', tree: 'S', at: '2026-10-23T06:00:00Z', classes: { ran: 86, kept: 0, sweep: '2026-10-23T06:00:00Z' } },
    // A whole pass on this very tree, three days old.
    { gate: 'modules-process', verdict: 'PASS', tree: 'T', at: '2026-10-21T12:00:00Z', classes: { ran: 19, kept: 0, sweep: '2026-10-21T12:00:00Z' } },
    // A whole pass by reach, 5 hours old.
    { gate: 'driver-process', verdict: 'PASS', tree: 'S', at: '2026-10-24T07:00:00Z', classes: { ran: 100, kept: 0, sweep: '2026-10-24T07:00:00Z' } },
  ];
  const plan = ['driver-process-git', 'modules-process', 'driver-process'];
  const staged = tool.stageJudgement({ ...judging, plan, verdicts, now, ageWhole: true });
  assert.deepEqual(staged.missing, ['driver-process-git'], 'the newer aged pass, and the older one behind it, both refused');
  const aged = staged.gates[0] as Judged & { swept?: { aged?: boolean; verdict: Verdict } };
  assert.deepEqual([aged.swept?.aged, aged.swept?.verdict.tree], [true, 'S']);
  // The CI's own judgement is unchanged (a pass stands at any age), and the stage's path judges as the stage does.
  assert.equal(tool.stageJudgement({ ...judging, plan, verdicts, now }).passed, true);
  assert.deepEqual(tool.ciPlan({ ...judging, plan, verdicts, now, ageWhole: true } as Parameters<typeof tool.ciPlan>[0]).run, ['driver-process-git']);
});


test('a known-red browser spec and a known-red rehearsal fail their gates at the stage: no waiver', () => {
  const judging = { tree: 'T', drift: () => [], isRecord: () => false, reaching: () => [] };
  const verdicts: Verdict[] = [
    { gate: 'universal', verdict: 'PASS', tree: 'T', at: '2026-10-24T10:00:00Z' },
    { gate: 'web-e2e', verdict: 'FAIL', tree: 'T', at: '2026-10-24T10:00:00Z' },
    { gate: 'rehearse-family', verdict: 'FAIL', tree: 'T', at: '2026-10-24T10:00:00Z' },
  ];
  const judged = tool.stageJudgement({ ...judging, plan: ['universal', 'web-e2e', 'rehearse-family'], verdicts, ageWhole: true });
  assert.deepEqual(judged.missing, ['web-e2e', 'rehearse-family']);
});


test("the gate store keeps each gate's verdicts apart for the scoped run and the background: a working day of scoped verdicts leaves a night's and a stage's in place", () => {
  const at = (minute: number) => new Date(Date.parse('2026-10-24T00:00:00Z') + minute * 60_000).toISOString();
  const night = { gate: 'cli', verdict: 'PASS', tree: 'N', at: at(0), kind: 'nightly' };
  const stage = { gate: 'cli', verdict: 'PASS', tree: 'S', at: at(1), kind: 'stage' };
  // 127 merges' scoped verdicts on the same gate, the day the design measured.
  const day = Array.from({ length: 127 }, (_, i) => ({ gate: 'cli', verdict: 'PASS', tree: `c${i}`, at: at(10 + i), kind: 'scoped' }));
  const kept = tool.keptVerdicts([night, stage, ...day]);
  assert.deepEqual(kept.filter((each) => each.kind !== 'scoped').map((each) => each.kind), ['nightly', 'stage']);
  assert.equal(kept.filter((each) => each.kind === 'scoped').length, tool.VERDICTS_KEPT_PER_GATE);
  assert.equal(kept.at(-1)!.tree, 'c126', 'the newest of each kept, in their order');
  // Another gate's verdicts are kept apart too.
  assert.equal(tool.keptVerdicts([...day, { gate: 'web', verdict: 'PASS', tree: 'W', at: at(500) }]).filter((each) => each.gate === 'web').length, 1);
});


test("CISCOPE1d: the watch runs a night due first, then a tip with no verdict, then a due sweep, each kind once a tip, and says the overdue report as it changes", async () => {
  const runs: string[] = [];
  const said: string[] = [];
  let clock = 0;
  const due = { night: true, sweep: true };
  const watching = tool.watchCi({
    tip: () => 'a'.repeat(40),
    gated: () => runs.includes('ordinary'),
    holder: () => null,
    schedule: () => (due.night ? { kind: 'nightly', key: 'nightly 03:00', why: 'the night due has not run' }
      : !runs.includes('ordinary') ? { kind: 'ordinary', key: 'ordinary' }
        : due.sweep ? { kind: 'sweep', key: 'sweep driver-process-git', why: 'driver-process-git is due a sweep' } : null),
    report: () => (due.sweep ? ["overdue: driver-process-git's sweep"] : []),
    run: async (kind?: string) => {
      runs.push(kind ?? '');
      if (kind === 'nightly') due.night = false;
      // The sweep's build fails and leaves the half unrun, so it stays due: the watch does not start it again for this tip.
      return kind === 'sweep' ? 1 : 0;
    },
    // Twenty polls: long enough to show the due sweep is not started again on the same tip.
    stopAsked: () => clock >= 2000,
    sleep: async () => { clock += 100; },
    say: (line) => said.push(line),
    pollMs: 100,
    tickMs: 100,
    now: () => clock,
  });
  assert.equal(await watching, 0);
  assert.deepEqual(runs, ['nightly', 'ordinary', 'sweep']);
  assert.match(said.join('\n'), /the CI watch: a nightly run of aaaaaaa: the night due has not run/);
  assert.match(said.join('\n'), /the CI watch: a sweep run of aaaaaaa: driver-process-git is due a sweep/);
  assert.equal(said.filter((line) => /overdue: driver-process-git's sweep/.test(line)).length, 1, 'the report said once while it holds');
  assert.equal(tool.nightHour({ MERGE_BRANCH_CI_NIGHT_HOUR: 'off' }), null);
  assert.equal(tool.nightHour({ MERGE_BRANCH_CI_NIGHT_HOUR: '23' }), 23);
  assert.equal(tool.nightHour({}), 3);
});
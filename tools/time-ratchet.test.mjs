import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, statSync, utimesSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { GATE_START, LIMITS, checkTimes, limitFor, readJunit, readTrx, readVitest, reasonLines, reasonProblems, runStart, scanRecords, settleLoaded, slowestClasses, tighten } from './time-ratchet.mjs';
import { isLoaded, readMarks, stampLoad } from './time-load.mjs';
import { judgeByOwner, splitByOwner, unitsOf } from './time-reach.mjs';

const trx = (rows) => `<TestRun><Results>${rows.map(([name, duration, outcome = 'Passed']) =>
  `<UnitTestResult executionId="x" testName="${name}" duration="${duration}" outcome="${outcome}" />`).join('')}</Results></TestRun>`;

/** A census world of units by their source file (the dotnet class's, a node or Vitest unit's own). */
const worldOf = (files) => ({ units: new Map(Object.entries(files).map(([unit, file]) => [unit, { unit, file }])), reaches: () => true });
const DOTNET_KEY = 'D.Tests.LineAccountTests.A_recorded_pick(x: 1)';
const WEB_KEY = 'src/Web/a/Box.test.tsx::opens';
const NO_RECORD = () => null;

test('a slow test is the merge\'s only where the merge adds or changes its file, or it ran over half slower than main\'s alone record (RATCHETOWN1)', () => {
  const world = worldOf({ 'D.Tests.LineAccountTests': 'src/D.Tests/LineAccountTests.cs', 'src/Web/a/Box.test.tsx': 'src/Web/a/Box.test.tsx' });
  const found = { [DOTNET_KEY]: { ms: 54_000, limit: 30_000 }, [WEB_KEY]: { ms: 11_000, limit: 10_000 } };
  const reads = splitByOwner(found, { changed: ['src/D/Ports/Driver.cs'], world, mainMs: NO_RECORD });
  assert.deepEqual(reads.mine, {}, 'a path the class reads is not its file: the slowness is main\'s');
  assert.deepEqual(Object.keys(reads.inherited), [DOTNET_KEY, WEB_KEY]);
  assert.deepEqual(Object.keys(splitByOwner(found, { changed: ['src/D.Tests/LineAccountTests.cs'], world, mainMs: NO_RECORD }).mine), [DOTNET_KEY], 'its class file changed');
  assert.deepEqual(Object.keys(splitByOwner(found, { changed: [WEB_KEY.split('::')[0]], world, mainMs: NO_RECORD }).mine), [WEB_KEY], 'the test\'s own file');
  const slower = splitByOwner(found, { changed: [], world, mainMs: (key) => (key === DOTNET_KEY ? 40_000 : 7_000) });
  assert.deepEqual(Object.keys(slower.mine), [WEB_KEY], '11 s against main\'s 7 s alone is over half slower; 54 s against 40 s is not');
  assert.deepEqual([slower.mine[WEB_KEY].main, slower.inherited[DOTNET_KEY].main], [7_000, 40_000]);
  assert.equal(Object.keys(splitByOwner({ 'Nowhere.Unknown.Case': { ms: 1, limit: 0 } }, { changed: ['docs/x.md'], world, mainMs: NO_RECORD }).mine).length, 1, 'a test of no known file is judged');
  assert.deepEqual(unitsOf(found, world), ['D.Tests.LineAccountTests', 'src/Web/a/Box.test.tsx']);
});

test('a test the merge owns is measured again alone and its alone time decides; an inherited one is not measured; nothing over reads no census', async () => {
  const world = worldOf({ 'D.Tests.LineAccountTests': 'src/D.Tests/LineAccountTests.cs' });
  const over = (ms) => ({ over: { [DOTNET_KEY]: { ms, limit: 30_000 } }, seen: new Set([DOTNET_KEY]) });
  const run = async (scans, changed) => {
    const asked = { world: 0, measured: [] };
    const result = await judgeByOwner({ scan: () => scans.shift(), changed, mainMs: NO_RECORD, world: async () => { asked.world += 1; return world; }, measure: (units) => asked.measured.push(units) });
    return { ...asked, result };
  };
  const own = ['src/D.Tests/LineAccountTests.cs'];
  const fast = await run([over(54_000), { over: {}, seen: new Set([DOTNET_KEY]) }], own);
  assert.deepEqual(fast.measured, [['D.Tests.LineAccountTests']]);
  assert.deepEqual(fast.result.mine, {}, 'fast alone: the load was the machine\'s');
  const slow = await run([over(54_000), over(41_000)], own);
  assert.deepEqual(slow.result.mine, { [DOTNET_KEY]: { ms: 41_000, limit: 30_000, why: 'changed' } }, 'slow alone: its alone time is judged');
  const inherited = await run([over(54_000)], ['src/D/Ports/Driver.cs']);
  assert.deepEqual(inherited.measured, [], 'a test the merge does not own is not measured');
  assert.deepEqual(Object.keys(inherited.result.inherited), [DOTNET_KEY]);
  const none = await run([{ over: {}, seen: new Set() }], own);
  assert.equal(none.world, 0);
});

/**
 * The week of 2026-11-17 to 19 replayed (RATCHETOWN1's proof): the fourteen merges whose ratchet failed with a cause, 21 breaches, each
 * merge's changed paths none of the slow tests' files (as each merge's record shows), and main keeping no alone record yet. Under the
 * rule none is the merge's; a planted slowdown in a test the merge changes is the one red.
 */
test('this week\'s ratchet breaches replayed: each is inherited from main, and only a planted slowdown in a changed test fails', () => {
  const FLOW = 'tools/merge/flow.test.ts::a gate whose hang bound killed a test under load fails nothing yet and runs again alone; a hang killed alone fails';
  const census = (name) => `Daoris.Desktop.Driver.Tests.${name}`;
  const units = Object.fromEntries(['CensusReachTests', 'SharedCompileTests', 'SettledReadersCensusTests', 'CheckoutWritersCensusTests', 'ContinuedReadersCensusTests',
    'SayKeysCensusTests', 'DeclarationLiteralsCensusTests', 'LineAccountTests', 'StructureTests', 'HomePurgeEdgeTests'].map((name) => [census(name), `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/${name}.cs`]));
  const world = worldOf({ ...units, 'tools/merge/flow.test.ts': 'tools/merge/flow.test.ts' });
  const at = (name, ms) => [`${census(name)}.Case`, { ms, limit: 30_000 }];
  const flow = (ms) => [FLOW, { ms, limit: 10_000 }];
  const week = [
    [at('CensusReachTests', 69_844), at('SharedCompileTests', 33_199)],
    [at('SettledReadersCensusTests', 76_930), at('CheckoutWritersCensusTests', 78_251), at('ContinuedReadersCensusTests', 79_035), at('CensusReachTests', 77_524), at('SharedCompileTests', 68_008), at('SayKeysCensusTests', 77_972)],
    [at('DeclarationLiteralsCensusTests', 48_506)], [at('LineAccountTests', 34_465)], [flow(10_235)], [flow(11_263)], [flow(16_340)],
    [at('DeclarationLiteralsCensusTests', 54_519), at('StructureTests', 34_154)], [flow(12_078)], [flow(20_009)], [at('HomePurgeEdgeTests', 32_135)],
    [flow(12_422)], [flow(30_631)], [flow(10_184)],
  ];
  assert.equal(week.flat().length, 21);
  const changed = ['tools/merge/flow.ts', 'tools/merge/reached-run.ts', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Review/Heavy/HeavyRun.cs', 'docs/areas/driving.md'];
  for (const breaches of week) {
    const { mine, inherited } = splitByOwner(Object.fromEntries(breaches), { changed, world, mainMs: NO_RECORD });
    assert.deepEqual(checkTimes(mine, {}), [], 'none of the week\'s breaches fails the merge');
    assert.equal(Object.keys(inherited).length, breaches.length);
  }
  const planted = { ...Object.fromEntries(week[1]), [`${census('LineAccountTests')}.Planted`]: { ms: 45_000, limit: 30_000 } };
  const { mine } = splitByOwner(planted, { changed: [...changed, units[census('LineAccountTests')]], world, mainMs: NO_RECORD });
  assert.deepEqual(checkTimes(mine, {}).map((line) => line.split(' ')[0]), [`${census('LineAccountTests')}.Planted`]);
});

test('a reached command\'s record, written when a class is measured again alone, is read against its suite\'s limit', () => {
  assert.equal(limitFor('reached-driver-1.trx'), LIMITS.unit);
  assert.equal(limitFor('reached-driver-process-git-2-again.trx'), LIMITS.process);
  assert.equal(limitFor('reached-web-unit-1.json'), LIMITS.web);
  assert.equal(limitFor('reached-tool-tests-1.json'), LIMITS.tool);
  assert.equal(limitFor('tool-e2e.xml'), LIMITS.tool);
  assert.equal(limitFor('reached-tool-e2e-3.xml'), LIMITS.tool);
  assert.equal(limitFor('reached-cli-1.json'), LIMITS.toolUnit);
  const dir = join('_fixtures', 'time-ratchet-reached');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    writeFileSync(join(dir, 'reached-web-unit-1.json'), JSON.stringify({ testResults: [{ name: join(process.cwd(), 'a.test.ts'), assertionResults: [{ fullName: 'slow', duration: 12_000, status: 'passed' }] }] }));
    assert.deepEqual(Object.keys(scanRecords(dir, process.cwd()).over), ['a.test.ts::slow']);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a census record written after a late warm-up is read as taken under load; one before it is judged', () => {
  const dir = join('_fixtures', 'time-ratchet-warm-late');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    const record = join(dir, 'censuses-driver.trx');
    const mark = join(dir, 'census-warm-late.json');
    writeFileSync(record, trx([['A.B.first', '00:00:42.5000000']]));
    writeFileSync(mark, '{}\n');
    const written = statSync(record).mtimeMs / 1000;
    utimesSync(mark, written - 1, written - 1);
    assert.deepEqual(scanRecords(dir, process.cwd()).over, {});
    assert.deepEqual(Object.keys(scanRecords(dir, process.cwd()).loaded), ['A.B.first']);
    utimesSync(mark, written + 60, written + 60);
    assert.deepEqual(Object.keys(scanRecords(dir, process.cwd()).over), ['A.B.first']);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a record file is held to the limit of its kind', () => {
  assert.equal(limitFor('driver.trx'), LIMITS.unit);
  assert.equal(limitFor('driver-process-git.trx'), LIMITS.process);
  assert.equal(limitFor('web-unit.json'), LIMITS.web);
  assert.equal(limitFor('tool-tests.xml'), LIMITS.tool);
  assert.equal(limitFor('tool-unit.json'), LIMITS.toolUnit);
  assert.equal(limitFor('tool-e2e.json'), LIMITS.tool);
  assert.equal(LIMITS.toolUnit, 10_000);
});

test('the merge tool\'s Vitest reports are read, each against its own limit, under a root of either drive-letter case', () => {
  const dir = join('_fixtures', 'time-ratchet-tool-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    const report = (name, ms) => JSON.stringify({ testResults: [{ name: `D:/repo/tools/merge/${name}`, assertionResults: [{ fullName: 'slow', duration: ms, status: 'passed' }] }] });
    writeFileSync(join(dir, 'tool-unit.json'), report('flow.test.ts', 11_000));
    writeFileSync(join(dir, 'tool-e2e.json'), report('merge.e2e.ts', 110_000));
    assert.deepEqual(scanRecords(dir, 'd:\\repo').over, { 'tools/merge/flow.test.ts::slow': { ms: 11_000, limit: LIMITS.toolUnit } });
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a trx gives each executed test its milliseconds', () => {
  const tests = readTrx(trx([['A.B.one', '00:01:02.5000000'], ['A.B.skipped', '00:00:00', 'NotExecuted']]));
  assert.deepEqual(tests, [{ key: 'A.B.one', ms: 62_500 }]);
});

test('a vitest report gives each test its file-relative key', () => {
  const report = JSON.stringify({ testResults: [{ name: 'C:\\repo\\src\\a.test.ts', assertionResults: [{ fullName: 'a it', duration: 7.4, status: 'passed' }, { fullName: 'b', status: 'pending' }] }] });
  assert.deepEqual(readVitest(report, 'C:\\repo'), [{ key: 'src/a.test.ts::a it', ms: 7 }]);
});

test('a junit report keys a test under its suites', () => {
  const xml = `<testsuites><testsuite name="outer"><testcase name="leaf" time="1.5" file="/r/t.mjs"/><testsuite name="inner"><testcase name="deep" time="0.25" file="/r/t.mjs"/></testsuite></testsuite><testcase name="top" time="2" file="/r/t.mjs"/></testsuites>`;
  assert.deepEqual(readJunit(xml, '/r'), [
    { key: 't.mjs::outer > leaf', ms: 1500 },
    { key: 't.mjs::outer > inner > deep', ms: 250 },
    { key: 't.mjs::top', ms: 2000 },
  ]);
});

const WHY = 'a real git process is started for each case, and the case checks what the process wrote';
const entry = (ms, why = WHY, row) => ({ ms, why, ...(row ? { row } : {}) });

test('a test over its limit and not listed fails, one listed may run up to its time and no longer', () => {
  const found = { 'A.slow': { ms: 200_000, limit: 120_000 }, 'B.listed': { ms: 150_000, limit: 120_000 }, 'C.worse': { ms: 190_000, limit: 120_000 } };
  const problems = checkTimes(found, { 'B.listed': entry(150_000), 'C.worse': entry(180_000) });
  assert.equal(problems.length, 2);
  assert.match(problems[0], /A\.slow took 200000 ms.*takes no new test/);
  assert.match(problems[1], /C\.worse.*does not get slower/);
});

test('an entry that starts a key covers every test under it', () => {
  const found = { 't.mjs::queue > one': { ms: 90_000, limit: 5_000 }, 't.mjs::other': { ms: 90_000, limit: 5_000 } };
  assert.equal(checkTimes(found, { 't.mjs::queue': entry(100_000) }).length, 1);
});

test('an entry with no reason, a short one, or one that only repeats the test name fails, naming the entry', () => {
  const list = {
    'A.B.none': { ms: 1 },
    'A.B.short': { ms: 1, why: 'slow' },
    'A.B.Restates_the_name_of_the_test_itself': { ms: 1, why: 'Restates the name of the test itself' },
    'A.B.number': 5,
    'A.B.fine': entry(1, WHY, 'TESTSLOW1'),
  };
  const problems = reasonProblems(list);
  assert.equal(problems.length, 4);
  for (const key of ['none', 'short', 'Restates_the_name', 'number']) assert.ok(problems.some((line) => line.includes(key)), key);
  assert.deepEqual(reasonProblems({ 'A.B.fine': entry(1) }), []);
});

test('every test over its limit says its reason, or that it has none', () => {
  const found = { 'A.listed': { ms: 150_000, limit: 120_000 }, 'B.bare': { ms: 150_000, limit: 120_000 } };
  const lines = reasonLines(found, { 'A.listed': entry(150_000, WHY, 'TESTSLOW1') });
  assert.match(lines[0], /A\.listed.*150000 ms.*a real git process.*TESTSLOW1/);
  assert.match(lines[1], /B\.bare.*no reason: optimize it or say why/);
});

test('the slowest classes of a record are the top by total time, each with its reason', () => {
  const tests = [
    { key: 'N.Slow.one(a: 1)', ms: 5000 }, { key: 'N.Slow.two', ms: 4000 }, { key: 'N.Quick.one', ms: 10 }, { key: 'f.test.ts::suite a', ms: 7000 }, { key: 'f.test.ts::suite b', ms: 1000 },
  ];
  const top = slowestClasses(tests, 2, { 'N.Slow': entry(5000, WHY, 'TESTSLOW1') });
  assert.deepEqual(top.map((each) => [each.name, each.ms, each.count]), [['f.test.ts', 8000, 2], ['N.Slow', 9000, 2]].sort((a, b) => b[1] - a[1]));
  assert.equal(top.find((each) => each.name === 'N.Slow').row, 'TESTSLOW1');
});

test('a merge judges only the records its own gates wrote: a stale record is not read, and none written means a pass', () => {
  const dir = join('_fixtures', 'time-ratchet-since');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    const old = join(dir, 'censuses.trx');
    writeFileSync(old, trx([['C.Census', '00:00:41']]));
    const earlier = new Date(Date.now() - 3_600_000);
    utimesSync(old, earlier, earlier);
    const since = Date.now() - 60_000;
    assert.deepEqual(checkTimes(scanRecords(dir, '.', { since }).over, {}), [], "the merge ran no census, so the earlier merge's record is not its own");
    assert.equal(Object.keys(scanRecords(dir, '.').over).length, 1, 'outside a merge the records on disk are all judged');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("a test's newest record decides: a run measured again replaces an older slow one, and a newer slow one still fails", () => {
  const dir = join('_fixtures', 'time-ratchet-newest');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    const older = join(dir, 'reached-driver-process-4.trx');
    writeFileSync(older, trx([['P.loaded', '00:02:31'], ['P.slower', '00:00:10']]));
    const earlier = new Date(Date.now() - 3_600_000);
    utimesSync(older, earlier, earlier);
    writeFileSync(join(dir, 'reached-driver-process-1.trx'), trx([['P.loaded', '00:00:40'], ['P.slower', '00:02:05']]));
    assert.deepEqual(scanRecords(dir, '.').over, { 'P.slower': { ms: 125_000, limit: LIMITS.process } });
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a merge whose own census run is over its listed time fails the ratchet', () => {
  const dir = join('_fixtures', 'time-ratchet-own');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    const since = Date.now() - 60_000;
    writeFileSync(join(dir, 'censuses.trx'), trx([['C.Census', '00:00:41']]));
    const problems = checkTimes(scanRecords(dir, '.', { since }).over, { 'C.Census': entry(35_000) });
    assert.equal(problems.length, 1);
    assert.match(problems[0], /C\.Census took 41000 ms, longer than its listed 35000 ms/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('the ratchet verify runs stands aside inside a merge, and says so', () => {
  const ran = spawnSync(process.execPath, [join(import.meta.dirname, 'time-ratchet.mjs')], { encoding: 'utf8', windowsHide: true, env: { ...process.env, MERGE_GATES_RUN: '1' } });
  assert.equal(ran.status, 0);
  assert.match(ran.stdout, /stands aside inside a merge/);
});

test('the list is cut to what the records show and never grows', () => {
  const list = { 'A.stays': entry(300_000), 'B.faster': entry(300_000), 'C.unseen': entry(300_000) };
  const found = { 'A.stays': { ms: 200_000, limit: 120_000 }, 'New.slow': { ms: 999_000, limit: 120_000 } };
  const seen = new Set(['A.stays', 'B.faster', 'New.slow']);
  assert.deepEqual(tighten(found, list, seen), { 'A.stays': entry(200_000), 'C.unseen': entry(300_000) });
});

test('a class entry keeps its reason while the class runs, though no test of it is over a limit', () => {
  const list = { 'A.Class': { ...entry(120_000), class: true }, 'B.faster': entry(300_000) };
  const seen = new Set(['A.Class.one', 'A.Class.two', 'B.faster']);
  assert.deepEqual(tighten({}, list, seen), { 'A.Class': { ...entry(120_000), class: true } });
  const ranked = slowestClasses([{ key: 'A.Class.one', ms: 5000 }, { key: 'A.Class.two', ms: 4000 }], 15, list);
  assert.equal(ranked[0].reason, entry(120_000).why);
});

test('a timing taken under load is kept and never fails for time; one taken alone does', () => {
  const dir = join('_fixtures', 'time-ratchet-load-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    writeFileSync(join(dir, 'driver.trx'), trx([['U.slow', '00:00:31']]));
    writeFileSync(join(dir, 'modules.trx'), trx([['M.slow', '00:00:31']]));
    const at = statSync(join(dir, 'driver.trx')).mtimeMs;
    const marks = { 'driver.trx': { holds: 3, at }, 'modules.trx': { holds: 1, at } };
    writeFileSync(join(dir, 'load-marks.json'), JSON.stringify(marks));
    const { over, loaded, seen } = scanRecords(dir, '.');
    assert.deepEqual(Object.keys(over), ['M.slow'], 'the alone timing is judged');
    assert.deepEqual(Object.keys(loaded), ['U.slow'], 'the loaded timing is kept, marked, and judges nothing');
    assert.ok(!seen.has('U.slow'));
    assert.deepEqual(checkTimes({ ...over }, {}).length, 1);
    const later = new Date(at + 5000);
    utimesSync(join(dir, 'driver.trx'), later, later);
    assert.deepEqual(Object.keys(scanRecords(dir, '.').over).sort(), ['M.slow', 'U.slow'], 'a record written again after its mark is judged as alone');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('stampLoad marks the records of a run with its peak load, and a loaded-only test is measured again alone or said unmeasured', async () => {
  const dir = join('_fixtures', 'time-load-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    writeFileSync(join(dir, 'old.trx'), trx([['O.x', '00:00:01']]));
    const old = new Date(Date.now() - 60_000);
    utimesSync(join(dir, 'old.trx'), old, old);
    writeFileSync(join(dir, 'new.trx'), trx([['N.slow', '00:00:31']]));
    assert.deepEqual(stampLoad(dir, 4, Date.now() - 10_000), ['new.trx']);
    assert.equal(readMarks(dir)['new.trx'].holds, 4);
    assert.equal(isLoaded(readMarks(dir), 'new.trx', statSync(join(dir, 'new.trx')).mtimeMs), true);
    const world = async () => worldOf({ N: ['x'] });
    let runs = [];
    const scan = () => scanRecords(dir, '.');
    const busy = await settleLoaded({ scan, free: () => false, measure: (u) => runs.push(u), world });
    assert.deepEqual([busy.unmeasured, runs], [['N.slow'], []], 'a busy machine measures nothing and says unmeasured');
    const free = await settleLoaded({ scan, free: () => true, measure: (u) => { runs.push(u); writeFileSync(join(dir, 'reached-x-1.trx'), trx([['N.slow', '00:00:05']])); }, world });
    assert.deepEqual(runs, [['N']]);
    assert.deepEqual([free.unmeasured, Object.keys(free.scanned.over)], [[], []], 'measured alone, it is judged by that time');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('inside a run, only the records the run wrote are judged; the tree\'s older slow ones are never a red', () => {
  const dir = join('_fixtures', 'time-ratchet-run-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    assert.equal(runStart(dir), null, 'with no marker every record is judged');
    writeFileSync(join(dir, 'stale.trx'), trx([['S.stale', '00:00:57']]));
    const old = new Date(Date.now() - 3_600_000);
    utimesSync(join(dir, 'stale.trx'), old, old);
    writeFileSync(join(dir, 'run-start.json'), JSON.stringify({ startedAt: Date.now() - 60_000 }));
    writeFileSync(join(dir, 'fresh.trx'), trx([['F.fine', '00:00:01']]));
    const since = runStart(dir);
    assert.ok(since !== null);
    assert.deepEqual(scanRecords(dir, '.', { since }).over, {}, 'the stale record is not read');
    assert.deepEqual(Object.keys(scanRecords(dir, '.').over), ['S.stale'], 'without the run start it is');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a gate run with no run start of its own judges only the records since the gate run began', () => {
  const dir = join('_fixtures', 'time-ratchet-gate-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  try {
    writeFileSync(join(dir, 'old.trx'), trx([['S.old', '00:00:57']]));
    const old = new Date(Date.now() - 3_600_000);
    utimesSync(join(dir, 'old.trx'), old, old);
    const started = Date.now() - 60_000;
    const env = { [GATE_START]: String(started) };
    assert.equal(runStart(dir, env), started, 'the gate run\'s start bounds the judged records');
    assert.deepEqual(scanRecords(dir, '.', { since: runStart(dir, env) }).over, {}, 'the older over-budget record reads clean');
    writeFileSync(join(dir, 'new.trx'), trx([['S.new', '00:00:57']]));
    assert.deepEqual(Object.keys(scanRecords(dir, '.', { since: runStart(dir, env) }).over), ['S.new'], 'a record written after the start is red');
    writeFileSync(join(dir, 'run-start.json'), JSON.stringify({ startedAt: started + 1000 }));
    assert.equal(runStart(dir, env), started + 1000, 'the later of the two starts holds');
    assert.equal(runStart(dir, {}), started + 1000, 'the merge\'s own start alone still holds');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('records on disk are scanned by their file and an absent folder holds nothing', () => {
  const dir = join('_fixtures', 'time-ratchet-test');
  rmSync(dir, { recursive: true, force: true });
  assert.deepEqual(scanRecords(dir, '.').over, {});
  mkdirSync(dir, { recursive: true });
  try {
    writeFileSync(join(dir, 'driver.trx'), trx([['U.fast', '00:00:01'], ['U.slow', '00:00:31']]));
    writeFileSync(join(dir, 'driver-process.trx'), trx([['P.ok', '00:01:59'], ['P.slow', '00:02:01']]));
    const { over } = scanRecords(dir, '.');
    assert.deepEqual(Object.keys(over).sort(), ['P.slow', 'U.slow']);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

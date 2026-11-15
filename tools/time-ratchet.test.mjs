import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, utimesSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { LIMITS, checkTimes, limitFor, readJunit, readTrx, readVitest, reasonLines, reasonProblems, scanRecords, slowestClasses, tighten } from './time-ratchet.mjs';
import { reaches, splitByReach } from './time-reach.mjs';

const trx = (rows) => `<TestRun><Results>${rows.map(([name, duration, outcome = 'Passed']) =>
  `<UnitTestResult executionId="x" testName="${name}" duration="${duration}" outcome="${outcome}" />`).join('')}</Results></TestRun>`;

test('a slow test no path of the merge reaches is not the merge\'s, and one a path reaches is', () => {
  const files = ['src/D/Tests/LineAccountTests.cs', 'src/D/Tests/Other.cs', 'src/D/Line/LineAccount.cs', 'src/D/Line/Unrelated.cs', 'src/Web/a/Box.test.tsx'];
  const found = {
    'D.Tests.LineAccountTests.A_recorded_pick(x: 1)': { ms: 54_000, limit: 30_000 },
    'src/Web/a/Box.test.tsx::opens': { ms: 11_000, limit: 10_000 },
  };
  const unrelated = splitByReach(found, ['docs/areas/x.md', 'src/D/Line/Unrelated.cs'], files);
  assert.deepEqual(Object.keys(unrelated.others).sort(), Object.keys(found).sort(), 'no path reaches either');
  assert.deepEqual(unrelated.mine, {});
  assert.deepEqual(Object.keys(splitByReach(found, ['src/D/Line/LineAccount.cs'], files).mine), ['D.Tests.LineAccountTests.A_recorded_pick(x: 1)'], 'the class\'s subject');
  assert.deepEqual(Object.keys(splitByReach(found, ['src/Web/a/Box.test.tsx'], files).mine), ['src/Web/a/Box.test.tsx::opens'], 'the test\'s own file');
  assert.equal(Object.keys(splitByReach(found, ['package.json'], files).mine).length, 2, 'a file every test reads');
  assert.equal(reaches('Nowhere.Unknown.Case', ['docs/x.md'], files), true, 'a test whose file is not found is judged');
});

test('inside a merge the tool names a slow test the merge does not reach and fails only on one it does', () => {
  const dir = join('_fixtures', 'time-ratchet-reach');
  let kept = null;
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'tools'), { recursive: true });
  try {
    const file = 'tools/time-ratchet.test.mjs';
    const records = join('_fixtures', 'test-timing');
    mkdirSync(records, { recursive: true });
    const record = join(records, 'tool-unit.json');
    kept = existsSync(record) ? readFileSync(record) : null;
    writeFileSync(record, JSON.stringify({ testResults: [{ name: join(process.cwd(), file), assertionResults: [{ fullName: 'slow one', duration: 99_000, status: 'passed' }] }] }));
    const run = (paths) => spawnSync(process.execPath, ['tools/time-ratchet.mjs', '--since', String(Date.now() - 60_000)], { encoding: 'utf8', env: { ...process.env, MERGE_GATES_RUN: '', DAORIS_RATCHET_CHANGED: JSON.stringify(paths) } });
    const unrelated = run(['docs/areas/x.md']);
    assert.equal(unrelated.status, 0, unrelated.stderr);
    assert.match(unrelated.stdout, /slow, not this merge's: tools\/time-ratchet\.test\.mjs::slow one took 99000 ms/);
    const reached = run([file]);
    assert.equal(reached.status, 1, 'a test the merge reaches is judged as before');
    assert.match(reached.stderr, /the list takes no new test/);
  } finally {
    const record = join('_fixtures', 'test-timing', 'tool-unit.json');
    if (kept === null) rmSync(record, { force: true });
    else writeFileSync(record, kept);
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

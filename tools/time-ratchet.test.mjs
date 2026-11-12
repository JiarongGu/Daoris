import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { LIMITS, checkTimes, limitFor, readJunit, readTrx, readVitest, scanRecords, tighten } from './time-ratchet.mjs';

const trx = (rows) => `<TestRun><Results>${rows.map(([name, duration, outcome = 'Passed']) =>
  `<UnitTestResult executionId="x" testName="${name}" duration="${duration}" outcome="${outcome}" />`).join('')}</Results></TestRun>`;

test('a record file is held to the limit of its kind', () => {
  assert.equal(limitFor('driver.trx'), LIMITS.unit);
  assert.equal(limitFor('driver-process-git.trx'), LIMITS.process);
  assert.equal(limitFor('web-unit.json'), LIMITS.web);
  assert.equal(limitFor('tool-tests.xml'), LIMITS.tool);
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

test('a test over its limit and not listed fails, one listed may run up to its time and no longer', () => {
  const found = { 'A.slow': { ms: 200_000, limit: 120_000 }, 'B.listed': { ms: 150_000, limit: 120_000 }, 'C.worse': { ms: 190_000, limit: 120_000 } };
  const problems = checkTimes(found, { 'B.listed': 150_000, 'C.worse': 180_000 });
  assert.equal(problems.length, 2);
  assert.match(problems[0], /A\.slow took 200000 ms.*takes no new test/);
  assert.match(problems[1], /C\.worse.*does not get slower/);
});

test('an entry that starts a key covers every test under it', () => {
  const found = { 't.mjs::queue > one': { ms: 90_000, limit: 5_000 }, 't.mjs::other': { ms: 90_000, limit: 5_000 } };
  assert.equal(checkTimes(found, { 't.mjs::queue': 100_000 }).length, 1);
});

test('the list is cut to what the records show and never grows', () => {
  const list = { 'A.stays': 300_000, 'B.faster': 300_000, 'C.unseen': 300_000 };
  const found = { 'A.stays': { ms: 200_000, limit: 120_000 }, 'New.slow': { ms: 999_000, limit: 120_000 } };
  const seen = new Set(['A.stays', 'B.faster', 'New.slow']);
  assert.deepEqual(tighten(found, list, seen), { 'A.stays': 200_000, 'C.unseen': 300_000 });
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

import { test } from 'vitest';
import assert from 'node:assert/strict';
import { StepClock, builtLine, outsideTests, parseSteps, stepsLine } from './reached-steps.ts';
import { runStartOf } from './reached-reports.ts';

/** A clock whose time moves only when told. */
const manual = () => {
  let at = 1000;
  return { now: () => at, move: (ms: number) => { at += ms; } };
};

test('each step says a line as it starts and as it ends, a suite discovery is carved from its run, and the log reads back to the same steps', async () => {
  const time = manual();
  const said: string[] = [];
  const clock = new StepClock((line) => said.push(line), time.now);
  await clock.time('census', () => time.move(2000));
  await clock.time('build Driver.Tests', () => time.move(60_000));
  await clock.time('run driver', () => { time.move(30_000); return 0; }, () => ({ step: 'discovery driver', ms: 12_000 }));
  await clock.time('run driver', () => time.move(1000), () => null);
  assert.deepEqual(clock.steps(), [
    { step: 'census', ms: 2000 }, { step: 'build Driver.Tests', ms: 60_000 }, { step: 'discovery driver', ms: 12_000 }, { step: 'run driver', ms: 19_000 },
  ], 'a step run again adds to its clock');
  assert.equal(said[0], 'reached-step: {"step":"census","at":0}');
  const read = parseSteps(said.join('\n'));
  assert.deepEqual(new Map(read.map((each) => [each.step, each.ms])), new Map(clock.steps().map((each) => [each.step, each.ms])));
  assert.equal(outsideTests(clock.steps()), 74_000, 'every step but a suite run is outside the tests');
  assert.equal(stepsLine(clock.steps()), 'census 2 s, build Driver.Tests 1 m 0 s, discovery driver 12 s, run driver 19 s');
});

test("a .NET suite's discovery ends where its first test starts: xUnit's results say their report's moment, so their end less their duration", () => {
  const trx = `<TestRun><Times creation="2026-11-17T17:09:59.2+11:00" start="2026-11-17T17:09:36.3688666+11:00" finish="2026-11-17T17:09:59.6+11:00" />
    <UnitTestResult testId="a" duration="00:00:00.2275287" startTime="2026-11-17T17:09:59.3618143+11:00" endTime="2026-11-17T17:09:59.3618145+11:00" outcome="Passed" />
    <UnitTestResult testId="b" duration="00:00:03.0000000" startTime="2026-11-17T17:09:59.5000000+11:00" endTime="2026-11-17T17:09:59.5000001+11:00" outcome="Passed" /></TestRun>`;
  assert.equal(runStartOf(trx), Date.parse('2026-11-17T17:09:56.500+11:00'), 'the earliest start any result shows');
  assert.equal(runStartOf('<TestRun><Times start="2026-11-17T17:09:36.3688666+11:00" /></TestRun>'), Date.parse('2026-11-17T17:09:36.368+11:00'), 'no result: the run\'s own start');
  assert.equal(runStartOf('<TestRun />'), null);
});

test('a log cut in a step names it open, timed to where the gate ended; with no end known it is still named', () => {
  const log = [
    'reached: driver: 40 reached units run',
    'reached-step: {"step":"census","at":0}',
    'reached-step: {"step":"census","at":0,"ms":45000}',
    'reached-step: {"step":"build Daoris.Desktop.Driver.Tests","at":46000}',
    '  Daoris.Contracts -> bin/Daoris.Contracts.dll',
  ].join('\n');
  assert.deepEqual(parseSteps(log, 300_000), [{ step: 'census', ms: 45_000 }, { step: 'build Daoris.Desktop.Driver.Tests', ms: 254_000, open: true }]);
  assert.match(stepsLine(parseSteps(log, 300_000)), /build Daoris\.Desktop\.Driver\.Tests 4 m 14 s \(running when it ended\)$/);
  assert.deepEqual(parseSteps(log).at(-1), { step: 'build Daoris.Desktop.Driver.Tests', ms: 0, open: true });
  assert.deepEqual(parseSteps('reached-step: {not json\nreached-step: {"at":3}'), [], 'a line it cannot read is passed over');
});

test('the builds-done line is its own machine line: no step, so no clock reads it as time and parseSteps passes it over', () => {
  assert.equal(builtLine(), 'reached-built: {}');
  assert.deepEqual(parseSteps(builtLine()), []);
});

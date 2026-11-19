import assert from 'node:assert/strict';
import test from 'node:test';
import { breachLine, breachLines, domainOf, gateBreach, hangBreaches, hungUnderLoad, parseBreaches, ratchetBreaches, sayBreach, separations, sourceReader, testBreach } from './time-why.mjs';

const PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests';
const cls = (name, body, trait = '') => `namespace Daoris.Desktop.Driver.Tests;\n\n${trait}public sealed class ${name}\n{\n    [Fact]\n    public async Task Runs()\n    {\n        ${body}\n    }\n}\n`;

/** Six planted classes, one per cause a test can show, and one that shows none. */
const PLANTED = {
  [`${PROJECT}/Workplace/StartsGitTests.cs`]: cls('StartsGitTests', 'using var p = Process.Start("git", "status");'),
  [`${PROJECT}/Store/RealHomeTests.cs`]: cls('RealHomeTests', 'var home = Path.Combine(Path.GetTempPath(), "x");'),
  [`${PROJECT}/Service/ListensTests.cs`]: cls('ListensTests', 'var listener = new TcpListener(IPAddress.Loopback, 0);'),
  [`${PROJECT}/Loop/WaitsTests.cs`]: cls('WaitsTests', 'await Task.Delay(2000);'),
  [`${PROJECT}/Census/ScansTests.cs`]: cls('ScansTests', 'var all = SourceFiles.All("*.cs");'),
  [`${PROJECT}/Loop/LoadedTests.cs`]: cls('LoadedTests', 'Assert.True(true);'),
  [`${PROJECT}/Grid/CpuTests.cs`]: cls('CpuTests', 'var sum = Enumerable.Range(0, 10).Sum();'),
};
const reader = sourceReader('.', { files: Object.keys(PLANTED), read: (path) => PLANTED[path] });
const planted = (name, over = {}) => {
  const key = `Daoris.Desktop.Driver.Tests.${name}.Runs`;
  return testBreach({ key, ms: 45_000, limit: 30_000, fails: true, record: 'reached-driver-1.trx', source: reader(key), ...over });
};

test('six planted breaches, one per cause, each classed right with its fix path', () => {
  const found = {
    process: planted('StartsGitTests'), disk: planted('RealHomeTests'), network: planted('ListensTests'), clock: planted('WaitsTests'),
    scan: planted('ScansTests'), load: planted('LoadedTests', { loaded: true }),
  };
  for (const [cause, breach] of Object.entries(found)) assert.equal(breach.cause, cause, `${breach.name} is classed ${cause}: ${breach.evidence}`);
  assert.equal(found.process.evidence, '1 process start');
  assert.equal(found.process.fix, 'UNITPURE1', 'a unit test that starts a process moves behind a fake');
  assert.equal(found.disk.fix, 'UNITPURE1');
  assert.equal(found.clock.fix, 'UNITPURE1');
  assert.equal(found.scan.fix, 'CENSUSDIFF1', 'a source scan is judged on the diff at a merge');
  assert.match(found.load.fix, /^none/, 'load has no fix: it is measured again alone');
  assert.equal(found.process.domain, 'Daoris.Desktop.Driver.Tests/Workplace');
});

test('of two files one class names, the source is the one under the project its namespace names; neither, none', () => {
  const files = ['src/D/Daoris.Desktop.Modules.Tests/X/SameTests.cs', 'src/D/Daoris.Desktop.Driver.Tests/Y/SameTests.cs'];
  const read = sourceReader('.', { files, read: (path) => path });
  assert.equal(read('Daoris.Desktop.Driver.Tests.SameTests.a')?.file, files[1]);
  assert.equal(read('Other.Tests.SameTests.a'), null);
});

test('a class that shows no cause, or whose source is not found, is unclassed, never a guess', () => {
  assert.equal(planted('CpuTests').cause, 'unclassed');
  assert.equal(planted('CpuTests').fix, 'TESTSLOW1');
  const missing = planted('NoSuchTests');
  assert.equal(missing.cause, 'unclassed');
  assert.match(missing.evidence, /no source found/);
});

test('a test of the Process category is a process by its category, whatever it reads, and its fix is its domain\'s', () => {
  const key = 'Daoris.Desktop.Driver.Tests.RealGitTests.Runs';
  const text = cls('RealGitTests', 'await Task.Delay(100);', '[Trait(Category.Name, Category.Process)]\n');
  const source = { file: `${PROJECT}/Workplace/RealGitTests.cs`, text };
  assert.equal(testBreach({ key, ms: 1, limit: 0, fails: false, source }).cause, 'process');
  assert.equal(testBreach({ key, ms: 1, limit: 0, fails: false, source, record: 'reached-driver-process-git-1.trx' }).fix, 'LAYERS1 f');
  const spec = testBreach({ key: 'tools/merge/merge.e2e.ts::lands', ms: 1, limit: 0, fails: true, source: { file: 'tools/merge/merge.e2e.ts', text: 'it()' } });
  assert.deepEqual([spec.cause, spec.fix], ['process', 'E2EFAST1']);
});

test('a node test is read by its own words: a spawn, a timer without fake timers', () => {
  const at = (text) => testBreach({ key: 'tools/a.test.mjs::x', ms: 1, limit: 0, fails: false, source: { file: 'tools/a.test.mjs', text } }).cause;
  assert.equal(at("spawnSync(process.execPath, ['x']);"), 'process');
  assert.equal(at('await new Promise((done) => setTimeout(done, 50));'), 'clock');
  assert.equal(at('vi.useFakeTimers();\nsetTimeout(done, 50);'), 'unclassed');
  assert.equal(at('// spawnSync(x) in a comment\nconst a = 1;'), 'unclassed');
});

test('a gate killed at its budget under load is classed load; alone, by its longest step, else its category', () => {
  const loaded = gateBreach({ gate: 'reached', ms: 300_000, budget: 300_000, peak: 4, fails: false });
  assert.deepEqual([loaded.cause, loaded.fails], ['load', false]);
  assert.match(loaded.evidence, /4 heavy runs/);
  const alone = gateBreach({ gate: 'reached', ms: 300_000, budget: 300_000, peak: 1, fails: true, steps: [{ step: 'census', ms: 90_000 }, { step: 'build Driver.Tests', ms: 200_000, open: true }] });
  assert.deepEqual([alone.cause, alone.fix], ['process', 'REACHCOST1']);
  assert.match(alone.evidence, /build Driver\.Tests 200 s, running when it was killed/);
  assert.equal(gateBreach({ gate: 'censuses', ms: 1, budget: 1, steps: [{ step: 'census', ms: 5 }], fails: true }).cause, 'scan');
  assert.equal(gateBreach({ gate: 'driver-process-git', ms: 1, budget: 1, fails: true }).fix, 'LAYERS1 f');
  assert.equal(gateBreach({ gate: 'cli', ms: 1, budget: 1, fails: true }).cause, 'unclassed', 'no step clocks and no category: unclassed');
});

test('three classes of one domain over by one cause make it a separation candidate; two do not', () => {
  const of = (name, domain, cause) => ({ kind: 'test', name: `${name}.a`, unit: name, domain, cause, ms: 1, limit: 0, evidence: '', fix: '', fails: false });
  const loop = [of('A', 'X.Tests/Loop', 'clock'), of('B', 'X.Tests/Loop', 'clock'), of('C', 'X.Tests/Loop', 'clock'), of('C', 'X.Tests/Loop', 'clock')];
  assert.deepEqual(separations([...loop, of('D', 'X.Tests/Git', 'process'), of('E', 'X.Tests/Git', 'process')]), [{ domain: 'X.Tests/Loop', cause: 'clock', classes: ['A', 'B', 'C'], fix: 'COMPONENT1' }]);
  assert.deepEqual(separations([of('A', 'Y', 'load'), of('B', 'Y', 'load'), of('C', 'Y', 'load')]), [], 'load is never a candidate');
  assert.equal(domainOf('tools/merge/flow.test.ts'), 'tools/merge');
  assert.equal(domainOf(`${PROJECT}/Top.cs`), 'Daoris.Desktop.Driver.Tests');
});

test('a test the blame collector killed at its hang timeout is a breach of that timeout, classed by its source', () => {
  const log = [
    'The active test run was aborted. Reason: Test host process crashed',
    "Data collector 'Blame' message: The specified inactivity time of 60 seconds has elapsed. Collecting hang dumps from testhost and its child processes.",
    'The active Test Run was aborted because the host process exited unexpectedly.',
    'The test running when the crash occurred: ',
    'Daoris.Desktop.Driver.Tests.WaitsTests.Runs',
    '',
    'This test may, or may not be the source of the crash.',
  ].join('\r\n');
  const [hang, ...more] = hangBreaches(log, reader);
  assert.deepEqual(more, []);
  assert.deepEqual([hang.kind, hang.name, hang.ms, hang.cause, hang.fails], ['hang', 'Daoris.Desktop.Driver.Tests.WaitsTests.Runs', 60_000, 'clock', true]);
  const [loaded] = hangBreaches(log, reader, 3);
  assert.deepEqual([loaded.cause, loaded.fails], ['load', false], 'a kill under load is the load\'s: nothing fails, the gate runs again alone');
  assert.match(loaded.evidence, /killed by the hang timeout; 3 heavy runs under way at the gate's peak: run again alone/);
  const [again] = hangBreaches(log, reader, 3, { alone: true });
  assert.deepEqual([again.cause, again.fails], ['clock', true], 'a kill in the run again alone is the test\'s own');
  assert.equal(hungUnderLoad([loaded]), true);
  assert.equal(hungUnderLoad([hang, again]), false);
  assert.match(breachLines([hang])[1], /WaitsTests\.Runs hung 60 s, its hang timeout 60 s: clock/);
  assert.equal(hangBreaches('5 minutes have passed', reader).length, 0, 'no hang said, none read');
  assert.equal(hangBreaches("The specified inactivity time of 5 minutes has elapsed.\nThe tests running when the crash occurred:\nA.B.c\nA.B.d(x: 1, y: 2)\n\n", () => null)[1].ms, 300_000);
});

test('each ratchet test over its limit is a breach: an unlisted one fails, a listed one within its time and one no path reaches do not, load is load', () => {
  const breaches = ratchetBreaches({
    found: { 'Daoris.Desktop.Driver.Tests.WaitsTests.Runs': { ms: 40_000, limit: 30_000 }, 'Daoris.Desktop.Driver.Tests.ScansTests.Runs': { ms: 35_000, limit: 30_000 } },
    inherited: { 'B.Tests.Far.c': { ms: 31_000, limit: 30_000, main: 29_000 } },
    loaded: { 'C.Tests.Busy.d': { ms: 50_000, limit: 30_000 } },
    listedAt: (key) => (key.includes('ScansTests') ? 36_000 : undefined),
    records: () => new Map([['Daoris.Desktop.Driver.Tests.WaitsTests.Runs', 'reached-driver-1.trx']]),
    source: reader,
  });
  assert.deepEqual(breaches.map((each) => [each.name.split('.').at(-2), each.cause, each.fails]), [
    ['WaitsTests', 'clock', true], ['ScansTests', 'scan', false], ['Far', 'unclassed', false], ['Busy', 'load', false],
  ]);
  assert.match(breaches[1].evidence, /listed at 36000 ms/);
  assert.match(breaches[2].evidence, /inherited from main: the merge does not change its file, and it ran within half again main's alone record, 29000 ms/);
  assert.deepEqual(breaches.map((each) => each.inherited === true), [false, false, true, false]);
  assert.match(breachLine(breaches[2]), /main's known red, failing nothing$/);
  assert.deepEqual(ratchetBreaches({ found: {} }), [], 'nothing over, nothing read');
});

test('a breach said as a machine line reads back, and the summary names its cause, failing ones first', () => {
  const breach = planted('WaitsTests');
  const log = `noise\n${sayBreach(breach)}\ntime-breach: not json\n  ${sayBreach(planted('LoadedTests', { loaded: true, fails: false }))}\n`;
  const read = parseBreaches(log);
  assert.deepEqual(read.map((each) => each.cause), ['clock', 'load']);
  const lines = breachLines(read, undefined, { file: 'local/x/breaches.json' });
  assert.match(lines[0], /time breaches: 2, 1 failing the run; each with its cause in local\/x\/breaches\.json/);
  assert.match(lines[1], /WaitsTests\.Runs took 45 s, its limit 30 s: clock \(1 wall-clock read or wait\); fix: UNITPURE1; it fails the run/);
  assert.deepEqual(breachLines([]), []);
});

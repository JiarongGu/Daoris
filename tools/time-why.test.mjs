import assert from 'node:assert/strict';
import test from 'node:test';
import { breachLines, domainOf, gateBreach, parseBreaches, sayBreach, separations, sourceReader, testBreach } from './time-why.mjs';

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

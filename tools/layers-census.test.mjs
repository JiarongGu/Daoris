import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { FIXTURE_HOME } from './merge/fixture-home.ts';
import { join } from 'node:path';
import { budgetBreaches, checkBudgetAlone, overBudgetOf } from './layers-census/budget-alone.mjs';
import {
  census, checkAmbient, checkBudget, checkClock, checkGit, checkMemory, checkProcesses, checkReferences, parseTrx, scanAmbient, scanClock,
  scanGit, scanProcesses,
} from './layers-census.mjs';
import { clockSites } from './layers-census/clock.mjs';
import { counts } from './layers-census/counts.mjs';
import { gitSites } from './layers-census/git.mjs';
import { processSites } from './layers-census/processes.mjs';

const project = (refs = [], packages = []) => `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>${packages.map((name) => `<PackageReference Include="${name}" />`).join('')}</ItemGroup>
  <ItemGroup>${refs.map((name) => `<ProjectReference Include="..\\${name}\\${name}.csproj" />`).join('')}</ItemGroup>
</Project>`;

const DESKTOP = 'src/Daoris.Desktop';
const at = (dir, name) => `${dir}/${name}/${name}.csproj`;

const tree = (projects) => ({
  files: Object.keys(projects),
  read: (path) => projects[path],
});

const clean = { packages: {}, ambient: {}, clock: {}, slowTests: {} };

test('LAYERS1: the wall clock and a real wait are counted, a clock handed or an infinite wait is not, and a comment is not', () => {
  const text = [
    'var a = DateTime.UtcNow; var b = DateTimeOffset.Now;',
    'Thread.Sleep(300);',
    'await Task.Delay(50);',
    'await Task.Delay(TimeSpan.FromSeconds(1), ct);',
    'await Task.Delay(span, clock, ct);',
    'await Task.Delay(Timeout.Infinite, ct);',
    'var c = clock ?? TimeProvider.System;',
    '// DateTime.UtcNow in a comment',
    '/// Thread.Sleep(1) in a doc line',
  ].join('\n');
  assert.equal(clockSites(text), 6);
});

test('an application or unit-test file over its clock allowance, or named by none, is a breach; one that fell is told to lower it', () => {
  const found = tree({
    [at(DESKTOP, 'Daoris.Desktop.Driver')]: project(),
    [at(DESKTOP, 'Daoris.Desktop.Driver.Tests')]: project(),
    [at(DESKTOP, 'Daoris.Desktop.Modules')]: project(),
    [`${DESKTOP}/Daoris.Desktop.Driver/Loop/A.cs`]: 'var a = DateTime.UtcNow;',
    [`${DESKTOP}/Daoris.Desktop.Driver/Loop/B.cs`]: 'var a = 1;',
    [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Loop/CTests.cs`]: 'Thread.Sleep(5); Thread.Sleep(5);',
    [`${DESKTOP}/Daoris.Desktop.Modules/Loop/D.cs`]: 'var a = DateTime.UtcNow;',
  });
  const files = scanClock(found);
  assert.deepEqual(Object.keys(files), [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Loop/CTests.cs`, `${DESKTOP}/Daoris.Desktop.Driver/Loop/A.cs`]);
  const problems = checkClock(files, { [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Loop/CTests.cs`]: 1, [`${DESKTOP}/Daoris.Desktop.Driver/Loop/B.cs`]: 1 });
  assert.equal(problems.length, 3, problems.join('\n'));
  assert.match(problems[0], /CTests\.cs reads the wall clock or waits for real 2 times, over its allowance of 1/);
  assert.match(problems[1], /A\.cs reads the wall clock or waits for real 1 times, over its allowance of 0/);
  assert.match(problems[2], /B\.cs fell to 0 from its allowance of 1/);
});

test('LAYERS1: an application or unit-test project that references an adapter is named, and a root may', () => {
  const found = tree({
    [at(DESKTOP, 'Daoris.Desktop.Driver')]: project(['Daoris.Adapters.Git']),
    [at(DESKTOP, 'Daoris.Desktop.Driver.Tests')]: project(['Daoris.Adapters.Machine']),
    [at(DESKTOP, 'Daoris.Desktop.Driver.Host')]: project(['Daoris.Adapters.Git']),
    'src/Daoris.Adapters/Daoris.Adapters.Git/Daoris.Adapters.Git.csproj': project(['Daoris.Ports']),
    'src/Daoris.Adapters/Daoris.Adapters.Git.Tests/Daoris.Adapters.Git.Tests.csproj': project(['Daoris.Adapters.Git']),
  });
  const problems = checkReferences(found, clean);
  assert.equal(problems.length, 2, problems.join('\n'));
  assert.match(problems[0], /Daoris\.Desktop\.Driver references the adapter Daoris\.Adapters\.Git/);
  assert.match(problems[1], /Daoris\.Desktop\.Driver\.Tests references the adapter Daoris\.Adapters\.Machine/);
});

test('LAYERS1: Ports references nothing, and Testing only Ports', () => {
  const found = tree({
    'src/Daoris.Ports/Daoris.Ports.csproj': project(['Daoris.Contracts'], ['Newtonsoft.Json', 'Microsoft.CodeAnalysis.BannedApiAnalyzers']),
    'src/Daoris.Testing/Daoris.Testing.csproj': project(['Daoris.Ports', 'Daoris.Desktop.Driver'], ['xunit']),
  });
  const problems = checkReferences(found, clean);
  assert.deepEqual(problems.map((each) => each.replace(/ .*/, '')), ['Daoris.Ports', 'Daoris.Ports', 'Daoris.Testing']);
  assert.match(problems[0], /references the project Daoris\.Contracts/);
  assert.match(problems[1], /takes the package Newtonsoft\.Json/);
  assert.match(problems[2], /references the project Daoris\.Desktop\.Driver/);
});

test('LAYERS1: the store\'s fake runs on SQLite and takes its package and bundle, and no other', () => {
  const found = tree({
    'src/Daoris.Testing/Daoris.Testing.csproj': project(['Daoris.Ports'], ['xunit', 'Microsoft.Data.Sqlite', 'SQLitePCLRaw.bundle_e_sqlite3', 'Newtonsoft.Json']),
  });
  const problems = checkReferences(found, clean);
  assert.equal(problems.length, 1, problems.join('\n'));
  assert.match(problems[0], /Daoris\.Testing takes the package Newtonsoft\.Json/);
});

test('LAYERS1: an application takes no package but analyzers and test libraries, but those on the list that only shrinks', () => {
  const found = tree({
    [at('src/X', 'Daoris.Desktop.Driver')]: project([], ['Microsoft.Data.Sqlite', 'Microsoft.CodeAnalysis.BannedApiAnalyzers']),
    [at('src/X', 'Daoris.Desktop.Driver.Tests')]: project([], ['xunit', 'Microsoft.NET.Test.Sdk', 'Some.Library']),
  });
  assert.equal(checkReferences(found, clean).length, 2);
  const listed = { ...clean, packages: { 'Daoris.Desktop.Driver': ['Microsoft.Data.Sqlite'], 'Daoris.Desktop.Driver.Tests': ['Some.Library'] } };
  assert.deepEqual(checkReferences(found, listed), []);
  const shrunk = { ...clean, packages: { 'Daoris.Desktop.Driver': ['Microsoft.Data.Sqlite', 'Gone.Package'], 'Daoris.Desktop.Driver.Tests': ['Some.Library'] } };
  assert.match(checkReferences(found, shrunk)[0], /Gone\.Package is on the list and no longer referenced/);
});

test('LAYERS1: the ambient reads are counted per subdomain and only tighten, at zero today', () => {
  const sources = {
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/A.cs': 'var p = AmbientPorts.Read();',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/B.cs': 'AmbientPorts.Read(); AmbientPorts.Read();',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Store/C.cs': 'nothing',
    'src/Daoris.Ports/Ambient/AmbientPorts.cs': 'class AmbientPorts {}',
    [at(DESKTOP, 'Daoris.Desktop.Driver')]: project(),
  };
  const found = scanAmbient(tree(sources));
  assert.deepEqual(found, { 'Daoris.Desktop.Driver/Loop': 3 });
  assert.match(checkAmbient(found, {})[0], /Loop reads the ambient ports 3 times, over the ratchet's 0/);
  assert.deepEqual(checkAmbient(found, { 'Daoris.Desktop.Driver/Loop': 3 }), []);
  assert.match(checkAmbient(found, { 'Daoris.Desktop.Driver/Loop': 5 })[0], /fell to 3/);
  assert.match(checkAmbient({}, { 'Daoris.Desktop.Driver/Loop': 1 })[0], /fell to 0/);
});

test('LAYERS1: a process start in the application or a unit test is held to its file\'s allowance, which only falls', () => {
  const sources = {
    [at(DESKTOP, 'Daoris.Desktop.Driver')]: project(),
    [at(DESKTOP, 'Daoris.Desktop.Driver.Tests')]: project(),
    [at('src/Daoris.Adapters', 'Daoris.Adapters.Machine')]: project(),
    [`${DESKTOP}/Daoris.Desktop.Driver/Platform/Starts.cs`]: 'var p = Process.Start(info);\n// Process.Start(said) in a comment\nvar q = new Process { StartInfo = info };',
    [`${DESKTOP}/Daoris.Desktop.Driver/Platform/Data.cs`]: 'var info = new ProcessStartInfo("git");',
    [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Platform/Probe.cs`]: 'foreach (var each in Process.GetProcessesByName("node")) { }',
    ['src/Daoris.Adapters/Daoris.Adapters.Machine/Host.cs']: 'Process.Start(info);',
  };
  const found = scanProcesses(tree(sources));

  assert.deepEqual(found, {
    [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Platform/Probe.cs`]: 1,
    [`${DESKTOP}/Daoris.Desktop.Driver/Platform/Starts.cs`]: 2,
  });
  assert.deepEqual(checkProcesses(found, found), []);
  assert.match(checkProcesses(found, {})[0], /Probe\.cs starts or finds a process 1 times, over its allowance of 0/);
  assert.match(checkProcesses({}, { [`${DESKTOP}/Daoris.Desktop.Driver/Platform/Starts.cs`]: 2 })[0], /fell to 0 from its allowance of 2/);
});

test('LAYERS1: a git start or read outside the git port in the application or a unit test is held to its file\'s allowance, which only falls', () => {
  const sources = {
    [at(DESKTOP, 'Daoris.Desktop.Driver')]: project(),
    [at(DESKTOP, 'Daoris.Desktop.Driver.Tests')]: project(),
    [at('src/Daoris.Adapters', 'Daoris.Adapters.Git')]: project(),
    [`${DESKTOP}/Daoris.Desktop.Driver/Workplace/Held.cs`]: 'var (info, _) = WorkingTree.GitStart(root, args, home);\n// GitStart(root) in a comment',
    [`${DESKTOP}/Daoris.Desktop.Driver/Workplace/Refs.cs`]: 'using LibGit2Sharp;\nvar dir = RefFiles.GitDirOf(root);\nvar marker = Path.Combine(root, ".git");',
    [`${DESKTOP}/Daoris.Desktop.Driver/Workplace/Asked.cs`]: 'var answer = await WorkingTree.GitAsync(root, ["status"], ct);',
    [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Workplace/Probe.cs`]: 'var start = GitRuns.GitStart(root, ["status"], null);',
    ['src/Daoris.Adapters/Daoris.Adapters.Git/Run.cs']: 'var start = GitStart(root, args, home); using LibGit2Sharp;',
  };
  const found = scanGit(tree(sources));

  assert.deepEqual(found, {
    [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Workplace/Probe.cs`]: 1,
    [`${DESKTOP}/Daoris.Desktop.Driver/Workplace/Held.cs`]: 1,
    [`${DESKTOP}/Daoris.Desktop.Driver/Workplace/Refs.cs`]: 3,
  });
  assert.deepEqual(checkGit(found, found), []);
  assert.match(checkGit(found, {})[0], /Probe\.cs starts git or reads its files 1 times, over its allowance of 0/);
  assert.match(checkGit({}, { [`${DESKTOP}/Daoris.Desktop.Driver/Workplace/Held.cs`]: 1 })[0], /fell to 0 from its allowance of 1/);
});

test('LAYERS1: a unit test over a second fails by name, a listed one may not slow, and a record of another tier is not read', () => {
  const records = [
    { gate: 'driver', tier: 'unit', tests: [{ class: 'Fast', test: 'a', ms: 12 }, { class: 'Slow', test: 'b', ms: 2100 }, { class: 'Listed', test: 'c', ms: 1500 }] },
    { gate: 'driver-process', tier: 'process', tests: [{ class: 'Proc', test: 'd', ms: 90000 }] },
  ];
  const problems = checkBudget(records, { slowTests: { 'Listed.c': 1500 } });
  assert.equal(problems.length, 1, problems.join('\n'));
  assert.match(problems[0], /Slow\.b took 2100 ms in the unit gate driver, over the 1000 ms budget/);
  assert.match(checkBudget(records, { slowTests: { 'Listed.c': 1400, 'Slow.b': 2100 } })[0], /Listed\.c slowed from 1400 to 1500 ms/);
  assert.match(checkBudget(records, { slowTests: { 'Listed.c': 1500, 'Slow.b': 2100, 'Fast.a': 1100 } })[0], /Fast\.a is on the slow list at 1100 ms and ran in 12/);
});

test('LAYERS1: a gate\'s trx is read as its tests with their milliseconds, and a Process gate is the process tier\'s', () => {
  const trx = `<TestRun><Results>
    <UnitTestResult executionId="1" testId="a" testName="Ns.Fast.Does_it" computerName="x" duration="00:00:00.0120000" outcome="Passed" />
    <UnitTestResult executionId="2" testId="b" testName="Ns.Theory.Row(a: &quot;x.y&quot;)" duration="00:00:02.5000000" outcome="Passed"></UnitTestResult>
  </Results></TestRun>`;
  const unit = parseTrx(trx, 'driver');
  assert.equal(unit.tier, 'unit');
  assert.deepEqual(unit.tests, [{ class: 'Ns.Fast', test: 'Does_it', ms: 12 }, { class: 'Ns.Theory', test: 'Row(a: "x.y")', ms: 2500 }]);
  assert.equal(parseTrx(trx, 'driver-process-git').tier, 'process');
  assert.match(checkBudget([unit], clean)[0], /Ns\.Theory\.Row/);
});

test('a test is judged by its latest run, so an older record of a loaded or since-fixed run does not outvote it', () => {
  const records = [
    { gate: 'service', tier: 'unit', at: 100, tests: [{ class: 'Ns.A', test: 'old_slow', ms: 9000 }, { class: 'Ns.A', test: 'both', ms: 9000 }] },
    { gate: 'reached-service-3', tier: 'unit', at: 300, tests: [{ class: 'Ns.A', test: 'both', ms: 40 }] },
    { gate: 'service-other', tier: 'unit', at: 200, tests: [{ class: 'Ns.A', test: 'both', ms: 5000 }] },
  ];
  const problems = checkBudget(records, clean);
  assert.equal(problems.length, 1, problems.join('\n'));
  assert.match(problems[0], /Ns\.A\.old_slow took 9000 ms/);
});

test('an over-budget test is a finding only if its class, measured again alone, is still over', async () => {
  const loaded = [{ gate: 'driver', tier: 'unit', at: 1, tests: [{ class: 'Ns.Loaded', test: 'a', ms: 5000 }, { class: 'Ns.Slow', test: 'b', ms: 5000 }] }];
  const world = async () => ({ units: new Map([['Ns.Loaded', { unit: 'Ns.Loaded' }], ['Ns.Slow', { unit: 'Ns.Slow' }]]) });
  const measured = [];
  const again = '<TestRun><Results><UnitTestResult testName="Ns.Loaded.a" duration="00:00:00.0300000" />'
    + '<UnitTestResult testName="Ns.Slow.b" duration="00:00:04.0000000" /></Results></TestRun>';
  const dir = join(FIXTURE_HOME, `census-alone-${process.pid}`);
  mkdirSync(dir, { recursive: true });
  try {
    const { problems } = await checkBudgetAlone({
      records: loaded, list: { slowTests: {} }, dir, world,
      measure: (units) => { measured.push(...units); writeFileSync(join(dir, 'again.trx'), again); },
    });
    assert.deepEqual(measured, ['Ns.Loaded', 'Ns.Slow']);
    assert.equal(problems.length, 1, problems.join('\n'));
    assert.match(problems[0], /Ns\.Slow\.b took 4000 ms/);
    assert.deepEqual(overBudgetOf(loaded, { slowTests: { 'Ns.Slow.b': 5000 } }), { 'Ns.Loaded.a': 5000 });
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('each unit test over the second is a breach with its cause, failing unless the slow list holds it at no less', () => {
  const records = [{ gate: 'driver', tier: 'unit', at: 1, tests: [{ class: 'Ns.WaitsTests', test: 'a', ms: 3000 }, { class: 'Ns.Listed', test: 'b', ms: 2000 }, { class: 'Ns.Fast', test: 'c', ms: 20 }] }];
  const sources = { 'Ns.WaitsTests.a': { file: 'src/X/X.Tests/Loop/WaitsTests.cs', text: 'await Task.Delay(900);' } };
  const breaches = budgetBreaches(records, { slowTests: { 'Ns.Listed.b': 2500 } }, (key) => sources[key] ?? null);
  assert.deepEqual(breaches.map((each) => [each.kind, each.name, each.cause, each.fails, each.limit]), [
    ['census', 'Ns.WaitsTests.a', 'clock', true, 1000], ['census', 'Ns.Listed.b', 'unclassed', false, 1000],
  ]);
  assert.equal(breaches[0].fix, 'UNITPURE1');
});

test('D294: a Process class\'s real waits, starts and git are not counted, and its reads of now still are', () => {
  const marked = '[Trait(Category.Name, Category.Process)]\npublic sealed class A {\n';
  const body = 'await Task.Delay(500);\nThread.Sleep(5);\nvar p = Process.Start(x);\nvar g = ".git";\nvar n = DateTimeOffset.UtcNow;\n}';
  assert.equal(clockSites(`${marked}${body}`), 1);
  assert.equal(clockSites(`public sealed class A {\n${body}`), 3);
  assert.equal(processSites(`${marked}${body}`), 0);
  assert.equal(processSites(`public sealed class A {\n${body}`), 1);
  assert.equal(gitSites(`${marked}${body}`), 0);
  assert.equal(gitSites(`public sealed class A {\n${body}`), 1);
});

test('the counts redden on a real clock read in a unit test and read no duration record', async () => {
  mkdirSync('_fixtures', { recursive: true });
  const root = mkdtempSync(join('_fixtures', 'census-counts-'));
  try {
    mkdirSync(join(root, 'tools'), { recursive: true });
    writeFileSync(join(root, 'tools', 'layers-census.json'), '{}');
    const records = join(root, '_fixtures', 'test-timing');
    mkdirSync(records, { recursive: true });
    writeFileSync(join(records, 'driver.trx'), '<TestRun><Results><UnitTestResult testName="Ns.Loaded.slow" duration="00:01:40.0000000" outcome="Passed" /></Results></TestRun>');
    const sources = {
      [at(DESKTOP, 'Daoris.Desktop.Driver.Tests')]: project(),
      [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Loop/WaitsTests.cs`]: 'public sealed class WaitsTests { void Go() { var now = DateTime.UtcNow; } }',
    };
    const quiet = { say: () => {}, warn: () => {} };
    assert.equal(counts({ root, tree: tree(sources), ...quiet }), 1, 'a clock read in a unit test is red');
    const clean = { ...sources, [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Loop/WaitsTests.cs`]: 'public sealed class WaitsTests { }' };
    assert.equal(counts({ root, tree: tree(clean), ...quiet }), 0, 'a test over the budget on disk is not the counts\' to judge');
    assert.equal(await census({ root, argv: ['--records', records], tree: tree(clean) }), 1, 'the full census still judges the budget');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('LAYERS1: no duration records is a clean run that says so', () => {
  assert.deepEqual(checkBudget([], clean), []);
});

test('LAYERS1: the service\'s application naming Lyntai\'s memory or storage types is named, and its adapter and its vectors may', () => {
  const SERVICE = 'src/Daoris.Service';
  const found = {
    files: [
      at(SERVICE, 'Daoris.Service.Core'), `${SERVICE}/Daoris.Service.Core/Knowledge/Planted.cs`, `${SERVICE}/Daoris.Service.Core/Knowledge/Search.cs`,
      'src/Daoris.Adapters/Daoris.Adapters.Memory.Lyntai/Daoris.Adapters.Memory.Lyntai.csproj',
      'src/Daoris.Adapters/Daoris.Adapters.Memory.Lyntai/LyntaiMemory.cs',
    ],
    read: (path) => (path.endsWith('.csproj') ? project()
      : path.endsWith('Planted.cs') ? 'using Lyntai.Storage.Sqlite;\n// GraphMemoryEngine in a comment is no use\nvar engine = new GraphMemoryEngine(name, store);\n'
      : path.endsWith('Search.cs') ? 'using Lyntai.Inference;\nIVectorStore vectors = new InMemoryVectorStore();\n'
      : 'using Lyntai.Storage;\nnew SqliteMemoryGraphStore(new SqliteConnectionFactory(path));\n'),
  };
  const problems = checkMemory(found);
  assert.equal(problems.length, 1, problems.join('\n'));
  assert.match(problems[0], /Knowledge\/Planted\.cs names Lyntai's memory or storage types 2 times/);
});

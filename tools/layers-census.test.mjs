import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkAmbient, checkBudget, checkClock, checkReferences, parseTrx, scanAmbient, scanClock } from './layers-census.mjs';
import { clockSites } from './layers-census/clock.mjs';

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

test('LAYERS1: an application or unit-test file over its clock allowance, or named by none, is a breach; one that fell is told to write the list', () => {
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

test('LAYERS1: no duration records is a clean run that says so', () => {
  assert.deepEqual(checkBudget([], clean), []);
});

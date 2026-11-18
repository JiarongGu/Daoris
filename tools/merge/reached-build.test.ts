import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, utimesSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { aheadOf, buildAhead, buildDecision, buildInputs, buildOnce, inputsDigest, onBuilt, outputsDigest, readStamp } from './reached-build.ts';

// test-map: strings name paths as data — a made-up tree's projects, never read from this repository

const TESTS = 'src/App/App.Tests';
const tree: Record<string, string> = {
  'Directory.Build.props': '<Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>',
  'src/App/Directory.Build.props': `<Project>
    <Import Project="$(MSBuildThisFileDirectory)..\\..\\Directory.Build.props" />
    <PropertyGroup><Release>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)..\\..\\canon\\canon.json'))</Release></PropertyGroup>
  </Project>`,
  'src/App/Directory.Packages.props': '<Project><ItemGroup><PackageVersion Include="xunit" Version="2.9.0" /></ItemGroup></Project>',
  [`${TESTS}/App.Tests.csproj`]: `<Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
      <PackageReference Include="xunit" />
      <ProjectReference Include="..\\App.Core\\App.Core.csproj" />
      <ProjectReference Include="..\\App.Host\\App.Host.csproj" ReferenceOutputAssembly="false" />
      <Compile Include="..\\..\\Shared\\Bridge\\Run.cs" Link="Bridge\\Run.cs" />
      <None Include="..\\..\\..\\fixtures\\**" CopyToOutputDirectory="PreserveNewest" />
      <None Include="$(PkgSome_Analyzer)/analyzers/*.dll" />
    </ItemGroup>
  </Project>`,
  [`${TESTS}/OneTests.cs`]: 'class OneTests {}',
  'src/App/App.Core/App.Core.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="..\\..\\Ports\\Ports.csproj" /></ItemGroup></Project>',
  'src/App/App.Core/Core.cs': 'class Core {}',
  'src/App/App.Host/App.Host.csproj': '<Project Sdk="Microsoft.NET.Sdk" />',
  'src/Ports/Ports.csproj': '<Project Sdk="Microsoft.NET.Sdk" />',
  'src/Shared/Bridge/Run.cs': 'class Run {}',
  'src/Shared/Bridge/Other.cs': 'class Other {}',
  'src/Web/page.ts': 'export {}',
  'fixtures/one.json': '{}',
  'canon/canon.json': '{"version":"1.0.0"}',
  'canon/other.md': '# other',
};
const files = Object.keys(tree);
const read = (path: string): string => tree[path] ?? '';

test("a test project's build inputs are what MSBuild reads for it: each project it references read through, the files they link, the build files above them and what those read", () => {
  const inputs = buildInputs(TESTS, files, read);
  assert.deepEqual(inputs.folders, ['fixtures', 'src/App/App.Core', 'src/App/App.Host', TESTS, 'src/Ports'],'a project built only to be started is built too, so it is an input');
  assert.deepEqual(inputs.files, ['Directory.Build.props', 'canon/canon.json', 'src/App/Directory.Build.props', 'src/App/Directory.Packages.props', 'src/Shared/Bridge/Run.cs']);
  assert.ok(!inputs.folders.includes('src/Web') && !inputs.files.includes('canon/other.md'), 'what no project file names is not an input');
});

test('the inputs digest changes with an input and only with one, and with what builds it', () => {
  const inputs = buildInputs(TESTS, files, read);
  const ids = (changed: Record<string, string> = {}) => (paths: string[]) => new Map(paths.map((path) => [path, changed[path] ?? `id-${path}`]));
  const before = inputsDigest(inputs, ids(), 'sdk-10');
  assert.equal(inputsDigest(inputs, ids(), 'sdk-10'), before, 'the same tree is the same digest');
  assert.notEqual(inputsDigest(inputs, ids({ 'src/App/App.Core': 'id-changed' }), 'sdk-10'), before, 'a referenced project changed');
  assert.notEqual(inputsDigest(inputs, ids({ 'canon/canon.json': 'id-changed' }), 'sdk-10'), before, 'a file a build file reads changed');
  assert.equal(inputsDigest(inputs, ids({ 'src/Web': 'id-changed' }), 'sdk-10'), before, 'a folder no project reads changed');
  assert.notEqual(inputsDigest(inputs, ids(), 'sdk-11'), before, 'another SDK builds it');
});

test('a project builds unless its stamp holds its current inputs and the output its build left is unchanged', () => {
  const stamp = { project: TESTS, inputs: 'in-1', outputs: 'out-1', at: '2026-11-17T00:00:00Z' };
  assert.deepEqual(buildDecision({ inputs: 'in-1', outputs: 'out-1', stamp }), { build: false, why: 'built for these inputs at 2026-11-17T00:00:00Z' });
  assert.equal(buildDecision({ inputs: 'in-2', outputs: 'out-1', stamp }).build, true, 'an input changed');
  assert.match(buildDecision({ inputs: 'in-1', outputs: 'out-2', stamp }).why, /output changed/, 'another build wrote its output since');
  assert.match(buildDecision({ inputs: 'in-1', outputs: null, stamp }).why, /no build output/);
  assert.match(buildDecision({ inputs: 'in-1', outputs: 'out-1', stamp: null }).why, /no build of it is stamped/);
});

test('a test command on a build made before it neither builds nor restores, once', () => {
  assert.deepEqual(onBuilt(['test', TESTS, '--no-build', '--filter', 'X']), ['test', TESTS, '--filter', 'X', '--no-build', '--no-restore']);
});

test('each project is restored and built once and stamped; run again on the same inputs it builds nothing, and a changed input builds it again', async () => {
  const { dir, remove } = fixtureFolder('reached-build');
  try {
    const bin = join(dir, TESTS, 'bin', 'Debug', 'net10.0');
    const calls: string[][] = [];
    const exec = (_command: string, args: string[]) => {
      calls.push(args);
      if (args[0] === 'build') {
        mkdirSync(bin, { recursive: true });
        writeFileSync(join(bin, 'App.Tests.dll'), `built ${calls.length}`);
      }
      return 0;
    };
    const steps: string[] = [];
    let inputs = 'in-1';
    const seams = { root: dir, exec, say: () => {}, time: async <T>(step: string, fn: () => T | Promise<T>) => { steps.push(step); return fn(); }, inputsOf: () => inputs, now: () => 'at-1' };
    assert.deepEqual(await buildOnce([TESTS], seams), { ready: [TESTS], failed: new Map() });
    assert.deepEqual(calls, [['restore', TESTS], ['build', TESTS, '--no-restore']]);
    assert.deepEqual(steps, ['restore App.Tests', 'build App.Tests'], 'each a step of its own on the clock');
    assert.equal(readStamp(dir, TESTS)?.outputs, outputsDigest(dir, TESTS));

    assert.deepEqual(await buildOnce([TESTS], seams), { ready: [TESTS], failed: new Map() });
    assert.equal(calls.length, 2, 'the same inputs on the same output build nothing');

    utimesSync(join(bin, 'App.Tests.dll'), new Date('2026-01-01'), new Date('2026-01-01'));
    await buildOnce([TESTS], seams);
    assert.equal(calls.length, 4, 'an output another build touched is built again');

    inputs = 'in-2';
    await buildOnce([TESTS], seams);
    assert.equal(calls.length, 6, 'a changed input is built again');

    const failing = { ...seams, inputsOf: () => 'in-3', exec: (_c: string, args: string[]) => (args[0] === 'build' ? 1 : 0) };
    const result = await buildOnce([TESTS], failing);
    assert.deepEqual(result.ready, []);
    assert.equal(result.failed.get(TESTS), 'its build failed (exit 1)');
  } finally {
    remove();
  }
});

test('the projects built beside the plan: each whose build reads a changed path and stands unbuilt for its inputs, the most classes first', () => {
  const other = 'src/Other/Other.Tests';
  const all: Record<string, string> = { ...tree, [`${other}/Other.Tests.csproj`]: '<Project Sdk="Microsoft.NET.Sdk" />' };
  const seams = { inputsOf: (project: string) => buildInputs(project, Object.keys(all), (path: string) => all[path] ?? ''), decide: () => ({ build: true, why: 'no build of it is stamped' }) };
  const units = [{ project: TESTS }, { project: other }, { project: other }, {}];
  assert.deepEqual(aheadOf(units, ['src/App/App.Core/Core.cs'], seams), [TESTS], 'a referenced project\'s file is read by its build');
  assert.deepEqual(aheadOf(units, ['src/Web/page.ts', 'canon/other.md'], seams), [], 'a path no build reads builds nothing ahead');
  assert.deepEqual(aheadOf(units, ['Directory.Build.props'], seams), [other, TESTS], 'a build file every project reads: the most classes first');
  assert.deepEqual(aheadOf(units, ['src/App/App.Core/Core.cs'], { ...seams, decide: () => ({ build: false, why: 'built for these inputs' }) }), [], 'a project built for its inputs is not built again');
});

test('a project built beside the plan is built once, restore and all, and stamped, so the run after the plan builds it no more', async () => {
  const { dir: root, remove } = fixtureFolder('reached-ahead');
  try {
    const calls: string[][] = [];
    const bin = join(root, TESTS, 'bin', 'Debug', 'net10.0');
    const seams = {
      root, say: () => undefined, time: async <T>(_step: string, fn: () => T | Promise<T>) => fn(), inputsOf: () => 'in-1',
      exec: (_command: string, args: string[]) => {
        calls.push(args);
        mkdirSync(bin, { recursive: true });
        writeFileSync(join(bin, 'App.Tests.dll'), 'x');
        return 0;
      },
    };
    await buildAhead([TESTS], seams);
    assert.deepEqual(calls, [['build', TESTS]], 'one command restores and builds it');
    assert.equal(readStamp(root, TESTS)?.inputs, 'in-1');
    assert.deepEqual(await buildOnce([TESTS], seams), { ready: [TESTS], failed: new Map() });
    assert.equal(calls.length, 1, 'the run after the plan finds it built for its inputs');
    await buildAhead([TESTS], { ...seams, inputsOf: () => 'in-2', exec: () => 1 });
    assert.equal(readStamp(root, TESTS)?.inputs, 'in-1', 'a failed build stamps nothing');
  } finally {
    remove();
  }
});

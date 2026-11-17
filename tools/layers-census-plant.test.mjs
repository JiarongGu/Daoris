import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const plant = join(root, '_fixtures', 'layers-plant');

const sources = {
  'Clean.cs': `using System.Net.Http;
class Clean { async Task Go(TimeProvider p, HttpMessageHandler h, CancellationToken ct) { using var c = new HttpClient(h); await Task.Delay(TimeSpan.FromSeconds(1), p, ct); _ = p.GetUtcNow(); } }`,
  'ProcessStart.cs': 'class ProcessStart { void Go() => System.Diagnostics.Process.Start("x"); }',
  'ProcessKill.cs': 'class ProcessKill { void Go(System.Diagnostics.Process p) => p.Kill(); }',
  'ProcessFind.cs': 'class ProcessFind { void Go() => System.Diagnostics.Process.GetProcessesByName("x"); }',
  'Sleep.cs': 'class Sleep { void Go() => Thread.Sleep(1); }',
  'Delay.cs': 'class Delay { Task Go() => Task.Delay(5); }',
  'WallClock.cs': 'class WallClock { object Go() => DateTime.UtcNow; }',
  'WallOffset.cs': 'class WallOffset { object Go() => DateTimeOffset.Now; }',
  'SystemProvider.cs': 'class SystemProvider { object Go() => TimeProvider.System; }',
  'Listener.cs': 'class Listener { object Go() => new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); }',
  'Pipe.cs': 'class Pipe { object Go() => new System.IO.Pipes.NamedPipeServerStream("x"); }',
  'BareClient.cs': 'class BareClient { object Go() => new System.Net.Http.HttpClient(); }',
  'Alias.cs': 'using P = System.Diagnostics.Process;\nclass Alias { void Go() => P.Start("x"); }',
  'Helper.cs': 'static class Helper { public static void Go() => System.Diagnostics.Process.Start("x"); }\nclass UsesHelper { void Go() => Helper.Go(); }',
  'FakeLaunches.cs': 'class FakeLaunches { public FakeLaunches() { System.Diagnostics.Process.Start("x"); } }',
};

test('LAYERS1: a process start, a kill, a sleep, the wall clock and a socket are build errors, whatever carries them', () => {
  rmSync(plant, { recursive: true, force: true });
  mkdirSync(plant, { recursive: true });
  writeFileSync(join(plant, 'Plant.csproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="${join(root, 'src', 'Layers.props').replace(/\\/g, '/')}" />
  <PropertyGroup><AssemblyName>Plant</AssemblyName></PropertyGroup>
</Project>
`);
  for (const [name, text] of Object.entries(sources)) writeFileSync(join(plant, name), text);
  const built = spawnSync('dotnet', ['build', join(plant, 'Plant.csproj'), '-nodeReuse:false', '--nologo', '-v:q'], { encoding: 'utf8', windowsHide: true, cwd: plant });
  const output = `${built.stdout}\n${built.stderr}`;
  const flagged = new Set([...output.matchAll(/([A-Za-z]+)\.cs\(\d+,\d+\): error RS0030/g)].map((m) => `${m[1]}.cs`));
  const planted = Object.keys(sources).filter((name) => name !== 'Clean.cs');
  assert.deepEqual([...flagged].sort(), planted.sort(), output);
  assert.notEqual(built.status, 0, 'the build fails');
  rmSync(plant, { recursive: true, force: true });
});

const assistant = join(root, 'src', 'Daoris.Assistant');
const slash = (path) => path.replace(/\\/g, '/');

/** A scratch project built as each of the assistant's projects is, over `Assistant.props`, with what `body` plants in it. */
function plantAssistant(folder, body, files) {
  const at = join(root, '_fixtures', 'assistant-plant', folder);
  rmSync(at, { recursive: true, force: true });
  mkdirSync(at, { recursive: true });
  writeFileSync(join(at, 'Plant.csproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="${slash(join(assistant, 'Assistant.props'))}" />
  <PropertyGroup><AssemblyName>Plant</AssemblyName></PropertyGroup>
  ${body}
</Project>
`);
  for (const [name, text] of Object.entries(files)) writeFileSync(join(at, name), text);
  const built = spawnSync('dotnet', ['build', join(at, 'Plant.csproj'), '-nodeReuse:false', '--nologo', '-v:q'], { encoding: 'utf8', windowsHide: true, cwd: at });
  rmSync(at, { recursive: true, force: true });
  return { status: built.status, output: `${built.stdout}\n${built.stderr}` };
}

test('ASSISTPKG1d: each of the assistant\'s projects builds over Assistant.props, its boundary and banned effects', () => {
  const projects = readdirSync(assistant, { withFileTypes: true }).filter((entry) => entry.isDirectory() && !entry.name.startsWith('Daoris.Assistant.Desktop'))
    .map((entry) => join(assistant, entry.name, `${entry.name}.csproj`));
  assert.deepEqual(projects.map((path) => slash(path).split('/').at(-1)).sort(), ['Daoris.Assistant.Testing.csproj', 'Daoris.Assistant.Tests.csproj', 'Daoris.Assistant.csproj']);
  for (const path of projects) assert.match(readFileSync(path, 'utf8'), /<Import Project="\.\.\\Assistant\.props" \/>/, path);
});

test('ASSISTPKG1d: a reference to the driver fails the assistant\'s build before it is restored, naming it', () => {
  const driver = slash(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Daoris.Desktop.Driver.csproj'));
  const built = plantAssistant('reference', `<ItemGroup><ProjectReference Include="${driver}" /></ItemGroup>`, { 'Clean.cs': 'class Clean { }' });
  assert.notEqual(built.status, 0, built.output);
  assert.match(built.output, /error ASSISTANT001: Plant references Daoris\.Desktop\.Driver: the assistant's package references Daoris\.Ports/, built.output);
});

test('ASSISTPKG1d: the wall clock is a build error in the assistant\'s package, and a clock handed is not', () => {
  const built = plantAssistant('clock', '', {
    'Clean.cs': 'class Clean { object Go(TimeProvider clock) => clock.GetUtcNow(); }',
    'WallClock.cs': 'class WallClock { object Go() => DateTime.UtcNow; }',
  });
  const flagged = [...new Set([...built.output.matchAll(/([A-Za-z]+)\.cs\(\d+,\d+\): error RS0030/g)].map((m) => `${m[1]}.cs`))];
  assert.deepEqual(flagged, ['WallClock.cs'], built.output);
  assert.notEqual(built.status, 0, 'the build fails');
});

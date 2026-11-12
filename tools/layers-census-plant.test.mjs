import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
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

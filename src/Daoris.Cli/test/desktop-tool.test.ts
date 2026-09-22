import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, utimesSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText, listFiles } from '../src/fsx.ts';
// The workspace's own tooling is plain `.mjs` and ships no declarations, so this import is untyped
// by construction. Suppressed at the one site rather than given a hand-written `.d.mts`, which would
// be a second description of the tool to keep in step with it — and the thing this suite asserts is
// the tool's BEHAVIOUR, which a stale declaration would not protect.
import {
  CLEARED, REDIRECTED, assemblyExe, prune, scratchEnvironment,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/desktop.mjs';

/** What `prune` takes: one capture on disk. Declared here because the tool itself is untyped. */
type Capture = { path: string; at: number; size: number };

/**
 * The desktop dev loop (`tools/desktop.mjs`) is not part of this package — it is the workspace's own
 * tooling, tested from here for the same reason `version.test.ts` asserts facts about the canon: this
 * suite is what `npm run verify` and the release workflow already run, and a seventh gate is a
 * seventh row two lists would have to agree on.
 *
 * What is asserted is the part that is silently wrong when it breaks. A dev run of the SHELL is not a
 * preview — the driver loop starts with the app and spawns real agent sessions — so a machine-local
 * file the scratch environment forgets is not a broken tool, it is the person's own Daoris being
 * edited by a test run.
 */

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = dirname(dirname(dirname(here)));

const environment = () => scratchEnvironment({
  home: '/scratch/home',
  family: '/scratch/family',
  serviceUrl: 'http://127.0.0.1:5188',
  httpHost: '/build/daoris-knowledge-http.exe',
  cdpPort: 9333,
});

/**
 * The pair-check the tool's own comment promises. Every source that builds a path under the person's
 * `~/.daoris` offers an environment override; a scratch run must either point that override somewhere
 * of its own or unset it. A NEW machine-local file is the case this exists for: it fails here, rather
 * than in somebody's real driver config three weeks later.
 */
test('every machine-local override the desktop reads is redirected, cleared, or named', () => {
  const sources = [
    join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver'),
    join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Core'),
    join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Shared'),
  ];

  const found = new Set<string>();
  for (const dir of sources) {
    for (const file of listFiles(dir)) {
      if (!file.endsWith('.cs') || file.startsWith('bin/') || file.startsWith('obj/')) continue;
      const text = readText(join(dir, file));
      // Only files that actually resolve a path under the profile's `.daoris`. Everything else names
      // variables about transport or identity, which a scratch machine has no opinion about.
      if (!text.includes('".daoris"')) continue;
      for (const [name] of text.matchAll(/DAORIS_[A-Z_]+/g)) found.add(name);
    }
  }

  assert.ok(found.size >= 4, `expected to find the machine-local variables, found ${[...found]}`);

  // Both host paths are pinned rather than redirected: their locators prefer an INSTALLED binary, so
  // an unnamed host means a scratch run silently uses the real machine's (ACP4 added the second).
  const handled = new Set([...REDIRECTED, ...CLEARED, 'DAORIS_HTTP_HOST', 'DAORIS_MCP_HOST']);
  const missed = [...found].filter((name) => !handled.has(name));
  assert.deepEqual(missed, [], `a scratch run would inherit ${missed.join(', ')} from the real machine`);
});

/** The two the service reads by constant rather than beside a `.daoris` literal. */
test('the index root and the store are redirected', () => {
  const factory = readText(join(
    repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Core', 'ServiceFactory.cs'));
  for (const match of factory.matchAll(/(?:RootVariable|DatabaseVariable)\s*=\s*"(DAORIS_[A-Z_]+)"/g)) {
    assert.ok(REDIRECTED.includes(match[1]!), `${match[1]} is not redirected by a scratch run`);
  }
});

test('a scratch run sets every redirected variable, and points the host at the workspace build', () => {
  const env = environment();
  for (const name of REDIRECTED) assert.ok(env[name], `${name} is missing from the scratch environment`);
  // ServiceHostLocator prefers an installed host over this workspace's build, so an unnamed host is a
  // dev run of yesterday's binary serving yesterday's bundle.
  assert.equal(env.DAORIS_HTTP_HOST, '/build/daoris-knowledge-http.exe');
  // The port is deliberately not 5177: HostSupervisor adopts a host already answering, so the default
  // would quietly attach a "scratch" shell to the person's real one.
  assert.ok(!env.DAORIS_SERVICE_URL?.includes('5177'));
  // The shell probes one variable and the host binds another, with nothing passing one to the other:
  // move only the probe and the window waits on the splash for a host answering elsewhere.
  assert.equal(env.ASPNETCORE_URLS, env.DAORIS_SERVICE_URL);
});

/**
 * Both halves, or neither. The runtime always sets `AdditionalBrowserArguments`, which makes WebView2
 * ignore the environment variable — so it re-appends that variable itself, and only in development
 * mode. A run that sets the port without the mode opens no port at all, silently, which is how this
 * was found: a window that started perfectly and answered nothing.
 */
test('the debug port needs the runtime in dev mode, and neither is set unasked', () => {
  const env = environment();
  assert.equal(env.WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS, '--remote-debugging-port=9333');
  assert.equal(env.DOTNET_ENVIRONMENT, 'Development');

  const quiet = scratchEnvironment({
    home: '/h', family: '/f', serviceUrl: 'http://127.0.0.1:5188', httpHost: '/x.exe',
  });
  assert.equal('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS' in quiet, false);
  assert.equal('DOTNET_ENVIRONMENT' in quiet, false);
});

/**
 * The sibling's version of this path carries the target framework as a literal, with a warning that
 * forgetting to update it launches the binary in the old folder while every change appears to do
 * nothing. Deriving it removes the thing that had to be remembered.
 */
test('the built executable is derived from the project, newest build first', () => {
  const root = mkdtempSync(join(tmpdir(), 'daoris-exe-'));
  writeFileSync(join(root, 'Thing.csproj'), '<Project><PropertyGroup>'
    + '<AssemblyName>thing</AssemblyName></PropertyGroup></Project>');

  assert.equal(assemblyExe(root), null, 'nothing built is an answer, not a path');

  mkdirSync(join(root, 'bin', 'Debug', 'net10.0-windows'), { recursive: true });
  const debug = join(root, 'bin', 'Debug', 'net10.0-windows', 'thing.exe');
  writeFileSync(debug, '');
  assert.equal(assemblyExe(root), debug);

  mkdirSync(join(root, 'bin', 'Release', 'net10.0-windows'), { recursive: true });
  const release = join(root, 'bin', 'Release', 'net10.0-windows', 'thing.exe');
  writeFileSync(release, '');
  // Explicit times: two files written in the same millisecond would make "newest" a coin toss.
  utimesSync(debug, new Date(1), new Date(1));
  utimesSync(release, new Date(2), new Date(2));
  assert.equal(assemblyExe(root), release, 'the newest build is what the person last asked for');
});

test('a project with no assembly name has no derivable executable', () => {
  const root = mkdtempSync(join(tmpdir(), 'daoris-exe-'));
  writeFileSync(join(root, 'Thing.csproj'), '<Project></Project>');
  assert.equal(assemblyExe(root), null);
});

/**
 * The capture script matches the window by process name, and the process name IS the assembly name.
 * A rename of the assembly would otherwise photograph nothing, in a loop whose whole job is to see.
 */
test('the capture script and the tool agree with the project on the process name', () => {
  const csproj = readText(join(
    repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.App', 'Daoris.Desktop.App.csproj'));
  const assembly = /<AssemblyName>([^<]+)<\/AssemblyName>/.exec(csproj)?.[1];
  assert.ok(assembly, 'the shell declares an assembly name');

  assert.ok(readText(join(repoRoot, 'tools', 'shot-window.ps1')).includes(`$ProcessName = '${assembly}'`));
  assert.ok(readText(join(repoRoot, 'tools', 'desktop.mjs')).includes(`'-ProcessName', '${assembly}'`));
});

test('a prune keeps the newest captures and drops the rest', () => {
  const entries = Array.from({ length: 30 }, (_, index) => ({ path: `${index}.png`, at: index, size: 10 }));
  const dropped = prune(entries, { keep: 25 }).map((entry: Capture) => entry.path);
  assert.equal(dropped.length, 5);
  assert.deepEqual(dropped.sort(), ['0.png', '1.png', '2.png', '3.png', '4.png']);
});

test('a prune also enforces the size cap, oldest first', () => {
  const entries = Array.from({ length: 10 }, (_, index) => ({ path: `${index}.png`, at: index, size: 10 }));
  const dropped = prune(entries, { keep: 25, maxBytes: 35 }).map((entry: Capture) => entry.path);
  // Newest first until the cap is spent: 9, 8, 7 fit in 35 bytes; everything older goes.
  assert.deepEqual(dropped.sort((a: string, b: string) => Number(a.split('.')[0]) - Number(b.split('.')[0])),
    ['0.png', '1.png', '2.png', '3.png', '4.png', '5.png', '6.png']);
});

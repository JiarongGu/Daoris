import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, utimesSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import { createServer, type Server } from 'node:net';
import { readText, listFiles } from '../src/fsx.ts';
// The workspace's own tooling is plain `.mjs` and ships no declarations, so this import is untyped
// by construction. Suppressed at the one site rather than given a hand-written `.d.mts`, which would
// be a second description of the tool to keep in step with it — and the thing this suite asserts is
// the tool's BEHAVIOUR, which a stale declaration would not protect.
import {
  CLEARED, PAGE_THEME, REDIRECTED, SHELL_ORIGIN, THEME_KEY, assemblyExe, awaitDebugPort, closedPortReport, engineLogOf,
  installEnvironment, installedExe, isShell, prune, scratchEnvironment, startedHere, withPageTheme,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/desktop.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { bindVerdictOf, freePort } from '../../../tools/cdp.mjs';
import {
  BROWSER_ARGUMENT, applicationsIn, browsersIn, isBrowserProcess, isEngineProcess, psQuote, running,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/processes.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { SHELL_EXE } from '../../../tools/desktop-publish.mjs';

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
 * Daoris home offers an environment override; a scratch run must either point that override somewhere
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
      // Only files that actually resolve a path under the Daoris home (D63) — the ones that touch
      // `DaorisHome`. Everything else names variables about transport or identity, which a scratch
      // machine has no opinion about. (Before D63 the tell was a `".daoris"` literal; there are none.)
      if (!text.includes('DaorisHome.') && !text.includes('class DaorisHome')) continue;
      for (const [name] of text.matchAll(/DAORIS_[A-Z_]+/g)) found.add(name);
    }
  }

  assert.ok(found.size >= 4, `expected to find the machine-local variables, found ${[...found]}`);
  // The home itself is the one that matters most: every other default derives from it.
  assert.ok(found.has('DAORIS_HOME') && REDIRECTED.includes('DAORIS_HOME'));

  // Both host paths are pinned rather than redirected: their locators prefer an INSTALLED binary, so
  // an unnamed host means a scratch run silently uses the real machine's (ACP4 added the second).
  // The plugin trio is WRITTEN into a hook process's environment and read by nothing here (D64 §4):
  // a scratch run has nothing to redirect, because the driver is the one setting them. The quest's
  // attachments directory is the same shape (D65 §2): written into a session's environment from what
  // the service answered, and read by nothing on this side.
  const handled = new Set([
    ...REDIRECTED, ...CLEARED, 'DAORIS_HTTP_HOST', 'DAORIS_MCP_HOST',
    'DAORIS_PLUGIN_ID', 'DAORIS_PLUGIN_FOLDER', 'DAORIS_PLUGIN_DATA',
    'DAORIS_QUEST_ATTACHMENTS',
  ]);
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
 * Both halves, or neither. The engine the shell ships (D92) opens the DevTools port the shell hands it
 * only in development, so a run that sets the port without the mode opens no port at all, silently —
 * a window that starts perfectly and answers nothing, which is how the WebView2 half of this was
 * found before it. The WebView2 variable went with WebView2 (D93).
 */
test('the debug port needs the runtime in dev mode, and neither is set unasked', () => {
  const env = environment();
  assert.equal(env.DOTNET_ENVIRONMENT, 'Development');
  assert.equal(env.DAORIS_DEVTOOLS_PORT, '9333');
  assert.equal('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS' in env, false);

  const quiet = scratchEnvironment({
    home: '/h', family: '/f', serviceUrl: 'http://127.0.0.1:5188', httpHost: '/x.exe',
  });
  assert.equal('DOTNET_ENVIRONMENT' in quiet, false);
  assert.equal('DAORIS_DEVTOOLS_PORT' in quiet, false);
});

/** Whether this process can bind `port` on `host` now. The test's own, so it does not grade the tool by itself. */
const bindsHere = (port: number, host: string) => new Promise<string>((resolve) => {
  const server = createServer();
  server.once('error', (error: NodeJS.ErrnoException) => resolve(error.code ?? 'error'));
  server.listen({ port, host, exclusive: true }, () => server.close(() => resolve('bound')));
});

const listening = (server: Server, host: string) => new Promise<number>((resolve) => {
  server.listen({ port: 0, host }, () => resolve((server.address() as { port: number }).port));
});

/**
 * LOOK4: `run` printed `debug port 9333` and nothing ever listened there, on a machine where Windows had
 * reserved 9309–9408 (`netsh interface ipv4 show excludedportrange protocol=tcp`). Nobody can bind a
 * reserved port, so the engine could not open it, and said nothing. The probe asked whether anything
 * ANSWERED there, and a reserved port answers nothing, so it read as free. Every port of the 20 the walk
 * tries sat inside the same reservation. On a machine with no reservation over 9333 this passes either
 * way; the two tests below it fail on any machine.
 */
test('a port handed out for the debug port is one this machine lets a process bind', async () => {
  const port = await freePort(9333);
  assert.equal(await bindsHere(port, '127.0.0.1'), 'bound', `port ${port} cannot be bound on 127.0.0.1`);
});

test('a port held by a listener that answers nothing is not free', async () => {
  // A raw socket server: it accepts, and never answers HTTP, so an asking probe times out and calls it free.
  const silent = createServer(() => {});
  const held = await listening(silent, '127.0.0.1');
  try {
    const port = await freePort(held, 1);
    assert.notEqual(port, held);
    assert.equal(await bindsHere(port, '127.0.0.1'), 'bound');
  } finally {
    silent.close();
  }
});

test('the walk passes a port the system refuses, and asks the system when every port it tries is refused', async () => {
  const refused = new Set([9333, 9334]);
  const isFree = async (port: number) => !refused.has(port);
  assert.equal(await freePort(9333, 3, isFree), 9335);

  // Windows reserves ports in blocks, so a whole walk can sit inside one; the system's own pick is outside it.
  const port = await freePort(9333, 2, isFree);
  assert.equal(refused.has(port), false);
  assert.ok(port > 0);
});

test('a bind refused for the port is taken, and one refused for an address the machine lacks is not', () => {
  assert.equal(bindVerdictOf('EACCES'), 'taken'); // a reserved port, Windows' WSAEACCES
  assert.equal(bindVerdictOf('EADDRINUSE'), 'taken');
  // A machine with no IPv6 loopback refuses [::1] for every port: that says nothing about this one.
  assert.equal(bindVerdictOf('EADDRNOTAVAIL'), 'absent');
  assert.equal(bindVerdictOf('EAFNOSUPPORT'), 'absent');
  assert.equal(bindVerdictOf('ESOMETHINGELSE'), 'taken');
});

/** A clock the wait reads and its sleep advances, so a 30-second bound takes no time at all. */
const fakeClock = () => {
  let at = 0;
  return { now: () => at, sleep: async (ms: number) => { at += ms; } };
};

test('run waits for the debug port and says when it answered', async () => {
  const clock = fakeClock();
  let asked = 0;
  const outcome = await awaitDebugPort({
    answers: async () => (asked += 1) >= 3, gone: () => null, limitMs: 30_000, stepMs: 500, ...clock,
  });
  assert.equal(outcome.open, true);
  assert.equal(asked, 3);
});

test('a port that never opens is reported once the bound is reached, with the window still running', async () => {
  const clock = fakeClock();
  const outcome = await awaitDebugPort({
    answers: async () => false, gone: () => null, limitMs: 30_000, stepMs: 500, ...clock,
  });
  assert.equal(outcome.open, false);
  assert.equal(outcome.gone, null);
  assert.ok(outcome.waitedMs >= 30_000);
});

test('an application that ended before its port opened is reported at once, not after the bound', async () => {
  const clock = fakeClock();
  let polls = 0;
  const outcome = await awaitDebugPort({
    answers: async () => false,
    gone: () => ((polls += 1) >= 2 ? { code: 0, signal: null } : null),
    limitMs: 30_000, stepMs: 500, ...clock,
  });
  assert.equal(outcome.open, false);
  assert.deepEqual(outcome.gone, { code: 0, signal: null });
  assert.ok(outcome.waitedMs < 30_000);
});

test('the report says the window started without its debug port, and what to try', () => {
  const text = closedPortReport({
    port: 9333, waitedMs: 30_000, gone: null, pid: 42,
    engineLog: '/install/data/chromium/cef.log', home: '/install/data',
  });
  assert.match(text, /started WITHOUT its debug port/);
  assert.match(text, /9333/);
  assert.match(text, /`shot`, `eval` and `click` cannot reach it/);
  assert.match(text, /restart/);
  assert.match(text, /\/install\/data\/chromium\/cef\.log/);
  assert.match(text, /excludedportrange/);
});

test('the report of an application that ended says so, and does not claim a window is up', () => {
  const text = closedPortReport({
    port: 9333, waitedMs: 1_500, gone: { code: 0, signal: null }, pid: 42,
    engineLog: '/install/data/chromium/cef.log', home: '/install/data',
  });
  assert.match(text, /pid 42/);
  assert.match(text, /exited 0/);
  assert.doesNotMatch(text, /started WITHOUT/);
  assert.match(text, /already running/);
  assert.match(text.replaceAll('\\', '/'), /\/install\/data\/logs/);
});

test('the engine log is under the root the run gave the application', () => {
  const sep = (path: string) => path.replaceAll('\\', '/');
  assert.equal(sep(engineLogOf({ install: '/i', real: true, exe: '/i/app/Daoris.Desktop.exe' })), '/i/data/chromium/cef.log');
  assert.equal(sep(engineLogOf({ install: null, real: true, exe: '/b/Daoris.Desktop.exe' })), '/b/data/chromium/cef.log');
  assert.match(sep(engineLogOf({ install: null, real: false, exe: '/b/Daoris.Desktop.exe' })),
    /_fixtures\/desktop\/app\/data\/chromium\/cef\.log$/);
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

/**
 * D92: on the Chromium the shell ships, the exe is CEF's launcher, named from the assembly less `.App`,
 * in a runtime identifier's folder — and copied with CEF's own file date, so it is dated by the assembly
 * it starts. Dated by itself, a stale build in the folder above would win.
 */
test('a launched assembly is found by its launcher beside it, dated by the assembly', () => {
  const root = mkdtempSync(join(tmpdir(), 'daoris-exe-'));
  writeFileSync(join(root, 'Thing.csproj'), '<Project><PropertyGroup>'
    + '<AssemblyName>thing.App</AssemblyName></PropertyGroup></Project>');
  const framework = join(root, 'bin', 'Debug', 'net10.0-windows');
  mkdirSync(join(framework, 'win-x64'), { recursive: true });
  const stale = join(framework, 'thing.exe');
  writeFileSync(stale, '');
  const launcher = join(framework, 'win-x64', 'thing.exe');
  writeFileSync(launcher, '');
  const assembly = join(framework, 'win-x64', 'thing.App.dll');
  writeFileSync(assembly, '');
  utimesSync(launcher, new Date(1), new Date(1));
  utimesSync(stale, new Date(2), new Date(2));
  utimesSync(assembly, new Date(3), new Date(3));
  assert.equal(assemblyExe(root), launcher);
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
  // On the Chromium it ships (D92) the process is CEF's launcher, named from the assembly less `.App`.
  const process = assembly.endsWith('.App') ? assembly.slice(0, -'.App'.length) : assembly;

  assert.ok(readText(join(repoRoot, 'tools', 'shot-window.ps1')).includes(`$ProcessName = '${process}'`));
  // The tool names the process by the executable it photographs, so the build's and an install's
  // launcher — `Daoris.exe`, or an older install's — are both found (CHR4).
  assert.ok(readText(join(repoRoot, 'tools', 'desktop.mjs')).includes("'-ProcessName', basename(exe, '.exe')"));
  assert.equal(`${process}.exe`, SHELL_EXE, 'the build names the application as the install does');

  // Daoris's own browser is the application's executable started with its argument since CHR8 (D99),
  // so `shot --window browser` photographs that process by its id, found by its argument; a name of
  // its own no longer tells it apart.
  const tool = readText(join(repoRoot, 'tools', 'desktop.mjs'));
  assert.ok(tool.includes('browsersAt('), 'the capture finds the browser among the application’s processes');
  assert.ok(!tool.includes('daoris-browser.exe') && !tool.includes('Daoris.Desktop.Browser'),
    'the capture still looks for the retired browser executable');
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

/**
 * 🔴 Reaching the DEPLOYED shell — the instrument the first deployment recorded as missing and
 * deliberately did not build (case study 2d: *"the honest options are a deliberate opt-in flag or
 * nothing, and that is a decision, not a patch"*). D62 made it: the desktop app is the focus, and the
 * install is where it is judged.
 *
 * What is asserted here is the part that is silently wrong when it breaks. The capture, the
 * attach and the kill all match a shell by its executable's PATH — that is what stops this loop
 * photographing somebody else's window — so an install path resolved loosely would point every one
 * of them at the wrong program while every command still appeared to work.
 */
test('an install is addressed by its folder, and the launcher is the one at its root', () => {
  const root = mkdtempSync(join(tmpdir(), 'daoris-install-'));
  writeFileSync(join(root, 'Daoris.exe'), '');
  // The launcher alone is not enough: it exits once it starts the application, which is what the
  // instruments address (D93).
  assert.equal(installedExe(root), null);

  mkdirSync(join(root, 'app'), { recursive: true });
  writeFileSync(join(root, 'app', 'Daoris.Desktop.exe'), '');
  assert.equal(installedExe(root), join(root, 'app', 'Daoris.Desktop.exe'));
});

/**
 * An install published before D93 has the single-file shell's launcher, and the instruments still
 * reach it until its next publish replaces it: that is the install the owner has open meanwhile.
 */
test('an install from before the rename is found by the launcher it has', () => {
  const root = mkdtempSync(join(tmpdir(), 'daoris-install-'));
  writeFileSync(join(root, 'daoris-desktop.exe'), '');
  assert.equal(installedExe(root), join(root, 'daoris-desktop.exe'));

  writeFileSync(join(root, 'Daoris.exe'), '');
  mkdirSync(join(root, 'app'), { recursive: true });
  writeFileSync(join(root, 'app', 'Daoris.Desktop.exe'), '');
  assert.equal(installedExe(root), join(root, 'app', 'Daoris.Desktop.exe'), 'the current layout first');
});

/**
 * A folder with no launcher is NOT an install, and saying so beats launching nothing. The first
 * deployment's own folder had the executable one level down (`<family>/app`), so "point it at the
 * folder you published to" is a thing a person gets wrong on their first try.
 */
test('a folder holding no launcher is not an install', () => {
  const root = mkdtempSync(join(tmpdir(), 'daoris-install-'));
  assert.equal(installedExe(root), null);

  mkdirSync(join(root, 'app'), { recursive: true });
  writeFileSync(join(root, 'app', 'Daoris.exe'), '');
  // Still null: the launcher is at the ROOT of an install by construction, and guessing one level
  // down would silently accept a folder that is not one.
  assert.equal(installedExe(root), null);
});

test('no install named is no install, rather than a path built from undefined', () => {
  assert.equal(installedExe(null), null);
  assert.equal(installedExe(undefined), null);
  assert.equal(installedExe(''), null);
});

/**
 * Every process query the tools build compares a path inside a PowerShell single-quoted string, and a
 * `'` in that path ended the string early (REV3): the query matched nothing, and a kill that matches
 * nothing is a stale window still holding the port. Backslashes must stay single — doubled, the
 * comparison never matches either.
 */
test('a path is quoted for PowerShell with its apostrophes doubled and its backslashes untouched', () => {
  assert.equal(psQuote('D:\\builds\\o\'brien\\app.exe'), "'D:\\builds\\o''brien\\app.exe'");
  assert.equal(psQuote('plain'), "'plain'");
  assert.equal(psQuote("''"), "''''''");
});

/**
 * The one query the dev loop, the deployment gate and the publish share (REV3 CLEAN1), asked about a
 * process that is certainly running from a known path: this test's own. One question only — each
 * costs a PowerShell start, seconds on this platform — and the deployment gate asks the rest.
 */
test('the processes running from a path are found by that path', { skip: process.platform !== 'win32' }, () => {
  assert.ok((running(process.execPath) as number[]).includes(process.pid));
  assert.deepEqual(running(''), []);
});

/**
 * The instruments refuse to report from a page that is not this run's shell (CHR2c). On Chromium every
 * shell's page has the same origin, so what tells this run's apart is the host it was told to reach:
 * the scratch and the install can both be up, and a reading taken in the other one is a claim about a
 * window nobody is looking at.
 */
test('the shell is told apart by the host its page reaches', () => {
  const scratch = 'http://127.0.0.1:5188';
  const chromium = { chromium: true, webview: false, origin: SHELL_ORIGIN, host: 'http://127.0.0.1:5188' };

  assert.equal(isShell(chromium, scratch), true);
  assert.equal(isShell({ ...chromium, host: 'http://localhost:5177' }, scratch), false, 'the install, not this run');
  assert.equal(isShell({ ...chromium, host: null }, scratch), false);
  assert.equal(isShell({ ...chromium, host: 'not a url' }, scratch), false);
  assert.equal(isShell({ ...chromium, origin: 'https://example.com' }, scratch), false);
  assert.equal(isShell({ chromium: false, webview: false, origin: SHELL_ORIGIN, host: scratch }, scratch), false,
    'a browser tab on the same address has no bridge');

  // WebView2 is no shell of Daoris's since D93, even on the host's own origin.
  assert.equal(isShell({ webview: true, chromium: false, origin: scratch, host: null }, scratch), false);
  assert.equal(isShell(null, scratch), false);
});

/**
 * On Chromium the engine's own processes run from the application's executable (CHR4), so "what runs
 * from this path" counts renderers, a GPU process and utilities beside the one window. Closing those
 * one by one cost fifteen seconds each and crashed a page; the application is the process with no
 * `--type=`.
 */
test('the application is told from the engine processes started from its own executable', () => {
  assert.equal(isEngineProcess('"D:\app\Daoris.exe"'), false);
  assert.equal(isEngineProcess('"D:\app\Daoris.exe" --type=renderer --lang=en-US'), true);
  assert.equal(isEngineProcess('"D:\app\Daoris.exe" --type=gpu-process'), true);
  assert.equal(isEngineProcess('"D:\my--type=folder\Daoris.exe"'), false, 'a path is not a switch');
  assert.equal(isEngineProcess(null), false);

  const rows = [
    '4100|"D:\app\Daoris.exe" --type=gpu-process --no-sandbox',
    '4200|"D:\app\Daoris.exe" ',
    '4300|"D:\app\Daoris.exe" --type=utility --utility-sub-type=network.mojom.NetworkService',
    '',
    'garbage',
  ].join('\n');
  assert.deepEqual(applicationsIn(rows), [4200]);
});

/**
 * CHR8 (D99): Daoris's browser is a fourth kind of process from the same executable, the application
 * started with the browser's argument first. It is not the application — a stop that closed it would
 * close the person's browser, and it follows the application out on its own — and it is not one of the
 * engine's processes either: it holds windows of its own. Told apart by its FIRST argument, the rule the
 * application routes on, so a folder or an engine switch that happens to contain it never counts.
 */
test('the browser is told from the application and from the engine processes', () => {
  const browser = '"D:\\app\\Daoris.Desktop.exe" --daoris-browser "--daoris-profile=D:\\h\\browser\\engine" '
    + '--daoris-port=9422 --daoris-parent=4200';
  assert.equal(isBrowserProcess(browser), true);
  assert.equal(isBrowserProcess('D:\\app\\Daoris.Desktop.exe --daoris-browser --daoris-port=9422'), true, 'an unquoted executable');
  assert.equal(isBrowserProcess('"D:\\app\\Daoris.Desktop.exe"'), false, 'the application');
  assert.equal(isBrowserProcess('"D:\\app\\Daoris.Desktop.exe" --type=renderer --daoris-browser'), false, 'an engine process');
  assert.equal(isBrowserProcess('"D:\\app\\Daoris.Desktop.exe" --app-root D:\\x --daoris-browser'), false, 'not first');
  assert.equal(isBrowserProcess('"D:\\my --daoris-browser\\Daoris.Desktop.exe"'), false, 'a path is not an argument');
  assert.equal(isBrowserProcess('"D:\\app\\Daoris.Desktop.exe" --daoris-browser-x'), false);
  assert.equal(isBrowserProcess(null), false);

  const rows = [
    `4100|"D:\\app\\Daoris.Desktop.exe" --type=gpu-process --no-sandbox`,
    '4200|"D:\\app\\Daoris.Desktop.exe" ',
    `4400|${browser}`,
    `4500|"D:\\app\\Daoris.Desktop.exe" --type=renderer --user-data-dir="D:\\h\\browser\\engine"`,
    '',
  ].join('\n');
  assert.deepEqual(applicationsIn(rows), [4200], 'a stop of the application never walks the browser');
  assert.deepEqual(browsersIn(rows), [4400]);
});

/**
 * The browser's argument is a twin (`twins.md`): the application routes on it and the shell starts the
 * browser with it (`EngineBrowser.Argument`), and the tools tell the browser apart by it. Spelled twice,
 * in two languages, and held together only here.
 */
test('the tools and the application spell the browser’s argument the same', () => {
  const engine = readText(join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Modules', 'EngineBrowser.cs'));
  assert.ok(engine.includes(`const string Argument = "${BROWSER_ARGUMENT}";`),
    `the tools look for ${BROWSER_ARGUMENT} and EngineBrowser routes on another`);
});

/**
 * `kill` stops what this loop started, by the pid it recorded (D93). After `run --install` the record
 * names the install's application, and the person's own start of the same install runs from the same
 * path: stopping by path closed their window.
 */
test('kill stops the process the loop recorded, never another from the same path', () => {
  assert.deepEqual(startedHere([4100, 4200], { pid: 4200 }), [4200]);
  assert.deepEqual(startedHere([4100], { pid: 4200 }), [], 'the recorded run has ended; the other is not ours');
  assert.deepEqual(startedHere([4100], {}), [4100], 'a record without a pid keeps the old answer');
  assert.deepEqual(startedHere([4100], null), [4100]);
});

/**
 * A page the theme tests photograph: the stored choice, the system's scheme, and the page's own
 * following of a storage event, as `theme.ts`'s `followStoredTheme` does. Its expressions run in a
 * context of their own, as `Runtime.evaluate` runs them in the page, and come back by value.
 */
function themedPage({
  choice = null as string | null, systemDark = true, follows = true,
  refuseWrite = ((_write: number) => false) as (write: number) => boolean,
} = {}) {
  const store = new Map<string, string>(choice === null ? [] : [['daoris.theme', choice]]);
  const root = { dataset: {} as Record<string, string> };
  let emulated: string | null = null;
  let writes = 0;
  const follow = () => {
    if (!follows) return;
    const held = store.get('daoris.theme');
    if (held === 'light' || held === 'dark') root.dataset.theme = held;
    else delete root.dataset.theme;
  };
  follow();
  const write = (apply: () => void) => {
    writes += 1;
    if (refuseWrite(writes)) throw new Error('storage is refused here');
    apply();
  };
  class StorageEvent {
    type: string;
    key: string | null;
    constructor(type: string, init: { key?: string | null } = {}) {
      this.type = type;
      this.key = init.key ?? null;
    }
  }
  const context = vm.createContext({
    document: { documentElement: root },
    localStorage: {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => write(() => store.set(key, String(value))),
      removeItem: (key: string) => write(() => store.delete(key)),
    },
    matchMedia: () => ({ matches: (emulated ?? (systemDark ? 'dark' : 'light')) === 'dark' }),
    StorageEvent,
    dispatchEvent: (event: StorageEvent) => {
      if (event.type === 'storage' && (event.key === 'daoris.theme' || event.key === null)) follow();
      return true;
    },
  });
  const cdp = {
    async send(method: string, params: { features?: { name: string; value: string }[] } = {}) {
      if (method === 'Emulation.setEmulatedMedia') emulated = params.features?.[0]?.value ?? null;
      return {};
    },
    async evaluate(expression: string) {
      const value = vm.runInContext(expression, context);
      return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
    },
  };
  return {
    cdp,
    stored: () => store.get('daoris.theme') ?? null,
    attribute: () => root.dataset.theme ?? null,
    writes: () => writes,
  };
}

/**
 * LOOK1: `shot --theme` emulates the system's scheme, which the page follows only while the viewer's
 * choice is System. On the install the choice was dark, and `--theme light` photographed dark with
 * nothing said. Where the emulation alone takes, the viewer's choice is never touched.
 */
test('a theme the page takes from the system is emulated, and the viewer’s choice is never touched', async () => {
  const page = themedPage({ choice: null, systemDark: true });
  let during: unknown = null;
  await withPageTheme(page.cdp, 'light', async () => { during = await page.cdp.evaluate(PAGE_THEME); }, { settle: 0 });

  assert.equal(during, 'light');
  assert.equal(page.writes(), 0);
  assert.equal(page.stored(), null);
});

test('a viewer’s own choice is set for the capture and put back after it', async () => {
  const page = themedPage({ choice: 'dark' });
  let during: unknown = null;
  let storedDuring: string | null = null;
  await withPageTheme(page.cdp, 'light', async () => {
    during = await page.cdp.evaluate(PAGE_THEME);
    storedDuring = page.stored();
  }, { settle: 0 });

  assert.equal(during, 'light');
  assert.equal(storedDuring, 'light');
  assert.equal(page.stored(), 'dark', 'the viewer’s choice is back');
  assert.equal(page.attribute(), 'dark', 'and the page follows it back');
  assert.equal(page.writes(), 2, 'one write to take, one to put back');
});

test('a theme the viewer already chose is photographed as it is, and nothing is written', async () => {
  const page = themedPage({ choice: 'light', systemDark: true });
  await withPageTheme(page.cdp, 'light', async () => {}, { settle: 0 });
  assert.equal(page.writes(), 0);
  assert.equal(page.stored(), 'light');
});

test('the viewer’s choice is put back even when the capture fails', async () => {
  const page = themedPage({ choice: 'dark' });
  await assert.rejects(
    withPageTheme(page.cdp, 'light', async () => { throw new Error('the capture exited 1'); }, { settle: 0 }),
    /the capture exited 1/);
  assert.equal(page.stored(), 'dark');
  assert.equal(page.attribute(), 'dark');
});

test('a choice that cannot be set refuses with the reason, and captures nothing', async () => {
  const page = themedPage({ choice: 'dark', refuseWrite: () => true });
  let captured = false;
  await assert.rejects(
    withPageTheme(page.cdp, 'light', async () => { captured = true; }, { settle: 0 }),
    (error: Error) => /refusing/.test(error.message) && /storage is refused here/.test(error.message)
      && /System/.test(error.message));
  assert.equal(captured, false);
  assert.equal(page.stored(), 'dark');
});

test('a page that does not follow its choice refuses rather than photographing the other theme', async () => {
  const page = themedPage({ choice: 'dark', follows: false });
  // Never followed, so the attribute the page started with is set by hand here: forced dark.
  await page.cdp.evaluate("document.documentElement.dataset.theme = 'dark'");
  let captured = false;
  await assert.rejects(
    withPageTheme(page.cdp, 'light', async () => { captured = true; }, { settle: 0 }),
    (error: Error) => /refusing/.test(error.message) && /still dark/.test(error.message));
  assert.equal(captured, false);
  assert.equal(page.stored(), 'dark', 'put back all the same');
});

test('a choice that cannot be put back says what it was, and fails the shot', async () => {
  const page = themedPage({ choice: 'dark', refuseWrite: (write) => write === 2 });
  await assert.rejects(
    withPageTheme(page.cdp, 'light', async () => {}, { settle: 0 }),
    (error: Error) => /not put back/.test(error.message) && /was dark/.test(error.message)
      && /is light now/.test(error.message));
});

test('a viewer on System is put back to System when the page had to be set', async () => {
  // The system's scheme emulated and a page that still reads dark: a choice the page holds alone,
  // since storage refused it earlier in the page's life. Setting it through the store makes it take;
  // putting it back removes the key, which is how the page remembers System.
  const page = themedPage({ choice: null, systemDark: true });
  await page.cdp.evaluate("document.documentElement.dataset.theme = 'dark'");
  let during: unknown = null;
  await withPageTheme(page.cdp, 'light', async () => { during = await page.cdp.evaluate(PAGE_THEME); }, { settle: 0 });
  assert.equal(during, 'light');
  assert.equal(page.stored(), null);
});

/**
 * The theme's key is a twin (`twins.md`): the page remembers the viewer's choice under it and hears a
 * write to it, and `shot --theme` sets it for a capture and puts it back. Spelled twice, held here.
 */
test('the tool and the page spell the theme’s key the same, and the page hears a write to it', () => {
  const theme = readText(join(repoRoot, 'src', 'Daoris.Web', 'src', 'theme.ts'));
  assert.ok(theme.includes(`export const THEME_KEY = '${THEME_KEY}';`),
    `the tool writes ${THEME_KEY} and the page remembers its choice under another key`);
  assert.ok(theme.includes("window.addEventListener('storage', followStoredTheme)"),
    'the page no longer hears a storage event, so a choice set for a capture would not take');
});

/**
 * An install run opens the debug port through the kit's development switch, and the host the shell starts inherits the
 * environment: in development ASP.NET answers a bad request with its exception page, source paths and all. The installed
 * host stays in production, as the deployment rehearsal keeps it (FIX-LOG 2026-10-04).
 */
test('a run of the install keeps the port’s development switch and its host in production', () => {
  const environment = installEnvironment(9444);
  assert.equal(environment.DOTNET_ENVIRONMENT, 'Development', 'the port opens only in development');
  assert.equal(environment.DAORIS_DEVTOOLS_PORT, '9444');
  assert.equal(environment.ASPNETCORE_ENVIRONMENT, 'Production', 'the installed host would serve its exception page');
});

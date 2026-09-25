#!/usr/bin/env node
/**
 * The desktop shell's dev loop: build it, run it on a machine of its own, look at it, ask it things.
 *
 * WHY THIS EXISTS. Every other surface in this repository has a loop that can see it — the CLI has
 * `npm test`, the service has its suite, the platform has vitest over a mocked bridge and Playwright
 * over the real bundle. The shell has neither: Playwright cannot reach it (the bridge is absent in a
 * browser, by construction — `docs/2026-09-19-frontend-architecture.md` §4), and until REV2 nothing
 * tested it at all, which is how 1,128 lines came to answer every refusal with a blank failure. This
 * is not a gate and does not pretend to be one; it is the instrument a person or an agent uses to
 * START the thing and SEE it, which is the step that was being done by hand or not at all.
 *
 *   node tools/desktop.mjs doctor              what this machine has, and what a run would use
 *   node tools/desktop.mjs build               the platform bundle, the host, then the shell
 *   node tools/desktop.mjs run                 start it on a scratch machine, debug port attached
 *   node tools/desktop.mjs run --install <dir> start the DEPLOYED one there, on your real machine
 *   node tools/desktop.mjs shot overview       capture the window
 *   node tools/desktop.mjs eval "document.title"
 *   node tools/desktop.mjs click "[data-nav=quests]"
 *   node tools/desktop.mjs kill | restart
 *
 * TWO RULES SHAPE ALL OF IT.
 *
 * **A dev run gets its own machine.** The shell is not a viewer: it runs the driver loop, and the
 * driver SPAWNS AGENT SESSIONS in real repositories against real quests. Pointed at the machine's
 * Daoris home it is the person's actual Daoris, with their registry, their quests and their drivable
 * set — so `run` redirects every machine-local file, takes its own port, and copies `examples/` to
 * work over. The real one is `--real`, spelled out, because reaching it should be a sentence somebody
 * typed.
 *
 * **It only ever touches what this checkout built.** `kill` matches the executable's PATH, never the
 * process name; `shot` photographs that same path. An installed Daoris and this one are two programs
 * that happen to share a name, and every family sibling that skipped this lesson paid it twice — once
 * killing the wrong window, once photographing it.
 */
import { spawn, spawnSync } from 'node:child_process';
import {
  existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, unlinkSync, writeFileSync,
} from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { copyTree, isMain } from './fsx.mjs';

export const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));

/** Where a scratch run keeps everything it owns. Under `_fixtures/`, which is gitignored. */
export const scratchRoot = join(repoRoot, '_fixtures', 'desktop');

/**
 * The machine-local files the desktop reads, and the variable that moves each one.
 *
 * ⚠ THIS LIST IS HALF OF A PAIR. The other half is the code that reads them — `DriverConfig`,
 * `RemoteTarget`, `Harnesses`, the store and the index root — and a name missing here does not fail:
 * it silently writes into the person's real Daoris home. A test asserts the two agree, because the
 * failure this prevents is invisible until somebody's driver config has a repository in it they never
 * opted in.
 */
export const REDIRECTED = [
  // The home itself (D63): every machine-local default derives from it, so a scratch run points it
  // at scratch before anything else — a new file under the home is then scratch by construction.
  'DAORIS_HOME',
  'DAORIS_KNOWLEDGE_DB',
  'DAORIS_KNOWLEDGE_ROOT',
  'DAORIS_DRIVER_CONFIG',
  'DAORIS_REMOTE_CONFIG',
  'DAORIS_HARNESS_CONFIG',
];

/**
 * Variables a scratch run must UNSET, not override.
 *
 * The remote trio is an environment PAIR that replaces the whole machine's remotes map — that is what
 * makes the rehearsals hermetic (WSP3), and inherited by a dev run it does the opposite: the scratch
 * shell's sync loop would feed a real deployment from a scratch store. A key is the same shape of
 * mistake one layer down. Redirecting them is not enough, because the file only governs when the
 * environment is silent.
 *
 * The rules home (PERM2, D74) is where a session's proposal to change the rules is written. The driver
 * sets it on every connector it hands over, to its own home. A connector it did not hand, such as a
 * repository's own `.mcp.json` in a pipe-door session, inherits the environment instead, and an inherited
 * one would file a scratch session's proposal among the real machine's. Unset, it falls back to the
 * redirected `DAORIS_HOME`.
 */
export const CLEARED = [
  'DAORIS_REMOTE_URL',
  'DAORIS_REMOTE_KEY',
  'DAORIS_REMOTE_WORKSPACE',
  'DAORIS_SERVICE_KEY',
  'DAORIS_RULES_HOME',
];

/**
 * The environment a scratch run needs, whole.
 *
 * ⚠ `DAORIS_HTTP_HOST` is not an optimisation. `ServiceHostLocator` prefers an INSTALLED host in
 * the home's `bin/` over this workspace's build, so a dev shell on a machine that has ever run
 * `npm run publish:service -- --install` brings up yesterday's binary serving yesterday's bundle, and
 * every change appears to do nothing. Naming the built host is what makes the loop honest.
 *
 * ⚠ The port is not the default one either. `HostSupervisor` ADOPTS a host already answering rather
 * than double-starting it (correctly — killing someone else's server is the process version of
 * writing into their tree), so a scratch run on :5177 would quietly adopt the person's real host and
 * show their real store while claiming to be a scratch machine.
 */
export function debugEnvironment(cdpPort) {
  if (!cdpPort) return {};
  return {
    // Both halves are needed, and finding that out cost a run. WebView2 reads
    // `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` only while nothing has set `AdditionalBrowserArguments`
    // — and the runtime always sets it. What it does instead is re-append that env var itself, ONLY
    // in development mode (`BrowserArguments.Build`), which is how a shipped window has nothing to
    // attach to. So dev mode is not a preference here: it is the switch that lets the port through.
    DOTNET_ENVIRONMENT: 'Development',
    WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${cdpPort}`,
  };
}

export function scratchEnvironment({ home, family, serviceUrl, httpHost, mcpHost, cdpPort }) {
  return {
    DAORIS_SERVICE_URL: serviceUrl,
    // The other half of the port, and it is not optional. The shell PROBES `DAORIS_SERVICE_URL` and
    // the host BINDS `ASPNETCORE_URLS` (defaulting to :5177) — nothing passes one to the other, so a
    // scratch run that moved only the probe waits on a host answering somewhere else and sits on the
    // splash forever. Found on this tool's first real run.
    ASPNETCORE_URLS: serviceUrl,
    DAORIS_HOME: home,
    DAORIS_KNOWLEDGE_DB: join(home, 'knowledge.db'),
    DAORIS_KNOWLEDGE_ROOT: family,
    DAORIS_DRIVER_CONFIG: join(home, 'driver.json'),
    DAORIS_REMOTE_CONFIG: join(home, 'remotes.json'),
    DAORIS_HARNESS_CONFIG: join(home, 'harnesses.json'),
    DAORIS_HTTP_HOST: httpHost,
    // The MCP host a driven session is handed over the protocol door (ACP4). Pinned for the same
    // reason the HTTP one is: the locator prefers an INSTALLED binary, so a scratch run would hand
    // the session the real machine every time this one is behind it.
    DAORIS_MCP_HOST: mcpHost,
    // Nothing in this repository reads these: the debug port is the runtime's and the loader's, which
    // is the point — the shipped app gains no debug surface, and a window nobody started this way has
    // nothing to attach to. Loopback only, on a port picked for this run.
    ...debugEnvironment(cdpPort),
  };
}

/**
 * A project's built executable — derived, never written down.
 *
 * The sibling's config file carries the target framework inside the path with a warning that it MUST
 * be updated whenever the project retargets, because getting it wrong launches the binary still
 * sitting in the old folder and every change appears to do nothing. Reading the assembly name from
 * the project and globbing for the build removes the thing that needed remembering: a retarget moves
 * the folder, the glob follows, and "not built" stays distinguishable from "built elsewhere".
 */
export function assemblyExe(projectDir, { flavours = ['Debug', 'Release'] } = {}) {
  const project = readdirSync(projectDir).find((file) => file.endsWith('.csproj'));
  if (!project) return null;

  const name = /<AssemblyName>([^<]+)<\/AssemblyName>/
    .exec(readFileSync(join(projectDir, project), 'utf8'))?.[1];
  if (!name) return null;

  const found = [];
  for (const flavour of flavours) {
    const flavourDir = join(projectDir, 'bin', flavour);
    if (!existsSync(flavourDir)) continue;
    for (const framework of readdirSync(flavourDir)) {
      const exe = join(flavourDir, framework, `${name}.exe`);
      if (existsSync(exe)) found.push({ exe, at: statSync(exe).mtimeMs });
    }
  }

  // Newest wins: a Release build made after a Debug one is what the person last asked for.
  found.sort((a, b) => b.at - a.at);
  return found[0]?.exe ?? null;
}

/** What a published install calls its launcher. One file at the root — `desktop-publish.mjs`'s rule. */
export const INSTALLED_LAUNCHER = 'daoris-desktop.exe';

/**
 * The launcher inside a published install, or null when that folder is not one.
 *
 * @remarks
 * 🔴 **The instrument the first deployment recorded as missing** (case study 2d): `shot`, `eval`
 * and `click` find a window by THIS checkout's executable path, so a deployed shell — a different
 * path, and no debug port — could only be looked at by a person. That was recorded rather than
 * fixed, because *"the honest options are a deliberate opt-in flag or nothing, and that is a
 * decision, not a patch"*. The owner made the decision on 2026-09-22: **develop against the
 * install**, because the desktop is where the capability is.
 *
 * **The launcher is at the ROOT, never guessed one level down.** An install shows one thing to
 * double-click and hides the rest under `app/` — so a folder with no launcher at its root is not an
 * install, and answering null beats launching something that happens to be nearby. The first
 * deployment's own folder is `<family>/app`, which is exactly the shape that makes "point it at the
 * folder you published to" easy to get wrong.
 */
export function installedExe(directory) {
  if (!directory) return null;
  const launcher = join(directory, INSTALLED_LAUNCHER);
  return existsSync(launcher) ? launcher : null;
}

/**
 * Which captures a prune would drop: keep the newest `keep`, and stay under `maxBytes`.
 *
 * A window capture is a full-resolution lossless PNG — several megabytes each — and the sibling's
 * folder had quietly reached 294 MB before anyone measured it. Every capture prunes afterwards, so
 * the policy applies whether or not someone remembers it.
 */
export function prune(entries, { keep = 25, maxBytes = 150 * 1024 * 1024 } = {}) {
  const newestFirst = [...entries].sort((a, b) => b.at - a.at);
  const drop = newestFirst.slice(keep);
  let kept = 0;
  for (const entry of newestFirst.slice(0, keep)) {
    kept += entry.size;
    if (kept > maxBytes) drop.push(entry);
  }

  return drop;
}

// ---------------------------------------------------------------------------------------------
// Everything below runs; everything above is asserted.

const DESKTOP_PROJECT = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.App');
const HTTP_PROJECT = join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http');
const MCP_PROJECT = join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Mcp');
const WEB = join(repoRoot, 'src', 'Daoris.Web');
const RUN_FILE = join(scratchRoot, 'run.json');
const SHOTS = join(scratchRoot, 'screenshots');

const fail = (message, code = 2) => {
  console.error(message);
  process.exit(code);
};

const run = (command, args, options = {}) => {
  const result = spawnSync(command, args, { stdio: 'inherit', shell: false, ...options });
  if (result.error) fail(`${command} did not start: ${result.error.message}`);
  if (result.status !== 0) fail(`${command} ${args[0] ?? ''} exited ${result.status}`, result.status ?? 2);
};

export const powershell = (script) =>
  spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).stdout ?? '';

/** The shells running from a given executable — pid and path, nothing guessed by name. */
export const running = (exe) => {
  if (!exe) return [];
  // Single-quoted PowerShell strings take backslashes literally; doubling them makes the comparison
  // never match, and a kill that silently no-ops leaves the old window holding the port.
  const out = powershell(
    `Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq '${exe}' } | `
    + 'ForEach-Object { $_.Id }');
  return out.split('\n').map((line) => Number(line.trim())).filter((pid) => Number.isInteger(pid) && pid > 0);
};

/**
 * Stop the shells from this checkout — by ASKING first.
 *
 * ⚠ A forced kill skips the app's own teardown, and the teardown is what stops the service host the
 * shell spawned (`OnStopping`: the driver loop, then the host it owns). Orphaned, that host keeps the
 * port and holds a file lock on the very assemblies the next `build` has to overwrite — which is how
 * this was found: two hosts left over from earlier runs made `dotnet build` fail on a copy.
 *
 * Closing the main window runs the same path a person's × does. The force is the backstop, not the
 * method — and nothing here ever touches a host directly, because a host this tool did not start
 * belongs to whoever did (the shell's own supervisor makes exactly that distinction).
 */
/* Stop the shell this checkout built — by CLOSING it, so its own shutdown path runs.
 *
 * 🔴 That path is what stops the HTTP host the shell spawned and owns, so a forced kill orphans a
 * `daoris-knowledge-http` that then holds the build's own DLLs. Measured: seven of them, after a
 * session of restarts, failing the next `build` with MSB3027.
 *
 * 🔴 `CloseMainWindow` closes whichever window WINDOWS calls main, and since SURF8 that may be the
 * monitor rather than the application — closing it leaves the app running, the wait expires, and the
 * force kill lands. So this closes REPEATEDLY, re-reading the handle each time: the secondary
 * windows go first, the main window last, and the app exits on its own terms. Same trap as the one
 * `shot --window` exists for, in its third disguise. */
export const stopAll = (exe) => powershell(`
  Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq '${exe}' } | ForEach-Object {
    $process = $_
    for ($attempt = 0; $attempt -lt 6 -and -not $process.HasExited; $attempt++) {
      $process.Refresh()
      if ($process.MainWindowHandle -ne 0) { $process.CloseMainWindow() | Out-Null }
      $process.WaitForExit(2500) | Out-Null
    }
    if (-not $process.HasExited) { $process | Stop-Process -Force }
  }`);

const readRun = () => (existsSync(RUN_FILE) ? JSON.parse(readFileSync(RUN_FILE, 'utf8')) : null);

/**
 * Which shell the instruments address — the one the last `run` started.
 *
 * 🔴 Every one of them matches by executable PATH, which is what stops this loop photographing or
 * killing an install the owner actually uses. That rule does not change when the target IS an
 * install; what changes is which path, and the run file already records it. Falling back to the
 * checkout keeps every existing invocation behaving exactly as it did.
 */
const targetExe = () => readRun()?.exe ?? assemblyExe(DESKTOP_PROJECT);

/** `--install <dir>`, taken off an argument list. */
function takeInstall(args) {
  const at = args.indexOf('--install');
  if (at === -1) return null;
  const directory = args[at + 1];
  if (!directory) fail('usage: --install <folder you published to>');
  args.splice(at, 2);
  return directory;
}

const ago = (path) => {
  const minutes = Math.round((Date.now() - statSync(path).mtimeMs) / 60000);
  return minutes < 1 ? 'just now' : minutes < 90 ? `${minutes}m ago` : `${Math.round(minutes / 60)}h ago`;
};

function pruneShots() {
  if (!existsSync(SHOTS)) return;
  const entries = readdirSync(SHOTS)
    .filter((file) => file.endsWith('.png'))
    .map((file) => {
      const path = join(SHOTS, file);
      const stat = statSync(path);
      return { path, at: stat.mtimeMs, size: stat.size };
    });
  for (const entry of prune(entries)) unlinkSync(entry.path);
}

/**
 * Take a `--window <name>` off an argument list, and say which window was asked for (SURF8).
 *
 * One flag for both instruments, deliberately: `eval` and `click` reach a *page* over CDP and `shot`
 * photographs a *window* through the OS, and a tool that needed a URL parameter for one and a
 * caption for the other would be a tool people get wrong. Null is the application's own window.
 */
function takeWindow(args) {
  const at = args.indexOf('--window');
  if (at === -1) return null;

  const name = args[at + 1];
  if (!name) fail('usage: --window <monitor|session:ID>');
  args.splice(at, 2);
  return name;
}

/** What the shell captions that window — how the OS-level capture finds it. */
function windowCaption(window) {
  return window === 'monitor' ? 'Monitor' : window;
}

/**
 * Attach to one of the running shell's pages, having first established that it IS the shell.
 *
 * @param window - null for the application's own window, or a secondary window's name (SURF8):
 * `monitor`, or `session:<id>`. Since the shell can hold more than one page, the caller says which.
 */
async function attach(window = null) {
  const state = readRun();
  if (!state?.cdpPort) {
    fail('nothing to attach to — `node tools/desktop.mjs run` starts a shell with its debug port open.');
  }

  const { Cdp, pickPageTarget, targetsAt } = await import('./cdp.mjs');
  const targets = await targetsAt(state.cdpPort);
  if (!targets) {
    fail(`nothing is listening on the debug port ${state.cdpPort} — the shell is not running, or was `
      + 'started without this tool. `node tools/desktop.mjs restart`.');
  }

  const target = pickPageTarget(targets, window);
  if (!target) {
    const which = window ? `no \`${window}\` window` : 'no main-window page';
    fail(`${which} on the debug port ${state.cdpPort}:\n${JSON.stringify(targets, null, 2)}`);
  }

  const cdp = await new Cdp(target.webSocketDebuggerUrl).open();

  /* IDENTIFY THE PAGE BEFORE REPORTING ITS ANSWER. The whole value of this instrument is that it
   * sees what nothing else can, so a reading taken in the wrong page is a claim about the desktop
   * that nobody can contradict. `chrome.webview` exists only inside a WebView2 — it is what the
   * bridge's transport is built on — and the origin is the one this run asked for. */
  const host = await cdp.evaluate('({ webview: !!window.chrome?.webview, origin: location.origin })');
  if (!host?.webview || (state.serviceUrl && host.origin !== new URL(state.serviceUrl).origin)) {
    cdp.close();
    fail(
      `refusing: the page on ${state.cdpPort} is not this shell.\n`
      + `  expected a WebView2 at ${new URL(state.serviceUrl).origin}\n`
      + `  found     ${host?.webview ? 'a WebView2' : 'a browser page'} at ${host?.origin ?? '(unknown)'}`,
      1);
  }

  return cdp;
}

async function build(args) {
  const configuration = args.includes('--release') ? 'Release' : 'Debug';

  // 🔴 An orphaned host from this checkout holds the assemblies this build has to overwrite, and
  // MSBuild says so in eleven lines ending `MSB3027 … Exceeded retry count of 10`. The trap is
  // already documented on `stopAll` below; naming it here is what makes the documentation reach the
  // person who hit it. NAMED, never killed: a host this tool did not start belongs to whoever did,
  // which is the same distinction the shell's own supervisor makes.
  const leftovers = running(assemblyExe(HTTP_PROJECT));
  if (leftovers.length) {
    fail(`${leftovers.length} service host(s) from this checkout are still running `
      + `(pid ${leftovers.join(', ')}), and they hold the assemblies this build overwrites.\n`
      + '  Close the shell — or `node tools/desktop.mjs kill`, which closes it so its own shutdown '
      + 'stops the host it owns.');
  }

  // The order is the dependency order and is not cosmetic: the platform builds INTO the host's
  // wwwroot, so a host built before the bundle serves the previous one, and the shell shows it.
  run('npm', ['--prefix', WEB, 'run', 'build'], { shell: true });
  run('dotnet', ['build', HTTP_PROJECT, '-c', configuration]);
  run('dotnet', ['build', DESKTOP_PROJECT, '-c', configuration]);
  console.log('\nbuilt — `node tools/desktop.mjs run` to look at it.');
}

async function start(command, args) {
  /* 🔴 The DEPLOYED shell, addressed by the folder it was published to. It implies `--real` and
     cannot mean anything else: an install has no `DAORIS_*` overrides and makes its own `data/` the
     Daoris home by construction (D63) — that is what makes it a deployment rather than a preview.
     What this adds is the debug port, and it adds it AT LAUNCH through the environment: the shipped
     application still exposes nothing, which is the half of case study 2d that was right. */
  const install = takeInstall(args);
  const exe = install ? installedExe(install) : assemblyExe(DESKTOP_PROJECT);
  if (install && !exe) {
    fail(`no \`${INSTALLED_LAUNCHER}\` at the root of \`${install}\` — that is not an install.\n`
      + '  Point --install at the folder you published to (the one holding the launcher, `app/`\n'
      + '  and `data/`), not at its parent.');
  }
  if (!exe) fail('the shell is not built — `node tools/desktop.mjs build`.');

  const live = running(exe);
  if (live.length && command === 'restart') {
    stopAll(exe);
    console.log(`stopped pid ${live.join(', ')}`);
  } else if (live.length) {
    fail(`a shell from this checkout is already running (pid ${live.join(', ')}). `
      + '`node tools/desktop.mjs restart` replaces it.');
  }

  const real = args.includes('--real') || Boolean(install);
  const { freePort } = await import('./cdp.mjs');
  const cdpPort = await freePort(9333);

  let environment = debugEnvironment(cdpPort);
  let serviceUrl = process.env.DAORIS_SERVICE_URL ?? 'http://localhost:5177';
  const extra = [];

  if (real) {
    console.log(install
      ? `⚠ --install: the DEPLOYED application, on its own home (${join(install, 'data')}).`
      : `⚠ --real: your own Daoris home (${process.env.DAORIS_HOME ?? 'DAORIS_HOME is not set — '
        + 'the shell will refuse'}) — your registry, your quests, your drivable set.`);
    console.log('  The driver loop starts with the app, so a drivable repository with an open quest');
    console.log('  gets a real agent session. This is the instance you use, not a copy of it.');
    if (install) {
      // Said plainly, because it is the one way this differs from double-clicking the launcher.
      console.log('  Started with a debug port so `shot`, `eval` and `click` can reach it. Nothing');
      console.log('  in the published application opens one — this run does, and only this run.');
    }
  } else {
    const home = join(scratchRoot, 'home');
    const family = join(scratchRoot, 'family');
    const appRoot = join(scratchRoot, 'app');
    mkdirSync(home, { recursive: true });
    mkdirSync(appRoot, { recursive: true });

    if (args.includes('--fresh')) rmSync(family, { recursive: true, force: true });
    if (!existsSync(family)) {
      // The example family (D39) is the fixture: two adopters the store's bootstrap import registers
      // on first sight, so the window has something in it. COPIED rather than pointed at, because
      // the shell can write a repository's own `daoris.json` from Projects — and the tracked
      // examples are a gate's fixture, not a scratchpad. The MEMBERS, not the folder: `examples/`
      // also holds the example plugin (D64), which is not a project and must not be imported as one.
      mkdirSync(family, { recursive: true });
      for (const name of ['engine', 'game']) copyTree(join(repoRoot, 'examples', name), join(family, name));
      console.log(`copied examples/{engine,game} -> ${family}`);
    }

    const httpHost = assemblyExe(HTTP_PROJECT);
    if (!httpHost) fail('the service host is not built — `node tools/desktop.mjs build`.');
    // Optional: a run without it drives, and a session simply has no connector (ACP4).
    const mcpHost = assemblyExe(MCP_PROJECT) ?? '';

    serviceUrl = `http://127.0.0.1:${await freePort(5188)}`;
    environment = scratchEnvironment({ home, family, serviceUrl, httpHost, mcpHost, cdpPort });
    // Its own root, so the WebView2 profile, the window geometry and the runtime's single-instance
    // scope all belong to this run — a scratch shell and a real one never contend for either.
    extra.push('--app-root', appRoot);
  }

  const env = { ...process.env, ...environment };
  if (!real) for (const name of CLEARED) delete env[name];

  const child = spawn(exe, extra, { env, detached: true, stdio: 'ignore' });
  child.unref();

  mkdirSync(scratchRoot, { recursive: true });
  writeFileSync(RUN_FILE, `${JSON.stringify(
    { pid: child.pid, exe, serviceUrl, cdpPort, real, started: new Date().toISOString() }, null, 2)}\n`);

  console.log(`shell started (pid ${child.pid})`);
  console.log(`  platform   ${serviceUrl}`);
  console.log(`  debug port ${cdpPort}`);
  console.log(`  machine    ${real
    ? `${install ? join(install, 'data') : process.env.DAORIS_HOME ?? '(no DAORIS_HOME)'} — YOUR OWN`
    : join(scratchRoot, 'home')}`);
}

function doctor() {
  const shell = assemblyExe(DESKTOP_PROJECT);
  const host = assemblyExe(HTTP_PROJECT);
  const bundle = join(HTTP_PROJECT, 'wwwroot', 'index.html');
  // Where `publish:service --install` lands one (D63) — and no home means none installed.
  const installed = process.env.DAORIS_HOME
    ? join(process.env.DAORIS_HOME, 'bin', 'daoris-knowledge-http', 'daoris-knowledge-http.exe')
    : null;
  const state = readRun();

  console.log('built');
  console.log(`  platform bundle  ${existsSync(bundle) ? ago(bundle) : 'absent — `build`'}`);
  console.log(`  service host     ${host ? ago(host) : 'absent — `build`'}`);
  console.log(`  shell            ${shell ? ago(shell) : 'absent — `build`'}`);

  console.log('\nrunning');
  const live = process.platform === 'win32' ? running(shell) : [];
  console.log(`  this checkout    ${live.length ? `pid ${live.join(', ')}` : 'nothing'}`);
  // A host with no shell is usually an orphan — a shell that died without its teardown. It is NOT
  // killed here: this tool did not start it, and it may be a gate's. Named, so the person can act.
  const hosts = process.platform === 'win32' ? running(host) : [];
  if (hosts.length && !live.length) {
    console.log(`  ⚠ service host   pid ${hosts.join(', ')} — running with no shell of this checkout.`);
    console.log('    It holds the port and a lock on the assemblies `build` overwrites. Stop it if it');
    console.log('    is yours: Get-Process daoris-knowledge-http | Stop-Process');
  }
  if (state) {
    console.log(`  last run         ${state.started} · ${state.serviceUrl} · debug ${state.cdpPort}`
      + `${state.real ? ' · --real' : ''}`);
  }

  console.log('\nwhat a scratch run would use');
  console.log(`  machine          ${join(scratchRoot, 'home')}`);
  console.log(`  family           ${join(scratchRoot, 'family')}`
    + `${existsSync(join(scratchRoot, 'family')) ? '' : ' (copied from examples/ on first run)'}`);
  console.log(`  service host     ${host ?? '(none built)'}`);
  if (installed && existsSync(installed)) {
    console.log('  ⚠ an installed service host exists on this machine.');
    console.log('    ServiceHostLocator prefers it over this workspace, which is why `run` names the');
    console.log('    built one explicitly — a shell started any other way serves that one instead.');
  }
}

function usage() {
  console.log(readFileSync(fileURLToPath(import.meta.url), 'utf8')
    .split('\n')
    .slice(1, 21)
    .map((line) => line.replace(/^ \*\/?/, '').replace(/^ /, ''))
    .join('\n'));
}

/**
 * Only when this file is what was run. The helpers above are imported by the test suite, and a
 * module that dispatched on `process.argv` at import time would answer `node --test`'s arguments.
 */
async function main(command, args) {
  if (process.platform !== 'win32' && command !== 'doctor') {
    fail('the shell is a Windows application (net10.0-windows) — this loop only runs there.');
  }

  switch (command) {
    case 'build':
      await build(args);
      break;

    case 'run':
    case 'restart':
      await start(command, args);
      break;

    case 'kill': {
      const exe = targetExe();
      const live = running(exe);
      if (!live.length) {
        console.log('no shell this loop started is running.');
        break;
      }

      stopAll(exe);
      console.log(`stopped pid ${live.join(', ')}`);
      break;
    }

    case 'shot': {
      const exe = targetExe();
      if (!exe) fail('the shell is not built — nothing to photograph.');

      /* `--theme light|dark` photographs the OTHER theme without touching the machine's setting.
       *
       * It exists because the window's own chrome — the DWM border and the caption buttons — is
       * painted NATIVELY from whatever the page last pushed over SET_THEME (SURF7), so it is
       * invisible to every other instrument: the page cannot see it, and a CSS-level check cannot
       * either. Emulating the media query makes the page push the other theme, and the window
       * repaints for real.
       *
       * 🔴 The emulation is scoped to the CDP SESSION and is reverted the moment it closes. A probe
       * that set it, closed, and then captured reported dark and photographed a light window — so
       * the capture happens HERE, while the connection is still open. */
      const themeFlag = args.indexOf('--theme');
      const theme = themeFlag === -1 ? null : args[themeFlag + 1];
      if (themeFlag !== -1) {
        if (!['light', 'dark'].includes(theme)) fail('usage: shot [name] --theme <light|dark>');
        args.splice(themeFlag, 2);
      }

      /* `--window <name>` photographs a SECONDARY window (SURF8) — `monitor`, or `session:<id>` —
       * rather than the main one.
       *
       * It exists because `Process.MainWindowHandle` answers for exactly one window and Windows
       * chooses which, so with the monitor open a capture silently photographs whichever the OS
       * calls main. A name that matches no open window is refused rather than falling back: the
       * whole point of asking is that the main window is not the one wanted. */
      const window = takeWindow(args);

      const name = (args[0] ?? `shell-${new Date().toISOString().slice(11, 19).replaceAll(':', '')}`)
        .replace(/[^\w.-]/g, '-');

      const capture = () => run('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', join(repoRoot, 'tools', 'shot-window.ps1'),
        '-ProcessName', 'daoris-desktop',
        '-ExePath', exe,
        ...(window ? ['-WindowTitle', windowCaption(window)] : []),
        '-OutFile', join(SHOTS, `${name}.png`)]);

      if (theme) {
        const cdp = await attach(window);
        try {
          await cdp.send('Emulation.setEmulatedMedia', {
            features: [{ name: 'prefers-color-scheme', value: theme }],
          });
          // The page's own listener has to fire and the SET_THEME round trip has to land before the
          // window has repainted. Nothing reports when that finished, so this waits.
          await new Promise((resolve) => setTimeout(resolve, 700));
          capture();
        } finally {
          cdp.close();
        }
      } else {
        capture();
      }

      pruneShots();
      break;
    }

    case 'eval': {
      const window = takeWindow(args);
      const expression = args.join(' ');
      if (!expression) fail('usage: node tools/desktop.mjs eval [--window <name>] "<js expression>"');

      const cdp = await attach(window);
      try {
        console.log(JSON.stringify(await cdp.evaluate(expression), null, 2));
      } catch (error) {
        cdp.close();
        fail(`the expression threw: ${error.message}`, 1);
      }

      cdp.close();
      break;
    }

    case 'click': {
      const window = takeWindow(args);
      const selector = args.join(' ');
      if (!selector) fail('usage: node tools/desktop.mjs click [--window <name>] "<css selector>"');

      const cdp = await attach(window);
      /* One element or none. A selector matching three things and clicking the first is how a loop
       * reports success for an interaction that never happened — so the COUNT is the answer, and a
       * miss is a refusal rather than a silent first-match. The click is the page's own, dispatched
       * where the page's handlers are; nothing here simulates a cursor. */
      const outcome = await cdp.evaluate(`(() => {
        const found = [...document.querySelectorAll(${JSON.stringify(selector)})];
        if (found.length !== 1) return { matched: found.length };
        const element = found[0];
        element.scrollIntoView({ block: 'center' });
        element.click();
        return {
          matched: 1,
          clicked: (element.innerText || element.value || element.tagName).trim().slice(0, 80),
        };
      })()`);
      cdp.close();

      if (outcome.matched !== 1) {
        fail(`${selector} matched ${outcome.matched} elements — a click needs exactly one.`, 1);
      }

      console.log(`clicked: ${outcome.clicked}`);
      break;
    }

    case 'doctor':
      doctor();
      break;

    default:
      usage();
      if (command) process.exit(2);
  }
}

if (isMain(import.meta.url)) {
  await main(process.argv[2], process.argv.slice(3));
}

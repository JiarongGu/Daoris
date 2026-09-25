#!/usr/bin/env node
/**
 * Deployment rehearsal — publish the desktop shell to a folder and drive THAT, not this checkout.
 *
 * WHY THIS EXISTS (DEPLOY2). `rehearse` installs and drives the CLI *package*; `rehearse:family`
 * drives the router, the driver and the remote — but both run inside the workspace, and the
 * workspace is what hid the two defects the first real deployment found
 * (`docs/2026-09-22-first-deployment-case-study.md`):
 *
 *   2a  `ServiceHostLocator` built only the FLAT candidate for an installed host, while the
 *       installer gives the HTTP host a directory of its own. Masked everywhere, because the next
 *       candidate is the workspace build and every machine it had run on had one. Fatal on the one
 *       machine it had never run on.
 *   4c  A transcript was written through the CONSOLE's codepage. `daoris-driver` sets its own
 *       console to UTF-8 at startup, so the family rehearsal decoded correctly and papered it over;
 *       the SHELL has no console, and a session record lost every non-ASCII character it held.
 *
 * Neither is exotic and neither needed a model, an account or a credential. What was missing was a
 * gate that installs and runs the ARTEFACT — which is what `rehearse` does for the CLI package and
 * nothing did for the desktop.
 *
 *   npm run rehearse:deploy
 *
 * Exit 0 = the deployed thing works. Exit 1 = it does not; the transcript names the first failure.
 *
 * WHAT THIS GATE CANNOT CONTROL, stated rather than implied:
 *
 *  - **The machine's own installed host used to be the decoy, and now the gate plants one.** Before
 *    D63 a machine that had run `publish:service --install` offered the deployed shell a second host
 *    under the profile, and phase 4 asserted the host the shell started was the one UNDER THE
 *    INSTALL — the check that failed on this machine and could not fail on a clean one. The home is
 *    scratch now (`DAORIS_HOME` is redirected like every other file), so nothing of the machine's
 *    reaches the shell; the gate puts an installed-looking host under the scratch home's `bin/`
 *    itself, so the order is asserted on every machine. 🔴 The first version of this header said the
 *    opposite — that the installed home outranked the install deliberately — and this gate passed
 *    32/32 for as long as it was true, naming the machine's host in its own transcript. The second
 *    deployment is what read the line.
 *  - **A machine whose ANSI codepage is already UTF-8 cannot fail phase 5.** The mangling in 4c is a
 *    round trip through a single-byte page; with ACP 65001 there is no round trip to make. The check
 *    is still the right one — it goes red on every machine that *can* express the defect, which is
 *    every machine Daoris has so far been deployed on.
 */
import { spawn } from 'node:child_process';
import {
  existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync,
} from 'node:fs';
import { dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { copyTree, isMain } from './fsx.mjs';
import { capture, makeChecker, openTranscript } from './rehearsal-kit.mjs';

// ---------------------------------------------------------------------------------------------
// Everything above the divider runs; everything below it is asserted by
// `src/Daoris.Cli/test/deployment-rehearsal.test.ts`. The phases are their own evidence — they
// publish a real folder and start a real window — but the PREDICATES are not: a transcript check
// that compared decoded strings would pass on exactly the bytes this gate exists to catch.

/** The one thing at an install's root a person is meant to run. */
export const SHELL_EXE = 'daoris-desktop.exe';

/** The service host's file name, both halves of the pair using the same spelling. */
export const HOST_EXE = 'daoris-knowledge-http.exe';

/**
 * Where `desktop-publish --service` puts the host inside an install, as path segments.
 *
 * 🔴 This is one half of the counterpart set defect 2a was: the installer's layout and the locator's
 * candidate list must be the same layout, and until a deployment there was nothing that read both.
 * The test beside this file reads `ServiceHostLocator.cs` for the other half.
 */
export const HOST_HOME = ['app', 'daoris-knowledge-http'];

/** Entries at an install's root that a person could double-click. The publish claims there is one. */
export function launchers(entries) {
  return entries.filter((entry) => /\.(exe|cmd|bat)$/i.test(entry));
}

/**
 * Build leftovers the publish claims it does not emit — `DebugType=none` for the symbols, and
 * `AllowedReferenceRelatedFileExtensions=none` for the package doc files, which are what made the
 * script's first guard refuse its own second run.
 */
export function strays(entries) {
  return entries.filter((entry) => /\.(pdb|xml)$/i.test(entry));
}

/**
 * Whether a located host is this workspace's own build.
 *
 * The workspace candidate is the masking agent in 2a: it exists on every developer machine and on no
 * deployed one, so a gate that let the deployed shell land on it would prove nothing. Compared by
 * resolved path segments — a prefix comparison calls a sibling folder `daoris-scratch` part of
 * `daoris`, and the gate would go red for nothing.
 */
export function insideWorkspace(path, repoRoot) {
  const inside = `${resolve(repoRoot)}${sep}`;
  if (!resolve(path).toLowerCase().startsWith(inside.toLowerCase())) return false;
  // The gate's own scratch lives under the workspace and is not part of it — the question is whether
  // the shell fell through to a PROJECT build, which is the candidate a deployment does not have.
  return !resolve(path).toLowerCase().startsWith(join(inside, '_fixtures').toLowerCase());
}

/** The bytes a line occupies in a transcript — computed, so nothing restates the string. */
export function utf8Of(text) {
  return Buffer.from(text, 'utf8');
}

/**
 * 🔴 Bytes, never a decoded string.
 *
 * A transcript mangled by the console codepage is still VALID UTF-8 — that is exactly what made 4c
 * invisible downstream — so a check that decodes first compares one wrong string against another and
 * cannot tell. This compares what is on disk.
 */
export function transcriptHolds(bytes, text) {
  return Buffer.from(bytes).includes(utf8Of(text));
}

/**
 * The example plugin's hook processes among `pid|command line` rows — only those started from under
 * `scratch`. Filtered here rather than by PowerShell's `-like`, whose wildcards a path can contain;
 * compared case-blind because Windows reports a path in whichever case it was spawned with.
 */
export function hookLines(rows, scratch) {
  const under = scratch.toLowerCase();
  return rows.split('\n')
    .map((line) => line.trim().toLowerCase())
    .filter((line) => line.includes('hold-by-title') && line.includes('hooks.mjs') && line.includes(under))
    .map((line) => Number(line.slice(0, line.indexOf('|'))))
    .filter((pid) => Number.isInteger(pid) && pid > 0);
}

// ---------------------------------------------------------------------------------------------

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const examplesRoot = join(repoRoot, 'examples');
const scratch = join(repoRoot, '_fixtures', 'deployment-rehearsal');
const install = join(scratch, 'install');
const home = join(scratch, 'home');
const family = join(scratch, 'family');
const newcomer = join(family, 'newcomer');

const shellExe = join(install, SHELL_EXE);
const installedHost = join(install, ...HOST_HOME, HOST_EXE);

/** The line the stub says, and the line phase 5 looks for on disk. Both halves, one constant. */
const NON_ASCII = 'stub: 道衍 — the unfolding of the way';

const EXAMPLES = ['engine', 'game'];

// Hermetic by construction, exactly as the family rehearsal is: every child points its remote and
// harness lookups at files that do not exist, so the machine's real map can never leak a deployment
// or a credential directory into a gate run.
const HERMETIC = {
  // 🔴 The home (D63) before anything else: an installed shell with no DAORIS_HOME would make the
  // install's own `data/` its home, set the variable in the user's environment and move the real
  // `~/.daoris` in — on the developer's machine, from a gate. Pointed at scratch, it does none of it.
  DAORIS_HOME: home,
  DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json'),
  DAORIS_HARNESS_CONFIG: join(home, 'harnesses.json'),
};

let shell = null;
const children = [];

const run = (command, cwd, env = {}, timeout = 0) => capture(command, cwd, { env, timeout });

const { CLEARED, REDIRECTED, powershell, psQuote, running, stopAll } = await import('./desktop.mjs');
const { freePort } = await import('./cdp.mjs');

/** One HTTP call against whichever host is being asked. Local trust — no key on this door (D21). */
async function api(method, path, base, body) {
  const response = await fetch(`${base}${path}`, {
    method,
    headers: body ? { 'content-type': 'application/json' } : {},
    ...(body ? { body: JSON.stringify(body) } : {}),
  });
  const text = await response.text();
  let json = null;
  try {
    json = JSON.parse(text);
  } catch {
    // not JSON — the text is still worth printing on a failure
  }
  return { status: response.status, json, text, headers: response.headers };
}

/** Wait for a base URL to answer, however it got there. */
async function answers(base, attempts = 200) {
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    try {
      const { status } = await api('GET', '/api/status', base);
      if (status === 200 || status === 401) return true;
    } catch {
      // not listening yet
    }
    await sleep(300);
  }
  return false;
}

/**
 * Every running service host on this machine, as pid and path.
 *
 * 🔴 BY PID, not by path. Two hosts published from the same install are two processes with one
 * path, so a diff on paths reports "no new host" for a host that had just started — which is how
 * this gate's own first run failed a passing check. Whose host it is has to be a process identity.
 */
function hostProcesses() {
  return powershell(
    'Get-Process -ErrorAction SilentlyContinue -Name daoris-knowledge-http | '
    + 'ForEach-Object { "$($_.Id)|$($_.Path)" }')
    .split('\n').map((line) => line.trim()).filter(Boolean)
    .map((line) => {
      const bar = line.indexOf('|');
      return { pid: Number(line.slice(0, bar)), path: line.slice(bar + 1) };
    });
}

/**
 * Every hook process a plugin has up, by command line — the plugin's own program, which the shell
 * starts with its loop and must stop with it (D64 §4: registrations are effects). Matched on the
 * script's name because a plugin process is `node <script>`, and `node` alone is everybody's.
 */
function hookProcesses() {
  // 🔴 `node.exe` only: the PowerShell process running this very query carries the pattern in its
  // own command line and matched itself — one phantom hook process while the shell ran, and one
  // "orphan" after it closed. Found by the first run of this check.
  // 🔴 And only THIS install's (REV3): matched by the pattern alone, the check counted a hook of the
  // same example plugin running anywhere on the machine — another rehearsal's, or the owner's own.
  return hookLines(powershell(
    "Get-CimInstance Win32_Process -Filter \"Name = 'node.exe'\" -ErrorAction SilentlyContinue | "
    + 'ForEach-Object { "$($_.ProcessId)|$($_.CommandLine)" }'), scratch);
}

function stopEverything() {
  if (shell) {
    // Closed, never killed: the shell's own shutdown is what ends the driver loop and the host it
    // owns, and a forced stop orphans both (case study 4c). This is also what phase 6 measures.
    stopAll(shellExe);
    shell = null;
  }
  for (const child of children) {
    if (child && !child.killed) child.kill();
  }
  children.length = 0;
}

/**
 * The gate, whole.
 *
 * 🔴 Inside a function, behind the entry guard below, for the reason `tools/desktop.mjs` states at
 * its own foot: the predicates above are imported by the test suite, and a module that ran its
 * phases at import time would publish a folder and start a window in the middle of `npm test`. It
 * did, once, before this was wrapped — a 70-second unit test and a real install in `_fixtures/`.
 */
async function main() {
  if (process.platform !== 'win32') {
    console.log('deployment rehearsal: the shell is a Windows application (net10.0-windows).');
    console.log('  Nothing here can run on this platform — that is a skip, not a pass.');
    process.exit(2);
  }

  openTranscript(repoRoot, 'deployment', { beforeExit: () => stopEverything() });
  const { totals, check, section } = makeChecker();

  // -------------------------------------------------------------- 1. publish the artefact

  section('1. Publish the artefact');
  stopAll(shellExe);      // a previous run's window would hold its own executable open
  await sleep(300);
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });

  const published = run(
    `node "${join(repoRoot, 'tools', 'desktop-publish.mjs')}" --to "${install}" --service`,
    repoRoot, {}, 15 * 60_000);
  check('publish:desktop --service exits 0', published.code === 0,
    published.out.split('\n').slice(-12).join('\n'));

  const atRoot = existsSync(install) ? readdirSync(install) : [];
  check('one executable at the root, and it is the shell', launchers(atRoot).join() === SHELL_EXE,
    `root holds: ${atRoot.join(', ')}`);
  check('no symbols and no package doc files rode along', strays(atRoot).length === 0,
    strays(atRoot).join(', '));
  check('the marker says whose folder this is',
    existsSync(join(install, 'INSTALLED.md'))
    && readFileSync(join(install, 'INSTALLED.md'), 'utf8').startsWith('# Daoris — installed desktop'));

  // The counterpart set, on a real publish: the host is where the locator looks, and its bundle is
  // beside it. A host installed without its page answers every API call and serves 404 for the UI.
  check(`the host is under ${HOST_HOME.join('/')}/`, existsSync(installedHost), installedHost);
  check('…and its bundle travelled beside it',
    existsSync(join(install, ...HOST_HOME, 'wwwroot', 'index.html')));

  // Framework-dependent on purpose (D43's opposite case): it carries no .NET this machine has.
  const shellSize = existsSync(shellExe) ? statSync(shellExe).size : 0;
  check('the shell is one small file, not a self-contained runtime',
    shellSize > 0 && shellSize < 40 * 1024 * 1024, `${Math.round(shellSize / 1024 / 1024)} MB`);

  // -------------------------------------------------------------- 2. the publish guard

  section('2. The publish refuses a folder it did not write');
  const foreign = join(scratch, 'someones-documents');
  mkdirSync(foreign, { recursive: true });
  writeFileSync(join(foreign, 'notes.txt'), 'someone else was here\n');

  const refused = run(
    `node "${join(repoRoot, 'tools', 'desktop-publish.mjs')}" --to "${foreign}" --service`,
    repoRoot, {}, 60_000);
  check('a folder holding something else is refused', refused.code === 2, refused.out);
  check('…and the refusal names what it found', /notes\.txt/.test(refused.out), refused.out);
  check('…and it refused BEFORE building anything',
    readdirSync(foreign).join() === 'notes.txt', readdirSync(foreign).join(', '));

  // The door through the guard: `--beside` installs next to other things — the repositories the
  // application drives, in the second deployment — and still refuses a name it would write OVER.
  // The accepting branch is unit-tested (`desktop-publish.test.ts`) and costs a full build here; the
  // refusing branch is free, so it is the one this gate drives.
  mkdirSync(join(foreign, 'app'));
  const besideRefused = run(
    `node "${join(repoRoot, 'tools', 'desktop-publish.mjs')}" --to "${foreign}" --service --beside`,
    repoRoot, {}, 60_000);
  check('--beside still refuses a folder where `app/` is somebody else’s',
    besideRefused.code === 2 && /\bapp\b/.test(besideRefused.out), besideRefused.out);
  check('…and touched nothing there', readdirSync(foreign).sort().join() === 'app,notes.txt',
    readdirSync(foreign).join(', '));

  /**
   * 🔴 A re-publish REPLACES the bundle rather than merging into it.
   *
   * A copy over the old directory leaves every previous hashed bundle in `wwwroot/assets`, and
   * `index.html` names only the current one — so the folder is *correct* and unreadable. That is not
   * a hypothetical cost: a real install reached **seven** bundles, and listing it produced a wrong
   * answer about which build was live **twice** — once in the first-deployment case study, and once
   * while re-publishing to this machine afterwards. `service-publish.mjs` already replaces for
   * exactly this reason; its sibling did not, which is the whole of the bug.
   *
   * This also proves the marker path at the same time: a second publish into a folder this script
   * wrote is accepted, where the phase above shows a foreign one refused.
   */
  const assets = join(install, ...HOST_HOME, 'wwwroot', 'assets');
  const decoy = join(assets, 'index-STALEBUNDLE.js');
  // Made if missing: when the first publish failed, the folder is not there, and a gate that
  // crashes on ENOENT here reports a stack trace instead of the publish check that already failed.
  mkdirSync(assets, { recursive: true });
  writeFileSync(decoy, '// a bundle from a publish that is no longer current\n');

  const republished = run(
    `node "${join(repoRoot, 'tools', 'desktop-publish.mjs')}" --to "${install}" --service`,
    repoRoot, {}, 15 * 60_000);
  check('a second publish into its own install is accepted', republished.code === 0,
    republished.out.split('\n').slice(-8).join('\n'));
  check('…and it left no bundle from the publish before it', !existsSync(decoy),
    readdirSync(assets).filter((entry) => entry.endsWith('.js')).join(', '));

  // Whatever survives, `index.html` has to name something that is actually there — the check that
  // would still hold if the replacement were done some other way.
  const indexHtml = readFileSync(join(install, ...HOST_HOME, 'wwwroot', 'index.html'), 'utf8');
  const named = /assets\/(index-[\w.-]+\.js)/.exec(indexHtml)?.[1] ?? '';
  check('…and the page names a bundle the install actually has',
    Boolean(named) && existsSync(join(assets, named)), `index.html names ${named || '(nothing)'}`);

  // -------------------------------------------------------------- 3. the install is self-sufficient

  section('3. The install carries a host that serves the install’s own page');
  const hostPort = await freePort(5301);
  const hostBase = `http://127.0.0.1:${hostPort}`;
  mkdirSync(home, { recursive: true });
  mkdirSync(join(scratch, 'standalone-family'), { recursive: true });

  const standalone = spawn(installedHost, {
    cwd: join(install, ...HOST_HOME),
    stdio: 'ignore',
    env: {
      ...process.env,
      ...HERMETIC,
      ASPNETCORE_URLS: hostBase,
      DAORIS_KNOWLEDGE_DB: join(scratch, 'standalone.db'),
      DAORIS_KNOWLEDGE_ROOT: join(scratch, 'standalone-family'),
    },
  });
  children.push(standalone);

  check('the published host answers on its own', await answers(hostBase, 120));

  const page = await api('GET', '/', hostBase);
  const asset = /\/assets\/([\w.-]+\.js)/.exec(page.text)?.[1] ?? '';
  check('…and serves a page, not a 404 for a missing bundle', page.status === 200 && Boolean(asset),
    page.text.slice(0, 200));
  check('…and the bundle it names is the one in its own wwwroot',
    Boolean(asset) && existsSync(join(install, ...HOST_HOME, 'wwwroot', 'assets', asset)),
    asset);

  // 🔴 The page is UNHASHED and names the hashed assets, so it is the page that decides what the
  // window runs — and a browser told nothing reuses it without asking. Seen on the second
  // deployment: a page loaded once from a stale host was the page on every start after, answered
  // from the WebView2 profile's cache with the correct host running and never asked. A hashed
  // asset may be kept for good; the page must be asked about every time (the ETag makes that a 304).
  const pageCaching = page.headers.get('cache-control') ?? '(none)';
  check('…and it tells the browser to ask about the page again next time',
    pageCaching === 'no-cache', `cache-control: ${pageCaching}`);
  const bundle = await api('GET', `/assets/${asset}`, hostBase);
  const bundleCaching = bundle.headers.get('cache-control') ?? '(none)';
  check('…and that the hashed bundle may be kept for good',
    bundle.status === 200 && /immutable/.test(bundleCaching), `cache-control: ${bundleCaching}`);

  // Stopped BEFORE the baseline below is taken, and asserted rather than assumed: a survivor would
  // be counted as the shell's own host in phase 4 and as an orphan in phase 6.
  standalone.kill();
  children.length = 0;
  await sleep(1200);   // the port and the store's handle outlive the kill by a beat on Windows
  check('…and it stops when it is told to',
    !hostProcesses().some((host) => host.pid === standalone.pid));

  // -------------------------------------------------------------- 4. the deployed shell runs

  section('4. The deployed shell comes up and finds a host — with nothing telling it where');

  // The family the store bootstrap-imports on first sight, copied rather than pointed at: the
  // tracked examples are a gate's fixture and the shell can write a repository's own daoris.json.
  for (const name of EXAMPLES) copyTree(join(examplesRoot, name), join(family, name));

  // The stub agent — real mechanics, no model (D46 §8). It says one line with an em-dash and two
  // Chinese characters in it, which is the whole of what phase 5 reads back off the disk.
  const stubAgent = join(scratch, 'stub-agent.mjs');
  writeFileSync(stubAgent, `
import { execSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';

if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }

const url = process.env.DAORIS_SERVICE_URL;
const id = process.env.DAORIS_QUEST_ID;
const respond = async (action, reason) => {
  const response = await fetch(url + '/api/quests/' + id + '/respond', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ action, reason }),
  });
  return { ok: response.ok, text: await response.text() };
};

const take = await respond('take', null);
if (!take.ok) { if (/already taken/i.test(take.text)) process.exit(0); throw new Error(take.text); }

// 🔴 The line this gate exists for. Node writes UTF-8 to a pipe whatever the console is set to, so
// what reaches the transcript is decided entirely by how the DEPLOYED SHELL decodes this stream.
console.log(${JSON.stringify(NON_ASCII)});

writeFileSync('answered-' + id + '.md', 'Answered by the stub session.\\n');
const git = 'git -c user.name="Deployment Rehearsal" -c user.email="rehearsal@example.invalid"';
execSync(git + ' add -A', { stdio: 'ignore' });
execSync(git + ' commit -q -m "stub: answer quest ' + id + '"', { stdio: 'ignore' });
const done = await respond('done', 'Landed by the stub session.');
if (!done.ok) throw new Error(done.text);
`);

  // A plugin that SPEAKS (D64), in the scratch home before the shell starts — the tracked example,
  // as a person would have added it. The family rehearsal drives the headless host's loop with it;
  // this is the only gate that drives the SHELL's, which owns the processes differently (it starts
  // them in its loop and stops them in its Stop), and is what a deployed machine actually runs.
  copyTree(join(examplesRoot, 'plugins', 'hold-by-title'), join(home, 'plugins', 'hold-by-title'));

  // The person's standing choices, scratch-local. Written BEFORE the shell starts: the loop begins
  // with the app, and a config arriving late makes the first ticks say "nothing is opted in".
  writeFileSync(join(home, 'driver.json'), `${JSON.stringify({
    drivable: ['newcomer'],
    adapter: 'stub',
    cap: 1,
    timeoutMinutes: 3,
    pollSeconds: 2,
    commands: { stub: ['node', stubAgent] },
    notify: false,
  }, null, 2)}\n`);

  const shellPort = await freePort(5311);
  const base = `http://127.0.0.1:${shellPort}`;

  /**
   * What a deployed shell is given, and — more importantly — what it is not.
   *
   * 🔴 `DAORIS_HTTP_HOST` is deliberately ABSENT. Naming the host is what `tools/desktop.mjs` does
   * to keep a dev loop honest, and it is exactly the variable that would skip the code path 2a
   * broke. Same for `--app-root`: the deployed shell's own default is `data/` beside the
   * executable, and the install is scratch, so the default already isolates this run.
   */
  const shellEnvironment = {
    ...HERMETIC,
    DAORIS_SERVICE_URL: base,
    ASPNETCORE_URLS: base,
    DAORIS_KNOWLEDGE_DB: join(home, 'knowledge.db'),
    DAORIS_KNOWLEDGE_ROOT: family,
    DAORIS_DRIVER_CONFIG: join(home, 'driver.json'),
  };

  // The pair-check `desktop-tool.test.ts` holds for the dev loop, applied to this run: a
  // machine-local file nobody redirected is not a broken gate, it is the person's own Daoris being
  // driven by one.
  const unredirected = REDIRECTED.filter((name) => !(name in shellEnvironment));
  check('every machine-local file this run reads points into scratch', unredirected.length === 0,
    `${unredirected.join(', ')} would come from the machine's real home`);

  // 🔴 The decoy, planted rather than hoped for. Before D63 it was whatever `publish:service
  // --install` had left under the machine's profile, which a clean machine never has — so the check
  // below was only ever a check on this machine. The home is scratch now, so the gate puts an
  // installed-looking host where the locator's next candidate looks: an empty file, because the
  // ORDER is the contract, and a shell that ranked it first would try to start it and fail here.
  const installedHome = join(home, 'bin', 'daoris-knowledge-http');
  mkdirSync(installedHome, { recursive: true });
  writeFileSync(join(installedHome, HOST_EXE), '');

  const before = new Set(hostProcesses().map((host) => host.pid));
  const environment = { ...process.env, ...shellEnvironment };
  for (const name of CLEARED) delete environment[name];

  shell = spawn(shellExe, { cwd: install, env: environment, detached: true, stdio: 'ignore' });
  shell.unref();

  check('the platform answers — the deployed shell brought a host up', await answers(base, 200));

  // 🔴 The install's OWN host, not merely "not this workspace's". The weaker check passed 32/32 on
  // a machine whose installed home held an older host, while the transcript's own `located:` line
  // named it — and the second deployment showed a window serving a bundle that existed nowhere on
  // disk but there. The decoy above is what makes the two checks differ on every machine.
  const started = hostProcesses().filter((host) => !before.has(host.pid));
  const ownHost = join(install, ...HOST_HOME, HOST_EXE);
  check('a host was started, and it is the one published with the install',
    started.length > 0
      && started.every((host) => resolve(host.path) === resolve(ownHost))
      && !started.some((host) => insideWorkspace(host.path, repoRoot)),
    started.length
      ? started.map((host) => host.path).join('\n          ')
      : 'no new host process appeared');
  console.log(`        located: ${started.map((host) => host.path).join(', ') || '(none)'}`);

  const window = powershell(
    `Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq ${psQuote(shellExe)} } | `
    + 'ForEach-Object { "$($_.MainWindowHandle)|$($_.MainWindowTitle)" }').trim();
  check('the window is up', Boolean(window) && !window.startsWith('0|'), window || '(no process)');

  // INSTALLED.md tells whoever opens the folder that `data/` is this install's own state. Nothing
  // read it back until now, and a deployed shell writing its WebView2 profile somewhere else is a
  // folder that cannot be deleted to uninstall.
  check('the install keeps its own state in data/', existsSync(join(install, 'data')),
    readdirSync(install).join(', '));

  // The plugin's process, started by the DEPLOYED shell's own loop from the home it was told. The
  // loop reconciles on its first tick, which follows the host coming up; waited for, never poked.
  let hooks = [];
  for (let attempt = 0; attempt < 30 && hooks.length === 0; attempt += 1) {
    await sleep(1000);
    hooks = hookProcesses();
  }
  check('the deployed shell started the plugin it found under its home', hooks.length === 1,
    `${hooks.length} hook process(es) named hold-by-title/hooks.mjs`);

  // -------------------------------------------------------------- 5. a session, and its transcript

  section('5. A session the DEPLOYED shell spawned, and what reached its transcript');

  mkdirSync(newcomer, { recursive: true });
  writeFileSync(join(newcomer, 'README.md'), '# newcomer\n\nBorn during the deployment rehearsal.\n');

  const cliEnv = { ...HERMETIC, DAORIS_SERVICE_URL: base };
  check('newcomer: init writes a manifest',
    run(`node "${cliBin}" init`, newcomer, cliEnv).code === 0
    && existsSync(join(newcomer, 'daoris.json')));

  const manifestPath = join(newcomer, 'daoris.json');
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  manifest.domain = {
    summary: 'Born during the deployment rehearsal.',
    owns: ['being driven by an installed application'],
    accepts: ['a quest that has to survive a codepage'],
  };
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  check('newcomer: sync materializes the canon',
    run(`node "${cliBin}" sync`, newcomer, cliEnv).code === 0);

  // A session spawns only onto a clean git tree. Identity per-command: a machine with no git config
  // at all must still be able to run this gate.
  const GIT_ID = '-c user.name="Deployment Rehearsal" -c user.email="rehearsal@example.invalid"';
  run('git init -q', newcomer);
  run(`git ${GIT_ID} add -A`, newcomer);
  run(`git ${GIT_ID} commit -q -m "the newcomer is born"`, newcomer);

  const connected = run(`node "${cliBin}" connect`, newcomer, cliEnv);
  check('newcomer: connect reaches the deployed shell’s host', connected.code === 0, connected.out);

  const quest = await api('POST', '/api/quests', base, {
    from: 'game',
    to: 'newcomer',
    title: 'Survive a deployment',
    body: 'Say one line with an em-dash and two Chinese characters in it, and land the work.',
  });
  check('a quest reaches the drivable repository', quest.status === 200, quest.text);
  const questId = quest.json?.quest?.id ?? '';

  // The driver loop is the shell's own, ticking every two seconds — waited for, never poked.
  let record = null;
  for (let attempt = 0; attempt < 120; attempt += 1) {
    const sessions = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true', base);
    record = (sessions.json ?? []).find((session) => session.quest === questId) ?? record;
    if (record?.state === 'completed') break;
    await sleep(1000);
  }

  check('the deployed shell’s driver loop spawned a session and observed it through',
    record?.state === 'completed', JSON.stringify(record));
  const closed = await api('GET', '/api/quests?repository=newcomer&includeClosed=true', base);
  check('the quest was closed by the SESSION, through its own door',
    (closed.json ?? []).some((q) => q.id === questId && q.status === 'Done'), closed.text);
  check('the record carries a transcript path', Boolean(record?.transcript), JSON.stringify(record));

  // 🔴 Defect 4c, read off the disk as bytes.
  const bytes = existsSync(record?.transcript ?? '')
    ? readFileSync(record.transcript)
    : Buffer.alloc(0);
  check('the transcript holds what the session said, byte for byte',
    transcriptHolds(bytes, NON_ASCII),
    `${record?.transcript}\n          it decodes to: ${bytes.toString('utf8').split('\n')
      .find((line) => line.includes('stub:')) ?? '(no stub line at all)'}`);

  // The plugin was asked before the start (it allowed — the title carries no `[hold]`) and told of
  // the ending, by the shell's loop; what it kept landed beside its install, in ITS data folder.
  const endedLog = join(home, 'plugins', '.data', 'hold-by-title', 'ended.log');
  let ended = '';
  for (let attempt = 0; attempt < 15 && !ended.includes(record?.id ?? '\0'); attempt += 1) {
    await sleep(1000);
    ended = existsSync(endedLog) ? readFileSync(endedLog, 'utf8') : '';
  }
  check('the plugin was told of the ending by the deployed shell’s loop, and kept it in its data folder',
    Boolean(record?.id) && new RegExp(`${record.id} ${questId} newcomer completed stub`).test(ended),
    ended || '(no ended.log)');

  // -------------------------------------------------------------- 6. it stops without orphaning

  section('6. Closing the shell stops what the shell owns');
  stopAll(shellExe);
  shell = null;
  await sleep(1500);

  check('no shell of this install is left running', running(shellExe).length === 0);
  const orphans = hostProcesses().filter((host) => !before.has(host.pid));
  check('and no host it started is orphaned', orphans.length === 0,
    orphans.map((host) => `pid ${host.pid} ${host.path}`).join(', '));
  // Registrations are effects (D64 §4): the plugin's process is exactly as alive as the loop.
  const hooksLeft = hookProcesses();
  check('and the plugin’s process went with the loop', hooksLeft.length === 0,
    hooksLeft.map((pid) => `pid ${pid}`).join(', '));

  // -------------------------------------------------------------- 7. report

  section('7. Result');
  stopEverything();
  await sleep(500);

  console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
  if (totals.failures) {
    console.log('  The deployed artefact is NOT proven — the transcript names the first failure.');
    console.log('  Scratch left at _fixtures/deployment-rehearsal for inspection.');
    process.exitCode = 1;
    return;
  }

  console.log('  The shell was published to a folder and driven from there: one launcher at the root');
  console.log('  with no symbols beside it, a host under app/ with its own bundle answering on its');
  console.log('  own, a folder somebody else owns refused before anything was built — then the');
  console.log('  DEPLOYED window, told nothing about where its host lives, finding one that is not');
  console.log('  this workspace’s build and keeping its state in data/ beside itself. Then a quest:');
  console.log('  spawned by the installed application’s own driver loop, carried to done through the');
  console.log('  session’s own door, and its transcript holding an em-dash and 道衍 BYTE FOR BYTE —');
  console.log('  which is the defect a console codepage hid behind valid UTF-8. A plugin found under');
  console.log('  the install’s own home was started by that loop, asked before the start, told of the');
  console.log('  ending, and kept it beside itself. Closed, not killed, and the host it owned and the');
  console.log('  plugin’s process went with it.');
  rmSync(scratch, { recursive: true, force: true });
}

/**
 * Only when this file is what was run — the same guard `tools/desktop.mjs` carries, for the same
 * reason and after the same mistake.
 */
if (isMain(import.meta.url)) {
  await main();
}

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
 * And a third, held since DEPLOY5 (FIX-LOG, 2026-09-25): a chat open when the shell closed came back
 * ended by the next start's SWEEP, because the shell's own shutdown disposed the client the chat's
 * record concluded through before it stopped the chat. The driver tests at the runner passed; only
 * the window saw it. So phase 6 closes the shell with a conversation open, and phase 7 reads the note
 * the close wrote.
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
 *  - **This start has a debug port, and a person's does not** (DEPLOY5). A chat starts over the
 *    bridge, which only the page holds, so the gate reaches the page the way `run --install` does:
 *    `debugEnvironment`, through the environment, on a port picked for this run. The port needs the
 *    kit's development switch, and the window and everything it starts run with it. Nothing of
 *    Daoris's own reads that switch; the kit does (the port), and so would the ASP.NET host, which is
 *    why the host is pinned to production beside it. That pin rests on the framework's documented
 *    order and is not measured here; phase 3's host, started directly, is production regardless.
 */
import { spawn } from 'node:child_process';
import {
  existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync,
} from 'node:fs';
import { dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { copyTree, isMain } from './fsx.mjs';
import {
  ACP_STUB_AGENT, capture, makeChecker, openTranscript,
} from './rehearsal-kit.mjs';
// The install's layout, from the script that makes it (REV3 CLEAN1) — never a second spelling of it.
import {
  HOME, HOST_EXE, HOST_HOME, KEPT_LOCALES, LAUNCHER, OFFERED_PLUGINS, OWN, PLUGIN_OFFERS, RETIRED_BROWSER_EXE, RETIRED_IN_APP,
  RETIRED_LAUNCHERS, SHELL_EXE, SHELL_FILES, SHELL_HOME,
} from './desktop-publish.mjs';

// ---------------------------------------------------------------------------------------------
// Everything above the divider runs; everything below it is asserted by
// `src/Daoris.Cli/test/deployment-rehearsal.test.ts`. The phases are their own evidence — they
// publish a real folder and start a real window — but the PREDICATES are not: a transcript check
// that compared decoded strings would pass on exactly the bytes this gate exists to catch.

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

/**
 * What is wrong with the install's offers (PLUG9 d, D103), as sentences: an offered plugin the install does
 * not carry in `app/plugin-offers/`, and 🔴 one installed under a home, which a publish must never do — an
 * offer is installed only by a person's press. Empty when the offers are as the publish claims.
 */
export function offerProblems(install, homes) {
  const problems = [];
  for (const id of OFFERED_PLUGINS) {
    if (!existsSync(join(install, ...PLUGIN_OFFERS, id, 'plugin.json'))) problems.push(`${PLUGIN_OFFERS.join('/')}/${id} is not carried`);
    for (const home of homes) {
      if (existsSync(join(home, 'plugins', id))) problems.push(`${id} is installed under ${home}`);
    }
  }
  return problems;
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

/**
 * The note a conversation's record takes when the shell holding it closes: `ChatRunner.ClosedNote`,
 * spelled a second time on purpose (`twins.md`), and `deployment-rehearsal.test.ts` reads both.
 */
export const CLOSED_NOTE =
  'the application closed while this conversation ran; its process was ended with it.';

/**
 * The note the next start's sweep writes on a record nothing runs any more: `Orphans.Note`, read the
 * same way. The same `stopped` as the close's, which is why the gate reads the note.
 */
export const SWEPT_NOTE =
  'nothing on this machine was running it any more — its process ended with the application that '
  + 'started it, and the record had not been told.';

/**
 * Whether a conversation's record was concluded by the shell's own close (DEPLOY5).
 *
 * 🔴 By its note, never its state. The FIX-LOG entry of 2026-09-25 found this on the window after the
 * driver tests passed: a chat open at close came back `stopped`, but with the SWEEP's note, because
 * the close had written nothing and the next start found the record claiming a process. The state
 * alone passes on exactly that defect.
 */
export function concludedByTheClose(record) {
  return record?.state === 'stopped' && record.note === CLOSED_NOTE;
}

/**
 * The marker a tracked process leaves under the home's `sessions/`, as `<pid> <start ticks>`, or null
 * for anything else. Read as `SessionProcesses.AliveOnThisMachine` reads it. The ticks are a BigInt:
 * .NET's ticks outgrow a JavaScript number, and a rounded start time would name a different second.
 */
export function markedProcess(text) {
  const fields = String(text ?? '').trim().split(' ');
  if (fields.length !== 2 || !fields.every((field) => /^\d+$/.test(field))) return null;
  return { pid: Number(fields[0]), started: BigInt(fields[1]) };
}

/**
 * Whether the process a marker names is the one running under its pid now: the start time `startedNow`
 * (UTC ticks, as PowerShell prints them, or empty for no such process) within a second of the marker's.
 * The pid alone could be a process the machine has since given that number to.
 */
export function isMarkedProcess(marked, startedNow) {
  if (!marked || !/^\s*\d+\s*$/.test(String(startedNow ?? ''))) return false;
  const apart = BigInt(String(startedNow).trim()) - marked.started;
  return (apart < 0n ? -apart : apart) < 10_000_000n;
}

/**
 * One call to a module of the shell, made from INSIDE its page, and its answer (DEPLOY5).
 *
 * A chat starts over the bridge, and the bridge is the page's alone, so the gate calls it where the
 * page would: this function is sent over the debug port as source (`bridgeCall`) and runs in the page,
 * which is why it closes over nothing and every name in it is a parameter or a page global. It speaks
 * the kit's Chromium transport as the page's own bridge does: the envelope is posted to the route the
 * marker names, and the answer is pushed back through the marker's `receive`.
 *
 * That `receive` belongs to the page's bridge, so this listens BESIDE it: every push still reaches the
 * page, in order, and the page has its own `receive` back once the answer has come. A page whose
 * bridge is not up yet is told so before anything is posted, since a reply pushed then would go to a
 * `receive` the page replaces as it starts.
 *
 * Answers `{ ok: true, data }` or `{ ok: false, error: { code, message } }`, never a throw: a refusal is
 * what a check prints.
 */
export function invokeInPage(host, module, type, payload, timeoutMs = 30000) {
  const marker = host.__shenora_chromium;
  if (!marker || typeof marker.ipc !== 'string' || typeof marker.receive !== 'function') {
    return Promise.resolve({ ok: false, error: { code: 'NOT_READY', message: 'the page has no bridge yet' } });
  }

  const id = `deploy5-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  const page = marker.receive;
  return new Promise((resolve) => {
    let timer = null;
    let listener = null;
    const done = (answer) => {
      clearTimeout(timer);
      if (marker.receive === listener) marker.receive = page;
      resolve(answer);
    };
    listener = (message) => {
      try {
        let reply = null;
        try { reply = JSON.parse(message); } catch { reply = null; }
        if (reply && reply.category === 'ipc' && reply.id === id) {
          done(reply.success
            ? { ok: true, data: reply.data ?? null }
            : { ok: false, error: reply.error ?? { code: 'UNKNOWN_ERROR', message: 'refused, with no error' } });
        }
      } finally {
        page(message);
      }
    };
    marker.receive = listener;
    timer = setTimeout(() => done({
      ok: false, error: { code: 'TIMEOUT', message: `${module}.${type} had no answer within ${timeoutMs} ms` },
    }), timeoutMs);
    let posted;
    try {
      posted = host.fetch(marker.ipc, {
        method: 'POST',
        body: JSON.stringify({ id, module, type, payload, timestamp: new Date().toISOString() }),
      });
    } catch (error) {
      posted = Promise.reject(error);
    }
    Promise.resolve(posted)
      .catch((error) => done({ ok: false, error: { code: 'NO_TRANSPORT', message: String(error?.message ?? error) } }));
  });
}

/** {@link invokeInPage} as an expression for `Runtime.evaluate` in the shell's page. */
export function bridgeCall(module, type, payload, timeoutMs = 30000) {
  return `(${invokeInPage.toString()})(window, ${JSON.stringify(module)}, ${JSON.stringify(type)}, `
    + `${JSON.stringify(payload ?? null)}, ${Number(timeoutMs)})`;
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

/** What a person double-clicks, and the application it starts from `app/` (D93), which holds the window. */
const launcherExe = join(install, LAUNCHER);
const shellExe = join(install, ...SHELL_HOME, SHELL_EXE);
const installedHost = join(install, ...HOST_HOME, HOST_EXE);
/** Where the browser lived before CHR8, with an engine of its own: a republish removes it (D93, D99). */
const retiredBrowser = join(install, ...SHELL_HOME, RETIRED_IN_APP[0]);

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

const {
  CLEARED, PAGE_IDENTITY, REDIRECTED, debugEnvironment, isShell,
} = await import('./desktop.mjs');
const {
  applicationsAt, browsersAt, eachApplicationAt, powershell, psQuote, running, stopAll,
} = await import('./processes.mjs');
const {
  Cdp, freePort, pickPageTarget, targetsAt,
} = await import('./cdp.mjs');

const API_TIMEOUT = 30_000;

/**
 * One HTTP call against whichever host is being asked. Local trust — no key on this door (D21).
 *
 * Bounded (REV3 CLEAN1), as the family rehearsal's is: no answer in time is an answer a check fails
 * on, where a bare `fetch` would hang the gate. A refused connection still throws, which is what
 * `answers` waits through.
 */
async function api(method, path, base, body) {
  let response;
  let text;
  try {
    response = await fetch(`${base}${path}`, {
      method,
      headers: body ? { 'content-type': 'application/json' } : {},
      ...(body ? { body: JSON.stringify(body) } : {}),
      signal: AbortSignal.timeout(API_TIMEOUT),
    });
    text = await response.text();
  } catch (error) {
    if (error?.name !== 'TimeoutError') throw error;
    return {
      status: 0, json: null, text: `no answer to ${method} ${path} within ${API_TIMEOUT / 1000}s`, headers: new Headers(),
    };
  }
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

/** A process's start time as UTC ticks, or empty when no process has that id: what `isMarkedProcess` compares. */
function startedTicks(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return '';
  return powershell(
    `$p = Get-Process -Id ${pid} -ErrorAction SilentlyContinue; if ($p) { $p.StartTime.ToUniversalTime().Ticks }`).trim();
}

/** A promise, or `fallback` once `ms` have passed: a CDP socket that dies mid-call never answers. */
const bounded = (promise, ms, fallback) => Promise.race([promise, sleep(ms, fallback)]);

/**
 * The shell's own page over the debug port this run opened, identified before anything is asked of
 * it (`isShell`): a port is not ours because a page answers on it, and a chat opened in the wrong page
 * would be a claim about a shell nobody started. `{ cdp: null, found }` says what was there instead.
 */
async function shellPage(port, serviceUrl) {
  let target = null;
  for (let attempt = 0; attempt < 40 && !target; attempt += 1) {
    target = pickPageTarget(await targetsAt(port).catch(() => null), null);
    if (!target) await sleep(500);
  }
  if (!target) return { cdp: null, found: `no page of the application's on the debug port ${port}` };

  let cdp = null;
  try {
    cdp = await bounded(new Cdp(target.webSocketDebuggerUrl).open(), 10_000, null);
    const page = cdp ? await bounded(cdp.evaluate(PAGE_IDENTITY), 10_000, null) : null;
    if (isShell(page, serviceUrl)) return { cdp, found: JSON.stringify(page) };
    cdp?.close();
    return { cdp: null, found: `not this shell's page: ${JSON.stringify(page ?? target)}` };
  } catch (error) {
    cdp?.close();
    return { cdp: null, found: `the page on ${port} did not answer: ${error.message}` };
  }
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
  const inApp = existsSync(join(install, ...SHELL_HOME)) ? readdirSync(join(install, ...SHELL_HOME)) : [];
  check('one executable at the root, and it is the launcher', launchers(atRoot).join() === LAUNCHER,
    `root holds: ${atRoot.join(', ')}`);
  // A regular application's root (D93): the launcher, `app/` and the marker — `data/` arrives on the
  // first start — and nothing else.
  check('…and nothing at the root but what makes an install',
    atRoot.every((name) => OWN.includes(name)), `root holds: ${atRoot.join(', ')}`);
  check('no symbols and no package doc files rode along', strays([...atRoot, ...inApp]).length === 0,
    strays([...atRoot, ...inApp]).join(', '));
  check('the marker says whose folder this is',
    existsSync(join(install, 'INSTALLED.md'))
    && readFileSync(join(install, 'INSTALLED.md'), 'utf8').startsWith('# Daoris — installed desktop'));

  // The counterpart set, on a real publish: the host is where the locator looks, and its bundle is
  // beside it. A host installed without its page answers every API call and serves 404 for the UI.
  check(`the host is under ${HOST_HOME.join('/')}/`, existsSync(installedHost), installedHost);
  check('…and its bundle travelled beside it',
    existsSync(join(install, ...HOST_HOME, 'wwwroot', 'index.html')));

  // One Chromium (CHR8, D99): Daoris's browser is the application started with the browser's
  // argument, so no folder under `app/` carries an executable and an engine of its own.
  check('one Chromium: the browser has no folder and no engine of its own under app/',
    RETIRED_IN_APP.every((name) => !inApp.includes(name)) && !inApp.some((name) => /cefsharp/i.test(name)),
    `app holds: ${inApp.join(', ')}`);

  // The launcher is small and carries no runtime: it starts the application and exits.
  const launcherSize = existsSync(launcherExe) ? statSync(launcherExe).size : 0;
  check('the launcher is one small file, not a self-contained runtime',
    launcherSize > 0 && launcherSize < 5 * 1024 * 1024, `${Math.round(launcherSize / 1024)} KB`);

  // The application on its own Chromium (D92, D93): CEF's launcher in `app/`, with the engine and the
  // app beside it. Framework-dependent on purpose (D43's opposite case): no .NET this machine has.
  check(`the application is ${[...SHELL_HOME, SHELL_EXE].join('/')}, with its engine beside it`,
    [SHELL_EXE, 'libcef.dll', 'icudtl.dat', 'resources.pak', 'Daoris.Desktop.App.dll'].every((name) => inApp.includes(name)),
    `app holds: ${inApp.join(', ')}`);
  // What Task Manager and the taskbar call the window: a copied CEF launcher said "CEF Bootstrap
  // Application" until Daoris's name was stamped onto it (D93; by the kit since Shenora 0.18, SHEN1).
  const described = existsSync(shellExe)
    ? powershell(`(Get-Item -LiteralPath ${psQuote(shellExe)}).VersionInfo.FileDescription`).trim()
    : '';
  check('…and it is called Daoris, not by the launcher it was copied from', described === 'Daoris',
    `FileDescription: ${described || '(none)'}`);
  check('…framework-dependent: no .NET runtime rode along',
    ![...atRoot, ...inApp].some((name) => /^(coreclr|hostfxr|hostpolicy)\.dll$/i.test(name)));
  const shellLocales = existsSync(join(install, ...SHELL_HOME, 'locales'))
    ? readdirSync(join(install, ...SHELL_HOME, 'locales')).sort()
    : [];
  check('…and only the locales the install keeps',
    shellLocales.join() === [...KEPT_LOCALES].sort().join(), `locales: ${shellLocales.join(', ') || '(none)'}`);
  const recorded = existsSync(join(install, ...SHELL_FILES))
    ? readFileSync(join(install, ...SHELL_FILES), 'utf8').split('\n').filter(Boolean).sort()
    : [];
  // Beside the application's own files: the host, the record itself, and the offers (PLUG9 d).
  const besideIt = [HOST_HOME.at(-1), SHELL_FILES.at(-1), PLUGIN_OFFERS.at(-1)];
  check(`…and ${SHELL_FILES.join('/')} names every file the application put there, and nothing else`,
    recorded.includes(SHELL_EXE)
      && recorded.join() === inApp.filter((name) => !besideIt.includes(name)).sort().join(),
    `recorded: ${recorded.join(', ') || '(nothing)'}`);

  // Daoris's own example plugins (PLUG9 d, D103): carried as offers beside the application, and none
  // installed — the install's `data/` does not exist yet, and nothing a publish writes goes into it.
  const laidOut = existsSync(join(install, ...PLUGIN_OFFERS)) ? readdirSync(join(install, ...PLUGIN_OFFERS)).sort() : [];
  check(`the install carries Daoris’s own plugins as offers in ${PLUGIN_OFFERS.join('/')}/, and only those`,
    offerProblems(install, []).length === 0 && laidOut.join() === [...OFFERED_PLUGINS].sort().join(),
    `offered: ${laidOut.join(', ') || '(nothing)'}; ${offerProblems(install, []).join('; ')}`);
  check('…and the publish installed none of them', !existsSync(join(install, HOME, 'plugins')),
    `${join(install, HOME, 'plugins')} exists`);

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

  // The shell's own leftovers (CHR4), planted the same way: the single-file launcher an install from
  // before CHR4 still has, and an engine file the last publish recorded that this build no longer
  // ships — an engine upgrade that dropped a library. Both are this script's, so both must go, and a
  // neighbour's file beside them must not.
  const retired = join(install, RETIRED_LAUNCHERS[0]);
  writeFileSync(retired, '');
  // …and the browser's own folder an install from before CHR8 has, with its executable and its second
  // engine: this script wrote it once, and writes it no more (D99).
  mkdirSync(join(retiredBrowser, 'locales'), { recursive: true });
  writeFileSync(join(retiredBrowser, RETIRED_BROWSER_EXE), '');
  writeFileSync(join(retiredBrowser, 'libcef.dll'), '');
  const staleEngine = join(install, ...SHELL_HOME, 'stale-engine.dll');
  writeFileSync(staleEngine, '');
  if (existsSync(join(install, ...SHELL_FILES))) {
    writeFileSync(join(install, ...SHELL_FILES),
      `${readFileSync(join(install, ...SHELL_FILES), 'utf8')}stale-engine.dll\n`);
  }
  const neighbour = join(install, 'a-neighbours-notes.txt');
  writeFileSync(neighbour, 'not the application’s\n');

  const republished = run(
    `node "${join(repoRoot, 'tools', 'desktop-publish.mjs')}" --to "${install}" --service`,
    repoRoot, {}, 15 * 60_000);
  check('a second publish into its own install is accepted', republished.code === 0,
    republished.out.split('\n').slice(-8).join('\n'));
  check('…and it left no bundle from the publish before it', !existsSync(decoy),
    readdirSync(assets).filter((entry) => entry.endsWith('.js')).join(', '));
  check('…nor the launcher the single-file shell had, nor an engine file it no longer ships',
    !existsSync(retired) && !existsSync(staleEngine),
    [retired, staleEngine].filter(existsSync).join(', '));
  check(`…nor the browser's own folder and its second engine (${[...SHELL_HOME, RETIRED_IN_APP[0]].join('/')}/)`,
    !existsSync(retiredBrowser), retiredBrowser);
  check('…and a file it never wrote is still there', existsSync(neighbour));
  rmSync(neighbour, { force: true });

  // Whatever survives, `index.html` has to name something that is actually there — the check that
  // would still hold if the replacement were done some other way. Read only if it is there: a gate
  // that crashes on ENOENT reports a stack trace instead of the publish check that already failed.
  const indexPath = join(install, ...HOST_HOME, 'wwwroot', 'index.html');
  const indexHtml = existsSync(indexPath) ? readFileSync(indexPath, 'utf8') : '';
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
  return { ok: response.ok, status: response.status, text: await response.text() };
};

// Somebody else's quest is a clean stand-down, read by the status the door answers and never by its
// wording (the family rehearsal's stub, REV3 CLEAN1: this one had drifted to matching the sentence).
const take = await respond('take', null);
if (!take.ok) { if (take.status === 409) process.exit(0); throw new Error(take.text); }

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

  // The protocol stub the conversation in phase 6 runs on (DEPLOY5): the family rehearsal's, from the
  // kit both gates share, so no model and no account. Named for `acp-stub` alone; the driven loop
  // stays on the pipe stub above.
  const acpAgent = join(scratch, 'acp-agent.mjs');
  writeFileSync(acpAgent, ACP_STUB_AGENT);

  // A plugin that SPEAKS (D64), in the scratch home before the shell starts — the tracked example,
  // as a person would have added it. The family rehearsal drives the headless host's loop with it;
  // this is the only gate that drives the SHELL's, which owns the processes differently (it starts
  // them in its loop and stops them in its Stop), and is what a deployed machine actually runs.
  copyTree(join(examplesRoot, 'plugins', 'hold-by-title'), join(home, 'plugins', 'hold-by-title'));

  // A server that drives Daoris's own browser (D78 §3.5): before the deployed shell's driver hands a
  // session its servers, it has to bring up the browser the INSTALL carries. The stub never runs the
  // server; what is measured is that the browser came up, from where, and on which profile.
  mkdirSync(join(home, 'plugins', 'drives-the-browser'), { recursive: true });
  writeFileSync(join(home, 'plugins', 'drives-the-browser', 'plugin.json'), `${JSON.stringify({
    id: 'drives-the-browser',
    apiVersion: 1,
    name: 'Drives the browser',
    version: '1.0.0',
    description: 'A server handed Daoris’s own browser, for the deployment rehearsal.',
    servers: [{ name: 'browser', command: ['node', '-e', '0', '${browser}'] }],
  }, null, 2)}\n`);

  // The person's standing choices, scratch-local. Written BEFORE the shell starts: the loop begins
  // with the app, and a config arriving late makes the first ticks say "nothing is opted in".
  writeFileSync(join(home, 'driver.json'), `${JSON.stringify({
    drivable: ['newcomer'],
    adapter: 'stub',
    cap: 1,
    timeoutMinutes: 3,
    pollSeconds: 2,
    commands: { stub: ['node', stubAgent], 'acp-stub': ['node', acpAgent] },
    notify: false,
  }, null, 2)}\n`);

  const shellPort = await freePort(5311);
  const base = `http://127.0.0.1:${shellPort}`;
  // The debug port (DEPLOY5), picked clear of the dev loop's 9333 and the ports after it, which an
  // install the owner is looking at through `run --install` may hold.
  const cdpPort = await freePort(9433);

  /**
   * What a deployed shell is given, and — more importantly — what it is not.
   *
   * 🔴 `DAORIS_HTTP_HOST` is deliberately ABSENT. Naming the host is what `tools/desktop.mjs` does
   * to keep a dev loop honest, and it is exactly the variable that would skip the code path 2a
   * broke. Same for `--app-root`: the deployed shell's own default is `data/` beside the
   * executable, and the install is scratch, so the default already isolates this run.
   *
   * What it is given that a person's start is not: the debug port (DEPLOY5), the dev loop's own
   * opt-in, so phase 6 can open a chat over the bridge.
   */
  const shellEnvironment = {
    ...HERMETIC,
    DAORIS_SERVICE_URL: base,
    ASPNETCORE_URLS: base,
    DAORIS_KNOWLEDGE_DB: join(home, 'knowledge.db'),
    DAORIS_KNOWLEDGE_ROOT: family,
    DAORIS_DRIVER_CONFIG: join(home, 'driver.json'),
    ...debugEnvironment(cdpPort),
    // The kit reads its development switch from `DOTNET_ENVIRONMENT` first, and the host the shell
    // starts inherits it. A host in development can serve a PROJECT's `wwwroot` through ASP.NET's
    // static-assets manifest (`DesktopPage.BundleOf` says so), which is the workspace masking a
    // deployment, the class this gate exists for. In ASP.NET `ASPNETCORE_ENVIRONMENT` overrides the
    // switch, so this keeps that host in production, as an installed one runs.
    ASPNETCORE_ENVIRONMENT: 'Production',
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

  // Started the way a person starts it: the launcher at the root, which hands over and exits.
  shell = spawn(launcherExe, { cwd: install, env: environment, detached: true, stdio: 'ignore' });
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

  // The application, not the engine's own processes, which run from the same executable (CHR4).
  const window = eachApplicationAt(shellExe, '"$($_.MainWindowHandle)|$($_.MainWindowTitle)"').trim();
  check('the window is up', Boolean(window) && !window.startsWith('0|'), window || '(no process)');
  // The launcher hands over and is gone: a launcher that lingered would be a second process holding
  // the install for as long as the window is open.
  check('…and the launcher that started it has exited', running(launcherExe).length === 0,
    running(launcherExe).map((pid) => `pid ${pid}`).join(', '));

  // WHICH ENGINE answered (CHR4). A published shell opens no debug port (D78 §3.1) — this run's is
  // the gate's own, for phase 6 — so the answer is read off the process tree, as it would be on a
  // person's start: the page renders in a renderer the INSTALL's own executable started,
  // and no WebView2 process is the shell's child. A shell that fell back to WebView2, or a page that
  // never rendered, fails here rather than looking fine in a screenshot.
  const shellPids = applicationsAt(shellExe);
  let renderers = [];
  for (let attempt = 0; attempt < 20 && renderers.length === 0; attempt += 1) {
    renderers = powershell(
      'Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | '
      + `Where-Object { $_.ExecutablePath -eq ${psQuote(shellExe)} -and $_.CommandLine -like '*--type=renderer*' } | `
      + 'ForEach-Object { $_.ProcessId }')
      .split('\n').map((line) => line.trim()).filter(Boolean);
    if (renderers.length === 0) await sleep(500);
  }
  check('the page renders on the install’s own Chromium', renderers.length > 0,
    'no renderer process started from the install’s executable');
  const webviews = shellPids.length === 0 ? '' : powershell(
    'Get-CimInstance Win32_Process -Filter "Name = \'msedgewebview2.exe\'" -ErrorAction SilentlyContinue | '
    + `Where-Object { @(${shellPids.join(',')}) -contains $_.ParentProcessId } | ForEach-Object { $_.ProcessId }`).trim();
  check('…and not on WebView2', webviews === '', `WebView2 processes under the shell: ${webviews}`);

  // INSTALLED.md tells whoever opens the folder that `data/` is this install's own state. Nothing
  // read it back until now, and a deployed shell writing its WebView2 profile somewhere else is a
  // folder that cannot be deleted to uninstall.
  check(`the install keeps its own state in ${HOME}/`, existsSync(join(install, HOME)),
    readdirSync(install).join(', '));
  // The application runs from `app/` and the home is still the install's (D93), never one beside it.
  check(`…at the install’s root, not beside the application in ${SHELL_HOME.join('/')}/`,
    !existsSync(join(install, ...SHELL_HOME, HOME)));

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

  // The browser the driver brought up for the session's server: the install's own application,
  // started with the browser's argument (CHR8, D99), on the profile under this run's home, and the
  // session was handed its server rather than told it was withheld.
  const browsers = browsersAt(shellExe);
  const browserLine = browsers.length
    ? powershell(`(Get-CimInstance Win32_Process -Filter "ProcessId = ${browsers[0]}").CommandLine`).trim()
    : '';
  check('the deployed shell’s driver brought up the install’s own browser for the session',
    browsers.length === 1, browsers.length ? `pid ${browsers.join(', ')}` : 'no browser from the install’s application');
  check('…on its profile under this machine’s home',
    browserLine.includes(join(home, 'browser', 'engine')), browserLine || '(no command line)');
  // One Chromium, the install's: the browser's pages render in the application's own executable too,
  // in engine processes whose profile is the browser's rather than the window's.
  const browserRenderers = browsers.length === 0 ? '' : powershell(
    'Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | '
    + `Where-Object { $_.ExecutablePath -eq ${psQuote(shellExe)} -and $_.CommandLine -like '*--type=*' `
    + `-and @(${browsers.join(',')}) -contains $_.ParentProcessId } | ForEach-Object { $_.ProcessId }`).trim();
  check('…and its engine is the application’s own Chromium', browserRenderers !== '',
    'no engine process started from the install’s executable under the browser');
  check('…and the session was handed the server that drives it',
    bytes.length > 0 && !bytes.toString('utf8').includes('was not handed'),
    bytes.toString('utf8').split('\n').find((line) => line.includes('was not handed')) ?? '(no transcript)');

  // -------------------------------------------------------------- 6. it stops without orphaning

  section('6. Closing the shell stops what the shell owns, a conversation open in it included');

  // DEPLOY5. A conversation runs in the chat runner, beside the driven loop rather than in it, and
  // the shutdown order that ends it was wrong twice — the second time visible on the window alone.
  // So one is open when the shell closes: on the protocol stub, in the newcomer (free since its
  // driven session completed, and a git tree of its own), started over the bridge as the page starts
  // one, from inside the page, over the debug port this run opened.
  const { cdp, found } = await shellPage(cdpPort, base);
  check(`the debug port this run opened (${cdpPort}) reaches the install’s own page`, Boolean(cdp), found);

  let opened = { ok: false, error: { code: 'NO_PAGE', message: found } };
  for (let attempt = 0; attempt < 40 && cdp; attempt += 1) {
    opened = await bounded(
      cdp.evaluate(bridgeCall('DAORIS.DRIVER', 'START_CHAT', { repository: 'newcomer', adapter: 'acp-stub' }))
        .catch((error) => ({ ok: false, error: { code: 'EVALUATE', message: error.message } })),
      45_000,
      { ok: false, error: { code: 'NO_ANSWER', message: 'the page did not answer within 45s' } });
    // Only a page whose bridge is not up yet is asked again: nothing was posted to it.
    if (opened?.error?.code !== 'NOT_READY') break;
    await sleep(500);
  }

  // PLUG9 (d): the DEPLOYED driver finds the install's offers beside the application — this run's home is
  // scratch, not the install's `data/` — and, after a whole run of its loop, has installed none of them.
  const catalogue = cdp
    ? await bounded(
      cdp.evaluate(bridgeCall('DAORIS.DRIVER', 'PLUGINS', {}))
        .catch((error) => ({ ok: false, error: { code: 'EVALUATE', message: error.message } })),
      45_000,
      { ok: false, error: { code: 'NO_ANSWER', message: 'the page did not answer within 45s' } })
    : { ok: false, error: { code: 'NO_PAGE', message: found } };
  const offeredHere = (catalogue?.data?.offers ?? []).filter((offer) => !offer.installed).map((offer) => offer.id).sort();
  check('the deployed shell offers Daoris’s own plugins, found beside the application, none installed',
    offeredHere.join() === [...OFFERED_PLUGINS].sort().join() && offerProblems(install, [home, join(install, HOME)]).length === 0,
    JSON.stringify(catalogue?.data?.offers ?? catalogue));
  cdp?.close();
  const chatId = (opened?.ok && opened.data?.sessionId) || '';
  check('a conversation opens in the newcomer, on the protocol stub, over the bridge',
    Boolean(chatId), JSON.stringify(opened));

  let chatRecord = null;
  for (let attempt = 0; attempt < 60 && chatId; attempt += 1) {
    const sessions = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true', base);
    chatRecord = (sessions.json ?? []).find((session) => session.id === chatId) ?? chatRecord;
    if (chatRecord && !['queued', 'starting'].includes(chatRecord.state)) break;
    await sleep(500);
  }
  check('…and its record is `working`: a conversation, serving no quest, on the stub',
    chatRecord?.state === 'working' && chatRecord.kind === 'chat' && chatRecord.adapter === 'acp-stub'
      && !chatRecord.quest,
    JSON.stringify(chatRecord));

  // The marker every tracked process leaves under the home (FIX-LOG, 2026-09-25): what tells a live
  // session from an orphan, and what names the harness this close has to end.
  const chatMarker = join(home, 'sessions', `${chatId || 'no-conversation'}.pid`);
  const marked = markedProcess(existsSync(chatMarker) ? readFileSync(chatMarker, 'utf8') : '');
  check('…with its harness running, and marked on this machine',
    isMarkedProcess(marked, startedTicks(marked?.pid)),
    marked ? `the marker names pid ${marked.pid}, and that process is not running` : `no marker at ${chatMarker}`);

  // Closed the way a person closes it — the same path as before a conversation was open in it.
  stopAll(shellExe);
  shell = null;
  await sleep(1500);

  // The browser closes its windows when the shell ends (`--daoris-parent`), as the shell's own window
  // did. Asked first, because since CHR8 it runs from the same executable as the application, and
  // the check below would otherwise count it without naming it.
  let browsersLeft = browsersAt(shellExe);
  for (let attempt = 0; attempt < 20 && browsersLeft.length > 0; attempt += 1) {
    await sleep(500);
    browsersLeft = browsersAt(shellExe);
  }
  check('the browser went with the shell', browsersLeft.length === 0,
    browsersLeft.map((pid) => `pid ${pid}`).join(', '));

  // Everything from the install's executable, the engine's processes and the browser's included: they
  // follow the application out, a moment after it.
  let shellsLeft = running(shellExe);
  for (let attempt = 0; attempt < 10 && shellsLeft.length > 0; attempt += 1) {
    await sleep(500);
    shellsLeft = running(shellExe);
  }
  check('no shell of this install is left running, nor any of its engine’s processes', shellsLeft.length === 0,
    shellsLeft.map((pid) => `pid ${pid}`).join(', '));
  const orphans = hostProcesses().filter((host) => !before.has(host.pid));
  check('and no host it started is orphaned', orphans.length === 0,
    orphans.map((host) => `pid ${host.pid} ${host.path}`).join(', '));
  // Registrations are effects (D64 §4): the plugin's process is exactly as alive as the loop.
  const hooksLeft = hookProcesses();
  check('and the plugin’s process went with the loop', hooksLeft.length === 0,
    hooksLeft.map((pid) => `pid ${pid}`).join(', '));

  // The conversation's harness is the shell's as much as the plugin's process is: a forced stop once
  // left one running against its repository (case study 4c), and the close's own path must not.
  let harnessLeft = isMarkedProcess(marked, startedTicks(marked?.pid));
  for (let attempt = 0; attempt < 10 && harnessLeft; attempt += 1) {
    await sleep(500);
    harnessLeft = isMarkedProcess(marked, startedTicks(marked?.pid));
  }
  check('and the conversation’s harness went with it', Boolean(marked) && !harnessLeft,
    marked ? `pid ${marked.pid} still runs` : 'there was no marked harness to watch');
  check('…and its marker, removed as the shell let the process go', Boolean(chatId) && !existsSync(chatMarker),
    chatMarker);

  // -------------------------------------------------------------- 7. what the close wrote

  section('7. What the close wrote in the conversation’s record');

  // The shell's host went with the shell, which phase 6 just asserted, so the record is read from the
  // install's host started on its own over the store the shell left, as phase 3 started it. A host
  // runs no sweep — that is a driver's, at its first tick — so what it answers is what the close
  // wrote and nothing since.
  const readerPort = await freePort(5321);
  const readerBase = `http://127.0.0.1:${readerPort}`;
  const readerEnvironment = {
    ...process.env,
    ...HERMETIC,
    ASPNETCORE_URLS: readerBase,
    DAORIS_KNOWLEDGE_DB: join(home, 'knowledge.db'),
    DAORIS_KNOWLEDGE_ROOT: family,
  };
  for (const name of CLEARED) delete readerEnvironment[name];
  const reader = spawn(installedHost, { cwd: join(install, ...HOST_HOME), stdio: 'ignore', env: readerEnvironment });
  children.push(reader);
  check('the install’s host answers again, over the store the shell left', await answers(readerBase, 120));

  const afterClose = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true', readerBase);
  const closedRecord = (afterClose.json ?? []).find((session) => session.id === chatId) ?? null;
  check('the conversation’s record says the CLOSE ended it: `stopped`, with the close’s note, not the sweep’s',
    concludedByTheClose(closedRecord),
    closedRecord?.note === SWEPT_NOTE
      ? `ended by the sweep's note, so the close wrote nothing: ${JSON.stringify(closedRecord)}`
      : JSON.stringify(closedRecord ?? afterClose.text));

  reader.kill();
  children.length = 0;
  await sleep(1200);   // the port and the store's handle outlive the kill by a beat on Windows
  check('…and that host stops when it is told to', !hostProcesses().some((host) => host.pid === reader.pid));

  // -------------------------------------------------------------- 8. report

  section('8. Result');
  stopEverything();
  await sleep(500);

  console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
  if (totals.failures) {
    console.log('  The deployed artefact is NOT proven — the transcript names the first failure.');
    console.log('  Scratch left at _fixtures/deployment-rehearsal for inspection.');
    process.exitCode = 1;
    return;
  }

  console.log('  The shell was published to a folder and driven from there: one launcher at the root,');
  console.log('  Chromium’s, with its engine beside it and no symbols, a host under app/ with its own');
  console.log('  bundle answering on its own, a folder somebody else owns refused before anything was');
  console.log('  built, a republish leaving none of the last one’s files — then the DEPLOYED window,');
  console.log('  rendering on its own Chromium, told nothing about where its host lives, finding one that is not');
  console.log('  this workspace’s build and keeping its state in data/ beside itself. Then a quest:');
  console.log('  spawned by the installed application’s own driver loop, carried to done through the');
  console.log('  session’s own door, and its transcript holding an em-dash and 道衍 BYTE FOR BYTE —');
  console.log('  which is the defect a console codepage hid behind valid UTF-8. A plugin found under');
  console.log('  the install’s own home was started by that loop, asked before the start, told of the');
  console.log('  ending, and kept it beside itself. A conversation opened over the bridge, on the');
  console.log('  protocol stub, was working when the shell was closed, not killed: the host it owned,');
  console.log('  the plugin’s process and the conversation’s harness went with it, and the record');
  console.log('  carries the close’s own note, not the sweep’s.');
  rmSync(scratch, { recursive: true, force: true });
}

/**
 * Only when this file is what was run — the same guard `tools/desktop.mjs` carries, for the same
 * reason and after the same mistake.
 */
if (isMain(import.meta.url)) {
  await main();
}

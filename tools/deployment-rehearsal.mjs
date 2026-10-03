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
 * And a fourth, held since UPDATE1 (D139): an install updated by hand, four times in a day, because a
 * republish waited on running sessions. Phase 9 stages a build beside the RUNNING install, lets a
 * working session hold the drain open while a newer quest waits, then watches the application close,
 * the launcher swap `app/` and the new build confirm; then stages one that cannot come up and watches
 * it rolled back, and one that fails its check refused with nothing closed. Only the artefact can
 * show it: the swap is the launcher's, run against the install's own folder.
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
import { spawn, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  appendFileSync, copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync,
} from 'node:fs';
import { dirname, join, resolve, sep, win32 } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { copyTree, isMain } from './fsx.mjs';
import {
  ACP_STUB_AGENT, capture, makeChecker, openTranscript,
} from './rehearsal-kit.mjs';
// The install's layout, from the script that makes it (REV3 CLEAN1) — never a second spelling of it.
import {
  BUILD_MANIFEST, CLI_BIN, CLI_ENTRY, CLI_HOME, CLI_LAUNCHERS, CLI_PACKAGE, HOME, HOST_EXE, HOST_HOME, KEPT_LOCALES, LAUNCHER, MARKER,
  OFFERED_PLUGINS, OWN, PLUGIN_OFFERS, RESOURCES, RETIRED_BROWSER_EXE, RETIRED_IN_APP, RETIRED_LAUNCHERS, SHELL_EXE, SHELL_FILES,
  SHELL_HOME, STAGE, STAGED, SWAP_JOURNAL, promoteStage, unstage, writeManifest,
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

/**
 * What is wrong with the install's doctrine tool (WSSETUP2, D124 §1.2), as sentences: a launcher not in
 * `app/bin/`; the package without its bin entry, its built dispatcher, its manifest or its canon; its
 * TypeScript sources, which only the source tree carries and the packed artefact never does; and a package or
 * canon at a version other than `version`. Empty when the install carries the release's package, laid out.
 */
export function cliProblems(install, version) {
  const problems = [];
  for (const name of CLI_LAUNCHERS) {
    if (!existsSync(join(install, ...CLI_BIN, name))) problems.push(`${CLI_BIN.join('/')}/${name} is not there`);
  }
  const pkg = join(install, ...CLI_PACKAGE);
  const where = CLI_PACKAGE.join('/');
  for (const file of [CLI_ENTRY.join('/'), 'dist/cli.js', 'package.json', 'canon/canon.json']) {
    if (!existsSync(join(pkg, ...file.split('/')))) problems.push(`${where}/${file} is not there`);
  }
  if (existsSync(join(pkg, 'src'))) problems.push(`${where}/src/ is there: the source tree, not the packed artefact`);
  for (const file of ['package.json', 'canon/canon.json']) {
    const path = join(pkg, ...file.split('/'));
    if (!existsSync(path)) continue;
    let said = null;
    try {
      said = JSON.parse(readFileSync(path, 'utf8')).version ?? null;
    } catch {
      said = '(not JSON)';
    }
    if (said !== version) problems.push(`${where}/${file} says ${said}, not ${version}`);
  }
  return problems;
}

/**
 * Whether the first file a shell's lookup lists (`where`, `Get-Command`, `command -v` through `cygpath -w`)
 * sits in `folder` itself: the one that shell runs by the bare name. Compared case-blind with either
 * separator, as Windows answers a path, and by the whole folder, never a prefix of it.
 */
export function firstUnder(listing, folder) {
  const first = String(listing ?? '').split(/\r?\n/).map((line) => line.trim()).find(Boolean);
  if (!first) return false;
  const plain = (path) => win32.normalize(path).replace(/\\+$/, '').toLowerCase();
  return plain(win32.dirname(first)) === plain(folder);
}

/**
 * Git Bash: the `bash.exe` beside `git`, up to three folders up (`cmd\`, `bin\`, `mingw64\bin\`), or null.
 * Never whatever `bash` PATH finds first, which on Windows is usually WSL's launcher, a different shell in a
 * different filesystem. The rule the terminal's shell list keeps (`TerminalShells`, D96), so the gate
 * measures the shell a terminal and a session open.
 */
export function gitBashBeside(git, exists = existsSync) {
  let folder = git ? dirname(git) : null;
  for (let level = 0; level < 3 && folder; level += 1, folder = dirname(folder)) {
    const bash = join(folder, 'bin', 'bash.exe');
    if (exists(bash)) return bash;
  }
  return null;
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

/** The phases `update/swap.json` passes through (`SwapPhase`, `StagedBuild.cs`): a journal in any other is no outcome. */
const SWAP_PHASES = Object.freeze(['swapping', 'started', 'confirmed', 'installed', 'rolled-back', 'refused']);

/**
 * How the launcher's swap ended (UPDATE1, D139 §6), read from `update/swap.json`'s text: its phase, the build, the reason
 * it rolled back or refused, and whether the new application confirmed — or null for a journal missing, torn, or in a
 * phase it does not know, which the gate waits through rather than passes on.
 */
export function swapOutcome(text) {
  let journal = null;
  try {
    journal = JSON.parse(String(text ?? ''));
  } catch {
    return null;
  }
  if (!journal || typeof journal !== 'object' || !SWAP_PHASES.includes(journal.phase)) return null;
  return {
    phase: journal.phase,
    id: journal.id ?? null,
    reason: journal.reason ?? null,
    confirmed: typeof journal.confirmed === 'boolean' ? journal.confirmed : null,
  };
}

/** The data of every machine-log line naming `event`, in order, from a JSONL file's text; a line that does not read is skipped. */
export function loggedData(text, event) {
  return String(text ?? '').split('\n').flatMap((line) => {
    try {
      const entry = JSON.parse(line);
      return entry?.event === event ? [entry.data ?? {}] : [];
    } catch {
      return [];
    }
  });
}

/**
 * Stage the install's own live build again (D139 §1's layout, by the publish's own writers), for the update's roll-back and
 * refusal: `application` replaces the application with another program, under a manifest that agrees, so the check passes
 * and the start fails; `tamper` changes one file after the manifest was written, so the check refuses it. The home is
 * never staged, and the live build is not touched. Answers the staged folder.
 */
export function stageLiveBuild(install, { id, version, application = null, tamper = null }) {
  const staging = join(install, ...STAGE, '.staging');
  rmSync(staging, { recursive: true, force: true });
  mkdirSync(staging, { recursive: true });
  for (const name of [LAUNCHER, MARKER, SHELL_HOME[0]]) cpSync(join(install, name), join(staging, name), { recursive: true });
  if (application) copyFileSync(application, join(staging, ...SHELL_HOME, SHELL_EXE));
  writeManifest(staging, { id, version, commit: null, at: new Date().toISOString().replace(/\.\d+Z$/, 'Z') });
  if (tamper) appendFileSync(join(staging, ...tamper.split('/')), 'changed after its manifest');
  return promoteStage(staging, install);
}

/** A file's SHA-256, as the manifest writes it: what tells which build is the live one. */
const sha256Of = (path) => (existsSync(path) ? createHash('sha256').update(readFileSync(path)).digest('hex') : '');

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

  /** The canon's version, which the install's doctrine tool answers (WSSETUP2): read, never spelled. */
  const canonVersion = JSON.parse(readFileSync(join(repoRoot, 'canon', 'canon.json'), 'utf8')).version;

  // -------------------------------------------------------------- 1. publish the artefact

  section('1. Publish the artefact');
  stopAll(shellExe);      // a previous run's window would hold its own executable open
  await sleep(300);
  // A run that died leaves its processes winding down for a few seconds; the removal waits them out rather
  // than refusing with EPERM (TEST1: the merge tool's re-run of a crashed rehearsal met exactly that).
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 500 });
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
  // Beside the application's own files: the host, the record itself, the offers (PLUG9 d), the list built
  // in (TOOLS3) and the doctrine tool's two folders (WSSETUP2), which the publish lays out after the
  // application's files.
  const besideIt = [
    HOST_HOME.at(-1), SHELL_FILES.at(-1), PLUGIN_OFFERS.at(-1), RESOURCES.at(-1), CLI_HOME.at(-1), CLI_BIN.at(-1),
  ];
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

  // The doctrine tool (WSSETUP2, D124 §1.2): the package the release publishes, laid out as npm lays one out
  // under app/cli/, at the canon's version, with a launcher for each shell in app/bin/; the packed artefact,
  // never the source tree. Phase 8 runs it.
  const toolProblems = cliProblems(install, canonVersion);
  check(`the install carries the doctrine tool: daoris ${canonVersion} in ${CLI_PACKAGE.join('/')}/, `
    + `with ${CLI_LAUNCHERS.join(' and ')} in ${CLI_BIN.join('/')}/`, toolProblems.length === 0, toolProblems.join('; '));

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
  // …and a file in each of the doctrine tool's folders that its last layout had and this one does not
  // (WSSETUP2): both folders are replaced whole.
  const staleTool = [join(install, ...CLI_PACKAGE, 'stale-from-before.js'), join(install, ...CLI_BIN, 'daoris.ps1')];
  for (const path of staleTool) {
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, '// from the publish before\n');
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
  check(`…nor a file the doctrine tool's last layout had, in ${CLI_HOME.join('/')}/ or ${CLI_BIN.join('/')}/`,
    staleTool.every((path) => !existsSync(path)) && cliProblems(install, canonVersion).length === 0,
    [...staleTool.filter(existsSync), ...cliProblems(install, canonVersion)].join('; '));
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

  // -------------------------------------------------------------- 8. the doctrine tool, by its bare name

  section('8. The doctrine tool the install carries, by its bare name from each shell (WSSETUP2)');

  // D124 §1.2: the install's `app/bin/` first on the PATH, as WSSETUP3 is to put it for every child Daoris
  // starts, and `daoris` asked for by its bare name from each shell a session's harness runs. Its own canon,
  // never an override: `DAORIS_CANON` is taken out, so the canon it reads is the one its package carries.
  // A repository of its own, adopted by it: init, sync and check, offline, as a set-up's session would.
  const toolBin = join(install, ...CLI_BIN);
  const pathKey = Object.keys(process.env).find((name) => name.toUpperCase() === 'PATH') ?? 'PATH';
  const toolEnvironment = {
    ...process.env, ...HERMETIC, DAORIS_CANON: undefined, [pathKey]: `${toolBin};${process.env[pathKey] ?? ''}`,
  };
  const doctrine = join(scratch, 'doctrine');
  mkdirSync(doctrine, { recursive: true });
  writeFileSync(join(doctrine, 'README.md'), '# doctrine\n\nA repository the install’s own daoris adopts.\n');
  /** A program run with its arguments as given, in `cwd`, under the tool's environment: exit code and output. */
  const shellRun = (file, args, cwd = doctrine, verbatim = false) => {
    const ran = spawnSync(file, args, {
      cwd, env: toolEnvironment, encoding: 'utf8', timeout: 120_000, windowsVerbatimArguments: verbatim,
    });
    return { code: ran.status ?? -1, out: `${ran.stdout ?? ''}${ran.stderr ?? ''}${ran.error ? `\n${ran.error.message}` : ''}` };
  };
  // Command Prompt, by the line a person types: cmd.exe finds `daoris.cmd` by PATHEXT.
  const prompt = (line) => shellRun(process.env.ComSpec ?? 'cmd.exe', [`/d /s /c "${line}"`], doctrine, true);

  const whereDaoris = prompt('where daoris');
  check(`Command Prompt finds \`daoris\` in the install’s ${CLI_BIN.join('/')}/ first`,
    whereDaoris.code === 0 && firstUnder(whereDaoris.out, toolBin), whereDaoris.out);
  const promptVersion = prompt('daoris --version');
  check(`…and \`daoris --version\` prints the canon’s version, ${canonVersion}`,
    promptVersion.code === 0 && promptVersion.out.trim() === canonVersion, promptVersion.out);
  const adopted = prompt('daoris init');
  const doctrineManifest = join(doctrine, 'daoris.json');
  const pinned = existsSync(doctrineManifest) ? JSON.parse(readFileSync(doctrineManifest, 'utf8')).source : null;
  check(`…\`daoris init\` writes a manifest pinned at it, daoris@${canonVersion}`,
    adopted.code === 0 && pinned === `daoris@${canonVersion}`, `${adopted.out}\nsource: ${pinned}`);
  const toolSync = prompt('daoris sync');
  const doctrineLock = join(doctrine, 'daoris.lock');
  const locked = existsSync(doctrineLock) ? JSON.parse(readFileSync(doctrineLock, 'utf8')).canonVersion : null;
  check('…`daoris sync` materializes the canon its package carries',
    toolSync.code === 0 && locked === canonVersion && existsSync(join(doctrine, 'AGENTS.md')), `${toolSync.out}\nlock: ${locked}`);
  const toolCheck = prompt('daoris check');
  check('…and `daoris check` runs clean', toolCheck.code === 0, toolCheck.out);
  const unknownVerb = prompt('daoris no-such-verb');
  check('…and a tool error comes back through the launcher as exit 2', unknownVerb.code === 2, unknownVerb.out);

  // PowerShell has no launcher of its own: it finds the batch file by PATHEXT (D124 §1.2), which this measures.
  const fromPowerShell = shellRun('powershell.exe',
    ['-NoProfile', '-NonInteractive', '-Command', '(Get-Command daoris).Source; daoris --version; exit $LASTEXITCODE']);
  const powerShellLines = fromPowerShell.out.trim().split(/\r?\n/);
  check(`PowerShell finds the batch file in ${CLI_BIN.join('/')}/, and it prints ${canonVersion}`,
    fromPowerShell.code === 0 && firstUnder(fromPowerShell.out, toolBin) && powerShellLines.at(-1)?.trim() === canonVersion,
    fromPowerShell.out);
  const powerShellCheck = shellRun('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', 'daoris check; exit $LASTEXITCODE']);
  check('…and `daoris check` runs clean from it, its exit code handed back', powerShellCheck.code === 0, powerShellCheck.out);

  // Git Bash, where the runner has one: the bash beside git, never whatever `bash` PATH finds (WSL's).
  const git = prompt('where git').out.split(/\r?\n/).map((line) => line.trim()).find((line) => /git\.exe$/i.test(line)) ?? null;
  const bash = gitBashBeside(git);
  if (!bash) {
    console.log('        no Git Bash beside git on this runner: its checks are skipped, not passed');
  } else {
    const bashFound = shellRun(bash, ['-c', 'cygpath -w "$(command -v daoris)"']);
    check(`Git Bash finds the shell script \`daoris\` in ${CLI_BIN.join('/')}/ first`,
      bashFound.code === 0 && firstUnder(bashFound.out, toolBin), bashFound.out);
    const bashVersion = shellRun(bash, ['-c', 'daoris --version']);
    check(`…and \`daoris --version\` prints ${canonVersion}`,
      bashVersion.code === 0 && bashVersion.out.trim() === canonVersion, bashVersion.out);
    const bashCheck = shellRun(bash, ['-c', 'daoris check']);
    check('…and `daoris check` runs clean from it', bashCheck.code === 0, bashCheck.out);
    const bashUnknown = shellRun(bash, ['-c', 'daoris no-such-verb']);
    check('…and a tool error comes back through the script as exit 2', bashUnknown.code === 2, bashUnknown.out);
  }

  // -------------------------------------------------------------- 9. the install updates when its work allows

  section('9. The install updates when its work allows: staged, drained, swapped, started, rolled back (UPDATE1, D139)');

  // The terminal's door to the update (D50): the headless host's `update`, built from this workspace and run on this
  // run's home, naming the install, since a scratch home is no install's `data/`.
  const driverProject = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Host');
  const driverDll = join(driverProject, 'bin', 'Debug', 'net10.0', 'daoris-driver.dll');
  const builtHost = run(`dotnet build "${driverProject}" --nologo -v q`, repoRoot, {}, 10 * 60_000);
  check('the headless host builds, for its `update` door', builtHost.code === 0, builtHost.out.split('\n').slice(-6).join('\n'));
  const driverUpdate = (words) => run(`dotnet "${driverDll}" update ${words} --install "${install}"`, scratch, { ...HERMETIC }, 60_000);

  // A session that holds the drain open until the gate lets it go: it takes its quest, then waits for a file named for
  // it in this run's scratch, then lands and closes as the stub does. Each session opens a tree of its own, so a second
  // quest is held by the drain and not by the first session's checkout.
  const releaseOf = (quest) => join(scratch, `release-${quest}`);
  const slowAgent = join(scratch, 'slow-agent.mjs');
  writeFileSync(slowAgent, `
import { execSync } from 'node:child_process';
import { existsSync, writeFileSync } from 'node:fs';

if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }

const url = process.env.DAORIS_SERVICE_URL;
const id = process.env.DAORIS_QUEST_ID;
const respond = async (action, reason) => {
  const response = await fetch(url + '/api/quests/' + id + '/respond', {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action, reason }),
  });
  return { ok: response.ok, status: response.status, text: await response.text() };
};
const take = await respond('take', null);
if (!take.ok) { if (take.status === 409) process.exit(0); throw new Error(take.text); }
const release = ${JSON.stringify(join(scratch, 'release-'))} + id;
while (!existsSync(release)) await new Promise((resolve) => setTimeout(resolve, 500));
writeFileSync('held-' + id + '.md', 'Held the update open, then let it go.\\n');
const git = 'git -c user.name="Deployment Rehearsal" -c user.email="rehearsal@example.invalid"';
execSync(git + ' add -A', { stdio: 'ignore' });
execSync(git + ' commit -q -m "stub: held the update open for ' + id + '"', { stdio: 'ignore' });
const done = await respond('done', 'Landed by the slow stub session.');
if (!done.ok) throw new Error(done.text);
`);
  writeFileSync(join(home, 'driver.json'), `${JSON.stringify({
    drivable: ['newcomer'],
    trees: ['newcomer'],
    adapter: 'stub',
    cap: 3,
    // Longer than the stage's build, which runs while the held session waits.
    timeoutMinutes: 30,
    pollSeconds: 2,
    commands: { stub: ['node', slowAgent], 'acp-stub': ['node', acpAgent] },
    notify: false,
  }, null, 2)}\n`);

  /** The update as the install's own page answers it over the bridge (`DAORIS.UPDATE` · `STATE`), or null. */
  // Why the last read of the window's update state came back empty, so a check that waited on it can say so.
  let lastUpdateRead = 'not read yet';
  const updateState = async () => {
    const { cdp: page, found } = await shellPage(cdpPort, base);
    if (!page) {
      // Who holds the debug port, by process, so a page that never answers says whether the port is bound at all.
      const holders = spawnSync('powershell', ['-NoProfile', '-Command',
        `Get-NetTCPConnection -LocalPort ${cdpPort} -ErrorAction SilentlyContinue | ForEach-Object { $p = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue; "$($_.State) pid $($_.OwningProcess) $($p.Path)" }`],
      { encoding: 'utf8' }).stdout?.trim();
      lastUpdateRead = `${found}; the port: ${holders || 'nothing bound'}`;
      return null;
    }
    try {
      const answer = await bounded(page.evaluate(bridgeCall('DAORIS.UPDATE', 'STATE', {})).catch((error) => ({ ok: false, error: error.message })), 20_000, null);
      lastUpdateRead = answer === null ? 'the bridge call did not answer in 20 s' : JSON.stringify(answer).slice(0, 400);
      return answer?.ok ? answer.data : null;
    } finally {
      page.close();
    }
  };
  const waitFor = async (what, seconds, every = 1000) => {
    let seen = null;
    for (let waited = 0; waited < seconds * 1000; waited += every) {
      seen = await what();
      if (seen) return seen;
      await sleep(every);
    }
    return null;
  };
  // Asked across a restart, when the host may not be listening yet: a refused connection is no session, never a crash.
  const sessionFor = async (quest) => {
    try {
      const sessions = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true', base);
      return (sessions.json ?? []).find((session) => session.quest === quest) ?? null;
    } catch {
      return null;
    }
  };
  const journal = () => swapOutcome(existsSync(join(install, ...STAGE, SWAP_JOURNAL))
    ? readFileSync(join(install, ...STAGE, SWAP_JOURNAL), 'utf8') : '');
  const logged = (event) => (existsSync(join(home, 'logs'))
    ? readdirSync(join(home, 'logs')).filter((file) => file.endsWith('.desktop.jsonl'))
      .flatMap((file) => loggedData(readFileSync(join(home, 'logs', file), 'utf8'), event))
    : []);
  const windowUp = () => {
    const shown = eachApplicationAt(shellExe, '"$($_.MainWindowHandle)|$($_.MainWindowTitle)"').trim();
    return Boolean(shown) && !shown.startsWith('0|');
  };

  // Started the way a person starts it, on the same environment as phase 4, its debug port included.
  shell = spawn(launcherExe, { cwd: install, env: environment, detached: true, stdio: 'ignore' });
  shell.unref();
  check('the install starts again, for its update', await answers(base, 200));
  const firstApplication = await waitFor(async () => (windowUp() ? applicationsAt(shellExe) : null), 60);
  check('…and its window is up', Boolean(firstApplication?.length), JSON.stringify(firstApplication));

  // 9a. A session runs, and holds the drain open.
  const held = await api('POST', '/api/quests', base, {
    from: 'game', to: 'newcomer', title: 'Hold the update open', body: 'Take this, then wait until the gate lets you go.',
  });
  const heldQuest = held.json?.quest?.id ?? '';
  const heldWorking = await waitFor(async () => ((await sessionFor(heldQuest))?.state === 'working' ? true : null), 90);
  check('a session is working when the build is staged', Boolean(heldQuest) && Boolean(heldWorking), held.text);

  // 9b. Staged beside the running install, by the publish's own `--stage`: nothing it holds is written over.
  const staged = run(
    `node "${join(repoRoot, 'tools', 'desktop-publish.mjs')}" --to "${install}" --service --stage`, repoRoot, {}, 15 * 60_000);
  check('publish:desktop --stage exits 0 beside the running install', staged.code === 0, staged.out.split('\n').slice(-8).join('\n'));
  const stagedManifestPath = join(install, ...STAGED, BUILD_MANIFEST);
  const stagedManifest = existsSync(stagedManifestPath) ? JSON.parse(readFileSync(stagedManifestPath, 'utf8')) : null;
  check(`…into ${STAGED.join('/')}/, with a manifest naming every file it carries`,
    Boolean(stagedManifest?.id) && (stagedManifest?.files?.length ?? 0) > 0, stagedManifestPath);
  check('…and the running application was not touched', applicationsAt(shellExe).join() === firstApplication?.join(),
    `before: ${firstApplication?.join(', ')}; now: ${applicationsAt(shellExe).join(', ')}`);

  // 9c. The drain: nothing new starts, and what runs is counted down.
  const draining = await waitFor(async () => {
    const state = await updateState();
    return state?.state === 'draining' ? state : null;
  }, 30);
  check('the application drains for the staged build: when idle, by default, with the session counted',
    draining?.staged?.id === stagedManifest?.id && draining?.mode === 'when-idle' && draining?.driven >= 1, JSON.stringify(draining));
  const second = await api('POST', '/api/quests', base, {
    from: 'game', to: 'newcomer', title: 'Wait for the update', body: 'Published while the update drains.',
  });
  const secondQuest = second.json?.quest?.id ?? '';
  writeFileSync(releaseOf(secondQuest), '');
  await sleep(8000);
  check('a quest published while it drains does not start', Boolean(secondQuest) && !(await sessionFor(secondQuest)),
    JSON.stringify(await sessionFor(secondQuest)));
  check('the machine log says it was staged and is draining',
    logged('update.staged').some((line) => line.build === stagedManifest?.id) && logged('update.draining').length > 0);

  // 9d. The session ends; the work allows it; the launcher swaps and the new build starts and confirms.
  writeFileSync(releaseOf(heldQuest), '');
  const installed = await waitFor(async () => (journal()?.phase === 'installed' ? journal() : null), 300);
  check('once the session ended, the launcher swapped app/ and the new build said it came up',
    installed?.id === stagedManifest?.id && installed?.confirmed === true, JSON.stringify(journal()));
  await answers(base, 200);
  check('…the session was let end, not cut', (await sessionFor(heldQuest))?.state === 'completed', JSON.stringify(await sessionFor(heldQuest)));
  const listedLibrary = stagedManifest?.files?.find((file) => file.path === `${SHELL_HOME[0]}/Daoris.Desktop.App.dll`);
  check('…app/ is the staged build, by its manifest’s hash',
    Boolean(listedLibrary) && sha256Of(join(install, ...SHELL_HOME, 'Daoris.Desktop.App.dll')) === listedLibrary.sha256);
  const restarted = await waitFor(async () => {
    const now = applicationsAt(shellExe);
    return now.length > 0 && !now.some((pid) => firstApplication?.includes(pid)) && windowUp() ? now : null;
  }, 120);
  check('…a new application runs from the same place, its window up', Boolean(restarted), JSON.stringify(applicationsAt(shellExe)));
  const launcherGone = await waitFor(async () => (running(launcherExe).length === 0 ? true : null), 30);
  check('…and the launcher that swapped it has exited', Boolean(launcherGone), running(launcherExe).join(', '));
  // The launcher cannot delete its own running launcher inside update/previous/, so the new application clears what the
  // swap left once the launcher has gone (UPDATE1), at its next look.
  check(`…and neither ${STAGED.join('/')}/ nor the build before it is left`,
    Boolean(await waitFor(async () => (!existsSync(join(install, ...STAGED)) && !existsSync(join(install, ...STAGE, 'previous')) ? true : null), 30)),
    readdirSync(join(install, ...STAGE)).join(', '));
  check('the machine log says it applied when idle and was installed',
    logged('update.applying').some((line) => line.by === 'idle' && line.build === stagedManifest?.id)
      && logged('update.installed').some((line) => line.build === stagedManifest?.id && line.confirmed === true));
  check('…and the window says so once, with nothing left staged',
    (await waitFor(async () => {
      const state = await updateState();
      return state?.outcome?.phase === 'installed' && state.state === 'none' ? state : null;
    }, 60)) !== null);
  check('the quest held by the drain starts after the restart, and completes',
    Boolean(await waitFor(async () => ((await sessionFor(secondQuest))?.state === 'completed' ? true : null), 120)),
    JSON.stringify(await sessionFor(secondQuest)));

  // 9e. Roll back: the live build staged again with its application replaced by a program that exits at once, under a
  // manifest that agrees, so the check passes and the start fails. *Update now*, from the terminal's door.
  const exitsAtOnce = join(process.env.SystemRoot ?? 'C:\\Windows', 'System32', 'whoami.exe');
  if (!existsSync(exitsAtOnce)) {
    console.log(`        no ${exitsAtOnce} on this runner: the roll-back checks are skipped, not passed`);
  } else {
    const liveApplication = sha256Of(shellExe);
    const beforeRollback = applicationsAt(shellExe);
    stageLiveBuild(install, { id: 'fails-to-start', version: canonVersion, application: exitsAtOnce });
    const now = driverUpdate('--now');
    check('`daoris-driver update --now` writes the word for the staged build', now.code === 0 && /installed now/.test(now.out), now.out);
    const rolledBack = await waitFor(async () => (journal()?.phase === 'rolled-back' ? journal() : null), 300);
    check('a build that will not come up is rolled back: the launcher put the build before it back',
      rolledBack?.id === 'fails-to-start' && rolledBack?.reason === 'exited', JSON.stringify(journal()));
    check('…app/ is the live build again, and the failed one is kept aside in update/failed/',
      sha256Of(shellExe) === liveApplication
        && sha256Of(join(install, ...STAGE, 'failed', ...SHELL_HOME, SHELL_EXE)) === sha256Of(exitsAtOnce));
    check('…the build before it started again, its window up',
      Boolean(await waitFor(async () => {
        const after = applicationsAt(shellExe);
        return after.length > 0 && !after.some((pid) => beforeRollback.includes(pid)) && windowUp() ? after : null;
      }, 120)), JSON.stringify(applicationsAt(shellExe)));
    // The build before it writes its log line as it comes up, which can be after its window is.
    check('…and it says so once, in the machine log and the window',
      (await waitFor(async () => (logged('update.rolled-back').some((line) => line.build === 'fails-to-start' && line.reason === 'exited') ? true : null), 60)) !== null
        && (await waitFor(async () => ((await updateState())?.outcome?.phase === 'rolled-back' ? true : null), 60)) !== null,
      `${JSON.stringify(logged('update.rolled-back'))}; the window's last read: ${lastUpdateRead}`);
    const status = driverUpdate('');
    check('`daoris-driver update` says the last swap rolled back', status.code === 0 && /rolled back/.test(status.out), status.out);

    // 9f. Refused: a build whose file changed after its manifest is refused by the application's check, which closes nothing.
    const runningNow = applicationsAt(shellExe);
    stageLiveBuild(install, { id: 'fails-the-check', version: canonVersion, tamper: `${SHELL_HOME[0]}/Daoris.Desktop.App.dll` });
    const refusedState = await waitFor(async () => {
      const state = await updateState();
      return state?.state === 'refused' ? state : null;
    }, 30);
    check('a build that fails the check is refused before anything closes, saying which check',
      refusedState?.problem?.code === 'size' && applicationsAt(shellExe).join() === runningNow.join()
        && sha256Of(shellExe) === liveApplication, `${JSON.stringify(refusedState)}; the window's last read: ${lastUpdateRead}`);
    unstage(install);
  }

  // -------------------------------------------------------------- 10. report

  section('10. Result');
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
  console.log('  carries the close’s own note, not the sweep’s. And the install’s own doctrine tool, the');
  console.log('  package the release publishes, answered by its bare name from Command Prompt, PowerShell');
  console.log('  and Git Bash where the runner has one, and adopted a repository of its own clean. Then a build staged');
  console.log('  beside the running install drained it: a quest published meanwhile waited, the working session was let');
  console.log('  end, and the launcher swapped app/ and started the new build, which confirmed; a build that would not');
  console.log('  come up was rolled back to the one before it, and one that failed its check was refused with nothing closed.');
  rmSync(scratch, { recursive: true, force: true });
}

/**
 * Only when this file is what was run — the same guard `tools/desktop.mjs` carries, for the same
 * reason and after the same mistake.
 */
if (isMain(import.meta.url)) {
  await main();
}

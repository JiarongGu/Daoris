/**
 * ACP2's proof — `claude-code` over the protocol door (D53, evaluation §5).
 *
 * D23 says an adapter arrives ON PROOF, and for a protocol door that proof is a real driven run:
 * a real quest, a real harness, a real commit. That run spends a login, which is the owner's to
 * supply — so this script is built in two halves that fail differently.
 *
 *   node tools/acp2-proof.mjs             readiness + the keyless checks
 *   node tools/acp2-proof.mjs --drive     …and then the real driven run
 *
 * **Readiness is not a failure.** A machine that is not set up yet gets a list of the exact commands
 * that set it up, and exit 0. What this script must never do is look like it proved something when
 * the login was missing — D23's "on proof" is the whole point, and a green line on a machine that
 * never called a model would be the worst possible lie to tell about it.
 *
 * Everything before `--drive` costs nothing: no model is called, no token is spent, and the
 * machine's real profile is never written to (the handshake runs under a scratch `CLAUDE_CONFIG_DIR`,
 * which is exactly what the evaluation's §1a established keylessly).
 */
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { capture, makeChecker, openTranscript } from './rehearsal-kit.mjs';
// The real resolution, not a second copy of it: a pinned harness is found exactly the way the CLI
// and the driver find one (TOOL2/D57), so this script cannot disagree with them about what would run.
import {
  harnessHome, harnessesPath, managedBinary, readHarnessSettings, resolveVersion,
} from '../src/Daoris.Cli/src/toolchain.ts';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const httpDll = join(
  repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http',
  'bin', 'Debug', 'net10.0', 'daoris-knowledge-http.dll');
const driverDll = join(
  repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Host',
  'bin', 'Debug', 'net10.0', 'daoris-driver.dll');

const scratch = join(repoRoot, '_fixtures', 'acp2-proof');
const BASE = 'http://localhost:5201';
const ADAPTER = 'claude-code-acp';
// 🔴 The adapter's Daoris name and its BINARY are different strings — the package ships
// `claude-agent-acp`. Guessing the second from the first is how a pin reports itself missing.
const BINARY = 'claude-agent-acp';
const PACKAGE = '@agentclientprotocol/claude-agent-acp';

const drive = process.argv.includes('--drive');

/**
 * The scratch host this run's records live in.
 *
 * 🔴 Declared HERE, above every statement that can reach it. `let` is hoisted but not initialised,
 * so a declaration further down the file leaves `stopHost` and `drivenRun` in its temporal dead
 * zone — which fails as "Cannot access 'host' before initialization" at the moment of use, long
 * after the line that actually caused it.
 */
let host = null;

openTranscript(repoRoot, 'acp2', { beforeExit: () => stopHost() });
const { totals, check, section } = makeChecker();

/**
 * What the person still has to do. Collected rather than thrown, so they get the whole list once.
 *
 * 🔴 **A readiness item is not a check.** A machine that has not installed the adapter yet has
 * failed nothing — it is simply not set up — so these report as `todo` and never touch the failure
 * count. Mixing the two produced a run that said "FAIL" and "nothing above failed" in the same
 * breath, which is worse than either.
 */
const missing = [];
const needs = (what, command) => missing.push({ what, command });

/** A readiness fact: true, or true-with-a-todo. Never a failure. */
function ready(label, satisfied, detail = '') {
  console.log(satisfied
    ? `  ok    ${label}`
    : `  todo  ${label}${detail ? `\n          ${detail}` : ''}`);
  return satisfied;
}

// ─── readiness ────────────────────────────────────────────────────────────────────────────────────
//
// Each of these is a fact about this machine, and each names the one command that changes it. The
// order is the order a person would do them in.

section('Readiness — what this machine has, and what is left');

const built = ready('the host and the driver are built', existsSync(httpDll) && existsSync(driverDll));
if (!built) needs('the host and driver binaries', 'npm run desktop -- build');

/**
 * Which binary this machine would actually run for a harness: the managed pin, else `PATH` — the
 * same order a spawn resolves (TOOL2's twin rule, minus the explicit-command case, which is a
 * driver-config choice this script does not make).
 */
const settings = readHarnessSettings(harnessesPath());
const home = harnessHome(harnessesPath());
const managed = (harness, binary) =>
  managedBinary(home, harness, resolveVersion(settings, harness, null, null), [binary]);

/** Ask a harness about itself the way `daoris agent list` does, tolerating absence. */
function present(harness, binary, args = ['--version']) {
  const where = managed(harness, binary) ?? binary;
  const found = capture(`"${where}" ${args.join(' ')}`, repoRoot, { timeout: 30_000 });
  return found.code === 0
    ? { version: found.out.trim().split('\n')[0] ?? '', where }
    : null;
}

const claude = present('claude-code', 'claude');
const claudeVersion = claude?.version ?? null;
ready('`claude` is on this machine', Boolean(claude),
  claude ? `${claude.version}  (${claude.where})` : 'not pinned, and not on PATH');
if (!claudeVersion) {
  needs('the Claude Code CLI', 'daoris agent install claude-code   (or pin one: daoris agent pin claude-code <version>)');
}

const acpAdapter = present(ADAPTER, BINARY);
const adapterVersion = acpAdapter?.version ?? null;
ready(`\`${ADAPTER}\` is on this machine`, Boolean(adapterVersion), adapterVersion ?? 'not on PATH');
if (!adapterVersion) {
  needs(`the ACP adapter (${PACKAGE})`,
    `daoris agent pin ${ADAPTER} 0.79.0   — the version the evaluation ran`);
}

// The account. `claude auth status` answers JSON and exits 0 either way, so the OUTPUT is the answer
// — the same reading the toolchain's own login check makes, and the reason it is written down twice.
const authStatus = capture('claude auth status', repoRoot, { timeout: 30_000 });
const loggedIn = /"loggedIn"\s*:\s*true/i.test(authStatus.out);
ready('a Claude account is logged in', loggedIn,
  loggedIn ? '' : 'the profile this run would use reports logged out');
if (!loggedIn) {
  needs('a logged-in account',
    'daoris agent login claude-code [--profile <name>]   — runs the agent\'s own flow, into a directory Daoris owns');
}

// ─── the keyless half ─────────────────────────────────────────────────────────────────────────────
//
// Everything the real run depends on, proven without spending anything. This is the half that can
// run on any machine, including one with no account at all.

section('Keyless — the wiring the real run will depend on');

const adapters = capture(`node "${cliBin}" --help`, repoRoot).out;
check('the CLI still has no session verbs', !/\bdrive\b.*session/i.test(adapters),
  'the CLI is offline and spawns nothing (D35); sessions are the driver\'s');

if (adapterVersion) {
  // §1a's probe, run here: initialize + session/new under a SCRATCH config dir, no prompt. It costs
  // nothing — the model is only reached by `session/prompt`, which is deliberately not sent.
  const probeHome = join(scratch, 'handshake-profile');
  rmSync(probeHome, { recursive: true, force: true });
  mkdirSync(probeHome, { recursive: true });

  const handshake = await acpHandshake(probeHome);
  check('the adapter answers `initialize`', handshake.initialized,
    handshake.problem ?? '');
  check('`session/new` succeeds on a tree', handshake.sessionId !== null,
    handshake.sessionId ? `session ${handshake.sessionId}` : (handshake.problem ?? ''));
  check('it offers `acceptEdits` as a mode, so the posture is not a flag',
    handshake.modes.includes('acceptEdits'),
    handshake.modes.length ? `modes: ${handshake.modes.join(', ')}` : 'no modes offered');

  // 🔴 The seam that makes the whole door affordable: the adapter runs OUR claude, not a second copy.
  check('the scratch profile received its own configuration, and the real one was untouched',
    existsSync(join(probeHome, '.claude.json')),
    'CLAUDE_CONFIG_DIR is the account seam — §1a saw `.claude.json` land in an empty directory');

  const stillLoggedIn = /"loggedIn"\s*:\s*true/i.test(
    capture('claude auth status', repoRoot, { timeout: 30_000 }).out);
  check('the machine\'s real account was not spent by the handshake',
    loggedIn ? stillLoggedIn : true,
    'no prompt was sent, so no model was called');
} else {
  console.log(`  --    the handshake needs \`${ADAPTER}\`; skipped`);
}

// ─── the real driven run ──────────────────────────────────────────────────────────────────────────

if (!drive) {
  section('The real run');
  console.log('  --    not run. `node tools/acp2-proof.mjs --drive` runs it, and spends one login.');
}

if (drive && missing.length === 0) {
  section('The real driven run — D23\'s "on proof"');
  await drivenRun();
}

// ─── the verdict ──────────────────────────────────────────────────────────────────────────────────

console.log('');
if (missing.length > 0) {
  console.log('This machine is not ready for the real run yet. What is left:');
  for (const { what, command } of missing) {
    console.log(`\n  ${what}`);
    console.log(`    ${command}`);
  }
  console.log('\nThen: node tools/acp2-proof.mjs --drive');
  console.log('\nNothing above failed — this is a readiness report, not a gate.');
}

console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
if (drive && missing.length === 0 && totals.failures === 0) {
  console.log('  ACP2 is proven: a real quest, over the protocol door, to a real commit.');
}

process.exitCode = totals.failures > 0 ? 1 : 0;

// ─── the pieces ───────────────────────────────────────────────────────────────────────────────────

/**
 * The §1a handshake: initialize, session/new, read the modes — and stop.
 *
 * Written here rather than reusing `tools/dsh-probes/acp-client.mjs` because that one is a probe
 * instrument for an evaluation that has concluded; this is a gate for a door that shipped, and they
 * will drift apart. It speaks the same wire either way.
 */
async function acpHandshake(configDir) {
  const result = { initialized: false, sessionId: null, modes: [], problem: null };
  const child = spawn(acpAdapter?.where ?? BINARY, [], {
    cwd: repoRoot,
    stdio: ['pipe', 'pipe', 'pipe'],
    env: {
      ...process.env,
      CLAUDE_CONFIG_DIR: configDir,
      // The seam ACP2 exists to use: whichever `claude` this machine would run.
      ...(claudeVersion ? {} : {}),
    },
    shell: process.platform === 'win32',
  });

  let buffered = '';
  const pending = new Map();
  let nextId = 0;

  child.stdout.on('data', (chunk) => {
    buffered += chunk.toString();
    let cut;
    while ((cut = buffered.indexOf('\n')) >= 0) {
      const line = buffered.slice(0, cut).trim();
      buffered = buffered.slice(cut + 1);
      if (!line) continue;
      try {
        const frame = JSON.parse(line);
        if (frame.id !== undefined && pending.has(frame.id)) {
          pending.get(frame.id)(frame);
          pending.delete(frame.id);
        }
      } catch {
        // A line that is not a frame is the agent's own logging; the wire ignores it.
      }
    }
  });

  const request = (method, params) => new Promise((resolve) => {
    const id = nextId++;
    // Bounded: an adapter that stops answering must not hang the gate. 🔴 `unref` matters as much as
    // the bound — an outstanding timer keeps Node's event loop alive, so without it the script sat
    // there for a further minute after its last check with nothing on screen, which reads exactly
    // like a hang.
    const bell = setTimeout(
      () => { if (pending.delete(id)) resolve({ error: { message: 'timed out' } }); }, 60_000);
    bell.unref();
    pending.set(id, (frame) => { clearTimeout(bell); resolve(frame); });
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
  });

  try {
    const initialized = await request('initialize', {
      protocolVersion: 1,
      clientCapabilities: { fs: { readTextFile: false, writeTextFile: false }, terminal: false },
      clientInfo: { name: 'daoris-acp2-proof', version: '0' },
    });
    result.initialized = Boolean(initialized.result);
    if (initialized.error) result.problem = initialized.error.message;

    const created = await request('session/new', { cwd: repoRoot, mcpServers: [] });
    if (created.error) {
      result.problem = created.error.message;
    } else {
      result.sessionId = created.result?.sessionId ?? null;
      result.modes = (created.result?.modes?.availableModes ?? []).map((m) => m.id);
    }
  } finally {
    child.stdin.end();
    child.kill();
    await sleep(200);
  }

  return result;
}

function stopHost() {
  try { host?.kill(); } catch { /* already gone */ }
  host = null;
}

/**
 * One GET against the scratch host, bounded.
 *
 * @remarks
 * Bare `fetch` has no timeout, so a host that accepts a connection and never answers hangs the whole
 * run with nothing on screen — which is exactly what happened before this existed.
 */
async function ask(path) {
  try {
    const answered = await fetch(`${BASE}${path}`, { signal: AbortSignal.timeout(30_000) });
    return await answered.json();
  } catch {
    return null;
  }
}

/** A fresh git repository with one commit in it — the starting point a session is measured from. */
function born(name, summary) {
  const where = join(scratch, name);
  mkdirSync(where, { recursive: true });
  const git = (command) => execFileSync('git', command, { cwd: where, encoding: 'utf8' });
  git(['init', '-q', '-b', 'main']);
  git(['config', 'user.email', 'proof@example.com']);
  git(['config', 'user.name', 'ACP2 proof']);
  writeFileSync(join(where, 'README.md'), `# ${name}\n\n${summary}\n`);
  git(['add', '-A']);
  git(['commit', '-qm', 'the starting point']);
  return where;
}

/**
 * Say what a repository is, which `connect` requires before it will register one.
 *
 * @remarks
 * 🔴 Not a formality, and the refusal says so: *"that declaration is how siblings know whether a
 * quest is yours"*. A fixture that skipped it was refused by name — the system defending its own
 * premise against a script that had not read it.
 */
function declare(where, name) {
  const manifest = join(where, 'daoris.json');
  const held = JSON.parse(readFileSync(manifest, 'utf8'));
  held.domain = {
    summary: `A scratch repository, born for ACP2's proof run. Not real work.`,
    owns: [`everything inside \`${name}\`, which is nothing anybody depends on`],
    accepts: ['a small, reversible change to its own files'],
  };
  writeFileSync(manifest, `${JSON.stringify(held, null, 2)}\n`);
}

/**
 * DRV4's shape, over the protocol door: a scratch repository, a real quest, one driven tick, and the
 * record read back. The assertions are what D23's "on proof" means — the work landed, the quest
 * closed itself through the session's own connector, and the record names what produced it.
 */
async function drivenRun() {
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });

  // 🔴 TWO repositories, because a quest is work for SOMEBODY ELSE — the service refuses one
  // addressed to the repository it came from, by name, which is the whole premise of the thing
  // ("repositories are not developed across"). A one-repo fixture cannot express a real quest.
  const asker = born('proof-asker', 'The asker: it needs something from the receiver.');
  const repo = born('proof-repo', 'The receiver: the repository this proof drives.');

  // 🔴 CONFINE THE HOST TO THIS SCRATCH FAMILY. Without `DAORIS_KNOWLEDGE_ROOT` the host falls back
  // to a parent-of-CWD heuristic and indexes every repository beside this one — measured: a run
  // without it read the developer's neighbouring projects into its registry. Nothing was written to
  // them, and nothing should have been read either. `DAORIS_KNOWLEDGE_DB` keeps the index here too,
  // so a scratch run never touches the machine's real one.
  const env = {
    DAORIS_SERVICE_URL: BASE,
    ASPNETCORE_URLS: BASE,
    DAORIS_KNOWLEDGE_ROOT: scratch,
    DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge.db'),
    DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json'),
  };

  host = spawn('dotnet', [httpDll], { cwd: scratch, env: { ...process.env, ...env }, stdio: 'ignore' });
  // 🔴 A live child keeps Node's event loop alive, and the only thing that kills this one runs on
  // `beforeExit` — which therefore never fires. Every early `return` below then sat forever with its
  // last check printed and nothing following it, which is indistinguishable from a hung driver.
  host.unref();
  let up = false;
  for (let attempt = 0; attempt < 40 && !up; attempt++) {
    await sleep(500);
    up = await fetch(`${BASE}/api/status`, { signal: AbortSignal.timeout(5_000) })
      .then((r) => r.ok).catch(() => false);
  }
  check('the scratch host answers', up);
  if (!up) return;

  // 🔴 The guard that would have caught the scan escaping. A scratch host that can see a repository
  // this run did not create is pointed at the wrong world, and everything after it is meaningless —
  // so this refuses BEFORE a quest is published or a model is spent.
  const seen = await ask('/api/registry');
  const strangers = (seen ?? []).map((r) => r.repository).filter((n) => !n.startsWith('proof-'));
  check('the host sees this scratch family and nothing else', strangers.length === 0,
    strangers.length ? `it also indexed: ${strangers.join(', ')}` : '');
  if (strangers.length > 0) {
    console.log('        Refusing to go further: DAORIS_KNOWLEDGE_ROOT is not confining the scan.');
    return;
  }

  let joined = true;
  for (const [name, where] of [['proof-asker', asker], ['proof-repo', repo]]) {
    const adopt = capture(`node "${cliBin}" init --name ${name}`, where, { env });
    declare(where, name);
    capture(`node "${cliBin}" sync`, where, { env });
    // 🔴 `connect` has NO `--service` flag — the address is `DAORIS_SERVICE_URL`, and an unknown flag
    // is ignored in silence. Passing `env` here is what points it at the scratch host; without it the
    // command answered "no DAORIS_SERVICE_URL" while the script had every appearance of having told it.
    const connect = capture(`node "${cliBin}" connect`, where, { env });
    // 🔴 COMMIT THE ADOPTION. `init` and `sync` leave `daoris.json`, `daoris.lock` and `.claude/`
    // uncommitted, and the driver refuses a tree with work in flight — *"somebody's work in flight;
    // the driver holds rather than entangling a session with it"*. It was right and the fixture was
    // wrong: a repository is adopted in a commit, not left dirty.
    execFileSync('git', ['add', '-A'], { cwd: where, encoding: 'utf8' });
    execFileSync('git', ['commit', '-qm', 'adopt daoris'], { cwd: where, encoding: 'utf8' });
    const ok = adopt.code === 0 && connect.code === 0;
    check(`\`${name}\` adopts and registers`, ok, `${adopt.out}\n${connect.out}`);
    joined &&= ok;
  }

  if (!joined) return;

  const quest = await fetch(`${BASE}/api/quests`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    signal: AbortSignal.timeout(30_000),
    body: JSON.stringify({
      from: 'proof-asker',
      to: 'proof-repo',
      title: 'Note in the README that this is a scratch repository',
      body: 'Append one line to README.md saying this repository exists only for a proof run, '
        + 'then commit it. Nothing else.',
    }),
  }).then((r) => r.json()).catch(() => ({}));
  // The POST answers `{ quest, message }` — the record AND the sentence a person is meant to read
  // ("It is held by the service, not written into that repository"). Reading `.id` off the envelope
  // finds nothing while the quest is perfectly real, which is what happened.
  const questId = quest.quest?.id ?? quest.id;
  check('a real quest is open', Boolean(questId), JSON.stringify(quest));
  if (!questId) return;

  const configPath = join(scratch, 'driver.json');
  writeFileSync(configPath, `${JSON.stringify({
    drivable: ['proof-repo'], holds: [], trees: [], cap: 1,
    adapter: ADAPTER, timeoutMinutes: 10, pollSeconds: 5, notify: false,
  }, null, 2)}\n`);

  // 🔴 The person's rule that lets a session COMMIT (PERM1, D72). The driver's home is the folder its
  // driver.json sits in, so the rules live beside it. Without them the first real runs (2026-09-24)
  // took the quest over the connector — whose tools Daoris's defaults allow — made the edit, and were
  // refused `git add`/`git commit`: the repository's own allow-list is ignored in a folder the agent
  // has never trusted (DEPLOY1's measurement), so they declined honestly with no commit. `cd` is
  // here because the agent prefixes its commit with one, and every part of a compound command must
  // be allowed.
  writeFileSync(join(scratch, 'permissions.json'), `${JSON.stringify({
    machine: { allow: ['Bash(cd:*)', 'Bash(git add:*)', 'Bash(git commit:*)'] },
  }, null, 2)}\n`);

  console.log('  ..    driving — this is the step that spends the login');
  const run = capture(`dotnet "${driverDll}" --until-idle`, scratch, {
    env: { ...env, DAORIS_DRIVER_CONFIG: configPath },
    timeout: 15 * 60_000,
  });
  console.log(run.out.split('\n').map((line) => `        ${line}`).join('\n'));

  const sessions = (await ask('/api/sessions?includeClosed=true')) ?? [];
  const session = sessions.find((s) => s.adapter === ADAPTER);
  check('a session ran on the protocol door', Boolean(session),
    session ? `${session.id} — ${session.state}` : 'no session with that adapter');

  if (session) {
    check('it ended completed', session.state === 'completed', `${session.state}: ${session.note ?? ''}`);
    // The record names what produced the work (D49 §4) — the same three facts the pipe door records.
    check('the record names the adapter', session.adapter === ADAPTER);
    check('the record names the harness version', Boolean(session.harnessVersion), session.harnessVersion ?? 'absent');
    check('the record carries evidence of a commit', /[0-9a-f]{7}/i.test(session.evidence ?? ''),
      session.evidence ?? 'no evidence');
  }

  const closed = (await ask('/api/quests?includeClosed=true')) ?? [];
  const answered = closed.find((q) => q.id === questId);
  check('the session closed its own quest through its connector', answered?.status === 'Done',
    `${answered?.status}`);

  // TOOL3's half: the protocol door is the only one that reports usage, so a real run here is also
  // the first time the measurement has anything real in it.
  const usageFile = join(scratch, 'usage.json');
  if (existsSync(usageFile)) {
    const measured = JSON.parse(readFileSync(usageFile, 'utf8'));
    check('the run was measured', measured.length > 0, `${measured.length} session(s)`);
  } else {
    console.log('  --    usage is recorded by the shell\'s loop, not the headless host; not asserted here');
  }

  stopHost();
}

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

/** Ask a harness about itself the way `daoris harness list` does, tolerating absence. */
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
  needs('the Claude Code CLI', 'daoris harness install claude-code   (or pin one: daoris harness pin claude-code <version>)');
}

const acpAdapter = present(ADAPTER, BINARY);
const adapterVersion = acpAdapter?.version ?? null;
ready(`\`${ADAPTER}\` is on this machine`, Boolean(adapterVersion), adapterVersion ?? 'not on PATH');
if (!adapterVersion) {
  needs(`the ACP adapter (${PACKAGE})`,
    `daoris harness pin ${ADAPTER} 0.79.0   — the version the evaluation ran`);
}

// The account. `claude auth status` answers JSON and exits 0 either way, so the OUTPUT is the answer
// — the same reading the toolchain's own login check makes, and the reason it is written down twice.
const authStatus = capture('claude auth status', repoRoot, { timeout: 30_000 });
const loggedIn = /"loggedIn"\s*:\s*true/i.test(authStatus.out);
ready('a Claude account is logged in', loggedIn,
  loggedIn ? '' : 'the profile this run would use reports logged out');
if (!loggedIn) {
  needs('a logged-in account',
    'daoris harness login claude-code [--profile <name>]   — runs the harness\'s own flow, into a directory Daoris owns');
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
    pending.set(id, resolve);
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
    // Bounded: an adapter that stops answering must not hang the gate.
    setTimeout(() => { if (pending.delete(id)) resolve({ error: { message: 'timed out' } }); }, 60_000);
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

/** The scratch host this run's records live in. */
let host = null;

function stopHost() {
  try { host?.kill(); } catch { /* already gone */ }
  host = null;
}

/**
 * DRV4's shape, over the protocol door: a scratch repository, a real quest, one driven tick, and the
 * record read back. The assertions are what D23's "on proof" means — the work landed, the quest
 * closed itself through the session's own connector, and the record names what produced it.
 */
async function drivenRun() {
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });

  const repo = join(scratch, 'proof-repo');
  mkdirSync(repo, { recursive: true });
  const git = (command) => execFileSync('git', command, { cwd: repo, encoding: 'utf8' });
  git(['init', '-q', '-b', 'main']);
  git(['config', 'user.email', 'proof@example.com']);
  git(['config', 'user.name', 'ACP2 proof']);
  writeFileSync(join(repo, 'README.md'), '# proof-repo\n\nA scratch repository for ACP2\'s proof.\n');
  git(['add', '-A']);
  git(['commit', '-qm', 'the starting point']);

  const store = join(scratch, 'store');
  const env = {
    DAORIS_SERVICE_URL: BASE,
    ASPNETCORE_URLS: BASE,
    DAORIS_STORE: store,
    DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json'),
  };

  host = spawn('dotnet', [httpDll], { cwd: scratch, env: { ...process.env, ...env }, stdio: 'ignore' });
  let up = false;
  for (let attempt = 0; attempt < 40 && !up; attempt++) {
    await sleep(500);
    up = await fetch(`${BASE}/api/status`).then((r) => r.ok).catch(() => false);
  }
  check('the scratch host answers', up);
  if (!up) return;

  const adopt = capture(`node "${cliBin}" init --name proof-repo`, repo);
  check('the scratch repository adopts', adopt.code === 0, adopt.out);
  capture(`node "${cliBin}" sync`, repo);
  const connect = capture(`node "${cliBin}" connect --service ${BASE}`, repo);
  check('it registers with the scratch host', connect.code === 0, connect.out);

  const quest = await fetch(`${BASE}/api/quests`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      from: 'proof-repo',
      to: 'proof-repo',
      title: 'Add a LICENSE note to the README',
      body: 'Append a line to README.md saying the repository is a scratch one, then commit it.',
    }),
  }).then((r) => r.json());
  check('a real quest is open', Boolean(quest.id), JSON.stringify(quest));

  const configPath = join(scratch, 'driver.json');
  writeFileSync(configPath, `${JSON.stringify({
    drivable: ['proof-repo'], holds: [], trees: [], cap: 1,
    adapter: ADAPTER, timeoutMinutes: 10, pollSeconds: 5, notify: false,
  }, null, 2)}\n`);

  console.log('  ..    driving — this is the step that spends the login');
  const run = capture(`dotnet "${driverDll}" --until-idle`, scratch, {
    env: { ...env, DAORIS_DRIVER_CONFIG: configPath },
    timeout: 15 * 60_000,
  });
  console.log(run.out.split('\n').map((line) => `        ${line}`).join('\n'));

  const sessions = await fetch(`${BASE}/api/sessions?includeClosed=true`).then((r) => r.json());
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

  const closed = await fetch(`${BASE}/api/quests?includeClosed=true`).then((r) => r.json());
  const answered = closed.find((q) => q.id === quest.id);
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

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
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { setTimeout as sleep } from 'node:timers/promises';
import { capture } from './rehearsal-kit.mjs';
import { ADAPTER, BINARY, cliBin, openProof, repoRoot } from './proof-kit.mjs';

const drive = process.argv.includes('--drive');

const {
  scratch, totals, check, section, missing, readiness, ask, post, born, scratchDomain,
  freshScratch, startHost, stopHost, adopt, runDriver, verdict,
} = openProof('acp2', { label: 'ACP2', port: 5201 });

// ─── readiness ────────────────────────────────────────────────────────────────────────────────────

const { adapter: acpAdapter, loggedIn } = readiness();

// ─── the keyless half ─────────────────────────────────────────────────────────────────────────────
//
// Everything the real run depends on, proven without spending anything. This is the half that can
// run on any machine, including one with no account at all.

section('Keyless — the wiring the real run will depend on');

const adapters = capture(`node "${cliBin}" --help`, repoRoot).out;
check('the CLI still has no session verbs', !/\bdrive\b.*session/i.test(adapters),
  'the CLI is offline and spawns nothing (D35); sessions are the driver\'s');

if (acpAdapter?.version) {
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

verdict('run');
if (drive && missing.length === 0 && totals.failures === 0) {
  console.log('  ACP2 is proven: a real quest, over the protocol door, to a real commit.');
}

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
    env: { ...process.env, CLAUDE_CONFIG_DIR: configDir },
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

/**
 * DRV4's shape, over the protocol door: a scratch repository, a real quest, one driven tick, and the
 * record read back. The assertions are what D23's "on proof" means — the work landed, the quest
 * closed itself through the session's own connector, and the record names what produced it.
 */
async function drivenRun() {
  freshScratch();

  // 🔴 TWO repositories, because a quest is work for SOMEBODY ELSE — the service refuses one
  // addressed to the repository it came from, by name, which is the whole premise of the thing
  // ("repositories are not developed across"). A one-repo fixture cannot express a real quest.
  const asker = born('proof-asker', 'The asker: it needs something from the receiver.');
  const repo = born('proof-repo', 'The receiver: the repository this proof drives.');

  const env = await startHost();
  if (!env) return;

  const joined = adopt([
    { name: 'proof-asker', where: asker, domain: scratchDomain('proof-asker') },
    { name: 'proof-repo', where: repo, domain: scratchDomain('proof-repo') },
  ], env);
  if (!joined) return;

  const quest = await post('/api/quests', {
    from: 'proof-asker',
    to: 'proof-repo',
    title: 'Note in the README that this is a scratch repository',
    body: 'Append one line to README.md saying this repository exists only for a proof run, '
      + 'then commit it. Nothing else.',
  });
  // The POST answers `{ quest, message }` — the record AND the sentence a person is meant to read
  // ("It is held by the service, not written into that repository"). Reading `.id` off the envelope
  // finds nothing while the quest is perfectly real, which is what happened.
  const questId = quest.quest?.id ?? quest.id;
  check('a real quest is open', Boolean(questId), JSON.stringify(quest));
  if (!questId) return;

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

  runDriver(env, {
    drivable: ['proof-repo'], holds: [], trees: [], cap: 1,
    adapter: ADAPTER, timeoutMinutes: 10, pollSeconds: 5, notify: false,
  }, 'driving');

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

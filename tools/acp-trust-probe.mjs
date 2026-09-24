/**
 * DEPLOY1's measurement — does the protocol door need the harness's trust flag?
 *
 * The pipe door does. Claude Code ignores a repository's own `permissions.allow` in a directory it
 * has never been told to trust, so a driven session there can work but cannot take or close its
 * quest, and the driver holds rather than spend a login on it (DEPLOY1's detection half). The ACP
 * door runs the adapter's Agent SDK rather than the CLI's trust flow, so it may not have the problem
 * at all — and the driver currently holds it on the same check, which is a guess until measured.
 *
 *   node tools/acp-trust-probe.mjs
 *
 * Two scratch rooms, both fresh and therefore untrusted, each a git repository. The CONTROL has no
 * allow-list; the MEASUREMENT's own `.claude/settings.json` allows `git commit`. Each gets one small
 * prompt over the wire — make one empty commit — and a permission request is answered the way Daoris
 * answers every one: refused (D52). What is recorded is whether a request arrived at all.
 *
 * 🔴 The command has to WRITE. The first version asked for `git status`, and the control ran it
 * unasked: Claude Code approves read-only commands on its own, so a read proves nothing about an
 * allow-list either way.
 *
 * - The control asks        → the adapter gates Bash under `acceptEdits`, so the probe can see a gate.
 * - The measurement asks    → the room's allow-list was ignored: the ACP door has the trust problem.
 * - The measurement doesn't → the allow-list was honoured untrusted: the ACP door does not need it.
 *
 * A third room measures PERM1's carrier (D72): no allow-list of its own, and the same `git commit`
 * allowed in a settings file handed on `session/new` as `_meta.claudeCode.options.settings`, the
 * harness's command-line tier and the way the driver hands every session its rules. If it runs
 * unasked in an untrusted room, Daoris's rules reach a session without anybody's trust grant. If it
 * asks, the carrier needs the grant as the repository's own list does.
 *
 * 🔴 It spends three small prompts on the machine's own signed-in account. It writes nothing under the
 * account's configuration — the trust flag is the person's grant, and this measures its absence.
 * Run with the `CLAUDE*` environment of any enclosing agent session stripped, as DRV4 notes.
 */
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { managedBinary, harnessHome, harnessesPath, readHarnessSettings, resolveVersion } from '../src/Daoris.Cli/src/toolchain.ts';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(repoRoot, '_fixtures', 'acp-trust-probe');
const ADAPTER = 'claude-code-acp';
const BINARY = 'claude-agent-acp';

const settings = readHarnessSettings(harnessesPath());
const adapter = managedBinary(
  harnessHome(harnessesPath()), ADAPTER, resolveVersion(settings, ADAPTER, null, null), [BINARY]) ?? BINARY;

/** Whether the account's own record already trusts a directory — read, never written. */
function trusted(dir) {
  const record = join(process.env.CLAUDE_CONFIG_DIR ?? homedir(), '.claude.json');
  if (!existsSync(record)) return false;
  const projects = JSON.parse(readFileSync(record, 'utf8')).projects ?? {};
  const wanted = dir.replaceAll('\\', '/').toLowerCase();
  return Object.entries(projects).some(([path, entry]) =>
    path.replaceAll('\\', '/').toLowerCase() === wanted && entry?.hasTrustDialogAccepted === true);
}

/** A fresh, untrusted git room; `allow` becomes its own `.claude/settings.json` when given. */
function room(name, allow) {
  const where = join(scratch, name);
  rmSync(where, { recursive: true, force: true });
  mkdirSync(where, { recursive: true });
  execFileSync('git', ['init', '-q', '-b', 'main'], { cwd: where });
  execFileSync('git', ['config', 'user.email', 'probe@example.com'], { cwd: where });
  execFileSync('git', ['config', 'user.name', 'DEPLOY1 probe'], { cwd: where });
  writeFileSync(join(where, 'README.md'), `# ${name}\n\nA scratch room for DEPLOY1's measurement.\n`);
  if (allow) {
    mkdirSync(join(where, '.claude'), { recursive: true });
    writeFileSync(join(where, '.claude', 'settings.json'),
      `${JSON.stringify({ permissions: { allow } }, null, 2)}\n`);
  }
  return where;
}

/**
 * One prompt over the wire in `cwd`: initialize, session/new, the `acceptEdits` mode the driver sets,
 * then the prompt. Every permission request is recorded and refused; the tool calls the agent reports
 * are recorded with their final status.
 */
async function probe(cwd, meta = null) {
  const seen = { permissions: [], tools: new Map(), text: '', stop: null, problem: null };
  const child = spawn(adapter, [], {
    cwd, stdio: ['pipe', 'pipe', 'pipe'], env: process.env, shell: process.platform === 'win32',
  });

  let buffered = '';
  const pending = new Map();
  let nextId = 0;
  const send = (frame) => child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', ...frame })}\n`);

  child.stdout.on('data', (chunk) => {
    buffered += chunk.toString();
    let cut;
    while ((cut = buffered.indexOf('\n')) >= 0) {
      const line = buffered.slice(0, cut).trim();
      buffered = buffered.slice(cut + 1);
      if (!line) continue;
      let frame;
      try { frame = JSON.parse(line); } catch { continue; }

      if (frame.method === 'session/request_permission') {
        // Refused, as Daoris refuses every one (D52): the outcome the driver's door would give.
        seen.permissions.push(frame.params?.toolCall?.title ?? frame.params?.toolCall?.kind ?? 'a tool');
        send({ id: frame.id, result: { outcome: { outcome: 'cancelled' } } });
      } else if (frame.method === 'session/update') {
        const update = frame.params?.update ?? {};
        if (update.sessionUpdate === 'agent_message_chunk' && update.content?.type === 'text') {
          seen.text += update.content.text;
        } else if (update.toolCallId) {
          const was = seen.tools.get(update.toolCallId) ?? {};
          seen.tools.set(update.toolCallId, {
            title: update.title ?? was.title ?? '', status: update.status ?? was.status ?? '',
          });
        }
      } else if (frame.id !== undefined && pending.has(frame.id)) {
        pending.get(frame.id)(frame);
        pending.delete(frame.id);
      } else if (frame.id !== undefined && frame.method) {
        // Anything else the agent asks of a client that declared no capabilities is refused.
        send({ id: frame.id, error: { code: -32601, message: 'not offered' } });
      }
    }
  });

  const request = (method, params, bound = 60_000) => new Promise((resolve) => {
    const id = nextId++;
    const bell = setTimeout(() => { if (pending.delete(id)) resolve({ error: { message: 'timed out' } }); }, bound);
    bell.unref();
    pending.set(id, (frame) => { clearTimeout(bell); resolve(frame); });
    send({ id, method, params });
  });

  try {
    const init = await request('initialize', {
      protocolVersion: 1,
      clientCapabilities: { fs: { readTextFile: false, writeTextFile: false }, terminal: false },
      clientInfo: { name: 'daoris-acp-trust-probe', version: '0' },
    });
    if (init.error) { seen.problem = `initialize: ${init.error.message}`; return seen; }

    // PERM1's carrier, exactly as the driver sends it (`ClaudeAcpAdapter.AcpSessionMeta`).
    const created = await request('session/new', meta ? { cwd, mcpServers: [], _meta: meta } : { cwd, mcpServers: [] });
    if (created.error) { seen.problem = `session/new: ${created.error.message}`; return seen; }
    const sessionId = created.result.sessionId;

    const mode = await request('session/set_mode', { sessionId, modeId: 'acceptEdits' });
    if (mode.error) { seen.problem = `set_mode: ${mode.error.message}`; return seen; }

    const prompted = await request('session/prompt', {
      sessionId,
      prompt: [{
        type: 'text',
        text: 'Run exactly `git commit --allow-empty -m "trust probe"` with your Bash tool, once, then '
          + 'reply with only the first line of its output. If you are not allowed to run it, reply with '
          + 'the single word: DENIED. Do nothing else and use no other tool.',
      }],
    }, 5 * 60_000);
    if (prompted.error) seen.problem = `session/prompt: ${prompted.error.message}`;
    seen.stop = prompted.result?.stopReason ?? null;
  } finally {
    child.stdin.end();
    child.kill();
    await sleep(300);
  }
  return seen;
}

function report(label, where, seen) {
  console.log(`\n${label}  (${where})`);
  console.log(`  trusted by the account: ${trusted(where) ? 'YES — not a valid measurement' : 'no'}`);
  if (seen.problem) console.log(`  problem: ${seen.problem}`);
  console.log(`  permission requests: ${seen.permissions.length ? seen.permissions.join(' | ') : 'none'}`);
  for (const [, tool] of seen.tools) console.log(`  tool call: ${tool.title} — ${tool.status}`);
  console.log(`  stop reason: ${seen.stop ?? 'none'}`);
  console.log(`  reply: ${seen.text.trim().split('\n')[0] ?? ''}`);
}

console.log(`DEPLOY1's measurement over the protocol door — ${adapter}`);
const control = room('control', null);
const measured = room('allowed', ['Bash(git commit:*)']);
const flagged = room('flagged', null);
for (const where of [control, measured, flagged]) {
  if (trusted(where)) {
    console.log(`\nRefusing: ${where} is already trusted, so it cannot measure trust's absence.`);
    process.exit(2);
  }
}

// The rules file PERM1 hands a session, outside the room — the driver keeps it under the home.
const flagFile = join(scratch, 'flag-settings.json');
writeFileSync(flagFile, `${JSON.stringify({ permissions: { allow: ['Bash(git commit:*)'], ask: [], deny: [] } }, null, 2)}\n`);

const controlSeen = await probe(control);
report('CONTROL — no allow-list', control, controlSeen);
const measuredSeen = await probe(measured);
report('MEASUREMENT — the room allows `git commit`', measured, measuredSeen);
const flaggedSeen = await probe(flagged, { claudeCode: { options: { settings: flagFile } } });
report('PERM1 — `git commit` allowed only in the settings handed on session/new', flagged, flaggedSeen);

console.log('\nVerdict:');
if (controlSeen.problem || measuredSeen.problem) {
  console.log('  inconclusive — a probe did not complete (see above).');
} else if (controlSeen.permissions.length === 0) {
  console.log('  inconclusive — the control was not gated, so a missing request proves nothing here.');
} else if (measuredSeen.permissions.length > 0) {
  console.log('  the ACP door IGNORES an untrusted room\'s allow-list: it has the trust problem too.');
} else {
  console.log('  the ACP door HONOURS an untrusted room\'s allow-list: it does not need the trust flag.');
}

// PERM1's carrier (D72), read against the same control.
if (flaggedSeen.problem || controlSeen.permissions.length === 0) {
  console.log('  PERM1: inconclusive — see above.');
} else if (flaggedSeen.permissions.length > 0) {
  console.log('  PERM1: the settings handed on session/new were NOT honoured untrusted — Daoris\'s rules need the trust grant too.');
} else {
  console.log('  PERM1: the settings handed on session/new WERE honoured untrusted — Daoris\'s rules reach a session with no trust grant.');
}

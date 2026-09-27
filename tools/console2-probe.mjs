/**
 * CONSOLE2a's measurement — what the protocol door carries when the client ASKS for the streams.
 *
 * The console is one stream per session today. The owner wants a tab for each thing that is running:
 * the session, each sub-session it spawns, and each process it starts. Reading the Claude Code ACP
 * adapter (0.79.0) found that the wire already carries all three, gated on what the client declares
 * at `initialize`, and Daoris declares none of them:
 *
 * - `clientCapabilities.subagents` as an object: a subagent streams as its own session, announced by
 *   `subagent_spawned` and ended by `subagent_state_update` (`acp-subagents.js`, `native-subagents.js`).
 * - the AIR extension's `asyncTasks` under `_meta.jetbrains.air`: background work, which is how a dev
 *   server runs, published as `async_task_spawned` / `_progress` / `_state_update` (`async-tasks.js`).
 * - `_meta.terminal_output`: a command's output rides its tool call as `terminal_info`,
 *   `terminal_output` and `terminal_exit` (`tools.js`).
 *
 *   node tools/console2-probe.mjs
 *
 * One scratch git room and ONE turn, under the posture and the rules carrier the driver uses (`auto`,
 * then `acceptEdits`; a settings file on `session/new`). The turn runs a foreground command, starts a
 * background one that outlives the turn, and delegates one read to a subagent. Every frame in both
 * directions is logged, and the reader stays open after the turn to see what arrives between turns.
 *
 * 🔴 It spends one small real turn on the machine's own signed-in account (authorised 2026-09-24).
 * The enclosing agent session's `CLAUDE*` environment is stripped, as DRV4 notes. The adapter is the
 * one the scratch machine pins, `_fixtures/dsh/npm` (the dsh probe's install).
 */
import { spawn, spawnSync } from 'node:child_process';
import { appendFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(repoRoot, '_fixtures', 'console2-probe');
const adapterRoot = join(repoRoot, '_fixtures', 'dsh', 'npm', 'node_modules', '@agentclientprotocol', 'claude-agent-acp');
const entry = join(adapterRoot, 'dist', 'index.js');
const framesPath = join(scratch, 'frames.jsonl');
const stderrPath = join(scratch, 'stderr.txt');

/** How long the reader stays open after the turn, to see what a background task says between turns. */
const LINGER_MS = 30_000;

if (!existsSync(entry)) {
  console.error(`No adapter at ${entry} — install the dsh probe's packages first (tools/dsh-probes/README.md).`);
  process.exit(2);
}

/** The environment without the enclosing agent session's markers (DRV4). */
const env = Object.fromEntries(Object.entries(process.env)
  .filter(([key]) => !/^CLAUDE/i.test(key) && key !== 'AI_AGENT'));

// A fresh room.
rmSync(scratch, { recursive: true, force: true });
const room = join(scratch, 'room');
mkdirSync(room, { recursive: true });
spawnSync('git', ['init', '-q', '-b', 'main'], { cwd: room });
spawnSync('git', ['config', 'user.email', 'probe@example.com'], { cwd: room });
spawnSync('git', ['config', 'user.name', 'CONSOLE2 probe'], { cwd: room });
writeFileSync(join(room, 'README.md'), '# console2 room\n\nA scratch room for the CONSOLE2 probe.\n');
spawnSync('git', ['add', '-A'], { cwd: room });
spawnSync('git', ['commit', '-q', '-m', 'room'], { cwd: room });

// The rules carrier the driver hands every session (PERM1), outside the room.
const settingsFile = join(scratch, 'settings.json');
writeFileSync(settingsFile, `${JSON.stringify({ permissions: { allow: ['Bash(node:*)', 'Read'], ask: [], deny: [] } }, null, 2)}\n`);

const version = JSON.parse(readFileSync(join(adapterRoot, 'package.json'), 'utf8')).version;
const cli = spawnSync(process.execPath, [entry, '--cli', '--version'], { env, encoding: 'utf8' });
console.log(`CONSOLE2a — claude-agent-acp ${version}, Claude Code ${(cli.stdout || cli.stderr || '?').trim()}`);

const started = Date.now();
const log = (record) => appendFileSync(framesPath, `${JSON.stringify({ ms: Date.now() - started, ...record })}\n`);

const child = spawn(process.execPath, [entry], { cwd: room, stdio: ['pipe', 'pipe', 'pipe'], env });
child.stderr.on('data', (chunk) => appendFileSync(stderrPath, chunk));

const pending = new Map();
let nextId = 1;
let turnEnded = null;
const send = (frame) => {
  const full = { jsonrpc: '2.0', ...frame };
  log({ dir: 'out', frame: full });
  child.stdin.write(`${JSON.stringify(full)}\n`);
};

let buffered = '';
child.stdout.on('data', (chunk) => {
  buffered += chunk.toString();
  let cut;
  while ((cut = buffered.indexOf('\n')) >= 0) {
    const line = buffered.slice(0, cut).trim();
    buffered = buffered.slice(cut + 1);
    if (!line) continue;
    let frame;
    try { frame = JSON.parse(line); } catch { log({ dir: 'in', raw: line }); continue; }
    log({ dir: 'in', frame, afterTurn: turnEnded !== null });

    if (frame.method === 'session/request_permission') {
      // Refused, as Daoris refuses every one (D52).
      const reject = (frame.params?.options ?? []).find((o) => o.kind === 'reject_once' || o.kind === 'reject_always');
      send({ id: frame.id, result: { outcome: reject ? { outcome: 'selected', optionId: reject.optionId } : { outcome: 'cancelled' } } });
    } else if (frame.id !== undefined && frame.method) {
      send({ id: frame.id, error: { code: -32601, message: `daoris probe does not implement ${frame.method}` } });
    } else if (frame.id !== undefined && pending.has(frame.id)) {
      pending.get(frame.id)(frame);
      pending.delete(frame.id);
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

const PROMPT = [
  'This is a probe of how your tools stream. Do exactly these steps, in order, and nothing else:',
  '1. With your Bash tool, in the foreground, run: node -e "for (let i = 1; i <= 3; i++) console.log(\'fg line \' + i)"',
  '2. With your Bash tool, start this IN THE BACKGROUND (run_in_background), and do not wait for it: '
    + 'node -e "let i = 0; const t = setInterval(() => { console.log(\'bg tick \' + (++i)); if (i === 20) clearInterval(t); }, 1000)"',
  '3. Use your Agent tool to start one subagent (general-purpose) with the task: '
    + '"Read README.md in the current directory and reply with its first line only."',
  '4. Reply with the single word DONE. Do not wait for the background command to finish.',
].join('\n');

let problem = null;
let sessionId = null;
try {
  const init = await request('initialize', {
    protocolVersion: 1,
    clientCapabilities: {
      fs: { readTextFile: false, writeTextFile: false },
      terminal: false,
      // The three declarations under test. 🔴 `subagents` alone does nothing at SDK 1.4.0: its
      // `zClientCapabilities` has no such key, so the adapter's parse strips it before the adapter
      // reads it (the first run saw no `subagent_spawned`). The AIR spelling is the one that arrives.
      subagents: {},
      _meta: {
        terminal_output: true,
        jetbrains: { air: { version: 1, capabilities: ['asyncTasks', 'nativeSubagentSessions'] } },
      },
    },
    clientInfo: { name: 'daoris-console2-probe', version: '0' },
  });
  if (init.error) throw new Error(`initialize: ${init.error.message}`);

  const created = await request('session/new', {
    cwd: room, mcpServers: [], _meta: { claudeCode: { options: { settings: settingsFile } } },
  });
  if (created.error) throw new Error(`session/new: ${created.error.message}`);
  sessionId = created.result.sessionId;
  const offered = (created.result.modes?.availableModes ?? []).map((m) => m.id);
  const posture = ['auto', 'acceptEdits'].find((m) => offered.includes(m));
  if (posture) {
    const mode = await request('session/set_mode', { sessionId, modeId: posture });
    if (mode.error) throw new Error(`set_mode ${posture}: ${mode.error.message}`);
  }
  console.log(`session ${sessionId}, posture ${posture ?? '(none offered)'}; prompting…`);

  const prompted = await request('session/prompt', { sessionId, prompt: [{ type: 'text', text: PROMPT }] }, 6 * 60_000);
  turnEnded = Date.now();
  if (prompted.error) problem = `session/prompt: ${prompted.error.message}`;
  console.log(`turn ended: ${prompted.result?.stopReason ?? problem}; lingering ${LINGER_MS / 1000}s for between-turn frames…`);
  await sleep(LINGER_MS);

  const closed = await request('session/close', { sessionId }, 10_000);
  log({ dir: 'meta', event: 'closed', error: closed.error?.message ?? null });
} catch (error) {
  problem = error.message;
} finally {
  child.stdin.end();
  await sleep(1500);
  // The whole tree, as the driver's job object would: a background command must not outlive the probe.
  if (child.exitCode === null) spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F']);
  log({ dir: 'meta', event: 'ended', exitCode: child.exitCode });
}

// The summary: what arrived, by session and by kind.
const frames = readFileSync(framesPath, 'utf8').trim().split('\n').map((l) => JSON.parse(l));
const kinds = new Map();
const tasks = new Map();
const subagents = new Map();
let terminalMetas = 0;
for (const { dir, frame, afterTurn } of frames) {
  if (dir !== 'in' || frame?.method !== 'session/update') continue;
  const update = frame.params?.update ?? {};
  const key = `${frame.params?.sessionId === sessionId ? 'root' : frame.params?.sessionId} ${update.sessionUpdate}${afterTurn ? ' (after the turn)' : ''}`;
  kinds.set(key, (kinds.get(key) ?? 0) + 1);
  if (update._meta?.terminal_output || update._meta?.terminal_info) terminalMetas++;
  if (update.sessionUpdate?.startsWith('async_task')) {
    const was = tasks.get(update.asyncTaskId) ?? {};
    tasks.set(update.asyncTaskId, { ...was, ...update });
  }
  if (update.sessionUpdate?.startsWith('subagent')) {
    const was = subagents.get(update.subagentSessionId) ?? {};
    subagents.set(update.subagentSessionId, { ...was, ...update });
  }
}

console.log(`\nproblem: ${problem ?? 'none'}`);
console.log('updates by session and kind:');
for (const [key, count] of [...kinds].sort()) console.log(`  ${key}: ${count}`);
console.log(`tool updates carrying terminal metas: ${terminalMetas}`);
for (const [id, task] of tasks) {
  const file = task.outputFilePath;
  const held = file && existsSync(file) ? readFileSync(file, 'utf8').trim().split('\n').length : null;
  console.log(`async task ${id}: ${task.name} — ${task.state ?? 'running'}; output file ${file ?? 'none'}${held !== null ? ` (${held} lines now)` : ''}`);
}
for (const [id, sub] of subagents) console.log(`subagent ${id}: ${sub.name} — ${sub.state ?? 'no end seen'}`);
console.log(`\nframes: ${framesPath}`);

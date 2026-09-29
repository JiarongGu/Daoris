/**
 * CONSOLE3c's measurement — what the NATIVE door's `stream-json` carries for a subagent and a
 * background task, which no probe had read (docs/2026-09-28-console2-streams-evidence.md, "Left").
 *
 * The protocol door carries each as its own stream once asked (CONSOLE2). The pipe door runs the same
 * harness as `claude -p … --output-format stream-json --verbose --include-partial-messages`, and the
 * questions are whether a subagent's messages arrive marked with the tool call that spawned it
 * (`parent_tool_use_id`), what a backgrounded command says and where its output goes, and what the
 * binary does with its background work when the turn ends and it exits.
 *
 *   node tools/console3-probe.mjs
 *
 * One scratch git room and ONE turn, with the arguments the pipe door passes (`Adapters.cs`, the
 * Claude Code adapter) and a rules carrier on `--settings` as the driver hands every session. The turn
 * runs a foreground command, starts a background one that outlives the turn, and delegates one read to
 * a subagent. Every line is logged with its time, and after the binary exits the probe watches for the
 * background command's process for a while, to see whether it was left running.
 *
 * 🔴 It spends one small real turn on the machine's own signed-in account, as the CONSOLE2 probe did
 * (authorised 2026-09-24). The enclosing agent session's `CLAUDE*` environment is stripped (DRV4).
 */
import { spawn, spawnSync } from 'node:child_process';
import { appendFileSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(repoRoot, '_fixtures', 'console3-probe');
const framesPath = join(scratch, 'frames.jsonl');
const stderrPath = join(scratch, 'stderr.txt');

/** How long to watch for the background command after the binary exits. */
const LINGER_MS = 25_000;
/** The turn's bound: a probe that hangs is a probe that says nothing. */
const TURN_MS = 240_000;

/** The environment without the enclosing agent session's markers (DRV4). */
const env = Object.fromEntries(Object.entries(process.env)
  .filter(([key]) => !/^CLAUDE/i.test(key) && key !== 'AI_AGENT'));

rmSync(scratch, { recursive: true, force: true });
const room = join(scratch, 'room');
mkdirSync(room, { recursive: true });
spawnSync('git', ['init', '-q', '-b', 'main'], { cwd: room });
spawnSync('git', ['config', 'user.email', 'probe@example.com'], { cwd: room });
spawnSync('git', ['config', 'user.name', 'CONSOLE3 probe'], { cwd: room });
writeFileSync(join(room, 'README.md'), '# console3 room\n\nA scratch room for the CONSOLE3 probe.\n');
spawnSync('git', ['add', '-A'], { cwd: room });
spawnSync('git', ['commit', '-q', '-m', 'room'], { cwd: room });

const settingsFile = join(scratch, 'settings.json');
writeFileSync(settingsFile, `${JSON.stringify({ permissions: { allow: ['Bash(node:*)', 'Read'], ask: [], deny: [] } }, null, 2)}\n`);

const version = spawnSync('claude', ['--version'], { env, encoding: 'utf8' });
console.log(`CONSOLE3c — ${(version.stdout || version.stderr || '?').trim()}`);

const PROMPT = [
  'This is a probe of how your tools stream. Do exactly these steps, in order, and nothing else:',
  '1. With your Bash tool, in the foreground, run: node -e "for (let i = 1; i <= 3; i++) console.log(\'fg line \' + i)"',
  '2. With your Bash tool, start this IN THE BACKGROUND (run_in_background), and do not wait for it: '
    + 'node -e "let i = 0; const t = setInterval(() => { console.log(\'console3 bg tick \' + (++i)); if (i === 40) clearInterval(t); }, 1000)"',
  '3. Use your Agent tool to start one subagent (general-purpose) with the task: '
    + '"Read README.md in the current directory and reply with its first line only."',
  '4. Reply with the single word DONE. Do not wait for the background command to finish.',
].join('\n');

const started = Date.now();
const log = (record) => appendFileSync(framesPath, `${JSON.stringify({ ms: Date.now() - started, ...record })}\n`);

const child = spawn('claude', [
  '-p', PROMPT, '--permission-mode', 'acceptEdits',
  '--output-format', 'stream-json', '--verbose', '--include-partial-messages',
  '--settings', settingsFile,
], { cwd: room, env, stdio: ['ignore', 'pipe', 'pipe'] });
child.stderr.on('data', (chunk) => appendFileSync(stderrPath, chunk));

const lines = [];
let buffered = '';
child.stdout.on('data', (chunk) => {
  buffered += chunk.toString();
  let cut;
  while ((cut = buffered.indexOf('\n')) >= 0) {
    const line = buffered.slice(0, cut).trim();
    buffered = buffered.slice(cut + 1);
    if (!line) continue;
    try {
      const frame = JSON.parse(line);
      lines.push(frame);
      log({ frame });
    } catch {
      log({ raw: line });
    }
  }
});

const exit = await Promise.race([
  new Promise((resolve) => child.on('exit', (code) => resolve(code))),
  sleep(TURN_MS).then(() => 'timed out'),
]);
if (exit === 'timed out') child.kill();
log({ exited: exit });
console.log(`exited: ${exit} after ${Math.round((Date.now() - started) / 1000)}s, ${lines.length} lines`);

// Is the background command still running, now the binary has gone?
const ticking = () => spawnSync('powershell', ['-NoProfile', '-Command',
  "Get-CimInstance Win32_Process -Filter \"Name = 'node.exe'\" | Where-Object { $_.CommandLine -like '*console3 bg tick*' } | ForEach-Object { $_.ProcessId }"],
{ encoding: 'utf8' }).stdout.trim();
const afterExit = ticking();
await sleep(LINGER_MS);
const later = ticking();
log({ backgroundAfterExit: afterExit, backgroundAfterLinger: later });
console.log(`background command after exit: ${afterExit || 'none'}; ${LINGER_MS / 1000}s later: ${later || 'none'}`);

// The summary the evidence is written from; the whole record is in frames.jsonl.
const kinds = new Map();
for (const frame of lines) {
  const kind = [frame.type, frame.subtype, frame.event?.type].filter(Boolean).join('/');
  kinds.set(kind, (kinds.get(kind) ?? 0) + 1);
}
console.log('\nkinds:');
for (const [kind, count] of [...kinds].sort()) console.log(`  ${kind}: ${count}`);

const parented = lines.filter((frame) => frame.parent_tool_use_id);
console.log(`\nlines with a parent_tool_use_id: ${parented.length}`);
for (const id of new Set(parented.map((frame) => frame.parent_tool_use_id))) {
  const own = parented.filter((frame) => frame.parent_tool_use_id === id);
  console.log(`  ${id}: ${own.length} (${[...new Set(own.map((frame) => frame.type))].join(', ')})`);
}

const system = lines.filter((frame) => frame.type === 'system' && frame.subtype !== 'init');
console.log(`\nsystem lines other than init: ${system.length}`);
for (const frame of system) console.log(`  ${JSON.stringify(frame).slice(0, 300)}`);

const results = lines.filter((frame) => frame.type === 'user' && frame.tool_use_result);
console.log('\ntool results carrying tool_use_result:');
for (const frame of results) console.log(`  ${JSON.stringify(frame.tool_use_result).slice(0, 300)}`);

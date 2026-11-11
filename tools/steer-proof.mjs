/**
 * Proof of *Send now* on Claude Code's native wire, keylessly: the harness on PATH as shipped, a scratch home and working folder
 * under `local/scratch/`, and a stub model on loopback (`fake-model.mjs`) with a placeholder key. No account, no credential.
 *
 *   node tools/steer-proof.mjs
 *
 * The harness runs as a conversation does (`-p --input-format stream-json --replay-user-messages`, stream-json out with partial
 * messages), and the word goes as Daoris frames it: one user line with no priority, under a uuid. Two cases:
 *   joined     written during an eight-second tool: echoed under its uuid before the turn's one `result`, and the model's next
 *              call carries it, in the same turn;
 *   next-turn  written while the turn's last text streams: the turn's `result` comes first, then the echo, then a second `result`.
 * A machine with no `claude` on PATH is not ready, and says so with exit 0; a case that does not hold exits 1.
 */
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { startFakeModel } from './fake-model.mjs';
import { isMain } from './fsx.mjs';
import { repoRoot } from './proof-kit.mjs';

const WORD = 'put MANGO in your reply';
const WORD_ID = '11111111-2222-4333-8444-555555555555';

/** One user line as Daoris's native dialect frames it, under `uuid`. */
export const frame = (text, uuid) => `${JSON.stringify({ type: 'user', uuid, message: { role: 'user', content: [{ type: 'text', text }] } })}\n`;

/** Where the word landed, read from the frames in order: before the turn's first result (`joined`), after it (`next-turn`), or never. */
export function landing(frames, uuid) {
  const echo = frames.findIndex((f) => f.type === 'user' && f.isReplay === true && f.uuid === uuid);
  if (echo < 0) return 'never';
  const result = frames.findIndex((f) => f.type === 'result');
  return result < 0 || echo < result ? 'joined' : 'next-turn';
}

async function run(kind, model) {
  const root = join(repoRoot, 'local', 'scratch', `steer-proof-${kind}`);
  rmSync(root, { recursive: true, force: true });
  const home = join(root, 'home');
  const work = join(root, 'work');
  mkdirSync(home, { recursive: true });
  mkdirSync(work, { recursive: true });
  const env = {
    ...process.env, CLAUDE_CONFIG_DIR: home, ANTHROPIC_BASE_URL: model.url, ANTHROPIC_API_KEY: 'placeholder-not-a-key',
    DISABLE_UPDATES: '1', CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC: '1',
  };
  delete env.ANTHROPIC_AUTH_TOKEN;
  const args = ['-p', '--input-format', 'stream-json', '--replay-user-messages', '--permission-mode', 'bypassPermissions',
    '--model', 'claude-sonnet-4-5', '--strict-mcp-config', '--setting-sources', 'project',
    '--output-format', 'stream-json', '--verbose', '--include-partial-messages'];
  const child = spawn('claude', args, { cwd: work, env, shell: process.platform === 'win32', stdio: ['pipe', 'pipe', 'ignore'] });
  const frames = [];
  const firstCall = model.calls.length;
  let pending = '';
  let sent = false;
  let results = 0;
  const done = new Promise((resolve) => {
    const bound = setTimeout(() => child.kill(), 90_000);
    child.on('exit', () => {
      clearTimeout(bound);
      resolve();
    });
  });
  child.stdout.on('data', (chunk) => {
    pending += chunk.toString('utf8');
    for (let at = pending.indexOf('\n'); at >= 0; at = pending.indexOf('\n')) {
      const line = pending.slice(0, at);
      pending = pending.slice(at + 1);
      let f;
      try {
        f = JSON.parse(line);
      } catch {
        continue;
      }
      if (f.type !== 'stream_event') frames.push(f);
      const toolAnnounced = f.type === 'assistant' && JSON.stringify(f.message?.content ?? []).includes('"tool_use"');
      const streaming = f.type === 'stream_event' && f.event?.type === 'content_block_delta' && f.event?.delta?.type === 'text_delta';
      if (!sent && ((kind === 'joined' && toolAnnounced) || (kind === 'next-turn' && streaming))) {
        sent = true;
        setTimeout(() => child.stdin.write(frame(WORD, WORD_ID)), kind === 'joined' ? 2000 : 500);
      }
      if (f.type === 'result' && ++results >= (kind === 'joined' ? 1 : 2)) setTimeout(() => child.stdin.end(), 2000);
    }
  });
  child.stdin.write(frame(kind === 'joined' ? 'run the TOOL now' : 'STREAM some words', '00000000-1111-4222-8333-444444444444'));
  await done;
  const calls = model.calls.slice(firstCall);
  const carried = calls.findIndex((call) => call.input.some((text) => text.includes('MANGO')));
  return { landed: landing(frames, WORD_ID), results: frames.filter((f) => f.type === 'result').length, carried, calls: calls.length };
}

async function main() {
  const version = spawnSync('claude', ['--version'], { shell: process.platform === 'win32', encoding: 'utf8' });
  if (version.status !== 0) {
    console.log('not ready: no `claude` on PATH, so nothing was proven.');
    return 0;
  }

  console.log(`claude ${version.stdout.trim()}`);
  const model = await startFakeModel();
  let failed = 0;
  try {
    const expected = { joined: { landed: 'joined', results: 1 }, 'next-turn': { landed: 'next-turn', results: 2 } };
    for (const kind of Object.keys(expected)) {
      const seen = await run(kind, model);
      const holds = seen.landed === expected[kind].landed && seen.results === expected[kind].results && seen.carried >= 0;
      if (!holds) failed += 1;
      console.log(`${holds ? 'ok  ' : 'FAIL'} ${kind}: landed ${seen.landed}, ${seen.results} result(s), the word in model call ${seen.carried + 1} of ${seen.calls}`);
    }
  } finally {
    await model.close();
  }

  return failed > 0 ? 1 : 0;
}

if (isMain(import.meta.url)) process.exit(await main());

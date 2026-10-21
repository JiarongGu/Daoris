// The example's own test of what its contract test cannot see: act(), check() and remove() on its real "platform", the previews in
// its data folder, with no stand-in world. It starts the plugin as the driver does and imports nothing but Node.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

const folder = dirname(fileURLToPath(import.meta.url));

/** The plugin started with a scratch data folder and home and no world; its answers and events heard. */
function start(t, data, home) {
  const child = spawn('node', [join(folder, 'plugin.mjs')], {
    cwd: folder,
    env: { ...process.env, DAORIS_PLUGIN_WORLD: '', DAORIS_PLUGIN_ID: 'local-preview', DAORIS_PLUGIN_FOLDER: folder, DAORIS_PLUGIN_DATA: data, DAORIS_HOME: home },
  });
  const gone = new Promise((resolveGone) => child.on('close', resolveGone));
  t.after(async () => {
    child.kill();
    await gone;
  });
  const waiting = new Map();
  const events = [];
  createInterface({ input: child.stdout }).on('line', (line) => {
    const frame = JSON.parse(line);
    if ('method' in frame) events.push(frame);
    else waiting.get(frame.id)?.(frame);
  });
  let next = 0;
  const ask = (method, params) => new Promise((resolveAnswer, reject) => {
    const id = ++next;
    const timer = setTimeout(() => reject(new Error(`no answer to ${method}`)), 10000);
    waiting.set(id, (frame) => {
      clearTimeout(timer);
      if (frame.error) reject(new Error(frame.error.message));
      else resolveAnswer(frame.result);
    });
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
  });
  const until = async (invocation) => {
    for (let waited = 0; waited < 10000; waited += 50) {
      const found = events.find((event) => event.params?.invocation === invocation);
      if (found) return found;
      await new Promise((resolveWait) => setTimeout(resolveWait, 50));
    }
    throw new Error(`no outcome for ${invocation}`);
  };
  return { ask, until, events };
}

const attempt = (n) => ({
  unit: 'u-1', step: 'local-preview/perform', stepVersion: 1, invocation: `i-${n}`, idempotencyKey: `k-${n}`, subjectRevision: `rev-${n}`,
  inputs: [], deadlineAt: new Date(Date.now() + 60000).toISOString(),
});

function scratch(t) {
  const at = mkdtempSync(join(tmpdir(), 'local-preview-'));
  t.after(() => rmSync(at, { recursive: true, force: true }));
  mkdirSync(join(at, 'home'));
  return { data: join(at, 'data'), home: join(at, 'home') };
}

test('an attempt writes its preview once, says it reached on its receipt, and its cleanup removes it', async (t) => {
  const { data, home } = scratch(t);
  const plugin = start(t, data, home);
  await plugin.ask('initialize', { protocolVersion: 1, plugin: 'local-preview', home, data, points: ['work/watch', 'work/step', 'work/perform'] });

  await plugin.ask('hook/work/perform', attempt(1));
  const outcome = await plugin.until('i-1');
  assert.equal(outcome.params.state, 'reached');
  assert.equal(outcome.params.evidence.ref, 'local-preview/receipt@1:i-1');
  assert.deepEqual(outcome.params.resources, [{ schema: 'local-preview/resource@1', id: 'i-1' }]);
  assert.equal(readFileSync(join(data, 'previews', 'i-1', 'revision.txt'), 'utf8'), 'rev-1\n');

  const cleanup = { receipt: 'c-1', resource: 'i-1', idempotencyKey: 'ck-1' };
  assert.equal((await plugin.ask('hook/work/cleanup', cleanup)).disposition, 'removed');
  assert.equal(existsSync(join(data, 'previews', 'i-1')), false);
  assert.equal((await plugin.ask('hook/work/cleanup', cleanup)).disposition, 'already-gone');
});

test('an attempt an earlier process began is done where its preview finished, and begun again where it did not', async (t) => {
  const { data, home } = scratch(t);
  // A journal and previews as a process left them: `i-1` finished its preview, `i-2` was cut off partway.
  mkdirSync(join(data, 'previews', 'i-1'), { recursive: true });
  writeFileSync(join(data, 'previews', 'i-1', 'done'), 'x\n');
  mkdirSync(join(data, 'previews', 'i-2'), { recursive: true });
  const begun = (n) => ({ ...attempt(n), state: 'begun', life: 'gone' });
  writeFileSync(join(data, 'work-journal.json'), JSON.stringify({ attempts: { 'k-1': begun(1), 'k-2': begun(2) } }));
  const plugin = start(t, data, home);
  await plugin.ask('initialize', { protocolVersion: 1, plugin: 'local-preview', home, data, points: ['work/watch', 'work/step', 'work/perform'] });

  const asked = (n) => plugin.ask('hook/work/reconcile', { unit: 'u-1', step: 'local-preview/perform', invocation: `i-${n}`, idempotencyKey: `k-${n}`, subjectRevision: `rev-${n}` });
  assert.equal((await asked(1)).state, 'done');
  assert.equal((await asked(2)).state, 'not-started');
  // Delivered again, the cut-off attempt is made whole.
  await plugin.ask('hook/work/perform', attempt(2));
  assert.equal((await plugin.until('i-2')).params.state, 'reached');
  assert.ok(existsSync(join(data, 'previews', 'i-2', 'done')));
});

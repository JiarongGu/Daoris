// The example's own test of what its contract test cannot see: find, release and publish on their real "platform", the folders in
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
const REF = 'local-release/release@1:1.2.0';

function start(t, data, home) {
  const child = spawn('node', [join(folder, 'plugin.mjs')], {
    cwd: folder,
    windowsHide: true,
    env: { ...process.env, DAORIS_PLUGIN_WORLD: '', DAORIS_PLUGIN_ID: 'local-release', DAORIS_PLUGIN_FOLDER: folder, DAORIS_PLUGIN_DATA: data, DAORIS_HOME: home },
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
  return { ask, until };
}

const attempt = (step, n, inputs = []) => ({
  unit: 'u-1', step: `local-release/${step}`, stepVersion: 1, invocation: `i-${n}`, idempotencyKey: `k-${n}`, subjectRevision: 'rev-1',
  inputs, deadlineAt: new Date(Date.now() + 60000).toISOString(),
});

function scratch(t) {
  const at = mkdtempSync(join(tmpdir(), 'local-release-'));
  t.after(() => rmSync(at, { recursive: true, force: true }));
  mkdirSync(join(at, 'home'));
  return { data: join(at, 'data'), home: join(at, 'home') };
}

async function ready(t) {
  const { data, home } = scratch(t);
  const plugin = start(t, data, home);
  await plugin.ask('initialize', { protocolVersion: 1, plugin: 'local-release', home, data, points: ['work/watch', 'work/step', 'work/perform'] });
  return { data, plugin };
}

test('find, release and publish run the tail: a version found, its tag recorded, its package copied, each staging removed once', async (t) => {
  const { data, plugin } = await ready(t);
  mkdirSync(join(data, 'pending'), { recursive: true });
  writeFileSync(join(data, 'pending', 'rev-1.json'), JSON.stringify({ version: '1.2.0', notes: 'Adds the thing.' }));
  mkdirSync(join(data, 'incoming', '1.2.0'), { recursive: true });
  writeFileSync(join(data, 'incoming', '1.2.0', 'pkg.txt'), 'built\n');

  await plugin.ask('hook/work/perform', attempt('find', 1));
  const found = await plugin.until('i-1');
  assert.equal(found.params.state, 'reached');
  assert.equal(found.params.outcome, 'release');
  assert.deepEqual(found.params.outputs, { release: { ref: REF } });

  await plugin.ask('hook/work/perform', attempt('release', 2, [REF]));
  assert.deepEqual((await plugin.until('i-2')).params.outputs, { release: { ref: REF } });
  assert.deepEqual(JSON.parse(readFileSync(join(data, 'releases', '1.2.0.json'), 'utf8')), { tag: 'v1.2.0', notes: 'Adds the thing.', revision: 'rev-1' });

  await plugin.ask('hook/work/perform', attempt('publish', 3, [REF]));
  assert.equal((await plugin.until('i-3')).params.state, 'reached');
  assert.equal(readFileSync(join(data, 'registry', '1.2.0', 'pkg.txt'), 'utf8'), 'built\n');

  for (const n of [1, 2, 3]) {
    const cleanup = { receipt: `c-${n}`, resource: `i-${n}`, idempotencyKey: `ck-${n}` };
    assert.equal((await plugin.ask('hook/work/cleanup', cleanup)).disposition, 'removed');
    assert.equal(existsSync(join(data, 'staging', `i-${n}`)), false);
    assert.equal((await plugin.ask('hook/work/cleanup', cleanup)).disposition, 'already-gone');
  }
  assert.ok(existsSync(join(data, 'registry', '1.2.0')), 'cleanup leaves what was published');
});

test('find says release when one is pending and none when not, and publish never copies what was not released', async (t) => {
  const { data, plugin } = await ready(t);

  await plugin.ask('hook/work/perform', attempt('find', 1));
  const none = await plugin.until('i-1');
  assert.equal(none.params.state, 'reached');
  assert.equal(none.params.outcome, 'none');
  assert.equal(none.params.outputs, undefined);

  await plugin.ask('hook/work/perform', attempt('publish', 2, [REF]));
  assert.equal((await plugin.until('i-2')).params.code, 'not-released');
  assert.equal(existsSync(join(data, 'registry')), false);
});

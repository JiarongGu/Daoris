// The reports' own cases, over the wire as the driver speaks it: a report filed and read back, two processes filing at once keeping both,
// a mark, an unread list in its order, and the refusals. `node --test` runs it with nothing of Daoris's installed.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

const entry = join(dirname(fileURLToPath(import.meta.url)), 'plugin.mjs');

function storage(t) {
  const at = mkdtempSync(join(tmpdir(), 'daoris-dev-report-'));
  t.after(() => rmSync(at, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 }));
  return at;
}

/** One round in a process of its own, as the driver starts one: the handshake, the command, the shutdown. */
async function round(folder, params, extra = {}) {
  const child = spawn(process.execPath, [entry], { env: { ...process.env, DAORIS_PLUGIN_PRIVATE: folder, ...extra }, stdio: ['pipe', 'pipe', 'inherit'] });
  const lines = createInterface({ input: child.stdout })[Symbol.asyncIterator]();
  const ask = async (frame) => {
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', ...frame })}\n`);
    return (await lines.next()).value;
  };
  const hello = JSON.parse(await ask({ id: 1, method: 'initialize', params: { points: ['session/ended'] } }));
  assert.deepEqual(hello.result.points, ['session/ended']);
  const answer = JSON.parse(await ask({ id: 2, method: 'hook/command/run', params: { run: 'r', answers: {}, from: 'agent', place: 'machine', language: 'en', ...params } }));
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'shutdown' })}\n`);
  await new Promise((resolve) => child.on('close', resolve));
  assert.equal(answer.error, undefined);
  return answer.result;
}

const report = (folder, args, session = 's-1') => round(folder, { command: 'report', args, session }, {});
const inbox = (folder, args = {}) => round(folder, { command: 'inbox', args, from: 'terminal' });

test('a report is filed with its id, and read back whole with the session and place', async (t) => {
  const folder = storage(t);
  const filed = await report(folder, { title: 'Driver stalls', body: 'The **loop** waits on a lock.', evidence: 'driver.log:41 held', severity: 'defect' });
  assert.equal(filed.exit, 0);
  assert.match(filed.data.id, /^\d{8}T\d{6}Z-[0-9a-f]{6}$/);
  assert.ok(filed.said[0].includes(filed.data.id));

  const read = await inbox(folder, { action: 'read', id: filed.data.id });
  assert.equal(read.exit, 0);
  assert.equal(read.data.title, 'Driver stalls');
  assert.equal(read.data.session, 's-1');
  assert.equal(read.data.place, 'machine');
  assert.equal(read.data.severity, 'defect');
  const text = read.said.join('\n');
  assert.ok(text.includes('The **loop** waits on a lock.') && text.includes('driver.log:41 held') && text.includes('unread'));

  const tail = await inbox(folder, { action: 'read', id: filed.data.id.split('-')[1] });
  assert.equal(tail.data.id, filed.data.id);
});

test('two reports filed at once are both kept', async (t) => {
  const folder = storage(t);
  const both = await Promise.all([
    report(folder, { title: 'first', body: 'a' }, 's-a'),
    report(folder, { title: 'second', body: 'b' }, 's-b'),
    report(folder, { title: 'third', body: 'c' }, 's-c'),
  ]);
  assert.equal(new Set(both.map((each) => each.data.id)).size, 3);
  const listed = await inbox(folder);
  assert.equal(listed.data.reports.length, 3);
});

test('mark read moves a report out of the unread list, which lists unread first and then newest first', async (t) => {
  const folder = storage(t);
  const one = await report(folder, { title: 'one', body: 'a' });
  await new Promise((resolve) => setTimeout(resolve, 1100));
  const two = await report(folder, { title: 'two', body: 'b', severity: 'blocking' });

  assert.deepEqual((await inbox(folder)).data.reports.map((each) => each.title), ['two', 'one']);
  const marked = await inbox(folder, { action: 'mark', id: two.data.id });
  assert.equal(marked.exit, 0);
  assert.deepEqual((await inbox(folder)).data.reports.map((each) => each.title), ['one', 'two']);
  assert.deepEqual((await inbox(folder, { unread: true })).data.reports.map((each) => each.id), [one.data.id]);
  assert.equal((await inbox(folder, { action: 'read', id: two.data.id })).data.read !== null, true);
});

test('a report with no title or body, a severity not offered, or an unknown id is refused', async (t) => {
  const folder = storage(t);
  assert.equal((await report(folder, { body: 'x' })).exit, 1);
  assert.equal((await report(folder, { title: 'x' })).exit, 1);
  assert.equal((await report(folder, { title: 'x', body: 'y', severity: 'fatal' })).exit, 1);
  assert.equal((await report(folder, { title: 'x', body: 'y'.repeat(6000) })).exit, 1);
  assert.equal((await inbox(folder, { action: 'read', id: 'nope' })).exit, 1);
  assert.equal((await inbox(folder)).said[0], 'No report is filed.');
});

test('with no private storage handed it says so and writes nothing', async () => {
  const child = await round('', { command: 'inbox', args: {} }, { DAORIS_PLUGIN_PRIVATE: '' });
  assert.equal(child.exit, 1);
});

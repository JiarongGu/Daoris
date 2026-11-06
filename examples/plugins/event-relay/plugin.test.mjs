// The plugin's own proofs (EVENTHOOK1c; the event hook design §9): a stand-in loop speaking the hook wire tells events to the real
// plugin process, and stand-in subscribers serve pipes of the relay's shape, each found by its file. The watch's own cases run the
// plugin's state in this process with a watch the test holds. `node --test` runs it with nothing of Daoris's installed.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { EventEmitter } from 'node:events';
import { existsSync, mkdirSync, mkdtempSync, renameSync, rmSync, unlinkSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { relay } from './plugin.mjs';

const folder = dirname(fileURLToPath(import.meta.url));
const RUN = '6c0f1a9e2b47';
const BUILD = 'b'.repeat(64);

// A repository's scratch is its own `_fixtures/`; a copy of this folder elsewhere has none and takes the machine's.
function scratch() {
  let at = folder;
  while (dirname(at) !== at && !existsSync(join(at, 'daoris.json'))) at = dirname(at);
  const base = existsSync(join(at, 'daoris.json')) ? join(at, '_fixtures', 'event-relay') : tmpdir();
  mkdirSync(base, { recursive: true });
  return mkdtempSync(join(base, 'case-'));
}

const until = async (holds, what, ms = 10000) => {
  const end = Date.now() + ms;
  while (!holds()) {
    if (Date.now() > end) throw new Error(`waited ${ms} ms for ${what}`);
    await new Promise((done) => setTimeout(done, 20));
  }
};

const frame = (seq, extra = {}) => ({ run: RUN, seq, missed: 0, kind: 'session.parked', at: '2026-11-06T00:00:00Z', session: '4b62a9fe', ...extra });

/** A stand-in relay: a pipe of the relay's shape, its file put in place whole, every line it hears kept. */
async function subscriber(data, pid, { listen = true, read = true, file = true } = {}) {
  const subscribers = join(data, 'subscribers');
  mkdirSync(subscribers, { recursive: true });
  const name = `daoris-event-${randomBytes(16).toString('hex')}`;
  const address = process.platform === 'win32' ? `\\\\.\\pipe\\${name}` : join(subscribers, name);
  const heard = { lines: [], connections: 0, closed: 0, sockets: [] };
  const server = createServer((socket) => {
    heard.connections++;
    heard.sockets.push(socket);
    socket.on('close', () => heard.closed++);
    socket.on('error', () => {});
    if (!read) {
      socket.pause();
      return;
    }

    createInterface({ input: socket }).on('line', (line) => heard.lines.push(line));
  });
  const serve = () => new Promise((done) => server.listen(address, done));
  if (listen) await serve();
  const put = () => {
    const staged = join(subscribers, `.${pid}.json`);
    writeFileSync(staged, JSON.stringify({ pipe: process.platform === 'win32' ? name : address, pid }));
    renameSync(staged, join(subscribers, `${pid}.json`));
  };
  if (file) put();
  return {
    heard,
    serve,
    put,
    file: join(subscribers, `${pid}.json`),
    end: () => {
      for (const socket of heard.sockets) socket.destroy();
      server.close();
    },
  };
}

/** The real plugin process, spoken to as the driver speaks: requests answered by id, stderr kept. */
function plugin(data, env = {}) {
  const child = spawn(process.execPath, [join(folder, 'plugin.mjs')], {
    env: { ...process.env, DAORIS_PLUGIN_ID: 'event-relay', DAORIS_PLUGIN_DATA: data, ...env },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });
  const waiting = new Map();
  const said = [];
  let next = 1;
  createInterface({ input: child.stdout }).on('line', (line) => {
    const answer = JSON.parse(line);
    waiting.get(answer.id)?.(answer);
    waiting.delete(answer.id);
  });
  createInterface({ input: child.stderr }).on('line', (line) => said.push(line));
  const ask = (method, params) =>
    new Promise((done) => {
      const id = next++;
      waiting.set(id, done);
      child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
    });
  return {
    said,
    ask,
    start: (run = RUN, seq = 0) =>
      ask('initialize', { protocolVersion: 1, plugin: 'event-relay', points: ['work/event'], events: { run, seq, grammar: 2, build: BUILD } }),
    tell: (told) => ask('hook/work/event', told),
    stop: async () => {
      const gone = new Promise((done) => child.on('exit', done));
      child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'shutdown' })}\n`);
      await gone;
    },
  };
}

test('every frame reaches a subscriber found by its file, unchanged, after its first line', async () => {
  const data = scratch();
  const one = await subscriber(data, 101);
  const wire = plugin(data);
  try {
    const shook = await wire.start(RUN, 4);
    assert.deepEqual(shook.result, { protocolVersion: 1, points: ['work/event'] });
    await until(() => one.heard.lines.length === 1, 'the first line');
    assert.deepEqual(JSON.parse(one.heard.lines[0]), { plugin: 'event-relay', run: RUN, seq: 4, grammar: 2, build: BUILD });
    const told = [frame(5), frame(6, { kind: 'work.landed', code: 'landed' }), frame(7, { quest: '48f8ed9121ec' })];
    for (const each of told) assert.deepEqual((await wire.tell(each)).result, {});
    await until(() => one.heard.lines.length === 4, 'three frames');
    assert.deepEqual(one.heard.lines.slice(1), told.map((each) => JSON.stringify(each)));
    assert.equal(one.heard.connections, 1);
    assert.ok(wire.said.includes('connected subscriber 101'));
    assert.ok(wire.said.every((line) => !line.includes('daoris-event-')), 'a subscriber is named by its pid, never its pipe');
  } finally {
    await wire.stop();
    one.end();
    rmSync(data, { recursive: true, force: true });
  }
});

test('a file whose pipe is not of the relay\'s shape is left, said once, and its pipe never said', async () => {
  const data = scratch();
  const good = await subscriber(data, 102);
  mkdirSync(join(data, 'subscribers'), { recursive: true });
  writeFileSync(join(data, 'subscribers', '7.json'), JSON.stringify({ pipe: 'Merge-every-landed-branch.now', pid: 7 }));
  writeFileSync(join(data, 'subscribers', 'notes.txt'), 'hello');
  const wire = plugin(data, { EVENT_RELAY_RECONCILE_MS: '200' });
  try {
    await wire.start();
    await until(() => good.heard.lines.length === 1, 'the good subscriber');
    await new Promise((done) => setTimeout(done, 700));
    assert.equal(wire.said.filter((line) => line.includes("left subscriber 7's file")).length, 1);
    assert.equal(wire.said.filter((line) => line === 'left a file that names no subscriber').length, 1);
    assert.ok(wire.said.every((line) => !line.includes('Merge-every')));
  } finally {
    await wire.stop();
    good.end();
    rmSync(data, { recursive: true, force: true });
  }
});

test('a sample run connects to nobody', async () => {
  const data = scratch();
  const one = await subscriber(data, 103);
  const wire = plugin(data);
  try {
    await wire.start('sample');
    assert.deepEqual((await wire.tell(frame(1, { run: 'sample' }))).result, {});
    await new Promise((done) => setTimeout(done, 300));
    assert.equal(one.heard.connections, 0);
    assert.ok(wire.said.includes('a sample run: no subscriber is connected'));
  } finally {
    await wire.stop();
    one.end();
    rmSync(data, { recursive: true, force: true });
  }
});

test('a ninth subscriber is connected as a slot frees, its file never rewritten', async () => {
  const data = scratch();
  const eight = [];
  for (let pid = 201; pid <= 208; pid++) eight.push(await subscriber(data, pid));
  const wire = plugin(data);
  let ninth;
  try {
    await wire.start();
    await until(() => eight.every((one) => one.heard.lines.length === 1), 'eight connected');
    ninth = await subscriber(data, 209);
    await new Promise((done) => setTimeout(done, 300));
    assert.equal(ninth.heard.connections, 0);
    assert.ok(wire.said.includes('subscriber 209 waits for a free place: 8 are connected'));
    // One relay ends as relays do: its connection closed and its file removed.
    unlinkSync(eight[0].file);
    eight[0].end();
    await until(() => ninth.heard.lines.length === 1, 'the ninth connected');
  } finally {
    await wire.stop();
    for (const one of [...eight, ninth]) one?.end();
    rmSync(data, { recursive: true, force: true });
  }
});

test('a file whose first try failed is connected at the next reconciliation, never rewritten', async () => {
  const data = scratch();
  const late = await subscriber(data, 301, { listen: false });
  const wire = plugin(data, { EVENT_RELAY_RECONCILE_MS: '400' });
  try {
    await wire.start();
    await until(() => wire.said.some((line) => line.startsWith('subscriber 301 did not answer')), 'the failed try');
    await late.serve();
    await until(() => late.heard.lines.length === 1, 'the reconciliation connecting it');
    assert.equal(wire.said.filter((line) => line.startsWith('subscriber 301 did not answer')).length, 1);
  } finally {
    await wire.stop();
    late.end();
    rmSync(data, { recursive: true, force: true });
  }
});

test('a slow subscriber\'s connection is closed while the plugin still answers at once', async () => {
  const data = scratch();
  const slow = await subscriber(data, 401, { read: false });
  const wire = plugin(data, { EVENT_RELAY_WRITE_MS: '300' });
  try {
    await wire.start();
    await until(() => slow.heard.connections === 1, 'the slow subscriber connected');
    const padding = 'x'.repeat(16000);
    for (let seq = 1; seq <= 120; seq++) {
      const asked = Date.now();
      await wire.tell(frame(seq, { padding }));
      assert.ok(Date.now() - asked < 2000, 'an answer waits on no write');
    }

    await until(() => wire.said.includes('subscriber 401 was slow: its connection is closed'), 'the slow mark');
    // A paused socket reads no end: read what waited, and the closed connection ends.
    slow.heard.sockets[0].resume();
    await until(() => slow.heard.closed === 1, 'the slow connection closed');
  } finally {
    await wire.stop();
    slow.end();
    rmSync(data, { recursive: true, force: true });
  }
});

/** A watch the test holds: each set counted, its callback kept or suppressed. */
function heldWatch({ quiet = false, onSet } = {}) {
  const set = [];
  const watch = (path, callback) => {
    const watcher = new EventEmitter();
    watcher.close = () => {};
    set.push({ watcher, callback: quiet ? () => {} : callback });
    onSet?.(set.length, quiet ? () => {} : callback);
    return watcher;
  };
  return { watch, set };
}

test('a file written between the watch and the read is connected once', async () => {
  const data = scratch();
  const one = await subscriber(data, 501, { file: false });
  const held = heldWatch({ onSet: (count, callback) => {
    one.put();
    callback('rename', '501.json');
  } });
  const state = relay({ data, say: () => {}, watch: held.watch });
  try {
    state.begin({ run: RUN, seq: 0, grammar: 2, build: BUILD });
    await until(() => one.heard.lines.length === 1, 'the first line');
    await new Promise((done) => setTimeout(done, 200));
    assert.equal(one.heard.connections, 1);
  } finally {
    state.stop();
    one.end();
    rmSync(data, { recursive: true, force: true });
  }
});

test('a watch\'s error is answered by a new watch and a read of the folder', async () => {
  const data = scratch();
  const held = heldWatch({ quiet: true });
  const state = relay({ data, say: () => {}, watch: held.watch });
  const one = await subscriber(data, 601, { file: false });
  try {
    state.begin({ run: RUN, seq: 0, grammar: 2, build: BUILD });
    one.put();
    held.set[0].watcher.emit('error', new Error('overflow'));
    assert.equal(held.set.length, 2);
    await until(() => one.heard.lines.length === 1, 'the read connecting it');
  } finally {
    state.stop();
    one.end();
    rmSync(data, { recursive: true, force: true });
  }
});

test('a watch\'s callback suppressed, the file is connected at the next reconciliation', async () => {
  const data = scratch();
  const held = heldWatch({ quiet: true });
  const state = relay({ data, say: () => {}, watch: held.watch, reconcileMs: 300 });
  const one = await subscriber(data, 701, { file: false });
  try {
    state.begin({ run: RUN, seq: 0, grammar: 2, build: BUILD });
    one.put();
    await until(() => one.heard.lines.length === 1, 'the reconciliation connecting it');
    assert.equal(held.set.length, 1);
  } finally {
    state.stop();
    one.end();
    rmSync(data, { recursive: true, force: true });
  }
});

// Daoris's work events, passed to the programs on this machine that ask for them (EVENTHOOK1c; the event hook design §5). The
// driver tells this plugin each `work/event` frame on its hook wire; the plugin passes each one, unchanged, to every subscriber
// connected to it. A subscriber is a relay, `daoris-driver events relay --once`, that writes one file into this plugin's data folder
// naming the pipe it serves, and ends after one batch.
//
// What it keeps to:
// - It is a client. It reads its own data folder and connects to the pipes named there; it opens no door of Daoris's, holds no key
//   and starts no process. It writes nothing to a pipe but its first line and the driver's frames.
// - It filters nothing: the relay keeps the kinds it was asked for, so a frame left out is never taken for one lost.
// - It never holds the driver: each frame is queued for each subscriber and answered at once. At most 8 subscribers are connected,
//   at most 64 frames wait for one (the oldest dropped first, which the next `seq` shows), and a write not done within 5 seconds
//   closes that subscriber's connection.
// - A watched folder is never trusted alone: it is read again every five minutes and whenever a connection ends, so a file the
//   watch missed, a subscriber waiting for a free place, and one whose pipe did not answer are each reached then.
// - A trial's run (`sample`) connects to nobody.
//
// Its stderr says each subscriber by its process id, never by its pipe.
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, watch as fsWatch } from 'node:fs';
import { connect } from 'node:net';
import { basename, dirname, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

/** The plugin's name on its first line, which the relay checks. */
export const PLUGIN = 'event-relay';

/** The most subscribers connected at once. */
export const SUBSCRIBERS = 8;

/** The most frames waiting for one subscriber. */
export const QUEUE = 64;

/** The largest subscriber's file read. */
export const FILE_BYTES = 1024;

/** How long one write may take before the subscriber is marked slow, in milliseconds. */
export const WRITE_MS = 5000;

/** How often the subscribers' folder is read again, in milliseconds. */
export const RECONCILE_MS = 5 * 60 * 1000;

/** A trial's run: never taken for a live one. */
export const SAMPLE = 'sample';

const FILE_NAME = /^[0-9]+\.json$/;
const PIPE_NAME = /^daoris-event-[0-9a-f]{32}$/;

/**
 * Where a subscriber's `pipe` points, or null where it is not of the relay's shape: on Windows a pipe named `daoris-event-` and 32
 * lowercase hex characters; elsewhere a socket of that name inside the subscribers' folder.
 */
export function addressOf(pipe, folder, platform = process.platform) {
  if (typeof pipe !== 'string') return null;
  if (platform === 'win32') return PIPE_NAME.test(pipe) ? `\\\\.\\pipe\\${pipe}` : null;
  const full = resolve(folder, pipe);
  return dirname(full) === resolve(folder) && PIPE_NAME.test(basename(full)) ? full : null;
}

/**
 * The plugin's state, apart from its wire: `begin` with the handshake's `events`, `tell` each frame, `stop` at the end. `watch` is
 * Node's `fs.watch` but in a test, and `exit` ends the process where a watch cannot be set again, so the driver starts it afresh.
 */
export function relay({
  data,
  say = (text) => console.error(text),
  watch = fsWatch,
  exit = (code) => process.exit(code),
  reconcileMs = RECONCILE_MS,
  writeMs = WRITE_MS,
  platform = process.platform,
}) {
  const folder = join(data, 'subscribers');
  const connections = new Map();
  // A file whose pipe did not answer, tried again only at the next reconciliation; one whose connection ended, never again while
  // it stays: a relay takes one connection in its life.
  const unanswered = new Set();
  const spent = new Set();
  const said = new Set();
  let events = null;
  let watcher = null;
  let timer = null;
  let stopped = false;

  const once = (name, what, text) => {
    const key = `${name}\u0000${what}`;
    if (said.has(key)) return;
    said.add(key);
    say(text);
  };
  const pidOf = (name) => name.slice(0, -'.json'.length);

  function begin(told) {
    if (!told || typeof told.run !== 'string') {
      say('the driver told no events: no subscriber is connected');
      return;
    }

    events = { run: told.run, seq: Number.isInteger(told.seq) ? told.seq : 0, grammar: told.grammar, build: told.build };
    if (events.run === SAMPLE) {
      say('a sample run: no subscriber is connected');
      return;
    }

    mkdirSync(folder, { recursive: true });
    // The watch is set before the folder is read, so a file that appears between the two is seen by one or both.
    if (!setWatch()) return exit(1);
    reconcile(true);
    timer = setInterval(() => reconcile(true), reconcileMs);
  }

  function setWatch() {
    try {
      watcher = watch(folder, (type, name) => {
        if (name) look(String(name));
      });
      watcher.on('error', () => {
        closeWatch();
        if (stopped) return;
        if (!setWatch()) return exit(1);
        reconcile(false);
      });
      return true;
    } catch {
      return false;
    }
  }

  function closeWatch() {
    try {
      watcher?.close();
    } catch {
      // Already closed.
    }

    watcher = null;
  }

  // What the watch saw: a file removed closes its connection; any other is considered.
  function look(name) {
    if (stopped || !events) return;
    if (!existsSync(join(folder, name))) {
      forget(name);
      return;
    }

    consider(name, false);
  }

  // Every file read again: on the clock, a periodic one tries the files whose pipe did not answer; one run because a slot freed does not.
  function reconcile(periodic) {
    if (stopped || !events) return;
    let names;
    try {
      names = readdirSync(folder);
    } catch {
      return;
    }

    const present = new Set(names);
    for (const name of [...connections.keys(), ...spent, ...unanswered]) if (!present.has(name)) forget(name);
    if (periodic) unanswered.clear();
    for (const name of names) consider(name, periodic);
  }

  function forget(name) {
    spent.delete(name);
    unanswered.delete(name);
    connections.get(name)?.socket.destroy();
  }

  function consider(name, periodic) {
    if (connections.has(name) || spent.has(name) || name.startsWith('.')) return;
    if (!FILE_NAME.test(name)) return once(name, 'shape', 'left a file that names no subscriber');
    if (unanswered.has(name) && !periodic) return;
    const subscriber = read(name);
    if (!subscriber) return once(name, 'shape', `left subscriber ${pidOf(name)}'s file: it is not of the relay's shape`);
    if (connections.size >= SUBSCRIBERS) {
      return once(name, 'place', `subscriber ${subscriber.pid} waits for a free place: ${SUBSCRIBERS} are connected`);
    }

    open(name, subscriber);
  }

  function read(name) {
    try {
      const file = join(folder, name);
      if (statSync(file).size > FILE_BYTES) return null;
      const { pipe, pid } = JSON.parse(readFileSync(file, 'utf8'));
      const address = addressOf(pipe, folder, platform);
      return Number.isInteger(pid) && pid > 0 && address ? { pid, address } : null;
    } catch {
      return null;
    }
  }

  function open(name, { pid, address }) {
    const connection = { pid, queue: [], writing: false, ready: false, slow: false, timer: null, socket: connect(address) };
    connections.set(name, connection);
    connection.socket.on('connect', () => {
      if (stopped) return;
      connection.ready = true;
      spent.add(name);
      say(`connected subscriber ${pid}`);
      connection.queue.push(JSON.stringify({ plugin: PLUGIN, run: events.run, seq: events.seq, grammar: events.grammar, build: events.build }));
      pump(connection);
    });
    // The relay sends nothing; what it might is read and left.
    connection.socket.on('data', () => {});
    connection.socket.on('error', () => {});
    connection.socket.on('close', () => {
      clearTimeout(connection.timer);
      if (connections.get(name) === connection) connections.delete(name);
      if (stopped) return;
      if (!connection.ready) {
        unanswered.add(name);
        once(name, 'unanswered', `subscriber ${pid} did not answer; it is tried again at the next reconciliation`);
      } else if (!connection.slow) {
        say(`subscriber ${pid} left`);
      }

      reconcile(false);
    });
  }

  // One writer per connection, in order: a write not done within the bound marks the subscriber slow and closes it.
  function pump(connection) {
    if (connection.writing || !connection.ready || connection.queue.length === 0) return;
    const line = connection.queue.shift();
    connection.writing = true;
    connection.timer = setTimeout(() => {
      connection.slow = true;
      say(`subscriber ${connection.pid} was slow: its connection is closed`);
      connection.socket.destroy();
    }, writeMs);
    connection.socket.write(`${line}\n`, (error) => {
      clearTimeout(connection.timer);
      connection.writing = false;
      if (!error) pump(connection);
    });
  }

  function tell(frame) {
    if (!events || events.run === SAMPLE) return;
    if (Number.isInteger(frame?.seq)) events.seq = frame.seq;
    const line = JSON.stringify(frame);
    for (const connection of connections.values()) {
      if (!connection.ready || connection.slow) continue;
      connection.queue.push(line);
      if (connection.queue.length > QUEUE) connection.queue.shift();
      pump(connection);
    }
  }

  function stop() {
    stopped = true;
    clearInterval(timer);
    closeWatch();
    for (const connection of connections.values()) connection.socket.destroy();
    connections.clear();
  }

  return { begin, tell, stop, connected: () => [...connections.values()].filter((c) => c.ready).map((c) => c.pid) };
}

/** The hook wire, JSON-RPC 2.0 one frame a line on stdin and stdout; stderr is the plugin's console. */
export async function main({ input = process.stdin, output = process.stdout, env = process.env } = {}) {
  const send = (frame) => output.write(`${JSON.stringify(frame)}\n`);
  // A test hands a short interval or bound; the driver hands none.
  const plugin = relay({
    data: env.DAORIS_PLUGIN_DATA,
    reconcileMs: Number(env.EVENT_RELAY_RECONCILE_MS) || RECONCILE_MS,
    writeMs: Number(env.EVENT_RELAY_WRITE_MS) || WRITE_MS,
  });
  for await (const line of createInterface({ input })) {
    if (!line.trim()) continue;
    let frame;
    try {
      frame = JSON.parse(line);
    } catch {
      continue;
    }

    switch (frame.method) {
      case 'initialize':
        plugin.begin(frame.params?.events);
        send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params?.points ?? [] } });
        break;
      case 'hook/work/event':
        plugin.tell(frame.params);
        if (frame.id !== undefined) send({ jsonrpc: '2.0', id: frame.id, result: {} });
        break;
      case 'shutdown':
        plugin.stop();
        process.exit(0);
        break;
      default:
        if (frame.id !== undefined) send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
    }
  }

  // The driver is gone: nothing more will be told.
  plugin.stop();
}

const invoked = process.argv[1] ? resolve(process.argv[1]) : '';
const self = fileURLToPath(import.meta.url);
if (process.platform === 'win32' ? invoked.toLowerCase() === self.toLowerCase() : invoked === self) await main();

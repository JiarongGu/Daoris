// A stand-in for the event-relay plugin, the Process tests' own: it speaks the hook wire (protocol 1) and the relay's pipe (wire protocol 1),
// no more. The real plugin is Daoris.Plugins'; this keeps the relay's side of the contract proven here without it.
// Per subscriber file in <data>/subscribers it connects to the named pipe, writes the first line the relay checks, then each
// `hook/work/event` frame unchanged, and says `connected subscriber <pid>` on stderr as the real plugin does.
import { existsSync, mkdirSync, readFileSync, readdirSync } from 'node:fs';
import { connect } from 'node:net';
import { join } from 'node:path';
import { createInterface } from 'node:readline';

const folder = join(process.env.DAORIS_PLUGIN_DATA, 'subscribers');
const connections = new Map();
let events = null;

function open(name) {
  if (connections.has(name)) return;
  let file;
  try {
    file = JSON.parse(readFileSync(join(folder, name), 'utf8'));
  } catch {
    return;
  }

  if (!/^daoris-event-[0-9a-f]{32}$/.test(file.pipe ?? '')) return;
  const socket = connect(`\\\\.\\pipe\\${file.pipe}`);
  connections.set(name, socket);
  socket.on('error', () => {});
  socket.on('data', () => {});
  socket.on('connect', () => {
    console.error(`connected subscriber ${file.pid}`);
    socket.write(`${JSON.stringify({ plugin: 'event-relay', run: events.run, seq: events.seq, grammar: events.grammar, build: events.build })}\n`);
  });
}

function look() {
  if (events && existsSync(folder)) for (const name of readdirSync(folder)) if (/^[0-9]+\.json$/.test(name)) open(name);
}

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
for await (const line of createInterface({ input: process.stdin })) {
  const frame = JSON.parse(line);
  switch (frame.method) {
    case 'initialize':
      events = frame.params?.events;
      if (events && events.run !== 'sample') {
        mkdirSync(folder, { recursive: true });
        setInterval(look, 100);
        look();
      }

      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params?.points ?? [] } });
      break;
    case 'hook/work/event':
      for (const socket of connections.values()) socket.write(`${JSON.stringify(frame.params)}\n`);
      if (frame.id !== undefined) send({ jsonrpc: '2.0', id: frame.id, result: {} });
      break;
    case 'shutdown':
      for (const socket of connections.values()) socket.destroy();
      process.exit(0);
  }
}

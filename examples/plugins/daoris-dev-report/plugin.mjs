// Development report (`daoris-dev-report`): a place for Daoris's sessions to report to the person's own development agent. It answers
// `command/run` for its two commands (reports.mjs), over JSON-RPC 2.0, one frame per line, on stdin and stdout. A manifest that
// declares interfaces must list a point, so it listens on the observation `session/ended` and does nothing with it.
//  DAORIS_PLUGIN_PRIVATE this plugin's own storage, where the reports are kept
import { createInterface } from 'node:readline';
import { run } from './reports.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);
const POINTS = ['session/ended'];

for await (const line of createInterface({ input: process.stdin })) {
  if (!line.trim()) continue;
  let frame;
  try {
    frame = JSON.parse(line);
  } catch {
    say(`not a frame: ${line}`);
    continue;
  }

  if (frame.method === 'initialize') {
    const declared = frame.params?.points ?? [];
    send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: POINTS.filter((point) => declared.includes(point)), capabilities: [] } });
  } else if (frame.method === 'shutdown') {
    process.exit(0);
  } else if (frame.method === 'hook/session/ended') {
    send({ jsonrpc: '2.0', id: frame.id, result: {} });
  } else if (frame.method === 'hook/command/run') {
    try {
      send({ jsonrpc: '2.0', id: frame.id, result: run(frame.params ?? {}) });
    } catch (error) {
      send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: String(error?.message ?? error) } });
    }
  } else if (frame.id !== undefined) {
    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
  }
}

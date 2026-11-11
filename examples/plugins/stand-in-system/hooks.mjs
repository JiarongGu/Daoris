// The stand-in system's adapter on the hook wire: JSON-RPC 2.0, one frame per line on stdin and stdout, stderr its console.
// It answers the two system points a plugin that declares `systems` speaks:
//  plan/read      the intended changes read against the system as it stands, answered as typed operations with their preconditions
//  apply/perform  a reviewed change set applied under them: `applied`, `unchanged`, or `stale` with each refused resource's present version
// Its store is DAORIS_PLUGIN_DATA's state.json (system.mjs), declared twice in plugin.json: guarded by its etags, and by `none`.
import { mkdirSync, readFileSync } from 'node:fs';
import { createInterface } from 'node:readline';
import { apply, keep, plan, read } from './system.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const data = process.env.DAORIS_PLUGIN_DATA;
mkdirSync(data, { recursive: true });
const declared = JSON.parse(readFileSync(new URL('./plugin.json', import.meta.url), 'utf8')).systems;
const guardOf = (system) => declared.find((each) => each.id === system)?.preconditions ?? 'etag';

for await (const line of createInterface({ input: process.stdin })) {
  if (!line.trim()) continue;
  const frame = JSON.parse(line);

  switch (frame.method) {
    case 'initialize':
      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } });
      break;

    case 'hook/plan/read':
      send({ jsonrpc: '2.0', id: frame.id, result: plan(read(data), frame.params.changes ?? [], guardOf(frame.params.system)) });
      break;

    case 'hook/apply/perform': {
      const { state, answer } = apply(read(data), frame.params.operations ?? [], { guarded: guardOf(frame.params.system) !== 'none' });
      if (answer.state === 'applied') keep(data, state);
      send({ jsonrpc: '2.0', id: frame.id, result: answer });
      break;
    }

    case 'shutdown':
      process.exit(0);
      break;

    default:
      if (frame.id !== undefined) send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no method ${frame.method}` } });
  }
}

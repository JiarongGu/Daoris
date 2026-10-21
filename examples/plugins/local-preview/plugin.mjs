// Local preview (`local-preview`): a Daoris plugin that speaks.
//
// It is a process of its own, started by the driver, answering over JSON-RPC 2.0 on this process's
// stdin and stdout, one frame per line. README.md says what each point is sent and must answer;
// `node --test` in this folder checks this file against it.
//
// Three rules the driver holds a plugin to:
//   1. stdout is the wire. Nothing but frames goes there; say anything else on stderr (console.error),
//      which the driver shows under this plugin's name.
//   2. What it keeps goes in DAORIS_PLUGIN_DATA, never beside this file: the install folder is replaced
//      whole by the next `daoris plugin add`.
//   3. Every request is answered, and promptly. A decision that comes late or wrong holds the quest; a
//      landing has two minutes.
//
// Where it is and what it keeps are told in the environment, never guessed:
//   DAORIS_PLUGIN_ID      this plugin's id, as plugin.json declares it
//   DAORIS_PLUGIN_FOLDER  the install folder
//   DAORIS_PLUGIN_DATA    the data folder beside it, which an update never touches
//   DAORIS_HOME           the Daoris home the driver runs against
import { createInterface } from 'node:readline';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { work } from './work.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

const id = process.env.DAORIS_PLUGIN_ID;
const data = process.env.DAORIS_PLUGIN_DATA;

// The work points (README.md, "Working"): the kit's work.mjs keeps the journal, answers each point, and sends each outcome at
// `work/step`. These are the external calls it makes, the only part to write. Under the kit's stand-in world, which its tests
// and `daoris-driver plugins try` set, none of them is called.
const kit = work({
  data,
  send,
  say,
  // The work, done once for an attempt: call.unit, call.step, call.invocation, call.idempotencyKey, call.subjectRevision,
  // call.inputs, call.deadlineAt. Say the receipt it reached on and what it made, which Daoris has removed at the end:
  //   return { state: 'reached', evidence: { ref: `local-preview/receipt@1:${call.invocation}`, url, message },
  //            resources: [{ schema: 'local-preview/resource@1', id: '<the platform's id for it>' }] };
  // A failure names its code; only the codes plugin.json lists as `transient` are tried again:
  //   return { state: 'failed', code: 'unavailable', evidence: { message: '…' } };
  //
  // This example's platform is its own data folder: the work is a preview of the attempt's revision, written to
  // `previews/<invocation>/`, its last file `done`, so a preview without `done` is one a process began and never finished.
  act: async (call) => {
    const folder = preview(call.invocation);
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, 'revision.txt'), `${call.subjectRevision}\n`);
    writeFileSync(join(folder, 'inputs.json'), `${JSON.stringify(call.inputs ?? [])}\n`);
    writeFileSync(join(folder, 'done'), `${new Date().toISOString()}\n`);
    return reached(call.invocation);
  },
  // A preview with `done` was made; one without was begun and is cleared, so the redelivery makes it whole; none was never begun.
  check: async (attempt) => {
    const folder = preview(attempt.invocation);
    if (existsSync(join(folder, 'done'))) return { state: 'done', outcome: reached(attempt.invocation) };
    rmSync(folder, { recursive: true, force: true });
    return { state: 'not-started' };
  },
  // The preview removed; one already gone says so, which is not a failure.
  remove: async (resource) => {
    const folder = preview(resource.resource);
    if (!existsSync(folder)) return { disposition: 'already-gone' };
    rmSync(folder, { recursive: true, force: true });
    return { disposition: 'removed', proof: `previews/${resource.resource} removed` };
  },
});

/** A preview's folder, by the invocation that made it: only a name of its own, never a path out of the data folder. */
function preview(name) {
  if (typeof name !== 'string' || !/^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$/.test(name) || name.includes('..')) {
    throw new Error(`\`${name}\` is not a preview's name`);
  }
  return join(data, 'previews', name);
}

/** A made preview's outcome: its receipt, and the preview itself as the temporary thing it made. */
function reached(invocation) {
  return {
    state: 'reached',
    evidence: { ref: `local-preview/receipt@1:${invocation}`, message: `previewed in previews/${invocation}` },
    resources: [{ schema: 'local-preview/resource@1', id: invocation }],
  };
}

// The points this plugin listens on, each answering the frame it is sent. Change what they do; keep
// the shape of what they answer (README.md). A point here that plugin.json does not declare is never
// asked: the handshake answers only the points the manifest names.
const points = {
  // The awaited set: which units await this plugin's steps. The kit's work.mjs answers it, and sends again what it knows.
  'work/watch': kit.points['work/watch'],

  // Not asked: the plugin's own event, which the kit's work.mjs sends for it. Listed so the handshake says it reports there.
  'work/step': null,

  // One attempt, written down and acknowledged by the kit's work.mjs, which then calls act() above once for its key.
  'work/perform': kit.points['work/perform'],
};

// What it answers: its points, and the requests asked beside them that no manifest names.
const answers = { ...points, ...kit.calls };

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
    // The handshake: the wire this plugin speaks, and the points it ACTUALLY listens on, which are never
    // more than the manifest declared. The driver sends those as `points`.
    const declared = frame.params?.points ?? [];
    send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: Object.keys(points).filter((point) => declared.includes(point)), capabilities: kit.capabilities } });
  } else if (frame.method === 'shutdown') {
    // The driver is stopping, or this plugin was switched off. Leave; the driver ends a process that stays.
    process.exit(0);
  } else if (typeof frame.method === 'string' && frame.method.startsWith('hook/') && answers[frame.method.slice(5)]) {
    try {
      send({ jsonrpc: '2.0', id: frame.id, result: await answers[frame.method.slice(5)](frame.params ?? {}) });
    } catch (error) {
      // A handler that throws is a refusal, which the driver says in this plugin's name. Never silence.
      send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: String(error?.message ?? error) } });
    }
  } else if (frame.id !== undefined) {
    // A request this plugin does not know is answered in the protocol's own words rather than left
    // hanging: an unanswered request is a driver waiting on its timeout.
    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
  }
}

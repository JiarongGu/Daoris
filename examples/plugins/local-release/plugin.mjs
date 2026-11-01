// Local release (`local-release`): a Daoris plugin that speaks.
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
//   3. Every request is answered, and promptly.
//
// Where it is and what it keeps are told in the environment, never guessed:
//   DAORIS_PLUGIN_ID      this plugin's id, as plugin.json declares it
//   DAORIS_PLUGIN_FOLDER  the install folder
//   DAORIS_PLUGIN_DATA    the data folder beside it, which an update never touches
//   DAORIS_HOME           the Daoris home the driver runs against
import { createInterface } from 'node:readline';
import { cpSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { work } from './work.mjs';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

const data = process.env.DAORIS_PLUGIN_DATA;
const RELEASE = 'local-release/release@1';
const RESOURCE = 'local-release/resource@1';

// This example's platform is its own data folder, so the whole tail runs on any machine and reaches nothing:
//   pending/<revision>.json   a release waiting at a revision: { "version", "notes" }, put there by whoever prepares it
//   found/<version>.json      what `find` recorded, which its output refers to
//   releases/<version>.json   what `release` recorded: { "tag", "notes", "revision" }
//   incoming/<version>/       the built package, put there by the build
//   registry/<version>/       the package `publish` copied
//   staging/<invocation>/     a step's work in progress, its last file `done`: the one temporary thing, removed by work/cleanup
const steps = {
  // Looks at the attempt's revision for a pending release and decides: `release` with its output, or `none` with nothing; what to do with
  // no release is the graph's.
  find: async (call) => {
    const pending = readJson(join(data, 'pending', `${name(call.subjectRevision)}.json`));
    if (!pending) return stage(call, `no release is pending at ${call.subjectRevision}`, null, () => {}, 'none');
    const version = name(pending.version);
    const found = { version, notes: String(pending.notes ?? ''), revision: call.subjectRevision };
    return stage(call, `found ${version}`, version, () => writeJson(join(data, 'found', `${version}.json`), found), 'release');
  },

  // Records the release it was given: its tag name and notes, kept in the data folder. Pushes no tag.
  release: async (call) => {
    const version = versionOf(call);
    const found = readJson(join(data, 'found', `${version}.json`));
    if (!found) return failed('unknown-release', `${version} was never found`);
    return stage(call, `released v${version}`, version, () => {
      writeJson(join(data, 'releases', `${version}.json`), { tag: `v${version}`, notes: found.notes, revision: found.revision });
    });
  },

  // Copies the built package into the registry folder, once per version. The copy is whole or not there.
  publish: async (call) => {
    const version = versionOf(call);
    if (!existsSync(join(data, 'releases', `${version}.json`))) return failed('not-released', `${version} is not released`);
    const built = join(data, 'incoming', version);
    if (!existsSync(built)) return failed('no-package', `no built package for ${version}`);
    return stage(call, `published ${version}`, null, () => {
      const target = join(data, 'registry', version);
      if (existsSync(target)) return;
      const partial = `${target}.${name(call.invocation)}.partial`;
      rmSync(partial, { recursive: true, force: true });
      mkdirSync(join(data, 'registry'), { recursive: true });
      cpSync(built, partial, { recursive: true });
      renameSync(partial, target);
    });
  },
};

const kit = work({
  data,
  send,
  say,
  act: async (call) => {
    const run = steps[String(call.step).split('/').pop()];
    if (!run) return failed('unknown-step', `no step ${call.step}`);
    return run(call);
  },
  // A step with `done` was made, and says what it reached; one without was begun and is cleared, so the redelivery makes it whole.
  check: async (attempt) => {
    const folder = staging(attempt.invocation);
    const outcome = readJson(join(folder, 'outcome.json'));
    if (outcome && existsSync(join(folder, 'done'))) return { state: 'done', outcome };
    rmSync(folder, { recursive: true, force: true });
    return { state: 'not-started' };
  },
  // The staging folder removed; one already gone says so, which is not a failure.
  remove: async (resource) => {
    const folder = staging(resource.resource);
    if (!existsSync(folder)) return { disposition: 'already-gone' };
    rmSync(folder, { recursive: true, force: true });
    return { disposition: 'removed', proof: `staging/${resource.resource} removed` };
  },
});

/** Runs a step's effect inside its staging folder: the outcome is written first, `done` last, so a cut-off step is told from a finished one. */
function stage(call, message, version, effect, decision) {
  const folder = staging(call.invocation);
  mkdirSync(folder, { recursive: true });
  effect();
  const outcome = {
    state: 'reached',
    evidence: { ref: `local-release/receipt@1:${call.invocation}`, message },
    ...(version ? { outputs: { release: { ref: `${RELEASE}:${version}` } } } : {}),
    resources: [{ schema: RESOURCE, id: call.invocation }],
    ...(decision ? { outcome: decision } : {}),
  };
  writeJson(join(folder, 'outcome.json'), outcome);
  writeFileSync(join(folder, 'done'), `${new Date().toISOString()}\n`);
  return outcome;
}

function failed(code, message) {
  return { state: 'failed', code, evidence: { message } };
}

/** The version a step was given: the id of its `local-release/release@1` input. */
function versionOf(call) {
  const prefix = `${RELEASE}:`;
  const given = (call.inputs ?? []).find((input) => typeof input === 'string' && input.startsWith(prefix));
  return name(given?.slice(prefix.length));
}

/** A name of its own, never a path out of the data folder. */
function name(value) {
  if (typeof value !== 'string' || !/^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$/.test(value) || value.includes('..')) {
    throw new Error(`\`${value}\` is not a name this plugin keeps`);
  }
  return value;
}

const staging = (invocation) => join(data, 'staging', name(invocation));

function readJson(file) {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    if (error.code === 'ENOENT') return null;
    throw error;
  }
}

function writeJson(file, value) {
  mkdirSync(join(file, '..'), { recursive: true });
  const written = `${file}.${process.pid}.tmp`;
  writeFileSync(written, `${JSON.stringify(value, null, 2)}\n`);
  renameSync(written, file);
}

// The points this plugin listens on, each answering the frame it is sent. A point here that plugin.json
// does not declare is never asked: the handshake answers only the points the manifest names.
const points = {
  // The awaited set: which units await this plugin's steps. The kit's work.mjs answers it.
  'work/watch': kit.points['work/watch'],

  // Not asked: the plugin's own event, which the kit's work.mjs sends for it.
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
    const declared = frame.params?.points ?? [];
    send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: Object.keys(points).filter((point) => declared.includes(point)), capabilities: kit.capabilities } });
  } else if (frame.method === 'shutdown') {
    process.exit(0);
  } else if (typeof frame.method === 'string' && frame.method.startsWith('hook/') && answers[frame.method.slice(5)]) {
    try {
      send({ jsonrpc: '2.0', id: frame.id, result: await answers[frame.method.slice(5)](frame.params ?? {}) });
    } catch (error) {
      // A handler that throws is a refusal, which the driver says in this plugin's name. Never silence.
      send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: String(error?.message ?? error) } });
    }
  } else if (frame.id !== undefined) {
    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
  }
}

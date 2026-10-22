// The wire test Daoris's plugin kit writes beside a plugin. It starts the plugin the way the driver
// does, speaks the handshake, one frame at every point plugin.json declares and the shutdown, and
// checks each answer by the driver's own rules. `node --test` runs it with nothing of Daoris's
// installed: it imports nothing but Node.
//
// The frames are the driver's own, written into this file when the kit made it. `daoris-driver plugins
// try` speaks the same ones through the driver itself, where Daoris is on the machine. `root` in a frame
// is an empty scratch folder here, where git is told there is no repository, so a plugin that pushes
// first fails on this machine and reaches nothing. To test past the push, write a test of your own that
// puts a fake of the platform's tool first on the PATH and a bare repository behind `origin`. A plugin that
// works is told the kit's stand-in world (DAORIS_PLUGIN_WORLD), and is sent no attempt unless it takes it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

const folder = dirname(fileURLToPath(import.meta.url));
const manifest = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8'));

/** The hook wire this file was written for. */
const PROTOCOL = 1;

/** How long the driver waits for a plugin to leave once it is told `shutdown`, in milliseconds. */
const GRACE = 2000;

/** The capability a performer's handshake names, without which the driver never asks it to perform. */
const CAPABILITY = 'work.perform@1';

/** A sample frame's step, which is this plugin's own: its first declared step. */
const STEP = '{step}';

/** Every point: what kind it is, how long the driver waits for its answer in milliseconds, and its frame. */
const POINTS = {
  "quest/consider": {
    "kind": "decision",
    "patience": 10000,
    "frame": {
      "quest": {
        "id": "0fda18",
        "title": "Expose a streaming budget",
        "from": "game",
        "to": "engine"
      },
      "repository": "engine",
      "workspace": "default",
      "root": "{root}"
    }
  },
  "session/ended": {
    "kind": "observation",
    "patience": 10000,
    "frame": {
      "session": "s1a2b3c4",
      "quest": "0fda18",
      "repository": "engine",
      "state": "completed",
      "adapter": "claude-code",
      "account": "work",
      "byPerson": false,
      "note": null
    }
  },
  "work/land": {
    "kind": "act",
    "patience": 120000,
    "frame": {
      "repository": "engine",
      "workspace": "default",
      "root": "{root}",
      "branch": "feature/0fda18-expose-a-streaming-budget",
      "base": "main",
      "title": "Expose a streaming budget",
      "quest": {
        "id": "0fda18",
        "title": "Expose a streaming budget"
      },
      "session": "s1a2b3c4",
      "commits": [
        {
          "sha": "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a",
          "subject": "Expose a streaming budget"
        }
      ],
      "pullRequest": null,
      "acceptedBy": "person",
      "tip": "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a",
      "origin": null
    }
  },
  "work/state": {
    "kind": "query",
    "patience": 30000,
    "frame": {
      "repository": "engine",
      "workspace": "default",
      "root": "{root}",
      "branch": "feature/0fda18-expose-a-streaming-budget",
      "line": "main",
      "pullRequest": "https://example.test/example-org/engine/pull/7",
      "pushedTip": "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a"
    }
  },
  "state/changed": {
    "kind": "observation",
    "patience": 10000,
    "frame": {
      "scopes": [
        "sessions",
        "landing",
        "diff",
        "handOff",
        "sweep",
        "treesSync"
      ],
      "sessions": [
        "s1a2b3c4"
      ]
    }
  },
  "work/watch": {
    "kind": "observation",
    "patience": 10000,
    "frame": {
      "generation": 1,
      "resync": true,
      "units": [
        {
          "repository": "engine",
          "workspace": "default",
          "root": "{root}",
          "branch": "feature/0fda18-expose-a-streaming-budget",
          "tip": "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a",
          "line": "main",
          "pullRequest": null,
          "awaiting": [
            "{step}"
          ]
        }
      ]
    }
  },
  "work/step": {
    "kind": "event",
    "patience": 10000,
    "frame": {
      "step": "{step}",
      "invocation": "i-0fda18a2b3c4",
      "state": "reached",
      "evidence": {
        "ref": "{plugin-id}/receipt@1:i-0fda18a2b3c4",
        "message": "deployed"
      },
      "resources": [
        {
          "schema": "{plugin-id}/resource@1",
          "id": "r-i-0fda18a2b3c4"
        }
      ],
      "at": "2026-10-21T09:02:00.0000000+00:00",
      "id": "i-0fda18a2b3c4"
    }
  },
  "work/perform": {
    "kind": "act",
    "patience": 10000,
    "frame": {
      "unit": "u-0fda18a2",
      "step": "{step}",
      "stepVersion": 1,
      "invocation": "i-0fda18a2b3c4",
      "idempotencyKey": "k-3f2a9c1e5b7d4a6f",
      "subjectRevision": "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a",
      "inputs": [],
      "deadlineAt": "2026-10-21T09:10:00.0000000+00:00"
    }
  }
};

/** Each point's answer, read by the driver's rules: null when it is one, else what is wrong with it. An event is never answered. */
const ANSWERS = {
  'quest/consider': (answer) => {
    const kind = answer !== null && typeof answer === 'object' && !Array.isArray(answer) ? answer.kind : undefined;
    return kind === 'allow' || kind === 'hold'
      ? null
      : `${JSON.stringify(answer)}, which is not a decision — { "kind": "allow" } or { "kind": "hold", "reason": "…" }`;
  },
  // An observation's answer is not read: any result will do, and {} is the one to send.
  'session/ended': () => null,
  'work/land': (answer) => {
    const wrong = `${JSON.stringify(answer)}, which is not a landing's answer — `
      + '{ "pushed": true, "pullRequest": "https://…", "message": "…" }, the last two optional';
    if (answer === null || typeof answer !== 'object' || Array.isArray(answer) || typeof answer.pushed !== 'boolean') return wrong;
    if (answer.pullRequest !== undefined && answer.pullRequest !== null) {
      if (typeof answer.pullRequest !== 'string') return wrong;
      let address;
      try {
        address = new URL(answer.pullRequest);
      } catch {
        return wrong;
      }
      if (address.protocol !== 'https:' && address.protocol !== 'http:') return wrong;
    }
    if (answer.message !== undefined && answer.message !== null && typeof answer.message !== 'string') return wrong;
    return null;
  },
  'work/state': (answer) => {
    const wrong = `${JSON.stringify(answer)}, which is not a pull request's state — `
      + '{ "state": "open" | "completed" | "abandoned" | "unknown", … }, a completed one naming its mergeCommit and sourceCommit in full';
    if (answer === null || typeof answer !== 'object' || Array.isArray(answer)) return wrong;
    if (!['open', 'completed', 'abandoned', 'unknown'].includes(answer.state)) return wrong;
    // Every other field is text or absent; null is absent.
    const fields = ['pullRequest', 'mergeCommit', 'sourceCommit', 'target', 'how', 'at', 'message'];
    if (fields.some((name) => answer[name] !== undefined && answer[name] !== null && typeof answer[name] !== 'string')) return wrong;
    const given = (name) => typeof answer[name] === 'string';
    if (given('pullRequest')) {
      let address;
      try {
        address = new URL(answer.pullRequest);
      } catch {
        return wrong;
      }
      if (address.protocol !== 'https:' && address.protocol !== 'http:') return wrong;
    }
    const full = /^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$/;
    if (['mergeCommit', 'sourceCommit'].some((name) => given(name) && !full.test(answer[name]))) return wrong;
    if (answer.state === 'completed' && !(given('mergeCommit') && given('sourceCommit'))) return wrong;
    if (given('target') && answer.target.length === 0) return wrong;
    if (given('at') && Number.isNaN(Date.parse(answer.at))) return wrong;
    // A word for how it merged that the driver has no word for is read as none, never refused.
    return null;
  },
  // An observation's answer is not read (STATEHUB1g): any result will do, and {} is the one to send.
  'state/changed': () => null,
  // The awaited set's answer is not read either (FABRIC-PERFORM1e).
  'work/watch': () => null,
  // An attempt is acknowledged by its invocation, and nothing more: the outcome comes later at `work/step`.
  'work/perform': (answer, frame) => (answer !== null && typeof answer === 'object' && !Array.isArray(answer) && answer.invocation === frame.invocation
    ? null
    : `${JSON.stringify(answer)}, which does not acknowledge \`${frame.invocation}\` — { "invocation": "${frame.invocation}" }`),
};

/** A sample frame made this plugin's: its repository the scratch root, its step the plugin's own. */
function own(frame, root) {
  if (Object.hasOwn(frame, 'root')) frame.root = root;
  const steps = Array.isArray(manifest.steps) ? manifest.steps.filter((step) => typeof step?.id === 'string') : [];
  if (steps.length === 0) return frame;
  if (frame.step === STEP) frame.step = `${manifest.id}/${(steps.find((step) => step.reach === 'performed') ?? steps[0]).id}`;
  for (const unit of frame.units ?? []) {
    unit.root = root;
    if (unit.awaiting?.includes(STEP)) unit.awaiting = steps.map((step) => step.id);
  }
  return frame;
}

/** The handshake's answer, read by the driver's rules and the kit's one more: every declared point is listened on. */
function handshake(answer, declared) {
  if (answer === null || typeof answer !== 'object' || Array.isArray(answer)) {
    return `it answered the handshake with ${JSON.stringify(answer)}, which is not one — { "protocolVersion": ${PROTOCOL}, "points": [ … ] }`;
  }
  if (answer.protocolVersion !== PROTOCOL) {
    return `it speaks hook wire ${JSON.stringify(answer.protocolVersion ?? null)}; this was written for ${PROTOCOL}`;
  }
  const listened = Array.isArray(answer.points) ? answer.points.filter((point) => typeof point === 'string') : [];
  for (const point of listened) {
    if (!declared.includes(point)) {
      return `it listens on \`${point}\`, which plugin.json does not declare — a manifest is what a person reads before turning a plugin on, so the process may not exceed it`;
    }
    if (!Object.hasOwn(POINTS, point)) return `it listens on \`${point}\`, which is not a point (${Object.keys(POINTS).join(', ')})`;
  }
  const unkept = declared.filter((point) => !listened.includes(point));
  if (unkept.length > 0) {
    return `plugin.json declares ${unkept.map((point) => `\`${point}\``).join(', ')} but the process listens on `
      + `${listened.length > 0 ? listened.join(', ') : 'nothing'} — a manifest is what a person reads, so the process keeps its word`;
  }
  if (declared.includes('work/perform') && !(Array.isArray(answer.capabilities) && answer.capabilities.includes(CAPABILITY))) {
    return `it declares \`work/perform\` but its handshake names no \`${CAPABILITY}\` among its capabilities, so the driver never asks it to perform`;
  }
  return null;
}

test('plugin.json names a hook command and the points it speaks on', () => {
  assert.equal(typeof manifest.id, 'string', 'plugin.json needs an `id`');
  const command = manifest.hooks?.command;
  assert.ok(Array.isArray(command) && command.length > 0 && command.every((part) => typeof part === 'string'),
    'plugin.json needs `hooks.command`: the program and its arguments');
  const points = manifest.hooks.points;
  assert.ok(Array.isArray(points) && points.length > 0, 'plugin.json needs `hooks.points`: where the plugin speaks');
  for (const point of points) {
    assert.ok(Object.hasOwn(POINTS, point), `\`${point}\` is not a point — one of ${Object.keys(POINTS).join(', ')}`);
  }
});

test('it answers the handshake, one frame at every point it declares, and leaves when told', async (t) => {
  const declared = Array.isArray(manifest.hooks?.points) ? manifest.hooks.points : [];
  const scratch = mkdtempSync(join(tmpdir(), 'daoris-plugin-test-'));
  const data = join(scratch, 'data');
  const root = join(scratch, 'root');
  mkdirSync(data);
  mkdirSync(root);
  // The kit's stand-in world: a plugin that works answers from it in its platform's place, so an attempt reaches nothing.
  const world = join(scratch, 'world.json');
  writeFileSync(world, '{}\n');

  // `${plugin}` is this folder and `${data}` the scratch data folder; an argument holding one is a path.
  const expand = (part) => (part.includes('${plugin}') || part.includes('${data}')
    ? resolve(part.split('${plugin}').join(folder).split('${data}').join(data))
    : part);
  const [command, ...args] = (manifest.hooks?.command ?? []).map(expand);
  const child = spawn(command, args, {
    cwd: folder,
    env: {
      ...process.env,
      DAORIS_PLUGIN_ID: manifest.id,
      DAORIS_PLUGIN_FOLDER: folder,
      DAORIS_PLUGIN_DATA: data,
      DAORIS_HOME: scratch,
      DAORIS_PLUGIN_WORLD: world,
      // The frames' repository is none: git there fails rather than walking up to one.
      GIT_DIR: join(root, '.git'),
      GIT_TERMINAL_PROMPT: '0',
    },
  });

  let ended = null;
  const gone = new Promise((resolveGone) => {
    child.on('close', (code, signal) => { ended ??= { code, signal }; resolveGone(); });
    child.on('error', (error) => { ended ??= { error }; resolveGone(); });
  });
  // A write to a process that has gone is not the failure; what it did before going is.
  child.stdin.on('error', () => {});
  t.after(async () => {
    if (ended === null) child.kill();
    await gone;
    rmSync(scratch, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
  });

  const said = [];
  createInterface({ input: child.stderr }).on('line', (line) => { if (line.trim()) said.push(line); });

  const noise = [];
  const waiting = new Map();
  createInterface({ input: child.stdout }).on('line', (line) => {
    if (!line.trim()) return;
    let frame;
    try {
      frame = JSON.parse(line);
    } catch {
      noise.push(line);
      return;
    }
    if (frame === null || typeof frame !== 'object' || Array.isArray(frame) || (!('id' in frame) && !('method' in frame))) {
      noise.push(line);
      return;
    }
    // A request from a plugin is not a thing this wire has; the driver neither answers nor errors it.
    if ('method' in frame) return;
    const answered = waiting.get(frame.id);
    waiting.delete(frame.id);
    answered?.(frame);
  });

  const lastWords = () => (said.length > 0 ? ` — the last thing it said: \`${said.at(-1)}\`` : '');
  const why = (method) => (ended?.error
    ? `\`${command}\` could not start: ${ended.error.message}`
    : `its process exited (code ${ended?.code ?? ended?.signal}) before answering \`${method}\`${lastWords()}`);
  let next = 0;
  const ask = (method, params, patience) => new Promise((resolveAnswer, reject) => {
    const id = ++next;
    const timer = setTimeout(() => {
      waiting.delete(id);
      reject(new Error(`it is running and did not answer \`${method}\` within ${patience / 1000}s`));
    }, patience);
    waiting.set(id, (frame) => {
      clearTimeout(timer);
      resolveAnswer(frame);
    });
    gone.then(() => {
      if (!waiting.has(id)) return;
      waiting.delete(id);
      clearTimeout(timer);
      reject(new Error(why(method)));
    });
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
  });
  const refused = (frame) => (frame.error ? `it refused the call — ${frame.error.message ?? JSON.stringify(frame.error)}` : null);

  const known = declared.filter((point) => Object.hasOwn(POINTS, point));
  const patience = Math.max(...known.map((point) => POINTS[point].patience), 10000);
  const hello = await ask('initialize', { protocolVersion: PROTOCOL, plugin: manifest.id, home: scratch, data, points: declared }, patience);
  assert.equal(refused(hello), null, `\`initialize\`: ${refused(hello)}`);
  const wrong = handshake(hello.result, declared);
  assert.equal(wrong, null, `\`initialize\`: ${wrong}`);

  for (const point of known) {
    // An event is the plugin's to send, never asked: the contract test reads it.
    if (POINTS[point].kind === 'event') continue;
    // An attempt goes only to a plugin that took the stand-in world: sent to one that did not, it would reach its platform.
    if (point === 'work/perform') {
      assert.ok(existsSync(`${world}.taken`),
        "`work/perform`: it did not take the kit's stand-in world (DAORIS_PLUGIN_WORLD), so an attempt would reach its platform; none was sent");
    }
    const frame = own(structuredClone(POINTS[point].frame), root);
    const answer = await ask(`hook/${point}`, frame, POINTS[point].patience);
    assert.equal(refused(answer), null, `\`${point}\`: ${refused(answer)}`);
    const problem = ANSWERS[point](answer.result, frame);
    assert.equal(problem, null, `\`${point}\`: it answered ${problem}`);
  }

  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'shutdown' })}\n`);
  // The grace bounds a plugin that stays; once the plugin has left, its timer goes too, or it holds the run open for the whole grace.
  let grace;
  const left = await Promise.race([gone.then(() => true), new Promise((resolveWait) => { grace = setTimeout(() => resolveWait(false), GRACE); })]);
  clearTimeout(grace);
  assert.ok(left, `it did not leave within ${GRACE / 1000}s of \`shutdown\` — the driver ends a plugin that stays`);
  assert.deepEqual(noise, [],
    `it wrote ${noise.length} line(s) on stdout that are not frames, first \`${noise[0]}\` — stdout is the wire: say anything else on stderr`);
});

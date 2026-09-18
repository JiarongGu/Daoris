#!/usr/bin/env node
/**
 * Family rehearsal — prove the ROUTER, across two projects, through the real artefacts.
 *
 * The release rehearsal proves the doctrine lifecycle for one consumer; nothing proved the routing
 * lifecycle across two — and the next real family (a game and its subsystems, with Daoris as their
 * centralized router) needs that to be a gate rather than a belief. This drives it end to end over
 * the tracked example family in `examples/`: current doctrine in both projects, the HTTP host over a
 * scratch store, registration through the real `daoris connect`, a quest published, refused where it
 * should be, taken, finished, and still there after a restart — plus one project's knowledge
 * answering a search made from outside it. See docs/DECISIONS.md D39.
 *
 *   npm run rehearse:family
 *
 * Exit 0 = the router works. Exit 1 = it does not; the transcript names the first thing that broke.
 */
import { execSync, spawn } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const examplesRoot = join(repoRoot, 'examples');
const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const httpProject = join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http');
const httpDll = join(httpProject, 'bin', 'Debug', 'net10.0', 'daoris-knowledge-http.dll');
const scratch = join(repoRoot, '_fixtures', 'family-rehearsal');

const BASE = 'http://localhost:5199';
const KEY = 'family-rehearsal-key';
const EXAMPLES = ['engine', 'game'];

let checks = 0;
let failures = 0;

// Same discipline as the release rehearsal: every run leaves a transcript, outside anything a
// passing run deletes, so a failure is evidence rather than a memory.
const transcript = [`family rehearsal — ${new Date().toISOString()} — node ${process.version}`];
const emit = console.log.bind(console);
console.log = (...args) => {
  const line = args.join(' ');
  transcript.push(line);
  emit(line);
};
process.on('exit', (code) => {
  stopHost();
  transcript.push(`\nexit ${code}`);
  const logDir = join(repoRoot, '_fixtures', 'rehearsal-logs');
  mkdirSync(logDir, { recursive: true });
  const logPath = join(logDir, `family-${new Date().toISOString().replace(/[:.]/g, '-')}.log`);
  writeFileSync(logPath, `${transcript.join('\n')}\n`);
  emit(`  transcript: ${logPath}`);
});

function check(label, condition, detail = '') {
  checks += 1;
  if (condition) {
    console.log(`  ok    ${label}`);
  } else {
    failures += 1;
    console.log(`  FAIL  ${label}${detail ? `\n          ${detail}` : ''}`);
  }
}

function section(title) {
  console.log(`\n${title}`);
}

/** Run a command, capturing output and exit code — never throwing, so a failure is a FAIL line. */
function run(command, cwd, env = {}) {
  try {
    const out = execSync(command, {
      cwd,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...process.env, ...env },
    });
    return { code: 0, out };
  } catch (error) {
    return { code: error.status ?? -1, out: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
}

/** One HTTP call against the host. The key rides only when a step is meant to be authorized. */
async function api(method, path, { body, key } = {}) {
  const response = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      ...(body ? { 'content-type': 'application/json' } : {}),
      ...(key ? { authorization: `Bearer ${key}` } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });
  const text = await response.text();
  let json = null;
  try {
    json = JSON.parse(text);
  } catch {
    // not JSON — the text is still worth printing on a failure
  }
  return { status: response.status, json, text };
}

let host = null;

/**
 * The built DLL is spawned directly rather than through `dotnet run`: `run` wraps the app in a child
 * process, and killing the wrapper on Windows orphans the server on its port — after which every
 * later run fails on the bind and the failure looks like the rehearsal's.
 */
async function startHost() {
  host = spawn('dotnet', [httpDll], {
    cwd: repoRoot,
    stdio: 'ignore',
    env: {
      ...process.env,
      DAORIS_KNOWLEDGE_ROOT: examplesRoot,
      DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge.db'),
      DAORIS_SERVICE_KEY: KEY,
      ASPNETCORE_URLS: BASE,
    },
  });
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      const { status } = await api('GET', '/api/status');
      if (status === 200) return true;
    } catch {
      // not listening yet
    }
    await sleep(300);
  }
  return false;
}

function stopHost() {
  if (host && !host.killed) host.kill();
  host = null;
}

// -------------------------------------------------- 1. doctrine is current

section('1. The examples hold current, clean doctrine');
for (const name of EXAMPLES) {
  const sync = run(`node "${cliBin}" sync`, join(examplesRoot, name));
  check(`${name}: sync exits 0`, sync.code === 0, sync.out);
}

// Tracked-and-modified only: untracked files are the first run of a fresh checkout, not drift.
const dirty = run('git status --porcelain -- examples', repoRoot)
  .out.split('\n')
  .filter((line) => line.trim() && !line.startsWith('??'));
check(
  'sync changed nothing tracked — the examples are current with the canon',
  dirty.length === 0,
  `${dirty.join('; ')}\n          a canon change must sync the examples in the same change (D39)`,
);

for (const name of EXAMPLES) {
  const gate = run(`node "${cliBin}" check`, join(examplesRoot, name));
  check(`${name}: check is clean`, gate.code === 0, gate.out);
}

// -------------------------------------------------- 2. the service comes up

section('2. The service, over the example family');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });

const build = run(`dotnet build "${httpProject}"`, repoRoot);
check('the HTTP host builds', build.code === 0, build.out.split('\n').slice(-4).join('\n'));
check('the host answers /api/status', await startHost());

const registry = await api('GET', '/api/registry');
const adopted = (registry.json ?? []).filter((r) => r.adopted).map((r) => r.repository);
check('both examples are adopted', adopted.includes('engine') && adopted.includes('game'), registry.text);
check(
  'both declared what they own',
  (registry.json ?? []).filter((r) => r.registered).length === 2,
  registry.text,
);

// -------------------------------------------------- 3. the real client registers

section('3. `daoris connect` — the real client, through the real door');
for (const name of EXAMPLES) {
  const connect = run(`node "${cliBin}" connect`, join(examplesRoot, name), {
    DAORIS_SERVICE_URL: BASE,
    DAORIS_SERVICE_KEY: KEY,
  });
  check(`${name}: connect exits 0`, connect.code === 0, connect.out);
}

// -------------------------------------------------- 4. work routes as quests

section('4. Work routes as quests — published, refused, taken, done');

const unauthorized = await api('POST', '/api/refresh');
check('a write without the key is refused (401)', unauthorized.status === 401, unauthorized.text);

const published = await api('POST', '/api/quests', {
  key: KEY,
  body: {
    from: 'game',
    to: 'engine',
    title: 'Expose a streaming budget on the chunk API',
    body:
      'World streaming needs to cap hydration work per frame; today the engine hydrates unbounded. '
      + 'Evidence: the seam appears whenever more than three chunks hydrate in one frame.',
  },
});
check(
  'game publishes a quest to engine',
  published.status === 200 && published.json?.quest?.status === 'Open',
  published.text,
);
const questId = published.json?.quest?.id ?? '';

const stranger = await api('POST', '/api/quests', {
  key: KEY,
  body: { from: 'game', to: 'somewhere-else', title: 'x', body: 'y' },
});
check(
  'a quest to a non-adopter is refused, naming who is addressable',
  stranger.status === 400 && /engine/.test(stranger.json?.error ?? ''),
  stranger.text,
);

const owed = await api('GET', '/api/quests?repository=engine');
check(
  'engine sees what it owes',
  owed.status === 200 && (owed.json ?? []).some((q) => q.id === questId),
  owed.text,
);

const bareDecline = await api('POST', `/api/quests/${questId}/respond`, {
  key: KEY,
  body: { action: 'decline', reason: null },
});
check('declining without a reason is refused', bareDecline.status === 400, bareDecline.text);

const taken = await api('POST', `/api/quests/${questId}/respond`, {
  key: KEY,
  body: { action: 'take', reason: null },
});
check('engine takes it', taken.status === 200 && taken.json?.quest?.status === 'Taken', taken.text);

const done = await api('POST', `/api/quests/${questId}/respond`, {
  key: KEY,
  body: { action: 'done', reason: 'Budget landed as MaxHydrationsPerFrame.' },
});
check('engine finishes it', done.status === 200 && done.json?.quest?.status === 'Done', done.text);

// -------------------------------------------------- 5. knowledge crosses

section('5. Knowledge crosses the family');
const found = await api('GET', `/api/search?q=${encodeURIComponent('chunk hydration')}&localOnly=true`);
check(
  "game's own knowledge answers a search made from outside it",
  found.status === 200 && (found.json ?? []).some((hit) => hit.repository === 'game'),
  found.text.slice(0, 300),
);

// -------------------------------------------------- 6. nothing is lost

section('6. Nothing is lost between sessions');
stopHost();
check('the host restarts over the same store', await startHost());

const after = await api('GET', '/api/quests?repository=engine&includeClosed=true');
check(
  'the finished quest is still there',
  (after.json ?? []).some((q) => q.id === questId && q.status === 'Done'),
  after.text,
);
const registryAfter = await api('GET', '/api/registry');
check(
  'the registry still knows both',
  (registryAfter.json ?? []).filter((r) => r.registered).length === 2,
  registryAfter.text,
);

// -------------------------------------------------- 7. report

section('7. Result');
stopHost();
await sleep(500); // the store's file handle outlives the kill by a beat on Windows

console.log(`\n  ${checks - failures}/${checks} checks passed`);
if (failures) {
  console.log('  The router is NOT proven — the transcript names the first thing that broke.');
  console.log(`  Scratch left at _fixtures/family-rehearsal for inspection.`);
  process.exitCode = 1;
} else {
  console.log('  Two projects, one router: adopted, declared, connected; a quest published,');
  console.log('  refused where it should be, taken, finished, and still there after a restart;');
  console.log("  one project's knowledge answering the other's search.");
  rmSync(scratch, { recursive: true, force: true });
}

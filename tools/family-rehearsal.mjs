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
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const examplesRoot = join(repoRoot, 'examples');
const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const httpProject = join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http');
const httpDll = join(httpProject, 'bin', 'Debug', 'net10.0', 'daoris-knowledge-http.dll');
const driverProject = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Host');
const driverDll = join(driverProject, 'bin', 'Debug', 'net10.0', 'daoris-driver.dll');
const scratch = join(repoRoot, '_fixtures', 'family-rehearsal');
const family = join(scratch, 'family');

/** Recursive copy. Deliberately not fs.cpSync — it has crashed on this platform. */
function copyTree(from, to) {
  mkdirSync(to, { recursive: true });
  for (const entry of readdirSync(from, { withFileTypes: true })) {
    const source = join(from, entry.name);
    const target = join(to, entry.name);
    if (entry.isDirectory()) copyTree(source, target);
    else copyFileSync(source, target);
  }
}

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
      DAORIS_KNOWLEDGE_ROOT: family,
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

// The service runs over a COPY of the tracked examples: a newcomer is born mid-rehearsal (D44), and
// nothing may dirty the tracked tree.
for (const name of EXAMPLES) copyTree(join(examplesRoot, name), join(family, name));

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

// -------------------------------------------------- 6. a newcomer joins

section('6. A newcomer is born and joins (D44)');
const newcomer = join(family, 'newcomer');
mkdirSync(newcomer, { recursive: true });
writeFileSync(join(newcomer, 'README.md'), '# newcomer\n\nBorn during the rehearsal.\n');

const init = run(`node "${cliBin}" init`, newcomer);
check('newcomer: init writes a manifest', init.code === 0 && existsSync(join(newcomer, 'daoris.json')), init.out);

// The declaration is the joining repository's own act; the rehearsal plays its agent.
const manifestPath = join(newcomer, 'daoris.json');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
manifest.domain = {
  summary: 'Born during the rehearsal.',
  owns: ['its own birth'],
  accepts: ['a first quest'],
};
writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);

const newcomerSync = run(`node "${cliBin}" sync`, newcomer);
check('newcomer: sync materializes the canon', newcomerSync.code === 0, newcomerSync.out);
const newcomerCheck = run(`node "${cliBin}" check`, newcomer);
check('newcomer: check is clean on first contact', newcomerCheck.code === 0, newcomerCheck.out);
const newcomerConnect = run(`node "${cliBin}" connect`, newcomer, {
  DAORIS_SERVICE_URL: BASE,
  DAORIS_SERVICE_KEY: KEY,
});
check('newcomer: connect exits 0', newcomerConnect.code === 0, newcomerConnect.out);

const registryGrown = await api('GET', '/api/registry');
check(
  'the registry now knows three members',
  (registryGrown.json ?? []).filter((r) => r.registered).length === 3,
  registryGrown.text,
);

const firstQuest = await api('POST', '/api/quests', {
  key: KEY,
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'A first quest for the newcomer',
    body: 'Joining means being askable — prove it.',
  },
});
check(
  'a quest reaches the newcomer at once',
  firstQuest.status === 200 && firstQuest.json?.quest?.status === 'Open',
  firstQuest.text,
);
const firstAnswer = await api('POST', `/api/quests/${firstQuest.json?.quest?.id ?? ''}/respond`, {
  key: KEY,
  body: { action: 'done', reason: 'Answered on day one.' },
});
check(
  '…and the newcomer answers it',
  firstAnswer.status === 200 && firstAnswer.json?.quest?.status === 'Done',
  firstAnswer.text,
);

// -------------------------------------------------- 7. the driver drives

section('7. The driver starts what should start (D46)');

// The newcomer is the drivable one: it was born in scratch and connected FROM scratch, so its
// registered root is a tree the rehearsal owns — never the tracked examples, which connect from the
// real repository and must never be spawned into by a test.
const driverBuild = run(`dotnet build "${driverProject}"`, repoRoot);
check('the driver host builds', driverBuild.code === 0, driverBuild.out.split('\n').slice(-4).join('\n'));

// A session spawns only onto a clean git tree, so the newcomer becomes one. Identity is passed
// per-command: the rehearsal must work on a machine with no git config at all.
const GIT_ID = '-c user.name="Family Rehearsal" -c user.email="rehearsal@example.invalid"';
run('git init -q', newcomer);
run(`git ${GIT_ID} add -A`, newcomer);
run(`git ${GIT_ID} commit -q -m "the newcomer is born"`, newcomer);

// The stub agent: a session with real mechanics and no model (D46 §8). It claims its own quest
// through the same HTTP door a real session's connector would use, works (a commit), and closes —
// or declines when the ask says to, because judging the ask is the session's job, not the driver's.
const stubAgent = join(scratch, 'stub-agent.mjs');
writeFileSync(stubAgent, `
import { execSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';

const url = process.env.DAORIS_SERVICE_URL;
const key = process.env.DAORIS_SERVICE_KEY;
const id = process.env.DAORIS_QUEST_ID;
const title = process.env.DAORIS_QUEST_TITLE ?? '';

const respond = async (action, reason) => {
  const response = await fetch(url + '/api/quests/' + id + '/respond', {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: 'Bearer ' + key },
    body: JSON.stringify({ action, reason }),
  });
  if (!response.ok) throw new Error(action + ' failed: ' + (await response.text()));
};

await respond('take', null);
if (/decline/i.test(title)) {
  await respond('decline', 'The stub declines what asks to be declined.');
  process.exit(0);
}

writeFileSync('answered-' + id + '.md', '# ' + title + '\\n\\nAnswered by the stub session.\\n');
const git = 'git -c user.name="Stub Session" -c user.email="stub@example.invalid"';
execSync(git + ' add -A', { stdio: 'ignore' });
execSync(git + ' commit -q -m "stub: answer quest ' + id + '"', { stdio: 'ignore' });
await respond('done', 'Landed by the stub session.');
`);

// The person's standing choices, scratch-local: only the newcomer is drivable, and the stub spawns.
const driverConfig = join(scratch, 'driver.json');
writeFileSync(driverConfig, `${JSON.stringify({
  drivable: ['newcomer'],
  adapter: 'stub',
  cap: 2,
  timeoutMinutes: 2,
  commands: { stub: ['node', stubAgent] },
}, null, 2)}\n`);

const DRIVER_ENV = {
  DAORIS_SERVICE_URL: BASE,
  DAORIS_SERVICE_KEY: KEY,
  DAORIS_DRIVER_CONFIG: driverConfig,
};
const drive = (mode = '--until-idle') => run(`dotnet "${driverDll}" ${mode}`, scratch, DRIVER_ENV);

const driven = await api('POST', '/api/quests', {
  key: KEY,
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Drive the newcomer',
    body: 'Prove the loop: a quest becomes a session becomes a commit becomes done.',
  },
});
const drivenId = driven.json?.quest?.id ?? '';

// A dirty tree holds the queue rather than entangling a session with somebody's work in flight —
// and holding is NOT progress, so --until-idle returns instead of spinning.
writeFileSync(join(newcomer, 'work-in-flight.txt'), 'uncommitted\n');
const heldRun = drive();
check('a dirty tree holds the start and says so', heldRun.code === 0 && /held/.test(heldRun.out), heldRun.out);
const stillOpen = await api('GET', '/api/quests?repository=newcomer');
check(
  'the held quest is untouched — still open, still nobody’s',
  (stillOpen.json ?? []).some((q) => q.id === drivenId && q.status === 'Open'),
  stillOpen.text,
);
const noSessions = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true');
check('no session record was created for a hold', (noSessions.json ?? []).length === 0, noSessions.text);

// Clean the tree; now the loop runs whole: spawn → claim → commit → done → observed.
rmSync(join(newcomer, 'work-in-flight.txt'));
const drivenRun = drive();
check('the driver runs the quest to done', drivenRun.code === 0 && /completed/.test(drivenRun.out), drivenRun.out);

const drivenQuest = await api('GET', '/api/quests?repository=newcomer&includeClosed=true');
check(
  'the quest reached done — closed by the SESSION, through its own door',
  (drivenQuest.json ?? []).some((q) => q.id === drivenId && q.status === 'Done'),
  drivenQuest.text,
);

const sessions = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true');
const completed = (sessions.json ?? []).find((s) => s.quest === drivenId);
check('the session record ends completed', completed?.state === 'completed', sessions.text);
check(
  'the record carries the evidence — the commit that landed',
  /commits landed/.test(completed?.evidence ?? '') && /stub: answer quest/.test(completed?.evidence ?? ''),
  JSON.stringify(completed),
);
check('the transcript path was recorded', Boolean(completed?.transcript), JSON.stringify(completed));
const landed = run('git log --oneline', newcomer);
check(
  'the commit is really in the newcomer’s history',
  new RegExp(`stub: answer quest ${drivenId}`).test(landed.out),
  landed.out,
);

// Declining is a real answer, and it is the session's answer — the driver only observes it.
const declineAsk = await api('POST', '/api/quests', {
  key: KEY,
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Please decline this ask',
    body: 'The stub judges the ask; this one asks to be turned down.',
  },
});
const declineRun = drive();
check('a declined quest concludes a declined session', declineRun.code === 0 && /declined/.test(declineRun.out), declineRun.out);
const declinedQuest = await api('GET', '/api/quests?repository=newcomer&includeClosed=true');
check(
  'the decline carries its reason on the quest',
  (declinedQuest.json ?? []).some(
    (q) => q.id === (declineAsk.json?.quest?.id ?? '') && q.status === 'Declined' && /declines/.test(q.note ?? ''),
  ),
  declinedQuest.text,
);

// Driving is additive, never exclusive (D46 §2): a quest an outside session already took is not the
// driver's to start — it is not even considered, and no record appears.
const outside = await api('POST', '/api/quests', {
  key: KEY,
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Outside work in flight',
    body: 'An interactive session took this before the driver ever looked.',
  },
});
await api('POST', `/api/quests/${outside.json?.quest?.id ?? ''}/respond`, {
  key: KEY,
  body: { action: 'take', reason: null },
});
const sessionsBefore = ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? []).length;
const outsideRun = drive('--once');
const sessionsAfter = ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? []).length;
check(
  'a quest an outside session took is left entirely alone',
  outsideRun.code === 0 && sessionsAfter === sessionsBefore,
  outsideRun.out,
);

// -------------------------------------------------- 8. nothing is lost

section('8. Nothing is lost between sessions');
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
  'the registry still knows all three — the newcomer survives the restart too',
  (registryAfter.json ?? []).filter((r) => r.registered).length === 3,
  registryAfter.text,
);
const sessionsAfterRestart = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true');
check(
  'the session records survive too — the record is service state, the process never was',
  (sessionsAfterRestart.json ?? []).some((s) => s.quest === drivenId && s.state === 'completed'),
  sessionsAfterRestart.text,
);

// -------------------------------------------------- 9. report

section('9. Result');
stopHost();
await sleep(500); // the store's file handle outlives the kill by a beat on Windows

console.log(`\n  ${checks - failures}/${checks} checks passed`);
if (failures) {
  console.log('  The router is NOT proven — the transcript names the first thing that broke.');
  console.log(`  Scratch left at _fixtures/family-rehearsal for inspection.`);
  process.exitCode = 1;
} else {
  console.log('  Two projects, one router — and a third born mid-run: adopted, declared, connected;');
  console.log('  a quest published, refused where it should be, taken, finished, and still there');
  console.log("  after a restart; one project's knowledge answering the other's search; a newcomer");
  console.log('  joining through the real CLI and answering its first quest on day one — then DRIVEN:');
  console.log('  a quest became a session became a commit became done, a dirty tree held, a decline');
  console.log('  carried its reason, and outside work was left entirely alone (D46).');
  rmSync(scratch, { recursive: true, force: true });
}

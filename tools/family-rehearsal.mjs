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
const REMOTE_BASE = 'http://localhost:5198';
const HOST_B_BASE = 'http://localhost:5197';
const EXAMPLES = ['engine', 'game'];

// Hermetic by construction: every host and driver in this rehearsal points its remote-config lookup at
// a file that does not exist, so a real ~/.daoris/remote.json on the developer's machine can never
// leak a real deployment into a gate run. The remote phase then opts in per process, by env pair.
const NO_REMOTE = { DAORIS_REMOTE_CONFIG: join(repoRoot, '_fixtures', 'family-rehearsal', 'no-remote.json') };

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
  stopEverything();
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

/** Run a command, capturing output and exit code — never throwing, so a failure is a FAIL line. A
 * timeout kills the child and returns its partial output, so a hung driver is a captured FAIL rather
 * than a frozen gate. */
function run(command, cwd, env = {}, timeout = 0) {
  try {
    const out = execSync(command, {
      cwd,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...process.env, ...env },
      ...(timeout ? { timeout, killSignal: 'SIGKILL' } : {}),
    });
    return { code: 0, out };
  } catch (error) {
    const timedOut = error.killed || error.signal === 'SIGKILL';
    return {
      code: error.status ?? -1,
      out: `${error.stdout ?? ''}${error.stderr ?? ''}${timedOut ? '\n[killed: exceeded the drive timeout]' : ''}`,
    };
  }
}

/** One HTTP call against a host. The key rides only when a step is meant to be authorized. */
async function api(method, path, { body, key, base = BASE } = {}) {
  const response = await fetch(`${base}${path}`, {
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
const children = [];

/**
 * The built DLL is spawned directly rather than through `dotnet run`: `run` wraps the app in a child
 * process, and killing the wrapper on Windows orphans the server on its port — after which every
 * later run fails on the bind and the failure looks like the rehearsal's.
 *
 * Readiness accepts 401 as listening: a shared-mode host answers nothing without a key, and "the gate
 * is up" is exactly the signal being waited for.
 */
async function startServer(env, base) {
  const child = spawn('dotnet', [httpDll], {
    cwd: repoRoot,
    stdio: 'ignore',
    env: { ...process.env, ...NO_REMOTE, ...env },
  });
  children.push(child);
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      const { status } = await api('GET', '/api/status', { base });
      if (status === 200 || status === 401) return child;
    } catch {
      // not listening yet
    }
    await sleep(300);
  }
  return null;
}

async function startHost(extraEnv = {}) {
  host = await startServer({
    DAORIS_KNOWLEDGE_ROOT: family,
    DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge.db'),
    ASPNETCORE_URLS: BASE,
    ...extraEnv,
  }, BASE);
  return host !== null;
}

function stopHost() {
  if (host && !host.killed) host.kill();
  host = null;
}

function stopEverything() {
  for (const child of children) {
    if (child && !child.killed) child.kill();
  }
  children.length = 0;
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
  });
  check(`${name}: connect exits 0`, connect.code === 0, connect.out);
}

// -------------------------------------------------- 4. work routes as quests

section('4. Work routes as quests — published, refused, taken, done');

// No key on this door: local trust — the OS account is the boundary (D21), and the host refuses to
// bind anywhere but the loopback in local mode. The keyed gate is the SHARED deployment's, phase 9.
const published = await api('POST', '/api/quests', {
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

  body: { action: 'decline', reason: null },
});
check('declining without a reason is refused', bareDecline.status === 400, bareDecline.text);

const taken = await api('POST', `/api/quests/${questId}/respond`, {

  body: { action: 'take', reason: null },
});
check('engine takes it', taken.status === 200 && taken.json?.quest?.status === 'Taken', taken.text);

const done = await api('POST', `/api/quests/${questId}/respond`, {

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
});
check('newcomer: connect exits 0', newcomerConnect.code === 0, newcomerConnect.out);

const registryGrown = await api('GET', '/api/registry');
check(
  'the registry now knows three members',
  (registryGrown.json ?? []).filter((r) => r.registered).length === 3,
  registryGrown.text,
);

const firstQuest = await api('POST', '/api/quests', {

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
    headers: {
      'content-type': 'application/json',
      ...(key ? { authorization: 'Bearer ' + key } : {}),
    },
    body: JSON.stringify({ action, reason }),
  });
  return { ok: response.ok, text: await response.text() };
};

// The claiming judgement a real session has (D46 §3): if somebody already has the quest, stand down
// CLEANLY — exit 0 with the quest still theirs is exactly the shape the driver concludes stood-down from.
const takeAnswer = await respond('take', null);
if (!takeAnswer.ok) {
  if (/already taken/i.test(takeAnswer.text)) process.exit(0);
  throw new Error('take failed: ' + takeAnswer.text);
}
if (/decline/i.test(title)) {
  await respond('decline', 'The stub declines what asks to be declined.');
  process.exit(0);
}

writeFileSync('answered-' + id + '.md', '# ' + title + '\\n\\nAnswered by the stub session.\\n');
const git = 'git -c user.name="Stub Session" -c user.email="stub@example.invalid"';
execSync(git + ' add -A', { stdio: 'ignore' });
execSync(git + ' commit -q -m "stub: answer quest ' + id + '"', { stdio: 'ignore' });
const doneAnswer = await respond('done', 'Landed by the stub session.');
if (!doneAnswer.ok) throw new Error('done failed: ' + doneAnswer.text);
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
  DAORIS_DRIVER_CONFIG: driverConfig,
  ...NO_REMOTE,
};
const drive = (mode = '--until-idle') => run(`dotnet "${driverDll}" ${mode}`, scratch, DRIVER_ENV);

const driven = await api('POST', '/api/quests', {

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

  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Outside work in flight',
    body: 'An interactive session took this before the driver ever looked.',
  },
});
await api('POST', `/api/quests/${outside.json?.quest?.id ?? ''}/respond`, {

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

// -------------------------------------------------- 9. the remote

section('9. The remote: two machines, one lock (D47)');

const remoteDb = join(scratch, 'remote.db');
const remoteRoot = join(scratch, 'remote-root');
mkdirSync(remoteRoot, { recursive: true });

// A decoy repository on the SERVER'S own disk, under the root the shared host is pointed at. A shared
// deployment is fed, not scanned (D47 §4) — and not only at the refresh route: the service indexes on
// first use when its store is empty, so without the empty-source composition the host's first request
// would scan this in and serve it to every keyed caller.
const decoy = join(remoteRoot, 'server-decoy');
mkdirSync(join(decoy, '.claude', 'knowledge'), { recursive: true });
writeFileSync(
  join(decoy, '.claude', 'knowledge', 'server-secret.md'),
  '# server secret\n\nThe shared host must never scan this off its own disk.\n',
);

// Keys are minted on the serving binary, against the same store it will serve (D47 §7).
const mint = (name) => run(`dotnet "${httpDll}" keys mint --name ${name} --days 2`, repoRoot, {
  DAORIS_KNOWLEDGE_DB: remoteDb,
});
const mintA = mint('person@machine-a');
const keyA = (mintA.out.split('\n')[0] ?? '').trim();
check('a key is minted for machine a, shown once', mintA.code === 0 && keyA.startsWith('dk_'), mintA.out);
const mintB = mint('person@machine-b');
const keyB = (mintB.out.split('\n')[0] ?? '').trim();
check('a second key for machine b', mintB.code === 0 && keyB.startsWith('dk_') && keyB !== keyA, mintB.out);

const remoteHost = await startServer({
  DAORIS_MODE: 'shared',
  DAORIS_KNOWLEDGE_DB: remoteDb,
  DAORIS_KNOWLEDGE_ROOT: remoteRoot,
  ASPNETCORE_URLS: REMOTE_BASE,
}, REMOTE_BASE);
check('the shared host is up', remoteHost !== null);

// The gate: every route, reads included (D47 §7) — and a refusal never echoes what was presented.
const noKey = await api('GET', '/api/quests', { base: REMOTE_BASE });
check('a read without a key answers 401', noKey.status === 401, noKey.text);
const garbage = 'dk_00000000notakey';
const wrongKey = await api('GET', '/api/quests', { base: REMOTE_BASE, key: garbage });
check(
  'a wrong key answers 401 without echoing what was presented',
  wrongKey.status === 401 && !wrongKey.text.includes(garbage),
  wrongKey.text,
);
const withKey = await api('GET', '/api/quests', { base: REMOTE_BASE, key: keyA });
check('a minted key opens the door', withKey.status === 200, withKey.text);
const page = await api('GET', '/', { base: REMOTE_BASE });
check('the shared host serves no page — an API until person-auth exists', page.status === 404, String(page.status));

// The read above was the shared host's first — exactly when index-on-first-use would have scanned.
const decoySearch = await api(
  'GET', `/api/search?q=${encodeURIComponent('server secret')}`, { base: REMOTE_BASE, key: keyA });
const decoyRepos = await api('GET', '/api/repositories', { base: REMOTE_BASE, key: keyA });
check(
  'the shared host never scans its own disk — the decoy repository beside it stays unserved',
  decoySearch.status === 200 && !(decoySearch.json ?? []).some((hit) => hit.repository === 'server-decoy')
    && decoyRepos.status === 200 && !(decoyRepos.json ?? []).some((r) => r.repository === 'server-decoy'),
  `${decoySearch.text.slice(0, 200)} | ${decoyRepos.text.slice(0, 200)}`,
);

// Machine B is born whole: its own family, its own store, its own host — joined, keeping its
// knowledge home. The manifest is the declaration (D47 §4).
const familyB = join(scratch, 'family-b');
const borealis = join(familyB, 'borealis');
mkdirSync(borealis, { recursive: true });
writeFileSync(join(borealis, 'README.md'), '# borealis\n\nMachine B has this checkout.\n');
run(`node "${cliBin}" init`, borealis);
const borealisManifest = JSON.parse(readFileSync(join(borealis, 'daoris.json'), 'utf8'));
borealisManifest.domain = {
  summary: 'Machine B has this checkout.',
  owns: ['the aurora'],
  accepts: ['a crossing quest'],
};
borealisManifest.remote = { join: true, knowledge: false };
writeFileSync(join(borealis, 'daoris.json'), `${JSON.stringify(borealisManifest, null, 2)}\n`);
run(`node "${cliBin}" sync`, borealis);
mkdirSync(join(borealis, '.claude', 'knowledge'), { recursive: true });
writeFileSync(
  join(borealis, '.claude', 'knowledge', 'private-lesson.md'),
  '# private lesson\n\nborealis keeps this lesson at home.\n',
);
run('git init -q', borealis);
run(`git ${GIT_ID} add -A`, borealis);
run(`git ${GIT_ID} commit -q -m "borealis is born"`, borealis);

const hostB = await startServer({
  DAORIS_KNOWLEDGE_ROOT: familyB,
  DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge-b.db'),
  ASPNETCORE_URLS: HOST_B_BASE,
  DAORIS_REMOTE_URL: REMOTE_BASE,
  DAORIS_REMOTE_KEY: keyB,
}, HOST_B_BASE);
check('machine b’s host is up, carrying its remote', hostB !== null);
const borealisConnect = run(`node "${cliBin}" connect`, borealis, { DAORIS_SERVICE_URL: HOST_B_BASE });
check('borealis connects on machine b', borealisConnect.code === 0, borealisConnect.out);

// Machine A opts the newcomer in and restarts its host carrying the remote — the relay is a
// composition-time choice (D47 §9), and the restart re-proves the store on the way.
stopHost();
const joinedManifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
joinedManifest.remote = { join: true, knowledge: true };
writeFileSync(manifestPath, `${JSON.stringify(joinedManifest, null, 2)}\n`);
mkdirSync(join(newcomer, '.claude', 'knowledge'), { recursive: true });
writeFileSync(
  join(newcomer, '.claude', 'knowledge', 'rehearsal-lesson.md'),
  '# rehearsal lesson\n\nThe stub taught the remote a lesson about crossings.\n',
);
run(`git ${GIT_ID} add -A`, newcomer);
run(`git ${GIT_ID} commit -q -m "the newcomer joins the remote"`, newcomer);
check('machine a’s host restarts carrying its remote', await startHost({
  DAORIS_REMOTE_URL: REMOTE_BASE,
  DAORIS_REMOTE_KEY: keyA,
}));
const rejoin = run(`node "${cliBin}" connect`, newcomer, { DAORIS_SERVICE_URL: BASE });
check('the newcomer re-registers with its declaration', rejoin.code === 0, rejoin.out);
await api('POST', '/api/refresh');
await api('POST', '/api/refresh', { base: HOST_B_BASE });

// The sync rides the driver tick (D47 §9): machine b feeds first so the remote knows borealis, then
// machine a feeds the newcomer and mirrors the family down.
const driverConfigB = join(scratch, 'driver-b.json');
writeFileSync(driverConfigB, `${JSON.stringify({
  drivable: ['borealis'],
  adapter: 'stub',
  cap: 2,
  timeoutMinutes: 2,
  commands: { stub: ['node', stubAgent] },
}, null, 2)}\n`);
// Default to a single tick — one --once does sync → plan → run-to-conclusion, which is all any of
// these steps needs. (An omitted mode used to fall through to watch-forever, which a bounded gate can
// never end — the timeout below is the backstop, but the default is what keeps a step from ever
// reaching it.)
const DRIVE_TIMEOUT = 90_000;
const driveA = (mode = '--once') => run(`dotnet "${driverDll}" ${mode}`, scratch, {
  DAORIS_SERVICE_URL: BASE,
  DAORIS_DRIVER_CONFIG: driverConfig,
  DAORIS_REMOTE_URL: REMOTE_BASE,
  DAORIS_REMOTE_KEY: keyA,
}, DRIVE_TIMEOUT);
const driveB = (mode = '--once', extra = {}) => run(`dotnet "${driverDll}" ${mode}`, scratch, {
  DAORIS_SERVICE_URL: HOST_B_BASE,
  DAORIS_DRIVER_CONFIG: driverConfigB,
  DAORIS_REMOTE_URL: REMOTE_BASE,
  DAORIS_REMOTE_KEY: keyB,
  ...NO_REMOTE,
  ...extra,
}, DRIVE_TIMEOUT);

const firstTickB = driveB('--once');
check('machine b’s tick syncs without a problem', firstTickB.code === 0 && !/sync {2}/.test(firstTickB.out), firstTickB.out);
const firstTickA = driveA('--once');
check('machine a’s tick syncs without a problem', firstTickA.code === 0 && !/sync {2}/.test(firstTickA.out), firstTickA.out);

const remoteRegistry = await api('GET', '/api/registry', { base: REMOTE_BASE, key: keyA });
const remoteRows = remoteRegistry.json ?? [];
check(
  'the remote knows both machines’ repositories, with their declarations',
  remoteRows.some((r) => r.repository === 'newcomer' && r.joined && r.sharesKnowledge)
    && remoteRows.some((r) => r.repository === 'borealis' && r.joined && !r.sharesKnowledge),
  remoteRegistry.text,
);
check(
  'no machine path reached the remote registry',
  remoteRows.length > 0 && remoteRows.every((r) => !('root' in r)) && !remoteRegistry.text.includes('_fixtures'),
  remoteRegistry.text,
);

const familyOnA = await api('GET', '/api/registry');
check(
  'machine a’s registry gained borealis — a foreign row, and never a root',
  (familyOnA.json ?? []).some((r) => r.repository === 'borealis' && r.joined && !('root' in r)),
  familyOnA.text,
);

const lesson = await api('GET', `/api/search?q=${encodeURIComponent('lesson about crossings')}`, {
  base: REMOTE_BASE, key: keyA,
});
check(
  'the sharing repository’s knowledge answers on the remote',
  lesson.status === 200 && (lesson.json ?? []).some((h) => h.repository === 'newcomer'),
  lesson.text.slice(0, 300),
);
const withheld = await api('GET', `/api/search?q=${encodeURIComponent('keeps this lesson at home')}`, {
  base: REMOTE_BASE, key: keyA,
});
check(
  'the joined-but-not-sharing repository’s knowledge stays home',
  withheld.status === 200 && (withheld.json ?? []).every((h) => h.repository !== 'borealis'),
  withheld.text.slice(0, 300),
);

// The crossing: published on machine a through its own local door, homed at the remote, driven to
// done on machine b — the loop D45's part 3 exists for.
const crossing = await api('POST', '/api/quests', {
  body: {
    from: 'newcomer',
    to: 'borealis',
    title: 'Cross the machines',
    body: 'Published on machine a, driven on machine b: the write-through and the mirror, end to end.',
  },
});
check(
  'a quest crosses machines through the ordinary local door',
  crossing.status === 200 && crossing.json?.quest?.status === 'Open',
  crossing.text,
);
const crossingId = crossing.json?.quest?.id ?? '';
const onRemote = await api('GET', '/api/quests', { base: REMOTE_BASE, key: keyA });
check(
  '…and lives at the remote — one home per quest',
  (onRemote.json ?? []).some((q) => q.id === crossingId && q.status === 'Open'),
  onRemote.text,
);

const crossingRun = driveB();
check('machine b drives the crossing to done', crossingRun.code === 0 && /completed/.test(crossingRun.out), crossingRun.out);
const landedB = run('git log --oneline', borealis);
check(
  'the commit is really in borealis’ history',
  new RegExp(`stub: answer quest ${crossingId}`).test(landedB.out),
  landedB.out,
);
const closedOnRemote = await api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA });
check(
  'the remote holds the closure',
  (closedOnRemote.json ?? []).some((q) => q.id === crossingId && q.status === 'Done'),
  closedOnRemote.text,
);
const tickBack = driveA('--once');
const closureOnA = await api('GET', '/api/quests?repository=borealis&includeClosed=true');
check(
  'the closure crossed back to machine a’s mirror',
  tickBack.code === 0 && (closureOnA.json ?? []).some((q) => q.id === crossingId && q.status === 'Done'),
  closureOnA.text,
);

// The race (D47 §5): two machines both believe they can drive one quest, and exactly one may. The
// quest is published on machine a, homed at the remote; an outside taker (standing in for another
// machine's session) claims it there. Machine b then drives — its tick syncs first, sees the quest
// already Taken at the one home, and LEAVES IT ALONE: the driver observes the lock and never starts
// what someone else holds (D46 §2). No session is spawned, and the newcomer's checkout is untouched.
// (The other resolution — a session that spawned before the mirror caught up, whose own take loses
// 409 at the home and stands down — is unit-proven in QuestRelayTests; here the deterministic,
// sync-first path is the driver-level guarantee.)
const raced = await api('POST', '/api/quests', {
  body: {
    from: 'newcomer',
    to: 'borealis',
    title: 'Somebody else got here first',
    body: 'The losing machine must not double the work.',
  },
});
const racedId = raced.json?.quest?.id ?? '';
const winner = await api('POST', `/api/quests/${racedId}/respond`, {
  base: REMOTE_BASE, key: keyA, body: { action: 'take', reason: null },
});
check('an outside winner takes the quest at its home', winner.status === 200, winner.text);

const sessionsBeforeRace = ((await api('GET', '/api/sessions?repository=borealis&includeClosed=true',
  { base: HOST_B_BASE })).json ?? []).length;
const race = driveB('--once');
const sessionsAfterRace = ((await api('GET', '/api/sessions?repository=borealis&includeClosed=true',
  { base: HOST_B_BASE })).json ?? []).length;
check(
  'the losing machine spawns no session for a quest already taken elsewhere',
  race.code === 0 && sessionsAfterRace === sessionsBeforeRace,
  `${sessionsBeforeRace} → ${sessionsAfterRace}\n${race.out}`,
);
const stillTheirs = await api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA });
check(
  'the winner keeps the quest',
  (stillTheirs.json ?? []).some((q) => q.id === racedId && q.status === 'Taken'),
  stillTheirs.text,
);

const remoteSessions = await api('GET', '/api/sessions?includeClosed=true', { base: REMOTE_BASE, key: keyA });
const fedRecords = remoteSessions.json ?? [];
check(
  'session records crossed, keyed by origin + id',
  fedRecords.some((s) => s.id.startsWith('person@machine-b/') && s.quest === crossingId && s.state === 'completed'),
  remoteSessions.text,
);
check(
  'no fed record carries a transcript — the path never left its machine',
  fedRecords.length > 0 && fedRecords.every((s) => !('transcript' in s)),
  remoteSessions.text,
);

const revoke = run(`dotnet "${httpDll}" keys revoke ${keyB.slice(3, 11)}`, repoRoot, {
  DAORIS_KNOWLEDGE_DB: remoteDb,
});
const revoked = await api('GET', '/api/quests', { base: REMOTE_BASE, key: keyB });
check(
  'a revoked key is refused by its audit prefix, without echoing the key',
  revoke.code === 0 && revoked.status === 401
    && revoked.text.includes(keyB.slice(3, 11)) && !revoked.text.includes(keyB),
  revoked.text,
);

// The artefact itself: stop the remote and scan its store — nothing machine-local may be in it, and
// nothing a repository kept home. Byte-level, because "the response strips it" is not "it never landed".
if (remoteHost && !remoteHost.killed) remoteHost.kill();
await sleep(700);
const remoteBytes = readFileSync(remoteDb, 'latin1');
check('the remote store holds no machine path at all', !remoteBytes.includes('_fixtures'),
  'a path fragment reached the remote store');
check('…and none of the knowledge that was kept home', !remoteBytes.includes('keeps this lesson at home'),
  'unshared knowledge reached the remote store');

// -------------------------------------------------- 10. report

section('10. Result');
stopEverything();
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
  console.log('  carried its reason, and outside work was left entirely alone (D46). Then REMOTE (D47):');
  console.log('  two machines and a shared host with minted keys — a quest published on one machine,');
  console.log('  driven to done on the other, the closure crossing back; a raced take standing down;');
  console.log('  knowledge crossing only where declared; and the remote store scanned to hold no');
  console.log('  machine path, no transcript, and nothing a repository kept home.');
  rmSync(scratch, { recursive: true, force: true });
}

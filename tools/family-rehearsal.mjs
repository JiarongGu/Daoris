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
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { copyTree } from './fsx.mjs';
import { capture, makeChecker, openTranscript } from './rehearsal-kit.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const examplesRoot = join(repoRoot, 'examples');
const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const httpProject = join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http');
const httpDll = join(httpProject, 'bin', 'Debug', 'net10.0', 'daoris-knowledge-http.dll');
const driverProject = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Host');
const driverDll = join(driverProject, 'bin', 'Debug', 'net10.0', 'daoris-driver.dll');
const scratch = join(repoRoot, '_fixtures', 'family-rehearsal');
const family = join(scratch, 'family');

const BASE = 'http://localhost:5199';
const REMOTE_BASE = 'http://localhost:5198';
const HOST_B_BASE = 'http://localhost:5197';
// 5200, not 5196: the platform's e2e host owns that one, the two suites run in the same CI job, and
// an orphaned host on a shared port makes one gate's readiness probe answer against the other's.
const AURORA_BASE = 'http://localhost:5200';
const EXAMPLES = ['engine', 'game'];

// Hermetic by construction: every host and driver in this rehearsal points its remote-config lookup at
// a file that does not exist, so the developer's real remotes map can never leak a real deployment
// into a gate run. The remote phases then opt in per process — by env pair, or by pointing the lookup
// at a map this run wrote itself.
const NO_REMOTE = { DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json') };

// The same guard for the toolchain (D49 §4). Profiles live BESIDE this file, so pointing it into
// scratch keeps the developer's real harness wiring — and their real credential directories —
// entirely out of a gate run. The toolchain phase opts into a map it writes itself.
const NO_HARNESS = { DAORIS_HARNESS_CONFIG: join(scratch, 'no-harness.json') };

// The home itself (D63), for every child of this run at once: a CLI or driver that resolves a default
// path resolves it under `DAORIS_HOME`, and with none set it REFUSES — there is no default under the
// profile any more, and a gate must never make one there. Set on the process because every child
// inherits it; the per-file guards above still outrank it where a phase wants a file of its own.
process.env.DAORIS_HOME = join(scratch, 'home');

openTranscript(repoRoot, 'family', { beforeExit: () => stopEverything() });
const { totals, check, section } = makeChecker();

const run = (command, cwd, env = {}, timeout = 0) => capture(command, cwd, { env, timeout });

// ONE helper for every driver invocation. Three copies had already diverged three ways: one dropped
// the kill-timeout (four invocations any of which could freeze the gate), one dropped the hermetic
// guard, one grew a parameter nothing passed. The timeout is always on — a hung driver is a captured
// FAIL, never a frozen gate — and NO_REMOTE is always underneath: a phase that wants a remote opts in
// by env pair, which outranks the config-file lookup by the loader's own rule.
const DRIVE_TIMEOUT = 90_000;
const driver = ({ serviceUrl, config, remote = {}, harness = {}, mode = '--once' }) =>
  run(`dotnet "${driverDll}" ${mode}`, scratch, {
    DAORIS_SERVICE_URL: serviceUrl,
    DAORIS_DRIVER_CONFIG: config,
    ...NO_REMOTE,
    ...NO_HARNESS,
    ...remote,
    ...harness,
  }, DRIVE_TIMEOUT);

/**
 * The same driver, running WHILE the gate acts — for a session that must be alive when something
 * happens to it (D68 §5: a losing session stopped). Same environment, same timeout, same answer.
 */
const driverInBackground = ({ serviceUrl, config, remote = {}, harness = {}, mode = '--once' }) =>
  new Promise((resolve) => {
    const child = spawn('dotnet', [driverDll, mode], {
      cwd: scratch,
      env: {
        ...process.env,
        DAORIS_SERVICE_URL: serviceUrl,
        DAORIS_DRIVER_CONFIG: config,
        ...NO_REMOTE,
        ...NO_HARNESS,
        ...remote,
        ...harness,
      },
    });
    children.push(child);
    let out = '';
    child.stdout.on('data', (chunk) => { out += chunk; });
    child.stderr.on('data', (chunk) => { out += chunk; });
    const timer = setTimeout(() => child.kill('SIGKILL'), DRIVE_TIMEOUT);
    child.on('close', (code) => {
      clearTimeout(timer);
      resolve({ code: code ?? -1, out });
    });
  });

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

// What the question is: did SYNC change anything? Measured as the tree before against the tree
// after, tracked-and-modified only (untracked files are the first run of a fresh checkout, not
// drift). Measured as absolute dirtiness once, which read an in-progress edit to `examples/README.md`
// — a file sync never touches — as a canon change nobody synced.
const trackedDirt = () => run('git status --porcelain -- examples', repoRoot)
  .out.split('\n')
  .filter((line) => line.trim() && !line.startsWith('??'))
  .sort();
const beforeSync = trackedDirt();
for (const name of EXAMPLES) {
  const sync = run(`node "${cliBin}" sync`, join(examplesRoot, name));
  check(`${name}: sync exits 0`, sync.code === 0, sync.out);
}

const afterSync = trackedDirt();
const changedBySync = afterSync.filter((line) => !beforeSync.includes(line));
check(
  'sync changed nothing tracked — the examples are current with the canon',
  changedBySync.length === 0,
  `${changedBySync.join('; ')}\n          a canon change must sync the examples in the same change (D39)`,
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

// 🔴 The gate that would have caught CANON8e. When the always-loaded tier moved out of
// `.claude/rules/` and into a span (D59), the scanner kept reading the empty directory and every
// canonical rule fell out of the index — silently, because nothing asserted one was in there. The
// count is not enough: an entry that is indexed under the wrong kind, or whose body came back empty,
// passes a count and fails a reader.
const canonical = await api(
  'GET', `/api/search?q=${encodeURIComponent('never write into another repository')}&localOnly=false`);
const rule = (canonical.json ?? []).find(
  (hit) => hit.kind === 'Rule' && hit.title === 'repository-owns-its-work');
check(
  'a canonical RULE is searchable, wherever the tier keeps it',
  canonical.status === 200 && Boolean(rule),
  canonical.text.slice(0, 300),
);
check(
  '...and it is found in the file that actually holds it',
  Boolean(rule) && /AGENTS\.md$/.test(rule.path ?? ''),
  JSON.stringify(rule ?? null).slice(0, 200),
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
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

// The stub is a fake BINARY as well as a fake session (D49 §4): before anything else it answers the
// two questions the toolchain probe asks any harness — its version, and whether a configuration home
// has been logged into. Answered FIRST and exited immediately, because a probe that fell through
// into the session body would take a quest nobody asked it to.
if (process.argv.includes('--version')) {
  console.log('stub-harness 1.0.0');
  process.exit(0);
}
if (process.argv.includes('--login-state')) {
  // Logged in when the harness has put something in the profile — which is what a real login does.
  const home = process.env.DAORIS_STUB_CONFIG_DIR;
  console.log(home && existsSync(home + '/credentials.json') ? 'logged-in' : 'logged-out');
  process.exit(0);
}

const url = process.env.DAORIS_SERVICE_URL;
const key = process.env.DAORIS_SERVICE_KEY;
const id = process.env.DAORIS_QUEST_ID;
const title = process.env.DAORIS_QUEST_TITLE ?? '';

// An INTAKE (D65 §1b): the same stub spawned for an ASK rather than a quest — no quest variable, the
// ask and its own session instead, in the circle's room under the driver's home. It decides from the
// room's declarations and publishes onto the ask through the door its connector would use; where the
// words do not settle it, it says what it would ask and publishes nothing — the intake asking.
const intakeFor = process.env.DAORIS_ASK_ID;
if (intakeFor) {
  const session = process.env.DAORIS_SESSION_ID;
  console.log('stub: intake for ask ' + intakeFor + ' as session ' + session + ', quest ' + (id ?? 'none'));
  const room = existsSync('AGENTS.md') ? readFileSync('AGENTS.md', 'utf8') : '';
  if (/unsettled/i.test(process.env.DAORIS_TARGET ?? '') || !room.includes('\`newcomer\`')) {
    console.log('stub: the declarations do not settle this — which repository should own it?');
    process.exit(0);
  }

  const published = await fetch(url + '/api/asks/' + intakeFor + '/publish', {
    method: 'POST',
    headers: { 'content-type': 'application/json', ...(key ? { authorization: 'Bearer ' + key } : {}) },
    body: JSON.stringify({
      to: 'newcomer',
      title: 'Say hello, as the intake decided',
      body: 'The intake read the circle, and the newcomer is who this belongs to.',
      then: [{ to: 'newcomer', title: 'Verify {parent} said hello', body: 'Check what {parent} landed.' }],
      session,
    }),
  });
  console.log('stub: intake publish answered ' + published.status);
  process.exit(published.ok ? 0 : 1);
}

const respond = async (action, reason) => {
  const response = await fetch(url + '/api/quests/' + id + '/respond', {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      ...(key ? { authorization: 'Bearer ' + key } : {}),
    },
    body: JSON.stringify({ action, reason }),
  });
  return { ok: response.ok, status: response.status, text: await response.text() };
};

// The failure DRV6 was written from, reproduced exactly: a session that dies BEFORE taking its
// quest. That is the shape that loops — the quest is left \`Open\` and untouched, so the very next
// tick considers it again. A session that fails after taking leaves it \`Taken\` and stops itself.
if (/never lands/i.test(title)) {
  console.log('stub: this one fails before it can take anything');
  process.exit(1);
}

// The claiming judgement a real session has (D46 §3): if somebody already has the quest — here, or
// at the remote first (D69's claim by push) — stand down CLEANLY. Exit 0 with the quest still theirs
// is exactly the shape the driver concludes stood-down from. Read by the status the door answers,
// never by its wording, which is written for a person and changes.
const takeAnswer = await respond('take', null);
if (!takeAnswer.ok) {
  if (takeAnswer.status === 409) process.exit(0);
  throw new Error('take failed: ' + takeAnswer.text);
}
console.log('stub: take answered ' + takeAnswer.text.split('"message":"')[1]?.split('"')[0]);
// A session says things while it works, and the driver captures every line: to the transcript on
// disk (the durable record, D46 §4) and to the live console (D49 §2). This is what the gate reads
// back out of the transcript afterwards.
console.log('stub: taking quest ' + id);
// Which credential profile this spawn actually runs as, straight out of the environment seam
// (D49 §4) — observable, so the gate can assert the session really ran in that configuration home
// rather than trusting the record's word for it.
console.log('stub: config home ' + (process.env.DAORIS_STUB_CONFIG_DIR ?? '(the harness’s own)'));

// A session that is still working when something happens to its claim (D68 §5): it lingers — up to
// a minute, well inside the driver's timeout — before it commits anything, so a driver that stops it
// for a take that lost stops it with nothing landed. If nothing stops it, it finishes, and the gate
// that expected it stopped sees a commit and fails.
if (/linger/i.test(title)) {
  console.log('stub: lingering');
  for (let wait = 0; wait < 240; wait += 1) await new Promise((resolve) => setTimeout(resolve, 250));
}

// What the quest carried (D65 §2), as a session meets it: the links its target names, and each file
// in the directory it was handed — READ, so the gate proves the bytes arrived rather than the name.
// A file named on the record and kept on another machine is said to be elsewhere by the target.
const target = process.env.DAORIS_TARGET ?? '';
for (const link of target.match(/https:\\/\\/\\S+/g) ?? []) console.log('stub: link ' + link);
// The agent producer (MAP3d): a repository that keeps a code map is asked to keep it current, and
// the stub says which file it was pointed at — so the gate reads the ASK back, not the driver's word.
const mapped = target.match(/keeps a code map in \\W(\\S+?\\.json)/);
if (mapped) console.log('stub: told to keep the code map ' + mapped[1]);
const attachments = process.env.DAORIS_QUEST_ATTACHMENTS;
if (attachments) {
  for (const name of readdirSync(attachments)) {
    console.log('stub: attachment ' + name + ' reads ' + readFileSync(join(attachments, name), 'utf8').trim());
  }
} else if (/not on this machine/.test(target)) {
  console.log('stub: a file is named and not here');
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

const drive = (mode = '--until-idle') => driver({ serviceUrl: BASE, config: driverConfig, mode });

// It carries a link and a file (D65 §2), so the loop proves the file reaches the session whole — kept
// by the host under ITS home, which is not the driver's (the driver's is wherever driver.json lives),
// and handed over by the path the host answered rather than one the driver guessed.
const BRIEF = 'the brief the asker attached, read back by the session';
const driven = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Drive the newcomer',
    body: 'Prove the loop: a quest becomes a session becomes a commit becomes done.',
    links: ['https://tickets.example/T-42'],
    attachments: [{ name: 'brief.txt', content: Buffer.from(BRIEF).toString('base64') }],
  },
});
const drivenId = driven.json?.quest?.id ?? '';
const brief = driven.json?.quest?.attachments?.[0];
check(
  'a quest carries a link and a file through the local door',
  driven.status === 200 && driven.json.quest.links?.[0] === 'https://tickets.example/T-42' && brief?.name === 'brief.txt',
  driven.text,
);
check(
  '…and the host keeps the file under ITS home, by the quest and the content',
  Boolean(brief?.path) && brief.path.startsWith(join(scratch, 'home', 'quests', drivenId, 'attachments'))
    && existsSync(brief.path) && readFileSync(brief.path, 'utf8') === BRIEF,
  JSON.stringify(brief),
);

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
// The capture pump tees since D49 §2 — the console is a second destination, never a replacement.
// What this gate can reach is the DURABLE half: the file still holds what the session said. The
// in-memory half rides the shell's IPC bridge, which has no headless door by design, and is held by
// the driver's own tests and the platform's.
check(
  'and the transcript holds what the session actually said',
  existsSync(completed?.transcript ?? '')
    && readFileSync(completed.transcript, 'utf8').includes(`stub: taking quest ${drivenId}`),
  `${completed?.transcript}`,
);
const said = existsSync(completed?.transcript ?? '') ? readFileSync(completed.transcript, 'utf8') : '';
check(
  'the session was handed the link in its target, and READ the file from the directory it was given',
  said.includes('stub: link https://tickets.example/T-42') && said.includes(`-brief.txt reads ${BRIEF}`),
  said.split('\n').filter((line) => line.startsWith('stub:')).join('\n'),
);
// The newcomer keeps no code map yet (it gains one before the crossing, below), so its session is
// not asked about one (MAP3d): a map nobody started is not a session's to invent.
check(
  'a repository that keeps no code map is not asked to keep one',
  said.includes(`stub: taking quest ${drivenId}`) && !said.includes('stub: told to keep the code map'),
  said.split('\n').filter((line) => line.startsWith('stub:')).join('\n'),
);
const landed = run('git log --oneline', newcomer);
check(
  'the commit is really in the newcomer’s history',
  new RegExp(`stub: answer quest ${drivenId}`).test(landed.out),
  landed.out,
);

// A CHAIN (D65 §4): develop, then verify. The close of the first publishes the second in the same
// transaction, and the driver starts it at its next look — no engine, no coordinator, the loop that
// already exists. One --until-idle run takes both, because the second exists the moment the first is
// done.
const chained = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Develop the chained change',
    body: 'The first step of a chain: make the change.',
    then: [{ to: 'newcomer', title: 'Verify {parent} landed', body: 'The second step: check what {parent} did.' }],
  },
});
const developId = chained.json?.quest?.id ?? '';
check(
  'a quest publishes with its chain, and nothing of the chain exists yet',
  chained.status === 200 && chained.json.quest.then?.[0]?.to === 'newcomer'
    && !((await api('GET', '/api/quests?repository=newcomer')).json ?? []).some((q) => q.parent === developId),
  chained.text,
);
const chainRun = drive();
const afterChain = (await api('GET', '/api/quests?repository=newcomer&includeClosed=true')).json ?? [];
const verifyStep = afterChain.find((q) => q.parent === developId);
check(
  'closing it done published the next step — asked by the same asker, naming what it follows',
  afterChain.some((q) => q.id === developId && q.status === 'Done')
    && verifyStep?.from === 'game' && verifyStep?.title === `Verify #${developId} landed`,
  `${chainRun.out}\n${JSON.stringify(afterChain.map((q) => ({ id: q.id, title: q.title, status: q.status, parent: q.parent })))}`,
);
check(
  '…and the driver took the step at its next look and drove it to done — the chain is the loop',
  verifyStep?.status === 'Done'
    && new RegExp(`stub: answer quest ${verifyStep.id}`).test(run('git log --oneline', newcomer).out),
  chainRun.out,
);

// AN ASK (D65 §1a): a sentence entered at a WORKSPACE, not at a repository. With no intake harness
// the declarations tier answers — it proposes, says that is all that ran, and publishes NOTHING. A
// receiver named with --to is the asker deciding: published at once, asked BY the ask, carrying its
// link and file, and driven like any quest. `daoris-driver ask` is the terminal door (D50).
const askVerb = (args) => driver({ serviceUrl: BASE, config: driverConfig, mode: `ask ${args}` });
const questCount = async () => ((await api('GET', '/api/quests?includeClosed=true')).json ?? []).length;
const questsBeforeAsk = await questCount();
const proposedAsk = askVerb('--workspace default "the rendering of the asset pipeline stalls whenever the simulation runs"');
check(
  'an ask with no intake harness is answered by declarations only, and says so, proposing the engine',
  proposedAsk.code === 0 && /by declarations only; no intake harness ran/.test(proposedAsk.out)
    && /proposed, best first: `engine`/.test(proposedAsk.out),
  proposedAsk.out,
);
check('…and publishes nothing — a proposal is a person’s to accept', (await questCount()) === questsBeforeAsk, proposedAsk.out);
const proposedAskId = /ask\s+#([0-9a-f]{6})/.exec(proposedAsk.out)?.[1] ?? '';
const closedAsk = askVerb(`--close ${proposedAskId} --reason "Asked again with the receiver named."`);
check('a person closes their own ask, with the reason', closedAsk.code === 0 && /is closed/.test(closedAsk.out), closedAsk.out);
const refusedAsk = askVerb('--to nobody-here "a receiver nobody registered"');
check(
  'a named receiver that cannot be asked is refused in the exchange’s words — and the ask is kept',
  refusedAsk.code === 1 && /nobody-here/.test(refusedAsk.out) && /kept as/.test(refusedAsk.out),
  refusedAsk.out,
);
const askBrief = join(scratch, 'ask-brief.txt');
writeFileSync(askBrief, 'the brief an ask carried\n');
const namedAsk = askVerb(
  `--workspace default --to newcomer --url https://tickets.example/T-77 --file "${askBrief}" "make the newcomer answer an ask"`);
const namedAskId = /ask\s+#([0-9a-f]{6})/.exec(namedAsk.out)?.[1] ?? '';
const namedQuestId = /quest\s+#([0-9a-f]{12})/.exec(namedAsk.out)?.[1] ?? '';
const namedQuest = ((await api('GET', '/api/quests?repository=newcomer')).json ?? []).find((q) => q.id === namedQuestId);
check(
  'a receiver named with --to is published at once — asked BY the ask, carrying its link and its file',
  namedAsk.code === 0 && namedQuest?.from === `ask #${namedAskId}`
    && namedQuest.links?.[0] === 'https://tickets.example/T-77' && namedQuest.attachments?.[0]?.name === 'ask-brief.txt',
  `${namedAsk.out}\n${JSON.stringify(namedQuest)}`,
);
const askRun = drive();
const askSession = ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? [])
  .find((s) => s.quest === namedQuestId);
const askSaid = existsSync(askSession?.transcript ?? '') ? readFileSync(askSession.transcript, 'utf8') : '';
check(
  '…and the driver drives it like any quest: the ask became work, and its session read the file',
  askSession?.state === 'completed' && askSaid.includes('-ask-brief.txt reads the brief an ask carried'),
  `${askRun.out}\n${askSaid.split('\n').filter((line) => line.startsWith('stub:')).join('\n')}`,
);

// AN INTAKE (D65 §1b, INT4b): the same kind of ask, on a machine that names a harness for it. The loop
// opens a SESSION for the ask in the circle's room under the driver's home, seeded with the
// declarations; the stub reads them and publishes onto the ask in its own words, with a chain the loop
// then drives like any. Where the words do not settle it, the intake publishes nothing and parks for
// the person — and the person's answer to the ask is what ends it. The stub is the harness, as
// everywhere here: no model, no account. The ask the refused receiver kept is closed first, so the
// one unserved ask in the circle is the one this phase makes.
const keptAskId = /kept as `#([0-9a-f]{6})`/.exec(refusedAsk.out)?.[1] ?? '';
askVerb(`--close ${keptAskId} --reason "Asked again with a receiver that exists."`);
const intakeConfig = join(scratch, 'driver-intake.json');
writeFileSync(intakeConfig, `${JSON.stringify({
  ...JSON.parse(readFileSync(driverConfig, 'utf8')),
  intakeAdapter: 'stub',
}, null, 2)}\n`);
const intakeDrive = (mode = '--until-idle') => driver({ serviceUrl: BASE, config: intakeConfig, mode });
const intakesOf = async (askId) =>
  ((await api('GET', '/api/sessions?includeClosed=true')).json ?? []).filter((s) => s.ask === askId);

const settledAsk = askVerb('--workspace default "the newcomer should say hello, and a check should follow"');
const settledAskId = /ask\s+#([0-9a-f]{6})/.exec(settledAsk.out)?.[1] ?? '';
const intakeRun = intakeDrive();
const [intakeSession, ...extraIntakes] = await intakesOf(settledAskId);
const intakeSaid = existsSync(intakeSession?.transcript ?? '') ? readFileSync(intakeSession.transcript, 'utf8') : '';
check(
  'an ask with an intake harness is answered by a SESSION — a conversation for the ask, in the circle’s room',
  extraIntakes.length === 0 && intakeSession?.kind === 'chat' && intakeSession.repository === `ask #${settledAskId}`
    && intakeSession.workspace === 'default' && !intakeSession.quest && intakeSession.state === 'completed'
    && /[\\/]intake[\\/]default$/.test(intakeSession.tree ?? ''),
  `${intakeRun.out}\n${JSON.stringify(intakeSession)}`,
);
const intakeRoom = join(scratch, 'intake', 'default');
check(
  '…its room holds the circle’s declarations, and the session saw an ask and itself — never a quest',
  existsSync(join(intakeRoom, 'AGENTS.md')) && readFileSync(join(intakeRoom, 'AGENTS.md'), 'utf8').includes('`newcomer`')
    && intakeSaid.includes(`stub: intake for ask ${settledAskId} as session ${intakeSession?.id}, quest none`),
  intakeSaid,
);
const settled = (await api('GET', `/api/asks/${settledAskId}`)).json;
const newcomerQuests = (await api('GET', '/api/quests?repository=newcomer&includeClosed=true')).json ?? [];
const intakeQuest = newcomerQuests.find((q) => q.from === `ask #${settledAskId}` && !q.parent);
check(
  '…it published onto the ask — asked BY the ask, in its words — and the ask says the intake answered it',
  settled?.state === 'Published' && settled.tier === 'intake' && settled.intake === intakeSession?.id
    && intakeQuest?.title === 'Say hello, as the intake decided' && settled.quests?.includes(intakeQuest.id),
  JSON.stringify({ settled, intakeQuest }),
);
const intakeStep = newcomerQuests.find((q) => q.parent === intakeQuest?.id);
check(
  '…with the chain it composed, which the loop drove like any: the work, then the check',
  intakeQuest?.status === 'Done' && intakeStep?.status === 'Done' && intakeStep.from === `ask #${settledAskId}`
    && intakeStep.title === `Verify #${intakeQuest.id} said hello`,
  `${intakeRun.out}\n${JSON.stringify({ intakeQuest, intakeStep })}`,
);

const unsettledAsk = askVerb('--workspace default "an unsettled question nobody has declared an answer to"');
const unsettledAskId = /ask\s+#([0-9a-f]{6})/.exec(unsettledAsk.out)?.[1] ?? '';
const parkRun = intakeDrive();
const [parkedIntake] = await intakesOf(unsettledAskId);
check(
  'where the declarations do not settle it, the intake publishes NOTHING and parks, asking the person',
  parkedIntake?.state === 'awaiting-person' && /asks you rather than guess/.test(parkedIntake.note ?? '')
    && (await api('GET', `/api/asks/${unsettledAskId}`)).json?.quests?.length === 0,
  `${parkRun.out}\n${JSON.stringify(parkedIntake)}`,
);
const answeredAsk = askVerb(`--close ${unsettledAskId} --reason "Not a change after all."`);
const endRun = intakeDrive('--once');
const endedIntakes = await intakesOf(unsettledAskId);
check(
  '…and the person’s answer to the ask is what ends it — one intake per ask, never a second',
  answeredAsk.code === 0 && endedIntakes.length === 1 && endedIntakes[0].state === 'stopped'
    && /the person closed ask/.test(endedIntakes[0].note ?? ''),
  `${endRun.out}\n${JSON.stringify(endedIntakes)}`,
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

// 🔴 DRV6 — a quest that keeps failing is PARKED, and parking is what stops an unattended loop
// spending an account on something no retry can fix. Measured before it was designed: ACP2's real
// driven run started one quest 18 times, because a session that dies before taking leaves the quest
// `Open` and untouched, and an untouched open quest is eligible again next tick.
const doomedAsk = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'This one never lands',
    body: 'The stub dies before it can take this — the shape that loops.',
  },
});
const doomedId = doomedAsk.json?.quest?.id ?? '';
const doomedRun = drive();
const doomedSessions = await api('GET', '/api/sessions?includeClosed=true');
const failures = (doomedSessions.json ?? []).filter((s) => s.quest === doomedId && s.state === 'failed');

check(
  'a quest that keeps failing is tried a bounded number of times, not forever',
  failures.length === 3,
  `${failures.length} session(s) ran on #${doomedId} — the limit is 3\n${doomedRun.out}`,
);
check(
  'the driver says it parked it, and names the verb that starts it again',
  /parked/i.test(doomedRun.out) && /driver retry/.test(doomedRun.out),
  doomedRun.out,
);
// The quest itself is untouched: parking is THIS MACHINE's decision about spending, and the driver
// never writes quest state (design §2). Another machine, or a person, can still take it.
const doomedQuest = await api('GET', '/api/quests?repository=newcomer&includeClosed=true');
check(
  'parking is the machine\'s decision and leaves the quest open to anyone else',
  (doomedQuest.json ?? []).some((q) => q.id === doomedId && q.status === 'Open'),
  doomedQuest.text,
);

// And the person's way back in, from a terminal — the second door (D50).
const retried = run(`node "${cliBin}" driver retry ${doomedId}`, scratch, { DAORIS_DRIVER_CONFIG: driverConfig });
const afterRetry = drive();
const retriedSessions = await api('GET', '/api/sessions?includeClosed=true');
check(
  'a retried quest runs again, and parks again after the same count',
  retried.code === 0
    && (retriedSessions.json ?? []).filter((s) => s.quest === doomedId && s.state === 'failed').length === 6,
  `${retried.out}\n${afterRetry.out}`,
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

// -------------------------------------------------- 9. the workspace boundary

section('9. Two workspaces on one machine — the unit of sharing (D48)');

// One machine, two circles. Both are born here rather than borrowed from the existing members,
// because the point being proven is a BOUNDARY: it has to hold between repositories that sit in the
// same folder, are indexed by the same host, and would have matched each other's searches word for
// word an hour ago.
const circleMember = (name, summary, lesson) => {
  const directory = join(family, name);
  mkdirSync(join(directory, '.claude', 'knowledge'), { recursive: true });
  writeFileSync(join(directory, 'README.md'), `# ${name}\n\n${summary}\n`);
  run(`node "${cliBin}" init`, directory);
  const declaration = JSON.parse(readFileSync(join(directory, 'daoris.json'), 'utf8'));
  declaration.domain = { summary, owns: [`the ${name} side`], accepts: ['a scoped quest'] };
  writeFileSync(join(directory, 'daoris.json'), `${JSON.stringify(declaration, null, 2)}\n`);
  run(`node "${cliBin}" sync`, directory);
  // The SAME lesson in both circles, deliberately: if the boundary leaks, this is what crosses.
  writeFileSync(
    join(directory, '.claude', 'knowledge', 'shared-lesson.md'),
    `# shared lesson\n\n${lesson}\n`,
  );
  // A real history, because since D48 §6 knowledge feeds from a named commit on the canonical line —
  // a checkout git cannot answer for feeds nothing, which is a property worth having the gate live
  // with rather than arrange around. The branch is named explicitly: `git init` picks `master` or
  // `main` depending on the version, and a gate that changed behaviour with the developer's git is
  // not a gate.
  run('git init -q', directory);
  run('git symbolic-ref HEAD refs/heads/main', directory);
  run(`git ${GIT_ID} add -A`, directory);
  run(`git ${GIT_ID} commit -q -m "${name} is born"`, directory);
  return directory;
};

const atelier = circleMember('atelier', 'A studio in the aurora circle.', 'Cap the batch size per tick.');
const foundry = circleMember('foundry', 'A workshop in the tools circle.', 'Cap the batch size per tick.');

// `--workspace` is the wiring statement (D48 §2). The repository's own files are untouched by it,
// which the next check proves by hashing the manifest across the call.
const manifestBefore = readFileSync(join(atelier, 'daoris.json'), 'utf8');
const wireAtelier = run(`node "${cliBin}" connect --workspace aurora`, atelier, { DAORIS_SERVICE_URL: BASE });
check(
  'a repository is wired to a workspace, and its manifest is not touched',
  wireAtelier.code === 0
    && /workspace: aurora/.test(wireAtelier.out)
    && readFileSync(join(atelier, 'daoris.json'), 'utf8') === manifestBefore,
  wireAtelier.out,
);
const wireFoundry = run(`node "${cliBin}" connect --workspace tools`, foundry, { DAORIS_SERVICE_URL: BASE });
check('a second repository is wired to a different workspace', wireFoundry.code === 0, wireFoundry.out);

// engine joins the aurora circle, so that circle has someone to ask.
const wireEngine = run(`node "${cliBin}" connect --workspace aurora`, join(examplesRoot, 'engine'), {
  DAORIS_SERVICE_URL: BASE,
});
check('an existing member is re-wired without re-adopting anything', wireEngine.code === 0, wireEngine.out);

// The ordinary re-registration — what every sync tick runs — says nothing about the workspace, and
// must therefore leave it alone. Silence that reset the wiring would quietly collapse every circle
// back into one, and nothing would report it.
const silentReconnect = run(`node "${cliBin}" connect`, atelier, { DAORIS_SERVICE_URL: BASE });
const wiringAfter = await api('GET', '/api/registry');
check(
  'a connect that names no workspace preserves the wiring',
  silentReconnect.code === 0
    && (wiringAfter.json ?? []).find((r) => r.repository === 'atelier')?.workspace === 'aurora',
  `${silentReconnect.out}\n${wiringAfter.text}`,
);
check(
  'a repository nobody wired stays in the default workspace',
  (wiringAfter.json ?? []).find((r) => r.repository === 'newcomer')?.workspace === 'default',
  wiringAfter.text,
);

const scopedRegistry = await api('GET', '/api/registry?workspace=aurora');
check(
  'the registry scoped to a workspace answers that circle and no other',
  (scopedRegistry.json ?? []).some((r) => r.repository === 'atelier')
    && (scopedRegistry.json ?? []).every((r) => r.repository !== 'foundry'),
  scopedRegistry.text,
);

await api('POST', '/api/refresh');
const auroraSearch = await api('GET', `/api/search?q=${encodeURIComponent('cap the batch size')}&workspace=aurora`);
const toolsSearch = await api('GET', `/api/search?q=${encodeURIComponent('cap the batch size')}&workspace=tools`);
check(
  'a search answers from one circle, though the other holds the same lesson word for word',
  auroraSearch.status === 200
    && (auroraSearch.json ?? []).some((h) => h.repository === 'atelier')
    && (auroraSearch.json ?? []).every((h) => h.repository !== 'foundry')
    && (toolsSearch.json ?? []).some((h) => h.repository === 'foundry')
    && (toolsSearch.json ?? []).every((h) => h.repository !== 'atelier'),
  `${auroraSearch.text.slice(0, 300)}\n${toolsSearch.text.slice(0, 300)}`,
);

const scopedConvergence = await api('GET', '/api/convergence?minimumSimilarity=0.8&workspace=aurora');
check(
  'convergence does not pair two circles that never had to agree',
  scopedConvergence.status === 200
    && (scopedConvergence.json ?? []).every((c) => !c.repositories.includes('foundry')),
  scopedConvergence.text.slice(0, 300),
);

const withinCircle = await api('POST', '/api/quests', {
  body: {
    from: 'atelier',
    to: 'engine',
    title: 'A quest inside the circle',
    body: 'Same workspace, so this is an ordinary ask.',
  },
});
check(
  'a quest inside one workspace is published, carrying it',
  withinCircle.status === 200 && withinCircle.json?.quest?.workspace === 'aurora',
  withinCircle.text,
);

const acrossCircles = await api('POST', '/api/quests', {
  body: {
    from: 'atelier',
    to: 'foundry',
    title: 'A quest across the boundary',
    body: 'Different workspaces, so there is nothing to deliver.',
  },
});
const acrossMessage = acrossCircles.json?.error ?? '';
check(
  'a quest across workspaces is refused, and the sentence names both sides',
  acrossCircles.status === 409
    && /atelier/.test(acrossMessage) && /aurora/.test(acrossMessage)
    && /foundry/.test(acrossMessage) && /tools/.test(acrossMessage),
  acrossCircles.text,
);
check(
  '…and nothing was published anywhere',
  ((await api('GET', '/api/quests?repository=foundry&includeClosed=true')).json ?? []).length === 0,
  'a refused cross-workspace quest left a row behind',
);

const auroraQuests = await api('GET', '/api/quests?workspace=aurora');
const toolsQuests = await api('GET', '/api/quests?workspace=tools');
check(
  'the quest list is scoped to its circle',
  (auroraQuests.json ?? []).some((q) => q.id === withinCircle.json?.quest?.id)
    && (toolsQuests.json ?? []).every((q) => q.id !== withinCircle.json?.quest?.id),
  `${auroraQuests.text}\n${toolsQuests.text}`,
);

// -------------------------------------------------- 10. the registration lifecycle

section('10. The registry is the authority, managed from a terminal (D48/D50)');

// A folder nobody registered is not a member. The scan stopped being the authority, which is the
// whole of §3 — and the way that fails silently is by a repository being served that nobody added.
const uninvited = join(family, 'uninvited');
mkdirSync(join(uninvited, '.claude', 'knowledge'), { recursive: true });
writeFileSync(join(uninvited, 'daoris.json'), `${JSON.stringify({ source: 's', packs: [] }, null, 2)}\n`);
writeFileSync(
  join(uninvited, '.claude', 'knowledge', 'uninvited.md'),
  '# uninvited\n\nPresent in the folder, and nobody asked for it.\n',
);
await api('POST', '/api/refresh');
const uninvitedRegistry = await api('GET', '/api/registry');
const uninvitedSearch = await api('GET', `/api/search?q=${encodeURIComponent('nobody asked for it')}`);
check(
  'a folder nobody registered is neither listed nor indexed — the scan is not the authority',
  (uninvitedRegistry.json ?? []).every((r) => r.repository !== 'uninvited')
    && (uninvitedSearch.json ?? []).every((h) => h.repository !== 'uninvited'),
  `${uninvitedRegistry.text.slice(0, 300)}\n${uninvitedSearch.text.slice(0, 300)}`,
);

// …and the verb that adds it. `import` is the old scan, demoted to something a person runs.
const imported = run(`node "${cliBin}" import "${family}"`, scratch, { DAORIS_SERVICE_URL: BASE });
const afterImport = await api('GET', '/api/registry');
check(
  '`daoris import` registers the folder, uninvited included',
  imported.code === 0 && (afterImport.json ?? []).some((r) => r.repository === 'uninvited'),
  `${imported.out}\n${afterImport.text.slice(0, 300)}`,
);
check(
  '…and re-importing re-points nobody: an import states no workspace, and unstated wiring is kept',
  (afterImport.json ?? []).find((r) => r.repository === 'atelier')?.workspace === 'aurora'
    && (afterImport.json ?? []).find((r) => r.repository === 'foundry')?.workspace === 'tools',
  afterImport.text,
);

// Retiring: the one control a person is right to be nervous about, so it says what it does not do.
const retired = run(`node "${cliBin}" retire`, uninvited, { DAORIS_SERVICE_URL: BASE });
const afterRetire = await api('GET', '/api/registry');
check(
  '`daoris retire` takes it off the map, and the sentence says nothing was deleted',
  retired.code === 0 && /[Nn]othing was deleted/.test(retired.out)
    && (afterRetire.json ?? []).every((r) => r.repository !== 'uninvited'),
  `${retired.out}\n${afterRetire.text.slice(0, 300)}`,
);
check(
  '…and it is true: every file the repository had is still there',
  existsSync(join(uninvited, 'daoris.json'))
    && existsSync(join(uninvited, '.claude', 'knowledge', 'uninvited.md')),
  'retiring a registration deleted files',
);
const retiredAgain = run(`node "${cliBin}" retire`, uninvited, { DAORIS_SERVICE_URL: BASE });
check(
  'retiring what is already retired is an answer, not a failure',
  retiredAgain.code === 0 && /not registered/.test(retiredAgain.out),
  retiredAgain.out,
);

// A registered checkout that vanished is NAMED. The ghost fix taught the other direction of this:
// a repository that quietly stops contributing looks exactly like one with nothing to say.
run(`node "${cliBin}" import "${family}"`, scratch, { DAORIS_SERVICE_URL: BASE });
rmSync(uninvited, { recursive: true, force: true });
const absent = await api('POST', '/api/refresh');
check(
  'a registered checkout that is gone is named by the refresh, never silently skipped',
  absent.status === 200 && (absent.json?.absent ?? []).includes('uninvited'),
  absent.text,
);
run(`node "${cliBin}" retire uninvited`, scratch, { DAORIS_SERVICE_URL: BASE });

// -------------------------------------------------- 11. the remote

section('11. The remote: two machines, one order (D47, D68)');

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

// Kept whole so the remote can be stopped and started again over the same store: a remote that is
// DOWN is a case this phase proves (D68 §1), not only one it survives.
const remoteEnv = {
  DAORIS_MODE: 'shared',
  DAORIS_KNOWLEDGE_DB: remoteDb,
  DAORIS_KNOWLEDGE_ROOT: remoteRoot,
  ASPNETCORE_URLS: REMOTE_BASE,
};
let remoteHost = await startServer(remoteEnv, REMOTE_BASE);
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

// Kept whole, like the remote's, so machine b's host can be restarted over the same store — pointed
// at a remote that is not there, which is how this phase takes ONE machine offline while the other
// stays on (D68 §5).
const hostBEnv = {
  DAORIS_KNOWLEDGE_ROOT: familyB,
  DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge-b.db'),
  ASPNETCORE_URLS: HOST_B_BASE,
  DAORIS_REMOTE_URL: REMOTE_BASE,
  DAORIS_REMOTE_KEY: keyB,
  // A machine of its own has a HOME of its own (D63). Inheriting machine a's would let b's host find
  // a's quest files on disk and call them here — two simulated machines quietly sharing one, which
  // is exactly the disclosure boundary this phase exists to prove (D65 §2).
  DAORIS_HOME: join(scratch, 'home-b'),
};
let hostB = await startServer(hostBEnv, HOST_B_BASE);
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
// And a code map of its own, so the one machine without its checkout has something to bring down
// (MAP3e) — committed, as a repository's map is.
mkdirSync(join(newcomer, 'docs'), { recursive: true });
writeFileSync(join(newcomer, 'docs', 'code-map.json'), `${JSON.stringify({
  version: 1,
  modules: [
    { id: 'crossing', path: 'src/crossing', summary: 'carries a quest between machines' },
    { id: 'ledger', path: 'src/ledger', summary: 'remembers what crossed' },
  ],
  dependencies: [{ from: 'crossing', to: 'ledger', kind: 'imports' }],
}, null, 2)}\n`);
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
// never end — the shared helper's timeout is the backstop, but the default is what keeps a step from
// ever reaching it.)
// Machine b runs its sessions as a NAMED ACCOUNT (D49 §4). The name is distinctive on purpose: it is
// what the scans below look for. Which account a session ran as is machine-local — a person may well
// have named a profile after themselves — so it must answer on the machine that ran it and nowhere
// else, exactly like the transcript beside it (D47 §4).
const machineBHarness = join(scratch, 'machine-b', 'harnesses.json');
const machineBProfile = join(scratch, 'machine-b', 'harnesses', 'stub', 'mach-b-account');
mkdirSync(machineBProfile, { recursive: true });
writeFileSync(join(machineBProfile, 'credentials.json'), '{}\n');
writeFileSync(machineBHarness, `${JSON.stringify(
  { defaults: { stub: 'mach-b-account' }, workspaces: {} }, null, 2)}\n`);

const driveA = (mode = '--once') => driver({
  serviceUrl: BASE, config: driverConfig, mode,
  remote: { DAORIS_REMOTE_URL: REMOTE_BASE, DAORIS_REMOTE_KEY: keyA },
});
const driveB = (mode = '--once') => driver({
  serviceUrl: HOST_B_BASE, config: driverConfigB, mode,
  remote: { DAORIS_REMOTE_URL: REMOTE_BASE, DAORIS_REMOTE_KEY: keyB },
  harness: { DAORIS_HARNESS_CONFIG: machineBHarness },
});

// The sync on demand, from a terminal (SYNC6a): the tick's own pass when asked, and where a circle
// stands — read from the machine's host, reaching no remote.
const syncA = (args) => run(`dotnet "${driverDll}" sync ${args}`, scratch, {
  DAORIS_SERVICE_URL: BASE, ...NO_REMOTE, DAORIS_REMOTE_URL: REMOTE_BASE, DAORIS_REMOTE_KEY: keyA,
}, DRIVE_TIMEOUT);
const syncB = (args) => run(`dotnet "${driverDll}" sync ${args}`, scratch, {
  DAORIS_SERVICE_URL: HOST_B_BASE, ...NO_REMOTE, DAORIS_REMOTE_URL: REMOTE_BASE, DAORIS_REMOTE_KEY: keyB,
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

// What the remote holds, and at which commit (SYNC5a): the question a feeding machine asks git about
// before its next feed. A clean checkout fed both its knowledge and its code map.
const newcomerHead = run('git rev-parse HEAD', newcomer).out.trim();
const heldNewcomer = await api('GET', '/api/feed/held?repository=newcomer', { base: REMOTE_BASE, key: keyA });
check(
  'the remote holds the newcomer’s registration, knowledge and code map at the commit its checkout stands on',
  heldNewcomer.json?.knowledge === newcomerHead && heldNewcomer.json?.codeMap === newcomerHead
    && heldNewcomer.json?.registration === newcomerHead,
  `${newcomerHead}\n${heldNewcomer.text}`,
);
const withheld = await api('GET', `/api/search?q=${encodeURIComponent('keeps this lesson at home')}`, {
  base: REMOTE_BASE, key: keyA,
});
check(
  'the joined-but-not-sharing repository’s knowledge stays home',
  withheld.status === 200 && (withheld.json ?? []).every((h) => h.repository !== 'borealis'),
  withheld.text.slice(0, 300),
);

// The crossing: published on machine a through its own local door, committed there, pushed by a's
// next sync, fetched and driven to done on machine b — the loop D45's part 3 exists for (D68).
// It carries a file (D65 §2), and the file is the boundary under test: machine a keeps the bytes,
// the remote and machine b learn the NAME, and the session on b is told the file is elsewhere.
const CROSSING_BYTES = 'the crossing marker bytes, which never leave machine a';
const crossing = await api('POST', '/api/quests', {
  body: {
    from: 'newcomer',
    to: 'borealis',
    title: 'Cross the machines',
    body: 'Published on machine a, driven on machine b: fetch, rebase and push, end to end.',
    attachments: [{ name: 'crossing.txt', content: Buffer.from(CROSSING_BYTES).toString('base64') }],
  },
});
check(
  'a quest crosses machines through the ordinary local door, committed on machine a',
  crossing.status === 200 && crossing.json?.quest?.status === 'Open',
  crossing.text,
);
const crossingId = crossing.json?.quest?.id ?? '';
const pushA = driveA('--once');
const onRemote = await api('GET', '/api/quests', { base: REMOTE_BASE, key: keyA });
check(
  '…and reaches the remote on machine a’s next sync',
  pushA.code === 0 && !/sync {2}/.test(pushA.out)
    && (onRemote.json ?? []).some((q) => q.id === crossingId && q.status === 'Open'),
  `${pushA.out}\n${onRemote.text}`,
);
const crossingAtRemote = (onRemote.json ?? []).find((q) => q.id === crossingId);
check(
  'the remote knows the file by NAME — and has no path for it, because it has no bytes',
  crossingAtRemote?.attachments?.[0]?.name === 'crossing.txt' && !('path' in (crossingAtRemote.attachments[0] ?? {})),
  JSON.stringify(crossingAtRemote),
);
const crossingOnA = (await api('GET', '/api/quests?repository=borealis')).json?.find((q) => q.id === crossingId);
check(
  '…while machine a, which published it, keeps the bytes under its own home',
  existsSync(crossingOnA?.attachments?.[0]?.path ?? '')
    && readFileSync(crossingOnA.attachments[0].path, 'utf8') === CROSSING_BYTES,
  JSON.stringify(crossingOnA),
);
const bytesAtRemote = await api('POST', '/api/quests', {
  base: REMOTE_BASE,
  key: keyA,
  body: {
    from: 'newcomer', to: 'borealis', title: 'Bytes straight at the remote', body: 'A client that sent content.',
    attachments: [{ name: 'leak.txt', content: Buffer.from('bytes that must not land').toString('base64') }],
  },
});
check(
  'a shared deployment refuses a file’s content outright — names, never bytes',
  bytesAtRemote.status === 400 && /keeps names, never bytes/.test(bytesAtRemote.text),
  bytesAtRemote.text,
);

const crossingRun = driveB();
check('machine b drives the crossing to done', crossingRun.code === 0 && /completed/.test(crossingRun.out), crossingRun.out);
const crossingSession = ((await api('GET', '/api/sessions?repository=borealis&includeClosed=true',
  { base: HOST_B_BASE })).json ?? []).find((s) => s.quest === crossingId);
const saidOnB = existsSync(crossingSession?.transcript ?? '') ? readFileSync(crossingSession.transcript, 'utf8') : '';
check(
  'the session on machine b was told the file is named and not on its machine — never handed a path',
  saidOnB.includes('stub: a file is named and not here') && !saidOnB.includes('stub: attachment '),
  saidOnB.split('\n').filter((line) => line.startsWith('stub:')).join('\n'),
);
const landedB = run('git log --oneline', borealis);
check(
  'the commit is really in borealis’ history',
  new RegExp(`stub: answer quest ${crossingId}`).test(landedB.out),
  landedB.out,
);
// The closure reached the remote WITHIN b's tick: the session's done committed on b as it happened,
// and the tick synced again after it concluded (sync design §8), rather than a tick later.
const closedOnRemote = await api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA });
check(
  'the remote holds the closure, pushed within the tick that made it',
  (closedOnRemote.json ?? []).some((q) => q.id === crossingId && q.status === 'Done'),
  closedOnRemote.text,
);

// A TEAMMATE'S CODE MAP, ON THIS MACHINE (MAP3e). Machine b has no checkout of the newcomer, so it
// used to answer "no map" for a repository that keeps one. Its host's pass now brings the map down
// as the remote holds it, and the answer says where it came from — a commit and a key, never a path.
const mapOnB = await api('GET', '/api/code-map/newcomer', { base: HOST_B_BASE });
const heldMapNow = (await api('GET', '/api/feed/held?repository=newcomer', { base: REMOTE_BASE, key: keyA })).json?.codeMap;
check(
  'machine b answers the newcomer’s code map, brought down by its sync, at the commit the remote holds',
  mapOnB.status === 200 && mapOnB.json?.file === 'docs/code-map.json'
    && (mapOnB.json?.modules ?? []).some((m) => m.id === 'crossing')
    && typeof heldMapNow === 'string' && mapOnB.json?.fed?.commit === heldMapNow
    && !mapOnB.text.includes('_fixtures'),
  `${heldMapNow}\n${mapOnB.text}`,
);
const tickBack = driveA('--once');
const closureOnA = await api('GET', '/api/quests?repository=borealis&includeClosed=true');
check(
  'the closure crossed back to machine a',
  tickBack.code === 0 && (closureOnA.json ?? []).some((q) => q.id === crossingId && q.status === 'Done'),
  closureOnA.text,
);
// Every machine sees the team's records (SYNC4): b's session came down to a keyed by its origin, the
// id the platform reads as "elsewhere" — with nothing that stays on b's machine — and none of a's
// own records came back doubled under a's key.
const recordsOnA = (await api('GET', '/api/sessions?includeClosed=true')).json ?? [];
check(
  'machine a sees machine b’s session record, keyed by b’s origin, with nothing machine-local — and none of its own doubled',
  recordsOnA.some((s) => s.id.startsWith('person@machine-b/') && s.quest === crossingId && s.state === 'completed'
    && !s.transcript && !s.profile && !s.tree)
    && !recordsOnA.some((s) => s.id.startsWith('person@machine-a/')),
  JSON.stringify(recordsOnA.filter((s) => s.id.includes('/'))),
);

// The ONLINE race (D46 §2, D68): two machines both believe they can drive one quest. The quest is
// published on machine a and pushed; an outside taker (standing in for another machine whose take
// reached the remote first) claims it there. Machine b then drives — its tick syncs first, sees the
// quest already Taken in the remote's order, and LEAVES IT ALONE: the driver observes the lock and
// never starts what someone else holds. No session is spawned.
const raced = await api('POST', '/api/quests', {
  body: {
    from: 'newcomer',
    to: 'borealis',
    title: 'Somebody else got here first',
    body: 'The losing machine must not double the work.',
  },
});
const racedId = raced.json?.quest?.id ?? '';
driveA('--once');
const winner = await api('POST', `/api/quests/${racedId}/respond`, {
  base: REMOTE_BASE, key: keyA, body: { action: 'take', reason: null },
});
check('an outside winner takes the quest at the remote', winner.status === 200, winner.text);

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

// The ONLINE race at the take (D69): both machines hold the same ask, and both take it while the
// remote answers. The take claims by push, so machine a's take is confirmed before its answer
// returns — and machine b's, pushed a moment later, finds the quest already taken there: it is
// rebased into a CONFLICT on the quest, and b hears the stand-down before any work starts.
const contested = {
  from: 'newcomer', to: 'borealis', title: 'Both machines took this', body: 'The loser is kept, not lost.',
};
const contestedOnA = await api('POST', '/api/quests', { body: contested });
const contestedOnB = await api('POST', '/api/quests', { base: HOST_B_BASE, body: contested });
const contestedId = contestedOnA.json?.quest?.id ?? '';
check(
  'the same ask published on both machines is one quest, by its id',
  contestedId !== '' && contestedOnB.json?.quest?.id === contestedId,
  `${contestedOnA.text}\n${contestedOnB.text}`,
);
const takeOnA = await api('POST', `/api/quests/${contestedId}/respond`, { body: { action: 'take', reason: null } });
const takeOnB = await api('POST', `/api/quests/${contestedId}/respond`, {
  base: HOST_B_BASE, body: { action: 'take', reason: 'machine b’s own session' },
});
check(
  'a take claims by push: the first is confirmed by the remote before it answers',
  takeOnA.status === 200 && /the remote confirmed the claim/.test(takeOnA.text),
  takeOnA.text,
);
const conflictOnB = ((await api('GET', '/api/quests?includeClosed=true', { base: HOST_B_BASE })).json ?? [])
  .find((q) => q.id === contestedId);
check(
  '…and the second stands down before any work, its take kept on the quest as a conflict',
  takeOnB.status === 409 && /taken on another machine first/.test(takeOnB.text)
    && conflictOnB?.status === 'Taken' && conflictOnB.conflicts?.length === 1
    && conflictOnB.conflicts[0].attempted === 'Taken' && conflictOnB.conflicts[0].note === 'machine b’s own session',
  `${takeOnB.text}\n${JSON.stringify(conflictOnB)}`,
);
driveA('--once');
const conflictAtRemote = ((await api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA })).json ?? [])
  .find((q) => q.id === contestedId);
const conflictOnA = ((await api('GET', '/api/quests?includeClosed=true')).json ?? []).find((q) => q.id === contestedId);
check(
  '…and the conflict travels: the remote and the winning machine hold it too',
  conflictAtRemote?.conflicts?.length === 1 && conflictOnA?.conflicts?.length === 1,
  `${JSON.stringify(conflictAtRemote)}\n${JSON.stringify(conflictOnA)}`,
);

// The OFFLINE race (D68 §4/§5): with the remote DOWN, every verb still commits locally and succeeds
// — a take stands UNCONFIRMED, and the answer says so. The tick names the wall and carries on. When
// the remote returns, machine a's tick pushes first and wins; machine b's fetches, rebases its take
// into a conflict, and says so. (A machine with NO remote at all is phases 1–10: nothing in them
// changed for this.)
if (remoteHost && !remoteHost.killed) remoteHost.kill();
await sleep(700);
const offline = {
  from: 'newcomer', to: 'borealis', title: 'Taken on both machines while the remote was down', body: 'It waits, committed.',
};
const offlineOnA = await api('POST', '/api/quests', { body: offline });
await api('POST', '/api/quests', { base: HOST_B_BASE, body: offline });
const offlineId = offlineOnA.json?.quest?.id ?? '';
const offlineTakeA = await api('POST', `/api/quests/${offlineId}/respond`, { body: { action: 'take', reason: null } });
const offlineTakeB = await api('POST', `/api/quests/${offlineId}/respond`, {
  base: HOST_B_BASE, body: { action: 'take', reason: 'machine b, offline' },
});
const downTick = driveA('--once');
check(
  'with the remote down, both machines take the quest locally — UNCONFIRMED, and said — and the tick names the wall',
  offlineTakeA.status === 200 && /UNCONFIRMED/.test(offlineTakeA.text)
    && offlineTakeB.status === 200 && /UNCONFIRMED/.test(offlineTakeB.text)
    && downTick.code === 0 && /sync {2}/.test(downTick.out),
  `${offlineTakeA.text}\n${offlineTakeB.text}\n${downTick.out}`,
);
const downSync = syncA('--workspace default');
const downStatus = syncA('status');
check(
  '`daoris-driver sync` asked with the remote down exits 2 naming the wall — a script that syncs can tell',
  downSync.code === 2 && /^sync {2}default: \S/m.test(downSync.out),
  downSync.out,
);
check(
  '`daoris-driver sync status` says where the circle stands with the remote down: ahead, and the wall as the last try',
  downStatus.code === 0 && /^default {2}[1-9]\d* ahead · \d+ behind · \d+ in conflict · synced /m.test(downStatus.out)
    && /the last try, \d\d:\d\dZ: \S/.test(downStatus.out),
  downStatus.out,
);
remoteHost = await startServer(remoteEnv, REMOTE_BASE);
const backTick = driveA('--once');
const caughtUp = ((await api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA })).json ?? [])
  .find((q) => q.id === offlineId);
check(
  '…and the first tick after it returns pushes what waited',
  remoteHost !== null && backTick.code === 0 && caughtUp?.status === 'Taken' && caughtUp.conflicts?.length === 0,
  `${backTick.out}\n${JSON.stringify(caughtUp)}`,
);
const losingTick = driveB('--once');
const offlineOnB = ((await api('GET', '/api/quests?includeClosed=true', { base: HOST_B_BASE })).json ?? [])
  .find((q) => q.id === offlineId);
check(
  '…and the machine whose take arrived second rebases it into a conflict, and its tick says so',
  losingTick.code === 0 && /kept on the quest as a conflict/.test(losingTick.out)
    && offlineOnB?.conflicts?.length === 1 && offlineOnB.conflicts[0].note === 'machine b, offline',
  `${losingTick.out}\n${JSON.stringify(offlineOnB)}`,
);
const losingStatus = syncB('status');
check(
  '…and machine b’s `sync status` lists that quest as in conflict, with the circle level again',
  losingStatus.code === 0 && /^default {2}0 ahead · 0 behind · [1-9]\d* in conflict · synced /m.test(losingStatus.out)
    && new RegExp(`in conflict: .*#${offlineId}`).test(losingStatus.out),
  losingStatus.out,
);

// A person dismisses that conflict (SYNC6c), on the machine that lost, from a terminal: an operation
// like any other, so the next pass carries it and every machine stops showing it.
const dismissedOnB = syncB(`dismiss ${offlineId}`);
syncB('--workspace default');
syncA('--workspace default');
const dismissedEverywhere = await Promise.all([
  api('GET', '/api/quests?includeClosed=true'),
  api('GET', '/api/quests?includeClosed=true', { base: HOST_B_BASE }),
  api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA }),
]);
check(
  'a conflict dismissed on machine b goes everywhere with the next passes — b, the remote and a — and the take stands',
  dismissedOnB.code === 0 && /Dismissed one conflict/.test(dismissedOnB.out)
    && dismissedEverywhere.every((answer) => (answer.json ?? []).some((q) => q.id === offlineId
      && q.status === 'Taken' && (q.conflicts ?? []).length === 0)),
  `${dismissedOnB.out}\n${dismissedEverywhere.map((answer) => answer.text.slice(0, 300)).join('\n')}`,
);

// On demand: the pass runs when asked, not when the tick comes round. The quest is declined straight
// after, and synced the same way, so nothing later in the run finds an extra quest open to drive.
const whenAsked = await api('POST', '/api/quests', {
  body: { from: 'newcomer', to: 'borealis', title: 'Pushed when asked', body: 'Not waiting for the tick.' },
});
const whenAskedId = whenAsked.json?.quest?.id ?? '';
const syncNow = syncA('--workspace default');
const pushedNow = ((await api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA })).json ?? [])
  .some((q) => q.id === whenAskedId);
check(
  '`daoris-driver sync` runs the tick’s pass when asked — the quest is at the remote before any tick, and the circle stands level',
  whenAsked.status === 200 && syncNow.code === 0 && pushedNow && /^default {2}0 ahead/m.test(syncNow.out),
  syncNow.out,
);
await api('POST', `/api/quests/${whenAskedId}/respond`, { body: { action: 'decline', reason: 'a rehearsal of the door, not work' } });
syncA('--workspace default');
const unwiredSync = syncA('--workspace studio');
check(
  '…and a circle this machine has no remote for is refused by name, not synced nowhere in silence',
  unwiredSync.code === 2 && /no remote for `studio`/.test(unwiredSync.out),
  unwiredSync.out,
);

// A LOSING SESSION STOPPED (D68 §5): a session on machine b takes its quest while b cannot reach the
// remote — so the take stands unconfirmed and the session works. Machine a, still online, takes the
// same quest and wins. When b reaches the remote again, the sync running BESIDE the session finds b's
// take lost, and b's driver stops its own session — before it lands anything — with the reason on
// the record. Deterministic by construction: b is offline exactly as long as this phase says.
const lingering = await api('POST', '/api/quests', {
  body: { from: 'newcomer', to: 'borealis', title: 'A session that lingers', body: 'Worked on two machines at once.' },
});
const lingeringId = lingering.json?.quest?.id ?? '';
driveA('--once');
// b learns of it without driving it: held for one tick.
const bConfig = (extra) => writeFileSync(driverConfigB, `${JSON.stringify({
  drivable: ['borealis'], adapter: 'stub', cap: 2, timeoutMinutes: 2, pollSeconds: 1,
  commands: { stub: ['node', stubAgent] }, ...extra,
}, null, 2)}\n`);
bConfig({ holds: ['borealis'] });
driveB('--once');
// b goes offline: its host is restarted pointed at a remote that is not there.
if (hostB && !hostB.killed) hostB.kill();
await sleep(700);
hostB = await startServer({ ...hostBEnv, DAORIS_REMOTE_URL: 'http://localhost:5191' }, HOST_B_BASE);
bConfig({ holds: [] });
const lingeringRun = driverInBackground({
  serviceUrl: HOST_B_BASE, config: driverConfigB,
  remote: { DAORIS_REMOTE_URL: REMOTE_BASE, DAORIS_REMOTE_KEY: keyB },
  harness: { DAORIS_HARNESS_CONFIG: machineBHarness },
});
// Waited on until the session has its take's ANSWER and is lingering — its own transcript says so —
// not merely until it spawned, nor until the take committed: the host answers only after its push
// failed, and restarting it under a take still in flight would cut the answer off, not the take.
let lingeringSession;
for (let attempt = 0; attempt < 160; attempt += 1) {
  await sleep(250);
  lingeringSession = ((await api('GET', '/api/sessions?repository=borealis', { base: HOST_B_BASE })
    .catch(() => ({ json: [] }))).json ?? []).find((s) => s.quest === lingeringId && s.state === 'working');
  if (lingeringSession && existsSync(lingeringSession.transcript ?? '')
      && readFileSync(lingeringSession.transcript, 'utf8').includes('stub: lingering')) {
    break;
  }
}
const claimWhileOffline = await api('GET', `/api/quests/${lingeringId}/claim`, { base: HOST_B_BASE }).catch(() => null);
check(
  'a session on machine b takes its quest while b cannot reach the remote — unconfirmed, and it works',
  lingeringSession !== undefined && claimWhileOffline?.json?.claim === 'unconfirmed',
  `${JSON.stringify(lingeringSession)}\n${claimWhileOffline?.text}`,
);
const winsOnA = await api('POST', `/api/quests/${lingeringId}/respond`, { body: { action: 'take', reason: 'machine a, online' } });
check('machine a, online, takes the same quest and the remote confirms it', /the remote confirmed the claim/.test(winsOnA.text), winsOnA.text);
// b comes back: the same host, over the same store, pointed at the remote again.
if (hostB && !hostB.killed) hostB.kill();
await sleep(700);
hostB = await startServer(hostBEnv, HOST_B_BASE);
const lingered = await lingeringRun;
const stoppedRecord = ((await api('GET', '/api/sessions?repository=borealis&includeClosed=true', { base: HOST_B_BASE })).json ?? [])
  .find((s) => s.quest === lingeringId);
check(
  '…and when b reaches the remote again, its driver stops its own losing session, with the reason on the record',
  lingered.code === 0 && /stop {2}session/.test(lingered.out)
    && stoppedRecord?.state === 'stood-down' && /another machine's take on the quest reached the remote first/.test(stoppedRecord.note ?? ''),
  `${lingered.out}\n${JSON.stringify(stoppedRecord)}`,
);
check(
  '…before it landed anything: no commit for that quest in borealis, and the quest is machine a’s',
  !new RegExp(`stub: answer quest ${lingeringId}`).test(run('git log --oneline', borealis).out)
    && ((await api('GET', '/api/quests?includeClosed=true', { base: REMOTE_BASE, key: keyA })).json ?? [])
      .some((q) => q.id === lingeringId && q.status === 'Taken' && q.note === 'machine a, online'),
  run('git log --oneline', borealis).out,
);
bConfig({});

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

// Which TOOL did the work travels; which ACCOUNT it ran as does not (D49 §4). The positive half
// matters as much as the negative one: a field that crossed as null because nothing ever set it
// would pass the second check and prove nothing.
const ownRecords = await api('GET', '/api/sessions?repository=borealis&includeClosed=true',
  { base: HOST_B_BASE });
check(
  'machine b’s own record names the account it ran as, the tool that ran it, and the TREE it held',
  (ownRecords.json ?? []).some((s) => s.profile === 'mach-b-account'
    && s.harnessVersion === 'stub-harness 1.0.0'
    // The tree is the unit of exclusion since D51, and the record says which one it was — answered
    // to the machine that ran the session, like the profile and the transcript beside it.
    && s.tree === borealis),
  ownRecords.text,
);
check(
  '…and the fed record carries the tool version but never the account name or the tree',
  fedRecords.some((s) => s.harnessVersion === 'stub-harness 1.0.0')
    && fedRecords.every((s) => !s.profile && !s.tree),
  remoteSessions.text,
);

// REGISTRATIONS FOLLOW THEIR CHECKOUT, BOTH WAYS (SYNC5b). The mirror used to copy a teammate's row
// once and never again (SYNC0b): a revised declaration never came down, and a retire went nowhere.
// Machine b holds borealis for these ticks, so no session moves its history underneath them.
bConfig({ holds: ['borealis'] });
const revised = JSON.parse(readFileSync(join(borealis, 'daoris.json'), 'utf8'));
revised.domain = { ...revised.domain, summary: 'Machine B revised this declaration.' };
writeFileSync(join(borealis, 'daoris.json'), `${JSON.stringify(revised, null, 2)}\n`);
run(`git ${GIT_ID} add daoris.json`, borealis);
run(`git ${GIT_ID} commit -q -m "borealis revises its declaration"`, borealis);
run(`node "${cliBin}" connect`, borealis, { DAORIS_SERVICE_URL: HOST_B_BASE });
driveB('--once');
driveA('--once');
const revisedHead = run('git rev-parse HEAD', borealis).out.trim();
const heldRevised = await api('GET', '/api/feed/held?repository=borealis', { base: REMOTE_BASE, key: keyA });
const remoteBorealis = ((await api('GET', '/api/registry', { base: REMOTE_BASE, key: keyA })).json ?? [])
  .find((r) => r.repository === 'borealis');
check(
  'a revised declaration is registered at the remote, at the commit that made it',
  heldRevised.json?.registration === revisedHead && remoteBorealis?.summary === 'Machine B revised this declaration.',
  `${revisedHead}\n${heldRevised.text}\n${JSON.stringify(remoteBorealis)}`,
);
const revisedOnA = ((await api('GET', '/api/registry')).json ?? []).find((r) => r.repository === 'borealis');
check(
  '…and machine a’s copy follows it, where it used to stay as first copied (SYNC0b) — and still no root',
  revisedOnA?.summary === 'Machine B revised this declaration.' && !('root' in revisedOnA),
  JSON.stringify(revisedOnA),
);

// A second checkout behind the first: its older declaration used to win by arriving last.
const staleDeclaration = await api('POST', '/api/registry', {
  base: REMOTE_BASE, key: keyA,
  body: {
    repository: 'borealis', packs: [], join: true, shareKnowledge: false,
    domain: { summary: 'An older checkout’s declaration.', owns: [], accepts: [] },
    commit: run('git rev-parse HEAD~1', borealis).out.trim(), committedAt: '2000-01-01T00:00:00Z',
    branch: run('git rev-parse --abbrev-ref HEAD', borealis).out.trim(),
  },
});
check(
  'a declaration from an older commit does not replace the held one — information, not a wall',
  staleDeclaration.status === 409 && staleDeclaration.json?.information === true
    && ((await api('GET', '/api/registry', { base: REMOTE_BASE, key: keyA })).json ?? [])
      .some((r) => r.repository === 'borealis' && r.summary === 'Machine B revised this declaration.'),
  staleDeclaration.text,
);

// A retire undone before any pass ran never reaches the circle: joining again takes the tombstone back.
run(`node "${cliBin}" retire`, borealis, { DAORIS_SERVICE_URL: HOST_B_BASE });
run(`node "${cliBin}" connect`, borealis, { DAORIS_SERVICE_URL: HOST_B_BASE });
const owedAfterUndo = await api('GET', '/api/registry/retired?workspace=default', { base: HOST_B_BASE });
check(
  'a retire undone by joining again before any pass owes the circle nothing',
  Array.isArray(owedAfterUndo.json?.repositories) && owedAfterUndo.json.repositories.length === 0,
  owedAfterUndo.text,
);

// A retire that stands travels: b owes it, the next pass carries it, and a's copy goes with it.
const retiredOnB = run(`node "${cliBin}" retire`, borealis, { DAORIS_SERVICE_URL: HOST_B_BASE });
const retireOwed = await api('GET', '/api/registry/retired?workspace=default', { base: HOST_B_BASE });
check(
  'retiring a joined checkout on machine b leaves a retire owed to its circle, and the sentence says so',
  retiredOnB.code === 0 && (retireOwed.json?.repositories ?? []).includes('borealis')
    && /leaves the `default` circle's deployment too/.test(retiredOnB.out),
  `${retiredOnB.out}\n${retireOwed.text}`,
);
driveB('--once');
const owedAfterPass = await api('GET', '/api/registry/retired?workspace=default', { base: HOST_B_BASE });
check(
  '…the next pass carries it: the remote no longer lists borealis, and machine b owes nothing',
  !((await api('GET', '/api/registry', { base: REMOTE_BASE, key: keyA })).json ?? []).some((r) => r.repository === 'borealis')
    && (owedAfterPass.json?.repositories ?? ['?']).length === 0,
  owedAfterPass.text,
);
driveA('--once');
check(
  '…and machine a’s copy goes with it — the circle said it is gone, and it was never a’s',
  !((await api('GET', '/api/registry')).json ?? []).some((r) => r.repository === 'borealis'),
  (await api('GET', '/api/registry')).text,
);

// Joining again registers it afresh — the deployment keeps no tombstone of its own — and it comes back down.
run(`node "${cliBin}" connect`, borealis, { DAORIS_SERVICE_URL: HOST_B_BASE });
driveB('--once');
driveA('--once');
check(
  'joining again registers borealis afresh at the remote, and machine a has its copy back',
  ((await api('GET', '/api/registry', { base: REMOTE_BASE, key: keyA })).json ?? []).some((r) => r.repository === 'borealis')
    && ((await api('GET', '/api/registry')).json ?? []).some((r) => r.repository === 'borealis' && !('root' in r)),
  (await api('GET', '/api/registry')).text,
);
bConfig({});

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
// Every machine path at once: the registration's root, a session's transcript, and — since D51 — the
// working TREE a session held. All three live under the scratch root, so one scan answers for all of
// them; the tree gets its own check below because a new field is exactly the kind of thing that
// starts travelling without anyone noticing.
check('the remote store holds no machine path at all — root, transcript or tree',
  !remoteBytes.includes('_fixtures'),
  'a path fragment reached the remote store');
check('…and specifically not the working tree a session held',
  !remoteBytes.includes(borealis),
  'a session tree path reached the remote store');
check('…and none of the knowledge that was kept home', !remoteBytes.includes('keeps this lesson at home'),
  'unshared knowledge reached the remote store');
// The account a session ran as is the newest thing on this list, and the one most likely to be a
// person's own name (D49 §4). Byte-level like the rest, and for the same reason: three guards drop
// it — the feed has no field, the payload builder omits it, the store's mirror writes NULL — and
// "three guards" is a claim about code, while this is a claim about the artefact.
check('…and no account name a session ran as', !remoteBytes.includes('mach-b-account'),
  'a credential profile name reached the remote store');
// A quest's file is the newest machine-local thing (D65 §2): its NAME is on the remote by design, and
// its bytes — kept by machine a — must be nowhere in the store, including the ones a client sent
// straight at the shared door and were refused.
check('…and none of a quest file’s bytes, relayed or sent straight at it',
  !remoteBytes.includes('crossing marker bytes') && !remoteBytes.includes('bytes that must not land'),
  'a quest attachment’s content reached the remote store');

// -------------------------------------------------- 12. the remotes are a map

section('12. One deployment per workspace — the remotes map (D48 §5)');

// The machine's remote stopped being a single thing the moment one machine could hold two circles.
// This phase wires ONE of them and proves the other is not merely unfed but unreachable: `foundry` is
// joined AND sharing, so everything about it says "feed me" except the one field that decides where.

const auroraDb = join(scratch, 'aurora.db');
const auroraRoot = join(scratch, 'aurora-root');
mkdirSync(auroraRoot, { recursive: true });

// A workspace's host declares which workspace it is. A LOCAL host may not: it holds every circle the
// person wired, so an identity there is a claim it cannot honour — and it refuses to start rather
// than ignoring the variable, the same fail-safe inversion as the loopback rule (D47 §3).
const localWithIdentity = run(`dotnet "${httpDll}" --urls http://localhost:5195`, repoRoot, {
  DAORIS_WORKSPACE: 'aurora',
  DAORIS_KNOWLEDGE_DB: join(scratch, 'never-created.db'),
  ...NO_REMOTE,
}, 30_000);
check(
  'a local host refuses a workspace identity rather than ignoring it',
  localWithIdentity.code === 2 && /DAORIS_WORKSPACE/.test(localWithIdentity.out)
    && /shared/.test(localWithIdentity.out),
  localWithIdentity.out,
);

const mintC = run(`dotnet "${httpDll}" keys mint --name person@machine-a --days 2`, repoRoot, {
  DAORIS_KNOWLEDGE_DB: auroraDb,
});
const keyC = (mintC.out.split('\n')[0] ?? '').trim();
check('aurora’s deployment mints a key', mintC.code === 0 && keyC.startsWith('dk_'), mintC.out);

const auroraHost = await startServer({
  DAORIS_MODE: 'shared',
  DAORIS_WORKSPACE: 'aurora',
  DAORIS_KNOWLEDGE_DB: auroraDb,
  DAORIS_KNOWLEDGE_ROOT: auroraRoot,
  ASPNETCORE_URLS: AURORA_BASE,
}, AURORA_BASE);
check('aurora’s host is up, carrying its own identity', auroraHost !== null);

// The map, through the real CLI — file-local, offline, and never printing a key back (D50).
const remotesFile = join(scratch, 'remotes.json');
const wire = run(
  `node "${cliBin}" remote add aurora --url ${AURORA_BASE} --key ${keyC}`, scratch,
  { DAORIS_REMOTE_CONFIG: remotesFile });
check(
  '`daoris remote add` wires a workspace and echoes the key redacted',
  wire.code === 0 && wire.out.includes(`${keyC.slice(0, 11)}…`) && !wire.out.includes(keyC),
  wire.out,
);
const listed = run(`node "${cliBin}" remote list`, scratch, { DAORIS_REMOTE_CONFIG: remotesFile });
check(
  '…and `remote list` shows the wiring without the key',
  listed.code === 0 && /aurora/.test(listed.out) && listed.out.includes(AURORA_BASE)
    && !listed.out.includes(keyC),
  listed.out,
);
const machineStatus = run(`node "${cliBin}" status --machine`, atelier, {
  DAORIS_REMOTE_CONFIG: remotesFile,
});
check(
  '`status --machine` reports the machine’s wiring beside the repository’s declaration',
  machineStatus.code === 0 && /aurora/.test(machineStatus.out) && machineStatus.out.includes(AURORA_BASE)
    && !machineStatus.out.includes(keyC),
  machineStatus.out,
);

// Both circles now DECLARE that their material may leave — the manifest says MAY (D47 §4). Only the
// machine's map says where, and it names one circle. `lantern` joins aurora so the crossing has a
// sender the remote knows: a quest needs both sides registered where it homes.
const lantern = circleMember('lantern', 'A lamp in the aurora circle.', 'Light the batch before capping it.');
// Committed, as a reviewed declaration is: since SYNC5a a checkout with work in flight feeds no
// knowledge, because the feed speaks for a commit and the index would describe the working tree.
const declareJoin = (directory, knowledge) => {
  const declaration = JSON.parse(readFileSync(join(directory, 'daoris.json'), 'utf8'));
  declaration.remote = { join: true, knowledge };
  writeFileSync(join(directory, 'daoris.json'), `${JSON.stringify(declaration, null, 2)}\n`);
  run(`git ${GIT_ID} add -A`, directory);
  run(`git ${GIT_ID} commit -q -m "join the remote"`, directory);
};
declareJoin(atelier, true);
declareJoin(foundry, true);
declareJoin(lantern, true);
for (const [directory, workspace] of [[atelier, 'aurora'], [foundry, 'tools'], [lantern, 'aurora']]) {
  run(`node "${cliBin}" connect --workspace ${workspace}`, directory, { DAORIS_SERVICE_URL: BASE });
}

// Machine A's host restarts carrying the MAP rather than an env pair — the relay resolves a quest's
// remote by the quest's workspace (D48 §5), and it can only do that from a map.
stopHost();
check('machine a’s host restarts carrying the map', await startHost({ DAORIS_REMOTE_CONFIG: remotesFile }));
await api('POST', '/api/refresh');

const mapTick = driver({
  serviceUrl: BASE, config: driverConfig, mode: '--once',
  remote: { DAORIS_REMOTE_CONFIG: remotesFile },
});
check('the tick syncs the wired circle without a problem', mapTick.code === 0 && !/sync {2}/.test(mapTick.out), mapTick.out);

const auroraRegistry = await api('GET', '/api/registry', { base: AURORA_BASE, key: keyC });
const auroraRows = auroraRegistry.json ?? [];
check(
  'only the wired circle’s repositories reach that deployment',
  auroraRows.some((r) => r.repository === 'atelier')
    && auroraRows.some((r) => r.repository === 'lantern')
    // The decoy: joined, sharing, and in another circle. If the boundary leaks, it is here.
    && auroraRows.every((r) => r.repository !== 'foundry')
    && auroraRows.every((r) => r.repository !== 'newcomer'),
  auroraRegistry.text,
);
check(
  '…and every row landed in the workspace the deployment IS',
  auroraRows.length > 0 && auroraRows.every((r) => r.workspace === 'aurora'),
  auroraRegistry.text,
);

// A deployment that serves one workspace refuses a row declaring another — plainly, naming both.
const foreign = await api('POST', '/api/registry', {
  base: AURORA_BASE,
  key: keyC,
  body: { repository: 'foundry', workspace: 'tools', join: true, shareKnowledge: true },
});
check(
  'a registration declaring another workspace is refused, naming both sides',
  foreign.status === 409 && /aurora/.test(foreign.text) && /tools/.test(foreign.text)
    && /foundry/.test(foreign.text),
  foreign.text,
);
check(
  '…and it did not land anyway',
  ((await api('GET', '/api/registry', { base: AURORA_BASE, key: keyC })).json ?? [])
    .every((r) => r.repository !== 'foundry'),
  'a refused registration reached the deployment’s registry',
);

// The same lesson sits in both circles, word for word (phase 9 put it there). Only one crossed.
const auroraLesson = await api('GET', `/api/search?q=${encodeURIComponent('cap the batch size')}`, {
  base: AURORA_BASE, key: keyC,
});
check(
  'the wired circle’s knowledge crosses, and the other circle’s identical lesson does not',
  auroraLesson.status === 200
    && (auroraLesson.json ?? []).some((h) => h.repository === 'atelier')
    && (auroraLesson.json ?? []).every((h) => h.repository !== 'foundry'),
  auroraLesson.text.slice(0, 300),
);

// The sync resolves by the quest's workspace: an aurora quest is pushed to aurora's deployment.
const auroraQuest = await api('POST', '/api/quests', {
  body: {
    from: 'lantern',
    to: 'atelier',
    title: 'A quest inside the wired circle',
    body: 'Committed on this machine, pushed to the deployment its workspace names.',
  },
});
check(
  'a quest in the wired circle publishes through the local door',
  auroraQuest.status === 200 && auroraQuest.json?.quest?.workspace === 'aurora',
  auroraQuest.text,
);
const auroraQuestId = auroraQuest.json?.quest?.id ?? '';
driver({ serviceUrl: BASE, config: driverConfig, mode: '--once', remote: { DAORIS_REMOTE_CONFIG: remotesFile } });
const atAurora = await api('GET', '/api/quests', { base: AURORA_BASE, key: keyC });
check(
  '…and the next sync pushes it to that workspace’s deployment',
  (atAurora.json ?? []).some((q) => q.id === auroraQuestId && q.workspace === 'aurora'),
  atAurora.text,
);

// The unwired circle publishes locally and says nothing to anyone: absence is the default (D21).
const toolsQuest = await api('POST', '/api/quests', {
  body: {
    from: 'foundry',
    to: 'foundry-nobody',
    title: 'Never delivered',
    body: 'There is nobody in this circle to ask.',
  },
});
check(
  'a quest in a circle with no remote is refused locally, and reaches no deployment',
  toolsQuest.status === 400
    && ((await api('GET', '/api/quests?includeClosed=true', { base: AURORA_BASE, key: keyC })).json ?? [])
      .every((q) => q.from !== 'foundry'),
  toolsQuest.text,
);

// Unwiring is a local act: the map loses a row, the deployment loses nothing.
const unwire = run(`node "${cliBin}" remote remove aurora`, scratch, { DAORIS_REMOTE_CONFIG: remotesFile });
const afterUnwire = run(`node "${cliBin}" remote list`, scratch, { DAORIS_REMOTE_CONFIG: remotesFile });
const stillThere = await api('GET', '/api/registry', { base: AURORA_BASE, key: keyC });
check(
  '`daoris remote remove` unwires here and changes nothing at the deployment',
  unwire.code === 0 && /revoke/.test(unwire.out)
    && /no remote/i.test(afterUnwire.out)
    && (stillThere.json ?? []).some((r) => r.repository === 'atelier'),
  `${unwire.out}\n${afterUnwire.out}`,
);

// -------------------------------------------------- 13. which commit speaks

section('13. Two checkouts, one history — which one speaks (D48 §6)');

// Wholesale replacement is right — the index is derived data, and merging two machines' derivations
// would invent a second truth beside git — but only once the deployment can order what arrives
// against what it holds. These are the three rules that make it safe, driven at the door itself.
const feed = (body) => api('POST', '/api/feed/entries', { base: AURORA_BASE, key: keyC, body });
const fedEntry = (title) => ({
  kind: 'knowledge', title, body: `${title} — the body.`, relativePath: '.claude/knowledge/fed.md',
});

const fedProvenance = async (name) => ((await api('GET', '/api/repositories', {
  base: AURORA_BASE, key: keyC,
})).json ?? []).find((r) => r.name === name)?.fed;

// What the deployment actually holds for a repository, by title. Asserted through the entries door
// rather than through a search: lexical search matches on any shared word, so "no hit for X" is a
// weaker claim than it reads as — the first draft of these checks passed and failed for the wrong
// reasons because `lesson` appears in every title here.
const heldTitles = async (name) => ((await api(
  'GET', `/api/entries?repository=${encodeURIComponent(name)}`, { base: AURORA_BASE, key: keyC },
)).json ?? []).map((e) => e.title).sort();

const drivenProvenance = await fedProvenance('atelier');
check(
  'the driver’s feed named the commit it spoke for, and the deployment serves it',
  Boolean(drivenProvenance?.commit) && drivenProvenance.branch === 'main'
    && drivenProvenance.origin === 'person@machine-a',
  JSON.stringify(drivenProvenance),
);

// A newer commit replaces wholesale — which is what makes DELETE work for free: an entry absent from
// the newest canonical view is an entry the repository deleted.
//
// The times are RELATIVE to this run, and that is load-bearing: the first version hard-coded a
// wall-clock date as "newer", which was true only while the rehearsal ran before that hour. It aged
// out mid-morning and five checks went red with nothing wrong in the product — a fixture with an
// expiry date, which is the same failure as a test that only passes on one machine.
const hoursFromNow = (hours) => new Date(Date.now() + hours * 3_600_000).toISOString();
const newer = await feed({
  repository: 'atelier',
  entries: [fedEntry('The newest lesson')],
  commit: 'ffff9999eeee8888',
  committedAt: hoursFromNow(1),
  branch: 'main',
});
const afterNewer = await heldTitles('atelier');
check(
  'a feed from a newer commit replaces what is held',
  newer.status === 200 && afterNewer.includes('The newest lesson'),
  `${newer.text}\n${afterNewer.join(', ')}`,
);
check(
  '…and what the newer view no longer carries is gone — a deletion travels',
  // The driver's own feed put `shared lesson` here a phase ago; the newest canonical view does not
  // carry it, so it is deleted. That is the whole of "delete for free" (D48 §6).
  afterNewer.length === 1 && !afterNewer.includes('shared lesson'),
  afterNewer.join(', '),
);

// The flapping this exists to end: a stale checkout must not clobber a fresher one.
const stale = await feed({
  repository: 'atelier',
  entries: [fedEntry('A lesson from last week')],
  commit: '1111222233334444',
  committedAt: hoursFromNow(-48),
  branch: 'main',
});
check(
  'a feed from an older commit is refused, and says which commit it is behind',
  stale.status === 409 && /newer commit/.test(stale.text) && /ffff9999/.test(stale.text),
  stale.text,
);
check(
  '…and it is INFORMATION, not a failure — the machine behind is simply behind',
  stale.json?.information === true,
  stale.text,
);
check(
  '…and the index kept what it had',
  (await heldTitles('atelier')).join() === 'The newest lesson',
  (await heldTitles('atelier')).join(', '),
);

// Only the canonical line feeds knowledge. Records and quests still travel from any checkout —
// they are records of activity, not claims of truth.
const branchFeed = await feed({
  repository: 'atelier',
  entries: [fedEntry('An unmerged lesson')],
  commit: '5555666677778888',
  committedAt: hoursFromNow(24),
  branch: 'feature/streaming',
});
check(
  'a feed from a branch that is not the canonical line is refused, naming both',
  branchFeed.status === 409 && /feature\/streaming/.test(branchFeed.text) && /main/.test(branchFeed.text)
    && branchFeed.json?.information === true,
  branchFeed.text,
);
check(
  '…even though it is NEWER than what is held — the line matters, not only the time',
  (await heldTitles('atelier')).join() === 'The newest lesson',
  (await heldTitles('atelier')).join(', '),
);

const unnamed = await feed({ repository: 'atelier', entries: [fedEntry('From nowhere in particular')] });
check(
  'a feed that names no commit is refused — a replacement that cannot be compared is not safe',
  unnamed.status === 400 && /commit/.test(unnamed.text),
  unnamed.text,
);

const served = await fedProvenance('atelier');
check(
  'the deployment serves the commit its copy stands on — staleness a person can see',
  served?.commit === 'ffff9999eeee8888' && served?.shortCommit === 'ffff9999'
    && served?.branch === 'main' && served?.origin === 'person@machine-a',
  JSON.stringify(served),
);

// Ancestry, where it was asked (SYNC5a). The feeding machine asks git whether its commit descends
// from the one held, and names that commit as its `base`; the door checks it still holds it. Commit
// time is only the fallback for a feed that could not be ordered — the checks above.
const fastForward = await feed({
  repository: 'atelier',
  entries: [fedEntry('The fast-forwarded lesson')],
  commit: 'abab0000cdcd1111',
  // OLDER than what is held: a rebase keeps author dates and a clock can be wrong. A descendant is
  // not stale because of either.
  committedAt: hoursFromNow(-72),
  branch: 'main',
  base: 'ffff9999eeee8888',
});
check(
  'a feed based on the held commit is a fast-forward, whatever its clock says',
  fastForward.status === 200 && (await heldTitles('atelier')).join() === 'The fast-forwarded lesson',
  fastForward.text,
);

const moved = await feed({
  repository: 'atelier',
  entries: [fedEntry('A lesson checked against the past')],
  commit: 'efef2222abab3333',
  committedAt: hoursFromNow(2),
  branch: 'main',
  base: 'ffff9999eeee8888',
});
check(
  'a feed checked against a commit no longer held is refused as moved — information, not a wall',
  moved.status === 409 && moved.json?.information === true && /moved/.test(moved.text) && /abab0000/.test(moved.text),
  moved.text,
);

// SYNC0c: the same commit, read two ways, used to replace itself on every tick. The first reading
// stands; the same reading again is simply already held.
const sameCommit = (title) => feed({
  repository: 'atelier', entries: [fedEntry(title)], commit: 'abab0000cdcd1111',
  committedAt: hoursFromNow(-72), branch: 'main',
});
const readAgain = await sameCommit('The fast-forwarded lesson');
const readOtherwise = await sameCommit('The same commit, read another way');
check(
  'the same commit fed again with the same content is already held',
  readAgain.status === 200 && /already held/.test(readAgain.text),
  readAgain.text,
);
check(
  '…and read differently, the first reading stands and the second hears why, as information',
  readOtherwise.status === 409 && readOtherwise.json?.information === true
    && (await heldTitles('atelier')).join() === 'The fast-forwarded lesson',
  readOtherwise.text,
);

// The code map rides the same judgement at its own commit (MAP3b). The driver fed atelier's with its
// knowledge a phase ago — none, since atelier keeps none — and a shared deployment has no checkout,
// so what is fed is what it answers with.
const heldAtelier = (await api('GET', '/api/feed/held?repository=atelier', { base: AURORA_BASE, key: keyC })).json;
check(
  'the driver fed atelier’s code map beside its knowledge, at the commit it spoke for',
  heldAtelier?.codeMap === drivenProvenance?.commit && heldAtelier?.knowledge === 'abab0000cdcd1111',
  JSON.stringify(heldAtelier),
);
const fedMap = JSON.stringify({
  version: 1,
  modules: [
    { id: 'studio', path: 'src/Studio', summary: 'where the work is done' },
    { id: 'kiln', path: 'src/Kiln', summary: 'what fires it' },
  ],
  dependencies: [{ from: 'studio', to: 'kiln', kind: 'project' }],
});
const feedMap = (map, commit) => api('POST', '/api/feed/code-map', {
  base: AURORA_BASE, key: keyC,
  body: {
    repository: 'atelier', file: 'docs/code-map.json', map, commit,
    committedAt: hoursFromNow(3), branch: 'main', base: heldAtelier?.codeMap,
  },
});
const mapFed = await feedMap(fedMap, 'c0de0000c0de1111');
const mapAtAurora = await api('GET', '/api/code-map/atelier', { base: AURORA_BASE, key: keyC });
check(
  'a fed code map is what a deployment with no checkout answers with',
  mapFed.status === 200 && mapAtAurora.json?.file === 'docs/code-map.json'
    && (mapAtAurora.json?.modules ?? []).map((m) => m.id).join() === 'studio,kiln'
    && mapAtAurora.json?.dependencies?.[0]?.to === 'kiln',
  `${mapFed.text}\n${mapAtAurora.text}`,
);
const brokenMap = await feedMap(
  JSON.stringify({ version: 1, modules: [{ id: 'studio', path: '/srv/studio', summary: 'x' }], dependencies: [] }),
  'c0de2222c0de3333',
);
check(
  '…judged whole at the door: a map that breaks a rule is refused naming the break, and nothing of it is kept',
  brokenMap.status === 400 && /repository-relative/.test(brokenMap.text)
    && ((await api('GET', '/api/code-map/atelier', { base: AURORA_BASE, key: keyC })).json?.modules ?? [])
      .map((m) => m.id).join() === 'studio,kiln',
  brokenMap.text,
);

// The driver's half: the deployment now holds commits atelier's checkout has never seen, so git
// cannot say how its own commit stands to them — and the driver feeds nothing rather than let commit
// time replace a history it cannot see. A note, never a wall.
const unaware = driver({
  serviceUrl: BASE, config: driverConfig, mode: '--once',
  remote: { DAORIS_REMOTE_URL: AURORA_BASE, DAORIS_REMOTE_KEY: keyC, DAORIS_REMOTE_WORKSPACE: 'aurora' },
});
const heldAfter = (await api('GET', '/api/feed/held?repository=atelier', { base: AURORA_BASE, key: keyC })).json;
check(
  'a checkout that does not have the held commit feeds nothing and says a fetch would let git answer',
  unaware.code === 0 && /held {2}.*`atelier`'s knowledge is held at `abab0000`, a commit this checkout does not have/.test(unaware.out)
    && /code map is held at `c0de0000`/.test(unaware.out) && !/sync {2}/.test(unaware.out)
    && heldAfter?.knowledge === 'abab0000cdcd1111' && heldAfter?.codeMap === 'c0de0000c0de1111',
  `${unaware.out}\n${JSON.stringify(heldAfter)}`,
);

if (auroraHost && !auroraHost.killed) auroraHost.kill();
await sleep(700);
const auroraBytes = readFileSync(auroraDb, 'latin1');
check(
  'aurora’s store holds nothing from the other circle, and no machine path',
  !auroraBytes.includes('foundry') && !auroraBytes.includes('_fixtures'),
  'material from another workspace reached a deployment that never serves it',
);
check(
  '…and nothing a refused feed carried',
  !auroraBytes.includes('last week') && !auroraBytes.includes('unmerged lesson')
    && !auroraBytes.includes('checked against the past') && !auroraBytes.includes('read another way')
    && !auroraBytes.includes('/srv/studio'),
  'a refused feed left its entries in the store',
);

// -------------------------------------------------- 14. a conversation

section('14. A conversation is a session (D49 §3)');

// A chat is the entity D46 built, entered by a person instead of planned from a quest: the same
// record, the same observed lifecycle, the same one-session-per-repository lock. Driven here through
// the headless door, because a machine with no screen is still a machine (D47/D50) — and because it
// is what lets a scripted exchange gate the whole loop with no model in it.
const stubChat = join(scratch, 'stub-chat.mjs');
writeFileSync(stubChat, `
import { createInterface } from 'node:readline';
import { existsSync } from 'node:fs';

// The same two probe answers as the driven stub, and for a sharper reason: this script blocks on
// stdin, so a probe that fell through into the conversation body would hang until the driver's
// patience ran out and then report the harness as absent — refusing a chat that would have worked.
if (process.argv.includes('--version')) {
  console.log('stub-harness 1.0.0');
  process.exit(0);
}
if (process.argv.includes('--login-state')) {
  const home = process.env.DAORIS_STUB_CONFIG_DIR;
  console.log(home && existsSync(home + '/credentials.json') ? 'logged-in' : 'logged-out');
  process.exit(0);
}

const url = process.env.DAORIS_SERVICE_URL;
const repository = process.env.DAORIS_REPOSITORY;

// A conversation knows which repository it is the agent for, and where to reach the service if what
// is said turns into work worth asking for. It knows no quest: there is none.
if (process.env.DAORIS_QUEST_ID) throw new Error('a chat must carry no quest');
console.log('chat: listening in ' + repository);

for await (const line of createInterface({ input: process.stdin })) {
  if (/publish/i.test(line)) {
    // The connector path, from inside a conversation (D49 §3): work that came up while talking is
    // published as a quest like any other, never edited across.
    const response = await fetch(url + '/api/quests', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        from: repository,
        to: 'game',
        title: 'Something that came up in conversation',
        body: 'Published by a chat session through its own connector.',
      }),
    });
    console.log('chat: published — ' + response.status);
  } else {
    console.log('chat heard: ' + line);
  }
}

console.log('chat: the person stopped talking');
`);

const chatConfig = join(scratch, 'driver-chat.json');
writeFileSync(chatConfig, `${JSON.stringify({
  drivable: [], adapter: 'stub', cap: 1, timeoutMinutes: 2,
  commands: { stub: ['node', stubChat] },
}, null, 2)}\n`);

// Stdin is the person, so the exchange is a file: two messages, then end of input. That END is the
// point — it finishes the conversation rather than killing it, which is what makes the record
// `completed` instead of `stopped`.
const chatInput = join(scratch, 'chat-input.txt');
writeFileSync(chatInput, 'what is this repository for?\npublish something\n');

const chatRun = run(
  `dotnet "${driverDll}" chat --repository newcomer --adapter stub < "${chatInput}"`,
  scratch,
  { DAORIS_SERVICE_URL: BASE, DAORIS_DRIVER_CONFIG: chatConfig, ...NO_HARNESS },
  DRIVE_TIMEOUT,
);
check(
  'a conversation runs from a terminal and answers what it hears',
  chatRun.code === 0 && /chat heard: what is this repository for\?/.test(chatRun.out),
  chatRun.out,
);
check(
  '…and the session said where it was listening',
  /chat: listening in newcomer/.test(chatRun.out),
  chatRun.out,
);
check(
  '…and end of input ended it rather than cutting it off',
  /chat: the person stopped talking/.test(chatRun.out),
  chatRun.out,
);

const chatRecords = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true');
const chatRecord = (chatRecords.json ?? []).find((s) => s.kind === 'chat');
check(
  'the chat is a first-class session record, serving no quest',
  chatRecord?.state === 'completed' && !chatRecord?.quest && chatRecord?.adapter === 'stub',
  JSON.stringify(chatRecord),
);
check(
  '…with its transcript kept like any other session’s',
  Boolean(chatRecord?.transcript) && existsSync(chatRecord.transcript)
    && readFileSync(chatRecord.transcript, 'utf8').includes('chat heard:'),
  `${chatRecord?.transcript}`,
);

const fromChat = await api('GET', '/api/quests?repository=game&includeClosed=true');
check(
  'work that came up in conversation was PUBLISHED, never edited across',
  (fromChat.json ?? []).some((q) => q.from === 'newcomer'
    && q.title === 'Something that came up in conversation'),
  fromChat.text.slice(0, 300),
);

// The lock is the working tree, and it does not care which way in a session came.
const firstChat = await api('POST', '/api/sessions/chat', { body: { repository: 'newcomer', adapter: 'stub' } });
const secondChat = await api('POST', '/api/sessions/chat', { body: { repository: 'newcomer', adapter: 'stub' } });
check('a chat opens through the ordinary door', firstChat.status === 200, firstChat.text);
check(
  'a second conversation in the same TREE is refused, naming what holds it',
  secondChat.status === 409 && /newcomer/.test(secondChat.text)
    // The sentence moved with the lock (D51): the repository was never the reason, the tree was.
    && /one session per working tree/i.test(secondChat.text)
    // …and it names the holder, never the holder's PATH — a refusal is the one surface with no
    // strip on it, so a machine path in it would travel wherever the sentence travels (D47 §4).
    && !secondChat.text.includes('_fixtures'),
  secondChat.text,
);
// A fresh, OPEN quest for the same repository — the driven door refuses on quest state before it
// ever looks at the tree, so a closed one would prove the wrong refusal.
const rival = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'A quest the conversation is in the way of',
    body: 'The tree is the unit of exclusion, whoever is holding it.',
  },
});
const chatBlocksDriven = await api('POST', '/api/sessions', {
  body: { quest: rival.json?.quest?.id, adapter: 'stub' },
});
check(
  '…and so is a DRIVEN session, with the refusal saying a chat has it',
  chatBlocksDriven.status === 409 && /a chat/.test(chatBlocksDriven.text),
  chatBlocksDriven.text,
);

// …and the other half of D51, over the real door: the lock is the TREE, not the repository, so a
// session in a second tree of the same repository is not the collision the rule exists to prevent.
// Nothing creates a tree yet (that is SURF3) — this states one, which is what a driver will do.
const besideIt = await api('POST', '/api/sessions/chat', {
  body: { repository: 'newcomer', adapter: 'stub', tree: join(scratch, 'trees', 'newcomer-2') },
});
check(
  'a second tree of the same repository is not blocked, and the record names it',
  besideIt.status === 200
    && besideIt.json?.session?.tree === join(scratch, 'trees', 'newcomer-2'),
  besideIt.text,
);
await api('POST', `/api/sessions/${besideIt.json?.session?.id}/state`, { body: { state: 'stopped' } });

// Put the tree back: a queued record with no process would hold the repository forever.
await api('POST', `/api/sessions/${firstChat.json?.session?.id}/state`, { body: { state: 'stopped' } });
const freed = await api('POST', '/api/sessions/chat', { body: { repository: 'newcomer', adapter: 'stub' } });
check(
  'a finished conversation frees the repository',
  freed.status === 200,
  freed.text,
);
await api('POST', `/api/sessions/${freed.json?.session?.id}/state`, { body: { state: 'stopped' } });

const unknownChat = await api('POST', '/api/sessions/chat', { body: { repository: 'nobody', adapter: 'stub' } });
check(
  'a conversation in a repository nobody registered is refused, and says how to register it',
  unknownChat.status === 404 && /connect/.test(unknownChat.text),
  unknownChat.text,
);

// -------------------------------------------------- 15. which tool, and which account

section('15. The toolchain: which tool, and which account (D49 §4)');

// Everything here is scratch-local: the profile tree lives beside this file, so the developer's real
// ~/.daoris and their real credential directories are nowhere near this gate. The stub is a fake
// BINARY as well as a fake session — it answers `--version` and `--login-state` — which is what lets
// the whole feature be proven with no account, no credential and no model anywhere in it.
const toolchainHome = join(scratch, 'toolchain');
const harnessConfig = join(toolchainHome, 'harnesses.json');
const HARNESS_ENV = { DAORIS_HARNESS_CONFIG: harnessConfig };
const profileAt = (harness, name) => join(toolchainHome, 'harnesses', harness, name);
mkdirSync(toolchainHome, { recursive: true });

// `alpha` has been logged into — the harness put something in it, which is what a login does.
// `fresh` is a directory nobody has ever signed into.
mkdirSync(profileAt('stub', 'alpha'), { recursive: true });
writeFileSync(join(profileAt('stub', 'alpha'), 'credentials.json'), '{}\n');
mkdirSync(profileAt('stub', 'fresh'), { recursive: true });

const setProfile = (name) => writeFileSync(
  harnessConfig, `${JSON.stringify({ defaults: { stub: name }, workspaces: {} }, null, 2)}\n`);
setProfile('alpha');

const underProfile = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Run as a named account',
    body: 'The spawn should carry alpha’s configuration home, and the record should name it.',
  },
});
const profileRun = driver({ serviceUrl: BASE, config: driverConfig, harness: HARNESS_ENV, mode: '--until-idle' });
check(
  'a session spawns under the machine’s default profile',
  profileRun.code === 0 && /completed/.test(profileRun.out),
  profileRun.out,
);

const profiled = ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? [])
  .find((s) => s.quest === (underProfile.json?.quest?.id ?? ''));
check(
  'the record names the account it ran as, and the version of the tool that ran it',
  profiled?.profile === 'alpha' && profiled?.harnessVersion === 'stub-harness 1.0.0',
  JSON.stringify(profiled),
);
// The record's word is not the evidence — the SESSION's own is. The environment seam is observable,
// so the gate reads back what the spawned process actually had rather than what the driver claimed.
check(
  '…and the session really ran in that configuration home, not just in the record',
  existsSync(profiled?.transcript ?? '')
    && readFileSync(profiled.transcript, 'utf8').includes(`stub: config home ${profileAt('stub', 'alpha')}`),
  `${profiled?.transcript}`,
);
// By now the newcomer keeps a code map (committed before the crossing), so its session is asked to
// keep it current, by the file the reader would find (MAP3d, the agent producer).
check(
  'a repository that keeps a code map is asked to keep it current, by its file',
  existsSync(join(newcomer, 'docs', 'code-map.json'))
    && existsSync(profiled?.transcript ?? '')
    && readFileSync(profiled.transcript, 'utf8').includes('stub: told to keep the code map docs/code-map.json'),
  existsSync(profiled?.transcript ?? '')
    ? readFileSync(profiled.transcript, 'utf8').split('\n').filter((line) => line.startsWith('stub:')).join('\n')
    : `${profiled?.transcript}`,
);

// A profile nobody has logged into refuses BEFORE anything is recorded, and names the action.
setProfile('fresh');
const loggedOutAsk = await api('POST', '/api/quests', {
  body: {
    from: 'game', to: 'newcomer',
    title: 'Run as an account nobody signed into',
    body: 'It should be held, with the sentence that says how to fix it.',
  },
});
const sessionsBeforeRefusal =
  ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? []).length;
const loggedOutRun = driver({ serviceUrl: BASE, config: driverConfig, harness: HARNESS_ENV });
check(
  'a logged-out profile holds the start, naming the login action rather than failing bare',
  loggedOutRun.code === 0
    && /not logged in/.test(loggedOutRun.out)
    && /daoris agent login stub --profile fresh/.test(loggedOutRun.out),
  loggedOutRun.out,
);
check(
  '…and nothing was recorded, so the quest is still open and nobody’s',
  ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? []).length
    === sessionsBeforeRefusal
  && ((await api('GET', '/api/quests?repository=newcomer')).json ?? [])
    .some((q) => q.id === (loggedOutAsk.json?.quest?.id ?? '') && q.status === 'Open'),
  loggedOutRun.out,
);

// A harness that is not on this machine refuses the same way, for the same reason — the two answers
// mirror each other deliberately, and each names what fixes it.
const missingConfig = join(scratch, 'driver-missing-harness.json');
writeFileSync(missingConfig, `${JSON.stringify({
  drivable: ['newcomer'], adapter: 'stub', cap: 2, timeoutMinutes: 2,
  commands: { stub: ['daoris-no-such-harness-anywhere'] },
}, null, 2)}\n`);
const missingRun = driver({ serviceUrl: BASE, config: missingConfig, harness: HARNESS_ENV });
check(
  'a harness that is not installed holds the start, naming the install action',
  missingRun.code === 0 && /not installed on this machine/.test(missingRun.out),
  missingRun.out,
);

// An account its provider refused (AGT3b). Claude Code, measured, is silent for minutes of retries
// and then prints "Failed to authenticate. API Error: 401 …" and exits 1 — so every further session
// on that account would pay the same minutes to fail the same way. The stub mirrors those words: the
// first session ends saying so, and the NEXT start is held rather than spent. One run, one driver,
// because the hold lives with the roster the loop keeps.
setProfile('alpha');
const refusedAgent = join(scratch, 'refused-agent.mjs');
writeFileSync(refusedAgent, [
  "if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }",
  "if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }",
  "console.log('Failed to authenticate. API Error: 401 API key is invalid.');",
  'process.exit(1);',
].join('\n'));
const refusedConfig = join(scratch, 'driver-refused.json');
writeFileSync(refusedConfig, `${JSON.stringify({
  drivable: ['newcomer'], adapter: 'stub', cap: 1, timeoutMinutes: 2,
  commands: { stub: ['node', refusedAgent] },
}, null, 2)}\n`);
await api('POST', '/api/quests', {
  body: { from: 'game', to: 'newcomer', title: 'A second quest for a refused account', body: 'It must be held, not spent.' },
});
const newcomerSessions = async () =>
  ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? []).length;
const sessionsBeforeRefused = await newcomerSessions();
const refusedRun = driver({ serviceUrl: BASE, config: refusedConfig, harness: HARNESS_ENV, mode: '--until-idle' });
check(
  'an account its provider refused ends its session saying so, and the next start is held, not spent',
  refusedRun.code === 0
    && /provider refused the `stub` account `alpha` \(401\)/.test(refusedRun.out)
    && /held .*an earlier session found that its provider refused/.test(refusedRun.out)
    && (await newcomerSessions()) - sessionsBeforeRefused === 1,
  refusedRun.out,
);

// Put the machine back, and let the held quest through — a held quest that never ran would leave the
// next phase looking at a queue nobody explained.
setProfile('alpha');
const releasedRun = driver({ serviceUrl: BASE, config: driverConfig, harness: HARNESS_ENV, mode: '--until-idle' });
check(
  'logging the account in releases the queue with no restart — the file is the truth',
  releasedRun.code === 0 && /completed/.test(releasedRun.out),
  releasedRun.out,
);

// D50, from a terminal: the same directories and the same file, through the real CLI. A machine with
// no screen sets all of this up the same way the desktop's roster does.
const cliHarness = (args) => run(`node "${cliBin}" agent ${args}`, scratch, HARNESS_ENV);
const added = cliHarness('profile add claude-code work');
check(
  '`daoris agent profile add` creates the directory and says it is empty until you log in',
  added.code === 0 && existsSync(profileAt('claude-code', 'work')) && /empty until you log into it/.test(added.out),
  added.out,
);
const defaulted = cliHarness('profile default claude-code work --workspace aurora');
check(
  '`daoris agent profile default --workspace` wires one circle’s account',
  defaulted.code === 0
    && JSON.parse(readFileSync(harnessConfig, 'utf8')).workspaces?.aurora?.['claude-code'] === 'work',
  defaulted.out,
);
// The stub's own default, written by hand above, survives the CLI's edit — the CLI is an editor over
// the file, not its owner.
check(
  '…and the edit left every other wiring in the file standing',
  JSON.parse(readFileSync(harnessConfig, 'utf8')).defaults?.stub === 'alpha',
  readFileSync(harnessConfig, 'utf8'),
);
const typo = cliHarness('profile default claude-code typo');
check(
  'a default naming a profile that does not exist is refused, naming the ones that do',
  typo.code !== 0 && /has no profile `typo`/.test(typo.out) && /work/.test(typo.out),
  typo.out,
);
// Removing an account removes it (D66 §3): un-pointed everywhere, and the directory gone with it.
const removed = cliHarness('profile remove claude-code work');
check(
  '`profile remove` un-defaults it everywhere, and the directory goes with it',
  removed.code === 0 && /are gone/.test(removed.out)
    && !existsSync(profileAt('claude-code', 'work'))
    && !JSON.parse(readFileSync(harnessConfig, 'utf8')).workspaces?.aurora?.['claude-code'],
  removed.out,
);
// 🔴 A signed-in one too, sign-in and all — amending SES3's "deletes nothing", on the owner's word
// that Forget did not delete the account: the old rule kept any directory the harness would not call
// signed out, so a removed account stayed listed and signed in.
cliHarness('profile add dsh signed');
writeFileSync(join(profileAt('dsh', 'signed'), 'credentials.json'), '{}\n');
const removedSigned = cliHarness('profile remove dsh signed');
check(
  '…and so does one holding a sign-in — the account a person removes is not left on disk',
  removedSigned.code === 0 && /the sign-in in it are gone/.test(removedSigned.out)
    && !existsSync(profileAt('dsh', 'signed')),
  removedSigned.out,
);

// An account that is an API key (AGT3, D67 §1), from a terminal. The key goes in on stdin, because an
// argument is visible in the process list and the shell's history, and it comes back only as its
// last four characters. The file it lands in is the one the desktop's roster reads.
const REHEARSAL_KEY = 'sk-ant-api03-rehearsal-0000-wxyz';
const keyed = capture(`node "${cliBin}" agent key claude-code`, scratch, {
  env: HARNESS_ENV, input: `${REHEARSAL_KEY}\n`,
});
const keysFile = join(toolchainHome, 'keys.json');
check(
  '`daoris agent key` keeps an API key as an account, and says it back only as its last four',
  keyed.code === 0 && /…wxyz/.test(keyed.out) && !keyed.out.includes(REHEARSAL_KEY)
    && existsSync(keysFile)
    && Object.values(JSON.parse(readFileSync(keysFile, 'utf8'))['claude-code'] ?? {}).includes(REHEARSAL_KEY),
  keyed.out,
);

// And the driving choices themselves, from a terminal (D50): the same `driver.json` the desktop's
// checkboxes edit and the loop re-reads every tick.
const cliDriver = (args) => run(`node "${cliBin}" driver ${args}`, scratch, { DAORIS_DRIVER_CONFIG: driverConfig });
const held = cliDriver('hold newcomer');
check(
  '`daoris driver hold` pauses a repository from a terminal',
  held.code === 0 && JSON.parse(readFileSync(driverConfig, 'utf8')).holds?.includes('newcomer'),
  held.out,
);
check(
  '…and the edit preserved the adapter command the loop needs — an editor, not the file’s owner',
  JSON.parse(readFileSync(driverConfig, 'utf8')).commands?.stub?.[1] === stubAgent,
  readFileSync(driverConfig, 'utf8'),
);
const holdBites = await api('POST', '/api/quests', {
  body: { from: 'game', to: 'newcomer', title: 'Held from a terminal', body: 'The hold should bite.' },
});
const heldRunFromCli = driver({ serviceUrl: BASE, config: driverConfig, harness: HARNESS_ENV });
check(
  '…and the driver honours it on the very next tick, with no restart',
  heldRunFromCli.code === 0 && /held by the person/.test(heldRunFromCli.out),
  heldRunFromCli.out,
);
const resumed = cliDriver('resume newcomer');
const resumedRun = driver({ serviceUrl: BASE, config: driverConfig, harness: HARNESS_ENV, mode: '--until-idle' });
check(
  '`daoris driver resume` releases it, and the quest runs',
  resumed.code === 0 && resumedRun.code === 0
    && ((await api('GET', '/api/quests?repository=newcomer&includeClosed=true')).json ?? [])
      .some((q) => q.id === (holdBites.json?.quest?.id ?? '') && q.status === 'Done'),
  resumedRun.out,
);

// -------------------------------------------------- 16. the tree is the unit of exclusion

section('16. The tree is the unit of exclusion (D51/SURF3)');

// The standing opt-in, from a terminal — and the price stated where the choice is made: a fresh tree
// holds nothing git does not track. The verb is an EDITOR over the same file the loop reads, so the
// adapter command that makes the stub run must survive the edit.
const treesOn = cliDriver('trees newcomer on');
check(
  '`daoris driver trees on` opts the repository in and states the price',
  treesOn.code === 0 && /nothing git does not track/.test(treesOn.out),
  treesOn.out,
);
const editedConfig = JSON.parse(readFileSync(driverConfig, 'utf8'));
check(
  '…and the edit preserved the adapter command the loop needs',
  editedConfig.trees?.includes('newcomer') && editedConfig.commands?.stub?.[0] === 'node',
  JSON.stringify(editedConfig),
);

// The point of the whole decision, over the real loop: the ROOT IS DIRTY — the person's work in
// flight, exactly what held the driver in section 7 — and the session runs anyway, in a tree of its
// own, touching neither the file nor the root's history.
writeFileSync(join(newcomer, 'work-in-flight.txt'), 'the person is mid-edit\n');
const rootHeadBefore = run('git rev-parse HEAD', newcomer).out.trim();
const isolatedQuest = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Run beside the person, not instead of them',
    body: 'The root is dirty; the session gets a tree of its own (D51).',
  },
});
const isolatedRun = driver({ serviceUrl: BASE, config: driverConfig, harness: HARNESS_ENV, mode: '--until-idle' });
check(
  'a dirty root no longer holds the driver — the session ran in its own tree',
  isolatedRun.code === 0 && /completed/.test(isolatedRun.out) && /own tree:/.test(isolatedRun.out),
  isolatedRun.out,
);
check(
  '…and the root was not touched: the person’s file stands, and no commit landed there',
  readFileSync(join(newcomer, 'work-in-flight.txt'), 'utf8') === 'the person is mid-edit\n'
    && run('git rev-parse HEAD', newcomer).out.trim() === rootHeadBefore
    && !existsSync(join(newcomer, `answered-${isolatedQuest.json?.quest?.id}.md`)),
  run('git status --porcelain', newcomer).out,
);

const isolatedRecord = ((await api(
  'GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? [])
  .find((s) => s.quest === isolatedQuest.json?.quest?.id);
const treePath = isolatedRecord?.tree ?? '';
check(
  'the record names the session tree, under the driver’s own trees home',
  treePath.includes('trees') && !treePath.includes(newcomer)
    && /commits landed/.test(isolatedRecord?.evidence ?? ''),
  JSON.stringify(isolatedRecord),
);

// The lifecycle from a terminal (D50): the tree is listed, refuses to die holding unmerged work —
// the stub's commit is real work the canonical line has not taken — and goes when the person means it.
const treesListed = run(`dotnet "${driverDll}" trees list`, scratch, { DAORIS_DRIVER_CONFIG: driverConfig });
check(
  '`daoris-driver trees list` names the tree, its circle and its branch',
  treesListed.code === 0 && /newcomer/.test(treesListed.out) && /daoris\//.test(treesListed.out),
  treesListed.out,
);
const refusedRemove = run(
  `dotnet "${driverDll}" trees remove "${treePath}"`, scratch, { DAORIS_DRIVER_CONFIG: driverConfig });
check(
  'removal refuses while the tree holds commits the canonical line has not taken, naming them',
  refusedRemove.code === 1 && /stub: answer quest/.test(refusedRemove.out) && existsSync(treePath),
  refusedRemove.out,
);

// `connect` from inside the linked worktree is refused NAMING the main tree — a registration
// re-pointed at an ephemeral tree keeps working right up until the tree is removed.
const fromTree = run(`node "${cliBin}" connect --dry-run`, treePath, { DAORIS_SERVICE_URL: BASE });
check(
  '`daoris connect` from a linked worktree is refused, naming the main tree',
  fromTree.code === 1 && /linked worktree/.test(fromTree.out)
    && fromTree.out.replaceAll('\\', '/').includes(newcomer.replaceAll('\\', '/')),
  fromTree.out,
);

// The feed still reads only the registered root (D51 rule 1): the tree is not a repository, so the
// registry has no row for it and its content answers no search. The phrase searched for exists ONLY
// in the tree — the root's earlier stub answers share a body, so the body would match the wrong copy.
const rowsAfterTree = (await api('GET', '/api/registry')).json ?? [];
const treeOnly = await api(
  'GET', `/api/search?q=${encodeURIComponent('Run beside the person, not instead of them')}`);
check(
  'the session tree is not a repository — no registry row, and its work answers no search',
  rowsAfterTree.every((r) => !(r.root ?? '').replaceAll('\\', '/').includes('/trees/'))
    && !(treeOnly.json ?? []).some((entry) => (entry.relativePath ?? '').includes('answered-')),
  treeOnly.text,
);

const forcedRemove = run(
  `dotnet "${driverDll}" trees remove "${treePath}" --force`, scratch,
  { DAORIS_DRIVER_CONFIG: driverConfig });
check(
  '…and --force is the person meaning it: the tree and its branch are gone',
  forcedRemove.code === 0 && !existsSync(treePath)
    && !new RegExp('daoris/').test(run('git branch --list "daoris/*"', newcomer).out),
  forcedRemove.out,
);

// Leave the root as section 7 left it — later phases assume the dirty file is theirs to manage.
rmSync(join(newcomer, 'work-in-flight.txt'), { force: true });
cliDriver('trees newcomer off');

// -------------------------------------------------- 17. the protocol door

section('17. The protocol door — a session held over ACP (D53/ACP1)');

// The ACP stub: the same fake-session trick as section 7's, one door over (D46 §8). It speaks
// JSON-RPC on stdout and nothing else — every human word goes to stderr, because stdout belongs to
// the protocol — and it does REAL work through the same HTTP door its pipe-door twin uses. No model,
// no account, no credential anywhere in this phase.
const acpAgent = join(scratch, 'acp-agent.mjs');
writeFileSync(acpAgent, `
import { execSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import { createInterface } from 'node:readline';

const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\\n');
const say = (...parts) => console.error('acp-agent:', ...parts);

let nextId = 9000;
const pending = new Map();
const ask = (method, params) => new Promise((resolve) => {
  const id = nextId++;
  pending.set(id, resolve);
  send({ jsonrpc: '2.0', id, method, params });
});
const update = (sessionId, body) =>
  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId, update: body } });

const url = process.env.DAORIS_SERVICE_URL;
const id = process.env.DAORIS_QUEST_ID;
const respond = async (action, reason) => {
  const response = await fetch(url + '/api/quests/' + id + '/respond', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ action, reason }),
  });
  return { ok: response.ok, text: await response.text() };
};

async function work(sessionId) {
  update(sessionId, { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'taking quest ' + id } });

  const taken = await respond('take', null);
  if (!taken.ok) { say('take refused:', taken.text); return 'end_turn'; }

  // A tool call, with its outcome — the structured boundaries the timeline needs, which the pipe
  // door could only have supplied by parsing this agent's stdout.
  update(sessionId, { sessionUpdate: 'tool_call', toolCallId: 't1', title: 'write', status: 'in_progress' });
  // Per quest, because this agent answers more than one over a run — a fixed file with fixed content
  // left the second session with nothing to commit, and a commit that failed left the turn hanging.
  writeFileSync('acp-answer-' + id + '.md', '# answered over ACP\\n\\nThe protocol door carried quest ' + id + '.\\n');
  update(sessionId, { sessionUpdate: 'tool_call_update', toolCallId: 't1', status: 'completed' });

  // The D37 boundary, asked ON THE WIRE: an outward-facing act the repository's own configuration
  // did not pre-approve. The driver must refuse it — and the agent reports what it was told, so the
  // transcript carries the refusal rather than only the driver's side of it.
  const answer = await ask('session/request_permission', {
    sessionId,
    toolCall: { title: 'git push origin main' },
    options: [
      { optionId: 'allow', name: 'Allow', kind: 'allow_once' },
      { optionId: 'deny', name: 'Reject', kind: 'reject_once' },
    ],
  });
  const outcome = JSON.stringify(answer?.outcome ?? answer);
  update(sessionId, {
    sessionUpdate: 'agent_message_chunk',
    content: { type: 'text', text: 'permission answer was ' + outcome + ' — not pushing' },
  });

  const git = 'git -c user.name="ACP Session" -c user.email="acp@example.invalid"';
  execSync(git + ' add -A', { stdio: 'ignore' });
  execSync(git + ' commit -q -m "acp: answer quest ' + id + '"', { stdio: 'ignore' });

  const done = await respond('done', 'Landed by the ACP session.');
  say('done:', done.ok);
  return 'end_turn';
}

const lines = createInterface({ input: process.stdin });
let session = null;

// Frames are handled WITHOUT awaiting inside the reader, and that is not a style choice: the prompt's
// work asks the client for a permission decision and must keep reading while it waits for the answer.
// The first version awaited here and deadlocked — the agent held the quest and never read the reply,
// and the driver's own two-minute timeout is what reported it.
const handle = async (line) => {
  if (!line.trim()) return;
  let frame;
  try { frame = JSON.parse(line); } catch { say('not a frame:', line); return; }

  if (frame.method === undefined && frame.id !== undefined) {
    const waiting = pending.get(frame.id);
    if (waiting) { pending.delete(frame.id); waiting(frame.result ?? frame.error); }
    return;
  }

  switch (frame.method) {
    case 'initialize':
      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
      break;
    case 'session/new': {
      session = 'acp-session-1';
      say('session on', frame.params?.cwd);
      // 🔴 ACP4, reported from the agent's own side. The composed target tells this session to take
      // and close its quest over a connector, and the protocol is what hands it one — so the stub
      // says what it was offered, and the gate reads it back out of the transcript. Reported rather
      // than asserted here: a stub that refused to start would tell the gate nothing about WHY.
      const offered = (frame.params?.mcpServers ?? []).map((s) => s.name).join(', ');
      say('mcp servers offered:', offered || '(none)');
      send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: session } });
      break;
    }
    case 'session/prompt': {
      // A turn that fails is ANSWERED as a failure, the way a real agent's would be: an unanswered
      // prompt is a driver waiting on its timeout, which is a hang dressed as a session.
      try {
        const stopReason = await work(frame.params?.sessionId ?? session);
        send({ jsonrpc: '2.0', id: frame.id, result: { stopReason } });
      } catch (error) {
        say('turn failed:', error.message);
        send({ jsonrpc: '2.0', id: frame.id, error: { code: -32000, message: error.message } });
      }
      break;
    }
    case 'session/close':
      send({ jsonrpc: '2.0', id: frame.id, result: {} });
      break;
    default:
      if (frame.id !== undefined) {
        send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: 'no ' + frame.method } });
      }
  }
};

lines.on('line', (line) => { handle(line).catch((error) => say('handler failed:', error.message)); });
// stdin closed: end of input is the ending, and the process exits on its own.
lines.on('close', () => { say('input closed; exiting'); process.exit(0); });
`);

const acpConfig = join(scratch, 'driver-acp.json');
writeFileSync(acpConfig, `${JSON.stringify({
  drivable: ['newcomer'],
  adapter: 'acp-stub',
  cap: 2,
  timeoutMinutes: 2,
  commands: { 'acp-stub': ['node', acpAgent] },
}, null, 2)}\n`);

const acpAsk = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'newcomer',
    title: 'Carry this one over the protocol',
    body: 'Prove the ACP door: a session created on the tree, a prompt, updates, and a quest closed.',
  },
});
const acpQuestId = acpAsk.json?.quest?.id ?? '';

const acpRun = driver({ serviceUrl: BASE, config: acpConfig, mode: '--until-idle' });
check(
  'the driver runs a quest to done over the ACP door',
  acpRun.code === 0 && /completed/.test(acpRun.out),
  acpRun.out,
);

const acpQuests = await api('GET', '/api/quests?repository=newcomer&includeClosed=true');
check(
  'the quest was closed by the SESSION, through its own door — the wire carried the work, not the verdict',
  (acpQuests.json ?? []).some((q) => q.id === acpQuestId && q.status === 'Done'),
  acpQuests.text,
);

const acpSessions = await api('GET', '/api/sessions?repository=newcomer&includeClosed=true');
const acpRecord = (acpSessions.json ?? []).find((s) => s.quest === acpQuestId);
check(
  'the record ends completed and names the adapter that held it',
  acpRecord?.state === 'completed' && acpRecord?.adapter === 'acp-stub',
  JSON.stringify(acpRecord),
);

const acpTranscript = existsSync(acpRecord?.transcript ?? '')
  ? readFileSync(acpRecord.transcript, 'utf8')
  : '';
check(
  'the transcript holds RENDERED updates, not the wire',
  acpTranscript.includes('taking quest ' + acpQuestId)
    && /→ write/.test(acpTranscript)
    && /t1 → completed/.test(acpTranscript)
    && !acpTranscript.includes('"jsonrpc"'),
  acpTranscript.slice(0, 600),
);

// 🔴 ACP4, read back from what the AGENT said it received. The composed target instructs every
// session to take and close its quest over its own connector; the pipe door leans on the repository's
// own `.mcp.json`, which an adopted repository may not have and which the driver may never reach in
// and write. The protocol carries it instead — measured before it was built, when a real driven
// session called `take`, found no such tool and ended its turn having touched nothing.
check(
  'the session is handed the knowledge server it is told to use, over the protocol',
  /mcp servers offered: daoris-knowledge/.test(acpTranscript),
  acpTranscript.slice(0, 600),
);

// The D37 boundary, proven on the wire: the driver refused, and the agent was TOLD it was refused.
check(
  'a permission request is refused by the driver, and the refusal reaches the session',
  /permission refused/.test(acpTranscript) && /"selected"/.test(acpTranscript)
    && /not pushing/.test(acpTranscript),
  acpTranscript.slice(-800),
);

// D46 §4 on the wire: the protocol's own ending is written down and nothing more. The record above
// was concluded from the exit code and the quest's state — this line is the transcript saying so.
check(
  'the wire’s ending is recorded as a self-report, never as the verdict',
  /the turn ended: end_turn/.test(acpTranscript) && /not\s+from this line/.test(acpTranscript),
  acpTranscript.slice(-400),
);

const acpLanded = run('git log --oneline', newcomer);
check(
  'the commit the ACP session made is really in the newcomer’s history',
  new RegExp(`acp: answer quest ${acpQuestId}`).test(acpLanded.out),
  acpLanded.out,
);

// -------------------------------------------------- 17b. registered is drivable over the protocol door

section('17b. Registered is drivable over the protocol door, and the pipe door still holds it (D70/INT3)');

// A repository that registered here WITHOUT adopting: a git tree with no manifest, no lock, no
// AGENTS.md and no .mcp.json — added the way the desktop's *add* does, saying it has not adopted.
// Outside the family folder, and retired at the end, so no later phase meets it.
const unadopted = join(scratch, 'unadopted');
mkdirSync(unadopted, { recursive: true });
writeFileSync(join(unadopted, 'README.md'), '# unadopted\n\nRegistered here, never adopted.\n');
run('git init -q', unadopted);
run(`git ${GIT_ID} add -A`, unadopted);
run(`git ${GIT_ID} commit -q -m "unadopted is born"`, unadopted);

const unadoptedAdded = await api('POST', '/api/registry', {
  body: { repository: 'unadopted', root: unadopted, adopted: false },
});
const unadoptedRow = ((await api('GET', '/api/registry')).json ?? []).find((r) => r.repository === 'unadopted');
check(
  'a folder added without a manifest is registered as NOT adopted, and the host says it can be asked',
  unadoptedAdded.status === 200 && unadoptedRow?.adopted === false && unadoptedRow?.addressable === true,
  JSON.stringify(unadoptedRow ?? null) + ' ' + unadoptedAdded.text,
);

const unadoptedAsk = await api('POST', '/api/quests', {
  body: {
    from: 'game',
    to: 'unadopted',
    title: 'Answer from a tree that never adopted',
    body: 'Registered is addressable (D70): prove it end to end.',
  },
});
const unadoptedQuestId = unadoptedAsk.json?.quest?.id ?? '';
check(
  'a quest to it is published, and the asker is told only the protocol door can answer it',
  Boolean(unadoptedQuestId)
    && /has not adopted/.test(unadoptedAsk.json?.message ?? '')
    && /protocol door/.test(unadoptedAsk.json?.message ?? ''),
  unadoptedAsk.text,
);

// The pipe door keeps its own requirement: its session reaches the knowledge tools only through the
// repository's own `.mcp.json`, which the driver may never write for it (D32). So it sits, saying why.
const unadoptedPipe = join(scratch, 'driver-unadopted-pipe.json');
writeFileSync(unadoptedPipe, `${JSON.stringify({
  drivable: ['unadopted'], adapter: 'stub', cap: 1, timeoutMinutes: 2,
  commands: { stub: ['node', stubAgent] },
}, null, 2)}\n`);
const pipeHeld = driver({ serviceUrl: BASE, config: unadoptedPipe });
const unadoptedStillOpen = ((await api('GET', '/api/quests?repository=unadopted')).json ?? [])
  .some((q) => q.id === unadoptedQuestId && q.status === 'Open');
check(
  'over the pipe door it sits, and the sentence names the door that could carry it',
  pipeHeld.code === 0 && unadoptedStillOpen
    && new RegExp(`sitting\\s+#${unadoptedQuestId} → unadopted — .*only the protocol door hands it one`).test(pipeHeld.out),
  pipeHeld.out,
);

// The protocol door hands the session its connector on the wire (ACP4), so the same quest is driven
// to done — with nothing of Daoris's written into the tree to make it possible.
const unadoptedAcp = join(scratch, 'driver-unadopted-acp.json');
writeFileSync(unadoptedAcp, `${JSON.stringify({
  drivable: ['unadopted'], adapter: 'acp-stub', cap: 1, timeoutMinutes: 2,
  commands: { 'acp-stub': ['node', acpAgent] },
}, null, 2)}\n`);
const acpDrove = driver({ serviceUrl: BASE, config: unadoptedAcp, mode: '--until-idle' });
const unadoptedQuests = await api('GET', '/api/quests?repository=unadopted&includeClosed=true');
const unadoptedRecord = ((await api('GET', '/api/sessions?repository=unadopted&includeClosed=true')).json ?? [])
  .find((s) => s.quest === unadoptedQuestId);
check(
  'over the protocol door the same quest is driven to done by a session in the unadopted tree',
  acpDrove.code === 0
    && (unadoptedQuests.json ?? []).some((q) => q.id === unadoptedQuestId && q.status === 'Done')
    && unadoptedRecord?.state === 'completed' && unadoptedRecord?.adapter === 'acp-stub',
  acpDrove.out + '\n' + JSON.stringify(unadoptedRecord ?? null),
);

const unadoptedTranscript = existsSync(unadoptedRecord?.transcript ?? '')
  ? readFileSync(unadoptedRecord.transcript, 'utf8')
  : '';
check(
  'the session was handed its connector on the wire — the voice a tree with no .mcp.json has',
  /mcp servers offered: daoris-knowledge/.test(unadoptedTranscript),
  unadoptedTranscript.slice(0, 600),
);

// D32, read off the tree itself: the only change is the session's own commit.
const unadoptedStatus = run('git status --porcelain', unadopted);
const unadoptedLog = run('git log --oneline', unadopted);
check(
  'nothing of Daoris\'s was written into it — no manifest, no lock, no doctrine, no connector, a clean tree',
  ['daoris.json', 'daoris.lock', 'AGENTS.md', 'CLAUDE.md', '.mcp.json', '.claude'].every((name) => !existsSync(join(unadopted, name)))
    && unadoptedStatus.out.trim() === ''
    && new RegExp(`acp: answer quest ${unadoptedQuestId}`).test(unadoptedLog.out),
  unadoptedStatus.out + '\n' + unadoptedLog.out,
);

const unadoptedRetired = await api('DELETE', '/api/registry/unadopted');
check('it is retired again, so no later phase meets it', unadoptedRetired.status === 200, unadoptedRetired.text);

// -------------------------------------------------- 18. a plugin that declares, and speaks

section('18. Three plugins: one declares a harness, one hands a server, one holds a quest with a sentence (D64, D65)');

// A plugin is a folder under the home (D63) with a manifest. Three here, one for each thing a plugin
// can do. `rehearsal.agent` DECLARES a harness — the ACP stub agent above, as a configuration of the
// door, so a session runs on a harness this build never named; it is written here because it has to
// name that agent's scratch path. `browser` HANDS every session a server (D65 §1f) — the tracked
// example declaring the Playwright MCP, which nothing here runs: what the gate proves is that a
// declared server reaches the session over the door, in the agent's own account of what it was
// offered. `hold-by-title` SPEAKS — a process the driver starts, asks and stops. The last two are the
// TRACKED examples under `examples/plugins/`, installed with the real `daoris plugin add`, so the
// example somebody copies is the one this gate drives. No code of any of them loads anywhere.
//
// The driver's home is the directory its config sits in (the per-file override wins, D63), which in
// this rehearsal is `scratch` — so the plugins live there, and the CLI's two doors onto the same
// folder are pointed there too, for this phase only.
const PLUGIN_HOME = { DAORIS_HOME: scratch };
const agentPlugin = join(scratch, 'plugins', 'rehearsal.agent');
mkdirSync(agentPlugin, { recursive: true });
writeFileSync(join(agentPlugin, 'plugin.json'), `${JSON.stringify({
  id: 'rehearsal.agent',
  apiVersion: 1,
  name: 'Rehearsal agent',
  version: '1.0.0',
  description: 'Declares the stub ACP agent as a harness.',
  harnesses: [{ name: 'gate-agent', command: ['node', acpAgent] }],
}, null, 2)}\n`);

const pluginAdded = run(`node "${cliBin}" plugin add "${join(examplesRoot, 'plugins', 'hold-by-title')}"`, scratch, PLUGIN_HOME);
check(
  '`daoris plugin add` copies the tracked example in under its id',
  pluginAdded.code === 0 && /added plugin `hold-by-title`/.test(pluginAdded.out)
    && existsSync(join(scratch, 'plugins', 'hold-by-title', 'hooks.mjs')),
  pluginAdded.out,
);
const pluginFolder = join(scratch, 'plugins', 'hold-by-title');

const browserAdded = run(`node "${cliBin}" plugin add "${join(examplesRoot, 'plugins', 'browser')}"`, scratch, PLUGIN_HOME);
check(
  '`daoris plugin add` takes the tracked browser example, which declares a server and nothing else',
  browserAdded.code === 0 && /added plugin `browser`/.test(browserAdded.out),
  browserAdded.out,
);

// The CLI twin reads the same folder by the same rules.
const pluginList = run(`node "${cliBin}" plugin list`, scratch, PLUGIN_HOME);
check(
  '`daoris plugin list` names all three — what each declares, hands and speaks on',
  pluginList.code === 0
    && /rehearsal\.agent[\s\S]*declares gate-agent/.test(pluginList.out)
    && /browser[\s\S]*hands sessions browser/.test(pluginList.out)
    && /hold-by-title[\s\S]*speaks on quest\/consider, session\/ended/.test(pluginList.out),
  pluginList.out,
);

const harnessList = run(`node "${cliBin}" agent list`, scratch, NO_HARNESS);
check(
  '`daoris agent list` shows the declared agent beside the build\'s own, naming the plugin',
  /gate-agent/.test(harnessList.out) && /declared by plugin `rehearsal\.agent`/.test(harnessList.out),
  harnessList.out,
);

// Driven on the DECLARED harness: nothing in driver.json names a command, because the declaration
// carries it. Two quests — one the gate lets through, one it holds.
const gateConfig = join(scratch, 'driver-gate.json');
writeFileSync(gateConfig, `${JSON.stringify({
  drivable: ['newcomer'], adapter: 'gate-agent', cap: 2, timeoutMinutes: 2,
}, null, 2)}\n`);

const gatePass = await api('POST', '/api/quests', {
  body: { from: 'game', to: 'newcomer', title: 'Carry this one on a declared harness',
    body: 'A session on a harness a plugin declared: the ACP door, configured from a file.' },
});
const gatePassId = gatePass.json?.quest?.id ?? '';
const gateHeld = await api('POST', '/api/quests', {
  body: { from: 'game', to: 'newcomer', title: '[hold] Rename everything overnight',
    body: 'The gate should hold this one and say why.' },
});
const gateHeldId = gateHeld.json?.quest?.id ?? '';

const gateRun = driver({ serviceUrl: BASE, config: gateConfig, mode: '--until-idle' });
check(
  'the driver starts the speaking plugin\'s process and says what it listens on — and starts nothing for the one that only declares',
  /plugin\s+hold-by-title: started, listening on quest\/consider, session\/ended/.test(gateRun.out)
    && !/plugin\s+rehearsal\.agent: started/.test(gateRun.out),
  gateRun.out,
);
check(
  'the quest the plugin lets through runs to done on the DECLARED harness',
  gateRun.code === 0 && new RegExp(`completed[^\\n]*#${gatePassId}`).test(gateRun.out),
  gateRun.out,
);
check(
  '🔴 the quest the plugin holds sits with the PLUGIN\'S sentence — "sitting must always say why" holds for a plugin too',
  new RegExp(`sitting\\s+#${gateHeldId} → newcomer — plugin \`hold-by-title\` holds it: its title asks to be held`).test(gateRun.out),
  gateRun.out,
);

const gateRecord = ((await api('GET', '/api/sessions?repository=newcomer&includeClosed=true')).json ?? [])
  .find((s) => s.quest === gatePassId);
check(
  'the record names the declared harness, and no plugin anywhere',
  gateRecord?.state === 'completed' && gateRecord?.adapter === 'gate-agent'
    && !/rehearsal\.agent|hold-by-title|"browser"/.test(JSON.stringify(gateRecord)),
  JSON.stringify(gateRecord),
);

// 🔴 INT1, read back from what the AGENT said it received: the plugin's server rides `session/new`
// BESIDE the knowledge host, never in its place — and the browser example is proven to reach a
// session without anything running the browser.
const gateTranscript = existsSync(gateRecord?.transcript ?? '') ? readFileSync(gateRecord.transcript, 'utf8') : '';
check(
  'the session is handed the plugin\'s server beside the knowledge host, over the protocol',
  /mcp servers offered: daoris-knowledge, browser/.test(gateTranscript),
  gateTranscript.slice(0, 600),
);

const endedLog = join(scratch, 'plugins', '.data', 'hold-by-title', 'ended.log');
const endedSoFar = () => (existsSync(endedLog) ? readFileSync(endedLog, 'utf8') : '(no ended.log)');
check(
  'the ending was told to the plugin, which kept it in ITS data folder — beside the install, never in it',
  existsSync(endedLog) && new RegExp(`${gateRecord?.id} ${gatePassId} newcomer completed gate-agent`).test(endedSoFar())
    && !existsSync(join(pluginFolder, 'ended.log')),
  endedSoFar(),
);

// Disabled from the terminal: a row, never a rename. The next run does not start the process and
// does not ask it, so the held quest goes — still on the declared harness, because the other plugin
// is still on: switching one off takes nothing from another.
const disabled = run(`node "${cliBin}" plugin disable hold-by-title`, scratch, PLUGIN_HOME);
check('`daoris plugin disable` switches it off and says so', disabled.code === 0 && /is off/.test(disabled.out), disabled.out);
const listedOff = run(`node "${cliBin}" plugin list`, scratch, PLUGIN_HOME).out;
check(
  'the folder and its data stay exactly where they were',
  existsSync(join(pluginFolder, 'plugin.json')) && existsSync(endedLog) && /hold-by-title[^\n]*\(off\)/.test(listedOff),
  listedOff,
);

const gateOff = driver({ serviceUrl: BASE, config: gateConfig, mode: '--until-idle' });
check(
  'with the speaking plugin off, the held quest runs to done on the still-declared harness, and no hook process is started',
  gateOff.code === 0 && new RegExp(`completed[^\\n]*#${gateHeldId}`).test(gateOff.out)
    && !/plugin\s+hold-by-title: started/.test(gateOff.out),
  gateOff.out,
);
check(
  'a disabled plugin is told nothing — its ending log did not grow',
  existsSync(endedLog) && !endedSoFar().includes(gateHeldId),
  endedSoFar(),
);

// -------------------------------------------------- 19. report

section('19. Result');
stopEverything();
await sleep(500); // the store's file handle outlives the kill by a beat on Windows

console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
if (totals.failures) {
  console.log('  The router is NOT proven — the transcript names the first thing that broke.');
  console.log(`  Scratch left at _fixtures/family-rehearsal for inspection.`);
  process.exitCode = 1;
} else {
  console.log('  Two projects, one router — and a third born mid-run: adopted, declared, connected;');
  console.log('  a quest published, refused where it should be, taken, finished, and still there');
  console.log("  after a restart; one project's knowledge answering the other's search; a newcomer");
  console.log('  joining through the real CLI and answering its first quest on day one — then DRIVEN:');
  console.log('  a quest carrying a link and a file became a session that read both, became a commit');
  console.log('  became done, a chain of two ran as one loop with no engine, a dirty tree held, a decline');
  console.log('  carried its reason, and outside work was left entirely alone (D46, D65). Then REMOTE');
  console.log('  (D47, D68): two machines and a shared host with');
  console.log('  minted keys — a quest committed on one machine, pushed, fetched and driven to done on the');
  console.log('  other under a named account, its file known there by name and its bytes kept home, the');
  console.log('  closure crossing back with the tool version but never the account name, and the session');
  console.log('  record arriving on the first machine keyed by its origin, read-only; a machine that');
  console.log('  saw the quest taken first leaving it alone; a take claiming by push, the second of two');
  console.log('  standing down before any work; both machines taking one quest offline, the second');
  console.log('  rebased into a conflict every machine holds; a session that took offline and lost, stopped');
  console.log('  by its own driver before it landed anything; knowledge crossing only where declared; and the remote store scanned to hold no');
  console.log('  machine path, no transcript, no file’s bytes, and nothing a repository kept home. And WORKSPACES');
  console.log('  (D48): two circles on one machine, wired by `connect --workspace` and written into no');
  console.log('  tracked file — a search answering from one circle while the other held the same');
  console.log('  lesson word for word, and a quest across the boundary refused in a sentence that');
  console.log('  names both sides. And the MAP (D48 §5): one deployment per workspace, wired and');
  console.log('  unwired from a terminal with the key never printed back — only the wired circle');
  console.log('  feeding it, a joined-and-sharing repository in the other circle reaching it not at');
  console.log('  all, a registration declaring another workspace refused naming both, a quest homing');
  console.log('  at the deployment its workspace names, and a local host refusing to be one.');
  console.log('  And WHICH COMMIT SPEAKS (D48 §6): a feed carrying the point in the history it came');
  console.log('  from — a newer commit replacing wholesale and carrying a deletion with it, a stale');
  console.log('  one refused as information rather than as a failure, an unmerged branch refused');
  console.log('  though it was newer, a feed naming no commit refused outright, and the commit each');
  console.log('  copy stands on served back so staleness is seen rather than assumed. Then by');
  console.log('  ANCESTRY (SYNC5a): a feed based on the held commit taken whatever its clock said,');
  console.log('  one checked against a commit since replaced refused as moved, the same commit read');
  console.log('  twice already held and read two ways keeping the first, a code map fed, answered');
  console.log('  and judged whole at the door, and a checkout that has not seen the held commit');
  console.log('  feeding nothing and saying why. And a');
  console.log('  CONVERSATION (D49 §3): a chat held from a terminal with no model in it — answering');
  console.log('  what it heard, publishing the work that came up rather than editing across, ending');
  console.log('  on end-of-input as a first-class record with no quest and a transcript of its own,');
  console.log('  while the working tree stayed the unit of exclusion in both directions. And the');
  console.log('  TOOLCHAIN (D49 §4): a session spawned under a named credential profile — the record');
  console.log('  naming the account and the tool version, and the session itself proving it really ran');
  console.log('  in that configuration home; a profile nobody had signed into holding the start with');
  console.log('  the sentence that says how to fix it, and recording nothing; a harness that is not');
  console.log('  installed held the same way; and all of it set from a terminal, where a profile');
  console.log('  removed left its directory untouched and a hold took effect on the very next tick.');
  console.log('  And the TREE (D51): the lock keys on the working tree rather than on the repository');
  console.log('  that owns it — a second tree of one repository opened beside the first, the record');
  console.log('  named the tree it held, and that path reached the remote store no more than a');
  console.log('  transcript or an account name does. And a PLUGIN (D64): a folder under the home that');
  console.log('  declared a harness a session then ran on, and spoke — holding one quest with its own');
  console.log('  sentence, told of an ending it kept beside its install, stopped with the loop, and');
  console.log('  switched off from a terminal as a row rather than a rename.');
  rmSync(scratch, { recursive: true, force: true });
}

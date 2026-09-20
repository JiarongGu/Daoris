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
// a file that does not exist, so a real ~/.daoris/remotes.json on the developer's machine can never
// leak a real deployment into a gate run. The remote phases then opt in per process — by env pair, or
// by pointing the lookup at a map this run wrote itself.
const NO_REMOTE = { DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json') };

openTranscript(repoRoot, 'family', { beforeExit: () => stopEverything() });
const { totals, check, section } = makeChecker();

const run = (command, cwd, env = {}, timeout = 0) => capture(command, cwd, { env, timeout });

// ONE helper for every driver invocation. Three copies had already diverged three ways: one dropped
// the kill-timeout (four invocations any of which could freeze the gate), one dropped the hermetic
// guard, one grew a parameter nothing passed. The timeout is always on — a hung driver is a captured
// FAIL, never a frozen gate — and NO_REMOTE is always underneath: a phase that wants a remote opts in
// by env pair, which outranks the config-file lookup by the loader's own rule.
const DRIVE_TIMEOUT = 90_000;
const driver = ({ serviceUrl, config, remote = {}, mode = '--once' }) =>
  run(`dotnet "${driverDll}" ${mode}`, scratch, {
    DAORIS_SERVICE_URL: serviceUrl,
    DAORIS_DRIVER_CONFIG: config,
    ...NO_REMOTE,
    ...remote,
  }, DRIVE_TIMEOUT);

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

const drive = (mode = '--until-idle') => driver({ serviceUrl: BASE, config: driverConfig, mode });

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

section('11. The remote: two machines, one lock (D47)');

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
// never end — the shared helper's timeout is the backstop, but the default is what keeps a step from
// ever reaching it.)
const driveA = (mode = '--once') => driver({
  serviceUrl: BASE, config: driverConfig, mode,
  remote: { DAORIS_REMOTE_URL: REMOTE_BASE, DAORIS_REMOTE_KEY: keyA },
});
const driveB = (mode = '--once') => driver({
  serviceUrl: HOST_B_BASE, config: driverConfigB, mode,
  remote: { DAORIS_REMOTE_URL: REMOTE_BASE, DAORIS_REMOTE_KEY: keyB },
});

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
const declareJoin = (directory, knowledge) => {
  const declaration = JSON.parse(readFileSync(join(directory, 'daoris.json'), 'utf8'));
  declaration.remote = { join: true, knowledge };
  writeFileSync(join(directory, 'daoris.json'), `${JSON.stringify(declaration, null, 2)}\n`);
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

// The relay resolves by the quest's workspace: an aurora quest homes at aurora's deployment.
const auroraQuest = await api('POST', '/api/quests', {
  body: {
    from: 'lantern',
    to: 'atelier',
    title: 'A quest inside the wired circle',
    body: 'Published on this machine, homed at the deployment its workspace names.',
  },
});
check(
  'a quest in the wired circle publishes through the local door',
  auroraQuest.status === 200 && auroraQuest.json?.quest?.workspace === 'aurora',
  auroraQuest.text,
);
const auroraQuestId = auroraQuest.json?.quest?.id ?? '';
const atAurora = await api('GET', '/api/quests', { base: AURORA_BASE, key: keyC });
check(
  '…and it lives at that workspace’s deployment',
  (atAurora.json ?? []).some((q) => q.id === auroraQuestId),
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
const newer = await feed({
  repository: 'atelier',
  entries: [fedEntry('The newest lesson')],
  commit: 'ffff9999eeee8888',
  committedAt: '2026-09-21T10:00:00+00:00',
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
  committedAt: '2026-09-19T10:00:00+00:00',
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
  committedAt: '2026-09-22T10:00:00+00:00',
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
  !auroraBytes.includes('last week') && !auroraBytes.includes('unmerged lesson'),
  'a refused feed left its entries in the store',
);

// -------------------------------------------------- 14. report

section('14. Result');
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
  console.log('  a quest became a session became a commit became done, a dirty tree held, a decline');
  console.log('  carried its reason, and outside work was left entirely alone (D46). Then REMOTE (D47):');
  console.log('  two machines and a shared host with minted keys — a quest published on one machine,');
  console.log('  driven to done on the other, the closure crossing back; a raced take standing down;');
  console.log('  knowledge crossing only where declared; and the remote store scanned to hold no');
  console.log('  machine path, no transcript, and nothing a repository kept home. And WORKSPACES');
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
  console.log('  copy stands on served back so staleness is seen rather than assumed.');
  rmSync(scratch, { recursive: true, force: true });
}

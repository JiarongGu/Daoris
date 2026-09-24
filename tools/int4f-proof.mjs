/**
 * INT4f's proof — a real ask through a real intake harness (D65 §1b), over the protocol door.
 *
 * INT4b proved the intake with a stub, which publishes over HTTP. What no gate has seen is the real
 * path: a real agent in the circle's room, reading the declarations, and publishing through the MCP
 * host's `quest_publish` under the harness's own environment — which is what makes a quest asked BY
 * the ask and moves the ask's tier to `intake` (it does only for the ask's own session). That run
 * spends a login, so this script is built in ACP2's two halves, which fail differently.
 *
 *   node tools/int4f-proof.mjs                 readiness + the keyless checks
 *   node tools/int4f-proof.mjs --drive         …and then the real intake
 *   node tools/int4f-proof.mjs --drive --no-carry
 *                                              the same, with an ask that carries no link and no file
 *
 * **Readiness is not a failure.** A machine that is not set up gets the commands that set it up, and
 * exit 0. What this script must never do is look like it proved something when the login was missing.
 *
 * **Two honest endings, and the door is proven by either.** A real model decides; the proof is of the
 * door, not of the model's judgement. The expected ending is the PUBLISH: the ask reaches Published
 * with tier `intake`, and its quests are asked by `ask #<id>`. The other is the PARK: the intake said
 * the declarations do not settle it and asked the person, which is correct behaviour and leaves the
 * publish unproven — the verdict says so rather than calling it a pass for INT4f.
 *
 * 🔴 **Run it with the `CLAUDE*` environment of any enclosing agent session unset** (DRV4's note).
 * This script strips nothing: what a real deployment's driver would inherit is what it inherits.
 *
 * 🔴 **The driver's home is the folder its `driver.json` sits in** (ACP2's lesson), so the intake's room
 * and any rules live in this script's scratch folder. None are written here: Daoris's `connector`
 * default allows `quest_publish`, which is all an intake needs to publish, and it reaches an untrusted
 * room (D72, D73) — which is what this run also proves.
 */
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as sleep } from 'node:timers/promises';
import { capture, makeChecker, openTranscript } from './rehearsal-kit.mjs';
// The real resolution, not a second copy of it (TOOL2/D57): this script cannot disagree with the CLI
// and the driver about which binary would run.
import {
  harnessHome, harnessesPath, managedBinary, readHarnessSettings, resolveVersion,
} from '../src/Daoris.Cli/src/toolchain.ts';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const httpDll = join(
  repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http',
  'bin', 'Debug', 'net10.0', 'daoris-knowledge-http.dll');
const driverDll = join(
  repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Host',
  'bin', 'Debug', 'net10.0', 'daoris-driver.dll');
// The connector the intake publishes through. The driver finds it by walking up from its own binary to
// this workspace's build (`KnowledgeConnector.Candidates`) — so if it is not built, the intake has no
// voice, and the run would prove nothing about the one thing it exists to prove.
const mcpHost = join(
  repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Mcp',
  'bin', 'Debug', 'net10.0', process.platform === 'win32' ? 'daoris-knowledge.exe' : 'daoris-knowledge');

const scratch = join(repoRoot, '_fixtures', 'int4f-proof');
// Its own port: ACP2's proof holds 5201, and two proofs must be able to share a machine.
const BASE = 'http://localhost:5202';
const ADAPTER = 'claude-code-acp';
// 🔴 The adapter's Daoris name and its BINARY differ — the package ships `claude-agent-acp`.
const BINARY = 'claude-agent-acp';
const PACKAGE = '@agentclientprotocol/claude-agent-acp';
const WORKSPACE = 'default';

const drive = process.argv.includes('--drive');
const carry = !process.argv.includes('--no-carry');

// 🔴 Declared HERE, above every statement that can reach them, for acp2-proof's reason: a `const`
// further down the file sits in its temporal dead zone when the top-level run reaches it, and fails
// as "Cannot access 'CIRCLE' before initialization" — which is how this script's first drive ended.

/**
 * The circle's two repositories, each declaring a domain the other plainly does not own — so a reader
 * of the declarations can decide, which is what an intake is for. Scratch, and said to be scratch.
 */
const CIRCLE = [
  {
    name: 'proof-atlas',
    readme: 'The maps: a scratch repository born for INT4f\'s proof run. Not real work.',
    domain: {
      summary: 'The world\'s maps and level layouts, for a scratch game born for a proof run.',
      owns: ['the world map and its regions', 'level layout files'],
      accepts: ['a map or layout correction, with the region named'],
    },
  },
  {
    name: 'proof-ledger',
    readme: 'The ledger: a scratch repository born for INT4f\'s proof run. Not real work.',
    domain: {
      summary: 'Prices, currency and the shop, for a scratch game born for a proof run.',
      owns: ['item prices and the currency table', 'the shop\'s catalogue'],
      accepts: ['a price or catalogue change, with the item named'],
    },
  },
];

/**
 * The ask: plainly the ledger's, and complete in its own words — so if a refused read of its link or
 * file (outside an untrusted room) costs the intake anything, the sentence still settles it.
 */
const SENTENCE = 'The iron sword costs 40 gold in the shop, but the design says it should cost 25. '
  + 'Change its price to 25 gold.';
const LINK = 'https://tickets.example/T-41';
const FILE = { name: 'design-note.txt', text: 'Design note: the iron sword costs 25 gold.\n' };

/** The scratch host — declared above every statement that can reach it (ACP2's temporal-dead-zone lesson). */
let host = null;

openTranscript(repoRoot, 'int4f', { beforeExit: () => stopHost() });
const { totals, check, section } = makeChecker();

/** What the person still has to do — collected, and never a failure. */
const missing = [];
const needs = (what, command) => missing.push({ what, command });

/** A readiness fact: true, or true-with-a-todo. Never a failure. */
function ready(label, satisfied, detail = '') {
  console.log(satisfied
    ? `  ok    ${label}`
    : `  todo  ${label}${detail ? `\n          ${detail}` : ''}`);
  return satisfied;
}

// ─── readiness ────────────────────────────────────────────────────────────────────────────────────

section('Readiness — what this machine has, and what is left');

const built = ready('the host and the driver are built', existsSync(httpDll) && existsSync(driverDll));
if (!built) needs('the host and driver binaries', 'npm run desktop -- build');

const connectorBuilt = ready('the connector (the MCP host) is built', existsSync(mcpHost),
  'an intake publishes through it; without it the intake has no voice');
if (!connectorBuilt) needs('the MCP host', 'dotnet build src/Daoris.Service/Daoris.Service.Mcp');

const settings = readHarnessSettings(harnessesPath());
const home = harnessHome(harnessesPath());
const managed = (harness, binary) =>
  managedBinary(home, harness, resolveVersion(settings, harness, null, null), [binary]);

/** Ask a harness about itself the way `daoris agent list` does, tolerating absence. */
function present(harness, binary, args = ['--version']) {
  const where = managed(harness, binary) ?? binary;
  const found = capture(`"${where}" ${args.join(' ')}`, repoRoot, { timeout: 30_000 });
  return found.code === 0
    ? { version: found.out.trim().split('\n')[0] ?? '', where }
    : null;
}

const claude = present('claude-code', 'claude');
ready('`claude` is on this machine', Boolean(claude),
  claude ? `${claude.version}  (${claude.where})` : 'not pinned, and not on PATH');
if (!claude) {
  needs('the Claude Code CLI', 'daoris agent install claude-code   (or pin one: daoris agent pin claude-code <version>)');
}

const acpAdapter = present(ADAPTER, BINARY);
const adapterVersion = acpAdapter?.version ?? null;
ready(`\`${ADAPTER}\` is on this machine`, Boolean(adapterVersion), adapterVersion ?? 'not on PATH');
if (!adapterVersion) {
  needs(`the ACP adapter (${PACKAGE})`, `daoris agent pin ${ADAPTER} 0.79.0   — the version ACP2 was proven on`);
}

// `claude auth status` answers JSON and exits 0 either way, so the OUTPUT is the answer.
const authStatus = capture('claude auth status', repoRoot, { timeout: 30_000 });
const loggedIn = /"loggedIn"\s*:\s*true/i.test(authStatus.out);
ready('a Claude account is logged in', loggedIn,
  loggedIn ? '' : 'the profile this run would use reports logged out');
if (!loggedIn) {
  needs('a logged-in account',
    'daoris agent login claude-code [--profile <name>]   — runs the agent\'s own flow, into a directory Daoris owns');
}

// ─── the keyless half ─────────────────────────────────────────────────────────────────────────────
//
// What the real run depends on, proven without spending anything.

section('Keyless — what the intake will be handed');

// The rules an intake is handed come from the driver's home, which here holds none — so it gets
// Daoris's defaults. Read through the CLI's own door over an EMPTY home, so this answers what a
// home with no `permissions.json` hands over, whatever the machine's own home holds.
const keylessHome = join(scratch, 'keyless-home');
rmSync(keylessHome, { recursive: true, force: true });
mkdirSync(keylessHome, { recursive: true });
const rules = capture(`node "${cliBin}" agent rules`, repoRoot, { env: { DAORIS_HOME: keylessHome } });
// It is also why the room's trust does not stand in the way: the driver holds an untrusted room only
// where this allowance would not reach the session (D73).
check('an empty home hands every session the `connector` default, `quest_publish` included (D72)',
  rules.code === 0 && /connector\s+on\s+allow\s+[^\n]*mcp__daoris-knowledge__quest_publish/.test(rules.out),
  rules.out);

// ─── the real intake ──────────────────────────────────────────────────────────────────────────────

if (!drive) {
  section('The real intake');
  console.log('  --    not run. `node tools/int4f-proof.mjs --drive` runs it, and spends one login.');
}

if (drive && missing.length === 0) {
  section('The real intake — D23\'s "on proof" for the intake');
  await intakeRun();
}

// ─── the verdict ──────────────────────────────────────────────────────────────────────────────────

console.log('');
if (missing.length > 0) {
  console.log('This machine is not ready for the real run yet. What is left:');
  for (const { what, command } of missing) {
    console.log(`\n  ${what}`);
    console.log(`    ${command}`);
  }
  console.log('\nThen: node tools/int4f-proof.mjs --drive');
  console.log('\nNothing above failed — this is a readiness report, not a gate.');
}

console.log(`\n  ${totals.checks - totals.failures}/${totals.checks} checks passed`);
process.exitCode = totals.failures > 0 ? 1 : 0;

// ─── the pieces ───────────────────────────────────────────────────────────────────────────────────

function stopHost() {
  try { host?.kill(); } catch { /* already gone */ }
  host = null;
}

/** One GET against the scratch host, bounded — bare `fetch` has no timeout. */
async function ask(path) {
  try {
    const answered = await fetch(`${BASE}${path}`, { signal: AbortSignal.timeout(30_000) });
    return await answered.json();
  } catch {
    return null;
  }
}

/** A fresh git repository with one commit in it. */
function born(name, summary) {
  const where = join(scratch, name);
  mkdirSync(where, { recursive: true });
  const git = (command) => execFileSync('git', command, { cwd: where, encoding: 'utf8' });
  git(['init', '-q', '-b', 'main']);
  git(['config', 'user.email', 'proof@example.com']);
  git(['config', 'user.name', 'INT4f proof']);
  writeFileSync(join(where, 'README.md'), `# ${name}\n\n${summary}\n`);
  git(['add', '-A']);
  git(['commit', '-qm', 'the starting point']);
  return where;
}

function declare(where, domain) {
  const manifest = join(where, 'daoris.json');
  const held = JSON.parse(readFileSync(manifest, 'utf8'));
  held.domain = domain;
  writeFileSync(manifest, `${JSON.stringify(held, null, 2)}\n`);
}

/** Where a repository stands: its HEAD and whether anything is uncommitted. */
function standing(where) {
  const head = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: where, encoding: 'utf8' }).trim();
  const status = execFileSync('git', ['status', '--porcelain'], { cwd: where, encoding: 'utf8' }).trim();
  return { head, status };
}

/**
 * The intake, for real: a circle of two declared repositories, one ask that names no receiver, and one
 * `--until-idle` run of the headless driver with an intake harness named and nothing drivable — so the
 * only session that can start is the intake, and nothing needs a commit.
 */
async function intakeRun() {
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });

  const repos = CIRCLE.map((repo) => ({ ...repo, where: born(repo.name, repo.readme) }));

  // 🔴 CONFINE THE HOST TO THIS SCRATCH FAMILY (ACP2's lesson): the index and the scan stay here.
  const env = {
    DAORIS_SERVICE_URL: BASE,
    ASPNETCORE_URLS: BASE,
    DAORIS_KNOWLEDGE_ROOT: scratch,
    DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge.db'),
    DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json'),
  };

  host = spawn('dotnet', [httpDll], { cwd: scratch, env: { ...process.env, ...env }, stdio: 'ignore' });
  host.unref();
  let up = false;
  for (let attempt = 0; attempt < 40 && !up; attempt++) {
    await sleep(500);
    up = await fetch(`${BASE}/api/status`, { signal: AbortSignal.timeout(5_000) })
      .then((r) => r.ok).catch(() => false);
  }
  check('the scratch host answers', up);
  if (!up) return;

  const seen = await ask('/api/registry');
  const strangers = (seen ?? []).map((r) => r.repository).filter((n) => !n.startsWith('proof-'));
  check('the host sees this scratch family and nothing else', strangers.length === 0,
    strangers.length ? `it also indexed: ${strangers.join(', ')}` : '');
  if (strangers.length > 0) {
    console.log('        Refusing to go further: DAORIS_KNOWLEDGE_ROOT is not confining the scan.');
    return;
  }

  let joined = true;
  for (const repo of repos) {
    const adopt = capture(`node "${cliBin}" init --name ${repo.name}`, repo.where, { env });
    declare(repo.where, repo.domain);
    capture(`node "${cliBin}" sync`, repo.where, { env });
    const connect = capture(`node "${cliBin}" connect`, repo.where, { env });
    // Adopted in a commit, not left dirty — and it is the baseline "nothing was written" is read against.
    execFileSync('git', ['add', '-A'], { cwd: repo.where, encoding: 'utf8' });
    execFileSync('git', ['commit', '-qm', 'adopt daoris'], { cwd: repo.where, encoding: 'utf8' });
    const ok = adopt.code === 0 && connect.code === 0;
    check(`\`${repo.name}\` adopts, declares what it owns, and registers`, ok, `${adopt.out}\n${connect.out}`);
    joined &&= ok;
  }
  if (!joined) return;
  const before = Object.fromEntries(repos.map((repo) => [repo.name, standing(repo.where)]));

  // The ask, over the platform's own door: a sentence at the WORKSPACE, no receiver named.
  const posted = await fetch(`${BASE}/api/asks`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    signal: AbortSignal.timeout(30_000),
    body: JSON.stringify({
      workspace: WORKSPACE,
      sentence: SENTENCE,
      ...(carry
        ? {
          links: [LINK],
          attachments: [{ name: FILE.name, content: Buffer.from(FILE.text, 'utf8').toString('base64') }],
        }
        : {}),
    }),
  }).then((r) => r.json()).catch(() => ({}));
  const askId = posted.ask?.id;
  check('the ask is kept, and the declarations tier only proposes — Proposed, published nothing',
    Boolean(askId) && posted.ask?.state === 'Proposed' && posted.ask?.tier === 'declarations'
      && (posted.ask?.quests ?? []).length === 0,
    JSON.stringify(posted));
  if (!askId) return;
  console.log(`        carrying: ${carry ? `a link and \`${FILE.name}\`` : 'nothing (--no-carry)'}`);

  // Nothing drivable, so the only session that can start is the intake; cap 1 is its one slot.
  const configPath = join(scratch, 'driver.json');
  writeFileSync(configPath, `${JSON.stringify({
    drivable: [], holds: [], trees: [], cap: 1,
    adapter: ADAPTER, intakeAdapter: ADAPTER, timeoutMinutes: 10, pollSeconds: 5, notify: false,
  }, null, 2)}\n`);

  console.log('  ..    the intake runs — this is the step that spends the login');
  const run = capture(`dotnet "${driverDll}" --until-idle`, scratch, {
    env: { ...env, DAORIS_DRIVER_CONFIG: configPath },
    timeout: 15 * 60_000,
  });
  console.log(run.out.split('\n').map((line) => `        ${line}`).join('\n'));

  // ── what happened, read back from the record — never from what the session said about itself.
  const sessions = ((await ask('/api/sessions?includeClosed=true')) ?? []).filter((s) => s.ask === askId);
  const session = sessions[0];
  check('one intake session ran for the ask, on the protocol door',
    sessions.length === 1 && session.adapter === ADAPTER,
    sessions.length ? sessions.map((s) => `${s.id} ${s.adapter} ${s.state}`).join('; ') : 'no session for the ask');
  if (!session) return;

  check('…a conversation for the ask, in the circle\'s room under the driver\'s home — never a repository',
    session.kind === 'chat' && session.repository === `ask #${askId}` && !session.quest
      && /[\\/]intake[\\/]default$/.test(session.tree ?? ''),
    JSON.stringify(session));
  check('…whose record names the harness version', Boolean(session.harnessVersion), session.harnessVersion ?? 'absent');

  // What the intake reached for and was refused: evidence either way, and the first thing to read if
  // it parked. Its transcript is machine-local; only the tool lines are shown.
  const transcript = session.transcript && existsSync(session.transcript)
    ? readFileSync(session.transcript, 'utf8')
    : '';
  const refused = transcript.split('\n').filter((line) => line.includes('permission refused'));
  const published = /mcp__daoris-knowledge__quest_publish/.test(transcript);
  console.log(`        tool it published with: ${published ? 'mcp__daoris-knowledge__quest_publish' : 'none seen'}`);
  console.log(`        permission requests refused: ${refused.length}`);
  for (const line of refused.slice(0, 6)) console.log(`          ${line.trim().slice(0, 200)}`);

  const after = await ask(`/api/asks/${askId}`);
  const quests = ((await ask('/api/quests?includeClosed=true')) ?? []).filter((q) => q.from === `ask #${askId}`);

  const publishedEnding = after?.state === 'Published';
  if (publishedEnding) {
    // THE EXPECTED ENDING: the MCP host published as the ask, under the harness's own environment.
    check('ENDING — published: the ask is Published, and its tier says the INTAKE answered it',
      after.tier === 'intake' && after.intake === session.id,
      JSON.stringify({ state: after.state, tier: after.tier, intake: after.intake }));
    check('…as quests asked BY the ask, each one on its record',
      quests.length > 0 && quests.every((q) => (after.quests ?? []).includes(q.id)),
      JSON.stringify({ quests: quests.map((q) => q.id), onRecord: after.quests }));
    const circle = new Set(repos.map((repo) => repo.name));
    check('…addressed to a repository in the circle',
      quests.every((q) => circle.has(q.to)), quests.map((q) => `#${q.id} → ${q.to}`).join('; '));
    if (carry) {
      check('…carrying the ask\'s link and its file, whatever the intake wrote',
        quests.every((q) => (q.links ?? []).includes(LINK)
          && (q.attachments ?? []).some((a) => a.name === FILE.name)),
        JSON.stringify(quests.map((q) => ({ links: q.links, files: (q.attachments ?? []).map((a) => a.name) }))));
    }
    check('…and the session ended completed, observed from the ask rather than its own account',
      session.state === 'completed' && /published/.test(session.note ?? ''),
      `${session.state}: ${session.note ?? ''}`);
    // The model's judgement, said rather than asserted: the proof is of the door.
    console.log(`        it chose: ${quests.map((q) => `\`${q.to}\` — "${q.title}"`).join('; ')}`
      + ` (the declarations name \`proof-ledger\`)`);
  } else {
    // THE OTHER HONEST ENDING: it asked the person. The door worked; the publish is unproven.
    check('ENDING — parked: it published nothing and asked the person, rather than guess',
      session.state === 'awaiting-person' && /asks you rather than/.test(session.note ?? '')
        && after?.state === 'Proposed' && (after?.quests ?? []).length === 0 && quests.length === 0
        && after?.intake === session.id,
      JSON.stringify({ session: { state: session.state, note: session.note }, ask: after }));
  }

  // D32: the intake publishes and never edits. Its room is Daoris's, and no repository is touched.
  for (const repo of repos) {
    const now = standing(repo.where);
    check(`nothing was written into \`${repo.name}\``,
      now.head === before[repo.name].head && now.status === '',
      `HEAD ${before[repo.name].head.slice(0, 7)} → ${now.head.slice(0, 7)}${now.status ? `; uncommitted:\n${now.status}` : ''}`);
  }

  stopHost();

  console.log('');
  if (totals.failures > 0) return;
  if (publishedEnding) {
    console.log('  INT4f is proven: a real ask, through a real intake over the protocol door, published as the ask.');
  } else {
    console.log('  The door is proven to the intake\'s park — a real intake read the room and asked the person.');
    console.log('  The publish through the MCP host is NOT proven by this run. Read the transcript\'s last words;');
    console.log('  if a refused read of the link or file is why, `--drive --no-carry` asks with the sentence alone.');
  }
}

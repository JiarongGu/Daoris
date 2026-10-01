/**
 * The family rehearsal's set-up phase, held where it can be without a host or a driver (LAYOUT7a): the reading
 * of what `daoris-driver setup` prints, the launcher a child finds `daoris` by, and the protocol stub's set-up
 * branch, run for real in a scratch repository against a stand-in for the service's quest door.
 *
 *   node --test tools/setup-kit.test.mjs
 *
 * Not part of `npm run verify`, whose tests are the CLI package's: the rehearsal's own tests live there, and
 * this branch's lane did not reach it.
 */
import assert from 'node:assert/strict';
import { execFileSync, execSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ACP_STUB_AGENT } from './rehearsal-kit.mjs';
import {
  SETUP_RULES, SETUP_TITLE, doctrineLauncher, readSetup, withFirstOnPath, writeDoctrineLauncher,
} from './setup-kit.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const cliBin = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
const version = execFileSync(process.execPath, [cliBin, '--version'], { encoding: 'utf8' }).trim();

// What `SetupCommand` writes, spelled as it writes it: its own lines end as the console's do (CRLF on Windows,
// so both are held here), while the ask's words keep the `\n` they were composed with.
const head = [
  'setup: `atlas`, in workspace `default`',
  '  line     `main` at `0123456789ab`',
  '  layout   none · adopted no · declares no · the whole set-up',
  '  agent    acp-stub',
  '  lands    merged into its line, where the landed history is the review',
  '  tools    node v24.19.0 · daoris 0.0.1',
];
const BODY = [
  '**What is asked, and whose it is.** Take up the shared doctrine in the layout every agent reads.',
  '',
  '**The steps**, in order.',
  '',
  '1. **The tool.** Run `daoris --version`. It prints `0.0.1`. If it prints anything else, stop and decline.',
  '',
].join('\n');
const SENTENCE = `Set up this repository for every agent (2026-10-01)\n\n${BODY}`;
const rules = SETUP_RULES.map((rule) => `  ${rule}`);
const PLAN = [
  ...head,
  'a press publishes this to `atlas`, as your ask, in `default`:',
  '',
  SENTENCE,
  'and adds to `atlas`\'s rules, so its session may run the doctrine tool:',
  ...rules,
  '--plan: nothing was published, and no rule was added.',
  '',
];
const REFUSED = [
  ...head,
  'refused:',
  '  - `atlas` is not driven here: it is not opted into driving on this machine, and only a driven session carries '
    + 'a set-up. Settings → Driver, or `daoris driver drive atlas`.',
  '  - `atlas`\'s sessions run in its checkout, not a tree of their own. Give it trees on Repositories, or with '
    + '`daoris driver trees atlas on`.',
  '--plan: nothing was published, and no rule was added.',
  '',
];
const PRESSED = [
  ...head,
  'Asked as `#a1b2c3`, published to `atlas` as `#0123456789ab`.',
  '  ask    #a1b2c3',
  '  quest  #0123456789ab',
  'added to `atlas`\'s rules, so its session may run the doctrine tool:',
  ...rules,
  '  taken back in Settings → Permissions, or `daoris agent rules remove <rule> --repository atlas`.',
  '',
];

for (const [ending, eol] of [['LF', '\n'], ['CRLF', '\r\n']]) {
  test(`a plan is read back whole: the facts, the quest's words and the rule, and that it published nothing (${ending})`, () => {
    const plan = readSetup(PLAN.join(eol));
    assert.equal(plan.repository, 'atlas');
    assert.equal(plan.workspace, 'default');
    assert.equal(plan.line, 'main');
    assert.equal(plan.commit, '0123456789ab');
    assert.equal(plan.layout, 'none · adopted no · declares no · the whole set-up');
    assert.equal(plan.agent, 'acp-stub');
    assert.equal(plan.lands, 'merged into its line, where the landed history is the review');
    assert.equal(plan.node, 'v24.19.0');
    assert.equal(plan.daoris, '0.0.1');
    assert.equal(plan.title, 'Set up this repository for every agent (2026-10-01)');
    assert.match(plan.title, SETUP_TITLE);
    assert.equal(plan.body, BODY.trim());
    assert.deepEqual(plan.rules, SETUP_RULES);
    assert.deepEqual(plan.refusals, []);
    assert.equal(plan.ask, null);
    assert.equal(plan.quest, null);
    assert.equal(plan.nothingPublished, true);
  });
}

test('a refused plan is read as its refusals, each whole, with no words and no rule', () => {
  const plan = readSetup(REFUSED.join('\r\n'));
  assert.equal(plan.refusals.length, 2);
  assert.match(plan.refusals[0], /^`atlas` is not driven here: .*`daoris driver drive atlas`\.$/);
  assert.match(plan.refusals[1], /`daoris driver trees atlas on`\.$/);
  assert.equal(plan.title, null);
  assert.equal(plan.body, null);
  assert.deepEqual(plan.rules, []);
  assert.equal(plan.nothingPublished, true);
});

test('a press is read as what it published and the rules it added, and never as a plan', () => {
  const pressed = readSetup(PRESSED.join('\r\n'));
  assert.equal(pressed.ask, 'a1b2c3');
  assert.equal(pressed.quest, '0123456789ab');
  // The line saying where the rules are taken back is no rule.
  assert.deepEqual(pressed.rules, SETUP_RULES);
  assert.equal(pressed.title, null);
  assert.equal(pressed.nothingPublished, false);
});

test('what is not the command\'s output reads as nothing at all', () => {
  const nothing = readSetup('setup: name the repository to set up.\nusage: daoris-driver setup <repository> [--plan]\n');
  assert.equal(nothing.repository, null);
  assert.equal(nothing.commit, null);
  assert.equal(nothing.title, null);
  assert.deepEqual(nothing.rules, []);
  assert.deepEqual(nothing.refusals, []);
  assert.equal(nothing.nothingPublished, false);
});

test('a set-up\'s title is the whole set-up\'s words with a day, and nothing else is', () => {
  assert.match('Set up this repository for every agent (2026-10-01)', SETUP_TITLE);
  for (const other of [
    'Set up this repository for every agent',
    'Set up this repository for every agent (today)',
    'Declare and document what this repository owns (2026-10-01)',
    'Set up this repository for every agent (2026-10-01) again',
  ]) {
    assert.doesNotMatch(other, SETUP_TITLE, other);
  }
});

test('the rule is the design\'s nine exact verbs, as Bash allows, with no runner and no management verb', () => {
  assert.equal(SETUP_RULES.length, 9);
  for (const rule of SETUP_RULES) assert.match(rule, /^Bash\(daoris [^()]+\)$/);
  assert.ok(!SETUP_RULES.some((rule) => /npx|upstream|connect|import|retire/.test(rule)), SETUP_RULES.join(' '));
});

test('the launcher is a batch file on Windows and a script elsewhere, each quoting the path it runs', () => {
  const windows = doctrineLauncher('C:\\work\\100% sure\\daoris.mjs', 'win32');
  assert.equal(windows.name, 'daoris.cmd');
  assert.equal(windows.text, '@echo off\r\nnode "C:\\work\\100%% sure\\daoris.mjs" %*\r\nexit /b %ERRORLEVEL%\r\n');

  const posix = doctrineLauncher("/work/it's here/daoris.mjs", 'linux');
  assert.equal(posix.name, 'daoris');
  assert.equal(posix.text, "#!/bin/sh\nexec node '/work/it'\\''s here/daoris.mjs' \"$@\"\n");
});

test('the PATH variable keeps the spelling the environment has, with the folder first', () => {
  const sep = process.platform === 'win32' ? ';' : ':';
  assert.deepEqual(withFirstOnPath('/bin-first', { Path: 'a', OTHER: 'b' }), { Path: `/bin-first${sep}a` });
  assert.deepEqual(withFirstOnPath('/bin-first', { PATH: 'a' }), { PATH: `/bin-first${sep}a` });
  assert.deepEqual(withFirstOnPath('/bin-first', {}), { PATH: '/bin-first' });
});

/** A scratch folder under the repository's own `_fixtures/`, never the OS's temp (the doctrine's no-tmp rule). */
function scratchFolder(name) {
  const folder = join(repoRoot, '_fixtures', 'setup-kit-test', `${name}-${process.pid}`);
  rmSync(folder, { recursive: true, force: true });
  mkdirSync(folder, { recursive: true });
  return folder;
}

test('the launcher this platform writes runs the workspace\'s CLI by the bare name, from the PATH it is put on', () => {
  const folder = scratchFolder('launcher');
  const bin = join(folder, 'bin');
  const file = writeDoctrineLauncher(bin, cliBin);
  assert.ok(existsSync(file));
  // A shell, as a session's own command runs: the bare name found on PATH, nothing else.
  const printed = execSync('daoris --version', {
    cwd: folder, encoding: 'utf8', env: { ...process.env, ...withFirstOnPath(bin) },
  }).trim();
  assert.equal(printed, version);
  rmSync(folder, { recursive: true, force: true });
});

const GIT = ['-c', 'user.name=Setup Kit Test', '-c', 'user.email=setup-kit@example.invalid'];
const git = (cwd, ...args) => execFileSync('git', [...GIT, ...args], { cwd, encoding: 'utf8' });

/** A repository nobody adopted: a README and one commit on `main`. */
function unadoptedRepository(folder) {
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, 'README.md'), '# atlas\n\nServes the map tiles.\n');
  git(folder, 'init', '-q');
  git(folder, 'symbolic-ref', 'HEAD', 'refs/heads/main');
  git(folder, 'add', '-A');
  git(folder, 'commit', '-q', '-m', 'atlas is born');
  return folder;
}

/** The quest door as a stand-in: every respond it is sent, answered as the service answers a move it made. */
async function questDoor() {
  const moves = [];
  const server = createServer((request, response) => {
    let text = '';
    request.on('data', (chunk) => { text += chunk; });
    request.on('end', () => {
      const match = /^\/api\/quests\/([^/]+)\/respond$/.exec(request.url ?? '');
      if (request.method !== 'POST' || !match) {
        response.writeHead(404).end('{}');
        return;
      }
      const { action, reason } = JSON.parse(text);
      moves.push({ quest: match[1], action, reason });
      response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({ message: `${action} recorded` }));
    });
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  return { url: `http://127.0.0.1:${server.address().port}`, moves, close: () => new Promise((resolve) => server.close(resolve)) };
}

/**
 * One driven turn over the protocol, as the driver holds one: the handshake, a session on the tree, the prompt,
 * and end of input once it is answered. Resolves with the prompt's answer, every update's text and title, and stderr.
 */
async function driveSetUp({ cwd, env }) {
  const agent = join(cwd, '..', 'acp-agent.mjs');
  writeFileSync(agent, ACP_STUB_AGENT);
  const child = spawn(process.execPath, [agent], { cwd, env, stdio: ['pipe', 'pipe', 'pipe'] });
  let stderr = '';
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  const updates = [];
  const answered = new Promise((resolve) => {
    createInterface({ input: child.stdout }).on('line', (line) => {
      const frame = JSON.parse(line);
      if (frame.method === 'session/update') updates.push(frame.params.update);
      else if (frame.id === 3) resolve(frame);
    });
  });
  const exited = new Promise((resolve) => child.on('exit', (code) => resolve(code)));
  const send = (frame) => child.stdin.write(`${JSON.stringify(frame)}\n`);
  send({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1 } });
  send({ jsonrpc: '2.0', id: 2, method: 'session/new', params: { cwd, mcpServers: [] } });
  send({ jsonrpc: '2.0', id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: 'the target' }] } });
  const answer = await answered;
  child.stdin.end();
  // The exit code is not what is held: after a `fetch`, Node on Windows can end this stub on a libuv assertion
  // (0xC0000409) in `process.exit`, the ordinary quest path as much as this one, and the driver concludes a
  // session from its quest over its exit ("the quest reached done (exit …)", `Observation.Conclude`).
  await exited;
  return { answer, updates, stderr };
}

/** A body that asks for what the set-up runs, in the words `SetupBrief` uses for each, and says what the tool prints. */
const askingBody = (prints) => [
  `1. **The tool.** Run \`daoris --version\`. It prints \`${prints}\`. If it prints anything else, stop and decline.`,
  '2. **Take up the doctrine.** Run `daoris init --harness agents`, and read what it prints.',
  '3. **Read the collisions.** Run `daoris sync --dry-run`, and read every line. Then run `daoris sync`.',
  '9. **Verify.** Run `daoris sync`, `daoris check` and `daoris status --json`.',
].join('\n');

const setUpEnvironment = (door, bin, body) => ({
  ...process.env,
  ...withFirstOnPath(bin),
  DAORIS_SERVICE_URL: door.url,
  DAORIS_QUEST_ID: '0123456789ab',
  DAORIS_QUEST_TITLE: 'Set up this repository for every agent (2026-10-01)',
  DAORIS_QUEST_BODY: body,
  DAORIS_REPOSITORY: 'atlas',
});

test('a set-up quest is done as its body says: the doctrine tool by its bare name, the knowledge written, one commit, done', async () => {
  const folder = scratchFolder('stub-setup');
  const bin = join(folder, 'bin');
  writeDoctrineLauncher(bin, cliBin);
  const tree = unadoptedRepository(join(folder, 'atlas'));
  const born = git(tree, 'rev-parse', 'HEAD').trim();
  const door = await questDoor();
  try {
    const { answer, updates, stderr } = await driveSetUp({ cwd: tree, env: setUpEnvironment(door, bin, askingBody(version)) });

    assert.equal(answer.result?.stopReason, 'end_turn', JSON.stringify(answer));
    assert.deepEqual(door.moves.map((move) => move.action), ['take', 'done'], stderr);

    // Each verb ran by its bare name and the body asks for it; each went on the wire as a call, and completed.
    const ran = [...stderr.matchAll(/setup ran `(daoris [^`]+)`, which the quest asks for/g)].map((match) => match[1]);
    assert.deepEqual(ran, [
      'daoris --version', 'daoris init --harness agents', 'daoris sync --dry-run', 'daoris sync', 'daoris sync', 'daoris check',
    ], stderr);
    assert.doesNotMatch(stderr, /does NOT ask/);
    assert.match(stderr, new RegExp(`daoris --version printed ${version.replaceAll('.', '\\.')}, and the quest said it prints ${version.replaceAll('.', '\\.')}`));
    const calls = updates.filter((update) => update.sessionUpdate === 'tool_call');
    assert.deepEqual(calls.map((call) => call.title), ran);
    assert.equal(updates.filter((update) => update.sessionUpdate === 'tool_call_update' && update.status === 'completed').length, ran.length);

    // One commit on what was there, holding the doctrine on the agents layout, the domain and the knowledge.
    assert.equal(git(tree, 'rev-parse', 'HEAD~1').trim(), born);
    assert.match(git(tree, 'log', '-1', '--format=%s'), /^setup: take up the doctrine and initialise the knowledge \(quest 0123456789ab\)/);
    const manifest = JSON.parse(git(tree, 'show', 'HEAD:daoris.json'));
    assert.equal(manifest.harness, 'agents');
    assert.equal(manifest.domain.summary, 'atlas, as its README says it is.');
    assert.equal(JSON.parse(git(tree, 'show', 'HEAD:daoris.lock')).harness, 'agents');
    assert.match(git(tree, 'show', 'HEAD:AGENTS.md'), /<!-- daoris:rules/);
    assert.match(git(tree, 'show', 'HEAD:.agents/knowledge/what-this-repository-owns.md'), /^---\nname: what-this-repository-owns\n/);
    assert.equal(git(tree, 'status', '--porcelain'), '');
  } finally {
    await door.close();
    rmSync(folder, { recursive: true, force: true });
  }
});

test('a tool that answers another version than the body says is the decline the body names, and nothing is committed', async () => {
  const folder = scratchFolder('stub-decline');
  const bin = join(folder, 'bin');
  writeDoctrineLauncher(bin, cliBin);
  const tree = unadoptedRepository(join(folder, 'atlas'));
  const born = git(tree, 'rev-parse', 'HEAD').trim();
  const door = await questDoor();
  try {
    const { answer, stderr } = await driveSetUp({ cwd: tree, env: setUpEnvironment(door, bin, askingBody('9.9.9')) });

    assert.equal(answer.result?.stopReason, 'end_turn');
    assert.deepEqual(door.moves.map((move) => move.action), ['take', 'decline'], stderr);
    assert.match(door.moves[1].reason, /^The doctrine command could not run here: it printed \S+, and the quest said 9\.9\.9$/);
    assert.equal(git(tree, 'rev-parse', 'HEAD').trim(), born);
    assert.ok(!existsSync(join(tree, 'daoris.json')), 'nothing ran after the version');
  } finally {
    await door.close();
    rmSync(folder, { recursive: true, force: true });
  }
});

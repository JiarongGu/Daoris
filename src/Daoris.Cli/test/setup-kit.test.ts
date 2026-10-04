import { test } from 'node:test';
import assert from 'node:assert/strict';
import { type ChildProcess, execFileSync, execSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import type { AddressInfo } from 'node:net';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { makeFixture } from './_fixture.ts';

/**
 * The family rehearsal's set-up phase, held where it can be without a host or a driver (LAYOUT7a): the reading
 * of what `daoris-driver setup` prints, the launcher a child finds `daoris` by, and the protocol stub's set-up
 * branch, run for real in a scratch repository against a stand-in for the service's quest door. Every turn over
 * the stub also holds how it ends, with exit 0 once its input closes (STUB1).
 *
 * Workspace tooling (`tools/setup-kit.mjs`, `tools/rehearsal-kit.mjs`), tested from here for the reason
 * `desktop-tool.test.ts` states: this suite is what `npm run verify` and the release workflow already run (TEST3).
 */

/** What `readSetup` reads back. Declared here because the tool itself is untyped. */
type SetupRead = {
  repository: string | null; workspace: string | null; line: string | null; commit: string | null;
  layout: string | null; agent: string | null; lands: string | null; node: string | null; daoris: string | null;
  title: string | null; body: string | null; rules: string[]; refusals: string[];
  ask: string | null; quest: string | null; nothingPublished: boolean;
};
type Env = Record<string, string | undefined>;

// @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
const { ACP_STUB_AGENT } = await import('../../../tools/rehearsal-kit.mjs') as { ACP_STUB_AGENT: string };
const {
  SETUP_RULES, SETUP_TITLE, doctrineLauncher, readEvents, readFollowed, readRegister, readSetup, readWorkspaceSetup,
  withFirstOnPath, writeDoctrineLauncher,
  // @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
} = await import('../../../tools/setup-kit.mjs') as {
  SETUP_RULES: string[];
  SETUP_TITLE: RegExp;
  doctrineLauncher: (cliBin: string, platform?: string) => { name: string; text: string };
  // WSSETUP5a and WSSETUP6a's readers, asserted here by shape: what they read back is held by deepEqual below.
  readEvents: (out: string, event: string) => Record<string, unknown>[];
  readFollowed: (out: string) => { repository: unknown; outcome: unknown; fields: string[] }[];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- read back field by field below
  readRegister: (out: string) => any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- read back field by field below
  readWorkspaceSetup: (out: string) => any;
  readSetup: (out: string) => SetupRead;
  withFirstOnPath: (folder: string, env?: Env) => Env;
  writeDoctrineLauncher: (folder: string, cliBin: string, platform?: string) => string;
};

const cliBin = join(dirname(fileURLToPath(import.meta.url)), '..', 'bin', 'daoris.mjs');
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
    + 'a set-up. Drive it under Repositories → the repository\'s page → Setup → Driving, or with `daoris driver drive atlas`.',
  '  - `atlas`\'s sessions run in its checkout, not a tree of their own. Give it trees under Repositories → the '
    + 'repository\'s page → Setup → Driving, or with `daoris driver trees atlas on`.',
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
  '  taken back in Repositories → the repository\'s page → Setup → Reach, or `daoris agent rules remove <rule> --repository atlas`.',
  '',
];

for (const [ending, eol] of [['LF', '\n'], ['CRLF', '\r\n']] as const) {
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
    assert.match(plan.title ?? '', SETUP_TITLE);
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
  assert.match(plan.refusals[0] ?? '', /^`atlas` is not driven here: .*`daoris driver drive atlas`\.$/);
  assert.match(plan.refusals[1] ?? '', /`daoris driver trees atlas on`\.$/);
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

// What `RegisterCommand` writes (WSSETUP5), spelled as it writes it: a line per repository, two spaces, the outcome
// padded to sixteen as C#'s `{0,-16}` pads it, a space, the name, two spaces and the sentence; then the counts.
const followedLine = (outcome: string, repository: string, said: string) => `  ${outcome.padEnd(16)} ${repository}  ${said}`;
const REGISTER_SENT = [
  followedLine('registered', 'atlas',
    'registered from its line `main` at `0123456`: adopted, and declaring what it owns, with no `connect` run.'),
  followedLine('lanes-unreadable', 'engine',
    'its daoris.lanes.json on `main` at `89abcde` cannot be read: `lanes` is not a list. Nothing was registered; its row keeps what it held.'),
  'register: the index was not read again after registering: the service answered 503.',
  'register: 1 registered, 0 already as their lines say, 1 not registered, each saying why above.',
  '',
];
const REGISTER_UNCHANGED = [
  followedLine('unchanged', 'atlas', 'its row already holds what its line `main` at `0123456` declares.'),
  'register: 0 registered, 1 already as their lines say, 0 not registered.',
  '',
];

for (const [ending, eol] of [['LF', '\n'], ['CRLF', '\r\n']]) {
  test(`what \`register\` printed is read back: each repository's outcome, name and sentence, and its counts (${ending})`, () => {
    const sent = readRegister(REGISTER_SENT.join(eol));
    assert.deepEqual(sent.followed, [
      {
        outcome: 'registered', repository: 'atlas',
        said: 'registered from its line `main` at `0123456`: adopted, and declaring what it owns, with no `connect` run.',
      },
      {
        outcome: 'lanes-unreadable', repository: 'engine',
        said: 'its daoris.lanes.json on `main` at `89abcde` cannot be read: `lanes` is not a list. Nothing was registered; its row keeps what it held.',
      },
    ]);
    // The refresh's sentence starts as the counts do, and is no count.
    assert.deepEqual([sent.registered, sent.unchanged, sent.refused], [1, 0, 1]);

    const again = readRegister(REGISTER_UNCHANGED.join(eol));
    assert.deepEqual(again.followed, [
      { outcome: 'unchanged', repository: 'atlas', said: 'its row already holds what its line `main` at `0123456` declares.' },
    ]);
    assert.deepEqual([again.registered, again.unchanged, again.refused], [0, 1, 0]);
  });
}

test('what is not `register`\'s report reads as no repository and no counts', () => {
  const nothing = readRegister('register: `--repository` takes a repository\'s name.\nusage: daoris-driver register [--repository <name>]\n');
  assert.deepEqual(nothing.followed, []);
  assert.deepEqual([nothing.registered, nothing.unchanged, nothing.refused], [null, null, null]);
  const none = readRegister('register: no repository has a checkout here, so there is nothing to register.\r\n');
  assert.deepEqual(none.followed, []);
  assert.equal(none.registered, null);
});

test('the machine log\'s `registry.followed` lines are read as a name, a word and the fields each carried, and no other event', () => {
  // As `MachineLog` writes a line and `daoris-driver logs --json` prints it: one JSON object a line.
  const logged = [
    '{"time":"2026-10-01T12:00:00.000Z","source":"driver","level":"info","event":"session.started","data":{"session":"s1","repository":"atlas"}}',
    '{"time":"2026-10-01T12:00:01.000Z","source":"driver","level":"info","event":"registry.followed","data":{"repository":"atlas","outcome":"registered"}}',
    'logs: 3 lines skipped, not the log\'s shape.',
    '{"time":"2026-10-01T12:00:02.000Z","source":"driver","level":"info","event":"registry.followed","data":{"repository":"atlas","outcome":"unchanged","root":"/somewhere"}}',
    '',
  ].join('\r\n');
  assert.deepEqual(readFollowed(logged), [
    { repository: 'atlas', outcome: 'registered', fields: ['repository', 'outcome'] },
    { repository: 'atlas', outcome: 'unchanged', fields: ['repository', 'outcome', 'root'] },
  ]);
  assert.deepEqual(readFollowed(''), []);
});

test('the machine log\'s lines of one event are read as the data each carried, in the order written', () => {
  const logged = [
    '{"time":"2026-10-02T09:00:00.000Z","source":"driver","level":"info","event":"setup.planned","data":{"workspace":"meridian","repositories":2,"atOnce":1,"pilot":1}}',
    '{"time":"2026-10-02T09:00:01.000Z","source":"driver","level":"info","event":"setup.published","data":{"workspace":"meridian","repository":"beacon","quest":"q1"}}',
    'not a line of the log',
    '{"time":"2026-10-02T09:00:02.000Z","source":"driver","level":"info","event":"setup.paused","data":{"workspace":"meridian","by":"pilot"}}',
    '{"time":"2026-10-02T09:00:03.000Z","source":"driver","level":"info","event":"setup.published","data":{"workspace":"meridian","repository":"quarry","quest":"q2"}}',
    '',
  ].join('\n');
  assert.deepEqual(readEvents(logged, 'setup.published'), [
    { workspace: 'meridian', repository: 'beacon', quest: 'q1' },
    { workspace: 'meridian', repository: 'quarry', quest: 'q2' },
  ]);
  assert.deepEqual(readEvents(logged, 'setup.paused'), [{ workspace: 'meridian', by: 'pilot' }]);
  assert.deepEqual(readEvents(logged, 'setup.stopped'), []);
});

// What `WorkspaceSetupCommand` writes (WSSETUP6), spelled as it writes it: the list a press works from, with its table
// padded as C# pads it, then the pacing, the agent and the rule; a press's message and the rules it added; a steer's
// message and where the plan stands.
const WORKSPACE_LIST = [
  'setup: workspace `meridian`, 2 repositories with a checkout here, the ones other work touches first',
  `${''.padStart(3)}  ${'repository'.padEnd(10)} ${'asked'.padStart(5)}  ${'read'.padStart(4)}  ${'sessions'.padStart(8)}  now`,
  `${'1'.padStart(3)}  ${'beacon'.padEnd(10)} ${'0'.padStart(5)}  ${'0'.padStart(4)}  ${'0'.padStart(8)}  to go`,
  `${'2'.padStart(3)}  ${'quarry'.padEnd(10)} ${'3'.padStart(5)}  ${'12'.padStart(4)}  ${'1'.padStart(8)}  to go`,
  '       refused: `quarry` is not driven here: it is not opted into driving on this machine. `daoris driver drive quarry`.',
  '  at once   1 (at most 1 while the cap is 2, so other work keeps a slot)',
  '  pilot     2: once the first 2 have closed, the plan pauses until you resume it',
  '  agent     acp-stub',
];
const WORKSPACE_PLAN = [
  ...WORKSPACE_LIST,
  'a press adds to workspace `meridian`\'s rules, once, so each set-up\'s session may run the doctrine tool:',
  ...SETUP_RULES.map((rule) => `  ${rule}`),
  '--plan: nothing was written, published or added.',
  '',
];
const WORKSPACE_PRESSED = [
  ...WORKSPACE_LIST,
  'setup: the plan for workspace `meridian` is written: 2 repositories, one at a time, pausing once the first 1 have closed. '
    + 'The driver\'s loop asks the next at each look while fewer are open; with no loop running, nothing is asked until one runs.',
  'added to workspace `meridian`\'s rules, so each set-up\'s session may run the doctrine tool:',
  ...SETUP_RULES.map((rule) => `  ${rule}`),
  '  taken back in Settings → Permissions, or `daoris agent rules remove <rule> --workspace meridian`.',
  '',
];
const WORKSPACE_RESUMED = [
  'setup: the plan for workspace `meridian` carries on past its pilot.',
  'setup: workspace `meridian`, a plan made 2026-10-02: one at a time, a pilot of 1',
  `${'1'.padStart(3)}  ${'beacon'.padEnd(6)}  waiting for your review — its set-up #q1 is done and waits for your review; once it is merged, *Bring up to date* registers it.`,
  `${'2'.padStart(3)}  ${'quarry'.padEnd(6)}  to go`,
  'Setting up — 1 waiting for your review · 1 to go',
  '',
];

for (const [ending, eol] of [['LF', '\n'], ['CRLF', '\r\n']]) {
  test(`a workspace plan's list is read back: each row with what touched it and its refusals, the pacing, and the rule (${ending})`, () => {
    const plan = readWorkspaceSetup(WORKSPACE_PLAN.join(eol));
    assert.equal(plan.workspace, 'meridian');
    assert.deepEqual(plan.rows, [
      { repository: 'beacon', asked: 0, read: 0, sessions: 0, now: 'to go', refusals: [] },
      {
        repository: 'quarry', asked: 3, read: 12, sessions: 1, now: 'to go',
        refusals: ['`quarry` is not driven here: it is not opted into driving on this machine. `daoris driver drive quarry`.'],
      },
    ]);
    assert.equal(plan.atOnce, '1 (at most 1 while the cap is 2, so other work keeps a slot)');
    assert.equal(plan.pilot, '2: once the first 2 have closed, the plan pauses until you resume it');
    assert.equal(plan.agent, 'acp-stub');
    assert.deepEqual(plan.rules, SETUP_RULES);
    assert.deepEqual(plan.messages, []);
    assert.equal(plan.nothingWritten, true);
    assert.deepEqual(plan.standing, []);
    assert.equal(plan.summary, null);
  });
}

test('a workspace press is read as its message and the rules it added, never as a plan', () => {
  const pressed = readWorkspaceSetup(WORKSPACE_PRESSED.join('\r\n'));
  assert.equal(pressed.rows.length, 2);
  assert.deepEqual(pressed.messages, [
    'the plan for workspace `meridian` is written: 2 repositories, one at a time, pausing once the first 1 have closed. '
      + 'The driver\'s loop asks the next at each look while fewer are open; with no loop running, nothing is asked until one runs.',
  ]);
  // The line saying where the rules are taken back is no rule.
  assert.deepEqual(pressed.rules, SETUP_RULES);
  assert.equal(pressed.nothingWritten, false);
});

test('a steer is read as its message, where each repository stands, and the head line', () => {
  const resumed = readWorkspaceSetup(WORKSPACE_RESUMED.join('\r\n'));
  assert.equal(resumed.workspace, 'meridian');
  assert.deepEqual(resumed.messages, ['the plan for workspace `meridian` carries on past its pilot.']);
  assert.deepEqual(resumed.standing, [
    {
      repository: 'beacon', state: 'waiting for your review',
      said: 'its set-up #q1 is done and waits for your review; once it is merged, *Bring up to date* registers it.',
    },
    { repository: 'quarry', state: 'to go', said: null },
  ]);
  assert.equal(resumed.summary, 'Setting up — 1 waiting for your review · 1 to go');
  assert.deepEqual(resumed.rows, []);
  assert.deepEqual(resumed.rules, []);
});

test('what is not a workspace plan\'s output reads as nothing at all', () => {
  const nothing = readWorkspaceSetup('setup: name the workspace: `--workspace <name>`.\nusage: daoris-driver setup --workspace <name> [--plan]\n');
  assert.equal(nothing.workspace, null);
  assert.deepEqual(nothing.rows, []);
  assert.deepEqual(nothing.standing, []);
  assert.deepEqual(nothing.rules, []);
  assert.equal(nothing.nothingWritten, false);
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

test('the launcher this platform writes runs the workspace\'s CLI by the bare name, from the PATH it is put on', () => {
  const fx = makeFixture('setup-kit-launcher');
  const bin = join(fx.root, 'bin');
  const file = writeDoctrineLauncher(bin, cliBin);
  assert.ok(existsSync(file));
  // A shell, as a session's own command runs: the bare name found on PATH, nothing else.
  const printed = execSync('daoris --version', {
    cwd: fx.root, encoding: 'utf8', env: { ...process.env, ...withFirstOnPath(bin) },
  }).trim();
  assert.equal(printed, version);
  fx.cleanup();
});

const GIT = ['-c', 'user.name=Setup Kit Test', '-c', 'user.email=setup-kit@example.invalid'];
const git = (cwd: string, ...args: string[]) => execFileSync('git', [...GIT, ...args], { cwd, encoding: 'utf8' });

/** A repository nobody adopted: a README and one commit on `main`. */
function unadoptedRepository(folder: string): string {
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, 'README.md'), '# atlas\n\nServes the map tiles.\n');
  git(folder, 'init', '-q');
  git(folder, 'symbolic-ref', 'HEAD', 'refs/heads/main');
  git(folder, 'add', '-A');
  git(folder, 'commit', '-q', '-m', 'atlas is born');
  return folder;
}

type Move = { quest: string | undefined; action: string; reason: string | null };

/** The quest door as a stand-in: every respond it is sent, answered as the service answers a move it made. */
async function questDoor(): Promise<{ url: string; moves: Move[]; close: () => Promise<void> }> {
  const moves: Move[] = [];
  const server = createServer((request, response) => {
    let text = '';
    request.on('data', (chunk) => { text += chunk; });
    request.on('end', () => {
      const match = /^\/api\/quests\/([^/]+)\/respond$/.exec(request.url ?? '');
      if (request.method !== 'POST' || !match) {
        response.writeHead(404).end('{}');
        return;
      }
      const { action, reason } = JSON.parse(text) as { action: string; reason: string | null };
      moves.push({ quest: match[1], action, reason });
      response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({ message: `${action} recorded` }));
    });
  });
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  return {
    url: `http://127.0.0.1:${(server.address() as AddressInfo).port}`,
    moves,
    close: () => new Promise<void>((resolve) => server.close(() => resolve())),
  };
}

type Update = { sessionUpdate: string; title?: string; status?: string };
type Frame = { id?: number; method?: string; params?: { update: Update }; result?: { stopReason?: string } };

/**
 * How long a row waits for each thing the stub owes it, its answers and then its exit (STUB3, FLAKE1): a few seconds past
 * the slowest a row expects, so a stub that will never answer fails its row in seconds. `speak()`'s slowest is the resumed
 * turn, about 3 s; `driveTurn()`'s is a set-up turn, which runs the doctrine tool six times and takes about 7 s alone.
 */
const STUB_WAIT_MS = 10_000;
const TURN_WAIT_MS = 30_000;

/** A spawned stub, and when its process has ended with its output read to the end, as `close` says it. */
type Stub = { child: ChildProcess; closed: Promise<number | null> };

/**
 * What a row awaits of the stub, bounded (STUB3): `until` settles it, or `wait` passes first and the row fails saying what
 * never came. Either way a failure stops the stub and waits for it to end, so nothing outlives the row and its folder can
 * go (Windows holds a running process's folder), and carries its stderr.
 */
async function bounded<T>(
  { child, closed }: Stub, until: Promise<T>, { wait, late, stderr }: { wait: number; late: () => string; stderr: () => string },
): Promise<T> {
  let timer: NodeJS.Timeout | undefined;
  const bound = new Promise<never>((_, reject) => {
    timer = setTimeout(() => reject(new Error(`${late()} within ${wait} ms`)), wait);
  });
  try {
    return await Promise.race([until, bound]);
  } catch (error) {
    child.kill();
    await closed;
    throw new Error(`${(error as Error).message}\n${stderr()}`);
  } finally {
    clearTimeout(timer);
  }
}

/**
 * One driven turn over the protocol, as the driver holds one: the handshake, a session on the tree, the prompt,
 * a permission the stub asks for refused (the driver refuses one by construction, D52), and end of input once
 * the prompt is answered. Resolves with the prompt's answer, every update, stderr, and the code the stub exited with.
 */
async function driveTurn({ cwd, env }: { cwd: string; env: Env }): Promise<{ answer: Frame; updates: Update[]; stderr: string; code: number | null }> {
  const agent = join(cwd, '..', 'acp-agent.mjs');
  writeFileSync(agent, ACP_STUB_AGENT);
  const child = spawn(process.execPath, [agent], { cwd, env, stdio: ['pipe', 'pipe', 'pipe'] });
  let stderr = '';
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  const send = (frame: object) => child.stdin.write(`${JSON.stringify(frame)}\n`);
  const updates: Update[] = [];
  // Bounded as `speak()` is (STUB3): the prompt's answer, then the exit, each failing the row where it never comes.
  const closed = new Promise<number | null>((resolve) => child.on('close', (code) => resolve(code)));
  const answered = new Promise<Frame>((resolve, reject) => {
    createInterface({ input: child.stdout }).on('line', (line) => {
      const frame = JSON.parse(line) as Frame;
      if (frame.method === 'session/update' && frame.params) updates.push(frame.params.update);
      else if (frame.method === 'session/request_permission') {
        send({ jsonrpc: '2.0', id: frame.id, result: { outcome: { outcome: 'selected', optionId: 'deny' } } });
      } else if (frame.id === 3) resolve(frame);
    });
    void closed.then((code) => reject(new Error(`the stub exited with code ${code} and never answered 3 (session/prompt)`)));
  });
  send({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1 } });
  send({ jsonrpc: '2.0', id: 2, method: 'session/new', params: { cwd, mcpServers: [] } });
  send({ jsonrpc: '2.0', id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: 'the target' }] } });
  const stub = { child, closed };
  const told = { wait: TURN_WAIT_MS, stderr: () => stderr };
  const answer = await bounded(stub, answered, { late: () => 'the stub never answered 3 (session/prompt)', ...told });
  child.stdin.end();
  const code = await bounded(stub, closed, { late: () => 'the stub did not exit once its input closed', ...told });
  return { answer, updates, stderr, code };
}

/** A body that asks for what the set-up runs, in the words `SetupBrief` uses for each, and says what the tool prints. */
const askingBody = (prints: string) => [
  `1. **The tool.** Run \`daoris --version\`. It prints \`${prints}\`. If it prints anything else, stop and decline.`,
  '2. **Take up the doctrine.** Run `daoris init --harness agents`, and read what it prints.',
  '3. **Read the collisions.** Run `daoris sync --dry-run`, and read every line. Then run `daoris sync`.',
  '9. **Verify.** Run `daoris sync`, `daoris check` and `daoris status --json`.',
].join('\n');

const setUpEnvironment = (door: { url: string }, bin: string, body: string): Env => ({
  ...process.env,
  ...withFirstOnPath(bin),
  DAORIS_SERVICE_URL: door.url,
  DAORIS_QUEST_ID: '0123456789ab',
  DAORIS_QUEST_TITLE: 'Set up this repository for every agent (2026-10-01)',
  DAORIS_QUEST_BODY: body,
  DAORIS_REPOSITORY: 'atlas',
});

test('a set-up quest is done as its body says: the doctrine tool by its bare name, the knowledge written, one commit, done', async () => {
  const fx = makeFixture('setup-kit-stub-setup');
  const bin = join(fx.root, 'bin');
  writeDoctrineLauncher(bin, cliBin);
  const tree = unadoptedRepository(join(fx.root, 'atlas'));
  const born = git(tree, 'rev-parse', 'HEAD').trim();
  const door = await questDoor();
  try {
    const { answer, updates, stderr, code } = await driveTurn({ cwd: tree, env: setUpEnvironment(door, bin, askingBody(version)) });

    assert.equal(answer.result?.stopReason, 'end_turn', JSON.stringify(answer));
    assert.deepEqual(door.moves.map((move) => move.action), ['take', 'done'], stderr);
    assert.equal(code, 0, `end of input is the ending, after the quest's fetches too (STUB1)\n${stderr}`);

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
    fx.cleanup();
  }
});

test('a tool that answers another version than the body says is the decline the body names, and nothing is committed', async () => {
  const fx = makeFixture('setup-kit-stub-decline');
  const bin = join(fx.root, 'bin');
  writeDoctrineLauncher(bin, cliBin);
  const tree = unadoptedRepository(join(fx.root, 'atlas'));
  const born = git(tree, 'rev-parse', 'HEAD').trim();
  const door = await questDoor();
  try {
    const { answer, stderr, code } = await driveTurn({ cwd: tree, env: setUpEnvironment(door, bin, askingBody('9.9.9')) });

    assert.equal(answer.result?.stopReason, 'end_turn');
    assert.deepEqual(door.moves.map((move) => move.action), ['take', 'decline'], stderr);
    assert.equal(code, 0, `end of input is the ending, after the quest's fetches too (STUB1)\n${stderr}`);
    assert.match(door.moves[1]?.reason ?? '', /^The doctrine command could not run here: it printed \S+, and the quest said 9\.9\.9$/);
    assert.equal(git(tree, 'rev-parse', 'HEAD').trim(), born);
    assert.ok(!existsSync(join(tree, 'daoris.json')), 'nothing ran after the version');
  } finally {
    await door.close();
    fx.cleanup();
  }
});

/** How many turns the ending is held over: it failed some turns and not others, so one proves little (STUB1). */
const ENDINGS = 6;

test('the stub\'s ordinary quest turn ends with exit 0 once its input closes, after its fetches, every time (STUB1)', async () => {
  const fx = makeFixture('setup-kit-stub-ending');
  const door = await questDoor();
  try {
    for (let turn = 1; turn <= ENDINGS; turn++) {
      const tree = unadoptedRepository(join(fx.root, `atlas-${turn}`));
      const { answer, stderr, code } = await driveTurn({
        cwd: tree,
        env: { ...process.env, DAORIS_SERVICE_URL: door.url, DAORIS_QUEST_ID: `quest-${turn}`, DAORIS_QUEST_TITLE: 'Answer the map tiles' },
      });
      assert.equal(answer.result?.stopReason, 'end_turn', `turn ${turn}: ${JSON.stringify(answer)}`);
      assert.match(git(tree, 'log', '-1', '--format=%s'), new RegExp(`^acp: answer quest quest-${turn}`), `turn ${turn}`);
      assert.equal(code, 0, `turn ${turn} exited ${code}${code === 0xC0000409 ? ' (0xC0000409)' : ''}\n${stderr}`);
    }
    assert.deepEqual(door.moves.map((move) => `${move.quest} ${move.action}`),
      Array.from({ length: ENDINGS }, (_, at) => [`quest-${at + 1} take`, `quest-${at + 1} done`]).flat());
  } finally {
    await door.close();
    fx.cleanup();
  }
});

type Said = { id: number; method: string; params: object };
type Heard = { id?: number; result?: { stopReason?: string; sessionId?: string; agentCapabilities?: unknown } };

/**
 * One process of the stub, spoken to as the driver speaks to one: the frames sent in order, a permission it asks for
 * refused (D52), and end of input once every frame is answered. Resolves with each answer by its id and when it came (in
 * `performance.now()` milliseconds), the text of every update, stderr, and the code the stub exited with. `agent` is the
 * stub's text, ACP's stub unless a row cuts one short.
 *
 * Each wait is bounded (STUB3): a stub that exits before answering fails the row as it exits, one that stays silent fails
 * it at `wait`, and one that does not exit once its input closes fails it there too, each naming what never came.
 */
async function speak({ cwd, env, frames, agent: text = ACP_STUB_AGENT, wait = STUB_WAIT_MS }: {
  cwd: string; env: Env; frames: Said[]; agent?: string; wait?: number;
}): Promise<{
  answers: Map<number, Heard>; at: Map<number, number>; texts: string[]; stderr: string; code: number | null;
}> {
  const agent = join(cwd, '..', 'acp-agent.mjs');
  writeFileSync(agent, text);
  const child = spawn(process.execPath, [agent], { cwd, env, stdio: ['pipe', 'pipe', 'pipe'] });
  let stderr = '';
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  const send = (frame: object) => child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', ...frame })}\n`);
  const answers = new Map<number, Heard>();
  const at = new Map<number, number>();
  const texts: string[] = [];
  const missing = () => frames.filter((said) => !answers.has(said.id)).map((said) => `${said.id} (${said.method})`).join(', ');
  // `close` comes once its output is read to the end, so every answer it gave is counted before one is called missing.
  const closed = new Promise<number | null>((resolve) => child.on('close', (code) => resolve(code)));
  const answered = new Promise<void>((resolve, reject) => {
    createInterface({ input: child.stdout }).on('line', (line) => {
      const frame = JSON.parse(line) as Heard & { method?: string; params?: { update?: { content?: { text?: string } } } };
      if (frame.method === 'session/update') texts.push(frame.params?.update?.content?.text ?? '');
      else if (frame.method === 'session/request_permission') {
        send({ id: frame.id, result: { outcome: { outcome: 'selected', optionId: 'deny' } } });
      } else if (frame.id !== undefined && frames.some((said) => said.id === frame.id)) {
        answers.set(frame.id, frame);
        at.set(frame.id, performance.now());
        if (answers.size === frames.length) resolve();
      }
    });
    void closed.then((code) => reject(new Error(`the stub exited with code ${code} and never answered ${missing()}`)));
  });
  for (const frame of frames) send(frame);
  const stub = { child, closed };
  const told = { wait, stderr: () => stderr };
  await bounded(stub, answered, { late: () => `the stub never answered ${missing()}`, ...told });
  child.stdin.end();
  const code = await bounded(stub, closed, { late: () => 'the stub did not exit once its input closed', ...told });
  return { answers, at, texts, stderr, code };
}

/**
 * ANSWER1b (D131 §1): the stub resumes a conversation, as the family rehearsal's answer phase needs it to. A quest that
 * asks the person first is taken, asks which port and ends its turn holding the quest, doing nothing else. The next
 * process, as the driver's next look starts one, is sent `session/resume` on the conversation the first one named,
 * never `session/new`, and its prompt is the person's answer: the report is served where it says, committed, and done.
 */
test('the stub resumes the conversation it is asked to, and the person\'s answer is the prompt it does the work with (ANSWER1b)', async () => {
  const fx = makeFixture('setup-kit-stub-resume');
  const tree = unadoptedRepository(join(fx.root, 'atlas'));
  const born = git(tree, 'rev-parse', 'HEAD').trim();
  const door = await questDoor();
  const env: Env = {
    ...process.env,
    DAORIS_SERVICE_URL: door.url,
    DAORIS_QUEST_ID: 'quest-port',
    DAORIS_QUEST_TITLE: 'Ask the person first: which port should the report listen on?',
  };
  try {
    const asking = await speak({
      cwd: tree, env, frames: [
        { id: 1, method: 'initialize', params: { protocolVersion: 1 } },
        { id: 2, method: 'session/new', params: { cwd: tree, mcpServers: [] } },
        { id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: 'You are carrying on quest quest-port.' }] } },
      ],
    });

    assert.deepEqual(asking.answers.get(1)?.result?.agentCapabilities, { sessionCapabilities: { resume: {} } });
    assert.equal(asking.answers.get(2)?.result?.sessionId, 'acp-session-1');
    assert.equal(asking.answers.get(3)?.result?.stopReason, 'end_turn', asking.stderr);
    assert.deepEqual(asking.texts, ['Which port should the report listen on?']);
    assert.deepEqual(door.moves.map((move) => move.action), ['take'], asking.stderr);
    assert.equal(git(tree, 'rev-parse', 'HEAD').trim(), born, 'nothing is done before the answer');
    assert.equal(asking.code, 0, asking.stderr);

    const resumed = await speak({
      cwd: tree, env, frames: [
        { id: 1, method: 'initialize', params: { protocolVersion: 1 } },
        { id: 2, method: 'session/resume', params: { sessionId: 'acp-session-1', cwd: tree, mcpServers: [] } },
        { id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: 'Port 8080.' }] } },
      ],
    });

    assert.deepEqual(resumed.answers.get(2)?.result, {});
    assert.equal(resumed.answers.get(3)?.result?.stopReason, 'end_turn', resumed.stderr);
    assert.match(resumed.stderr, /acp-agent: resumed conversation acp-session-1 on /);
    assert.doesNotMatch(resumed.stderr, /acp-agent: session on /);
    assert.deepEqual(resumed.texts, ['Serving on the port you named: Port 8080.']);
    assert.deepEqual(door.moves.map((move) => move.action), ['take', 'done'], resumed.stderr);
    assert.match(git(tree, 'log', '-1', '--format=%s'), /^acp: serve the report where the person said \(quest quest-port\)/);
    assert.match(git(tree, 'show', 'HEAD:acp-port-quest-port.md'), /It listens where the person said: Port 8080\./);
    assert.equal(git(tree, 'rev-parse', 'HEAD~1').trim(), born);
    assert.equal(git(tree, 'status', '--porcelain'), '');
    assert.equal(resumed.code, 0, `end of input is the ending, after the quest's fetches too (STUB1)\n${resumed.stderr}`);
  } finally {
    await door.close();
    fx.cleanup();
  }
});

/** How long the stub's resumed turn is held to last: about three seconds, never at once and never a hang (STUB2). */
const HEARD_TURN_MS = { least: 2900, most: 6000 };

/**
 * STUB2 (D137's MSG1e4 and MSG1e3 note): words said to a session after it ended resume its own conversation, and the
 * person's words are the prompt. On a quest whose title asks nothing of it, the stub says what it heard, touches nothing
 * and takes nothing, since its quest had closed. Its turn lasts about three seconds, which family phase 17a2 leans on:
 * the verb says *going on* only if it sees the record working at one of its looks, 250 ms apart.
 */
test('the stub resumed on a quest that asks nothing says what it heard, takes nothing, and ends its turn about 3 s later (STUB2)', async () => {
  const fx = makeFixture('setup-kit-stub-heard');
  const tree = unadoptedRepository(join(fx.root, 'atlas'));
  const born = git(tree, 'rev-parse', 'HEAD').trim();
  const door = await questDoor();
  try {
    const heard = await speak({
      cwd: tree,
      env: { ...process.env, DAORIS_SERVICE_URL: door.url, DAORIS_QUEST_ID: 'quest-tiles', DAORIS_QUEST_TITLE: 'Answer the map tiles' },
      frames: [
        { id: 1, method: 'initialize', params: { protocolVersion: 1 } },
        { id: 2, method: 'session/resume', params: { sessionId: 'acp-session-1', cwd: tree, mcpServers: [] } },
        { id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: 'The tiles moved to v2.' }] } },
      ],
    });

    assert.deepEqual(heard.answers.get(2)?.result, {});
    assert.match(heard.stderr, /acp-agent: resumed conversation acp-session-1 on /);
    assert.match(heard.stderr, /acp-agent: heard after it ended: The tiles moved to v2\./);
    assert.deepEqual(heard.texts, ['Heard after the work: The tiles moved to v2.']);

    // Timed from the resume's answer, which comes the moment it is asked, so a slow start of the process is not counted.
    assert.equal(heard.answers.get(3)?.result?.stopReason, 'end_turn', heard.stderr);
    const turn = heard.at.get(3)! - heard.at.get(2)!;
    assert.ok(turn >= HEARD_TURN_MS.least && turn < HEARD_TURN_MS.most, `the resumed turn lasted ${Math.round(turn)} ms\n${heard.stderr}`);

    assert.deepEqual(door.moves, [], heard.stderr);
    assert.equal(git(tree, 'rev-parse', 'HEAD').trim(), born);
    assert.equal(git(tree, 'status', '--porcelain'), '');
    assert.equal(heard.code, 0, `end of input is the ending (STUB1)\n${heard.stderr}`);
  } finally {
    await door.close();
    fx.cleanup();
  }
});

/**
 * A stub cut short (STUB3): it answers `initialize`, then, at the next frame, either ends without a word or hears nothing
 * more and stays. Either is a stub that will never answer what the row waits for.
 */
const cutShort = (then: 'exits' | 'stays') => `
import { createInterface } from 'node:readline';
createInterface({ input: process.stdin }).on('line', (line) => {
  const frame = JSON.parse(line);
  if (frame.method === 'initialize') process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: frame.id, result: {} }) + '\\n');
  else if (${JSON.stringify(then)} === 'exits') { process.exitCode = 3; process.stdin.destroy(); }
});
`;

const RESUME_FRAMES: Said[] = [
  { id: 1, method: 'initialize', params: { protocolVersion: 1 } },
  { id: 2, method: 'session/new', params: { cwd: '.', mcpServers: [] } },
  { id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: 'Port 8080.' }] } },
];

/**
 * STUB3 (FLAKE1): `speak()` awaited the stub's answers with no bound, so ANSWER1b's row hung ten minutes under load when
 * the stub exited without answering, and passed alone. A stub that exits early now fails its row as it exits, naming each
 * answer that never came, with the stub's stderr beside it.
 */
test('a stub that exits without answering fails its row in seconds, naming the answers that never came (STUB3)', { timeout: 30_000 }, async () => {
  const fx = makeFixture('setup-kit-stub-early');
  const tree = join(fx.root, 'atlas');
  mkdirSync(tree, { recursive: true });
  try {
    const began = Date.now();
    await assert.rejects(
      speak({ cwd: tree, env: { ...process.env }, frames: RESUME_FRAMES, agent: cutShort('exits') }),
      /^Error: the stub exited with code 3 and never answered 2 \(session\/new\), 3 \(session\/prompt\)/,
    );
    assert.ok(Date.now() - began < STUB_WAIT_MS, `it failed as the stub exited, not at the bound: ${Date.now() - began} ms`);
  } finally {
    fx.cleanup();
  }
});

/** STUB3: a stub that stays without answering fails its row at the bound, naming what never came, and is stopped. */
test('a stub that stays without answering fails its row at the bound, and is stopped (STUB3)', { timeout: 30_000 }, async () => {
  const fx = makeFixture('setup-kit-stub-stays');
  const tree = join(fx.root, 'atlas');
  mkdirSync(tree, { recursive: true });
  try {
    await assert.rejects(
      speak({ cwd: tree, env: { ...process.env }, frames: RESUME_FRAMES, agent: cutShort('stays'), wait: 1_000 }),
      /^Error: the stub never answered 2 \(session\/new\), 3 \(session\/prompt\) within 1000 ms/,
    );
    // Stopped: a stub left running holds this file's process open past its last row, which the run would show as a hang.
  } finally {
    fx.cleanup();
  }
});

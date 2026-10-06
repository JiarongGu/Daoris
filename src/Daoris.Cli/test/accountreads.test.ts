import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { READS_FILE, forgetRead, keepRead, readOf, readsOf } from '../src/accountreads.ts';
import type { Login } from '../src/accountreads.ts';
import {
  TOOLCHAINS, commandHarness, managedHome, profileHome, removeProfile, signInNew, signInTo,
} from '../src/toolchain.ts';
import type { Toolchain } from '../src/toolchain.ts';
import { driverRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

/**
 * `reads.json` under the home (ROSTER1, D150 §5.3; AGENTREAD1): what was last read of each account's sign-in, and of the
 * tool's own, with when. The driver's roster reports from it, so a reading the terminal made is one the screen and the
 * driver loop start from too (D50). The CLI's half of a TWIN with the driver's `AccountReads`: 🔴 both hold the table in
 * `fixtures/account-reads.json`, row for row, and the driver's own rows of what does not read are held to it here, cell for
 * cell.
 */

type Cell = string | null;

const here = dirname(fileURLToPath(import.meta.url));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'account-reads.json'), 'utf8')) as {
  read: [why: string, file: Cell, agent: string, account: Cell, said: Cell][];
  keep: [why: string, file: Cell, agent: string, account: Cell, login: Login, at: string, after: string][];
  forget: [why: string, file: Cell, agent: string, account: string, after: string][];
};

const DRIVER = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver');
const DRIVER_TESTS = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'AccountReadsTests.cs');

function holding(root: string, file: Cell): void {
  if (file !== null) writeFileSync(join(root, READS_FILE), file, 'utf8');
}

/** The file as written, or null where there is none. */
function onDisk(root: string): string | null {
  return existsSync(join(root, READS_FILE)) ? readFileSync(join(root, READS_FILE), 'utf8') : null;
}

/** What a twin writes for `after`: indented two spaces, LF, a final newline. */
function written(after: string): string {
  return `${JSON.stringify(JSON.parse(after), null, 2)}\n`;
}

test('a reading reads as the driver reads it (the shared table)', () => {
  for (const [index, [why, file, agent, account, said]] of TABLE.read.entries()) {
    const fx = makeFixture(`reads-read-${index}`);
    holding(fx.root, file);

    const read = readOf(readsOf(fx.root, agent), account);

    assert.equal(read === null ? null : `${read.login} ${read.at.toISOString()}`, said, why);
    fx.cleanup();
  }
});

test('a reading is kept as the driver keeps it, byte for byte (the shared table)', () => {
  for (const [index, [why, file, agent, account, login, at, after]] of TABLE.keep.entries()) {
    const fx = makeFixture(`reads-keep-${index}`);
    holding(fx.root, file);

    const kept = keepRead(fx.root, agent, account, login, new Date(at));

    assert.equal(kept, after !== 'unchanged', `${why}: whether it wrote`);
    assert.equal(onDisk(fx.root), after === 'unchanged' ? file : written(after), why);
    fx.cleanup();
  }
});

test('a removed account\'s reading is forgotten as the driver forgets it (the shared table)', () => {
  for (const [index, [why, file, agent, account, after]] of TABLE.forget.entries()) {
    const fx = makeFixture(`reads-forget-${index}`);
    holding(fx.root, file);

    forgetRead(fx.root, agent, account);

    assert.equal(onDisk(fx.root), after === 'unchanged' ? file : written(after), why);
    fx.cleanup();
  }
});

/** The driver's own table of what does not read (`AccountReadsTests`) is in the shared one, as nothing known. */
test('the driver\'s rows of what does not read are the shared table\'s, cell for cell', () => {
  const rows = driverRows(readFileSync(DRIVER_TESTS, 'utf8').replace(/\r\n/g, '\n'), 'What_does_not_read_is_nothing_known',
    {}, 'AccountReadsTests');

  assert.ok(rows.length > 0);
  for (const [file] of rows) {
    const row = TABLE.read.find(([, held, agent, account]) => held === file && agent === 'claude-code' && account === 'account-1');
    assert.ok(row, `the shared table has no row for the driver's ${String(file)}`);
    assert.equal(row[4], null, `${row[0]}: the driver reads it as nothing known`);
  }
});

/** The file's name, its keys and its words are the driver's spelling (`AccountReads.cs`), so the twins meet in one file. */
test('the file, its keys and its words are spelled as the driver spells them', () => {
  const source = readFileSync(join(DRIVER, 'AccountReads.cs'), 'utf8');

  assert.match(source, new RegExp(`FileName = "${READS_FILE.replace('.', '\\.')}"`));
  assert.match(source, /Own = "own"/);
  assert.match(source, /AccountsKey = "accounts"/);
  for (const word of ['in', 'out', 'unknown'] as const) assert.match(source, new RegExp(`=> "${word}"`), word);
  assert.match(source, /\["login"\] = Word\(login\), \["read"\] = AccountCooling\.Stamp\(at\)/);
});

// ——— The doors that ask (AGENTREAD1): `agent list`, a sign-in's end, and a removal forgetting.

function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HARNESS_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = commandHarness({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HARNESS_CONFIG;
    else process.env.DAORIS_HARNESS_CONFIG = saved;
  }
}

/**
 * A stand-in agent whose login question reads the home it is asked about, by `variable`: signed in exactly when the home
 * holds `credentials.json`, saying who by its contents, as `claude auth status` names an email.
 */
function standIn(root: string, variable: string): string {
  const script = join(root, 'stand-in.mjs');
  writeFileSync(script, [
    "import { existsSync, readFileSync } from 'node:fs';",
    `const home = process.env.${variable};`,
    "if (process.argv[2] === '--version') { console.log('stand-in 1.0'); process.exit(0); }",
    "const signed = home && existsSync(home + '/credentials.json');",
    "console.log(JSON.stringify({ loggedIn: Boolean(signed), email: signed ? readFileSync(home + '/credentials.json', 'utf8').trim() : null }));",
  ].join('\n'), 'utf8');
  return script;
}

function fakeHarness(root: string): Toolchain {
  return {
    binary: [process.execPath, standIn(root, 'FAKE_HARNESS_HOME')],
    version: ['--version'],
    profileVariable: 'FAKE_HARNESS_HOME',
    login: ['login'],
    loginCheck: { ...TOOLCHAINS['claude-code']!.loginCheck!, args: ['status'] },
  };
}

/** What `reads.json` says of one agent, as the file holds it. */
function fileOf(root: string): Record<string, { accounts?: Record<string, Record<string, unknown>>; own?: unknown }> {
  return JSON.parse(readFileSync(join(root, READS_FILE), 'utf8'));
}

/** The moment the driver's `AccountCooling.Stamp` writes, and the first form its reader takes: UTC, to the second. */
const STAMP = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;

/**
 * 🔴 AGENTREAD1: `agent list` reads each account, and keeps what it read where the driver keeps its own, so the screen and
 * the loop learn from a terminal's reading (D50). Claude Code is pinned to a stand-in installed in the managed layout, so
 * the real tool is asked nothing; every other agent is pinned to a version nothing installed, so it is asked nothing.
 */
test('`agent list` keeps each account it read, in the shape the driver reads', () => {
  const fx = makeFixture('reads-list');
  const script = standIn(fx.root, 'CLAUDE_CONFIG_DIR');
  const bin = join(managedHome(fx.root, 'claude-code', '1.2.3'), 'node_modules', '.bin');
  mkdirSync(bin, { recursive: true });
  const windows = process.platform === 'win32';
  const shim = join(bin, windows ? 'claude.cmd' : 'claude');
  writeFileSync(shim, windows
    ? `@"${process.execPath}" "${script}" %*\r\n`
    : `#!/bin/sh\nexec "${process.execPath}" "${script}" "$@"\n`, 'utf8');
  if (!windows) chmodSync(shim, 0o755);
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-1'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-2'), { recursive: true });
  writeFileSync(join(profileHome(fx.root, 'claude-code', 'account-1'), 'credentials.json'), 'someone@example.invalid', 'utf8');
  const path = join(fx.root, 'harnesses.json');
  writeFileSync(path, JSON.stringify({ versions: {
    'claude-code': '1.2.3', 'claude-code-acp': '0.0.0-none', codex: '0.0.0-none', 'codex-acp': '0.0.0-none', dsh: '0.0.0-none',
  } }), 'utf8');
  const before = Math.floor(Date.now() / 1000) * 1000;

  const listed = run(['list'], path);

  assert.equal(listed.code, 0);
  assert.match(listed.out, /account-1\s+in\s+someone@example\.invalid/);
  const file = fileOf(fx.root);
  // Only the tool that was asked: a door asks no account (AGT7), and an agent nothing is installed for is asked nothing.
  assert.deepEqual(Object.keys(file), ['claude-code']);
  assert.deepEqual(Object.keys(file['claude-code']!), ['accounts']);
  const accounts = file['claude-code']!.accounts!;
  assert.deepEqual(Object.keys(accounts), ['account-1', 'account-2']);
  assert.deepEqual(Object.values(accounts).map((entry) => [Object.keys(entry), entry.login]), [
    [['login', 'read'], 'in'], [['login', 'read'], 'out'],
  ]);
  for (const entry of Object.values(accounts)) {
    assert.match(String(entry.read), STAMP);
    assert.ok(Date.parse(String(entry.read)) >= before && Date.parse(String(entry.read)) <= Date.now());
  }
  // Who signed in is read fresh and written nowhere (D66 §3).
  assert.doesNotMatch(readFileSync(join(fx.root, READS_FILE), 'utf8'), /@|\r/);
  // And it reads back as the driver's reader reads it: the twin's rules, held by the shared table above.
  const reads = readsOf(fx.root, 'claude-code');
  assert.equal(readOf(reads, 'account-1')?.login, 'in');
  assert.equal(readOf(reads, 'account-2')?.login, 'out');
  fx.cleanup();
});

/** A sign-in's end reads its account (ROSTER1, D150 §5.3), and keeps the reading; a sign-in that did not finish keeps none. */
test('a new account\'s sign-in keeps its reading at its end, and one that did not finish keeps none', () => {
  const fx = makeFixture('reads-sign-in-new');
  const toolchain = fakeHarness(fx.root);

  assert.equal(signInNew('fake', toolchain, fx.root, () => 2, () => {}), 2);
  assert.ok(!existsSync(join(fx.root, READS_FILE)));

  signInNew('fake', toolchain, fx.root, (where) => {
    writeFileSync(join(where, 'credentials.json'), 'someone@example.invalid', 'utf8');
    return 0;
  }, () => {});

  const accounts = fileOf(fx.root).fake!.accounts!;
  assert.deepEqual(Object.values(accounts).map((entry) => entry.login), ['in']);
  assert.match(Object.keys(accounts)[0]!, /^acct-[0-9a-f]{8}$/);
  fx.cleanup();
});

/**
 * Signing back in reads the account at its end whether or not it has a name (ROSTER1): a terminal's sign-in into an account
 * the loop read signed out is then read signed in, where before only its mark said to ask again.
 */
test('signing back in keeps the account\'s reading at its end, a named account too', () => {
  const fx = makeFixture('reads-sign-in-to');
  const toolchain = fakeHarness(fx.root);
  mkdirSync(profileHome(fx.root, 'fake', 'account-1'), { recursive: true });
  writeFileSync(join(fx.root, 'accounts.json'), JSON.stringify({ fake: { 'account-1': { name: 'seat' } } }), 'utf8');
  keepRead(fx.root, 'fake', 'account-1', 'out', new Date(Date.now() - 60_000));

  signInTo('fake', toolchain, fx.root, 'seat', (where) => {
    writeFileSync(join(where, 'credentials.json'), 'back@example.invalid', 'utf8');
    return 0;
  }, () => {});

  assert.equal(readOf(readsOf(fx.root, 'fake'), 'account-1')?.login, 'in');
  fx.cleanup();
});

/** An account removed from this machine: its reading goes with it, as the driver's `Removed` forgets one (ROSTER1). */
test('removing an account forgets its reading, and the others stand', () => {
  const fx = makeFixture('reads-remove');
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-1'), { recursive: true });
  keepRead(fx.root, 'claude-code', 'account-1', 'in', new Date('2026-10-04T10:00:00Z'));
  keepRead(fx.root, 'claude-code', 'account-2', 'out', new Date('2026-10-04T10:00:00Z'));

  removeProfile(fx.root, 'claude-code', 'account-1');

  const reads = readsOf(fx.root, 'claude-code');
  assert.equal(readOf(reads, 'account-1'), null);
  assert.equal(readOf(reads, 'account-2')?.login, 'out');
  fx.cleanup();
});

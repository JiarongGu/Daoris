import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { READS_FILE, forgetRead, keepRead, readOf, readsOf } from '../../src/accounts/accountreads.ts';
import type { Login } from '../../src/accounts/accountreads.ts';
import { profileHome, removeProfile } from '../../src/agents/toolchain.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { driverFile, driverRows, driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * `reads.json` under the home (ROSTER1, D150 §5.3; AGENTREAD1): what was last read of each account's sign-in, and of the
 * tool's own, with when. The driver's roster reports from it, so a reading the terminal made is one the screen and the
 * driver loop start from too (D50). The CLI's half of a TWIN with the driver's `AccountReads`: 🔴 both hold the table in
 * `fixtures/account-reads.json`, row for row, and the driver's own rows of what does not read are held to it here, cell for
 * cell.
 */

type Cell = string | null;

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'account-reads.json'), 'utf8')) as {
  read: [why: string, file: Cell, agent: string, account: Cell, said: Cell][];
  keep: [why: string, file: Cell, agent: string, account: Cell, login: Login, at: string, after: string][];
  forget: [why: string, file: Cell, agent: string, account: string, after: string][];
};

const DRIVER_TESTS = driverTestFile('AccountReadsTests.cs');

function holding(root: string, file: Cell): void {
  if (file === null) return;
  const path = homeEntry(root, 'reads');
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, file, 'utf8');
}

/** The file as written, or null where there is none. */
function onDisk(root: string): string | null {
  const path = homeEntry(root, 'reads');
  return existsSync(path) ? readFileSync(path, 'utf8') : null;
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
  const source = readFileSync(driverFile('AccountReads.cs'), 'utf8');

  // The file is the home's `reads` entry, by its place in the driver's table (HOMELAYOUT2).
  assert.match(source, /HomeEntry\.Reads\b/);
  assert.match(readFileSync(driverFile('DaorisHome.cs'), 'utf8'), new RegExp(`new\\("reads", HomeCategory\\.Data, "data/${READS_FILE.replace('.', '\\.')}", "${READS_FILE.replace('.', '\\.')}"`));
  assert.match(source, /Own = "own"/);
  assert.match(source, /AccountsKey = "accounts"/);
  for (const word of ['in', 'out', 'unknown'] as const) assert.match(source, new RegExp(`=> "${word}"`), word);
  assert.match(source, /\["login"\] = Word\(login\), \["read"\] = AccountCooling\.Stamp\(at\)/);
});

// ——— The doors that ask (AGENTREAD1): a removal forgetting. A sign-in's end is the driver program's since PUSH1f2e6.

// `agent list`'s reading of each account, kept in `reads.json` (AGENTREAD1), is the driver's program's since PUSH1f2e4: an
// `AgentsTextTests` row asks a stand-in Claude Code and holds the file it keeps.

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

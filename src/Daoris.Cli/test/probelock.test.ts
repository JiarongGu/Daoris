import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, utimesSync, writeFileSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { markSignedIn, probeLockPath, probeLockStale, signedInPath, takeProbeLock } from '../src/probelock.ts';
import { probe, profileHome, readHarnessSettings, TOOLCHAINS } from '../src/toolchain.ts';
import type { Toolchain } from '../src/toolchain.ts';
import { driverRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

// TOOL6g: one status question per configuration home at a time, across the desktop and the terminal. The rows are the
// driver's `ProbeLockTests`, read from its source, so a rule changed on one side alone fails here.

const DRIVER_TESTS = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'ProbeLockTests.cs');
const source = readFileSync(DRIVER_TESTS, 'utf8');

const PATH_ROWS: [string, string | null, string][] = [
  ['claude-code', 'account-1', 'harnesses/.probing/claude-code/account-1.lock'],
  ['claude-code', 'work.account', 'harnesses/.probing/claude-code/work.account.lock'],
  ['claude-code', null, 'harnesses/.probing/claude-code.lock'],
  ['codex', 'account-2', 'harnesses/.probing/codex/account-2.lock'],
];

const STALE_ROWS: [number, boolean][] = [
  [0, false],
  [59, false],
  [61, true],
  [3600, true],
];

const MARK_ROWS: [string, string, string][] = [
  ['claude-code', 'account-1', 'harnesses/.probing/claude-code/account-1.signed-in'],
  ['codex', 'work', 'harnesses/.probing/codex/work.signed-in'],
];

test('the lock table is the driver\'s, cell for cell', () => {
  assert.deepEqual(driverRows(source, 'Each_lock_lives_beside_the_accounts_never_inside_one'), PATH_ROWS);
  assert.deepEqual(driverRows(source, 'A_lock_older_than_a_minute_is_a_holder_that_died'), STALE_ROWS);
  assert.deepEqual(driverRows(source, 'A_sign_in_is_marked_beside_the_account_s_lock'), MARK_ROWS);
});

test('a sign-in is marked beside the account\'s lock', () => {
  const fx = makeFixture('probelock-mark');
  for (const [owner, profile, expected] of MARK_ROWS) {
    assert.equal(relative(fx.root, signedInPath(fx.root, owner, profile)).split(sep).join('/'), expected);
  }
  markSignedIn(fx.root, 'claude-code', 'account-1', new Date());
  assert.match(readFileSync(signedInPath(fx.root, 'claude-code', 'account-1'), 'utf8'), /^\{"at":"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ"\}\n$/);
  fx.cleanup();
});

test('each lock lives beside the accounts, never inside one', () => {
  const fx = makeFixture('probelock-paths');
  for (const [owner, profile, expected] of PATH_ROWS) {
    const path = probeLockPath(fx.root, owner, profile);
    assert.equal(relative(fx.root, path).split(sep).join('/'), expected);
    assert.ok(!path.startsWith(join(fx.root, 'harnesses', owner) + sep), `${expected} is inside the account's folder`);
  }
  fx.cleanup();
});

test('a lock older than a minute is a holder that died, and one that is not there is not stale', () => {
  const fx = makeFixture('probelock-stale');
  const path = probeLockPath(fx.root, 'claude-code', 'account-1');
  mkdirSync(dirname(path), { recursive: true });
  const now = new Date();
  for (const [seconds, stale] of STALE_ROWS) {
    writeFileSync(path, '{}\n', 'utf8');
    const then = new Date(now.getTime() - seconds * 1000);
    utimesSync(path, then, then);
    assert.equal(probeLockStale(path, now), stale, `${seconds} s`);
  }
  assert.equal(probeLockStale(probeLockPath(fx.root, 'claude-code', 'account-2'), now), false);
  fx.cleanup();
});

test('a lock taken is a file until it is let go, and a second taker waits it out', () => {
  const fx = makeFixture('probelock-take');
  const path = probeLockPath(fx.root, 'claude-code', 'account-1');

  const release = takeProbeLock(path);
  assert.ok(release);
  assert.match(readFileSync(path, 'utf8'), /"pid":\d+/);
  assert.equal(takeProbeLock(path, 300), null);

  release();
  assert.equal(existsSync(path), false);
  const again = takeProbeLock(path, 300);
  assert.ok(again);
  again();
  fx.cleanup();
});

test('a lock a dead holder left is taken', () => {
  const fx = makeFixture('probelock-dead');
  const path = probeLockPath(fx.root, 'claude-code', 'account-1');
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, '{"pid":1}\n', 'utf8');
  const then = new Date(Date.now() - 5 * 60_000);
  utimesSync(path, then, then);

  const release = takeProbeLock(path, 300);
  assert.ok(release);
  release();
  fx.cleanup();
});

/** A stand-in agent whose status question says signed in where the home holds `credentials.json`. */
function fakeHarness(fx: { root: string }): Toolchain {
  const script = join(fx.root, 'fake-harness.mjs');
  writeFileSync(script, [
    "import { existsSync } from 'node:fs';",
    'const home = process.env.FAKE_HARNESS_HOME;',
    "if (process.argv[2] === '--version') { console.log('fake 1.0'); process.exit(0); }",
    "console.log(JSON.stringify({ loggedIn: Boolean(home && existsSync(home + '/credentials.json')) }));",
  ].join('\n'), 'utf8');
  return {
    binary: [process.execPath, script],
    version: ['--version'],
    profileVariable: 'FAKE_HARNESS_HOME',
    loginCheck: { ...TOOLCHAINS['claude-code']!.loginCheck!, args: ['status'] },
  };
}

test('the terminal\'s probe asks an account under its lock, and lets it go', () => {
  const fx = makeFixture('probelock-probe');
  const toolchain = fakeHarness(fx);
  const home = profileHome(fx.root, 'fake', 'account-1');
  mkdirSync(home, { recursive: true });
  writeFileSync(join(home, 'credentials.json'), '{}', 'utf8');
  // A lock left by a holder that died does not keep the account from being asked.
  const lock = probeLockPath(fx.root, 'fake', 'account-1');
  mkdirSync(dirname(lock), { recursive: true });
  writeFileSync(lock, '{"pid":1}\n', 'utf8');
  const then = new Date(Date.now() - 5 * 60_000);
  utimesSync(lock, then, then);

  const report = probe('fake', toolchain, fx.root, readHarnessSettings(join(fx.root, 'harnesses.json')));

  assert.deepEqual(report.profiles.map((p) => [p.name, p.login]), [['account-1', 'in']]);
  assert.equal(existsSync(lock), false);
  fx.cleanup();
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import type { Fixture } from './_fixture.ts';
import { makeFixture, captureError } from './_fixture.ts';
import { commandSync } from '../src/materialize.ts';
import { commandUpstream } from '../src/upstream.ts';
import { commandStatus } from '../src/commands.ts';
import { readText } from '../src/fsx.ts';
import { DaorisError } from '../src/errors.ts';

/**
 * WSSETUP4 (D124 §1.4): an older doctrine tool never rewrites a newer lock. `sync` measures against the lock
 * (D13), so to an older canon a file the newer canon improved and nobody touched here reads as *improved
 * upstream, untouched here* (D19's second row), and `sync` would rewrite it to the older text and stamp the
 * lock back. Two versions on one machine is a hazard with npm alone (`npx daoris@<older> sync`), and since
 * D124 an install carries a copy of its own. So `sync` and `upstream` refuse such a lock, exit 1, naming both
 * versions and the command at the lock's.
 */

const doc = (name: string, body: string) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\n${body}\n`;

/** A canon at `version`, whose one rule says `body`. */
function canonAt(name: string, version: string, body: string): Fixture {
  const fx = makeFixture(name);
  fx.write('canon.json', JSON.stringify({ version }));
  fx.write('core/rules/sensitive-info.md', doc('sensitive-info', body));
  return fx;
}

/** Run a command against `canon`, as the tool carrying that canon would. */
function withCanon<T>(canon: Fixture, run: () => T): T {
  const held = process.env.DAORIS_CANON;
  process.env.DAORIS_CANON = canon.root;
  try {
    return run();
  } finally {
    if (held === undefined) delete process.env.DAORIS_CANON;
    else process.env.DAORIS_CANON = held;
  }
}

const sync = (repo: Fixture, argv: string[] = [], out: string[] = []) =>
  commandSync({ root: repo.root, argv, write: (line: string) => out.push(line), packageRoot: '' });

/** A repository the newer tool synced: its lock says `newer`, and its region carries the newer canon's text. */
function syncedByNewer(newer = '0.2.0', older = '0.1.0') {
  const newerCanon = canonAt('newer-lock-canon-new', newer, 'Body as the newer canon wrote it.');
  const olderCanon = canonAt('newer-lock-canon-old', older, 'Body as the older canon wrote it.');
  const repo = makeFixture('newer-lock-repo');
  repo.write('daoris.json', JSON.stringify({ source: `daoris@${newer}`, packs: [] }));
  assert.equal(withCanon(newerCanon, () => sync(repo)), 0);
  const cleanup = () => {
    newerCanon.cleanup();
    olderCanon.cleanup();
    repo.cleanup();
  };
  return { newerCanon, olderCanon, repo, cleanup };
}

/** Every byte `sync` writes in this repository: the region, the pointer and the lock. */
const written = (repo: Fixture) => ['AGENTS.md', 'CLAUDE.md', 'daoris.lock'].map((file) => (repo.exists(file) ? repo.read(file) : null));

test('sync refuses a lock a newer canon wrote, exit 1, naming both versions and the command at the lock’s', () => {
  const fx = syncedByNewer();
  const before = written(fx.repo);
  assert.match(fx.repo.read('AGENTS.md'), /as the newer canon wrote it/);

  const error = captureError(() => withCanon(fx.olderCanon, () => sync(fx.repo)));

  assert.ok(error instanceof DaorisError, String(error));
  assert.equal(error.exitCode, 1, 'a policy refusal, not a tool error');
  assert.match(error.message, /0\.2\.0/);
  assert.match(error.message, /0\.1\.0/);
  assert.match(error.message, /npx daoris@0\.2\.0 sync/);
  assert.deepEqual(written(fx.repo), before, '🔴 nothing rewritten to the older text, and the lock not stamped back');
  assert.match(fx.repo.read('AGENTS.md'), /as the newer canon wrote it/);
  fx.cleanup();
});

/**
 * Neither door around it: a dry run is how a person asks whether `sync` would refuse, so it answers with the
 * same refusal; and `--force` discards local edits, which is a different question from discarding a newer
 * canon's text.
 */
test('a dry run and --force refuse it the same way, and write nothing', () => {
  const fx = syncedByNewer();
  const before = written(fx.repo);
  for (const argv of [['--dry-run'], ['--force']]) {
    const error = captureError(() => withCanon(fx.olderCanon, () => sync(fx.repo, argv)));
    assert.ok(error instanceof DaorisError, `${argv}: ${String(error)}`);
    assert.equal(error.exitCode, 1, argv.join(' '));
    assert.match(error.message, new RegExp(`npx daoris@0\\.2\\.0 sync ${argv[0]}`), argv.join(' '));
    assert.deepEqual(written(fx.repo), before, `${argv}: nothing written`);
  }
  fx.cleanup();
});

/** Newer by number, never by text: `0.10.0` follows `0.9.0`, which string comparison gets backwards. */
test('newer is by number: 0.10.0 is newer than 0.9.0, and an older lock or the same version syncs as before', () => {
  const tenOverNine = syncedByNewer('0.10.0', '0.9.0');
  const error = captureError(() => withCanon(tenOverNine.olderCanon, () => sync(tenOverNine.repo)));
  assert.ok(error instanceof DaorisError && error.exitCode === 1, String(error));
  assert.match(error.message, /npx daoris@0\.10\.0 sync/);
  tenOverNine.cleanup();

  // The ordinary upgrade: a lock at 0.9.0, a tool carrying 0.10.0.
  const older = canonAt('newer-lock-canon-old', '0.9.0', 'Body at 0.9.0.');
  const newer = canonAt('newer-lock-canon-new', '0.10.0', 'Body at 0.10.0.');
  const repo = makeFixture('newer-lock-repo');
  repo.write('daoris.json', JSON.stringify({ source: 'daoris@0.9.0', packs: [] }));
  assert.equal(withCanon(older, () => sync(repo)), 0);
  assert.equal(withCanon(older, () => sync(repo)), 0, 'the same version syncs');
  assert.equal(withCanon(newer, () => sync(repo)), 0, 'a newer tool upgrades the lock');
  assert.match(repo.read('AGENTS.md'), /Body at 0\.10\.0\./);
  assert.equal(JSON.parse(repo.read('daoris.lock')).canonVersion, '0.10.0');
  older.cleanup();
  newer.cleanup();
  repo.cleanup();
});

/**
 * `upstream` writes into the canon the tool carries. From a repository a newer canon wrote, the edit is a
 * change to text the older canon never had, and promoting it would write the newer canon's words over the
 * older canon's file.
 */
test('upstream refuses a lock a newer canon wrote, one file or all, and the canon is untouched', () => {
  const fx = syncedByNewer();
  fx.repo.write('AGENTS.md', fx.repo.read('AGENTS.md').replace('as the newer canon wrote it.', 'as the newer canon wrote it, then improved here.'));
  const canonFile = join(fx.olderCanon.root, 'core', 'rules', 'sensitive-info.md');
  const before = readText(canonFile);

  for (const argv of [['rules/sensitive-info.md'], ['--all']]) {
    const error = captureError(() => withCanon(fx.olderCanon, () =>
      commandUpstream({ root: fx.repo.root, argv, write: () => {}, packageRoot: '' })));
    assert.ok(error instanceof DaorisError, `${argv}: ${String(error)}`);
    assert.equal(error.exitCode, 1, argv.join(' '));
    assert.match(error.message, /0\.2\.0/);
    assert.match(error.message, /0\.1\.0/);
    assert.match(error.message, new RegExp(`npx daoris@0\\.2\\.0 upstream ${argv[0]}`), argv.join(' '));
    assert.equal(readText(canonFile), before, `${argv}: the older canon is untouched`);
  }
  fx.cleanup();
});

/**
 * `status` reads the versions offline and said *canon 0.1.0 available — run 'daoris sync'* for a lock a newer
 * canon wrote, which sends the person to the command that now refuses. It says what is so instead, in both
 * shapes.
 */
test('status says the lock is newer than the tool, names the command at the lock’s version, and does not say to sync', () => {
  const fx = syncedByNewer();
  const out: string[] = [];
  assert.equal(withCanon(fx.olderCanon, () =>
    commandStatus({ root: fx.repo.root, write: (line: string) => out.push(line), packageRoot: '' })), 0);
  const text = out.join('\n');
  assert.match(text, /0\.2\.0/);
  assert.match(text, /0\.1\.0/);
  assert.match(text, /npx daoris@0\.2\.0/);
  assert.match(text, /sync and upstream refuse/);
  assert.doesNotMatch(text, /available/);
  assert.doesNotMatch(text, /run 'daoris sync'/);

  const json: string[] = [];
  withCanon(fx.olderCanon, () =>
    commandStatus({ root: fx.repo.root, argv: ['--json'], write: (line: string) => json.push(line), packageRoot: '' }));
  const report = JSON.parse(json.join('\n'));
  assert.equal(report.update, null);
  assert.deepEqual(report.newerLock, { locked: '0.2.0', carried: '0.1.0', run: 'npx daoris@0.2.0' });
  fx.cleanup();
});

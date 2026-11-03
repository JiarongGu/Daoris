import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { keepLockPath, keepLockStale, takeKeepLock } from '../../src/platform/keeplock.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * CONFIGLOCK1 (D173's CONFIG1 note): a keeping write holds `<file>.lock` from its re-read to its rename, so a change another writer makes
 * in between is never lost. The stale rules are one table both twins read, `driver-config-keeps.json`'s `lock`, row for row with the
 * driver's `KeepLockTests`, among the driver's tests' fixtures since PUSH1f2b; the driver's `KeepLockProcessTests` holds it across the
 * two runtimes.
 */

type LockRow = { name: string; pid?: 'live' | 'gone'; age?: number; text?: string; stale: boolean };

const TABLE = JSON.parse(readFileSync(driverTestFile('driver-config-keeps.json'), 'utf8')) as { lock: LockRow[] };
const NOW = Date.parse('2026-10-20T12:00:00.000Z');
/** A process id no process has (`process.kill` answers ESRCH), as the driver's twin uses `int.MaxValue`. */
const GONE = 2_147_483_647;

TABLE.lock.forEach((row) => {
  test(`keeplock: the shared table judges a lock as the driver does: ${row.name}`, () => {
    const text = row.text ?? `${JSON.stringify({ pid: 4242, since: new Date(NOW - row.age! * 1000).toISOString() })}\n`;
    assert.equal(keepLockStale(text, NOW, () => row.pid !== 'gone'), row.stale, row.name);
  });
});

/** A lock at `file`'s place as a holder with this process id wrote it, `since` now. */
function plant(file: string, pid: number): string {
  const text = `${JSON.stringify({ pid, since: new Date().toISOString() })}\n`;
  writeFileSync(keepLockPath(file), text, 'utf8');
  return text;
}

test('keeplock: the lock is the file\'s full path with .lock after it, there only while held, saying who holds it since when', () => {
  const fx = makeFixture('keeplock-take');
  const file = homeEntry(fx.root, 'driver');
  assert.equal(keepLockPath(file), `${resolve(file)}.lock`);
  const held = takeKeepLock(file);
  const said = JSON.parse(readFileSync(keepLockPath(file), 'utf8')) as { pid: number; since: string };
  assert.equal(said.pid, process.pid);
  assert.match(said.since, /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$/);
  held.release();
  assert.equal(existsSync(keepLockPath(file)), false);
});

test('keeplock: a lock a live writer holds is waited for, then refused naming it, and left as it is', () => {
  const fx = makeFixture('keeplock-held');
  const file = homeEntry(fx.root, 'driver');
  const text = plant(file, process.pid);
  assert.throws(() => takeKeepLock(file, { within: 100 }), (error: Error) => error.message.includes(`process ${process.pid}`)
    && error.message.includes(keepLockPath(file)));
  assert.equal(readFileSync(keepLockPath(file), 'utf8'), text);
});

test('keeplock: a lock whose process is gone is removed and taken', () => {
  const fx = makeFixture('keeplock-stale');
  const file = homeEntry(fx.root, 'driver');
  plant(file, GONE);
  const held = takeKeepLock(file, { within: 100 });
  assert.equal((JSON.parse(readFileSync(keepLockPath(file), 'utf8')) as { pid: number }).pid, process.pid);
  held.release();
});

test('keeplock: a release leaves a lock another holder wrote since', () => {
  const fx = makeFixture('keeplock-other');
  const file = homeEntry(fx.root, 'driver');
  const held = takeKeepLock(file);
  const other = plant(file, GONE);
  held.release();
  assert.equal(readFileSync(keepLockPath(file), 'utf8'), other);
});

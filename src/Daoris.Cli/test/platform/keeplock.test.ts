import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { keepLockPath, keepLockStale, takeKeepLock } from '../../src/platform/keeplock.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * CONFIGLOCK1 (D173's CONFIG1 note): a keeping write holds `<file>.lock` from its re-read to its rename, so a change another writer makes
 * in between is never lost. The stale rules are one table both twins read, `fixtures/driver-config-keeps.json`'s `lock`, row for row with
 * the driver's `KeepLockTests`; the driver's `KeepLockProcessTests` holds it across the two runtimes.
 */

type LockRow = { name: string; pid?: 'live' | 'gone'; age?: number; text?: string; stale: boolean };

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'driver-config-keeps.json'), 'utf8')) as { lock: LockRow[] };
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

/** A Node process running `script` as a module, its lines said and its end awaited. */
function started(script: string, env: Record<string, string>) {
  const child = spawn(process.execPath, ['--input-type=module', '-e', script], { env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
  let out = '';
  let err = '';
  const ended = new Promise<number>((done) => child.on('close', (code) => done(code ?? -1)));
  child.stdout.setEncoding('utf8').on('data', (chunk: string) => { out += chunk; });
  child.stderr.setEncoding('utf8').on('data', (chunk: string) => { err += chunk; });
  return {
    async said(word: string): Promise<void> {
      for (const until = Date.now() + 30_000; !out.includes(word); await new Promise((done) => setTimeout(done, 10))) {
        if (child.exitCode !== null || Date.now() > until) throw new Error(`the other writer ended or stalled without saying ${word}: ${err}`);
      }
    },
    async ended(): Promise<{ code: number; out: string; err: string }> {
      return { code: await ended, out, err };
    },
  };
}

const source = (...parts: string[]) => pathToFileURL(join(here, '..', 'src', ...parts)).href;

/**
 * 🔴 This process holds the lock from its re-read to its write, as a writer between the two does, and writes what it read then with its
 * own change. The other process's edit, made meanwhile, waits for the lock, then reads the file again and keeps this one's change. Without
 * the lock the other's edit landed inside this one's window, and this one's write, made from what it read before, dropped it.
 */
async function heldAcross(name: string, start: string, script: string, mine: (text: string) => string): Promise<string> {
  const fx = makeFixture(name);
  const file = fx.write('home.json', start);
  const held = takeKeepLock(file);
  const read = readFileSync(file, 'utf8');
  const other = started(script, { KEEP_FILE: file });
  try {
    await other.said('read');
    // Long enough for the other writer to reach its write, and to have made it, had it not waited.
    await new Promise((done) => setTimeout(done, 400));
    writeFileSync(file, mine(read), 'utf8');
  } finally {
    held.release();
  }
  const { code, err } = await other.ended();
  assert.equal(code, 0, err);
  return readFileSync(file, 'utf8');
}

test('keeplock: an edit of driver.json in another process waits for this one\'s, and neither is lost (CONFIGLOCK1)', async () => {
  const text = await heldAcross('keeplock-driver', '{\n  "drivable": [\n    "engine"\n  ],\n  "notify": true\n}\n', [
    `import { readDriverChoices, writeDriverChoices } from '${source('driver', 'driverconfig.ts')}';`,
    'const choices = readDriverChoices(process.env.KEEP_FILE);',
    "process.stdout.write('read\\n');",
    "writeDriverChoices(process.env.KEEP_FILE, { ...choices, drivable: [...choices.drivable, 'orders'] });",
  ].join('\n'), (read) => read.replace('"notify": true', '"notify": false'));
  assert.equal(text, '{\n  "drivable": [\n    "engine",\n    "orders"\n  ],\n  "notify": false\n}\n');
});

test('keeplock: an edit of another home file in another process waits for this one\'s, and neither is lost (CONFIGLOCK1)', async () => {
  const text = await heldAcross('keeplock-home', '{\n  "disabled": [\n    "acme.pr"\n  ]\n}\n', [
    `import { writeKeeping } from '${source('platform', 'jsonkeep.ts')}';`,
    "process.stdout.write('read\\n');",
    "writeKeeping(process.env.KEEP_FILE, true, { disabled: ['acme.pr'] }, { disabled: ['acme.pr', 'acme.ci'] });",
  ].join('\n'), (read) => read.replace('\n}', ',\n  "theme": "dark"\n}'));
  assert.equal(text, '{\n  "disabled": [\n    "acme.pr",\n    "acme.ci"\n  ],\n  "theme": "dark"\n}\n');
});

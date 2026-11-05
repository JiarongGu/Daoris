import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { RUN_LOCK_STALE_MS, runLockStale, withRunLock } from './run-lock.mjs';

const REPO = dirname(dirname(fileURLToPath(import.meta.url)));
mkdirSync(join(REPO, '_fixtures'), { recursive: true });

const NOW = Date.parse('2026-11-05T09:00:00Z');
const said = (pid, at = NOW) => JSON.stringify({ pid, since: new Date(at).toISOString() });

function scratch(each) {
  const root = mkdtempSync(join(REPO, '_fixtures', 'run-lock-'));
  try {
    return each(join(root, 'step.lock'), root);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

test('a lock is a live run\'s while its process runs and it is younger than the bound; else it is stale', () => {
  const alive = () => true;
  assert.equal(runLockStale(said(7), NOW, { lives: alive }), false);
  assert.equal(runLockStale(said(7), NOW, { lives: () => false }), true, 'its process is gone');
  assert.equal(runLockStale(said(7, NOW - RUN_LOCK_STALE_MS - 1), NOW, { lives: alive }), true, 'older than the bound');
  for (const text of ['', '{', 'null', '[]', '{"pid":"7","since":"2026-11-05T09:00:00Z"}', '{"pid":7}', '{"pid":7,"since":"later"}']) {
    assert.equal(runLockStale(text, NOW, { lives: alive }), true, text);
  }
});

test('the lock is held for the work and let go after it, when it returns and when it throws, leaving nothing beside', () => scratch((lock, root) => {
  assert.equal(withRunLock(lock, () => {
    assert.equal(JSON.parse(readFileSync(lock, 'utf8')).pid, process.pid);
    return 'done';
  }), 'done');
  assert.equal(existsSync(lock), false);
  assert.throws(() => withRunLock(lock, () => { throw new Error('the build failed'); }), /the build failed/);
  assert.equal(existsSync(lock), false);
  assert.deepEqual(readdirSync(root), []);
}));

test('a live run\'s lock is waited on; a stale one is taken; a release leaves a lock another run wrote', () => scratch((lock) => {
  writeFileSync(lock, said(7));
  const lives = { 7: true };
  let waits = 0;
  // The holder ends after two looks.
  const pause = () => { waits += 1; if (waits === 2) lives[7] = false; };
  assert.equal(withRunLock(lock, () => 'mine', { lives: (pid) => lives[pid] ?? pid === process.pid, now: () => NOW, pause }), 'mine');
  assert.equal(waits, 2);

  withRunLock(lock, () => writeFileSync(lock, said(8)));
  assert.equal(readFileSync(lock, 'utf8'), said(8), 'a release removed a lock it did not write');
}));

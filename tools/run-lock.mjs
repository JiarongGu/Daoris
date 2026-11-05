/**
 * One run at a time of a step that builds and writes files every run of it shares, by a lock file a crash cannot leave held.
 *
 * The lock is `{ pid, since }` at the path it is named by. Taking it puts it in place with no overwrite, written beside first, so it
 * is never read half written; a run that finds it held waits, looking again every `POLL_MS`. A lock is stale where it does not read
 * as one, where its process is gone (a run that died holds nothing), or where it is older than the bound (a reused process id holds
 * it that long at most); a stale lock is removed only while it still says what was judged stale, and taken. A release removes the
 * lock only while it still says what its holder wrote.
 */
import { randomBytes } from 'node:crypto';
import { linkSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

/** How long a run may hold the lock before another takes it for a dead one's: longer than any build a held step makes. */
export const RUN_LOCK_STALE_MS = 15 * 60_000;

const POLL_MS = 200;

/** Whether a process runs with this id: signal 0 tests and sends nothing, and one not ours to signal still runs. */
export function processLives(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error.code === 'EPERM';
  }
}

/** Whether the lock's `text` at `now` (milliseconds) is no live run's. */
export function runLockStale(text, now, { lives = processLives, staleMs = RUN_LOCK_STALE_MS } = {}) {
  let said;
  try {
    said = JSON.parse(text);
  } catch {
    return true;
  }
  const at = Date.parse(said?.since);
  if (!Number.isInteger(said?.pid) || Number.isNaN(at) || now - at > staleMs) return true;
  return !lives(said.pid);
}

/** The lock's text; null where there is none, undefined where it cannot be read this moment (Windows, a delete under way). */
const peek = (path) => {
  try {
    return readFileSync(path, 'utf8');
  } catch (error) {
    return error.code === 'ENOENT' ? null : undefined;
  }
};

const removeQuietly = (path) => {
  try {
    unlinkSync(path);
  } catch {
    // Gone already, or held by a reader for a moment: the next look judges it again.
  }
};

const sleep = (ms) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);

/**
 * Run `work` holding the lock at `path`, waiting while a live run holds it, and answer what `work` answers. The lock goes when
 * `work` returns or throws. `lives`, `now`, `staleMs` and `pause` are a test's.
 */
export function withRunLock(path, work, { lives = processLives, now = Date.now, staleMs = RUN_LOCK_STALE_MS, pause = sleep } = {}) {
  mkdirSync(dirname(path), { recursive: true });
  const beside = `${path}.${process.pid}.${randomBytes(4).toString('hex')}`;
  let mine;
  try {
    for (;;) {
      mine = `${JSON.stringify({ pid: process.pid, since: new Date(now()).toISOString() })}\n`;
      writeFileSync(beside, mine);
      try {
        linkSync(beside, path);
        break;
      } catch (error) {
        if (error.code !== 'EEXIST') throw error;
      }
      const held = peek(path);
      if (held === null) continue;
      if (held !== undefined && runLockStale(held, now(), { lives, staleMs }) && peek(path) === held) removeQuietly(path);
      else pause(POLL_MS);
    }
  } finally {
    removeQuietly(beside);
  }
  try {
    return work();
  } finally {
    if (peek(path) === mine) removeQuietly(path);
  }
}

/**
 * The merge tool's lock files, in a unit of their own: the verdict store's lock and the gate tree's, both held by a run and
 * both put in place complete (`run-lock.mjs`), so no run reads one half written or removes one another run took since.
 */
import { randomBytes } from 'node:crypto';
import { readFileSync, rmSync } from 'node:fs';
import { putIfAbsent, withRunLock } from '../run-lock.mjs';

/** How long a verdict write may hold its lock: a write takes milliseconds, so a longer one is a run that hung or died. */
const STORE_STALE_MS = 30_000;

/**
 * `fn` run holding a lock beside `file`, so two runs writing one store (a merge's and a full set's beside it) never write over each
 * other's verdict. A waiter waits while the holder runs and the lock is younger than `staleMs`, and takes a lock past it, so a hung
 * run costs a wait and never a run that never ends; a lock is taken over only after a second look finds it stale, and let go
 * only while it still says what this run wrote. `lives`, `now` and `pause` are a test's.
 */
export function holdingLock(file, fn, { staleMs = STORE_STALE_MS, ...clock } = {}) {
  return withRunLock(`${file}.lock`, fn, { staleMs, ...clock });
}

/** The lock's text at `path`; null where there is none or it cannot be read. */
export function lockText(path) {
  try {
    return readFileSync(path, 'utf8');
  } catch {
    return null;
  }
}

/**
 * The lock at `path` removed only while it still reads as `text`, which a caller judged dead: the lock a second run put in place
 * since is that run's own. Whether this removed it.
 */
export function removeWhileSame(path, text) {
  if (text === null || lockText(path) !== text) return false;
  rmSync(path, { force: true });
  return true;
}

/**
 * The lock file at `path` put in place holding `text`, with no overwrite and never read empty: written beside first and linked in.
 * Whether it is there now, which is false where another run's lock already is.
 */
export function putComplete(path, text) {
  const beside = `${path}.${process.pid}.${randomBytes(4).toString('hex')}`;
  try {
    return putIfAbsent(path, beside, text);
  } finally {
    rmSync(beside, { force: true });
  }
}

/**
 * `text` put in place as the lock at `path`, taking over one `live` (given the other run's text) says no run holds; a lock another run
 * put in place meanwhile is left. `held` is the text of a live run's lock, null where no run holds one or the attempts ran out.
 */
export function takeLock(path, text, live, attempts = 3) {
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    if (putComplete(path, text)) return { taken: true, held: null };
    const seen = lockText(path);
    if (seen !== null && live(seen)) return { taken: false, held: seen };
    removeWhileSame(path, seen);
  }
  return { taken: false, held: null };
}

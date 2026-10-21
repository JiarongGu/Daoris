// One writer at a time from the re-read to the rename of a home file an edit keeps (CONFIGLOCK1, D173's CONFIG1 note). A keeping
// write reads the file again, makes its edit onto it and renames its own text over it; another writer whose rename landed between
// that read and that rename was lost. Both twins take the same lock file, so a terminal's edit and the desktop's wait for each other.
//
// It is the CLI's twin of the driver's `KeepLock`; `keeplock.test.ts` reads `driver-config-keeps.json`'s `lock` rows, as
// `KeepLockTests` does. The rules both keep:
//
//   1. Where: `<file>.lock`, the file's full path with `.lock` after it, beside the file whoever names it (never the home's
//      `locks/`, which a writer handed a file does not know, and never `driver.lock`, an older build's lease).
//   2. Taking it is putting it in place with no overwrite, written beside first (twins.md: Node links, C# moves), so it is never
//      seen half written. It says `{ "pid", "since" }`, `since` in UTC to the millisecond. A release removes it only while it still
//      says what its holder wrote.
//   3. A writer that finds it waits, looking every 15 ms, for ten seconds (the driver's `RecordLock.Wait`), then is refused as a tool
//      error naming the lock and its holder, nothing written.
//   4. Stale: its process gone, written more than a minute ago (a write holds it for milliseconds, so a reused process id holds it a
//      minute at most), or not that object. It is removed only while it still says what was judged stale, then taken.

import { randomBytes } from 'node:crypto';
import { linkSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { DaorisError } from './errors.ts';

/**
 * Whether a process runs with this id: signal 0 tests and sends nothing; one not ours to signal still runs. The home's lease asked it
 * here until the lease left with the last family's TypeScript (PUSH1f2f): a stale keep lock is still judged by it.
 */
function processLives(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === 'EPERM';
  }
}

/** How long a writer waits for another's edit, in milliseconds, before it is refused (the driver's `KeepLock.Wait`). */
export const KEEP_LOCK_WAIT_MS = 10_000;

/** How old a lock may be, in milliseconds, before it is taken for one whose holder died (the driver's `KeepLock.Stale`). */
export const KEEP_LOCK_STALE_MS = 60_000;

const POLL_MS = 15;

/** A lock held: released once, its file removed only while it says what this holder wrote. */
export interface KeepLock {
  readonly path: string;
  release(): void;
}

/** Where the lock of `file` is: its full path with `.lock` after it. */
export function keepLockPath(file: string): string {
  return `${resolve(file)}.lock`;
}

/**
 * Whether a lock's text at `now` (milliseconds) is no live holder's: one that does not read as `{ pid, since }`, one written more than
 * `KEEP_LOCK_STALE_MS` before `now`, or one whose process `lives` says is gone. The driver's `KeepLock.IsStale`.
 */
export function keepLockStale(text: string, now: number, lives: (pid: number) => boolean = processLives): boolean {
  let said: unknown;
  try {
    said = JSON.parse(text);
  } catch {
    return true;
  }
  if (typeof said !== 'object' || said === null || Array.isArray(said)) return true;
  const { pid, since } = said as Record<string, unknown>;
  if (typeof pid !== 'number' || !Number.isInteger(pid) || typeof since !== 'string') return true;
  const at = Date.parse(since);
  if (Number.isNaN(at) || now - at > KEEP_LOCK_STALE_MS) return true;
  return !lives(pid);
}

/**
 * Hold the lock of `file`, waiting while another writer holds it, for at most `within` milliseconds. Synchronous, as every keeping
 * write is.
 *
 * @throws DaorisError It was held by a live writer for the whole of the wait; nothing was taken.
 */
export function takeKeepLock(
  file: string, { within = KEEP_LOCK_WAIT_MS, lives = processLives }: { within?: number; lives?: (pid: number) => boolean } = {},
): KeepLock {
  const path = keepLockPath(file);
  mkdirSync(dirname(path), { recursive: true });
  const until = Date.now() + within;
  for (;;) {
    const mine = `${JSON.stringify({ pid: process.pid, since: new Date().toISOString() })}\n`;
    const failed = create(path, mine);
    if (failed === null) return held(path, mine);

    const seen = readText(path);
    // Stale: removed only while it still says what was judged stale, so a lock another has just taken is not the one removed.
    if (seen !== null && keepLockStale(seen, Date.now(), lives) && readText(path) === seen && remove(path)) continue;
    if (Date.now() >= until) {
      const holder = seen === null ? `nothing this writer could read (${failed.message})` : holderOf(seen);
      throw new DaorisError(`the edit lock ${path} was held by ${holder} for the whole of the wait, so this edit was not written. Try again.`);
    }
    sleep(POLL_MS);
  }
}

/** `act` done holding the lock of `file`, released however it ends. */
export function withKeepLock<T>(file: string, act: () => T): T {
  const held = takeKeepLock(file);
  try {
    return act();
  } finally {
    held.release();
  }
}

function held(path: string, written: string): KeepLock {
  let released = false;
  return {
    path,
    release() {
      if (released) return;
      released = true;
      // A scanner holding the file a moment refuses the removal; a lock left behind would hold every writer for its minute.
      for (let attempt = 0; attempt < 20 && readText(path) === written; attempt++) {
        if (remove(path)) return;
        sleep(POLL_MS);
      }
    },
  };
}

/** The holder a lock names, as a refusal says it. */
function holderOf(text: string): string {
  try {
    const { pid, since } = JSON.parse(text) as { pid?: unknown; since?: unknown };
    return `process ${String(pid)} since ${String(since)}`;
  } catch {
    return 'a writer';
  }
}

/** Written beside, then linked into place only where nothing is: never over another holder's lock. Null, or why it was not. */
function create(path: string, text: string): Error | null {
  const beside = `${path}.${randomBytes(8).toString('hex')}.writing`;
  try {
    writeFileSync(beside, text, 'utf8');
    linkSync(beside, path);
    return null;
  } catch (error) {
    // Almost always: a lock is already there. Otherwise the wait's end says what.
    return error as Error;
  } finally {
    remove(beside);
  }
}

/** The lock's text, or null where there is none or none readable this moment. */
function readText(path: string): string | null {
  try {
    return readFileSync(path, 'utf8');
  } catch {
    return null;
  }
}

function remove(path: string): boolean {
  try {
    unlinkSync(path);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === 'ENOENT';
  }
}

/** A synchronous pause: a keeping write is one straight line, with no event loop to yield to. */
function sleep(ms: number): void {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

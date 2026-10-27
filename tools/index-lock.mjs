/**
 * QUEUELOCK1: what the merge queue says when a promotion is held on git's `index.lock` (FIX-LOG 2026-10-28, QUEUELOCK1).
 *
 * A stale 0-byte `.git/index.lock` in the main checkout, left by a git that died, refused the queue's fast-forward for about
 * 1 h 45 min while the queue said only `index.lock: File exists`. The note here names the lock's age and whether a git process
 * is running, so a person can tell a lock in use from one left behind. It only reads: the queue never removes the lock, which
 * only the person who knows no git is writing may do.
 *
 * The functions are pure but for `readIndexLock`, which asks the file system; the process list is handed in.
 */
import { statSync } from 'node:fs';
import { join } from 'node:path';

/** Whether git's own words say an `index.lock` refused it. */
export const mentionsIndexLock = (text) => /index\.lock/.test(text ?? '');

/** An age in milliseconds as `42 s`, `7 min`, `1 h 45 min` or `2 d 3 h`; a clock that ran backward reads as `0 s`. */
export function ageText(ms) {
  const seconds = Math.max(0, Math.floor(ms / 1000));
  if (seconds < 60) return `${seconds} s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return minutes % 60 ? `${hours} h ${minutes % 60} min` : `${hours} h`;
  return hours % 24 ? `${Math.floor(hours / 24)} d ${hours % 24} h` : `${Math.floor(hours / 24)} d`;
}

/**
 * The lock in git folder `gitDir` as `{ ageMs, bytes }`, or null where there is none. Its age is its last write's, at `now`
 * (milliseconds). Any other failure to look is thrown: a lock that could not be read is not an absent one.
 */
export function readIndexLock(gitDir, now = Date.now()) {
  try {
    const stat = statSync(join(gitDir, 'index.lock'));
    return { ageMs: now - stat.mtimeMs, bytes: stat.size };
  } catch (error) {
    if (error?.code === 'ENOENT') return null;
    throw error;
  }
}

/** The names a git process has: `git` and `git.exe`, as the first word of a command line or as an executable's file. */
const GIT = /^git(?:\.exe)?$/i;
const word = (text) => {
  const first = /^\s*(?:"([^"]*)"|(\S+))/.exec(text ?? '');
  return ((first?.[1] ?? first?.[2] ?? '').split(/[\\/]/).pop() ?? '');
};

/** The pids of the git processes among `processes` (`{ pid, exe, command }`, as `parseProcesses` gives them). */
export function gitProcessIds(processes) {
  return processes.filter((one) => GIT.test(word(one.exe)) || GIT.test(word(one.command))).map((one) => one.pid);
}

/**
 * The plain statement of a held lock: `lock` from `readIndexLock` (null for none), and `ran` from the process list, `{ processes }`
 * or `{ error }` for a list that could not be read. Null where there is no lock to speak of.
 */
export function indexLockNote(lock, ran) {
  if (!lock) return null;
  const how = `.git/index.lock is ${ageText(lock.ageMs)} old (${lock.bytes} bytes)`;
  const never = 'the queue never removes it';
  if (ran.error) return `${how}; whether a git process is running could not be read (${ran.error}), so it is not known whether it is stale; ${never}, and a person removes it once no git is writing`;
  const pids = gitProcessIds(ran.processes);
  if (!pids.length) return `${how} and no git process is running, so it looks left behind by a git that died; ${never}, and a person removes it, after which the promotion goes on at the next look`;
  return `${how} and a git process is running (pid ${pids.slice(0, 5).join(', ')}${pids.length > 5 ? ', …' : ''}), which may hold it, so the promotion waits for it; ${never}`;
}

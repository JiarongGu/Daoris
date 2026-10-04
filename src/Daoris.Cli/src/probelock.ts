// One status question per configuration home at a time (TOOL6g). An agent asked whether an account is signed in may
// refresh that account's token to answer, and two asked at once on one account spend one single-use refresh token twice:
// the second is refused, and the agent signs the account out, the first one's new tokens with it. The desktop's probe and
// `daoris agent list` ask the same accounts, so the guard is a file both take.
//
// It is the CLI's twin of the driver's `ProbeLock`; `probelock.test.ts` reads `ProbeLockTests.cs`'s rows, cell for cell.
// The rules both keep:
//
//   1. Where: beside the accounts, under the home's `harnesses/.probing/`, never inside an account's directory, which is
//      the agent's (D66 §3): `<agent>/<account>.lock` for an account, `<agent>.lock` for the tool's own sign-in.
//   2. Taking it is creating the file new, so two takers cannot both hold it; it says who took it and when, and letting
//      go deletes it.
//   3. A file last written more than a minute ago (three times a status question's 20 seconds) is a holder that died,
//      and the next taker removes it.
//   4. A taker that cannot have it within its patience (30 seconds) does not ask: the account is then unknown, never
//      asked twice at once.
//   5. A sign-in or a key into an account through either door leaves `<agent>/<account>.signed-in` beside its lock. The
//      desktop believes the agent's word that an account is signed out until a mark is newer than it (or an hour has
//      passed), so a start held on a signed-out account asks it once per sign-out, never at every look.
//
// It spawns nothing and opens no socket.

import { closeSync, mkdirSync, openSync, statSync, unlinkSync, writeSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { writeTextAtomic } from './fsx.ts';

/** The folder under the home's `harnesses` the locks are kept in; no agent is ever named so. */
export const PROBE_FOLDER = '.probing';

/** How old a lock file may be, in milliseconds, before it is taken for a holder that died. */
export const PROBE_STALE_MS = 60_000;

/** How long a taker waits for another holder, in milliseconds, before it gives up and does not ask. */
export const PROBE_PATIENCE_MS = 30_000;

const POLL_MS = 100;

/** The lock for one configuration home: an account's, or with `profile` null the tool's own. */
export function probeLockPath(home: string, owner: string, profile: string | null): string {
  return profile === null
    ? join(home, 'harnesses', PROBE_FOLDER, `${owner}.lock`)
    : join(home, 'harnesses', PROBE_FOLDER, owner, `${profile}.lock`);
}

/**
 * Where a sign-in or a key into an account is marked, beside its lock (rule 5): the desktop believes the agent's word that
 * an account is signed out until a mark is newer than it, so a sign-in at the terminal is seen at its next look.
 */
export function signedInPath(home: string, owner: string, profile: string): string {
  return join(home, 'harnesses', PROBE_FOLDER, owner, `${profile}.signed-in`);
}

/** Mark a sign-in or a key into an account. A mark not written costs the desktop one sign-out's backstop, never the sign-in. */
export function markSignedIn(home: string, owner: string, profile: string, now: Date): void {
  try {
    // The same line the driver's `MarkSignedIn` writes; only its last write is read.
    writeTextAtomic(signedInPath(home, owner, profile), `${JSON.stringify({ at: now.toISOString().replace(/\.\d{3}Z$/, 'Z') })}\n`);
  } catch {
    // The sign-in stands.
  }
}

/** Whether a lock file was last written more than `PROBE_STALE_MS` before `now`; a missing one is not. */
export function probeLockStale(path: string, now: Date): boolean {
  try {
    return now.getTime() - statSync(path).mtimeMs > PROBE_STALE_MS;
  } catch {
    return false;
  }
}

/**
 * Take the lock at `path`, waiting while another holds it, for at most `patience` milliseconds. The release, or null
 * when it stayed held: the caller then does not ask. Synchronous, as the terminal's probe is.
 */
export function takeProbeLock(path: string, patience = PROBE_PATIENCE_MS): (() => void) | null {
  const until = Date.now() + patience;
  for (;;) {
    try {
      mkdirSync(dirname(path), { recursive: true });
      const fd = openSync(path, 'wx');
      try {
        writeSync(fd, `${JSON.stringify({ pid: process.pid, at: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z') })}\n`);
      } finally {
        closeSync(fd);
      }
      return () => {
        try {
          unlinkSync(path);
        } catch {
          // Gone already: a taker that thought it stale removed it.
        }
      };
    } catch (error) {
      const code = (error as NodeJS.ErrnoException).code;
      // Held by another (on Windows a file the driver holds open reads as busy rather than there).
      if (code !== 'EEXIST' && code !== 'EPERM' && code !== 'EBUSY') return null;
    }

    if (probeLockStale(path, new Date())) {
      try {
        unlinkSync(path);
      } catch {
        // Another taker removed it first, or its holder still has it: the next try says which.
      }
      continue;
    }

    if (Date.now() >= until) return null;
    sleep(POLL_MS);
  }
}

/** A synchronous pause: the terminal's probe is one straight line, with no event loop to yield to. */
function sleep(ms: number): void {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

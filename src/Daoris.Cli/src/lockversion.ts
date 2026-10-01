import type { Lock } from './types.ts';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText } from './fsx.ts';
import { compareVersions } from './notes.ts';
import { DaorisError } from './errors.ts';

/** A lock a newer canon wrote than the one this tool carries, and the command that runs the tool at the lock's. */
export interface NewerLock {
  /** The canon version the lock records. */
  locked: string;
  /** The canon version this tool carries. */
  carried: string;
  /** The tool at the lock's version, by npm's spelling, which a manifest's `source` uses too (D105). */
  run: string;
}

/** The version in `canon.json` at `canonRoot`, or null when there is none to read. */
export function canonVersionAt(canonRoot: string): string | null {
  const file = join(canonRoot, 'canon.json');
  if (!existsSync(file)) return null;
  const version = (JSON.parse(readText(file)) as { version?: unknown }).version;
  return typeof version === 'string' ? version : null;
}

/**
 * Whether a newer canon than the one this tool carries wrote `lock`, compared by number (`0.10.0` follows
 * `0.9.0`). Null for no lock, the same version, or an older one: the ordinary upgrade.
 *
 * During `0.0.x` every build carries `0.0.1`, so two builds of it read as the same version and this cannot
 * tell them apart until a release moves the number (D124 §1.4).
 */
export function newerLock(lock: Pick<Lock, 'canonVersion'> | null, carried: string): NewerLock | null {
  if (!lock || compareVersions(lock.canonVersion, carried) <= 0) return null;
  return { locked: lock.canonVersion, carried, run: `npx daoris@${lock.canonVersion}` };
}

/**
 * 🔴 An older doctrine tool never rewrites a newer lock (WSSETUP4, D124 §1.4).
 *
 * Drift is measured against the lock (D13), so to an older canon a file the newer one improved and nobody
 * touched here reads as *improved upstream, untouched here* (D19), and `sync` would rewrite it to the older
 * text and stamp the lock back; `upstream` would write the newer canon's words over the older canon's file. Two
 * versions on one machine is a hazard with npm alone, and an install carries a copy of its own (D124 §1.2), so
 * the tool refuses: exit 1, a policy refusal, naming both versions and the command at the lock's.
 *
 * @param argv the command as it was given, verb first, said back at the lock's version.
 */
export function refuseNewerLock(lock: Pick<Lock, 'canonVersion'> | null, carried: string, argv: readonly string[]): void {
  const newer = newerLock(lock, carried);
  if (!newer) return;
  throw new DaorisError(
    `daoris.lock was written by canon ${newer.locked}, and this daoris carries canon ${newer.carried} — an older `
    + 'canon would rewrite what the newer one wrote, so nothing was changed.\n'
    + `  Run the daoris at the lock's version: ${[newer.run, ...argv].join(' ')}`,
    1);
}

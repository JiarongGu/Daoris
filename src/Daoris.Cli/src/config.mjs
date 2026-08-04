import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText, writeTextAtomic } from './fsx.mjs';
import { DaorisError } from './errors.mjs';
import { DEFAULT_HARNESS, resolveHarness } from './harness.mjs';

export const MANIFEST_FILE = 'daoris.json';
export const LOCK_FILE = 'daoris.lock';
export const LOCK_VERSION = 1;

// `harness` names which agent layout to generate. It defaults rather than being required, because a
// manifest written before harnesses existed must keep working — and because there is one supported
// value today (D23), so demanding it would be ceremony.
const DEFAULTS = { packs: [], harness: DEFAULT_HARNESS, target: null, coreBudgetBytes: 24000 };

export function readManifest(root) {
  const file = join(root, MANIFEST_FILE);
  if (!existsSync(file)) {
    throw new DaorisError(`no ${MANIFEST_FILE} in '${root}' — run 'daoris init' first`);
  }
  const manifest = { ...DEFAULTS, ...JSON.parse(readText(file)) };
  if (!manifest.source) throw new DaorisError(`${MANIFEST_FILE} has no 'source'`);

  // Resolve here so an unknown name fails at the edge, naming what exists, rather than deeper down
  // where the message would be about a missing directory.
  manifest.harnessDescriptor = resolveHarness(manifest.harness);
  manifest.target ??= manifest.harnessDescriptor.defaultTarget;
  return manifest;
}

export function writeManifest(root, manifest) {
  writeTextAtomic(join(root, MANIFEST_FILE), `${JSON.stringify(manifest, null, 2)}\n`);
}

export function readLock(root) {
  const file = join(root, LOCK_FILE);
  return existsSync(file) ? JSON.parse(readText(file)) : null;
}

/**
 * Entries are sorted by target and the shape is fixed, so the lock diffs
 * cleanly in review — a reviewer should see what moved, not a reshuffle.
 */
export function writeLock(root, lock) {
  const sorted = {
    version: LOCK_VERSION,
    canonVersion: lock.canonVersion,
    source: lock.source,
    entries: [...lock.entries].sort((a, b) => a.target.localeCompare(b.target)),
  };
  writeTextAtomic(join(root, LOCK_FILE), `${JSON.stringify(sorted, null, 2)}\n`);
}

/** The lock is the authority on what is canonical: anything absent here is local. */
export function lockIndex(lock) {
  return new Map((lock?.entries ?? []).map((entry) => [entry.target, entry]));
}

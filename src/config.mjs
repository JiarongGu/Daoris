import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText, writeTextAtomic } from './fsx.mjs';
import { DaorisError } from './errors.mjs';

export const MANIFEST_FILE = 'daoris.json';
export const LOCK_FILE = 'daoris.lock';
export const LOCK_VERSION = 1;

const DEFAULTS = { packs: [], target: '.claude', coreBudgetBytes: 24000 };

export function readManifest(root) {
  const file = join(root, MANIFEST_FILE);
  if (!existsSync(file)) {
    throw new DaorisError(`no ${MANIFEST_FILE} in '${root}' — run 'daoris init' first`);
  }
  const manifest = { ...DEFAULTS, ...JSON.parse(readText(file)) };
  if (!manifest.source) throw new DaorisError(`${MANIFEST_FILE} has no 'source'`);
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

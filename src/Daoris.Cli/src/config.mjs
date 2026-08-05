import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText, writeTextAtomic } from './fsx.mjs';
import { DaorisError } from './errors.mjs';
import { DEFAULT_HARNESS, resolveHarness } from './harness.mjs';

export const MANIFEST_FILE = 'daoris.json';
const LOCK_FILE = 'daoris.lock';
const LOCK_VERSION = 1;

// `harness` names which agent layout to generate. It defaults rather than being required, because a
// manifest written before harnesses existed must keep working — and because there is one supported
// value today (D23), so demanding it would be ceremony.
/**
 * `coreBudgetBytes` is what a repository is willing to pay, in always-loaded bytes, on every task.
 *
 * 30000 rather than the original 24000, and the reason is a design error the release rehearsal caught:
 * at 24000, core plus ONE pack already measured 24061, so a clean adopter's very first `check` failed
 * before they had written a single rule of their own. Worse, the gate then fired on the CANON — adding
 * a core rule broke a consumer — which is backwards. The budget exists to constrain what a repository
 * chooses to carry, not to cap what the doctrine may contain.
 *
 * 30000 leaves core, an index and two packs comfortably inside, so the gate fires on the case that
 * actually matters: a repository's OWN always-loaded material getting fat. That is the case it has
 * earned its keep on — it caught a 45% overage on first contact with one adopter, and forced the
 * retirement of an 8.3 KB duplicated rule in another.
 */
const DEFAULTS = { packs: [], harness: DEFAULT_HARNESS, target: null, coreBudgetBytes: 30000 };

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

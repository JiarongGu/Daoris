import { existsSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { readText, sha256, writeTextAtomic } from './fsx.mjs';
import { makeHeader, stripHeader, withHeader } from './document.mjs';
import { significantTokens, containment } from './twins.mjs';
import { readCanon, resolveCanonRoot, selectFiles } from './canon.mjs';
import { lockIndex, readLock, readManifest, writeLock } from './config.mjs';
import { buildIndex, writeIndex } from './indexgen.mjs';
import { DaorisError } from './errors.mjs';

/**
 * Planning is separate from applying so the plan can be printed (--dry-run) or
 * asserted in a test without touching disk.
 *
 * Only files the canon selects are considered; anything else in the target
 * directory is this repo's own and is invisible to the tool.
 */
export function planSync({ root, manifest, canon, lock }) {
  const locked = lockIndex(lock);
  const selected = selectFiles(canon, manifest.packs);
  const writes = [];
  const drifted = [];
  const collisions = [];

  for (const file of selected) {
    const body = readText(join(canon.root, file.source));
    // Only markdown gets stamped: an HTML comment in a script is a syntax
    // error, and the lock's hash catches an edit to it either way (D6).
    const content = file.target.endsWith('.md')
      ? withHeader(makeHeader(file.pack, file.source, canon.version), body)
      : body;
    const digest = sha256(content);
    const abs = join(root, manifest.target, file.target);
    const entry = locked.get(file.target);

    let state = 'create';
    if (existsSync(abs)) {
      const onDisk = sha256(readText(abs));
      state = onDisk === digest ? 'unchanged' : 'update';

      if (entry) {
        // In the lock, so compare against what daoris last WROTE, not against
        // what the canon says now. A file still matching its lock entry is
        // untouched, and the difference is an upstream improvement — refusing
        // that would break the very direction the tool exists to serve, and
        // would accuse every consumer of an edit nobody made.
        //
        // A file matching the NEW canon is likewise not drift, whatever the
        // lock says: that is the state right after `upstream`, where the edit
        // has already become canonical and only the lock hash is stale. Demand
        // --force there and the return path ends by telling the contributor to
        // discard the improvement they just promoted.
        if (onDisk !== entry.sha256 && onDisk !== digest) drifted.push(file.target);
      } else if (onDisk !== digest) {
        // Not in the lock: the repo wrote this file itself, before it ever
        // adopted daoris. Silently overwriting it would destroy work the tool
        // never had any claim to.
        collisions.push(file.target);
      }
    }
    writes.push({ ...file, content, sha256: digest, state });
  }

  // A file that left the canon leaves every repo — the thing copy-paste can never do.
  const wanted = new Set(selected.map((file) => file.target));
  const deletes = [...locked.keys()].filter((target) => !wanted.has(target));
  const renames = detectRenames({ root, manifest, writes, deletes });
  return { writes, deletes, drifted, collisions, renames };
}

/**
 * A canonical file renamed upstream arrives as a delete plus an add, which loses
 * nothing and explains nothing. Pair them up by CONTENT rather than by a
 * declared `renamedFrom` field: a metadata ledger is a second source of truth
 * that can claim a rename which never happened, while content cannot lie about
 * what moved. This is how version control has always done it, for the same reason.
 *
 * Reporting only — the delete and the create still happen exactly as before.
 * Deliberately conservative: below the bar it stays a retirement plus an
 * addition, which is the honest description of two unrelated changes.
 */
const RENAME_SIMILARITY = 0.6;

function detectRenames({ root, manifest, writes, deletes }) {
  const created = writes.filter((write) => write.state === 'create');
  if (!created.length || !deletes.length) return [];

  const renames = [];
  const claimed = new Set();
  for (const from of deletes) {
    const abs = join(root, manifest.target, from);
    if (!existsSync(abs)) continue;
    const old = significantTokens(readText(abs));

    let best = null;
    for (const write of created) {
      if (claimed.has(write.target)) continue;
      const score = containment(old, significantTokens(write.content));
      if (score >= RENAME_SIMILARITY && (!best || score > best.score)) {
        best = { target: write.target, score };
      }
    }
    if (best) {
      claimed.add(best.target);
      renames.push({ from, to: best.target });
    }
  }
  return renames;
}

/**
 * What actually changed in the doctrine since this repo last synced.
 *
 * Computed from the lock and the shipped canon, so it needs no network and no
 * version control — the lock already records a per-file hash, which is a better
 * marker than "commits since last run" because it survives a shallow clone.
 *
 * Bodies are compared with the provenance header stripped: a version bump
 * rewrites every header, and listing all of them as changes is noise that
 * teaches people to stop reading the list. A file the repo has edited itself is
 * skipped rather than guessed at — that is drift, and it is reported as drift.
 */
export function planChanges({ root, manifest, canon, lock }) {
  const locked = lockIndex(lock);
  const selected = selectFiles(canon, manifest.packs);
  const added = [];
  const changed = [];

  for (const file of selected) {
    const entry = locked.get(file.target);
    if (!entry) {
      added.push(file.target);
      continue;
    }
    const abs = join(root, manifest.target, file.target);
    if (!existsSync(abs)) continue;
    const onDisk = readText(abs);
    if (sha256(onDisk) !== entry.sha256) continue;
    if (stripHeader(onDisk) !== readText(join(canon.root, file.source))) changed.push(file.target);
  }

  const wanted = new Set(selected.map((file) => file.target));
  return { added, changed, retired: [...locked.keys()].filter((t) => !wanted.has(t)) };
}

export function applySync({ root, manifest, plan, canonVersion, force }) {
  if (plan.collisions.length && !force) {
    throw new DaorisError(
      `this repo already has its own ${plan.collisions.join(', ')}\n` +
        `  daoris did not write those files and will not overwrite them. Move each aside\n` +
        `  (or fold anything worth keeping into the canon), then 'daoris sync' — or accept\n` +
        `  the canonical version with 'daoris sync --force'`,
      1,
    );
  }
  if (plan.drifted.length && !force) {
    throw new DaorisError(
      `${plan.drifted.length} vendored file(s) edited locally: ${plan.drifted.join(', ')}\n` +
        `  promote the edit with 'daoris upstream <file>', or discard it with 'daoris sync --force'`,
      1,
    );
  }

  for (const write of plan.writes) {
    if (write.state !== 'unchanged' || force) {
      writeTextAtomic(join(root, manifest.target, write.target), write.content);
    }
  }
  for (const target of plan.deletes) {
    rmSync(join(root, manifest.target, target), { force: true });
  }

  const lock = {
    canonVersion,
    source: manifest.source,
    entries: plan.writes.map(({ pack, source, target, sha256: digest }) => ({
      pack,
      source,
      target,
      canonVersion,
      sha256: digest,
    })),
  };
  writeLock(root, lock);
  writeIndex(root, manifest.target, buildIndex({ root, target: manifest.target, lock }));
  return lock;
}

export function commandSync({ root, argv, write, packageRoot }) {
  const manifest = readManifest(root);
  const canon = readCanon(resolveCanonRoot(packageRoot));
  const plan = planSync({ root, manifest, canon, lock: readLock(root) });

  // A rename is reported in place of the delete and the add it is made of,
  // because "these two are the same rule" is the part a reader cannot recover.
  const renamedFrom = new Set(plan.renames.map((rename) => rename.from));
  const renamedTo = new Set(plan.renames.map((rename) => rename.to));

  if (argv.includes('--dry-run')) {
    for (const rename of plan.renames) write(`  renamed   ${rename.from} -> ${rename.to}`);
    for (const entry of plan.writes) {
      if (entry.state !== 'unchanged' && !renamedTo.has(entry.target)) {
        write(`  ${entry.state.padEnd(9)} ${entry.target}`);
      }
    }
    for (const target of plan.deletes) {
      if (!renamedFrom.has(target)) write(`  retire    ${target}`);
    }
    for (const target of plan.drifted) write(`  DRIFTED   ${target}`);
    for (const target of plan.collisions) write(`  COLLIDES  ${target} (this repo's own)`);
    write(`daoris: ${plan.writes.length} file(s) selected, ${plan.deletes.length} to retire`);
    return plan.drifted.length || plan.collisions.length ? 1 : 0;
  }

  applySync({ root, manifest, plan, canonVersion: canon.version, force: argv.includes('--force') });
  for (const rename of plan.renames) write(`  renamed   ${rename.from} -> ${rename.to}`);
  const retired = plan.deletes.length - plan.renames.length;
  write(`daoris: synced ${plan.writes.length} file(s); retired ${retired}`);
  return 0;
}

import { existsSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { readText, sha256, writeTextAtomic } from './fsx.mjs';
import { makeHeader } from './document.mjs';
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

  for (const file of selected) {
    const body = readText(join(canon.root, file.source));
    const content = `${makeHeader(file.pack, file.source, canon.version)}\n${body}`;
    const digest = sha256(content);
    const abs = join(root, manifest.target, file.target);
    const entry = locked.get(file.target);

    let state = 'create';
    if (existsSync(abs)) {
      const onDisk = sha256(readText(abs));
      if (entry && onDisk !== entry.sha256) drifted.push(file.target);
      state = onDisk === digest ? 'unchanged' : 'update';
    }
    writes.push({ ...file, content, sha256: digest, state });
  }

  // A file that left the canon leaves every repo — the thing copy-paste can never do.
  const wanted = new Set(selected.map((file) => file.target));
  const deletes = [...locked.keys()].filter((target) => !wanted.has(target));
  return { writes, deletes, drifted };
}

export function applySync({ root, manifest, plan, canonVersion, force }) {
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

  if (argv.includes('--dry-run')) {
    for (const entry of plan.writes) {
      if (entry.state !== 'unchanged') write(`  ${entry.state.padEnd(9)} ${entry.target}`);
    }
    for (const target of plan.deletes) write(`  retire    ${target}`);
    for (const target of plan.drifted) write(`  DRIFTED   ${target}`);
    write(`daoris: ${plan.writes.length} file(s) selected, ${plan.deletes.length} to retire`);
    return plan.drifted.length ? 1 : 0;
  }

  applySync({ root, manifest, plan, canonVersion: canon.version, force: argv.includes('--force') });
  write(`daoris: synced ${plan.writes.length} file(s); retired ${plan.deletes.length}`);
  return 0;
}

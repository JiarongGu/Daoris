import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText, sha256, writeTextAtomic } from './fsx.mjs';
import { stripHeader } from './document.mjs';
import { resolveCanonRoot } from './canon.mjs';
import { lockIndex, readLock, readManifest } from './config.mjs';
import { DaorisError } from './errors.mjs';

/**
 * The 衍 half: a refinement found in one repo flows back and evolves the canon.
 * A one-way push would be distribution; this is what keeps the canon from
 * ossifying, which is why it ships in v0.1 rather than later.
 */
export function upstreamFile({ root, manifest, lock, canonRoot, file }) {
  const locked = lockIndex(lock);
  const normalized = file.replace(/\\/g, '/').replace(new RegExp(`^${manifest.target}/`), '');
  const entry =
    locked.get(normalized) ??
    [...locked.values()].find((candidate) => candidate.target.endsWith(`/${normalized}`));

  if (!entry) {
    throw new DaorisError(
      `'${file}' is not canonical in this repo — it is local, so there is nothing to upstream`,
    );
  }
  if (!existsSync(canonRoot)) throw new DaorisError(`no canon at '${canonRoot}'`);

  const body = stripHeader(readText(join(root, manifest.target, entry.target)));
  writeTextAtomic(join(canonRoot, entry.source), body);
  return { target: entry.target, source: entry.source };
}

/**
 * Promote every canonical file that differs from what the lock recorded. A
 * working session usually improves several rules at once, and one command per
 * file is friction on exactly the direction that must stay easy (D9).
 */
export function upstreamAll({ root, manifest, lock, canonRoot }) {
  const promoted = [];
  for (const entry of lock?.entries ?? []) {
    const abs = join(root, manifest.target, entry.target);
    if (!existsSync(abs)) continue;
    if (sha256(readText(abs)) === entry.sha256) continue;
    promoted.push(upstreamFile({ root, manifest, lock, canonRoot, file: entry.target }));
  }
  return promoted;
}

export function commandUpstream({ root, argv, write, packageRoot }) {
  const canonRoot = resolveCanonRoot(packageRoot);

  if (argv.includes('--all')) {
    const promoted = upstreamAll({
      root,
      manifest: readManifest(root),
      lock: readLock(root),
      canonRoot,
    });
    if (!promoted.length) {
      write('daoris: nothing to upstream — no canonical file differs from the lock');
      return 0;
    }
    for (const result of promoted) write(`  ${result.target} -> canon ${result.source}`);
    write(`daoris: promoted ${promoted.length} file(s)`);
    write("daoris: review and commit them in the canon repo, then 'daoris sync' here");
    return 0;
  }

  const file = argv.find((arg) => !arg.startsWith('--'));
  if (!file) throw new DaorisError('usage: daoris upstream <file> | daoris upstream --all');

  const result = upstreamFile({
    root,
    manifest: readManifest(root),
    lock: readLock(root),
    canonRoot,
    file,
  });
  write(`daoris: ${result.target} -> canon ${result.source}`);
  write("daoris: review and commit it in the canon repo, then 'daoris sync' here");
  return 0;
}

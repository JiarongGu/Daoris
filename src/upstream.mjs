import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText, writeTextAtomic } from './fsx.mjs';
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

export function commandUpstream({ root, argv, write, packageRoot }) {
  const file = argv.find((arg) => !arg.startsWith('--'));
  if (!file) throw new DaorisError('usage: daoris upstream <file>');

  const result = upstreamFile({
    root,
    manifest: readManifest(root),
    lock: readLock(root),
    canonRoot: resolveCanonRoot(packageRoot),
    file,
  });
  write(`daoris: ${result.target} -> canon ${result.source}`);
  write("daoris: review and commit it in the canon repo, then 'daoris sync' here");
  return 0;
}

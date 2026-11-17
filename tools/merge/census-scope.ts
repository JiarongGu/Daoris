/**
 * The censuses' scope at a merge. A census checks a rule over the sources, and at a merge only the changed files can
 * newly break a rule that holds of each file alone, so the merge names its changed paths (the ones its plan reads, `mergeChanges`) in
 * a file and passes the file's path as `CENSUS_SCOPE`; the desktop's census scan reads it (`CensusScope.cs` in the driver's tests):
 * a census marked diff-able judges only those files, a whole one the tree. A run that names no scope (the CI after the landing, a
 * developer's run) has every census judge the whole tree. A file, not the paths themselves, so a wide merge fits any environment.
 */
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { mergeChanges } from './changes.ts';

/** The variable the census scan reads: the path of the scope's file. */
export const CENSUS_SCOPE = 'CENSUS_SCOPE';

/** Where a merge's scopes are written, in the merge's checkout: `local/`, which every checkout ignores, a test's scratch repository too. */
export const SCOPE_FOLDER = ['local', 'scratch', 'census-scope'];

/**
 * The scope's file for `paths` under `root`, one path a line, named by its content: every gate of a merge names the same file, and a
 * file is never rewritten while a census reads it.
 */
export function scopeFile(root: string, paths: readonly string[]): string {
  const text = paths.map((path) => `${path}\n`).join('');
  const folder = join(root, ...SCOPE_FOLDER);
  const file = join(folder, `${createHash('sha256').update(text).digest('hex').slice(0, 16)}.txt`);
  if (!existsSync(file)) {
    mkdirSync(folder, { recursive: true });
    writeFileSync(file, text);
  }
  return file;
}

/**
 * A merge gate's environment: `env`, with verify's time ratchet standing aside (the merge runs it last) and the censuses' scope named
 * from the merge in place at `root`. A checkout whose changes cannot be read names no scope, and its censuses judge the whole tree.
 */
export function mergeGateEnvironment(env: NodeJS.ProcessEnv, root: string, changes: (root: string) => string[] = mergeChanges): NodeJS.ProcessEnv {
  const merged = { ...env, MERGE_GATES_RUN: '1' };
  let paths: string[];
  try {
    paths = changes(root);
  } catch {
    return merged;
  }
  return { ...merged, [CENSUS_SCOPE]: scopeFile(root, paths) };
}

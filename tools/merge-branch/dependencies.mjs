/**
 * The npm dependencies of a tree: which folders install from a lockfile, and whether an install is current. The gate tree and
 * a builder's worktree (`tools/worktree-setup.mjs`) read them one way.
 */
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { writeAtomic } from '../fsx.mjs';

/** The folders whose `package-lock.json` a tree installs from, as AGENTS.md has a fresh checkout do. */
export const DEPENDENCY_FOLDERS = ['.', 'src/Daoris.Web'];

/** Where a folder's install notes the lockfile it installed from: inside `node_modules`, which `npm ci` lays anew. */
export const DEPENDENCY_STAMP = '.gate-tree-lockfile';

const fileHash = (file) => createHash('sha256').update(readFileSync(file)).digest('hex');
const readJson = (file) => {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
};

/** The note that a folder's dependencies were installed from its lockfile as it is now. */
export function stampDependencies(tree, folder) {
  const stamp = join(tree, folder, 'node_modules', DEPENDENCY_STAMP);
  mkdirSync(dirname(stamp), { recursive: true });
  writeAtomic(stamp, `${fileHash(join(tree, folder, 'package-lock.json'))}\n`);
}

/**
 * Whether a folder's dependencies are `current` (its stamp is the lockfile's, or npm's own record of what is installed holds
 * only versions the lockfile has, which stamps it), `missing` (no lockfile or no install), `stale`, or `linked`: a
 * `node_modules` that is a link into another folder, which an install would write through and git would follow.
 */
export function dependencyState(tree, folder) {
  const lockfile = join(tree, folder, 'package-lock.json');
  const modules = join(tree, folder, 'node_modules');
  if (!existsSync(lockfile)) return 'missing';
  let entry;
  try {
    entry = lstatSync(modules);
  } catch {
    return 'missing';
  }
  if (entry.isSymbolicLink()) return 'linked';
  try {
    if (readFileSync(join(modules, DEPENDENCY_STAMP), 'utf8').trim() === fileHash(lockfile)) return 'current';
  } catch {
    // No stamp: an install made another way is judged by npm's own record below.
  }
  const installed = readJson(join(modules, '.package-lock.json'))?.packages;
  const wanted = readJson(lockfile)?.packages;
  if (!installed || !wanted) return 'stale';
  const keys = Object.keys(installed);
  if (keys.length === 0 || !keys.every((key) => wanted[key]?.version === installed[key]?.version)) return 'stale';
  stampDependencies(tree, folder);
  return 'current';
}

/** The folders of `DEPENDENCY_FOLDERS` with a lockfile whose dependencies are not current. */
export const staleDependencies = (tree) => DEPENDENCY_FOLDERS.filter((folder) => existsSync(join(tree, folder, 'package-lock.json')) && dependencyState(tree, folder) !== 'current');

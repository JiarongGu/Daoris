/** How the merge tool reads a change's paths and tells where a path is kept, in a unit of its own. */
import { isAbsolute, relative, sep } from 'node:path';

/** Paths from a `-z` listing: git quotes nothing there, so a name with any character comes back whole. */
export const zPaths = (text) => text.split('\0').map((path) => path.trim()).filter(Boolean);

/**
 * The paths a change differs by, from `git diff` of `revs` (`from to`, or `HEAD...branch`): read with `-z` and without renames, so each
 * path comes back whole and a moved file lists both its places. `run` is the tool's git, which answers `{ status, out }` and refuses on a
 * failure; with `allowFail` the failure is null instead (a tree long gone).
 */
export function diffPaths(run, root, revs, { allowFail = false } = {}) {
  const diff = run(root, ['--no-optional-locks', 'diff', '-z', '--name-only', '--no-renames', ...revs], { allowFail });
  return diff.status === 0 ? zPaths(diff.out) : null;
}

/**
 * Whether `file` lies inside `folder` and is not the folder itself, as a platform's path module sees them: a file on another
 * drive comes back from `relative` as an absolute path, and a name that merely starts with two dots is inside.
 */
export function isInside(folder, file, platform = { isAbsolute, relative, sep }) {
  const rel = platform.relative(folder, file);
  return rel !== '' && !platform.isAbsolute(rel) && rel.split(platform.sep)[0] !== '..';
}

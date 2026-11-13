/** The paths a merge asks its plan about: what the merge in place changes, and what a branch would. */
import { git } from './git.ts';
import { diffPaths, zPaths } from './paths.ts';

/** What the merge in place changes on main: tracked paths against HEAD, the parent's fixes included, and new files. */
export function mergeChanges(root: string): string[] {
  const tracked = zPaths(git(root, ['--no-optional-locks', 'diff', '-z', '--name-only', '--no-renames', 'HEAD']).out);
  const untracked = zPaths(git(root, ['ls-files', '-z', '--others', '--exclude-standard']).out);
  return [...new Set([...tracked, ...untracked])];
}

/** What a branch changes since it left main: a plan's question, asked before anything is merged. */
export const branchChanges = (root: string, branch: string): string[] => diffPaths(git, root, [`HEAD...${branch}`]) ?? [];

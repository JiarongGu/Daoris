/**
 * The builder worktrees the prune keeps because their branch is not merged, said for the steward: each with its age and last
 * commit, oldest first, so a builder that ended without landing is seen instead of holding a gigabyte unremarked.
 * The prune never removes one: an unmerged branch is work until someone says otherwise.
 */
import type { Worktree } from './git.ts';
import { git } from './git.ts';

/** A commit as the report names it. */
export type LastCommit = { at: number; sha: string; subject: string };

/** One unmerged builder worktree. */
export type Unmerged = { branch: string; path: string; last: LastCommit | null; ageDays: number | null };

const DAY = 86_400_000;
const inHarnessFolder = (path: string): boolean => /[\\/]\.claude[\\/]worktrees[\\/]/.test(path);

/**
 * The worktrees under `.claude/worktrees/` whose branch main does not hold (`merged` is the branches it does), but the main
 * checkout's and a Daoris session's or landing's (`daoris/`, `quest/`), which Daoris's housekeeping owns. `read` gives a
 * worktree's last commit, null where it could not be read.
 */
export function unmergedWorktrees(worktrees: readonly Worktree[], merged: readonly string[], read: (path: string) => LastCommit | null, now = Date.now()): Unmerged[] {
  return worktrees.slice(1)
    .filter((tree) => inHarnessFolder(tree.path) && !tree.bare && !merged.includes(tree.branch) && !/^(daoris|quest)\//.test(tree.branch))
    .map((tree) => {
      const last = read(tree.path);
      return { branch: tree.branch || '(detached)', path: tree.path, last, ageDays: last ? Math.max(0, Math.floor((now - last.at) / DAY)) : null };
    })
    .sort((a, b) => (b.ageDays ?? -1) - (a.ageDays ?? -1));
}

/** The report's lines: a header and one line a worktree; none where nothing is unmerged. */
export function unmergedLines(found: readonly Unmerged[]): string[] {
  if (!found.length) return [];
  const plural = found.length === 1 ? '' : 's';
  return [
    `${found.length} unmerged builder worktree${plural} kept (the branch is not in main, and the prune never removes one): land or remove each`,
    ...found.map((one) => {
      const what = one.last ? `${one.ageDays} day${one.ageDays === 1 ? '' : 's'}, ${one.last.sha} ${one.last.subject}` : 'no commit read';
      return `  ${one.branch}  ${one.path}  (${what})`;
    }),
  ];
}

/** The line the prune says of the `_fixtures/` sweep (`sweep` runs it): what went and what a run still held; its failure is said, never thrown. */
export function scratchLine(sweep: () => { removed: string[]; held: unknown[] }): string {
  try {
    const { removed, held } = sweep();
    return `_fixtures/: ${removed.length} entr${removed.length === 1 ? 'y' : 'ies'} older than a day removed${held.length ? `, ${held.length} held by a run and left` : ''}`;
  } catch (error) {
    return `_fixtures/: not swept (${(error as Error)?.message ?? error})`;
  }
}

/** A worktree's last commit by git, or null. */
export function lastCommitOf(path: string): LastCommit | null {
  const ran = git(path, ['log', '-1', '--format=%ct%x09%h%x09%s'], { allowFail: true });
  const [seconds, sha, ...subject] = ran.out.split('\t');
  const at = Number(seconds) * 1000;
  return ran.status === 0 && sha && Number.isFinite(at) ? { at, sha, subject: subject.join('\t') } : null;
}

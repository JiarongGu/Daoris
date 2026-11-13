/**
 * The commit check: every commit a branch adds carries its `Co-Authored-By:` line naming an allowed maker (a merge of main
 * into the branch needs none), and the branch's worktree, if it has one, holds nothing uncommitted (work a hand-back calls
 * done and left out of the merge).
 */
import { existsSync } from 'node:fs';
import { git, worktreeFor, lines } from './git.ts';
import { Refusal } from './args.ts';

export type Commit = { sha: string; merge: boolean; subject: string; trailer: string };

/** The log format `parseCommits` reads: short sha, parents, subject, the Co-Authored-By values. */
export const COMMIT_FORMAT = '--format=%h%x1f%p%x1f%s%x1f%(trailers:key=Co-Authored-By,valueonly)%x1e';

/** A branch's commits from `git log` in `COMMIT_FORMAT`: sha, whether it is a merge, subject, trailer. */
export function parseCommits(text: string): Commit[] {
  return text.split('\x1e').map((entry) => entry.trim()).filter(Boolean).map((entry) => {
    const [sha = '', parents = '', subject = '', trailer = ''] = entry.split('\x1f');
    return { sha, merge: parents.trim().split(/\s+/).length > 1, subject, trailer: trailer.trim() };
  });
}

/** The commits main lacks, one git start for both the count and the commit check. */
export function branchCommits(root: string, branch: string): Commit[] {
  const commits = parseCommits(git(root, ['log', COMMIT_FORMAT, `HEAD..${branch}`]).out);
  if (commits.length === 0) throw new Refusal(`'${branch}' has nothing main lacks: it is merged already`);
  return commits;
}

/**
 * What the commit check finds wrong: a commit of the branch's own without the trailer (a merge of main
 * into it carries none, and needs none), and work left uncommitted in its worktree.
 */
export function commitAttributionProblems(commits: Commit[]): string[] {
  // Makers of this repository's built-in agents (Adapters.cs), and Daoris itself for the commits its
  // driver writes (DaorisMaker.cs). Claude's harness has historically written its model name alone,
  // so accept Claude and Codex names as well as explicit makers.
  const maker = /\b(?:Anthropic|Claude|OpenAI|Codex|DeepSeek|Daoris)\b/i;
  const expected = 'expected its own maker and running model: Anthropic (Claude), OpenAI (Codex), DeepSeek, or Daoris';
  return commits.filter((commit) => !commit.merge && !commit.trailer.split('\n').some((line) => {
    const name = /^\s*(.+?)\s+<[^<>\s]+@[^<>\s]+>\s*$/.exec(line)?.[1];
    return name !== undefined && maker.test(name);
  })).map((commit) => `${commit.sha} ${commit.subject}: ${commit.trailer ? 'no allowed maker in its Co-Authored-By line' : 'no Co-Authored-By line'}; ${expected}`);
}

export function commitProblems(root: string, branch: string, commits: Commit[]): string[] {
  const problems = commitAttributionProblems(commits);
  const tree = worktreeFor(git(root, ['worktree', 'list', '--porcelain']).out, branch);
  if (tree && existsSync(tree)) {
    const left = lines(git(tree, ['--no-optional-locks', 'status', '--porcelain']).out);
    if (left.length) {
      problems.push(`its worktree holds uncommitted work, which the merge would leave out:\n${left.slice(0, 12).map((line) => `    ${line}`).join('\n')}`);
    }
  }
  return problems;
}

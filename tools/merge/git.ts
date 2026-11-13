/**
 * The merge tool's git: the one runner every read and write goes through, the checkout's git folder asked once, the
 * status and worktree listings read, and what a merge needs of the checkout before it starts.
 */
import { resolve, join } from 'node:path';
import { existsSync } from 'node:fs';
import { spawnGit } from '../fsx.mjs';
import { Refusal } from './args.ts';
import { sweepAndSay } from './merge-temps.ts';
import { SCRATCH } from './paths.ts';

export const MAIN = 'main';

export type Ran = { status: number | null; out: string; err: string };
export type Changed = { code: string; path: string };
export type Worktree = { path: string; branch: string; locked: boolean; lockReason: string; prunable: boolean; bare: boolean };

export function git(cwd: string, args: string[], { allowFail = false, env }: { allowFail?: boolean; env?: NodeJS.ProcessEnv | undefined } = {}): Ran {
  const result = spawnGit(args, { cwd, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, ...(env ? { env: { ...process.env, ...env } } : {}) });
  if (result.error) throw new Refusal(`git could not start: ${result.error.message}`);
  if (result.status !== 0 && !allowFail) {
    throw new Refusal(`git ${args.join(' ')} failed: ${(result.stderr || result.stdout).trim()}`);
  }
  return { status: result.status, out: result.stdout.trimEnd(), err: result.stderr.trim() };
}

/**
 * What a `git check-ignore -q` probe answered: status 0 is ignored, 1 is not, and anything else (128, or a
 * git that could not answer under load) is a tool error, not a false "not ignored".
 */
export function ignoredVerdict({ status, err }: { status: number | null; err: string }, path: string): boolean {
  if (status === 0) return true;
  if (status === 1) return false;
  throw new Refusal(`git check-ignore ${path} failed (status ${status}): ${err || 'no message'}`);
}

export const isIgnored = (cwd: string, path: string): boolean => ignoredVerdict(git(cwd, ['check-ignore', '-q', path], { allowFail: true }), path);

export const lines = (text: string): string[] => text.split('\n').filter((line) => line.trim());

// Each git start costs tens of milliseconds on Windows, so the checkout's git folder is asked for once,
// with its top level, and an open merge is read from the MERGE_HEAD file git writes there.
export const gitDirs = new Map<string, string>();
export function locate(cwd: string): string {
  const [top = '', gitDir = ''] = git(cwd, ['rev-parse', '--show-toplevel', '--absolute-git-dir']).out.split('\n').map((line) => line.trim());
  const root = resolve(top);
  gitDirs.set(root, resolve(gitDir));
  return root;
}
export const mergeInProgress = (root: string): boolean => existsSync(join(gitDirs.get(root) ?? join(root, '.git'), 'MERGE_HEAD'));
export const unmergedPaths = (root: string): string[] => lines(git(root, ['--no-optional-locks', 'diff', '--name-only', '--diff-filter=U']).out);

/**
 * `git status --porcelain=v2 --branch`, read: the current branch ('' when detached) and each changed or
 * untracked path with its two-letter code (`??` untracked).
 */
export function parseStatus(text: string): { branch: string; changed: Changed[] } {
  let branch = '';
  const changed: Changed[] = [];
  for (const line of text.replace(/\r\n/g, '\n').split('\n')) {
    if (line.startsWith('# branch.head ')) {
      const head = line.slice('# branch.head '.length).trim();
      branch = head === '(detached)' ? '' : head;
    } else if (line.startsWith('? ')) changed.push({ code: '??', path: line.slice(2) });
    else if (/^[12u] /.test(line)) {
      // Fields before the path: 8 for an ordinary entry, 9 for a rename (then `path<TAB>from`), 10 unmerged.
      const fields = line.split(' ');
      const skip = ({ 1: 8, 2: 9, u: 10 } as Record<string, number>)[line[0] as string];
      changed.push({ code: fields[1] as string, path: fields.slice(skip).join(' ').split('\t')[0] as string });
    }
  }
  return { branch, changed };
}

/** A tree that is not clean, each changed or untracked path with its code, the first twelve. */
export const notClean = (changed: Changed[]): string => {
  const shown = changed.slice(0, 12).map(({ code, path }) => `  ${code} ${path}`).join('\n');
  return `the tree is not clean:\n${shown}${changed.length > 12 ? `\n  … ${changed.length - 12} more` : ''}`;
};

/** Why a merge cannot start from what git reported, or null. */
export function startRefusal({ branch, merging, changed, ignored }: { branch: string; merging: boolean; changed: Changed[]; ignored: boolean }): string | null {
  if (branch !== MAIN) {
    return `the current branch is '${branch || 'a detached HEAD'}', not ${MAIN}: merges land on ${MAIN}, from its own checkout`;
  }
  if (merging) return 'a merge is already in progress: resolve it and run --continue, or git merge --abort';
  if (changed.length) return notClean(changed);
  if (!ignored) return `${SCRATCH}/ is not ignored here, so the gate logs would dirty the tree`;
  return null;
}

export const treeState = (root: string): { branch: string; changed: Changed[] } => parseStatus(git(root, ['--no-optional-locks', 'status', '--porcelain=v2', '--branch']).out);

export function requireMain(root: string): void {
  const refusal = startRefusal({ branch: treeState(root).branch, merging: false, changed: [], ignored: true });
  if (refusal) throw new Refusal(refusal);
}

/** On main, no merge open, a clean tree, and a scratch folder git ignores (or the logs would dirty it). */
export function preconditions(root: string): void {
  const merging = mergeInProgress(root);
  // With no merge open no git of this checkout is running a driver, so a driver's temp file at the root is a stray that would dirty it.
  if (!merging) sweepAndSay(root);
  const facts = { ...treeState(root), merging, ignored: true };
  const refusal = startRefusal(facts);
  if (refusal) throw new Refusal(refusal);
  // Asked last, and only of a checkout that passed the rest: it is one more git start.
  if (!isIgnored(root, `${SCRATCH}/merge-probe.log`)) {
    throw new Refusal(startRefusal({ ...facts, ignored: false }) as string);
  }
}

/** The branch's tip, or a refusal naming it. */
export function requireBranch(root: string, branch: string): string {
  const tip = git(root, ['rev-parse', '--verify', '--quiet', `${branch}^{commit}`], { allowFail: true });
  if (tip.status !== 0) throw new Refusal(`no branch '${branch}'`);
  return tip.out.trim();
}

/**
 * `git worktree list --porcelain`, read: each worktree's path, the branch it has out ('' when detached),
 * whether it is locked and git's reason, and whether git says its folder is gone. The first is the main
 * checkout.
 */
export function parseWorktrees(porcelain: string): Worktree[] {
  const trees: Worktree[] = [];
  for (const line of porcelain.replace(/\r\n/g, '\n').split('\n')) {
    const tree = trees.at(-1);
    if (line.startsWith('worktree ')) {
      trees.push({ path: line.slice('worktree '.length).trim(), branch: '', locked: false, lockReason: '', prunable: false, bare: false });
    } else if (!tree) continue;
    else if (line.startsWith('branch refs/heads/')) tree.branch = line.slice('branch refs/heads/'.length).trim();
    else if (/^locked(?: |$)/.test(line)) Object.assign(tree, { locked: true, lockReason: line.slice('locked'.length).trim() });
    else if (/^prunable(?: |$)/.test(line)) tree.prunable = true;
    else if (line.trim() === 'bare') tree.bare = true;
  }
  return trees;
}

/** The path of the worktree `git worktree list --porcelain` shows a branch checked out in, or null. */
export function worktreeFor(porcelain: string, branch: string): string | null {
  return (branch && parseWorktrees(porcelain).find((tree) => tree.branch === branch)?.path) || null;
}

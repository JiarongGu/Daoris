/**
 * What the prune decides of a worktree and of a folder under `.claude/worktrees/` that git does not list: whether it goes, and if it
 * stays, why. The prune itself (reading the facts, removing, the lines it says) is `tools/merge-branch.mjs`.
 */
import { lstatSync, readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import type { Worktree } from './git.ts';
import { ENDED_IDLE_MINUTES, HARNESS_IDLE_MINUTES, idleNeeded } from './ended-agent.ts';

export { ENDED_IDLE_MINUTES, HARNESS_IDLE_MINUTES };

/** One running process, as the prune reads it. */
export type Proc = { pid: number; exe?: string; command: string };

/** What a prune finds in a worktree it may remove. */
export type Facts = {
  gone?: boolean; error?: string; tracked: string[]; untracked: string[]; local: string | null;
  atTip?: boolean; processes?: Proc[]; processError?: string; idleMinutes?: number; ended?: boolean;
};

export type Candidate = { branch: string; worktree: Worktree | null; held: string | null };

export type Verdict = { remove: boolean; why: string; gone?: boolean; unlock?: boolean };

/** Up to `max` names of `files`, then how many more. */
export const listed = (files: readonly string[], max = 3): string => `${files.slice(0, max).join(', ')}${files.length > max ? `, … ${files.length - max} more` : ''}`;

/**
 * Whether a lock is the agent harness's own, `claude agent <id> (pid N)`. The harness holds it for its
 * session's whole life, not only while the agent runs, so on a finished agent's worktree it says nothing of use.
 */
export const harnessLock = (reason: string | undefined): boolean => /^claude agent \S+ \(pid \d+\)$/.test(reason ?? '');

// Folders whose children are not read: build output and dependencies, thousands of entries a worker's edits rarely reach first.
const WRITE_SKIP = new Set(['.git', 'node_modules', 'bin', 'obj', 'dist', 'wwwroot']);

/**
 * The newest write time, in ms, of a worktree's folder, 0 when nothing could be read. Read cheaply, never a
 * full walk (a tree is 300 MB): the times of the folder's top-level entries (a file or folder made or removed there moves
 * the folder's own time, and a worker's builds, logs and scratch land there), of the children of each top-level folder
 * but build output (`src/`, `local/`, `docs/`), and of the git folder the worktree keeps beside the repository: its HEAD,
 * its index and its HEAD log, which every commit, checkout and `git add` writes. A file edited in place deeper down, with
 * nothing made or committed, is not seen; the quiet the caller waits covers that.
 */
export function newestWrite(path: string): number {
  let newest = 0;
  const seen = (file: string): void => {
    try {
      newest = Math.max(newest, lstatSync(file).mtimeMs);
    } catch { /* gone between the listing and the stat: nothing to read */ }
  };
  const children = (folder: string) => {
    try {
      return readdirSync(folder, { withFileTypes: true });
    } catch {
      return [];
    }
  };
  seen(path);
  for (const entry of children(path)) {
    const at = join(path, entry.name);
    seen(at);
    if (entry.isDirectory() && !WRITE_SKIP.has(entry.name)) for (const child of children(at)) seen(join(at, child.name));
  }
  try {
    const pointer = /^gitdir: (.+)$/m.exec(readFileSync(join(path, '.git'), 'utf8'));
    if (pointer) for (const name of ['HEAD', 'index', join('logs', 'HEAD')]) seen(join(resolve(path, pointer[1]!.trim()), name));
  } catch { /* no .git file: the folder's own entries are all there is */ }
  return newest;
}

/** One process, as a prune names it: its pid and its command line, cut short. */
export const processSaid = (one: Proc): string => `pid ${one.pid}: ${one.command.length > 120 ? `${one.command.slice(0, 120)}…` : one.command}`;

/**
 * Whether a path under `local/` (as `local/…`, `/`-separated) is an agent's disposable output: logs and scratch under
 * `local/scratch/`, and a `*.log` file anywhere under `local/`. Anything else there may be work, and keeps its worktree.
 */
export function disposableLocal(path: string, directory = false): boolean {
  if (path === 'local/scratch' || path.startsWith('local/scratch/')) return true;
  return !directory && path.endsWith('.log');
}

/** A folder under `.claude/worktrees/` that git does not list, as the prune looked into it. */
export type Folder = { link?: boolean; git?: boolean; processes?: Proc[]; processError?: string };

/**
 * Whether a folder under `.claude/worktrees/` that git does not list goes: what a `git worktree remove`
 * left when it could not finish. It goes when it is no link (a reparse point is never followed), holds no `.git` (then
 * it is no half-removed worktree), and no running process is inside it, which is never stopped here.
 */
export function folderVerdict(folder: Folder): { remove: boolean; why: string } {
  if (folder.link) return { remove: false, why: 'a link (a reparse point), never followed or removed here' };
  if (folder.git) return { remove: false, why: 'a folder git does not list, but it holds a .git, so it is no half-removed worktree and is never touched here' };
  if (!folder.processes) {
    return { remove: false, why: `a folder git does not list, kept: whether a process runs inside it could not be read (${folder.processError ?? 'not looked into'})` };
  }
  if (folder.processes.length) {
    return {
      remove: false,
      why: `a folder git does not list, kept: a running process is inside it (${folder.processes.map(processSaid).join('; ')}), and a process is never stopped here: end it, and the next prune removes the folder`,
    };
  }
  return { remove: true, why: '' };
}

/**
 * Whether a candidate goes, and if it stays, why. `facts` is what is found in its worktree, and null
 * where nothing is looked into: a worktree locked for any reason but the agent harness's is in use, so
 * the lock decides and what it holds does not, and nothing runs in it. The harness's own lock
 * is lifted, `unlock`, when its branch is not at main's tip unless the agent committed in it (a just-dispatched agent's is), it has no
 * change, nothing under `local/` but logs and scratch, no running process inside it and no write for the quiet its agent's state asks
 * (`idleNeeded`). Every reason to keep is said, not only the first.
 */
export function pruneVerdict(candidate: Candidate, facts: Facts | null): Verdict {
  if (candidate.held) return { remove: false, why: candidate.held };
  const tree = candidate.worktree;
  if (!tree) return { remove: true, why: '' };
  const harness = tree.locked && harnessLock(tree.lockReason);
  if (tree.locked && !harness) {
    return { remove: false, why: `its worktree is locked, so in use${tree.lockReason ? ` (${tree.lockReason})` : ''}; it is never looked into or unlocked here` };
  }
  if (!facts) return { remove: false, why: 'its worktree was not looked into' };
  const unlock = harness ? { unlock: true } : {};
  if (facts.gone) return { remove: true, why: '', gone: true, ...unlock };
  const has: string[] = [];
  if (facts.error) has.push(`a state git could not read (${facts.error})`);
  if (facts.tracked.length) has.push(`modified or staged tracked files (${listed(facts.tracked)})`);
  if (facts.local) has.push(`private output under local/ (${facts.local}), which nothing else keeps`);
  if (facts.untracked.length) has.push(`untracked files git removes only with --force (${listed(facts.untracked)})`);
  if (!harness) return has.length ? { remove: false, why: `its worktree has ${has.join('; ')}` } : { remove: true, why: '' };

  const kept: string[] = [];
  if (facts.atTip && !facts.ended) kept.push("its branch is at main's tip, as a just-dispatched agent's is");
  kept.push(...has.map((one) => `it has ${one}`));
  if (!facts.processes) kept.push(`whether a process runs inside it could not be read (${facts.processError ?? 'not looked into'})`);
  else if (facts.processes.length) kept.push(`a running process is inside it (${facts.processes.map(processSaid).join('; ')})`);
  // A shell's working folder is on no command line, so a quiet tree is the only sign a worker has left it.
  if (facts.idleMinutes === undefined) kept.push('when it was last written could not be read');
  else if (facts.idleMinutes < idleNeeded(facts)) kept.push(`written ${facts.idleMinutes} minute${facts.idleMinutes === 1 ? '' : 's'} ago, a worker may be in it`);
  if (kept.length) return { remove: false, why: `its worktree is locked by the agent harness (${tree.lockReason}), and stays locked: ${kept.join('; ')}` };
  return { remove: true, why: '', ...unlock };
}

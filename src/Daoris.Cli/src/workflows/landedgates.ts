// The declaration a landed route runs unasked, accepted by its blob (FABRIC-PIPELINE1a, D192 point 2, the pipeline design §4.2): the
// person accepts the `daoris.gates.json` at the line's tip, kept as the blob id git gives it, so any edit of the file asks again and a
// landed edit of it never runs by itself. Read as git objects with no checkout, so what the person has in flight there is neither read
// nor touched.
//
// A TWIN with the driver's `LandedGates.cs` (`ReadAsync`, `TextAsync`): the same reads, `ls-tree` of the tip's top for the file, a
// blob alone a declaration and anything else `none`. git starts through the toolchain's one door (`gitAnswer`), as every child does.

import { gitAnswer } from '../agents/toolchain.ts';
import { NO_GATES, isGates } from './workflowchoice.ts';

/** The declaration's name at a repository's top — the driver's `LandedGates.File`. */
export const GATES_FILE = 'daoris.gates.json';

/** The line's tip as a landed route reads it: its line, the commit at its tip, and its declaration's blob, or `none`. */
export interface LandedTip {
  line: string;
  commit: string;
  gates: string;
}

/** How git is asked: the toolchain's door, or a test's. */
export type GitAnswer = (root: string, args: readonly string[]) => { status: number | null; stdout: string };

const isBlob = (text: string): boolean => text !== NO_GATES && isGates(text);

/**
 * The tip of `line` in the checkout at `root` and its declaration's blob, or why it could not be read — the driver's
 * `LandedGates.ReadAsync`.
 */
export function readLandedTip(root: string, line: string, git: GitAnswer = gitAnswer): { tip: LandedTip } | { problem: string } {
  // A folder that is not a repository's top would answer for the one above it.
  const top = git(root, ['rev-parse', '--show-prefix']);
  if (top.status !== 0 || top.stdout.trim() !== '') return { problem: 'its checkout here is not a git repository' };
  const found = git(root, ['rev-parse', '--verify', '--quiet', `refs/heads/${line}^{commit}`]);
  const commit = found.stdout.trim();
  if (found.status !== 0 || !/^[0-9a-f]{40}(?:[0-9a-f]{24})?$/.test(commit)) return { problem: `its line \`${line}\` names no commit here` };
  const listed = git(root, ['ls-tree', '--full-tree', '-z', commit, '--', GATES_FILE]);
  if (listed.status !== 0) return { problem: `git could not list \`${line}\` at \`${commit.slice(0, 7)}\`` };
  const parts = listed.stdout.split('\t')[0]!.split(' ');
  const gates = parts.length === 3 && parts[1] === 'blob' && isBlob(parts[2]!) ? parts[2]! : NO_GATES;
  return { tip: { line, commit, gates } };
}

/** The declaration's text at `blob`, for the person to read before accepting it; null for `none` — the driver's `LandedGates.TextAsync`. */
export function declarationText(root: string, blob: string, git: GitAnswer = gitAnswer): string | null {
  if (!isBlob(blob)) return null;
  const read = git(root, ['cat-file', 'blob', blob]);
  return read.status === 0 ? read.stdout : null;
}

/**
 * The line a repository's work lands on, as the driver chooses it (`CanonicalLine.Choose`): the one set for it, else its workspace's,
 * else the checkout's own guess (`origin/HEAD`, else `main`, else `master`).
 */
export function lineOf(
  lines: Record<string, string>, workspaceLines: Record<string, string>, repository: string, workspace: string, root: string,
  git: GitAnswer = gitAnswer,
): string {
  const set = Object.entries(lines).find(([name]) => name.toLowerCase() === repository.toLowerCase())?.[1]
    ?? Object.entries(workspaceLines).find(([name]) => name.toLowerCase() === workspace.toLowerCase())?.[1];
  if (set !== undefined) return set;
  const head = git(root, ['symbolic-ref', '--quiet', '--short', 'refs/remotes/origin/HEAD']);
  if (head.status === 0 && head.stdout.trim().startsWith('origin/')) return head.stdout.trim().slice('origin/'.length);
  return git(root, ['rev-parse', '--verify', '--quiet', 'refs/heads/main']).status === 0 ? 'main' : 'master';
}

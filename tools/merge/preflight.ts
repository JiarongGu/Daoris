/**
 * The preflight: a builder runs its merge's own gates from its own worktree before it hands back, so the merge
 * stops being where a branch's reds are first found. `node tools/merge-branch.mjs --preflight [--base <ref>]` judges the branch
 * the worktree has out against `main` (or `--base`) as the queue's scoped run judges its candidate: the paths from their merge
 * base to the branch's tip choose the gates (`scopedChoice`, the queue's own), and every gate runs to its end in the worktree.
 * Nothing on main, in the main checkout or in the queue moves; its verdicts go to the one store, keyed by the tree they ran on, so
 * a merge whose candidate holds the same tree keeps them. A red writes the brief (`red-brief.ts`), each failure checked on main's
 * tip in a tree of the worktree's own under `local/scratch/main-tree`.
 *
 * Here: what the preflight reads of the worktree before it runs, and what it refuses.
 */
import { Refusal } from './args.ts';

type GitRun = (cwd: string, args: string[], options?: { allowFail?: boolean }) => { status: number | null; out: string };

/** The tree a merge's or preflight's failures are checked on: main's tip, detached, under the run's own scratch. */
export const MAIN_TREE = 'local/scratch/main-tree';

export type PreflightFacts = { branch: string; base: string; baseTip: string; from: string; tip: string; behind: number; lines: string[] };

/**
 * The branch this worktree has out, the base's tip and their merge base, read through `run`: refused on the base itself, on a
 * detached head, on an unknown base, on a branch that adds no commit, and on a tree with changes, which no merge would gate.
 * `lines` says what is judged, and how far the base has moved past the merge base.
 */
export function preflightFacts(root: string, base: string, run: GitRun): PreflightFacts {
  const read = (args: string[]): string | null => {
    const ran = run(root, args, { allowFail: true });
    return ran.status === 0 ? ran.out.trim() : null;
  };
  const branch = read(['symbolic-ref', '--quiet', '--short', 'HEAD']);
  if (!branch) throw new Refusal('--preflight gates a branch: this worktree has no branch out (a detached head)');
  const baseTip = read(['rev-parse', '--verify', '--quiet', `${base}^{commit}`]);
  if (!baseTip) throw new Refusal(`--preflight: no commit '${base}' to judge the branch against`);
  if (branch === base) throw new Refusal(`--preflight runs in a branch's worktree: this one has ${base} out, which is what a branch is merged into`);
  const changed = (read(['--no-optional-locks', 'status', '--porcelain']) ?? '').split('\n').filter(Boolean);
  if (changed.length) {
    throw new Refusal(`--preflight gates the branch's tip, as its merge does: commit or drop these first\n${changed.slice(0, 12).map((line) => `  ${line}`).join('\n')}${changed.length > 12 ? `\n  … ${changed.length - 12} more` : ''}`);
  }
  const tip = read(['rev-parse', 'HEAD']) as string;
  const from = read(['merge-base', baseTip, tip]);
  if (!from) throw new Refusal(`--preflight: ${branch} and ${base} share no history`);
  if (from === tip) throw new Refusal(`--preflight: ${branch} adds no commit to ${base}`);
  const behind = Number(read(['rev-list', '--count', `${tip}..${baseTip}`]) ?? 0);
  const lines = [`preflight of ${branch} at ${tip.slice(0, 7)} against ${base} at ${baseTip.slice(0, 7)}: the paths from their merge base ${from.slice(0, 7)} choose the gates, as its merge's do`];
  if (behind > 0) lines.push(`  ${base} is ${behind} commit${behind === 1 ? '' : 's'} past this branch: the gates run on the branch as it stands; merge ${base} into it to gate the tree its merge will make`);
  return { branch, base, baseTip, from, tip, behind, lines };
}

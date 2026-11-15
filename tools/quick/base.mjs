/**
 * The base of a quick run: main at the merge base of the tree's HEAD with main (as `tools/comment-gate.mjs` and the size ratchet
 * judge), never the tree as an earlier step left it. Its verdicts are the steps run in a detached worktree of that commit under
 * `local/` (node finds the tree's own `node_modules` above it), kept in `local/quick-base.json` with the commit they were run on and
 * reused while the merge base stands; a record of another commit is not read.
 */
import { spawnSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { execGit } from '../fsx.mjs';
import { BASE_FILE, readVerdicts, writeVerdicts } from './verdicts.mjs';

export const BASE_TREE = 'local/quick-base-tree';

const git = (root, args) => execGit(args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();

/** The commit the base is: `git merge-base main HEAD`, or null where there is none (no main, no history). */
export function baseCommit(root) {
  try {
    return git(root, ['merge-base', 'main', 'HEAD']) || null;
  } catch {
    return null;
  }
}

function removeTree(root, tree) {
  try {
    git(root, ['worktree', 'remove', '--force', tree]);
  } catch {
    rmSync(tree, { recursive: true, force: true });
    try {
      git(root, ['worktree', 'prune']);
    } catch {
      /* nothing to prune */
    }
  }
}

/** A step run with its folder at `tree`: its status. */
const shellAt = (tree) => (command) => {
  const ran = spawnSync(command, { cwd: tree, shell: true, encoding: 'utf8', windowsHide: true, maxBuffer: 256 * 1024 * 1024 });
  return ran.error ? 2 : ran.status ?? 1;
};

/** Each of `steps` run in a detached worktree of `commit`: `{ step: 'pass' | 'fail' }`; the worktree is gone after. */
export function runAtCommit(root, commit, steps, runAt = shellAt) {
  const tree = resolve(root, BASE_TREE);
  removeTree(root, tree);
  git(root, ['worktree', 'add', '--detach', tree, commit]);
  try {
    const run = runAt(tree);
    return Object.fromEntries(steps.map((step) => [step, run(step) === 0 ? 'pass' : 'fail']));
  } finally {
    removeTree(root, tree);
  }
}

/**
 * The base's verdict for each of `steps`: what `local/quick-base.json` holds for the merge-base commit, the rest run there now and
 * recorded. Null where there is no base commit, so a red is said to have no base.
 */
export function baseVerdicts(root, steps, { commit = baseCommit(root), runAt } = {}) {
  if (!commit) return null;
  const held = readVerdicts(join(root, BASE_FILE));
  const known = held?.head === commit ? held.steps : {};
  const missing = steps.filter((step) => !(step in known));
  if (missing.length === 0) return { head: commit, steps: known };
  const record = { head: commit, at: new Date().toISOString(), steps: { ...known, ...runAtCommit(root, commit, missing, runAt) } };
  writeVerdicts(join(root, BASE_FILE), record);
  return record;
}

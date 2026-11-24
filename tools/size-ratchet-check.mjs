/**
 * The size ratchet's check of a tree, and whose each breach is.
 *
 * A landing's check judges every file of the merged tree against the merged tree's own list, not the files its diff touches, so a
 * landing that leaves a file over its entry is refused. A breach main's own tree stands in just the same, a file the tree holds at
 * main's size against main's own list, is main's red: it is said so and does not fail a tree that only carries it, since sending it back
 * as the branch's own would fail every branch begun before the landing that broke it. It stays owed to main, which a run on main itself
 * (HEAD at main's tip, no merge open) never excuses.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { execGit } from './fsx.mjs';
import { baseList, BUDGET, breaches, sized, sizeOf } from './size-ratchet.mjs';

const MAIN = 'main';

const git = (root, args) => execGit(args, { cwd: root, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
const gitOrNull = (root, args) => {
  try {
    return git(root, args);
  } catch {
    return null;
  }
};

/** Whether `root`'s tree is more than main's own: HEAD is not main's tip, or a merge is open on it. */
export function beyondMain(root) {
  const tip = gitOrNull(root, ['rev-parse', '-q', '--verify', MAIN])?.trim();
  if (!tip) return false;
  return gitOrNull(root, ['rev-parse', '-q', '--verify', 'HEAD'])?.trim() !== tip || gitOrNull(root, ['rev-parse', '-q', '--verify', 'MERGE_HEAD']) !== null;
}

/** Main's own sizes (over the budget only) and list, for `paths`; null where main has no list to hold them to. */
export function mainStands(root, paths) {
  const listText = gitOrNull(root, ['show', `${MAIN}:tools/size-ratchet.json`]);
  if (listText === null) return null;
  const list = JSON.parse(listText);
  const found = {};
  for (const path of paths) {
    const text = sized(path) ? gitOrNull(root, ['show', `${MAIN}:${path}`]) : null;
    if (text !== null && sizeOf(text) > BUDGET) found[path] = sizeOf(text);
  }
  return { found, list };
}

/** The `problems` (from `breaches`) split into those main stands in as they are and those it does not. */
export function attribute(problems, found, main) {
  const own = { main: [], tree: [] };
  for (const problem of problems) {
    const { path } = problem;
    const there = main && breaches(
      main.found[path] === undefined ? {} : { [path]: main.found[path] },
      main.list[path] === undefined ? {} : { [path]: main.list[path] },
    ).some((each) => each.path === path);
    (there && main.found[path] === found[path] ? own.main : own.tree).push(problem);
  }
  return own;
}

/** Checks the tree at `root` against its list; the exit code (0 clean or only main's, 1 a breach of this tree's own). */
export function checkTree(root, found, { say = console.log, err = console.error } = {}) {
  const problems = breaches(found, JSON.parse(readFileSync(join(root, 'tools', 'size-ratchet.json'), 'utf8')), baseList(root));
  const split = problems.length && beyondMain(root) ? attribute(problems, found, mainStands(root, problems.map((each) => each.path))) : { main: [], tree: problems };
  for (const each of split.tree) err(`size-ratchet: ${each.text}`);
  for (const each of split.main) say(`size-ratchet: main's red, not this tree's: ${each.text}; main stands in it at this size, so it does not fail this tree and stays owed to main`);
  if (split.tree.length) return 1;
  say(`size-ratchet: ${split.main.length ? `main's red only (${split.main.length})` : 'clean'} (${Object.keys(found).length} files over the budget, none grown)`);
  return 0;
}

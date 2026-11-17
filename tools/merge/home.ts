/**
 * Where a run stands, asked in as few git starts as git allows, since every run of the merge tool begins here and a start costs
 * seconds on a loaded machine: the checkout whose verdict store and queue the run reads (the repository's first worktree, from
 * one worktree listing the run passes along), the checkout holding the tool, and the paths git must ignore there, in one
 * `check-ignore`.
 */
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { commonDirs, git, lines, parseWorktrees, requireIgnored } from './git.ts';

export const samePath = (a: string, b: string): boolean => {
  const norm = (path: string): string => resolve(path).split('\\').join('/').replace(/\/+$/, '');
  return process.platform === 'win32' ? norm(a).toLowerCase() === norm(b).toLowerCase() : norm(a) === norm(b);
};

/** The verdicts record: which gate passed which tree, kept in the main checkout. */
export const VERDICTS_FILE = 'local/gate-verdicts.json';

const homes = new Map<string, string>();
const listings = new Map<string, string>();

/**
 * The checkout whose verdict store a run in `root` reads and writes: the first worktree git lists (the main checkout),
 * whichever worktree the run is in, or `root` itself where git cannot say or the repository is bare. Verdicts are keyed by
 * tree, so one store serves every worktree, each judging its own tree. A tree a listing this run read already names is of
 * that listing's repository, whose first worktree does not move, so it asks git nothing.
 */
export function verdictsHome(root: string): string {
  const known = homes.get(root);
  if (known) return known;
  for (const listing of listings.values()) {
    const trees = parseWorktrees(listing);
    if (trees[0] && !trees[0].bare && trees.some((tree) => samePath(tree.path, root))) return remember(root, resolve(trees[0].path));
  }
  const listed = git(root, ['worktree', 'list', '--porcelain'], { allowFail: true });
  if (listed.status === 0) listings.set(root, listed.out);
  const first = listed.status === 0 ? parseWorktrees(listed.out)[0] : null;
  return remember(root, first && first.path && !first.bare ? resolve(first.path) : root);
}

const remember = (root: string, home: string): string => {
  homes.set(root, home);
  return home;
};

/** The worktree listing `verdictsHome` read for `root` at the run's start, for a reader in the same short verb; null where it read none. */
export const startListing = (root: string): string | null => listings.get(root) ?? null;

/** The folder holding the merge tool. */
const TOOLS = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/**
 * The checkout holding this tool, and whether it is one of `root`'s repository (whose common git folder `locate` read); none
 * where git finds none. A tool in `root`'s own `tools/` is `root`'s, so git is asked only for a tool another checkout holds.
 */
export function toolCheckout(root: string, tools: string = TOOLS): { tool: string | null; sameRepository: boolean } {
  const ours = commonDirs.get(root) ?? null;
  if (ours && samePath(dirname(tools), root) && !existsSync(join(tools, '.git'))) return { tool: resolve(root), sameRepository: true };
  const asked = git(tools, ['rev-parse', '--show-toplevel', '--path-format=absolute', '--git-common-dir'], { allowFail: true });
  const [top, common] = asked.status === 0 ? lines(asked.out) : [];
  if (!top || !common) return { tool: null, sameRepository: false };
  return { tool: resolve(top), sameRepository: ours !== null && samePath(common, ours) };
}

/**
 * The verdicts record's need, in its main checkout (`verdictsHome`): written as each gate ends, tracked or untracked it would
 * change the tree it judges.
 */
export function verdictsNeed(root: string): [string, string] {
  const where = samePath(verdictsHome(root), root) ? 'here' : 'in the main checkout, where every worktree records';
  return [VERDICTS_FILE, `${VERDICTS_FILE} is not ignored ${where}, so each verdict written would change the tree it is a verdict on`];
}

export const requireVerdictsIgnored = (root: string): void => requireIgnored(verdictsHome(root), [verdictsNeed(root)]);

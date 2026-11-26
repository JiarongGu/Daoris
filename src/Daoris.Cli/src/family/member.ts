// Which member a folder stands for, and where its family file is.
//
// A member's checkout is the folder of its registry name beside the family file. A session's tree is a linked worktree of that
// checkout, standing elsewhere, so a tree is read back to its checkout through git's own files (its `.git` file's `gitdir`, then that
// folder's `commondir`): the member is the checkout's name, and the family file is beside the checkout, never beside the tree.

import { existsSync, readFileSync, statSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { FAMILY_FILE } from './family.ts';

/** The checkout `root` is: itself, or, for a linked worktree, the checkout whose `.git` folder holds it. */
export function checkoutOf(root: string): string {
  const dotGit = join(root, '.git');
  if (!existsSync(dotGit) || !statSync(dotGit).isFile()) return root;
  const pointer = /^gitdir:\s*(.+)$/m.exec(readFileSync(dotGit, 'utf8'));
  if (!pointer) return root;
  const gitDir = resolve(root, pointer[1]!.trim());
  const commonFile = join(gitDir, 'commondir');
  if (!existsSync(commonFile)) return root;
  const commonDir = resolve(gitDir, readFileSync(commonFile, 'utf8').trim());
  return basename(commonDir) === '.git' ? dirname(commonDir) : root;
}

/** The member `root` stands for and the family file it reads: `named` resolved from `root`, else the one beside its checkout. */
export function familyOf(root: string, named: string | undefined): { member: string; file: string } {
  const checkout = checkoutOf(root);
  return { member: basename(checkout), file: named ? resolve(root, named) : resolve(checkout, '..', FAMILY_FILE) };
}

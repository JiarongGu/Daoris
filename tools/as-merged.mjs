#!/usr/bin/env node
/**
 * Runs a command so that git, inside it, reads HEAD as the commit an open merge would make (GATE1).
 *
 * ## Why
 *
 * `tools/merge-branch.mjs` merges `--no-ff --no-commit` and gates the tree before the parent commits it, so a check
 * that reads history from HEAD judged main as it was before the merge. The devkit's docs gate is that check: it dates
 * each tracked document and the code it describes by `git log -1 -- <path>`. On 2026-10-02 the merge of TOOL4e brought
 * CLI source changed that day past a README last changed the day before. Before the merge both sides were a day old,
 * so every gate passed; once committed, `verify` failed on main until the next merge edited the README
 * (`docs/FIX-LOG.md`, GATE1). The other docs checks (the budgets, shapes and duplicates, the orientation index, and the
 * devkit's other gates) read files or the index, and already saw the merge.
 *
 * ## What it does
 *
 * Outside a merge it runs the command as it is: the same environment, its own exit, nothing said.
 *
 * With a merge open it builds that commit without making it. The tree is the checkout's content as `git add -A` would
 * stage it, the same tree the merge tool records a verdict on. Its parents are HEAD and each MERGE_HEAD, and no ref
 * names it. A folder in the git folder then holds what a linked worktree's git folder holds: HEAD at that commit, a
 * copy of the index, and `commondir` naming the repository's own folder, so objects, refs and config stay the
 * repository's. The command runs with GIT_DIR at that folder and GIT_WORK_TREE at the checkout, so any git it starts
 * sees the merge as made, with nothing left open. The person's index, HEAD and open merge are never written, and the
 * folder goes when the command ends. A folder left by a run that was killed goes at the next run, once its process has
 * ended.
 *
 * A merge with unmerged paths has no commit yet, so it refuses rather than judge HEAD or conflict markers.
 *
 *   node tools/as-merged.mjs <command> [<argument>…]
 *
 * The command is started directly, not through a shell, from the current folder. Exit: the command's own · 2 a usage
 * error, a merge with unmerged paths, a commit git could not build, or a command that could not start.
 */
import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { isMain } from './fsx.mjs';

export const USAGE = 'usage: node tools/as-merged.mjs <command> [<argument>…]';

/** The folders this tool makes in a git folder, each named for the process that made it. */
export const FOLDER = 'daoris-as-merged-';

/** Why there is no commit to run the command against. Exit 2. */
export class AsMergedError extends Error {}

// Who the commit would be by is never read, and a checkout with no identity configured must still build it.
const IDENTITY = {
  GIT_AUTHOR_NAME: 'Proposed merge',
  GIT_AUTHOR_EMAIL: 'proposed-merge@daoris.invalid',
  GIT_COMMITTER_NAME: 'Proposed merge',
  GIT_COMMITTER_EMAIL: 'proposed-merge@daoris.invalid',
};

function git(cwd, args, env) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true, ...(env ? { env: { ...process.env, ...env } } : {}) });
  if (result.error) throw new AsMergedError(`git could not start: ${result.error.message}`);
  if (result.status !== 0) throw new AsMergedError(`git ${args.join(' ')} failed: ${(result.stderr || result.stdout).trim()}`);
  return result.stdout.trim();
}

const lines = (text) => text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
const short = (sha) => sha.slice(0, 8);

/** The commits an open merge brings, as git wrote them in the checkout's git folder; none outside a merge. */
export function mergeHeads(gitDir) {
  const file = join(gitDir, 'MERGE_HEAD');
  return existsSync(file) ? lines(readFileSync(file, 'utf8')) : [];
}

const alive = (pid) => {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === 'EPERM';
  }
};

/** Removes the folders of runs whose process has ended: one killed mid-run could not remove its own. */
function sweep(gitDir) {
  for (const name of readdirSync(gitDir)) {
    if (!name.startsWith(FOLDER)) continue;
    const pid = Number(name.slice(FOLDER.length));
    if (Number.isInteger(pid) && pid > 0 && pid !== process.pid && !alive(pid)) rmSync(join(gitDir, name), { recursive: true, force: true });
  }
}

/**
 * The commit the merge open in `cwd`'s checkout would make, and the environment that makes it git's HEAD. Null outside
 * a merge, and outside a checkout, where there is no merge to judge. Throws `AsMergedError` when the merge has unmerged
 * paths or git cannot build the commit. The caller removes `folder` when the command ends.
 */
export function prepare(cwd) {
  const located = spawnSync('git', ['rev-parse', '--show-toplevel', '--absolute-git-dir'], { cwd, encoding: 'utf8', windowsHide: true });
  if (located.error || located.status !== 0) return null;
  const [top, gitDir] = lines(located.stdout);
  const heads = mergeHeads(gitDir);
  if (heads.length === 0) return null;

  const unmerged = lines(git(top, ['diff', '--name-only', '--diff-filter=U']));
  if (unmerged.length) {
    const named = `${unmerged.slice(0, 5).join(', ')}${unmerged.length > 5 ? ` and ${unmerged.length - 5} more` : ''}`;
    throw new AsMergedError(`the merge has unmerged paths, so there is no commit to judge yet: ${named}. Resolve and add them first.`);
  }

  sweep(gitDir);
  const commonDir = git(top, ['rev-parse', '--path-format=absolute', '--git-common-dir']);
  const index = resolve(top, git(top, ['rev-parse', '--git-path', 'index']));
  const folder = join(gitDir, `${FOLDER}${process.pid}`);
  rmSync(folder, { recursive: true, force: true });
  mkdirSync(folder, { recursive: true });
  try {
    // The tree as `git add -A` would stage it, on an index of its own: an unstaged fix in the merge is in it.
    const staging = join(folder, 'staging.index');
    copyFileSync(index, staging);
    git(top, ['add', '-A'], { GIT_INDEX_FILE: staging });
    const tree = git(top, ['write-tree'], { GIT_INDEX_FILE: staging });
    rmSync(staging, { force: true });
    const parents = [git(top, ['rev-parse', 'HEAD']), ...heads];
    const commit = git(top, ['commit-tree', tree, ...parents.flatMap((parent) => ['-p', parent]), '-m', 'The commit this merge would make (GATE1)'], IDENTITY);

    // A linked worktree's git folder, in miniature: its own HEAD and index, everything else the repository's.
    copyFileSync(index, join(folder, 'index'));
    writeFileSync(join(folder, 'commondir'), `${commonDir}\n`);
    writeFileSync(join(folder, 'HEAD'), `${commit}\n`);
    return { commit, tree, parents, folder, env: { GIT_DIR: folder, GIT_WORK_TREE: top } };
  } catch (error) {
    rmSync(folder, { recursive: true, force: true });
    throw error;
  }
}

/** Runs the command as the open merge would commit, or as it is outside one: its exit, or 2 when it could not. */
export function main(argv = process.argv.slice(2), cwd = process.cwd()) {
  const [command, ...args] = argv;
  if (!command) {
    console.error(USAGE);
    return 2;
  }
  let prepared;
  try {
    prepared = prepare(cwd);
  } catch (error) {
    if (!(error instanceof AsMergedError)) throw error;
    console.error(`as-merged: ${error.message}`);
    return 2;
  }
  if (prepared) {
    const [head, ...merging] = prepared.parents.map(short);
    console.error(`as-merged: a merge is open, so git here reads HEAD as the commit it would make (${short(prepared.commit)}): `
      + `the tree as \`git add -A\` would stage it, on ${head} and ${merging.join(', ')}`);
  }
  try {
    const child = spawnSync(command, args, {
      cwd, stdio: 'inherit', windowsHide: true, env: prepared ? { ...process.env, ...prepared.env } : process.env,
    });
    if (child.error) {
      console.error(`as-merged: could not start ${command}: ${child.error.message}`);
      return 2;
    }
    return child.status ?? 1;
  } finally {
    if (prepared) rmSync(prepared.folder, { recursive: true, force: true });
  }
}

if (isMain(import.meta.url)) process.exitCode = main();

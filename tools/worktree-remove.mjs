/**
 * A worktree removed without reaching through its links. Git (and a recursive delete) follows a Windows junction
 * a builder made to the main checkout's `node_modules`, and empties the target. So each link under the worktree is
 * removed as a link first, never followed; one that cannot be removed stops the removal before git touches
 * anything; and after git, the main checkout is checked for tracked files that were not deleted before.
 *
 *   node tools/worktree-remove.mjs <worktree> [--force] [--repo <main checkout>]
 *
 * Exit 0 removed, 1 kept (the reason is printed), 2 tool error. Every removal of a worktree in this repository
 * goes through `removeWorktree`.
 */
import { existsSync, lstatSync, readdirSync, readlinkSync, rmdirSync, unlinkSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { isMain, spawnGit } from './fsx.mjs';

/** The links under `root`, found without following any and without entering `.git`. */
export function findLinks(root) {
  const found = [];
  const walk = (folder) => {
    let entries;
    try {
      entries = readdirSync(folder, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      if (entry.name === '.git') continue;
      const path = join(folder, entry.name);
      let stat;
      try {
        stat = lstatSync(path);
      } catch {
        continue;
      }
      if (stat.isSymbolicLink()) found.push(path);
      else if (stat.isDirectory()) walk(path);
    }
  };
  walk(root);
  return found;
}

/** Removes the link itself: `unlink` takes a file link or a junction's reparse point; `rmdir` (never recursive) the rest. */
function removeLinkOnly(path) {
  try {
    unlinkSync(path);
  } catch (error) {
    if (!['EISDIR', 'EPERM', 'ENOTDIR', 'EACCES'].includes(error?.code)) throw error;
    rmdirSync(path);
  }
}

const entriesOf = (path) => {
  try {
    return readdirSync(path).length;
  } catch {
    return null;
  }
};

function targetOf(path) {
  try {
    const target = readlinkSync(path);
    return isAbsolute(target) ? target : resolve(dirname(path), target);
  } catch {
    return null;
  }
}

function git(cwd, args) {
  const run = spawnGit(args, { cwd, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  return { status: run.status, out: (run.stdout ?? '').trim(), err: (run.stderr ?? run.error?.message ?? '').trim() };
}

const deletedIn = (repo) => new Set(git(repo, ['ls-files', '--deleted']).out.split('\n').filter(Boolean));

/**
 * Removes the worktree at `path` of the repository at `repo`. `force` is passed to git only where the caller asks.
 * `removeLink` and `afterRemove` are seams for tests. Returns `{ ok, why, links }`.
 */
export function removeWorktree(repo, path, { force = false, removeLink = removeLinkOnly, afterRemove = () => {} } = {}) {
  const links = [];
  if (existsSync(path) || lstatSyncSafe(path)) {
    for (const link of findLinks(path)) {
      const target = targetOf(link);
      const before = target ? entriesOf(target) : null;
      try {
        removeLink(link);
      } catch (error) {
        return { ok: false, links, why: `link ${link} could not be removed (${error?.code ?? error?.message}); nothing was removed` };
      }
      const after = target ? entriesOf(target) : null;
      if (before !== after) {
        return { ok: false, links, why: `removing link ${link} changed its target (${before} entries, then ${after}); nothing more was removed` };
      }
      links.push(link);
    }
  }
  const deletedBefore = deletedIn(repo);
  const run = git(repo, ['worktree', 'remove', ...(force ? ['--force'] : []), path]);
  afterRemove();
  const lost = [...deletedIn(repo)].filter((file) => !deletedBefore.has(file));
  if (lost.length) {
    return { ok: false, links, why: `the main checkout lost tracked files: ${lost.slice(0, 10).join(', ')}${lost.length > 10 ? ` and ${lost.length - 10} more` : ''}; restore them with git checkout` };
  }
  if (run.status !== 0) return { ok: false, links, why: run.err || run.out || 'git refused to remove the worktree' };
  return { ok: true, links, why: '' };
}

function lstatSyncSafe(path) {
  try {
    return lstatSync(path);
  } catch {
    return null;
  }
}

function main(argv) {
  const args = [...argv];
  const take = (flag) => {
    const at = args.indexOf(flag);
    if (at < 0) return null;
    const [, value] = args.splice(at, 2);
    return value ?? null;
  };
  const force = args.includes('--force');
  const given = take('--repo');
  const rest = args.filter((arg) => arg !== '--force');
  if (rest.length !== 1) {
    console.error('usage: node tools/worktree-remove.mjs <worktree> [--force] [--repo <main checkout>]');
    return 2;
  }
  const path = resolve(rest[0]);
  let repo = given ? resolve(given) : null;
  if (!repo) {
    const common = git(existsSync(path) ? path : process.cwd(), ['rev-parse', '--path-format=absolute', '--git-common-dir']);
    if (common.status !== 0) {
      console.error(`could not find the main checkout: ${common.err}`);
      return 2;
    }
    repo = dirname(common.out);
  }
  const result = removeWorktree(repo, path, { force });
  if (!result.ok) {
    console.error(`kept ${path}: ${result.why}`);
    return 1;
  }
  console.log(`removed ${path}${result.links.length ? ` (${result.links.length} link${result.links.length === 1 ? '' : 's'} removed as links)` : ''}`);
  return 0;
}

if (isMain(import.meta.url)) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (error) {
    console.error(error?.message ?? error);
    process.exitCode = 2;
  }
}

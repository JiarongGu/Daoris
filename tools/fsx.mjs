/**
 * Import-safe filesystem and Git operations shared within repository tooling.
 */
import { spawnSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import {
  copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, realpathSync, renameSync, rmSync, statSync, writeFileSync,
} from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Whether the module at `url` is the script node was asked to run — the runner guard every tool whose
 * helpers are also imported needs, in one place (REV3; it existed in three forms).
 *
 * Compared as REAL paths. Node resolves the main module's links, so through a junction or a symlink
 * `import.meta.url` is the real path and `process.argv[1]` the one typed: the guard read false, and a
 * gate ran nothing and exited 0.
 */
export function isMain(url) {
  const invoked = process.argv[1];
  if (!invoked) return false;
  try {
    return realpathSync(invoked) === realpathSync(fileURLToPath(url));
  } catch {
    return false;
  }
}

/**
 * The work tree's regular files, tracked and non-ignored untracked, once each as Git's slash-separated
 * paths sorted by code unit. Deleted files and directories are excluded. No Git state
 * is written. `root` must be the tree's top so Git cannot silently answer for a parent repository.
 * `env` defaults to the caller's environment; isolated fixtures can supply their own Git locations.
 */
export function repositoryFiles(root, { env = process.env } = {}) {
  const options = { cwd: root, encoding: 'utf8', windowsHide: true, env, maxBuffer: 256 * 1024 * 1024 };
  const top = spawnSync('git', ['rev-parse', '--show-toplevel'], options);
  const real = (path) => {
    const value = realpathSync(path).replace(/\\/g, '/').replace(/\/+$/, '');
    return process.platform === 'win32' ? value.toLowerCase() : value;
  };
  if (top.status !== 0 || real(top.stdout.trim()) !== real(root)) {
    throw new Error(`${root} is not the top of a git work tree`);
  }
  const listed = spawnSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], options);
  if (listed.status !== 0) throw new Error(`git ls-files failed: ${listed.stderr?.trim() ?? listed.error?.message}`);
  return [...new Set(listed.stdout.split('\0').filter(Boolean))]
    .filter((path) => {
      try {
        return statSync(join(root, path)).isFile();
      } catch {
        return false;
      }
    })
    .sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
}

/** The refusals that mean something still holds a file in the way, and give way once it lets go. */
const HELD = new Set(['EPERM', 'EACCES', 'EBUSY']);

/**
 * A rename tried again while a held file refuses it (UPDATE1's stage). On Windows a folder of executables written a
 * moment ago cannot be renamed while something still holds a file in it, the virus scanner most often, and the refusal
 * is EPERM; it gives way within seconds. Any other failure throws at once, and the last refusal throws after the tries.
 */
export function renameHeld(from, to, { tries = 50, waitMs = 200, rename = renameSync } = {}) {
  for (let attempt = 1; ; attempt++) {
    try {
      rename(from, to);
      return;
    } catch (error) {
      if (attempt >= tries || !HELD.has(error?.code)) throw error;
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, waitMs);
    }
  }
}

/** A name beside `file` that no other write shares: this process's, and random within it. */
const besideName = (file, suffix) => `${file}.${process.pid}-${randomBytes(4).toString('hex')}${suffix}`;

/**
 * A file replaced whole, the one way the tooling writes one (REFAC2; each tool had assembled it itself, one with a bare
 * rename). `data` is written beside the file under a name no other write shares, then renamed into place through
 * `renameHeld`, so a reader finds the old file or the new one, never half of one, and a held file is waited for. The
 * folder is made first. A string is written as UTF-8 with no BOM, bytes as they are; line endings are the caller's.
 *
 * When the write or the rename fails, what was written beside is removed and the failure thrown: the file stands as it
 * was, and nothing is left for a later run to trip over or for `git add -A` to stage (the merge tool left a rewritten
 * decision beside itself so). `tries`, `waitMs` and `rename` are `renameHeld`'s.
 */
export function writeAtomic(file, data, { tries, waitMs, rename } = {}) {
  mkdirSync(dirname(file), { recursive: true });
  const beside = besideName(file, '.partial');
  try {
    writeFileSync(beside, data);
    renameHeld(beside, file, { tries, waitMs, rename });
  } catch (error) {
    try {
      rmSync(beside, { force: true });
    } catch {
      // The failure to report is the write's; a beside file that will not go is the lesser fact.
    }
    throw error;
  }
}

/**
 * The checkout's content as a tree id, as `git add -A` would stage it (REFAC2; the merge tool's verdicts and
 * `as-merged.mjs`'s commit each built it): tracked changes and deletions, and every untracked file that is not ignored,
 * so an open merge's resolutions and an unstaged fix in it are in the tree. Nothing is committed and no ref names it.
 *
 * Staged through an index of its own: a copy of the one git uses here, named by `git rev-parse --git-path`, so a linked
 * worktree's own index and git folder are the ones used, and an inherited `GIT_INDEX_FILE` (a hook's) is read and never
 * written. With no index yet it starts from HEAD's tree, so a tracked file that is now ignored stays, as it would in the
 * person's index; with no HEAD either, from nothing. The person's index, HEAD and open merge are never written, and the
 * copy is removed, with its lock, however it ends.
 *
 * Git starts in `cwd` with `env`, the caller's environment unless given, so an inherited `GIT_DIR` or `GIT_WORK_TREE`
 * names the same checkout here as for every other git the caller starts. An unmerged path is staged as the file stands,
 * markers and all: a caller that must not judge one refuses first. Throws when git cannot say (no checkout, git missing,
 * a failed add), for the caller to answer by its own policy.
 */
export function stagedTree(cwd, { env = process.env } = {}) {
  const git = (args, extra = null) => {
    const result = spawnSync('git', args, {
      cwd, encoding: 'utf8', windowsHide: true, maxBuffer: 256 * 1024 * 1024, env: extra ? { ...env, ...extra } : env,
    });
    if (result.error) throw new Error(`git could not start: ${result.error.message}`);
    return { status: result.status, out: result.stdout.trim(), err: (result.stderr || result.stdout).trim() };
  };
  const must = (args, extra) => {
    const result = git(args, extra);
    if (result.status !== 0) throw new Error(`git ${args.join(' ')} failed: ${result.err}`);
    return result.out;
  };

  const located = must(['rev-parse', '--path-format=absolute', '--git-path', 'index', '--git-path', besideName('daoris-snapshot', '.index')]);
  // Resolved against `cwd`: an inherited GIT_INDEX_FILE may be relative to it.
  const [index, staging] = located.split(/\r?\n/).map((line) => line.trim()).filter(Boolean).map((path) => resolve(cwd, path));
  if (!index || !staging) throw new Error(`git rev-parse named no index: ${located}`);
  const own = { GIT_INDEX_FILE: staging };
  try {
    if (existsSync(index)) copyFileSync(index, staging);
    else git(['read-tree', 'HEAD'], own); // an unborn HEAD has no tree, and the stage starts from nothing
    must(['add', '-A'], own);
    const tree = must(['write-tree'], own);
    if (!/^[0-9a-f]{40,64}$/.test(tree)) throw new Error(`git write-tree answered no tree: ${tree}`);
    return tree;
  } finally {
    for (const file of [staging, `${staging}.lock`]) rmSync(file, { force: true });
  }
}

/** Recursive copy. Deliberately not fs.cpSync — it has crashed on this platform. */
export function copyTree(from, to) {
  mkdirSync(to, { recursive: true });
  for (const entry of readdirSync(from, { withFileTypes: true })) {
    const source = join(from, entry.name);
    const target = join(to, entry.name);
    if (entry.isDirectory()) copyTree(source, target);
    else copyFileSync(source, target);
  }
}

/** Every file under `root`, as `/`-separated paths relative to it. */
function filesUnder(root, at = '') {
  if (!existsSync(join(root, at))) return [];
  return readdirSync(join(root, at), { withFileTypes: true }).flatMap((entry) => {
    const path = at ? `${at}/${entry.name}` : entry.name;
    return entry.isDirectory() ? filesUnder(root, path) : [path];
  });
}

/**
 * The files two trees disagree on, sorted: different bytes, or present on one side only. How a gate
 * asks "would this change anything?" of a copy, so that asking never repairs the tree being judged.
 */
export function treeDiff(left, right) {
  const leftFiles = new Set(filesUnder(left));
  const rightFiles = new Set(filesUnder(right));
  return [...new Set([...leftFiles, ...rightFiles])].sort().filter((path) =>
    !leftFiles.has(path) || !rightFiles.has(path)
      || !readFileSync(join(left, path)).equals(readFileSync(join(right, path))));
}

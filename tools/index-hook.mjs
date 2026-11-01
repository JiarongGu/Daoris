/**
 * INDEXAUTO1: a pre-commit hook regenerates `docs/index/` in the commit that stales it.
 *
 *   node tools/index-hook.mjs --install   write the pre-commit hook (0 written, left, or no work tree here · 2 a write failed)
 *   node tools/index-hook.mjs             the hook's run: write the index and stage it (0 done or skipped · 1 the tool failed)
 *
 * A commit staging nothing outside `docs/index/` does no work. A commit with a file staged in part skips, saying so:
 * the index would describe the working copy, not the commit, and `orient-index --check` still gates. `DAORIS_INDEX_HOOK=0`
 * skips the hook. A hook that is not this one's is never overwritten.
 *
 * NOTEBREAK1: before the index, a blank line goes before each `**Note, ` line glued to the line above in a staged
 * `docs/decisions/*.md`, which is re-staged; one staged in part is left, saying so. A conflicted merge finishes through
 * `git commit`, so pre-commit covers it. A clean merge commits without pre-commit and ignores a `pre-merge-commit` hook's
 * staging, so `--merged` (the `post-merge` hook) mends the merge commit just made and amends it.
 */
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, spawnGit, writeAtomic } from './fsx.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const MARK = '# daoris index-hook';
const INDEX = 'docs/index/';

function hookText(script, arg) {
  if (script) return `#!/bin/sh\n${MARK}\nexec node "${script.replaceAll('\\', '/')}"${arg}\n`;
  return `#!/bin/sh\n${MARK}\nroot=$(git rev-parse --show-toplevel) || exit 0\n[ -f "$root/tools/index-hook.mjs" ] || exit 0\nexec node "$root/tools/index-hook.mjs"${arg}\n`;
}

// git computes a clean merge's tree before `pre-merge-commit` runs and ignores what that hook stages, so the clean path is mended
// after the commit by `post-merge`, which amends the merge commit that was just made.
const HOOKS = { 'pre-commit': '', 'post-merge': ' --merged' };

/** Writes the pre-commit hook into the repository's hooks folder; `script` names the tool by absolute path (tests). */
export function install({ out, err, cwd, env, script }) {
  const inside = spawnGit(['rev-parse', '--is-inside-work-tree'], { cwd, env, encoding: 'utf8' });
  if (inside.status !== 0 || inside.stdout.trim() !== 'true') {
    out('index-hook: not a git work tree; nothing installed');
    return 0;
  }
  const found = spawnGit(['rev-parse', '--git-path', 'hooks'], { cwd, env, encoding: 'utf8' });
  if (found.status !== 0) {
    err(`index-hook: git rev-parse --git-path hooks failed: ${found.stderr?.trim()}`);
    return 2;
  }
  const folder = resolve(cwd, found.stdout.trim());
  try {
    mkdirSync(folder, { recursive: true });
    for (const [name, arg] of Object.entries(HOOKS)) {
      const file = join(folder, name);
      if (existsSync(file) && !readFileSync(file, 'utf8').includes(MARK)) {
        out(`index-hook: ${file} is not its own; left as it is`);
        continue;
      }
      writeAtomic(file, hookText(script, arg));
      chmodSync(file, 0o755);
    }
  } catch (error) {
    err(`index-hook: ${error.message}`);
    return 2;
  }
  out('index-hook: pre-commit and post-merge hooks installed');
  return 0;
}

/** Puts a blank line before each `**Note, ` line that follows a non-blank line (a union merge coalesces the two branches' blanks). */
export function separateNotes(text) {
  return text.replace(/([^\r\n])(\r?\n)(\*\*Note, )/g, '$1$2$2$3');
}

const names = (result) => (result.stdout ?? '').split('\0').filter(Boolean);

/** Separates and re-stages the glued notes of the decision files among `paths`; the count mended, or null when a stage failed. */
function mendNotes({ paths, unstaged, git, cwd, out, err }) {
  let mended = 0;
  for (const path of paths.filter((name) => /^docs\/decisions\/[^/]+\.md$/.test(name))) {
    if (unstaged.has(path)) {
      out(`index-hook: ${path} is partially staged; its notes not checked`);
      continue;
    }
    const file = join(cwd, path);
    if (!existsSync(file)) continue; // deleted in the commit
    const text = readFileSync(file, 'utf8');
    const fixed = separateNotes(text);
    if (fixed === text) continue;
    writeAtomic(file, fixed);
    const added = git(['add', '--', path]);
    if (added.status !== 0) {
      err(`index-hook: git add ${path} failed: ${added.stderr?.trim()}`);
      return null;
    }
    mended += 1;
  }
  return mended;
}

/** The `post-merge` run: a clean merge commit holding a glued note is amended with the blank line (a squash or a fast-forward has no second parent). */
export function runMerged({ out, err, cwd, env }) {
  if (env.DAORIS_INDEX_HOOK === '0') return 0;
  const hookEnv = { ...env };
  delete hookEnv.GIT_DIR;
  delete hookEnv.GIT_WORK_TREE;
  const git = (args) => spawnGit(args, { cwd, env: hookEnv, encoding: 'utf8' });
  const parents = git(['--no-optional-locks', 'rev-list', '--parents', '-n', '1', 'HEAD']);
  if (parents.status !== 0 || parents.stdout.trim().split(/\s+/).length < 3) return 0;
  if (git(['--no-optional-locks', 'diff', '--cached', '--quiet']).status !== 0) return 0;
  const changed = git(['--no-optional-locks', 'diff', '--name-only', '-z', 'HEAD^1', 'HEAD', '--', 'docs/decisions']);
  if (changed.status !== 0) return 0;
  const unstaged = new Set(names(git(['--no-optional-locks', 'diff', '--name-only', '-z'])));
  const mended = mendNotes({ paths: names(changed), unstaged, git, cwd, out, err });
  if (mended === null) return 1;
  if (mended === 0) return 0;
  // `git commit --amend` refuses while MERGE_HEAD still stands, so the commit is rebuilt with the same parents, message and author.
  const [head, ...parentIds] = parents.stdout.trim().split(/\s+/);
  const tree = git(['write-tree']).stdout.trim();
  const meta = git(['--no-optional-locks', 'log', '-1', '--format=%an%x00%ae%x00%aI%x00%B', 'HEAD']).stdout.split('\0');
  const authorEnv = { ...hookEnv, GIT_AUTHOR_NAME: meta[0], GIT_AUTHOR_EMAIL: meta[1], GIT_AUTHOR_DATE: meta[2] };
  const made = spawnGit(['commit-tree', tree, ...parentIds.flatMap((id) => ['-p', id]), '-F', '-'],
    { cwd, env: authorEnv, input: `${meta.slice(3).join('\0').trimEnd()}\n`, encoding: 'utf8' });
  const created = made.stdout?.trim();
  const moved = made.status === 0 && created ? git(['update-ref', '-m', 'index-hook: blank line before a note', 'HEAD', created, head]) : made;
  if (moved.status !== 0) {
    err(`index-hook: remaking the merge commit failed: ${moved.stderr?.trim()}`);
    return 1;
  }
  return 0;
}

/** The hook's run, in `cwd` (the work tree's top, where git starts a hook). */
export function run({ out, err, cwd, env }) {
  if (env.DAORIS_INDEX_HOOK === '0') return 0;
  const hookEnv = { ...env };
  delete hookEnv.GIT_DIR;
  delete hookEnv.GIT_WORK_TREE;
  const git = (args) => spawnGit(args, { cwd, env: hookEnv, encoding: 'utf8' });
  const staged = git(['--no-optional-locks', 'diff', '--cached', '--name-only', '-z']);
  if (staged.status !== 0) {
    err(`index-hook: git diff --cached failed: ${staged.stderr?.trim()}`);
    return 1;
  }
  const outside = names(staged).filter((path) => !path.startsWith(INDEX));
  if (outside.length === 0) return 0;
  const unstaged = new Set(names(git(['--no-optional-locks', 'diff', '--name-only', '-z'])));
  if (mendNotes({ paths: outside, unstaged, git, cwd, out, err }) === null) return 1;
  if (outside.some((path) => unstaged.has(path))) {
    out('index-hook: a file is partially staged; docs/index/ not regenerated (orient-index --check still gates)');
    return 0;
  }
  const written = spawnSync(process.execPath, [join(here, 'orient-index.mjs'), '--root', cwd], { cwd, env: hookEnv, encoding: 'utf8', windowsHide: true });
  if (written.status !== 0) {
    err(`index-hook: orient-index failed, commit aborted (DAORIS_INDEX_HOOK=0 skips the hook): ${(written.stderr || written.error?.message || '').trim()}`);
    return 1;
  }
  const added = git(['add', '--', 'docs/index']);
  if (added.status !== 0) {
    err(`index-hook: git add docs/index failed, commit aborted: ${added.stderr?.trim()}`);
    return 1;
  }
  return 0;
}

export function main(argv, { out = console.log, err = console.error, cwd = process.cwd(), env = process.env } = {}) {
  if (argv[0] === '--install' && argv.length === 1) return install({ out, err, cwd, env });
  if (argv[0] === '--merged' && argv.length === 1) return runMerged({ out, err, cwd, env });
  if (argv.length > 0) {
    err('usage: node tools/index-hook.mjs [--install | --merged]');
    return 2;
  }
  return run({ out, err, cwd, env });
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

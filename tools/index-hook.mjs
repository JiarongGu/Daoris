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
 * `docs/decisions/*.md`, which is re-staged; one staged in part is left, saying so (a conflicted merge finishes through
 * `git commit`, so this covers it). A clean merge commits without pre-commit and git ignores what a `pre-merge-commit`
 * hook stages, so `--install` also sets the `notes` merge driver (`merge.notes.*`, the repository's own configuration)
 * that `.gitattributes` names for `docs/decisions/*.md`: `--merge-notes <base> <ours> <theirs>` merges by union and
 * separates the notes, so the merge's own result is right. A clone without the driver merges those files as ordinary
 * text, which conflicts on two notes added at one place rather than gluing them.
 */
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, spawnGit, writeAtomic } from './fsx.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const MARK = '# daoris index-hook';
const INDEX = 'docs/index/';

function hookText(script) {
  if (script) return `#!/bin/sh\n${MARK}\nexec node "${script.replaceAll('\\', '/')}"\n`;
  return `#!/bin/sh\n${MARK}\nroot=$(git rev-parse --show-toplevel) || exit 0\n[ -f "$root/tools/index-hook.mjs" ] || exit 0\nexec node "$root/tools/index-hook.mjs"\n`;
}

/** The notes merge driver's command as `--install` configures it, run by git from the work tree's top. */
export const NOTES_DRIVER = 'node tools/index-hook.mjs --merge-notes %O %A %B';

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
    const file = join(folder, 'pre-commit');
    if (existsSync(file) && !readFileSync(file, 'utf8').includes(MARK)) {
      out(`index-hook: ${file} is not its own; left as it is`);
    } else {
      writeAtomic(file, hookText(script));
      chmodSync(file, 0o755);
      out('index-hook: pre-commit hook installed');
    }
  } catch (error) {
    err(`index-hook: ${error.message}`);
    return 2;
  }
  const driver = script ? `node "${script.replaceAll('\\', '/')}" --merge-notes %O %A %B` : NOTES_DRIVER;
  for (const [key, value] of [['merge.notes.name', 'notes: union, a blank line before each decision note (NOTEBREAK1)'], ['merge.notes.driver', driver]]) {
    const set = spawnGit(['config', '--local', key, value], { cwd, env, encoding: 'utf8' });
    if (set.status !== 0) {
      err(`index-hook: git config ${key} failed: ${set.stderr?.trim() ?? set.error?.message}`);
      return 2;
    }
  }
  return 0;
}

/** The `notes` merge driver: git's union merge into ours (%A), then each glued note separated. 0 merged, 2 a tool error. */
export function mergeNotes([base, ours, theirs], { err, cwd, env }) {
  if (!base || !ours || !theirs) {
    err('usage: node tools/index-hook.mjs --merge-notes <base> <ours> <theirs>');
    return 2;
  }
  const merged = spawnGit(['merge-file', '--union', ours, base, theirs], { cwd, env, encoding: 'utf8' });
  if (merged.status !== 0) {
    err(`index-hook: git merge-file failed: ${merged.stderr?.trim() ?? merged.error?.message}`);
    return 2;
  }
  const text = readFileSync(ours, 'utf8');
  const fixed = separateNotes(text);
  if (fixed !== text) writeAtomic(ours, fixed);
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
  if (argv[0] === '--merge-notes') return mergeNotes(argv.slice(1), { err, cwd, env });
  if (argv.length > 0) {
    err('usage: node tools/index-hook.mjs [--install]');
    return 2;
  }
  return run({ out, err, cwd, env });
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

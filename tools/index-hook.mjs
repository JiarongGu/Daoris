/**
 * INDEXAUTO1: a pre-commit hook regenerates `docs/index/` in the commit that stales it.
 *
 *   node tools/index-hook.mjs --install   write the pre-commit hook (0 written, left, or no work tree here · 2 a write failed)
 *   node tools/index-hook.mjs             the hook's run: write the index and stage it (0 done or skipped · 1 the tool failed)
 *
 * A commit staging nothing outside `docs/index/` does no work. A commit with a file staged in part skips, saying so:
 * the index would describe the working copy, not the commit, and `orient-index --check` still gates. `DAORIS_INDEX_HOOK=0`
 * skips the hook. A hook that is not this one's is never overwritten. Merge commits run no pre-commit hook;
 * `merge-branch.mjs` writes the index into its own.
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
  const file = join(folder, 'pre-commit');
  try {
    if (existsSync(file) && !readFileSync(file, 'utf8').includes(MARK)) {
      out(`index-hook: ${file} is not its own; left as it is`);
      return 0;
    }
    mkdirSync(folder, { recursive: true });
    writeAtomic(file, hookText(script));
    chmodSync(file, 0o755);
  } catch (error) {
    err(`index-hook: ${error.message}`);
    return 2;
  }
  out('index-hook: pre-commit hook installed');
  return 0;
}

const names = (result) => (result.stdout ?? '').split('\0').filter(Boolean);

/** The hook's run, in `cwd` (the work tree's top, where git starts a hook). */
export function run({ out, err, cwd, env }) {
  if (env.DAORIS_INDEX_HOOK === '0') return 0;
  const hookEnv = { ...env };
  delete hookEnv.GIT_DIR;
  delete hookEnv.GIT_WORK_TREE;
  const git = (args) => spawnGit(args, { cwd, env: hookEnv, encoding: 'utf8' });
  const staged = git(['diff', '--cached', '--name-only', '-z']);
  if (staged.status !== 0) {
    err(`index-hook: git diff --cached failed: ${staged.stderr?.trim()}`);
    return 1;
  }
  const outside = names(staged).filter((path) => !path.startsWith(INDEX));
  if (outside.length === 0) return 0;
  const unstaged = new Set(names(git(['diff', '--name-only', '-z'])));
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
  if (argv.length > 0) {
    err('usage: node tools/index-hook.mjs [--install]');
    return 2;
  }
  return run({ out, err, cwd, env });
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

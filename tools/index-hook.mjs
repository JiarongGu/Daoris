/**
 * The repository's own git configuration, which `npm ci` installs (`prepare`): the checkout hooks that write the ignored
 * orientation index, and the `notes` merge driver for the decisions.
 *
 *   node tools/index-hook.mjs --install                          (0 installed, left, or no work tree here · 2 a write failed)
 *   node tools/index-hook.mjs --merge-notes <base> <ours> <theirs>   the driver's run (0 merged · 2 a tool error)
 *
 * `post-checkout` (a branch's checkout, a new worktree's included) and `post-merge` run `tools/index-fresh.mjs` in the work
 * tree, quietly and always exiting 0, since a checkout hook's status becomes the checkout's. A detached checkout is a tool's
 * tree at a commit (a merge's gate tree, a replay), which nobody reads for where things are, so it is skipped, as is every
 * run under `DAORIS_INDEX_HOOK=0`. A hook that is not this one's is never overwritten. A fresh clone has no index until
 * its first checkout, so `--install` writes one where none stands.
 *
 * The `notes` driver `.gitattributes` names for `docs/decisions/*.md` merges by union and puts a blank line
 * before each glued `**Note, ` line, so the merge's own result is right. A clone without the driver merges those files as
 * ordinary text, which conflicts on two notes added at one place rather than gluing them.
 */
import { chmodSync, existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setApart } from './doc-duplicates.mjs';
import { isMain, spawnGit, writeAtomic } from './fsx.mjs';
import { INDEX, writeIndex } from './index-fresh.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const MARK = '# daoris index-hook';
/** Each hook written, with the line that lets only a branch's checkout through (`post-checkout`'s third argument is 1). */
const HOOKS = [['post-checkout', '[ "$3" = 1 ] || exit 0\n'], ['post-merge', '']];

/** A hook's text; `script` names `index-fresh.mjs` by absolute path (tests), else the work tree's own is run. */
export function hookText(gate, script) {
  const fresh = script ? `"${script.replaceAll('\\', '/')}"` : '"$root/tools/index-fresh.mjs"';
  return `#!/bin/sh\n${MARK}\n[ "$DAORIS_INDEX_HOOK" = 0 ] && exit 0\n${gate}`
    + 'root=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0\n'
    + 'git symbolic-ref -q HEAD >/dev/null || exit 0\n'
    + (script ? '' : '[ -f "$root/tools/index-fresh.mjs" ] || exit 0\n')
    + `node ${fresh} --root "$root" >/dev/null 2>&1\nexit 0\n`;
}

/** The notes merge driver's command as `--install` configures it, run by git from the work tree's top. */
export const NOTES_DRIVER = 'node tools/index-hook.mjs --merge-notes %O %A %B';

/** Writes the checkout hooks and the notes driver; `script` names this tool by absolute path (tests). */
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
  const fresh = script ? join(dirname(script), 'index-fresh.mjs') : undefined;
  try {
    mkdirSync(folder, { recursive: true });
    // The pre-commit hook an older install wrote, which staged the index into each commit.
    const old = join(folder, 'pre-commit');
    if (existsSync(old) && readFileSync(old, 'utf8').includes(MARK)) rmSync(old, { force: true });
    for (const [name, gate] of HOOKS) {
      const file = join(folder, name);
      if (existsSync(file) && !readFileSync(file, 'utf8').includes(MARK)) {
        out(`index-hook: ${file} is not its own; left as it is`);
        continue;
      }
      writeAtomic(file, hookText(gate, fresh));
      chmodSync(file, 0o755);
    }
    out('index-hook: checkout hooks installed');
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
  const top = spawnGit(['rev-parse', '--show-toplevel'], { cwd, env, encoding: 'utf8' });
  if (top.status === 0 && !existsSync(join(top.stdout.trim(), INDEX, 'README.md'))) {
    const root = top.stdout.trim();
    const written = writeIndex(root, { tool: join(here, 'orient-index.mjs'), env });
    out(`index-hook: ${written.ok ? written.said : `the index was not written: ${written.said}`}`);
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

/** Sets apart each glued note label (a union merge coalesces the two branches' blanks), and an undated `**Note, ` line. */
export function separateNotes(text) {
  return setApart(text).text.replace(/([^\r\n])(\r?\n)(\*\*Note, )/g, '$1$2$2$3');
}

export function main(argv, { out = console.log, err = console.error, cwd = process.cwd(), env = process.env } = {}) {
  if (argv[0] === '--install' && argv.length === 1) return install({ out, err, cwd, env });
  if (argv[0] === '--merge-notes') return mergeNotes(argv.slice(1), { err, cwd, env });
  // An older install's pre-commit hook calls this with nothing, until `--install` removes that hook.
  if (argv.length === 0) return 0;
  err('usage: node tools/index-hook.mjs --install | --merge-notes <base> <ours> <theirs>');
  return 2;
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

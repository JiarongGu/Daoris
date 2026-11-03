/**
 * Keeps a checkout's orientation index (`docs/index/`, ignored and never committed) written from the tree it stands at.
 *
 *   node tools/index-fresh.mjs [--root <dir>]   write the index of the work tree at <dir>, this checkout's by default
 *                                               (0 written · 1 the generator failed · 2 usage)
 *
 * Two places ask. The checkout hooks `tools/index-hook.mjs --install` writes run this after git moves a branch's work
 * tree (a new worktree, a switch, a merge into it), so a session starts with an index. The knowledge server asks
 * `freshIndex` before it answers a request, so a read follows the commit the checkout it serves moved to. A writer is
 * the tree's own `tools/orient-index.mjs`; a tree without one is left as it is.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, spawnGit, writeAtomic } from './fsx.mjs';

export const INDEX = 'docs/index';
const GENERATOR = 'orient-index.mjs';

/** The environment a write runs in: a hook is started with git's own variables, which would point the generator's git elsewhere. */
export function cleanEnv(env = process.env) {
  const out = { ...env };
  for (const name of ['GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'GIT_COMMON_DIR', 'GIT_PREFIX']) delete out[name];
  return out;
}

/** Writes the index of the work tree at `root` with `tool`, the tree's own generator by default: `{ ok, said }`. */
export function writeIndex(root, { tool = join(root, 'tools', GENERATOR), env = process.env } = {}) {
  if (!existsSync(tool)) return { ok: false, said: `no ${GENERATOR} in this tree` };
  const ran = spawnSync(process.execPath, [tool, '--root', root], { cwd: root, env: cleanEnv(env), encoding: 'utf8', windowsHide: true });
  const said = ran.status === 0 ? ran.stdout : ran.stderr || ran.error?.message || `exit ${ran.status}`;
  return { ok: ran.status === 0, said: String(said).trim() };
}

/** The commit `HEAD` names in the work tree at `root`, or null. */
export function headOf(root, env = process.env) {
  const head = spawnGit(['rev-parse', '--verify', '-q', 'HEAD'], { cwd: root, env: cleanEnv(env), encoding: 'utf8' });
  return head.status === 0 ? head.stdout.trim() : null;
}

/**
 * Writes the index of `root` where it is missing or `stamp` names a commit other than `HEAD`'s, then stamps `HEAD`'s:
 * `{ written, ok, said }`. A failed write is not stamped, so the next ask tries again.
 */
export function freshIndex(root, { stamp, head = headOf, write = writeIndex } = {}) {
  const at = head(root);
  let stamped = null;
  try {
    stamped = readFileSync(stamp, 'utf8').trim();
  } catch {
    // Never stamped.
  }
  if (at !== null && stamped === at && existsSync(join(root, INDEX, 'README.md'))) return { written: false, ok: true, said: '' };
  const result = write(root);
  if (result.ok && at !== null) writeAtomic(stamp, `${at}\n`);
  return { written: true, ...result };
}

export function main(argv, { out = console.log, err = console.error, cwd = process.cwd() } = {}) {
  const at = argv.indexOf('--root');
  if (argv.length !== (at >= 0 ? 2 : 0) || (at >= 0 && !argv[at + 1])) {
    err('usage: node tools/index-fresh.mjs [--root <dir>]');
    return 2;
  }
  const root = at >= 0 ? resolve(cwd, argv[at + 1]) : join(dirname(fileURLToPath(import.meta.url)), '..');
  const result = writeIndex(root, { tool: join(dirname(fileURLToPath(import.meta.url)), GENERATOR) });
  (result.ok ? out : err)(`index-fresh: ${result.said}`);
  return result.ok ? 0 : 1;
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

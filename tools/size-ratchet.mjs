/**
 * A source file over the budget does not grow, and a new one does not start over it.
 *
 *   node tools/size-ratchet.mjs           check the tree against tools/size-ratchet.json (exit 0 clean, 1 breach, 2 tool error)
 *   node tools/size-ratchet.mjs --write   rewrite tools/size-ratchet.json from the tree (the first list, or after a split)
 *   node tools/size-ratchet.mjs --shrink  only lower the list to the tree (the merge's, and the commit hook's, writer; growth is never written)
 *
 * The list names each source file over `BUDGET` bytes and the size it stands at. A listed file may shrink but never grow
 * past its entry, and a file not listed may not pass the budget: new behaviour goes in a unit of its own. A listed file
 * that shrank is a breach until its entry is cut down to it, so the list only tightens; a file that fell under the budget
 * leaves the list. Sizes are bytes with LF endings, so a checkout's line endings change nothing.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execGit, isMain, repositoryFiles, writeAtomic } from './fsx.mjs';

/** The budget a source file is held under, in bytes with LF endings. */
export const BUDGET = 40 * 1024;

const SOURCE = /\.(cs|ts|tsx|mjs|js)$/;
const SKIPPED = /(^|\/)(bin|obj|node_modules|_fixtures|wwwroot|dist)\//;

/** Whether the ratchet holds `path`: a source file, build output and dependencies aside. */
export const sized = (path) => SOURCE.test(path) && !SKIPPED.test(path);

/** A file's size in bytes with LF endings. */
export const sizeOf = (text) => Buffer.byteLength(text.replace(/\r\n/g, '\n'), 'utf8');

/** Each source file of the tree at `root` over the budget, with its size, sorted by path. */
export function scanSizes(root, { files = repositoryFiles(root), read = (path) => readFileSync(join(root, path), 'utf8') } = {}) {
  const over = {};
  for (const path of files) {
    if (!sized(path)) continue;
    const size = sizeOf(read(path));
    if (size > BUDGET) over[path] = size;
  }
  return Object.fromEntries(Object.entries(over).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
}

/** The breaches of `list` (path -> bytes) against `found` (the same, from the tree), each `{ path, text }`: no file past its size in `base` (main's list) where given, nor past its own entry. */
export function breaches(found, list, base = null) {
  const problems = [];
  const add = (path, text) => problems.push({ path, text });
  for (const [path, size] of Object.entries(found)) {
    const listed = list[path];
    const limit = base ? base[path] : listed;
    if (listed === undefined || limit === undefined) {
      add(path, `${path} is ${size} bytes, over the ${BUDGET}-byte budget: put the new behaviour in a unit of its own`);
    } else if (size > limit) {
      add(path, `${path} grew from ${limit} to ${size} bytes: a file over the budget does not grow, so move the new behaviour into a unit of its own`);
    } else if (size > listed) {
      add(path, `${path} is ${size} bytes, over the ${listed} its entry holds (main's list allows ${limit}): write the list again (node tools/size-ratchet.mjs --write) so it holds the size`);
    } else if (size < listed) {
      add(path, `${path} shrank from ${listed} to ${size} bytes: write the list again (node tools/size-ratchet.mjs --write) so it holds the new size`);
    }
  }
  for (const path of Object.keys(list)) {
    if (found[path] === undefined) add(path, `${path} is under the budget now or gone: write the list again (node tools/size-ratchet.mjs --write)`);
  }
  return problems;
}

export const checkSizes = (found, list, base = null) => breaches(found, list, base).map((each) => each.text);

/** `list` cut down to the tree (`found`, the files over the budget): a shrunk entry takes its size, one under the budget or gone leaves; growth and new files stay for the check. */
export function lowered(list, found) {
  const out = {};
  for (const [path, listed] of Object.entries(list)) {
    if (found[path] !== undefined) out[path] = Math.min(listed, found[path]);
  }
  return out;
}

/**
 * The staged files settled against the list at a commit: `sizes` maps each staged source path to its size (null where it is deleted).
 * Returns the list with every shrink written, and the refusals: a file past the size main's list gives it, or new over the budget.
 */
export function settleStaged(sizes, list, base = null) {
  const settled = { ...list };
  const problems = [];
  for (const [path, size] of Object.entries(sizes)) {
    const listed = list[path];
    const limit = base ? base[path] : listed;
    if (size === null || size <= BUDGET) {
      delete settled[path];
    } else if (listed === undefined || limit === undefined) {
      problems.push(`${path} is ${size} bytes, over the ${BUDGET}-byte budget: put the new behaviour in a unit of its own`);
    } else if (size > limit) {
      problems.push(`${path} grew from ${limit} to ${size} bytes: a file over the budget does not grow, so move the new behaviour into a unit of its own`);
    } else if (size !== listed) {
      settled[path] = size;
    }
  }
  return { list: settled, problems };
}

/** The list as main holds it at the merge-base. */
export function baseList(root) {
  const git = (args) => execGit(args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  const base = git(['merge-base', 'main', 'HEAD']).trim();
  return JSON.parse(git(['show', `${base}:tools/size-ratchet.json`]));
}

async function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const listFile = join(root, 'tools', 'size-ratchet.json');
  try {
    const found = scanSizes(root);
    if (process.argv.includes('--write')) {
      writeAtomic(listFile, JSON.stringify(found, null, 2) + '\n');
      console.log(`size-ratchet: wrote ${Object.keys(found).length} files over the budget to tools/size-ratchet.json`);
      return 0;
    }
    if (process.argv.includes('--shrink')) {
      const before = JSON.parse(readFileSync(listFile, 'utf8'));
      const list = lowered(before, found);
      const cut = Object.keys(before).filter((path) => list[path] !== before[path]).length;
      if (cut > 0) writeAtomic(listFile, JSON.stringify(list, null, 2) + '\n');
      console.log(`size-ratchet: lowered ${cut} entr${cut === 1 ? 'y' : 'ies'} of tools/size-ratchet.json to the tree`);
      return 0;
    }
    return (await import('./size-ratchet-check.mjs')).checkTree(root, found);
  } catch (e) {
    console.error(`size-ratchet: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = await main();

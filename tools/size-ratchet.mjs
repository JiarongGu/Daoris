/**
 * A source file over the budget does not grow, and a new one does not start over it.
 *
 *   node tools/size-ratchet.mjs           check the tree against tools/size-ratchet.json (exit 0 clean, 1 breach, 2 tool error)
 *   node tools/size-ratchet.mjs --write   rewrite tools/size-ratchet.json from the tree (the first list, or after a split)
 *
 * The list names each source file over `BUDGET` bytes and the size it stands at. A listed file may shrink but never grow
 * past its entry, and a file not listed may not pass the budget: new behaviour goes in a unit of its own. A listed file
 * that shrank is a breach until its entry is cut down to it, so the list only tightens; a file that fell under the budget
 * leaves the list. Sizes are bytes with LF endings, so a checkout's line endings change nothing.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, repositoryFiles, writeAtomic } from './fsx.mjs';

/** The budget a source file is held under, in bytes with LF endings. */
export const BUDGET = 40 * 1024;

const SOURCE = /\.(cs|ts|tsx|mjs|js)$/;
const SKIPPED = /(^|\/)(bin|obj|node_modules|_fixtures|wwwroot|dist)\//;

/** A file's size in bytes with LF endings. */
export const sizeOf = (text) => Buffer.byteLength(text.replace(/\r\n/g, '\n'), 'utf8');

/** Each source file of the tree at `root` over the budget, with its size, sorted by path. */
export function scanSizes(root, { files = repositoryFiles(root), read = (path) => readFileSync(join(root, path), 'utf8') } = {}) {
  const over = {};
  for (const path of files) {
    if (!SOURCE.test(path) || SKIPPED.test(path)) continue;
    const size = sizeOf(read(path));
    if (size > BUDGET) over[path] = size;
  }
  return Object.fromEntries(Object.entries(over).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
}

/** The breaches of `list` (path -> bytes) against `found` (the same, from the tree). */
export function checkSizes(found, list) {
  const problems = [];
  for (const [path, size] of Object.entries(found)) {
    const listed = list[path];
    if (listed === undefined) {
      problems.push(`${path} is ${size} bytes, over the ${BUDGET}-byte budget: put the new behaviour in a unit of its own`);
    } else if (size > listed) {
      problems.push(`${path} grew from ${listed} to ${size} bytes: a file over the budget does not grow, so move the new behaviour into a unit of its own`);
    } else if (size < listed) {
      problems.push(`${path} shrank from ${listed} to ${size} bytes: write the list again (node tools/size-ratchet.mjs --write) so it holds the new size`);
    }
  }
  for (const path of Object.keys(list)) {
    if (found[path] === undefined) problems.push(`${path} is under the budget now or gone: write the list again (node tools/size-ratchet.mjs --write)`);
  }
  return problems;
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const listFile = join(root, 'tools', 'size-ratchet.json');
  try {
    const found = scanSizes(root);
    if (process.argv.includes('--write')) {
      writeAtomic(listFile, JSON.stringify(found, null, 2) + '\n');
      console.log(`size-ratchet: wrote ${Object.keys(found).length} files over the budget to tools/size-ratchet.json`);
      return 0;
    }
    const problems = checkSizes(found, JSON.parse(readFileSync(listFile, 'utf8')));
    for (const p of problems) console.error(`size-ratchet: ${p}`);
    if (problems.length) return 1;
    console.log(`size-ratchet: clean (${Object.keys(found).length} files over the budget, none grown)`);
    return 0;
  } catch (e) {
    console.error(`size-ratchet: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();

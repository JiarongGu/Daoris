/**
 * The layers' census's source counts alone (1, 2 and 4 of `tools/layers-census.mjs`, and the clock's allowance): no duration record is
 * read and no test timed, so it reads only the sources, their projects, the lanes and the list. It is a step of the merge's `cli` gate
 * (`tools/verify-checks/steps.mjs`), run where a changed path is one of those, and of `npm run quick`; the full census adds the unit budget.
 *
 *   node tools/layers-census/counts.mjs   check the tree's counts (exit 0 clean, 1 a breach, 2 tool error)
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, repositoryFiles } from '../fsx.mjs';
import { checkAmbient, scanAmbient } from './ambient.mjs';
import { checkBoundaries } from './boundary.mjs';
import { checkClock, scanClock } from './clock.mjs';
import { checkGit, scanGit } from './git.mjs';
import { checkMemory } from './memory.mjs';
import { checkProcesses, scanProcesses } from './processes.mjs';
import { checkReferences } from './references.mjs';

/** The list's sections, each empty, under what the file holds. */
export const EMPTY = Object.freeze({ packages: {}, ambient: {}, clock: {}, processes: {}, git: {}, slowTests: {}, boundaries: {} });

/** The list the tree at `root` holds, `tools/layers-census.json`. */
export const LIST_FILE = join('tools', 'layers-census.json');

/** The tracked files at `root` and a reader of each, read once. */
export function treeAt(root) {
  const read = new Map();
  return { files: repositoryFiles(root), read: (path) => read.get(path) ?? read.set(path, readFileSync(join(root, path), 'utf8')).get(path) };
}

/** What the counts find wrong with `tree` against `list`. */
export function countProblems(tree, list) {
  return [
    ...checkReferences(tree, list), ...checkAmbient(scanAmbient(tree), list.ambient), ...checkClock(scanClock(tree), list.clock),
    ...checkProcesses(scanProcesses(tree), list.processes), ...checkGit(scanGit(tree), list.git), ...checkMemory(tree),
    ...checkBoundaries(tree, list.boundaries),
  ];
}

/** The counts of the tree at `root` (or `tree`) against its list, each breach said; the exit code. */
export function counts({ root, tree = null, say = console.log, warn = console.error }) {
  try {
    const problems = countProblems(tree ?? treeAt(root), { ...EMPTY, ...JSON.parse(readFileSync(join(root, LIST_FILE), 'utf8')) });
    for (const problem of problems) warn(`layers-census: ${problem}`);
    if (problems.length === 0) say('layers-census: the reference graph, the ambient ratchet, the clock\'s, process starts\' and git\'s allowances and the lanes\' boundaries hold; the unit budget is the full census\'s');
    return problems.length === 0 ? 0 : 1;
  } catch (error) {
    warn(`layers-census: ${error.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = counts({ root: dirname(dirname(dirname(fileURLToPath(import.meta.url)))) });

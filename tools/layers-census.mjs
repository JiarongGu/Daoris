/**
 * The layers' census: what holds the application apart from its infrastructure by construction (the ports-and-adapters design,
 * `docs/2026-11-13-layers-design.md` §6), the checks that cost no test time.
 *
 *   node tools/layers-census.mjs            check the tree (exit 0 clean, 1 a breach, 2 tool error)
 *   node tools/layers-census.mjs --write    write tools/layers-census.json from the tree, the first list or after a shrink
 *   node tools/layers-census.mjs --counts   the source counts alone (1, 2, 4 and the wall clock's allowance): no duration record is read, no test timed
 *   node tools/layers-census.mjs --boundary <lane>   a lane's references across its boundary today, held to the list where it names the
 *                                                     lane; with --write, the lane joins the list
 *
 * 1. The reference graph: read from the project files, so it holds before any adapter exists.
 * 2. The ambient ports' reads per subdomain, a ratchet that only tightens; beside it the process and git sites' allowances, which only fall.
 * 3. The unit budget: a unit test over a second fails, read from each non-Process .NET gate's trx under `_fixtures/test-timing/`
 *    (`--records <dir>` reads another folder; none, nothing read), each test over it said with its cause; beside it the clock's per-file allowance, which only falls.
 * 4. A package's boundary: for each lane the list's `boundaries` names, the references across it both ways per type, a ratchet that
 *    only falls while the package's code moves behind its ports.
 *
 * The compiler's check, the banned effects, is the build's: `src/BannedEffects.txt` through `src/Layers.props`.
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, repositoryFiles, writeAtomic } from './fsx.mjs';
import { checkAmbient, scanAmbient } from './layers-census/ambient.mjs';
import { checkClock, scanClock } from './layers-census/clock.mjs';
import { BUDGET_MS, checkBudget, parseTrx, readRecords, slowTestsOf } from './layers-census/budget.mjs';
import { budgetBreaches, checkBudgetAlone } from './layers-census/budget-alone.mjs';
import { measureAlone } from './time-ratchet.mjs';
import { sayBreach, sourceReader } from './time-why.mjs';
import { loadWorld } from './time-reach.mjs';
import { checkGit, scanGit } from './layers-census/git.mjs';
import { checkMemory } from './layers-census/memory.mjs';
import { checkProcesses, scanProcesses } from './layers-census/processes.mjs';
import { checkReferences, packagesOutside } from './layers-census/references.mjs';
import { checkBoundaries, sayBoundary, scanBoundary } from './layers-census/boundary.mjs';

export {
  checkAmbient, checkBudget, checkClock, checkGit, checkMemory, checkProcesses, checkReferences, parseTrx, scanAmbient, scanClock, scanGit,
  scanProcesses, BUDGET_MS,
};

const EMPTY = { packages: {}, ambient: {}, clock: {}, processes: {}, git: {}, slowTests: {}, boundaries: {} };

export function countProblems(tree, list) {
  return [
    ...checkReferences(tree, list), ...checkAmbient(scanAmbient(tree), list.ambient), ...checkClock(scanClock(tree), list.clock),
    ...checkProcesses(scanProcesses(tree), list.processes), ...checkGit(scanGit(tree), list.git), ...checkMemory(tree),
    ...checkBoundaries(tree, list.boundaries),
  ];
}

/** The lanes whose boundaries the list holds, with the lane the argument names joined; each with its counts today. */
function boundariesOf(tree, listed, named) {
  const ids = [...new Set([...Object.keys(listed ?? {}), ...(named ? [named] : [])])].sort();
  return Object.fromEntries(ids.map((id) => {
    const { inbound, outbound } = scanBoundary(tree, id);
    return [id, { inbound, outbound }];
  }));
}

export async function census({ root, argv = [], tree = null }) {
  const listFile = join(root, 'tools', 'layers-census.json');
  const recordsAt = argv.includes('--records') ? argv[argv.indexOf('--records') + 1] : join(root, '_fixtures', 'test-timing');
  try {
    const read = new Map();
    tree ??= { files: repositoryFiles(root), read: (path) => read.get(path) ?? read.set(path, readFileSync(join(root, path), 'utf8')).get(path) };
    const lane = argv.includes('--boundary') ? argv[argv.indexOf('--boundary') + 1] : null;
    if (lane && !argv.includes('--write')) {
      const listed = { ...EMPTY, ...JSON.parse(readFileSync(listFile, 'utf8')) }.boundaries;
      for (const line of sayBoundary(lane, scanBoundary(tree, lane))) console.log(line);
      if (!listed[lane]) {
        console.log(`layers-census: the list does not hold lane ${lane}; node tools/layers-census.mjs --write --boundary ${lane} adds it`);
        return 0;
      }
      const problems = checkBoundaries(tree, { [lane]: listed[lane] });
      for (const problem of problems) console.error(`layers-census: ${problem}`);
      return problems.length === 0 ? 0 : 1;
    }
    if (argv.includes('--counts')) {
      const problems = countProblems(tree, { ...EMPTY, ...JSON.parse(readFileSync(listFile, 'utf8')) });
      for (const problem of problems) console.error(`layers-census: ${problem}`);
      if (problems.length === 0) console.log('layers-census: the reference graph, the ambient ratchet, the clock\'s, process starts\' and git\'s allowances and the lanes\' boundaries hold; the unit budget is the full census\'s');
      return problems.length === 0 ? 0 : 1;
    }
    const records = readRecords(recordsAt);
    if (argv.includes('--write')) {
      const before = existsSync(listFile) ? JSON.parse(readFileSync(listFile, 'utf8')) : {};
      const list = {
        packages: packagesOutside(tree), ambient: scanAmbient(tree), clock: scanClock(tree), processes: scanProcesses(tree), git: scanGit(tree),
        slowTests: slowTestsOf(records), boundaries: boundariesOf(tree, before.boundaries, lane),
      };
      writeAtomic(listFile, `${JSON.stringify(list, null, 2)}\n`);
      console.log(`layers-census: wrote ${listFile.slice(root.length + 1).replace(/\\/g, '/')}`);
      return 0;
    }
    const list = { ...EMPTY, ...JSON.parse(readFileSync(listFile, 'utf8')) };
    // Only the default folder is one a measure writes into, so another folder's records are judged as they are.
    const measuring = !argv.includes('--records');
    const budget = measuring
      ? await checkBudgetAlone({ records, list, dir: recordsAt, world: () => loadWorld(root), measure: (units) => measureAlone(root, units) })
      : { problems: checkBudget(records, list) };
    const problems = [...countProblems(tree, list), ...budget.problems];
    for (const problem of problems) console.error(`layers-census: ${problem}`);
    const judged = budget.measured?.length ? readRecords(recordsAt) : records;
    for (const breach of budgetBreaches(judged, list, sourceReader(root, tree))) console.log(sayBreach(breach));
    if (records.length === 0) console.log(`layers-census: no duration records under ${recordsAt.slice(root.length + 1).replace(/\\/g, '/')}, so the unit budget read nothing`);
    if (problems.length === 0) console.log('layers-census: the reference graph, the ambient ratchet, the process starts\' and git\'s allowances, the lanes\' boundaries and the unit budget hold');
    return problems.length === 0 ? 0 : 1;
  } catch (error) {
    console.error(`layers-census: ${error.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) census({ root: dirname(dirname(fileURLToPath(import.meta.url))), argv: process.argv.slice(2) }).then((code) => process.exit(code));

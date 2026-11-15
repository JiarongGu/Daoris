/**
 * The layers' census: what holds the application apart from its infrastructure by construction (the ports-and-adapters design,
 * `docs/2026-11-13-layers-design.md` §6), the checks that cost no test time.
 *
 *   node tools/layers-census.mjs            check the tree (exit 0 clean, 1 a breach, 2 tool error)
 *   node tools/layers-census.mjs --write    write tools/layers-census.json from the tree, the first list or after a shrink
 *   node tools/layers-census.mjs --counts   the source counts alone (1, 2 and the wall clock's allowance): no duration record is read, no test timed
 *
 * 1. The reference graph: read from the project files, so it holds before any adapter exists.
 * 2. The ambient ports' reads per subdomain, a ratchet that only tightens; beside it the process and git sites' allowances, which only fall.
 * 3. The unit budget: a unit test over a second fails, read from each non-Process .NET gate's trx under `_fixtures/test-timing/`
 *    (`--records <dir>` reads another folder; no records, nothing read). Beside it the wall clock's per-file allowance, which only falls.
 *
 * The compiler's check, the banned effects, is the build's: `src/BannedEffects.txt` through `src/Layers.props`.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, repositoryFiles, writeAtomic } from './fsx.mjs';
import { checkAmbient, scanAmbient } from './layers-census/ambient.mjs';
import { checkClock, scanClock } from './layers-census/clock.mjs';
import { BUDGET_MS, checkBudget, parseTrx, readRecords, slowTestsOf } from './layers-census/budget.mjs';
import { checkBudgetAlone } from './layers-census/budget-alone.mjs';
import { measureAlone } from './time-ratchet.mjs';
import { loadWorld } from './time-reach.mjs';
import { checkGit, scanGit } from './layers-census/git.mjs';
import { checkMemory } from './layers-census/memory.mjs';
import { checkProcesses, scanProcesses } from './layers-census/processes.mjs';
import { checkReferences, packagesOutside } from './layers-census/references.mjs';

export {
  checkAmbient, checkBudget, checkClock, checkGit, checkMemory, checkProcesses, checkReferences, parseTrx, scanAmbient, scanClock, scanGit,
  scanProcesses, BUDGET_MS,
};

const EMPTY = { packages: {}, ambient: {}, clock: {}, processes: {}, git: {}, slowTests: {} };

export function countProblems(tree, list) {
  return [
    ...checkReferences(tree, list), ...checkAmbient(scanAmbient(tree), list.ambient), ...checkClock(scanClock(tree), list.clock),
    ...checkProcesses(scanProcesses(tree), list.processes), ...checkGit(scanGit(tree), list.git), ...checkMemory(tree),
  ];
}

export async function census({ root, argv = [], tree = null }) {
  const listFile = join(root, 'tools', 'layers-census.json');
  const recordsAt = argv.includes('--records') ? argv[argv.indexOf('--records') + 1] : join(root, '_fixtures', 'test-timing');
  try {
    tree ??= { files: repositoryFiles(root), read: (path) => readFileSync(join(root, path), 'utf8') };
    if (argv.includes('--counts')) {
      const problems = countProblems(tree, { ...EMPTY, ...JSON.parse(readFileSync(listFile, 'utf8')) });
      for (const problem of problems) console.error(`layers-census: ${problem}`);
      if (problems.length === 0) console.log('layers-census: the reference graph, the ambient ratchet and the clock\'s, process starts\' and git\'s allowances hold; the unit budget is the full census\'s');
      return problems.length === 0 ? 0 : 1;
    }
    const records = readRecords(recordsAt);
    if (argv.includes('--write')) {
      const list = {
        packages: packagesOutside(tree), ambient: scanAmbient(tree), clock: scanClock(tree), processes: scanProcesses(tree), git: scanGit(tree),
        slowTests: slowTestsOf(records),
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
    if (records.length === 0) console.log(`layers-census: no duration records under ${recordsAt.slice(root.length + 1).replace(/\\/g, '/')}, so the unit budget read nothing`);
    if (problems.length === 0) console.log('layers-census: the reference graph, the ambient ratchet, the process starts\' and git\'s allowances and the unit budget hold');
    return problems.length === 0 ? 0 : 1;
  } catch (error) {
    console.error(`layers-census: ${error.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) census({ root: dirname(dirname(fileURLToPath(import.meta.url))), argv: process.argv.slice(2) }).then((code) => process.exit(code));

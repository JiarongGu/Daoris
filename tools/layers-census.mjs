/**
 * The layers' census: what holds the application apart from its infrastructure by construction (the ports-and-adapters design,
 * `docs/2026-11-13-layers-design.md` §6), the checks that cost no test time.
 *   node tools/layers-census.mjs            check the tree (exit 0 clean, 1 a breach, 2 tool error)
 *   node tools/layers-census.mjs --write    write tools/layers-census.json from the tree, the first list (it also fills `slowTests` from this machine's last run)
 *   node tools/layers-census.mjs --shrink   lower the allowances the tree fell under, each said, and write nothing else (the merge's and the commit hook's writer)
 *   node tools/layers-census.mjs --boundary <lane>   a lane's references across its boundary today, held to the list where it names the
 *                                                     lane; with --write, the lane joins the list
 * 1. The reference graph: read from the project files, so it holds before any adapter exists.
 * 2. The ambient ports' reads per subdomain, a ratchet that only tightens; beside it the process and git sites' allowances, which only fall.
 * 3. The unit budget: a unit test over a second fails, read from each non-Process .NET gate's trx under `_fixtures/test-timing/`
 *    (`--records <dir>` reads another folder; none, nothing read), each test over it said with its cause; beside it the clock's per-file allowance, which only falls.
 * 4. A package's boundary: for each lane the list's `boundaries` names, the references across it both ways per type, a ratchet that
 *    only falls while the package's code moves behind its ports.
 * 1, 2 and 4 alone are `tools/layers-census/counts.mjs`; the banned effects are the build's (`src/BannedEffects.txt`, `src/Layers.props`).
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';
import { checkAmbient, scanAmbient } from './layers-census/ambient.mjs';
import { checkClock, scanClock } from './layers-census/clock.mjs';
import { BUDGET_MS, checkBudget, parseTrx, readRecords, slowTestsOf } from './layers-census/budget.mjs';
import { budgetBreaches, checkBudgetAlone } from './layers-census/budget-alone.mjs';
import { EMPTY, countProblems, treeAt } from './layers-census/counts.mjs';
import { measureAlone } from './time-ratchet.mjs';
import { sayBreach, sourceReader, readLedger } from './time-why.mjs';
import { loadWorld } from './time-reach.mjs';
import { checkGit, scanGit } from './layers-census/git.mjs';
import { checkMemory } from './layers-census/memory.mjs';
import { shrinkFile } from './layers-census/shrink.mjs';
import { checkProcesses, scanProcesses } from './layers-census/processes.mjs';
import { checkReferences, packagesOutside } from './layers-census/references.mjs';
import { checkBoundaries, sayBoundary, scanBoundary } from './layers-census/boundary.mjs';

export {
  checkAmbient, checkBudget, checkClock, checkGit, checkMemory, checkProcesses, checkReferences, countProblems, parseTrx, scanAmbient, scanClock,
  scanGit, scanProcesses, BUDGET_MS,
};

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
    tree ??= treeAt(root);
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
    if (argv.includes('--shrink')) {
      const lowered = shrinkFile(listFile, tree, EMPTY);
      if (lowered.length === 0) {
        console.log('layers-census: no allowance fell, so the list is as it was');
        return 0;
      }
      console.log(`layers-census: lowered ${lowered.length} allowance${lowered.length === 1 ? '' : 's'} of ${listFile.slice(root.length + 1).replace(/\\/g, '/')}`);
      for (const line of lowered) console.log(`  ${line}`);
      return 0;
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
    for (const breach of budgetBreaches(judged, list, sourceReader(root, tree), readLedger(recordsAt))) console.log(sayBreach(breach));
    if (records.length === 0) console.log(`layers-census: no duration records under ${recordsAt.slice(root.length + 1).replace(/\\/g, '/')}, so the unit budget read nothing`);
    if (problems.length === 0) console.log('layers-census: the reference graph, the ambient ratchet, the process starts\' and git\'s allowances, the lanes\' boundaries and the unit budget hold');
    return problems.length === 0 ? 0 : 1;
  } catch (error) {
    console.error(`layers-census: ${error.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) census({ root: dirname(dirname(fileURLToPath(import.meta.url))), argv: process.argv.slice(2) }).then((code) => process.exit(code));

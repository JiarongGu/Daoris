import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join, posix } from 'node:path';
import { REACH } from './reach-table.ts';
import type { Rule } from './reach-table.ts';
import { selectGates } from './reach.ts';
import { folderSet } from './folder-set.ts';
import { pageReads } from './page-reads.ts';
import { srcReads, treeReads, sourceRootReads, localReads, namedReads, isReal } from './source-reads.ts';
import { KEPT_NAMES, PAGE_PARSED, declared, inPlanOrder, isPageParsedRule, reaches, repoLanes, repoPlan, repoRoot, runs, trackedFiles } from './repo-gates.ts';

// The census of test units reads this file's strings as the lane table's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them

const [driver, modules, service, devkit, driverProcess, gitHalf, modulesProcess, webUnit] = declared(
  'driver', 'modules', 'service', 'devkit', 'driver-process', 'driver-process-git', 'modules-process', 'web-unit',
);

/**
 * Two C# tests read the web's catalogues, and a batch called "web only"
 * skipped them. Every repository path a .NET test reads must be one whose change reaches that test's
 * suite, which a merge then runs, and its Process half when the reading class is a Process class,
 * which the full set runs. A read is a path under `src/Daoris.…` (`srcReads`), or under any other
 * top-level folder (`treeReads`): the service's twin reads the digest's note table under `tools/`,
 * and a scan seeing `src/` alone never asks for the row that sends its change there. Or it
 * is a path under a source root (`sourceRootReads`): the driver's console and occasion scans read
 * the modules' and the app's sources from `SourceRoot()`, which neither pattern sees. Or it is read through
 * a local holding a repository folder (`localReads`).
 *
 * Each read must also be a file or folder this repository tracks, and one a rule of the lane table
 * places. A read no rule places reaches every gate, so the checks after it pass while holding nothing:
 * three modules tests read the whole desktop tree, `src/Daoris.Desktop`, through a local, and a driver test's
 * scratch workspace in this repository's layout read as `src/Daoris.Desktop/app/bin`.
 */
test('every repository file a .NET suite reads is one whose change reaches that suite', () => {
  // A suite's fast gate, its Process half, and the half its Parallel=Git classes run in, where it has one.
  const suites: [string, string, string | null, string | null][] = [
    ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/', driver, driverProcess, gitHalf],
    ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/', modules, modulesProcess, null],
    ['src/Daoris.Service/Daoris.Service.Tests/', service, null, null],
    ['src/Daoris.Service/Daoris.Service.Http.Tests/', service, null, null],
    ['src/Daoris.Devkit/Daoris.Devkit.Tests/', devkit, null, null],
  ];
  const files = trackedFiles();
  const folders = folderSet(files);
  let reads = 0;
  let outsideSrc = 0;
  let rooted = 0;
  let throughLocals = 0;
  let byName = 0;
  const problems: string[] = [];
  for (const file of files.filter((path) => path.endsWith('.cs'))) {
    const suite = suites.find(([folder]) => file.startsWith(folder));
    if (!suite) continue;
    // A suite's `SourceFiles` is how its tests read a source by name: each read is counted where it is called
    // (namedReads), so the helper's own walk of a project's folder is no read of its own.
    if (posix.basename(file) === 'SourceFiles.cs') continue;
    const text = readFileSync(join(repoRoot, file), 'utf8');
    // A class's traits are its code's, not its strings': CategoryTests holds both traits in its fixtures' raw strings.
    const code = text.replace(/"""[\s\S]*?"""/g, '""').replace(/"(?:[^"\\\r\n]|\\.)*"/g, '""');
    const isProcess = /\[Trait\(Category\.Name, Category\.Process\)\]/.test(code);
    const half = /\[Trait\(Category\.Parallel, Category\.Git\)\]/.test(code) ? suite[3] ?? suite[2] : suite[2];
    const found = srcReads(text, files);
    const outside = treeReads(text, files);
    const fromRoot = sourceRootReads(file, text, files);
    rooted += fromRoot.length;
    const fromLocals = localReads(text, files);
    throughLocals += fromLocals.length;
    const named = namedReads(text, files);
    byName += named.length;
    outsideSrc += [...outside, ...fromLocals].filter((read) => !read.startsWith('src/')).length;
    for (const read of [...found, ...outside, ...fromRoot, ...fromLocals, ...named]) {
      reads += 1;
      // A read no rule places reaches every gate, so the two checks below pass while holding nothing.
      if (!isReal(read, files, folders)) problems.push(`${file} reads ${read}, which is no file or folder this repository tracks`);
      else if (selectGates(repoPlan, [read], { lanes: repoLanes }).unplaced.length) problems.push(`${file} reads ${read}, which no rule of the lane table places`);
      else if (!runs([read]).includes(suite[1])) problems.push(`${file} reads ${read}, and a merge changing it does not run ${suite[1]}`);
      else if (isProcess && half && !reaches([read]).includes(half)) problems.push(`${file} (a Process class) reads ${read}, and a change there does not reach ${half}`);
    }
  }
  assert.deepEqual(problems, [], 'each a read the lane table does not hold');
  assert.ok(reads >= 10, `the scan found only ${reads} reads: its pattern no longer matches how the tests read the repository`);
  assert.ok(outsideSrc >= 3, `the scan found only ${outsideSrc} reads outside src/: its pattern no longer matches how the tests read the repository`);
  assert.ok(rooted >= 10, `the scan found only ${rooted} reads from a source root: its pattern no longer matches how the tests read their sources`);
  assert.ok(throughLocals >= 10, `the scan found only ${throughLocals} reads through a local: its pattern no longer matches how the tests read through one`);
  assert.ok(byName >= 8, `the scan found only ${byName} reads by name: its pattern no longer matches how the tests find a source`);
});

/**
 * Reads outside the page by the page's twins that no rule sends to the web gate, each named exactly with its reason, so a new
 * read is placed on purpose or owed here, and a row that places one fails this test until the row drops it. None is owed: the
 * six driver sources the scan finds are placed by the page-parsed row (`PAGE_PARSED`).
 */
const OWED_PAGE_READS: string[] = [];

/**
 * The page's vitests run in the web gate alone, so a file outside the page a
 * vitest reads (a twin's table, a writer's declarations) is one whose change must reach the web gate, or a merge changing it
 * passes and the full set fails. The kept names' table sits among the driver's tests, whose rule reaches the driver's
 * suites alone.
 */
test("every file outside the page a page's vitest reads is one whose change reaches the web gate", () => {
  const files = trackedFiles();
  const reads = new Set<string>();
  for (const file of files.filter((path) => /^src\/Daoris\.Web\/src\/.+\.test\.tsx?$/.test(path))) {
    for (const read of pageReads(file, readFileSync(join(repoRoot, file), 'utf8'), files)) reads.add(read);
  }
  // The vitests run in the web gate's unit run.
  const unreached = [...reads].filter((read) => !runs([read]).includes(webUnit)).sort();
  assert.deepEqual(unreached, OWED_PAGE_READS, "each a file a vitest reads, and a merge changing it does not run the web gate's unit run");
  assert.ok(reads.has(KEPT_NAMES), "the scan does not see the page's twin read the kept names' table");
  for (const path of PAGE_PARSED) assert.ok(reads.has(path), `the scan does not see a page twin parse ${path}`);
  assert.ok(reads.size >= 8, `the scan found only ${reads.size} reads: its pattern no longer matches how the page's tests read the repository`);

  // What the scan asks of the lane table, with the row taken away: the six reach the driver's gates and no vitest.
  const without = REACH.filter((rule) => !isPageParsedRule(rule));
  assert.equal(without.length, REACH.length - 1, "the rule names each source by its name under the driver's folder");
  for (const path of PAGE_PARSED) {
    const reached = (reach: readonly Rule[]) => selectGates(repoPlan, [path], { lanes: repoLanes, reach, gated: true }).gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
    assert.ok(!reached(without).includes(webUnit), `${path} without the row reaches no vitest`);
    assert.deepEqual(reached(REACH), inPlanOrder(reached(without), [webUnit]), path);
  }
});

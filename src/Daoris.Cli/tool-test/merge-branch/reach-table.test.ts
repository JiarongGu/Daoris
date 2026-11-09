import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { join, posix } from 'node:path';
import {
  ALL, tool, repoLanes, repoRoot, reaches, trackedFiles, repoPlan, runs, KEPT_NAMES, PAGE_PARSED, isPageParsedRule, type Rule,
} from '../support/merge-branch.ts';
import { folderSet } from '../support/folder-set.ts';
import { srcReads, treeReads, sourceRootReads, localReads, namedReads, isReal } from '../support/reads.ts';

// The census of test units reads this file's strings as the lane table's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them

test('every gate the lane table names is one this repository runs, and every lane of the map has its rule', () => {
  const names = new Set(ALL);
  for (const gate of tool.BASELINE) assert.ok(names.has(gate), `baseline: '${gate}' is not in the plan`);
  for (const rule of tool.REACH) {
    assert.ok(rule.why.trim(), `a rule says why: ${JSON.stringify(rule)}`);
    assert.ok((rule.paths?.length ?? 0) > 0 !== (rule.lane !== undefined), `a rule names paths or a lane, not both: ${JSON.stringify(rule)}`);
    if (rule.gates !== '*') for (const gate of rule.gates) assert.ok(names.has(gate), `'${gate}' (${rule.lane ?? rule.paths!.join(', ')}) is not in the plan`);
  }
  const ruled = new Set(tool.REACH.map((rule) => rule.lane).filter(Boolean));
  for (const lane of repoLanes) assert.ok(ruled.has(lane.id), `the lane '${lane.id}' has no rule, so every path in it would run everything`);
  for (const id of ruled) assert.ok(repoLanes.some((lane) => lane.id === id), `a rule names the lane '${id}', which the map does not declare`);
  // A path no rule places is not refused here: it runs every gate, and the merge names it. A branch in
  // another lane may add one, and the table is the tools lane's to extend.
});

/**
 * What a rehearsal runs is code: its own script, everything that imports, and the tools it starts. A
 * change to any of those must reach it, or the table hides the rehearsal's own failure (and, for the
 * deployment rehearsal, which only the full set runs, says the wrong thing about it). The
 * spawned scripts are named here by hand (a spawn is not an import); the imports are followed.
 */
test('every tool a rehearsal imports or starts is one whose change reaches that rehearsal', () => {
  const closure = (entry: string): string[] => {
    const seen = new Set<string>();
    const stack = [entry];
    while (stack.length) {
      const file = stack.pop()!;
      if (seen.has(file)) continue;
      seen.add(file);
      for (const match of readFileSync(join(repoRoot, file), 'utf8').matchAll(/(?:from\s+|import\(\s*)'(\.{1,2}\/[^']+)'/g)) {
        const target = posix.normalize(posix.join(posix.dirname(file), match[1]!));
        if (existsSync(join(repoRoot, target))) stack.push(target);
      }
    }
    return [...seen];
  };
  const entries: [string, string][] = [
    ['tools/release-rehearsal.mjs', 'rehearse'], ['tools/stage-package.mjs', 'rehearse'],
    ['tools/family-rehearsal.mjs', 'rehearse-family'], ['tools/usage-report.mjs', 'rehearse-family'],
    ['tools/deployment-rehearsal.mjs', 'deployment'], ['tools/desktop-publish.mjs', 'deployment'],
    ['src/Daoris.Web/scripts/e2e-host.mjs', 'web'], ['tools/fsx.mjs', 'web'],
  ];
  for (const [entry, gate] of entries) {
    for (const file of closure(entry)) assert.ok(reaches([file]).includes(gate), `${file} (from ${entry}) does not reach ${gate}`);
  }
});

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
test("every repository file a .NET suite reads is one whose change reaches that suite (MOD9's incident)", () => {
  // A suite's fast gate, its Process half, and the half its Parallel=Git classes run in, where it has one.
  const suites: [string, string, string | null, string | null][] = [
    ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/', 'driver', 'driver-process', 'driver-process-git'],
    ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/', 'modules', 'modules-process', null],
    ['src/Daoris.Service/Daoris.Service.Tests/', 'service', null, null],
    ['src/Daoris.Service/Daoris.Service.Http.Tests/', 'service', null, null],
    ['src/Daoris.Devkit/Daoris.Devkit.Tests/', 'devkit', null, null],
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
      else if (tool.selectGates(repoPlan, [read], { lanes: repoLanes }).unplaced.length) problems.push(`${file} reads ${read}, which no rule of the lane table places`);
      else if (!runs([read]).includes(suite[1])) problems.push(`${file} reads ${read}, and a merge changing it does not run ${suite[1]}`);
      else if (isProcess && half && !reaches([read]).includes(half)) problems.push(`${file} (a Process class) reads ${read}, and a change there does not reach ${half}`);
    }
  }
  assert.deepEqual(problems, [], 'each a read the lane table does not hold');
  assert.ok(reads >= 10, `the scan found only ${reads} reads: its pattern no longer matches how the tests read the repository`);
  assert.ok(outsideSrc >= 3, `the scan found only ${outsideSrc} reads outside src/: its pattern no longer matches how the tests read the repository`);
  assert.ok(rooted >= 10, `the scan found only ${rooted} reads from a source root: its pattern no longer matches how the tests read their sources`);
  assert.ok(throughLocals >= 10, `the scan found only ${throughLocals} reads through a local: its pattern no longer matches how the tests read through one`);
  assert.ok(byName >= 8, `the scan found only ${byName} reads by name: its pattern no longer matches how the tests find a source (STRUCT1)`);
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
test("every file outside the page a page's vitest reads is one whose change reaches the web gate (SESSDEL1c)", () => {
  const files = trackedFiles();
  const reads = new Set<string>();
  for (const file of files.filter((path) => /^src\/Daoris\.Web\/src\/.+\.test\.tsx?$/.test(path))) {
    for (const read of pageReads(file, readFileSync(join(repoRoot, file), 'utf8'), files)) reads.add(read);
  }
  // The vitests run in the web gate's unit run.
  const unreached = [...reads].filter((read) => !runs([read]).includes('web-unit')).sort();
  assert.deepEqual(unreached, OWED_PAGE_READS, "each a file a vitest reads, and a merge changing it does not run the web gate's unit run");
  assert.ok(reads.has(KEPT_NAMES), "the scan does not see the page's twin read the kept names' table");
  for (const path of PAGE_PARSED) assert.ok(reads.has(path), `the scan does not see a page twin parse ${path}`);
  assert.ok(reads.size >= 8, `the scan found only ${reads.size} reads: its pattern no longer matches how the page's tests read the repository`);

  // What the scan asks of the lane table, with the row taken away: the six reach the driver's gates and no vitest.
  const without = tool.REACH.filter((rule) => !isPageParsedRule(rule));
  assert.equal(without.length, tool.REACH.length - 1, "MOD9e's rule names each source by its name under the driver's folder");
  for (const path of PAGE_PARSED) {
    const reached = (reach: readonly Rule[]) => tool.selectGates(repoPlan, [path], { lanes: repoLanes, reach, gated: true }).gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
    assert.deepEqual(reached(without), ['universal', 'code-map', 'test-map', 'cli', 'comments', 'driver', 'modules', 'censuses', 'rehearse-family'], path);
    assert.deepEqual(reached(tool.REACH), ['universal', 'code-map', 'test-map', 'cli', 'comments', 'driver', 'modules', 'censuses', 'rehearse-family', 'web-unit'], path);
  }
});

test("the page's scan reads through node:fs from the page, the repository or the test, and never a path a test only says (SESSDEL1c)", () => {
  const files = [
    'src/Daoris.Cli/test/fixtures/shell-words.json', KEPT_NAMES,
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Landing.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Trace.Chain.cs',
    'src/Daoris.Service/Daoris.Service.Core/NoteParts.cs', 'src/Daoris.Web/src/tokens.css', 'src/Daoris.Web/src/format.ts',
    'docs/code-map.json',
  ];
  const at = 'src/Daoris.Web/src/work/twin.test.ts';
  const reading = [
    "import { readFileSync } from 'node:fs';",
    "import { list } from '../format';",
    // From where vitest runs, the page; from the repository; one literal of a run that names a file; a helper's files by name.
    "const table = join(process.cwd(), '..', 'Daoris.Cli', 'test', 'fixtures', 'shell-words.json');",
    "const root = join(process.cwd(), '..', '..');",
    "const kept = readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'kept-names.json'), 'utf8');",
    "const ledger = declared('service', 'src/Daoris.Service/Daoris.Service.Core/NoteParts.cs');",
    "const driver = (file: string) => readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', file), 'utf8');",
    "const chain = driver('Trace.Chain.cs') + driver('Landing.cs');",
    // The page's own files are the web gate's already.
    "const css = readFileSync(join(process.cwd(), 'src', 'tokens.css'), 'utf8');",
  ].join('\n');
  assert.deepEqual(pageReads(at, reading, files), [
    'src/Daoris.Cli/test/fixtures/shell-words.json', KEPT_NAMES, 'src/Daoris.Service/Daoris.Service.Core/NoteParts.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Trace.Chain.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Landing.cs',
  ]);
  // A folder it names no file of is read for any file in it.
  assert.deepEqual(pageReads(at, "import { readdirSync } from 'node:fs';\nreaddirSync(join(root, 'src', 'Daoris.Cli', 'test', 'fixtures'));", files),
    ['src/Daoris.Cli/test/fixtures/probe.txt']);
  // A test that does not read the disk only says a path, as a map's data; an import out of the page is a read whatever it imports.
  assert.deepEqual(pageReads(at, "import { render } from '@testing-library/react';\nexpect(map.source).toBe('docs/code-map.json');", files), []);
  assert.deepEqual(pageReads(at, "import table from '../../../Daoris.Cli/test/fixtures/shell-words.json';", files),
    ['src/Daoris.Cli/test/fixtures/shell-words.json']);
  // A twin that reads the driver by name reads each source it names, wherever the driver keeps it, and the helper
  // it imports from the page is the page's own.
  const moved = ['src/Daoris.Desktop/Daoris.Desktop.Driver/Sessions/Trace.Chain.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Landing.cs'];
  const byName = [
    "import { driverSource } from '../test/driverSource';",
    "const chain = driverSource('Trace.Chain.cs');",
    "const codes = codesOf('Landing.cs', 'SweepKind');",
    "expect(kind).toBe('closed');",
  ].join('\n');
  assert.deepEqual(pageReads(at, byName, moved), moved);

  // What the scan above then asks of the lane table: without the kept names' row, the kept names' table reaches no web gate.
  const without = tool.REACH.filter((rule) => !rule.paths?.includes(KEPT_NAMES));
  const reached = (reach: readonly Rule[]) => tool.selectGates(repoPlan, [KEPT_NAMES], { lanes: repoLanes, reach, gated: true })
    .gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
  assert.equal(reached(without).includes('web-unit'), false);
  assert.equal(reached(tool.REACH).includes('web-unit'), true);
});

/**
 * The paths outside the page a page's vitest reads. A read from disk goes through `node:fs`, so a test that does not
 * import it reads nothing from disk: a path it says (`docs/code-map.json`, a map's data) is no read. In one that does, a read
 * is a run of string literals, joined, naming a tracked file or folder from where vitest runs, the page
 * (`join(process.cwd(), '..', 'Daoris.Cli', …)`), from the repository (`join(root, 'src', …)`) or from the test's own folder;
 * or, where the run names nothing, one literal of it that does (`declared('service', 'src/…/NoteParts.cs')`). A folder is read
 * for each file in it the test names (`driver('Trace.Chain.cs')`), else for any file in it. A relative import naming a file
 * outside the page is a read in any test. The page's own files are no read: the web gate runs with them.
 */
function pageReads(file: string, text: string, files: readonly string[]): string[] {
  const page = 'src/Daoris.Web';
  const tracked = new Set(files);
  const folders = new Set(files.flatMap((path) => path.split('/').slice(0, -1).map((_, i, parts) => parts.slice(0, i + 1).join('/'))));
  const outside = (path: string) => path.includes('/') && !path.startsWith('..') && path !== page && !path.startsWith(`${page}/`);
  const reads = new Set<string>();
  for (const match of text.matchAll(/(?:\bfrom\s+|\bimport\s*\(\s*)'(\.{1,2}\/[^'?]+)'/g)) {
    const path = posix.normalize(posix.join(posix.dirname(file), match[1]!));
    const found = ['', '.ts', '.tsx', '.mjs', '.js', '.json'].map((extension) => `${path}${extension}`).find((candidate) => tracked.has(candidate));
    if (found && outside(found)) reads.add(found);
  }
  // A twin that reads the driver by name (`driverSource('Trace.Chain.cs')`, or a name it hands on to it) reads
  // each driver source it names, the one tracked file of that name at any depth of the driver's folder.
  if (/\bdriverSource\b/.test(text)) {
    const driver = 'src/Daoris.Desktop/Daoris.Desktop.Driver/';
    for (const match of text.matchAll(/'([^'\\\r\n/]+)'/g)) {
      const named = files.filter((path) => path.startsWith(driver) && posix.basename(path) === match[1]);
      if (named.length === 1) reads.add(named[0]!);
    }
  }
  if (!/\bfrom\s+'(?:node:)?fs(?:\/promises)?'/.test(text)) return [...reads];
  const resolve = (path: string) => [page, '.', posix.dirname(file)]
    .map((base) => posix.normalize(posix.join(base, path)))
    .find((candidate) => outside(candidate) && (tracked.has(candidate) || folders.has(candidate)));
  const literals = [...text.matchAll(/'([^'\\\r\n]*)'/g)].map((match) => match[1]!);
  for (const match of text.matchAll(/'([^'\\\r\n]*)'((?:\s*,\s*'[^'\\\r\n]*')*)/g)) {
    const run = [match[1]!, ...[...match[2]!.matchAll(/'([^']*)'/g)].map((part) => part[1]!)];
    const whole = resolve(run.join('/'));
    const found = whole ? [whole] : run.filter((one) => one.includes('/')).map(resolve).filter((path): path is string => path !== undefined);
    for (const path of found) {
      if (tracked.has(path)) {
        reads.add(path);
        continue;
      }
      const named = literals.filter((name) => name && !name.includes('/') && tracked.has(`${path}/${name}`));
      for (const name of named.length > 0 ? named : ['probe.txt']) reads.add(`${path}/${name}`);
    }
  }
  return [...reads];
}

test("the module that builds the lane table's rows reaches the tool-tests that hold the table", () => {
  assert.ok(reaches(['tools/reach-rows.mjs']).includes('tool-tests'));
});

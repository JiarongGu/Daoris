import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { join, posix } from 'node:path';
import { ALL, repoLanes, repoRoot, reaches, repoPlan, runs, KEPT_NAMES, declared, inPlanOrder } from './repo-gates.ts';
import { BASELINE, selectGates } from './reach.ts';
import { REACH } from './reach-table.ts';
import type { Rule } from './reach-table.ts';
import { pageReads } from './page-reads.ts';

// The census of test units reads this file's strings as the lane table's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them

const [service, rehearse, family, deployment, web, webUnit, toolTests] = declared(
  'service', 'rehearse', 'rehearse-family', 'deployment', 'web', 'web-unit', 'tool-tests',
);

test('every gate the lane table names is one this repository runs, and every lane of the map has its rule', () => {
  const names = new Set(ALL);
  for (const gate of BASELINE) assert.ok(names.has(gate), `baseline: '${gate}' is not in the plan`);
  for (const rule of REACH) {
    assert.ok(rule.why.trim(), `a rule says why: ${JSON.stringify(rule)}`);
    assert.ok((rule.paths?.length ?? 0) > 0 !== (rule.lane !== undefined), `a rule names paths or a lane, not both: ${JSON.stringify(rule)}`);
    if (rule.gates !== '*') for (const gate of rule.gates) assert.ok(names.has(gate), `'${gate}' (${rule.lane ?? rule.paths!.join(', ')}) is not in the plan`);
  }
  const ruled = new Set(REACH.map((rule) => rule.lane).filter(Boolean));
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
    ['tools/release-rehearsal.mjs', rehearse], ['tools/stage-package.mjs', rehearse],
    ['tools/family-rehearsal.mjs', family], ['tools/usage-report.mjs', family],
    ['tools/deployment-rehearsal.mjs', deployment], ['tools/desktop-publish.mjs', deployment],
    ['src/Daoris.Web/scripts/e2e-host.mjs', web], ['tools/fsx.mjs', web],
  ];
  for (const [entry, gate] of entries) {
    for (const file of closure(entry)) assert.ok(reaches([file]).includes(gate), `${file} (from ${entry}) does not reach ${gate}`);
  }
});

test("the page's scan reads through node:fs from the page, the repository or the test, and never a path a test only says", () => {
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
  const without = REACH.filter((rule) => !rule.paths?.includes(KEPT_NAMES));
  const reached = (reach: readonly Rule[]) => selectGates(repoPlan, [KEPT_NAMES], { lanes: repoLanes, reach, gated: true })
    .gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
  assert.equal(reached(without).includes(webUnit), false);
  assert.equal(reached(REACH).includes(webUnit), true);
});

test("the module that builds the lane table's rows reaches the tool-tests that hold the table", () => {
  assert.ok(reaches(['tools/merge/reach-rows.ts']).includes(toolTests));
});

test('a file of canon/ with a narrow rule of its own reaches every gate the whole folder does', () => {
  const folder = runs(['canon/README.md']);
  for (const gate of [service, rehearse, family]) assert.ok(folder.includes(gate), `canon/** runs ${gate}`);
  const version = runs(['canon/canon.json']);
  assert.deepEqual(folder.filter((gate) => !version.includes(gate)), [], 'canon/canon.json runs less than canon/**');
  assert.deepEqual(inPlanOrder(folder), folder);
});

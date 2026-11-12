import { test } from 'node:test';
import assert from 'node:assert/strict';
import { repoRoot, tool, type Verdict, type Rule, type Lane, repoPlan, repoLanes, ALL, BASE, ALWAYS_REACHED } from './support/merge-branch.ts';
import { hourOf, fixJudging } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


// ---------------------------------------------------------------------------------------------------
// A fixed gate re-runs alone, and the rest's verdicts are kept, saying from when

test('a re-run runs the named gates and any the merge has not run yet, and keeps the rest\'s verdicts with their time', () => {
  const plan = ['universal', 'cli', 'driver', 'driver-process', 'web', 'deployment'];
  const chosen = ['universal', 'cli', 'driver', 'driver-process', 'web'];
  const results = {
    universal: { verdict: 'PASS', at: '2026-10-04T10:00:00Z' },
    cli: { verdict: 'PASS', at: '2026-10-04T10:03:00Z' },
    driver: { verdict: 'FAIL', at: '2026-10-04T10:09:00Z' },
  };
  // The failing driver suite, fixed: it runs again, and so do the gates its failure stopped, which have no verdict.
  assert.deepEqual(tool.rerunPlan(plan, chosen, results, ['driver']), {
    run: ['driver', 'driver-process', 'web'],
    kept: [{ name: 'universal', verdict: 'PASS', at: '2026-10-04T10:00:00Z' }, { name: 'cli', verdict: 'PASS', at: '2026-10-04T10:03:00Z' }],
    fresh: ['driver-process', 'web'],
    stale: [],
  });
  // A gate the lanes did not reach runs when named, in the plan's order; a kept FAIL stays a FAIL.
  const named = tool.rerunPlan(plan, chosen, { ...results, 'driver-process': { verdict: 'PASS', at: 'x' }, web: { verdict: 'PASS', at: 'y' } }, ['deployment', 'cli']);
  assert.deepEqual(named.run, ['cli', 'deployment']);
  assert.deepEqual(named.kept.map((kept) => `${kept.name} ${kept.verdict}`), ['universal PASS', 'driver FAIL', 'driver-process PASS', 'web PASS']);
  assert.deepEqual(named.fresh, []);

  // A verdict a fixed path reaches is not kept: that gate runs again, saying which path, and a named one is just named.
  const passed = { ...results, driver: { verdict: 'PASS', at: '2026-10-04T10:09:00Z' }, 'driver-process': { verdict: 'PASS', at: 'x' }, web: { verdict: 'FAIL', at: 'y' } };
  const fixed = tool.rerunPlan(plan, chosen, passed, ['web'], { universal: ['src/Daoris.Web/e2e/platform.spec.ts'], web: ['src/Daoris.Web/e2e/platform.spec.ts'] });
  assert.deepEqual(fixed.run, ['universal', 'web']);
  assert.deepEqual(fixed.kept.map((kept) => kept.name), ['cli', 'driver', 'driver-process']);
  assert.deepEqual(fixed.stale, [{ name: 'universal', verdict: 'PASS', at: '2026-10-04T10:00:00Z', paths: ['src/Daoris.Web/e2e/platform.spec.ts'] }]);
  assert.deepEqual(fixed.fresh, []);
});


// ---------------------------------------------------------------------------------------------------
// The record a stage reads: which gates passed which tree

test('the stage counts a gate passed on this tree, or on one that differs from it only by the records; the newest verdict there decides', () => {
  const at = (hour: number) => `2026-10-04T${String(hour).padStart(2, '0')}:00:00Z`;
  const verdicts: Verdict[] = [
    { gate: 'universal', verdict: 'PASS', tree: 'T', at: at(9) },
    // Passed before the parent wrote the records and committed: the trees differ by the records alone.
    { gate: 'cli', verdict: 'PASS', tree: 'R', at: at(9) },
    // Passed before a fix in code: that verdict is not this tree's.
    { gate: 'driver', verdict: 'PASS', tree: 'C', at: at(9) },
    // Passed, then failed on the same tree: the newest says.
    { gate: 'web', verdict: 'PASS', tree: 'T', at: at(9) },
    { gate: 'web', verdict: 'FAIL', tree: 'T', at: at(11) },
    // A flake is a pass that is said.
    { gate: 'driver-process', verdict: 'FLAKE', tree: 'T', at: at(10) },
    { gate: 'deployment', verdict: 'PASS', tree: null, at: at(10) },
  ];
  // Every path reaches every gate here: what a stage forgives without the lane table.
  const judging = {
    tree: 'T', drift: (from: string) => ({ R: ['TASKS.md'], C: ['src/x.cs', 'TASKS.md'] } as Record<string, string[]>)[from] ?? null,
    isRecord: (path: string) => path === 'TASKS.md', reaching: (_gate: string, paths: string[]) => paths,
  };
  const judged = tool.stageJudgement({ ...judging, plan: ['universal', 'cli', 'driver', 'driver-process', 'web', 'deployment', 'rehearse'], verdicts });
  assert.equal(judged.passed, false);
  assert.deepEqual(judged.missing, ['driver', 'web', 'deployment', 'rehearse']);
  const byName = Object.fromEntries(judged.gates.map((gate) => [gate.name, gate]));
  assert.equal(byName['universal']!.exact, true);
  assert.equal(byName['cli']!.exact, false);
  assert.equal(byName['cli']!.verdict!.tree, 'R');
  assert.deepEqual(byName['cli']!.standing, { standing: 'records', paths: [], records: ['TASKS.md'] });
  assert.equal(byName['web']!.verdict!.verdict, 'FAIL');
  assert.equal(byName['rehearse']!.verdict, null);
  assert.equal(byName['rehearse']!.stale, null, 'no verdict anywhere is not a stale one');
  // A stale verdict is said with the paths that made it so, the records left out; one with no tree, with none.
  assert.equal(byName['driver']!.verdict, null);
  assert.deepEqual([byName['driver']!.stale!.standing, byName['driver']!.stale!.paths, byName['driver']!.stale!.verdict.tree], ['stale', ['src/x.cs'], 'C']);
  assert.deepEqual([byName['deployment']!.stale!.standing, byName['deployment']!.stale!.paths], ['stale', []]);

  const all = tool.stageJudgement({ ...judging, plan: ['universal', 'cli'], verdicts });
  assert.deepEqual([all.passed, all.missing], [true, []]);

  // A newer verdict that no longer stands is said only where it explains a gate that does not count.
  const later = tool.stageJudgement({ ...judging, plan: ['service', 'devkit'], verdicts: [
    { gate: 'service', verdict: 'PASS', tree: 'T', at: at(9) },
    { gate: 'service', verdict: 'FAIL', tree: 'C', at: at(11) },
    { gate: 'devkit', verdict: 'FAIL', tree: 'T', at: at(9) },
    { gate: 'devkit', verdict: 'PASS', tree: 'C', at: at(11) },
  ] });
  assert.deepEqual(later.missing, ['devkit']);
  assert.deepEqual(later.gates.map((gate) => [gate.name, gate.verdict!.verdict, gate.stale?.verdict.verdict ?? null]), [
    ['service', 'PASS', null],
    ['devkit', 'FAIL', 'PASS'],
  ]);
});


// ---------------------------------------------------------------------------------------------------
// A verdict stays good until a path its gate reaches changes

test('SCOPEPLAN1: gate reach and selection prepare each rule a bounded number of times per read', () => {
  let directReads = 0;
  let laneReads = 0;
  const lane: Lane = {
    id: 'service', title: 'Service', summary: 'The service', steward: false,
    get paths() { laneReads += 1; return ['service/**', '!service/private/**']; },
  };
  const reach: Rule[] = [
    { get paths() { directReads += 1; return ['web/**']; }, gates: ['web'], why: 'the page' },
    { lane: 'service', gates: ['service'], why: 'the service' },
    { paths: ['policy.json'], gates: '*', why: 'every gate' },
  ];
  const paths = Array.from({ length: 40 }, (_, i) => [
    `web/${i}.ts`, `service/${i}.cs`, `service/private/${i}.cs`, `other/${i}.txt`,
  ]).flat().concat('policy.json');
  const web = paths.filter((path) => !/^service\/\d+\.cs$/.test(path));
  assert.deepEqual(tool.pathsReaching('web', paths, { lanes: [lane], reach }), web,
    'a lane exclusion remains unplaced, which conservatively reaches every gate');
  assert.ok(directReads <= 2 && laneReads <= 1, `one reach read prepared direct=${directReads}, lane=${laneReads}`);

  directReads = laneReads = 0;
  assert.deepEqual(tool.pathsReaching('web-types', paths, { lanes: [lane], reach }), ['policy.json']);
  assert.ok(directReads <= 2 && laneReads <= 1, `one path-check read prepared direct=${directReads}, lane=${laneReads}`);

  directReads = laneReads = 0;
  const selection = tool.selectGates(repoPlan, paths, { lanes: [lane], reach, gated: true });
  assert.deepEqual(selection.unplaced, paths.filter((path) => path.startsWith('service/private/') || path.startsWith('other/')));
  assert.ok(selection.gates.every((entry) => entry.reached), 'the global rule still reaches the path checks as well');
  const reads = 1 + tool.PATH_CHECKS.length;
  assert.ok(directReads <= 2 * reads && laneReads <= reads,
    `a selection prepared direct=${directReads}, lane=${laneReads}, across ${reads} readers`);
});


test('SCOPEPLAN1: later reads see changed rule and lane paths, preserving first-match precedence', () => {
  const lane: Lane = { id: 'service', title: 'Service', summary: 'The service', steward: false, paths: ['service/**'] };
  const reach: Rule[] = [
    { paths: ['web/**'], gates: ['web'], why: 'the page' },
    { lane: 'service', gates: ['service'], why: 'the service' },
    { paths: ['**'], gates: ['driver'], why: 'the remainder' },
  ];
  const paths = ['web/a.ts', 'service/b.cs', 'other/c.txt'];
  assert.deepEqual(tool.pathsReaching('web', paths, { lanes: [lane], reach }), ['web/a.ts']);
  reach[0]!.paths![0] = 'other/**';
  lane.paths[0] = 'web/**';
  assert.deepEqual(tool.pathsReaching('web', paths, { lanes: [lane], reach }), ['other/c.txt']);
  assert.deepEqual(tool.pathsReaching('service', paths, { lanes: [lane], reach }), ['web/a.ts']);
  reach.unshift({ paths: ['other/**'], gates: ['service'], why: 'a narrower rule first' });
  assert.deepEqual(tool.pathsReaching('web', paths, { lanes: [lane], reach }), []);
  assert.deepEqual(tool.pathsReaching('service', paths, { lanes: [lane], reach }), ['web/a.ts', 'other/c.txt']);
});


test('a path reaches a gate as a merge reads the table: the baseline and an unnamed gate by every path, any gate by an unplaced one', () => {
  const reach: Rule[] = [
    { paths: ['web/**'], gates: ['web'], why: 'the page' },
    { paths: ['driver/**'], gates: ['driver', 'driver-process'], why: 'the driver' },
    { paths: ['gates.json'], gates: '*', why: 'how every gate runs' },
    { lane: 'service', gates: ['service'], why: 'a lane this map does not declare places nothing' },
  ];
  const paths = ['web/a.ts', 'driver/b.cs'];
  assert.deepEqual(tool.pathsReaching('web', paths, { reach }), ['web/a.ts']);
  assert.deepEqual(tool.pathsReaching('driver-process', paths, { reach }), ['driver/b.cs']);
  assert.deepEqual(tool.pathsReaching('cli', paths, { reach }), paths, 'the baseline runs at every merge, so every path reaches it');
  assert.deepEqual(tool.pathsReaching('mystery', paths, { reach }), paths, 'a gate no rule names runs at every merge');
  assert.deepEqual(tool.pathsReaching('web', ['gates.json', 'driver/b.cs'], { reach }), ['gates.json']);
  assert.deepEqual(tool.pathsReaching('web', ['elsewhere/c.cs', 'driver/b.cs', 'service/d.cs'], { reach }), ['elsewhere/c.cs', 'service/d.cs'],
    'a path no rule places reaches every gate');
  assert.deepEqual(tool.pathsReaching('web', [], { reach }), []);
});


test("a verdict stands until a path that reaches its gate changes: a browser test keeps the driver's verdicts, a driver change does not", () => {
  const at = (hour: number) => `2026-10-04T${String(hour).padStart(2, '0')}:00:00Z`;
  const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
  const driverCs = 'src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs';
  // The full set passed tree F. W is F with a browser test fixed and the records written; D is F with the driver changed.
  const between: Record<string, string[]> = { 'F>W': [spec, 'TASKS.md', 'docs/decisions/D150.md'], 'F>D': [driverCs] };
  const judging = (tree: string) => ({
    tree,
    drift: (from: string) => between[`${from}>${tree}`] ?? null,
    isRecord: tool.recordMatcher({ lanes: repoLanes, union: tool.unionRecords(repoRoot) }),
    reaching: (gate: string, paths: string[]) => tool.pathsReaching(gate, paths, { lanes: repoLanes }),
  });
  const full: Verdict[] = ALL.map((gate) => ({ gate, verdict: 'PASS', tree: 'F', at: at(9) }));

  // The browser test reaches the web gate and the baseline; every other verdict stands, the driver's among them.
  // The gates a path reaches are read from the lane table, so a gate added to the declaration is judged the same way.
  const reachedBy = (path: string) => ALL.filter((gate) => [...BASE, ...ALWAYS_REACHED].includes(gate) || tool.pathsReaching(gate, [path], { lanes: repoLanes }).length > 0);
  const web = tool.stageJudgement({ ...judging('W'), plan: ALL, verdicts: full });
  assert.deepEqual(web.missing, reachedBy(spec));
  assert.ok(reachedBy(spec).includes('web') && !reachedBy(spec).includes('driver'), 'a browser spec reaches the web gate, not the driver');
  const w = Object.fromEntries(web.gates.map((gate) => [gate.name, gate]));
  assert.deepEqual(w['web']!.stale!.paths, [spec], 'which path made it stale, the records forgiven');
  assert.equal(w['web']!.stale!.verdict.at, at(9));
  for (const gate of ALL.filter((each) => !reachedBy(spec).includes(each))) {
    assert.equal(w[gate]!.verdict!.tree, 'F', gate);
    assert.deepEqual(w[gate]!.standing, { standing: 'unreached', paths: [spec], records: ['TASKS.md', 'docs/decisions/D150.md'] }, gate);
  }
  // Passing again on W, the stage approves it.
  const again = [...full, ...reachedBy(spec).map((gate) => ({ gate, verdict: 'PASS', tree: 'W', at: at(11) }))];
  assert.deepEqual(tool.stageJudgement({ ...judging('W'), plan: ALL, verdicts: again }).missing, []);

  // A driver change voids the driver's verdicts and every gate it reaches.
  const driver = tool.stageJudgement({ ...judging('D'), plan: ALL, verdicts: full });
  assert.deepEqual(driver.missing, reachedBy(driverCs));
  assert.ok(['driver', 'modules', 'driver-process'].every((gate) => reachedBy(driverCs).includes(gate)), 'a driver change reaches the driver\'s gates');
  const d = Object.fromEntries(driver.gates.map((gate) => [gate.name, gate]));
  assert.deepEqual(d['driver-process']!.stale!.paths, [driverCs]);
  for (const gate of ALL.filter((each) => !reachedBy(driverCs).includes(each))) assert.equal(d[gate]!.standing!.standing, 'unreached', gate);

  // A tree git can no longer compare stands for nothing, and says so with no path.
  assert.deepEqual(tool.verdictStanding({ ...judging('X'), from: 'F', gate: 'web' }), { standing: 'stale', paths: [], records: [] });
  assert.deepEqual(tool.verdictStanding({ ...judging('F'), from: 'F', gate: 'web' }), { standing: 'exact', paths: [], records: [] });
});


test("a stage forgives only known records declared by the steward or union, never gate policy", () => {
  const isRecord = tool.recordMatcher({
    lanes: repoLanes, union: tool.unionRecords(repoRoot),
  });
  for (const path of ['TASKS.md', 'docs/task-archive.md', 'CHANGELOG.md', 'docs/decisions/D115.md', 'docs/FIX-LOG.md', 'docs/README.md']) {
    assert.ok(isRecord(path), path);
  }
  for (const path of ['daoris.lanes.json', 'README.md', '.claude/knowledge/twins.md', 'docs/2026-09-30-parallel-development-design.md', 'canon/CHANGELOG.md', 'src/Daoris.Cli/src/cli.ts', 'tools/merge-branch.mjs']) {
    assert.ok(!isRecord(path), path);
  }
  assert.equal(tool.VERDICTS_FILE, 'local/gate-verdicts.json', 'gitignored, under local/');
});


test('a changed lane map cannot forgive itself or source by broadening its steward lane or union patterns', () => {
  const lanes = repoLanes.map((lane) => lane.steward ? { ...lane, paths: ['**'] } : lane);
  const paths = ['daoris.lanes.json', 'src/Daoris.Web/src/App.tsx', 'TASKS.md'];
  const judgement = tool.stageJudgement({
    plan: ALL, tree: 'changed',
    verdicts: ALL.map((gate) => ({ gate, verdict: 'PASS', tree: 'tested', at: hourOf(9) })),
    drift: () => paths,
    isRecord: tool.recordMatcher({ lanes, union: ['**'] }),
    reaching: (gate, changed) => tool.pathsReaching(gate, changed, { lanes }),
  });
  assert.deepEqual(judgement.missing, ALL, 'editing the map changes what every verdict means');
  for (const gate of judgement.gates) {
    assert.ok(gate.stale?.paths.includes('daoris.lanes.json'), gate.name);
    assert.deepEqual(gate.stale?.records, ['TASKS.md']);
  }
});


test('a refusal names the smallest command that would pass the stage: --stale, --rerun naming what failed or never ran, or --full', () => {
  const plan = ['universal', 'cli', 'web', 'deployment'];
  const judge = (verdicts: Verdict[]) => tool.stageJudgement({ ...fixJudging, plan, verdicts }).gates;
  const onT: Verdict[] = plan.map((gate) => ({ gate, verdict: 'PASS', tree: 'T', at: hourOf(9) }));
  const beforeFix = (gate: string): Verdict => ({ gate, verdict: 'PASS', tree: 'S', at: hourOf(9) });

  assert.equal(tool.stageRemedy(judge(onT)), null, 'nothing to name when the stage passes');
  // A fix committed past the full set: the gates it reaches are stale, and --stale runs exactly those.
  const fixed = [...onT.filter((v) => v.gate !== 'cli' && v.gate !== 'web'), beforeFix('cli'), beforeFix('web')];
  assert.deepEqual(tool.stageRemedy(judge(fixed)), { command: tool.STALE_COMMAND, what: 'the 2 stale gates', runs: ['cli', 'web'] });
  assert.deepEqual(tool.stageRemedy(judge([...onT.filter((v) => v.gate !== 'web'), beforeFix('web')]))!.what, 'the stale gate');
  assert.equal(tool.STALE_COMMAND, 'node tools/merge-branch.mjs --stale');
  // One gate flaked on this tree: --rerun names it alone.
  const flaked = onT.map((v) => (v.gate === 'cli' ? { ...v, verdict: 'FAIL' } : v));
  assert.deepEqual(tool.stageRemedy(judge(flaked)), { command: 'node tools/merge-branch.mjs --rerun cli', what: 'the gate it lacks', runs: ['cli'] });
  // A failure, a gate never run and a stale one: --rerun names the first two, and runs the stale one with them.
  const mixed = [...flaked.filter((v) => v.gate !== 'web' && v.gate !== 'deployment'), beforeFix('web')];
  assert.deepEqual(tool.stageRemedy(judge(mixed)), {
    command: 'node tools/merge-branch.mjs --rerun cli deployment', what: 'the 3 gates it lacks', runs: ['cli', 'web', 'deployment'],
  });
  // When that would run every gate anyway, the full set is named.
  assert.deepEqual(tool.stageRemedy(judge([])), { command: tool.FULL_COMMAND, what: 'every gate', runs: plan });
  assert.deepEqual(tool.stageRemedy(judge(onT.map((v) => ({ ...v, verdict: 'FAIL' }))))!.command, tool.FULL_COMMAND);
});


test("a re-run on the checkout runs the named gates and each the stage calls stale, in the plan's order, and keeps every other verdict", () => {
  const plan = ['universal', 'cli', 'devkit', 'web', 'deployment'];
  const gates = tool.stageJudgement({
    ...fixJudging, plan, verdicts: [
      { gate: 'universal', verdict: 'PASS', tree: 'T', at: hourOf(9) },
      { gate: 'cli', verdict: 'FAIL', tree: 'T', at: hourOf(9) },
      // Passed before the fix, then failed on this tree: the newest that stands says FAIL, and it is not stale.
      { gate: 'devkit', verdict: 'PASS', tree: 'S', at: hourOf(9) },
      { gate: 'devkit', verdict: 'FAIL', tree: 'T', at: hourOf(11) },
      { gate: 'web', verdict: 'PASS', tree: 'S', at: hourOf(9) },
    ],
  }).gates;
  const named = tool.checkoutRerun(gates, ['cli']);
  assert.deepEqual(named.run, ['cli', 'web']);
  assert.deepEqual(named.stale, [{ name: 'web', verdict: 'PASS', at: hourOf(9), paths: ['src/fix.ts'] }]);
  assert.deepEqual(named.kept.map((kept) => `${kept.name} ${kept.verdict}`), ['universal PASS', 'devkit FAIL']);
  assert.deepEqual(named.none, ['deployment'], 'a gate with no verdict at all runs only when named');
  // --stale names nothing: the stale gates alone.
  const stale = tool.checkoutRerun(gates, []);
  assert.deepEqual(stale.run, ['web']);
  assert.deepEqual(stale.kept.map((kept) => kept.name), ['universal', 'cli', 'devkit']);
  assert.deepEqual(tool.checkoutRerun(gates, ['deployment']).run, ['web', 'deployment']);
  assert.deepEqual(tool.checkoutRerun(gates, ['deployment']).none, []);
});
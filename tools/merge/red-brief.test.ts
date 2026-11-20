import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { briefGates, briefText, failuresInLog, markOfJudgement, markOfProbe, placeIn, writeBrief, type Failed, type Signed } from './red-brief.ts';

const VITEST = [
  ' RUN  v5.0.0',
  ' ❯ tools/merge/plan.test.ts (12 tests | 1 failed) 40ms',
  '⎯⎯⎯⎯⎯⎯⎯ Failed Tests 1 ⎯⎯⎯⎯⎯⎯⎯',
  '',
  ' FAIL  tools/merge/plan.test.ts > the plan > orders the rehearsals last',
  "AssertionError: expected [ 'deployment', 'family' ] to deeply equal [ 'family', 'deployment' ]",
  '',
  ' ❯ tools/merge/plan.test.ts:88:10',
  '     86|   const plan = gatePlan(declared);',
  '',
].join('\n');

const NODE = [
  'ℹ tests 40',
  'ℹ fail 1',
  '✖ failing tests:',
  '',
  'test at src/Daoris.Cli/test/home-census.test.ts:31:1',
  '✖ every tool that reads the home names it (3.1ms)',
  "  AssertionError [ERR_ASSERTION]: tools/merge/new.ts reads the home and names no 'reaches' line",
  '      at TestContext.<anonymous> (file:///x/src/Daoris.Cli/test/home-census.test.ts:40:12)',
].join('\n');

const TSC = [
  '> daoris@1.0.0 typecheck',
  "src/Daoris.Cli/src/sync.ts(120,7): error TS2322: Type 'string' is not assignable to type 'number'.",
  'npm error Lifecycle script `typecheck` failed with error:',
].join('\n');

test('a log names its failing tests with their file and line, or its first error where it names none', () => {
  const vitest = failuresInLog(VITEST);
  assert.deepEqual(vitest.map((each) => [each.test, each.file, each.line]), [['tools/merge/plan.test.ts > the plan > orders the rehearsals last', 'tools/merge/plan.test.ts', 88]]);
  assert.match(vitest[0]!.lines[0]!, /^AssertionError: expected/);
  const node = failuresInLog(NODE);
  assert.deepEqual(node.map((each) => [each.test, each.file, each.line]), [['src/Daoris.Cli/test/home-census.test.ts > every tool that reads the home names it', 'src/Daoris.Cli/test/home-census.test.ts', 31]]);
  const tsc = failuresInLog(TSC);
  assert.deepEqual(tsc.map((each) => [each.test, each.file, each.line]), [[null, 'src/Daoris.Cli/src/sync.ts', 120]]);
  assert.ok(!tsc[0]!.lines.some((line) => line.startsWith('npm error Lifecycle')), "npm's own trailer is not the error");
  assert.deepEqual(placeIn('   at X.Y() in C:/r/src/A/ATests.cs:line 42'), { file: 'C:/r/src/A/ATests.cs', line: 42 });
});

const PASS_LINES = ['✔ reports an error (2.1ms)', 'src/A/A.cs(3,5): warning CS8602: a possible null error', '    0 Error(s)', 'ℹ fail 0'];

test('a log whose first error-like line is a pass line, a count or a warning names its failing test, or says none failed', () => {
  const spec = failuresInLog([...PASS_LINES, '✖ breaks the page (1.5ms)', '  AssertionError: x'].join('\n'));
  assert.equal(spec.length, 1);
  assert.match(spec[0]!.lines[0]!, /^✖ breaks the page/);
  const tap = failuresInLog(['ok 1 - names an error case', 'not ok 2 - breaks the page', '  ---', "  error: 'x'"].join('\n'));
  assert.equal(tap[0]!.lines[0], 'not ok 2 - breaks the page');
  const none = failuresInLog(PASS_LINES.join('\n'), 143);
  assert.equal(none[0]!.test, null);
  assert.equal(none[0]!.lines[0], 'ended without a failing test, exit 143');
  assert.equal(failuresInLog(PASS_LINES.join('\n'))[0]!.lines[0], 'ended without a failing test');
  const build = failuresInLog([...PASS_LINES, 'src/B/B.cs(9,1): error CS1002: ; expected'].join('\n'));
  assert.deepEqual(build.map((each) => [each.test, each.file, each.line]), [[null, 'src/B/B.cs', 9]]);
});

test('a log whose cross or not ok names no test, or whose count line is an error, is read as the failure it is', () => {
  const lint = failuresInLog(['src/a.ts', '  3:5  error  no-unused-vars', '', '✖ 1 problem (1 error, 0 warnings)'].join('\n'));
  assert.match(lint[0]!.lines[0]!, /no-unused-vars/);
  const todo = failuresInLog(['not ok 1 - later # TODO not done', 'not ok 2 - breaks the page'].join('\n'));
  assert.equal(todo[0]!.test, 'breaks the page');
  const spec = failuresInLog(['✖ failing tests:', '✖ breaks the page (1.5ms)', '  AssertionError: x'].join('\n'));
  assert.equal(spec[0]!.test, 'breaks the page');
  const filter = failuresInLog(['    0 Error(s)', 'Error: 0 tests matched the filter'].join('\n'));
  assert.equal(filter[0]!.lines[0], 'Error: 0 tests matched the filter');
  assert.equal(failuresInLog('✖ reports no errors (2ms)')[0]!.test, 'reports no errors');
  const reached = failuresInLog(['✖ flaky thing (1ms)', 'reached: RED: suite/unit failed again alone'].join('\n'));
  assert.equal(reached[0]!.lines[0], 'reached: RED: suite/unit failed again alone');
});

test('a planted failure in two gates gives one brief with both, each marked by its run on main', async () => {
  const { dir, remove } = fixtureFolder('red-brief');
  try {
    writeFileSync(join(dir, 'tool-tests.log'), VITEST);
    writeFileSync(join(dir, 'cli-tests.log'), NODE);
    const results: Failed[] = [{ gate: { name: 'tool-tests' }, log: join(dir, 'tool-tests.log') }, { gate: { name: 'cli-tests' }, log: join(dir, 'cli-tests.log') }];
    // On main the plan's test passes (the branch broke it), and the home census fails the same way (main's red too).
    const onMain: Record<string, { result: string }> = { 'tool-tests': { result: 'pass' }, 'cli-tests': { result: 'fail' } };
    const gates = await briefGates(results, {
      signed: (result): Signed[] => [{ names: 'gate', test: result.gate.name }],
      read: (file) => readFileSync(file, 'utf8'),
      shown: (file) => file.slice(dir.length + 1),
      mark: async (failure) => markOfProbe(onMain[failure.test] ?? null, true, 'abc1234'),
    });
    const file = writeBrief(dir, briefText({ title: 'Red brief: roles-scope', ran: '2 of 9 gates failed.', gates }));
    assert.ok(existsSync(file) && file.endsWith('brief.md'));
    const text = readFileSync(file, 'utf8');
    assert.match(text, /## tool-tests \[BRANCH: passes on main \(abc1234\)\]/);
    assert.match(text, /`tools\/merge\/plan\.test\.ts > the plan > orders the rehearsals last` at `tools\/merge\/plan\.test\.ts:88`/);
    assert.match(text, /## cli-tests \[MAIN TOO: fails on main too \(abc1234\), the same way\]/);
    assert.match(text, /home-census\.test\.ts:31/);
    assert.match(text, /Marks: 1 the branch's, 1 main's too, 0 not checked\./);
  } finally {
    remove();
  }
});

test("a trx's tests are marked one by one, and a death of memory is never checked on main", async () => {
  const signed: Signed[] = [
    { names: 'test', test: 'N.ATests.Fails', failure: { test: 'N.ATests.Fails', message: 'Assert.Equal() Failure\nExpected: 1', frames: [{ file: 'src/A/ATests.cs', line: 12 }] } },
    { names: 'test', test: 'N.BTests.Fails', failure: { test: 'N.BTests.Fails', message: 'boom', frames: [] } },
  ];
  let asked = 0;
  const gates = await briefGates([{ gate: { name: 'driver' }, trx: 'x.trx' }, { gate: { name: 'modules' }, cause: 'memory' }], {
    signed: () => signed, read: () => null, shown: (file) => file,
    mark: async (failure) => { asked += 1; return markOfProbe({ result: failure.test.includes('ATests') ? 'fail' : 'pass' }, true, 'abc1234'); },
  });
  assert.equal(asked, 2);
  assert.deepEqual(gates[0]!.failures.map((each) => [each.test, each.file, each.line, each.mark?.on]), [['N.ATests.Fails', 'src/A/ATests.cs', 12, 'main'], ['N.BTests.Fails', null, null, 'branch']]);
  assert.equal(gates[1]!.mark?.on, 'unchecked');
});

test("the queue's judgement marks a failure: inherited from the base is main's, pinned on a member is the branch's", () => {
  const members = [{ branch: 'a' }, { branch: 'b' }];
  assert.equal(markOfJudgement({ outcome: 'inherited', why: 'it fails on the base with the same signature' }, members).on, 'main');
  assert.equal(markOfJudgement({ outcome: 'stop', why: 'base' }, members).on, 'main');
  const pinned = markOfJudgement({ outcome: 'pinned', member: 2 }, members);
  assert.equal(pinned.on, 'branch');
  assert.match(pinned.why, /\bb brings it/);
  assert.equal(markOfJudgement({ outcome: 'unconfirmed', why: 'it did not fail again' }, members).on, 'unchecked');
  assert.deepEqual(markOfProbe({ result: 'fail' }, false, 'abc1234').on, 'other');
  assert.deepEqual(markOfProbe({ result: 'cannot' }, false, 'abc1234').on, 'unchecked');
});

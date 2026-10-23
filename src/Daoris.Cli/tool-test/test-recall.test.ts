import { test } from 'node:test';
import assert from 'node:assert/strict';

/**
 * CISCOPE1c (D207, design §3.5 and §4.1 point 4): the failure reader and the signature (`tools/test-failures.mjs`), and
 * recall's search for a failure's introducing commit (`tools/test-recall.mjs`) over synthetic histories, each reaching the
 * verdict the design gives it. Run by the `tool-tests` gate.
 */

interface Frame { method: string; file: string | null; line: number | null }
interface Failure { runner: string; unit: string; test: string; exception: string; message: string; frames: Frame[] }
interface Signature { test?: string; exception?: string; location?: { method: string; text: string; ordinal: number }; message?: string; gate?: string; step?: string; errors?: string[] }
type Result = 'pass' | 'absent' | 'nobuild' | Signature;
interface Searched {
  settled: boolean; reason: string; introduction: string | null; verdict: string; runs: number; walked: number; capped: boolean;
  halved: boolean; probes: number; open?: { size: number; counts: Record<string, number> };
}

const failures = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/test-failures.mjs') as {
  normalise: (line: string) => string;
  readTrx: (trx: string) => Failure[];
  readVitest: (json: string) => Failure[];
  readNodeTap: (tap: string, unit?: string) => Failure[];
  signatureOf: (failure: Failure, read?: (path: string) => string | null, root?: string) => Signature;
  gateSignature: (gate: string, step: string, output: string) => Signature;
  sameSignature: (a: Signature, b: Signature) => boolean;
};
const recall = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/test-recall.mjs') as {
  introduction: (input: {
    commits: string[]; start?: number; startPasses?: boolean; f: number; signature: Signature;
    run: (i: number) => Result; classify: (i: number) => string; named?: number | null; cap?: number; stretch?: number;
  }) => Searched;
  recallOf: (judged: { verdict: string }[]) => { counts: Record<string, number>; lowerBound: number | null; ifUnsettledSelected: number | null };
  dedupe: (list: { unit: string; signature: Signature; introduction?: string; source: string }[]) => { sightings: string[] }[];
  fixLogFailures: (text: string, since: string, until: string) => { date: string; classes: string[]; files: string[]; commits: string[] }[];
};

// ---------------------------------------------------------------------------------------------------
// The reader

const TRX = `<?xml version="1.0" encoding="utf-8"?>
<TestRun id="1" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testId="t1" testName="N.Tests.WidgetTests.Adds(left: 1, right: 2)" outcome="Failed">
      <Output><ErrorInfo>
        <Message>Assert.Equal() Failure: Values differ&#xD;
Expected: 3&#xD;
Actual:   4</Message>
        <StackTrace>   at N.Tests.WidgetTests.Adds(Int32 left, Int32 right) in D:\\work\\gate-tree\\src\\N.Tests\\WidgetTests.cs:line 4</StackTrace>
      </ErrorInfo></Output>
    </UnitTestResult>
    <UnitTestResult testId="t2" testName="N.Tests.WidgetTests+Nested.Throws" outcome="Failed">
      <Output><ErrorInfo>
        <Message>System.InvalidOperationException : tree s-a2762239 at 4f3c2a1b-9d8e-4c7b-a6f5-0e1d2c3b4a59 is gone</Message>
        <StackTrace>   at N.Core.Store.Open(String id) in D:\\work\\gate-tree\\src\\N.Core\\Store.cs:line 40
   at N.Tests.WidgetTests.Nested.&lt;Throws&gt;d__2.MoveNext() in D:\\work\\gate-tree\\src\\N.Tests\\WidgetTests.cs:line 9</StackTrace>
      </ErrorInfo></Output>
    </UnitTestResult>
    <UnitTestResult testId="t3" testName="N.Tests.WidgetTests.Passes" outcome="Passed" />
  </Results>
  <TestDefinitions>
    <UnitTest id="t1"><TestMethod className="N.Tests.WidgetTests" name="Adds" /></UnitTest>
    <UnitTest id="t2"><TestMethod className="N.Tests.WidgetTests+Nested" name="Throws" /></UnitTest>
    <UnitTest id="t3"><TestMethod className="N.Tests.WidgetTests" name="Passes" /></UnitTest>
  </TestDefinitions>
</TestRun>`;

const WIDGET_CS = [
  'public class WidgetTests {',
  '  [Theory] [InlineData(1, 2)] public void Adds(int left, int right) {',
  '    var sum = left + right;',
  '    Assert.Equal(3, sum + 1);',
  '  }',
  '  public class Nested {',
  '    [Fact] public async Task Throws() {',
  '      await Task.Yield();',
  '      Store.Open("x");',
  '    }',
  '  }',
  '}',
].join('\n');

test("the reader keeps a trx failure's case, its exception type and its own frame", () => {
  const read = failures.readTrx(TRX) as [Failure, Failure];
  assert.equal(read.length, 2);
  assert.equal(read[0].test, 'N.Tests.WidgetTests.Adds(left: 1, right: 2)', 'the theory case is kept, as the merge tool cuts it');
  assert.equal(read[0].exception, 'Assert.Equal');
  assert.equal(read[1].unit, 'N.Tests.WidgetTests+Nested');
  assert.equal(read[1].exception, 'System.InvalidOperationException');
  assert.deepEqual(read[1].frames.map((frame) => frame.method), ['N.Core.Store.Open', 'N.Tests.WidgetTests.Nested.Throws']);
  const signature = failures.signatureOf(read[1], (path) => (path === 'src/N.Tests/WidgetTests.cs' ? WIDGET_CS : null));
  assert.deepEqual(signature.location, { method: 'N.Tests.WidgetTests.Nested.Throws', text: 'Store.Open("x");', ordinal: 0 });
  assert.equal(signature.message, undefined, 'never the message where a location is read');
});

test("the reader keeps a vitest failure's titles, exception type and location", () => {
  const report = JSON.stringify({
    testResults: [{
      name: 'D:/work/src/Daoris.Web/src/work/Board.test.tsx',
      assertionResults: [
        { status: 'passed', title: 'draws', ancestorTitles: ['Board'], fullName: 'Board draws', failureMessages: [] },
        {
          status: 'failed', title: 'sorts', ancestorTitles: ['Board', 'rows'], fullName: 'Board rows sorts',
          failureMessages: ['AssertionError: expected [ 2, 1 ] to deeply equal [ 1, 2 ]\n    at D:/work/src/Daoris.Web/src/work/Board.test.tsx:3:20\n    at node:internal/x:1:1'],
        },
      ],
    }],
  });
  const [read] = failures.readVitest(report) as [Failure];
  assert.equal(read.test, 'Board rows sorts');
  assert.equal(read.exception, 'AssertionError');
  const source = "import { it } from 'vitest';\nit('sorts', () => {\n  expect(sort([2, 1])).toEqual([1, 2]);\n});";
  const signature = failures.signatureOf(read, (path) => (path === 'src/Daoris.Web/src/work/Board.test.tsx' ? source : null));
  assert.equal(signature.location?.text, 'expect(sort([<n>, <n>])).toEqual([<n>, <n>]);');
});

test("the reader keeps a node --test failure's titles, exception type and location, and not its suite's", () => {
  const tap = [
    'TAP version 13',
    '# Subtest: the census',
    '    # Subtest: reads a class',
    '    not ok 1 - reads a class',
    '      ---',
    '      duration_ms: 1.2',
    "      location: 'D:\\\\work\\\\src\\\\Daoris.Cli\\\\tool-test\\\\x.test.ts:5:3'",
    "      failureType: 'testCodeFailure'",
    '      error: |-',
    '        Expected values to be strictly equal:',
    '        ',
    '        1 !== 2',
    "      code: 'ERR_ASSERTION'",
    "      name: 'AssertionError'",
    '      stack: |-',
    '        TestContext.<anonymous> (file:///D:/work/src/Daoris.Cli/tool-test/x.test.ts:6:10)',
    '        Test.runInAsyncScope (node:async_hooks:211:14)',
    '      ...',
    '    1..1',
    'not ok 1 - the census',
    '  ---',
    "  failureType: 'subtestsFailed'",
    '  ...',
  ].join('\n');
  const read = failures.readNodeTap(tap) as [Failure];
  assert.equal(read.length, 1, "a suite failing only for its subtest is not a failure of its own");
  assert.equal(read[0].test, 'the census > reads a class');
  assert.equal(read[0].exception, 'AssertionError');
  const source = "import { test } from 'node:test';\n\ntest('the census', async (t) => {\n  // a\n  await t.test('reads a class', () => {\n    assert.equal(1, 2);\n  });\n});";
  const signature = failures.signatureOf(read[0], (path) => (path === 'src/Daoris.Cli/tool-test/x.test.ts' ? source : null));
  assert.deepEqual(signature.location, { method: 'TestContext.<anonymous>', text: 'assert.equal(<n>, <n>);', ordinal: 0 });
});

// ---------------------------------------------------------------------------------------------------
// The signature

test('two runs whose messages differ by a GUID, a hex id and an actual value, and lines inserted above, are one signature', () => {
  const one: Failure = { runner: 'trx', unit: 'N.T', test: 'N.T.M', exception: 'System.IO.IOException', message: 'tree 4f3c2a1b-9d8e-4c7b-a6f5-0e1d2c3b4a59 at 0f086f0e4ab held 3', frames: [{ method: 'N.T.M', file: 'C:\\a\\src\\T.cs', line: 3 }] };
  const two: Failure = { ...one, message: 'tree 11111111-2222-3333-4444-555555555555 at cdf6d5f55aa held 7', frames: [{ method: 'N.T.M', file: 'D:\\b\\src\\T.cs', line: 6 }] };
  const before = 'class T {\n void M() {\n  Throw();\n }\n}';
  const after = 'class T {\n void M() {\n  var a = 1;\n  var b = 2;\n  var c = 3;\n  Throw();\n }\n}';
  const a = failures.signatureOf(one, () => before);
  const b = failures.signatureOf(two, () => after);
  assert.ok(failures.sameSignature(a, b), 'the location is its source text, never its line number');
  // Without a location, the message line normalised.
  const c = failures.signatureOf({ ...one, frames: [] });
  const d = failures.signatureOf({ ...two, frames: [] });
  assert.ok(failures.sameSignature(c, d), 'GUIDs, hex runs and numbers are normalised');
  assert.ok(!failures.sameSignature(a, failures.signatureOf({ ...one, exception: 'System.TimeoutException' }, () => before)), 'another exception type is another failure');
  assert.ok(!failures.sameSignature(a, failures.signatureOf({ ...one, test: 'N.T.M(case: 2)' }, () => before)), 'another theory case is another failure');
});

test("a build's signature is the set of its errors, in either order", () => {
  const first = 'src/A/A.cs(3,5): error CS0103: The name x does not exist\nsrc/B/B.cs(9,1): error CS1002: ; expected\n';
  const second = 'src/B/B.cs(9,1): error CS1002: ; expected\nsrc/A/A.cs(3,5): error CS0103: The name x does not exist\nsrc/A/A.cs(3,5): error CS0103: The name x does not exist\n';
  const a = failures.gateSignature('driver', 'build', first);
  assert.deepEqual(a.errors, ['CS0103 src/A/A.cs', 'CS1002 src/B/B.cs']);
  assert.ok(failures.sameSignature(a, failures.gateSignature('driver', 'build', second)));
});

// ---------------------------------------------------------------------------------------------------
// The search, over synthetic histories

const SIG: Signature = { test: 'N.T.M', exception: 'Assert.True', location: { method: 'N.T.M', text: 'Assert.True(x);', ordinal: 0 } };
const OTHER: Signature = { ...SIG, exception: 'System.NullReferenceException' };
const commitsOf = (n: number) => Array.from({ length: n }, (_, i) => `c${i}`);

/** A history: each commit's result, the failing signature from each index in `red` (as [from, to) ranges), a pass elsewhere. */
function history(_n: number, red: [number, number][], overrides: Record<number, Result> = {}) {
  const ran: number[] = [];
  const run = (i: number): Result => {
    ran.push(i);
    if (i in overrides) return overrides[i] as Result;
    return red.some(([from, to]) => i >= from && i < to) ? SIG : 'pass';
  };
  return { run, ran };
}

test('a transition within the cap is found by the walk exactly, with no halving run', () => {
  const { run } = history(200, [[180, 200]]);
  const found = recall.introduction({ commits: commitsOf(200), start: 0, startPasses: true, f: 199, signature: SIG, run, classify: (i) => (i === 180 ? 'selected' : i % 2 ? 'missed' : 'unscoped') });
  assert.equal(found.introduction, 'c180');
  assert.equal(found.verdict, 'selected');
  assert.equal(found.halved, false);
  assert.equal(found.walked, 20);
});

test('an open set whose commits all get one verdict is settled without a walk', () => {
  const { run, ran } = history(30, [[10, 30]]);
  const found = recall.introduction({ commits: commitsOf(30), start: 0, startPasses: true, f: 29, signature: SIG, run, classify: () => 'selected' });
  assert.equal(found.settled, true);
  assert.equal(found.verdict, 'selected');
  assert.equal(found.walked, 0);
  assert.deepEqual(ran, [29], 'only the first sighting is run');
});

test('a fix then a reintroduction inside a short range: the walk finds the last transition', () => {
  const { run } = history(100, [[20, 40], [70, 100]]);
  const found = recall.introduction({ commits: commitsOf(100), start: 0, startPasses: true, f: 99, signature: SIG, run, classify: (i) => (i === 20 ? 'selected' : i === 70 ? 'missed' : i % 2 ? 'selected' : 'missed') });
  assert.equal(found.introduction, 'c70');
  assert.equal(found.verdict, 'missed');
});

test('a transition past the cap is settled by step 5 where the open set agrees, and unsettled where it does not', () => {
  const { run } = history(400, [[300, 400]]);
  const agree = recall.introduction({ commits: commitsOf(400), start: 0, startPasses: true, f: 399, signature: SIG, run, classify: (i) => (i >= 290 && i <= 351 ? 'selected' : i % 2 ? 'missed' : 'selected') });
  assert.equal(agree.capped, true);
  assert.equal(agree.walked, 48);
  assert.equal(agree.settled, true, `${agree.reason} ${JSON.stringify(agree.open)}`);
  assert.equal(agree.verdict, 'selected');
  const { run: run2 } = history(400, [[300, 400]]);
  const disagree = recall.introduction({ commits: commitsOf(400), start: 0, startPasses: true, f: 399, signature: SIG, run: run2, classify: (i) => (i === 300 ? 'selected' : 'missed') });
  assert.equal(disagree.settled, false);
  assert.equal(disagree.reason, 'open set disagrees');
  assert.equal(disagree.verdict, 'unsettled');
});

test('the probes recurse until no stretch between run commits exceeds sixteen', () => {
  const { run, ran } = history(2000, [[1000, 2000]]);
  const found = recall.introduction({ commits: commitsOf(2000), start: 0, startPasses: true, f: 1999, signature: SIG, run, classify: (i) => (i === 1000 ? 'selected' : 'missed') });
  const known = [...new Set(ran)].filter((i) => i >= 999 && i <= 1951).sort((a, b) => a - b);
  for (let k = 1; k < known.length; k += 1) assert.ok((known[k] as number) - (known[k - 1] as number) <= 16, `a stretch from ${known[k - 1]} to ${known[k]}`);
  assert.ok(found.probes > 0);
  assert.equal(found.verdict, 'unsettled');
});

test('a fix and a reintroduction hidden wholly between two probes that both show the signature: never selected', () => {
  // The earlier introduction (c500) is reached; the fix (c900..c905) and the reintroduction (c905, not reached) sit between
  // probes; the walk caps above them.
  const { run } = history(1200, [[500, 900], [905, 1200]]);
  const found = recall.introduction({ commits: commitsOf(1200), start: 0, startPasses: true, f: 1199, signature: SIG, run, classify: (i) => (i === 500 ? 'selected' : 'missed'), stretch: 400 });
  assert.notEqual(found.verdict, 'selected', JSON.stringify(found));
  assert.ok(found.verdict === 'missed' || found.verdict === 'unsettled');
});

test('a signature that changes partway is unsettled, signature changed; a rename is never selected at the rename', () => {
  const { run } = history(60, [[10, 60]], { 50: OTHER });
  const changed = recall.introduction({ commits: commitsOf(60), start: 0, startPasses: true, f: 59, signature: SIG, run, classify: (i) => (i === 50 ? 'selected' : 'missed') });
  assert.equal(changed.reason, 'signature changed');
  assert.equal(changed.verdict, 'unsettled');
  const { run: renamed } = history(60, [[10, 60]], Object.fromEntries(Array.from({ length: 45 }, (_, i) => [i, 'absent' as Result])));
  const atRename = recall.introduction({ commits: commitsOf(60), start: 0, startPasses: true, f: 59, signature: SIG, run: renamed, classify: (i) => (i === 45 ? 'selected' : 'missed') });
  assert.notEqual(atRename.verdict, 'selected');
});

test('lines inserted above a failing assertion between its introduction and its first failure: the introduction found, not the insertion', () => {
  // The signature is the source text, so a run after the insertion reads the same signature: the history is red from c20.
  const { run } = history(40, [[20, 40]]);
  const found = recall.introduction({ commits: commitsOf(40), start: 0, startPasses: true, f: 39, signature: SIG, run, classify: (i) => (i === 20 ? 'selected' : i === 30 ? 'missed' : i % 2 ? 'missed' : 'unscoped') });
  assert.equal(found.introduction, 'c20');
});

test("a FIX-LOG entry's named commit whose two runs show the transition is judged first seen there", () => {
  const { run, ran } = history(300, [[120, 300]]);
  const found = recall.introduction({ commits: commitsOf(300), start: 0, startPasses: true, f: 299, named: 120, signature: SIG, run, classify: (i) => (i === 120 ? 'selected' : 'missed') });
  assert.equal(found.introduction, 'c120');
  assert.equal(found.walked, 1, 'its first walked run is the named commit\'s parent, already run');
  assert.ok(ran.length <= 3);
});

test('a reproducible failure whose message carries a fresh GUID each run is found at its introducing commit', () => {
  let n = 0x10000000;
  const run = (i: number): Result => {
    if (i < 70) return 'pass';
    n += 1;
    return failures.signatureOf({ runner: 'trx', unit: 'N.T', test: 'N.T.M', exception: 'System.X', message: `held by ${n.toString(16).padStart(8, 'a')}-0000-4000-8000-000000000000`, frames: [] });
  };
  const signature = failures.signatureOf({ runner: 'trx', unit: 'N.T', test: 'N.T.M', exception: 'System.X', message: 'held by 4f3c2a1b-9d8e-4c7b-a6f5-0e1d2c3b4a59', frames: [] });
  const found = recall.introduction({ commits: commitsOf(80), start: 0, startPasses: true, f: 79, signature, run, classify: (i) => (i === 70 ? 'selected' : 'missed') });
  assert.equal(found.introduction, 'c70');
});

test('a range with no recorded pass is settled where the start passes, and unsettled, no pass found, where it does not', () => {
  const { run } = history(30, [[20, 30]]);
  const settled = recall.introduction({ commits: commitsOf(30), start: 0, f: 29, signature: SIG, run, classify: (i) => (i === 20 ? 'selected' : 'missed') });
  assert.equal(settled.introduction, 'c20');
  const { run: red } = history(30, [[0, 30]]);
  const none = recall.introduction({ commits: commitsOf(30), start: 0, f: 29, signature: SIG, run: red, classify: () => 'missed' });
  assert.equal(none.reason, 'no pass found');
});

test('a commit that does not build is walked past and named; in a disagreeing open set the failure is unsettled', () => {
  const { run } = history(40, [[20, 40]], { 25: 'nobuild' });
  const found = recall.introduction({ commits: commitsOf(40), start: 0, startPasses: true, f: 39, signature: SIG, run, classify: (i) => (i === 20 ? 'selected' : 'missed') });
  assert.equal(found.verdict, 'unsettled');
  assert.equal(found.reason, 'open set disagrees');
});

test('recall is a lower bound with unsettled counted as missed, and one regression is counted once', () => {
  const figures = recall.recallOf([{ verdict: 'selected' }, { verdict: 'selected' }, { verdict: 'missed' }, { verdict: 'unsettled' }, { verdict: 'unscoped' }, { verdict: 'deferred' }]);
  assert.equal(figures.lowerBound, 0.5);
  assert.equal(figures.ifUnsettledSelected, 0.75);
  const once = recall.dedupe([
    { unit: 'A', signature: SIG, introduction: 'c1', source: 'run 1' },
    { unit: 'A', signature: SIG, introduction: 'c1', source: 'run 2' },
    { unit: 'A', signature: OTHER, introduction: 'c1', source: 'run 3' },
  ]);
  assert.equal(once.length, 2);
  assert.deepEqual(once[0]?.sightings, ['run 1', 'run 2']);
});

test("the FIX-LOG's failing entries are read with the units and commits they name", () => {
  const text = '# Fix log\n\n## 2026-10-21 — Three censuses broken\n\n`NoConsoleWindowTests` failed after `abc1234`.\n\n## 2026-10-22 — A wording fix\n\nNothing failed here but prose.\n\n## 2026-08-01 — Old\n\n`OldTests` failed.\n';
  const read = recall.fixLogFailures(text, '2026-09-24', '2026-10-24');
  assert.equal(read.length, 1);
  assert.deepEqual(read[0]?.classes, ['NoConsoleWindowTests']);
  assert.deepEqual(read[0]?.commits, ['abc1234']);
});

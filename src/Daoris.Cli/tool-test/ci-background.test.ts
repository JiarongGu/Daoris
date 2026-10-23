import { test } from 'node:test';
import assert from 'node:assert/strict';

/**
 * CISCOPE1d (D207, design §5.1 to §5.3 and §8 row 4): the background run's due record (the sweep, the night and its catch-up,
 * the overdue report), the run-kind matrix, known red by test and fingerprint matched by signature with its clearing and
 * expiry, the receipts a stage counts, and the stage history (`tools/ci-background.mjs`). The merge tool's own use of them, and
 * the stage's refusals, are in `merge-branch.test.ts`. Run by the `tool-tests` gate.
 */

interface Frame { method: string; file: string | null; line: number | null }
interface Failure { runner: string; unit: string; test: string; exception: string; message: string; frames: Frame[] }
interface Signature { test?: string; exception?: string; location?: { method: string; text: string; ordinal: number }; message?: string; gate?: string; step?: string }
interface Fingerprint { test: string; exception: string; at: string | null; message: string }
interface Finding {
  gate: string | null; unit: string | null; test: string; fingerprint: Fingerprint; signature: Signature; state: string;
  firstSeen: { commit: string; at: string }; lastSeen: { commit: string; at: string; message: string }; since: string;
  cleared: { commit: string; at: string; kind: string } | null;
}
interface Findings { schema: number; findings: Finding[] }
interface Sweep { schema: number; halves: Record<string, { last: string; commit: string; kind: string }>; night: { last: string; commit: string; ended: string } | null }
interface Receipt { commit: string; tree: string; kind: string; finished: boolean; state?: string }
interface Sighting { gate?: string; unit?: string; test: string; fingerprint: Fingerprint; signature: Signature; message?: string; commit: string; at: string; kind?: string }

const failures = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/test-failures.mjs') as {
  readTrx: (trx: string) => Failure[];
  signatureOf: (failure: Failure, read?: (path: string) => string | null, root?: string) => Signature;
  gateSignature: (gate: string, step: string, output: string) => Signature;
};
const bg = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/ci-background.mjs') as {
  SWEEP_BOUND_MS: number;
  KNOWN_RED_MS: number;
  RUN_KINDS: Record<string, { rehearsals: string; counts: boolean }>;
  COUNTED_KINDS: string[];
  sweepState: (record: Sweep, halves: string[], now: number) => { gate: string; last: string | null; overdue: boolean }[];
  dueHalves: (record: Sweep, halves: string[], now: number) => string[];
  sweepSatisfied: (result: { whole: string | null; verdict: string; trx: string | null }) => boolean;
  noteSweeps: (record: Sweep, swept: { gate: string; at: string; commit: string; kind: string }[]) => Sweep;
  lastNight: (now: number, hour?: number) => number;
  nightDue: (record: Sweep, now: number, hour?: number) => { due: boolean; since: string };
  nightSatisfied: (state: string) => boolean;
  noteNight: (record: Sweep, night: { at: string; commit: string; ended: string }) => Sweep;
  overdueLines: (record: Sweep, halves: string[], now: number, options?: { hour?: number }) => string[];
  nextRun: (input: { record: Sweep; halves: string[]; tipGated: boolean; now: number; hour?: number }) => { kind: string; key: string; why: string; halves?: string[] } | null;
  kindPlan: (kind: string, input: { plan: string[]; ordinary: { run: string[]; owed: string[] }; due?: string[]; halves?: string[] }) => { run: string[]; owed: string[]; whole: Map<string, string>; skipped: string[] };
  fingerprintOf: (failure: Failure, root?: string | null) => Fingerprint;
  fingerprintKey: (fingerprint: Fingerprint) => string;
  fileSighting: (record: Findings, sighting: Sighting) => { record: Findings; how: string; finding: Finding };
  clearPasses: (record: Findings, input: { commit: string; at: string; kind: string; passed: (finding: Finding) => boolean; descends: (from: string, to: string) => boolean }) => { record: Findings; cleared: Finding[] };
  knownRedStanding: (finding: Finding, now: number) => { expired: boolean; warn: boolean };
  knownRed: (record: Findings, failure: { test: string; signature: Signature }, now: number) => Finding | null;
  knownRedLines: (record: Findings, now: number) => string[];
  passingTests: (trx: string) => Set<string>;
  receiptFor: (receipts: Receipt[], tree: string) => { receipt: Receipt | null; why: string | null };
  stageRange: (history: { commit: string; at: string }[], commit: string, git: { isAncestor: (a: string, b: string) => boolean; commits: (a: string, b: string) => string[] }) => { known: boolean; why?: string; commits?: string[] };
  stageRangeLines: (range: object) => string[];
};

const HOUR = 60 * 60 * 1000;
const empty = (): Sweep => ({ schema: 1, halves: {}, night: null });
const at = (ms: number): string => new Date(ms).toISOString().replace(/\.\d+Z$/, 'Z');
// A local noon, so the night's hour (3 local) is well behind it whatever the zone.
const NOON = new Date(2026, 9, 24, 12, 0, 0).getTime();
const HALVES = ['driver-process', 'driver-process-git', 'modules-process'];

// ---------------------------------------------------------------------------------------------------
// The due record (§5.1)

test('a night missed is caught up at the next start, before any other background run, and a finished night satisfies it', () => {
  // The last night finished two days ago: the machine was off last night.
  let record: Sweep = { ...empty(), night: { last: at(bg.lastNight(NOON) - 48 * HOUR), commit: 'a'.repeat(40), ended: at(bg.lastNight(NOON) - 47 * HOUR) } };
  for (const gate of HALVES) record = bg.noteSweeps(record, [{ gate, at: at(NOON - HOUR), commit: 'a'.repeat(40), kind: 'ordinary' }]);
  assert.equal(bg.nightDue(record, NOON).due, true);
  // A tip no run gave a verdict on waits for the night: the catch-up runs first.
  const next = bg.nextRun({ record, halves: HALVES, tipGated: false, now: NOON });
  assert.equal(next?.kind, 'nightly');
  assert.match(bg.overdueLines(record, HALVES, NOON).join('\n'), /overdue: the night due .* has not finished/);

  // A nightly run that finished red satisfies the night; one stopped, or one in error, does not.
  assert.equal(bg.nightSatisfied('failed'), true);
  assert.equal(bg.nightSatisfied('green'), true);
  assert.equal(bg.nightSatisfied('stopped'), false);
  assert.equal(bg.nightSatisfied('error'), false);
  record = bg.noteNight(record, { at: at(NOON + HOUR), commit: 'b'.repeat(40), ended: at(NOON + 3 * HOUR) });
  assert.equal(bg.nightDue(record, NOON + 4 * HOUR).due, false);
  assert.equal(bg.nextRun({ record, halves: HALVES, tipGated: false, now: NOON + 4 * HOUR })?.kind, 'ordinary');
  assert.equal(bg.nextRun({ record, halves: HALVES, tipGated: true, now: NOON + 4 * HOUR }), null, 'nothing due, the tip gated: nothing runs');
  // The next night is due at its hour, the day after.
  assert.equal(bg.nightDue(record, bg.lastNight(NOON) + 24 * HOUR + 1).due, true);
});

test('a red whole run satisfies the sweep; a run stopped before its classes ran, or a build that left them unrun, leaves it due', () => {
  const now = NOON;
  let record = empty();
  for (const gate of HALVES) record = bg.noteSweeps(record, [{ gate, at: at(now - 25 * HOUR), commit: 'a'.repeat(40), kind: 'ordinary' }]);
  assert.deepEqual(bg.dueHalves(record, HALVES, now), HALVES, 'a day and an hour: each is due');

  // The git half ran whole and failed, its trx read: every class has a verdict, so its sweep is satisfied.
  assert.equal(bg.sweepSatisfied({ whole: 'its sweep is due', verdict: 'FAIL', trx: 'driver-process-git.trx' }), true);
  // The run was stopped mid-half: ENDED gives no class a verdict.
  assert.equal(bg.sweepSatisfied({ whole: 'its sweep is due', verdict: 'ENDED', trx: null }), false);
  // A build failure: the half failed with no trx, its classes unrun.
  assert.equal(bg.sweepSatisfied({ whole: 'its sweep is due', verdict: 'FAIL', trx: null }), false);
  // By reach is no sweep, whatever it ran.
  assert.equal(bg.sweepSatisfied({ whole: null, verdict: 'PASS', trx: 'x.trx' }), false);

  record = bg.noteSweeps(record, [{ gate: 'driver-process-git', at: at(now), commit: 'b'.repeat(40), kind: 'ordinary' }]);
  assert.deepEqual(bg.dueHalves(record, HALVES, now + HOUR), ['driver-process', 'modules-process']);
  const lines = bg.overdueLines(record, HALVES, now + HOUR, { hour: new Date(now).getHours() });
  assert.equal(lines.filter((line) => /sweep/.test(line)).length, 2);
  assert.match(lines.join('\n'), /overdue: driver-process's sweep, last run whole .*, was due .*; the next run runs it whole/);
  assert.doesNotMatch(lines.join('\n'), /driver-process-git/);
  // A half the record never saw is due: no sweep of it is known.
  assert.match(bg.overdueLines(empty(), ['modules-process'], now).join('\n'), /modules-process has no sweep recorded/);
});

test("the watch's next run: the night first, then a tip with no verdict, then a due sweep with no promotion to ride", () => {
  let record: Sweep = { ...empty(), night: { last: at(NOON - HOUR), commit: 'a'.repeat(40), ended: at(NOON) } };
  record = bg.noteSweeps(record, [{ gate: 'driver-process', at: at(NOON - 30 * HOUR), commit: 'a'.repeat(40), kind: 'ordinary' }]);
  record = bg.noteSweeps(record, [{ gate: 'modules-process', at: at(NOON - HOUR), commit: 'a'.repeat(40), kind: 'ordinary' }]);
  const tip = bg.nextRun({ record, halves: ['driver-process', 'modules-process'], tipGated: false, now: NOON });
  assert.equal(tip?.kind, 'ordinary', 'a promotion to ride: the ordinary run runs the due half whole');
  const sweep = bg.nextRun({ record, halves: ['driver-process', 'modules-process'], tipGated: true, now: NOON });
  assert.equal(sweep?.kind, 'sweep');
  assert.deepEqual(sweep?.halves, ['driver-process']);
});

test('the run-kind matrix: an ordinary run owes the rehearsals and runs a due half whole; a sweep only the half; a night every gate; a stage pays them', () => {
  const plan = ['cli', 'driver', 'driver-process', 'driver-process-git', 'rehearse', 'rehearse-family', 'deployment'];
  const ordinary = { run: ['cli', 'driver'], owed: ['rehearse-family', 'deployment'] };
  const due = ['driver-process-git'];
  const plain = bg.kindPlan('ordinary', { plan, ordinary, due, halves: HALVES });
  assert.deepEqual(plain.run, ['cli', 'driver', 'driver-process-git']);
  assert.deepEqual(plain.owed, ['rehearse-family', 'deployment']);
  assert.match(plain.whole.get('driver-process-git') ?? '', /its sweep is due/);
  const sweep = bg.kindPlan('sweep', { plan, ordinary, due, halves: HALVES });
  assert.deepEqual(sweep.run, ['driver-process-git']);
  assert.deepEqual(sweep.owed, []);
  assert.ok(sweep.skipped.includes('cli') && sweep.skipped.includes('rehearse-family'));
  const night = bg.kindPlan('nightly', { plan, ordinary, due, halves: ['driver-process', 'driver-process-git'] });
  assert.deepEqual(night.run, plan, 'every gate, the rehearsals included: the one mode that overrides CIFAST1');
  assert.deepEqual([...night.whole.keys()], ['driver-process', 'driver-process-git']);
  const stage = bg.kindPlan('stage', { plan, ordinary: { run: ['cli', 'rehearse-family', 'deployment'], owed: [] }, due: [], halves: HALVES });
  assert.deepEqual(stage.run, ['cli', 'rehearse-family', 'deployment']);
  assert.equal(bg.RUN_KINDS.ordinary!.rehearsals, 'owed');
  assert.equal(bg.RUN_KINDS.nightly!.rehearsals, 'run');
  assert.equal(bg.RUN_KINDS.sweep!.counts, false, 'a sweep-only run gives no receipt a stage counts');
  assert.deepEqual([...bg.COUNTED_KINDS].sort(), ['ci', 'full', 'nightly', 'ordinary', 'stage']);
});

// ---------------------------------------------------------------------------------------------------
// Known red (§5.2)

const trxOf = (results: { name: string; outcome: string; message?: string; line?: number }[]): string => `<?xml version="1.0" encoding="utf-8"?>
<TestRun id="1" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
${results.map((each, i) => (each.outcome === 'Passed'
    ? `    <UnitTestResult testId="t${i}" testName="${each.name}" outcome="Passed" />`
    : `    <UnitTestResult testId="t${i}" testName="${each.name}" outcome="Failed"><Output><ErrorInfo>
      <Message>${each.message}</Message>
      <StackTrace>   at ${each.name.replace(/\(.*$/, '')}() in D:\\work\\gate-tree\\src\\N.Tests\\WidgetTests.cs:line ${each.line}</StackTrace>
    </ErrorInfo></Output></UnitTestResult>`)).join('\n')}
  </Results>
  <TestDefinitions>
${results.map((each, i) => `    <UnitTest id="t${i}"><TestMethod className="N.Tests.WidgetTests" name="${each.name.split('.').at(-1)}" /></UnitTest>`).join('\n')}
  </TestDefinitions>
</TestRun>`;

const WIDGET = [
  'public class WidgetTests {',
  '  [Theory] [InlineData(1)] [InlineData(2)] public void Adds(int n) {',
  '    Assert.Equal(3, n + 1);',
  '  }',
  '  [Fact] public void Opens() {',
  '    var store = Store.Open();',
  '    Assert.True(store.Ready);',
  '    Assert.True(store.Fresh);',
  '  }',
  '  [Fact] public void Closes() {',
  '    Assert.True(Store.Close());',
  '  }',
  '}',
].join('\n');

/** Each failure of a trx as a sighting on `commit` at `when`, its signature read from `source`. */
function sightings(trx: string, commit: string, when: string, source = WIDGET): Sighting[] {
  return failures.readTrx(trx).map((failure) => ({
    gate: 'driver-process', unit: failure.unit, test: failure.test,
    fingerprint: bg.fingerprintOf(failure),
    signature: failures.signatureOf(failure, (path) => (path === 'src/N.Tests/WidgetTests.cs' ? source : null)),
    message: failure.message, commit, at: when, kind: 'ordinary',
  }));
}

function file(record: Findings, list: Sighting[]): { record: Findings; how: string[] } {
  const how: string[] = [];
  for (const sighting of list) {
    const filed = bg.fileSighting(record, sighting);
    record = filed.record;
    how.push(filed.how);
  }
  return { record, how };
}

test('known red is by test and fingerprint: a second failing test in a known-red class counts, and so does a changed signature', () => {
  const t0 = at(NOON);
  let { record } = file({ schema: 1, findings: [] }, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c1', t0));
  assert.equal(record.findings.length, 1);
  const [opens] = sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c2', t0);
  assert.ok(bg.knownRed(record, opens!, NOON + HOUR), 'the same signature on a later commit is inherited');
  // Another test of the same class failing: not known red, it counts against the candidate.
  const [closes] = sightings(trxOf([{ name: 'N.Tests.WidgetTests.Closes', outcome: 'Failed', message: 'Assert.True() Failure', line: 11 }]), 'c2', t0);
  assert.equal(bg.knownRed(record, closes!, NOON + HOUR), null);
  // The same test failing at its other assertion (line 8, `store.Fresh`): another signature, so it counts too, and is a new finding.
  const [moved] = sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 8 }]), 'c2', t0);
  assert.equal(bg.knownRed(record, moved!, NOON + HOUR), null);
  ({ record } = file(record, [moved!]));
  assert.equal(record.findings.length, 2);
});

test('another theory case, and another assertion with the same first line, are new fingerprints', () => {
  const two = sightings(trxOf([
    { name: 'N.Tests.WidgetTests.Adds(n: 1)', outcome: 'Failed', message: 'Assert.Equal() Failure: Values differ', line: 3 },
    { name: 'N.Tests.WidgetTests.Adds(n: 2)', outcome: 'Failed', message: 'Assert.Equal() Failure: Values differ', line: 3 },
  ]), 'c1', at(NOON));
  assert.notEqual(bg.fingerprintKey(two[0]!.fingerprint), bg.fingerprintKey(two[1]!.fingerprint), 'the case is kept');
  const assertions = sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c1', at(NOON))
    .concat(sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 8 }]), 'c1', at(NOON)));
  assert.equal(assertions[0]!.fingerprint.message, assertions[1]!.fingerprint.message, 'the same first line');
  assert.notEqual(bg.fingerprintKey(assertions[0]!.fingerprint), bg.fingerprintKey(assertions[1]!.fingerprint), 'two locations');
  const { record } = file({ schema: 1, findings: [] }, [...two, ...assertions]);
  assert.equal(record.findings.length, 4);
});

test('two runs of one failure whose messages differ by a GUID, a hex id and an actual value are one failure, matched by signature', () => {
  const first = sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', line: 6, message: 'System.InvalidOperationException : session 4f3c2a1b-9d8e-4c7b-a6f5-0e1d2c3b4a59 tree a2762239ff00 held 12 rows' }]), 'c1', at(NOON));
  const second = sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', line: 6, message: 'System.InvalidOperationException : session 0d1e2f3a-4b5c-4d6e-8f70-81a2b3c4d5e6 tree 9b81c4d2ee17 held 13 rows' }]), 'c2', at(NOON + HOUR));
  const filed = file({ schema: 1, findings: [] }, [...first, ...second]);
  assert.deepEqual(filed.how, ['new', 'seen']);
  assert.equal(filed.record.findings.length, 1);
  assert.equal(filed.record.findings[0]!.lastSeen.commit, 'c2', 'its last seen moved');
  assert.match(filed.record.findings[0]!.lastSeen.message, /0d1e2f3a/, 'its last seen message updated');
  assert.equal(filed.record.findings[0]!.firstSeen.commit, 'c1');
});

test('expiry counts per test from its first sighting: a direct commit that moves the assertion, or changes it, renews nothing', () => {
  const t0 = NOON;
  let { record } = file({ schema: 1, findings: [] }, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c1', at(t0)));
  // Fifty hours on, a direct commit adds two lines above the assertion: the same signature, its last seen moved, its since kept.
  const shifted = WIDGET.replace('  [Fact] public void Opens() {', '  [Fact] public void Opens() {\n    // one\n    // two');
  ({ record } = file(record, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 9 }]), 'c5', at(t0 + 50 * HOUR), shifted)));
  assert.equal(record.findings.length, 1);
  assert.equal(record.findings[0]!.since, at(t0));
  // Sixty hours on, another changes the assertion's text: a new finding, its expiry still the test's first sighting.
  const changed = WIDGET.replace('    Assert.True(store.Ready);', '    Assert.True(store.Ready, "ready");');
  ({ record } = file(record, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c6', at(t0 + 60 * HOUR), changed)));
  assert.equal(record.findings.length, 2);
  assert.equal(record.findings[1]!.since, at(t0), 'counted from the first sighting of the test under any fingerprint');
  assert.equal(bg.knownRedStanding(record.findings[1]!, t0 + 61 * HOUR).warn, true, 'twelve hours before, the watch warns');
  assert.match(bg.knownRedLines(record, t0 + 61 * HOUR).join('\n'), /known red expiring/);
  for (const finding of record.findings) assert.equal(bg.knownRedStanding(finding, t0 + 73 * HOUR).expired, true);
  const [again] = sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c7', at(t0 + 73 * HOUR), changed);
  assert.equal(bg.knownRed(record, again!, t0 + 73 * HOUR), null, 'expired: no longer inherited');
});

test('a known-red browser spec and a known-red rehearsal past 72 hours are said by the watch', () => {
  let record: Findings = { schema: 1, findings: [] };
  for (const gate of ['web-e2e', 'rehearse-family']) {
    const signature = failures.gateSignature(gate, 'run', `Error: ${gate} failed at step 4`);
    ({ record } = bg.fileSighting(record, { gate, unit: gate, test: gate, fingerprint: { test: gate, exception: '', at: null, message: 'x' }, signature, commit: 'c1', at: at(NOON), kind: 'nightly' }));
  }
  const lines = bg.knownRedLines(record, NOON + 73 * HOUR);
  assert.equal(lines.length, 2);
  assert.match(lines[0]!, /known red past 72 hours: web-e2e, known red since .*: no longer inherited; the stage refuses it/);
  assert.match(lines[1]!, /rehearse-family/);
  assert.deepEqual(bg.knownRedLines(record, NOON + 10 * HOUR), [], 'nothing said before the warning');
});

test('clearing by lineage: an older run finishing late and a bisection on a passing ancestor clear nothing; a contradicted clearing keeps the first sighting', () => {
  // main's first-parent line: c1 - c2 - c3 - c4. The test fails on c3, first seen on c2.
  const line = ['c1', 'c2', 'c3', 'c4'];
  const descends = (from: string, to: string): boolean => line.indexOf(to) >= line.indexOf(from);
  let { record } = file({ schema: 1, findings: [] }, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c2', at(NOON)));
  ({ record } = file(record, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c3', at(NOON + HOUR))));
  const passes = bg.passingTests(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Passed' }]));
  const passed = (finding: Finding): boolean => passes.has(finding.test);
  // A background run on c2 that started before c3's and finishes late: c2 does not descend from c3.
  let cleared = bg.clearPasses(record, { commit: 'c2', at: at(NOON + 2 * HOUR), kind: 'ordinary', passed, descends });
  assert.equal(cleared.cleared.length, 0);
  // A bisection's run on a passing ancestor clears nothing, whatever its commit.
  cleared = bg.clearPasses(record, { commit: 'c4', at: at(NOON + 2 * HOUR), kind: 'bisect', passed, descends });
  assert.equal(cleared.cleared.length, 0);
  // A pass on c4, a descendant, by a CI run, clears it.
  cleared = bg.clearPasses(record, { commit: 'c4', at: at(NOON + 3 * HOUR), kind: 'ordinary', passed, descends });
  assert.equal(cleared.cleared.length, 1);
  record = cleared.record;
  assert.equal(record.findings[0]!.state, 'cleared');
  // Seen again within a day of the clearing: the clearing was contradicted, and the first sighting is kept.
  const filed = bg.fileSighting(record, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c4', at(NOON + 5 * HOUR))[0]!);
  assert.equal(filed.how, 'restored');
  assert.equal(filed.finding.firstSeen.commit, 'c2');
  assert.equal(filed.finding.since, at(NOON));
  assert.equal(filed.record.findings.length, 1);
  // Seen two days after a clearing: a new finding, its own first sighting.
  const later = bg.fileSighting(cleared.record, sightings(trxOf([{ name: 'N.Tests.WidgetTests.Opens', outcome: 'Failed', message: 'Assert.True() Failure', line: 7 }]), 'c4', at(NOON + 51 * HOUR))[0]!);
  assert.equal(later.how, 'new');
  assert.equal(later.finding.since, at(NOON + 51 * HOUR));
});

// ---------------------------------------------------------------------------------------------------
// The receipts and the stage history (§5.3)

test('a receipt is matched by tree, of a run that finished and of a counted kind; a sweep-only or unfinished run is said, never counted', () => {
  const receipts: Receipt[] = [
    { commit: 'c1', tree: 't1', kind: 'sweep', finished: true },
    { commit: 'c2', tree: 't2', kind: 'ordinary', finished: false, state: 'stopped' },
    { commit: 'c3', tree: 't3', kind: 'stage', finished: true, state: 'failed' },
    { commit: 'c4', tree: 't4', kind: 'ci', finished: true, state: 'green' },
  ];
  assert.match(bg.receiptFor(receipts, 't1').why ?? '', /only a sweep-only run finished on it/);
  assert.match(bg.receiptFor(receipts, 't2').why ?? '', /no background run on it finished/);
  assert.equal(bg.receiptFor(receipts, 't3').receipt?.commit, 'c3', 'a --ci --stage run that finished: its gates are judged apart');
  assert.equal(bg.receiptFor(receipts, 't4').receipt?.commit, 'c4');
  assert.match(bg.receiptFor(receipts, 't9').why ?? '', /no background run finished on it/);
  // The commit's name alone is not the match: c4 named on another tree is no receipt for it.
  assert.equal(bg.receiptFor([{ commit: 'c4', tree: 't-other', kind: 'ci', finished: true }], 't4').receipt, null);
});

test('the stage history says what a stage brings since the last, and an unknown range where it cannot', () => {
  const git = { isAncestor: (a: string, b: string): boolean => a === 'c1' && b === 'c3', commits: (): string[] => ['c3 three', 'c2 two'] };
  assert.match(bg.stageRangeLines(bg.stageRange([], 'c3', git)).join('\n'), /unknown \(no stage is recorded yet\)/);
  assert.match(bg.stageRangeLines(bg.stageRange([{ commit: 'c9', at: at(NOON) }], 'c3', git)).join('\n'), /unknown \(the last staged commit, c9, is not an ancestor of this one\)/);
  const lines = bg.stageRangeLines(bg.stageRange([{ commit: 'c1', at: at(NOON) }], 'c3', git));
  assert.match(lines[0]!, /since the last stage \(c1, .*\): 2 commits/);
  assert.deepEqual(lines.slice(1), ['  c3 three', '  c2 two']);
});

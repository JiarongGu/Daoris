import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync, type ChildProcess } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { makeFixture, PATIENCE } from '../test/_fixture.ts';

/**
 * CISCOPE1e (D207, design §4 and §5.1, §8 row 5): the merge queue and its candidates, the frozen request, the judgement of a red
 * candidate over its base and every prefix by the failure's signature, a death of memory kept out of the base run and the stop,
 * the records-only re-form, resume by ancestry, the scoped set with the git half's budget, and the admission record both runners
 * share (`tools/merge-queue.mjs`). The merge tool's verbs over them are in `merge-branch.test.ts`. Run by the `tool-tests` gate.
 */

interface Signature { test?: string; exception?: string; location?: { method: string; text: string; ordinal: number }; message?: string; gate?: string; step?: string; errors?: string[] }
type Outcome = { result: 'pass' } | { result: 'fail'; signature: Signature } | { result: 'cannot'; signature: Signature };
interface Failure { unit?: string; test?: string | undefined; gate: string; signature: Signature; names: 'test' | 'gate' }
interface ContainedIn { line?: boolean; branch?: string; request?: string | undefined; commit?: string }
interface Member { branch: string; tip: string; request?: string; seq?: number; message?: string; commit?: string; at?: string; containedIn?: ContainedIn; handedBack?: { how: string; at?: string; failure?: string; with?: string[]; interacting?: string[]; paths?: string[]; why?: string } }
interface Judged { outcome: string; member?: number; with?: number[]; interacting?: number[]; why?: string; said?: string; runs: unknown[]; confirm?: Confirm }
interface Evidence { conclusive: boolean; doubts: { at: string; why: string }[]; waitedMs?: number | null; ids?: string[] }
interface Confirm { result: string; why?: string; evidence?: Evidence | null; composition?: { units: string[] | null; digest: string | null } | null; failures?: Failure[]; error?: unknown }
type Whole = { held: true; ids: string[]; end: () => Evidence } | { held: false; dropped: boolean; why: string; evidence: Evidence };
interface Candidate { base: string; tip: string; members: Member[]; contained?: Member[]; state: string; reforms: number; recordsOnly: boolean; held?: { on: string; why: string }; run?: { pid: number } }
interface Queue {
  schema: number; queued: Member[]; previews: Record<string, { tip: string }>; candidate: Candidate | null; stop: unknown; handedBack: Member[]; landed?: Member[]; drained?: Drained;
  forming?: { base: string; members: Member[] } | null; contained?: Member[]; runner?: unknown;
}
type Standing = { landed?: Member; contained?: Member; back?: Member; pending?: string; gone?: boolean };
interface Reservation { runner: string; gate: string; needGb: number; job: string }
interface Admission { schema: number; reservations: Reservation[]; scopedTier: { state: string; since: string } | null; waiting: unknown[] }
interface Job { state: string; workingGb?: number; committedGb?: number; why?: string }
interface Rule { lane?: string; paths?: string[]; gates: string[] | '*' }
interface Drained { tip: string; at: string; ended: string[]; left: string[]; probe: string | null; strays: number[] }
/** INTEGRATE1b part 2: a confirm run as the scope run's record keeps it. */
interface ConfirmRecord { gate: string; result: string; why: string; composition: { units: string[] | null; digest: string | null } | null; admission: (Evidence & { capacity?: number }) | null; waitedMs: number | null; ms: number | null }
interface ScopeRun {
  state: string; tip: string; tree: string; members: { branch: string }[]; outcome?: string; judged?: { test: string; outcome: string; member: number | null; why: string | null }[];
  confirms?: ConfirmRecord[]; policy?: { unconfirmed: string; said: string }; passedOnConfirm?: string[]; drained?: Drained;
}
interface ProcessRow { pid: number; ppid: number; command: string }
interface Trees { ended: string | null; hold: (pid: number) => void; release: (pid: number) => boolean; end: (why: string) => void }
interface Watch { trees: Trees; check: () => boolean; start: () => Watch; stop: () => void }

const q = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/merge-queue.mjs') as {
  CANDIDATE_MAX: number;
  RECHECK_MS: number;
  HELD_REFORMS: number;
  FOREGROUND_HEADROOM_GB: number;
  BACKGROUND_SLOT_WAIT_MS: number;
  emptyQueue: () => Queue;
  readQueue: (home: string) => Queue;
  writeQueue: (home: string, queue: Queue) => void;
  notePreview: (queue: Queue, preview: { branch: string; tip: string; base: string; at: string }) => Queue;
  enqueue: (queue: Queue, request: { branch: string; tip: string; message: string | null; at: string; requeue?: boolean }) => { queue?: Queue; entry?: Member; refused?: string };
  messageOf: (entry: Member) => string;
  dropQueued: (queue: Queue, branches: string[], at?: string) => { queue: Queue; dropped: string[]; ended: Candidate | null };
  requestStanding: (queue: Queue, request: { branch: string; tip: string; request?: string }) => Standing;
  drainedRecord: (candidate: Candidate, what: { ended?: string[]; left?: string[]; probe?: string | null; strays?: number[] }, at?: string) => Drained;
  drainedLine: (drained: Drained) => string;
  sameCandidate: (current: Candidate | null, expected: Candidate) => boolean;
  changeCandidate: (queue: Queue, expected: Candidate, change: (queue: Queue) => { queue: Queue }) => { queue?: Queue; obsolete?: boolean };
  nextMembers: (queued: Member[], options?: { max?: number; oneAtATime?: boolean }) => { members: Member[]; rest: Member[] };
  formCandidate: (input: { base: string; members: Member[]; merge: (member: Member, onto: string, formed: Member[]) => Promise<{ commit?: string; conflict?: string[]; contained?: ContainedIn }>; at: string }) => Promise<{ candidate: Candidate | null; setAside: Member[]; contained: Member[] }>;
  IN_THE_LINE: ContainedIn;
  inArrival: (requests: Member[]) => Member[];
  requestsOf: (candidate: Candidate | null, requests?: Member[]) => Member[];
  settleContained: (candidate: Candidate | null, at: string) => { branch: string; tip: string; request?: string; containedIn: ContainedIn; at: string }[];
  recordsOnly: (paths: string[], ruleOf: (path: string) => Rule | null) => boolean;
  resumeCandidate: (candidate: Candidate | null, facts: { mainTip: string; isAncestor: (a: string, b: string) => boolean; runAlive?: (pid?: number) => boolean }) => string;
  promotion: (candidate: Candidate, facts: { mainTip: string; clean: boolean }) => { action: string; why?: string };
  reformCount: (candidate: Candidate, facts: { recordsOnlyMove: boolean }) => { reforms: number; recordsOnly: boolean; heldByDirect: boolean };
  placeOnPrefixes: (seq: Outcome[], signature: Signature) => { outcome: string; member?: number; interacting?: number[]; why?: string };
  judgeFailure: (failure: Failure, context: { members: number; run: (failure: Failure, k: number) => Promise<Outcome>; alone?: (failure: Failure, k: number) => Promise<Outcome>; known?: { expired: boolean; lastSeen: string } | null; baseCommit?: string; confirm?: Confirm | (() => Confirm | Promise<Confirm>) }) => Promise<Judged>;
  memoryStep: (attempts: { died: boolean; peakGb?: number }[], options?: { afterHold?: boolean; needGb?: number }) => { next: string; note?: string };
  candidateOutcome: (judgements: { failure: Failure; judged: Judged }[], members: Member[], policy?: { unconfirmed?: string | undefined }) => {
    action: string; handedBack?: Member[]; retain?: Member[]; inherited?: Failure[]; flakes?: Failure[]; why?: string; on?: string;
    sightings?: { failure: Failure; confirm: Confirm }[]; inconclusive?: Failure[]; passedOnConfirm?: string[];
  };
  confirmStep: (input: { take: (request: { what: string; halted: () => boolean }) => Promise<Whole>; run: () => Promise<unknown>; halted?: () => boolean; what?: string }) => Promise<Confirm>;
  confirmReadings: (confirm: Confirm, failures: Failure[]) => { failure: Failure; confirm: Confirm }[];
  INTEGRATION_POLICY: string;
  INTEGRATION_SIGHTINGS: string;
  readIntegrationPolicy: (root: string) => { unconfirmed: string; said: string };
  sightingOf: (input: { failure: Failure; candidate: { base: string; tip: string; members: Member[] }; confirm: Confirm; policy: string; at: string }) => Record<string, unknown>;
  appendSighting: (root: string, sighting: object) => void;
  readSightings: (root: string) => Record<string, unknown>[];
  recheckDue: (stop: { at: string; rechecks?: { at: string }[] } | null, now: number) => boolean;
  noteRecheck: (stop: { at: string }, recheck: { at: string; passed: boolean; by: string }) => unknown;
  gitBudget: (input: { classes: { name: string; ms?: number; subject?: boolean; failures?: number; knownRed?: boolean }[]; overheadMs?: number; budgetMs?: number; lanes?: number }) => { run: string[]; deferred: string[]; predictedMs: number; overrun: boolean };
  scopedSet: (input: { gates: { name: string; kind: string }[]; reached: Set<string>; standing?: Set<string>; knownRed?: Set<string>; recordsOnly?: boolean }) => { run: { name: string; why: string }[]; kept: { name: string }[]; deferred: { name: string; why: string }[] };
  appendScopeRun: (home: string, entry: object) => void;
  readScopeRuns: (home: string) => object[];
  emptyAdmission: () => Admission;
  admit: (input: { record: Admission; request: { runner: string; gate: string; lane: string; needGb: number; waitingSince?: string }; free: { physicalGb: number; commitGb: number }; readJob: (name: string) => Job; now?: number; betweenTiers?: boolean }) => { admit: boolean; wait: string | null; why: string; record: Admission; said: string[] };
  reserve: (record: Admission, input: { runner: string; run: string; gate: string; lane: string; needGb: number; pid: number; at: string }) => { record: Admission; job: string };
  scopedTier: (record: Admission, state: string | null, at: string) => Admission;
  overdueWaits: (record: Admission, now: number) => string[];
  queueLines: (queue: Queue, options?: { branchTip?: (branch: string) => string | null; now?: number }) => string[];
  runnerStanding: (queue: Queue, pid: number) => { stands: true } | { stands: false; why: string };
  censusTrees: (home: string) => string[];
  orphansUnder: (processes: ProcessRow[], options: { trees: string[]; ignore?: number[] }) => ProcessRow[];
  censusLine: (orphans: ProcessRow[], options?: { limit?: number; width?: number }) => string;
};
const failures = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/test-failures.mjs') as { gateSignature: (gate: string, step: string, output: string) => Signature; signatureKey: (signature: Signature) => string };
const machine = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/machine-admission.mjs') as {
  admissionFile: (home: string) => string;
  createAdmission: (options: { daorisHome: string; capacity?: number; say?: (line: string) => void }) => { take: (request: { what: string }) => Promise<{ id: string; release: () => void } | null> };
};

const tool = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/merge-branch.mjs') as {
  REACH: Rule[];
  ruleFor: (path: string, rules: Rule[], lanes: unknown[]) => Rule | null;
  readLanes: (root: string) => { lanes: unknown[] } | null;
  endTree: (pid: number) => void;
  stepTrees: (options?: { end?: (pid: number) => void }) => Trees;
  watchCandidate: (candidate: Candidate, current: () => Candidate | null, trees: Trees, options?: { ms?: number; superseded?: () => string | null; taken?: () => void }) => Watch;
  ownerEnded: (environment?: Record<string, string | undefined>, alive?: (pid: number) => boolean) => string | null;
  prefixRunner: (tree: string, candidate: { base: string; members: Member[] }, plan: { name: string; run: string }[], dir: string, watch?: Watch | null) => (failure: Failure, k: number) => Promise<Outcome>;
  queueHomeRefusal: (facts: { root: string; home: string; tool?: string | null; sameRepository?: boolean; run?: boolean }) => string | null;
  treeBindingRefusal: (facts: { path: string; reported: string | null }) => string | null;
  gateEnvironment: (start?: Record<string, string | undefined>, options?: { pass?: readonly string[] }) => Record<string, string>;
  GATE_REMOVED: readonly (string | RegExp)[];
  waitSaid: (standing: Standing, facts: { branch: string; request?: string; holder?: string | boolean | null }) => { code: number; lines: string[] } | null;
  BESIDE_ENVIRONMENT: Record<string, string>;
  DRAIN_WAIT_MS: number;
};

const procs = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/processes.mjs') as {
  parseProcessRows: (text: string, form: 'rows' | 'ps') => ProcessRow[];
  parseCreatedRows: (text: string) => (ProcessRow & { created: number })[];
  strandedRoots: (processes: (ProcessRow & { created: number })[], options: { folder: string; since: number; except?: number[] }) => (ProcessRow & { created: number })[];
  endStrandedUnder: (folder: string, since: number, options?: { except?: number[]; platform?: string; run?: (script: string) => string; end?: (pid: number) => void }) => number[];
};

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const AT = '2026-10-24T10:00:00.000Z';
const NOW = Date.parse(AT);

const sig = (assertion: string, test = 'Driver.Tests.SessionTests.Resumes'): Signature => ({ test, exception: 'Xunit.Sdk.EqualException', location: { method: 'Resumes', text: assertion, ordinal: 0 } });
const fail = (signature: Signature): Outcome => ({ result: 'fail', signature });
const PASS: Outcome = { result: 'pass' };
const testFailure = (signature: Signature): Failure => ({ unit: 'SessionTests', test: signature.test, gate: 'driver', signature, names: 'test' });

/** A runner over a fixed answer per prefix (and per second run, where given), keeping the order it was asked. */
function prefixes(answers: Outcome[], second: Partial<Record<number, Outcome>> = {}) {
  const asked: number[] = [];
  const seen = new Map<number, number>();
  return {
    asked,
    run: async (_failure: Failure, k: number) => {
      asked.push(k);
      const times = (seen.get(k) ?? 0) + 1;
      seen.set(k, times);
      return (times > 1 && second[k] ? second[k] : answers[k]) as Outcome;
    },
  };
}

const members = (n: number): Member[] => Array.from({ length: n }, (_, i) => ({ branch: `b${i + 1}`, tip: `t${i + 1}`.padEnd(40, '0') }));

describe('the queue: a frozen request, read before it is queued', () => {
  test('a branch queued without a message is refused', () => {
    const queue = q.notePreview(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), base: 'm', at: AT });
    assert.match(q.enqueue(queue, { branch: 'b1', tip: 'a'.repeat(40), message: null, at: AT }).refused ?? '', /--message <file>/);
  });

  test("a tip that differs from the branch's last preview is refused, saying both; a records commit made before the preview is queued", () => {
    let queue = q.notePreview(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), base: 'm', at: AT });
    // The records commit after the preview moved the tip.
    const refused = q.enqueue(queue, { branch: 'b1', tip: 'b'.repeat(40), message: 'Merge b1\n', at: AT }).refused ?? '';
    assert.match(refused, /bbbbbbb/);
    assert.match(refused, /aaaaaaa/);
    // In the order records commit, preview, queue, the previewed tip is the queued tip.
    queue = q.notePreview(queue, { branch: 'b1', tip: 'b'.repeat(40), base: 'm', at: AT });
    const queued = q.enqueue(queue, { branch: 'b1', tip: 'b'.repeat(40), message: 'Merge b1\n', at: AT });
    assert.equal(queued.refused, undefined);
    assert.equal(queued.queue!.queued.length, 1);
  });

  test('a branch never previewed is refused', () => {
    assert.match(q.enqueue(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), message: 'x', at: AT }).refused ?? '', /--preview/);
  });

  test('QUEUEWAIT1: two enqueue occurrences of one tip in the same clock tick have different identities', () => {
    const preview = q.notePreview(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), base: 'm', at: AT });
    const first = q.enqueue(preview, { branch: 'b1', tip: 'a'.repeat(40), message: 'm', at: AT }).entry!;
    const history = { ...preview, handedBack: [{ ...first, handedBack: { how: 'unresolved' } }] };
    const next = q.enqueue(history, { branch: 'b1', tip: first.tip, message: 'm', at: AT });
    assert.ok(first.request);
    assert.ok(next.entry!.request);
    assert.notEqual(next.entry!.request, first.request);
    assert.deepEqual(next.queue!.handedBack, history.handedBack, 'the historical evidence is retained');
  });

  test('a branch and its message file changed while queued: the frozen request is what merges, and --queue says the branch moved', () => {
    let queue = q.notePreview(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), base: 'm', at: AT });
    queue = q.enqueue(queue, { branch: 'b1', tip: 'a'.repeat(40), message: 'Merge b1: the first words\n\nCo-Authored-By: X <x@y>\n', at: AT }).queue!;
    const entry = queue.queued[0]!;
    assert.equal(entry.tip, 'a'.repeat(40));
    assert.equal(q.messageOf(entry), 'Merge b1: the first words\n\nCo-Authored-By: X <x@y>\n');
    const lines = q.queueLines(queue, { branchTip: () => 'c'.repeat(40), now: NOW }).join('\n');
    assert.match(lines, /now at ccccccc: the frozen tip is what merges/);
  });

  test('a branch queued twice is refused; --drop-batch takes it off, and ends a candidate, handing its members back', () => {
    let queue = q.notePreview(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), base: 'm', at: AT });
    queue = q.enqueue(queue, { branch: 'b1', tip: 'a'.repeat(40), message: 'm', at: AT }).queue!;
    assert.match(q.enqueue(queue, { branch: 'b1', tip: 'a'.repeat(40), message: 'm', at: AT }).refused ?? '', /queued already/);
    assert.deepEqual(q.dropQueued(queue, ['b1']).queue.queued, []);
    const running = { ...queue, queued: [], candidate: { base: 'm', tip: 'c', members: members(2), state: 'running', reforms: 0, recordsOnly: false } };
    const dropped = q.dropQueued(running, ['b2']);
    assert.equal(dropped.queue.candidate, null);
    assert.deepEqual(dropped.dropped, ['b1', 'b2']);
    assert.deepEqual(dropped.queue.handedBack.map((each) => each.handedBack?.how), ['dropped', 'dropped']);
  });

  test('QUEUEDROP1: every late outcome is refused for a dropped or replaced enqueue, including the same tip', () => {
    const expected: Candidate = {
      base: 'm', tip: 'c', members: [{ branch: 'b1', tip: 'b', commit: 'c', request: 'first' }],
      state: 'running', reforms: 0, recordsOnly: false,
    };
    const replacement: Candidate = { ...expected, members: [{ ...expected.members[0]!, request: 'second' }] };
    for (const current of [null, replacement, { ...expected, base: 'new-base' }, { ...expected, tip: 'new-tip' }]) {
      for (const state of ['green', 'stopped', 'held', 'split', 'red']) {
        const queue = { ...q.emptyQueue(), candidate: current };
        let called = false;
        const result = q.changeCandidate(queue, expected, (record) => {
          called = true;
          return { queue: { ...record, candidate: { ...expected, state }, stop: state === 'stopped' ? 'late stop' : null } };
        });
        assert.deepEqual(result, { obsolete: true });
        assert.equal(called, false, `${state} must not enter the write callback`);
        assert.equal(queue.candidate, current);
        assert.equal(queue.stop, null);
      }
    }
    const current = { ...expected, members: expected.members.map((member) => ({ ...member })), state: 'bisecting' };
    assert.equal(q.sameCandidate(current, expected), true, 'state changes do not change the frozen request');
    const result = q.changeCandidate({ ...q.emptyQueue(), candidate: current }, expected,
      (queue) => ({ queue: { ...queue, candidate: { ...current, state: 'green' } } }));
    assert.equal(result.queue!.candidate!.state, 'green');
  });

  test('QUEUEDRAIN1: a drop answers the candidate it ended, and a queued branch ends none; what a drain ended is said whole', () => {
    const queue = { ...q.emptyQueue(), queued: members(1), candidate: { base: 'm', tip: 'c', members: members(2).map((each) => ({ ...each, branch: `c${each.branch}` })), state: 'running', reforms: 0, recordsOnly: false } };
    assert.equal(q.dropQueued(queue, ['b1']).ended, null, 'a queued branch has no run to end');
    assert.equal(q.dropQueued(queue, ['cb2']).ended, queue.candidate);
    const drained = q.drainedRecord(queue.candidate, { ended: ['check', 'driver'], left: ['modules'], probe: 'driver.prefix-2' }, AT);
    assert.deepEqual(drained, { tip: 'c', at: AT, ended: ['check', 'driver'], left: ['modules'], probe: 'driver.prefix-2', strays: [] });
    assert.equal(q.drainedLine(drained), 'ended gates check, driver, the probe driver.prefix-2, each with everything it started; never started: modules');
    assert.equal(q.drainedLine(q.drainedRecord(queue.candidate, {}, AT)), 'ended no gate running, each with everything it started');
    // QUEUEDROPLEAK1: what a test had left running is said with the gates; a record written before it was kept says nothing of it.
    assert.equal(q.drainedLine(q.drainedRecord(queue.candidate, { ended: ['check'], strays: [11, 12] }, AT)), 'ended gates check, each with everything it started, and 2 processes their tests had left running under the scope tree (pid 11, 12)');
    assert.equal(q.drainedLine(q.drainedRecord(queue.candidate, { strays: [11] }, AT)), 'ended no gate running, each with everything it started, and 1 process their tests had left running under the scope tree (pid 11)');
    const { strays: _kept, ...older } = drained;
    assert.equal(q.drainedLine(older as Drained), 'ended gates check, driver, the probe driver.prefix-2, each with everything it started; never started: modules');
  });

  test('QUEUEWAITEND1: a request dropped while queued or forming is handed back as dropped, so its waiter has an outcome; a drop leaves the rest forming', () => {
    let queue = q.notePreview(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), base: 'm', at: AT });
    queue = q.enqueue(queue, { branch: 'b1', tip: 'a'.repeat(40), message: 'm', at: AT }).queue!;
    const request = queue.queued[0]!.request;
    const off = q.dropQueued(queue, ['b1'], AT);
    assert.deepEqual(off.dropped, ['b1']);
    assert.deepEqual(off.queue.handedBack.map((each) => [each.branch, each.request, each.handedBack?.how]), [['b1', request, 'dropped']]);
    const forming = { ...q.emptyQueue(), forming: { base: 'm', members: members(2).map((each, i) => ({ ...each, request: `r${i + 1}` })) } };
    const one = q.dropQueued(forming, ['b2'], AT);
    assert.deepEqual(one.dropped, ['b2']);
    assert.deepEqual(one.queue.forming!.members.map((each) => each.branch), ['b1'], 'the rest stay forming');
    assert.deepEqual(one.queue.handedBack.map((each) => [each.branch, each.handedBack?.how]), [['b2', 'dropped']]);
    assert.equal(q.dropQueued(one.queue, ['b1'], AT).queue.forming, null, 'a form left with no request is none');
    const previewed = q.notePreview(forming, { branch: 'b1', tip: 't1'.padEnd(40, '0'), base: 'm', at: AT });
    assert.match(q.enqueue(previewed, { branch: 'b1', tip: 't1'.padEnd(40, '0'), message: 'x', at: AT }).refused ?? '', /b1 is queued already/, 'a branch forming is queued already');
  });

  test('QUEUEWAITEND1: where a request stands, for its waiter: an outcome, in flight, or in no record at all', () => {
    const asked = { branch: 'b1', tip: 't1', request: 'r1' };
    const it = { ...asked, at: AT };
    const candidate = { base: 'm', tip: 'c', members: [{ ...it, commit: 'c1' }], state: 'running', reforms: 0, recordsOnly: false };
    const empty = q.emptyQueue();
    assert.deepEqual(q.requestStanding({ ...empty, landed: [{ ...it, commit: 'c1' }] }, asked).landed?.commit, 'c1');
    assert.ok(q.requestStanding({ ...empty, contained: [it] }, asked).contained);
    assert.equal(q.requestStanding({ ...empty, handedBack: [{ ...it, handedBack: { how: 'dropped' } }] }, asked).back?.handedBack?.how, 'dropped');
    assert.equal(q.requestStanding({ ...empty, queued: [it] }, asked).pending, 'queued');
    assert.equal(q.requestStanding({ ...empty, forming: { base: 'm', members: [it] } }, asked).pending, 'forming');
    assert.equal(q.requestStanding({ ...empty, candidate }, asked).pending, 'candidate');
    assert.equal(q.requestStanding({ ...empty, candidate: { ...candidate, members: [], contained: [it] } }, asked).pending, 'contained');
    assert.equal(q.requestStanding({ ...empty, queued: [{ ...it, request: 'r2' }], handedBack: [{ ...it, request: 'r0', handedBack: { how: 'pinned' } }] }, asked).gone, true,
      'another occurrence of the same tip is not this request');
  });

  test("QUEUEWAITEND1: what a waiter says as it ends: in main (0), contained (0), handed back or dropped (1), or gone from the queue's records (1)", () => {
    const it = { branch: 'b1', tip: 't1', request: 'r1aaaaaaaaa', at: AT };
    assert.equal(tool.waitSaid({ pending: 'queued' }, { branch: 'b1' }), null, 'a request in flight is waited for');
    assert.equal(tool.waitSaid({ landed: { ...it, commit: 'c1c1c1c1c1' } }, { branch: 'b1' })!.code, 0);
    assert.match(tool.waitSaid({ contained: { ...it, containedIn: { line: true } } }, { branch: 'b1' })!.lines[0]!, /b1 is contained: main holds it already/);
    const inLine = tool.waitSaid({ contained: { ...it, containedIn: { branch: 'line', request: 'r0', commit: 'c0c0c0c0c0' } } }, { branch: 'b1' })!;
    assert.equal(inLine.code, 0);
    assert.match(inLine.lines[0]!, /b1 is contained in line, which promoted \(c0c0c0c\)/, 'a container named `line` is named as the branch it is');
    const dropped = tool.waitSaid({ back: { ...it, handedBack: { how: 'dropped', at: AT } } }, { branch: 'b1' })!;
    assert.equal(dropped.code, 1);
    assert.match(dropped.lines.join('\n'), /b1's request was taken off the queue \(--drop-batch, 2026-10-24T10:00:00\.000Z\): nothing of it was merged by this request\. Preview it and queue it again to merge it\./);
    const inMain = tool.waitSaid({ back: { ...it, handedBack: { how: 'dropped', at: AT } } }, { branch: 'b1', holder: 'parent/batch' })!;
    assert.match(inMain.lines.join('\n'), /main holds its tip already, landed with parent\/batch: there is nothing to queue again/);
    assert.equal(tool.waitSaid({ back: { ...it, handedBack: { how: 'pinned', failure: 'check' } } }, { branch: 'b1' })!.code, 1);
    const gone = tool.waitSaid({ gone: true }, { branch: 'b1', request: it.request })!;
    assert.equal(gone.code, 1);
    assert.match(gone.lines.join('\n'), /b1's request \(r1aaaaaa\) is no longer in the queue's records: it is not queued, forming or on the candidate, and was neither landed, contained nor handed back\. This wait ends; --queue says what the queue holds\./);
  });

  test('QUEUEDRAIN1: the watch ends the run\'s step trees once the candidate is no longer the queue\'s, and a read that fails is not a drop', () => {
    const candidate: Candidate = { base: 'm', tip: 'c', members: [{ branch: 'b1', tip: 'b', commit: 'c', request: 'first' }], state: 'running', reforms: 0, recordsOnly: false };
    const ended: number[] = [];
    const trees = tool.stepTrees({ end: (pid) => { ended.push(pid); } });
    trees.hold(101);
    trees.hold(102);
    let current: Candidate | null = { ...candidate, state: 'bisecting' };
    let unreadable = false;
    // DROPWAIT1: the take is said before anything is ended, and once.
    const taken: number[] = [];
    const watch = tool.watchCandidate(candidate, () => {
      if (unreadable) throw new Error('the queue is locked');
      return current;
    }, trees, { taken: () => { taken.push(ended.length); } });
    assert.equal(watch.check(), false, 'a state change is not a drop');
    unreadable = true;
    assert.equal(watch.check(), false, 'a record that cannot be read ends nothing');
    assert.deepEqual([ended, taken], [[], []]);
    unreadable = false;
    current = null;
    assert.equal(watch.check(), true);
    assert.deepEqual(ended, [101, 102], 'every step held, and only those');
    assert.equal(trees.ended, 'the candidate was dropped or replaced');
    current = candidate;
    assert.equal(watch.check(), true, 'once ended it stays ended');
    assert.deepEqual([ended, taken], [[101, 102], [0]], 'ended once, taken once before');
    const failing = tool.watchCandidate(candidate, () => null, tool.stepTrees({ end: () => {} }), { taken: () => { throw new Error('the queue is locked'); } });
    assert.equal(failing.check(), true, 'a take that cannot be said still ends the run');
    const replaced = tool.watchCandidate(candidate, () => ({ ...candidate, members: [{ ...candidate.members[0]!, request: 'second' }] }), tool.stepTrees({ end: () => {} }));
    assert.equal(replaced.check(), true, 'the same tip queued again is another request');
  });

  test('the record survives a write and a read', () => {
    mkdirSync(join(repoRoot, '_fixtures'), { recursive: true });
    const home = mkdtempSync(join(repoRoot, '_fixtures', 'merge-queue-'));
    try {
      const queue = q.notePreview(q.emptyQueue(), { branch: 'b1', tip: 'a'.repeat(40), base: 'm', at: AT });
      q.writeQueue(home, queue);
      assert.equal(q.readQueue(home).previews.b1!.tip, 'a'.repeat(40));
      q.appendScopeRun(home, { base: 'm', members: ['b1'], verdict: 'green' });
      assert.equal(q.readScopeRuns(home).length, 1);
    } finally {
      rmSync(home, { recursive: true, force: true });
    }
  });
});

describe('the candidate', () => {
  test('two branches in one candidate, one merge commit each on the last', async () => {
    const { members: chosen } = q.nextMembers(members(2));
    const formed = await q.formCandidate({ base: 'm0', members: chosen, merge: async (member, onto) => ({ commit: `${onto}+${member.branch}` }), at: AT });
    assert.equal(formed.candidate!.tip, 'm0+b1+b2');
    assert.deepEqual(formed.candidate!.members.map((each) => each.commit), ['m0+b1', 'm0+b1+b2']);
  });

  test('at most eight, in arrival order; one where --one-at-a-time', () => {
    const { members: eight, rest } = q.nextMembers(members(10));
    assert.equal(eight.length, q.CANDIDATE_MAX);
    assert.deepEqual(rest.map((each) => each.branch), ['b9', 'b10']);
    assert.equal(q.nextMembers(members(3), { oneAtATime: true }).members.length, 1);
  });

  test('a conflict set aside mid-batch and handed back with its paths; the rest go on, and it is queued again later', async () => {
    const formed = await q.formCandidate({
      base: 'm0', members: members(3), at: AT,
      merge: async (member, onto) => (member.branch === 'b2' ? { conflict: ['TASKS.md.bak', 'src/x.ts'] } : { commit: `${onto}+${member.branch}` }),
    });
    assert.deepEqual(formed.candidate!.members.map((each) => each.branch), ['b1', 'b3']);
    assert.equal(formed.candidate!.tip, 'm0+b1+b3');
    assert.deepEqual(formed.setAside[0]!.handedBack, { how: 'conflict', paths: ['TASKS.md.bak', 'src/x.ts'], at: AT });
  });
});

// INTEGRATE1c (design §5.2 step 3, C4): a contained member is recorded as contained, never set aside; one an earlier member holds is
// provisional, settled only when that member promotes, and judged again where the candidate ends otherwise.
describe('a contained member (INTEGRATE1c)', () => {
  /** A merge that finds the `contained` members held by the line, and makes every other one's merge commit. */
  const inLine = (...contained: string[]) => async (member: Member, onto: string) => (contained.includes(member.branch) ? { contained: q.IN_THE_LINE } : { commit: `${onto}+${member.branch}` });
  /** A merge that finds the `contained` members held by the earlier member `by`, and makes every other one's merge commit. */
  const inMember = (by: string, ...contained: string[]) => async (member: Member, onto: string, formed: Member[]) => (contained.includes(member.branch)
    ? { contained: { branch: by, request: formed.find((each) => each.branch === by)?.request } }
    : { commit: `${onto}+${member.branch}` });

  test('one the line holds is contained and settled at once: it is no conflict, and no member of the candidate', async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(2), at: AT, merge: inLine('b1') });
    assert.deepEqual(formed.setAside, []);
    assert.deepEqual(formed.contained.map((each) => [each.branch, each.containedIn]), [['b1', { line: true }]]);
    assert.deepEqual(formed.candidate!.members.map((each) => each.branch), ['b2']);
    assert.equal(formed.candidate!.contained, undefined, 'the line already holds it, so the candidate waits on nothing');
    const alone = await q.formCandidate({ base: 'm0', members: members(1), at: AT, merge: inLine('b1') });
    assert.equal(alone.candidate, null, 'a candidate of members the line holds is no candidate');
    assert.equal(alone.contained.length, 1);
  });

  test("one an earlier member holds is provisional: on the candidate, outside its members, settled only at that member's promotion", async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(3), at: AT, merge: inMember('b1', 'b2') });
    assert.deepEqual(formed.candidate!.members.map((each) => each.branch), ['b1', 'b3'], "the judge's prefixes stay the merge commits");
    assert.deepEqual(formed.candidate!.contained!.map((each) => [each.branch, each.containedIn?.branch]), [['b2', 'b1']]);
    assert.deepEqual(formed.setAside, []);
    assert.deepEqual(q.settleContained(formed.candidate, AT).map((each) => [each.branch, each.containedIn]), [['b2', { branch: 'b1', request: undefined, commit: 'm0+b1' }]]);
    assert.deepEqual(q.settleContained({ ...formed.candidate!, members: formed.candidate!.members.slice(1) }, AT), [], 'a holder no longer a member settles nothing');
  });

  // The review's first finding on INTEGRATE1c: the line's marker was the word `line`, which a branch may be named.
  test('an earlier member whose branch is named `line` is a member, never the line: what it holds is provisional on it', async () => {
    const queued = [{ branch: 'line', tip: 't1'.padEnd(40, '0'), request: 'r1' }, { branch: 'b2', tip: 't2'.padEnd(40, '0'), request: 'r2' }];
    const formed = await q.formCandidate({ base: 'm0', members: queued, at: AT, merge: inMember('line', 'b2') });
    assert.deepEqual(formed.candidate!.contained!.map((each) => [each.branch, each.containedIn]), [['b2', { branch: 'line', request: 'r1' }]]);
    assert.deepEqual(q.settleContained({ ...formed.candidate!, members: [] }, AT), [], 'its container gone, nothing is settled');
    assert.deepEqual(q.settleContained(formed.candidate, AT).map((each) => each.containedIn), [{ branch: 'line', request: 'r1', commit: 'm0+line' }]);
    assert.match(q.queueLines({ ...q.emptyQueue(), candidate: formed.candidate }).join('\n'), /contained: b2 at t200000, in line \(settled when line promotes\)/);
  });

  test('a candidate that does not promote gives it back as its own request, in arrival order, to be judged again', async () => {
    const queued = members(3).map((each, i) => ({ ...each, request: `r${i + 1}`, at: `2026-10-24T10:00:0${i}.000Z`, message: 'x' }));
    const formed = await q.formCandidate({ base: 'm0', members: queued, at: AT, merge: inMember('b1', 'b2') });
    const retained = formed.candidate!.members.map(({ commit, ...request }) => request) as Member[];
    const again = q.requestsOf(formed.candidate, retained);
    assert.deepEqual(again.map((each) => each.branch), ['b1', 'b2', 'b3']);
    assert.ok(again.every((each) => each.containedIn === undefined && each.commit === undefined), 'the frozen request, nothing the candidate added');
    assert.deepEqual(again.map((each) => each.request), ['r1', 'r2', 'r3']);
    assert.deepEqual(q.requestsOf(null, retained).map((each) => each.branch), ['b1', 'b3']);
  });

  // The review's third finding on INTEGRATE1c: arrival was read off a clock of whole seconds.
  test('requests queued within one second are given back in the order they were queued: a sequence the queue assigns orders them, never the clock', async () => {
    let queue = q.emptyQueue();
    for (const [i, name] of ['b1', 'b2', 'b3'].entries()) {
      const tip = `t${i + 1}`.padEnd(40, '0');
      queue = q.notePreview(queue, { branch: name, tip, base: 'm0', at: AT });
      queue = q.enqueue(queue, { branch: name, tip, message: 'x', at: '2026-10-24T10:00:00Z' }).queue!;
    }
    const formed = await q.formCandidate({ base: 'm0', members: queue.queued, at: AT, merge: inMember('b1', 'b2') });
    const kept = formed.candidate!.members.map(({ commit, ...request }) => request) as Member[];
    assert.deepEqual(q.requestsOf(formed.candidate, kept).map((each) => each.branch), ['b1', 'b2', 'b3'], 'a split or a kept candidate leaves the queue in its order');
    // A request queued before the sequence (a record kept on a machine) is earlier than any that has one, and keeps its clock's order.
    const old = [{ branch: 'o2', tip: 'x', at: '2026-10-24T10:00:02Z' }, { branch: 'o1', tip: 'x', at: '2026-10-24T10:00:01Z' }];
    assert.deepEqual(q.inArrival([{ ...queue.queued[1]! }, ...old, { ...queue.queued[0]! }]).map((each) => each.branch), ['o1', 'o2', 'b1', 'b2']);
  });

  test('a branch contained on a candidate is queued already: it is not queued a second time', async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(2), at: AT, merge: inMember('b1', 'b2') });
    const tip = 't2'.padEnd(40, '0');
    const queue = q.notePreview({ ...q.emptyQueue(), candidate: formed.candidate }, { branch: 'b2', tip, base: 'm0', at: AT });
    assert.match(q.enqueue(queue, { branch: 'b2', tip, message: 'x', at: AT }).refused ?? '', /b2 is queued already/);
  });

  test("a drop that ends a candidate hands its contained member back with the rest; one dropped by name alone leaves the candidate standing", async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(3), at: AT, merge: inMember('b1', 'b2') });
    const queue = { ...q.emptyQueue(), candidate: formed.candidate };
    const all = q.dropQueued(queue, ['b1'], AT);
    assert.deepEqual(all.queue.handedBack.map((each) => [each.branch, each.handedBack!.how]), [['b1', 'dropped'], ['b3', 'dropped'], ['b2', 'dropped']]);
    assert.equal(all.queue.candidate, null);
    const one = q.dropQueued(queue, ['b2'], AT);
    assert.deepEqual(one.dropped, ['b2']);
    assert.equal(one.ended, null);
    assert.deepEqual(one.queue.candidate!.members.map((each) => each.branch), ['b1', 'b3']);
    assert.deepEqual(one.queue.candidate!.contained, []);
  });
});

describe('a red candidate, judged by its signature over the base and every prefix', () => {
  const s = sig('Assert.Equal(expected, actual);');

  test('a red candidate of four bisected to one member, every prefix run', async () => {
    const runner = prefixes([PASS, PASS, PASS, fail(s), fail(s)]);
    const judged = await q.judgeFailure(testFailure(s), { members: 4, run: runner.run });
    assert.equal(judged.outcome, 'pinned');
    assert.equal(judged.member, 3);
    assert.deepEqual(judged.interacting, []);
    assert.deepEqual([...new Set(runner.asked)].sort(), [0, 1, 2, 3, 4]);
  });

  test('a failure two members cause together: pinned with the first', async () => {
    const runner = prefixes([PASS, PASS, fail(s)]);
    const judged = await q.judgeFailure(testFailure(s), { members: 2, run: runner.run, alone: async () => PASS });
    assert.equal(judged.member, 2);
    assert.deepEqual(judged.with, [1]);
    const outcome = q.candidateOutcome([{ failure: testFailure(s), judged }], members(2));
    assert.equal(outcome.handedBack![0]!.handedBack!.how, 'pinned with');
    assert.deepEqual(outcome.handedBack![0]!.handedBack!.with, ['b1']);
  });

  test('a fix then a reintroduction (PASS, FAIL, FAIL, PASS, FAIL): the last transition handed back, the first named interacting', async () => {
    const judged = await q.judgeFailure(testFailure(s), { members: 4, run: prefixes([PASS, fail(s), fail(s), PASS, fail(s)]).run });
    assert.equal(judged.member, 4);
    assert.deepEqual(judged.interacting, [1]);
    const outcome = q.candidateOutcome([{ failure: testFailure(s), judged }], members(4));
    assert.deepEqual(outcome.handedBack!.map((each) => each.branch), ['b4']);
    assert.deepEqual(outcome.handedBack![0]!.handedBack!.interacting, ['b1']);
    assert.deepEqual(outcome.retain!.map((each) => each.branch), ['b1', 'b2', 'b3']);
  });

  test('a changed signature on a failing base: pinned on the candidate\'s signature', async () => {
    const old = sig('Assert.True(ok);');
    const judged = await q.judgeFailure(testFailure(s), { members: 3, run: prefixes([fail(old), fail(old), fail(s), fail(s)]).run });
    assert.equal(judged.outcome, 'pinned');
    assert.equal(judged.member, 2);
  });

  test('a partial fix of a known red: its changed signature pinned and handed back', async () => {
    const known = sig('Assert.Equal(3, count);');
    const partial = sig('Assert.Equal("done", state);');
    const judged = await q.judgeFailure(testFailure(partial), { members: 1, run: prefixes([fail(known), fail(partial)]).run, known: null, baseCommit: 'base' });
    // Its signature is not the known red's, so no known red matches it: the base runs, fails the old way, and the member is pinned.
    assert.equal(judged.outcome, 'pinned');
    assert.equal(q.candidateOutcome([{ failure: testFailure(partial), judged }], members(1)).handedBack![0]!.handedBack!.how, 'pinned');
  });

  test('an inherited failure on the base is filed at once and pinned to none, the queue going on', async () => {
    const judged = await q.judgeFailure(testFailure(s), { members: 3, run: prefixes([fail(s)]).run });
    assert.equal(judged.outcome, 'inherited');
    const outcome = q.candidateOutcome([{ failure: testFailure(s), judged }], members(3));
    assert.equal(outcome.action, 'green');
    assert.equal(outcome.inherited!.length, 1);
    assert.deepEqual(outcome.handedBack, []);
  });

  test('a known red last seen on this base skips its base run', async () => {
    const runner = prefixes([fail(s)]);
    const judged = await q.judgeFailure(testFailure(s), { members: 2, run: runner.run, known: { expired: false, lastSeen: 'b0' }, baseCommit: 'b0' });
    assert.equal(judged.outcome, 'inherited');
    assert.deepEqual(runner.asked, []);
  });

  test('a candidate meeting an expired known red stops the queue, every merge waiting', async () => {
    const judged = await q.judgeFailure(testFailure(s), { members: 2, run: prefixes([fail(s)]).run, known: { expired: true, lastSeen: 'b0' }, baseCommit: 'b0' });
    assert.equal(judged.outcome, 'stop');
    assert.equal(judged.why, 'expired');
    assert.equal(q.candidateOutcome([{ failure: testFailure(s), judged }], members(2)).action, 'stop');
  });

  test('a reproducible failure whose message carries a fresh GUID each run: matched by its location, pinned to its member, and inherited on a failing base', async () => {
    // The signature has no message where a location is read, so two runs' GUIDs never part it.
    const one = { ...sig('Assert.Equal(expected, id);'), };
    const again = JSON.parse(JSON.stringify(one)) as Signature;
    assert.equal((await q.judgeFailure(testFailure(one), { members: 2, run: prefixes([PASS, fail(again), fail(again)]).run })).member, 1);
    assert.equal((await q.judgeFailure(testFailure(one), { members: 2, run: prefixes([fail(again)]).run })).outcome, 'inherited');
  });

  test('a member that adds lines above a known-red assertion: the source text matches, the failure inherited and nobody pinned', async () => {
    // test-failures.mjs reads the location as the frame's method and its line's text, so the move leaves the signature equal.
    const judged = await q.judgeFailure(testFailure(s), { members: 2, run: prefixes([fail(sig('Assert.Equal(expected, actual);'))]).run });
    assert.equal(judged.outcome, 'inherited');
  });

  test('member 2 breaking a test and member 5 adding lines above it: member 2 pinned', async () => {
    const judged = await q.judgeFailure(testFailure(s), { members: 5, run: prefixes([PASS, PASS, fail(s), fail(s), fail(s), fail(s)]).run });
    assert.equal(judged.member, 2);
  });

  test('two independent failures: each placed, each member handed back, the rest judged again', async () => {
    const other = sig('Assert.Empty(errors);', 'Driver.Tests.TreeTests.Lands');
    const first = await q.judgeFailure(testFailure(s), { members: 3, run: prefixes([PASS, fail(s), fail(s), fail(s)]).run });
    const second = await q.judgeFailure(testFailure(other), { members: 3, run: prefixes([PASS, PASS, PASS, fail(other)]).run });
    const outcome = q.candidateOutcome([{ failure: testFailure(s), judged: first }, { failure: testFailure(other), judged: second }], members(3));
    assert.deepEqual(outcome.handedBack!.map((each) => each.branch), ['b1', 'b3']);
    assert.deepEqual(outcome.retain!.map((each) => each.branch), ['b2']);
  });

  test('a flake mid-bisection: the adjacent prefixes\' second run disagrees, so it is unresolved', async () => {
    const judged = await q.judgeFailure(testFailure(s), { members: 3, run: prefixes([PASS, PASS, fail(s), fail(s)], { 2: PASS }).run });
    assert.equal(judged.outcome, 'unresolved');
    assert.match(judged.why ?? '', /disagree on a second run/);
  });

  test('an unresolved failure whose re-run passes: never green, the candidate run one member at a time', async () => {
    const judged = await q.judgeFailure(testFailure(s), { members: 3, run: prefixes([PASS, PASS, PASS, PASS]).run });
    assert.equal(judged.outcome, 'unresolved');
    const outcome = q.candidateOutcome([{ failure: testFailure(s), judged }], members(3));
    assert.equal(outcome.action, 'split');
    assert.deepEqual(outcome.retain!.map((each) => each.branch), ['b1', 'b2', 'b3']);
    assert.equal(outcome.flakes!.length, 1);
  });

  test('an unresolved failure on a candidate of one: handed back as unresolved, its flake filed, the queue going on', async () => {
    const judged = await q.judgeFailure(testFailure(s), { members: 1, run: prefixes([PASS, PASS]).run });
    const outcome = q.candidateOutcome([{ failure: testFailure(s), judged }], members(1));
    assert.equal(outcome.action, 'hand back');
    assert.equal(outcome.handedBack![0]!.handedBack!.how, 'unresolved');
    assert.equal(outcome.flakes!.length, 1);
    assert.deepEqual(outcome.retain, []);
  });

  test('a prefix that does not build in a failing test\'s sequence (a member breaks compilation, a later one repairs it): unresolved', async () => {
    const build = failures.gateSignature('driver', 'build', 'src/X.cs(3,1): error CS0103: The name y does not exist');
    const judged = await q.judgeFailure(testFailure(s), { members: 3, run: prefixes([PASS, { result: 'cannot', signature: build }, fail(s), fail(s)]).run });
    assert.equal(judged.outcome, 'unresolved');
    assert.match(judged.why ?? '', /cannot run the selection/);
  });
});

/**
 * INTEGRATE1b part 1 (design §4.5, §5.3 step 3, §11 row b): the confirm run's pure pieces, which no queue path calls yet. The failing
 * gate is run once more as composed holding the whole admission; a failure that does not recur is a sighting and splits by default.
 */
describe('INTEGRATE1b: the confirm run, before any base or prefix run', () => {
  const s = sig('Assert.Equal(expected, actual);');
  const other = sig('Assert.Empty(errors);', 'Driver.Tests.TreeTests.Lands');
  const SHOWN: Evidence = { conclusive: true, doubts: [], waitedMs: 1200, ids: ['p-01', 'p-02', 'p-03'] };
  const BESIDE: Evidence = { conclusive: false, doubts: [{ at: 'beat', why: "another live hold stands beside it: session s-1's dotnet test" }], waitedMs: 0, ids: ['p-01'] };
  /** A whole admission that answers the evidence given, counting its takes and its ends. */
  function wholeOf(evidence: Evidence, refused: { dropped?: boolean; why?: string } | null = null) {
    const calls = { take: 0, end: 0 };
    return {
      calls,
      take: async (): Promise<Whole> => {
        calls.take += 1;
        if (refused) return { held: false, dropped: refused.dropped ?? false, why: refused.why ?? 'no home', evidence };
        return { held: true, ids: evidence.ids ?? [], end: () => { calls.end += 1; return evidence; } };
      },
    };
  }
  const composition = { units: ['SessionTests'], settings: { gate: 'driver' }, digest: 'sha256:abc' };

  test('a confirm run that passes holding the whole admission is unconfirmed, and its places are let go', async () => {
    const whole = wholeOf(SHOWN);
    const confirm = await q.confirmStep({ take: whole.take, run: async () => ({ result: 'pass', composition }) });
    assert.equal(confirm.result, 'unconfirmed');
    assert.deepEqual(confirm.evidence, SHOWN);
    assert.deepEqual(confirm.composition, { units: ['SessionTests'], digest: 'sha256:abc' });
    assert.deepEqual(whole.calls, { take: 1, end: 1 });
  });

  test('one that fails again is confirmed, with the failures it failed with', async () => {
    const whole = wholeOf(SHOWN);
    const confirm = await q.confirmStep({ take: whole.take, run: async () => ({ result: 'fail', failures: [testFailure(s)] }) });
    assert.equal(confirm.result, 'confirmed');
    assert.deepEqual(confirm.failures, [testFailure(s)]);
    assert.equal(whole.calls.end, 1);
  });

  test('inconclusive is judged first: beside another live hold, a failure again and a pass both read inconclusive', async () => {
    for (const answer of [{ result: 'fail', failures: [testFailure(s)] }, { result: 'pass' }]) {
      const confirm = await q.confirmStep({ take: wholeOf(BESIDE).take, run: async () => answer });
      assert.equal(confirm.result, 'inconclusive', answer.result);
      assert.match(confirm.why ?? '', /another live hold stands beside it/);
    }
  });

  test('an admission it could not take is inconclusive, and the gate is not run again', async () => {
    let ran = false;
    const confirm = await q.confirmStep({ take: wholeOf({ ...SHOWN, conclusive: false }, { why: "the machine's record could not be written (EPERM)" }).take, run: async () => { ran = true; return { result: 'pass' }; } });
    assert.equal(confirm.result, 'inconclusive');
    assert.match(confirm.why ?? '', /could not be written/);
    assert.equal(ran, false);
  });

  test('an answer that does not read as a pass or as failures is inconclusive', async () => {
    for (const answer of [{ result: 'fail', failures: [] }, { result: 'fail' }, { result: 'busy' }, null, { result: 'fail', failures: [{ gate: 'driver' }] }]) {
      const confirm = await q.confirmStep({ take: wholeOf(SHOWN).take, run: async () => answer });
      assert.equal(confirm.result, 'inconclusive', JSON.stringify(answer));
    }
  });

  test('a drop ends it: before the take, while it waits, and during the run, every place let go', async () => {
    const before = wholeOf(SHOWN);
    assert.equal((await q.confirmStep({ take: before.take, run: async () => ({ result: 'pass' }), halted: () => true })).result, 'dropped');
    assert.equal(before.calls.take, 0, 'nothing is taken once dropped');

    assert.equal((await q.confirmStep({ take: wholeOf(SHOWN, { dropped: true }).take, run: async () => ({ result: 'pass' }) })).result, 'dropped');

    const during = wholeOf(SHOWN);
    let stop = false;
    const ended = new Error('the drop ended the probe running');
    const confirm = await q.confirmStep({ take: during.take, run: async () => { stop = true; throw ended; }, halted: () => stop });
    assert.equal(confirm.result, 'dropped');
    assert.equal(confirm.error, ended, 'the drop the run threw is handed on');
    assert.equal(during.calls.end, 1);
  });

  test('a throw that is no drop is the tool\'s error, its places let go first', async () => {
    const whole = wholeOf(SHOWN);
    await assert.rejects(q.confirmStep({ take: whole.take, run: async () => { throw new Error('the gate tree is gone'); } }), /the gate tree is gone/);
    assert.equal(whole.calls.end, 1);
  });

  test('each failure of the gate read by its confirm run, and a failure of its own added as confirmed', () => {
    const gate = [testFailure(s), testFailure(other)];
    const read = (confirm: Confirm) => q.confirmReadings(confirm, gate).map(({ failure, confirm: reading }) => [failure.test, reading.result]);
    assert.deepEqual(read({ result: 'unconfirmed', evidence: SHOWN }), [[s.test, 'unconfirmed'], [other.test, 'unconfirmed']]);
    assert.deepEqual(read({ result: 'inconclusive', evidence: BESIDE }), [[s.test, 'inconclusive'], [other.test, 'inconclusive']]);
    assert.deepEqual(read({ result: 'dropped' }), [[s.test, 'dropped'], [other.test, 'dropped']]);
    const own = sig('Assert.NotNull(tree);', 'Driver.Tests.TreeTests.Opens');
    const readings = q.confirmReadings({ result: 'confirmed', evidence: SHOWN, failures: [testFailure(s), testFailure(own)] }, gate);
    assert.deepEqual(readings.map(({ failure, confirm }) => [failure.test, confirm.result]), [[s.test, 'confirmed'], [other.test, 'inconclusive'], [own.test, 'confirmed']]);
    assert.match(readings[1]!.confirm.why ?? '', /failed another way/);
    assert.deepEqual(readings[0]!.confirm.evidence, SHOWN, 'each reading carries the admission evidence');
  });

  test('confirmed: judged as today, the base and then every prefix', async () => {
    const runner = prefixes([PASS, PASS, fail(s), fail(s)]);
    const judged = await q.judgeFailure(testFailure(s), { members: 3, run: runner.run, confirm: { result: 'confirmed', evidence: SHOWN } });
    assert.equal(judged.outcome, 'pinned');
    assert.equal(judged.member, 2);
    assert.equal(runner.asked[0], 0, 'the base first');
  });

  test('unconfirmed: a sighting, with no base and no prefix run, a gate that names no test included', async () => {
    const runner = prefixes([fail(s)]);
    const judged = await q.judgeFailure(testFailure(s), { members: 3, run: runner.run, confirm: { result: 'unconfirmed', evidence: SHOWN } });
    assert.equal(judged.outcome, 'unconfirmed');
    assert.match(judged.why ?? '', /sighting/);
    assert.deepEqual(judged.confirm?.evidence, SHOWN);
    assert.deepEqual(runner.asked, []);
    const check = failures.gateSignature('universal', 'check', 'docs/README.md: error: a broken link');
    const gate = await q.judgeFailure({ gate: 'universal', signature: check, names: 'gate' }, { members: 2, run: prefixes([fail(check)]).run, confirm: { result: 'unconfirmed', evidence: SHOWN } });
    assert.equal(gate.outcome, 'unconfirmed', 'a failure that does not recur needs no base run, so it never stops the line');
  });

  test('inconclusive and dropped: no run, inconclusive saying why', async () => {
    const runner = prefixes([PASS, fail(s)]);
    const inconclusive = await q.judgeFailure(testFailure(s), { members: 1, run: runner.run, confirm: { result: 'inconclusive', why: 'another live hold stands beside it', evidence: BESIDE } });
    assert.equal(inconclusive.outcome, 'inconclusive');
    assert.match(inconclusive.why ?? '', /another live hold stands beside it.*never land/);
    assert.equal((await q.judgeFailure(testFailure(s), { members: 1, run: runner.run, confirm: { result: 'dropped' } })).outcome, 'dropped');
    assert.equal((await q.judgeFailure(testFailure(s), { members: 1, run: runner.run, confirm: { result: 'what' } })).outcome, 'inconclusive', 'a reading it does not know is inconclusive');
    assert.deepEqual(runner.asked, []);
  });

  test('a known red last seen on the base is inherited before any confirm run', async () => {
    let asked = 0;
    const judged = await q.judgeFailure(testFailure(s), { members: 2, run: prefixes([fail(s)]).run, known: { expired: false, lastSeen: 'b0' }, baseCommit: 'b0', confirm: () => { asked += 1; return { result: 'unconfirmed' }; } });
    assert.equal(judged.outcome, 'inherited');
    assert.equal(asked, 0);
  });

  test('an expired known red runs on the base before the confirm run: failing there it stops with none; passing there the confirm reads, the base run once', async () => {
    let asked = 0;
    const confirm = (result: string) => () => { asked += 1; return { result, evidence: SHOWN }; };
    const stopped = await q.judgeFailure(testFailure(s), { members: 2, run: prefixes([fail(s)]).run, known: { expired: true, lastSeen: 'b0' }, baseCommit: 'b0', confirm: confirm('confirmed') });
    assert.equal(stopped.outcome, 'stop');
    assert.equal(stopped.why, 'expired');
    assert.equal(asked, 0);
    const runner = prefixes([PASS, fail(s), fail(s)]);
    const pinned = await q.judgeFailure(testFailure(s), { members: 2, run: runner.run, known: { expired: true, lastSeen: 'b0' }, baseCommit: 'b0', confirm: confirm('confirmed') });
    assert.equal(pinned.outcome, 'pinned');
    assert.equal(asked, 1);
    assert.equal(runner.asked.filter((k) => k === 0).length, 2, 'the base once, then once more beside the transition, as today');
    const sighted = prefixes([PASS]);
    assert.equal((await q.judgeFailure(testFailure(s), { members: 2, run: sighted.run, known: { expired: true, lastSeen: 'b0' }, baseCommit: 'b0', confirm: confirm('unconfirmed') })).outcome, 'unconfirmed');
    assert.deepEqual(sighted.asked, [0]);
  });

  const judgedAs = (outcome: string, failure = testFailure(s), extra: Partial<Judged> = {}) => ({ failure, judged: { outcome, why: `${outcome} why`, runs: [], confirm: { result: outcome, evidence: SHOWN }, ...extra } as Judged });

  test('unconfirmed under split, the default: a candidate of several splits, never green, its sighting kept apart from the flakes', () => {
    for (const policy of [undefined, { unconfirmed: 'split' }]) {
      const outcome = q.candidateOutcome([judgedAs('unconfirmed')], members(3), policy);
      assert.equal(outcome.action, 'split');
      assert.deepEqual(outcome.retain!.map((each) => each.branch), ['b1', 'b2', 'b3']);
      assert.equal(outcome.sightings!.length, 1);
      assert.deepEqual(outcome.sightings![0]!.confirm.evidence, SHOWN);
      assert.deepEqual(outcome.flakes, [], 'the flakes list is not the sighting store');
    }
  });

  test('unconfirmed under split: one of one handed back as unresolved with the sighting', () => {
    const outcome = q.candidateOutcome([judgedAs('unconfirmed')], members(1));
    assert.equal(outcome.action, 'hand back');
    assert.equal(outcome.handedBack![0]!.handedBack!.how, 'unresolved');
    assert.match(outcome.handedBack![0]!.handedBack!.why ?? '', /unconfirmed why/);
    assert.equal(outcome.sightings!.length, 1);
    assert.deepEqual(outcome.flakes, []);
    assert.deepEqual(outcome.retain, []);
  });

  test('unconfirmed under hold: the candidate waits for a decide, never green', () => {
    const outcome = q.candidateOutcome([judgedAs('unconfirmed')], members(2), { unconfirmed: 'hold' });
    assert.equal(outcome.action, 'hold');
    assert.equal(outcome.on, 'decide');
    assert.deepEqual(outcome.retain!.map((each) => each.branch), ['b1', 'b2']);
    assert.deepEqual(outcome.handedBack, []);
    assert.equal(outcome.sightings!.length, 1);
  });

  test('unconfirmed under land: the confirm run\'s pass is that tip\'s verdict alone, landed and never green', () => {
    for (const n of [1, 3]) {
      const outcome = q.candidateOutcome([judgedAs('unconfirmed')], members(n), { unconfirmed: 'land' });
      assert.equal(outcome.action, 'land');
      assert.deepEqual(outcome.passedOnConfirm, ['driver'], 'the gates whose only pass is the confirm run');
      assert.deepEqual(outcome.retain!.map((each) => each.branch), members(n).map((each) => each.branch));
      assert.equal(outcome.sightings!.length, 1);
    }
  });

  test('inconclusive reads as split under every policy, never land; one of one is handed back unresolved', () => {
    for (const unconfirmed of ['split', 'hold', 'land']) {
      const several = q.candidateOutcome([judgedAs('inconclusive'), judgedAs('unconfirmed', testFailure(other))], members(3), { unconfirmed });
      assert.equal(several.action, 'split', unconfirmed);
      assert.equal(several.inconclusive!.length, 1);
      assert.deepEqual(several.flakes, []);
      const one = q.candidateOutcome([judgedAs('inconclusive')], members(1), { unconfirmed });
      assert.equal(one.action, 'hand back', unconfirmed);
      assert.equal(one.handedBack![0]!.handedBack!.how, 'unresolved');
      assert.match(one.handedBack![0]!.handedBack!.why ?? '', /inconclusive why/);
    }
  });

  test('a member pinned beside an unconfirmed failure under hold or land: the pinned handed back, the rest formed again', () => {
    const pinned = { failure: testFailure(other), judged: { outcome: 'pinned', member: 2, with: [], interacting: [], runs: [] } as Judged };
    for (const unconfirmed of ['hold', 'land']) {
      const outcome = q.candidateOutcome([pinned, judgedAs('unconfirmed')], members(3), { unconfirmed });
      assert.equal(outcome.action, 'hand back', unconfirmed);
      assert.deepEqual(outcome.handedBack!.map((each) => each.branch), ['b2']);
      assert.deepEqual(outcome.retain!.map((each) => each.branch), ['b1', 'b3']);
      assert.equal(outcome.sightings!.length, 1);
    }
    const split = q.candidateOutcome([pinned, judgedAs('unconfirmed')], members(3));
    assert.equal(split.action, 'split', 'under split it is today\'s rule');
    assert.deepEqual(split.handedBack!.map((each) => each.branch), ['b2']);
  });

  test('an inherited failure beside an unconfirmed one under land: landed, the inherited filed', () => {
    const outcome = q.candidateOutcome([{ failure: testFailure(other), judged: { outcome: 'inherited', runs: [] } }, judgedAs('unconfirmed')], members(2), { unconfirmed: 'land' });
    assert.equal(outcome.action, 'land');
    assert.equal(outcome.inherited!.length, 1);
  });

  test('a policy it does not know reads as split, and no unconfirmed gate is ever green', () => {
    assert.equal(q.candidateOutcome([judgedAs('unconfirmed')], members(2), { unconfirmed: 'promote' }).action, 'split');
    for (const unconfirmed of ['split', 'hold', 'land', 'promote', undefined]) {
      for (const n of [1, 2]) assert.notEqual(q.candidateOutcome([judgedAs('unconfirmed')], members(n), { unconfirmed }).action, 'green', `${unconfirmed} of ${n}`);
    }
  });

  test('a drop: nothing handed back, filed or kept, before any stop', () => {
    const stop = { failure: testFailure(other), judged: { outcome: 'stop', why: 'base', said: 'main is broken', runs: [] } as Judged };
    const outcome = q.candidateOutcome([stop, judgedAs('dropped')], members(2), { unconfirmed: 'land' });
    assert.equal(outcome.action, 'dropped');
    assert.deepEqual([outcome.handedBack, outcome.retain, outcome.inherited, outcome.flakes, outcome.sightings], [[], [], [], [], []]);
  });

  test('the policy is the tool\'s own switch file in the main checkout\'s local/, split where it is missing, does not read or names no setting', () => {
    const root = makeFixture('integration-policy').root;
    const file = join(root, q.INTEGRATION_POLICY);
    assert.equal(q.INTEGRATION_POLICY, 'local/integration-policy.json');
    const read = () => q.readIntegrationPolicy(root);
    assert.equal(read().unconfirmed, 'split');
    assert.match(read().said, /is not there/);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, '{ "unconfirmed": ');
    assert.equal(read().unconfirmed, 'split');
    assert.match(read().said, /does not read/);
    for (const value of ['split', 'hold', 'land']) {
      writeFileSync(file, JSON.stringify({ unconfirmed: value }));
      assert.equal(read().unconfirmed, value);
    }
    writeFileSync(file, `﻿${JSON.stringify({ unconfirmed: 'hold' })}`);
    assert.equal(read().unconfirmed, 'hold', 'a byte-order mark an editor wrote is read past');
    for (const written of [{ unconfirmed: 'Land' }, { unconfirmed: 'promote' }, { unconfirmed: true }, {}, ['land'], 'land']) {
      writeFileSync(file, JSON.stringify(written));
      assert.equal(read().unconfirmed, 'split', JSON.stringify(written));
      assert.match(read().said, /names no setting it knows/);
    }
  });

  test('no repository file, gate environment or MERGE_BRANCH_ variable sets the policy, so a steward\'s run cannot set land', () => {
    const root = makeFixture('integration-policy-elsewhere').root;
    writeFileSync(join(root, 'integration-policy.json'), JSON.stringify({ unconfirmed: 'land' }));
    writeFileSync(join(root, 'daoris.gates.json'), JSON.stringify({ gates: [], integration: { unconfirmed: 'land' } }));
    const names = ['MERGE_BRANCH_UNCONFIRMED', 'MERGE_BRANCH_POLICY', 'DAORIS_INTEGRATION_UNCONFIRMED', 'DAORIS_UNCONFIRMED', 'UNCONFIRMED'];
    const kept = Object.fromEntries(names.map((name) => [name, process.env[name]]));
    try {
      for (const name of names) process.env[name] = 'land';
      assert.equal(q.readIntegrationPolicy(root).unconfirmed, 'split');
    } finally {
      for (const name of names) {
        if (kept[name] === undefined) delete process.env[name];
        else process.env[name] = kept[name];
      }
    }
  });

  test('the sighting: on its unit, with the candidate\'s tip and the confirm run\'s admission evidence, in a record of its own', () => {
    const root = makeFixture('integration-sightings').root;
    const candidate = { base: 'b'.repeat(40), tip: 'c'.repeat(40), members: members(2) };
    const sighting = q.sightingOf({ failure: testFailure(s), candidate, confirm: { result: 'unconfirmed', evidence: SHOWN, composition: { units: ['SessionTests'], digest: 'sha256:abc' } }, policy: 'split', at: AT });
    assert.deepEqual(sighting, {
      schema: 1, at: AT, gate: 'driver', unit: 'SessionTests', test: s.test, names: 'test', signature: s, key: failures.signatureKey(s),
      commit: candidate.tip, base: candidate.base, members: ['b1', 'b2'], policy: 'split', admission: SHOWN, composition: { units: ['SessionTests'], digest: 'sha256:abc' },
    });
    assert.equal(q.INTEGRATION_SIGHTINGS, 'local/integration-sightings.jsonl');
    q.appendSighting(root, sighting);
    q.appendSighting(root, { ...sighting, at: '2026-10-24T11:00:00.000Z' });
    assert.deepEqual(q.readSightings(root).map((each) => each['at']), [AT, '2026-10-24T11:00:00.000Z']);
    assert.equal(existsSync(join(root, 'local', 'ci-findings.json')), false, 'nothing is filed as a known red or a flake');
  });

  // INTEGRATE1b part 2: what the queue's runner does with the readings it now makes.
  test('a confirm run that gives no reading says why in its own words: a death of memory is the machine\'s, and inconclusive', async () => {
    const why = 'it died of memory (exit 0xC0000017), which is the machine\'s: whether it recurs is not shown';
    const confirm = await q.confirmStep({ take: wholeOf(SHOWN).take, run: async () => ({ result: 'memory', why }) });
    assert.equal(confirm.result, 'inconclusive');
    assert.equal(confirm.why, why);
  });

  test('a candidate held for a decide waits, since no decide door exists yet; a commit on main forms it again', () => {
    const held: Candidate = { base: 'm0', tip: 'c1', members: members(2), state: 'held', reforms: 0, recordsOnly: false, held: { on: 'decide', why: 'an unconfirmed failure holds the candidate for a decide: check' } };
    assert.equal(q.resumeCandidate(held, { mainTip: 'm0', isAncestor: () => false, runAlive: () => false }), 'wait', 'never run again on its own');
    assert.equal(q.resumeCandidate(held, { mainTip: 'm1', isAncestor: () => false }), 'reform');
    assert.equal(q.resumeCandidate({ ...held, held: { on: 'memory', why: 'a second death' } }, { mainTip: 'm0', isAncestor: () => false }), 'run', 'one held on memory runs again, as before');
    assert.match(q.queueLines({ ...q.emptyQueue(), candidate: held }).join('\n'), /held on decide: an unconfirmed failure holds the candidate for a decide: check/);
  });

  test('--queue says a candidate that lands on its confirm run alone, naming the gates whose only pass that was', () => {
    const landing = { base: 'm0', tip: 'c1', members: members(2), state: 'green', reforms: 0, recordsOnly: false, landing: { onConfirm: ['check'] } } as Candidate;
    assert.match(q.queueLines({ ...q.emptyQueue(), candidate: landing }).join('\n'), /candidate on m0: green, landing on its confirm run alone \(check\)/);
  });
});

describe('a failing gate that names no test: on the base first', () => {
  test('a direct docs commit that breaks a documentation check before a candidate forms stops the queue; a recheck that passes lifts it', async () => {
    const check = failures.gateSignature('universal', 'check', 'docs/README.md: error: a broken link');
    const failure: Failure = { gate: 'universal', signature: check, names: 'gate' };
    const judged = await q.judgeFailure(failure, { members: 2, run: prefixes([fail(check)]).run });
    assert.equal(judged.outcome, 'stop');
    assert.equal(judged.why, 'base');
    const stop = { at: AT };
    assert.equal(q.recheckDue(stop, NOW + 10 * 60_000), false);
    assert.equal(q.recheckDue(stop, NOW + q.RECHECK_MS), true, "the runner's own recheck an hour on");
    assert.equal(q.noteRecheck(stop, { at: AT, passed: true, by: '--queue --recheck' }), null, 'a host failure that does not recur lifts the stop');
    const kept = q.noteRecheck(stop, { at: AT, passed: false, by: 'the runner' }) as { rechecks: unknown[] };
    assert.equal(kept.rechecks.length, 1);
    assert.equal(q.recheckDue(kept as { at: string; rechecks: { at: string }[] }, NOW + 30 * 60_000), false);
  });

  test('a member that breaks code-map: found by running the check over the prefixes', async () => {
    const check = failures.gateSignature('code-map', 'check', 'code-map: error: tools/merge-queue.mjs has no module');
    const judged = await q.judgeFailure({ gate: 'code-map', signature: check, names: 'gate' }, { members: 3, run: prefixes([PASS, PASS, fail(check), fail(check)]).run });
    assert.equal(judged.member, 2);
  });

  test('a compilation failure found by prefix builds', async () => {
    const build = failures.gateSignature('driver', 'build', 'src/A.cs(1,1): error CS0246: missing type');
    const judged = await q.judgeFailure({ gate: 'driver', signature: build, names: 'gate' }, { members: 4, run: prefixes([PASS, PASS, PASS, PASS, fail(build)]).run });
    assert.equal(judged.member, 4);
  });

  test('a discovery failure and a host failure on one prefix, each found by its gate\'s selection there, every prefix build passing', async () => {
    const discovery = failures.gateSignature('modules', 'discovery', 'error: the test run printed no result');
    const host = failures.gateSignature('modules', 'host', 'The active test run was aborted. Reason: Test host process crashed');
    assert.equal((await q.judgeFailure({ gate: 'modules', signature: discovery, names: 'gate' }, { members: 2, run: prefixes([PASS, fail(discovery), fail(discovery)]).run })).member, 1);
    assert.equal((await q.judgeFailure({ gate: 'modules', signature: host, names: 'gate' }, { members: 2, run: prefixes([PASS, PASS, fail(host)]).run })).member, 2);
  });

  test('two projects failing to build, their errors printed in either order: one signature on the base and the prefix', async () => {
    const one = failures.gateSignature('driver', 'build', 'src/A/X.cs(1,1): error CS0103: a\nsrc/B/Y.cs(2,2): error CS1002: b');
    const two = failures.gateSignature('driver', 'build', 'src/B/Y.cs(2,2): error CS1002: b\nsrc/A/X.cs(1,1): error CS0103: a');
    const judged = await q.judgeFailure({ gate: 'driver', signature: one, names: 'gate' }, { members: 2, run: prefixes([fail(two)]).run });
    assert.equal(judged.outcome, 'stop');
  });
});

describe('a death of memory is the machine\'s', () => {
  test('never run on the base nor over the prefixes: run again once, and a pass reads FLAKE of memory', () => {
    assert.equal(q.memoryStep([{ died: true }]).next, 'again');
    const passed = q.memoryStep([{ died: true }, { died: false }]);
    assert.equal(passed.next, 'pass');
    assert.match(passed.note ?? '', /FLAKE of memory/);
  });

  test('a second death holds the candidate until the admission reserves its need; then a death within its need is held, one past it judged', () => {
    assert.equal(q.memoryStep([{ died: true }, { died: true }]).next, 'hold');
    assert.equal(q.memoryStep([{ died: true, peakGb: 6 }], { afterHold: true, needGb: 8 }).next, 'hold');
    assert.equal(q.memoryStep([{ died: true, peakGb: 11 }], { afterHold: true, needGb: 8 }).next, 'judge');
    assert.equal(q.memoryStep([{ died: false }], { afterHold: true, needGb: 8 }).next, 'pass');
  });
});

describe('main moved before promotion', () => {
  const lanes = tool.readLanes(repoRoot)!.lanes;
  const ruleOf = (path: string) => tool.ruleFor(path, tool.REACH, lanes);

  test('by records only: the backlog, the archive, a decision, the index', () => {
    assert.equal(q.recordsOnly(['TASKS.md', 'docs/task-archive.md', 'docs/decisions/D207.md', 'docs/index/README.md', 'CHANGELOG.md'], ruleOf), true);
  });

  test('a direct commit to .claude/knowledge/adoption.md, to daoris.lanes.json, and to src/Daoris.Cli/test/**, none records only', () => {
    assert.equal(q.recordsOnly(['.claude/knowledge/adoption.md'], ruleOf), false);
    assert.equal(q.recordsOnly(['daoris.lanes.json'], ruleOf), false);
    assert.equal(q.recordsOnly(['src/Daoris.Cli/test/dogfood.test.ts'], ruleOf), false);
    assert.equal(q.recordsOnly(['TASKS.md', 'tools/merge-queue.mjs'], ruleOf), false);
  });

  test('the re-form after a records-only move runs the whole cheap tier and defers only service', () => {
    const gates = [{ name: 'universal', kind: 'cheap' }, { name: 'code-map', kind: 'cheap' }, { name: 'test-map', kind: 'cheap' }, { name: 'service', kind: 'fast' }, { name: 'cli', kind: 'node' }];
    const set = q.scopedSet({ gates, reached: new Set(['service']), recordsOnly: true, standing: new Set(['universal']) });
    assert.deepEqual(set.run.map((each) => each.name), ['code-map', 'test-map']);
    assert.deepEqual(set.kept.map((each) => each.name), ['universal']);
    assert.deepEqual(set.deferred.map((each) => each.name), ['service']);
  });

  test('a re-form counted; three running are held by direct commits', () => {
    const candidate: Candidate = { base: 'm0', tip: 'c', members: members(1), state: 'running', reforms: q.HELD_REFORMS - 1, recordsOnly: false };
    assert.deepEqual(q.reformCount(candidate, { recordsOnlyMove: true }), { reforms: q.HELD_REFORMS, recordsOnly: true, heldByDirect: true });
    assert.match(q.queueLines({ ...q.emptyQueue(), candidate: { ...candidate, reforms: 3 } }).join('\n'), /held by direct commits/);
  });

  test('a checkout with a tracked change holds promotion until it is clean; a moved base re-forms', () => {
    const candidate: Candidate = { base: 'm0', tip: 'c', members: members(1), state: 'green', reforms: 0, recordsOnly: false };
    assert.equal(q.promotion(candidate, { mainTip: 'm0', clean: false }).action, 'held');
    assert.equal(q.promotion(candidate, { mainTip: 'm0', clean: true }).action, 'promote');
    assert.equal(q.promotion(candidate, { mainTip: 'm1', clean: true }).action, 'reform');
  });
});

describe('the runner killed and resumed, by ancestry, without a second merge', () => {
  const candidate = (state: string): Candidate => ({ base: 'm0', tip: 'c1', members: members(1), state, reforms: 0, recordsOnly: false, run: { pid: 1 } });
  const ancestry = (landed: boolean) => (a: string, b: string) => landed && a === 'c1' && b === 'm9';

  test('killed before the fast-forward: promoted on resume', () => {
    assert.equal(q.resumeCandidate(candidate('promoting'), { mainTip: 'm0', isAncestor: ancestry(false) }), 'promote');
  });

  test('killed between the fast-forward and the record: recorded promoted, nothing merged again', () => {
    assert.equal(q.resumeCandidate(candidate('promoting'), { mainTip: 'c1', isAncestor: ancestry(false) }), 'promoted');
    assert.equal(q.resumeCandidate(candidate('green'), { mainTip: 'm9', isAncestor: ancestry(true) }), 'promoted');
  });

  test('killed after both: nothing left to do', () => {
    assert.equal(q.resumeCandidate(candidate('promoted'), { mainTip: 'c1', isAncestor: ancestry(false) }), 'promoted');
  });

  test('a run whose process is gone runs again; a base main left re-forms', () => {
    assert.equal(q.resumeCandidate(candidate('running'), { mainTip: 'm0', isAncestor: ancestry(false), runAlive: () => false }), 'run');
    assert.equal(q.resumeCandidate(candidate('running'), { mainTip: 'm0', isAncestor: ancestry(false), runAlive: () => true }), 'wait');
    assert.equal(q.resumeCandidate(candidate('running'), { mainTip: 'm5', isAncestor: ancestry(false) }), 'reform');
  });
});

describe('the scoped set', () => {
  const gates = [
    { name: 'universal', kind: 'cheap' }, { name: 'driver', kind: 'fast' }, { name: 'driver-process', kind: 'process' },
    { name: 'driver-process-git', kind: 'git' }, { name: 'web', kind: 'browser' }, { name: 'rehearse-family', kind: 'rehearsal' }, { name: 'modules', kind: 'fast' },
  ];

  test('a reached .NET fast half runs whole; browser specs and rehearsals are deferred, known red or not', () => {
    const set = q.scopedSet({ gates, reached: new Set(['driver', 'web', 'rehearse-family']), knownRed: new Set(['web', 'rehearse-family']) });
    assert.deepEqual(set.run.map((each) => each.name), ['universal', 'driver']);
    assert.match(set.run[1]!.why, /fast half runs whole/);
    assert.deepEqual(set.deferred.map((each) => each.name), ['web', 'rehearse-family']);
  });

  test('a known-red unit the scoped run may run runs in every candidate, whatever it reaches', () => {
    const set = q.scopedSet({ gates, reached: new Set(), knownRed: new Set(['modules']) });
    assert.deepEqual(set.run.map((each) => each.name), ['universal', 'modules']);
  });

  test('a known-red git class past the budget runs first, and every other reached class is deferred', () => {
    const minute = 60_000;
    const classes = [
      { name: 'Red', ms: 9 * minute, knownRed: true },
      { name: 'Subject', ms: 2 * minute, subject: true },
      { name: 'Other', ms: 1 * minute },
    ];
    const budget = q.gitBudget({ classes, budgetMs: 8 * minute });
    assert.deepEqual(budget.run, ['Red']);
    assert.deepEqual(budget.deferred, ['Subject', 'Other']);
    assert.equal(budget.overrun, true);
  });

  test('the git half within its budget: all when it fits, else the subject first, then by recent failures and shortest', () => {
    const minute = 60_000;
    assert.deepEqual(q.gitBudget({ classes: [{ name: 'A', ms: minute }, { name: 'B', ms: minute }], budgetMs: 8 * minute }).deferred, []);
    const classes = [
      { name: 'Long', ms: 7 * minute },
      { name: 'Subject', ms: 6 * minute, subject: true },
      { name: 'Flaky', ms: 5 * minute, failures: 3 },
      { name: 'Short', ms: 1 * minute },
    ];
    const budget = q.gitBudget({ classes, budgetMs: 7 * minute, overheadMs: minute });
    assert.deepEqual(budget.run, ['Subject', 'Flaky', 'Short']);
    assert.deepEqual(budget.deferred, ['Long']);
    assert.ok(budget.predictedMs <= 7 * minute);
  });
});

describe('admission: one record for both runners', () => {
  const free = { physicalGb: 64, commitGb: 64 };
  const none = () => ({ state: 'gone' });

  test('the background\'s git half and a rehearsal already running when a scoped tier starts: the scoped tier runs beside them, and its FLAKE1 re-run too', () => {
    let record = q.emptyAdmission();
    record = q.reserve(record, { runner: 'background', run: 'ci-1', gate: 'driver-process-git', lane: 'process', needGb: 8, pid: 1, at: AT }).record;
    record = q.reserve(record, { runner: 'background', run: 'ci-1', gate: 'rehearse-family', lane: 'rehearsal', needGb: 10, pid: 1, at: AT }).record;
    const active = () => ({ state: 'active', workingGb: 2, committedGb: 3 });
    const scoped = q.admit({ record: q.scopedTier(record, 'running', AT), request: { runner: 'scoped', gate: 'driver-process', lane: 'process', needGb: 8 }, free, readJob: active });
    assert.equal(scoped.admit, true);
    const rerun = q.admit({ record: q.scopedTier(record, 'running', AT), request: { runner: 'scoped', gate: 'driver-process (FLAKE1)', lane: 'process', needGb: 4 }, free, readJob: active });
    assert.equal(rerun.admit, true);
  });

  test('no next rehearsal of the chain starts while the scoped tier waits; the background\'s light lane still runs', () => {
    const record = q.scopedTier(q.emptyAdmission(), 'queued', AT);
    const rehearsal = q.admit({ record, request: { runner: 'background', gate: 'rehearse-deploy', lane: 'rehearsal', needGb: 10 }, free, readJob: none });
    assert.equal(rehearsal.wait, 'slot');
    const light = q.admit({ record, request: { runner: 'background', gate: 'web-unit', lane: 'light', needGb: 6 }, free, readJob: none });
    assert.equal(light.admit, true);
  });

  test('a background gate takes the slot after its 60-minute wait, between scoped tiers only; one held on memory is reported overdue', () => {
    const record = q.scopedTier(q.emptyAdmission(), 'running', AT);
    const request = { runner: 'background', gate: 'driver-process-git', lane: 'process', needGb: 8, waitingSince: AT };
    const later = NOW + q.BACKGROUND_SLOT_WAIT_MS;
    assert.equal(q.admit({ record, request, free, readJob: none, now: later, betweenTiers: false }).wait, 'slot');
    assert.equal(q.admit({ record, request, free, readJob: none, now: later, betweenTiers: true }).admit, true);
    const held = { ...record, waiting: [{ runner: 'background', gate: 'driver-process-git', since: AT, on: 'memory' }] };
    assert.match(q.overdueWaits(held, later)[0] ?? '', /driver-process-git has waited on memory/);
  });

  test('the background leaves the foreground headroom; a scoped gate does not', () => {
    const tight = { physicalGb: q.FOREGROUND_HEADROOM_GB + 4, commitGb: 64 };
    assert.equal(q.admit({ record: q.emptyAdmission(), request: { runner: 'background', gate: 'service', lane: 'light', needGb: 6 }, free: tight, readJob: none }).wait, 'memory');
    assert.equal(q.admit({ record: q.emptyAdmission(), request: { runner: 'scoped', gate: 'service', lane: 'light', needGb: 6 }, free: tight, readJob: none }).admit, true);
  });

  test('a reservation charged apart in physical memory and commit: its holder\'s working set falling while its commit stays', () => {
    const record = q.reserve(q.emptyAdmission(), { runner: 'scoped', run: 'c1', gate: 'driver-process', lane: 'process', needGb: 10, pid: 1, at: AT }).record;
    const trimmed = () => ({ state: 'active', workingGb: 1, committedGb: 10 });
    // Physical: 20 free less 9 unused leaves 11, enough; commit: 12 free less 0 unused, enough for 8.
    assert.equal(q.admit({ record, request: { runner: 'scoped', gate: 'modules', lane: 'light', needGb: 8 }, free: { physicalGb: 20, commitGb: 12 }, readJob: trimmed }).admit, true);
    // Physical: 16 free less 9 unused leaves 7, short of 8, though its commit is spent.
    assert.equal(q.admit({ record, request: { runner: 'scoped', gate: 'modules', lane: 'light', needGb: 8 }, free: { physicalGb: 16, commitGb: 12 }, readJob: trimmed }).wait, 'memory');
  });

  test('released on a read that finds the job empty, or on a name no job answers; a query that fails keeps the charge, said', () => {
    let record = q.reserve(q.emptyAdmission(), { runner: 'scoped', run: 'c1', gate: 'a', lane: 'process', needGb: 8, pid: 1, at: AT }).record;
    record = q.reserve(record, { runner: 'scoped', run: 'c1', gate: 'b', lane: 'light', needGb: 6, pid: 1, at: AT }).record;
    record = q.reserve(record, { runner: 'background', run: 'ci', gate: 'c', lane: 'light', needGb: 6, pid: 2, at: AT }).record;
    const jobs: Record<string, Job> = {
      'daoris-scoped-c1-a': { state: 'empty' },
      'daoris-scoped-c1-b': { state: 'gone' },
      'daoris-background-ci-c': { state: 'error', why: 'access denied' },
    };
    const read = q.admit({ record, request: { runner: 'scoped', gate: 'd', lane: 'light', needGb: 1 }, free, readJob: (name) => jobs[name] ?? { state: 'error' } });
    assert.deepEqual(read.record.reservations.map((each) => each.gate), ['c']);
    assert.match(read.said[0] ?? '', /could not be read \(access denied\), so its whole charge is kept/);
  });

  test('a holder killed with children surviving: its job still active, the reservation held until they end', () => {
    const record = q.reserve(q.emptyAdmission(), { runner: 'scoped', run: 'c1', gate: 'a', lane: 'process', needGb: 8, pid: 99, at: AT }).record;
    const survived = q.admit({ record, request: { runner: 'scoped', gate: 'b', lane: 'light', needGb: 1 }, free, readJob: () => ({ state: 'active', workingGb: 2, committedGb: 2 }) });
    assert.equal(survived.record.reservations.length, 1);
    const ended = q.admit({ record, request: { runner: 'scoped', gate: 'b', lane: 'light', needGb: 1 }, free, readJob: () => ({ state: 'empty' }) });
    assert.equal(ended.record.reservations.length, 0);
  });

  test('two gates of one run in one tree each have a job of their own, charged only for their own', () => {
    let record = q.emptyAdmission();
    const a = q.reserve(record, { runner: 'scoped', run: 'c1', gate: 'driver', lane: 'light', needGb: 6, pid: 1, at: AT });
    record = a.record;
    const b = q.reserve(record, { runner: 'scoped', run: 'c1', gate: 'modules', lane: 'light', needGb: 6, pid: 1, at: AT });
    assert.notEqual(a.job, b.job);
    assert.equal(b.record.reservations.length, 2);
  });
});

// ---------------------------------------------------------------------------------------------------
// End to end, in a scratch repository of its own: the verbs, the detached runner, the scope tree, promotion

const TOOL = join(repoRoot, 'tools', 'merge-branch.mjs');
const GIT_ENV = {
  GIT_TERMINAL_PROMPT: '0',
  GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
  GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.autocrlf', GIT_CONFIG_VALUE_0: 'false',
};
const TRAILER = '\n\nCo-Authored-By: Claude Fixture (Anthropic) <fixture@example.test>';

/** The test process's environment as this file loaded, before any case could change it (QUEUETESTLEAK1). */
const START_ENV: Readonly<Record<string, string | undefined>> = Object.freeze({ ...process.env });

/**
 * QUEUETESTLEAK1: what a case's children never inherit. Git's repository-locating variables point a scratch's git at another
 * repository (`git init` re-initialises it, and the queue home becomes its first worktree); the tool's switches, the stand-in gates'
 * variables and Daoris's home are each case's own or the machine's.
 */
const NOT_INHERITED = [
  /^GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES|DISCOVERY_ACROSS_FILESYSTEM|PREFIX|CONFIG.*)$/i,
  /^MERGE_BRANCH_/i, /^QUEUE_/i, /^DAORIS_/i, /^NODE_TEST_CONTEXT$/i,
];

/**
 * QUEUETESTLEAK1: the environment a case builds for its children, from the test process's start (never its live `process.env`, which
 * a case may not change while others run): nothing `NOT_INHERITED`, the fixture's git identity, `ceiling` as git's ceiling so a
 * scratch whose `.git` is gone is no repository rather than the checkout around it, and the case's own variables.
 */
function caseEnvironment(ceiling: string, own: Record<string, string> = {}): Record<string, string> {
  const env: Record<string, string> = {};
  for (const [name, value] of Object.entries(START_ENV)) {
    if (value !== undefined && !NOT_INHERITED.some((pattern) => pattern.test(name))) env[name] = value;
  }
  return { ...env, ...GIT_ENV, GIT_CEILING_DIRECTORIES: ceiling, ...own };
}

/** `env` with `extra` over it, a name given in either spelling replacing the other (Windows names `Path` and `PATH` as one). */
function withEnv(env: Record<string, string>, extra: Record<string, string>): Record<string, string> {
  const given = new Set(Object.keys(extra).map((name) => name.toUpperCase()));
  return { ...Object.fromEntries(Object.entries(env).filter(([name]) => !given.has(name.toUpperCase()))), ...extra };
}

/** The search path in `env`, under whichever spelling it has. */
const pathOf = (env: Record<string, string | undefined>) => Object.entries(env).find(([name]) => name.toUpperCase() === 'PATH')?.[1] ?? '';

/** What `PROBE_DRIVER` probes: the candidate, the plan, the probe's folder, the failure and the prefixes, in order. */
interface ProbeSpec { candidate: Candidate; plan: { name: string; run: string }[]; dir: string; failure: Failure; ks: number[]; live?: string; after?: Record<string, string> }

/**
 * QUEUETESTLEAK1: the tool's failure probe (`prefixRunner`) in a process of its own. It reads its spec from the file it is given:
 * the tool's URL, the root, `ProbeSpec`. The candidate stands while `live` exists (where one is named); `after` is set in its own
 * environment once the tool has loaded. Each probe's answer, or the error it threw, is a line of JSON.
 */
const PROBE_DRIVER = [
  "import { existsSync, readFileSync } from 'node:fs';",
  "const spec = JSON.parse(readFileSync(process.argv[2], 'utf8'));",
  'const tool = await import(spec.tool);',
  'Object.assign(process.env, spec.after ?? {});',
  'const live = () => (spec.live && !existsSync(spec.live) ? null : spec.candidate);',
  'const watch = tool.watchCandidate(spec.candidate, live, tool.stepTrees(), { ms: 50 }).start();',
  'const probe = tool.prefixRunner(spec.root, spec.candidate, spec.plan, spec.dir, watch);',
  'for (const k of spec.ks) {',
  '  try {',
  '    console.log(JSON.stringify({ k, answer: await probe(spec.failure, k) }));',
  '  } catch (error) {',
  '    console.log(JSON.stringify({ k, error: error.message }));',
  '  }',
  '}',
  'watch.stop();',
  '',
].join('\n');

// A gate that fails, saying so on one line, where a file named `break-<gate>` stands in the tree it runs in.
const QUEUE_GATE = [
  "import { existsSync } from 'node:fs';",
  'const name = process.argv[2];',
  // A case that must queue a branch while a candidate is still running holds its `check` gate on a file it writes after.
  'const hold = process.env.QGATE_HOLD;',
  "if (hold && name === 'check') { const end = Date.now() + 300000; while (!existsSync(hold) && Date.now() < end) await new Promise((done) => setTimeout(done, 100)); }",
  'console.log(`${name} ran`);',
  "if (existsSync(`break-${name}`)) { console.log(`error: ${name} is broken`); process.exit(1); }",
  '',
].join('\n');

/** Git in `cwd` with the case's environment `env`, which must succeed; its output trimmed. */
function sh(env: Record<string, string>, cwd: string, ...args: string[]): string {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', env });
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

/** A path as the file system names it, for comparing two spellings of one folder. */
const realPath = (path: string) => realpathSync(path).toLowerCase();

const pause = (ms: number) => new Promise((done) => { setTimeout(done, ms); });

/**
 * How long one scratch run of the merge tool, or one wait on its queue, may take before the test calls it hung
 * (TOOLTESTHANG1): a guard against the hang, never a pace, so it elapses only on failure. A scratch queue gates real git
 * under whatever else the machine runs, and a healthy run took longer than the two-minute shared patience under load
 * (2026-10-28, the bound ended a run still promoting), so the guard is ten minutes; the hang it catches lasted ninety. It is
 * five times the shared patience (TESTPATIENCE3), so the one knob moves it, and stays longer because a healthy run is.
 */
const HANG_MS = 5 * PATIENCE;

/** Waits for the condition, at most `ms`: a bound that elapses only on failure. */
async function until(holds: () => boolean, ms = HANG_MS) {
  for (const end = Date.now() + ms; !holds() && Date.now() < end;) await pause(100);
}

const alive = (pid: number) => {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
};

function queueScratch(name: string) {
  // QUEUETESTLEAK1: named for this process too, so a second run of the file beside this one, or one after it killed, never
  // clears a scratch whose processes still stand in it: a scratch that lost its `.git` was git's way out to the checkout around it.
  const fx = makeFixture(`merge-queue-${name}-${process.pid}`);
  const root = join(fx.root, 'repo');
  mkdirSync(root, { recursive: true });
  // TOOLTESTHANG1: the runner's own `git status` (its clean-checkout look, every tick while a candidate is held) refreshes the
  // index and takes `index.lock` to write it back, so a test's git write on this repository met the lock now and then. The
  // runner is told to take no optional lock: it reads the checkout as before and never holds the index, so the test's writes
  // need no retry and no sleep. A write the queue itself makes (the fast-forward, the scope tree's merges) is not optional.
  // QUEUETESTLEAK1: every child of the case, the tool's invocations and every git, gets this environment and no other, with the
  // fixture's folder as git's ceiling.
  // INTEGRATE1b part 2: the machine's admission is the case's own, in a Daoris home of its own two places wide, so a red candidate's
  // confirm run takes the whole of it as the runner does on a machine, and no case waits on what the machine around it runs. Without
  // it every confirm run would read inconclusive, since no admission could be shown.
  const home = join(fx.root, 'home');
  mkdirSync(home, { recursive: true });
  const env = caseEnvironment(fx.root, {
    GIT_OPTIONAL_LOCKS: '0', MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_CI_NIGHT_HOUR: 'off', MERGE_BRANCH_QUEUE_POLL_MS: '200', MERGE_BRANCH_QUEUE_TICK_MS: '300', MERGE_BRANCH_DROP_POLL_MS: '200',
    MERGE_BRANCH_ADMISSION: 'on', DAORIS_HOME: home, DAORIS_ADMISSION_SLOTS: '2',
    // PROCLEAK1: the runner and the waiter this case starts end with this process, so a test killed leaves neither polling on.
    MERGE_BRANCH_QUEUE_OWNER: String(process.pid),
  });
  const git = (...args: string[]) => sh(env, root, ...args);
  git('init', '--quiet', '--template=', '-b', 'main');
  // The guard: its own repository, its own git storage and its own queue home (the first worktree), never the one around it.
  assert.equal(realPath(git('rev-parse', '--show-toplevel')), realPath(root));
  assert.equal(realPath(git('rev-parse', '--path-format=absolute', '--git-common-dir')), realPath(join(root, '.git')));
  assert.equal(realPath(git('worktree', 'list', '--porcelain').split('\n')[0]!.replace(/^worktree /, '')), realPath(root));
  writeFileSync(join(root, '.gitignore'), 'local/\n');
  writeFileSync(join(root, 'qgate.mjs'), QUEUE_GATE);
  const gates = [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check' }];
  writeFileSync(join(root, 'daoris.gates.json'), `${JSON.stringify({ gates }, null, 2)}\n`);
  git('add', '-A');
  git('commit', '--quiet', '-m', `first${TRAILER}`);
  mkdirSync(join(root, 'local'), { recursive: true });
  // Every invocation the test started and has not seen end: a failed assertion leaves them for `cleanup` to end.
  const started = new Set<ChildProcess>();
  const queueFile = join(root, 'local', 'merge-queue.json');
  const queue = () => (existsSync(queueFile)
    ? JSON.parse(readFileSync(queueFile, 'utf8')) as Queue & { runner: unknown }
    : null);
  const admissionFile = join(root, 'local', 'admission.json');
  let probes = 0;
  /** A node process of the case, bounded by `ms` (TOOLTESTHANG1), ended with its tree at the bound; `extra` over the case's environment. */
  const node = (args: string[], extra: Record<string, string>, ms: number, cwd: string): Promise<{ status: number | null; out: string }> => {
    const child = spawn(process.execPath, args, { cwd, env: withEnv(env, extra) });
    started.add(child);
    let out = '';
    child.stdout.on('data', (chunk) => { out += String(chunk); });
    child.stderr.on('data', (chunk) => { out += String(chunk); });
    return new Promise((done) => {
      const bound = setTimeout(() => {
        out += `\n[the test ended this invocation: no verdict within ${ms} ms (TOOLTESTHANG1)]\n`;
        tool.endTree(child.pid!);
      }, ms);
      const ended = (status: number | null) => { clearTimeout(bound); started.delete(child); done({ status, out }); };
      child.on('close', ended);
      child.on('error', (error) => { out += `${error.message}\n`; ended(null); });
    });
  };
  return {
    root,
    /** The case's Daoris home, whose machine admission the runner takes its places in (INTEGRATE1b). */
    home,
    /** The environment the case's children get (QUEUETESTLEAK1). */
    env,
    /** Git in the scratch with the case's environment. */
    git,
    queue,
    /** The admission record as it stands, `{}` where none is written, undefined for a read that met a write. */
    admission(): { scopedTier?: { state: string } | null } | undefined {
      if (!existsSync(admissionFile)) return {};
      try {
        return JSON.parse(readFileSync(admissionFile, 'utf8'));
      } catch {
        return undefined;
      }
    },
    branch(branchName: string, files: Record<string, string>) {
      git('checkout', '--quiet', '-b', branchName, 'main');
      for (const [path, text] of Object.entries(files)) {
        mkdirSync(dirname(join(root, path)), { recursive: true });
        writeFileSync(join(root, path), text);
      }
      git('add', '-A');
      git('commit', '--quiet', '-m', `work on ${branchName}${TRAILER}`);
      git('checkout', '--quiet', 'main');
      writeFileSync(join(root, 'local', `${branchName}.msg`), `Merge ${branchName}: its words${TRAILER}\n`);
    },
    /**
     * One invocation of the tool, bounded: one that has given no verdict by `ms` is ended with its tree, and answers `status: null`.
     * It runs in `cwd` (the scratch unless named) with the case's environment and `extra`.
     */
    run(args: string[], extra: Record<string, string> = {}, ms = HANG_MS, cwd = root): Promise<{ status: number | null; out: string }> {
      return node([TOOL, ...args], extra, ms, cwd);
    },
    /**
     * A failure probe of the tool's runner (`prefixRunner`) in a process of its own, with the case's environment and `extra`
     * (QUEUETESTLEAK1: never in the test process, whose environment no case may change): `PROBE_DRIVER` over `spec`, each probe's
     * answer or error a line of JSON.
     */
    async probe(spec: ProbeSpec, extra: Record<string, string> = {}): Promise<{ status: number | null; out: string; lines: { k: number; answer?: Outcome; error?: string }[] }> {
      probes += 1;
      const driver = join(fx.root, 'probe-driver.mjs');
      const file = join(fx.root, `probe-${probes}.json`);
      writeFileSync(driver, PROBE_DRIVER);
      writeFileSync(file, JSON.stringify({ tool: pathToFileURL(TOOL).href, root, ...spec }));
      const ran = await node([driver, file], extra, HANG_MS, root);
      return { ...ran, lines: ran.out.split('\n').filter((line) => line.startsWith('{')).map((line) => JSON.parse(line)) };
    },
    /** The process ids of the invocations still running. */
    running: () => [...started].map((child) => child.pid!),
    log: () => (existsSync(join(root, 'local', 'merge-queue.log')) ? readFileSync(join(root, 'local', 'merge-queue.log'), 'utf8') : ''),
    until,
    async cleanup() {
      // An invocation a failed assertion left waiting is ended first, so nothing started here can hold the run (TOOLTESTHANG1).
      for (const child of started) tool.endTree(child.pid!);
      // The runner ends once idle; its scope tree goes with the fixture.
      // A grace for an idle runner to end by itself, kept at five seconds: a runner holding a candidate never does, and is ended below.
      for (let i = 0; i < 50 && queue()?.runner; i += 1) await pause(100);
      // One that is not idle (a candidate held on the checkout, a stop) never ends by itself: it is ended, with its gates.
      const runner = queue()?.runner as { pid: number } | null | undefined;
      if (runner && alive(runner.pid)) {
        tool.endTree(runner.pid);
        await until(() => !alive(runner.pid), PATIENCE);
      }
      fx.cleanup();
    },
  };
}

/** The processes a held gate writes its pid for: the gate itself, what it started, and what that started. */
const TREE = ['check', 'child', 'grandchild'];
const pidsIn = (dir: string) => TREE.filter((role) => existsSync(join(dir, `${role}.pid`))).map((role) => Number(readFileSync(join(dir, `${role}.pid`), 'utf8')));

/**
 * A gate that, when `holdWhen` holds, writes its pid, starts a child that starts a grandchild (each writing its own), and waits
 * for `release` in `dir`: a process tree three deep, the shape a build and its test hosts make. `count` is how many times `check`
 * has run there. With `record`, the arguments it was given are kept in `args`. The folder is written into the gate
 * (QUEUETESTLEAK1): the queue's runner passes no test's variable on to a gate.
 */
function treeGate(dir: string, holdWhen: string, record = ''): string {
  return [
    "import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';",
    "import { spawn } from 'node:child_process';",
    `const dir = ${JSON.stringify(dir)};`,
    'const role = process.argv[2];',
    "const wait = async () => { while (!existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 25)); };",
    'let count = 0;',
    "if (dir && role === 'check') { appendFileSync(`${dir}/runs`, 'x'); count = readFileSync(`${dir}/runs`, 'utf8').length; }",
    record ? `if (dir && role === 'check') writeFileSync(\`\${dir}/${record}\`, process.argv.slice(3).join(' '));` : '',
    `const holding = role === 'child' || role === 'grandchild' || (role === 'check' && dir && (${holdWhen}));`,
    'if (holding) {',
    "  writeFileSync(`${dir}/${role}.pid`, String(process.pid));",
    "  if (role === 'check') spawn(process.execPath, [process.argv[1], 'child'], { stdio: 'ignore' });",
    "  if (role === 'child') spawn(process.execPath, [process.argv[1], 'grandchild'], { stdio: 'ignore' });",
    '  await wait();',
    '}',
    'console.log(`${role} ran`);',
    "if (role === 'check' && existsSync('break-check')) { console.log('error: check is broken'); process.exit(1); }",
    '',
  ].join('\n');
}

/**
 * INTEGRATE1b: a `check` that counts its runs in `dir` (`runs`) and fails where `failWhen` holds, an expression over `count` and
 * `load`, how many holds named `the load beside it` the machine's admission file `admission` keeps. Where `holdWhen` holds it first
 * writes `<count>.started` and waits for `release` in `dir`. Every other gate passes.
 */
function confirmGate(dir: string, { failWhen, holdWhen = 'false', admission = '' }: { failWhen: string; holdWhen?: string; admission?: string }): string {
  return [
    "import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';",
    `const dir = ${JSON.stringify(dir)};`,
    `const admission = ${JSON.stringify(admission)};`,
    'const role = process.argv[2];',
    "if (role !== 'check') { console.log(`${role} ran`); process.exit(0); }",
    "appendFileSync(`${dir}/runs`, 'x');",
    "const count = readFileSync(`${dir}/runs`, 'utf8').length;",
    "const load = admission && existsSync(admission) ? JSON.parse(readFileSync(admission, 'utf8')).holds.filter((hold) => hold.what === 'the load beside it').length : 0;",
    `if (${holdWhen}) {`,
    "  writeFileSync(`${dir}/${count}.started`, '');",
    "  while (!existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 25));",
    '}',
    'console.log(`check ran (${count})`);',
    `if (${failWhen}) { console.log('error: check is broken'); process.exit(1); }`,
    '',
  ].join('\n');
}

/** QUEUETESTLEAK1: a gate that writes what of its environment a leak would carry into `file`, then passes. */
const envGate = (file: string, admission = '', role = 'check') => [
  "import { existsSync, readFileSync, writeFileSync } from 'node:fs';",
  // INTEGRATE1a: also what of the install's home, a role and git's locating variables reached it, and the admission file it can read.
  `const admission = ${JSON.stringify(admission)};`,
  "const held = admission && existsSync(admission) ? JSON.parse(readFileSync(admission, 'utf8')).holds.map((hold) => hold.what) : null;",
  `if (process.argv[2] === ${JSON.stringify(role)}) writeFileSync(${JSON.stringify(file)}, JSON.stringify({ drain: process.env.QUEUE_DRAIN_DIR ?? null, tick: process.env.MERGE_BRANCH_QUEUE_TICK_MS ?? null, gitDir: process.env.GIT_DIR ?? null, ceiling: process.env.GIT_CEILING_DIRECTORIES ?? null, home: process.env.DAORIS_HOME ?? null, role: process.env.DAORIS_ROLE ?? null, beside: process.env.MSBUILDDISABLENODEREUSE ?? null, held, path: process.env.PATH ?? '' }));`,
  'console.log(`${process.argv[2]} ran`);',
  '',
].join('\n');

/** INTEGRATE1a: what `envGate` records. */
interface SeenEnvironment { drain: string | null; tick: string | null; gitDir: string | null; ceiling: string | null; home: string | null; role: string | null; beside: string | null; held: string[] | null; path: string }

// QUEUETESTLEAK1 (2026-10-28): a case's enqueue waited on the main checkout's queue and started a runner there from the tree's tool,
// with the class-probe case's PATH. Its cases run one at a time here, so the variables it sets in the test process meet no other case.
describe('QUEUETESTLEAK1: a case reaches only its own scratch repository and queue', () => {
  test("only the queue's own checkout queues to it or runs it, and a tree's tool never runs the main checkout's queue", () => {
    const home = 'X:/daoris';
    assert.equal(tool.queueHomeRefusal({ root: home, home }), null);
    assert.equal(tool.queueHomeRefusal({ root: 'X:/daoris/', home, tool: home, sameRepository: true, run: true }), null, 'its own tool, the path spelled with a slash');
    assert.match(tool.queueHomeRefusal({ root: 'X:/daoris/local/tree', home })!, /X:\/daoris\/local\/tree is not the queue's checkout: the queue is kept in X:\/daoris, the repository's main checkout/);
    assert.match(tool.queueHomeRefusal({ root: home, home, tool: 'X:/daoris/local/scope-tree', sameRepository: true, run: true })!,
      /the tool of X:\/daoris\/local\/scope-tree, another checkout of the queue's repository: a tree's tool never runs X:\/daoris's queue/);
    assert.equal(tool.queueHomeRefusal({ root: home, home, tool: 'X:/daoris/local/scope-tree', sameRepository: true }), null, 'a verb that starts no runner here runs any checkout\'s tool');
    assert.equal(tool.queueHomeRefusal({ root: home, home, tool: 'X:/elsewhere', sameRepository: false, run: true }), null, "another repository's tool (the tool tests' scratch queues) runs it");
  });

  // GITWORKTREECONFIG1: a shared `core.worktree` can make a linked worktree report another checkout's root as its own.
  test("a tree that reports another folder as its working root is refused before any write, the same folder spelled otherwise is not", () => {
    const path = 'X:/daoris/local/scope-tree';
    assert.equal(tool.treeBindingRefusal({ path, reported: path }), null);
    assert.equal(tool.treeBindingRefusal({ path, reported: 'x:/daoris/local/scope-tree/' }), null, 'the same folder, spelled with another case and a slash');
    assert.equal(tool.treeBindingRefusal({ path, reported: null }), null, 'git reported none: nothing to say here, git refuses on its own');
    const why = tool.treeBindingRefusal({ path, reported: 'X:/daoris' })!;
    assert.match(why, /X:\/daoris\/local\/scope-tree reports X:\/daoris as its working root/);
    assert.match(why, /core\.worktree/);
    assert.match(why, /nothing was reset, checked out or removed/);
    assert.match(why, /worktree configuration/);
  });

  test("a gate the tool starts gets the environment the tool had at its start, without a Daoris name, git's locating variables, the tool tests' variables or the tool's switches, with the build servers of its own", () => {
    const start = { Path: 'X:/bin', HOME: 'X:/home', QUEUE_DRAIN_DIR: 'X:/drain', queue_drop_hold: 'X:/hold', MERGE_BRANCH_QUEUE_TICK_MS: '300', MERGE_BRANCH_MEMORY: 'off', GIT_OPTIONAL_LOCKS: '0' };
    assert.deepEqual(tool.gateEnvironment(start), { Path: 'X:/bin', HOME: 'X:/home', GIT_OPTIONAL_LOCKS: '0', ...tool.BESIDE_ENVIRONMENT });
  });

  // INTEGRATE1a (design §5.5, F20): the steward's shell held the install's home on 2026-10-28, and every gate the queue's runners started inherited it.
  test("a gate gets no Daoris home, role or key and none of git's locating variables, in either spelling, and what a person set for git otherwise stays", () => {
    const start = {
      Path: 'X:/bin', DAORIS_HOME: 'X:/install', daoris_role: 'steward', DAORIS_SUPERVISOR_KEY: 'k', DAORIS_ADMISSION_HOLD: 'h-1',
      GIT_DIR: 'X:/g', git_work_tree: 'X:/w', GIT_INDEX_FILE: 'i', GIT_OBJECT_DIRECTORY: 'o', GIT_ALTERNATE_OBJECT_DIRECTORIES: 'a', GIT_COMMON_DIR: 'c', GIT_NAMESPACE: 'n',
      GIT_CEILING_DIRECTORIES: 'X:/', GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'k', GIT_CONFIG_GLOBAL: 'X:/cfg', GIT_AUTHOR_NAME: 'A', GIT_OPTIONAL_LOCKS: '0',
    };
    assert.deepEqual(tool.gateEnvironment(start), { Path: 'X:/bin', GIT_AUTHOR_NAME: 'A', GIT_OPTIONAL_LOCKS: '0', ...tool.BESIDE_ENVIRONMENT });
    assert.ok(tool.GATE_REMOVED.length > 0, 'the list is exported, so the driver\'s twin and a census read the same names');
  });

  // INTEGRATE1d part 4 (design §5.5, §4.1's `env.pass`): the names an accepted declaration passes come back from the tool's own start environment,
  // the removed list's included, and never a Daoris name; a name not passed stays out.
  test("the names an accepted declaration passes reach a gate from the start environment, a removed one's among them; a Daoris name and a name not passed never do", () => {
    const start = { Path: 'X:/bin', GIT_CONFIG_GLOBAL: 'X:/cfg', git_config_key_0: 'k', MERGE_BRANCH_MEMORY: 'off', DAORIS_HOME: 'X:/install', daoris_role: 'steward', TOKEN_FOR_GATE: 't' };
    const base = { Path: 'X:/bin', TOKEN_FOR_GATE: 't', ...tool.BESIDE_ENVIRONMENT };
    assert.deepEqual(tool.gateEnvironment(start), base, 'nothing is passed unless an accepted declaration says so');
    assert.deepEqual(tool.gateEnvironment(start, { pass: ['GIT_CONFIG_GLOBAL'] }), { ...base, GIT_CONFIG_GLOBAL: 'X:/cfg' }, 'a removed name comes back only by name');
    assert.deepEqual(tool.gateEnvironment(start, { pass: ['GIT_CONFIG_KEY_0', 'NOT_SET_HERE'] }), { ...base, git_config_key_0: 'k' }, 'the name is matched in any case, and one the start lacks adds nothing');
    assert.deepEqual(tool.gateEnvironment(start, { pass: ['DAORIS_HOME', 'daoris_role'] }), base, 'a Daoris name is never passed, however it is spelled or declared');
  });

  test('git\'s repository-locating variables, a stand-in gate\'s variable and a PATH set in the test process reach no case: its tool reads and writes only its scratch queue', async () => {
    // The repository around the tests, stood in for by a decoy with a queue of its own that must be left as it is.
    const decoy = makeFixture(`merge-queue-decoy-${process.pid}`);
    const outer = join(decoy.root, 'repo');
    mkdirSync(join(outer, 'local'), { recursive: true });
    const env = caseEnvironment(decoy.root);
    const outerGit = (...args: string[]) => spawnSync('git', args, { cwd: outer, encoding: 'utf8', env });
    outerGit('init', '--quiet', '--template=', '-b', 'main');
    writeFileSync(join(outer, '.gitignore'), 'local/\n');
    outerGit('add', '-A');
    outerGit('commit', '--quiet', '-m', `the decoy${TRAILER}`);
    q.writeQueue(outer, { ...q.emptyQueue(), previews: { sentinel: { tip: '0'.repeat(40) } } });
    const before = { head: outerGit('rev-parse', 'HEAD').stdout.trim(), queue: readFileSync(join(outer, 'local', 'merge-queue.json'), 'utf8') };
    const shims = join(decoy.root, 'shims');
    mkdirSync(shims, { recursive: true });
    const leaked: Record<string, string> = {
      GIT_DIR: join(outer, '.git'), GIT_COMMON_DIR: join(outer, '.git'), GIT_INDEX_FILE: join(outer, '.git', 'index'),
      QUEUE_DRAIN_DIR: join(decoy.root, 'drain'), PATH: `${shims}${delimiter}${START_ENV.PATH ?? START_ENV.Path ?? ''}`,
    };
    const saved = Object.fromEntries(Object.keys(leaked).map((name) => [name, process.env[name]]));
    Object.assign(process.env, leaked);
    let repo: ReturnType<typeof queueScratch> | null = null;
    try {
      repo = queueScratch('leak');
      const record = join(repo.root, 'local', 'gate-env.json');
      repo.branch('b1', { 'qgate.mjs': envGate(record) });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const done = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is in main/);
      assert.deepEqual(repo.queue()!.landed!.map((each) => each.branch), ['b1'], 'the scratch queue is the one that landed it');
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { drain: string | null; gitDir: string | null; path: string };
      assert.equal(seen.drain, null, 'a stand-in gate\'s variable set in the test process reaches no gate');
      assert.equal(seen.gitDir, null);
      assert.ok(!seen.path.split(delimiter).includes(shims), 'a PATH set in the test process reaches no gate');
    } finally {
      for (const [name, value] of Object.entries(saved)) {
        if (value === undefined) delete process.env[name];
        else process.env[name] = value;
      }
      await repo?.cleanup();
    }
    assert.equal(outerGit('rev-parse', 'HEAD').stdout.trim(), before.head, 'nothing was committed in the repository around the case');
    assert.equal(outerGit('rev-parse', '--verify', '--quiet', 'refs/heads/b1').status, 1, 'no branch of the case is made there');
    assert.equal(readFileSync(join(outer, 'local', 'merge-queue.json'), 'utf8'), before.queue, "the decoy's queue is as it was");
    assert.equal(existsSync(join(outer, 'local', 'merge-queue.log')), false, 'no runner ran on it');
    decoy.cleanup();
  });
});

describe('the queue, end to end in a scratch repository', { concurrency: true }, () => {
  test('QUEUEDROP1: a dropped candidate cannot return when its held gate completes', async () => {
    const repo = queueScratch('drop-late');
    const hold = join(repo.root, 'local', 'release');
    const started = `${hold}.started`;
    try {
      const gate = [
        "import { existsSync, writeFileSync } from 'node:fs';",
        // QUEUETESTLEAK1: the hold's path is written into the gate, since the runner passes no test's variable on to a gate.
        `const hold = ${JSON.stringify(hold)};`,
        "if (process.argv[2] === 'check') {",
        "  writeFileSync(`${hold}.started`, 'started');",
        '  while (!existsSync(hold)) await new Promise((done) => setTimeout(done, 25));',
        '}',
        'console.log(`${process.argv[2]} ran`);',
      ].join('\n');
      repo.branch('b1', { 'a.txt': 'a\n', 'qgate.mjs': gate });
      const before = repo.git('rev-parse', 'main');
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => existsSync(started));
      assert.ok(existsSync(started), repo.log());
      assert.equal((await repo.run(['--drop-batch', 'b1'])).status, 0);
      writeFileSync(hold, 'release');
      assert.equal((await waiting).status, 1);
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(repo.queue()?.candidate, null, repo.log());
      assert.equal(repo.queue()?.stop, null, repo.log());
      assert.equal(repo.git('rev-parse', 'main'), before);
      assert.deepEqual(repo.queue()!.handedBack.map((entry) => entry.handedBack?.how), ['dropped']);
    } finally {
      writeFileSync(hold, 'release');
      await repo.cleanup();
    }
  });

  test('QUEUEWAIT1: requeueing an unchanged rejected tip waits for its new promotion after a base repair', async () => {
    const repo = queueScratch('requeue-same-tip');
    try {
      repo.branch('b1', { 'break-check': 'x\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const rejected = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(rejected.status, 1, rejected.out);
      await repo.until(() => !repo.queue()?.runner);
      const previous = repo.queue()!.handedBack[0]!;
      writeFileSync(join(repo.root, 'qgate.mjs'), 'console.log("the repaired check passed");\n');
      repo.git('add', 'qgate.mjs');
      repo.git('commit', '--quiet', '-m', `repair the base${TRAILER}`);
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const retried = await repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(retried.status, 0, `${retried.out}\n${repo.log()}`);
      assert.match(retried.out, /b1 is in main/);
      assert.deepEqual(repo.queue()!.handedBack, [previous]);
      const landed = repo.queue()!.landed!.at(-1)!;
      assert.equal(landed.tip, previous.tip);
      assert.ok(landed.request);
      assert.notEqual(landed.request, previous.request);
    } finally {
      await repo.cleanup();
    }
  });

  test('preview, then queue with a message: the detached runner forms the candidate in the scope tree, runs it, and fast-forwards main', async () => {
    const repo = queueScratch('green');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      let result = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, /no preview/);
      result = await repo.run(['b1', '--preview']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /nothing is committed/);
      assert.match(result.out, /\+a/);
      assert.equal(existsSync(join(repo.root, 'a.txt')), false, 'a preview touches no file of the checkout');
      result = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(result.status, 0, `${result.out}\n${repo.log()}`);
      assert.match(result.out, /queued b1/);
      assert.match(result.out, /runner started detached/);
      assert.match(result.out, /b1 is in main/);
      assert.equal(readFileSync(join(repo.root, 'a.txt'), 'utf8'), 'a\n', 'the fast-forward moved the checkout');
      assert.equal(repo.git('log', '-1', '--format=%s'), 'Merge b1: its words', "the message is the parent's file, verbatim");
      assert.match(repo.git('log', '-1', '--format=%P'), / /, 'one merge commit, --no-ff');
      const runs = q.readScopeRuns(repo.root) as { state: string; ran: { gate: string }[] }[];
      assert.equal(runs.at(-1)?.state, 'green');
      assert.deepEqual(runs.at(-1)?.ran.map((each) => each.gate).sort(), ['check', 'universal']);
      const verdicts = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { verdicts: { kind?: string }[] };
      assert.ok(verdicts.verdicts.every((each) => each.kind === 'scoped'), 'every verdict says the scoped run gave it');
      result = await repo.run(['--queue']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /the queue is empty/);
    } finally {
      await repo.cleanup();
    }
  });

  test('a red candidate of two: its check run on the base and every prefix, the member that broke it handed back, the other promoted', async () => {
    const repo = queueScratch('red');
    const hold = join(repo.root, 'local', 'go');
    try {
      repo.branch('good', { 'good.txt': 'fine\n' });
      repo.branch('bad', { 'break-check': 'x\n' });
      for (const name of ['good', 'bad']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const good = repo.run(['good', '--message', 'local/good.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const bad = repo.run(['bad', '--message', 'local/bad.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [goodOut, badOut] = await Promise.all([good, bad]);
      assert.equal(badOut.status, 1, `${badOut.out}\n${repo.log()}`);
      assert.match(badOut.out, /bad is handed back \(pinned: check\)/);
      assert.equal(goodOut.status, 0, `${goodOut.out}\n${repo.log()}`);
      assert.equal(existsSync(join(repo.root, 'good.txt')), true);
      assert.equal(existsSync(join(repo.root, 'break-check')), false, 'a red member never reaches main');
      const runs = q.readScopeRuns(repo.root) as ScopeRun[];
      const red = runs.find((each) => each.state === 'red');
      assert.deepEqual(red?.members.map((each) => each.branch), ['good', 'bad'], `one candidate takes both, never good alone first:\n${repo.log()}`);
      assert.deepEqual(red?.judged?.map((each) => [each.outcome, each.member]), [['pinned', 2]]);
      // INTEGRATE1b part 2: it failed again when run once more holding the whole admission, so it was bisected as before.
      assert.deepEqual(red?.confirms?.map((each) => [each.gate, each.result, each.admission?.conclusive]), [['check', 'confirmed', true]], repo.log());
      assert.equal(runs.at(-1)?.state, 'green', 'the retained candidate is judged again, and promoted on its own green');
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('a direct commit that breaks a check stops the queue; a direct commit that fixes it lifts the stop, and the candidate re-forms', async () => {
    const repo = queueScratch('stop');
    try {
      writeFileSync(join(repo.root, 'break-universal'), 'x\n');
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a records commit that breaks a check${TRAILER}`);
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => Boolean(repo.queue()?.stop));
      assert.ok(repo.queue()?.stop, `the queue never stopped:\n${repo.log()}`);
      const said = await repo.run(['--queue']);
      assert.match(said.out, /stopped since/);
      assert.match(said.out, /universal fails on the base/);
      repo.git('rm', '--quiet', 'break-universal');
      repo.git('commit', '--quiet', '-m', `the fix${TRAILER}`);
      const done = await waiting;
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /the queue is stopped/);
      assert.match(repo.log(), /the stop on universal is lifted/);
      assert.equal(existsSync(join(repo.root, 'a.txt')), true);
    } finally {
      await repo.cleanup();
    }
  });

  // INTEGRATE1c (C4): 2026-10-28's set-aside, a branch whose merge commit could not be made because its work was already in an earlier
  // member of the batch. `inner` is a branch at the first of `outer`'s two commits, so queued after it, a candidate holds it already.
  const nested = (repo: ReturnType<typeof queueScratch>, files: Record<string, string> = { 'a.txt': 'a\n', 'b.txt': 'b\n' }, outer = 'outer') => {
    const [head, ...rest] = Object.entries(files);
    const [first, text] = head!;
    repo.branch(outer, { [first]: text });
    repo.git('branch', 'inner', outer);
    repo.git('checkout', '--quiet', outer);
    for (const [path, body] of rest) {
      writeFileSync(join(repo.root, path), body);
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `more work on ${outer}${TRAILER}`);
    }
    repo.git('checkout', '--quiet', 'main');
    writeFileSync(join(repo.root, 'local', 'inner.msg'), `Merge inner: its words${TRAILER}\n`);
  };

  test('a member already in an earlier member of its batch is contained in it, not set aside; settled when that member promotes', async () => {
    const repo = queueScratch('contained');
    const hold = join(repo.root, 'local', 'go');
    try {
      nested(repo);
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const inner = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [outerOut, innerOut] = await Promise.all([outer, inner]);
      assert.equal(outerOut.status, 0, `${outerOut.out}\n${repo.log()}`);
      assert.equal(innerOut.status, 0, `${innerOut.out}\n${repo.log()}`);
      assert.match(innerOut.out, /inner is contained in outer, which promoted/);
      assert.doesNotMatch(repo.log(), /set aside|could not be made/);
      assert.match(repo.log(), /contained: inner is in outer already; settled when outer promotes/);
      assert.match(repo.log(), /contained in them, settled with them: inner/);
      assert.equal(repo.git('rev-list', '--count', '--merges', 'main'), '1', "one merge commit, the container's; nothing was merged for the contained one");
      const queue = repo.queue()!;
      assert.deepEqual(queue.contained!.map((each) => [each.branch, each.containedIn?.branch]), [['inner', 'outer']]);
      assert.equal(queue.handedBack.length, 0);
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('a member the line holds by the time its candidate forms is contained at once, and said so', async () => {
    const repo = queueScratch('contained-line');
    const gate = join(repo.root, 'local', 'gate-go');
    try {
      nested(repo);
      // Queued behind a candidate in flight: the enqueue refuses a branch the line holds, but main lacks inner until outer promotes.
      // Outer's `check` gate waits on `gate` until inner is queued, so outer cannot promote first on a loaded machine (FLAKE1).
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { QGATE_HOLD: gate });
      await repo.until(() => repo.queue()?.candidate !== null && repo.queue()?.candidate !== undefined);
      const inner = repo.run(['inner', '--message', 'local/inner.msg']);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      writeFileSync(gate, '');
      const [outerOut, done] = await Promise.all([outer, inner]);
      assert.equal(outerOut.status, 0, `${outerOut.out}\n${repo.log()}`);
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /inner is contained: main holds it already/);
      assert.doesNotMatch(repo.log(), /set aside|could not be made/);
      assert.deepEqual(repo.queue()!.contained!.map((each) => [each.branch, each.containedIn]), [['inner', { line: true }]]);
      assert.match((await repo.run(['--queue'])).out, /contained: inner \(the line held it already\)/);
    } finally {
      await repo.cleanup();
    }
  });

  test('a contained member whose container is then handed back is judged again, on its own, and promoted', async () => {
    const repo = queueScratch('contained-back');
    const hold = join(repo.root, 'local', 'go');
    try {
      // inner is the work before outer's commit that breaks the check: outer is pinned, and what it held of inner stands alone.
      nested(repo, { 'a.txt': 'a\n', 'break-check': 'x\n' });
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const inner = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [outerOut, innerOut] = await Promise.all([outer, inner]);
      assert.equal(outerOut.status, 1, `${outerOut.out}\n${repo.log()}`);
      assert.match(outerOut.out, /outer is handed back \(pinned: check\)/);
      assert.equal(innerOut.status, 0, `${innerOut.out}\n${repo.log()}`);
      assert.match(innerOut.out, /inner is in main/, 'judged again and landed on its own, not settled as contained in a member that failed');
      assert.equal(existsSync(join(repo.root, 'a.txt')), true);
      assert.equal(existsSync(join(repo.root, 'break-check')), false, 'a red member never reaches main');
      const queue = repo.queue() as unknown as { contained?: unknown[]; landed: { branch: string }[] };
      assert.equal(queue.contained, undefined, 'nothing was settled as contained: the container never promoted');
      assert.deepEqual(queue.landed.map((each) => each.branch), ['inner']);
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  // The review's second finding on INTEGRATE1c: the run's own copy of the candidate still held a contained member dropped meanwhile.
  test('a contained member dropped by name while its container runs, and queued again, is not brought back by the container\'s hand-back: only the new request lands', async () => {
    const repo = queueScratch('contained-dropped');
    const dir = join(repo.root, 'local', 'drain');
    const hold = join(repo.root, 'local', 'go');
    mkdirSync(dir, { recursive: true });
    try {
      // The check holds where `break-check` stands, until `release`: the container's run is under way while the drop comes.
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, "existsSync('break-check')"));
      repo.git('commit', '--quiet', '-am', `a gate that holds a breaking tree${TRAILER}`);
      nested(repo, { 'a.txt': 'a\n', 'break-check': 'x\n' });
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const first = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      await repo.until(() => existsSync(join(dir, 'check.pid')));
      assert.deepEqual(repo.queue()!.candidate!.contained!.map((each) => each.branch), ['inner'], repo.log());
      assert.equal((await repo.run(['--drop-batch', 'inner'])).status, 0);
      const dropped = await first;
      assert.equal(dropped.status, 1, dropped.out);
      const old = repo.queue()!.handedBack.find((each) => each.branch === 'inner')!.request;
      assert.equal((await repo.run(['inner', '--preview'])).status, 0);
      const again = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => repo.queue()?.queued.some((each) => each.branch === 'inner') === true);
      const fresh = repo.queue()!.queued.find((each) => each.branch === 'inner')!.request;
      writeFileSync(join(dir, 'release'), '');
      const [outerOut, againOut] = await Promise.all([outer, again]);
      assert.equal(outerOut.status, 1, `${outerOut.out}\n${repo.log()}`);
      assert.equal(againOut.status, 0, `${againOut.out}\n${repo.log()}`);
      assert.match(againOut.out, /inner is in main/, 'the new request merges on its own');
      const landed = repo.queue()!.landed!.filter((each) => each.branch === 'inner');
      assert.deepEqual(landed.map((each) => each.request), [fresh], `the dropped request ${old} never lands:\n${repo.log()}`);
      assert.equal((repo.queue()!.contained ?? []).length, 0);
    } finally {
      writeFileSync(join(dir, 'release'), '');
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  // The review's fourth finding on INTEGRATE1c: the row tables were read from the candidate's tip, which an earlier member had changed.
  test("a row table a member's own declaration adds settles nothing for a later member of its candidate: the declaration read is the line's the candidate started on", async () => {
    const repo = queueScratch('rows-line');
    const hold = join(repo.root, 'local', 'go');
    try {
      const table = (...rows: string[]) => ['# Table', '', '| When | Run |', '|---|---|', '| one | `a` |', ...rows, ''].join('\n');
      const gates = JSON.parse(readFileSync(join(repo.root, 'daoris.gates.json'), 'utf8')) as { gates: unknown[] };
      mkdirSync(join(repo.root, 'docs'), { recursive: true });
      writeFileSync(join(repo.root, 'docs', 'table.md'), table());
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a table${TRAILER}`);
      const declares = `${JSON.stringify({ ...gates, integration: { merge: [{ paths: 'docs/table.md', by: 'rows' }] } }, null, 2)}\n`;
      repo.branch('declares', { 'daoris.gates.json': declares, 'docs/table.md': table('| two | `b` |') });
      repo.branch('row', { 'docs/table.md': table('| three | `c` |') });
      for (const name of ['declares', 'row']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const first = repo.run(['declares', '--message', 'local/declares.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const second = repo.run(['row', '--message', 'local/row.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [declared, rowed] = await Promise.all([first, second]);
      assert.equal(declared.status, 0, `${declared.out}\n${repo.log()}`);
      assert.equal(rowed.status, 1, `${rowed.out}\n${repo.log()}`);
      assert.match(rowed.out, /row is handed back \(conflict: a conflict in docs\/table\.md\)/);
      assert.doesNotMatch(repo.log(), /resolved by the merge base \(a declared row table\)/);
      assert.equal(readFileSync(join(repo.root, 'docs', 'table.md'), 'utf8'), table('| two | `b` |'));
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test("a preview names a conflict in a row table the line declares as one the merge settles, never as one the resolvers cannot merge", async () => {
    const repo = queueScratch('rows-preview');
    try {
      const table = (...rows: string[]) => ['# Table', '', '| When | Run |', '|---|---|', '| one | `a` |', ...rows, ''].join('\n');
      const gates = JSON.parse(readFileSync(join(repo.root, 'daoris.gates.json'), 'utf8')) as { gates: unknown[] };
      mkdirSync(join(repo.root, 'docs'), { recursive: true });
      writeFileSync(join(repo.root, 'docs', 'table.md'), table());
      writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ ...gates, integration: { merge: [{ paths: 'docs/table.md', by: 'rows' }] } }, null, 2)}\n`);
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a declared table${TRAILER}`);
      repo.branch('row', { 'docs/table.md': table('| three | `c` |') });
      writeFileSync(join(repo.root, 'docs', 'table.md'), table('| two | `b` |'));
      repo.git('commit', '--quiet', '-am', `main adds a row${TRAILER}`);
      const previewed = await repo.run(['row', '--preview']);
      assert.equal(previewed.status, 0, previewed.out);
      assert.match(previewed.out, /conflicts: docs\/table\.md/);
      assert.doesNotMatch(previewed.out, /the resolvers cannot merge/);
      assert.match(previewed.out, /a union record, the backlog, a declared row table or a generated folder/);
    } finally {
      await repo.cleanup();
    }
  });

  // The review's first finding on INTEGRATE1c: the line's marker was the word `line`, which a branch may be named.
  test('a container whose branch is named `line` holds its member provisionally, as any member does: handed back, the member is judged again on its own', async () => {
    const repo = queueScratch('contained-named-line');
    const hold = join(repo.root, 'local', 'go');
    try {
      nested(repo, { 'a.txt': 'a\n', 'break-check': 'x\n' }, 'line');
      for (const name of ['line', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const container = repo.run(['line', '--message', 'local/line.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const inner = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [containerOut, innerOut] = await Promise.all([container, inner]);
      assert.equal(containerOut.status, 1, `${containerOut.out}\n${repo.log()}`);
      assert.match(repo.log(), /contained: inner is in line already; settled when line promotes/);
      assert.equal(innerOut.status, 0, `${innerOut.out}\n${repo.log()}`);
      assert.doesNotMatch(innerOut.out, /main holds it already/, 'never settled as the line\'s');
      assert.match(innerOut.out, /inner is in main/);
      assert.equal(existsSync(join(repo.root, 'a.txt')), true, 'its work is in main, merged on its own');
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('a tracked change in the main checkout holds promotion until it is clean', async () => {
    const repo = queueScratch('dirty');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      writeFileSync(join(repo.root, 'qgate.mjs'), `${QUEUE_GATE}// an edit the parent has not committed\n`);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => repo.queue()?.candidate?.state === 'held');
      assert.equal(repo.queue()?.candidate?.state, 'held', repo.log());
      repo.git('checkout', '--quiet', '--', 'qgate.mjs');
      const done = await waiting;
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /held on checkout/);
    } finally {
      await repo.cleanup();
    }
  });

  test('TOOLTESTHANG1: a test that fails on a held index leaves no enqueue and no runner behind, and an enqueue without a verdict is ended at its bound', async () => {
    const repo = queueScratch('forced-lock');
    let enqueue: number[] = [];
    let runner = 0;
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      writeFileSync(join(repo.root, 'qgate.mjs'), `${QUEUE_GATE}// an edit the parent has not committed\n`);
      // The enqueue the sighting left behind: a candidate green and held on the dirty checkout never gives a verdict.
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => repo.queue()?.candidate?.state === 'held');
      assert.equal(repo.queue()?.candidate?.state, 'held', repo.log());
      enqueue =repo.running();
      runner = (repo.queue()!.runner as { pid: number }).pid;
      assert.equal(enqueue.length, 1);
      // The lock the sighting met, held by another writer: the test's own git write is refused, as it was.
      writeFileSync(join(repo.root, '.git', 'index.lock'), '');
      const refused = spawnSync('git', ['checkout', '--quiet', '--', 'qgate.mjs'], { cwd: repo.root, encoding: 'utf8', env: repo.env });
      assert.notEqual(refused.status, 0);
      assert.match(refused.stderr, /index\.lock/);
      // Ending the test here, as a failed assertion does, must leave neither child holding the run.
      rmSync(join(repo.root, '.git', 'index.lock'), { force: true });
      await repo.cleanup();
      const ended = await waiting;
      assert.notEqual(ended.status, 0, 'the waiting enqueue ended with its test');
      assert.deepEqual(enqueue.filter(alive), [], 'no enqueue is left running');
      assert.equal(alive(runner), false, 'the held runner is ended');
    } finally {
      rmSync(join(repo.root, '.git', 'index.lock'), { force: true });
      await repo.cleanup();
    }
  });

  test('TOOLTESTHANG1: an invocation with no verdict is ended at its bound, saying so', async () => {
    const repo = queueScratch('bound');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      writeFileSync(join(repo.root, 'qgate.mjs'), `${QUEUE_GATE}// an edit the parent has not committed\n`);
      // patience: claim — the invocation is ended at this bound, which is what the row tests
      const ended = await repo.run(['b1', '--message', 'local/b1.msg'], {}, 5_000);
      assert.notEqual(ended.status, 0, ended.out);
      assert.match(ended.out, /no verdict within 5000 ms/);
      assert.deepEqual(repo.running(), []);
    } finally {
      await repo.cleanup();
    }
  });

  // QUEUETESTLEAK1: the leaked runner was started in a folder git read as the session's tree, whose queue home is the main checkout's.
  test('QUEUETESTLEAK1: a runner, a preview, an enqueue, a drop or a recheck started in a linked worktree of the repository is refused, saying whose the queue is, and its queue is left as it was', async () => {
    const repo = queueScratch('linked');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      const linked = join(dirname(repo.root), 'linked');
      repo.git('worktree', 'add', '--quiet', '--detach', linked, 'main');
      mkdirSync(join(linked, 'local'), { recursive: true });
      writeFileSync(join(linked, 'local', 'b1.msg'), `Merge b1: its words${TRAILER}\n`);
      for (const args of [['--queue-run'], ['b1', '--preview'], ['b1', '--message', 'local/b1.msg'], ['--drop-batch', 'b1'], ['--queue', '--recheck']]) {
        const refused = await repo.run(args, {}, HANG_MS, linked);
        assert.equal(refused.status, 2, `${args.join(' ')}: ${refused.out}`);
        assert.match(refused.out, /is not the queue's checkout: the queue is kept in .*repo.*, the repository's main checkout, and only that checkout queues to it or runs it/, args.join(' '));
      }
      assert.equal(repo.queue(), null, 'nothing was written to the queue');
      assert.equal(repo.log(), '', 'no runner was started');
    } finally {
      await repo.cleanup();
    }
  });

  test("QUEUETESTLEAK1: a stand-in gate's variable and the tool's switches a runner inherited from the enqueue that started it reach none of its gates", async () => {
    const repo = queueScratch('gate-env');
    try {
      const record = join(repo.root, 'local', 'gate-env.json');
      repo.branch('b1', { 'qgate.mjs': envGate(record) });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      // The enqueue the leak's was: its environment holds the class probe's folder, and the runner it starts inherits it.
      const done = await repo.run(['b1', '--message', 'local/b1.msg'], { QUEUE_DRAIN_DIR: join(repo.root, 'local', 'drain') });
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { drain: string | null; tick: string | null };
      assert.deepEqual(seen.drain, null, 'the stand-in gate\'s variable is not passed on');
      assert.deepEqual(seen.tick, null, "the tool's own switches are not passed on");
    } finally {
      await repo.cleanup();
    }
  });

  // INTEGRATE1a (F20): the invoker's shell held the install's home, a role and git's locating variables; the runner it started, and the gates
  // of every verb, inherited them, while the machine's admission is still found from the home the tool read at its start.
  const ownedGate = () => JSON.stringify({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check', memoryGb: 0.1 }] });
  // GIT_DIR is set only where the tool's own git may take it: in the queue's verbs it makes the tool's checkout and the scratch one repository,
  // and the tool refuses (`queueHomeRefusal`); git's ceiling, which the case's environment always sets, is the locating variable proved there.
  const shell = (repo: { root: string }, gitDir = false) => ({
    DAORIS_HOME: join(repo.root, 'local', 'install-home'), DAORIS_ROLE: 'steward', MERGE_BRANCH_ADMISSION: 'on', ...(gitDir ? { GIT_DIR: join(repo.root, '.git') } : {}),
  });
  const assertClean = (seen: SeenEnvironment, what: string) => {
    assert.equal(seen.home, null, `${what}: the home reached the gate`);
    assert.equal(seen.role, null, `${what}: a role variable reached the gate`);
    assert.equal(seen.gitDir, null, `${what}: GIT_DIR reached the gate`);
    assert.equal(seen.ceiling, null, `${what}: git's ceiling reached the gate`);
    assert.equal(seen.beside, '1', `${what}: the gate has the build servers of its own`);
  };

  test("INTEGRATE1a: a gate the queue's runner starts sees no Daoris home, role or git locating variable the shell that started the runner held, and the admission is still found from the home", async () => {
    const repo = queueScratch('gate-shell');
    try {
      const home = join(repo.root, 'local', 'install-home');
      mkdirSync(home, { recursive: true });
      const record = join(repo.root, 'local', 'gate-shell.json');
      repo.branch('b1', { 'qgate.mjs': envGate(record, machine.admissionFile(home)), 'daoris.gates.json': ownedGate() });
      assert.equal((await repo.run(['b1', '--preview'], shell(repo))).status, 0);
      const done = await repo.run(['b1', '--message', 'local/b1.msg'], shell(repo));
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as SeenEnvironment;
      assertClean(seen, 'the queue');
      assert.deepEqual(seen.held, ['check gate'], 'the runner took its place in the admission of the home it read at its start');
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1a: a merge in place, a gate run again and a full run of the checkout start their gates the same way', async () => {
    const repo = queueScratch('gate-shell-direct');
    try {
      const record = join(repo.root, 'local', 'gate-shell-direct.json');
      // `universal` runs at every merge and every full run; `check` is skipped by the lane table for a file like this.
      repo.branch('b1', { 'qgate.mjs': envGate(record, '', 'universal') });
      // A gate run again on the checkout answers 1 while the full set lacks a verdict (`check` has none here): the gate ran all the same.
      const seenAfter = (what: string, status: number | null, out: string) => {
        assert.ok(status === 0 || (status === 1 && /^merge-branch: .*has NOT passed this checkout/m.test(out)), `${what}: ${status} ${out}`);
        assert.ok(existsSync(record), `${what}: the check gate ran: ${out}`);
        assertClean(JSON.parse(readFileSync(record, 'utf8')) as SeenEnvironment, what);
        rmSync(record);
      };
      const direct = shell(repo, true);
      const merged = await repo.run(['b1'], direct);
      seenAfter('merge in place', merged.status, merged.out);
      const open = await repo.run(['--rerun', 'universal'], direct);
      seenAfter('--rerun with the merge open', open.status, open.out);
      repo.git('commit', '--quiet', '-m', `Merge b1${TRAILER}`);
      const again = await repo.run(['--rerun', 'universal'], direct);
      seenAfter('--rerun with none open', again.status, again.out);
      const full = await repo.run(['--full'], direct);
      seenAfter('--full', full.status, full.out);
      const beside = await repo.run(['--full', '--beside'], direct);
      seenAfter('--full --beside', beside.status, beside.out);
    } finally {
      await repo.cleanup();
    }
  });

  // INTEGRATE1j part 1 (design §5.9, D192 points 6 and 9): this tool as the declared runner, `--full --beside --daoris-verify`, its inputs in
  // DAORIS_VERIFY_* set on the job alone, the gates read from the declaration handed and never from the tree.
  type Scratch = ReturnType<typeof queueScratch>;
  const VERIFY = ['--full', '--beside', '--daoris-verify'];
  const verifier = (repo: Scratch) => {
    const dir = join(repo.root, 'local', 'verify');
    mkdirSync(dir, { recursive: true });
    const declaration = join(dir, 'declaration.json');
    const answer = join(dir, 'answer.json');
    return {
      answer,
      declare: (declared: object) => writeFileSync(declaration, JSON.stringify(declared)),
      env: (commit: string, more: Record<string, string> = {}) => ({ DAORIS_VERIFY_COMMIT: commit, DAORIS_VERIFY_RESULT: answer, DAORIS_VERIFY_DECLARATION: declaration, ...more }),
      read: () => (existsSync(answer) ? JSON.parse(readFileSync(answer, 'utf8')) as Record<string, any> : null),
    };
  };
  /** A gate that records which Daoris variables reached it, where it runs, and whether the private patterns file was copied in. */
  const recorderGate = (file: string) => [
    "import { existsSync, writeFileSync } from 'node:fs';",
    `writeFileSync(${JSON.stringify(file)}, JSON.stringify({ daoris: Object.keys(process.env).filter((name) => /^DAORIS_/i.test(name)), cwd: process.cwd(), copied: existsSync('local/sensitive-patterns.txt') }));`,
    "console.log('recorder ran');",
    '',
  ].join('\n');
  const commitOnMain = (repo: Scratch, files: Record<string, string>, what: string) => {
    for (const [path, text] of Object.entries(files)) writeFileSync(join(repo.root, path), text);
    repo.git('add', '-A');
    repo.git('commit', '--quiet', '-m', `${what}${TRAILER}`);
    return repo.git('rev-parse', 'HEAD');
  };

  test('INTEGRATE1j: the declared runner answers D192\'s schema 1 when no mode is asked: each declared gate under its own name, the full commit, its verdict and exit, its log kept where it stays', async () => {
    const repo = queueScratch('verify-answer');
    try {
      const commit = commitOnMain(repo, { 'break-check': '' }, 'a gate that fails');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'The universal gate', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check' }] });
      writeFileSync(run.answer, '{"schema":1,"commit":"stale","gates":[]}');
      const done = await repo.run([...VERIFY, '--keep-going'], run.env(commit));
      assert.equal(done.status, 1, done.out);
      const answer = run.read()!;
      assert.deepEqual(Object.keys(answer).sort(), ['commit', 'gates', 'schema']);
      assert.equal(answer.schema, 1);
      assert.equal(answer.commit, commit, 'the full commit, not seven characters');
      type Answered = { name: string; verdict: string; exit?: number; wallSeconds?: number; log: string };
      const byName: Record<string, Answered> = Object.fromEntries((answer.gates as Answered[]).map((gate) => [gate.name, gate]));
      assert.deepEqual(Object.keys(byName).sort(), ['The universal gate', 'check']);
      assert.equal(byName['The universal gate']!.verdict, 'pass');
      assert.equal(byName['check']!.verdict, 'fail');
      assert.equal(byName['check']!.exit, 1);
      for (const gate of Object.values(byName)) {
        assert.equal(typeof gate.wallSeconds, 'number', gate.name);
        // The log stays in the main checkout once the tree moves, not in the gate tree the next run clears.
        assert.ok(gate.log.startsWith(join(repo.root, 'local', 'scratch', `beside-${commit.slice(0, 7)}`)), gate.log);
        assert.ok(existsSync(gate.log), gate.log);
      }
      assert.match(readFileSync(byName['The universal gate']!.log, 'utf8'), /universal ran/);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.json')), false, 'a stage of this tree is not allowed by a runner\'s answer');
      // The gate tree's lock is let go: a second run is not busy.
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false);
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: a member\'s edit of a gate\'s command in the tree does not run; the gates are the handed declaration\'s, and no DAORIS_VERIFY_* reaches one', async () => {
    const repo = queueScratch('verify-edit');
    try {
      const record = join(repo.root, 'local', 'recorded.json');
      mkdirSync(join(repo.root, 'local'), { recursive: true });
      writeFileSync(join(repo.root, 'local', 'sensitive-patterns.txt'), 'x\n');
      const commit = commitOnMain(repo, {
        'vgate.mjs': recorderGate(record),
        'edit.mjs': "import { writeFileSync } from 'node:fs';\nwriteFileSync('edited.marker', 'ran');\n",
        'daoris.gates.json': JSON.stringify({ gates: [{ name: 'recorder', run: 'node edit.mjs' }] }),
      }, 'a member edits the gate\'s command');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'recorder', run: 'node vgate.mjs' }] });
      const done = await repo.run(VERIFY, run.env(commit, { DAORIS_HOME: join(repo.root, 'local', 'install-home') }));
      assert.equal(done.status, 0, done.out);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree', 'edited.marker')), false, 'the edited command in the tree did not run');
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { daoris: string[]; cwd: string; copied: boolean };
      assert.deepEqual(seen.daoris, [], 'no Daoris variable reached the gate, DAORIS_VERIFY_* among them');
      assert.equal(realPath(seen.cwd), realPath(join(repo.root, 'local', 'gate-tree')), 'the runner made its own tree');
      assert.equal(seen.copied, true, 'the private files a gate reads are copied into a tree the runner made');
      assert.deepEqual(run.read()!.gates.map((gate: { name: string; verdict: string }) => [gate.name, gate.verdict]), [['recorder', 'pass']]);
    } finally {
      await repo.cleanup();
    }
  });

  // INTEGRATE1d part 4: the declaration the runner is handed is the accepted blob (design §5.9), so its `env.pass` is the owner's.
  test('INTEGRATE1d: the declared runner passes the names the declaration handed lists, from its start environment, and no Daoris name or other removed one', async () => {
    const repo = queueScratch('verify-pass');
    try {
      const record = join(repo.root, 'local', 'passed.json');
      mkdirSync(join(repo.root, 'local'), { recursive: true });
      writeFileSync(join(repo.root, 'local', 'sensitive-patterns.txt'), 'x\n');
      const commit = commitOnMain(repo, {
        'pgate.mjs': [
          "import { writeFileSync } from 'node:fs';",
          `writeFileSync(${JSON.stringify(record)}, JSON.stringify(Object.fromEntries(['GIT_CONFIG_GLOBAL', 'GIT_CONFIG_NOSYSTEM', 'DAORIS_HOME'].map((name) => [name, process.env[name] ?? null]))));`,
          '',
        ].join('\n'),
      }, 'a gate that reads what it was passed');
      const run = verifier(repo);
      // `DAORIS_HOME` is refused by the declaration's own read, so the case declares only what a declaration may; the install's home rides the shell.
      run.declare({ gates: [{ name: 'passed', run: 'node pgate.mjs' }], integration: { env: { pass: ['GIT_CONFIG_GLOBAL'] } } });
      const done = await repo.run(VERIFY, run.env(commit, { DAORIS_HOME: join(repo.root, 'local', 'install-home'), GIT_CONFIG_GLOBAL: 'X:/cfg', GIT_CONFIG_NOSYSTEM: '1' }));
      assert.equal(done.status, 0, done.out);
      assert.deepEqual(JSON.parse(readFileSync(record, 'utf8')), { GIT_CONFIG_GLOBAL: 'X:/cfg', GIT_CONFIG_NOSYSTEM: null, DAORIS_HOME: null }, 'only the name the declaration passes came through');
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: a held gate-tree lock writes busy to the answer first and exits 75; without the flag the refusal is exit 2 as it was', async () => {
    const repo = queueScratch('verify-busy');
    try {
      const commit = repo.git('rev-parse', 'HEAD');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }] });
      writeFileSync(join(repo.root, 'local', 'gate-tree.lock'), `${JSON.stringify({ pid: process.pid, commit, at: '2026-10-29T10:00:00Z', holder: 'full' })}\n`);
      const busy = await repo.run(VERIFY, run.env(commit));
      assert.equal(busy.status, 75, busy.out);
      assert.match(busy.out, /a full set beside the merges is running already/);
      assert.deepEqual(run.read(), { schema: 1, commit, busy: true });
      // The variable a person's shell left behind retargets nothing: without the flag the run is the person's own and refuses as before.
      rmSync(run.answer);
      const own = await repo.run(['--full', '--beside'], run.env(commit));
      assert.equal(own.status, 2, own.out);
      assert.equal(run.read(), null, 'nothing is written without the flag');
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: the commits mode is the commit check on base..tip, a schema-2 answer naming the declaration blob it read', async () => {
    const repo = queueScratch('verify-commits');
    try {
      const base = repo.git('rev-parse', 'HEAD');
      repo.git('checkout', '--quiet', '-b', 'work');
      commitOnMain(repo, { 'a.txt': 'a\n' }, 'work with its maker');
      repo.git('commit', '--quiet', '--allow-empty', '-m', 'work with none');
      const tip = repo.git('rev-parse', 'HEAD');
      const run = verifier(repo);
      const declared = { gates: [{ name: 'universal', run: 'node qgate.mjs universal' }] };
      run.declare(declared);
      const more = { DAORIS_VERIFY_MODE: 'commits', DAORIS_VERIFY_BASE: base };
      const red = await repo.run(VERIFY, run.env(tip, more));
      assert.equal(red.status, 1, red.out);
      const answer = run.read()!;
      assert.equal(answer.schema, 2);
      assert.deepEqual([answer.mode, answer.commit, answer.base], ['commits', tip, base]);
      assert.equal(answer.declaration, repo.git('hash-object', join(repo.root, 'local', 'verify', 'declaration.json')));
      assert.equal(answer.problems.length, 1, JSON.stringify(answer));
      assert.match(answer.problems[0], /work with none: no Co-Authored-By line/);
      // A range whose commits all carry their maker has no problem, and exit 0; no gate ran.
      const clean = repo.git('rev-parse', 'HEAD~1');
      const green = await repo.run(VERIFY, run.env(clean, more));
      assert.equal(green.status, 0, green.out);
      assert.deepEqual(run.read()!.problems, []);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree')), false, 'the commits mode makes no tree');
      // A declaration that checks commits by a gate leaves the check to that gate: the runner's mode refuses.
      run.declare({ ...declared, integration: { commits: 'universal' } });
      const gate = await repo.run(VERIFY, run.env(tip, more));
      assert.equal(gate.status, 2, gate.out);
      assert.match(gate.out, /checks commits by its gate 'universal', not by the runner/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: a tree the caller hands is the run\'s tree: the runner makes none, takes no lock and copies no private file; one at another commit is refused', async () => {
    const repo = queueScratch('verify-tree');
    try {
      const record = join(repo.root, 'local', 'recorded.json');
      writeFileSync(join(repo.root, 'local', 'sensitive-patterns.txt'), 'x\n');
      const first = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'vgate.mjs': recorderGate(record) }, 'a recorder');
      const handed = join(dirname(repo.root), 'handed');
      repo.git('worktree', 'add', '--quiet', '--detach', handed, commit);
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'recorder', run: 'node vgate.mjs' }] });
      const done = await repo.run(VERIFY, run.env(commit, { DAORIS_VERIFY_TREE: handed }));
      assert.equal(done.status, 0, done.out);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { cwd: string; copied: boolean };
      assert.equal(realPath(seen.cwd), realPath(handed), 'the gate ran in the caller\'s tree');
      assert.equal(seen.copied, false, 'a private file is not copied into the caller\'s tree');
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree')), false, 'the runner made no tree of its own');
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'and took no lock');
      assert.equal(run.read()!.gates[0].log.startsWith(join(repo.root, 'local', 'scratch', `beside-${commit.slice(0, 7)}`)), true);
      // A tree standing at another commit than the one named is the caller's mistake: refused, nothing moved, nothing run.
      rmSync(run.answer);
      repo.git('-C', handed, 'checkout', '--quiet', '--detach', first);
      const wrong = await repo.run(VERIFY, run.env(commit, { DAORIS_VERIFY_TREE: handed }));
      assert.equal(wrong.status, 2, wrong.out);
      assert.match(wrong.out, /DAORIS_VERIFY_TREE stands at .* not at DAORIS_VERIFY_COMMIT/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: the runner refuses a missing, malformed or contradicting input in a sentence with exit 2 and no answer, and the variable left in a shell retargets no run without the flag', async () => {
    const repo = queueScratch('verify-refusals');
    try {
      const first = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'later.txt': 'later\n' }, 'a later commit');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }] });
      const refused = async (what: string, env: Record<string, string>, message: RegExp) => {
        const done = await repo.run(VERIFY, env);
        assert.equal(done.status, 2, `${what}: ${done.out}`);
        assert.match(done.out, message, what);
        assert.equal(run.read(), null, `${what}: no answer is written for an input refused`);
      };
      await refused('nothing set', {}, /DAORIS_VERIFY_RESULT is not set/);
      await refused('no commit', { DAORIS_VERIFY_RESULT: run.answer }, /DAORIS_VERIFY_COMMIT is not set/);
      await refused('a run with no base', run.env(commit, { DAORIS_VERIFY_MODE: 'run' }), /DAORIS_VERIFY_BASE is not set: the run mode reaches its gates by base\.\.commit/);
      await refused('a commit the repository lacks', run.env('f'.repeat(40)), /is no commit of this repository/);
      await refused('a declaration that is no file', run.env(commit, { DAORIS_VERIFY_DECLARATION: join(repo.root, 'local', 'verify', 'none.json') }), /DAORIS_VERIFY_DECLARATION names no file/);
      const named = await repo.run(['--full', '--beside', commit, '--daoris-verify'], run.env(commit));
      assert.equal(named.status, 2, named.out);
      assert.match(named.out, /takes its commit from DAORIS_VERIFY_COMMIT/);
      // No flag: the variable names nothing, and `--full --beside` gates main's tip as it always did.
      const own = await repo.run(['--full', '--beside'], run.env(first));
      assert.equal(own.status, 0, own.out);
      assert.match(own.out, new RegExp(`the full set on ${commit.slice(0, 7)}`));
      assert.equal(run.read(), null, 'nothing is written without the flag');
    } finally {
      await repo.cleanup();
    }
  });

  // INTEGRATE1j part 2 (design §4.3, §4.5, §5.9): the schema-2 run, plan and probe modes.
  type Answered = Record<string, any>;
  const modes = (repo: Scratch, run: ReturnType<typeof verifier>, base: string) => (mode: string, commit: string, more: Record<string, string> = {}) => (
    repo.run(VERIFY, run.env(commit, { DAORIS_VERIFY_MODE: mode, DAORIS_VERIFY_BASE: base, ...more })));

  test('INTEGRATE1j: the run mode answers by gate, each with its composition kept beside its log and the failures read from it, beside what its plan kept and deferred', async () => {
    const repo = queueScratch('verify-run');
    try {
      const base = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'break-check': '' }, 'a gate that fails');
      const run = verifier(repo);
      run.declare({ gates: [
        { name: 'universal', run: 'node qgate.mjs universal' }, { name: 'The check', run: 'node qgate.mjs check' }, { name: 'rehearse', run: 'npm run rehearse:fixture' },
      ] });
      const done = await modes(repo, run, base)('run', commit, {});
      assert.equal(done.status, 1, done.out);
      const answer = run.read()!;
      assert.deepEqual(Object.keys(answer).sort(), ['base', 'commit', 'declaration', 'deferred', 'gates', 'kept', 'mode', 'schema']);
      assert.deepEqual([answer.schema, answer.mode, answer.commit, answer.base], [2, 'run', commit, base]);
      assert.equal(answer.declaration, repo.git('hash-object', join(repo.root, 'local', 'verify', 'declaration.json')));
      // The scoped set (§4.3): the cheap tier and what the change reaches run; a rehearsal is deferred to the background run.
      assert.deepEqual(answer.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'pass'], ['The check', 'fail']]);
      assert.deepEqual(answer.kept, []);
      assert.deepEqual(answer.deferred.map((each: Answered) => each.name), ['rehearse']);
      for (const gate of answer.gates as Answered[]) {
        // Run whole, so no units; its settings kept beside its log, which the digest is of with them.
        assert.equal(gate.composition.units, null, gate.name);
        assert.match(gate.composition.digest, /^sha256:[0-9a-f]{64}$/);
        assert.equal(dirname(gate.composition.settings), dirname(gate.log), 'beside its log, where it stays once the tree moves');
        const kept = JSON.parse(readFileSync(gate.composition.settings, 'utf8')) as Answered;
        assert.equal(kept.digest, gate.composition.digest);
        assert.deepEqual([kept.settings.gate, kept.settings.runsettings, kept.settings.narrowed], [gate.name, null, []]);
        assert.deepEqual(kept.settings.environment.added, tool.BESIDE_ENVIRONMENT);
      }
      // A gate that names no failing test fails by its step, signed under the name the declaration writes.
      const check = (answer.gates as Answered[]).find((gate) => gate.name === 'The check')!;
      assert.deepEqual(check.failures, [{ unit: 'The check', test: 'The check', signature: { gate: 'The check', step: 'run', message: 'error: check is broken' } }]);
      assert.deepEqual((answer.gates as Answered[]).find((gate) => gate.name === 'universal')!.failures, []);
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: the plan mode says what a run would run, keep and defer, running nothing and the same each time for a tree; a run then keeps what passed, on the commit and tree it passed on; DAORIS_VERIFY_GATES narrows both', async () => {
    const repo = queueScratch('verify-plan');
    try {
      const record = join(repo.root, 'local', 'recorded.json');
      const base = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'vgate.mjs': recorderGate(record) }, 'a recorder');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'The recorder', run: 'node vgate.mjs' }, { name: 'rehearse', run: 'npm run rehearse:fixture' }] });
      const asked = modes(repo, run, base);
      const planned = await asked('plan', commit);
      assert.equal(planned.status, 0, planned.out);
      const first = run.read()!;
      assert.deepEqual(Object.keys(first).sort(), ['base', 'commit', 'declaration', 'deferred', 'kept', 'mode', 'run', 'schema']);
      assert.deepEqual([first.schema, first.mode, first.commit, first.base], [2, 'plan', commit, base]);
      assert.deepEqual(first.run.map((each: Answered) => each.name), ['universal', 'The recorder']);
      assert.equal(first.run[0].why, 'the cheap tier runs in every candidate');
      assert.deepEqual(first.kept, []);
      assert.deepEqual(first.deferred, [{ name: 'rehearse', why: 'a browser spec or a rehearsal: deferred to the background run, which the install waits for' }]);
      assert.equal(existsSync(record), false, 'a plan runs nothing');
      // The same answer for the same tree.
      assert.equal((await asked('plan', commit)).status, 0);
      assert.deepEqual(run.read(), first);
      // A run runs the plan's run; their passes stand on its tree, so the next plan keeps them, on that commit and tree.
      const ran = await asked('run', commit);
      assert.equal(ran.status, 0, ran.out);
      assert.equal(existsSync(record), true);
      assert.deepEqual(run.read()!.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'pass'], ['The recorder', 'pass']]);
      assert.equal((await asked('plan', commit)).status, 0);
      const tree = repo.git('rev-parse', `${commit}^{tree}`);
      const again = run.read()!;
      assert.deepEqual(again.run, []);
      assert.deepEqual(again.kept, [
        { name: 'universal', why: 'its pass stands on this tree', commit, tree, standing: 'exact' },
        { name: 'The recorder', why: 'its pass stands on this tree', commit, tree, standing: 'exact' },
      ]);
      // DAORIS_VERIFY_GATES: the gates named, each run whole, nothing kept or deferred; a name the declaration does not write is refused.
      rmSync(record);
      assert.equal((await asked('plan', commit, { DAORIS_VERIFY_GATES: 'The recorder' })).status, 0);
      assert.deepEqual([run.read()!.run, run.read()!.kept, run.read()!.deferred], [[{ name: 'The recorder', why: 'named in DAORIS_VERIFY_GATES: it runs whole' }], [], []]);
      const named = await asked('run', commit, { DAORIS_VERIFY_GATES: 'The recorder' });
      assert.equal(named.status, 0, named.out);
      assert.deepEqual(run.read()!.gates.map((gate: Answered) => gate.name), ['The recorder']);
      assert.equal(existsSync(record), true, 'a gate named runs, whatever pass of it stands');
      const unknown = await asked('run', commit, { DAORIS_VERIFY_GATES: 'recorder' });
      assert.equal(unknown.status, 2, unknown.out);
      assert.match(unknown.out, /DAORIS_VERIFY_GATES names 'recorder', which the declaration handed does not declare/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });

  /**
   * A stand-in `dotnet test` with a pollution within a class: `PollutedTests.B_Reads` fails only after `A_Pollutes` ran before it in the
   * same run, so it fails in its gate and in its class run alone, and passes run by itself. It runs the tests its `--filter` selects,
   * writes their results to the trx its logger names, and records each call's arguments in `calls`. With `marker`, the pollution is
   * only in a tree that holds that file (a member's change), so the base and a prefix without it pass.
   */
  const pollutedDotnet = (calls: string, marker = '') => [
    "import { appendFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';",
    "import { dirname } from 'node:path';",
    'const args = process.argv.slice(2);',
    `appendFileSync(${JSON.stringify(calls)}, JSON.stringify(args) + '\\n');`,
    'const value = (flag) => { const at = args.indexOf(flag); return at >= 0 ? args[at + 1] : null; };',
    "const filter = value('--filter') ?? '';",
    "const trx = /LogFileName=(.+)$/.exec(value('--logger') ?? '')?.[1] ?? null;",
    "const tests = ['Fixture.PollutedTests.A_Pollutes', 'Fixture.PollutedTests.B_Reads', 'Fixture.OtherTests.C_Passes'];",
    "const holds = (test) => filter.replace(/[()]/g, '').split('&').map((term) => term.trim()).filter(Boolean).every((term) => {",
    '  const not = /^FullyQualifiedName!~(.+)$/.exec(term);',
    '  if (not) return !test.includes(not[1]);',
    '  const has = /^FullyQualifiedName~(.+)$/.exec(term);',
    '  return has ? test.includes(has[1]) : true;',
    '});',
    'const ran = tests.filter(holds);',
    `const polluting = ${JSON.stringify(marker)} ? existsSync(${JSON.stringify(marker)}) : true;`,
    'const failed = polluting && ran.includes(tests[0]) && ran.includes(tests[1]) ? [tests[1]] : [];',
    'const id = (test) => `t${tests.indexOf(test)}`;',
    'const own = (test) => test.split(\'.\').slice(0, -1).join(\'.\');',
    'if (trx) {',
    '  mkdirSync(dirname(trx), { recursive: true });',
    '  const results = ran.map((test) => (failed.includes(test)',
    '    ? `<UnitTestResult testId="${id(test)}" testName="${test}" outcome="Failed"><Output><ErrorInfo><Message>Assert.Equal() Failure: the state A_Pollutes left</Message><StackTrace>   at ${test}()</StackTrace></ErrorInfo></Output></UnitTestResult>`',
    '    : `<UnitTestResult testId="${id(test)}" testName="${test}" outcome="Passed" />`));',
    '  const definitions = ran.map((test) => `<UnitTest id="${id(test)}" name="${test}"><TestMethod className="${own(test)}" name="${test.split(\'.\').at(-1)}" /></UnitTest>`);',
    '  writeFileSync(trx, `<?xml version="1.0" encoding="utf-8"?><TestRun id="r"><Results>${results.join(\'\')}</Results><TestDefinitions>${definitions.join(\'\')}</TestDefinitions></TestRun>`);',
    '}',
    'for (const test of ran) console.log(`  ${failed.includes(test) ? \'Failed\' : \'Passed\'} ${test}`);',
    'process.exit(failed.length ? 1 : 0);',
    '',
  ].join('\n');

  test('INTEGRATE1j: a probe repeats a failure as composed, so a test its own class pollutes fails again with its signature where alone it passes; a composition that would differ, or is not the one kept, is refused', async () => {
    const repo = queueScratch('verify-probe');
    const shims = join(repo.root, 'local', 'shims');
    mkdirSync(shims, { recursive: true });
    try {
      const calls = join(repo.root, 'local', 'dotnet-calls.jsonl');
      writeFileSync(join(shims, 'dotnet.mjs'), pollutedDotnet(calls));
      if (process.platform === 'win32') writeFileSync(join(shims, 'dotnet.cmd'), '@node "%~dp0dotnet.mjs" %*\r\n');
      else writeFileSync(join(shims, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/dotnet.mjs" "$@"\n', { mode: 0o755 });
      // The stand-in is first on the runner's own PATH as it starts (QUEUETESTLEAK1): the test process's environment is never changed.
      const PATH = `${shims}${delimiter}${pathOf(repo.env)}`;
      const base = repo.git('rev-parse', 'HEAD');
      const settings = '<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration></RunSettings>\n';
      const commit = commitOnMain(repo, { 'fixture.runsettings': settings, 'Fixture.csproj': '<Project />\n' }, 'a test project');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'The fixture', run: 'dotnet test Fixture.csproj --settings fixture.runsettings --filter "Category!=Process"' }] });
      const asked = modes(repo, run, base);
      const ran = await asked('run', commit, { PATH });
      assert.equal(ran.status, 1, ran.out);
      const [gate] = run.read()!.gates as Answered[];
      assert.equal(gate!.verdict, 'fail');
      assert.deepEqual(gate!.failures.map((each: Answered) => [each.unit, each.test]), [['Fixture.PollutedTests', 'Fixture.PollutedTests.B_Reads']]);
      const kept = JSON.parse(readFileSync(gate!.composition.settings, 'utf8')) as Answered;
      assert.deepEqual(kept.settings.runsettings, { path: 'fixture.runsettings', text: settings }, 'the run settings as the commit holds them');
      // Alone, the failing test passes: it fails only after the test before it in its class.
      const alone = spawnSync(process.execPath, [join(shims, 'dotnet.mjs'), 'test', 'Fixture.csproj', '--filter', 'FullyQualifiedName~Fixture.PollutedTests.B_Reads'], { encoding: 'utf8' });
      assert.equal(alone.status, 0, alone.stdout);
      // The probe runs its class as the gate composed it, and it fails again with its signature.
      const failure = join(repo.root, 'local', 'verify', 'failure.json');
      const named = (composition: object) => writeFileSync(failure, JSON.stringify({ gate: gate!.name, ...gate!.failures[0], composition }));
      named(gate!.composition);
      const probe = (at: string) => asked('probe', at, { DAORIS_VERIFY_FAILURE: failure, PATH });
      const again = await probe(commit);
      assert.equal(again.status, 1, again.out);
      const answer = run.read()!;
      assert.deepEqual(Object.keys(answer).sort(), ['base', 'commit', 'declaration', 'log', 'mode', 'result', 'schema', 'signature']);
      assert.deepEqual([answer.schema, answer.mode, answer.commit, answer.base, answer.result], [2, 'probe', commit, base, 'fail']);
      assert.deepEqual(answer.signature, gate!.failures[0].signature);
      assert.ok(existsSync(answer.log), answer.log);
      const last = readFileSync(calls, 'utf8').trim().split('\n').at(-1)!;
      assert.match(last, /\(Category!=Process\)&\(FullyQualifiedName~Fixture\.PollutedTests\)/, 'the probe ran the failing class, not the test alone');
      assert.match(last, /fixture\.runsettings/, 'with the gate\'s own run settings');
      // At a commit whose run settings differ, the composition would differ: refused, nothing written, nothing run.
      const later = commitOnMain(repo, { 'fixture.runsettings': settings.replace('>1<', '>4<') }, 'the tests run four at once');
      const before = readFileSync(calls, 'utf8');
      const differs = await probe(later);
      assert.equal(differs.status, 2, differs.out);
      assert.match(differs.out, new RegExp(`the composition at ${later.slice(0, 7)} differs from the run's \\(the run settings fixture\\.runsettings\\)`));
      assert.equal(run.read(), null);
      assert.equal(readFileSync(calls, 'utf8'), before, 'nothing ran');
      // A digest that is not the kept composition's is refused too.
      named({ ...gate!.composition, digest: `sha256:${'0'.repeat(64)}` });
      const forged = await probe(commit);
      assert.equal(forged.status, 2, forged.out);
      assert.match(forged.out, /the composition handed is not the one kept/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1j: a run that started and did not finish (npm ci failing in its tree) answers so, each gate not run, and exits 2; D192\'s schema 1 does the same', async () => {
    const repo = queueScratch('verify-unfinished');
    try {
      const base = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'package.json': '{ "name": "fixture", "version": "1.0.0" }\n', 'package-lock.json': '{ not json\n' }, 'a lockfile npm cannot read');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check' }] });
      const stopped = await modes(repo, run, base)('run', commit);
      assert.equal(stopped.status, 2, stopped.out);
      const answer = run.read()!;
      assert.deepEqual([answer.schema, answer.mode, answer.commit, answer.base], [2, 'run', commit, base]);
      assert.match(answer.unfinished, /^the run did not finish: npm ci failed in the gate tree's \./);
      assert.deepEqual(answer.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'not-run'], ['check', 'not-run']]);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');
      const plain = await repo.run(VERIFY, run.env(commit));
      assert.equal(plain.status, 2, plain.out);
      const said = run.read()!;
      assert.equal(said.schema, 1);
      assert.match(said.unfinished, /^the run did not finish: npm ci failed/);
      assert.deepEqual(said.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'not-run'], ['check', 'not-run']]);
    } finally {
      await repo.cleanup();
    }
  });

  test('QUEUETESTLEAK1: a probe the runner starts gets the environment its process had at its start: a PATH and a variable set in it later reach no probe', async () => {
    const repo = queueScratch('probe-env');
    const dir = join(repo.root, 'local', 'probe');
    const shims = join(dirname(repo.root), 'shims');
    mkdirSync(dir, { recursive: true });
    mkdirSync(shims, { recursive: true });
    try {
      const gate = join(dirname(repo.root), 'envgate.mjs');
      const record = join(dirname(repo.root), 'probe-env.json');
      writeFileSync(gate, envGate(record));
      const base = repo.git('rev-parse', 'main');
      const candidate: Candidate = { base, tip: base, members: [], state: 'running', reforms: 0, recordsOnly: false };
      const failure: Failure = { unit: 'check', test: 'check', gate: 'check', signature: { gate: 'check', step: 'run' }, names: 'gate' };
      const after = { PATH: `${shims}${delimiter}${pathOf(repo.env)}`, QUEUE_DRAIN_DIR: dir };
      const probed = await repo.probe({ candidate, plan: [{ name: 'check', run: `node "${gate}" check` }], dir, failure, ks: [0], after });
      assert.equal(probed.status, 0, probed.out);
      assert.deepEqual(probed.lines.map((line) => line.answer?.result), ['pass'], probed.out);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { drain: string | null; path: string };
      assert.equal(seen.drain, null, 'a variable set in the runner after its start is not passed on');
      assert.ok(!seen.path.split(delimiter).includes(shims), 'a PATH set in the runner after its start is not passed on');
    } finally {
      await repo.cleanup();
    }
  });

  // QUEUEWAITEND1 (2026-10-28): eleven waiters whose requests had left the queue (dropped while queued, or merged in another branch's
  // batch) started a runner every second for a day.
  test('QUEUEWAITEND1: a request dropped while queued is told to its waiter, which ends saying so; a request gone from every record ends its wait too', async () => {
    const repo = queueScratch('wait-ends');
    const hold = join(repo.root, 'local', 'go');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      repo.branch('b2', { 'b.txt': 'b\n' });
      for (const name of ['b1', 'b2']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      // The runner holds before it forms, so both requests stay queued; a waiter that never ends is ended at the shared patience.
      const first = repo.run(['b1', '--message', 'local/b1.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1 && Boolean(repo.queue()?.runner));
      const second = repo.run(['b2', '--message', 'local/b2.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      assert.equal((await repo.run(['--drop-batch', 'b1'])).status, 0);
      const dropped = await first;
      assert.equal(dropped.status, 1, dropped.out);
      assert.match(dropped.out, /b1's request was taken off the queue \(--drop-batch, .*\): nothing of it was merged by this request/);
      // b2's request leaves every record with no outcome (a record written by hand): its waiter says so and ends.
      q.writeQueue(repo.root, { ...repo.queue()!, queued: [] });
      const gone = await second;
      assert.equal(gone.status, 1, gone.out);
      assert.match(gone.out, /b2's request \([0-9a-f]{8}\) is no longer in the queue's records/);
      assert.deepEqual(repo.queue()!.handedBack.map((each) => [each.branch, each.handedBack?.how]), [['b1', 'dropped']]);
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('QUEUEDRAIN1: a candidate dropped while its gate runs ends the gate, its child and grandchild; the scoped tier is released after; what is not its own survives; the next candidate forms at once', async () => {
    const repo = queueScratch('drain-gate');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    const bystander = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore' });
    try {
      repo.branch('b1', { 'hold-check': 'x\n', 'qgate.mjs': treeGate(dir, "existsSync('hold-check')") });
      repo.branch('b2', { 'b.txt': 'b\n' });
      for (const name of ['b1', 'b2']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const first = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.ok(pids.every(alive), 'the gate and what it started run');
      await repo.until(() => repo.admission()?.scopedTier?.state === 'running');
      assert.equal(repo.admission()?.scopedTier?.state, 'running');
      const second = repo.run(['b2', '--message', 'local/b2.msg']);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      // The tier must never read released while a process of the dropped run still lives: read the tier first, then the processes.
      const unsafe: string[] = [];
      const sampled = (async () => {
        while (pids.some(alive)) {
          const record = repo.admission();
          const released = record !== undefined && !record.scopedTier;
          if (released && pids.some(alive)) unsafe.push('the scoped tier was released while a process of the dropped run lived');
          await pause(10);
        }
      })();
      const dropped = await repo.run(['--drop-batch', 'b1']);
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /taken off the queue: b1/);
      assert.match(dropped.out, /the runner \(pid \d+\) ended gates .*check.*each with everything it started/);
      await sampled;
      assert.deepEqual(unsafe, []);
      assert.deepEqual(pids.filter(alive), [], 'the gate, its child and its grandchild are gone, though nothing released them');
      assert.equal(alive(bystander.pid!), true, 'a process the run did not start is left alone');
      assert.equal((await first).status, 1);
      const promoted = await second;
      assert.equal(promoted.status, 0, `${promoted.out}\n${repo.log()}`);
      assert.match(promoted.out, /b2 is in main/);
      assert.equal(existsSync(join(repo.root, 'b.txt')), true);
      assert.equal(existsSync(join(repo.root, 'hold-check')), false, 'the dropped branch never lands');
      assert.match(repo.log(), /was dropped or replaced: ended gates .*check/);
      const runs = q.readScopeRuns(repo.root) as { state: string; members: { branch: string }[]; drained?: Drained }[];
      const ended = runs.find((each) => each.members.some((member) => member.branch === 'b1'));
      assert.equal(ended?.state, 'ended');
      assert.ok(ended?.drained?.ended.includes('check'));
      assert.equal(runs.at(-1)?.state, 'green');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      bystander.kill();
      await repo.cleanup();
    }
  });

  test('QUEUEDRAIN1: a candidate dropped during a failure probe ends the probe with its tree, starts no more, and the runner is free', async () => {
    const repo = queueScratch('drain-probe');
    const dir = join(repo.root, 'local', 'drain');
    const hold = join(repo.root, 'local', 'go');
    mkdirSync(dir, { recursive: true });
    try {
      // The first run of `check` (the candidate's) fails, and so does its second, the confirm run (INTEGRATE1b); every run after
      // them is a probe, and a probe holds.
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, 'count >= 3'));
      repo.git('commit', '--quiet', '-am', `a gate that holds its probes${TRAILER}`);
      repo.branch('good', { 'good.txt': 'fine\n' });
      repo.branch('bad', { 'break-check': 'x\n' });
      for (const name of ['good', 'bad']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const env = { MERGE_BRANCH_QUEUE_HOLD: hold };
      const good = repo.run(['good', '--message', 'local/good.msg'], env);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const bad = repo.run(['bad', '--message', 'local/bad.msg'], env);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 3, 'the candidate ran once, its confirm run once, and its first probe is the one held');
      assert.ok(pids.every(alive));
      const dropped = await repo.run(['--drop-batch', 'good']);
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /the probe check\.prefix-1/);
      assert.deepEqual(pids.filter(alive), [], 'the probe, its child and its grandchild are gone');
      assert.equal((await good).status, 1);
      assert.equal((await bad).status, 1);
      // A runner that went on probing would hold at the next probe for ever; it is idle, and no probe started after the drop.
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(repo.queue()?.runner, null, repo.log());
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 3);
      assert.match(repo.log(), /was dropped or replaced: ended no gate running, the probe check\.prefix-1/);
      const runs = q.readScopeRuns(repo.root) as { state: string; judged?: unknown }[];
      assert.equal(runs.at(-1)?.state, 'ended');
      assert.equal(runs.at(-1)?.judged, undefined, 'a judgement cut short is not recorded');
      assert.equal(existsSync(join(repo.root, 'good.txt')), false);
    } finally {
      writeFileSync(join(dir, 'release'), '');
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('QUEUEDRAIN1: a class probe, which runs the gate narrowed to the failing class, is ended with its tree when the candidate is dropped, and none starts after', async () => {
    const repo = queueScratch('drain-class');
    const dir = join(repo.root, 'local', 'drain');
    const shims = join(repo.root, 'local', 'shims');
    mkdirSync(dir, { recursive: true });
    mkdirSync(shims, { recursive: true });
    const live = join(dir, 'live');
    try {
      // A `dotnet` that records what it was asked and holds, with a child and a grandchild, as a test host does.
      writeFileSync(join(shims, 'dotnet.mjs'), treeGate(dir, 'true', 'args'));
      if (process.platform === 'win32') writeFileSync(join(shims, 'dotnet.cmd'), `@node "%~dp0dotnet.mjs" check %*\r\n`);
      else {
        writeFileSync(join(shims, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/dotnet.mjs" check "$@"\n', { mode: 0o755 });
      }
      repo.branch('b1', { 'a.txt': 'a\n' });
      const base = repo.git('rev-parse', 'main');
      const commit = repo.git('rev-parse', 'b1');
      const candidate: Candidate = { base, tip: commit, members: [{ branch: 'b1', tip: commit, commit, request: 'first' }], state: 'running', reforms: 0, recordsOnly: false };
      const plan = [{ name: 'driver', run: 'dotnet test Fixture.csproj --filter "Category!=Process"' }];
      const failure: Failure = { unit: 'SessionTests', test: 'Driver.Tests.SessionTests.Resumes', gate: 'driver', signature: sig('x'), names: 'test' };
      // QUEUETESTLEAK1: the stand-in `dotnet` is first on the probe's own PATH, as its process starts; the test process's environment,
      // which every other case's children were once spawned from, is never changed.
      writeFileSync(live, '');
      const probing = repo.probe({ candidate, plan, dir, failure, ks: [1, 0], live }, { PATH: `${shims}${delimiter}${pathOf(repo.env)}` });
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3);
      assert.match(readFileSync(join(dir, 'args'), 'utf8'), /\(Category!=Process\)&\(FullyQualifiedName~SessionTests\)/, 'the probe is the gate narrowed to the failing class');
      rmSync(live);
      const probed = await probing;
      assert.equal(probed.status, 0, probed.out);
      assert.match(probed.lines[0]?.error ?? '', /was dropped/, probed.out);
      assert.deepEqual(pids.filter(alive), [], 'the class probe, its child and its grandchild are gone');
      assert.match(probed.lines[1]?.error ?? '', /was dropped/, 'a probe asked after the drop is refused');
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 1, 'no probe started after the drop');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      await repo.cleanup();
    }
  });

  // ---------------------------------------------------------------------------------------------------
  // INTEGRATE1b part 2 (design §4.5, §5.3 step 3, §11 row b): the confirm run in the queue's runner, before any base or prefix run.

  /** Two branches queued into one candidate: the runner is held until both are queued, then forms them together. Answers both waits. */
  const queueTogether = async (repo: ReturnType<typeof queueScratch>, names: [string, string]) => {
    const hold = join(repo.root, 'local', 'go');
    for (const name of names) assert.equal((await repo.run([name, '--preview'])).status, 0);
    const first = repo.run([names[0], '--message', `local/${names[0]}.msg`], { MERGE_BRANCH_QUEUE_HOLD: hold });
    await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
    const second = repo.run([names[1], '--message', `local/${names[1]}.msg`], { MERGE_BRANCH_QUEUE_HOLD: hold });
    await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
    writeFileSync(hold, '');
    return [first, second] as const;
  };
  const runsOf = (dir: string) => (existsSync(join(dir, 'runs')) ? readFileSync(join(dir, 'runs'), 'utf8').length : 0);
  const verdictsOf = (repo: { root: string }) => (JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { verdicts: { gate: string; verdict: string; tree: string }[] }).verdicts;

  for (const policy of ['split', 'land', 'hold'] as const) {
    const goes = { split: 'splits, and each member promotes alone', land: 'promotes as it stands, its pass standing for no other tip', hold: 'waits for a decide, which no door gives yet' }[policy];
    test(`INTEGRATE1b: a gate that fails once beside a held load and passes quiet holding the whole admission is a sighting; under ${policy}${policy === 'split' ? ', the default with no policy written,' : ''} the candidate ${goes}`, async () => {
      const repo = queueScratch(`confirm-${policy}`);
      const dir = join(repo.root, 'local', 'confirm');
      mkdirSync(dir, { recursive: true });
      // The load: a place in the case's admission this process holds, as a driven session's heavy run holds one beside the queue.
      const load = await machine.createAdmission({ daorisHome: repo.home, capacity: 2, say: () => {} }).take({ what: 'the load beside it' });
      try {
        assert.ok(load, 'the load holds a place');
        if (policy !== 'split') writeFileSync(join(repo.root, 'local', 'integration-policy.json'), JSON.stringify({ unconfirmed: policy }));
        writeFileSync(join(repo.root, 'qgate.mjs'), confirmGate(dir, { failWhen: 'load > 0', admission: machine.admissionFile(repo.home) }));
        repo.git('commit', '--quiet', '-am', `a check that fails beside a heavy load${TRAILER}`);
        const base = repo.git('rev-parse', 'main');
        repo.branch('b1', { 'one.txt': '1\n' });
        repo.branch('b2', { 'two.txt': '2\n' });
        const waits = await queueTogether(repo, ['b1', 'b2']);
        // The confirm run asks every place, so it waits for the load; nothing more of the gate runs while it does.
        await repo.until(() => /waiting for the machine's whole heavy-run admission before the confirm run of check/.test(repo.log()));
        assert.match(repo.log(), /waiting for the machine's whole heavy-run admission before the confirm run of check/, repo.log());
        assert.equal(runsOf(dir), 1, 'no base or prefix run while the confirm run waits');
        await pause(300);
        load!.release();
        let red: ScopeRun | undefined;
        if (policy === 'hold') {
          await repo.until(() => repo.queue()?.candidate?.held?.on === 'decide');
          const said = await repo.run(['--queue']);
          assert.match(said.out, /held on decide: an unconfirmed failure holds the candidate for a decide: check; no decide door exists yet/, `${said.out}\n${repo.log()}`);
          assert.equal(repo.git('rev-parse', 'main'), base, 'nothing promoted while it is held');
          red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red');
          assert.equal(red?.outcome, 'hold');
          assert.equal((await repo.run(['--drop-batch', 'b1'])).status, 0);
          for (const done of await Promise.all(waits)) assert.equal(done.status, 1, done.out);
          assert.match((await waits[0]).out, /the queue is held on decide/);
        } else {
          for (const done of await Promise.all(waits)) assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
          assert.equal(existsSync(join(repo.root, 'one.txt')) && existsSync(join(repo.root, 'two.txt')), true, 'both members are in main');
        }
        const runs = q.readScopeRuns(repo.root) as ScopeRun[];
        red = runs.find((each) => each.state === 'red');
        assert.deepEqual(red?.members.map((each) => each.branch), ['b1', 'b2'], repo.log());
        assert.deepEqual(red?.judged?.map((each) => each.outcome), ['unconfirmed']);
        assert.equal(red?.policy?.unconfirmed, policy);
        // The scope run's record keeps the confirm run: its composition, its admission's evidence and its wait (§11 row b's measures).
        const [confirm] = red!.confirms!;
        assert.deepEqual([confirm!.gate, confirm!.result], ['check', 'unconfirmed']);
        assert.equal(confirm!.admission?.conclusive, true, JSON.stringify(confirm));
        assert.equal(confirm!.admission?.ids?.length, 2, 'every place of the two');
        assert.ok((confirm!.waitedMs ?? 0) >= 300, `it waited for the load: ${confirm!.waitedMs}`);
        assert.equal(confirm!.composition?.units, null, 'the gate ran whole, and ran again whole');
        assert.match(confirm!.composition?.digest ?? '', /^sha256:[0-9a-f]{64}$/);
        // The sighting, apart from the known red and the flakes.
        const sightings = q.readSightings(repo.root);
        assert.equal(sightings.length, 1, JSON.stringify(sightings));
        assert.deepEqual([sightings[0]!['gate'], sightings[0]!['commit'], sightings[0]!['base'], sightings[0]!['policy'], sightings[0]!['members']], ['check', red!.tip, base, policy, ['b1', 'b2']]);
        assert.equal((sightings[0]!['admission'] as Evidence).conclusive, true);
        const findings = join(repo.root, 'local', 'ci-findings.json');
        assert.deepEqual(existsSync(findings) ? (JSON.parse(readFileSync(findings, 'utf8')) as { flakes?: unknown[] }).flakes ?? [] : [], [], 'no flake is filed for it');
        if (policy === 'split') {
          assert.equal(red!.outcome, 'split');
          assert.deepEqual(runs.slice(runs.indexOf(red!) + 1).map((each) => [each.state, each.members.map((member) => member.branch).join()]), [['green', 'b1'], ['green', 'b2']], repo.log());
          assert.equal(runsOf(dir), 4, 'the candidate, its confirm run, then each member alone: no base or prefix run');
        }
        if (policy === 'land') {
          assert.equal(red!.outcome, 'land');
          assert.deepEqual(red!.passedOnConfirm, ['check']);
          assert.equal(runs.indexOf(red!), runs.length - 1, 'no other run: the candidate promoted as it stood');
          assert.equal(repo.git('rev-parse', 'main'), red!.tip, 'main is the candidate itself');
          assert.equal(runsOf(dir), 2);
          assert.match(repo.log(), /lands on its confirm run alone/);
          assert.equal(verdictsOf(repo).some((each) => each.gate === 'check' && each.tree === red!.tree && ['PASS', 'FLAKE'].includes(each.verdict)), false, 'no pass of check stands on its tree');
        }
      } finally {
        load?.release();
        await repo.cleanup();
      }
    });
  }

  test('INTEGRATE1b: a pollution within a class fails again as composed: the confirm run repeats the gate as it ran, and the failure is bisected as before', async () => {
    const repo = queueScratch('confirm-polluted');
    const shims = join(repo.root, 'local', 'shims');
    mkdirSync(shims, { recursive: true });
    try {
      const calls = join(repo.root, 'local', 'dotnet-calls.jsonl');
      writeFileSync(join(shims, 'dotnet.mjs'), pollutedDotnet(calls, 'pollute'));
      if (process.platform === 'win32') writeFileSync(join(shims, 'dotnet.cmd'), '@node "%~dp0dotnet.mjs" %*\r\n');
      else writeFileSync(join(shims, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/dotnet.mjs" "$@"\n', { mode: 0o755 });
      // The stand-in is first on the runner's own PATH as it starts (QUEUETESTLEAK1): the test process's environment is never changed.
      const PATH = `${shims}${delimiter}${pathOf(repo.env)}`;
      writeFileSync(join(repo.root, 'Fixture.csproj'), '<Project />\n');
      writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'fixture', run: 'dotnet test Fixture.csproj --filter "Category!=Process"' }] }, null, 2)}\n`);
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a test project${TRAILER}`);
      repo.branch('good', { 'good.txt': 'fine\n' });
      repo.branch('bad', { pollute: 'x\n' });
      const hold = join(repo.root, 'local', 'go');
      for (const name of ['good', 'bad']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const good = repo.run(['good', '--message', 'local/good.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold, PATH });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const bad = repo.run(['bad', '--message', 'local/bad.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold, PATH });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [goodOut, badOut] = await Promise.all([good, bad]);
      assert.equal(badOut.status, 1, `${badOut.out}\n${repo.log()}`);
      assert.match(badOut.out, /bad is handed back \(pinned: Fixture\.PollutedTests\.B_Reads\)/);
      assert.equal(goodOut.status, 0, `${goodOut.out}\n${repo.log()}`);
      const red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red')!;
      assert.deepEqual(red.confirms?.map((each) => [each.gate, each.result]), [['fixture', 'confirmed']], repo.log());
      assert.deepEqual(red.judged?.map((each) => [each.outcome, each.member]), [['pinned', 2]]);
      // The confirm run is the gate's run again, its whole selection and settings, never the failing class or test alone.
      const [scoped, confirm, onBase] = readFileSync(calls, 'utf8').trim().split('\n');
      assert.doesNotMatch(confirm!, /FullyQualifiedName~/, 'as composed: not narrowed to the failing class');
      assert.equal(confirm, scoped!.replace('fixture.scoped.trx', 'fixture.confirm.trx'), 'the same command, into a trx of its own');
      assert.match(onBase!, /\(Category!=Process\)&\(FullyQualifiedName~Fixture\.PollutedTests\)/, 'then the failing class on the base, as today');
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1b: another live hold beside the confirm run makes it inconclusive, read as split even under land: one of one is handed back unresolved, saying so, with no base run', async () => {
    const repo = queueScratch('confirm-beside');
    const dir = join(repo.root, 'local', 'confirm');
    mkdirSync(dir, { recursive: true });
    let beside: { release: () => void } | null = null;
    try {
      writeFileSync(join(repo.root, 'local', 'integration-policy.json'), JSON.stringify({ unconfirmed: 'land' }));
      // The candidate's run fails; its second run, the confirm run, waits for `release` and then passes.
      writeFileSync(join(repo.root, 'qgate.mjs'), confirmGate(dir, { failWhen: 'count === 1', holdWhen: 'count === 2' }));
      repo.git('commit', '--quiet', '-am', `a check that fails once${TRAILER}`);
      repo.branch('b1', { 'one.txt': '1\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => existsSync(join(dir, '2.started')));
      assert.ok(existsSync(join(dir, '2.started')), repo.log());
      // A writer that counts a larger capacity takes a place while the whole admission is held, as an older build's would.
      beside = await machine.createAdmission({ daorisHome: repo.home, capacity: 3, say: () => {} }).take({ what: 'a run beside it' });
      assert.ok(beside, 'it took a place beside the whole admission');
      writeFileSync(join(dir, 'release'), '');
      const done = await waiting;
      assert.equal(done.status, 1, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is handed back \(unresolved: check; its confirm run is inconclusive \(another live hold stands beside it: the merge queue's a run beside it\): read as split, never land/);
      const red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red')!;
      assert.deepEqual(red.judged?.map((each) => each.outcome), ['inconclusive']);
      const [confirm] = red.confirms!;
      assert.equal(confirm!.result, 'inconclusive');
      assert.equal(confirm!.admission?.conclusive, false);
      assert.match(confirm!.admission?.doubts[0]?.why ?? '', /another live hold stands beside it/);
      assert.equal(runsOf(dir), 2, 'the candidate and its confirm run: no base or prefix run');
      assert.deepEqual(q.readSightings(repo.root), [], 'an inconclusive confirm run is no sighting');
    } finally {
      beside?.release();
      writeFileSync(join(dir, 'release'), '');
      await repo.cleanup();
    }
  });

  test('INTEGRATE1b: an admission the confirm run cannot write is inconclusive: the gate is not run again, nothing runs on the base, and one of one is handed back unresolved', async () => {
    const repo = queueScratch('confirm-unwritable');
    const dir = join(repo.root, 'local', 'confirm');
    mkdirSync(dir, { recursive: true });
    try {
      // The machine's record is a folder, so no write of it lands.
      mkdirSync(machine.admissionFile(repo.home), { recursive: true });
      writeFileSync(join(repo.root, 'qgate.mjs'), confirmGate(dir, { failWhen: 'true' }));
      repo.git('commit', '--quiet', '-am', `a check that always fails${TRAILER}`);
      repo.branch('b1', { 'one.txt': '1\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const done = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(done.status, 1, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is handed back \(unresolved: check; its confirm run is inconclusive \(the machine's record could not be written/);
      const red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red')!;
      const [confirm] = red.confirms!;
      assert.deepEqual([confirm!.result, confirm!.admission?.conclusive, confirm!.admission?.doubts[0]?.at], ['inconclusive', false, 'take']);
      assert.equal(runsOf(dir), 1, 'neither a confirm run nor a base run: an admission not shown is never run without waiting');
    } finally {
      await repo.cleanup();
    }
  });

  test('INTEGRATE1b: a drop ends a confirm run with its tree, starts no base or prefix run, and lets every place of the whole admission go', async () => {
    const repo = queueScratch('confirm-drop');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    try {
      // The candidate's run of `check` fails; its second, the confirm run, holds with a child and a grandchild.
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, 'count === 2'));
      repo.git('commit', '--quiet', '-am', `a gate that holds its confirm run${TRAILER}`);
      repo.branch('bad', { 'break-check': 'x\n' });
      assert.equal((await repo.run(['bad', '--preview'])).status, 0);
      const report = join(dir, 'report');
      const waiting = repo.run(['bad', '--message', 'local/bad.msg'], { MERGE_BRANCH_DRAIN_HOLD: report });
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.equal(runsOf(dir), 2, 'the candidate ran once, and its confirm run is the one held');
      const holds = () => (JSON.parse(readFileSync(machine.admissionFile(repo.home), 'utf8')) as { holds: { what: string }[] }).holds;
      assert.equal(holds().length, 2, 'the confirm run holds every place');
      const asked = Date.now();
      const dropping = repo.run(['--drop-batch', 'bad']);
      // DROPWAIT1: the runner has ended the tree, and its report is kept late past the drop's thirty seconds on the machine's clock, as
      // a loaded runner's was: the drop waits on the clock only for the runner to take it, then on what it says.
      // The tree is looked at before the pause, whose half minute lets Windows give its pids to other processes.
      await repo.until(() => pids.every((pid) => !alive(pid)));
      assert.deepEqual(pids.filter(alive), [], 'the confirm run, its child and its grandchild are gone');
      await pause(Math.max(0, asked + tool.DRAIN_WAIT_MS + 5_000 - Date.now()));
      writeFileSync(report, '');
      const dropped = await dropping;
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /the probe check\.confirm/);
      assert.equal((await waiting).status, 1);
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(repo.queue()?.runner, null, repo.log());
      assert.equal(runsOf(dir), 2, 'no base or prefix run after the drop');
      assert.match(repo.log(), /was dropped or replaced: ended no gate running, the probe check\.confirm/);
      const last = (q.readScopeRuns(repo.root) as ScopeRun[]).at(-1)!;
      assert.deepEqual([last.state, last.drained?.probe, last.judged], ['ended', 'check.confirm', undefined]);
      assert.deepEqual(holds(), [], 'every place of the whole admission let go');
      assert.deepEqual(q.readSightings(repo.root), []);
    } finally {
      writeFileSync(join(dir, 'release'), '');
      writeFileSync(join(dir, 'report'), '');
      await repo.cleanup();
    }
  });
});

// PROCLEAK1 (2026-10-30): the machine was found slowed by what the merge tool and its tests had left running: waiters whose requests
// were gone, runners no record named, a stand-in polling for a file its killed test never wrote.
/** A process of the test's own that does nothing, ended by `kill`: a live pid a record can name, and an owner that can end. */
const idle = () => spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore' });

/** A process whose parent is gone and whose command line names `marker`: started by a node that ended at once. Answers its pid. */
function orphanNaming(marker: string): number {
  const maker = spawnSync(process.execPath, ['-e',
    `const c = require('node:child_process').spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)', ${JSON.stringify(marker)}], { detached: true, stdio: 'ignore' }); console.log(c.pid); c.unref();`],
  { encoding: 'utf8' });
  return Number(maker.stdout.trim());
}

describe('PROCLEAK1: nothing the merge tool and its tests start outlives what started it', { concurrency: true }, () => {
  const request = (branch: string, id: string): Member => ({ branch, tip: `${branch}-tip`, request: id });
  const asked = (member: Member) => ({ branch: member.branch, tip: member.tip, request: member.request! });

  test('a runner stands while the queue names it, and is told so where the queue names another or none', () => {
    const queue = { ...q.emptyQueue(), runner: { pid: 41, started: AT } } as Queue;
    assert.deepEqual(q.runnerStanding(queue, 41), { stands: true });
    const other = q.runnerStanding(queue, 42);
    assert.equal(other.stands, false);
    assert.equal((other as { why: string }).why, 'the queue names pid 41 as its runner');
    const none = q.runnerStanding({ ...queue, runner: null }, 42);
    assert.equal(none.stands, false);
    assert.equal((none as { why: string }).why, 'the queue names no runner');
    assert.equal(q.runnerStanding({ ...q.emptyQueue() }, 42).stands, false, 'a record that was never written names none');
  });

  test('an owner a runner was started with ends it when the owner is gone; one with no owner has none to lose', () => {
    assert.equal(tool.ownerEnded({}, () => false), null, 'no owner named: a runner started by a person has none');
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: '' }, () => false), null);
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: 'soon' }, () => false), null, 'a value that is no pid names no owner');
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: '77' }, (pid) => pid === 77), null, 'the owner runs');
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: '77' }, () => false), 'its owner (pid 77), the process that started it, has ended');
  });

  test('a candidate run asks its runner too: a watch whose runner is no longer the queue\'s ends every step it owns, naming why', () => {
    const ended: number[] = [];
    const trees = tool.stepTrees({ end: (pid) => { ended.push(pid); } });
    trees.hold(4242);
    const candidate: Candidate = { base: 'b0', tip: 'c1', members: [{ branch: 'b1', tip: 't1', request: 'r1', commit: 'k1' }], state: 'running', reforms: 0, recordsOnly: false };
    let lost: string | null = null;
    const watch = tool.watchCandidate(candidate, () => candidate, trees, { superseded: () => lost });
    assert.equal(watch.check(), false, 'the candidate stands and so does its runner');
    lost = "this runner is no longer the queue's";
    assert.equal(watch.check(), true);
    assert.equal(trees.ended, "this runner is no longer the queue's");
    assert.deepEqual(ended, [4242]);
  });

  test("a request's wait ends only with an outcome, and a request kept through a re-form, a contained member or a hand-back is never lost between them", () => {
    const [one, two] = [request('b1', 'r1'), request('b2', 'r2')];
    const standing = (queue: Queue, member: Member) => q.requestStanding(queue, asked(member));
    const empty = q.emptyQueue();
    const first: Candidate = { base: 'm0', tip: 'c1', members: [{ ...one, commit: 'k1' }], state: 'running', reforms: 0, recordsOnly: false };
    // Re-formed: the candidate ends and its requests stand as `forming`, then as the next candidate's members; main holds them then.
    assert.equal(standing({ ...empty, candidate: first }, one).pending, 'candidate');
    assert.equal(standing({ ...empty, candidate: null, forming: { base: 'm1', members: [one] } }, one).pending, 'forming');
    const second = { ...first, base: 'm1', tip: 'c2', reforms: 1 };
    assert.equal(standing({ ...empty, candidate: second }, one).pending, 'candidate');
    assert.equal(tool.waitSaid(standing({ ...empty, candidate: second }, one), { branch: 'b1' }), null);
    const landed = standing({ ...empty, landed: [{ ...one, commit: 'k2' }] }, one);
    assert.equal(tool.waitSaid(landed, { branch: 'b1' })!.code, 0);
    // Contained in another member: pending while its container runs, and with the container's outcome it has its own.
    const holding: Candidate = { ...first, contained: [{ ...two, containedIn: { branch: 'b1', request: 'r1' } }] };
    assert.equal(standing({ ...empty, candidate: holding }, two).pending, 'contained');
    assert.equal(tool.waitSaid(standing({ ...empty, candidate: holding }, two), { branch: 'b2' }), null);
    const settled = q.settleContained(holding, AT);
    const promoted = { ...empty, landed: [{ ...one, commit: 'k1' }], contained: settled };
    assert.match(tool.waitSaid(standing(promoted, two), { branch: 'b2' })!.lines[0]!, /b2 is contained in b1, which promoted/);
    assert.equal(tool.waitSaid(standing(promoted, two), { branch: 'b2' })!.code, 0);
    // The container handed back: the contained one is its own request again, forming, and still pending.
    const again = q.requestsOf(holding, []);
    assert.deepEqual(again.map((each) => each.request), ['r2']);
    assert.equal(standing({ ...empty, forming: { base: 'm0', members: again } }, two).pending, 'forming');
    // Dropped by name alone while contained: handed back as dropped, so its wait ends; the container's candidate stands.
    const byName = q.dropQueued({ ...empty, candidate: holding }, ['b2']);
    assert.equal(byName.ended, null);
    assert.equal(standing(byName.queue, one).pending, 'candidate');
    const said = tool.waitSaid(standing(byName.queue, two), { branch: 'b2' })!;
    assert.equal(said.code, 1);
    assert.match(said.lines[0]!, /b2's request was taken off the queue/);
    // The whole candidate dropped: its member and the contained one are both handed back as dropped.
    const whole = q.dropQueued({ ...empty, candidate: holding }, ['b1']);
    assert.deepEqual(whole.queue.handedBack.map((each) => [each.branch, each.handedBack?.how]), [['b1', 'dropped'], ['b2', 'dropped']]);
    for (const member of [one, two]) assert.equal(tool.waitSaid(standing(whole.queue, member), { branch: member.branch })!.code, 1);
    // A request in none of the records ends its wait, whatever became of the candidate that held it.
    assert.equal(tool.waitSaid(standing({ ...empty, candidate: null }, two), { branch: 'b2', request: 'r2' })!.code, 1);
  });

  test('a process list reads as rows: Windows prints pid, parent and command line, ps prints them in its columns', () => {
    const windows = ['12|4|node C:\\repo\\local\\scope-tree\\a.js --flag', '', '13|12|"C:\\Program Files\\git\\git.exe" status', 'junk', '0||System Idle'].join('\r\n');
    assert.deepEqual(procs.parseProcessRows(windows, 'rows'), [
      { pid: 12, ppid: 4, command: 'node C:\\repo\\local\\scope-tree\\a.js --flag' },
      { pid: 13, ppid: 12, command: '"C:\\Program Files\\git\\git.exe" status' },
    ]);
    const ps = ['  501     1 node /repo/local/scope-tree/a.js --flag', '  502   501 git status', '   bad'].join('\n');
    assert.deepEqual(procs.parseProcessRows(ps, 'ps'), [
      { pid: 501, ppid: 1, command: 'node /repo/local/scope-tree/a.js --flag' },
      { pid: 502, ppid: 501, command: 'git status' },
    ]);
  });

  test("the census names a process whose command line is under the repository's trees and whose parent is gone, and no other", () => {
    const trees = q.censusTrees('C:/repo');
    assert.deepEqual(trees, ['C:/repo/local/scope-tree', 'C:/repo/local/gate-tree', 'C:/repo/.claude/worktrees/', 'C:/repo/_fixtures/']);
    const rows: ProcessRow[] = [
      { pid: 10, ppid: 1, command: 'node C:\\Repo\\local\\scope-tree\\tool-test\\x.js' },
      { pid: 11, ppid: 10, command: 'node C:/repo/local/scope-tree/child.js' },
      { pid: 12, ppid: 999, command: 'node C:/other/thing.js' },
      { pid: 13, ppid: 998, command: 'git -C C:/repo/_fixtures/review-leave/x status' },
      { pid: 14, ppid: 997, command: 'node C:/repo/local/gate-tree/y.js' },
      { pid: 15, ppid: 996, command: 'node C:/repo/_fixtures-other/z.js' },
      { pid: 16, ppid: 995, command: 'node C:/repo/local/scope-tree2/w.js' },
      { pid: 17, ppid: 994, command: 'node C:/repo/.claude/worktrees/s-1/a.js' },
    ];
    assert.deepEqual(q.orphansUnder(rows, { trees }).map((each) => each.pid), [10, 13, 14, 17], 'a live parent, no tree and a folder that only starts like one are not named');
    assert.deepEqual(q.orphansUnder(rows, { trees, ignore: [10, 14] }).map((each) => each.pid), [13, 17], 'the runner and what it names are left out');
  });

  test('the census says its count and the first few command lines, or that none has lost its parent', () => {
    assert.equal(q.censusLine([]), "census at the candidate's end: no process under this repository's trees has lost its parent");
    const one = q.censusLine([{ pid: 10, ppid: 1, command: 'node x.js' }]);
    assert.equal(one, "census at the candidate's end: 1 process under this repository's trees has lost its parent and runs on (this runner stops none): pid 10 (node x.js)");
    const many = q.censusLine([1, 2, 3, 4, 5].map((pid) => ({ pid, ppid: 0, command: `node ${'long '.repeat(40)}${pid}` })), { limit: 2, width: 30 });
    assert.match(many, /^census at the candidate's end: 5 processes under this repository's trees have lost their parent and run on \(this runner stops none\): pid 1 \(node long long long long long…\), pid 2 \(node long long long long long…\) and 3 more$/);
  });

  test('a runner the queue no longer names ends while it holds, saying so, and leaves the record that names another as it is', async () => {
    const repo = queueScratch('runner-named');
    const hold = join(repo.root, 'local', 'go');
    const other = idle();
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1 && Boolean(repo.queue()?.runner));
      const runner = repo.queue()!.runner as unknown as { pid: number };
      assert.ok(alive(runner.pid), repo.log());
      // Another runner took the record (as one does where a start found this pid gone): this one is no longer the queue's.
      q.writeQueue(repo.root, { ...repo.queue()!, runner: { pid: other.pid!, started: AT } } as unknown as Queue);
      await repo.until(() => !alive(runner.pid), PATIENCE);
      assert.equal(alive(runner.pid), false, `the runner went on holding, no longer named:\n${repo.log()}`);
      assert.match(repo.log(), new RegExp(`this runner \\(pid ${runner.pid}\\) is no longer the queue's: the queue names pid ${other.pid} as its runner; it ends`));
      assert.deepEqual((repo.queue()!.runner as unknown as { pid: number }).pid, other.pid, 'it never clears a record that names another');
      assert.equal(repo.queue()!.queued.length, 1, 'its request is still queued: ending leaves it to the runner the queue names');
      void waiting;
    } finally {
      q.writeQueue(repo.root, { ...repo.queue()!, runner: null } as unknown as Queue);
      other.kill();
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test("a runner the queue no longer names ends the gate it runs with its whole tree, and starts no more", async () => {
    const repo = queueScratch('runner-named-gate');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    const other = idle();
    try {
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, "existsSync('hold-check')"));
      repo.git('commit', '--quiet', '-am', `a gate that holds${TRAILER}`);
      repo.branch('b1', { 'hold-check': 'x\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg'], {}, PATIENCE);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      const runner = repo.queue()!.runner as unknown as { pid: number };
      q.writeQueue(repo.root, { ...repo.queue()!, runner: { pid: other.pid!, started: AT } } as unknown as Queue);
      await repo.until(() => pids.every((pid) => !alive(pid)) && !alive(runner.pid), PATIENCE);
      assert.deepEqual(pids.filter(alive), [], `the gate, its child and its grandchild ran on with their runner no longer named:\n${repo.log()}`);
      assert.equal(alive(runner.pid), false, repo.log());
      assert.match(repo.log(), /is no longer the queue's: the queue names pid \d+ as its runner; it ends/);
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 1, 'no gate started after');
      void waiting;
    } finally {
      writeFileSync(join(dir, 'release'), '');
      q.writeQueue(repo.root, { ...repo.queue()!, runner: null } as unknown as Queue);
      other.kill();
      await repo.cleanup();
    }
  });

  test('a runner and its waiter end with the process that owned them, saying so, and leave the request queued', async () => {
    const repo = queueScratch('owner-gone');
    const hold = join(repo.root, 'local', 'go');
    const owner = idle();
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold, MERGE_BRANCH_QUEUE_OWNER: String(owner.pid) }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1 && Boolean(repo.queue()?.runner));
      const runner = repo.queue()!.runner as unknown as { pid: number };
      assert.ok(alive(runner.pid), repo.log());
      owner.kill();
      const ended = await waiting;
      assert.equal(ended.status, 1, ended.out);
      assert.match(ended.out, new RegExp(`its owner \\(pid ${owner.pid}\\), the process that started it, has ended: this wait ends; b1 stays queued`));
      await repo.until(() => !alive(runner.pid), PATIENCE);
      assert.equal(alive(runner.pid), false, `the runner outlived its owner:\n${repo.log()}`);
      assert.match(repo.log(), new RegExp(`its owner \\(pid ${owner.pid}\\), the process that started it, has ended: this runner \\(pid ${runner.pid}\\) ends`));
      assert.equal(repo.queue()!.queued.length, 1);
      assert.equal(repo.log().split("the queue's runner started").length - 1, 1, 'no waiter went on starting runners');
    } finally {
      owner.kill();
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test("a request re-formed into another candidate, because main moved, keeps its waiter, which ends with the landing", async () => {
    const repo = queueScratch('reformed');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    try {
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, "existsSync('hold-check')"));
      repo.git('commit', '--quiet', '-am', `a gate that holds${TRAILER}`);
      repo.branch('b1', { 'hold-check': 'x\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const first = repo.queue()!.candidate!;
      const id = first.members[0]!.request!;
      // A direct commit moves main while the candidate's gate holds: the candidate is formed again on it from the same request.
      writeFileSync(join(repo.root, 'moved.txt'), 'm\n');
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a direct commit${TRAILER}`);
      writeFileSync(join(dir, 'release'), '');
      const done = await waiting;
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is in main/);
      assert.match(repo.log(), /by a direct commit.*the candidate is formed again/);
      const queue = repo.queue() as unknown as { landed: { request: string; commit: string }[] };
      assert.deepEqual(queue.landed.map((each) => each.request), [id], 'the request the waiter holds is the one that landed');
      assert.notEqual(queue.landed[0]!.commit, first.tip, 'on the candidate formed again, not the first');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      await repo.cleanup();
    }
  });

  test("the census at a candidate's end names a process under the trees whose parent is gone, stops it not, and says none where none is", async () => {
    const repo = queueScratch('census');
    let orphan = 0;
    try {
      orphan = orphanNaming(join(repo.root, 'local', 'scope-tree', 'planted-by-a-killed-test'));
      assert.ok(orphan > 0 && alive(orphan), 'the orphan was planted');
      repo.branch('b1', { 'a.txt': 'a\n' });
      repo.branch('b2', { 'b.txt': 'b\n' });
      for (const name of ['b1', 'b2']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const first = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(first.status, 0, `${first.out}\n${repo.log()}`);
      // The waiter ends with the promotion; the runner's census, a process list read, follows it.
      await repo.until(() => /census at the candidate's end/.test(repo.log()), PATIENCE);
      assert.match(repo.log(), new RegExp(`census at the candidate's end: 1 process under this repository's trees has lost its parent and runs on \\(this runner stops none\\): pid ${orphan} \\(.*setInterval`));
      assert.equal(alive(orphan), true, 'the census stops nothing');
      process.kill(orphan);
      await repo.until(() => !alive(orphan), PATIENCE);
      const second = await repo.run(['b2', '--message', 'local/b2.msg']);
      assert.equal(second.status, 0, `${second.out}\n${repo.log()}`);
      await repo.until(() => /census at the candidate's end: no process/.test(repo.log()), PATIENCE);
      assert.match(repo.log(), /census at the candidate's end: no process under this repository's trees has lost its parent/);
    } finally {
      if (orphan > 0) try { process.kill(orphan); } catch { /* ended */ }
      await repo.cleanup();
    }
  });
});

// QUEUEDROPLEAK1 (2026-10-31): a drop said it ended the run "with everything it started", yet minutes later the tool-tests gate's
// processes ran on with their parents gone: a fixture's `tree.mjs`, and a `node qgate.mjs check` with two node children. The drop
// ends each step by its tree of parent ids; a test killed with its step had already orphaned those, or never got to end them.
describe('QUEUEDROPLEAK1: a dropped run ends what its tests had left running under the scope tree', { concurrency: true }, () => {
  const row = (pid: number, ppid: number, created: number, command: string) => ({ pid, ppid, created, command });
  const FOLDER = 'D:/work/repo/local/scope-tree';
  const SINCE = 1_000_000;

  test('the roots are the processes since the run began whose parent is gone and which, or what they hold, name the folder', () => {
    const rows = [
      row(1, 0, 0, 'init'),
      row(10, 99, SINCE + 5, 'node D:\\work\\repo\\local\\scope-tree\\src\\_fixtures\\tree.mjs'),
      row(20, 98, SINCE + 5, 'node qgate.mjs check'),
      row(21, 20, SINCE + 6, 'node "D:\\Work\\Repo\\local\\scope-tree\\src\\_fixtures\\qgate.mjs" child'),
      row(22, 21, SINCE + 7, 'node qgate.mjs grandchild'),
      row(30, 97, SINCE + 5, 'node D:/work/repo/local/scope-tree-other/x.mjs'),
      row(31, 96, SINCE + 5, 'node outsider.mjs'),
      row(32, 31, SINCE + 6, 'node D:/work/repo/local/other/x.mjs'),
      row(40, 95, SINCE - 5000, 'node D:/work/repo/local/scope-tree/older.mjs'),
      row(50, 10, SINCE + 6, 'node D:/work/repo/local/scope-tree/held.mjs'),
      row(60, 94, SINCE + 5, 'cmd /c "D:/work/repo/local/scope-tree"'),
    ];
    const found = (options: { except?: number[] } = {}) => procs.strandedRoots(rows, { folder: FOLDER, since: SINCE, ...options }).map((each) => each.pid);
    assert.deepEqual(found(), [10, 20, 60],
      'a root naming the folder, a root that names nothing but holds a process that does, and the folder itself as a whole word; not a sibling folder, not an older process, not a process whose parent runs, not a root of another folder');
    assert.deepEqual(found({ except: [22] }), [10, 60], "a root holding the runner is not the run's to end");
    assert.deepEqual(procs.strandedRoots(rows, { folder: '', since: SINCE }), [], 'no folder names nothing');
    assert.deepEqual(procs.strandedRoots([row(5, 6, SINCE, 'node D:/work/repo/local/scope-tree/a.mjs'), row(6, 5, SINCE, 'node b.mjs')], { folder: FOLDER, since: SINCE }), [],
      "a reused id that makes two processes each other's parent holds no root, and ends the walk");
  });

  test('the rows a Windows listing gives are read whole, a command line with a bar in it kept', () => {
    assert.deepEqual(procs.parseCreatedRows('4|0|1700000000000|\r\n12|4|1700000000500|node a.mjs "x|y"\r\nnot a row\r\n'),
      [{ pid: 4, ppid: 0, created: 1700000000000, command: '' }, { pid: 12, ppid: 4, created: 1700000000500, command: 'node a.mjs "x|y"' }]);
  });

  test('only Windows ends any, each root with its tree and nothing the run did not make; a list that cannot be read ends none', () => {
    const rows = '10|99|1000500|node D:/work/repo/local/scope-tree/a.mjs\n11|99|1000500|node unrelated.mjs\n12|10|1000600|node b.mjs\n';
    const ended: number[] = [];
    const asked: string[] = [];
    const run = (script: string) => { asked.push(script); return rows; };
    assert.deepEqual(procs.endStrandedUnder(FOLDER, 1_001_000, { platform: 'linux', run, end: (pid) => ended.push(pid) }), []);
    assert.deepEqual(asked, [], 'nothing is read elsewhere');
    assert.deepEqual(procs.endStrandedUnder(FOLDER, 1_001_000, { platform: 'win32', run, end: (pid) => ended.push(pid) }), [10]);
    assert.deepEqual(ended, [10]);
    assert.deepEqual(procs.endStrandedUnder(FOLDER, 1_001_000, { platform: 'win32', run: () => '', end: (pid) => ended.push(pid) }), []);
    assert.deepEqual(procs.endStrandedUnder('', 1_001_000, { platform: 'win32', run, end: (pid) => ended.push(pid) }), []);
  });

  test('a candidate dropped while its gate runs ends the orphans its tests left under the scope tree, one named by a descendant alone; a process outside it is untouched', { skip: process.platform !== 'win32' }, async () => {
    const repo = queueScratch('drain-strays');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    const roles = ['one', 'two', 'three'];
    const pidOf = (role: string) => Number(readFileSync(join(dir, `${role}.pid`), 'utf8'));
    const strays = () => roles.filter((role) => existsSync(join(dir, `${role}.pid`))).map(pidOf);
    let outsider = 0;
    try {
      repo.branch('b1', { 'hold-check': 'x\n', 'qgate.mjs': strayGate(dir) });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const first = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => roles.every((role) => existsSync(join(dir, `${role}.pid`))), PATIENCE);
      const pids = strays();
      assert.ok(pids.every(alive), `the orphans run: ${repo.log()}`);
      // A process the run did not make: orphaned too, created after the run began, naming a folder beside the scope tree and not it.
      outsider = orphanNaming(join(dirname(repo.root), 'outsider'));
      assert.ok(outsider > 0 && alive(outsider));
      const dropped = await repo.run(['--drop-batch', 'b1']);
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /the runner \(pid \d+\) ended gates .*check.*each with everything it started, and 2 processes their tests had left running under the scope tree \(pid \d+, \d+\)/);
      await repo.until(() => pids.every((pid) => !alive(pid)), PATIENCE);
      assert.deepEqual(pids.filter(alive), [], 'the orphan naming the folder, the one named by its child, and that child are gone');
      assert.equal(alive(outsider), true, "a process outside the run's trees is left alone");
      assert.equal((await first).status, 1);
      const ended = (q.readScopeRuns(repo.root) as { state: string; drained?: Drained }[]).find((each) => each.state === 'ended');
      assert.equal(ended?.drained?.strays.length, 2, 'the two roots are recorded, the child going with its root');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      for (const pid of [...strays(), outsider]) if (pid > 0) try { process.kill(pid); } catch { /* ended */ }
      await repo.cleanup();
    }
  });
});

/**
 * What a test of a gate leaves running: it waits for `release` in the folder it is given, and gives up at the bound whatever happens, so
 * one a failed case misses ends by itself. `two` starts `three`, which its absolute path names.
 */
const STRAY = [
  "import { existsSync, writeFileSync } from 'node:fs';",
  "import { spawn } from 'node:child_process';",
  "import { fileURLToPath } from 'node:url';",
  'const [role, dir] = process.argv.slice(2);',
  'writeFileSync(`${dir}/${role}.pid`, String(process.pid));',
  "if (role === 'two') spawn(process.execPath, [fileURLToPath(import.meta.url), 'three', dir], { detached: true, stdio: 'ignore' }).unref();",
  'const bound = Date.now() + 120000;',
  'while (Date.now() < bound && !existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 100));',
  '',
].join('\n');

/** What the gate runs to leave them: `one` is named by its absolute path, `two` by a relative one, so its command line names no folder. */
const MAKE_STRAYS = [
  "import { spawn } from 'node:child_process';",
  "import { resolve } from 'node:path';",
  'const dir = process.argv[2];',
  "spawn(process.execPath, [resolve('_fixtures/stray.mjs'), 'one', dir], { detached: true, stdio: 'ignore' }).unref();",
  "spawn(process.execPath, ['_fixtures/stray.mjs', 'two', dir], { detached: true, stdio: 'ignore' }).unref();",
  '',
].join('\n');

/**
 * A `check` that, where `hold-check` stands, writes both scripts into the scope tree it runs in and leaves three orphans there, as a test
 * killed with its step does (a node that starts them ends at once, so their parent is gone), then waits for `release` as `treeGate` does.
 */
function strayGate(dir: string): string {
  return [
    "import { existsSync, mkdirSync, writeFileSync } from 'node:fs';",
    "import { spawnSync } from 'node:child_process';",
    `const dir = ${JSON.stringify(dir)};`,
    "if (process.argv[2] === 'check' && existsSync('hold-check')) {",
    "  mkdirSync('_fixtures', { recursive: true });",
    `  writeFileSync('_fixtures/stray.mjs', ${JSON.stringify(STRAY)});`,
    `  writeFileSync('_fixtures/make.mjs', ${JSON.stringify(MAKE_STRAYS)});`,
    "  spawnSync(process.execPath, ['_fixtures/make.mjs', dir], { stdio: 'ignore' });",
    '  while (!existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 25));',
    '}',
    "console.log(`${process.argv[2]} ran`);",
    '',
  ].join('\n');
}

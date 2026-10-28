import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync, type ChildProcess } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
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
interface Member { branch: string; tip: string; request?: string; message?: string; commit?: string; at?: string; containedBy?: string; handedBack?: { how: string; failure?: string; with?: string[]; interacting?: string[]; paths?: string[]; why?: string } }
interface Judged { outcome: string; member?: number; with?: number[]; interacting?: number[]; why?: string; said?: string; runs: unknown[] }
interface Candidate { base: string; tip: string; members: Member[]; contained?: Member[]; state: string; reforms: number; recordsOnly: boolean; held?: { on: string; why: string }; run?: { pid: number } }
interface Queue { schema: number; queued: Member[]; previews: Record<string, { tip: string }>; candidate: Candidate | null; stop: unknown; handedBack: Member[]; landed?: Member[]; drained?: Drained }
interface Reservation { runner: string; gate: string; needGb: number; job: string }
interface Admission { schema: number; reservations: Reservation[]; scopedTier: { state: string; since: string } | null; waiting: unknown[] }
interface Job { state: string; workingGb?: number; committedGb?: number; why?: string }
interface Rule { lane?: string; paths?: string[]; gates: string[] | '*' }
interface Drained { tip: string; at: string; ended: string[]; left: string[]; probe: string | null }
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
  drainedRecord: (candidate: Candidate, what: { ended?: string[]; left?: string[]; probe?: string | null }, at?: string) => Drained;
  drainedLine: (drained: Drained) => string;
  sameCandidate: (current: Candidate | null, expected: Candidate) => boolean;
  changeCandidate: (queue: Queue, expected: Candidate, change: (queue: Queue) => { queue: Queue }) => { queue?: Queue; obsolete?: boolean };
  nextMembers: (queued: Member[], options?: { max?: number; oneAtATime?: boolean }) => { members: Member[]; rest: Member[] };
  formCandidate: (input: { base: string; members: Member[]; merge: (member: Member, onto: string, formed: Member[]) => Promise<{ commit?: string; conflict?: string[]; contained?: string }>; at: string }) => Promise<{ candidate: Candidate | null; setAside: Member[]; contained: Member[] }>;
  BY_LINE: string;
  inArrival: (requests: Member[]) => Member[];
  requestsOf: (candidate: Candidate | null, requests?: Member[]) => Member[];
  settleContained: (candidate: Candidate | null, at: string) => { branch: string; tip: string; request?: string; by: string; commit?: string; at: string }[];
  recordsOnly: (paths: string[], ruleOf: (path: string) => Rule | null) => boolean;
  resumeCandidate: (candidate: Candidate | null, facts: { mainTip: string; isAncestor: (a: string, b: string) => boolean; runAlive?: (pid?: number) => boolean }) => string;
  promotion: (candidate: Candidate, facts: { mainTip: string; clean: boolean }) => { action: string; why?: string };
  reformCount: (candidate: Candidate, facts: { recordsOnlyMove: boolean }) => { reforms: number; recordsOnly: boolean; heldByDirect: boolean };
  placeOnPrefixes: (seq: Outcome[], signature: Signature) => { outcome: string; member?: number; interacting?: number[]; why?: string };
  judgeFailure: (failure: Failure, context: { members: number; run: (failure: Failure, k: number) => Promise<Outcome>; alone?: (failure: Failure, k: number) => Promise<Outcome>; known?: { expired: boolean; lastSeen: string } | null; baseCommit?: string }) => Promise<Judged>;
  memoryStep: (attempts: { died: boolean; peakGb?: number }[], options?: { afterHold?: boolean; needGb?: number }) => { next: string; note?: string };
  candidateOutcome: (judgements: { failure: Failure; judged: Judged }[], members: Member[]) => { action: string; handedBack?: Member[]; retain?: Member[]; inherited?: Failure[]; flakes?: Failure[]; why?: string };
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
};
const failures = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/test-failures.mjs') as { gateSignature: (gate: string, step: string, output: string) => Signature };
const tool = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/merge-branch.mjs') as {
  REACH: Rule[];
  ruleFor: (path: string, rules: Rule[], lanes: unknown[]) => Rule | null;
  readLanes: (root: string) => { lanes: unknown[] } | null;
  endTree: (pid: number) => void;
  stepTrees: (options?: { end?: (pid: number) => void }) => Trees;
  watchCandidate: (candidate: Candidate, current: () => Candidate | null, trees: Trees, options?: { ms?: number }) => Watch;
  prefixRunner: (tree: string, candidate: { base: string; members: Member[] }, plan: { name: string; run: string }[], dir: string, watch?: Watch | null) => (failure: Failure, k: number) => Promise<Outcome>;
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
    assert.deepEqual(drained, { tip: 'c', at: AT, ended: ['check', 'driver'], left: ['modules'], probe: 'driver.prefix-2' });
    assert.equal(q.drainedLine(drained), 'ended gates check, driver, the probe driver.prefix-2, each with everything it started; never started: modules');
    assert.equal(q.drainedLine(q.drainedRecord(queue.candidate, {}, AT)), 'ended no gate running, each with everything it started');
  });

  test('QUEUEDRAIN1: the watch ends the run\'s step trees once the candidate is no longer the queue\'s, and a read that fails is not a drop', () => {
    const candidate: Candidate = { base: 'm', tip: 'c', members: [{ branch: 'b1', tip: 'b', commit: 'c', request: 'first' }], state: 'running', reforms: 0, recordsOnly: false };
    const ended: number[] = [];
    const trees = tool.stepTrees({ end: (pid) => { ended.push(pid); } });
    trees.hold(101);
    trees.hold(102);
    let current: Candidate | null = { ...candidate, state: 'bisecting' };
    let unreadable = false;
    const watch = tool.watchCandidate(candidate, () => {
      if (unreadable) throw new Error('the queue is locked');
      return current;
    }, trees);
    assert.equal(watch.check(), false, 'a state change is not a drop');
    unreadable = true;
    assert.equal(watch.check(), false, 'a record that cannot be read ends nothing');
    assert.deepEqual(ended, []);
    unreadable = false;
    current = null;
    assert.equal(watch.check(), true);
    assert.deepEqual(ended, [101, 102], 'every step held, and only those');
    assert.equal(trees.ended, 'the candidate was dropped or replaced');
    current = candidate;
    assert.equal(watch.check(), true, 'once ended it stays ended');
    assert.deepEqual(ended, [101, 102], 'ended once');
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
  const holds = (by: string, ...contained: string[]) => async (member: Member, onto: string) => (contained.includes(member.branch) ? { contained: by } : { commit: `${onto}+${member.branch}` });

  test('one the line holds is contained and settled at once: it is no conflict, and no member of the candidate', async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(2), at: AT, merge: holds(q.BY_LINE, 'b1') });
    assert.deepEqual(formed.setAside, []);
    assert.deepEqual(formed.contained.map((each) => [each.branch, each.containedBy]), [['b1', q.BY_LINE]]);
    assert.deepEqual(formed.candidate!.members.map((each) => each.branch), ['b2']);
    assert.equal(formed.candidate!.contained, undefined, 'the line already holds it, so the candidate waits on nothing');
    const alone = await q.formCandidate({ base: 'm0', members: members(1), at: AT, merge: holds(q.BY_LINE, 'b1') });
    assert.equal(alone.candidate, null, 'a candidate of members the line holds is no candidate');
    assert.equal(alone.contained.length, 1);
  });

  test("one an earlier member holds is provisional: on the candidate, outside its members, settled only at that member's promotion", async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(3), at: AT, merge: holds('b1', 'b2') });
    assert.deepEqual(formed.candidate!.members.map((each) => each.branch), ['b1', 'b3'], "the judge's prefixes stay the merge commits");
    assert.deepEqual(formed.candidate!.contained!.map((each) => [each.branch, each.containedBy]), [['b2', 'b1']]);
    assert.deepEqual(formed.setAside, []);
    assert.deepEqual(q.settleContained(formed.candidate, AT).map((each) => [each.branch, each.by, each.commit]), [['b2', 'b1', 'm0+b1']]);
    assert.deepEqual(q.settleContained({ ...formed.candidate!, members: formed.candidate!.members.slice(1) }, AT), [], 'a holder no longer a member settles nothing');
  });

  test('a candidate that does not promote gives it back as its own request, in arrival order, to be judged again', async () => {
    const queued = members(3).map((each, i) => ({ ...each, request: `r${i + 1}`, at: `2026-10-24T10:00:0${i}.000Z`, message: 'x' }));
    const formed = await q.formCandidate({ base: 'm0', members: queued, at: AT, merge: holds('b1', 'b2') });
    const retained = formed.candidate!.members.map(({ commit, ...request }) => request) as Member[];
    const again = q.requestsOf(formed.candidate, retained);
    assert.deepEqual(again.map((each) => each.branch), ['b1', 'b2', 'b3']);
    assert.ok(again.every((each) => each.containedBy === undefined && each.commit === undefined), 'the frozen request, nothing the candidate added');
    assert.deepEqual(again.map((each) => each.request), ['r1', 'r2', 'r3']);
    assert.deepEqual(q.requestsOf(null, retained).map((each) => each.branch), ['b1', 'b3']);
  });

  test('a branch contained on a candidate is queued already: it is not queued a second time', async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(2), at: AT, merge: holds('b1', 'b2') });
    const tip = 't2'.padEnd(40, '0');
    const queue = q.notePreview({ ...q.emptyQueue(), candidate: formed.candidate }, { branch: 'b2', tip, base: 'm0', at: AT });
    assert.match(q.enqueue(queue, { branch: 'b2', tip, message: 'x', at: AT }).refused ?? '', /b2 is queued already/);
  });

  test("a drop that ends a candidate hands its contained member back with the rest; one dropped by name alone leaves the candidate standing", async () => {
    const formed = await q.formCandidate({ base: 'm0', members: members(3), at: AT, merge: holds('b1', 'b2') });
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

// A gate that fails, saying so on one line, where a file named `break-<gate>` stands in the tree it runs in.
const QUEUE_GATE = [
  "import { existsSync } from 'node:fs';",
  'const name = process.argv[2];',
  'console.log(`${name} ran`);',
  "if (existsSync(`break-${name}`)) { console.log(`error: ${name} is broken`); process.exit(1); }",
  '',
].join('\n');

function sh(cwd: string, ...args: string[]): string {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', env: { ...process.env, ...GIT_ENV } });
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

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
  const fx = makeFixture(`merge-queue-${name}`);
  const root = join(fx.root, 'repo');
  mkdirSync(root, { recursive: true });
  sh(root, 'init', '--quiet', '--template=', '-b', 'main');
  // The guard: this is its own repository, never the one around it.
  assert.equal(realpathSync(sh(root, 'rev-parse', '--show-toplevel')).toLowerCase(), realpathSync(root).toLowerCase());
  writeFileSync(join(root, '.gitignore'), 'local/\n');
  writeFileSync(join(root, 'qgate.mjs'), QUEUE_GATE);
  const gates = [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check' }];
  writeFileSync(join(root, 'daoris.gates.json'), `${JSON.stringify({ gates }, null, 2)}\n`);
  sh(root, 'add', '-A');
  sh(root, 'commit', '--quiet', '-m', `first${TRAILER}`);
  mkdirSync(join(root, 'local'), { recursive: true });
  // TOOLTESTHANG1: the runner's own `git status` (its clean-checkout look, every tick while a candidate is held) refreshes the
  // index and takes `index.lock` to write it back, so a test's git write on this repository met the lock now and then. The
  // runner is told to take no optional lock: it reads the checkout as before and never holds the index, so the test's writes
  // need no retry and no sleep. A write the queue itself makes (the fast-forward, the scope tree's merges) is not optional.
  const env = { ...GIT_ENV, GIT_OPTIONAL_LOCKS: '0', MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_CI_NIGHT_HOUR: 'off', MERGE_BRANCH_QUEUE_POLL_MS: '200', MERGE_BRANCH_QUEUE_TICK_MS: '300', MERGE_BRANCH_DROP_POLL_MS: '200' };
  // Every invocation the test started and has not seen end: a failed assertion leaves them for `cleanup` to end.
  const started = new Set<ChildProcess>();
  const queueFile = join(root, 'local', 'merge-queue.json');
  const queue = () => (existsSync(queueFile)
    ? JSON.parse(readFileSync(queueFile, 'utf8')) as Queue & { runner: unknown }
    : null);
  const admissionFile = join(root, 'local', 'admission.json');
  return {
    root,
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
      sh(root, 'checkout', '--quiet', '-b', branchName, 'main');
      for (const [path, text] of Object.entries(files)) writeFileSync(join(root, path), text);
      sh(root, 'add', '-A');
      sh(root, 'commit', '--quiet', '-m', `work on ${branchName}${TRAILER}`);
      sh(root, 'checkout', '--quiet', 'main');
      writeFileSync(join(root, 'local', `${branchName}.msg`), `Merge ${branchName}: its words${TRAILER}\n`);
    },
    /** One invocation of the tool, bounded: one that has given no verdict by `ms` is ended with its tree, and answers `status: null`. */
    run(args: string[], extra: Record<string, string> = {}, ms = HANG_MS): Promise<{ status: number | null; out: string }> {
      const child = spawn(process.execPath, [TOOL, ...args], { cwd: root, env: { ...process.env, ...env, ...extra } });
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
 * for `release` in `QUEUE_DRAIN_DIR`: a process tree three deep, the shape a build and its test hosts make. `count` is how many
 * times `check` has run there. With `record`, the arguments it was given are kept in `args`.
 */
function treeGate(holdWhen: string, record = ''): string {
  return [
    "import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';",
    "import { spawn } from 'node:child_process';",
    'const dir = process.env.QUEUE_DRAIN_DIR;',
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

describe('the queue, end to end in a scratch repository', { concurrency: true }, () => {
  test('QUEUEDROP1: a dropped candidate cannot return when its held gate completes', async () => {
    const repo = queueScratch('drop-late');
    const hold = join(repo.root, 'local', 'release');
    const started = `${hold}.started`;
    try {
      const gate = [
        "import { existsSync, writeFileSync } from 'node:fs';",
        'const hold = process.env.QUEUE_DROP_HOLD;',
        "if (process.argv[2] === 'check' && hold) {",
        "  writeFileSync(`${hold}.started`, 'started');",
        '  while (!existsSync(hold)) await new Promise((done) => setTimeout(done, 25));',
        '}',
        'console.log(`${process.argv[2]} ran`);',
      ].join('\n');
      repo.branch('b1', { 'a.txt': 'a\n', 'qgate.mjs': gate });
      const before = sh(repo.root, 'rev-parse', 'main');
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg'], { QUEUE_DROP_HOLD: hold });
      await repo.until(() => existsSync(started));
      assert.ok(existsSync(started), repo.log());
      assert.equal((await repo.run(['--drop-batch', 'b1'])).status, 0);
      writeFileSync(hold, 'release');
      assert.equal((await waiting).status, 1);
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(repo.queue()?.candidate, null, repo.log());
      assert.equal(repo.queue()?.stop, null, repo.log());
      assert.equal(sh(repo.root, 'rev-parse', 'main'), before);
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
      sh(repo.root, 'add', 'qgate.mjs');
      sh(repo.root, 'commit', '--quiet', '-m', `repair the base${TRAILER}`);
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
      assert.equal(sh(repo.root, 'log', '-1', '--format=%s'), 'Merge b1: its words', "the message is the parent's file, verbatim");
      assert.match(sh(repo.root, 'log', '-1', '--format=%P'), / /, 'one merge commit, --no-ff');
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
      const runs = q.readScopeRuns(repo.root) as { state: string; members: { branch: string }[]; judged?: { outcome: string; member: number }[] }[];
      const red = runs.find((each) => each.state === 'red');
      assert.deepEqual(red?.members.map((each) => each.branch), ['good', 'bad'], `one candidate takes both, never good alone first:\n${repo.log()}`);
      assert.deepEqual(red?.judged?.map((each) => [each.outcome, each.member]), [['pinned', 2]]);
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
      sh(repo.root, 'add', '-A');
      sh(repo.root, 'commit', '--quiet', '-m', `a records commit that breaks a check${TRAILER}`);
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => Boolean(repo.queue()?.stop));
      assert.ok(repo.queue()?.stop, `the queue never stopped:\n${repo.log()}`);
      const said = await repo.run(['--queue']);
      assert.match(said.out, /stopped since/);
      assert.match(said.out, /universal fails on the base/);
      sh(repo.root, 'rm', '--quiet', 'break-universal');
      sh(repo.root, 'commit', '--quiet', '-m', `the fix${TRAILER}`);
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
  const nested = (repo: ReturnType<typeof queueScratch>, files: Record<string, string> = { 'a.txt': 'a\n', 'b.txt': 'b\n' }) => {
    const [[first, text], ...rest] = Object.entries(files);
    repo.branch('outer', { [first!]: text! });
    sh(repo.root, 'branch', 'inner', 'outer');
    sh(repo.root, 'checkout', '--quiet', 'outer');
    for (const [path, body] of rest) {
      writeFileSync(join(repo.root, path), body);
      sh(repo.root, 'add', '-A');
      sh(repo.root, 'commit', '--quiet', '-m', `more work on outer${TRAILER}`);
    }
    sh(repo.root, 'checkout', '--quiet', 'main');
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
      assert.equal(sh(repo.root, 'rev-list', '--count', '--merges', 'main'), '1', "one merge commit, the container's; nothing was merged for the contained one");
      const queue = repo.queue()!;
      assert.deepEqual((queue as unknown as { contained: { branch: string; by: string }[] }).contained.map((each) => [each.branch, each.by]), [['inner', 'outer']]);
      assert.equal(queue.handedBack.length, 0);
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('a member the line holds by the time its candidate forms is contained at once, and said so', async () => {
    const repo = queueScratch('contained-line');
    try {
      nested(repo);
      // Queued behind a candidate in flight: the enqueue refuses a branch the line holds, but main lacks inner until outer promotes.
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg']);
      await repo.until(() => repo.queue()?.candidate !== null && repo.queue()?.candidate !== undefined);
      const inner = repo.run(['inner', '--message', 'local/inner.msg']);
      const [outerOut, done] = await Promise.all([outer, inner]);
      assert.equal(outerOut.status, 0, `${outerOut.out}\n${repo.log()}`);
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /inner is contained: main holds it already/);
      assert.doesNotMatch(repo.log(), /set aside|could not be made/);
      assert.deepEqual((repo.queue() as unknown as { contained: { branch: string; by: string }[] }).contained.map((each) => [each.branch, each.by]), [['inner', 'line']]);
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

  test('a tracked change in the main checkout holds promotion until it is clean', async () => {
    const repo = queueScratch('dirty');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      writeFileSync(join(repo.root, 'qgate.mjs'), `${QUEUE_GATE}// an edit the parent has not committed\n`);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => repo.queue()?.candidate?.state === 'held');
      assert.equal(repo.queue()?.candidate?.state, 'held', repo.log());
      sh(repo.root, 'checkout', '--quiet', '--', 'qgate.mjs');
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
      const refused = spawnSync('git', ['checkout', '--quiet', '--', 'qgate.mjs'], { cwd: repo.root, encoding: 'utf8', env: { ...process.env, ...GIT_ENV } });
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

  test('QUEUEDRAIN1: a candidate dropped while its gate runs ends the gate, its child and grandchild; the scoped tier is released after; what is not its own survives; the next candidate forms at once', async () => {
    const repo = queueScratch('drain-gate');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    const bystander = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore' });
    try {
      repo.branch('b1', { 'hold-check': 'x\n', 'qgate.mjs': treeGate("existsSync('hold-check')") });
      repo.branch('b2', { 'b.txt': 'b\n' });
      for (const name of ['b1', 'b2']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const env = { QUEUE_DRAIN_DIR: dir };
      const first = repo.run(['b1', '--message', 'local/b1.msg'], env);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.ok(pids.every(alive), 'the gate and what it started run');
      await repo.until(() => repo.admission()?.scopedTier?.state === 'running');
      assert.equal(repo.admission()?.scopedTier?.state, 'running');
      const second = repo.run(['b2', '--message', 'local/b2.msg'], env);
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
      // The first run of `check` (the candidate's) fails; every run after it is a probe, and a probe holds.
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate('count >= 2'));
      sh(repo.root, 'commit', '--quiet', '-am', `a gate that holds its probes${TRAILER}`);
      repo.branch('good', { 'good.txt': 'fine\n' });
      repo.branch('bad', { 'break-check': 'x\n' });
      for (const name of ['good', 'bad']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const env = { QUEUE_DRAIN_DIR: dir, MERGE_BRANCH_QUEUE_HOLD: hold };
      const good = repo.run(['good', '--message', 'local/good.msg'], env);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const bad = repo.run(['bad', '--message', 'local/bad.msg'], env);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 2, 'the candidate ran once, and its first probe is the one held');
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
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 2);
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
    const path = process.env.PATH ?? process.env.Path ?? '';
    try {
      // A `dotnet` that records what it was asked and holds, with a child and a grandchild, as a test host does.
      writeFileSync(join(shims, 'dotnet.mjs'), treeGate('true', 'args'));
      if (process.platform === 'win32') writeFileSync(join(shims, 'dotnet.cmd'), `@node "%~dp0dotnet.mjs" check %*\r\n`);
      else {
        writeFileSync(join(shims, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/dotnet.mjs" check "$@"\n', { mode: 0o755 });
      }
      process.env.PATH = `${shims}${process.platform === 'win32' ? ';' : ':'}${path}`;
      process.env.QUEUE_DRAIN_DIR = dir;
      repo.branch('b1', { 'a.txt': 'a\n' });
      const base = sh(repo.root, 'rev-parse', 'main');
      const commit = sh(repo.root, 'rev-parse', 'b1');
      const candidate: Candidate = { base, tip: commit, members: [{ branch: 'b1', tip: commit, commit, request: 'first' }], state: 'running', reforms: 0, recordsOnly: false };
      let current: Candidate | null = candidate;
      const watch = tool.watchCandidate(candidate, () => current, tool.stepTrees(), { ms: 50 }).start();
      const plan = [{ name: 'driver', run: 'dotnet test Fixture.csproj --filter "Category!=Process"' }];
      const failure: Failure = { unit: 'SessionTests', test: 'Driver.Tests.SessionTests.Resumes', gate: 'driver', signature: sig('x'), names: 'test' };
      const probe = tool.prefixRunner(repo.root, candidate, plan, dir, watch);
      const running = probe(failure, 1).then(() => 'answered', (error: Error) => error.message);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3);
      assert.match(readFileSync(join(dir, 'args'), 'utf8'), /\(Category!=Process\)&\(FullyQualifiedName~SessionTests\)/, 'the probe is the gate narrowed to the failing class');
      current = null;
      assert.match(await running, /was dropped/);
      assert.deepEqual(pids.filter(alive), [], 'the class probe, its child and its grandchild are gone');
      const runsBefore = readFileSync(join(dir, 'args'), 'utf8');
      await assert.rejects(probe(failure, 0), /was dropped/);
      assert.equal(readFileSync(join(dir, 'args'), 'utf8'), runsBefore, 'no probe started after the drop');
      watch.stop();
    } finally {
      process.env.PATH = path;
      delete process.env.QUEUE_DRAIN_DIR;
      writeFileSync(join(dir, 'release'), '');
      await repo.cleanup();
    }
  });
});

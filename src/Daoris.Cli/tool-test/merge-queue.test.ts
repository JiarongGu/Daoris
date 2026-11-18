import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import {
  repoRoot, AT, type Signature, type Outcome, type Failure, type Member, q, type Candidate, tool, sig, failures,
  type Evidence, type Whole, type Confirm, type Judged, type Job,
} from './support/merge-queue-kit.ts';

/**
 * The merge queue and its candidates (design §4 and §5.1), the frozen request, the judgement of a red
 * candidate over its base and every prefix by the failure's signature, a death of memory kept out of the base run and the stop,
 * the records-only re-form, resume by ancestry, the scoped set with the git half's budget, and the admission record both runners
 * share (`tools/merge-queue.mjs`). The merge tool's verbs over them are in `merge-branch.test.ts`. Run by the `tool-tests` gate.
 */
const NOW = Date.parse(AT);
const fail = (signature: Signature): Outcome => ({ result: 'fail', signature });
const PASS: Outcome = { result: 'pass' };
const testFailure = (signature: Signature): Failure => ({ unit: 'SessionTests', test: signature.test, gate: 'driver', signature, names: 'test' });

/** A runner over a fixed answer per prefix (and per second run, where given), keeping the order it is asked. */
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
    // What a test left running is said with the gates.
    assert.equal(q.drainedLine(q.drainedRecord(queue.candidate, { ended: ['check'], strays: [11, 12] }, AT)), 'ended gates check, each with everything it started, and 2 processes their tests had left running under the scope tree (pid 11, 12)');
    assert.equal(q.drainedLine(q.drainedRecord(queue.candidate, { strays: [11] }, AT)), 'ended no gate running, each with everything it started, and 1 process their tests had left running under the scope tree (pid 11)');
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
    // The take is said before anything is ended, and once.
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

  test('a record that exists but cannot be read is no empty queue, which the next write would put in its place', () => {
    mkdirSync(join(repoRoot, '_fixtures'), { recursive: true });
    const home = mkdtempSync(join(repoRoot, '_fixtures', 'merge-queue-'));
    try {
      mkdirSync(join(home, q.MERGE_QUEUE), { recursive: true });
      assert.throws(() => q.readQueue(home), /EISDIR/);
      rmSync(join(home, q.MERGE_QUEUE), { recursive: true });
      writeFileSync(join(home, q.MERGE_QUEUE), '{ not json');
      assert.deepEqual(q.readQueue(home).queued, [], 'text that does not parse is read as no queue');
    } finally {
      rmSync(home, { recursive: true, force: true });
    }
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

// A contained member (design §5.2 step 3) is recorded as contained, never set aside; one an earlier member holds is
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

  // The line's marker is not the word `line`, which a branch may be named.
  test('an earlier member whose branch is named `line` is a member, never the line: what it holds is provisional on it', async () => {
    const queued = [{ branch: 'line', tip: 't1'.padEnd(40, '0'), request: 'r1' }, { branch: 'b2', tip: 't2'.padEnd(40, '0'), request: 'r2' }];
    const formed = await q.formCandidate({ base: 'm0', members: queued, at: AT, merge: inMember('line', 'b2') });
    assert.deepEqual(formed.candidate!.contained!.map((each) => [each.branch, each.containedIn]), [['b2', { branch: 'line', request: 'r1' }]]);
    assert.deepEqual(q.settleContained({ ...formed.candidate!, members: [] }, AT), [], 'its container gone, nothing is settled');
    assert.deepEqual(q.settleContained(formed.candidate, AT).map((each) => each.containedIn), [{ branch: 'line', request: 'r1', commit: 'm0+line' }]);
    assert.match(q.queueLines({ ...q.emptyQueue(), candidate: formed.candidate }).join('\n'), /contained: b2 at t200000, in line \(settled when line promotes\)/);
  });

  test('a candidate that does not promote gives it back as its own request, in arrival order, to be judged again', async () => {
    const queued = members(3).map((each, i) => ({ ...each, request: `r${i + 1}`, seq: i + 1, at: `2026-10-24T10:00:0${i}.000Z`, message: 'x' }));
    const formed = await q.formCandidate({ base: 'm0', members: queued, at: AT, merge: inMember('b1', 'b2') });
    const retained = formed.candidate!.members.map(({ commit, ...request }) => request) as Member[];
    const again = q.requestsOf(formed.candidate, retained);
    assert.deepEqual(again.map((each) => each.branch), ['b1', 'b2', 'b3']);
    assert.ok(again.every((each) => each.containedIn === undefined && each.commit === undefined), 'the frozen request, nothing the candidate added');
    assert.deepEqual(again.map((each) => each.request), ['r1', 'r2', 'r3']);
    assert.deepEqual(q.requestsOf(null, retained).map((each) => each.branch), ['b1', 'b3']);
  });

  // Arrival is not read off a clock of whole seconds.
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
    assert.deepEqual(q.inArrival([{ ...queue.queued[1]! }, { ...queue.queued[0]! }]).map((each) => each.branch), ['b1', 'b2']);
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
 * The confirm run's pure pieces (design §4.5, §5.3 step 3). The failing
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

  // What the queue's runner does with the readings it makes.
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

  test('a reached Process or git half is deferred to the CI run on main, and runs before the landing only when it holds a known red', () => {
    const set = q.scopedSet({ gates, reached: new Set(['driver-process', 'driver-process-git']) });
    assert.deepEqual(set.run.map((each) => each.name), ['universal']);
    assert.deepEqual(set.deferred.map((each) => each.name), ['driver-process', 'driver-process-git']);
    assert.match(set.deferred[0]!.why, /CI run on main/);
    const red = q.scopedSet({ gates, reached: new Set(['driver-process', 'driver-process-git']), knownRed: new Set(['driver-process-git']) });
    assert.deepEqual(red.run.map((each) => each.name), ['universal', 'driver-process-git']);
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

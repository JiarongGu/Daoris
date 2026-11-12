import { test } from 'node:test';
import assert from 'node:assert/strict';
import { clearMerged, staleRun, type QueueRecord } from './queue-stale.ts';

const candidate = (over: Record<string, unknown> = {}) => ({ state: 'running', run: { pid: 10 }, members: [{ branch: 'a', tip: 'ta', request: 'r1' }], ...over });
const queue = (over: Partial<QueueRecord> = {}): QueueRecord => ({ runner: { pid: 20 }, candidate: candidate(), queued: [], ...over });

test('a candidate that runs with no live run and no live runner is stale; a live one of either is not', () => {
  assert.match(staleRun(queue(), () => false) as string, /says running, but its run \(pid 10\) has ended and the queue's runner \(pid 20\) is not running/);
  assert.equal(staleRun(queue(), (pid) => pid === 10), null);
  assert.equal(staleRun(queue(), (pid) => pid === 20), null);
  assert.equal(staleRun(queue({ candidate: candidate({ state: 'green' }) }), () => false), null, 'a green candidate claims no run');
  assert.match(staleRun(queue({ runner: null, candidate: candidate({ run: undefined }) }), () => false) as string, /its run has ended and the queue's runner is not running/);
});

test('a queued request and a candidate whose tips main holds are cleared into landed, the rest left', () => {
  const merged = (tip: string) => tip === 'ta' || tip === 'tq';
  const swept = clearMerged(queue({ queued: [{ branch: 'q', tip: 'tq', request: 'r2' }, { branch: 'w', tip: 'tw', request: 'r3' }] }), merged, '2026-11-13T00:00:00Z');
  assert.deepEqual(swept.cleared, ['q', 'a']);
  assert.equal(swept.queue.candidate, null);
  assert.deepEqual(swept.queue.queued.map((each) => each.branch), ['w']);
  assert.deepEqual((swept.queue.landed as Array<{ branch: string; commit: string }>).map((each) => [each.branch, each.commit]), [['q', 'tq'], ['a', 'ta']]);
});

test('a candidate with a member main lacks stays, and a queue with nothing merged is the queue as it was', () => {
  const two = queue({ candidate: candidate({ members: [{ branch: 'a', tip: 'ta' }, { branch: 'b', tip: 'tb' }] }) });
  assert.equal(clearMerged(two, (tip) => tip === 'ta', 'x').queue.candidate, two.candidate);
  assert.equal(clearMerged(two, () => false, 'x').queue, two);
});

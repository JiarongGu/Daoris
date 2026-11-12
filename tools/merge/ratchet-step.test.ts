import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ratchetStep } from './ratchet-step.ts';

test('a merge whose gates wrote no timing records passes the ratchet, asked for the records since its start', () => {
  const asked: number[] = [];
  const result = ratchetStep((since) => { asked.push(since); return { status: 0, output: 'time-ratchet: clean (0 tests over their limits)\n' }; }, 1234);
  assert.deepEqual(asked, [1234]);
  assert.equal(result.verdict, 'PASS');
  assert.equal(result.gate.name, 'time-ratchet');
});

test('a merge whose own census run is over its listed time fails the ratchet like a gate, with the test named', () => {
  const result = ratchetStep(() => ({ status: 1, output: 'time-ratchet: A.Census took 41000 ms, longer than its listed 35000 ms\n' }), 1);
  assert.equal(result.verdict, 'FAIL');
  assert.match(result.note, /A\.Census took 41000 ms/);
  assert.equal(ratchetStep(() => ({ status: 2, output: '' }), 1).verdict, 'FAIL');
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { slowestLines } from './merge-branch.mjs';

const slowest = [{ ms: 3000, tests: 1, name: 'SlowProcessTests' }];

test('a gate result with its slowest classes but no results file prints them and names no file', () => {
  const lines = slowestLines('.', { gate: { name: 'driver-process' }, slowest });
  assert.match(lines[0], /slowest classes of driver-process, by their own times:$/);
  assert.match(lines[1], /SlowProcessTests$/);
});

test('a gate result with its results file names it from the root', () => {
  const lines = slowestLines('.', { gate: { name: 'driver-process' }, slowest, trx: 'gate.trx' });
  assert.match(lines[0], /, from gate\.trx:$/);
});

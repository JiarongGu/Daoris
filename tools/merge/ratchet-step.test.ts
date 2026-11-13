import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gatesToRerun, ratchetIn, ratchetStep, withFinalRatchet } from './ratchet-step.ts';

test('the time check named to --rerun is no gate to run again, and the other names stay', () => {
  assert.deepEqual(gatesToRerun(['cli', 'time-ratchet', 'web']), ['cli', 'web']);
  assert.deepEqual(gatesToRerun(['time-ratchet']), []);
});

test('a rerun judges the records since the merge began, not only since its own start', async () => {
  const asked: number[] = [];
  const ratchet = (since: number) => { asked.push(since); return { status: 0, output: 'clean\n' }; };
  await withFinalRatchet(async () => [], ratchet, () => 5000, 1000);
  await withFinalRatchet(async () => [], ratchet, () => 5000, null);
  await withFinalRatchet(async () => [], ratchet, () => 5000, 9000);
  assert.deepEqual(asked, [1000, 5000, 5000]);
});

test('a repository with no time ratchet has nothing to judge: the last check passes and says so', () => {
  const root = join(dirname(dirname(dirname(fileURLToPath(import.meta.url)))), '_fixtures', 'ratchet-step-none');
  rmSync(root, { recursive: true, force: true });
  mkdirSync(root, { recursive: true });
  try {
    const result = ratchetStep(ratchetIn(root, process.env), 1);
    assert.equal(result.verdict, 'PASS');
    assert.match(result.note, /no time ratchet/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

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

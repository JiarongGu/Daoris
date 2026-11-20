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

test('the ratchet\'s breaches, each with its cause, ride on its result and stay out of its note', () => {
  const breach = { kind: 'test', name: 'A.Tests.slow', ms: 40_000, limit: 30_000, cause: 'clock', evidence: '2 waits', fix: 'UNITPURE1', fails: true };
  const output = `time-ratchet: over budget: A.Tests.slow took 40000 ms\ntime-breach: ${JSON.stringify(breach)}\ntime-ratchet: A.Tests.slow took 40000 ms, over its 30000 ms limit\n`;
  const result = ratchetStep(() => ({ status: 1, output }), 0, () => 0);
  assert.deepEqual(result.breaches, [breach]);
  assert.doesNotMatch(result.note, /time-breach/);
  assert.match(result.note, /^2 lines: /);
  assert.equal(ratchetStep(() => ({ status: 0, output: 'clean\n' }), 0, () => 0).breaches, undefined);
});

test('a rerun that ran gates judges only the records since its own start, not a first run the merge killed for load', async () => {
  const asked: number[] = [];
  const ratchet = (since: number) => { asked.push(since); return { status: 0, output: 'clean\n' }; };
  await withFinalRatchet(async () => [{}], ratchet, () => 5000, 1000);
  await withFinalRatchet(async () => [{}], ratchet, () => 5000, null);
  assert.deepEqual(asked, [5000, 5000]);
});

test('a rerun of the ratchet alone judges the records since the merge began, which an earlier run measured again', async () => {
  const asked: number[] = [];
  const ratchet = (since: number) => { asked.push(since); return { status: 0, output: 'clean\n' }; };
  await withFinalRatchet(async () => [], ratchet, () => 5000, 1000);
  await withFinalRatchet(async () => [], ratchet, () => 5000, 9000);
  assert.deepEqual(asked, [1000, 5000]);
});

test('slow timings written before a rerun pass it when its own are within budget, and an over-budget one of its own still fails', async () => {
  const records = [{ at: 2000, ms: 45_000 }, { at: 6000, ms: 100 }];
  const judge = (since: number) => {
    const over = records.filter((record) => record.at >= since && record.ms > 30_000);
    return over.length ? { status: 1, output: `time-ratchet: over budget ${over[0]?.ms}\n` } : { status: 0, output: 'clean\n' };
  };
  const clock = () => 5000;
  const gates = async () => [ratchetStep(() => ({ status: 0, output: 'clean\n' }), 0, clock)];
  assert.equal((await withFinalRatchet(gates, judge, clock, 1000)).at(-1)?.verdict, 'PASS');
  records.push({ at: 6500, ms: 40_000 });
  assert.equal((await withFinalRatchet(gates, judge, clock, 1000)).at(-1)?.verdict, 'FAIL');
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

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../../test/_fixture.ts';

// test-map: reads the merge tool's lock unit only
// reaches: tool-tests — the unit and its test are the tool-tests gate's

const locks = await import(new URL('../../../../tools/merge-branch/locks.mjs', import.meta.url).href) as {
  holdingLock: (file: string, fn: () => unknown, options?: { staleMs?: number; lives?: (pid: number) => boolean; now?: () => number; pause?: (ms: number) => void }) => unknown;
  putComplete: (path: string, text: string) => boolean;
};

const SINCE = Date.parse('2026-11-09T09:00:00Z');
const said = (pid: number) => `${JSON.stringify({ pid, since: new Date(SINCE).toISOString() })}\n`;

test('CODESTRREV2a: a verdict lock a live run holds is waited for past ten seconds, taken once it is stale, and an older run never lets go of the next one\'s', () => {
  const fx = makeFixture('merge-branch-store-lock');
  try {
    const file = join(fx.root, 'gate-verdicts.json');
    const lock = `${file}.lock`;
    writeFileSync(lock, said(4242));
    let clock = SINCE;
    const pauses: number[] = [];
    let ranAt = -1;
    locks.holdingLock(file, () => { ranAt = clock - SINCE; }, {
      lives: (pid) => pid === 4242 || pid === process.pid,
      now: () => clock,
      pause: (ms) => { pauses.push(clock - SINCE); clock += ms * 100; },
    });
    assert.ok(pauses.some((at) => at > 10_000), 'a live holder is waited for beyond the old ten seconds');
    assert.ok(ranAt > 30_000, `the lock is taken only once stale (ran at ${ranAt} ms)`);
    assert.equal(existsSync(lock), false);

    // A run whose lock another took over meanwhile must not remove the taker's.
    locks.holdingLock(file, () => { writeFileSync(lock, said(777)); });
    assert.equal(readFileSync(lock, 'utf8'), said(777), "another run's lock stands after this run's release");
  } finally {
    fx.cleanup();
  }
});

test('CODESTRREV2b: a gate lock is put in place complete, never overwriting another run\'s, and leaves nothing beside', () => {
  const fx = makeFixture('merge-branch-gate-lock-put');
  try {
    const lock = join(fx.root, 'gate.lock');
    assert.equal(locks.putComplete(lock, '{"pid":1}\n'), true);
    assert.equal(readFileSync(lock, 'utf8'), '{"pid":1}\n');
    assert.equal(locks.putComplete(lock, '{"pid":2}\n'), false);
    assert.equal(readFileSync(lock, 'utf8'), '{"pid":1}\n', 'the lock another run holds is left whole');
    assert.deepEqual(readdirSync(fx.root), ['gate.lock']);
  } finally {
    fx.cleanup();
  }
});

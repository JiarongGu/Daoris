import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../../test/_fixture.ts';
import { refusal, tool } from '../support/merge-branch.ts';

// test-map: reads the merge tool's lock unit only
// reaches: tool-tests — the unit and its test are the tool-tests gate's

const locks = await import(new URL('../../../../tools/merge-branch/locks.mjs', import.meta.url).href) as {
  holdingLock: (file: string, fn: () => unknown, options?: { staleMs?: number; lives?: (pid: number) => boolean; now?: () => number; pause?: (ms: number) => void }) => unknown;
  putComplete: (path: string, text: string) => boolean;
  lockText: (path: string) => string | null;
  removeWhileSame: (path: string, text: string | null) => boolean;
  takeLock: (path: string, text: string, live: (held: string) => boolean, attempts?: number) => { taken: boolean; held: string | null };
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

test('a run that takes over a dead gate lock never removes the lock another run put in place while it ended the processes of the dead run', () => {
  const fx = makeFixture('merge-branch-gate-lock-race');
  try {
    mkdirSync(join(fx.root, 'local'), { recursive: true });
    const file = join(fx.root, tool.GATE_TREE_LOCK);
    writeFileSync(file, `${JSON.stringify({ pid: 4242, commit: 'a'.repeat(40), at: '2026-10-18T10:00:00Z' })}\n`);
    const other = `${JSON.stringify({ pid: 9999, commit: 'c'.repeat(40), at: '2026-10-18T12:00:00Z', holder: 'ci' })}\n`;
    // Ending the dead run's processes takes seconds, and a second run that found the same dead lock takes it over in that time.
    const end = (): number[] => { rmSync(file, { force: true }); writeFileSync(file, other); return []; };
    const error = refusal(() => tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: (pid: number) => pid === 9999, end, endStrays: () => [] }));
    assert.match(error.message, /a CI run holds the gate tree already/);
    assert.equal(readFileSync(file, 'utf8'), other, 'the second run still holds its lock');
  } finally {
    fx.cleanup();
  }
});

test('removeWhileSame leaves a lock that reads differently, and one that is gone', () => {
  const fx = makeFixture('merge-branch-lock-same');
  try {
    const lock = join(fx.root, 'gate.lock');
    writeFileSync(lock, 'one');
    assert.equal(locks.removeWhileSame(lock, 'two'), false);
    assert.equal(existsSync(lock), true);
    assert.equal(locks.removeWhileSame(lock, 'one'), true);
    assert.equal(existsSync(lock), false);
    assert.equal(locks.removeWhileSame(lock, locks.lockText(lock)), false, 'a lock that was not there is no lock to remove');
  } finally {
    fx.cleanup();
  }
});

test('takeLock takes a lock no run holds, and leaves the one another run took over while this run judged the dead one', () => {
  const fx = makeFixture('merge-branch-take-lock');
  try {
    const lock = join(fx.root, 'watch.lock');
    assert.deepEqual(locks.takeLock(lock, 'mine', () => true), { taken: true, held: null });
    assert.deepEqual(locks.takeLock(lock, 'second', (held) => held === 'mine'), { taken: false, held: 'mine' }, 'a live holder is left');
    assert.equal(readFileSync(lock, 'utf8'), 'mine');

    writeFileSync(lock, 'dead');
    const judged: string[] = [];
    assert.deepEqual(locks.takeLock(lock, 'third', (held) => { judged.push(held); return held !== 'dead'; }), { taken: true, held: null }, 'a dead lock is taken over');
    assert.equal(readFileSync(lock, 'utf8'), 'third');
    assert.deepEqual(judged, ['dead']);

    // A second run takes the dead lock over while this one is still judging it: its lock stands.
    writeFileSync(lock, 'dead');
    const result = locks.takeLock(lock, 'late', (held) => {
      if (held !== 'dead') return true;
      rmSync(lock, { force: true });
      writeFileSync(lock, 'other');
      return false;
    });
    assert.deepEqual(result, { taken: false, held: 'other' });
    assert.equal(readFileSync(lock, 'utf8'), 'other');
  } finally {
    fx.cleanup();
  }
});

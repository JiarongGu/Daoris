import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { holderLine, holdingCheckout, releaseCheckout, takeCheckout, type Holder } from './checkout-lock.ts';

const me: Holder = { pid: 100, started: 1000, at: '2026-11-13T10:00:00Z', verb: 'rerun' };
const other: Holder = { pid: 200, started: 2000, at: '2026-11-13T10:01:00Z', verb: 'merge' };

test('one run holds the checkout; a second is refused with the holder\'s pid, start and verb, and a dead holder is taken over', () => {
  const { dir, remove } = fixtureFolder('checkout-lock');
  try {
    const file = join(dir, 'local', 'merge-run.lock');
    assert.deepEqual(takeCheckout(file, me, { runs: () => true }), { taken: true });
    const second = takeCheckout(file, other, { runs: () => true });
    assert.deepEqual(second, { taken: false, holder: me });
    assert.match(holderLine(me), /pid 100, started 2026-11-13T10:00:00Z, running a rerun/);

    assert.deepEqual(takeCheckout(file, other, { runs: () => false }), { taken: true }, 'a holder that no longer runs is taken over');
    assert.equal(JSON.parse(readFileSync(file, 'utf8')).pid, 200);

    releaseCheckout(file, me);
    assert.equal(existsSync(file), true, 'a lock another run took since is not let go');
    releaseCheckout(file, other);
    assert.equal(existsSync(file), false);
  } finally {
    remove();
  }
});

test('a run held under the lock lets it go however it ends, and a second is refused naming the first', async () => {
  const { dir, remove } = fixtureFolder('checkout-lock');
  try {
    const file = join(dir, 'merge-run.lock');
    const refuse = (message: string) => new Error(message);
    const seen: string[] = [];
    await holdingCheckout(file, me, () => true, async () => {
      await assert.rejects(holdingCheckout(file, other, () => true, async () => { seen.push('second ran'); }, refuse), /holds this checkout \(pid 100, started 2026-11-13T10:00:00Z, running a rerun\)/);
    }, refuse);
    assert.deepEqual(seen, []);
    assert.equal(existsSync(file), false, 'let go when it ended');
    await assert.rejects(holdingCheckout(file, me, () => true, async () => { throw new Error('boom'); }, refuse), /boom/);
    assert.equal(existsSync(file), false, 'and when it threw');
  } finally {
    remove();
  }
});

test('a lock that does not read is no run\'s, and is replaced', () => {
  const { dir, remove } = fixtureFolder('checkout-lock');
  try {
    const file = join(dir, 'merge-run.lock');
    writeFileSync(file, 'garbage');
    assert.deepEqual(takeCheckout(file, me, { runs: () => true }), { taken: true });
  } finally {
    remove();
  }
});

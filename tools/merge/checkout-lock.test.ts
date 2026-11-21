import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { LANDING_VERB, holderLine, holdingCheckout, releaseCheckout, takeCheckout, type Holder, type Waiting } from './checkout-lock.ts';

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

const landing: Holder = { pid: 300, started: 3000, at: '2026-11-21T10:00:00Z', verb: LANDING_VERB };

const waitingWith = (over: Partial<Waiting> = {}): Waiting => ({ ms: 60_000, pollMs: 1000, sleep: async () => {}, now: () => 0, say: () => {}, ...over });

test('a run that finds a landing in flight waits, said once, and runs when the landing ends', async () => {
  const { dir, remove } = fixtureFolder('checkout-lock');
  try {
    const file = join(dir, 'merge-run.lock');
    const refuse = (message: string) => new Error(message);
    assert.deepEqual(takeCheckout(file, landing, { runs: () => true }), { taken: true });
    let clock = 0;
    const said: string[] = [];
    const slept: number[] = [];
    const sleep = async (ms: number) => {
      slept.push(ms);
      clock += ms;
      if (slept.length === 3) releaseCheckout(file, landing);
    };
    const ran: string[] = [];
    await holdingCheckout(file, me, () => true, async () => { ran.push('merge'); }, refuse, waitingWith({ sleep, now: () => clock, say: (line) => said.push(line) }));
    assert.deepEqual(ran, ['merge']);
    assert.deepEqual(slept, [1000, 1000, 1000]);
    assert.equal(said.length, 1, 'said once, not at each look');
    assert.match(said[0] ?? '', /a landing is moving the line \(pid 300, started 2026-11-21T10:00:00Z\): waiting for it to end/);
    assert.equal(existsSync(file), false, 'the run let its lock go');
  } finally {
    remove();
  }
});

test('a landing that outlasts the wait refuses the run, naming it, and a merge holder is never waited for', async () => {
  const { dir, remove } = fixtureFolder('checkout-lock');
  try {
    const file = join(dir, 'merge-run.lock');
    const refuse = (message: string) => new Error(message);
    takeCheckout(file, landing, { runs: () => true });
    let clock = 0;
    await assert.rejects(
      holdingCheckout(file, me, () => true, async () => { throw new Error('ran'); }, refuse, waitingWith({ ms: 2500, sleep: async (ms) => { clock += ms; }, now: () => clock })),
      /holds this checkout \(pid 300, started 2026-11-21T10:00:00Z, running a landing\)/,
    );
    assert.ok(clock >= 2500 && clock < 5000, 'waited the deadline, no longer');

    releaseCheckout(file, landing);
    takeCheckout(file, other, { runs: () => true });
    let slept = 0;
    await assert.rejects(
      holdingCheckout(file, me, () => true, async () => {}, refuse, waitingWith({ sleep: async () => { slept += 1; } })),
      /running a merge/,
    );
    assert.equal(slept, 0, 'a second merge run is refused at once, as it always was');
  } finally {
    remove();
  }
});

test('a landing whose process was killed is taken back at once, with no wait', async () => {
  const { dir, remove } = fixtureFolder('checkout-lock');
  try {
    const file = join(dir, 'merge-run.lock');
    takeCheckout(file, landing, { runs: () => true });
    let slept = 0;
    const ran: string[] = [];
    await holdingCheckout(file, me, (held) => held.pid !== landing.pid, async () => {
      ran.push('merge');
      assert.equal(JSON.parse(readFileSync(file, 'utf8')).pid, me.pid);
    }, (message) => new Error(message), waitingWith({ sleep: async () => { slept += 1; } }));
    assert.deepEqual(ran, ['merge']);
    assert.equal(slept, 0);
  } finally {
    remove();
  }
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { homeEntry, homeId } from '../../src/platform/home.ts';
import {
  LEASE_FILE, LEGACY_LOCK_FILE, leaseForVerb, leaseHolder, leaseNamed, tryLease, VERB_WAIT_MS,
} from '../../src/platform/lease.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * 🔴 HOMELEASE1 (the terminal RPC design §5): one writer per home. The CLI's copy of the lease's twin; the driver's
 * `HomeLeaseTests` hold the same rows in C#, and the file is the contract between them (`twins.md`). This CLI cannot read
 * another process's start, so a holder lives to it while its process id does: the rows hand a judge in.
 */

/** A lease as another holder writes it, the driver's spelling: `started` in UTC ticks. */
function plant(home: string, kind: string, pid = process.pid, what?: string, file = LEASE_FILE): string {
  const text = JSON.stringify({ kind, pid, started: 639000000000000000, since: '2026-10-16T08:02:13Z', ...(what ? { what } : {}) });
  writeFileSync(join(home, file), text);
  return text;
}

const alive = () => true;
const gone = () => false;

test('a free home is taken, named after its holder and the home’s id, and let go on release', () => {
  const fx = makeFixture('lease-free');
  try {
    const home = join(fx.root, 'data');
    const taken = tryLease(home, 'verb', 'daoris driver cap', alive);
    assert.ok('lease' in taken);
    assert.equal(taken.lease.holds, true);

    const written = JSON.parse(readFileSync(homeEntry(home, 'home-lease'), 'utf8')) as Record<string, unknown>;
    assert.equal(written.kind, 'verb');
    assert.equal(written.pid, process.pid);
    assert.equal(written.what, 'daoris driver cap');
    assert.equal(written.home, homeId(home));
    assert.ok(typeof written.started === 'number' && written.started > 630_000_000_000_000_000, 'started is in UTC ticks, as the driver writes it');
    assert.deepEqual(leaseHolder(home, alive), { kind: 'verb', pid: process.pid, since: written.since, what: 'daoris driver cap' });

    taken.lease.release();
    assert.equal(existsSync(homeEntry(home, 'home-lease')), false);
    assert.equal(leaseHolder(home, alive), null);
  } finally {
    fx.cleanup();
  }
});

test('of two takers at once one holds it, and the other is told who', () => {
  const fx = makeFixture('lease-two');
  try {
    const first = tryLease(fx.root, 'desktop', null, alive);
    const second = tryLease(fx.root, 'verb', 'daoris plugin add', alive);
    assert.ok('lease' in first);
    assert.ok('holder' in second && second.holder.kind === 'desktop' && second.holder.pid === process.pid);
    first.lease.release();
  } finally {
    fx.cleanup();
  }
});

test('a dead holder never blocks: its process gone, or a lease that does not read', () => {
  const fx = makeFixture('lease-dead');
  try {
    plant(fx.root, 'verb', 4242, 'daoris-driver trees land');
    assert.equal(leaseHolder(fx.root, gone), null);
    const replaced = tryLease(fx.root, 'desktop', null, gone);
    assert.ok('lease' in replaced);
    replaced.lease.release();

    writeFileSync(join(fx.root, LEASE_FILE), '{ "kind": "verb", "pid": ');
    const torn = tryLease(fx.root, 'verb', 'daoris tool use', alive);
    assert.ok('lease' in torn);
    torn.lease.release();
  } finally {
    fx.cleanup();
  }
});

test('another path to the folder meets the same lease', () => {
  const fx = makeFixture('lease-alias');
  try {
    const home = join(fx.root, 'data');
    const held = tryLease(home, 'verb', 'daoris-driver sessions archive', alive);
    assert.ok('lease' in held);
    fx.write(join('data', 'sub', '.keep'), '');
    const aliases = [join(home, 'sub', '..'), ...(process.platform === 'win32' ? [home.toUpperCase()] : [])];
    for (const alias of aliases) {
      const met = tryLease(alias, 'desktop', null, alive);
      assert.ok('holder' in met && met.holder.what === 'daoris-driver sessions archive', alias);
    }
    held.lease.release();
  } finally {
    fx.cleanup();
  }
});

test('a verb waits for another verb’s hold, then says which verb holds it', async () => {
  const fx = makeFixture('lease-wait');
  try {
    plant(fx.root, 'verb', process.pid, 'daoris-driver trees sync');
    let now = 0;
    let slept = 0;
    const waited = await leaseForVerb(fx.root, 'daoris driver cap', {
      lives: alive, now: () => now, sleep: async (ms) => { slept += ms; now += ms; },
    });
    assert.ok('refusal' in waited);
    assert.ok(slept >= VERB_WAIT_MS, `waited ${slept}`);
    assert.ok(waited.refusal.startsWith(`${fx.root} is held by a terminal's \`daoris-driver trees sync\` (pid ${process.pid}, since `), waited.refusal);
    assert.ok(waited.refusal.includes('for the 30 seconds this waited: nothing was done'), waited.refusal);

    // The other verb ends inside the wait: this one takes the home the moment it is free.
    let looks = 0;
    const taken = await leaseForVerb(fx.root, 'daoris driver cap', {
      lives: alive, sleep: async () => { if (++looks === 3) rmSync(join(fx.root, LEASE_FILE)); },
    });
    assert.ok('lease' in taken && taken.lease.holds);
    taken.lease.release();
  } finally {
    fx.cleanup();
  }
});

test('a verb finding an instance acts beside it, holding nothing, and leaves its lease', async () => {
  const fx = makeFixture('lease-beside');
  try {
    const planted = plant(fx.root, 'desktop');
    const beside = await leaseForVerb(fx.root, 'daoris driver cap', { lives: alive, sleep: async () => { throw new Error('waited'); } });
    assert.ok('lease' in beside && !beside.lease.holds && beside.lease.beside?.kind === 'desktop');
    beside.lease.release();
    assert.equal(readFileSync(join(fx.root, LEASE_FILE), 'utf8'), planted);
  } finally {
    fx.cleanup();
  }
});

test('an older build’s driver.lock holds the home while its holder lives', () => {
  const fx = makeFixture('lease-legacy');
  try {
    plant(fx.root, 'headless', process.pid, undefined, LEGACY_LOCK_FILE);
    const met = tryLease(fx.root, 'verb', 'daoris driver cap', alive);
    assert.ok('holder' in met && met.holder.kind === 'headless');
    assert.equal(leaseNamed(met.holder), `a headless \`daoris-driver\` (pid ${process.pid}, since 2026-10-16T08:02:13Z)`);

    const stale = tryLease(fx.root, 'verb', 'daoris driver cap', gone);
    assert.ok('lease' in stale);
    stale.lease.release();
    assert.ok(existsSync(join(fx.root, LEGACY_LOCK_FILE)), 'a stale one is left where it is');
  } finally {
    fx.cleanup();
  }
});

test('a release removes only the lease it wrote', () => {
  const fx = makeFixture('lease-release');
  try {
    const taken = tryLease(fx.root, 'verb', 'daoris plugin remove', alive);
    assert.ok('lease' in taken);
    const planted = plant(fx.root, 'desktop');
    taken.lease.release();
    assert.equal(readFileSync(join(fx.root, LEASE_FILE), 'utf8'), planted);
  } finally {
    fx.cleanup();
  }
});

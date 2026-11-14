/**
 * A start of the desktop waits for an update's swap, bounded and said:
 *
 *   node --test tools/desktop-swap-wait.test.mjs
 *
 * Fixture install folders under the repository's `_fixtures/`, never a real install; the process query is handed in.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { awaitSwap, swapUnderWay } from './desktop/swap-wait.mjs';

const fixtures = join(fileURLToPath(new URL('..', import.meta.url)), '_fixtures', 'desktop-swap-wait');

function install(name, phase) {
  const folder = join(fixtures, `${name}-${process.pid}`);
  rmSync(folder, { recursive: true, force: true });
  mkdirSync(join(folder, 'update'), { recursive: true });
  if (phase) writeFileSync(join(folder, 'update', 'swap.json'), JSON.stringify({ phase }));
  return folder;
}

const nothing = () => '';
const clock = () => {
  let at = 0;
  return { now: () => at, sleep: async (ms) => { at += ms; } };
};

test('a swap under way in the journal is waited for, and the wait ends when the journal says installed', async () => {
  const folder = install('journal', 'swapping');
  try {
    const time = clock();
    const said = [];
    let looks = 0;
    const sleep = async (ms) => {
      looks += 1;
      if (looks === 3) writeFileSync(join(folder, 'update', 'swap.json'), JSON.stringify({ phase: 'installed' }));
      await time.sleep(ms);
    };
    assert.equal(await awaitSwap(folder, { now: time.now, sleep, lines: nothing, say: (line) => said.push(line), within: 60_000, every: 1000 }), true);
    assert.equal(looks, 3);
    assert.equal(said.length, 2);
    assert.match(said[0], /swapping.*waiting up to 60 s/);
    assert.match(said[1], /finished/);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('a launcher waiting for the application to close counts before it has written a journal', () => {
  const folder = install('launcher', null);
  try {
    const waiting = () => `4242|"${join(folder, 'Daoris.exe')}" --update --after 99`;
    assert.match(swapUnderWay(folder, { lines: waiting }), /launcher is waiting/);
    assert.equal(swapUnderWay(folder, { lines: () => `4242|"${join(folder, 'Daoris.exe')}"` }), null);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('a finished, refused or absent journal waits for nothing', async () => {
  for (const phase of ['installed', 'rolled-back', 'refused', null]) {
    const folder = install(`done-${phase}`, phase);
    try {
      assert.equal(await awaitSwap(folder, { ...clock(), lines: nothing, say: () => assert.fail('said') }), false);
    } finally {
      rmSync(folder, { recursive: true, force: true });
    }
  }
});

test('a swap that does not end is said once the bound is spent, and nothing is started', async () => {
  const folder = install('stuck', 'started');
  try {
    await assert.rejects(
      awaitSwap(folder, { ...clock(), lines: nothing, say: () => {}, within: 5000, every: 1000 }),
      /still under way after 5 s \(its swap is started\); nothing was started/);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { commandLinesAt } from '../processes.mjs';
import { LAUNCHER } from '../desktop-publish.mjs';

/** The journal's phases that mean an update's swap is under way: `swapping`, `started`, and `confirmed` until it is marked installed. */
export const UNDER_WAY = ['swapping', 'started', 'confirmed'];

/**
 * How long a start waits for an update's swap: past the launcher's three minutes for the application to close and its own
 * two minutes for a held move, so a swap that is only slow is waited out and one that is stuck is said.
 */
export const SWAP_WAIT_MS = 6 * 60 * 1000;

/**
 * What an install's update is doing now, from the places the launcher leaves: the journal `update/swap.json`, and a launcher
 * started with `--update`, which is waiting for the application to close before it has written anything. Null when none is.
 *
 * @param {string} install the folder holding the launcher, `app/` and `data/`
 * @param {{ lines?: (exe: string) => string }} [reads] the process query, handed in by a test
 */
export function swapUnderWay(install, { lines = commandLinesAt } = {}) {
  const journal = join(install, 'update', 'swap.json');
  if (existsSync(journal)) {
    try {
      const phase = JSON.parse(readFileSync(journal, 'utf8'))?.phase;
      if (UNDER_WAY.includes(phase)) return `its swap is ${phase}`;
    } catch {
      // A journal half written is read again at the next look.
    }
  }

  const waiting = String(lines(join(install, LAUNCHER)) ?? '')
    .split(/\r?\n/)
    .some((row) => /\|.*(?:^|\s)--update(?:\s|$)/.test(row));
  return waiting ? 'its launcher is waiting for the application to close' : null;
}

/**
 * Waits, bounded and said, while an install's update is under way, so a start never runs beside the swap: the swap refuses
 * while anything runs from `app/`, and a start in its middle is what it found. Returns once none is; throws once the bound is spent.
 *
 * @param {string} install
 * @param {{ say?: (line: string) => void, within?: number, every?: number, sleep?: (ms: number) => Promise<void>, now?: () => number, lines?: (exe: string) => string }} [options]
 * @returns {Promise<boolean>} whether it waited
 */
export async function awaitSwap(install, options = {}) {
  const {
    say = console.log, within = SWAP_WAIT_MS, every = 1000, now = Date.now, lines = commandLinesAt,
    sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
  } = options;
  let why = swapUnderWay(install, { lines });
  if (!why) return false;

  const until = now() + within;
  say(`an update is being installed (${why}); waiting up to ${Math.round(within / 1000)} s for it before starting.`);
  while (why) {
    if (now() >= until) {
      throw new Error(`the update is still under way after ${Math.round(within / 1000)} s (${why}); `
        + 'nothing was started. Look at update/swap.json in the install, then run this again.');
    }

    await sleep(every);
    why = swapUnderWay(install, { lines });
  }

  say('the update has finished; starting.');
  return true;
}

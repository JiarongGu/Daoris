/**
 * One lock per checkout for any run that touches an open merge, and for Daoris's landing's move of the line (, verb ):
 * a second merge run is refused naming who holds it, a run that finds a landing waits for it. The lock is put in place complete, and let go
 * only while it still says what this run wrote.
 */
import { linkSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

export type Holder = { pid: number; started: number | null; at: string; verb: string };
export const CHECKOUT_LOCK = 'local/merge-run.lock';

const read = (file: string): Holder | null => {
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8')) as Holder;
    return Number.isFinite(parsed?.pid) ? parsed : null;
  } catch {
    return null;
  }
};

const put = (file: string, holder: Holder): boolean => {
  const beside = `${file}.${holder.pid}.tmp`;
  try {
    writeFileSync(beside, JSON.stringify(holder));
    linkSync(beside, file);
    return true;
  } catch {
    return false;
  } finally {
    rmSync(beside, { force: true });
  }
};

/** Take the lock, or answer who holds it. `runs` says whether a holder is still the run it names (pid alive and started when it says). */
export function takeCheckout(file: string, me: Holder, { runs }: { runs: (holder: Holder) => boolean }): { taken: true } | { taken: false; holder: Holder | null } {
  mkdirSync(dirname(file), { recursive: true });
  for (let attempt = 0; attempt < 3; attempt += 1) {
    if (put(file, me)) return { taken: true };
    const held = read(file);
    if (held !== null && runs(held)) return { taken: false, holder: held };
    if (JSON.stringify(read(file)) === JSON.stringify(held)) rmSync(file, { force: true });
  }
  return { taken: false, holder: read(file) };
}

export function releaseCheckout(file: string, me: Holder): void {
  const held = read(file);
  if (held && held.pid === me.pid && held.at === me.at) rmSync(file, { force: true });
}

/** A landing's verb in the lock: its move ends in seconds, so a run waits for it, within LANDING_WAIT_MS. */
export const LANDING_VERB = 'landing';
export const LANDING_WAIT_MS = 5 * 60_000;
export type Waiting = { ms: number; pollMs: number; sleep: (ms: number) => Promise<void>; now: () => number; say: (line: string) => void };

const lockedOut = (holder: Holder | null, ms: number): string => {
  const who = holder ? holderLine(holder) : 'a holder that does not read';
  return holder?.verb === LANDING_VERB
    ? `a landing holds this checkout (${who}) and did not end within ${Math.round(ms / 1000)} s: the line is not moved under a merge, so wait for it to end`
    : `another merge-branch run holds this checkout (${who}): wait for it to end; two runs on one open merge collide`;
};

/** `run` held under the lock, a landing in flight waited for within `waiting.ms` and said once; the lock is let go however `run` ends. */
export async function holdingCheckout<T>(
  file: string, me: Holder, runs: (holder: Holder) => boolean, run: () => Promise<T>, refuse: (message: string) => Error,
  waiting: Partial<Waiting> = {},
): Promise<T> {
  const wait: Waiting = { ms: LANDING_WAIT_MS, pollMs: 500, sleep: (ms) => new Promise((done) => { setTimeout(done, ms); }), now: Date.now, say: (line) => console.log(`merge-branch: ${line}`), ...waiting };
  const until = wait.now() + wait.ms;
  let said = false;
  let took = takeCheckout(file, me, { runs });
  while (!took.taken && took.holder?.verb === LANDING_VERB && wait.now() < until) {
    if (!said) {
      said = true;
      wait.say(`a landing is moving the line (pid ${took.holder.pid}, started ${took.holder.at}): waiting for it to end`);
    }
    await wait.sleep(wait.pollMs);
    took = takeCheckout(file, me, { runs });
  }
  if (!took.taken) throw refuse(lockedOut(took.holder, wait.ms));
  try {
    return await run();
  } finally {
    releaseCheckout(file, me);
  }
}

export const holderLine =(holder: Holder): string => `pid ${holder.pid}, started ${holder.at}, running a ${holder.verb}`;

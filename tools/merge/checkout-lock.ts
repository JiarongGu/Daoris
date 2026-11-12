/**
 * One lock per checkout for any run that touches an open merge: a second run is refused naming who holds it, never let to collide
 * (two runs renamed each other's gate logs). The lock is put in place complete, and let go only while it still says what this run wrote.
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

/** `run` held under the checkout's lock; a second run is refused, naming the holder, and the lock is let go however `run` ends. */
export async function holdingCheckout<T>(
  file: string, me: Holder, runs: (holder: Holder) => boolean, run: () => Promise<T>, refuse: (message: string) => Error,
): Promise<T> {
  const took = takeCheckout(file, me, { runs });
  if (!took.taken) {
    const who = took.holder ? holderLine(took.holder) : 'a holder that does not read';
    throw refuse(`another merge-branch run holds this checkout (${who}): wait for it to end; two runs on one open merge collide`);
  }
  try {
    return await run();
  } finally {
    releaseCheckout(file, me);
  }
}

export const holderLine =(holder: Holder): string => `pid ${holder.pid}, started ${holder.at}, running a ${holder.verb}`;

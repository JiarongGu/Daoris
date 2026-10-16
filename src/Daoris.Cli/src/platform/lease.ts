import { linkSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { randomBytes } from 'node:crypto';
import { ensureHomeId, homeEntry } from './home.ts';

/**
 * One writer per home (HOMELEASE1, the terminal RPC design §5): `<home>/home.lease`, the driver's lock (DRV8a's
 * `driver.lock`) grown to cover every writer. An instance (the desktop's loop, a headless `drive`) holds it for its life; a
 * verb acting alone, the desktop's first-start migration (D63) and the launcher's swap of `app/` (D139 §5) for their act. It
 * is a file in the folder, so every path to the folder takes the same lease, and its holder records the home's id.
 *
 * The twin of the driver's `HomeLease` (C#): the file is the contract, and the two share no code (`twins.md`). Taken as the
 * driver's is: written beside, then put in place with no overwrite (a hard link, which refuses where a lease is), released only
 * while it still says what its holder wrote, and a holder whose process is gone replaced. This CLI cannot read another
 * process's start, so it judges a holder by its process id alone, where the driver also compares its start: a process id the
 * system gave to another process since holds the lease to this CLI, which then waits and says so rather than take it.
 *
 * An older build's `driver.lock` is read as its lease while its holder lives, so a desktop from before HOMELEASE1 is still a
 * holder to this one.
 *
 * The twin's half for this CLI's own verbs: `daoris driver …`, `agent`, `plugin` and `tool` edit files under the home, and
 * RPC1f sends them over the running instance's pipe while one runs and takes this lease (`leaseForVerb`) when none does, the
 * design's §9. Until then they act as they always did.
 */

/** The lease's name: the home's `home-lease` entry, which `homeEntry` places (D172, HOMELAYOUT2). */
export const LEASE_FILE = 'home.lease';

/** DRV8a's lock, which a build from before HOMELEASE1 still takes: read as that build's lease while its holder lives. */
export const LEGACY_LOCK_FILE = 'driver.lock';

/** Who holds a home: an instance for its life, or a verb, a migration or a swap for its act. */
export type LeaseKind = 'desktop' | 'headless' | 'verb' | 'migration' | 'swap';

const KINDS: readonly LeaseKind[] = ['desktop', 'headless', 'verb', 'migration', 'swap'];

/** How long a verb waits on another verb's, a migration's or a swap's lease before it says which holds it. */
export const VERB_WAIT_MS = 30_000;

export interface LeaseHolder {
  kind: LeaseKind;
  pid: number;
  since: string;
  /** What a verb is doing (its verb's words, never its arguments), or null. */
  what: string | null;
}

export interface Lease {
  /** Whether this process holds the home's lease. */
  readonly holds: boolean;
  /** The instance it acts beside, holding nothing (until RPC1f's pipe makes a verb that instance's client), or null. */
  readonly beside: LeaseHolder | null;
  /** Let the home go, only while the lease still says what this one wrote. */
  release(): void;
}

/** Whether a holder is an instance, which holds the home for its life. */
export function isInstance(holder: LeaseHolder): boolean {
  return holder.kind === 'desktop' || holder.kind === 'headless';
}

/** The holder as a sentence names it: what it is, its process, and since when. */
export function leaseNamed(holder: LeaseHolder): string {
  const who = holder.kind === 'desktop' ? 'the desktop'
    : holder.kind === 'headless' ? 'a headless `daoris-driver`'
      : holder.kind === 'migration' ? "the desktop's first-start move of an older home"
        : holder.kind === 'swap' ? "the launcher's update of the application"
          : holder.what ? `a terminal's \`${holder.what}\`` : 'a terminal\'s verb';
  return `${who} (pid ${holder.pid}, since ${holder.since})`;
}

/** What a verb says when another act held the home for the whole of its wait. */
export function leaseRefusal(home: string, holder: LeaseHolder): string {
  return `${home} is held by ${leaseNamed(holder)}, and was for the ${VERB_WAIT_MS / 1000} seconds this waited: nothing was `
    + 'done. Run this again once that one ends.';
}

type Lives = (pid: number) => boolean;

/** Whether a process runs with this id: signal 0 tests and sends nothing; one not ours to signal still runs. */
export function processLives(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === 'EPERM';
  }
}

/** The live holder of `home`'s lease, or null when none holds it. */
export function leaseHolder(home: string, lives: Lives = processLives): LeaseHolder | null {
  for (const name of ['home-lease', 'driver-lock'] as const) {
    const seen = readText(homeEntry(home, name));
    const named = seen === null ? null : parse(seen);
    if (named && lives(named.pid)) return named;
  }
  return null;
}

/** Take `home`'s lease, or the live holder that has it. */
export function tryLease(
  home: string, kind: LeaseKind, what: string | null = null, lives: Lives = processLives,
): { lease: Lease } | { holder: LeaseHolder } {
  mkdirSync(home, { recursive: true });
  const path = homeEntry(home, 'home-lease');
  const legacy = leaseHolderIn(homeEntry(home, 'driver-lock'), lives);
  if (legacy) return { holder: legacy };

  for (let attempt = 0; attempt < 8; attempt++) {
    const mine = describe(kind, what, home);
    if (create(path, mine)) return { lease: held(path, mine) };

    const seen = readText(path);
    if (seen === null) continue;
    const named = parse(seen);
    if (named && lives(named.pid)) return { holder: named };

    // Stale: removed only while it still says what was judged stale, so a lease another has just taken is not the one removed.
    if (readText(path) === seen) remove(path);
  }

  throw new Error(`the home's lease (${path}) could not be taken: it changed under every attempt. Try again.`);
}

/**
 * The lease for a verb acting alone (§5, §7.2): taken where the home is free; beside an instance, holding nothing, as a verb
 * acts today until RPC1f's pipe makes it that instance's client; and where another verb, a migration or a swap holds it,
 * waited for up to thirty seconds, then refused naming which holds it.
 */
export async function leaseForVerb(
  home: string, what: string,
  { within = VERB_WAIT_MS, poll = 250, lives = processLives, sleep = (ms: number) => new Promise<void>((done) => setTimeout(done, ms)), now = Date.now }:
  { within?: number; poll?: number; lives?: Lives; sleep?: (ms: number) => Promise<void>; now?: () => number } = {},
): Promise<{ lease: Lease } | { refusal: string }> {
  const until = now() + within;
  for (;;) {
    const taken = tryLease(home, 'verb', what, lives);
    if ('lease' in taken) return taken;
    if (isInstance(taken.holder)) return { lease: { holds: false, beside: taken.holder, release() {} } };
    if (now() >= until) return { refusal: leaseRefusal(home, taken.holder) };
    await sleep(poll);
  }
}

function leaseHolderIn(path: string, lives: Lives): LeaseHolder | null {
  const seen = readText(path);
  const named = seen === null ? null : parse(seen);
  return named && lives(named.pid) ? named : null;
}

function held(path: string, written: string): Lease {
  let released = false;
  return {
    holds: true,
    beside: null,
    release() {
      if (released) return;
      released = true;
      if (readText(path) === written) remove(path);
    },
  };
}

/** This process, as its lease names it: `started` in UTC ticks, as the driver writes its own, so the C# twin can judge it. */
function describe(kind: LeaseKind, what: string | null, home: string): string {
  const started = Math.round(performance.timeOrigin * 10_000) + 621_355_968_000_000_000;
  const id = ensureHomeId(home);
  return `${JSON.stringify({
    kind, pid: process.pid, started, since: new Date().toISOString(), ...(what ? { what } : {}), ...(id ? { home: id } : {}),
  }, null, 2)}\n`;
}

/** Written beside, then linked into place only where nothing is: never over another lease. */
function create(path: string, text: string): boolean {
  const beside = `${path}.${randomBytes(8).toString('hex')}.writing`;
  try {
    writeFileSync(beside, text, 'utf8');
    linkSync(beside, path);
    return true;
  } catch {
    return false;
  } finally {
    remove(beside);
  }
}

function readText(path: string): string | null {
  try {
    return readFileSync(path, 'utf8');
  } catch {
    return null;
  }
}

function remove(path: string): void {
  try {
    unlinkSync(path);
  } catch {
    // Gone already, or held this moment: the next reader judges what is left by its process.
  }
}

/** The holder a lease names, or null for one that does not read. */
function parse(text: string): LeaseHolder | null {
  try {
    const value: unknown = JSON.parse(text);
    if (typeof value !== 'object' || value === null) return null;
    const { kind, pid, started, since, what } = value as Record<string, unknown>;
    if (!KINDS.includes(kind as LeaseKind) || !Number.isInteger(pid) || typeof started !== 'number') return null;
    return {
      kind: kind as LeaseKind,
      pid: pid as number,
      since: typeof since === 'string' ? since : new Date((started - 621_355_968_000_000_000) / 10_000).toISOString(),
      what: typeof what === 'string' && what ? what : null,
    };
  } catch {
    return null;
  }
}

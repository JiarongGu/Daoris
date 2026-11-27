/**
 * The heavy stack: which heavy tier (`tools/heavy-run.mjs`) holds the heavy tree, and what an older one handed a newer one. A landing
 * starts a heavy tier on main's new tip; landings come faster than a tier's git half ends, and tiers run side by side filled the
 * machine's heavy places and fought over the one heavy tree. So a newer tier supersedes the one before it:
 *
 * - The first tier holds the tree. A tier that lands while one holds it waits, and asks the holder to stop (`halt`, which the
 *   holder's `reached-checks.mjs` reads through `--halt-file`): it starts no class more, ends the class group in flight where the
 *   newer commit changes what its classes read (finishes it otherwise, so its passes stand there), and hands over.
 * - What it hands is its range (the base it ran from) with the classes it left unrun, named for the records. The newer tier runs
 *   from the oldest base handed to it, on its own commit: the classes that range reaches are the union of the older tier's and
 *   its own, and each the older tier passed on inputs the newer commit keeps is replayed from the store (`class-reuse.ts`), so
 *   every class runs once, on the newest commit.
 * - A tier still waiting when a third lands is superseded at once: its range is carried by the third. A holder or waiter whose
 *   process ended, or whose beat is older than its staleness, counts no more: its range is carried too, never lost.
 *
 * One file, `local/heavy-stack.json` in the main checkout, changed under its keeping lock (`keeplock.ts`); the transitions here are
 * pure, over a `live` seam, so a test drives two tiers in one process.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { withKeepLock } from '../../src/Daoris.Cli/src/platform/keeplock.ts';
import { writeAtomic } from '../fsx.mjs';
import type { Range } from './heavy-batch.ts';

export const HEAVY_STACK = 'local/heavy-stack.json';
/** Where a holder's halt is written, one file a tier, read by its reached run (`heavy-halt.ts`). */
export const HEAVY_HALTS = 'local/heavy-halts';
/** How often a waiting tier looks again, how often a tier beats, and how old a beat may be before its tier counts no more. */
export const STACK_POLL_MS = 2_000;
export const STACK_BEAT_MS = 20_000;
export const STACK_STALE_MS = 120_000;
/** How long a tier waits for the tree, or for the machine's admission, before it says so, and how long before it gives up the tree. */
export const WAIT_BOUND_MS = 10 * 60_000;
export const WAIT_DEADLINE_MS = 3 * 60 * 60_000;

/** A tier as the stack keeps it: the landing's run, its process, and (on a holder) the newer tier that asked it to stop. */
export type Tier = { id: string; pid: number; commit: string; base: string; branches: string[]; since: string; beat: string; supersededBy?: string; ranges?: Range[] };
/** A tier whose range is carried: the base it ran from, the classes it left unrun (null: it ran none) and the landings' changes it ran over, none for a run by hand. */
export type Carried = { id: string; commit: string; base: string; branches: string[]; since: string; unrun: string[] | null; ranges?: Range[] };
/** What waits for the next holder: the oldest base carried, and each tier carried, oldest first. */
export type Handed = { base: string; from: Carried[] };
export type Stack = { holder: Tier | null; waiting: Tier | null; handed: Handed | null };

export const haltFileOf = (home: string, id: string): string => join(home, HEAVY_HALTS, `${id.replace(/[^\w.-]+/g, '-')}.json`);

const carriedOf = (tier: Tier, unrun: string[] | null, base = tier.base): Carried => ({
  id: tier.id, commit: tier.commit, base, branches: tier.branches, since: tier.since, unrun, ...(tier.ranges ? { ranges: tier.ranges } : {}),
});

/** `handed` with `entries` carried too: oldest first by when each tier landed, the range from the oldest's base. */
export function carry(handed: Handed | null, ...entries: Carried[]): Handed {
  const byId = new Map([...(handed?.from ?? []), ...entries].map((each) => [each.id, each]));
  const from = [...byId.values()].sort((a, b) => a.since.localeCompare(b.since) || a.id.localeCompare(b.id));
  return { base: (from[0] as Carried).base, from };
}

/** The stack with each tier that counts no more taken out, its range carried. */
function pruned(stack: Stack, live: (tier: Tier) => boolean): { stack: Stack; dead: Tier[] } {
  const dead = [stack.holder, stack.waiting].filter((tier): tier is Tier => tier !== null && !live(tier));
  if (!dead.length) return { stack, dead };
  const gone = new Set(dead.map((tier) => tier.id));
  return {
    stack: {
      holder: stack.holder && gone.has(stack.holder.id) ? null : stack.holder,
      waiting: stack.waiting && gone.has(stack.waiting.id) ? null : stack.waiting,
      handed: carry(stack.handed, ...dead.map((tier) => carriedOf(tier, null))),
    },
    dead,
  };
}

/** The stack with `me` holding the tree, taking what the tiers before it handed. */
const take = (stack: Stack, me: Tier): { stack: Stack; took: Handed | null } => ({
  stack: { holder: { ...me }, waiting: stack.waiting?.id === me.id ? null : stack.waiting, handed: null }, took: stack.handed,
});

export type Arrival = { stack: Stack; role: 'hold' | 'wait'; took: Handed | null; halt: Tier | null; superseded: Tier | null; dead: Tier[] };

/** A tier that lands: it holds a free tree, or waits as the newest, superseding a waiting one and asking the holder to stop. */
export function arrive(before: Stack, me: Tier, live: (tier: Tier) => boolean): Arrival {
  const { stack, dead } = pruned(before, live);
  if (!stack.holder) return { ...take(stack, me), role: 'hold', halt: null, superseded: null, dead };
  const superseded = stack.waiting && stack.waiting.id !== me.id ? stack.waiting : null;
  const holder = { ...stack.holder, supersededBy: me.id };
  return {
    stack: { holder, waiting: { ...me }, handed: superseded ? carry(stack.handed, carriedOf(superseded, null)) : stack.handed },
    role: 'wait', took: null, halt: holder, superseded, dead,
  };
}

export type Look = { stack: Stack; role: 'hold' | 'wait' | 'superseded'; took: Handed | null; by: Tier | null; dead: Tier[] };

/** A waiting tier's look, its beat renewed: superseded where a newer one waits in its stead, holding where the tree is free. */
export function look(before: Stack, me: Tier, live: (tier: Tier) => boolean, now: string): Look {
  if (before.waiting?.id !== me.id) return { stack: before, role: 'superseded', took: null, by: before.waiting, dead: [] };
  const { stack, dead } = pruned({ ...before, waiting: { ...before.waiting, beat: now } }, (tier) => tier.id === me.id || live(tier));
  if (stack.holder) return { stack, role: 'wait', took: null, by: null, dead };
  return { ...take(stack, { ...me, beat: now }), role: 'hold', by: null, dead };
}

/** The tier's beat renewed, holding or waiting. */
export const beat = (stack: Stack, me: Tier, now: string): Stack => ({
  ...stack,
  holder: stack.holder?.id === me.id ? { ...stack.holder, beat: now } : stack.holder,
  waiting: stack.waiting?.id === me.id ? { ...stack.waiting, beat: now } : stack.waiting,
});

/**
 * The holder stops and hands over: its range from `from` (the base it ran from) with `unrun` (null: it ran no class), and every tier
 * it carried itself, wait for the newest waiting tier, `to`.
 */
export function handOver(stack: Stack, me: Tier, { unrun, from, took }: { unrun: string[] | null; from: string; took: Handed | null }): { stack: Stack; to: Tier | null } {
  const handed = carry(stack.handed, ...(took?.from ?? []), carriedOf(me, unrun, from));
  return { stack: { holder: stack.holder?.id === me.id ? null : stack.holder, waiting: stack.waiting, handed }, to: stack.waiting };
}

/** The holder ends having run every class it planned: the tree is free and nothing is carried. */
export const release = (stack: Stack, me: Tier): Stack => ({ ...stack, holder: stack.holder?.id === me.id ? null : stack.holder });

/** A waiting tier that gave up at its deadline: its range is carried by the next tier to hold. */
export const giveUp = (stack: Stack, me: Tier): Stack => (stack.waiting?.id === me.id
  ? { ...stack, waiting: null, handed: carry(stack.handed, carriedOf(stack.waiting, null)) }
  : stack);

const EMPTY: Stack = { holder: null, waiting: null, handed: null };

/** The stack as the file holds it; a missing or unreadable file is an empty stack. */
export function readStack(file: string): Stack {
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8')) as Partial<Stack>;
    const tier = (value: unknown): Tier | null => {
      const each = value as Tier | null;
      return each && typeof each.id === 'string' && Number.isInteger(each.pid) && typeof each.commit === 'string' && typeof each.base === 'string' && typeof each.beat === 'string' ? each : null;
    };
    const handed = parsed.handed && typeof parsed.handed.base === 'string' && Array.isArray(parsed.handed.from) ? parsed.handed : null;
    return { holder: tier(parsed.holder), waiting: tier(parsed.waiting), handed };
  } catch {
    return EMPTY;
  }
}

/** One change of the stack file under its lock: `fn` answers the stack to write and what its caller reads. */
export function changeStack<R extends { stack: Stack }>(file: string, fn: (stack: Stack) => R): R {
  return withKeepLock(file, () => {
    const result = fn(readStack(file));
    writeAtomic(file, `${JSON.stringify({ schema: 1, ...result.stack }, null, 2)}\n`);
    return result;
  });
}

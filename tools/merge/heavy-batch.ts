/**
 * What a landing owes the heavy tier, judged by the change it landed, not by the run's kind: a landing whose change bears a full run's
 * reason (`fullReason`) joins one pending batch, whose tier starts once no landing has come for `HEAVY_QUIET_MS` and runs only the reasoned
 * landings' own changes; one with none queues nothing. The batch is `local/heavy-pending.json`, changed under its keeping lock; the
 * transitions are pure and `tools/heavy-run.mjs` wires them.
 */
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { withKeepLock } from '../../src/Daoris.Cli/src/platform/keeplock.ts';
import { writeAtomic } from '../fsx.mjs';
import { fullReason, type FullReason, type Reach, type Verification } from './verification.ts';

export const HEAVY_PENDING = 'local/heavy-pending.json';
/** How long no landing may have come before the batch's tier starts. */
export const HEAVY_QUIET_MS = 20 * 60_000;
/** How often a waiting tier looks again. */
/** The longest a reasoned landing's tier waits, from the oldest landing in its batch, however steadily landings keep coming. */
export const HEAVY_MAX_WAIT_MS = 2 * 60 * 60_000;
export const HEAVY_QUIET_POLL_MS = 30_000;

/** One landing's change: the main tip before it and the commit it landed. */
export type Range = { base: string; commit: string };
export type Carried = { id: string; commit: string; branches: string[] };
/** The batch waiting: the newest reasoned landing's run, the oldest base, every branch, the ranges, the tiers it carries, when a landing last came. */
export type Pending = { id: string; commit: string; base: string; branches: string[]; first: string; last: string; ranges: Range[]; carried: Carried[] };
export type Landing = { id: string; commit: string; base: string; branches: string[]; at: string };

/** Whether a landing queues a heavy tier, and why, as the line says it. */
export type Judged = { run: true; reason: FullReason | null; said: string } | { run: false; said: string };

/**
 * A landing judged by the paths it changed. A wide path decides without measuring; the reach (`reachOf`, the census's share) is read only
 * where none does. A repository with no rule (`rule` null) keeps the tier after every landing, as before.
 */
export function judgeLanding(rule: Verification | null, paths: readonly string[], reachOf: () => Reach | undefined): Judged {
  if (!rule) return { run: true, reason: null, said: 'the repository sets no verification rule, so its heavy tier follows every landing' };
  const wide = fullReason(rule, paths);
  if (wide) return { run: true, reason: wide, said: wide.said };
  const reach = reachOf();
  const reason = fullReason(rule, paths, reach);
  if (reason) return { run: true, reason, said: reason.said };
  const reached = reach ? `its ${paths.length} path${paths.length === 1 ? '' : 's'} reach ${reach.reached} of ${reach.of} ${reach.what}, under the declared ${Math.round(rule.reachShare * 100)}%` : `its ${paths.length} path${paths.length === 1 ? '' : 's'} reach could not be measured`;
  return { run: false, said: `the landing bears no reason for a heavy run (no declared wide path; ${reached}): no heavy tier is queued for it (D349)` };
}

/** `landing` joins the batch: it becomes the one that runs, with the older ranges and tiers carried, and the older tier it replaces is named. */
export function enlist(before: Pending | null, landing: Landing): { pending: Pending; superseded: Carried | null } {
  const own: Range = { base: landing.base, commit: landing.commit };
  const superseded = before && before.id !== landing.id ? { id: before.id, commit: before.commit, branches: before.branches } : null;
  const branches = [...new Set([...(before?.branches ?? []), ...landing.branches])];
  return {
    pending: {
      id: landing.id, commit: landing.commit, base: before?.base ?? landing.base, branches,
      first: before?.first ?? landing.at,
      last: before && Date.parse(before.last) > Date.parse(landing.at) ? before.last : landing.at,
      ranges: [...(before?.ranges ?? []).filter((each) => each.commit !== own.commit), own],
      carried: [...(before?.carried ?? []), ...(superseded ? [superseded] : [])],
    },
    superseded,
  };
}

/** A landing that bears no reason moves the pause, and adds nothing to the batch; none where nothing waits. */
export function bump(before: Pending | null, at: string): Pending | null {
  if (!before) return null;
  return Date.parse(at) > Date.parse(before.last) ? { ...before, last: at } : before;
}

export type Look = {
  pending: Pending | null;
  /** `wait` while landings may still come; `start` when the batch's tier may; `superseded` when a newer landing carries it; `gone` where nothing waits. */
  role: 'wait' | 'start' | 'superseded' | 'gone';
  by?: Carried;
  batch?: Pending;
};

/** One look of the tier `me` at the batch: it starts once no landing has come for `quietMs`, or the oldest has waited `maxWaitMs`, taking the batch out of the file. */
export function look(before: Pending | null, me: string, now: number, quietMs: number, maxWaitMs: number = HEAVY_MAX_WAIT_MS): Look {
  if (!before) return { pending: null, role: 'gone' };
  if (before.id !== me) return { pending: before, role: 'superseded', by: { id: before.id, commit: before.commit, branches: before.branches } };
  if (now - Date.parse(before.last) < quietMs && now - Date.parse(before.first) < maxWaitMs) return { pending: before, role: 'wait' };
  return { pending: null, role: 'start', batch: before };
}

/** The ranges the batch's tier reaches its classes over: each reasoned landing's own change. */
export const rangesOf = (batch: Pending): Range[] => batch.ranges.map(({ base, commit }) => ({ base, commit }));

/** The pending record as the file holds it; none where it is missing or does not read. */
export function readPending(file: string): Pending | null {
  if (!existsSync(file)) return null;
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8')) as Partial<Pending> | null;
    if (!parsed || typeof parsed.id !== 'string' || typeof parsed.commit !== 'string' || typeof parsed.base !== 'string' || typeof parsed.last !== 'string') return null;
    return { id: parsed.id, commit: parsed.commit, base: parsed.base, first: parsed.first ?? parsed.last, last: parsed.last, branches: parsed.branches ?? [], ranges: parsed.ranges ?? [], carried: parsed.carried ?? [] };
  } catch {
    return null;
  }
}

/** One change of the pending record under its lock: `fn` answers the record to keep (null clears it) and what its caller reads. */
export function changePending<R extends { pending: Pending | null }>(file: string, fn: (pending: Pending | null) => R): R {
  mkdirSync(dirname(file), { recursive: true });
  return withKeepLock(file, () => {
    const result = fn(readPending(file));
    writeAtomic(file, `${JSON.stringify(result.pending ?? {}, null, 2)}\n`);
    return result;
  });
}

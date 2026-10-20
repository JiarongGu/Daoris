/**
 * A repository's heavy-run slot as the page reads it (HEAVYSLOT1, the UI/UX design §3.5): who holds it, at which gate, and who waits
 * behind it, as the driver read the records its runs keep beside the slot (`HEAVY_SLOT`), never a run's output. A holder or a waiter
 * with no session is a run from a terminal. `HeavySlotLine` draws it.
 */

/** The gate a holder runs now: its name, its place from 0, and its start. */
export type HeavyGateNow = { gate: string; at: number; since: string };

/** The run holding the slot, as `HEAVY_SLOT` answers it. */
export type HeavyHolder = {
  /** The session whose run it is; absent for a run from a terminal. */
  session?: string | null;
  gates: string[];
  since: string;
  now?: HeavyGateNow | null;
  /** The gate it failed at and its exit, while it ends what it started before letting go. */
  failed?: { gate: string; exit: number } | null;
};

/** A run waiting for the slot. */
export type HeavyWaiter = { session?: string | null; since: string };

/** A repository's slot as `HEAVY_SLOT` answers it: no holder is no heavy run now; `refusal` where it could not be read. */
export type HeavySlot = { repository: string; holder?: HeavyHolder | null; waiters: HeavyWaiter[]; refusal?: string | null };

/** A session's part in its repository's slot: the run holding it at a gate, failed at one, or waiting behind another. */
export type HeavyPart =
  | { kind: 'running'; n: number; total: number; gate: string; since: string }
  | { kind: 'failed'; gate: string }
  | { kind: 'waiting'; behind: string | null; since: string };

const text = (value: unknown): string | null => (typeof value === 'string' && value.trim() !== '' ? value : null);

const record = (value: unknown): Record<string, unknown> | null =>
  value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null;

/** The answer read field by field, an older shell's none or a shape it does not know read as nothing. */
export function readHeavySlot(raw: unknown): HeavySlot | null {
  const answer = record(raw);
  const repository = text(answer?.repository);
  if (!answer || !repository) return null;
  const waiters = (Array.isArray(answer.waiters) ? answer.waiters : [])
    .map(record)
    .filter((row): row is Record<string, unknown> => row !== null && text(row.since) !== null)
    .map((row) => ({ session: text(row.session), since: row.since as string }));
  const held = record(answer.holder);
  const now = record(held?.now);
  const failed = record(held?.failed);
  const holder: HeavyHolder | null = held && text(held.since) ? {
    session: text(held.session),
    gates: Array.isArray(held.gates) ? held.gates.filter((gate): gate is string => typeof gate === 'string') : [],
    since: held.since as string,
    now: now && text(now.gate) && typeof now.at === 'number' && text(now.since)
      ? { gate: now.gate as string, at: now.at, since: now.since as string }
      : null,
    failed: failed && text(failed.gate) && typeof failed.exit === 'number' ? { gate: failed.gate as string, exit: failed.exit } : null,
  } : null;
  return { repository, holder, waiters, refusal: text(answer.refusal) };
}

/**
 * The part `session` has in its repository's slot, or null where it has none. A holder that has taken the slot and not yet started
 * its first gate reads as at its first, from when it took the slot.
 */
export function heavyPartOf(slot: HeavySlot | null | undefined, session: string): HeavyPart | null {
  if (!slot) return null;
  const { holder } = slot;
  if (holder?.session === session) {
    if (holder.failed) return { kind: 'failed', gate: holder.failed.gate };
    const gate = holder.now?.gate ?? holder.gates[0];
    if (!gate) return null;
    return { kind: 'running', n: (holder.now?.at ?? 0) + 1, total: Math.max(holder.gates.length, 1), gate, since: holder.now?.since ?? holder.since };
  }
  const waiting = slot.waiters.find((waiter) => waiter.session === session);
  // Behind the holder's session; a holder with none, or a slot taken by a run that has not said what it runs yet, names no session.
  return waiting ? { kind: 'waiting', behind: holder?.session ?? null, since: waiting.since } : null;
}

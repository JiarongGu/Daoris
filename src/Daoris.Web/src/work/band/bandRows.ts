import type { Beside, SessionEvent } from '../conversation/conversationTypes';
import { type BesideFocus, besideOf } from '../beside/besides';

/** How long a running thing goes without output before its line says so: the stall signal. */
export const QUIET = 6 * 60_000;

/** The agent's current act while a turn runs: the call it is inside, by its title, or none named (it thinks); since when, where held. */
export type BandAct = { title?: string; since?: string };

/**
 * What a session's conversation tells the band above the dock: the record's starts and ends of what it runs beside itself, its
 * agent's act, and the way to put a subagent or a task in the panel's focus.
 */
export type BandTold = {
  session: string;
  lifetimes: readonly SessionEvent[];
  act: BandAct | null;
  open: (focus: BesideFocus) => void;
};

/** What the session runs beside itself now: each subagent and task whose start the record keeps and no end, in the order they started. */
export function runningIn(lifetimes: readonly SessionEvent[]): Beside[] {
  const held = new Map<string, Beside>();
  for (const event of lifetimes) {
    if (!event.id) continue;
    const key = `${event.kind}:${event.id}`;
    held.set(key, besideOf(held.get(key), event));
  }
  return [...held.values()].filter((beside) => !beside.end);
}

/** A subagent's acts, oldest first, as its own record keeps them: each call by its title once, each message by its last line. */
export function actsIn(events: readonly SessionEvent[]): string[] {
  const acts = new Map<string, string>();
  for (const event of events) {
    const key = `${event.kind}:${event.id ?? event.seq}`;
    if (event.kind === 'tool' && event.title) acts.set(key, event.title);
    if (event.kind === 'message' && event.text) acts.set(key, (acts.get(key) ?? '') + event.text);
  }
  return [...acts.values()].map((said) => said.trim().split('\n').pop()!.trim()).filter(Boolean);
}

/** How long it has gone without output, from the moment its last came, where that is past `QUIET`; null where it is not quiet. */
export function quietFor(from: number | null, now: number): number | null {
  return from !== null && Number.isFinite(from) && now - from >= QUIET ? now - from : null;
}

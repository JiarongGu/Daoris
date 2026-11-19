import type { ComponentType, ReactNode } from 'react';
import type { Beside, SessionEvent } from '../conversation/conversationTypes';

/** A subagent's own record as its reader holds it: its events once read, null where none is kept on this machine. */
export type ChildRead = { events: readonly SessionEvent[] | null; loaded: boolean };

/**
 * How the rows read what a session ran beside itself, each a component holding its own read: the organism above the view holds the
 * bridge, so the rows below it hold none. `running` says whether what it reads still grows.
 */
export type BesideReader = {
  /** A subagent's own record (`SESSION_CHILD_HISTORY`). */
  Child: ComponentType<{ child: string; running?: boolean; children: (read: ChildRead) => ReactNode }>;
  /**
   * A background task's last lines on its console, where its reader keeps them, and the page's moment its last line came (null
   * where none has); absent, its end's row draws none.
   */
  Tail?: ComponentType<{ task: string; children: (lines: readonly string[], heard: number | null) => ReactNode }>;
  /** A background task's whole console, drawn in the panel's focus. */
  Stream?: ComponentType<{ task: string }>;
};

/** What the panel's focus holds: a subagent, by the way to it from the session, or a background task's console. */
export type BesideFocus = { child: readonly string[] } | { task: string };

/** The record's starts and ends of each subagent and background task: what a subagent's own timeline is joined with. */
export const lifetimesIn = (events: readonly SessionEvent[]): SessionEvent[] =>
  events.filter((event) => event.kind === 'child' || event.kind === 'task');

/** A card's subagent or task with one more of its record's events said: a start's name, task words and moment, or an end's word and moment. */
export function besideOf(was: Beside | undefined, event: SessionEvent): Beside {
  const beside: Beside = was ?? { kind: event.kind as Beside['kind'], id: event.id ?? '' };
  return {
    ...beside,
    ...(event.name ? { name: event.name } : {}),
    ...(event.task ? { task: event.task } : {}),
    ...(event.depth ? { depth: event.depth } : {}),
    ...(event.end
      ? { end: event.end, by: event.by ?? null, ended: event.at, ...(event.origin === 'person' ? { stoppedBy: 'person' as const } : {}) }
      : { started: beside.started ?? event.at }),
  };
}

/**
 * How a subagent or task stands, as its catalogue key: running until its end, an end only its session's close implies said as that,
 * a stop the person made said as theirs.
 */
export function standing(beside: Beside): string {
  if (!beside.end) return 'work.beside.end.running';
  if (beside.by === 'inferred') return 'work.beside.end.withSession';
  switch (beside.end) {
    case 'ok': return 'work.beside.end.ok';
    case 'failed': return 'work.beside.end.failed';
    case 'stopped': return beside.stoppedBy === 'person' ? 'work.beside.end.stoppedByYou' : 'work.beside.end.stopped';
    default: return 'work.beside.end.lost';
  }
}

/** How long it ran, from its start to its end or to `now` while it runs; null where its start is not held. */
export function ranFor(beside: Beside, now: number): number | null {
  const from = beside.started ? Date.parse(beside.started) : Number.NaN;
  const to = beside.ended ? Date.parse(beside.ended) : now;
  return Number.isFinite(from) && Number.isFinite(to) && to >= from ? to - from : null;
}

/** The calls a subagent's own record holds, each once, its updates merged by id. */
export const callsIn = (events: readonly SessionEvent[]): number =>
  new Set(events.filter((event) => event.kind === 'tool').map((event) => event.id ?? `#${event.seq}`)).size;

/** A subagent's closing words: its last message whole, its chunks joined by id, as the record keeps them. */
export function closingIn(events: readonly SessionEvent[]): string | undefined {
  const last = [...events].reverse().find((event) => event.kind === 'message');
  if (!last) return undefined;
  const words = last.id ? events.filter((event) => event.kind === 'message' && event.id === last.id).map((event) => event.text ?? '').join('') : last.text ?? '';
  return words.trim() || undefined;
}

/** The subagent the record names by `id`, as the session's record says it: its start and end, or null where neither is held. */
export function lifetimeOf(lifetimes: readonly SessionEvent[], id: string): Beside | null {
  let beside: Beside | undefined;
  for (const event of lifetimes) if (event.kind === 'child' && event.id === id) beside = besideOf(beside, event);
  return beside ?? null;
}

/** The way from the session to a subagent through the subagents that started it, the session's own child first. */
export function pathTo(lifetimes: readonly SessionEvent[], id: string): string[] {
  const parentOf = new Map(lifetimes.filter((event) => event.kind === 'child' && event.id && event.parent).map((event) => [event.id!, event.parent!]));
  const path = [id];
  for (let at = parentOf.get(id); at && !path.includes(at); at = parentOf.get(at)) path.unshift(at);
  return path;
}

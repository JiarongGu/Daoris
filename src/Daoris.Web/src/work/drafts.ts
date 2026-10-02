import { useCallback, useState } from 'react';
import { store, stored } from '../lib/stored';

// What a person was typing to each session (CONV4b) — a per-viewer convenience, like the panel's
// height and the language (D42): never machine wiring, never a tracked file, and never shared. A
// storage that is absent or refused costs the draft across a reload, and nothing else.

/** Where the drafts are kept: session and unsent words, oldest first. */
export const DRAFTS = 'daoris.drafts';

/** How many sessions keep a draft. The newest are kept — a draft nobody came back to is the one to lose. */
export const MAX_DRAFTS = 50;

/** The kept drafts, oldest first: `[session, words]`. */
export type Drafts = ReadonlyArray<readonly [string, string]>;

/**
 * Every kept draft, or none when storage is garbage or refused.
 *
 * @remarks
 * A LIST, because the order is the age. It was a map once, and a map's keys are not in the order they
 * were written: JavaScript puts integer-like keys first. A session id is eight hex characters, and
 * about one in fifty is all digits — so with the limit reached, the draft being typed was the "oldest"
 * and went on every keystroke (REV3). A map written by the earlier build is still read.
 */
export function readDrafts(): Drafts {
  try {
    // The parse is what this guards: a refused store is already `stored`'s null.
    const held: unknown = JSON.parse(stored(DRAFTS) ?? '[]');
    const pairs: unknown[] = Array.isArray(held)
      ? held
      : held && typeof held === 'object' ? Object.entries(held) : [];
    return pairs.filter((pair): pair is [string, string] =>
      Array.isArray(pair) && typeof pair[0] === 'string' && typeof pair[1] === 'string' && pair[1].length > 0);
  } catch {
    return [];
  }
}

/**
 * The drafts with one session's changed: an emptied draft is forgotten, a touched one becomes the
 * newest, and past the limit the oldest go. Pure.
 */
export function withDraft(all: Drafts, session: string, text: string): Drafts {
  const rest = all.filter(([held]) => held !== session);
  const next: Drafts = text ? [...rest, [session, text] as const] : rest;
  return next.slice(-MAX_DRAFTS);
}

/** One session's draft out of the list. */
export function draftOf(all: Drafts, session: string): string {
  return all.find(([held]) => held === session)?.[1] ?? '';
}

/**
 * Forget one session's draft (SESSUX1f, D126 §5.1): a deleted conversation's words are gone from this machine, and an
 * unsent draft is the viewer's copy of some of them.
 */
export function forgetDraft(session: string): void {
  const all = readDrafts();
  if (all.some(([held]) => held === session)) keep(withDraft(all, session, ''));
}

function keep(all: Drafts): void {
  // Not keeping it across a reload is a lesser failure than not taking the keystroke.
  store(DRAFTS, JSON.stringify(all));
}

/**
 * The attended session's draft and a way to change it — each session its own, kept across a reload.
 *
 * @remarks
 * A state hook, not a data hook: it reaches neither the service nor the shell (components §2), so a
 * molecule could hold it. The frame does, because the frame knows which session is attended.
 */
export function useDraft(session: string | null): [string, (next: string | ((was: string) => string)) => void] {
  const [all, setAll] = useState<Drafts>(readDrafts);

  const set = useCallback((next: string | ((was: string) => string)) => {
    if (!session) return;
    setAll((held) => {
      const text = typeof next === 'function' ? next(draftOf(held, session)) : next;
      const changed = withDraft(held, session, text);
      keep(changed);
      return changed;
    });
  }, [session]);

  return [session ? draftOf(all, session) : '', set];
}

import { useCallback, useState } from 'react';
import { store, stored } from '../../lib/stored';

// What a person is typing to each session — a per-viewer convenience, like the panel's
// height and the language: never machine wiring, never a tracked file, and never shared. A
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
 * A LIST, because the order is the age. A map's keys are not in the order they
 * are written: JavaScript puts integer-like keys first. A session id is eight hex characters, and
 * about one in fifty is all digits — so with the limit reached, the draft being typed would be the "oldest"
 * and go on every keystroke. A map written by an earlier build is still read.
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
 * Forget one session's draft: a deleted conversation's words are gone from this machine, and an
 * unsent draft is the viewer's copy of some of them.
 */
export function forgetDraft(session: string): void {
  const all = readDrafts();
  if (all.some(([held]) => held === session)) keep(withDraft(all, session, ''));
}

/**
 * Put words into a session's draft before its page is shown: a conversation that opened without the words it is
 * started on keeps them in its box. Words already in its draft stay, after a blank line, as a send handed back keeps them.
 */
export function keepDraft(session: string, text: string): void {
  const all = readDrafts();
  keep(withDraft(all, session, [text, draftOf(all, session).trim()].filter(Boolean).join('\n\n')));
}

function keep(all: Drafts): void {
  // Not keeping it across a reload is a lesser failure than not taking the keystroke.
  store(DRAFTS, JSON.stringify(all));
}

/**
 * The attended session's draft and a way to change it — each session its own, kept across a reload.
 *
 * @remarks
 * A state hook, not a data hook: it reaches neither the service nor the shell, so a
 * molecule could hold it. The frame does, because the frame knows which session is attended.
 */
export function useDraft(session: string | null): [string, (next: string | ((was: string) => string)) => void] {
  const [all, setAll] = useState<Drafts>(readDrafts);
  // Read again as another session is attended: `keepDraft` may have put words in its draft meanwhile, and every set here
  // writes the store as it changes the list, so the store is never behind it.
  const [reading, setReading] = useState(session);
  if (reading !== session) {
    setReading(session);
    setAll(readDrafts());
  }

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

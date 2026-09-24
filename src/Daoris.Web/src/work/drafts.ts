import { useCallback, useState } from 'react';

// What a person was typing to each session (CONV4b) — a per-viewer convenience, like the panel's
// height and the language (D42): never machine wiring, never a tracked file, and never shared. A
// storage that is absent or refused costs the draft across a reload, and nothing else.

/** Where the drafts are kept: one map from session to its unsent words. */
export const DRAFTS = 'daoris.drafts';

/** How many sessions keep a draft. The newest are kept — a draft nobody came back to is the one to lose. */
export const MAX_DRAFTS = 50;

/** Every kept draft, or none when storage is garbage or refused. */
export function readDrafts(): Record<string, string> {
  try {
    const held: unknown = JSON.parse(window.localStorage.getItem(DRAFTS) ?? '{}');
    if (!held || typeof held !== 'object' || Array.isArray(held)) return {};
    return Object.fromEntries(Object.entries(held).filter((entry): entry is [string, string] => typeof entry[1] === 'string'));
  } catch {
    return {};
  }
}

/**
 * The drafts with one session's changed: an emptied draft is forgotten, a touched one becomes the
 * newest, and past the limit the oldest go. Pure — the order of the map's keys is its age.
 */
export function withDraft(all: Record<string, string>, session: string, text: string): Record<string, string> {
  const { [session]: _was, ...rest } = all;
  if (!text) return rest;
  const next = { ...rest, [session]: text };
  const keys = Object.keys(next);
  return keys.length <= MAX_DRAFTS ? next : Object.fromEntries(keys.slice(-MAX_DRAFTS).map((key) => [key, next[key]!]));
}

function keep(all: Record<string, string>): void {
  try {
    window.localStorage.setItem(DRAFTS, JSON.stringify(all));
  } catch {
    // Not keeping it across a reload is a lesser failure than not taking the keystroke.
  }
}

/**
 * The attended session's draft and a way to change it — each session its own, kept across a reload.
 *
 * @remarks
 * A state hook, not a data hook: it reaches neither the service nor the shell (components §2), so a
 * molecule could hold it. The frame does, because the frame knows which session is attended.
 */
export function useDraft(session: string | null): [string, (next: string | ((was: string) => string)) => void] {
  const [all, setAll] = useState<Record<string, string>>(readDrafts);

  const set = useCallback((next: string | ((was: string) => string)) => {
    if (!session) return;
    setAll((held) => {
      const text = typeof next === 'function' ? next(held[session] ?? '') : next;
      const changed = withDraft(held, session, text);
      keep(changed);
      return changed;
    });
  }, [session]);

  return [session ? all[session] ?? '' : '', set];
}

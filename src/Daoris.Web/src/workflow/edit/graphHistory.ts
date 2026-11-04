const slug = (text: string) => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

/**
 * A new workflow's id from its name (D221 §2.3, as role ids are made): its words in lower case joined by dashes; a name with no ASCII letter
 * or digit takes the id it was copied from, numbered (`release` → `release-2`), else `workflow`; numbered on past an id saved already.
 */
export function workflowIdFor(name: string, copiedFrom: string | null, saved: readonly string[]): string {
  const named = slug(name);
  const base = named || (copiedFrom ? slug(copiedFrom) : '') || 'workflow';
  let n = named || !copiedFrom ? 1 : 2;
  let id = n === 1 ? base : `${base}-${n}`;
  while (saved.includes(id)) {
    n += 1;
    id = `${base}-${n}`;
  }
  return id;
}

/** The editor's history: what was, what is, and what an undo has set aside for a redo. */
export type History<T> = { past: readonly T[]; present: T; future: readonly T[] };

/** How many changes an undo can go back. */
const HISTORY_MOST = 100;

export const historyOf = <T>(present: T): History<T> => ({ past: [], present, future: [] });

/** One change: a step of history, and nothing left to redo. */
export const commit = <T>(history: History<T>, next: T): History<T> =>
  ({ past: [...history.past, history.present].slice(-HISTORY_MOST), present: next, future: [] });

export const undo = <T>(history: History<T>): History<T> => (history.past.length === 0 ? history
  : { past: history.past.slice(0, -1), present: history.past[history.past.length - 1]!, future: [history.present, ...history.future] });

export const redo = <T>(history: History<T>): History<T> => (history.future.length === 0 ? history
  : { past: [...history.past, history.present], present: history.future[0]!, future: history.future.slice(1) });

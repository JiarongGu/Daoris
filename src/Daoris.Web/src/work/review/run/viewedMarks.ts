import { useCallback, useState } from 'react';
import type { ReviewRow } from './runReviewWire';
import type { DiffFile } from '../diff';
import { store, stored } from '../../../lib/stored';

// A reader's *viewed* marks, kept on this machine by a file's path and its content, so a mark outlives a revision that left the
// file untouched and is gone from one that wrote it again. One reader per window's storage: they are the person's, never the run's.

const KEY = 'daoris.reviewViewed';

/** The most marks kept: the oldest go first, since a review long settled is read again rarely. */
const KEPT = 4000;

const keyOf = (path: string, content: string) => `${path}\u0000${content}`;

const read = (): string[] => {
  try {
    const kept = JSON.parse(stored(KEY) ?? '[]') as unknown;
    return Array.isArray(kept) ? kept.filter((each): each is string => typeof each === 'string') : [];
  } catch {
    return [];
  }
};

/** The marks kept now, asked by path and content. */
export function keptViewed(): { has: (path: string, content: string) => boolean } {
  const marks = new Set(read());
  return { has: (path, content) => marks.has(keyOf(path, content)) };
}

/** Mark `path` at `content` viewed, or take the mark back. */
export function markViewed(path: string, content: string, viewed: boolean): void {
  const key = keyOf(path, content);
  const rest = read().filter((each) => each !== key);
  store(KEY, JSON.stringify(viewed ? [...rest, key].slice(-KEPT) : rest));
}

/** A short digest of a text: FNV-1a, enough to tell two patches of one path apart. */
const digest = (text: string) => {
  let hash = 0x811c9dc5;
  for (let at = 0; at < text.length; at += 1) hash = Math.imul(hash ^ text.charCodeAt(at), 0x01000193);
  return `p${(hash >>> 0).toString(16)}:${text.length}`;
};

/**
 * What a file's mark is kept against: a record's row by the hash its content has after (a removed one by its hash before), and a file
 * read from git by its patch's text, which changes exactly when its change does.
 */
export function contentOf(file: ReviewRow | DiffFile): string {
  if ('change' in file) return file.after ?? `deleted:${file.before ?? ''}`;
  return digest(file.patch ?? `${file.status}:${file.added ?? '-'}:${file.removed ?? '-'}`);
}

/** The marks as the page reads and presses them, drawn again at each press. */
export function useViewedMarks() {
  const [marks, setMarks] = useState(keptViewed);
  const mark = useCallback((path: string, content: string, viewed: boolean) => {
    markViewed(path, content, viewed);
    setMarks(keptViewed());
  }, []);
  return { isViewed: marks.has, mark };
}

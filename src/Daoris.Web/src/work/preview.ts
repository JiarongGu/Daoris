import { createContext } from 'react';

// A file's preview (PREVIEW1, D111): its shape and the derivations the tool card, the review and the
// preview share. In its own module for `diff.ts`'s reason: the molecules that RENDER a preview may not
// import the bridge, and the hook that FETCHES one lives there.

/** One file in a session's tree, as the host read it for its preview (`SESSION_FILE`). */
export type TreeFile = {
  session: string;
  /** Relative to the tree, with forward slashes — never the machine's path. */
  path: string;
  /** The whole file's size in bytes, however much of it `text` holds. */
  size: number;
  /** A NUL in its first 8,000 bytes, git's own test: then there is no text. */
  binary: boolean;
  /** The file's text, or its first 256 KiB cut at a line's end. Null for a binary file. */
  text: string | null;
  /** Whether the file holds more than `text`. */
  truncated: boolean;
};

/** Lines of a file, one-based and both ends included. */
export type LineRange = { from: number; to: number };

/** What a door asks the preview to open: a path relative to the tree, and the lines to mark, where known. */
export type FileOpen = { path: string; lines?: LineRange | null };

/**
 * What opens a file's preview, or null where nothing does (PREVIEW1, D111).
 *
 * @remarks
 * **Context, not a prop**, as the link opener is (`links.tsx`): a tool card deep in a conversation holds no
 * hook of the bridge's and needs nothing threaded through every level above it. The Work frame provides it
 * for the attended session; rendered with no provider — a browser, a detached session's window, Ask
 * Daoris — a path is simply text.
 */
export const FileOpener = createContext<((open: FileOpen) => void) | null>(null);

const segmentsOf = (path: string) => path.split(/[\\/]+/).filter((segment) => segment.length > 0);

const isAbsolute = (path: string) => /^(?:[A-Za-z]:)?[\\/]/.test(path) || /^[A-Za-z]:/.test(path);

/**
 * The path a preview asks for: `location` relative to `tree`, with forward slashes — or null where the page
 * can already see the preview would refuse it (outside the tree, the tree itself, under `.git`).
 *
 * @remarks
 * **A door that could only refuse is not offered** (UX5 U66), so a card shows such a path as text. The host
 * judges again by its own rule, links included, which a page cannot see. A folder is compared as a folder,
 * never by string prefix, and a drive letter's case is the same place.
 */
export function treePath(location: string, tree: string | null | undefined): string | null {
  if (!location) return null;
  let segments: string[];
  if (isAbsolute(location)) {
    if (!tree) return null;
    const root = segmentsOf(tree);
    const whole = segmentsOf(location);
    const same = (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: 'accent' }) === 0;
    if (whole.length <= root.length || !root.every((segment, index) => same(segment, whole[index]!))) return null;
    segments = whole.slice(root.length);
  } else {
    segments = segmentsOf(location);
  }

  const resolved: string[] = [];
  for (const segment of segments) {
    if (segment === '.') continue;
    if (segment === '..') {
      if (resolved.length === 0) return null;
      resolved.pop();
    } else {
      resolved.push(segment);
    }
  }
  if (resolved.length === 0 || resolved.some((segment) => segment.toLowerCase() === '.git')) return null;
  return resolved.join('/');
}

const whole = (value: unknown): number | null =>
  typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : null;

/**
 * The lines a tool call named, from its own input as the wire carried it: a read's `offset` (the line it
 * starts at) and `limit` (how many), which both doors carry for Claude Code's read. Null where the input
 * names none, or was cut before it could be read — nothing is inferred from a title or an output.
 */
export function namedLines(input: string | null | undefined): LineRange | null {
  if (!input) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(input);
  } catch {
    return null;
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null;
  const { offset, limit } = parsed as { offset?: unknown; limit?: unknown };
  const start = whole(offset);
  const count = whole(limit);
  if (start === null && (count === null || count === 0)) return null;
  const from = Math.max(1, start ?? 1);
  return { from, to: count ? from + count - 1 : from };
}

/** A file's text as its lines: the last newline ends a line rather than starting an empty one. */
export function fileLines(text: string): string[] {
  if (text === '') return [];
  const lines = text.split(/\r?\n/);
  if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
  return lines;
}

/** A path's last segment: the name a tab can hold. */
export function fileName(path: string): string {
  return segmentsOf(path).pop() ?? path;
}

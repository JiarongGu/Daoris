/**
 * The lines an edit changed, from the old and new text a tool call carried (ACP's `diff` content) —
 * what an edit card shows, and its +/− counts.
 *
 * @remarks
 * **A longest-common-subsequence over lines**, which is exact for the edits an agent makes (a few
 * lines in a file) and cheap at that size. Past {@link LIMIT} lines on either side the table would
 * be large for a card nobody reads line by line, so the whole old text reads as removed and the new
 * as added — still true, only less precise, and the review pane (SURF6) has git's own diff.
 */

import type { PatchLine } from './patch';

export type DiffLine = { kind: 'same' | 'del' | 'add'; text: string };

/** The most lines on either side that get a precise diff. */
export const LIMIT = 600;

const lines = (text: string | null | undefined) => (text ? text.replace(/\r\n/g, '\n').split('\n') : []);

export function lineDiff(oldText: string | null | undefined, newText: string | null | undefined): DiffLine[] {
  const a = lines(oldText);
  const b = lines(newText);
  if (a.length > LIMIT || b.length > LIMIT) {
    return [...a.map((text) => ({ kind: 'del' as const, text })), ...b.map((text) => ({ kind: 'add' as const, text }))];
  }

  // lcs[i][j]: the longest common run of a[i..] and b[j..].
  const lcs = Array.from({ length: a.length + 1 }, () => new Array<number>(b.length + 1).fill(0));
  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) {
      lcs[i]![j] = a[i] === b[j] ? lcs[i + 1]![j + 1]! + 1 : Math.max(lcs[i + 1]![j]!, lcs[i]![j + 1]!);
    }
  }

  const out: DiffLine[] = [];
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    if (a[i] === b[j]) {
      out.push({ kind: 'same', text: a[i]! });
      i++;
      j++;
    } else if (lcs[i + 1]![j]! >= lcs[i]![j + 1]!) {
      out.push({ kind: 'del', text: a[i++]! });
    } else {
      out.push({ kind: 'add', text: b[j++]! });
    }
  }
  while (i < a.length) out.push({ kind: 'del', text: a[i++]! });
  while (j < b.length) out.push({ kind: 'add', text: b[j++]! });
  return out;
}

/**
 * An edit's lines in the review's line model (REVIEW2), so one renderer draws both. Numbered from 1
 * within the edit's own text, which is why the card draws them unnumbered: they are not the file's.
 */
export function patchLines(diff: readonly DiffLine[]): PatchLine[] {
  let old = 1;
  let now = 1;
  return diff.map((line): PatchLine => {
    if (line.kind === 'del') return { kind: 'del', text: line.text, old: old++ };
    if (line.kind === 'add') return { kind: 'add', text: line.text, new: now++ };
    return { kind: 'same', text: line.text, old: old++, new: now++ };
  });
}

/** How many lines an edit added and removed — the card's `+n −m`. */
export function diffCounts(diff: readonly DiffLine[]): { added: number; removed: number } {
  return {
    added: diff.filter((line) => line.kind === 'add').length,
    removed: diff.filter((line) => line.kind === 'del').length,
  };
}

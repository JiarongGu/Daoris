import type { PatchLine } from '../patch';

/** The most cells the comparison's table may hold; past it the file is drawn removed and added whole. */
const BOUND = 4_000_000;

const linesOf = (text: string | null) => {
  if (text === null || text.length === 0) return [];
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  if (lines.at(-1) === '') lines.pop();
  return lines;
};

/**
 * Two versions of one file compared line by line, for a record that keeps a file's versions and no patch (a folder's, a copy's): the
 * longest run of lines both keep, the rest removed and added, numbered on each side. `whole` is false where the file is too large to
 * compare and is drawn removed and added.
 */
export function lineDiff(before: string | null, after: string | null): { lines: PatchLine[]; whole: boolean } {
  const old = linesOf(before);
  const now = linesOf(after);
  if (old.length * now.length > BOUND) {
    return {
      whole: false,
      lines: [
        ...old.map((text, at): PatchLine => ({ kind: 'del', text, old: at + 1 })),
        ...now.map((text, at): PatchLine => ({ kind: 'add', text, new: at + 1 })),
      ],
    };
  }

  // The longest common run, read from the end, so the walk below goes forward.
  const width = now.length + 1;
  const table = new Uint32Array((old.length + 1) * width);
  for (let i = old.length - 1; i >= 0; i -= 1) {
    for (let j = now.length - 1; j >= 0; j -= 1) {
      table[i * width + j] = old[i] === now[j]
        ? table[(i + 1) * width + j + 1] + 1
        : Math.max(table[(i + 1) * width + j], table[i * width + j + 1]);
    }
  }

  const lines: PatchLine[] = [];
  let i = 0;
  let j = 0;
  while (i < old.length || j < now.length) {
    if (i < old.length && j < now.length && old[i] === now[j]) {
      lines.push({ kind: 'same', text: old[i], old: i + 1, new: j + 1 });
      i += 1;
      j += 1;
    } else if (j >= now.length || (i < old.length && table[(i + 1) * width + j] >= table[i * width + j + 1])) {
      lines.push({ kind: 'del', text: old[i], old: i + 1 });
      i += 1;
    } else {
      lines.push({ kind: 'add', text: now[j], new: j + 1 });
      j += 1;
    }
  }
  return { lines, whole: true };
}

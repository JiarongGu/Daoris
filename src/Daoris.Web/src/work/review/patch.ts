// A review's patch as structure (REVIEW2): git's unified diff, exactly as the host passed it on, read
// into hunks with the line numbers each side had — so a view can number the lines, highlight each
// side, and set them side by side. Reading, never re-deriving: the lines and their order are git's.

/** One line of a hunk. `old` and `new` are the numbers it had on each side it appears on. */
export type PatchLine =
  | { kind: 'same'; text: string; old: number; new: number }
  | { kind: 'del'; text: string; old: number }
  | { kind: 'add'; text: string; new: number }
  /** git's own remark, such as `\ No newline at end of file` — about the line before it, not a line. */
  | { kind: 'note'; text: string };

export type Hunk = { header: string; lines: PatchLine[] };

/** Everything before the first hunk (the file's `diff --git`, index and paths), and the hunks. */
export type Patch = { preamble: string[]; hunks: Hunk[] };

const HUNK = /^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/;

/**
 * A patch read into hunks. Lines outside any hunk are the preamble; a patch with none — a rename, a
 * mode change — is its preamble alone. A line in a hunk that is none of git's four kinds is kept as a
 * note rather than dropped.
 */
export function parsePatch(patch: string): Patch {
  const preamble: string[] = [];
  const hunks: Hunk[] = [];
  let hunk: Hunk | null = null;
  let old = 0;
  let now = 0;

  const all = patch.replace(/\r\n/g, '\n').split('\n');
  // The patch's own final newline is not a line of the file; every other empty line is (below).
  if (all.at(-1) === '') all.pop();

  for (const line of all) {
    const start = HUNK.exec(line);
    if (start) {
      hunk = { header: line, lines: [] };
      hunks.push(hunk);
      old = Number(start[1]);
      now = Number(start[2]);
      continue;
    }
    if (!hunk) {
      if (line) preamble.push(line);
      continue;
    }

    const body = line.slice(1);
    switch (line[0]) {
      case ' ': hunk.lines.push({ kind: 'same', text: body, old: old++, new: now++ }); break;
      case '-': hunk.lines.push({ kind: 'del', text: body, old: old++ }); break;
      case '+': hunk.lines.push({ kind: 'add', text: body, new: now++ }); break;
      // A blank context line whose single space was stripped on the way — still a line of the file.
      case undefined: hunk.lines.push({ kind: 'same', text: '', old: old++, new: now++ }); break;
      default: hunk.lines.push({ kind: 'note', text: line });
    }
  }

  return { preamble, hunks };
}

/** One row of a side-by-side view: the old side and the new, or a note across both. */
export type SplitRow =
  | { left?: Extract<PatchLine, { kind: 'same' | 'del' }>; right?: Extract<PatchLine, { kind: 'same' | 'add' }> }
  | { note: Extract<PatchLine, { kind: 'note' }> };

/**
 * A hunk's lines set side by side: an unchanged line on both sides, and a run of removals paired, line
 * for line, with the run of additions right after it — the shape every side-by-side review draws, so
 * a changed line sits across from what replaced it.
 */
export function pairRows(lines: readonly PatchLine[]): SplitRow[] {
  const rows: SplitRow[] = [];
  let at = 0;
  while (at < lines.length) {
    const line = lines[at]!;
    if (line.kind === 'note') {
      rows.push({ note: line });
      at++;
    } else if (line.kind === 'same') {
      rows.push({ left: line, right: line });
      at++;
    } else {
      const removed: Array<Extract<PatchLine, { kind: 'del' }>> = [];
      const added: Array<Extract<PatchLine, { kind: 'add' }>> = [];
      while (lines[at]?.kind === 'del') removed.push(lines[at++] as Extract<PatchLine, { kind: 'del' }>);
      while (lines[at]?.kind === 'add') added.push(lines[at++] as Extract<PatchLine, { kind: 'add' }>);
      for (let i = 0; i < Math.max(removed.length, added.length); i++) {
        rows.push({ ...(removed[i] ? { left: removed[i] } : {}), ...(added[i] ? { right: added[i] } : {}) });
      }
    }
  }
  return rows;
}

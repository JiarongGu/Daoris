/**
 * ROUTERMERGE1: git's merge driver for a Markdown file whose table rows are keyed by their first cell.
 *
 *   node tools/merge-rows.mjs <base> <ours> <theirs> [<marker size>] [<path>]   (git runs it: %O %A %B %L %P)
 *   node tools/merge-rows.mjs --install                                      (configures the driver, once a clone)
 *
 * `docs/README.md` merged by union, so a branch that changed a row `main` also changed, or that still carried a row `main`
 * had since replaced, left both rows and no conflict: on 2026-10-24 six landings left a row twice, an older copy coming
 * back each time. This merges such a file three ways, base, ours and theirs:
 *
 * - each table's body rows by their first cell: a row one side changed takes that side, a row both changed differently
 *   (or one changed and the other removed, or both added differently) is a conflict marked in git's own form, a row one
 *   side removed and the other left alone goes, and a row both changed or added the same way is kept once;
 * - rows keep their table and their order: ours' order, a row only theirs has placed after the row it follows there;
 * - everything else (prose, headings, a table's header and separator) merges as text, line by line, a region both sides
 *   changed differently a conflict. Each table stands in that text as one line, so its rows never meet the text merge.
 *
 * A table is a line starting `|` followed by a separator line (`|---|`), its body the `|` lines after; one inside a
 * fenced block (as `doc-duplicates`' `fenced` reads one) is text. A table is known across the three by the heading above it and its place under that heading, or,
 * where a side moved or renamed it, by the base table sharing most of its row keys. Two rows with one key in a table are
 * told apart by their order.
 *
 * The result is written over ours (git's %A), its line endings ours'. `--install` sets `merge.rows.driver` in the
 * repository's own configuration (never the global one); the root `npm ci` runs it as the workspace's `prepare`, and it
 * does nothing outside a git work tree. Exit 0 clean, 1 conflicted (the result written, marked), 2 a tool error (usage,
 * an unreadable file, git failing), the file left as it was.
 */
import { readFileSync } from 'node:fs';
import { fenced } from './doc-duplicates.mjs';
import { isMain, spawnGit, writeAtomic } from './fsx.mjs';

/** The driver's command as `--install` configures it, run by git from the work tree's top. */
export const DRIVER = 'node tools/merge-rows.mjs %O %A %B %L %P';
const NAME = 'rows: Markdown table rows by their first cell (ROUTERMERGE1)';

const HEADING = /^#{1,6}\s/;
const ROW = /^\s*\|/;
const SEPARATOR = /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$/;
const SLOT = '\u0000rows\u0000';

/** The first cell of a table row, trimmed: the text before its first unescaped `|` after the opening one. */
export function keyOf(row) {
  const text = row.trim().replace(/^\|/, '');
  let at = 0;
  while (at < text.length && text[at] !== '|') at += text[at] === '\\' ? 2 : 1;
  return text.slice(0, at).trim();
}

/**
 * A document as its text lines with each table's body replaced by one slot line, and the tables by id: the heading above
 * and the table's place under it.
 */
export function parseDocument(lines) {
  const skeleton = [];
  const tables = new Map();
  const inFence = fenced(lines);
  let heading = '';
  let under = 0;
  for (let at = 0; at < lines.length; at += 1) {
    const line = lines[at];
    if (inFence[at]) { skeleton.push(line); continue; }
    if (HEADING.test(line)) { heading = line.trim(); under = 0; skeleton.push(line); continue; }
    if (ROW.test(line) && at + 1 < lines.length && !inFence[at + 1] && SEPARATOR.test(lines[at + 1])) {
      skeleton.push(line, lines[at + 1]);
      at += 2;
      const rows = [];
      while (at < lines.length && !inFence[at] && ROW.test(lines[at])) rows.push(lines[at++]);
      at -= 1;
      const id = `${heading}#${under++}`;
      tables.set(id, keyed(rows));
      skeleton.push(SLOT + id);
      continue;
    }
    skeleton.push(line);
  }
  return { skeleton, tables };
}

/** Rows as `{ key, line }`, a repeated key told apart by its order. */
function keyed(rows) {
  const seen = new Map();
  return rows.map((line) => {
    const key = keyOf(line);
    const n = seen.get(key) ?? 0;
    seen.set(key, n + 1);
    return { key: n === 0 ? key : `${key}\u0000${n}`, line };
  });
}

/** Renames a side's tables the base does not know to the base table sharing most row keys, where one does. */
function align(side, base) {
  const unknown = [...side.tables.keys()].filter((id) => !base.tables.has(id));
  const free = [...base.tables.keys()].filter((id) => !side.tables.has(id));
  for (const id of unknown) {
    const keys = new Set(side.tables.get(id).map((row) => row.key));
    let best = null;
    let most = 0;
    for (const candidate of free) {
      const shared = base.tables.get(candidate).filter((row) => keys.has(row.key)).length;
      if (shared > most) { best = candidate; most = shared; }
    }
    if (best === null) continue;
    free.splice(free.indexOf(best), 1);
    side.tables.set(best, side.tables.get(id));
    side.tables.delete(id);
    side.skeleton = side.skeleton.map((line) => (line === SLOT + id ? SLOT + best : line));
  }
}

/** The longest common subsequence of two line arrays, as index pairs in order. */
function common(a, b) {
  let start = 0;
  while (start < a.length && start < b.length && a[start] === b[start]) start += 1;
  let endA = a.length;
  let endB = b.length;
  while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) { endA -= 1; endB -= 1; }
  const n = endA - start;
  const m = endB - start;
  const table = Array.from({ length: n + 1 }, () => new Uint32Array(m + 1));
  for (let i = n - 1; i >= 0; i -= 1) {
    for (let j = m - 1; j >= 0; j -= 1) {
      table[i][j] = a[start + i] === b[start + j] ? table[i + 1][j + 1] + 1 : Math.max(table[i + 1][j], table[i][j + 1]);
    }
  }
  const pairs = [];
  for (let k = 0; k < start; k += 1) pairs.push([k, k]);
  for (let i = 0, j = 0; i < n && j < m;) {
    if (a[start + i] === b[start + j]) { pairs.push([start + i, start + j]); i += 1; j += 1; } else if (table[i + 1][j] >= table[i][j + 1]) i += 1; else j += 1;
  }
  for (let k = 0; k < a.length - endA; k += 1) pairs.push([endA + k, endB + k]);
  return pairs;
}

/** The changes from base to a side, each `{ start, end, lines }` replacing base's lines [start, end). */
function changes(base, side, label) {
  const hunks = [];
  let i0 = 0;
  let j0 = 0;
  for (const [i, j] of [...common(base, side), [base.length, side.length]]) {
    if (i > i0 || j > j0) hunks.push({ start: i0, end: i, lines: side.slice(j0, j), label });
    i0 = i + 1;
    j0 = j + 1;
  }
  return hunks;
}

function apply(base, start, end, hunks) {
  const out = [];
  let at = start;
  for (const hunk of hunks) { out.push(...base.slice(at, hunk.start), ...hunk.lines); at = hunk.end; }
  out.push(...base.slice(at, end));
  return out;
}

const same = (a, b) => a.length === b.length && a.every((line, at) => line === b[at]);

function marked(ours, theirs, size) {
  return [`${'<'.repeat(size)} ours`, ...ours, '='.repeat(size), ...theirs, `${'>'.repeat(size)} theirs`];
}

/** A three-way line merge: `{ lines, conflicts }`, a region both sides changed differently marked. */
export function mergeLines(base, ours, theirs, size = 7) {
  const all = [...changes(base, ours, 'ours'), ...changes(base, theirs, 'theirs')]
    .sort((a, b) => a.start - b.start || a.end - b.end);
  const lines = [];
  let conflicts = 0;
  let at = 0;
  for (let next = 0; next < all.length;) {
    const group = [all[next++]];
    let start = group[0].start;
    let end = group[0].end;
    // A change joins the region it overlaps, or an insertion the point it touches.
    while (next < all.length && (all[next].start < end
      || (all[next].start === end && (all[next].start === all[next].end || group.some((h) => h.start === h.end && h.start === end))))) {
      end = Math.max(end, all[next].end);
      group.push(all[next++]);
    }
    start = Math.min(start, ...group.map((h) => h.start));
    lines.push(...base.slice(at, start));
    const mine = group.filter((h) => h.label === 'ours');
    const yours = group.filter((h) => h.label === 'theirs');
    const a = apply(base, start, end, mine);
    const b = apply(base, start, end, yours);
    if (yours.length === 0 || same(a, b)) lines.push(...a);
    else if (mine.length === 0) lines.push(...b);
    else { lines.push(...marked(a, b, size)); conflicts += 1; }
    at = end;
  }
  lines.push(...base.slice(at));
  return { lines, conflicts };
}

/** One table's rows merged by key: `{ lines, conflicts }`. */
export function mergeTable(base, ours, theirs, size = 7) {
  const lineOf = (rows) => new Map(rows.map((row) => [row.key, row.line]));
  const B = lineOf(base);
  const O = lineOf(ours);
  const T = lineOf(theirs);
  const order = ours.map((row) => row.key);
  theirs.forEach((row, index) => {
    if (O.has(row.key)) return;
    let after = -1;
    for (let back = index - 1; back >= 0 && after < 0; back -= 1) after = order.indexOf(theirs[back].key);
    order.splice(after + 1, 0, row.key);
  });
  const lines = [];
  let conflicts = 0;
  for (const key of order) {
    const b = B.get(key);
    const o = O.get(key);
    const t = T.get(key);
    let take;
    if (o === t || t === b) take = o;
    else if (o === b) take = t;
    else {
      lines.push(...marked(o === undefined ? [] : [o], t === undefined ? [] : [t], size));
      conflicts += 1;
      continue;
    }
    if (take !== undefined) lines.push(take);
  }
  return { lines, conflicts };
}

/** Merges a document's three versions: `{ text, conflicts }`, the text in ours' line endings. */
export function mergeRows(baseText, oursText, theirsText, { markerSize = 7 } = {}) {
  const eol = oursText.includes('\r\n') ? '\r\n' : '\n';
  const split = (text) => text.replace(/\r\n/g, '\n').split('\n');
  const base = parseDocument(split(baseText));
  const ours = parseDocument(split(oursText));
  const theirs = parseDocument(split(theirsText));
  align(ours, base);
  align(theirs, base);
  const text = mergeLines(base.skeleton, ours.skeleton, theirs.skeleton, markerSize);
  let conflicts = text.conflicts;
  const placed = new Set();
  const rowsOf = (id) => mergeTable(base.tables.get(id) ?? [], ours.tables.get(id) ?? [], theirs.tables.get(id) ?? [], markerSize);
  const lines = [];
  for (const line of text.lines) {
    if (!line.startsWith(SLOT)) { lines.push(line); continue; }
    const id = line.slice(SLOT.length);
    if (placed.has(id)) continue;
    placed.add(id);
    const rows = rowsOf(id);
    lines.push(...rows.lines);
    conflicts += rows.conflicts;
  }
  // A table the merged text dropped, whose rows a side changed: that change is not lost silently.
  for (const id of new Set([...ours.tables.keys(), ...theirs.tables.keys()])) {
    if (placed.has(id)) continue;
    const before = (base.tables.get(id) ?? []).map((row) => row.line);
    const mine = (ours.tables.get(id) ?? []).map((row) => row.line);
    const yours = (theirs.tables.get(id) ?? []).map((row) => row.line);
    if ((ours.tables.has(id) && !same(mine, before)) || (theirs.tables.has(id) && !same(yours, before))) {
      const tail = lines.length > 0 && lines.at(-1) === '' ? lines.pop() : undefined;
      lines.push(...marked(mine, yours, markerSize));
      if (tail !== undefined) lines.push(tail);
      conflicts += 1;
    }
  }
  return { text: lines.join(eol), conflicts };
}

/** Sets the driver in this repository's own configuration: 0 set or no git work tree here, 2 git failing. */
export function install({ out, err, cwd, env }) {
  const inside = spawnGit(['rev-parse', '--is-inside-work-tree'], { cwd, env, encoding: 'utf8' });
  if (inside.status !== 0 || inside.stdout.trim() !== 'true') {
    out('merge-rows: not a git work tree; nothing configured');
    return 0;
  }
  for (const [key, value] of [['merge.rows.name', NAME], ['merge.rows.driver', DRIVER]]) {
    const set = spawnGit(['config', '--local', key, value], { cwd, env, encoding: 'utf8' });
    if (set.status !== 0) {
      err(`merge-rows: git config ${key} failed: ${set.stderr?.trim() ?? set.error?.message}`);
      return 2;
    }
  }
  out(`merge-rows: merge.rows.driver set to \`${DRIVER}\``);
  return 0;
}

export function main(argv, { out = console.log, err = console.error, cwd = process.cwd(), env = process.env } = {}) {
  if (argv[0] === '--install' && argv.length === 1) return install({ out, err, cwd, env });
  const [basePath, oursPath, theirsPath, size, path] = argv;
  if (!basePath || !oursPath || !theirsPath) {
    err('usage: node tools/merge-rows.mjs <base> <ours> <theirs> [<marker size>] [<path>] | --install');
    return 2;
  }
  const markerSize = Number.parseInt(size ?? '7', 10);
  let texts;
  try {
    texts = [basePath, oursPath, theirsPath].map((file) => readFileSync(file, 'utf8'));
  } catch (error) {
    err(`merge-rows: ${error.message}`);
    return 2;
  }
  const merged = mergeRows(...texts, { markerSize: Number.isInteger(markerSize) && markerSize > 0 ? markerSize : 7 });
  try {
    writeAtomic(oursPath, merged.text);
  } catch (error) {
    err(`merge-rows: ${error.message}`);
    return 2;
  }
  if (merged.conflicts > 0) err(`merge-rows: ${path ?? oursPath}: ${merged.conflicts} conflict(s) marked`);
  return merged.conflicts > 0 ? 1 : 0;
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

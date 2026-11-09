/**
 * The declared backlog merged: by the merge base line by line (`mergeByBase`, a declared row table's rule) and by its rows' ids
 * (`mergeByRows`, the backlog's), with the ids a row covers (`rowOf`) and a made backlog held to its rows (`rowsHeld`).
 *
 * A twin of the driver's `BacklogMerge`, `BacklogRows`, `BacklogIds` and `BacklogHeld`: no code is shared (this is the repository's
 * own Node tooling, that the driver), so both hold `backlog-merges.json` among the driver's test fixtures, row for row, and a change to
 * one is a change to both, in the same commit.
 */

/** A range covers at most this many ids; one wider does not read. */
export const RANGE_AT_MOST = 64;

const ID = /[A-Z][A-Z0-9-]*[0-9][A-Za-z0-9-]*/y;
const SHORT = /[0-9][A-Za-z0-9-]*/y;
const SEPARATOR = /, and |, | \/ | and | to /y;
const ROW_START = /^[-*] (?:\[[ xX]\] )?\*\*/;

// A row stands in the line merge as this mark and its key, and no line of text begins with a NUL.
const MARK = '\0';

/** What a sticky pattern matches at `index` of `text`, or null. */
function matchAt(pattern, text, index) {
  pattern.lastIndex = index;
  return pattern.exec(text)?.[0] ?? null;
}

/**
 * The ids a backlog row covers, from the list its first bold token opens with: `{ key, ids }`, the id it opens with and every id it
 * covers, `ids` null where the list begins with an id but goes on in a way that names no id for certain; null where the line is no row.
 * Short parts (`3d`) take the stem of the id before them; a range (`to`) runs over one trailing letter or one trailing number.
 */
export function rowOf(line) {
  const start = ROW_START.exec(line);
  if (!start) return null;
  const key = matchAt(ID, line, start[0].length);
  if (key === null) return null;
  const ids = [key];
  let last = key;
  let position = start[0].length + key.length;
  for (let separator = matchAt(SEPARATOR, line, position); separator !== null; separator = matchAt(SEPARATOR, line, position)) {
    const next = position + separator.length;
    const full = matchAt(ID, line, next);
    const part = full === null ? matchAt(SHORT, line, next) : null;
    if (full === null && part === null) break;
    const id = full ?? expanded(last, part);
    position = next + (full ?? part).length;
    if (id === null) return { key, ids: null };
    if (separator === ' to ') {
      const range = rangeOf(last, id);
      if (range === null) return { key, ids: null };
      ids.push(...range.slice(1));
    } else {
      ids.push(id);
    }
    last = id;
  }
  return { key, ids: [...new Set(ids)], span: [start[0].length, position] };
}

/** `ids` written as a list that reads back as them: each after the first as its shortest part on the stem before it, else each in full; null where neither does. */
export function writtenIds(ids) {
  for (const shortened of [true, false]) {
    const parts = ids.map((id, at) => (at > 0 && shortened ? partOf(ids[at - 1], id) : id));
    const list = parts.length === 1 ? parts[0] : `${parts.slice(0, -1).join(', ')} and ${parts.at(-1)}`;
    const read = rowOf(`- [ ] **${list}**`)?.ids;
    if (read && read.length === ids.length && read.every((id, at) => id === ids[at])) return list;
  }
  return null;
}

/** The shortest part of `id` the stem of `before` gives back as it; the whole id where none does. */
function partOf(before, id) {
  const parts = [...id.matchAll(/[0-9]+/g)].map((run) => id.slice(run.index)).filter((part) => expanded(before, part) === id);
  return parts.sort((x, y) => x.length - y.length)[0] ?? id;
}

/** A short part on the stem of `before` whose digit run equals the part's own; null where none or two do. */
function expanded(before, part) {
  const digits = /[0-9]+/.exec(part)[0];
  const stems = [...before.matchAll(/[0-9]+/g)].filter((run) => run[0] === digits);
  return stems.length === 1 ? before.slice(0, stems[0].index) + part : null;
}

/** The ids from `from` to `to`: one trailing letter apart, or one trailing number; null otherwise. */
function rangeOf(from, to) {
  let common = 0;
  while (common < from.length && common < to.length && from[common] === to[common]) common += 1;
  let [stem, a, b] = [from.slice(0, common), from.slice(common), to.slice(common)];
  const letter = (text) => text.length === 1 && text >= 'a' && text <= 'z';
  if (letter(a) && letter(b) && a < b) {
    return Array.from({ length: b.charCodeAt(0) - a.charCodeAt(0) + 1 }, (_, i) => stem + String.fromCharCode(a.charCodeAt(0) + i));
  }
  // A number's run is read whole: the common part gives back the digits it took from it.
  while (common > 0 && /[0-9]/.test(from[common - 1])) common -= 1;
  [stem, a, b] = [from.slice(0, common), from.slice(common), to.slice(common)];
  const number = (text) => /^[1-9][0-9]{0,4}$/.test(text);
  if (number(a) && number(b) && Number(a) < Number(b) && Number(b) - Number(a) < RANGE_AT_MOST) {
    return Array.from({ length: Number(b) - Number(a) + 1 }, (_, i) => stem + (Number(a) + i));
  }
  return null;
}

/** The changes from `base` to `side`, as hunks of the base's lines `[start, end)` and what replaced them; by the longest common run of lines. */
export function lineHunks(base, side) {
  let head = 0;
  while (head < base.length && head < side.length && base[head] === side[head]) head += 1;
  let tail = 0;
  while (tail < base.length - head && tail < side.length - head && base[base.length - 1 - tail] === side[side.length - 1 - tail]) tail += 1;
  const a = base.slice(head, base.length - tail);
  const b = side.slice(head, side.length - tail);
  // The common run's length from each pair of positions to the end, one row of b per line of a.
  const width = b.length + 1;
  const run = new Uint32Array((a.length + 1) * width);
  for (let i = a.length - 1; i >= 0; i -= 1) {
    for (let j = b.length - 1; j >= 0; j -= 1) {
      run[i * width + j] = a[i] === b[j] ? run[(i + 1) * width + j + 1] + 1 : Math.max(run[(i + 1) * width + j], run[i * width + j + 1]);
    }
  }
  const hunks = [];
  let open = null;
  const close = () => {
    if (open) hunks.push(open);
    open = null;
  };
  let [i, j] = [0, 0];
  while (i < a.length || j < b.length) {
    if (i < a.length && j < b.length && a[i] === b[j]) {
      close();
      [i, j] = [i + 1, j + 1];
      continue;
    }
    open ??= { start: head + i, end: head + i, lines: [] };
    if (j < b.length && (i === a.length || run[i * width + j + 1] >= run[(i + 1) * width + j])) {
      open.lines.push(b[j]);
      j += 1;
    } else {
      i += 1;
      open.end = head + i;
    }
  }
  close();
  return hunks;
}

const sameHunk = (x, y) => x.start === y.start && x.end === y.end && x.lines.length === y.lines.length && x.lines.every((line, i) => line === y.lines[i]);

/** Whether two sides' hunks touch the same base line, or one adds inside a run the other replaced; adds at one place never do. */
function hunksCollide(x, y) {
  if (x.start === x.end && y.start === y.end) return false;
  if (x.start === x.end) return y.start < x.start && x.start < y.end;
  if (y.start === y.end) return x.start < y.start && y.start < x.end;
  return x.start < y.end && y.start < x.end;
}

/**
 * The text merged line by line against its merge base: a line one side added is kept, one a side removed is dropped, and two sides
 * that changed the same base line are a conflict, left for the person. Lines two sides add at one place are both kept, this side's
 * first, unless `collide` says they meet. With `text` when nothing collided, and what is kept, dropped and left.
 *
 * A twin of the driver's `BacklogMerge.ByBase`: a declared row table settles by it, and the backlog's rows stand on it (`mergeByRows`).
 */
export function mergeByBase(base, ours, theirs, collide = hunksCollide) {
  const [o, t, b] = [ours, theirs, base].map((text) => text.split('\n'));
  const mine = lineHunks(b, o).map((hunk) => ({ ...hunk, side: 0 }));
  const yours = lineHunks(b, t).map((hunk) => ({ ...hunk, side: 1 }));
  const both = [];
  const hunks = [...mine];
  for (const hunk of yours) {
    const twin = mine.find((other) => sameHunk(other, hunk));
    if (twin) continue;
    const met = mine.filter((other) => collide(other, hunk));
    if (met.length) both.push({ line: Math.min(hunk.start, ...met.map((other) => other.start)) + 1 });
    hunks.push(hunk);
  }
  const kept = hunks.reduce((sum, hunk) => sum + hunk.lines.length, 0);
  const dropped = hunks.reduce((sum, hunk) => sum + hunk.end - hunk.start, 0);
  if (both.length) return { text: null, kept, dropped, both };
  hunks.sort((x, y) => x.start - y.start || x.end - y.end || x.side - y.side);
  const out = [];
  let at = 0;
  for (const hunk of hunks) {
    out.push(...b.slice(at, hunk.start), ...hunk.lines);
    at = hunk.end;
  }
  out.push(...b.slice(at));
  return { text: out.join('\n'), kept, dropped, both };
}

const trimmed = (line) => line.replace(/[ \t\r]+$/, '');
// A row with its whitespace normalised: each run of spaces one space, its line ending and trailing spaces cut.
const normal = (line) => trimmed(line).replace(/[ \t]+/g, ' ');
const alike = (x, y) => (x === null || y === null ? x === null && y === null : normal(x) === normal(y));
// Where a row's list stood in its rest: a row's text is judged without its ids.
const HOLE = '\u0001';
const restOf = (row) => (row === null ? null : row.line.slice(0, row.span[0]) + HOLE + row.line.slice(row.span[1]));
const stands = (b, o, t) => (o === t ? o : o === b ? t : o);
const covers = (row, id) => row !== null && row.ids.includes(id);

/**
 * The rows that share an id, on any side, joined into one family, each keyed by the first id met of it (the base's rows first, then
 * ours, then theirs, each list in its order): every id a row opens with mapped to its family's key.
 */
function families(sides) {
  const parent = new Map();
  const order = [];
  const find = (id) => {
    while (parent.get(id) !== id) {
      parent.set(id, parent.get(parent.get(id)));
      id = parent.get(id);
    }
    return id;
  };
  for (const row of sides.flat()) {
    for (const id of row.ids) {
      if (!parent.has(id)) {
        parent.set(id, id);
        order.push(id);
      }
    }
    for (const id of row.ids.slice(1)) parent.set(find(id), find(row.ids[0]));
  }
  const keys = new Map();
  for (const id of order) if (!keys.has(find(id))) keys.set(find(id), id);
  const keyOf = new Map();
  for (const row of sides.flat()) if (!keyOf.has(row.key)) keyOf.set(row.key, keys.get(find(row.key)));
  return keyOf;
}

/**
 * The line a family of rows makes (`{ line }`, null where it leaves), or null where it is left for the person: each id stands as only one
 * side changed it, or both alike; the rest takes the side that changed it. Where every id leaves, the row leaves only where no side's
 * rest changed. A side's own line is kept where the row is that side's; else its list is written again.
 */
function takenOf(b, o, t) {
  const ids = [...new Set([b, o, t].flatMap((row) => row?.ids ?? []))].filter((id) => stands(covers(b, id), covers(o, id), covers(t, id)));
  const [rb, ro, rt] = [b, o, t].map(restOf);
  if (ids.length === 0) return (o === null || alike(ro, rb)) && (t === null || alike(rt, rb)) ? { line: null } : null;
  const side = alike(ro, rt) ? o : alike(ro, rb) ? t : alike(rt, rb) ? o : null;
  if (side === null) return null;
  if (side.ids.length === ids.length && side.ids.every((id, at) => id === ids[at])) return { line: side.line };
  const list = writtenIds(ids);
  return list === null ? null : { line: restOf(side).replace(HOLE, list) };
}

/** Two changes meet where the line merge says, and where both add at one place unless both add rows alone. */
function rowsCollide(x, y) {
  return hunksCollide(x, y)
    || (x.start === x.end && y.start === y.end && x.start === y.start && ![...x.lines, ...y.lines].every((line) => line.startsWith(MARK)));
}

/** A side's rows (`{ line, key, ids, span }`); each id it covers twice added to `twice`, each row whose ids do not read to `unread`. */
function rowsOf(lines, twice, unread) {
  const rows = [];
  const covered = new Set();
  for (const line of lines) {
    const row = rowOf(line);
    if (row === null) continue;
    if (row.ids === null) {
      unread.push(trimmed(line));
      continue;
    }
    for (const id of row.ids) {
      if (!covered.has(id)) covered.add(id);
      else if (!twice.includes(id)) twice.push(id);
    }
    rows.push({ ...row, line });
  }
  return rows;
}

/**
 * The backlog merged by its rows' ids, rows that share an id on any side one row: each id stands as only one side changed it, or both
 * alike, and the rest of the line takes the one side that changed it, or both alike (whitespace normalised), so a row a side archived
 * leaves only where the other's copy is the base's or moved, and one both archived in part keeps the ids neither did, its list written
 * again. Every other row is left for the person with both texts (`differ`), never merged word by word. A side covering an id twice
 * (`twice`), or holding a row whose ids do not read (`unread`), is refused before anything merges. The lines that are no row merge by the
 * base, each row standing as its key; there two adds at one place meet unless both add rows alone. With `text` null where anything is left.
 */
export function mergeByRows(base, ours, theirs) {
  const sides = [base, ours, theirs].map((text) => text.split('\n'));
  const [twice, unread] = [[], []];
  const rows = sides.map((lines) => rowsOf(lines, twice, unread));
  const none = { both: [], differ: [], twice: [], unread: [] };
  if (twice.length || unread.length) return { ...none, text: null, kept: 0, dropped: 0, twice, unread: [...new Set(unread)] };
  const family = families(rows);
  const keyOf = (line) => {
    const row = rowOf(line);
    return row === null || row.ids === null ? null : family.get(row.key);
  };
  const taken = new Map();
  const differ = [];
  let [kept, dropped] = [0, 0];
  for (const key of new Set(family.values())) {
    const of = rows.map((side) => side.filter((row) => family.get(row.key) === key));
    const joined = (side) => (side.length ? side.map((row) => row.line).join('\n') : null);
    const made = of.some((side) => side.length > 1) ? null : takenOf(of[0][0] ?? null, of[1][0] ?? null, of[2][0] ?? null);
    if (made === null) {
      differ.push({ id: key, ours: joined(of[1]), theirs: joined(of[2]) });
      continue;
    }
    taken.set(key, made.line);
    const b = of[0][0] ?? null;
    if (b === null) continue;
    if (made.line === null) dropped += 1;
    else if (!alike(made.line, b.line)) [kept, dropped] = [kept + 1, dropped + 1];
  }
  if (differ.length) {
    const lines = differ.map((row) => sides[0].findIndex((line) => keyOf(line) === row.id) + 1).filter((line) => line > 0);
    return { ...none, text: null, kept, dropped, both: lines.sort((x, y) => x - y).map((line) => ({ line })), differ };
  }
  const stays = (line) => keyOf(line) === null || taken.get(keyOf(line)) !== null;
  const laid = sides.map((lines) => lines.filter(stays).map((line) => (keyOf(line) === null ? line : MARK + keyOf(line))).join('\n'));
  const layout = mergeByBase(laid[0], laid[1], laid[2], rowsCollide);
  [kept, dropped] = [kept + layout.kept, dropped + layout.dropped];
  if (layout.text === null) {
    const at = sides[0].flatMap((line, index) => (stays(line) ? [index + 1] : []));
    return { ...none, text: null, kept, dropped, both: layout.both.map((entry) => ({ line: at[entry.line - 1] })) };
  }
  const out = [];
  const placed = new Set();
  for (const line of layout.text.split('\n')) {
    if (!line.startsWith(MARK)) out.push(line);
    else if (!placed.has(line.slice(1))) {
      placed.add(line.slice(1));
      out.push(taken.get(line.slice(1)));
    } else {
      // A row both sides added alike at two places stands once, at the first.
      kept -= 1;
    }
  }
  return { ...none, text: out.join('\n'), kept, dropped };
}

/** A backlog's ids covered twice, and every id it covers in the order met; a row whose ids do not read covers its key alone. */
function coveredOf(text) {
  const [twice, ids] = [[], []];
  for (const id of text.split('\n').map(rowOf).filter((each) => each !== null).flatMap((row) => row.ids ?? [row.key])) {
    if (!ids.includes(id)) ids.push(id);
    else if (!twice.includes(id)) twice.push(id);
  }
  return { twice, ids };
}

/**
 * Whether a made backlog holds what its rows' rules keep (`mergeByRows`), judged once the merge is made: the rules run only where git
 * stops, and a merge git makes clean can keep a row one side moved while the other archived it, or one id twice. The ids it covers
 * twice, the rows it keeps that the rules leave out, those it lost that they keep, and the rules' own refusal (`refused`) where they
 * refuse; it holds where all are empty.
 */
export function rowsHeld(base, ours, theirs, made) {
  const merged = mergeByRows(base, ours, theirs);
  if (merged.text === null) return { twice: [], kept: [], lost: [], refused: merged };
  const [had, rules] = [coveredOf(made), coveredOf(merged.text)];
  return { twice: had.twice, kept: had.ids.filter((id) => !rules.ids.includes(id)), lost: rules.ids.filter((id) => !had.ids.includes(id)), refused: null };
}

/** What a refused merge says of the backlog's rows, one line each: each id twice, each row whose ids do not read, each row both changed. */
export function rowsRefused(path, merged) {
  const has = (row) => (row === null ? 'removed it' : `has "${trimmed(row)}"`);
  // A row table's merge by its base carries none of these.
  return [
    ...(merged.twice ?? []).map((id) => `${path}: holds the row ${id} twice on a side, so none of it is resolved by its rows; left for you`),
    ...(merged.unread ?? []).map((line) => `${path}: a row's ids do not read, so none of it is resolved by its rows; left for you: ${line}`),
    ...(merged.differ ?? []).map((row) => `${path}: row ${row.id} changed on both sides; left for you: main ${has(row.ours)}, and the branch ${has(row.theirs)}`),
  ];
}

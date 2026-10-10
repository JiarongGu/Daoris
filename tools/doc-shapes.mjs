#!/usr/bin/env node
/**
 * Each entry against its shape: a backlog row, a router row, and an archive outcome written since the
 * cut-over.
 *
 * ## Why
 *
 * A session pays for what it reads on every step after it reads it (D127). A ceiling on a document read
 * whole says when the document is long. It cannot say which kind of line made it long, and the backlog
 * here was trimmed twice and grew back both times, because its rows went on being written the same way
 * (`docs/2026-10-02-session-economy-design.md` §1.5). So the canon gives each entry a shape
 * (`development-documents`: a row or a router row, 60 words; an archive outcome, 60), and this reports
 * the entries over theirs, by name, longest first, so the one to move is the one at the top.
 *
 * ## Why a sibling of doc-budgets, and not inside it or `check`
 *
 * `doc-budgets` exists to draw one line: a ceiling belongs on a document read whole, never on an
 * append-only record. A shape belongs on an entry of either, so folding entry parsing into it would blur
 * the line it draws. It imports that tool's `words`, so the two counts can never disagree.
 *
 * `check` ships to every adopter, and splitting an adopter's archive into entries guesses at a format the
 * canon leaves to each repository: a wrong guess would be a false report in every session there. The canon
 * carries the shape everywhere in words and a template; this report stays this repository's until a
 * second repository keeps the template's shape and asks (design §3.2).
 *
 * ## What it reads
 *
 * The roles `daoris.json` declares under `documents` (`backlog`, `router`, `archive`), by the path each
 * names, as `doc-budgets` reads a declared ceiling. The numbers and the cut-over are in
 * `tools/doc-shapes.json`, with its `_why`. A number is raised by a one-line diff there, with the reason
 * in the commit.
 *
 * Every entry's words are counted as written, the list marker, the box and a table's pipes included,
 * because a reader pays for them, as `doc-budgets` says of a whole document. The canon's backlog-row
 * template is 44 words counted this way. Fenced examples do not introduce entries; indented examples
 * belonging to a real backlog row still contribute to its word count.
 *
 * - **A backlog row** is a checklist item at the start of a line and every indented line after it, blank
 *   lines between them included, until a line that is neither. A paragraph indented under an item after
 *   a blank line is still that item's, as a reader sees it, so a log of sightings appended that way is
 *   counted. It is named by its first bold span up to the dash.
 * - **A router row** is a table row whose first cell is a code span, named by that cell.
 * - **An archive entry** is a second-level heading and what follows it, until the next heading of that
 *   level or higher (a fenced block's lines are never headings, a fence read as markdown reads one, by
 *   `doc-duplicates`' `fenced`: ORIENT2h4). It is dated by the last ISO date its
 *   heading names, since a heading such as `(2026-09-28 → 2026-09-29)` names when it started and when it
 *   finished. Its outcome runs from the `**Outcome.**` label, label included, to the entry's end; an entry
 *   with no label is measured by what it says beyond its heading and its quoted row, so leaving the label
 *   out never hides one.
 *
 * Entries headed before the cut-over stand as written (`set-up-documents`, step 4): they are counted
 * once and never measured. With no cut-over yet, every entry stands. The steward writes the date in the
 * merge that lands the hand-back's outcome line (SESSOPT1c, design §3.5), so every entry measured is one
 * the new shape asked for.
 *
 * ## It reports; it fails only on what it cannot read
 *
 * Whether an entry says too much is a judgement, so a shape prints and exits 0 (D54). A configuration
 * that does not read, names a key this tool does not know, gives a shape that is not a positive whole
 * number, or a cut-over that is not a date, fails: each is a shape that silently stopped applying. A role
 * declared with no file on disk is `check`'s failure already, so here it is one line and no second
 * failure.
 *
 *   node tools/doc-shapes.mjs
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { words } from './doc-budgets.mjs';
import { fenced } from './doc-duplicates.mjs';
import { isMain } from './fsx.mjs';

export { fenced, words };

const CONFIG = 'tools/doc-shapes.json';
const DECLARED = 'daoris.json';
const SHAPES = ['backlogRow', 'routerRow', 'archiveOutcome'];
const KNOWN = new Set([...SHAPES, 'archiveCutOver']);

/** A real calendar date in ISO form: `2026-02-30` is not one, and a typo there would measure nothing. */
const isDate = (value) =>
  typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)
  && !Number.isNaN(Date.parse(`${value}T00:00:00Z`))
  && new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value;

/** The configuration's numbers and cut-over, and every way it does not read. */
export function readShapes(text) {
  let parsed;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    return { shapes: null, errors: [`${CONFIG} does not read as JSON: ${error.message}`] };
  }
  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    return { shapes: null, errors: [`${CONFIG} is not an object of shapes`] };
  }
  const errors = [];
  for (const key of Object.keys(parsed)) {
    if (!key.startsWith('_') && !KNOWN.has(key)) errors.push(`${CONFIG} names an unknown key \`${key}\``);
  }
  for (const key of KNOWN) {
    if (!(key in parsed)) errors.push(`${CONFIG} names no \`${key}\``);
  }
  for (const key of SHAPES) {
    if (key in parsed && !(Number.isInteger(parsed[key]) && parsed[key] > 0)) {
      errors.push(`${CONFIG}: \`${key}\` is ${JSON.stringify(parsed[key])}, not a positive whole number of words`);
    }
  }
  const cutOver = parsed.archiveCutOver;
  if ('archiveCutOver' in parsed && cutOver !== null && !isDate(cutOver)) {
    errors.push(`${CONFIG}: \`archiveCutOver\` is ${JSON.stringify(cutOver)}, not a date (YYYY-MM-DD) or null`);
  }
  return { shapes: errors.length === 0 ? parsed : null, errors };
}

/** A row's or a heading's name: its first bold span, or its own text, up to the dash. */
function nameOf(text) {
  const bold = /\*\*(.+?)\*\*/.exec(text);
  const name = (bold ? bold[1] : text).split(/\s+—\s+/)[0].trim().replace(/[:.]$/, '');
  return name.length > 48 ? `${name.slice(0, 47)}…` : name;
}

const lines = (text) => text.split(/\r?\n/);
const ROW = /^[-*] \[[ xX]\]\s*/;

/** Each checklist item in the backlog, with its indented lines, in the document's order. */
export function backlogRows(text) {
  const rows = [];
  let current = null;
  const read = lines(text);
  const inFence = fenced(read);
  for (const [i, line] of read.entries()) {
    if (!inFence[i] && ROW.test(line)) {
      current = [line];
      rows.push(current);
    } else if (current && (line.trim() === '' || /^\s/.test(line))) {
      current.push(line);
    } else {
      current = null;
    }
  }
  return rows.map((row) => {
    const body = row.join('\n');
    return { id: nameOf(body.replace(ROW, '')), words: words(body) };
  });
}

/** Each router row whose first cell is a code span. */
export function routerRows(text) {
  const rows = [];
  const read = lines(text);
  const inFence = fenced(read);
  for (const [i, line] of read.entries()) {
    if (inFence[i]) continue;
    const first = /^\|\s*(`[^`]+`)\s*(?<!\\)\|/.exec(line);
    if (first) rows.push({ id: first[1], words: words(line) });
  }
  return rows;
}

/** Each second-level entry in the archive: its name, the date its heading finishes on, and its outcome's words. */
export function archiveEntries(text) {
  const entries = [];
  let current = null;
  const read = lines(text);
  const inFence = fenced(read);
  read.forEach((line, i) => {
    if (!inFence[i] && /^#{1,2} /.test(line)) {
      current = line.startsWith('## ') ? { heading: line.slice(3).trim(), body: [] } : null;
      if (current) entries.push(current);
      return;
    }
    if (current) current.body.push(line);
  });
  return entries.map(({ heading, body }) => {
    const dates = heading.match(/\d{4}-\d{2}-\d{2}/g);
    const all = body.join('\n');
    const label = /\*\*Outcome[.:]?\*\*/.exec(all);
    const outcome = label ? all.slice(label.index) : body.filter((line) => !/^\s*>/.test(line)).join('\n');
    return { id: nameOf(heading), date: dates ? dates.at(-1) : null, outcome: words(outcome) };
  });
}

/** The path a declared role names, whether written as a path or as `{ path, words }`. */
function declaredPath(entry) {
  if (typeof entry === 'string') return entry;
  if (typeof entry === 'object' && entry !== null && typeof entry.path === 'string') return entry.path;
  return null;
}

const listing = (over) => {
  const width = Math.max(...over.map((entry) => entry.id.length));
  return over.map((entry) => `  ${entry.id.padEnd(width)}  ${entry.words}`);
};

const overShape = (entries, shape) =>
  entries.filter((entry) => entry.words > shape).sort((a, b) => b.words - a.words);

/**
 * Every declared role against its shapes under `root`. `lines` is the report; `failures` is what the
 * configuration could not say, and when there is one nothing is measured, since a shape read wrong would
 * report wrongly.
 */
export function report(root) {
  const configFile = join(root, CONFIG);
  if (!existsSync(configFile)) return { lines: [], failures: [`${CONFIG}: no file, so no shape applies`] };
  const { shapes, errors } = readShapes(readFileSync(configFile, 'utf8'));
  if (errors.length > 0) return { lines: [], failures: errors };

  const manifest = join(root, DECLARED);
  const documents = existsSync(manifest) ? (JSON.parse(readFileSync(manifest, 'utf8')).documents ?? {}) : {};
  const out = [];
  const read = (role) => {
    const path = declaredPath(documents[role]);
    if (path === null) {
      out.push(`doc-shapes: ${role}: none declared`);
      return null;
    }
    const file = join(root, path);
    if (!existsSync(file)) {
      out.push(`doc-shapes: ${role} \`${path}\`: declared, and no file is there (check's failure, not this report's)`);
      return null;
    }
    return { path, text: readFileSync(file, 'utf8') };
  };

  for (const [role, parse, shape] of [['backlog', backlogRows, shapes.backlogRow], ['router', routerRows, shapes.routerRow]]) {
    const doc = read(role);
    if (!doc) continue;
    const rows = parse(doc.text);
    const over = overShape(rows, shape);
    out.push(`doc-shapes: ${role} \`${doc.path}\`: ${over.length} of ${rows.length} rows over ${shape} words`);
    if (over.length > 0) out.push(...listing(over));
  }

  const archive = read('archive');
  if (archive) {
    const entries = archiveEntries(archive.text);
    const cutOver = shapes.archiveCutOver;
    if (cutOver === null) {
      const stand = entries.length === 1 ? 'entry stands' : 'entries stand';
      out.push(`doc-shapes: archive \`${archive.path}\`: no cut-over yet, so its ${entries.length} ${stand} as written`);
    } else {
      const measured = entries.filter((entry) => entry.date !== null && entry.date >= cutOver)
        .map((entry) => ({ id: entry.id, words: entry.outcome }));
      const before = entries.filter((entry) => entry.date !== null && entry.date < cutOver).length;
      const undated = entries.filter((entry) => entry.date === null).length;
      const over = overShape(measured, shapes.archiveOutcome);
      out.push(`doc-shapes: archive \`${archive.path}\`: ${over.length} of ${measured.length} outcomes since ${cutOver} `
        + `over ${shapes.archiveOutcome} words; ${before} before it, standing as written; ${undated} undated, not measured`);
      if (over.length > 0) out.push(...listing(over));
    }
  }
  return { lines: out, failures: [] };
}

if (isMain(import.meta.url)) {
  const { lines: printed, failures } = report(dirname(dirname(fileURLToPath(import.meta.url))));
  if (failures.length > 0) {
    console.error(`doc-shapes: the configuration does not read\n  ${failures.join('\n  ')}`);
    process.exit(1);
  }
  // Advisory: every line prints and the run exits 0. A row named with its count is one somebody can move.
  for (const line of printed) console.log(line);
}

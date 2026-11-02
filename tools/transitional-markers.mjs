/**
 * ONEWAY1ad-i: every kept transitional read is registered, marked, and has a row that deletes it (D228; the oneway design §1.3).
 *
 *   node tools/transitional-markers.mjs        check the driver's register against the markers (exit 0 clean, 1 breach, 2 tool error)
 *
 * An older way kept for an old record or an older build survives only with the condition that ends it and the row that deletes
 * it. `Transitional/Register.cs` lists each one, an entry per `Id = "…"` with its `Row = "…"`, and the kept site carries the
 * comment `// transitional: <id>`. The check fails on a marker naming no entry, an entry with no marker, an id registered twice,
 * and an entry whose deleting row is not open. It reads markers and register entries only: a row named in prose is no marker.
 *
 * Open: a row is open while its id is in `TASKS.md` and `docs/task-archive.md` has no heading `## <ID> —` for it. A
 * `ONEWAY1`-prefixed id also stands in `TASKS.md` as its letters alone inside the ONEWAY1 entry's list of rows, so it is open
 * while that entry is, and closes with its own heading or ONEWAY1's. A marker is read from C# under `src/`, tests and build
 * output skipped, through `blank` so one inside a string or a block comment is never read. There is no .NET build.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { blank } from './partials.mjs';

export const REGISTER = 'src/Daoris.Desktop/Daoris.Desktop.Driver/Transitional/Register.cs';
const SKIPPED = new Set(['bin', 'obj', 'node_modules', '_fixtures']);
const MARKER = /\/\/[ \t]*transitional:[ \t]*([A-Za-z0-9][\w-]*)/g;
const FIELD = /\b(Id|Row)\s*=\s*"([^"\r\n]*)"/g;
const PREFIX = 'ONEWAY1';
const escaped = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const word = (text) => new RegExp(`(?<![\\w-])${escaped(text)}(?![\\w-])`);

/** Whether row `id` is open: named in `tasks` (or, for a `ONEWAY1` row, by its letters in the ONEWAY1 entry) with no heading in `archive`. */
export function isOpen(id, tasks, archive) {
  const archived = (row) => new RegExp(`^##[ \\t]+${escaped(row)}[ \\t]+—`, 'm').test(archive);
  if (archived(id)) return false;
  if (word(id).test(tasks)) return true;
  if (!id.startsWith(PREFIX) || id === PREFIX || archived(PREFIX)) return false;
  const entry = /^[ \t]*- \[ \] \*\*ONEWAY1\b[^\n]*/m.exec(tasks)?.[0] ?? '';
  const rows = entry.slice(entry.indexOf('prefixed') + 1);
  return rows.length > 0 && word(id.slice(PREFIX.length)).test(rows);
}

function* sources(dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIPPED.has(entry.name) && !entry.name.endsWith('.Tests')) yield* sources(full);
    } else if (entry.name.endsWith('.cs')) {
      yield full;
    }
  }
}

/** The entries the register holds, `{ id, row }`, read from its literals where `blank` shows code; `problems` for one without a row. */
function entriesOf(raw, problems) {
  const text = blank(raw);
  const found = [];
  for (const m of raw.matchAll(FIELD)) {
    if (text[m.index] !== m[1][0]) continue;
    if (m[1] === 'Id') found.push({ id: m[2], row: null });
    else if (found.length && found[found.length - 1].row === null) found[found.length - 1].row = m[2];
  }
  for (const { id, row } of found) if (row === null) problems.push(`${REGISTER}: entry "${id}" names no \`Row = "…"\` that deletes it`);
  return found;
}

/** The markers in `raw`, `{ id, line }`, each a line comment in code: one inside a string or a block comment is skipped. */
function markersOf(raw) {
  const found = [];
  for (const m of raw.matchAll(MARKER)) {
    // A sentinel after the prefix survives only where the prefix ends in code; a string or a block comment swallows it.
    if (!blank(`${raw.slice(0, m.index)}\u0001`).endsWith('\u0001')) continue;
    found.push({ id: m[1], line: raw.slice(0, m.index).split('\n').length });
  }
  return found;
}

/** Check the tree under `root`: `{ problems, entries, markers }`, the last two counts. Throws where the register or a record cannot be read. */
export function checkTransitional(root) {
  const problems = [];
  const entries = entriesOf(readFileSync(join(root, REGISTER), 'utf8'), problems);
  const tasks = readFileSync(join(root, 'TASKS.md'), 'utf8');
  const archive = readFileSync(join(root, 'docs', 'task-archive.md'), 'utf8');
  const ids = new Set();
  for (const { id, row } of entries) {
    if (ids.has(id)) problems.push(`${REGISTER}: entry "${id}" is registered twice`);
    ids.add(id);
    if (row !== null && !isOpen(row, tasks, archive)) {
      problems.push(`${REGISTER}: entry "${id}" names row ${row}, which is not open in TASKS.md: delete the read and its marker, or name the row that does`);
    }
  }
  const marked = new Set();
  let markers = 0;
  const register = join(root, REGISTER);
  for (const file of sources(join(root, 'src'))) {
    if (file === register) continue;
    const rel = file.slice(root.length + 1).replaceAll('\\', '/');
    for (const { id, line } of markersOf(readFileSync(file, 'utf8'))) {
      markers++;
      marked.add(id);
      if (!ids.has(id)) problems.push(`${rel}:${line}: marker "${id}" names no entry in ${REGISTER}: register it, or take the marker out`);
    }
  }
  for (const id of ids) if (!marked.has(id)) problems.push(`${REGISTER}: entry "${id}" has no marker \`// transitional: ${id}\` at its kept site`);
  return { problems, entries: entries.length, markers };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, entries, markers } = checkTransitional(root);
    for (const p of problems) console.error(`transitional-markers: ${p}`);
    if (problems.length) return 1;
    console.log(`transitional-markers: clean (${entries} entries, ${markers} markers)`);
    return 0;
  } catch (e) {
    console.error(`transitional-markers: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();

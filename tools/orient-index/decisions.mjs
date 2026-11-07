import { join } from 'node:path';
import { byNumber, declaredDecisions, fenced, isNoteLabel } from '../doc-duplicates.mjs';
import { repositoryFiles as listFiles } from '../fsx.mjs';
import { GENERATED, INDEX, reader, truncate } from './common.mjs';

/**
 * A note's label: the emphasised span that opens its line, when it is dated or in one of the forms the record writes
 * undated (`doc-duplicates`' `isNoteLabel`), cut to a row. Null for any other line.
 */
export function noteLabel(line) {
  const span = noteSpan(line);
  return span === null ? null : truncate(span, 80);
}

/**
 * A note's label in full, whitespace flattened: the emphasised span that opens its line when the line is a note's.
 * The digest cuts it to a row; a mark's `replacing "<label>"` quote names it whole.
 */
export function noteSpan(line) {
  let span = null;
  if (line.startsWith('**')) {
    const end = line.indexOf('**', 2);
    span = end > 0 ? line.slice(2, end) : line.slice(2);
  } else if (line.startsWith('*') && line[1] !== ' ') {
    let end = -1;
    for (let k = 1; k < line.length; k += 1) {
      if (line[k] !== '*') continue;
      if (line[k + 1] === '*' || line[k - 1] === '*') continue;
      end = k;
      break;
    }
    span = end > 0 ? line.slice(1, end) : line.slice(1);
  }
  if (span === null || !span.trim()) return null;
  if (!/\d{4}-\d{2}-\d{2}/.test(span) && !isNoteLabel(line)) return null;
  return span.replace(/\s+/g, ' ').trim();
}

/**
 * One decision file read for the digest: its title and date, its entry's lines before the first note, and each note's.
 * The service's `DecisionNotes` finds and labels the notes by the same rules with code of its own; both are
 * held to `orient-index-fixtures/decision-notes.json`, row for row, here by `orient-index.test.mjs`.
 * A note also keeps its label in full, and whether an emphasised span or a dated heading gave it (`emphasised`).
 */
export function readDecision(id, lines) {
  const inFence = fenced(lines);
  const heading = lines.findIndex((line, i) => !inFence[i] && line.startsWith(`## ${id}`));
  const title = heading >= 0 ? lines[heading].slice(`## ${id}`.length).replace(/^\s*[—–-]\s*/, '') : '';
  const notes = [];
  // The entry's own first paragraph is never a note, even dated (`*Decided 2026-08-05.*`, `**Decision (<task>, …)**`);
  // an entry whose title carries no date is dated there.
  const opening = lines.findIndex((line, i) => i > heading && line.trim() !== '');
  const date = /\((\d{4}-\d{2}-\d{2})\)\s*$/.exec(title)?.[1] ?? (opening >= 0 ? /\d{4}-\d{2}-\d{2}/.exec(lines[opening])?.[0] : null) ?? null;
  lines.forEach((line, i) => {
    if (i <= opening || inFence[i] || (i > 0 && lines[i - 1].trim() !== '')) return;
    const dated = /^###\s+(.+)$/.exec(line);
    const heading = dated && /\d{4}-\d{2}-\d{2}/.test(dated[1]);
    const full = heading ? dated[1].replace(/\s+/g, ' ').trim() : noteSpan(line);
    if (full) notes.push({ start: i, label: truncate(full, 80), full, emphasised: !heading });
  });
  // Each part runs to the line before the next, blank lines at its end left out.
  const until = (from, next) => {
    let end = next - 1;
    while (end > from && !lines[end].trim()) end -= 1;
    return end + 1;
  };
  notes.forEach((note, k) => {
    note.end = until(note.start, k + 1 < notes.length ? notes[k + 1].start : lines.length);
  });
  return {
    id,
    start: heading + 1,
    end: until(Math.max(heading, 0), notes.length ? notes[0].start : lines.length),
    date,
    title: title.replace(/\s*\(\d{4}-\d{2}-\d{2}\)\s*$/, ''),
    notes,
  };
}

/**
 * What a note's label says of a later record: `Amended by D<n>` or
 * `Superseded by D<n>` opening it, the rest free; or, with no decision named, the same decision's own amendment, which
 * carries a `, replacing "<label>"` quote. Null for any other label. `quotes` are the quoted texts the label can be read
 * to hold, longest last: a quoted label may itself hold a quote mark, so the reading tries each place it could end.
 * The service's `DecisionNotes.Marks` reads the same with code of its own, and both are held to
 * `orient-index-fixtures/note-marks.json`.
 */
export function noteMark(label) {
  const named = /^(Amended|Superseded) by D([1-9]\d*)\b/.exec(label);
  const own = named ? null : /^(Amended|Superseded)\b/.exec(label);
  const opener = /,\s*replacing "/.exec(label);
  if (!named && !(own && opener)) return null;
  const quotes = [];
  if (opener) {
    const from = opener.index + opener[0].length;
    for (let at = label.indexOf('"', from); at >= 0; at = label.indexOf('"', at + 1)) quotes.push(label.slice(from, at));
  }
  return { kind: (named ?? own)[1].toLowerCase(), by: named ? Number(named[2]) : null, quotes };
}

/**
 * The marks of one decision as `readDecision` gave it: each emphasised note whose label is a mark, with the 1-based line
 * it starts on, the quote it holds (null for none) and the line of the earlier note whose full label the quote is (null
 * for none). A quote is matched against each earlier note's full label, never the digest's cut one.
 */
export function readMarks(decision) {
  return decision.notes.flatMap((note, k) => {
    const mark = note.emphasised ? noteMark(note.full) : null;
    if (!mark) return [];
    const earlier = decision.notes.slice(0, k);
    const hit = mark.quotes.map((quote) => ({ quote, note: earlier.find((e) => e.full === quote) })).find((m) => m.note);
    const quote = hit?.quote ?? mark.quotes[0] ?? null;
    return [{ line: note.start + 1, kind: mark.kind, by: mark.by, quote, target: hit ? hit.note.start + 1 : null }];
  });
}

/** Every decision file of the tree, read, in their numbers' order. */
function readDecisions(ctx) {
  const folder = declaredDecisions(ctx.root);
  const decisions = ctx.files.filter((path) => path.startsWith(`${folder}/`) && /^D[1-9]\d*\.md$/.test(path.slice(folder.length + 1)))
    .map((path) => path.slice(folder.length + 1)).sort(byNumber)
    .map((name) => readDecision(name.slice(0, -'.md'.length), ctx.lines(`${folder}/${name}`)));
  return { folder, decisions };
}

/**
 * What a mark gets wrong, as `{ file, line, fact }`: a decision it names that is no file of the folder, or
 * is numbered no higher than the one it sits in (a higher number rules out a cycle), and a quote naming no earlier note
 * of the record it sits in. `--check` fails on each, so a record's standing can be read from its marks.
 */
export function markFindings(root, files = listFiles(root)) {
  const { folder, decisions } = readDecisions(reader(root, files.filter((path) => !path.startsWith(`${INDEX}/`))));
  const known = new Set(decisions.map((decision) => decision.id));
  return decisions.flatMap((decision) => readMarks(decision).flatMap((mark) => {
    const file = `${folder}/${decision.id}.md`;
    const own = Number(decision.id.slice(1));
    const found = (fact) => ({ file, line: mark.line, fact });
    const refused = [];
    if (mark.by !== null && !known.has(`D${mark.by}`)) refused.push(found(`names D${mark.by}, which is no decision of this repository`));
    else if (mark.by !== null && mark.by <= own) refused.push(found(`names D${mark.by}, which is not numbered higher than ${decision.id}`));
    if (mark.quote !== null && mark.target === null) refused.push(found(`quotes a label that is no earlier note of ${decision.id}, the full text of one and never the digest's cut`));
    return refused;
  }));
}

/**
 * The digest: every decision's title with its entry's own lines, then every dated note with its lines, so a
 * session reads the decision or the one note it needs and not the file. Titles first, so they read in one go.
 */
export function decisionsPage(ctx) {
  const { folder, decisions } = readDecisions(ctx);
  const titles = decisions.map((decision) => {
    const notes = decision.notes.length ? ` · ${decision.notes.length} note${decision.notes.length === 1 ? '' : 's'}` : '';
    return `- ${decision.id}:${decision.start}-${decision.end}${decision.date ? ` (${decision.date})` : ''} ${truncate(decision.title, 90)}${notes}`;
  });
  const notes = decisions.flatMap((decision) => decision.notes.map((note) => `- ${decision.id}:${note.start + 1}-${note.end} ${note.label}`));
  const head = [
    '# Decisions digest',
    '',
    '', // the sentence below, once the line numbers are known
    '',
    `## Decisions (${decisions.length})`,
    '',
  ];
  const titlesAt = head.length + 1;
  const notesAt = titlesAt + titles.length + 3;
  head[2] = `${GENERATED} \`D<n>:<a>-<b>\` is lines a to b of \`${folder}/D<n>.md\`: read them with offset a and limit b - a + 1. First each decision's title, with the lines of its entry before its notes (here lines ${titlesAt}-${titlesAt + titles.length - 1}); then each dated note, with its lines (from line ${notesAt}). Search for \`D<n>:\` to list one decision's rows.`;
  const out = [...head, ...titles, '', `## Notes (${notes.length})`, '', ...notes];
  if (decisions.length === 0) out.push(`No decision file was found in \`${folder}/\`.`);
  return `${out.join('\n')}\n`;
}

// ---------------------------------------------------------------------------------------------------
// The index's own page

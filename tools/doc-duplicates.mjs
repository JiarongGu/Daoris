#!/usr/bin/env node
/**
 * The append-only records, checked for what a union merge can leave behind.
 *
 * ## Why
 *
 * Eighteen merges in three days collided most on records every branch appends to: the changelog, the
 * decision log, the task archive, the fix log, the documents' index, the twins table
 * (`docs/2026-09-30-parallel-development-design.md` §1). Nothing about the work collided, only the
 * insertion point. MOD1 (D106) marks those files `merge=union` in `.gitattributes`, so two branches adding
 * lines at one place keep both.
 *
 * Union has one cost, and this check is for it: when two branches CHANGE the same line, union keeps both
 * versions, silently. In these records that shows up as a heading, a decision number, a table row or a
 * changelog line that appears twice. So each record is checked for exactly that duplicate.
 *
 * ## The decisions record, a file or a folder
 *
 * It is read where `daoris.json` declares it (`documents.decisions`). As one file it tore in 33 of 134
 * merges, so D134 makes it a folder of one file per decision, merged by union per file
 * (`docs/2026-10-03-decisions-record-design.md`). A file is checked as it always was, for a number twice. A
 * folder is checked for what union can still leave in one decision's file (§3.4, DOC8b): a file not named
 * `D<n>.md`, a heading that is not exactly one and its own, a conflict marker, a note's label glued to the
 * line above it, a note held twice (DUPNOTE1: union keeps a note both sides carried, each copy set apart).
 * And the page the record left at its old path is checked for a decision or a note written into it, which a
 * branch from before the migration would otherwise land there.
 *
 * ## It fails
 *
 * Unlike the doc budgets (D54: a judgement reports), a duplicated decision number or index row is a fact
 * about the file, and a fact gates; so is each of the folder's. Exit 1 names every finding and its file.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

/** The records marked `merge=union` besides the decisions record, and what a duplicate is in each. */
const APPEND_ONLY = [
  { file: 'docs/task-archive.md', kind: 'heading' },
  { file: 'docs/FIX-LOG.md', kind: 'heading' },
  { file: 'docs/README.md', kind: 'row' },
  { file: '.claude/knowledge/twins.md', kind: 'row' },
  { file: 'CHANGELOG.md', kind: 'line' },
];

/**
 * The decisions record's old path. While the record is one file it is the record, and where it is looked for
 * when `daoris.json` declares none; once the record is a folder it is the page that says where the decisions
 * are, and holds none of them (D134 §3.2).
 */
export const PAGE = 'docs/DECISIONS.md';

/** A markdown table's separator row: `|---|:--:|`. */
const SEPARATOR = /^\|(\s*:?-+:?\s*\|)+\s*$/;

/**
 * What appears twice in a record's text, by its kind: a decision's number (`## D106`), a second-level
 * heading, a table row's first cell (header rows skipped: several tables share one header), or a whole
 * list line in the changelog. Each duplicate once, in the order first seen twice.
 */
export function duplicates(text, kind) {
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  const keys = [];
  lines.forEach((line, i) => {
    if (kind === 'decision') {
      const m = /^## (D\d+)\b/.exec(line);
      if (m) keys.push(m[1]);
    } else if (kind === 'heading') {
      if (line.startsWith('## ')) keys.push(line.trimEnd());
    } else if (kind === 'row') {
      // A separator row, and the header above one, are the table's frame, never an entry.
      if (line.startsWith('|') && !SEPARATOR.test(line) && !SEPARATOR.test(lines[i + 1] ?? '')) {
        keys.push(line.split('|')[1].trim());
      }
    } else if (kind === 'line') {
      if (/^- \S/.test(line) && line.length > 20) keys.push(line.trimEnd());
    }
  });
  return duplicateKeys(keys);
}

/** Each key that appears more than once, once, in the order first seen twice. */
function duplicateKeys(keys) {
  const seen = new Set();
  const twice = [];
  for (const key of keys) {
    if (seen.has(key) && !twice.includes(key)) twice.push(key);
    seen.add(key);
  }
  return twice;
}

/**
 * Where `daoris.json` declares the decisions record, `/`-separated and without a trailing slash. The orientation
 * index's digest reads the record from here too (ORIENT1b).
 */
export function declaredDecisions(root) {
  const manifest = join(root, 'daoris.json');
  if (!existsSync(manifest)) return PAGE;
  const entry = JSON.parse(readFileSync(manifest, 'utf8')).documents?.decisions;
  const path = typeof entry === 'string' ? entry : entry?.path;
  return typeof path === 'string' ? path.replace(/\\/g, '/').replace(/\/+$/, '') : PAGE;
}

const isDirectory = (path) => {
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
};

/**
 * Every record marked `merge=union` under `root`, as the attribute names it, and what is checked in it. The
 * decisions record is a file (`kind: decision`, a number twice) or a folder, named as its union pattern
 * `<folder>/*.md` (`kind: decisions`, the folder's facts).
 */
export function records(root) {
  const path = declaredDecisions(root);
  const decisions = isDirectory(join(root, path)) ? { file: `${path}/*.md`, kind: 'decisions' } : { file: path, kind: 'decision' };
  return [decisions, ...APPEND_ONLY];
}

const lines = (text) => text.replace(/\r\n/g, '\n').split('\n');

/**
 * Whether a fenced block holds each line, its fences too: a fence's lines are text, never a heading or a
 * label (as `doc-shapes` reads a fence). The orientation index reads headings and notes the same way.
 */
export function fenced(text) {
  let open = false;
  return text.map((line) => {
    if (/^\s*```/.test(line)) {
      open = !open;
      return true;
    }
    return open;
  });
}

/**
 * A note's label, in the forms the record writes them (D134 §3.4). The design counted 178 notes in sixteen forms
 * (§1.3), each a bold or italic word naming a build, a fix, an amendment or a reading, with its date on the line;
 * and its per-file replay found the commonest left glued to the paragraph above (§2.1), which count with or
 * without a date. A rarer word counts only with a date, so an emphasised sentence that opens with one
 * (`**Proven without the rehearsal**`) is not taken for a note. The service's `DecisionNotes` copies these forms; the
 * digest that reads them and it are held to `orient-index-fixtures/decision-notes.json` (ORIENT1h).
 */
const NOTE_LABEL = [
  /^\*\*(As built|Built|Fixed|Read|Amended)\b/,
  /^\*(As built|Built|Amended)\b/,
  /^\*\*?(Corrected|Measured|Landed|Proved|Proven|Probed|Reviewed|Superseded|Noted|Note|Revised|Decided|Extended|Accepted)\b.*\d{4}-\d{2}-\d{2}/,
  // A note named by its task first: `**DRIFT1a, built 2026-10-02: …**`.
  /^\*\*[^*]*\b(built|landed|amended)\b[^*]*\d{4}-\d{2}-\d{2}/,
];

export const isNoteLabel = (line) => NOTE_LABEL.some((form) => form.test(line));

/**
 * The lines, by index, that are a note's label straight after a non-blank line, outside a fence: what union leaves
 * when two notes written under one decision at once lose the blank line between them (§2.1). The one definition, so
 * the merge tool sets apart exactly what this check refuses (MERGEJOIN1). A line ending in `\r` is blank when the
 * rest of it is.
 */
export function gluedLabels(text) {
  const inFence = fenced(text);
  return text.flatMap((line, i) => (i > 0 && !inFence[i] && text[i - 1].trim() !== '' && isNoteLabel(line) ? [i] : []));
}

/** Each note label held more than once outside a fence, once, in the order first seen twice (DUPNOTE1). */
function twiceLabels(text) {
  const inFence = fenced(text);
  return duplicateKeys(text.flatMap((line, i) => (!inFence[i] && isNoteLabel(line) ? [line.trimEnd()] : [])));
}

/** The `## D<n>` headings outside a fence, as `{ id, line }`. */
function decisionHeadings(text) {
  const inFence = fenced(text);
  return text.flatMap((line, i) => {
    const m = !inFence[i] && /^## (D\d+)\b/.exec(line);
    return m ? [{ id: m[1], line: line.trimEnd() }] : [];
  });
}

/** Decision files in their numbers' order (D2 before D10), any other name after them. */
export const byNumber = (a, b) => {
  const [m, n] = [/^D(\d+)\.md$/.exec(a), /^D(\d+)\.md$/.exec(b)];
  if (m && n) return Number(m[1]) - Number(n[1]) || a.localeCompare(b);
  return m ? -1 : n ? 1 : a.localeCompare(b);
};

/** The files a folder's union pattern `<folder>/*.md` covers, by name, in their numbers' order. */
function markdownIn(root, folder) {
  return readdirSync(join(root, folder), { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith('.md'))
    .map((entry) => entry.name)
    .sort(byNumber);
}

/**
 * What union can leave in a folder of decisions, one file each, and in the page the record left behind
 * (D134 §3.4), as `{ file, fact, key }`: the file, what is wrong, and the line or name it is wrong at.
 */
export function folderFacts(root, folder) {
  const found = [];
  for (const name of markdownIn(root, folder)) {
    const file = `${folder}/${name}`;
    const text = lines(readFileSync(join(root, file), 'utf8'));
    // Union never writes one, but a plain merge of the same file does (§3.3), and so does a hand resolution
    // left half done. Every line, a fence's too: git writes its markers wherever the sides met.
    for (const line of text) if (/^(<<<<<<<|>>>>>>>)( |$)/.test(line)) found.push({ file, fact: 'a conflict marker', key: line.trimEnd() });
    // Two notes written under one decision at once meet in its file, and union keeps both whole; in 12 of the
    // files replayed one lost the blank line above it, so a renderer reads it as the paragraph before (§2.1).
    for (const i of gluedLabels(text)) found.push({ file, fact: 'a note label after a non-blank line', key: text[i].trimEnd() });
    // A note both sides carried is kept twice, each copy after its own blank line, so the glued check never sees it:
    // D150 held PLUGTOOL1a's note twice (DUPNOTE1). A note is named by its label's line, which carries its task and date.
    for (const key of twiceLabels(text)) found.push({ file, fact: 'a note twice', key });
    // The path is the citation (§3.1): `D7.md` is D7, so a padded or slugged name would need a search to find.
    if (!/^D[1-9]\d*\.md$/.test(name)) {
      found.push({ file, fact: 'not named D<n>.md', key: name });
      continue;
    }
    // One decision, its own: a criss-cross that added it on both sides leaves its heading twice, and a note or
    // a decision written into the wrong file brings another's.
    const own = name.slice(0, -'.md'.length);
    const headings = decisionHeadings(text);
    const mine = headings.filter((heading) => heading.id === own);
    if (mine.length === 0) found.push({ file, fact: 'no heading of its own', key: `## ${own}` });
    if (mine.length > 1) found.push({ file, fact: 'its heading twice', key: mine[1].line });
    for (const heading of headings) if (heading.id !== own) found.push({ file, fact: 'a heading not its own', key: heading.line });
  }
  // The page is no longer union-merged (§3.3), so a branch from before the migration that wrote under an old
  // decision conflicts there or lands its lines in it; this refuses the landing (§4 step 4).
  const page = join(root, PAGE);
  if (existsSync(page) && !isDirectory(page)) {
    const text = lines(readFileSync(page, 'utf8'));
    const inFence = fenced(text);
    for (const heading of decisionHeadings(text)) found.push({ file: PAGE, fact: 'a decision in the page', key: heading.line });
    text.forEach((line, i) => {
      if (!inFence[i] && isNoteLabel(line)) found.push({ file: PAGE, fact: 'a note label in the page', key: line.trimEnd() });
    });
  }
  return found;
}

/** Every finding in every record under `root`, as `{ file, fact, key }`; a record that is absent is skipped. */
export function check(root) {
  const found = [];
  for (const { file, kind } of records(root)) {
    if (kind === 'decisions') {
      found.push(...folderFacts(root, file.slice(0, -'/*.md'.length)));
      continue;
    }
    let text;
    try {
      text = readFileSync(join(root, file), 'utf8');
    } catch {
      continue;
    }
    for (const key of duplicates(text, kind)) found.push({ file, fact: 'twice', key });
  }
  return found;
}

if (isMain(import.meta.url)) {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..');
  const found = check(root);
  if (found.length === 0) {
    // A folder says how many files it read, so a check that found none to read cannot pass unseen.
    const folder = records(root).find((record) => record.kind === 'decisions');
    const files = folder ? `, ${markdownIn(root, folder.file.slice(0, -'/*.md'.length)).length} decision files each whole` : '';
    console.log(`doc-duplicates: ${records(root).length} append-only records, nothing twice${files}`);
  } else {
    console.error(`doc-duplicates: ${found.length} finding(s) — what a union merge can leave in a record:`);
    for (const { file, fact, key } of found) console.error(`  ${file}: ${fact}: ${key.length > 110 ? key.slice(0, 110) + '…' : key}`);
    process.exit(1);
  }
}

#!/usr/bin/env node
/**
 * The read-path bench: what a change reads at its start, and what a words-only search returns, on a repository as it
 * has grown against the same repository's present alone.
 *
 * ## Why
 *
 * A long-lived repository developed worse than a fresh start because what a change reads and what a search returns grew
 * with its age. This bench measures both, on the repository's own questions, so each step that files a design or
 * cuts an area can be measured again.
 *
 * ## What it measures
 *
 * - **Present questions**: the right area document first, and within three, by the question bench's floor (Okapi over
 *   every line with its neighbours) over the corpus as it is, over the present and dated documents, and over the
 *   present alone, with the share of all hits that were history.
 * - **History questions**: by identifier, the first file an exact search names (a decision by its file, a row id in the
 *   records first); by words, the floor over the corpus as it is.
 * - **The bytes at a change's start**: the brief, an area document (median and largest), a backlog row (median and
 *   largest) and the index's front page.
 *
 * Each document is present, dated (a file or folder named for its date, not filed) or history (the declared decisions,
 * archive, fix log and changelog, and anything under an `archive` folder), as the knowledge server's search reads it.
 *
 *   node tools/read-path-bench.mjs [--root <checkout>] [--questions <file>] [--json]
 *
 * The questions default to `tools/read-path-bench-questions.json`. The report is printed; exit 0 when it ran, 2 on a
 * tool error. The helpers are tested by `node --test tools/read-path-bench.test.mjs`.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { FLOOR_LIMIT, floorHits, indexPassages } from './question-bench.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));

export const DEFAULT_QUESTIONS = join(HERE, 'read-path-bench-questions.json');

const DATE = /\d{4}-\d{2}-\d{2}/;

const linesOf = (text) => String(text ?? '').replace(/^﻿/, '').replace(/\r\n?/g, '\n').split('\n');

/** The questions file read and checked: `{ present: [{ question, answer }], history: [{ question, by, answer }] }`. */
export function readQuestions(text) {
  const read = JSON.parse(text);
  for (const set of ['present', 'history']) {
    if (!Array.isArray(read[set])) throw new Error(`questions: \`${set}\` is not a list`);
    for (const item of read[set]) {
      if (typeof item?.question !== 'string' || !item.question.trim()) throw new Error(`questions: a ${set} item has no question`);
      if (!Array.isArray(item.answer) || !item.answer.length) throw new Error(`questions: "${item.question}" names no answer`);
      if (set === 'history' && !['id', 'words'].includes(item.by)) throw new Error(`questions: "${item.question}" is by neither id nor words`);
    }
  }
  return read;
}

/** The records a manifest declares, by role: `{ decisions, archive, fixes, changelog }`, each a path or null. */
export function declaredRecords(manifest) {
  const documents = manifest?.documents ?? {};
  const pathOf = (role) => {
    const value = documents[role];
    return typeof value === 'string' ? value : typeof value?.path === 'string' ? value.path : null;
  };
  return { decisions: pathOf('decisions'), archive: pathOf('archive'), fixes: pathOf('fixes'), changelog: pathOf('changelog') };
}

/** Whether a path is `present`, `dated` or `history`, as the knowledge server's search reads it. */
export function tenseOf(path, records) {
  const parts = path.split('/');
  const files = [records.archive, records.fixes, records.changelog].filter(Boolean);
  if (files.includes(path) || (records.decisions && path.startsWith(`${records.decisions}/`))
    || parts.slice(0, -1).some((folder) => folder.toLowerCase() === 'archive')) return 'history';
  return parts.some((part) => DATE.test(part)) ? 'dated' : 'present';
}

/** Every markdown file a search reads: the brief, backlog and changelog, and every one under `docs/` and `.claude/`. */
export function loadCorpus(root) {
  const out = [];
  const add = (path) => out.push({ path, lines: linesOf(readFileSync(join(root, path), 'utf8')) });
  const walk = (at) => {
    if (!existsSync(join(root, at))) return;
    for (const entry of readdirSync(join(root, at), { withFileTypes: true })) {
      const path = `${at}/${entry.name}`;
      if (entry.isDirectory()) walk(path);
      else if (entry.name.endsWith('.md')) add(path);
    }
  };
  for (const file of ['AGENTS.md', 'TASKS.md', 'CHANGELOG.md']) if (existsSync(join(root, file))) add(file);
  walk('docs');
  walk('.claude');
  return out.sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0));
}

/** The first file an exact search names for an identifier: a decision's own file, else the records first. */
export function exactLookup(id, corpus, records) {
  if (/^D\d+$/.test(id) && records.decisions) {
    const own = `${records.decisions}/${id}.md`;
    if (corpus.some((doc) => doc.path === own)) return own;
  }
  const word = new RegExp(`(^|[^A-Za-z0-9])${id.replace(/[.*+?^${}()|[\]\\-]/g, '\\$&')}([^A-Za-z0-9]|$)`);
  const rank = (doc) => (tenseOf(doc.path, records) === 'history' ? 0 : 1);
  const ordered = [...corpus].sort((a, b) => rank(a) - rank(b));
  return ordered.find((doc) => doc.lines.some((line) => word.test(line)))?.path ?? null;
}

/** A question set scored by the floor over one index: right first, right within three, and the hits' tenses. */
export function scoreFloor(items, index, records) {
  let first = 0;
  let withinThree = 0;
  let history = 0;
  let hits = 0;
  const rows = items.map((item) => {
    const found = floorHits(item.question, index);
    const place = found.findIndex((hit) => item.answer.includes(hit.path));
    if (place === 0) first++;
    if (place >= 0 && place < FLOOR_LIMIT) withinThree++;
    hits += found.length;
    history += found.filter((hit) => tenseOf(hit.path, records) === 'history').length;
    return { question: item.question, place: place + 1, first: found[0]?.path ?? null };
  });
  return { first, withinThree, history, hits, count: items.length, rows };
}

const median = (values) => {
  if (!values.length) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor((sorted.length - 1) / 2)];
};

/** The bytes a change reads at its start: the brief, its area, its row and the index's front page. */
export function bytesAtStart(root, manifest, size = (path) => statSync(join(root, path)).size, read = (path) => readFileSync(join(root, path), 'utf8')) {
  const documents = manifest?.documents ?? {};
  const areasDir = typeof documents.areas === 'string' ? documents.areas : documents.areas?.path;
  const backlog = typeof documents.backlog === 'string' ? documents.backlog : documents.backlog?.path;
  const index = typeof documents.index === 'string' ? documents.index : documents.index?.path;
  const has = (path) => path && existsSync(join(root, path));
  const areas = has(areasDir)
    ? readdirSync(join(root, areasDir)).filter((name) => name.endsWith('.md') && name !== 'README.md').map((name) => size(`${areasDir}/${name}`))
    : [];
  const rows = has(backlog)
    ? read(backlog).split(/\r?\n(?=- \[ \] )/).filter((row) => row.startsWith('- [ ] ')).map((row) => Buffer.byteLength(row))
    : [];
  const brief = has('AGENTS.md') ? size('AGENTS.md') : 0;
  const front = has(index) ? size(index) : 0;
  const area = { median: median(areas), largest: Math.max(0, ...areas) };
  const row = { median: median(rows), largest: Math.max(0, ...rows) };
  return {
    brief, area, row, index: front,
    atMedian: brief + area.median + row.median + front,
    atLargest: brief + area.largest + row.largest + front,
  };
}

/** The whole bench over one checkout. */
export function runBench(root, questions) {
  const manifest = JSON.parse(readFileSync(join(root, 'daoris.json'), 'utf8'));
  const records = declaredRecords(manifest);
  const corpus = loadCorpus(root);
  const by = (tenses) => corpus.filter((doc) => tenses.includes(tenseOf(doc.path, records)));
  const sizeOf = (docs) => docs.reduce((n, doc) => n + Buffer.byteLength(doc.lines.join('\n')), 0);
  const corpora = {
    'as it is': corpus,
    'less history': by(['present', 'dated']),
    'the present': by(['present']),
  };
  const present = Object.fromEntries(Object.entries(corpora).map(([name, docs]) => [name, {
    files: docs.length, bytes: sizeOf(docs), ...scoreFloor(questions.present, indexPassages(docs), records),
  }]));
  const all = indexPassages(corpus);
  const ids = questions.history.filter((item) => item.by === 'id');
  const idRows = ids.map((item) => ({ question: item.question, first: exactLookup(item.question, corpus, records) }));
  const history = {
    byId: { right: idRows.filter((row, i) => ids[i].answer.includes(row.first)).length, count: ids.length, rows: idRows },
    byWords: scoreFloor(questions.history.filter((item) => item.by === 'words'), all, records),
  };
  const docsTop = corpus.filter((doc) => /^docs\/[^/]+$/.test(doc.path) && DATE.test(doc.path));
  const filed = corpus.filter((doc) => /^docs\/archive\//.test(doc.path));
  return {
    present, history, start: bytesAtStart(root, manifest),
    documents: { datedAtTop: docsTop.length, datedAtTopBytes: sizeOf(docsTop), filed: filed.length },
  };
}

const kb = (bytes) => `${(bytes / 1024).toFixed(1)} KB`;

/** The report as markdown. */
export function renderReport(result) {
  const out = ['# Read-path bench', '', '## Present questions, by the floor', '',
    '| Corpus | Files | Size | Top hit | Within three | Hits from history |', '|---|---|---|---|---|---|'];
  for (const [name, r] of Object.entries(result.present)) {
    out.push(`| ${name} | ${r.files} | ${(r.bytes / 1048576).toFixed(1)} MB | ${r.first}/${r.count} | ${r.withinThree}/${r.count} | ${r.history}/${r.hits} |`);
  }
  const { byId, byWords } = result.history;
  out.push('', '## History questions', '', `- By identifier, an exact search: ${byId.right}/${byId.count} at the first file named.`,
    `- By words, the floor as it is: ${byWords.first}/${byWords.count} top, ${byWords.withinThree}/${byWords.count} within three.`);
  const s = result.start;
  out.push('', '## The bytes at a change\'s start', '',
    `The brief ${kb(s.brief)}, an area ${kb(s.area.median)} median and ${kb(s.area.largest)} the largest, a row ${kb(s.row.median)}`
    + ` median and ${kb(s.row.largest)} the largest, the index's front page ${kb(s.index)}: **${kb(s.atMedian)} at the median, ${kb(s.atLargest)} at the largest.**`,
    '', `Dated documents at \`docs/\`'s top: ${result.documents.datedAtTop} (${(result.documents.datedAtTopBytes / 1048576).toFixed(1)} MB); filed under \`docs/archive/\`: ${result.documents.filed}.`);
  return `${out.join('\n')}\n`;
}

function argOf(argv, flag, fallback) {
  const at = argv.indexOf(flag);
  return at >= 0 && argv[at + 1] ? argv[at + 1] : fallback;
}

export function main(argv, { out = (text) => process.stdout.write(text), err = (line) => console.error(line) } = {}) {
  try {
    const root = argOf(argv, '--root', process.cwd());
    const questions = readQuestions(readFileSync(argOf(argv, '--questions', DEFAULT_QUESTIONS), 'utf8'));
    const result = runBench(root, questions);
    out(argv.includes('--json') ? `${JSON.stringify(result, null, 2)}\n` : renderReport(result));
    return 0;
  } catch (error) {
    err(`read-path-bench: ${error.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

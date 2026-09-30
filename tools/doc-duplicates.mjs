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
 * ## It fails
 *
 * Unlike the doc budgets (D54: a judgement reports), a duplicated decision number or index row is a fact
 * about the file, and a fact gates. Exit 1 names every duplicate and its file.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

/** The records marked `merge=union`, and what a duplicate is in each. */
export const RECORDS = [
  { file: 'docs/DECISIONS.md', kind: 'decision' },
  { file: 'docs/task-archive.md', kind: 'heading' },
  { file: 'docs/FIX-LOG.md', kind: 'heading' },
  { file: 'docs/README.md', kind: 'row' },
  { file: '.claude/knowledge/twins.md', kind: 'row' },
  { file: 'CHANGELOG.md', kind: 'line' },
];

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
  const seen = new Set();
  const twice = [];
  for (const key of keys) {
    if (seen.has(key) && !twice.includes(key)) twice.push(key);
    seen.add(key);
  }
  return twice;
}

/** Every duplicate in every record under `root`, as `{ file, key }`; a record that is absent is skipped. */
export function check(root) {
  const found = [];
  for (const { file, kind } of RECORDS) {
    let text;
    try {
      text = readFileSync(join(root, file), 'utf8');
    } catch {
      continue;
    }
    for (const key of duplicates(text, kind)) found.push({ file, key });
  }
  return found;
}

if (isMain(import.meta.url)) {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..');
  const found = check(root);
  if (found.length === 0) {
    console.log(`doc-duplicates: ${RECORDS.length} append-only records, nothing twice`);
  } else {
    console.error(`doc-duplicates: ${found.length} duplicate(s) — a union merge kept both versions of a changed line:`);
    for (const { file, key } of found) console.error(`  ${file}: ${key.length > 110 ? key.slice(0, 110) + '…' : key}`);
    process.exit(1);
  }
}

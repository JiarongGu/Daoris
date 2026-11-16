#!/usr/bin/env node
// The UX trail's terminal reader (UXTRAIL1): `node tools/ux-trail.mjs <home's ux-trail folder or a day's .jsonl>` prints a trail's
// summary: how many pages and presses, and for each row that waited on the person, the time from its wait to their first press in that
// row, with the pages opened between; a wait never pressed says so. The trail is the person's, kept on their desktop when they turn it on.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

/** The entries of a folder's day files in order, or of one file; a line that does not read is passed over. */
export function readTrail(path) {
  const files = statSync(path).isDirectory()
    ? readdirSync(path).filter((name) => name.endsWith('.jsonl')).sort().map((name) => join(path, name))
    : [path];
  return files.flatMap((file) => readFileSync(file, 'utf8').split('\n').flatMap((line) => {
    try { return line.trim() ? [JSON.parse(line)] : []; } catch { return []; }
  }));
}

/** The summary of `entries`: pages and presses counted, and each wait with its seconds to the press in its row and the pages between. */
export function summarise(entries) {
  const seconds = (from, to) => Math.round((Date.parse(to) - Date.parse(from)) / 1000);
  const waits = entries.flatMap((wait, index) => {
    if (wait.kind !== 'wait') return [];
    const after = entries.slice(index + 1);
    const pressAt = after.findIndex((entry) => entry.kind === 'press' && entry.row === wait.row);
    const between = (pressAt < 0 ? [] : after.slice(0, pressAt)).filter((entry) => entry.kind === 'page').map((entry) => entry.page);
    const press = pressAt < 0 ? null : after[pressAt];
    return [{ row: wait.row, seconds: press ? seconds(wait.since ?? wait.at, press.at) : null, press: press?.press ?? null, pages: between }];
  });
  return {
    pages: entries.filter((entry) => entry.kind === 'page').length,
    presses: entries.filter((entry) => entry.kind === 'press').length,
    waits,
  };
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const path = process.argv[2];
  if (!path) {
    console.error('usage: node tools/ux-trail.mjs <ux-trail folder | day.jsonl>');
    process.exit(2);
  }
  const summary = summarise(readTrail(path));
  console.log(`${summary.pages} pages opened, ${summary.presses} presses.`);
  for (const wait of summary.waits) {
    console.log(wait.press === null
      ? `${wait.row}: never pressed here.`
      : `${wait.row}: ${wait.seconds} s to “${wait.press}”, ${wait.pages.length} pages between${wait.pages.length ? ` (${wait.pages.join(' → ')})` : ''}.`);
  }
}

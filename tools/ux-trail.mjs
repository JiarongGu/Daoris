#!/usr/bin/env node
// The UX trail's terminal reader:`node tools/ux-trail.mjs <home's ux-trail folder or a day's .jsonl>` prints a trail's
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

/** The attention kinds that may wait on the owner (design §5.2, the conditional ones included); a row is `kind:id`. */
export const OWNER_KINDS = [
  'signed-out', 'git-held', 'account-wait', 'confirmation', 'go-ahead', 'git-ask', 'trust', 'space', 'step', 'set-up',
  'integration', 'departure', 'intake', 'plan-question', 'design-question', 'unanswerable', 'policy-hold', 'memory',
];

const HOUR = 3600 * 1000;

/** The entries whose UTC day lies from `from` to `to` (YYYY-MM-DD, either may be missing), both included. */
export function inRange(entries, from, to) {
  return entries.filter((entry) => {
    const day = String(entry.at ?? '').slice(0, 10);
    return (!from || day >= from) && (!to || day <= to);
  });
}

/**
 * The trial's five measures (design §17) from a trail in order: a row waits once, from its first `wait`, and its answer is its first
 * press; a return follows `away` hours (2) of silence with waits open. `returns`, `screens`, `outside`, `changed` and `amber` as §17 says.
 */
export function measures(entries, { ownerKinds = OWNER_KINDS, away = 2 } = {}) {
  const time = (entry) => Date.parse(entry.at);
  const kindOf = (row) => row.split(':')[0];
  const waitAt = new Map();
  for (const [index, entry] of entries.entries()) if (entry.kind === 'wait' && !waitAt.has(entry.row)) waitAt.set(entry.row, index);
  const pressOf = (row, from = 0) => {
    const start = waitAt.get(row);
    return entries.slice(Math.max(start ?? 0, from)).find((entry) => entry.kind === 'press' && entry.row === row);
  };
  const gaps = entries.flatMap((entry, index) => (index > 0 && time(entry) - time(entries[index - 1]) >= away * HOUR ? [{ index, at: time(entry) }] : []));
  const resets = [...gaps, ...entries.flatMap((entry, index) => (entry.kind === 'start' ? [{ index, at: time(entry) }] : []))];

  const returns = gaps.flatMap((reset) => {
    const open = [...waitAt].filter(([row, index]) => {
      const wait = entries[index];
      if (time(wait) > reset.at + 60 * 1000 || Date.parse(wait.since ?? wait.at) >= reset.at) return false;
      const gone = entries.find((entry) => (entry.kind === 'press' || entry.kind === 'cleared') && entry.row === row && index < entries.indexOf(entry));
      return !gone || time(gone) >= reset.at;
    }).map(([row]) => row);
    if (open.length === 0) return [];
    const answers = open.flatMap((row) => pressOf(row, reset.index) ?? []);
    const last = Math.max(...answers.map(time));
    return [{ at: entries[reset.index].at, open: open.length, answered: answers.length, seconds: answers.length ? Math.round((last - reset.at) / 1000) : null }];
  });

  const answers = [...waitAt].flatMap(([row, index]) => {
    const press = pressOf(row);
    return press ? [{ row, wait: entries[index], press }] : [];
  }).sort((a, b) => time(a.press) - time(b.press));
  const perAnswer = answers.map((answer, n) => {
    const before = n > 0 ? time(answers[n - 1].press) : -Infinity;
    const reset = Math.max(-Infinity, ...resets.map((r) => r.at).filter((at) => at <= time(answer.press)));
    const from = Math.max(time(answer.wait), before, reset);
    return entries.filter((entry) => entry.kind === 'page' && time(entry) > from && time(entry) < time(answer.press)).length;
  });
  const changed = answers.filter(({ row, wait, press }) =>
    entries.some((entry) => entry.kind === 'changed' && entry.row === row && time(entry) >= time(wait) && time(entry) < time(press))).map(({ row }) => row);

  const rows = [...waitAt.keys()];
  const outside = rows.filter((row) => !ownerKinds.includes(kindOf(row)));
  const needless = rows.filter((row) => {
    if (outside.includes(row)) return true;
    const cleared = entries.find((entry, index) => entry.kind === 'cleared' && entry.row === row && index > waitAt.get(row));
    const press = pressOf(row);
    return cleared && !(press && time(press) < time(cleared));
  });
  return {
    returns,
    screens: { answers: answers.length, perAnswer, withoutOpening: perAnswer.filter((pages) => pages === 0).length },
    outside: { waits: rows.length, outside: outside.length, rows: outside },
    changed: { answers: answers.length, onChanged: changed.length, rows: changed },
    amber: { waits: rows.length, needless: needless.length, rows: needless },
  };
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const args = process.argv.slice(2);
  const option = (name) => { const at = args.indexOf(name); return at < 0 ? undefined : args.splice(at, 2)[1]; };
  const [from, to] = [option('--from'), option('--to')];
  const path = args[0];
  if (!path) {
    console.error('usage: node tools/ux-trail.mjs <ux-trail folder | day.jsonl> [--from YYYY-MM-DD] [--to YYYY-MM-DD]');
    process.exit(2);
  }
  const entries = inRange(readTrail(path), from, to);
  const summary = summarise(entries);
  const trial = measures(entries);
  const share = (part, whole) => `${part} of ${whole}`;
  console.log(`The trial's five measures, ${from ?? 'the first day'} to ${to ?? 'the last'}:`);
  console.log(`1. Return to the last wait answered: ${trial.returns.length === 0 ? 'no return after 2 h with waits open'
    : trial.returns.map((r) => `${r.at}: ${r.seconds === null ? 'none answered' : `${r.seconds} s`} (${r.answered} of ${r.open} open answered)`).join('; ')}.`);
  console.log(`2. Screens opened per answer: ${trial.screens.answers === 0 ? 'no answers' : `${(trial.screens.perAnswer.reduce((a, b) => a + b, 0) / trial.screens.answers).toFixed(2)} on average, ${share(trial.screens.withoutOpening, trial.screens.answers)} with none`}.`);
  console.log(`3. Waits outside the owner kinds: ${share(trial.outside.outside, trial.outside.waits)}${trial.outside.rows.length ? ` (${trial.outside.rows.join(', ')})` : ''}.`);
  console.log(`4. Answers on a changed question: ${share(trial.changed.onChanged, trial.changed.answers)}.`);
  console.log(`5. Amber that did not need the owner: ${share(trial.amber.needless, trial.amber.waits)}.`);
  console.log('');
  console.log(`${summary.pages} pages opened, ${summary.presses} presses.`);
  for (const wait of summary.waits) {
    console.log(wait.press === null
      ? `${wait.row}: never pressed here.`
      : `${wait.row}: ${wait.seconds} s to “${wait.press}”, ${wait.pages.length} pages between${wait.pages.length ? ` (${wait.pages.join(' → ')})` : ''}.`);
  }
}

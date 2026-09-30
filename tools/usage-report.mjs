#!/usr/bin/env node
/**
 * The usage report (LOG1d, D94): an install's machine log, summarised for a development session.
 *
 *   node tools/usage-report.mjs --home <dir> [--days 7] [--json]
 *   node tools/usage-report.mjs --install <dir> [--days 7] [--json]     (the install's `data/` home)
 *
 * ## Why
 *
 * The machine log (`docs/2026-09-30-machine-log-design.md`) exists so Daoris can be improved from real
 * use rather than from a guess. Reading thirty days of JSON lines is not how anyone starts a session, so
 * this reads them for the period and says, in a terminal's width: what was used most (views opened,
 * commands run, panels moved), how the conversations and sessions went (how many, by kind and adapter,
 * the median and slowest open and first answer, how long turns took and how they ended), what was
 * refused (by code, most first), what failed (exceptions, the page's errors, error-level log lines,
 * failed or slow requests, grouped) and the lifecycle (starts, stops, uptime, versions). `--json` is the
 * same as data.
 *
 * ## What it never prints
 *
 * **Anyone's words.** There are none in the log (D94 §5), and this adds none: it counts and times. The
 * one free text a line carries, a caught error's message, is shown as its first line and never beyond
 * the log's own 120-character cap, so a stack or a paragraph cannot reach the screen through it.
 *
 * ## A twin
 *
 * The driver's `MachineLogReader` reads the same lines with its own code; the two share none, and the
 * format is the contract (§3). What is a line and what is skipped is one table, held by both tests:
 * `MachineLogReaderTests.Parsing` and `usage-report.test.ts`. Change one and the other changes with it.
 *
 * It reads; it writes nothing, and nothing leaves the machine. Point it at a copy when the install is
 * running, or at the live folder: every file is opened for reading only.
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from './fsx.mjs';
// The install's layout, from the script that makes it: its home is `data/` beside the launcher.
import { HOME } from './desktop-publish.mjs';

export const DAY_MS = 86_400_000;

/** The longest a caught error's message is shown: the log's own cap (`LogModule.MaxText`). */
export const SAMPLE = 120;

export const USAGE = 'usage: node tools/usage-report.mjs (--home <dir> | --install <dir>) [--days <n>] [--json]';

const FILE_NAME = /^(\d{4}-\d{2}-\d{2})\.([a-z]+)\.jsonl$/;

// UTC, as the writers write it. A time with no zone would be local time to Date.parse and UTC to the
// driver's reader, so neither twin reads one.
const TIME_SHAPE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$/;

const named = (value) => (typeof value === 'string' && value.length > 0 ? value : null);

/**
 * One line of a file, or null when it is not one the format describes: a JSON object whose `time` is UTC
 * in the §3 shape and whose `source`, `level` and `event` are strings. Data that is absent or no object
 * is none; a field nobody knows is ignored.
 */
export function parseLine(text) {
  let root;
  try {
    root = JSON.parse(text);
  } catch {
    return null;
  }
  if (root === null || typeof root !== 'object' || Array.isArray(root)) return null;
  const stamp = named(root.time);
  if (!stamp || !TIME_SHAPE.test(stamp)) return null;
  const time = Date.parse(stamp);
  const source = named(root.source);
  const level = named(root.level);
  const event = named(root.event);
  if (Number.isNaN(time) || !source || !level || !event) return null;
  const data = root.data !== null && typeof root.data === 'object' && !Array.isArray(root.data) ? root.data : {};
  return { time, stamp: new Date(time).toISOString(), source, level, event, data };
}

/**
 * Every line in `folder` at or after `since`, merged by time, and how many could not be read. Only the
 * log's own files are opened, and a day's file older than `since` is not.
 */
export function readLogs(folder, { since } = {}) {
  if (!existsSync(folder)) return { lines: [], skipped: 0 };
  const oldest = since ? since.toISOString().slice(0, 10) : null;
  const files = readdirSync(folder)
    .map((name) => ({ name, match: FILE_NAME.exec(name) }))
    .filter(({ match }) => match
      && new Date(`${match[1]}T00:00:00Z`).toISOString().slice(0, 10) === match[1]
      && (!oldest || match[1] >= oldest))
    .sort((a, b) => a.match[1].localeCompare(b.match[1]) || a.match[2].localeCompare(b.match[2]));

  const lines = [];
  let skipped = 0;
  for (const { name } of files) {
    let text;
    try {
      text = readFileSync(join(folder, name), 'utf8');
    } catch {
      // A file pruned or held between the listing and the read is passed over.
      continue;
    }
    for (const raw of text.split(/\r?\n/)) {
      if (raw.trim() === '') continue;
      const line = parseLine(raw);
      if (!line) {
        skipped++;
        continue;
      }
      if (!since || line.time >= since.getTime()) lines.push(line);
    }
  }
  // A stable sort: lines of one moment keep their day-then-source order.
  lines.sort((a, b) => a.time - b.time);
  return { lines, skipped };
}

/** `--home <dir>` or `--install <dir>` (its `data/`), `--days <n>` (7), `--json`. Throws a sentence. */
export function parseArguments(argv) {
  let home = null;
  let install = null;
  let days = 7;
  let json = false;
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    const value = argv[i + 1];
    switch (flag) {
      case '--home':
      case '--install':
        if (!value || value.startsWith('--')) throw new Error(`${flag} takes a folder`);
        if (flag === '--home') home = value;
        else install = value;
        i++;
        break;
      case '--days':
        if (!value || !/^[1-9]\d{0,3}$/.test(value)) throw new Error('--days takes a whole number of days, such as 7');
        days = Number(value);
        i++;
        break;
      case '--json':
        json = true;
        break;
      default:
        throw new Error(`\`${flag}\` is not an option`);
    }
  }
  if (home && install) throw new Error('name one of --home and --install, not both');
  if (!home && !install) throw new Error('name the log to read: --home <dir> or --install <dir>');
  return { home: home ?? join(install, HOME), days, json };
}

/** The middle value, or the mean of the two middles; null for none. */
export function median(values) {
  if (values.length === 0) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1 ? sorted[middle] : Math.round((sorted[middle - 1] + sorted[middle]) / 2);
}

/** A tally, most first, a tie by name: `[{ name, count }]`. */
function counted(names) {
  const tally = new Map();
  for (const name of names) tally.set(name, (tally.get(name) ?? 0) + 1);
  return [...tally].map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
}

/**
 * A time field across lines: how many were timed, the median, the slowest and whose. A time that could
 * not be known is null in the log and is left out here: absent is never zero.
 */
function timing(lines, field) {
  const timed = lines.filter((line) => typeof line.data[field] === 'number' && Number.isFinite(line.data[field]) && line.data[field] >= 0);
  const slowest = timed.reduce((worst, line) => (!worst || line.data[field] > worst.data[field] ? line : worst), null);
  return {
    count: timed.length,
    medianMs: median(timed.map((line) => line.data[field])),
    slowestMs: slowest ? slowest.data[field] : null,
    slowestSession: slowest ? named(slowest.data.session) : null,
  };
}

/** A caught error's message as the report shows it: its first line, cut at the log's own cap. */
function sampleOf(message) {
  if (typeof message !== 'string' || message.trim() === '') return null;
  const first = message.split(/\r?\n/)[0].trim();
  return first.length > SAMPLE ? `${first.slice(0, SAMPLE)}…` : first;
}

/** `0.0.1+abcdef1234567890` as `0.0.1+abcdef12`: the commit is a prefix's worth. */
function shortVersion(version) {
  const [release, commit] = version.split('+');
  return commit ? `${release}+${commit.slice(0, 8)}` : release;
}

/** What failed, grouped by what it was: most first, a tie in the order each first happened. */
function failures(lines) {
  const groups = new Map();
  const note = (line, key, what, sample) => {
    const group = groups.get(key) ?? { event: line.event, source: line.source, what, count: 0, sample, slowestMs: null };
    group.count++;
    if (line.event === 'request.failed' && typeof line.data.ms === 'number') {
      group.slowestMs = Math.max(group.slowestMs ?? 0, line.data.ms);
    }
    groups.set(key, group);
  };

  for (const line of lines) {
    const { data } = line;
    switch (line.event) {
      case 'error': {
        const what = `${named(data.type) ?? 'an exception'} in ${named(data.where) ?? 'an unnamed place'}`;
        note(line, `error|${line.source}|${what}`, what, sampleOf(data.message));
        break;
      }
      case 'page.error': {
        const what = `the page (${named(data.where) ?? 'somewhere'})`;
        const sample = sampleOf(data.message);
        note(line, `page.error|${what}|${sample}`, what, sample);
        break;
      }
      case 'log': {
        if (line.level !== 'error') break;
        const what = named(data.category) ?? 'an unnamed category';
        note(line, `log|${line.source}|${what}`, what, sampleOf(data.message));
        break;
      }
      case 'request.failed': {
        const what = `${named(data.method) ?? '?'} ${named(data.route) ?? '?'} ${data.status ?? '?'}`;
        note(line, `request.failed|${line.source}|${what}`, what, null);
        break;
      }
      default:
        break;
    }
  }
  return [...groups.values()].sort((a, b) => b.count - a.count);
}

/** The period's lines, summarised. Counts and times only: no line's words are carried. */
export function summarise(lines, { from, to, skipped = 0 }) {
  const of = (event) => lines.filter((line) => line.event === event);
  const field = (event, name) => of(event).map((line) => named(line.data[name])).filter(Boolean);

  const lifecycle = new Map();
  for (const line of lines) {
    if (line.event !== 'app.started' && line.event !== 'app.stopped') continue;
    const entry = lifecycle.get(line.source) ?? { source: line.source, starts: 0, stops: 0, uptimeSeconds: 0, versions: [] };
    if (line.event === 'app.started') {
      entry.starts++;
      const version = named(line.data.version);
      if (version && !entry.versions.includes(shortVersion(version))) entry.versions.push(shortVersion(version));
    } else {
      entry.stops++;
      if (typeof line.data.uptimeSeconds === 'number') entry.uptimeSeconds += line.data.uptimeSeconds;
    }
    lifecycle.set(line.source, entry);
  }

  const refused = new Map();
  for (const line of of('refused')) {
    const code = named(line.data.code) ?? 'an unnamed code';
    const entry = refused.get(code) ?? { code, count: 0, requests: [] };
    entry.count++;
    entry.requests.push(named(line.data.request) ?? 'an unnamed request');
    refused.set(code, entry);
  }

  const turns = of('turn.ended');
  const settled = of('proposal.settled');
  return {
    period: { from: from.toISOString(), to: to.toISOString(), days: Math.round((to - from) / DAY_MS) },
    lines: lines.length,
    skipped,
    lifecycle: [...lifecycle.values()].sort((a, b) => b.starts - a.starts || a.source.localeCompare(b.source)),
    used: {
      views: counted(field('view.opened', 'view')),
      commands: counted(field('command.run', 'command')),
      panels: counted(of('panel.moved').map((line) => `${named(line.data.view) ?? '?'} → ${named(line.data.region) ?? '?'}`)),
    },
    sessions: {
      started: of('session.started').length,
      byKind: counted(field('session.started', 'kind')),
      byAdapter: counted(field('session.started', 'adapter')),
      opened: timing(of('session.opened'), 'openMs'),
      answered: timing(of('turn.answered'), 'firstAnswerMs'),
      turns: {
        ended: turns.length,
        ...timing(turns, 'turnMs'),
        byStopReason: counted(turns.map((line) => named(line.data.stopReason) ?? 'unknown')),
      },
      ended: counted(field('session.ended', 'state')),
      messages: { sent: of('message.sent').length, byKind: counted(field('message.sent', 'kind')) },
      proposals: {
        applied: settled.filter((line) => line.data.applied === true).length,
        declined: settled.filter((line) => line.data.applied === false).length,
      },
    },
    refused: [...refused.values()]
      .sort((a, b) => b.count - a.count)
      .map((entry) => ({ ...entry, requests: counted(entry.requests) })),
    failed: failures(lines),
  };
}

/** A span in milliseconds, as a person reads it: 412 ms · 5.8 s · 3m 10s. */
function duration(ms) {
  if (ms < 1000) return `${ms} ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)} s`;
  return `${Math.floor(ms / 60_000)}m ${Math.floor((ms % 60_000) / 1000)}s`;
}

/** An uptime in seconds: 45s · 12m 3s · 2h 12m. */
function uptime(seconds) {
  if (seconds < 60) return `${seconds}s`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
  return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m`;
}

const plural = (count, one, other) => `${count} ${count === 1 ? one : other}`;
const listed = (items) => items.map((item) => `${item.name} ${item.count}`).join(' · ');
const minute = (iso) => iso.slice(0, 16).replace('T', ' ');

function timed(label, value) {
  if (value.count === 0) return `  ${label.padEnd(15)}none timed`;
  const whose = value.slowestSession ? ` (${value.slowestSession})` : '';
  return `  ${label.padEnd(15)}median ${duration(value.medianMs)} · slowest ${duration(value.slowestMs)}${whose} · ${value.count} timed`;
}

/** The report as text, for a terminal: one heading a section, and `none` where a section holds nothing. */
export function render(report, folder) {
  const out = [`Daoris usage, ${minute(report.period.from)} → ${minute(report.period.to)} UTC (${report.period.days} days)`];
  if (report.lines === 0) {
    out.push('No lines in this period.');
  } else {
    const skipped = report.skipped === 0
      ? '.'
      : `; ${report.skipped} could not be read and ${report.skipped === 1 ? 'was' : 'were'} skipped.`;
    out.push(`${plural(report.lines, 'line', 'lines')} read from ${folder}${skipped}`);
  }

  out.push('', 'Lifecycle');
  if (report.lifecycle.length === 0) out.push('  none');
  for (const entry of report.lifecycle) {
    const parts = [plural(entry.starts, 'start', 'starts')];
    if (entry.stops > 0) parts.push(plural(entry.stops, 'stop', 'stops'), `${uptime(entry.uptimeSeconds)} up`);
    if (entry.versions.length > 0) parts.push(entry.versions.join(', '));
    out.push(`  ${entry.source.padEnd(9)} ${parts.join(' · ')}`);
  }

  const { used, sessions } = report;
  out.push('', 'Used most');
  if (used.views.length + used.commands.length + used.panels.length === 0) {
    out.push('  none');
  } else {
    out.push(`  views      ${listed(used.views) || 'none'}`);
    out.push(`  commands   ${listed(used.commands) || 'none'}`);
    out.push(`  panels     ${listed(used.panels) || 'none'}`);
  }

  out.push('', 'Conversations and sessions');
  const anySession = sessions.started + sessions.opened.count + sessions.answered.count + sessions.turns.ended
    + sessions.ended.length + sessions.messages.sent + sessions.proposals.applied + sessions.proposals.declined;
  if (anySession === 0) {
    out.push('  none');
  } else {
    out.push(`  ${'started'.padEnd(15)}${sessions.started}${sessions.byKind.length ? ` — ${listed(sessions.byKind)}` : ''}`);
    if (sessions.byAdapter.length) out.push(`  ${'adapters'.padEnd(15)}${listed(sessions.byAdapter)}`);
    out.push(timed('opening', sessions.opened));
    out.push(timed('first answer', sessions.answered));
    const turnTimes = timed('', sessions.turns).trimStart();
    out.push(`  ${'turns'.padEnd(15)}${sessions.turns.ended} ended`
      + `${sessions.turns.byStopReason.length ? ` — ${listed(sessions.turns.byStopReason)}` : ''}; ${turnTimes}`);
    out.push(`  ${'sessions ended'.padEnd(15)}${listed(sessions.ended) || 'none'}`);
    out.push(`  ${'messages'.padEnd(15)}${sessions.messages.sent} sent${sessions.messages.byKind.length ? ` — ${listed(sessions.messages.byKind)}` : ''}`);
    out.push(`  ${'proposals'.padEnd(15)}${sessions.proposals.applied} applied · ${sessions.proposals.declined} not now`);
  }

  out.push('', 'Refused');
  if (report.refused.length === 0) out.push('  none');
  const codeWidth = Math.max(0, ...report.refused.map((entry) => entry.code.length));
  for (const entry of report.refused) {
    const requests = entry.requests.length === 1 ? entry.requests[0].name : listed(entry.requests);
    out.push(`  ${entry.code.padEnd(codeWidth)}  ${String(entry.count).padStart(3)}  ${requests}`);
  }

  out.push('', 'Failed');
  if (report.failed.length === 0) out.push('  none');
  for (const failure of report.failed) {
    const sample = failure.sample ? ` — ${failure.sample}` : '';
    const slowest = failure.slowestMs !== null ? ` — slowest ${duration(failure.slowestMs)}` : '';
    out.push(`  ${`${failure.count}×`.padStart(4)} ${failure.event.padEnd(15)} ${failure.source.padEnd(8)} ${failure.what}${sample}${slowest}`);
  }
  return out.join('\n');
}

/** The runner: the report for the period, as text or JSON. 0 printed · 2 a flag it cannot use, or no log. */
export function main(argv, io = { now: new Date(), out: (text) => console.log(text), err: (text) => console.error(text) }) {
  let options;
  try {
    options = parseArguments(argv);
  } catch (error) {
    io.err(`usage-report: ${error.message}.`);
    io.err(USAGE);
    return 2;
  }

  const folder = join(options.home, 'logs');
  if (!existsSync(folder)) {
    io.err(`usage-report: no machine log in ${folder} — nothing has written one there, or the home is another.`);
    return 2;
  }

  const from = new Date(io.now.getTime() - options.days * DAY_MS);
  const read = readLogs(folder, { since: from });
  const report = summarise(read.lines, { from, to: io.now, skipped: read.skipped });
  io.out(options.json ? JSON.stringify(report, null, 2) : render(report, folder));
  return 0;
}

if (isMain(import.meta.url)) {
  process.exitCode = main(process.argv.slice(2));
}

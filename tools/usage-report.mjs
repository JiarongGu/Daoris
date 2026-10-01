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
 * commands run, panels moved, files previewed by kind), how the conversations and sessions went (how
 * many, by kind and adapter, the median and slowest open and first answer, how long turns took and how
 * they ended), the asks (UNBLOCK5: the permissions each session's harness would have asked a person for,
 * refused because nobody was at the prompt, per session by adapter and repository), the rule proposals
 * (UNBLOCK5: what waits for the person, and those made in the period by week and state), the set-ups
 * (WSSETUP11: each set-up session's minutes, tool calls, tokens and context), the parks (WSSETUP11: the
 * sessions that stopped to ask the person, per week by workspace, beside the sessions started there), what
 * was refused (by code, most first), what failed (exceptions, the page's errors, error-level log lines,
 * failed or slow requests, grouped) and the lifecycle (starts, stops, uptime, versions). `--json` is the
 * same as data.
 *
 * ## What it never prints
 *
 * **Anyone's words.** There are none in the log (D94 §5), and this adds none: it counts and times. The
 * one free text a line carries, a caught error's message, is shown as its first line and never beyond
 * the log's own 120-character cap, so a stack or a paragraph cannot reach the screen through it. A rule
 * proposal holds words (its rule, its reason): only its id, its time and its state are read.
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
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
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

/**
 * The states a rule proposal can stand in, as its file's other readers know them (`RuleProposals.cs`,
 * `ruleproposals.ts`, PERM2, D74). A state not among them is read as `proposed`, as theirs read it.
 */
const PROPOSAL_STATES = ['proposed', 'applied', 'waiting', 'accepted', 'declined', 'refused', 'unchanged'];

const text = (value) => (typeof value === 'string' && value.trim() ? value.trim() : null);
const record = (value) => (value !== null && typeof value === 'object' && !Array.isArray(value) ? value : null);

/**
 * The rule proposals in a home's `proposals/` (UNBLOCK5): each as `{ proposed, state }`, and nothing else
 * of it. The file is the contract its readers share (the twins rule), so this reads it by their rules: a
 * file that is not JSON, has no id or no `change` object is not a proposal and is left out; a state they
 * do not know is `proposed`; a proposal with no readable time takes its file's. None of its words (the
 * rule, the reason, who wrote it) is read, so none can be printed.
 */
export function readProposals(folder) {
  if (!existsSync(folder)) return [];
  const read = [];
  for (const name of readdirSync(folder).filter((each) => each.endsWith('.json')).sort()) {
    const path = join(folder, name);
    let root;
    try {
      root = record(JSON.parse(readFileSync(path, 'utf8')));
    } catch {
      // Torn, held, or not JSON: it is no proposal, as the other readers leave it.
      continue;
    }
    if (!root || !text(root.id) || !record(root.change)) continue;
    const state = text(root.state);
    const stamped = Date.parse(text(root.proposed) ?? '');
    let proposed = stamped;
    if (Number.isNaN(stamped)) {
      try {
        proposed = statSync(path).mtime.getTime();
      } catch {
        continue;
      }
    }
    read.push({ proposed, state: state && PROPOSAL_STATES.includes(state) ? state : 'proposed' });
  }
  return read;
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

/**
 * A previewed file's kind (LEFT3 f): its extension, lower case, or `(none)`. The kind is what the report says, never the
 * path: whether the side bar's reading room is used, and for what, is the question (LEFT2).
 */
function kindOf(path) {
  const name = named(path)?.split('/').pop() ?? '';
  const dot = name.lastIndexOf('.');
  return dot > 0 ? name.slice(dot).toLowerCase() : '(none)';
}

/** A number to two places, as a person reads a mean: 1.33, 0.5, 2. */
const twoPlaces = (value) => Math.round(value * 100) / 100;

/**
 * Asks per session, spread: how many sessions and asks, the mean, the median (the two middles' mean), the
 * 90th percentile (the nearest rank) and the share of sessions with none. No session is no statistic, and
 * null, never zero.
 */
function spread(counts) {
  if (counts.length === 0) return { sessions: 0, asks: 0, mean: null, median: null, p90: null, none: null };
  const sorted = [...counts].sort((a, b) => a - b);
  const asks = sorted.reduce((sum, count) => sum + count, 0);
  const middle = Math.floor(sorted.length / 2);
  return {
    sessions: sorted.length,
    asks,
    mean: twoPlaces(asks / sorted.length),
    median: sorted.length % 2 === 1 ? sorted[middle] : twoPlaces((sorted[middle - 1] + sorted[middle]) / 2),
    p90: sorted[Math.ceil(0.9 * sorted.length) - 1],
    none: twoPlaces(sorted.filter((count) => count === 0).length / sorted.length),
  };
}

/**
 * The asks (UNBLOCK5, D122 §3.10): every session started in the period with how many of its calls were
 * refused (`permission.refused`, one line per refused call), spread overall, by adapter and by repository,
 * so a week before a repository declares its safe work can be held against a week after. What was refused
 * is counted over every ask in the period: by tool kind, by tool, and by what decided it, where the wire
 * said (`(unsaid)` where it did not). An ask from a session whose start the period does not hold is counted
 * apart, and in no session's spread.
 */
function asksOf(lines) {
  const sessions = new Map();
  for (const line of lines) {
    if (line.event !== 'session.started') continue;
    const session = named(line.data.session);
    if (session) {
      sessions.set(session, {
        adapter: named(line.data.adapter) ?? '(unnamed)', repository: named(line.data.repository) ?? '(unnamed)', asks: 0,
      });
    }
  }

  const refusals = lines.filter((line) => line.event === 'permission.refused');
  let unstarted = 0;
  for (const line of refusals) {
    const session = sessions.get(named(line.data.session));
    if (session) session.asks++;
    else unstarted++;
  }

  const grouped = (key) => {
    const groups = new Map();
    for (const session of sessions.values()) groups.set(session[key], [...(groups.get(session[key]) ?? []), session.asks]);
    return [...groups].map(([name, counts]) => ({ name, ...spread(counts) }))
      .sort((a, b) => b.sessions - a.sessions || a.name.localeCompare(b.name));
  };
  const said = (field) => counted(refusals.map((line) => named(line.data[field]) ?? '(unsaid)'));
  return {
    refused: refusals.length,
    ...spread([...sessions.values()].map((session) => session.asks)),
    byAdapter: grouped('adapter'),
    byRepository: grouped('repository'),
    byKind: said('kind'),
    byTool: said('tool'),
    by: said('by'),
    unstarted,
  };
}

/** The Monday a moment's week begins on, in UTC, as `2026-09-28`. */
function weekOf(time) {
  const day = new Date(time);
  const back = (day.getUTCDay() + 6) % 7;
  return new Date(Date.UTC(day.getUTCFullYear(), day.getUTCMonth(), day.getUTCDate() - back)).toISOString().slice(0, 10);
}

/**
 * The rule proposals (UNBLOCK5): how many wait for the person now, whenever they were made, and those
 * made in the period by the week they were made in and the state each stands in, oldest week first.
 */
function proposalsOf(proposals, { from, to }) {
  const weeks = new Map();
  for (const proposal of proposals) {
    if (proposal.proposed < from.getTime() || proposal.proposed > to.getTime()) continue;
    const week = weekOf(proposal.proposed);
    weeks.set(week, [...(weeks.get(week) ?? []), proposal.state]);
  }
  return {
    waiting: proposals.filter((proposal) => proposal.state === 'waiting').length,
    byWeek: [...weeks].sort(([a], [b]) => a.localeCompare(b))
      .map(([week, states]) => ({ week, made: states.length, byState: counted(states) })),
  };
}

/** A count as a line carries it: a number at or above zero, or null for anything else. Absent is never zero. */
const countOf = (value) => (typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : null);

/** The sum of what was measured, or null when nothing was. */
const sumOf = (values) => {
  const known = values.filter((value) => value !== null);
  return known.length === 0 ? null : known.reduce((sum, value) => sum + value, 0);
};

/** The counts a turn's end carries (WSSETUP11): its tokens as METER1 splits them, its calls, and its context. */
const TURN_COUNTS = ['input', 'cacheRead', 'cacheWrite', 'output', 'calls'];

/**
 * Each set-up's cost (WSSETUP11, D124 §7.3): every session whose start says `setup` true, in the order they
 * started, with how it stands (its end's state, `awaiting-person` when it parked and has not ended, or
 * `not ended`), how many times it parked, its seconds as its end says them, and its turns summed: the tool
 * calls, the tokens read anew, read from the cache, written to it and written out, and the context at its
 * largest against the window. What no line said is null, never zero; no price is claimed.
 */
function setupsOf(lines) {
  const sessions = new Map();
  for (const line of lines) {
    if (line.event !== 'session.started' || line.data.setup !== true) continue;
    const session = named(line.data.session);
    if (!session) continue;
    sessions.set(session, {
      session, repository: named(line.data.repository) ?? '(unnamed)', workspace: named(line.data.workspace) ?? '(unnamed)',
      ended: null, parked: 0, seconds: null, turns: [],
    });
  }

  for (const line of lines) {
    const entry = sessions.get(named(line.data.session));
    if (!entry) continue;
    if (line.event === 'turn.ended') entry.turns.push(line.data);
    else if (line.event === 'session.parked') entry.parked++;
    else if (line.event === 'session.ended') {
      entry.ended = named(line.data.state) ?? 'ended';
      entry.seconds = countOf(line.data.seconds);
    }
  }

  const costs = [...sessions.values()].map((entry) => {
    const summed = Object.fromEntries(TURN_COUNTS.map((field) => [field, sumOf(entry.turns.map((turn) => countOf(turn[field])))]));
    const widest = entry.turns
      .filter((turn) => countOf(turn.used) !== null)
      .reduce((most, turn) => (!most || turn.used > most.used ? turn : most), null);
    return {
      session: entry.session, repository: entry.repository, workspace: entry.workspace,
      state: entry.ended ?? (entry.parked > 0 ? 'awaiting-person' : 'not ended'),
      parked: entry.parked, seconds: entry.seconds, calls: summed.calls,
      input: summed.input, cacheRead: summed.cacheRead, cacheWrite: summed.cacheWrite, output: summed.output,
      used: widest ? widest.used : null, size: widest ? countOf(widest.size) : null,
    };
  });

  const widest = costs.filter((cost) => cost.used !== null).reduce((most, cost) => (!most || cost.used > most.used ? cost : most), null);
  return {
    started: costs.length,
    byState: counted(costs.map((cost) => cost.state)),
    sessions: costs,
    total: {
      sessions: costs.length,
      seconds: sumOf(costs.map((cost) => cost.seconds)),
      ...Object.fromEntries(TURN_COUNTS.map((field) => [field, sumOf(costs.map((cost) => cost[field]))])),
      used: widest ? widest.used : null,
      size: widest ? widest.size : null,
    },
  };
}

/** The doors whose sessions can park (D83, D65): a driven session and an intake. A conversation never does. */
const PARKING_KINDS = ['driven', 'intake'];

/**
 * Parks per week by workspace (WSSETUP11, D124 §7.3): the sessions that stopped to ask the person, which
 * setting a workspace up is meant to make rarer, beside the sessions started that week in that workspace by
 * a door that can park, so a week before a workspace is set up can be held against the weeks after. Weeks
 * begin on Monday, in UTC; a workspace a line does not say is `(unnamed)`, and a kind it does not say
 * `(unsaid)`.
 */
function parksOf(lines) {
  const weeks = new Map();
  const tally = (line, key) => {
    const week = weekOf(line.time);
    const workspaces = weeks.get(week) ?? new Map();
    const name = named(line.data.workspace) ?? '(unnamed)';
    const entry = workspaces.get(name) ?? { name, parked: 0, started: 0 };
    entry[key]++;
    workspaces.set(name, entry);
    weeks.set(week, workspaces);
  };

  const parks = lines.filter((line) => line.event === 'session.parked');
  for (const line of parks) tally(line, 'parked');
  for (const line of lines) {
    if (line.event === 'session.started' && PARKING_KINDS.includes(line.data.kind)) tally(line, 'started');
  }

  return {
    parked: parks.length,
    byKind: counted(parks.map((line) => named(line.data.kind) ?? '(unsaid)')),
    byWeek: [...weeks].sort(([a], [b]) => a.localeCompare(b)).map(([week, workspaces]) => {
      const byWorkspace = [...workspaces.values()]
        .sort((a, b) => b.parked - a.parked || b.started - a.started || a.name.localeCompare(b.name));
      return {
        week,
        parked: byWorkspace.reduce((sum, entry) => sum + entry.parked, 0),
        started: byWorkspace.reduce((sum, entry) => sum + entry.started, 0),
        byWorkspace,
      };
    }),
  };
}

/** The period's lines, summarised. Counts and times only: no line's words are carried. */
export function summarise(lines, { from, to, skipped = 0, proposals = [] }) {
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
  const previews = of('preview.opened');
  return {
    period: { from: from.toISOString(), to: to.toISOString(), days: Math.round((to - from) / DAY_MS) },
    lines: lines.length,
    skipped,
    lifecycle: [...lifecycle.values()].sort((a, b) => b.starts - a.starts || a.source.localeCompare(b.source)),
    used: {
      views: counted(field('view.opened', 'view')),
      commands: counted(field('command.run', 'command')),
      panels: counted(of('panel.moved').map((line) => `${named(line.data.view) ?? '?'} → ${named(line.data.region) ?? '?'}`)),
      // The side bar's file previews (LEFT2, D111): how many, in how many sessions, and of what kind (LEFT3 f).
      previews: {
        opened: previews.length,
        sessions: new Set(field('preview.opened', 'session')).size,
        byKind: counted(previews.map((line) => kindOf(line.data.path))),
      },
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
    asks: asksOf(lines),
    proposals: proposalsOf(proposals, { from, to }),
    setups: setupsOf(lines),
    parks: parksOf(lines),
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

/** A token count as a person reads it: 87 · 16.7K · 1M · 2.4B. */
function compact(count) {
  const [scale, unit] = count >= 1e9 ? [1e9, 'B'] : count >= 1e6 ? [1e6, 'M'] : count >= 1e3 ? [1e3, 'K'] : [1, ''];
  return `${(count / scale).toFixed(scale === 1 ? 0 : 1).replace(/\.0$/, '')}${unit}`;
}

/**
 * A set-up's cost as a line reads it (WSSETUP11): its minutes, calls, tokens and context, each only where
 * measured, or `not measured` when none was. Anew is the input read fresh, what was written to the cache
 * included; the rest was read from the cache (METER1's split).
 */
function costOf(cost, { largest = false } = {}) {
  const parts = [];
  if (cost.seconds !== null) parts.push(duration(cost.seconds * 1000));
  if (cost.calls !== null) parts.push(plural(cost.calls, 'call', 'calls'));
  const anew = sumOf([cost.input, cost.cacheWrite]);
  if (anew !== null) parts.push(`${compact(anew)} anew`);
  if (cost.cacheRead !== null) parts.push(`${compact(cost.cacheRead)} from cache`);
  if (cost.output !== null) parts.push(`${compact(cost.output)} out`);
  if (cost.used !== null) {
    parts.push(`${largest ? 'largest context' : 'context'} ${compact(cost.used)}${cost.size !== null ? ` of ${compact(cost.size)}` : ''}`);
  }
  return parts.length > 0 ? parts : ['not measured'];
}

const plural = (count, one, other) => `${count} ${count === 1 ? one : other}`;
const listed = (items) => items.map((item) => `${item.name} ${item.count}`).join(' · ');
const minute = (iso) => iso.slice(0, 16).replace('T', ' ');

/** Asks per session as a line reads them: mean 1.33 · median 1 · 90th 3 · 40% with none. */
const spreadOf = (entry) =>
  `mean ${entry.mean} · median ${entry.median} · 90th ${entry.p90} · ${Math.round(entry.none * 100)}% with none`;

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
  if (used.views.length + used.commands.length + used.panels.length + used.previews.opened === 0) {
    out.push('  none');
  } else {
    out.push(`  views      ${listed(used.views) || 'none'}`);
    out.push(`  commands   ${listed(used.commands) || 'none'}`);
    out.push(`  panels     ${listed(used.panels) || 'none'}`);
    const { previews } = used;
    out.push(`  previews   ${previews.opened === 0 ? 'none'
      : `${previews.opened} opened in ${plural(previews.sessions, 'session', 'sessions')} — ${listed(previews.byKind)}`}`);
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

  const { asks, proposals } = report;
  out.push('', 'Asks');
  if (asks.sessions + asks.refused === 0) {
    out.push('  none');
  } else {
    out.push(`  ${'sessions'.padEnd(15)}${asks.sessions} started · ${plural(asks.asks, 'ask', 'asks')}${asks.sessions ? ` · ${spreadOf(asks)}` : ''}`);
    const groups = (label, entries) => entries.forEach((entry, at) => out.push(
      `  ${(at === 0 ? label : '').padEnd(15)}${entry.name} ${plural(entry.sessions, 'session', 'sessions')}: ${spreadOf(entry)}`));
    groups('by adapter', asks.byAdapter);
    groups('by repository', asks.byRepository);
    if (asks.refused > 0) {
      out.push(`  ${'kinds'.padEnd(15)}${listed(asks.byKind)}`);
      out.push(`  ${'tools'.padEnd(15)}${listed(asks.byTool)}`);
      out.push(`  ${'decided by'.padEnd(15)}${listed(asks.by)}`);
    }
    if (asks.unstarted > 0) {
      out.push(`  ${'unstarted'.padEnd(15)}${plural(asks.unstarted, 'ask', 'asks')} from ${asks.unstarted === 1 ? 'a session' : 'sessions'} that did not start in this period`);
    }
  }

  out.push('', 'Rule proposals');
  if (proposals.waiting + proposals.byWeek.length === 0) {
    out.push('  none');
  } else {
    out.push(`  ${'waiting now'.padEnd(15)}${proposals.waiting}`);
    for (const week of proposals.byWeek) {
      out.push(`  ${`week of ${week.week}`.padEnd(20)}${week.made} made — ${listed(week.byState)}`);
    }
  }

  const { setups, parks } = report;
  out.push('', 'Set-ups');
  if (setups.started === 0) {
    out.push('  none');
  } else {
    out.push(`  ${'started'.padEnd(15)}${setups.started} — ${listed(setups.byState)}`);
    const sessionWidth = Math.max(14, ...setups.sessions.map((cost) => cost.session.length));
    for (const cost of setups.sessions) {
      const how = [cost.state, ...(cost.parked > 0 ? [`parked ${cost.parked}`] : []), ...costOf(cost)];
      out.push(`  ${cost.session.padEnd(sessionWidth)} ${`${cost.repository} (${cost.workspace})`.padEnd(24)} ${how.join(' · ')}`);
    }
    out.push(`  ${'total'.padEnd(15)}${[plural(setups.total.sessions, 'session', 'sessions'), ...costOf(setups.total, { largest: true })].join(' · ')}`);
  }

  out.push('', 'Parks');
  if (parks.parked + parks.byWeek.length === 0) {
    out.push('  none');
  } else {
    if (parks.byKind.length > 0) out.push(`  ${'kinds'.padEnd(15)}${listed(parks.byKind)}`);
    for (const week of parks.byWeek) {
      out.push(`  ${`week of ${week.week}`.padEnd(20)}${week.parked} parked · ${week.started} started`);
      for (const workspace of week.byWorkspace) {
        out.push(`    ${workspace.name.padEnd(16)} ${workspace.parked} parked · ${workspace.started} started`);
      }
    }
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
  const proposals = readProposals(join(options.home, 'proposals'));
  const report = summarise(read.lines, { from, to: io.now, skipped: read.skipped, proposals });
  io.out(options.json ? JSON.stringify(report, null, 2) : render(report, folder));
  return 0;
}

if (isMain(import.meta.url)) {
  process.exitCode = main(process.argv.slice(2));
}

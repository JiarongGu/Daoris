#!/usr/bin/env node
/**
 * The usage report (LOG1d, D94): an install's machine log, summarised for a development session.
 *
 *   node tools/usage-report.mjs --home <dir> [--days 7] [--json] [--service <url>]
 *   node tools/usage-report.mjs --install <dir> [--days 7] [--json] [--service <url>]     (the install's `data/` home)
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
 * ## Outcomes (OUTCOME1)
 *
 * With `--service <url>` naming this machine's local host, it also says how each quest the period touched
 * went (`docs/2026-10-03-future-directions-review.md` §3, row 4): its sessions and how each ended, its
 * carry-ons, the person's answers on it, its departures and the person's yes (D133), its strikes (D58 as
 * D104 and D125 amend it), the limits it met counted apart, how and when it closed and the time from publish
 * to close; then the same rolled up by repository and by agent and account. It is derived from the stores
 * that hold each fact: the host's quests and session records, each ask's words (three GETs, `RECORD_ROUTES`),
 * and the machine log's parks. Nothing is stored and nothing is written. A report, never a gate (D54): no
 * score folds the counts into one number, and each rate is said with its count, so a small sample says so.
 * A fact no store keeps is said missing with why, never zero (D57, D143 point 3). Without `--service`, or
 * when the host does not answer, the section says so and the rest of the report stands.
 *
 * ## What it never prints
 *
 * **Anyone's words.** There are none in the log (D94 §5), and this adds none: it counts and times. The
 * one free text a line carries, a caught error's message, is shown as its first line and never beyond
 * the log's own 120-character cap, so a stack or a paragraph cannot reach the screen through it. A rule
 * proposal holds words (its rule, its reason): only its id, its time and its state are read. A quest, a
 * session record and an ask hold words too (a title, a body, a note, a requirement's quote and check, a
 * done's answers, the person's words): only ids, names, states, flags, moments and counts are carried.
 *
 * ## A twin
 *
 * The driver's `MachineLogReader` reads the same lines with its own code; the two share none, and the
 * format is the contract (§3). What is a line and what is skipped is one table, held by both tests:
 * `MachineLogReaderTests.Parsing` and `usage-report.test.ts`. Change one and the other changes with it.
 *
 * It reads; it writes nothing, and nothing leaves the machine. Point it at a copy when the install is
 * running, or at the live folder: every file is opened for reading only. `--service` takes only a
 * loopback address (`hostOf`), so the host it asks is this machine's own, and every request is a GET.
 */
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from './fsx.mjs';
// The install's layout, from the script that makes it: its home is `data/` beside the launcher.
import { HOME } from './desktop-publish.mjs';

export const DAY_MS = 86_400_000;

/** The longest a caught error's message is shown: the log's own cap (`LogModule.MaxText`). */
export const SAMPLE = 120;

export const USAGE = 'usage: node tools/usage-report.mjs (--home <dir> | --install <dir>) [--days <n>] [--json] [--service <url>]';

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

/** This machine's own addresses (OUTCOME1): the outcomes read the local host, never a shared one (D47 §4). */
const LOOPBACK = /^(localhost|127(\.\d{1,3}){3}|\[::1\])$/i;

const LOCAL_HOST = '--service takes the local host\'s address, such as http://localhost:5177';

/** The origin `--service` names, held to a loopback address over HTTP. Throws a sentence. */
function hostOf(value) {
  let url;
  try {
    url = new URL(value);
  } catch {
    throw new Error(LOCAL_HOST);
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') throw new Error(LOCAL_HOST);
  if (!LOOPBACK.test(url.hostname)) {
    throw new Error(`--service reads only this machine's own host (localhost, 127.0.0.1 or [::1]), not ${url.hostname}`);
  }
  return url.origin;
}

/**
 * `--home <dir>` or `--install <dir>` (its `data/`), `--days <n>` (7), `--json`, and `--service <url>`, the local host
 * whose quests and sessions the outcomes read (none: the outcomes are not read). Throws a sentence.
 */
export function parseArguments(argv) {
  let home = null;
  let install = null;
  let days = 7;
  let json = false;
  let service = null;
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
      case '--service':
        if (!value || value.startsWith('--')) throw new Error(LOCAL_HOST);
        service = hostOf(value);
        i++;
        break;
      default:
        throw new Error(`\`${flag}\` is not an option`);
    }
  }
  if (home && install) throw new Error('name one of --home and --install, not both');
  if (!home && !install) throw new Error('name the log to read: --home <dir> or --install <dir>');
  return { home: home ?? join(install, HOME), days, json, service };
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

/**
 * The routes the outcomes read (OUTCOME1), each a GET the local host answers, closed records included: the quests as
 * their operations replay to (a local host serves no operations, D143's note), the session records as this machine is
 * answered them (its accounts included, D49 §4), and the asks with the person's words (DRIFT1a), a local host's alone.
 */
export const RECORD_ROUTES = Object.freeze({
  quests: '/api/quests?includeClosed=true',
  sessions: '/api/sessions?includeClosed=true',
  asks: '/api/asks?includeClosed=true',
});

/**
 * The host's records for the outcomes: each route's list, or null and the sentence saying why it was not read (a route
 * that failed, answered no list, or did not answer). Every request is a GET; nothing is sent but the route.
 */
export async function readRecords(service, { fetch: get = globalThis.fetch, timeoutMs = 15_000 } = {}) {
  const read = async (route) => {
    let body;
    try {
      const response = await get(`${service}${route}`, {
        method: 'GET', headers: { accept: 'application/json' }, signal: AbortSignal.timeout(timeoutMs),
      });
      if (!response.ok) return { list: null, unread: `${route} answered ${response.status}` };
      body = await response.text();
    } catch (error) {
      // Node's fetch says only `fetch failed`; its cause carries the system's code, or the reason it refused the address.
      const reason = named(error?.cause?.code) ?? sampleOf(error?.cause?.message) ?? sampleOf(error?.message) ?? 'no reason given';
      return { list: null, unread: `${route} did not answer: ${reason}` };
    }
    let root;
    try {
      root = JSON.parse(body);
    } catch {
      return { list: null, unread: `${route} answered something that is not JSON` };
    }
    return Array.isArray(root) ? { list: root, unread: null } : { list: null, unread: `${route} answered no list` };
  };
  const [quests, sessions, asks] = await Promise.all(
    [RECORD_ROUTES.quests, RECORD_ROUTES.sessions, RECORD_ROUTES.asks].map(read));
  return {
    quests: quests.list, questsUnread: quests.unread,
    sessions: sessions.list, sessionsUnread: sessions.unread,
    asks: asks.list, asksUnread: asks.unread,
  };
}

/** A sender that is an ask: `ask #<id>`, the service's `AskDesk.SenderOf` (D65 §1a), a chain step's included. */
const ASK_SENDER = 'ask #';

/**
 * The ask a quest's sender names, or null for a quest a repository asked: the service's `AskDesk.AskOf`, read the same
 * way (ordinal, and an id after the prefix), as the driver's `AskWords.AskOf` reads it.
 */
export function askOf(sender) {
  return typeof sender === 'string' && sender.length > ASK_SENDER.length && sender.startsWith(ASK_SENDER)
    ? sender.slice(ASK_SENDER.length)
    : null;
}

/**
 * How a session record ended, as the strikes read it (`ServiceClient.ReadStrikes`, D58 as D104 and D125 amend it): a
 * failure an account's limit made is `limit`, a stop that was not the person's is `interrupted`, and anything else is
 * its state as the record says it. A flag counts only as the boolean true.
 */
export function endingOf(session) {
  const state = named(session?.state);
  if (state === 'failed' && session.limit === true) return 'limit';
  if (state === 'stopped' && session.interrupted === true) return 'interrupted';
  return state ?? '(unsaid)';
}

/** The endings after which the next run on the quest carries it on (D80, D104): a failure, a limit, an interrupted stop. */
const CUT_OFF = ['failed', 'limit', 'interrupted'];

/** The endings that are strikes (D58 as amended): a failure that was not a limit, and a stop that was not the person's. */
const STRIKES = ['failed', 'interrupted'];

/** A moment a record carries, in milliseconds, or null where it carries none that reads. */
const momentOf = (value) => {
  const time = typeof value === 'string' ? Date.parse(value) : Number.NaN;
  return Number.isNaN(time) ? null : time;
};
const isoOf = (time) => (time === null ? null : new Date(time).toISOString());

/** The outcomes when the records were not read, and why. */
const unreadOutcomes = (why) => ({ read: false, unread: why, quests: [], byRepository: [], byAgent: [] });

/**
 * One quest's outcome (OUTCOME1), from its record, its sessions on the host, its ask's words and the log's parks. Each
 * fact no store keeps is null and said in `missing`, with why. A teammate's record (keyed `origin/id`, SYNC4) is listed
 * and counted apart: its account and its log are on its machine, and a strike is this machine's judgement of its own
 * runs (`ReadStrikes`).
 */
function outcomeOf(quest, runs, facts) {
  const missing = [];
  const status = (named(quest.status) ?? '(unsaid)').toLowerCase();
  const filed = momentOf(quest.filed);
  const accepted = momentOf(quest.accepted);

  const sessions = runs.map((run) => {
    const teammate = run.id.includes('/');
    return {
      session: run.id, state: named(run.state) ?? '(unsaid)', ending: endingOf(run), adapter: named(run.adapter),
      account: teammate ? '(a teammate\'s)' : named(run.profile) ?? '(own sign-in)', teammate,
      created: isoOf(momentOf(run.created)),
      // The log is this machine's, and holds a session's parks only where the period holds its start.
      parked: !teammate && facts.started.has(run.id) ? facts.parked.get(run.id) ?? 0 : null,
    };
  });
  const mine = sessions.filter((session) => !session.teammate);

  // A carry-on is a run that follows a cut-off on its quest; a stand-down is the race resolving, not a run (`ReadLastRun`).
  let carryOns = 0;
  let previous = null;
  for (const session of mine) {
    if (session.ending === 'stood-down') continue;
    if (previous && CUT_OFF.includes(previous.ending)) carryOns++;
    previous = session;
  }

  const unparked = mine.filter((session) => session.parked === null).map((session) => session.session);
  const parks = unparked.length > 0 ? null : mine.reduce((sum, session) => sum + session.parked, 0);
  if (parks === null) {
    missing.push({
      fact: 'parks',
      why: `the period read holds no start for ${unparked.join(', ')} (one that began before the period, or that no log saw)`,
    });
  }

  let answers = null;
  let added = null;
  const askId = askOf(quest.from);
  const ask = askId && facts.asks ? facts.asks.get(askId) : null;
  const keptFrom = momentOf(ask?.wordsKeptFrom);
  if (!askId) {
    missing.push({ fact: 'answers', why: 'asked by a repository, and only an ask keeps the person\'s words (D133)' });
  } else if (!facts.asks) {
    missing.push({ fact: 'answers', why: `its ask's words were not read: ${facts.asksUnread ?? 'the host answered no asks'}` });
  } else if (!ask) {
    missing.push({ fact: 'answers', why: `its ask #${askId} is not on the host` });
  } else if (!Array.isArray(ask.words)) {
    missing.push({ fact: 'answers', why: 'the host answered its ask without the person\'s words (a host from before DRIFT1a)' });
  } else if (keptFrom !== null && (filed === null || keptFrom > filed)) {
    missing.push({
      fact: 'answers',
      why: `its ask keeps the person's words from ${minute(isoOf(keptFrom))} UTC, after the quest was published`,
    });
  } else {
    const on = ask.words.filter((each) => record(each) && each.quest === quest.id);
    answers = on.filter((each) => each.kind === 'answered').length;
    added = on.filter((each) => each.kind === 'added' || each.kind === 'reopened').length;
  }

  const replies = Array.isArray(quest.answers) ? quest.answers.filter(record) : [];
  const departures = replies.filter((reply) => text(reply.departed)).length;

  let closed = null;
  if (status === 'done' || status === 'declined') {
    // A yes is an operation that moves the quest's moment (`QuestLog.Step`), and a local host serves no operations.
    const at = status === 'done' && accepted !== null ? null : momentOf(quest.updated);
    closed = { how: status, at: isoOf(at), afterMs: at !== null && filed !== null && at >= filed ? at - filed : null };
    if (status === 'declined') closed.bySession = sessions.some((session) => session.ending === 'declined');
    if (at === null) {
      missing.push({
        fact: 'closed',
        why: accepted !== null
          ? 'the person\'s yes is its last moment, and a local host serves a quest\'s state, not when its done was made'
          : 'the host said no moment for it',
      });
    }
  }

  // A first pass: done by its one working session, with no departure and nothing asked of the person (an answer on its
  // ask, or a park in the log). What the person chose to add while it ran is theirs, not the work's asking.
  let firstPass = null;
  if (status === 'done') {
    const working = sessions.filter((session) => session.ending !== 'stood-down');
    const logKnows = working.length > 0 && working.every((session) => session.parked !== null);
    const asked = (answers ?? 0) > 0 || working.some((session) => (session.parked ?? 0) > 0);
    if (working.length === 0) {
      missing.push({ fact: 'firstPass', why: 'no session record of its work' });
    } else if (working.length > 1 || departures > 0 || asked) {
      firstPass = false;
    } else if (answers === null && !logKnows) {
      missing.push({ fact: 'firstPass', why: 'neither its ask\'s words nor the log say whether the person was asked' });
    } else {
      firstPass = true;
    }
  }

  return {
    quest: quest.id,
    repository: named(quest.to) ?? '(unnamed)',
    workspace: named(quest.workspace),
    status,
    waiting: text(quest.awaits) !== null,
    filed: isoOf(filed),
    closed,
    sessions,
    teammates: sessions.length - mine.length,
    carryOns,
    strikes: mine.filter((session) => STRIKES.includes(session.ending)).length,
    limits: sessions.filter((session) => session.ending === 'limit').length,
    parks,
    answers,
    added,
    requirements: Array.isArray(quest.requirements) ? quest.requirements.length : 0,
    departures,
    held: quest.held === true,
    accepted: isoOf(accepted),
    firstPass,
    missing,
  };
}

/**
 * The outcomes rolled up by a key (OUTCOME1): how many quests, done and declined; first passes of those done, with how
 * many are not known; the quests carried on of those this machine ran, and the carry-ons; and the answers over the quests
 * whose answers are kept, with how many are not. Each figure carries its count, and none is folded into a score (D54).
 */
function rollUp(outcomes, keyOf) {
  const groups = new Map();
  for (const outcome of outcomes) {
    const { id, fields } = keyOf(outcome);
    const group = groups.get(id) ?? {
      ...fields, quests: 0, done: 0, declined: 0,
      firstPass: { yes: 0, of: 0, unknown: 0 },
      carryOn: { quests: 0, of: 0, carryOns: 0 },
      answers: { total: 0, over: 0, unknown: 0 },
    };
    group.quests++;
    if (outcome.status === 'done') {
      group.done++;
      group.firstPass.of++;
      if (outcome.firstPass === true) group.firstPass.yes++;
      else if (outcome.firstPass === null) group.firstPass.unknown++;
    }
    if (outcome.status === 'declined') group.declined++;
    if (outcome.sessions.some((session) => !session.teammate)) {
      group.carryOn.of++;
      if (outcome.carryOns > 0) group.carryOn.quests++;
      group.carryOn.carryOns += outcome.carryOns;
    }
    if (outcome.answers === null) group.answers.unknown++;
    else {
      group.answers.over++;
      group.answers.total += outcome.answers;
    }
    groups.set(id, group);
  }
  return [...groups.values()];
}

/** No session yet: the agent and account a quest is rolled up under before anything ran it. */
const NO_SESSION = '(no session)';

/**
 * Each quest the period touched (published, moved, accepted, or a session on it opened or moved within it), oldest
 * published first, with its outcome; then the roll-ups by repository and by agent and account. A quest is rolled up under
 * the agent and account of its first working session: the one that took the first pass. Null records are the outcomes
 * not read, and why.
 */
export function outcomesOf(records, lines, { from, to }) {
  if (!records) return unreadOutcomes('no host was named: --service <url> reads its quests and sessions');
  const problem = records.questsUnread ?? records.sessionsUnread ?? null;
  if (problem || !Array.isArray(records.quests) || !Array.isArray(records.sessions)) {
    return unreadOutcomes(problem ?? 'the host\'s quests and sessions did not read');
  }

  const started = new Set();
  const parked = new Map();
  for (const line of lines) {
    const session = named(line.data.session);
    if (!session) continue;
    if (line.event === 'session.started') started.add(session);
    else if (line.event === 'session.parked') parked.set(session, (parked.get(session) ?? 0) + 1);
  }
  const asks = Array.isArray(records.asks)
    ? new Map(records.asks.filter((ask) => record(ask) && named(ask.id)).map((ask) => [ask.id, ask]))
    : null;

  const runsOf = new Map();
  for (const run of records.sessions) {
    if (!record(run) || !named(run.id) || !named(run.quest)) continue;
    runsOf.set(run.quest, [...(runsOf.get(run.quest) ?? []), run]);
  }
  const opened = (run) => momentOf(run.created) ?? Number.NEGATIVE_INFINITY;
  const inPeriod = (time) => time !== null && time >= from.getTime() && time <= to.getTime();

  const facts = { started, parked, asks, asksUnread: records.asksUnread };
  const touched = [];
  for (const quest of records.quests) {
    if (!record(quest) || !named(quest.id)) continue;
    const runs = [...(runsOf.get(quest.id) ?? [])].sort((a, b) => opened(a) - opened(b) || a.id.localeCompare(b.id));
    const moments = [quest.filed, quest.updated, quest.accepted, ...runs.flatMap((run) => [run.created, run.updated])];
    if (!moments.map(momentOf).some(inPeriod)) continue;
    touched.push({ filed: momentOf(quest.filed) ?? Number.NEGATIVE_INFINITY, outcome: outcomeOf(quest, runs, facts) });
  }
  const quests = touched
    .sort((a, b) => a.filed - b.filed || a.outcome.quest.localeCompare(b.outcome.quest))
    .map((each) => each.outcome);

  const byRepository = rollUp(quests, (outcome) => ({ id: outcome.repository, fields: { repository: outcome.repository } }))
    .sort((a, b) => b.quests - a.quests || a.repository.localeCompare(b.repository));
  const byAgent = rollUp(quests, (outcome) => {
    const first = outcome.sessions.find((session) => session.ending !== 'stood-down');
    const agent = first ? first.adapter ?? '(unnamed)' : NO_SESSION;
    const account = first ? first.account : null;
    return { id: `${agent}\u0000${account}`, fields: { agent, account } };
  }).sort((a, b) => b.quests - a.quests
    || Number(a.agent === NO_SESSION) - Number(b.agent === NO_SESSION)
    || a.agent.localeCompare(b.agent)
    || (a.account ?? '').localeCompare(b.account ?? ''));

  return { read: true, unread: null, quests, byRepository, byAgent };
}

/** The period's lines, summarised. Counts and times only: no line's words are carried. */
export function summarise(lines, { from, to, skipped = 0, proposals = [], records = null }) {
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
    outcomes: outcomesOf(records, lines, { from, to }),
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

/** A span a quest takes, in milliseconds: 412 ms · 45m 0s · 2h 10m · 3d 4h. */
function span(ms) {
  if (ms < 3_600_000) return duration(ms);
  if (ms < DAY_MS) return `${Math.floor(ms / 3_600_000)}h ${Math.floor((ms % 3_600_000) / 60_000)}m`;
  return `${Math.floor(ms / DAY_MS)}d ${Math.floor((ms % DAY_MS) / 3_600_000)}h`;
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

/**
 * A quest's outcome as its line reads it (OUTCOME1): how it stands or closed and in how long, whether it was a first
 * pass, its departures and the person's yes, its sessions, then each count only where it is more than none. A fact that
 * is not known is not here: the quest's `not known` line says it, so an absent count is never read as none.
 */
function outcomeParts(outcome) {
  const parts = [];
  if (outcome.closed) {
    const after = outcome.closed.afterMs !== null ? ` in ${span(outcome.closed.afterMs)}` : '';
    parts.push(`${outcome.closed.how}${after}${outcome.closed.bySession ? ' by its session' : ''}`);
  } else {
    parts.push(outcome.waiting ? `${outcome.status}, waiting on a quest` : outcome.status);
  }
  if (outcome.status === 'done') {
    parts.push(outcome.firstPass === true ? 'first pass' : outcome.firstPass === false ? 'not first pass' : 'first pass not known');
  }
  if (outcome.departures > 0) {
    const filed = momentOf(outcome.filed);
    const accepted = momentOf(outcome.accepted);
    const yes = outcome.held
      ? ', held for the person\'s yes'
      : accepted !== null ? `, accepted${filed !== null && accepted >= filed ? ` ${span(accepted - filed)} after publish` : ''}` : '';
    parts.push(`${plural(outcome.departures, 'departure', 'departures')}${yes}`);
  }
  parts.push(`${plural(outcome.sessions.length, 'session', 'sessions')}${outcome.teammates > 0 ? ` (${outcome.teammates} a teammate's)` : ''}`);
  if (outcome.carryOns > 0) parts.push(plural(outcome.carryOns, 'carry-on', 'carry-ons'));
  if (outcome.strikes > 0) parts.push(plural(outcome.strikes, 'strike', 'strikes'));
  if (outcome.limits > 0) parts.push(plural(outcome.limits, 'limit', 'limits'));
  if (outcome.parks) parts.push(`parked ${outcome.parks}`);
  if (outcome.answers !== null) parts.push(`answers ${outcome.answers}`);
  if (outcome.added) parts.push(`added ${outcome.added}`);
  return parts;
}

/** A roll-up's row as a line reads it: each figure with its count, so a small sample says so, and no score. */
function groupParts(group) {
  const { firstPass, carryOn, answers } = group;
  const parts = [plural(group.quests, 'quest', 'quests'), `done ${group.done}`];
  if (group.declined > 0) parts.push(`declined ${group.declined}`);
  parts.push(firstPass.of === 0
    ? 'first pass: none done'
    : `first pass ${firstPass.yes} of ${firstPass.of} done${firstPass.unknown > 0 ? `, ${firstPass.unknown} not known` : ''}`);
  parts.push(carryOn.of === 0
    ? 'carried on: no session here'
    : `carried on ${carryOn.quests} of ${carryOn.of} run here`
      + `${carryOn.carryOns > 0 ? ` (${plural(carryOn.carryOns, 'carry-on', 'carry-ons')})` : ''}`);
  parts.push(answers.over === 0
    ? `answers not kept (${answers.unknown})`
    : `answers ${answers.total} over ${plural(answers.over, 'quest', 'quests')} (${twoPlaces(answers.total / answers.over)} a quest)`
      + `${answers.unknown > 0 ? `, ${answers.unknown} not kept` : ''}`);
  return parts;
}

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

  const { outcomes } = report;
  out.push('', 'Outcomes');
  if (!outcomes.read) {
    out.push(`  not read — ${outcomes.unread}`);
  } else if (outcomes.quests.length === 0) {
    out.push('  none');
  } else {
    out.push(`  ${'quests'.padEnd(15)}${outcomes.quests.length} — ${listed(counted(outcomes.quests.map((each) => each.status)))}`);
    const idWidth = Math.max(...outcomes.quests.map((each) => each.quest.length));
    const repositoryWidth = Math.max(...outcomes.quests.map((each) => each.repository.length));
    const under = ' '.repeat(2 + idWidth + 2 + repositoryWidth + 2);
    for (const outcome of outcomes.quests) {
      out.push(`  ${outcome.quest.padEnd(idWidth)}  ${outcome.repository.padEnd(repositoryWidth)}  ${outcomeParts(outcome).join(' · ')}`);
      if (outcome.sessions.length > 0) {
        out.push(`${under}${outcome.sessions
          .map((session) => `${session.session} ${session.ending} [${session.adapter ?? '?'} · ${session.account}]`).join(' → ')}`);
      }
      if (outcome.missing.length > 0) {
        out.push(`${under}not known: ${outcome.missing.map((each) => `${each.fact} — ${each.why}`).join('; ')}`);
      }
    }
    const rows = (label, groups, name) => {
      out.push(`  ${label}`);
      const width = Math.max(...groups.map((group) => name(group).length));
      for (const group of groups) out.push(`    ${name(group).padEnd(width)}  ${groupParts(group).join(' · ')}`);
    };
    rows('by repository', outcomes.byRepository, (group) => group.repository);
    rows('by agent and account, each quest under its first session', outcomes.byAgent,
      (group) => (group.account === null ? group.agent : `${group.agent} · ${group.account}`));
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

/**
 * The runner: the report for the period, as text or JSON, with the outcomes when `--service` names the local host. 0
 * printed, a host that did not answer included, since the section says so · 2 a flag it cannot use, or no log.
 */
export async function main(argv, io = {
  now: new Date(), out: (text) => console.log(text), err: (text) => console.error(text), fetch: globalThis.fetch,
}) {
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
  const records = options.service ? await readRecords(options.service, { fetch: io.fetch ?? globalThis.fetch }) : null;
  const report = summarise(read.lines, { from, to: io.now, skipped: read.skipped, proposals, records });
  io.out(options.json ? JSON.stringify(report, null, 2) : render(report, folder));
  return 0;
}

if (isMain(import.meta.url)) {
  process.exitCode = await main(process.argv.slice(2));
}

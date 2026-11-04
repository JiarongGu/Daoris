import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { HOME } from '../desktop-publish.mjs';

export const DAY_MS = 86_400_000;

/** The longest a caught error's message is shown: the log's own cap (`LogModule.MaxText`). */
export const SAMPLE = 120;

export const USAGE = 'usage: node tools/usage-report.mjs (--home <dir> | --install <dir>) [--days <n>] [--json] [--service <url>]';

const FILE_NAME = /^(\d{4}-\d{2}-\d{2})\.([a-z]+)\.jsonl$/;

// UTC, as the writers write it. A time with no zone would be local time to Date.parse and UTC to the
// driver's reader, so neither twin reads one.
const TIME_SHAPE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$/;

export const named = (value) => (typeof value === 'string' && value.length > 0 ? value : null);

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
 * The states a rule proposal can stand in, as its file's other reader knows them (`RuleProposals.cs`, PERM2, D74; the CLI's
 * `ruleproposals.ts` left in PUSH1f2d). A state not among them is read as `proposed`, as it reads it.
 */
const PROPOSAL_STATES = ['proposed', 'applied', 'waiting', 'accepted', 'declined', 'refused', 'unchanged'];

export const text = (value) => (typeof value === 'string' && value.trim() ? value.trim() : null);
export const record = (value) => (value !== null && typeof value === 'object' && !Array.isArray(value) ? value : null);

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

import { SAMPLE, named } from './reading.mjs';

/** The middle value, or the mean of the two middles; null for none. */
export function median(values) {
  if (values.length === 0) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1 ? sorted[middle] : Math.round((sorted[middle - 1] + sorted[middle]) / 2);
}

/** A tally, most first, a tie by name: `[{ name, count }]`. */
export function counted(names) {
  const tally = new Map();
  for (const name of names) tally.set(name, (tally.get(name) ?? 0) + 1);
  return [...tally].map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
}

/**
 * A time field across lines: how many were timed, the median, the slowest and whose. A time that could
 * not be known is null in the log and is left out here: absent is never zero.
 */
export function timing(lines, field) {
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
export function sampleOf(message) {
  if (typeof message !== 'string' || message.trim() === '') return null;
  const first = message.split(/\r?\n/)[0].trim();
  return first.length > SAMPLE ? `${first.slice(0, SAMPLE)}…` : first;
}

/** `0.0.1+abcdef1234567890` as `0.0.1+abcdef12`: the commit is a prefix's worth. */
export function shortVersion(version) {
  const [release, commit] = version.split('+');
  return commit ? `${release}+${commit.slice(0, 8)}` : release;
}

/** What failed, grouped by what it was: most first, a tie in the order each first happened. */
export function failures(lines) {
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
export function kindOf(path) {
  const name = named(path)?.split('/').pop() ?? '';
  const dot = name.lastIndexOf('.');
  return dot > 0 ? name.slice(dot).toLowerCase() : '(none)';
}

/** A number to two places, as a person reads a mean: 1.33, 0.5, 2. */
export const twoPlaces = (value) => Math.round(value * 100) / 100;

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
export function asksOf(lines) {
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
export function weekOf(time) {
  const day = new Date(time);
  const back = (day.getUTCDay() + 6) % 7;
  return new Date(Date.UTC(day.getUTCFullYear(), day.getUTCMonth(), day.getUTCDate() - back)).toISOString().slice(0, 10);
}

/**
 * The rule proposals (UNBLOCK5): how many wait for the person now, whenever they were made, and those
 * made in the period by the week they were made in and the state each stands in, oldest week first.
 */
export function proposalsOf(proposals, { from, to }) {
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
export const countOf = (value) => (typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : null);

/** The sum of what was measured, or null when nothing was. */
export const sumOf = (values) => {
  const known = values.filter((value) => value !== null);
  return known.length === 0 ? null : known.reduce((sum, value) => sum + value, 0);
};

/** The counts a turn's end carries (WSSETUP11): its tokens as METER1 splits them, its calls, and its context. */
export const TURN_COUNTS = ['input', 'cacheRead', 'cacheWrite', 'output', 'calls'];

/**
 * Each set-up's cost (WSSETUP11, D124 §7.3): every session whose start says `setup` true, in the order they
 * started, with how it stands (its end's state, `awaiting-person` when it parked and has not ended, or
 * `not ended`), how many times it parked, its seconds as its end says them, and its turns summed: the tool
 * calls, the tokens read anew, read from the cache, written to it and written out, and the context at its
 * largest against the window. What no line said is null, never zero; no price is claimed.
 */
export function setupsOf(lines) {
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

/**
 * The models an `actor.ran` line names (USAGECOST1): a list of names, or one name. The log once wrote a collection as
 * its CLR type name (`System.String[]`, LOGCOLLECTION1), which names no model and is read as none, never as a model.
 */
export function modelsOf(value) {
  const names = Array.isArray(value) ? value : [value];
  return names.map(named).filter((name) => name !== null && !/^System\.|\[\]$/.test(name));
}

/** What a driven session's cost is rolled up under when no line named its models. */
export const NO_MODEL = '(model not logged)';

import { join } from 'node:path';
import { NO_MODEL, TURN_COUNTS, countOf, counted, median, modelsOf, sumOf, weekOf } from './figures.mjs';
import { doorOf, named } from './reading.mjs';

/** The most driven sessions the text lists (the rest are counted), and the columns a line of it may take. */
export const DRIVEN_LISTED = 10;
export const DRIVEN_WIDTH = 120;

/**
 * A rolled-up row of driven sessions' cost: how many, how many had a `turn.ended` at all, the median and
 * slowest of their turn time, the tokens summed, and the cache read per call — the cache read over the calls of the
 * sessions that measured both, which is the figure a smaller starting context or fewer steps would move. What no
 * line says is null, never zero.
 */
function drivenGroupOf(costs) {
  const timed = costs.filter((cost) => cost.ms !== null);
  const slowest = timed.reduce((most, cost) => (!most || cost.ms > most.ms ? cost : most), null);
  const paired = costs.filter((cost) => cost.cacheRead !== null && cost.calls !== null && cost.calls > 0);
  const callsPaired = paired.reduce((sum, cost) => sum + cost.calls, 0);
  const widest = costs.filter((cost) => cost.used !== null).reduce((most, cost) => (!most || cost.used > most.used ? cost : most), null);
  return {
    sessions: costs.length,
    measured: costs.filter((cost) => cost.turns > 0).length,
    turns: costs.reduce((sum, cost) => sum + cost.turns, 0),
    timed: timed.length,
    medianMs: median(timed.map((cost) => cost.ms)),
    slowestMs: slowest ? slowest.ms : null,
    slowestSession: slowest ? slowest.session : null,
    ...Object.fromEntries(TURN_COUNTS.map((field) => [field, sumOf(costs.map((cost) => cost[field]))])),
    cacheReadPerCall: callsPaired > 0 ? Math.round(paired.reduce((sum, cost) => sum + cost.cacheRead, 0) / callsPaired) : null,
    used: widest ? widest.used : null,
    size: widest ? widest.size : null,
  };
}

/**
 * What each driven session cost: every session started in the period whose start says `driven` and not
 * `setup` (a set-up has its own section), newest first, with its repository and workspace, adapter, the models the
 * `actor.ran` lines name, its turns, its turn time, and its turns summed — the calls, the tokens read anew, read from
 * the cache, written to it and written out — and its context at the largest. Then the same rolled up by repository and
 * by adapter and model. A session with no `turn.ended` is said so (`turns` 0, every figure null), never zero;
 * a turn of a session whose start the period does not hold is counted apart. Counts and times only.
 */
export function drivenOf(lines) {
  const known = new Set();
  const sessions = new Map();
  for (const line of lines) {
    if (line.event !== 'session.started') continue;
    const session = named(line.data.session);
    if (!session) continue;
    known.add(session);
    if (line.data.kind !== 'driven' || line.data.setup === true) continue;
    sessions.set(session, {
      session, time: line.time, started: line.stamp, adapter: doorOf(line.data) ?? '(unnamed)',
      repository: named(line.data.repository) ?? '(unnamed)', workspace: named(line.data.workspace) ?? '(unnamed)',
      models: new Set(), turns: [],
    });
  }

  let unstarted = 0;
  for (const line of lines) {
    if (line.event !== 'turn.ended' && line.event !== 'actor.ran') continue;
    const session = named(line.data.session);
    const entry = sessions.get(session);
    if (!entry) {
      if (line.event === 'turn.ended' && !known.has(session)) unstarted++;
    } else if (line.event === 'turn.ended') {
      entry.turns.push(line.data);
    } else {
      for (const model of modelsOf(line.data.models)) entry.models.add(model);
    }
  }

  const costs = [...sessions.values()].reverse().sort((a, b) => b.time - a.time).map((entry) => {
    const widest = entry.turns
      .filter((turn) => countOf(turn.used) !== null)
      .reduce((most, turn) => (!most || turn.used > most.used ? turn : most), null);
    return {
      session: entry.session, started: entry.started, repository: entry.repository, workspace: entry.workspace,
      adapter: entry.adapter, models: [...entry.models].sort(), turns: entry.turns.length,
      ms: sumOf(entry.turns.map((turn) => countOf(turn.turnMs))),
      ...Object.fromEntries(TURN_COUNTS.map((field) => [field, sumOf(entry.turns.map((turn) => countOf(turn[field])))])),
      used: widest ? widest.used : null, size: widest ? countOf(widest.size) : null,
    };
  });

  const groups = (key) => {
    const rows = new Map();
    for (const cost of costs) rows.set(key(cost), [...(rows.get(key(cost)) ?? []), cost]);
    return rows;
  };
  const ordered = (rows) => rows.sort((a, b) => b.sessions - a.sessions || a.label.localeCompare(b.label));
  return {
    started: costs.length,
    measured: costs.filter((cost) => cost.turns > 0).length,
    unstarted,
    sessions: costs,
    byRepository: ordered([...groups((cost) => cost.repository)]
      .map(([repository, members]) => ({ label: repository, repository, ...drivenGroupOf(members) }))),
    byAdapterAndModel: ordered([...groups((cost) => `${cost.adapter}\u0000${cost.models.join(' + ')}`)].map(([, members]) => {
      const { adapter, models } = members[0];
      return { label: `${adapter} · ${models.join(' + ') || NO_MODEL}`, adapter, models, ...drivenGroupOf(members) };
    })),
  };
}

/** The doors whose sessions can park: a driven session and an intake. A conversation never does. */
const PARKING_KINDS = ['driven', 'intake'];

/**
 * Parks per week by workspace: the sessions that stopped to ask the person, which
 * setting a workspace up is meant to make rarer, beside the sessions started that week in that workspace by
 * a door that can park, so a week before a workspace is set up can be held against the weeks after. Weeks
 * begin on Monday, in UTC; a workspace a line does not say is `(unnamed)`, and a kind it does not say
 * `(unsaid)`.
 */
export function parksOf(lines) {
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

import { DAY_MS, doorOf, named } from './reading.mjs';
import { asksOf, counted, failures, kindOf, proposalsOf, setupsOf, shortVersion, timing } from './figures.mjs';
import { drivenOf, parksOf } from './driven.mjs';
import { outcomesOf } from './outcomes.mjs';

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
    // A harness action's refusal says which action beside its request, so a refused sign-in is told from an install.
    const action = named(line.data.action);
    entry.requests.push(`${named(line.data.request) ?? 'an unnamed request'}${action ? ` ${action}` : ''}`);
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
      // The side bar's file previews: how many, in how many sessions, and of what kind.
      previews: {
        opened: previews.length,
        sessions: new Set(field('preview.opened', 'session')).size,
        byKind: counted(previews.map((line) => kindOf(line.data.path))),
      },
    },
    sessions: {
      started: of('session.started').length,
      byKind: counted(field('session.started', 'kind')),
      byAdapter: counted(of('session.started').map((line) => doorOf(line.data)).filter(Boolean)),
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
    driven: drivenOf(lines),
    parks: parksOf(lines),
    outcomes: outcomesOf(records, lines, { from, to }),
    refused: [...refused.values()]
      .sort((a, b) => b.count - a.count)
      .map((entry) => ({ ...entry, requests: counted(entry.requests) })),
    harness: harnessActionsOf(lines),
    failed: failures(lines),
  };
}

/**
 * Each agent action's runs (`harness.started`, `harness.prompted`, `harness.ended`): by agent and action, how many
 * started and asked the person, how they ended, and each phase's time — from the press to the tool's process up, from there
 * to its first link, the tool's run, the driver's read of who signed in, its write of the lists, and the whole. A phase a run
 * never reached is null in the log and is not timed here. Most started first.
 */
function harnessActionsOf(lines) {
  const rows = new Map();
  const rowOf = (line) => {
    const harness = doorOf(line.data, 'harness') ?? '(unnamed)';
    const action = named(line.data.action) ?? '(unnamed)';
    const id = `${harness}\u0000${action}`;
    if (!rows.has(id)) rows.set(id, { harness, action, started: [], prompted: [], ended: [] });
    return rows.get(id);
  };
  for (const line of lines) {
    if (line.event === 'harness.started') rowOf(line).started.push(line);
    else if (line.event === 'harness.prompted') rowOf(line).prompted.push(line);
    else if (line.event === 'harness.ended') rowOf(line).ended.push(line);
  }
  return [...rows.values()]
    .map((row) => ({
      harness: row.harness,
      action: row.action,
      started: row.started.length,
      prompted: row.prompted.length,
      ended: counted(row.ended.map((line) => named(line.data.outcome) ?? 'unknown')),
      starting: timing(row.started, 'ms'),
      asking: timing(row.prompted, 'ms'),
      tool: timing(row.ended, 'toolMs'),
      who: timing(row.ended, 'whoMs'),
      lists: timing(row.ended, 'listsMs'),
      whole: timing(row.ended, 'ms'),
    }))
    .sort((a, b) => b.started - a.started || a.harness.localeCompare(b.harness) || a.action.localeCompare(b.action));
}

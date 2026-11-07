import { join } from 'node:path';
import { DAY_MS, text } from './reading.mjs';
import { NO_MODEL, counted, median, sumOf, twoPlaces } from './figures.mjs';
import { minute, momentOf } from './outcomes.mjs';
import { DRIVEN_LISTED, DRIVEN_WIDTH } from './driven.mjs';

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
 * A set-up's cost as a line reads it: its minutes, calls, tokens and context, each only where
 * measured, or `not measured` when none is. Anew is the input read fresh, what is written to the cache
 * included; the rest is read from the cache.
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

/**
 * A driven session's cost as a line reads it: turns, turn time, calls, the four token counts and the peak
 * context, each only where a line said it. A session with no `turn.ended` says so; it is never a row of zeros.
 */
function drivenParts(cost) {
  if (cost.turns === 0) return ['no turn.ended read'];
  const parts = [plural(cost.turns, 'turn', 'turns')];
  if (cost.ms !== null) parts.push(duration(cost.ms));
  if (cost.calls !== null) parts.push(plural(cost.calls, 'call', 'calls'));
  for (const [label, field] of [['in', 'input'], ['cache read', 'cacheRead'], ['cache write', 'cacheWrite'], ['out', 'output']]) {
    if (cost[field] !== null) parts.push(`${label} ${compact(cost[field])}`);
  }
  if (cost.used !== null) parts.push(`peak ${compact(cost.used)}`);
  return parts;
}

/** A rolled-up row as a line reads it: sessions, the median and slowest turn time, and the cache read per call. */
function drivenGroupParts(group) {
  const parts = [plural(group.sessions, 'session', 'sessions')];
  if (group.measured < group.sessions) parts.push(`${group.sessions - group.measured} with no turn.ended`);
  if (group.timed > 0) parts.push(`median ${duration(group.medianMs)}`, `slowest ${duration(group.slowestMs)}`);
  parts.push(group.cacheReadPerCall !== null ? `cache read ${compact(group.cacheReadPerCall)}/call` : 'cache read/call not measured');
  if (group.used !== null) parts.push(`peak ${compact(group.used)}`);
  return parts;
}

const plural = (count, one, other) => `${count} ${count === 1 ? one : other}`;
const listed = (items) => items.map((item) => `${item.name} ${item.count}`).join(' · ');

/** Asks per session as a line reads them: mean 1.33 · median 1 · 90th 3 · 40% with none. */
const spreadOf = (entry) =>
  `mean ${entry.mean} · median ${entry.median} · 90th ${entry.p90} · ${Math.round(entry.none * 100)}% with none`;

/**
 * A quest's outcome as its line reads it: how it stands or closed and in how long, whether it is a first
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

export function timed(label, value) {
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

  const { driven } = report;
  out.push('', 'Driven sessions');
  if (driven.started + driven.unstarted === 0) {
    out.push('  none');
  } else {
    const narrow = (text) => (text.length > DRIVEN_WIDTH ? `${text.slice(0, DRIVEN_WIDTH - 1)}…` : text);
    const without = driven.started - driven.measured;
    out.push(narrow(`  ${'started'.padEnd(15)}${driven.started}${driven.started > 0
      ? ` — ${driven.measured} with a turn.ended${without > 0 ? `, ${without} with none read` : ''}` : ''}`));
    if (driven.unstarted > 0) {
      out.push(`  ${'unstarted'.padEnd(15)}${plural(driven.unstarted, 'turn', 'turns')} from sessions that did not start in this period`);
    }
    for (const cost of driven.sessions.slice(0, DRIVEN_LISTED)) {
      const models = cost.models.join(' + ') || NO_MODEL;
      out.push(narrow(`  ${cost.started.slice(5, 16).replace('T', ' ')}  ${cost.session}  ${cost.repository} (${cost.workspace}) · ${cost.adapter} · ${models}`));
      out.push(narrow(`    ${drivenParts(cost).join(' · ')}`));
    }
    if (driven.sessions.length > DRIVEN_LISTED) {
      out.push(`  … and ${driven.sessions.length - DRIVEN_LISTED} older (all in --json)`);
    }
    const rows = (label, groups) => {
      out.push(`  ${label}`);
      for (const group of groups) {
        out.push(narrow(`    ${group.label}`));
        out.push(narrow(`      ${drivenGroupParts(group).join(' · ')}`));
      }
    };
    rows('by repository', driven.byRepository);
    rows('by adapter and model', driven.byAdapterAndModel);
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

  out.push('', 'Agent actions');
  if (report.harness.length === 0) out.push('  none');
  const actionWidth = Math.max(0, ...report.harness.map((row) => `${row.harness} ${row.action}`.length));
  for (const row of report.harness) {
    const ends = row.ended.length ? ` · ${listed(row.ended)}` : '';
    const times = [['starting', row.starting], ['to the link', row.asking], ['the tool', row.tool], ['who', row.who], ['lists', row.lists]]
      .filter(([, value]) => value.count > 0)
      .map(([label, value]) => `${label} median ${duration(value.medianMs)}`);
    out.push(`  ${`${row.harness} ${row.action}`.padEnd(actionWidth)}  ${row.started} started${ends}${times.length ? ` — ${times.join(' · ')}` : ''}`);
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

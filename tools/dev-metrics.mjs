#!/usr/bin/env node
/**
 * How this repository's development cycle performed over a date range, from read-only sources.
 *
 *   node tools/dev-metrics.mjs --home <data> --logs <logs> --since YYYY-MM-DD --until YYYY-MM-DD
 *        [--service <url> | --quests <file>] [--workspaces a,b] [--work <dir>] [--merge-queue <file>] [--repo <checkout>] [--case <ask-or-quest-ids>] [--json]
 *   node tools/dev-metrics.mjs --transcripts <subagents dir> --ids <a,b,...> [--json]
 *
 * `--home` is an install's `data` folder, `--logs` its `logs` folder. A quest's record comes from the running
 * service (`/api/quests?workspace=`, a read) when `--service` is given, else from `--quests` or the home's knowledge.db. A figure
 * whose source holds nothing names where it would have to be logged: never a fabricated zero.
 * `--merge-queue` is this repository's `local/merge-queue.json`, where integration requests land or are handed back.
 * `--json` also writes docs/metrics/<until-or-today>.json, retaining each case's receipt. Builder mode times transcripts.
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { join, dirname } from 'node:path';
import { isMain } from './fsx.mjs';
import { clockOf } from './subagent-time.mjs';
import { homeEntry } from '../src/Daoris.Cli/src/platform/home.ts';
import { MERGE_RECORD, readRecords, summarize } from './merge/merge-record.ts';
import { readHome, missing } from './dev-metrics/records.mjs';
import { sessionLinks, caseScope, askOf } from './dev-metrics/scope.mjs';
import { lifecycleRows } from './dev-metrics/lifecycle.mjs';
import { workflowRows, personQueue } from './dev-metrics/workflow.mjs';
import { communicationRows } from './dev-metrics/communication.mjs';
import { actorRows, replyRows } from './dev-metrics/actors.mjs';
import { BUDGET_FILE, budgets, judge } from './dev-metrics/budgets.mjs';
import { writeSnapshot } from './dev-metrics/snapshot.mjs';
import { parse } from './dev-metrics/options.mjs';
import { addTokens, zeroTokens as zero, sumTokens, costBreakdown } from './dev-metrics/cost.mjs';
export { writeSnapshot } from './dev-metrics/snapshot.mjs';

const HOUR = 3600000;

const readJson = (file, fallback) => { try { return JSON.parse(readFileSync(file, 'utf8')); } catch { return fallback; } };
const readLines = (file) => {
  try { return readFileSync(file, 'utf8').split('\n').filter(Boolean).map((l) => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean); }
  catch { return []; }
};
const filesOf = (dir, test) => { try { return readdirSync(dir).filter(test).sort(); } catch { return []; } };

function windowOf({ since, until }) {
  const from = since ? Date.parse(`${since}T00:00:00Z`) : -Infinity;
  const to = until ? Date.parse(`${until}T00:00:00Z`) + 24 * HOUR : Infinity;
  return (at) => { const t = Date.parse(at); return !Number.isNaN(t) && t >= from && t < to; };
}

function spread(values) {
  const v = values.filter(Number.isFinite).sort((a, b) => a - b);
  if (!v.length) return { n: 0 };
  const at = (p) => v[Math.min(v.length - 1, Math.floor(p * v.length))];
  const mid = v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2;
  return { n: v.length, median: mid, mean: Math.round((v.reduce((a, b) => a + b, 0) / v.length) * 10) / 10, p90: at(0.9), max: v[v.length - 1] };
}
const hoursOf = (ms) => { const s = spread(ms.map((m) => m / HOUR)); return s.n ? { n: s.n, medianHours: round(s.median), meanHours: round(s.mean), p90Hours: round(s.p90) } : { n: 0 }; };
const round = (x) => Math.round(x * 100) / 100;

async function questsOf({ file, service, workspaces }) {
  if (service) {
    const out = [];
    let recorded = workspaces.length > 0;
    for (const w of workspaces) {
      try {
        const res = await fetch(`${service.replace(/\/$/, '')}/api/quests?workspace=${encodeURIComponent(w)}`);
        if (res.ok) {
          const body = await res.json();
          if (Array.isArray(body)) out.push(...body); else recorded = false;
        } else recorded = false;
      } catch { recorded = false; }
    }
    return { rows: out, recorded };
  }
  const body = file ? readJson(file, null) : null;
  const rows = Array.isArray(body) ? body : body?.id ? [body] : [];
  return { rows: rows.filter(q => typeof q?.id === 'string'), recorded: body != null && rows.every(q => typeof q?.id === 'string') };
}

function sessionsOf(home) {
  const quests = new Map();
  const events = new Map();
  const folder = homeEntry(home, 'sessions');
  for (const f of filesOf(folder, (n) => n.endsWith('.events.jsonl'))) {
    const id = f.replace('.events.jsonl', '');
    const rows = readLines(join(folder, f));
    events.set(id, rows);
    const first = rows.find((r) => r.kind === 'user' && /quest `#([0-9a-f]{8,})`/.test(r.text ?? ''));
    if (first) quests.set(id, /quest `#([0-9a-f]{8,})`/.exec(first.text)[1]);
  }
  return { quests, events };
}

function turnsOf(home) {
  const folder = homeEntry(home, 'usage-history');
  return filesOf(folder, (f) => /^turns-.*\.jsonl$/.test(f)).flatMap((f) => readLines(join(folder, f)));
}

function logEvents(logs, inRange) {
  return filesOf(logs, (f) => /\.jsonl$/.test(f)).flatMap((f) => readLines(join(logs, f))).filter((e) => inRange(e.time));
}

/**
 * What a completed tool row answered, as text: Claude Code's `content` parts, or Codex's `output`, the whole result wrapped as JSON
 * (`{result: {content: [...]}, error}`); a row of neither shape answers nothing, which is no hit.
 */
function resultText(row) {
  if (Array.isArray(row.content)) return row.content.map((p) => p.text ?? '').join('\n');
  if (typeof row.output !== 'string') return '';
  try {
    const wrapped = JSON.parse(row.output);
    if (wrapped?.error) return String(wrapped.error?.message ?? wrapped.error);
    return (wrapped?.result?.content ?? []).map((p) => p.text ?? '').join('\n');
  } catch { return row.output; }
}

/** Memory calls from the session event streams: a call is its in-progress row, its input row and its completed row, joined by id. */
function memoryCalls(events, inRange) {
  const calls = [];
  for (const [session, rows] of events) {
    const byId = new Map();
    for (const r of rows) {
      if (r.kind !== 'tool' || !r.id) continue;
      const c = byId.get(r.id) ?? { session, id: r.id };
      c.title ??= r.title;
      c.startedAt ??= r.at;
      if (r.status === 'completed' || r.status === 'failed') { c.endedAt = r.at; c.failed = r.status === 'failed'; c.result = resultText(r); }
      byId.set(r.id, c);
    }
    for (const c of byId.values()) {
      const kind = /(?:^|[_.])memory_(search|get|write|propose)$/.exec(c.title ?? '')?.[1];
      if (kind && inRange(c.startedAt)) calls.push({ ...c, kind });
    }
  }
  return calls;
}

function lyntaiOf(calls) {
  const by = (k) => calls.filter((c) => c.kind === k);
  const searches = by('search');
  // A read that found an entry answers its text; one that found none, or was refused, says so in words the connector fixes.
  const hit = (c) => c.endedAt && !c.failed && !/^(No memory entry|Refused:|Memory is not on)/.test(c.result ?? '') && (c.result ?? '') !== '';
  const hits = searches.filter(hit);
  const reads = [...searches, ...by('get')];
  const fetched = hits.filter((s) => calls.some((c) => c.kind === 'get' && c.session === s.session && c.startedAt >= s.startedAt));
  // A write the connector kept says it staged an operation; any other answer (a refusal, memory off) changed nothing.
  const kept = (c) => c.endedAt && !c.failed && /^Kept as operation /.test(c.result ?? '');
  const latency = calls.filter((c) => c.endedAt).map((c) => Date.parse(c.endedAt) - Date.parse(c.startedAt));
  return {
    searches: searches.length,
    searchesWithHits: hits.length,
    reads: reads.length,
    readsWithHits: reads.filter(hit).length,
    hitsFetchedSameSession: fetched.length,
    cited: missing('the handback or session log of memory entry ids cited by a result'),
    gets: by('get').length,
    writes: by('write').length,
    writesKept: by('write').filter(kept).length,
    proposes: by('propose').length,
    latencyMs: spread(latency),
    latencyNote: 'a call\'s first and last event in the session stream: includes any permission wait; the connector\'s own log (the mcp log) records only app.started and app.stopped',
  };
}

/**
 * The memory's own row counts, read-only, through node:sqlite where this Node has it: Lyntai's entries and nodes in `memory.db`, and
 * the operations the coordinator's log holds by state in `knowledge.db`, so a write's way (staged, released, applied) is seen where it stops.
 */
async function storeCounts(home) {
  const out = { entries: missing('no memory.db in the home'), operations: missing('no knowledge.db memory_ops in the home') };
  let sqlite;
  try { sqlite = await import('node:sqlite'); } catch { return { entries: missing('node:sqlite'), operations: missing('node:sqlite') }; }
  const open = (file) => { const db = new sqlite.DatabaseSync(file, { readOnly: true }); return db; };
  const memory = homeEntry(home, 'memory');
  if (existsSync(memory)) {
    try {
      const db = open(memory);
      const count = (t) => db.prepare(`select count(*) c from ${t}`).get().c;
      out.entries = count('lyntai_memory_entry');
      try { out.nodes = count('lyntai_memory_node'); } catch { /* an older schema */ }
      db.close();
    } catch { out.entries = missing('memory.db readable Lyntai tables through node:sqlite'); }
  }
  const knowledge = homeEntry(home, 'knowledge');
  if (existsSync(knowledge)) {
    try {
      const db = open(knowledge);
      out.operations = Object.fromEntries(db.prepare('select state, count(*) c from memory_ops group by state order by state').all().map((r) => [r.state, r.c]));
      db.close();
    } catch { out.operations = missing('knowledge.db memory_ops readable through node:sqlite'); }
  }
  return out;
}

/** Merge commits on the repository's main in the range, less the ones that only bring main into a branch. */
function mergesOnMain(repo, { since, until }) {
  try {
    const args = ['-C', repo, 'log', 'main', '--merges', '--format=%s'];
    if (since) args.push(`--since=${since}T00:00:00Z`);
    if (until) args.push(`--until=${until}T23:59:59Z`);
    return execFileSync('git', args, { encoding: 'utf8', windowsHide: true }).split('\n').filter((s) => s && !/^Merge (branch 'main'|main\b)/.test(s)).length;
  } catch { return missing('readable main merge commit log'); }
}

function leftoversOf(home, work, landings) {
  const trees = ['trees']
    .flatMap((t) => filesOf(join(work, t), () => true).flatMap((ws) => filesOf(join(work, t, ws), () => true).flatMap((repo) => filesOf(join(work, t, ws, repo), (n) => /^s-/.test(n)))));
  const branches = readJson(homeEntry(home, 'session-branches'), { branches: [] }).branches ?? [];
  return {
    trees: trees.length,
    sessionBranchesPresent: branches.filter((b) => !b.goneAt).length,
    landedBranchesPresent: landings.filter((b) => !b.goneAt).length,
  };
}

function frictionOf(events) {
  const named = (n) => events.filter((e) => e.event === n);
  const looks = named('watch.looked').map((e) => e.data?.ms).filter(Number.isFinite);
  const refusedBy = {};
  for (const e of named('permission.refused')) refusedBy[e.data?.tool ?? '?'] = (refusedBy[e.data?.tool ?? '?'] ?? 0) + 1;
  return {
    permissionRefused: named('permission.refused').length,
    permissionRefusedByTool: refusedBy,
    refusalsToAgents: named('person.refused').length,
    lookSlow: named('look.slow').length,
    lookMs: { ...spread(looks), total: looks.reduce((a, b) => a + b, 0) },
  };
}

export async function collect(options) {
  const { home, logs, service, mergeQueue } = options;
  const inRange = windowOf(options);
  const work = options.work ?? join(dirname(home), 'work');
  const workspaces = (options.workspaces ?? filesOf(join(work, 'trees'), () => true).join(',')).split(',').filter(Boolean);
  const records = await readHome(home);
  const questRead = options.quests || service ? await questsOf({ file: options.quests, service, workspaces }) : { rows: records.tables.quests, recorded: records.availability.quests === true };
  const allQuests = questRead.rows;
  const allLandings = readJson(homeEntry(home, 'landings'), { branches: [] }).branches ?? [];
  const allEvents = sessionsOf(home).events;
  const sessionQuest = sessionLinks(records.tables.sessions, allEvents, allLandings);
  const scope = caseScope(options.case, allQuests, records.tables.asks, records.tables.sessions, sessionQuest, allEvents);
  const scoped = scope.requested.length > 0;
  const quests = allQuests.filter(q => !scoped || scope.quests.includes(q.id));
  const sessions = records.tables.sessions.filter(s => !scoped || scope.sessions.includes(s.id));
  const events = new Map([...allEvents].filter(([id]) => !scoped || scope.sessions.includes(id)));
  const landings = allLandings.filter(l => !scoped || scope.quests.includes(l.quest) || scope.sessions.includes(l.session));
  const turns = turnsOf(home).filter(t => !scoped || scope.sessions.includes(t.session));
  const allLogs = logEvents(logs, inRange);
  const logRows = allLogs.filter(e => !scoped || scope.sessions.includes(e.data?.session) || scope.quests.includes(e.data?.quest) || scope.quests.includes(e.data?.run) || scope.asks.includes(e.data?.ask));
  const now = options.now ?? new Date().toISOString();

  const doneAt = (q) => q.submission?.at ?? records.tables.quest_log.find(o => o.quest === q.id && o.kind === 'done')?.at ?? q.updated;
  const filed = quests.filter((q) => inRange(q.filed));
  const done = quests.filter((q) => q.status === 'Done' && inRange(doneAt(q)));
  const onLine = (id) => landings.find((l) => l.quest === id && l.goneAt && ['on-line', 'merged'].includes(l.removedAs));

  const served = new Map();
  for (const [session, q] of sessionQuest) served.set(q, [...(served.get(q) ?? []), session]);
  const activeQuests = quests.filter(q => scoped || turns.some(t => sessionQuest.get(t.session) === q.id && inRange(t.ended ?? t.written)));
  const perQuest = [...new Map([...filed, ...done, ...activeQuests].map(q => [q.id, q])).values()].map((q) => {
    const sessions = served.get(q.id) ?? [];
    const used = turns.filter((t) => sessions.includes(t.session) && inRange(t.ended ?? t.written));
    const tokens = sumTokens(used);
    return { quest: q.id, sessions: sessions.length || missing('knowledge.db sessions.quest/took_quest, landing session links or opening quest prompt'), tokens };
  });
  const perActor = {};
  for (const t of turns.filter((t) => inRange(t.ended ?? t.written))) perActor[t.actor ?? 'unknown'] = addTokens(perActor[t.actor ?? 'unknown'] ?? zero(), t);

  const answers = done.flatMap((q) => (q.answers ?? []));
  const requirements = done.reduce((n, q) => n + (q.requirements?.length ?? 0), 0);
  const met = answers.filter((a) => a.met != null).length;
  const departed = answers.filter((a) => a.departed != null).length;

  const queue = mergeQueue ? readJson(mergeQueue, null) : null;
  const firstPass = queue
    ? { integrated: (queue.landed ?? []).filter(l => inRange(l.at) && (!scoped || landings.some(b => b.branch === l.branch))).length, handedBack: (queue.handedBack ?? []).filter(l => inRange(l.at) && (!scoped || landings.some(b => b.branch === l.branch))).length }
    : missing('merge queue records; pass --merge-queue <local/merge-queue.json>');

  if (options.repo && firstPass && typeof firstPass === 'object') firstPass.mergeCommitsOnMain = scoped ? missing('case-specific main merge correlation') : mergesOnMain(options.repo, options);
  const recordFile = options.mergeRecord ?? (options.repo ? join(options.repo, MERGE_RECORD) : null);
  const branches = landings.map(l => l.branch).filter(Boolean);
  const mergeRecords = recordFile ? readRecords(recordFile).filter(each => inRange(each.at) && (!scoped || branches.includes(each.branch))) : [];
  const mergeFlow = recordFile ? mergeRecords.length ? summarize(mergeRecords) : missing('merge-record rows in the selected range and scope') : missing('merge run records; pass --merge-record <local/merge-record.jsonl>');
  const askTurns = turns.filter((t) => t.actor === 'ask-daoris' && inRange(t.ended ?? t.written));
  const lyntai = lyntaiOf(memoryCalls(events, inRange));
  lyntai.store = await storeCounts(home);

  const asks = records.tables.asks.filter(a => !scoped || scope.asks.includes(a.id));
  const workflow = workflowRows(records, scope, logRows, inRange, now);
  scope.completed = !scoped ? missing('--case and a completed workflow receipt') : quests.some(q => q.status !== 'Done') ? false
    : workflow.length && workflow.every(r => r.ended?.outcome === 'completed') ? true : missing('workflow run ended.outcome=completed for every case run');
  const questSource = questRead.recorded;
  const report = {
    generatedAt: now,
    scope,
    budgets: { file: BUDGET_FILE, values: budgets },
    range: { since: options.since ?? null, until: options.until ?? null },
    source: { quests: service ? 'service read' : options.quests ? 'supplied quest file' : 'knowledge.db quests', workspaces, questRecords: questSource ? quests.length : missing('quest records'), tables: records.availability },
    throughput: {
      questsFiled: questSource ? filed.length : missing('knowledge.db quests or --quests/--service quest records'),
      questsDone: questSource ? done.length : missing('knowledge.db quests or --quests/--service quest records'),
      asksFiled: records.availability.asks === true ? asks.filter(a => inRange(a.asked)).length : questSource ? new Set(filed.map(askOf).filter(Boolean)).size : missing('knowledge.db asks.asked or quest sender'),
      asksClosed: records.availability.asks === true ? asks.filter(a => a.state === 'Closed' && inRange(a.updated)).length : missing('knowledge.db asks state Closed and updated; derived Done is not a stored close'),
    },
    leadTime: {
      filedToDone: hoursOf(done.map((q) => Date.parse(doneAt(q)) - Date.parse(q.filed))),
      doneToLine: hoursOf(done.map((q) => { const l = onLine(q.id); return l ? Date.parse(l.goneAt) - Date.parse(doneAt(q)) : NaN; })),
      doneNotOnLine: done.filter((q) => !onLine(q.id)).length,
    },
    cost: { perQuest, perActor, total: sumTokens(turns.filter(t => inRange(t.ended ?? t.written))), ...costBreakdown(turns, sessions, quests, asks, sessionQuest, inRange) },
    completion: { requirements, met, departed, unanswered: requirements - met - departed },
    firstPass,
    mergeFlow,
    friction: frictionOf([...logRows.filter(e => !['watch.looked', 'look.slow'].includes(e.event)), ...allLogs.filter(e => ['watch.looked', 'look.slow'].includes(e.event))]),
    leftovers: leftoversOf(home, work, landings),
    askDaoris: {
      conversations: new Set(askTurns.map((t) => t.session)).size,
      turns: askTurns.length,
      tokens: sumTokens(askTurns),
      actsInFullAccess: logRows.filter((e) => e.event === 'help.pressed').length,
      cardsAsked: logRows.filter((e) => e.event === 'person.confirmation' && e.data?.state === 'asked').length,
      replies: replyRows(sessions, events, inRange),
    },
    lyntai,
    lifecycle: lifecycleRows(quests.filter(q => inRange(q.filed) || inRange(q.updated) || scoped), sessions, sessionQuest, records, landings, allLogs, workflow),
    workflow,
    communication: communicationRows(records, scope, events, quests, inRange),
    actors: actorRows(sessions, records, inRange, allLogs),
    browser: { openMs: missing('browser plugin session tab open request and completion clock'), signInShared: missing('browser probe local storage key names; no credential values') },
  };
  report.personQueue = personQueue(records, sessions, quests, report.workflow, logRows, now, scope);
  if (!report.friction.lookMs.n) report.friction.lookMs = missing('logs watch.looked data.ms');
  if (!allLogs.length) for (const k of ['permissionRefused', 'refusalsToAgents', 'lookSlow']) report.friction[k] = missing('machine logs events in range');
  if (!Array.isArray(readJson(homeEntry(home, 'landings'), null)?.branches)) report.leftovers.landedBranchesPresent = missing('readable landings branches and goneAt');
  const branchRecords = readJson(homeEntry(home, 'session-branches'), null)?.branches;
  if (!Array.isArray(branchRecords)) report.leftovers.sessionBranchesPresent = missing('readable session-branches branches and goneAt');
  else if (scoped) {
    const selected = branchRecords.filter(b => landings.some(l => l.branch === b.branch) || sessions.some(s => b.branch?.endsWith(`/s-${s.id}`)));
    report.leftovers.sessionBranchesPresent = selected.length ? selected.filter(b => !b.goneAt).length : missing('case branch identity in landing records or session branch name');
  }
  if (scoped) report.leftovers.trees = sessions.some(s => s.tree) ? sessions.filter(s => s.tree && existsSync(s.tree)).length : missing('case session tree paths in knowledge.db sessions.tree');
  else if (!existsSync(join(work, 'trees'))) report.leftovers.trees = missing('work/trees directory inventory');
  report.leftovers.staleWorktrees = missing('git worktree prunable inventory correlated with case sessions');
  if (!questSource) for (const key of Object.keys(report.completion)) report.completion[key] = missing('quest requirements and answers in the selected range');
  for (const key of ['filedToDone', 'doneToLine']) if (!report.leadTime[key].n) report.leadTime[key] = missing(key === 'filedToDone' ? 'quests.filed and done timestamp pairs' : 'quest done and landing line removal timestamp pairs');
  if (!questSource || typeof report.leftovers.landedBranchesPresent === 'string') report.leadTime.doneNotOnLine = missing('quest done and readable landing line-removal records');
  if (!events.size) {
    for (const key of ['searches', 'searchesWithHits', 'reads', 'readsWithHits', 'hitsFetchedSameSession', 'gets', 'writes', 'writesKept', 'proposes', 'latencyMs']) report.lyntai[key] = missing('session events memory tool calls');
  }
  if (typeof report.lyntai.latencyMs !== 'string' && !report.lyntai.latencyMs.n) report.lyntai.latencyMs = missing('memory tool call start and completion timestamp pairs');
  if (!askTurns.length) for (const key of ['turns', 'conversations']) report.askDaoris[key] = missing('usage-history Ask Daoris turns in range');
  if (!logRows.length) for (const key of ['actsInFullAccess', 'cardsAsked']) report.askDaoris[key] = missing('scoped machine log help.pressed or person.confirmation events');
  Object.assign(report, judge(report, allLogs, mergeRecords));
  return report;
}

export function builderTimes(dir, ids) {
  const files = readdirSync(dir);
  return ids.map((id) => {
    const file = files.find((f) => f.includes(id) && f.endsWith('.jsonl'));
    if (!file) return { id, found: false };
    const clock = clockOf(readLines(join(dir, file)));
    const byKind = {};
    for (const [k, ms] of Object.entries(clock.buckets)) byKind[k] = { calls: clock.calls[k], minutes: round(ms / 60000) };
    const tools = Object.values(clock.buckets).reduce((a, b) => a + b, 0);
    return {
      id, found: true, wallMinutes: round(clock.total / 60000), toolMinutes: round(tools / 60000),
      modelMinutes: round(clock.model / 60000), otherMinutes: round(Math.max(0, clock.total - tools - clock.model) / 60000), byKind,
    };
  });
}

const tok = (t) => typeof t === 'string' ? t : `in ${t.input}, out ${t.output}, cacheRead ${t.cacheRead}, cacheWrite ${t.cacheWrite}`;
const stat = (s, unit = '') => typeof s === 'string' ? s : (s.n ? `n=${s.n}, median ${s.median ?? s.medianHours}${unit}, mean ${s.mean ?? s.meanHours}${unit}, p90 ${s.p90 ?? s.p90Hours}${unit}` : missing('source timestamps for this distribution'));

export function render(r) {
  const L = [];
  const put = (k, v) => L.push(`  ${k.padEnd(34)} ${typeof v === 'object' && v !== null ? JSON.stringify(v) : v}`);
  L.push(`Daoris development, ${r.range.since ?? '...'} to ${r.range.until ?? '...'} (quests from ${r.source.quests}, ${r.source.questRecords} records)`);
  L.push('Throughput');
  put('quests filed / done', `${r.throughput.questsFiled} / ${r.throughput.questsDone}`);
  put('asks filed (by quests filed)', r.throughput.asksFiled);
  put('asks closed', r.throughput.asksClosed);
  L.push('Lead time');
  put('filed to done', stat(r.leadTime.filedToDone, ' h'));
  put('done to branch on the line', stat(r.leadTime.doneToLine, ' h'));
  put('done, not (yet) on the line', r.leadTime.doneNotOnLine);
  L.push('Cost (tokens)');
  put('all turns in range', tok(r.cost.total));
  for (const [a, t] of Object.entries(r.cost.perActor).sort()) put(`  actor ${a}`, tok(t));
  const quests = r.cost.perQuest.filter((q) => typeof q.sessions === 'number');
  put('quests with sessions found', quests.length ? `${quests.length} of ${r.cost.perQuest.length}` : missing('quest-linked sessions in knowledge.db, landing records or session opening events'));
  for (const q of quests.sort((a, b) => b.tokens.output - a.tokens.output).slice(0, 10)) put(`  quest ${q.quest}`, `${q.sessions} sessions; ${tok(q.tokens)}`);
  L.push('Completion');
  put('requirements met / departed', `${r.completion.met} / ${r.completion.departed} of ${r.completion.requirements} (unanswered ${r.completion.unanswered})`);
  L.push('First pass');
  put('integrated / handed back', typeof r.firstPass === 'string' ? r.firstPass : `${r.firstPass.integrated} / ${r.firstPass.handedBack}${r.firstPass.mergeCommitsOnMain === undefined ? '' : ` (merge commits on main: ${r.firstPass.mergeCommitsOnMain})`}`);
  put('merge runs / first pass', typeof r.mergeFlow === 'string' ? r.mergeFlow : `${r.mergeFlow.runs} / ${r.mergeFlow.firstPass}; reds set down as main's ${r.mergeFlow.mainReds}; gates replayed ${r.mergeFlow.replayed}; over budget: ${r.mergeFlow.overBudget.join(', ') || 'none'}`);
  L.push('Friction');
  put('permission.refused', `${r.friction.permissionRefused} ${JSON.stringify(r.friction.permissionRefusedByTool)}`);
  put('refusals answered to agents', r.friction.refusalsToAgents);
  put('look.slow', r.friction.lookSlow);
  put('look duration (watch.looked)', typeof r.friction.lookMs === 'string' ? r.friction.lookMs : `${stat(r.friction.lookMs, ' ms')}, max ${r.friction.lookMs.max}, total ${r.friction.lookMs.total} ms`);
  L.push('Leftovers (now)');
  put('session trees under work/trees', r.leftovers.trees);
  put('session branches still present', r.leftovers.sessionBranchesPresent);
  put('landed branches not yet gone', r.leftovers.landedBranchesPresent);
  L.push('Ask Daoris');
  put('conversations / turns', `${r.askDaoris.conversations} / ${r.askDaoris.turns}`);
  put('tokens', tok(r.askDaoris.tokens));
  put('acts in full access / cards asked', `${r.askDaoris.actsInFullAccess} / ${r.askDaoris.cardsAsked}`);
  const y = r.lyntai;
  L.push('Lyntai (memory through the connector)');
  put('memory reads (searches and gets) / with hits', `${y.reads} / ${y.readsWithHits}`);
  put('searches / with hits', `${y.searches} / ${y.searchesWithHits}`);
  put('hits fetched in the same session', `${y.hitsFetchedSameSession} of ${y.searchesWithHits}`);
  put('hits cited', y.cited);
  put('gets / writes (kept) / proposes', `${y.gets} / ${y.writes} (${y.writesKept}) / ${y.proposes}`);
  put('latency per call', typeof y.latencyMs === 'string' ? y.latencyMs : `${stat(y.latencyMs, ' ms')}, max ${y.latencyMs.max ?? missing('memory tool call completion timestamps')}`);
  put('latency note', y.latencyNote);
  put('store row counts', y.store);
  for (const [label, rows] of [['Quest lifecycle', r.lifecycle], ['Workflow steps', r.workflow], ['Communication', r.communication], ["Person's queue", r.personQueue], ['Actors and roles', r.actors], ['Browser', r.browser]]) {
    L.push(label);
    if (Array.isArray(rows)) { if (!rows.length) put('source', missing(`${label} records in the selected scope and date range`)); for (const row of rows) put('record', row); }
    else put('records', rows);
  }
  L.push(`Budgets (${r.budgets.file})`);
  for (const check of r.checks) put(check.verdict === 'defect' ? `DEFECT ${check.metric}` : check.metric, check);
  return L.join('\n');
}

export function renderBuilders(rows) {
  const L = ['Builders (subagent transcripts)'];
  for (const b of rows) {
    if (!b.found) { L.push(`  ${b.id}: no transcript`); continue; }
    L.push(`  ${b.id}: ${b.wallMinutes} min wall, ${b.toolMinutes} in tools, ${b.modelMinutes} model, ${b.otherMinutes} between`);
    for (const [k, v] of Object.entries(b.byKind).sort(([, x], [, y]) => y.minutes - x.minutes)) L.push(`      ${k.padEnd(18)} ${String(v.calls).padStart(4)} calls ${String(v.minutes).padStart(7)} min`);
  }
  return L.join('\n');
}

async function main() {
  const o = parse(process.argv.slice(2));
  if (o.transcripts) {
    if (!o.ids || !existsSync(o.transcripts)) { console.error('dev-metrics: --transcripts <dir> needs --ids <a,b,...>'); return 2; }
    const rows = builderTimes(o.transcripts, o.ids.split(','));
    console.log(o.json ? JSON.stringify(rows, null, 2) : renderBuilders(rows));
    return 0;
  }
  if (!o.home || !o.logs) { console.error('dev-metrics: name --home <data> and --logs <logs>, or --transcripts <dir> --ids <a,b>'); return 2; }
  if (o.json) { o.since ??= o.until ?? new Date().toISOString().slice(0, 10); o.until ??= o.since; }
  const result = await collect(o);
  if (o.json) writeSnapshot(result);
  console.log(o.json ? JSON.stringify(result, null, 2) : render(result));
  return result.defects.length ? 1 : 0;
}

if (isMain(import.meta.url)) {
  try { process.exitCode = await main(); }
  catch (error) { console.error(`dev-metrics: ${error.message}`); process.exitCode = 2; }
}

#!/usr/bin/env node
/**
 * How this repository's development cycle performed over a date range, read without changing anything.
 *
 *   node tools/dev-metrics.mjs --home <data> --logs <logs> --since YYYY-MM-DD --until YYYY-MM-DD
 *        [--service <url>] [--workspaces a,b] [--work <dir>] [--merge-queue <file>] [--repo <checkout>] [--json]
 *   node tools/dev-metrics.mjs --transcripts <subagents dir> --ids <a,b,...> [--json]
 *
 * `--home` is an install's `data` folder, `--logs` its `logs` folder. A quest's record comes from the running
 * service (`/api/quests?workspace=`, a read) when `--service` is given, else from `<home>/quests/*.json`. A figure
 * whose source holds nothing is "not recorded" or "not read", with the place it would have to be logged: never a zero.
 * `--merge-queue` is this repository's `local/merge-queue.json`, where integration requests land or are handed back.
 * Builder mode times subagent transcripts with the clock `subagent-time.mjs` already keeps.
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { join, dirname } from 'node:path';
import { isMain } from './fsx.mjs';
import { clockOf } from './subagent-time.mjs';
import { MERGE_RECORD, readRecords, summarize } from './merge/merge-record.ts';

const HOUR = 3600000;
const TOKENS = ['input', 'output', 'cacheRead', 'cacheWrite'];
const NOT_COUNTED = 'not recorded';

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
const addTokens = (into, t) => { for (const k of TOKENS) into[k] += Number(t[k] ?? 0); return into; };
const zero = () => ({ input: 0, output: 0, cacheRead: 0, cacheWrite: 0 });

async function questsOf({ home, service, workspaces }) {
  if (service) {
    const out = [];
    for (const w of workspaces) {
      try {
        const res = await fetch(`${service.replace(/\/$/, '')}/api/quests?workspace=${encodeURIComponent(w)}`);
        if (res.ok) out.push(...await res.json());
      } catch { /* the service not answering leaves the home's own records */ }
    }
    return out;
  }
  return filesOf(join(home, 'quests'), (f) => f.endsWith('.json')).flatMap((f) => {
    const body = readJson(join(home, 'quests', f), []);
    return Array.isArray(body) ? body : [body];
  });
}

function sessionsOf(home) {
  const quests = new Map();
  const events = new Map();
  for (const f of filesOf(join(home, 'sessions'), (n) => n.endsWith('.events.jsonl'))) {
    const id = f.replace('.events.jsonl', '');
    const rows = readLines(join(home, 'sessions', f));
    events.set(id, rows);
    const first = rows.find((r) => r.kind === 'user' && /quest `#([0-9a-f]{8,})`/.test(r.text ?? ''));
    if (first) quests.set(id, /quest `#([0-9a-f]{8,})`/.exec(first.text)[1]);
  }
  return { quests, events };
}

function turnsOf(home) {
  return filesOf(join(home, 'usage-history'), (f) => /^turns-.*\.jsonl$/.test(f)).flatMap((f) => readLines(join(home, 'usage-history', f)));
}

function logEvents(logs, inRange) {
  return filesOf(logs, (f) => /\.jsonl$/.test(f)).flatMap((f) => readLines(join(logs, f))).filter((e) => inRange(e.time));
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
      if (r.status === 'completed' || r.status === 'failed') { c.endedAt = r.at; c.result = (r.content ?? []).map((p) => p.text ?? '').join('\n'); }
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
  const hit = (c) => c.endedAt && !/^No memory entry/.test(c.result ?? '');
  const hits = searches.filter(hit);
  const fetched = hits.filter((s) => calls.some((c) => c.kind === 'get' && c.session === s.session && c.startedAt >= s.startedAt));
  const latency = calls.filter((c) => c.endedAt).map((c) => Date.parse(c.endedAt) - Date.parse(c.startedAt));
  return {
    searches: searches.length,
    searchesWithHits: hits.length,
    hitsFetchedSameSession: fetched.length,
    cited: NOT_COUNTED,
    citedWhere: 'the handback or the session would have to log the memory entry ids a result names (nothing carries an entry id today)',
    gets: by('get').length,
    writes: by('write').length,
    proposes: by('propose').length,
    latencyMs: spread(latency),
    latencyNote: 'a call\'s first and last event in the session stream: includes any permission wait; the connector\'s own log (the mcp log) records only app.started and app.stopped',
  };
}

/** The Lyntai store's own row counts, read-only, through node:sqlite where this Node has it. */
async function storeCounts(home) {
  const file = join(home, 'memory.db');
  if (!existsSync(file)) return 'not recorded: no memory.db in the home';
  try {
    const { DatabaseSync } = await import('node:sqlite');
    const db = new DatabaseSync(file, { readOnly: true });
    const count = (t) => db.prepare(`select count(*) c from ${t}`).get().c;
    const out = { entries: count('lyntai_memory_entry') };
    try { out.nodes = count('lyntai_memory_node'); } catch { /* an older schema */ }
    db.close();
    return out;
  } catch { return 'not read: this Node has no node:sqlite'; }
}

/** Merge commits on the repository's main in the range, less the ones that only bring main into a branch. */
function mergesOnMain(repo, { since, until }) {
  try {
    const args = ['-C', repo, 'log', 'main', '--merges', '--format=%s'];
    if (since) args.push(`--since=${since}T00:00:00Z`);
    if (until) args.push(`--until=${until}T23:59:59Z`);
    return execFileSync('git', args, { encoding: 'utf8', windowsHide: true }).split('\n').filter((s) => s && !/^Merge (branch 'main'|main\b)/.test(s)).length;
  } catch { return 'not read'; }
}

function leftoversOf(home, work, landings) {
  const trees = ['trees']
    .flatMap((t) => filesOf(join(work, t), () => true).flatMap((ws) => filesOf(join(work, t, ws), () => true).flatMap((repo) => filesOf(join(work, t, ws, repo), (n) => /^s-/.test(n)))));
  const branches = readJson(join(home, 'session-branches.json'), { branches: [] }).branches ?? [];
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
  const workspaces = (options.workspaces ?? filesOf(join(home, 'trees'), () => true).join(',')).split(',').filter(Boolean);
  const work = options.work ?? join(dirname(home), 'work');
  const quests = await questsOf({ home, service, workspaces });
  const landings = readJson(join(home, 'landings.json'), { branches: [] }).branches ?? [];
  const { quests: sessionQuest, events } = sessionsOf(home);
  const turns = turnsOf(home);
  const logRows = logEvents(logs, inRange);

  const doneAt = (q) => q.accepted ?? q.updated;
  const filed = quests.filter((q) => inRange(q.filed));
  const done = quests.filter((q) => q.status === 'Done' && inRange(doneAt(q)));
  const onLine = (id) => landings.find((l) => l.quest === id && l.goneAt && ['on-line', 'merged'].includes(l.removedAs));

  const served = new Map();
  for (const [session, q] of sessionQuest) served.set(q, [...(served.get(q) ?? []), session]);
  const perQuest = [...new Map([...filed, ...done].map((q) => [q.id, q])).values()].map((q) => {
    const sessions = served.get(q.id) ?? [];
    const tokens = turns.filter((t) => sessions.includes(t.session)).reduce(addTokens, zero());
    return { quest: q.id, sessions: sessions.length, tokens };
  });
  const perActor = {};
  for (const t of turns.filter((t) => inRange(t.ended ?? t.written))) perActor[t.actor ?? 'unknown'] = addTokens(perActor[t.actor ?? 'unknown'] ?? zero(), t);

  const answers = done.flatMap((q) => (q.answers ?? []));
  const requirements = done.reduce((n, q) => n + (q.requirements?.length ?? 0), 0);
  const met = answers.filter((a) => a.met !== undefined).length;
  const departed = answers.filter((a) => a.departed !== undefined).length;

  const queue = mergeQueue ? readJson(mergeQueue, null) : null;
  const firstPass = queue
    ? { integrated: (queue.landed ?? []).filter((l) => inRange(l.at)).length, handedBack: (queue.handedBack ?? []).filter((l) => inRange(l.at)).length }
    : 'not read: pass --merge-queue <local/merge-queue.json>';

  if (options.repo && firstPass && typeof firstPass === 'object') firstPass.mergeCommitsOnMain = mergesOnMain(options.repo, options);
  const recordFile = options.mergeRecord ?? (options.repo ? join(options.repo, MERGE_RECORD) : null);
  const mergeFlow = recordFile ? summarize(readRecords(recordFile).filter((each) => inRange(each.at))) : 'not read: pass --merge-record <local/merge-record.jsonl>';
  const askTurns = turns.filter((t) => t.actor === 'ask-daoris' && inRange(t.ended ?? t.written));
  const lyntai = lyntaiOf(memoryCalls(events, inRange));
  lyntai.store = await storeCounts(home);

  return {
    range: { since: options.since ?? null, until: options.until ?? null },
    source: { quests: service ? `service ${service}` : 'home quests folder', workspaces, questRecords: quests.length },
    throughput: {
      questsFiled: filed.length,
      questsDone: done.length,
      asksFiled: new Set(filed.map((q) => q.from).filter((f) => /^ask #/.test(f ?? ''))).size,
      asksClosed: `${NOT_COUNTED}: the home keeps no ask records (its asks folder is empty); an ask's close would have to be logged when its last quest closes`,
    },
    leadTime: {
      filedToDone: hoursOf(done.map((q) => Date.parse(doneAt(q)) - Date.parse(q.filed))),
      doneToLine: hoursOf(done.map((q) => { const l = onLine(q.id); return l ? Date.parse(l.goneAt) - Date.parse(doneAt(q)) : NaN; })),
      doneNotOnLine: done.filter((q) => !onLine(q.id)).length,
    },
    cost: { perQuest, perActor, total: Object.values(perActor).reduce(addTokens, zero()) },
    completion: { requirements, met, departed, unanswered: requirements - met - departed },
    firstPass,
    mergeFlow,
    friction: frictionOf(logRows),
    leftovers: leftoversOf(home, work, landings),
    askDaoris: {
      conversations: new Set(askTurns.map((t) => t.session)).size,
      turns: askTurns.length,
      tokens: askTurns.reduce(addTokens, zero()),
      actsInFullAccess: logRows.filter((e) => e.event === 'help.pressed').length,
      cardsAsked: logRows.filter((e) => e.event === 'person.confirmation' && e.data?.state === 'asked').length,
    },
    lyntai,
  };
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

const tok = (t) => `in ${t.input}, out ${t.output}, cacheRead ${t.cacheRead}, cacheWrite ${t.cacheWrite}`;
const stat = (s, unit = '') => (s.n ? `n=${s.n}, median ${s.median ?? s.medianHours}${unit}, mean ${s.mean ?? s.meanHours}${unit}, p90 ${s.p90 ?? s.p90Hours}${unit}` : 'none');

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
  const quests = r.cost.perQuest.filter((q) => q.sessions);
  put('quests with sessions found', `${quests.length} of ${r.cost.perQuest.length}`);
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
  put('look duration (watch.looked)', `${stat(r.friction.lookMs, ' ms')}, max ${r.friction.lookMs.max ?? '-'}, total ${r.friction.lookMs.total} ms`);
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
  put('searches / with hits', `${y.searches} / ${y.searchesWithHits}`);
  put('hits fetched in the same session', `${y.hitsFetchedSameSession} of ${y.searchesWithHits}`);
  put('hits cited', `${y.cited}: ${y.citedWhere}`);
  put('gets / writes / proposes', `${y.gets} / ${y.writes} / ${y.proposes}`);
  put('latency per call', `${stat(y.latencyMs, ' ms')}, max ${y.latencyMs.max ?? '-'}`);
  put('latency note', y.latencyNote);
  put('store row counts', y.store);
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

function parse(argv) {
  const o = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--json') o.json = true;
    else if (a.startsWith('--')) o[a.slice(2).replace(/-(\w)/g, (_, c) => c.toUpperCase())] = argv[++i];
  }
  return o;
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
  const result = await collect(o);
  console.log(o.json ? JSON.stringify(result, null, 2) : render(result));
  return 0;
}

if (isMain(import.meta.url)) process.exitCode = await main();

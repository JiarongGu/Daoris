import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { spawnGit } from './fsx.mjs';
import { collect, builderTimes, render, writeSnapshot } from './dev-metrics.mjs';
import { homeEntry } from '../src/Daoris.Cli/src/platform/home.ts';

const root = join('_fixtures', 'dev-metrics-fixture');
const home = join(root, 'data');
const logs = join(root, 'logs');
const questsFile = join(root, 'quests.json');
const jl = (rows) => rows.map((r) => JSON.stringify(r)).join('\n') + '\n';

const quest = (id, filed, accepted, answers, requirements = 2) => ({
  id, from: `ask #${id.slice(0, 3)}`, to: 'widget-app', title: id, status: 'Done', filed, updated: accepted, accepted,
  requirements: Array.from({ length: requirements }, (_, i) => ({ quote: `q${i}` })), answers,
});

before(() => {
  rmSync(root, { recursive: true, force: true });
  mkdirSync(homeEntry(home, 'usage-history'), { recursive: true });
  mkdirSync(homeEntry(home, 'sessions'), { recursive: true });
  mkdirSync(join(root, 'work', 'trees', 'ws', 'widget-app', 's-aaaa1111'), { recursive: true });
  mkdirSync(logs, { recursive: true });
  writeFileSync(questsFile, JSON.stringify([
    quest('aaa111111111', '2026-01-02T00:00:00Z', '2026-01-02T04:00:00Z', [{ requirement: 1, met: 'ok' }, { requirement: 2, departed: 'chose x' }]),
    quest('bbb222222222', '2026-01-02T01:00:00Z', '2026-01-02T03:00:00Z', [{ requirement: 1, met: 'ok' }, { requirement: 2, met: 'ok' }]),
    quest('ccc333333333', '2025-12-01T00:00:00Z', '2025-12-01T01:00:00Z', []),
  ]));
  writeFileSync(homeEntry(home, 'landings'),JSON.stringify({ branches: [
    { quest: 'aaa111111111', session: 'aaaa1111', landedAt: '2026-01-02T04:10:00Z', goneAt: '2026-01-02T06:00:00Z', removedAs: 'on-line' },
    { quest: 'bbb222222222', session: 'bbbb2222', landedAt: '2026-01-02T03:10:00Z' },
  ] }));
  writeFileSync(homeEntry(home, 'session-branches'),JSON.stringify({ branches: [{ branch: 'x/s-1', at: '2026-01-02T00:00:00Z' }] }));
  const turn = (session, actor, input, output, cacheRead, cacheWrite, ended) => ({ kind: 'turn', session, actor, input, output, cacheRead, cacheWrite, ended });
  writeFileSync(homeEntry(home, 'usage-history', 'turns-2026-01.jsonl'),jl([
    turn('aaaa1111', 'build', 10, 100, 1000, 50, '2026-01-02T02:00:00Z'),
    turn('aaaa1111', 'build', 5, 50, 500, 25, '2026-01-02T03:00:00Z'),
    turn('bbbb2222', 'build', 1, 2, 3, 4, '2026-01-02T02:30:00Z'),
    turn('dddd4444', 'ask-daoris', 7, 70, 700, 7, '2026-01-02T05:00:00Z'),
    turn('dddd4444', 'ask-daoris', 1, 1, 1, 1, '2026-02-02T05:00:00Z'),
  ]));
  const user = (id) => ({ seq: 1, at: '2026-01-02T01:00:00Z', kind: 'user', text: `Your target is quest \`#${id}\`, asked by x` });
  const call = (id, title, at, input) => ({ seq: 2, at, kind: 'tool', id, title, status: 'in_progress', input: JSON.stringify(input) });
  const done = (id, at, text) => ({ seq: 3, at, kind: 'tool', id, status: 'completed', content: [{ type: 'text', text }] });
  writeFileSync(homeEntry(home, 'sessions', 'aaaa1111.events.jsonl'),jl([
    user('aaa111111111'),
    call('t1', 'memory_search', '2026-01-02T01:10:00Z', { query: 'one' }), done('t1', '2026-01-02T01:10:02Z', 'No memory entry in workspace `w` matches `one`.'),
    call('t2', 'mcp__srv__memory_search', '2026-01-02T01:20:00Z', { query: 'two' }), done('t2', '2026-01-02T01:20:04Z', '1. entry: a note'),
    call('t3', 'mcp__srv__memory_get', '2026-01-02T01:30:00Z', { id: 'a' }), done('t3', '2026-01-02T01:30:01Z', 'the note'),
    call('t4', 'memory_write', '2026-01-02T01:40:00Z', { content: 'c' }), done('t4', '2026-01-02T01:40:01Z', 'written'),
  ]));
  writeFileSync(homeEntry(home, 'sessions', 'bbbb2222.events.jsonl'),jl([
    user('bbb222222222'),
    call('u1', 'memory_search', '2026-01-02T01:10:00Z', { query: 'three' }), done('u1', '2026-01-02T01:10:06Z', '1. entry: other'),
  ]));
  writeFileSync(join(logs, '2026-01-02.desktop.jsonl'), jl([
    { time: '2026-01-02T02:00:00Z', event: 'permission.refused', data: { tool: 'Bash' } },
    { time: '2026-01-02T02:00:01Z', event: 'look.slow', data: { ms: 5000 } },
    { time: '2026-01-02T02:00:02Z', event: 'watch.looked', data: { ms: 100 } },
    { time: '2026-01-02T02:00:03Z', event: 'watch.looked', data: { ms: 300 } },
    { time: '2026-01-02T02:00:04Z', event: 'help.pressed', data: { session: 'dddd4444' } },
    { time: '2026-01-02T02:00:05Z', event: 'person.confirmation', data: { state: 'asked' } },
    { time: '2026-01-02T02:00:06Z', event: 'person.confirmation', data: { state: 'shown' } },
  ]));
  writeFileSync(join(logs, '2026-01-02.host.jsonl'), jl([{ time: '2026-01-02T02:00:00Z', event: 'person.refused', data: { code: 'person-only' } }]));
  writeFileSync(join(root, 'merge-queue.json'), JSON.stringify({
    landed: [{ branch: 'a', at: '2026-01-02T05:00:00Z' }, { branch: 'b', at: '2026-01-02T06:00:00Z' }],
    handedBack: [{ branch: 'c', at: '2026-01-02T07:00:00Z' }, { branch: 'old', at: '2025-01-01T00:00:00Z' }],
  }));
});

after(() => rmSync(root, { recursive: true, force: true }));

const options = () => ({ home, logs, quests: questsFile, since: '2026-01-02', until: '2026-01-02', mergeQueue: join(root, 'merge-queue.json') });

test('throughput, lead time and completion are read from the quests and the landing records', async () => {
  const r = await collect(options());
  assert.equal(r.throughput.questsFiled, 2);
  assert.equal(r.throughput.questsDone, 2);
  assert.equal(r.throughput.asksFiled, 2);
  assert.equal(r.leadTime.filedToDone.n, 2);
  assert.equal(r.leadTime.filedToDone.medianHours, 3);
  assert.equal(r.leadTime.doneToLine.n, 1);
  assert.equal(r.leadTime.doneToLine.medianHours, 2);
  assert.deepEqual(r.completion, { requirements: 4, met: 3, departed: 1, unanswered: 0 });
  assert.deepEqual(r.firstPass, { integrated: 2, handedBack: 1 });
});

test('cost sums the turns of the sessions that served each quest, and by actor inside the range', async () => {
  const r = await collect(options());
  const a = r.cost.perQuest.find((q) => q.quest === 'aaa111111111');
  assert.deepEqual(a.tokens, { input: 15, output: 150, cacheRead: 1500, cacheWrite: 75 });
  assert.equal(a.sessions, 1);
  assert.deepEqual(r.cost.perActor['ask-daoris'], { input: 7, output: 70, cacheRead: 700, cacheWrite: 7 });
});

test('friction, leftovers and Ask Daoris come from the logs and the home', async () => {
  const r = await collect(options());
  assert.equal(r.friction.permissionRefused, 1);
  assert.equal(r.friction.refusalsToAgents, 1);
  assert.equal(r.friction.lookSlow, 1);
  assert.deepEqual([r.friction.lookMs.n, r.friction.lookMs.max], [2, 300]);
  assert.equal(r.leftovers.trees, 1);
  assert.equal(r.leftovers.sessionBranchesPresent, 1);
  assert.equal(r.leftovers.landedBranchesPresent, 1);
  assert.deepEqual([r.askDaoris.conversations, r.askDaoris.turns, r.askDaoris.actsInFullAccess, r.askDaoris.cardsAsked], [1, 1, 1, 1]);
});

test('Lyntai use counts the connector\'s memory calls and says what is not recorded', async () => {
  const r = await collect(options());
  assert.equal(r.lyntai.searches, 3);
  assert.equal(r.lyntai.searchesWithHits, 2);
  assert.equal(r.lyntai.hitsFetchedSameSession, 1);
  assert.equal(r.lyntai.writes, 1);
  assert.equal(r.lyntai.latencyMs.n, 5);
  assert.equal(r.lyntai.latencyMs.max, 6000);
  assert.match(r.lyntai.cited, /^not recorded: .*handback/);
  assert.equal(r.lyntai.proposes, 0);
});

test('memory reads, hits and writes are counted for a Codex session\'s result shape too, and a get that found nothing is no hit', async () => {
  const dir = join(root, 'shape-home');
  mkdirSync(homeEntry(dir, 'sessions'), { recursive: true });
  const user = { seq: 1, at: '2026-01-02T01:00:00Z', kind: 'user', text: 'Your target is quest `#aaa111111111`, asked by x' };
  const call = (id, title, at) => ({ seq: 2, at, kind: 'tool', id, title, status: 'in_progress', input: '{}' });
  // Codex completes a call with its result wrapped as JSON in `output`, and the title only on the in-progress row.
  const wrapped = (id, at, text) => ({ seq: 3, at, kind: 'tool', id, status: 'completed', output: JSON.stringify({ result: { content: [{ type: 'text', text }] }, error: null }) });
  writeFileSync(homeEntry(dir, 'sessions', 'aaaa1111.events.jsonl'), jl([
    user,
    call('c1', 'mcp.daoris-knowledge.memory_search', '2026-01-02T01:10:00Z'), wrapped('c1', '2026-01-02T01:10:02Z', 'No memory entry in workspace `w` matches `x`.'),
    call('c2', 'mcp.daoris-knowledge.memory_search', '2026-01-02T01:11:00Z'), wrapped('c2', '2026-01-02T01:11:02Z', 'Memory in workspace `w`:\n\n- `r:e1` (revision 1): a fact'),
    call('c3', 'mcp.daoris-knowledge.memory_get', '2026-01-02T01:12:00Z'), wrapped('c3', '2026-01-02T01:12:01Z', 'No memory entry `r:zz`. References come from memory_search.'),
    call('c4', 'mcp.daoris-knowledge.memory_get', '2026-01-02T01:13:00Z'), wrapped('c4', '2026-01-02T01:13:01Z', '`r:e1`, revision 1\n\na fact'),
    call('c5', 'mcp.daoris-knowledge.memory_write', '2026-01-02T01:14:00Z'), wrapped('c5', '2026-01-02T01:14:01Z', 'Kept as operation `abc`, staged: ...'),
    call('c6', 'mcp.daoris-knowledge.memory_write', '2026-01-02T01:15:00Z'), wrapped('c6', '2026-01-02T01:15:01Z', 'Refused: this session works in no repository. Nothing was kept.'),
  ]));
  const r = await collect({ home: dir, logs, quests: questsFile, since: '2026-01-02', until: '2026-01-02' });
  assert.equal(r.lyntai.searches, 2);
  assert.equal(r.lyntai.searchesWithHits, 1);
  assert.equal(r.lyntai.reads, 4, 'a read is a search or a get');
  assert.equal(r.lyntai.readsWithHits, 2, 'a hit is a read that returned an entry');
  assert.equal(r.lyntai.writes, 2, 'every write call is counted');
  assert.equal(r.lyntai.writesKept, 1, 'a kept write is one the connector said it staged');
  assert.match(render(r), /memory reads/);
});

test('the store\'s memory operations are counted by state beside its entries, and no store is no figure', async () => {
  const dir = join(root, 'ops-home');
  mkdirSync(dir, { recursive: true });
  const { DatabaseSync } = await import('node:sqlite');
  const db = new DatabaseSync(homeEntry(dir, 'knowledge'));
  db.exec('create table memory_ops (id text primary key, state text not null)');
  for (const [id, state] of [['a', 'staged'], ['b', 'applied'], ['c', 'applied'], ['d', 'dropped']]) db.prepare('insert into memory_ops values (?, ?)').run(id, state);
  db.close();
  const r = await collect({ home: dir, logs, quests: questsFile, since: '2026-01-02', until: '2026-01-02' });
  assert.deepEqual(r.lyntai.store.operations, { staged: 1, applied: 2, dropped: 1 });
  const none = await collect({ home, logs, quests: questsFile, since: '2026-01-02', until: '2026-01-02' });
  assert.match(none.lyntai.store.operations, /^not recorded/);
});

test('the table says "not recorded" where nothing is logged, and an absent source is never a zero', async () => {
  const r = await collect({ home, logs, quests: questsFile, since: '2026-01-02', until: '2026-01-02' });
  assert.equal(r.firstPass, 'not recorded: merge queue records; pass --merge-queue <local/merge-queue.json>');
  const text = render(r);
  assert.match(text, /not recorded/);
  assert.match(text, /Lead time/);
});

test('builder time folds the transcripts into wall, tool and model time by kind', () => {
  const dir = join(root, 'subagents');
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, 'agent-abc123.jsonl'), jl([
    { type: 'user', timestamp: '2026-01-02T00:00:00Z', message: { content: 'go' } },
    { type: 'assistant', timestamp: '2026-01-02T00:00:10Z', message: { content: [{ type: 'tool_use', id: 'x', name: 'Bash', input: { command: 'dotnet build a' } }] } },
    { type: 'user', timestamp: '2026-01-02T00:01:10Z', message: { content: [{ type: 'tool_result', tool_use_id: 'x' }] } },
  ]));
  const [b] = builderTimes(dir, ['abc123', 'missing']);
  assert.equal(b.id, 'abc123');
  assert.equal(b.wallMinutes, 1.17);
  assert.equal(b.toolMinutes, 1);
  assert.equal(b.byKind['run:build'].minutes, 1);
  assert.equal(builderTimes(dir, ['missing'])[0].found, false);
});

async function matrixHome(name) {
  const { DatabaseSync } = await import('node:sqlite');
  const dir = join(root, name);
  const data = join(dir, 'data');
  mkdirSync(data, { recursive: true });
  mkdirSync(join(dir, 'logs'), { recursive: true });
  const db = new DatabaseSync(homeEntry(data, 'knowledge'));
  db.exec(`
    CREATE TABLE sessions (id TEXT, quest TEXT, took_quest TEXT, entry TEXT, created TEXT, updated TEXT, state TEXT, actor TEXT, from_session TEXT, ran TEXT);
    CREATE TABLE asks (id TEXT, state TEXT, asked TEXT, updated TEXT, quests TEXT);
    CREATE TABLE quest_log (quest TEXT, kind TEXT, at TEXT, payload TEXT);
    CREATE TABLE channel (id TEXT, ask TEXT, at TEXT, to_repo TEXT, session TEXT);
    CREATE TABLE channel_acks (ask TEXT, entry TEXT, message TEXT, session TEXT, state TEXT, at TEXT);
    CREATE TABLE event_journal (record TEXT, family TEXT, change TEXT, state_new TEXT, at INTEGER, quest TEXT);
  `);
  const insert = (table, row) => db.prepare(`INSERT INTO ${table} (${Object.keys(row).join(',')}) VALUES (${Object.keys(row).map(() => '?').join(',')})`).run(...Object.values(row));
  const at = (seconds) => new Date(Date.parse('2026-01-02T00:00:00Z') + seconds * 1000).toISOString();
  for (const [id, q, ask] of [['lead', '111111111111', 'a11111'], ['other', '222222222222', 'b22222']]) {
    insert('sessions', { id, quest: q, entry: `ask:${ask}/widget-app`, created: at(8), updated: at(180), state: 'Completed', actor: 'build', ran: JSON.stringify({ models: ['reported-model'] }) });
    insert('asks', { id: ask, state: 'Closed', asked: at(0), updated: at(190), quests: JSON.stringify([q]) });
    insert('quest_log', { quest: q, kind: 'taken', at: at(5), payload: '{}' });
    insert('quest_log', { quest: q, kind: 'done', at: at(100), payload: '{}' });
    insert('event_journal', { record: `session:${id}`, family: 'sessions', change: 'state', state_new: 'Working', quest: q, at: Date.parse(at(10)) });
  }
  insert('channel', { id: 'post', ask: 'a11111', at: at(20), to_repo: 'widget-app', session: 'sender' });
  insert('channel_acks', { ask: 'a11111', entry: 'ask:a11111/widget-app', message: 'post', session: 'lead', state: 'read', at: at(24) });
  db.close();
  const quests = join(dir, 'quests.json');
  writeFileSync(quests, JSON.stringify([
    { id: '111111111111', from: 'ask #a11111', filed: at(0), updated: at(100), status: 'Done', accepted: at(120) },
    { id: '222222222222', from: 'ask #b22222', filed: at(0), updated: at(100), status: 'Done' },
  ]));
  mkdirSync(homeEntry(data, 'sessions'), { recursive: true });
  writeFileSync(homeEntry(data, 'sessions', 'lead.events.jsonl'), jl([
    { kind: 'user', id: 'word', reaches: 'turn-end', at: at(30), origin: 'person' },
    { kind: 'user', id: 'word', at: at(40), origin: 'person' },
    { kind: 'receipt', id: 'word', receipt: 'sent', at: at(41) },
    { kind: 'user', id: 'answer-word', helper: 'helper-session', at: at(55) },
    { kind: 'receipt', id: 'answer-word', receipt: 'sent', at: at(56) },
  ]));
  mkdirSync(homeEntry(data, 'helpers'), { recursive: true });
  writeFileSync(homeEntry(data, 'helpers', 'h.ask.json'), JSON.stringify({ id: 'h', asker: 'lead', at: at(15), question: 'contract?' }));
  writeFileSync(homeEntry(data, 'helpers', 'h.state.json'), JSON.stringify({ id: 'h', helper: 'helper-session', state: 'answered', at: at(50), handed: 'answer-word' }));
  mkdirSync(homeEntry(data, 'workflows', 'runs'), { recursive: true });
  mkdirSync(homeEntry(data, 'workflows', 'units'), { recursive: true });
  writeFileSync(homeEntry(data, 'workflows', 'runs', '111111111111.json'), JSON.stringify({ run: '111111111111', ask: 'a11111', at: at(8), workflow: 'fixture', version: 1 }));
  writeFileSync(homeEntry(data, 'workflows', 'fixture.json'), JSON.stringify({ id: 'fixture', versions: [{ version: 1, schema: 2, graph: { nodes: [{ id: 'check', kind: 'check', reach: 'performed' }, { id: 'review', kind: 'review', reach: 'pressed' }] } }] }));
  writeFileSync(homeEntry(data, 'workflows', 'units', 'record.json'), JSON.stringify({
    unit: 'run:111111111111',
    statuses: { check: { node: 'check', state: 'reached', since: at(60), attempt: 1, by: 'plugin' }, review: { node: 'review', state: 'active', holds: 'Waiting for Accept', since: at(120), attempt: 1 } },
    attempts: [{ node: 'check', attempt: 1, recordedAt: at(60), state: 'settled', outcome: 'reached', resolution: 'happened', resolvedAt: at(90), call: { step: 'check' } }],
    history: [{ at: at(55), why: 'person-again', was: { node: 'check', state: 'unknown', since: at(50), attempt: 0 } }],
  }));
  writeFileSync(homeEntry(data, 'landings'), JSON.stringify({ branches: [{ quest: '111111111111', session: 'lead', landedAt: at(150) }] }));
  writeFileSync(join(dir, 'logs', 'driver.jsonl'), jl([
    { event: 'watch.looked', time: at(6), data: { ms: 4000 } },
    { event: 'watch.looked', time: at(12), data: { ms: 8000 } },
  ]));
  return { home: data, logs: join(dir, 'logs'), quests, since: '2026-01-02', until: '2026-01-02', now: at(4000) };
}

test('matrix reads database links, actual lifecycle clocks and ask closures without prompt text', async () => {
  const r = await collect(await matrixHome('matrix-records'));
  assert.equal(r.cost.perQuest.find(q => q.quest === '111111111111').sessions, 1);
  assert.equal(r.throughput.asksClosed, 2);
  const q = r.lifecycle.find(q => q.quest === '111111111111');
  assert.equal(q.stages.taken, '2026-01-02T00:00:05.000Z');
  assert.equal(q.stages.started, '2026-01-02T00:00:10.000Z');
  assert.equal(q.elapsedMs.takenToStarted, 5000);
  assert.equal(q.stages.questAccepted, '2026-01-02T00:02:00.000Z');
  assert.match(q.elapsedMs.acceptedToLanded, /^not recorded:/);
  assert.match(q.stages.firstCommit, /^not recorded: .*commit/);
  assert.match(q.stages.pushed, /^not recorded:/);
});

test('case scopes related records and usage, while preserving global look provenance', async () => {
  const o = await matrixHome('matrix-case');
  const r = await collect({ ...o, case: 'ask #a11111' });
  assert.deepEqual(r.cost.perQuest.map(q => q.quest), ['111111111111']);
  assert.equal(r.throughput.asksClosed, 1);
  assert.deepEqual(r.scope.sessions, ['lead']);
  assert.equal(r.lifecycle.length, 1);
  assert.equal(r.workflow.length, 1);
  assert.equal(r.communication.channel[0].latencyMs, 4000);
  assert.match(r.scope.looks, /global/);
  await assert.rejects(collect({ ...o, case: 'unknown' }), /case.*unknown/i);
});

test('workflow attempts keep work clocks, hold clearer and unknown wait boundaries explicit', async () => {
  const options = await matrixHome('matrix-workflow');
  const { DatabaseSync } = await import('node:sqlite');
  const db = new DatabaseSync(homeEntry(options.home, 'knowledge'));
  db.exec("UPDATE sessions SET state = 'AwaitingPerson' WHERE id = 'lead'");
  db.close();
  const r = await collect(options);
  const step = r.workflow[0].steps.find(s => s.node === 'check');
  assert.equal(step.workMs, 30000);
  assert.equal(step.waitMs, 5000);
  assert.equal(step.clearedBy, 'person');
  assert.equal(r.personQueue.items.find(i => i.node === 'review').ageMs, 3880000);
  assert.equal(r.personQueue.items.find(i => i.node === 'review').waiter, 'person');
  assert.equal(r.personQueue.items.find(i => i.session === 'lead').ageMs, 3820000);
  assert.match(r.personQueue.items.find(i => i.node === 'review').surfacedAt, /^not recorded:/);
});

test('communications pair identity receipts and never call turn-end or prompt-taking a turn-start', async () => {
  const r = await collect(await matrixHome('matrix-comms'));
  assert.equal(r.communication.helpers[0].latencyMs, 41000);
  assert.equal(r.communication.words[0].deliveredMs, 11000);
  assert.match(r.communication.words[0].turnStartedMs, /^not recorded: .*turn/);
  assert.match(r.communication.questResponses[0].latencyMs, /^not recorded:/);
});

test('budgets produce defect rows and an empty home reports missing figures rather than fabricated zeroes', async () => {
  const r = await collect(await matrixHome('matrix-budgets'));
  assert.ok(r.defects.some(d => d.metric === 'look.medianMs' && d.value === 6000));
  assert.match(render(r), /DEFECT.*look.medianMs/);
  const empty = await collect({ home: join(root, 'absent'), logs: join(root, 'absent-logs') });
  assert.match(empty.throughput.questsFiled, /^not recorded:/);
  assert.match(empty.friction.lookMs, /^not recorded:/);
  assert.match(empty.cost.total, /^not recorded:/);
  assert.match(empty.personQueue.coverage, /^not recorded:/);
});

test('daily JSON snapshots retain separate case receipts and omit machine paths', async () => {
  const o = await matrixHome('matrix-snapshot');
  const out = join(root, 'snapshots');
  const a = await collect({ ...o, case: '111111111111' });
  const file = writeSnapshot(a, { directory: out, date: '2026-01-02' });
  assert.ok(existsSync(file));
  writeSnapshot(await collect({ ...o, case: '222222222222' }), { directory: out, date: '2026-01-02' });
  const snapshot = JSON.parse(readFileSync(file, 'utf8'));
  assert.equal(snapshot.receipts.length, 2);
  assert.equal(snapshot.date, '2026-01-02');
  assert.ok(!JSON.stringify(snapshot).includes(o.home));
});

test('the CLI writes its daily snapshot, accepts case ids and reports defects as policy failures', async () => {
  const o = await matrixHome('matrix-cli');
  const cwd = join(root, 'cli-output');
  mkdirSync(cwd, { recursive: true });
  const result = spawnSync(process.execPath, [resolve('tools/dev-metrics.mjs'), '--home', resolve(o.home), '--logs', resolve(o.logs), '--quests', resolve(o.quests), '--case', '#111111111111', '--since', o.since, '--until', o.until, '--json'], { cwd, encoding: 'utf8', windowsHide: true });
  assert.equal(result.status, 1, result.stderr);
  assert.equal(JSON.parse(result.stdout).scope.quests[0], '111111111111');
  assert.equal(JSON.parse(readFileSync(join(cwd, 'docs', 'metrics', '2026-01-02.json'), 'utf8')).receipts.length, 1);
  const refused = spawnSync(process.execPath, [resolve('tools/dev-metrics.mjs'), '--case'], { cwd, encoding: 'utf8', windowsHide: true });
  assert.equal(refused.status, 2);
  assert.match(refused.stderr, /--case needs a value/);
});

test('missing token counters remain unknown, case totals exclude other asks and dates', async () => {
  const o = await matrixHome('matrix-usage');
  mkdirSync(homeEntry(o.home, 'usage-history'), { recursive: true });
  writeFileSync(homeEntry(o.home, 'usage-history', 'turns-2026-01.jsonl'), jl([
    { session: 'lead', actor: 'build', models: ['actual'], input: 7, output: 2, ended: '2026-01-02T00:02:00Z' },
    { session: 'other', actor: 'build', models: ['other'], input: 999, output: 999, ended: '2026-01-02T00:02:00Z' },
    { session: 'lead', actor: 'build', input: 999, output: 999, ended: '2026-01-03T00:02:00Z' },
  ]));
  const r = await collect({ ...o, case: 'a11111' });
  assert.equal(r.cost.total.input, 7);
  assert.match(r.cost.total.cacheRead, /^not recorded:/);
  assert.equal(r.cost.perRole[0].tokens.output, 2);
  assert.equal(r.cost.perModel[0].name, 'actual');
  assert.equal(r.cost.perAsk[0].tokens.input, 7);
});

test('missing acknowledgements stay visible and a send is not reported as a read', async () => {
  const o = await matrixHome('matrix-unread');
  const { DatabaseSync } = await import('node:sqlite');
  const db = new DatabaseSync(homeEntry(o.home, 'knowledge'));
  db.exec("UPDATE channel_acks SET state = 'sent'");
  db.close();
  const r = await collect(o);
  assert.match(r.communication.channel[0].latencyMs, /^not recorded:/);
  assert.equal(r.communication.channel[0].deliveredMs, 4000);
});

test('a bound graph reports every node, including nodes with no status, and actual observed push/release clocks', async () => {
  const o = await matrixHome('matrix-graph');
  const path = homeEntry(o.home, 'workflows', 'runs', '111111111111.json');
  writeFileSync(path, JSON.stringify({ ...JSON.parse(readFileSync(path, 'utf8')), workflow: 'delivery', version: 1 }));
  writeFileSync(homeEntry(o.home, 'workflows', 'delivery.json'), JSON.stringify({ id: 'delivery', versions: [{ version: 1, schema: 2, graph: { nodes: [{ id: 'push', kind: 'push' }, { id: 'release', kind: 'sample/release' }, { id: 'unstarted', kind: 'check' }], wires: [] } }] }));
  const unit = homeEntry(o.home, 'workflows', 'units', 'record.json');
  writeFileSync(unit, JSON.stringify({ unit: 'run:111111111111', statuses: { push: { node: 'push', state: 'reached', since: '2026-01-02T00:02:40Z', attempt: 1 }, release: { node: 'release', state: 'reached', since: '2026-01-02T00:03:20Z', attempt: 1 } }, observations: [{ node: 'push', commit: 'tip', state: 'reached', at: '2026-01-02T00:03:00Z' }, { node: 'release', commit: 'tip', state: 'reached', at: '2026-01-02T00:04:00Z' }] }));
  const r = await collect({ ...o, case: '111111111111' });
  assert.ok(r.workflow[0].steps.some(s => s.node === 'unstarted' && /^not recorded:/.test(s.state)));
  assert.equal(r.lifecycle[0].stages.pushed, '2026-01-02T00:03:00Z');
  assert.equal(r.lifecycle[0].stages.released, '2026-01-02T00:04:00Z');
  assert.equal(r.workflow[0].steps.find(s => s.node === 'push').workMs, 20000);
});

test('the first commit uses a real fixture tree after the recorded base, never a queued/start clock', async () => {
  const o = await matrixHome('matrix-commit');
  const tree = resolve(root, 'commit-tree');
  mkdirSync(tree, { recursive: true });
  const git = (args, at) => {
    const result = spawnGit(['-C', tree, ...args], { encoding: 'utf8', env: { ...process.env, GIT_AUTHOR_DATE: at, GIT_COMMITTER_DATE: at } });
    assert.equal(result.status, 0, result.stderr);
    return result.stdout.trim();
  };
  git(['init', '--quiet'], '2026-01-02T00:00:00Z');
  const commit = ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--allow-empty', '--quiet', '-m', 'fixture'];
  git(commit, '2026-01-02T00:00:00Z');
  const base = git(['rev-parse', 'HEAD'], '2026-01-02T00:00:00Z');
  git(commit, '2026-01-02T00:00:25Z');
  const { DatabaseSync } = await import('node:sqlite');
  const db = new DatabaseSync(homeEntry(o.home, 'knowledge'));
  db.exec('ALTER TABLE sessions ADD COLUMN tree TEXT; ALTER TABLE sessions ADD COLUMN base_commit TEXT');
  db.prepare("UPDATE sessions SET tree = ?, base_commit = ? WHERE id = 'lead'").run(tree, base);
  db.close();
  const r = await collect({ ...o, case: '111111111111' });
  assert.equal(Date.parse(r.lifecycle[0].stages.firstCommit), Date.parse('2026-01-02T00:00:25Z'));
  assert.equal(r.lifecycle[0].elapsedMs.startedToFirstCommit, 15000);
});

test('an unreadable prior snapshot is kept, and a case alias replaces the same receipt', async () => {
  const o = await matrixHome('matrix-retain');
  const directory = join(root, 'retained');
  const r = await collect({ ...o, case: '#111111111111' });
  const file = writeSnapshot(r, { directory, date: '2026-01-02' });
  writeSnapshot(await collect({ ...o, case: '11111111' }), { directory, date: '2026-01-02' });
  assert.equal(JSON.parse(readFileSync(file, 'utf8')).receipts.length, 1);
  writeFileSync(file, '{torn');
  assert.throws(() => writeSnapshot(r, { directory, date: '2026-01-02' }), /kept unchanged/);
  assert.equal(readFileSync(file, 'utf8'), '{torn');
});

test('recorded out-of-order lifecycle clocks stay values and become defects', async () => {
  const o = await matrixHome('matrix-order');
  const { DatabaseSync } = await import('node:sqlite');
  const db = new DatabaseSync(homeEntry(o.home, 'knowledge'));
  db.prepare("UPDATE event_journal SET at = ? WHERE record = 'session:lead'").run(Date.parse('2026-01-02T00:00:01Z'));
  db.close();
  const r = await collect({ ...o, case: '111111111111' });
  assert.equal(r.lifecycle[0].elapsedMs.takenToStarted, -4000);
  assert.ok(r.defects.some(d => d.metric === 'quest.orderViolations' && d.value === 1));
});

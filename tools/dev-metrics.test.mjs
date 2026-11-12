import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { collect, builderTimes, render } from './dev-metrics.mjs';
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
  assert.equal(r.lyntai.cited, 'not recorded');
  assert.match(r.lyntai.citedWhere, /handback/i);
  assert.equal(r.lyntai.proposes, 0);
});

test('the table says "not recorded" where nothing is logged, and an absent source is never a zero', async () => {
  const r = await collect({ home, logs, quests: questsFile, since: '2026-01-02', until: '2026-01-02' });
  assert.equal(r.firstPass, 'not read: pass --merge-queue <local/merge-queue.json>');
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

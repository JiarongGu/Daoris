import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { VARIANTS, sharesOf, usageOf } from './dev-metrics/startup.mjs';
import { builderMeasures } from './dev-metrics/first-edit.mjs';
import { builderOf } from './dev-metrics/builder.mjs';
import { mainMeasures } from './dev-metrics/red-rate.mjs';
import { LANDINGS, landingRecord, recordLanding, summarizeLandings } from './dev-metrics/landing.mjs';

mkdirSync('_fixtures', { recursive: true });
const root = mkdtempSync(join('_fixtures', 'dev-landing-'));
const jl = rows => rows.map(r => JSON.stringify(r)).join('\n') + '\n';
const write = (file, text) => { mkdirSync(join(file, '..'), { recursive: true }); writeFileSync(file, text); };
after(() => rmSync(root, { recursive: true, force: true }));

const at = s => new Date(Date.UTC(2026, 10, 10, 9, 0, s)).toISOString();
const turn = (id, s, usage, ...blocks) => ({ type: 'assistant', timestamp: at(s), message: { id, model: 'model-x', usage, content: blocks } });
const result = (id, s, content) => ({ type: 'user', timestamp: at(s), message: { content: [{ type: 'tool_result', tool_use_id: id, content }] } });
const transcript = jl([
  { type: 'user', timestamp: at(0), message: { content: 'the brief' } },
  turn('m1', 60, { input_tokens: 100, cache_creation_input_tokens: 1000, cache_read_input_tokens: 0, output_tokens: 10 }, { type: 'tool_use', id: 'r1', name: 'Read', input: { file_path: 'a.ts' } }),
  result('r1', 61, 'x'.repeat(500)),
  turn('m2', 90, { input_tokens: 10, cache_creation_input_tokens: 400, cache_read_input_tokens: 1100, output_tokens: 20 }, { type: 'tool_use', id: 'g1', name: 'Grep', input: {} }),
  result('g1', 91, [{ type: 'text', text: 'y'.repeat(300) }]),
  turn('m3', 120, { input_tokens: 5, cache_creation_input_tokens: 0, cache_read_input_tokens: 1500, output_tokens: 30 }, { type: 'tool_use', id: 'e1', name: 'Edit', input: { file_path: 'src/a.ts' } }),
  result('e1', 150, 'ok'),
  turn('m4', 180, { input_tokens: 5, cache_creation_input_tokens: 0, cache_read_input_tokens: 1600, output_tokens: 5 }),
]);

test('a builder transcript gives its bytes, tokens and clock before and after the first edit', () => {
  const m = builderMeasures(transcript, 'abc');
  assert.equal(m.preEditBytes, 800);
  assert.equal(m.startupTokens, 1100);
  assert.equal(m.preEditTokens, 405);
  assert.equal(m.wallMinutes, 3);
  assert.equal(m.firstEditAt, at(120));
  assert.equal(m.turns, 4);
  assert.deepEqual(m.tokens, { input: 120, output: 65, cacheRead: 4200, cacheWrite: 1400 });
});

test('a transcript with no edit says so instead of a zero', () => {
  const m = builderMeasures(transcript.split('\n').slice(0, 5).join('\n'), 'abc');
  assert.equal(m.preEditBytes, null);
  assert.match(m.missing, /^not recorded/);
});

test('a branch finds its transcript through the meta, and one without says why it has none', () => {
  const dir = join(root, 'projects');
  write(join(dir, 'session', 'subagents', 'agent-abc.jsonl'), transcript);
  write(join(dir, 'session', 'subagents', 'agent-abc.meta.json'), JSON.stringify({ worktreeBranch: 'worktree-agent-abc' }));
  assert.equal(builderOf('worktree-agent-abc', root, { option: dir }).builder.preEditBytes, 800);
  assert.equal(builderOf('worktree-agent-zzz', root, { option: dir }).builder, null);
  const steward = builderOf('steward/records', root, { option: dir });
  assert.equal(steward.builder, null);
  assert.match(steward.missing, /not a builder's branch/);
});

const gateRow = (branch, at, over = {}) => ({ schema: 1, branch, at, ms: 1000, attempt: 1, firstPass: true, passed: true, gates: [], mainReds: [], overBudget: { gates: [], merge: false }, ...over });

test('main red rates count first-pass failures, settled heavy reds and failed CI, and days since green', () => {
  const repo = join(root, 'repo');
  const day = n => `2026-11-${String(n).padStart(2, '0')}T10:00:00Z`;
  write(join(repo, 'local', 'merge-record.jsonl'), jl([
    gateRow('b1', day(14)), gateRow('b2', day(13), { firstPass: false, attempt: 1, passed: false }), gateRow('b2', day(13), { attempt: 2 }),
    gateRow('b3', day(12)), gateRow('b4', day(11)), gateRow('old', '2026-09-01T10:00:00Z', { firstPass: false }),
  ]));
  write(join(repo, 'local', 'heavy-runs.jsonl'), jl([
    { id: 'h3', state: 'running', commit: 'c3', base: 'c0', branches: ['b3'], at: day(12) },
    { id: 'h3', state: 'red', commit: 'c3', base: 'c0', branches: ['b3'], at: day(12), ended: day(12) },
    { id: 'h1', state: 'running', commit: 'c1', base: 'c0', branches: ['b1'], at: day(14) },
    { id: 'h1', state: 'green', commit: 'c1', base: 'c0', branches: ['b1'], at: day(14), ended: day(14) },
    { id: 'h4', state: 'running', commit: 'c4', base: 'c0', branches: ['b4'], at: day(11) },
  ]));
  write(join(repo, 'local', 'ci-runs.jsonl'), jl([{ commit: 'c4', finished: true, state: 'green' }, { commit: 'c1', finished: true, state: 'failed' }]));
  write(join(repo, 'local', 'ci.json'), JSON.stringify({ lastGreen: { at: '2026-11-10T10:00:00Z' } }));
  const m = mainMeasures(repo, Date.parse('2026-11-15T10:00:00Z'));
  // b1 red by CI, b2 red by its first pass, b3 red by its settled heavy row (not the running one), b4 stays green.
  assert.deepEqual(m.redRate7d, { rate: 0.75, landings: 4, red: 3 });
  assert.deepEqual(m.redRate30d, m.redRate7d);
  assert.equal(m.daysSinceGreen, 5);
  const none = mainMeasures(join(root, 'empty'), Date.parse('2026-11-15T10:00:00Z'));
  assert.match(none.redRate7d, /^not recorded/);
  assert.match(none.daysSinceGreen, /^not recorded/);
});

test('a landing is recorded once, in the machine-local log, and a failing record is lost, not thrown', () => {
  const repo = join(root, 'repo');
  assert.equal(recordLanding(repo, { commit: 'c9', branch: 'steward/x', at: '2026-11-15T10:00:00Z' }), true);
  const lines = readFileSync(join(repo, LANDINGS), 'utf8').trim().split('\n');
  assert.equal(lines.length, 1);
  const row = JSON.parse(lines[0]);
  assert.equal(row.commit, 'c9');
  assert.equal(row.builder, null);
  assert.match(row.builderMissing, /^not recorded/);
  assert.equal(row.gates, null);
  assert.equal(landingRecord(repo, { commit: 'c1', branch: 'b1', at: '2026-11-15T10:00:00Z' }).gates.firstPass, true);
  write(join(root, 'blocked'), 'a file where the log folder must go');
  assert.equal(recordLanding(join(root, 'blocked'), { commit: 'c', branch: 'b' }), false);
  assert.equal(existsSync(join(root, 'blocked', LANDINGS)), false);
});

const read = (id, s, file, size, name = 'Read') => [
  turn(`t${id}`, s, { input_tokens: 1, cache_creation_input_tokens: 1, cache_read_input_tokens: 1, output_tokens: 1 }, { type: 'tool_use', id, name, input: name === 'Bash' ? { command: file } : { file_path: file } }),
  result(id, s + 1, 'z'.repeat(size)),
];
const classed = jl([
  { type: 'user', timestamp: at(0), message: { content: 'the brief' } },
  ...read('a', 10, 'D:\\repo\\AGENTS.md', 10),
  ...read('b', 20, '/repo/docs/areas/driving.md', 20),
  ...read('c', 30, '/repo/docs/index/README.md', 30),
  ...read('d', 40, '/repo/.claude/INDEX.md', 1),
  ...read('e', 50, '/repo/TASKS.md', 40),
  ...read('f', 60, '/repo/docs/decisions/D12.md', 50),
  ...read('g', 70, 'cat docs/task-archive.md', 5, 'Bash'),
  ...read('h', 80, '/repo/docs/archive/areas/x.md', 2),
  ...read('i', 90, '/repo/src/a.ts', 60),
  turn('m9', 100, { input_tokens: 1, cache_creation_input_tokens: 1, cache_read_input_tokens: 1, output_tokens: 1 }, { type: 'tool_use', id: 'e', name: 'Edit', input: { file_path: 'src/a.ts' } }),
]);

test('the bytes before the first edit are split by the class of what was read, and library reads are counted', () => {
  const m = builderMeasures(classed, 'cls');
  assert.deepEqual(m.preEditByClass, { brief: 10, area: 20, index: 31, backlog: 40, library: 57, code: 60 });
  assert.equal(m.preEditBytes, 218);
  assert.equal(m.libraryReads, 3);
});

test('a window summary gives mean and median per class, and a window with no edit says classes are not recorded', () => {
  const one = builderMeasures(classed, 'one').preEditByClass;
  const doubled = Object.fromEntries(Object.entries(one).map(([k, v]) => [k, 2 * v]));
  const builder = (preEditByClass, libraryReads) => ({ preEditBytes: 1, preEditTokens: 1, startupTokens: 1, wallMinutes: 1, tokens: { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 }, preEditByClass, libraryReads });
  const summary = summarizeLandings([
    { schema: 2, branch: 'a', builder: builder(one, 3) }, { schema: 2, branch: 'b', builder: builder(doubled, 5) },
    { schema: 2, branch: 'c', builder: builder(doubled, 7) },
  ], join(root, 'empty'));
  assert.equal(summary.preEditByClass.records, 3);
  assert.deepEqual(summary.preEditByClass.library, { mean: 95, median: 114 });
  assert.deepEqual(summary.preEditByClass.brief, { mean: 16.7, median: 20 });
  assert.deepEqual(summary.preEditByClass.libraryReads, { mean: 5, median: 5 });
  const unedited = { schema: 2, branch: 'd', builder: { ...builder(null, 0), preEditBytes: null } };
  assert.match(summarizeLandings([unedited], join(root, 'empty')).preEditByClass, /^not recorded/);
});

test('the startup plan lists every variant per model and starts no process', () => {
  const plan = spawnSync(process.execPath, [resolve('tools/dev-metrics.mjs'), '--startup', '--plan', '--json'], { encoding: 'utf8', windowsHide: true });
  assert.equal(plan.status, 0);
  const rows = JSON.parse(plan.stdout);
  assert.equal(rows.length, 2 * VARIANTS.length);
  assert.equal(VARIANTS.length, 8);
  assert.deepEqual([...new Set(rows.map(r => r.variant))], ['all', 'without-brief', 'without-claude-md', 'without-skills', 'without-agents', 'without-mcp', 'without-plugins', 'floor']);
});

test('a share is what the whole start has over the start without the source', () => {
  const row = (variant, total) => ({ model: 'm', variant, total });
  const [m] = sharesOf([row('all', 30000), row('floor', 24000), row('without-brief', 28500), row('without-skills', 29000)]);
  assert.equal(m.repository, 6000);
  assert.equal(m.sources.brief, 1500);
  assert.equal(m.sources.mcp, null);
  assert.deepEqual(usageOf(JSON.stringify({ usage: { input_tokens: 3, cache_creation_input_tokens: 10, cache_read_input_tokens: 100, output_tokens: 2 }, num_turns: 1 })), { input: 3, cacheWrite: 10, cacheRead: 100, output: 2, total: 113, turns: 1 });
  assert.equal(usageOf('not json'), null);
});

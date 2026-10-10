/**
 * D160: what a dispatched subagent's context cost, read from the harness's own transcripts.
 *
 *   node --test tools/subagent-usage.test.mjs
 *
 * Run by `npm run verify`. The transcripts are written here as the harness writes them: one JSON object a line, an
 * assistant message streamed as several lines that repeat its usage, and a `.meta.json` beside each.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, utimesSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { collect, readTranscript, summarize } from './subagent-usage.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'subagent-usage-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const usage = (read, write, out) => ({ input_tokens: 2, cache_creation_input_tokens: write, cache_read_input_tokens: read, output_tokens: out });
const assistant = (id, u, content = [], model = 'claude-opus-5-5') =>
  JSON.stringify({ type: 'assistant', message: { id, model, usage: u, content } });
const result = (toolUseId, text) =>
  JSON.stringify({ type: 'user', message: { content: [{ type: 'tool_result', tool_use_id: toolUseId, content: text }] } });
const tool = (id, name, input) => ({ type: 'tool_use', id, name, input });

const transcript = [
  JSON.stringify({ type: 'user', message: { content: 'Task: ROW1: a row.' } }),
  assistant('m1', usage(0, 50_000, 100), [tool('t1', 'Read', { file_path: 'docs/a.md' })]),
  assistant('m1', usage(0, 50_000, 100), [tool('t1', 'Read', { file_path: 'docs/a.md' })]), // the same message, streamed again
  result('t1', 'x'.repeat(3600)),
  assistant('m2', usage(50_000, 20_000, 200), [tool('t2', 'Write', { file_path: 'worktree\\local\\scratch\\probe.txt' })]),
  result('t2', 'ok'),
  assistant('m3', usage(70_000, 10_000, 300), [tool('t3', 'Edit', { file_path: 'src/x.ts' })]),
  result('t3', 'ok'),
  assistant('m4', usage(80_000, 5_000, 400)),
].join('\n');

test('a streamed message is one turn, and its usage counts once', () => {
  const a = readTranscript(transcript);
  assert.equal(a.turns, 4);
  assert.equal(a.output, 1000);
  assert.equal(a.cacheRead, 200_000);
  assert.equal(a.cacheCreate, 85_000);
  assert.equal(a.model, 'claude-opus-5-5');
});

test('the load is the context at the first edit of the work, not of scratch', () => {
  const a = readTranscript(transcript);
  assert.equal(a.firstContext, 50_002);
  assert.equal(a.preEditTurns, 2, 'a write under local/scratch is not the work');
  assert.equal(a.editContext, 80_002);
  assert.equal(a.lastContext, 85_002);
});

test('each cache read is split into the startup, the orientation, its load carried, and the work', () => {
  const a = readTranscript(transcript);
  // The startup is the first turn's 50,002, read again by every later turn. The orientation grew 30,000 by the
  // first edit: 19,998 of it read while orienting, then all of it carried by the turn after.
  assert.deepEqual(a.reads, { startup: 150_004, orientation: 19_998, carried: 29_998, work: 0 });
  assert.equal(Object.values(a.reads).reduce((n, v) => n + v, 0), a.cacheRead);
});

test('weighted counts each kind of token at its ratio to an input token, not a price', () => {
  const a = readTranscript(transcript);
  assert.equal(a.weighted, 8 + 1.25 * 85_000 + 0.1 * 200_000 + 5 * 1000);
});

test('an edit made through the shell is counted, and is the work\'s first edit too', () => {
  const scripted = [
    JSON.stringify({ type: 'user', message: { content: 'Task: ROW2.' } }),
    assistant('s1', usage(0, 40_000, 100), [tool('b1', 'Bash', { command: 'grep -n onPause work/pausing.ts' })]),
    assistant('s2', usage(40_000, 10_000, 100), [tool('b2', 'Bash', { command: "cd web && python - <<'E'\nopen(p,'w').write(s)\nE" })]),
    assistant('s2', usage(40_000, 10_000, 100), [tool('b2', 'Bash', { command: "cd web && python - <<'E'\nopen(p,'w').write(s)\nE" })]),
    assistant('s3', usage(50_000, 5_000, 100), [tool('b3', 'PowerShell', { command: "sed -i 's/onCancel/onClose/g' A.tsx" })]),
    assistant('s4', usage(55_000, 5_000, 100), [tool('b4', 'Bash', { command: 'sed -n 1,40p A.tsx' })]),
    assistant('s5', usage(60_000, 5_000, 100), [tool('b5', 'Bash', { command: 'cd web && grep -c "writeFileSync(" tools/*.mjs' })]),
    assistant('s6', usage(65_000, 5_000, 100), [tool('b6', 'Bash', { command: "node -e \"fs.writeFileSync('local/scratch/x.json', s)\"" })]),
    assistant('s7', usage(70_000, 5_000, 100), [tool('b7', 'PowerShell', { command: 'npm test 2>&1 | Out-File -Encoding utf8 $env:TEMP\\t.log' })]),
  ].join('\n');
  const a = readTranscript(scripted);
  assert.equal(a.shellEdits, 2, 'a python write and a sed -i, each once; a sed -n reads, a grep searches, scratch is no work');
  assert.equal(a.preEditTurns, 1);
  assert.equal(readTranscript(transcript).shellEdits, 0);
});

test('a message the harness wrote itself is not a turn', () => {
  const stopped = [transcript, assistant('m5', usage(0, 0, 0), [{ type: 'text', text: 'limit reached' }], '<synthetic>')].join('\n');
  const a = readTranscript(stopped);
  assert.equal(a.turns, 4);
  assert.equal(a.model, 'claude-opus-5-5');
  assert.equal(a.lastContext, 85_002);
});

test('a transcript that never edits reports its last turn as its load', () => {
  const a = readTranscript([JSON.stringify({ type: 'user', message: { content: 'Probe' } }), assistant('m1', usage(0, 20_000, 10))].join('\n'));
  assert.equal(a.preEditTurns, 1);
  assert.equal(a.editContext, 20_002);
});

test('collect reads each subagent with its meta, and leaves out those before --since', () => {
  const dir = join(scratch, 'projects');
  const sub = join(dir, 'session-1', 'subagents');
  mkdirSync(sub, { recursive: true });
  writeFileSync(join(sub, 'agent-a.jsonl'), transcript);
  writeFileSync(join(sub, 'agent-a.meta.json'), JSON.stringify({ agentType: 'branch-worker', description: 'ROW1 a row' }));
  writeFileSync(join(sub, 'agent-b.jsonl'), transcript);
  const old = new Date('2026-01-01');
  utimesSync(join(sub, 'agent-b.jsonl'), old, old);
  writeFileSync(join(dir, 'session-1.jsonl'), transcript); // the parent's own transcript is not a subagent's
  const agents = collect(dir, { since: new Date('2026-06-01') });
  assert.equal(agents.length, 1);
  assert.equal(agents[0].type, 'branch-worker');
  assert.equal(agents[0].description, 'ROW1 a row');
});

test('summarize groups by type and model, with medians and each group\'s share', () => {
  const a = readTranscript(transcript);
  const agents = [
    { ...a, type: 'branch-worker', model: 'claude-opus-5-5' },
    { ...a, type: 'branch-worker', model: 'claude-opus-5-5', turns: 10 },
    { ...a, type: 'branch-scout', model: 'claude-sonnet-5-5', weighted: a.weighted * 1.5 },
  ];
  const groups = summarize(agents);
  assert.deepEqual(groups.map(g => [g.type, g.model, g.agents]), [
    ['branch-worker', 'claude-opus-5-5', 2],
    ['branch-scout', 'claude-sonnet-5-5', 1],
  ], 'heaviest group first');
  assert.equal(groups[0].turns.median, 10, 'the upper middle of an even count');
  assert.ok(Math.abs(groups[0].share - 2 / 3.5) < 1e-9);
  assert.deepEqual([groups[0].shellEdits, groups[0].shellEditors], [0, 0]);
  const parts = groups[0].split;
  assert.ok(Math.abs(Object.values(parts).reduce((n, v) => n + v, 0) - 1) < 1e-9, 'the parts are the whole');
  assert.ok(Math.abs(parts.startup - 0.1 * 150_004 / a.weighted) < 1e-9);
});

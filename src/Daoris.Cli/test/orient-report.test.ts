import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from './_fixture.ts';

// ORIENT1d: `tools/orient-report.mjs` reads a session's transcript and says what it spent before its first edit, so
// the orientation index's effect is measured on the branches it was built for, before and after.
type Summary = {
  label: string;
  total: number;
  before: number;
  byTool: Record<string, number>;
  bytes: number;
  bytesByTool: Record<string, number>;
  shell: Record<string, number>;
  reads: { path: string; bytes: number; whole: boolean }[];
  indexReads: number;
  firstEdit: { name: string; path: string } | null;
};
// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const tool = await import('../../../tools/orient-report.mjs') as {
  shellKind: (command: string) => string;
  readTranscript: (text: string) => { cwd: string | null; calls: { name: string; input: Record<string, unknown>; bytes: number }[] };
  summarize: (transcript: ReturnType<typeof tool.readTranscript>, label: string) => Summary;
  report: (summaries: Summary[]) => string;
};

const toolPath = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'tools', 'orient-report.mjs');

/** A transcript as the harness writes it: one JSON object a line, each tool call answered by its result. */
function transcript(calls: [string, Record<string, unknown>, string | { type: string; text: string }[]][], cwd = 'D:/work/repo') {
  const lines = [JSON.stringify({ type: 'user', cwd, message: { role: 'user', content: 'the task' } })];
  calls.forEach(([name, input, result], n) => {
    const id = `toolu_${n}`;
    lines.push(JSON.stringify({ type: 'assistant', cwd, message: { content: [{ type: 'thinking', thinking: '…' }, { type: 'tool_use', id, name, input }] } }));
    lines.push(JSON.stringify({ type: 'user', cwd, message: { content: [{ type: 'tool_result', tool_use_id: id, content: result }] } }));
  });
  lines.push('not json: a torn last line is skipped');
  return lines.join('\n');
}

const SESSION = transcript([
  ['Skill', { skill: 'doc-loader' }, 'Launching skill: doc-loader'],
  ['Read', { file_path: 'D:\\work\\repo\\docs\\decisions\\D7.md' }, 'x'.repeat(1000)],
  ['Read', { file_path: 'D:/work/repo/src/a.ts', offset: 10, limit: 20 }, [{ type: 'text', text: 'y'.repeat(300) }]],
  ['Bash', { command: 'grep -rn "foo" src | head -5' }, 'z'.repeat(50)],
  ['Bash', { command: 'sed -n 1,20p src/a.ts' }, 'w'.repeat(200)],
  ['PowerShell', { command: 'Get-ChildItem src' }, 'v'.repeat(20)],
  ['Read', { file_path: 'D:/work/repo/docs/index/routes.md' }, 'u'.repeat(500)],
  // A plan written outside the repository is not the first edit: nothing of the work has changed yet.
  ['Write', { file_path: 'D:/scratchpad/plan.md', content: 'plan' }, 'File created'],
  ['Read', { file_path: 'D:/work/repo/docs/decisions/D12.md' }, 't'.repeat(2000)],
  ['Edit', { file_path: 'D:/work/repo/src/a.ts', old_string: 'a', new_string: 'b' }, 'updated'],
  ['Read', { file_path: 'D:/work/repo/src/b.ts' }, 's'.repeat(9000)],
]);

test('a shell command is a search, a dump, a listing, a git read, a build or something else, in that order', () => {
  assert.equal(tool.shellKind('grep -rn foo src'), 'search');
  assert.equal(tool.shellKind('git grep -n foo'), 'search');
  assert.equal(tool.shellKind('rg foo'), 'search');
  assert.equal(tool.shellKind('Select-String -Path x -Pattern y'), 'search');
  assert.equal(tool.shellKind('sed -n 1,20p a.ts'), 'dump');
  assert.equal(tool.shellKind('cat a.ts | wc -l'), 'dump');
  assert.equal(tool.shellKind('Get-Content a.ts -TotalCount 40'), 'dump');
  assert.equal(tool.shellKind('ls -la src'), 'list');
  assert.equal(tool.shellKind('find . -name "*.cs"'), 'list');
  assert.equal(tool.shellKind('Get-ChildItem src'), 'list');
  assert.equal(tool.shellKind('git log --oneline -5'), 'git');
  assert.equal(tool.shellKind('npm run verify'), 'build');
  assert.equal(tool.shellKind('dotnet test src/Daoris.Service'), 'build');
  assert.equal(tool.shellKind('node tools/orient-index.mjs'), 'other');
});

test('a transcript is read for each call and the size of what came back, past a torn line', () => {
  const read = tool.readTranscript(SESSION);
  assert.equal(read.cwd, 'D:/work/repo');
  assert.equal(read.calls.length, 11);
  assert.deepEqual(read.calls.slice(0, 3).map((call) => [call.name, call.bytes]), [['Skill', 27], ['Read', 1000], ['Read', 300]]);
});

test('what a session spent before its first edit: calls by tool, bytes, the shell by kind, and what it read', () => {
  const summary = tool.summarize(tool.readTranscript(SESSION), 'branch-a');
  assert.equal(summary.total, 11);
  assert.equal(summary.before, 9);
  assert.deepEqual(summary.byTool, { Skill: 1, Read: 4, Bash: 2, PowerShell: 1, Write: 1 });
  assert.equal(summary.bytes, 27 + 1000 + 300 + 50 + 200 + 20 + 500 + 'File created'.length + 2000);
  assert.equal(summary.bytesByTool['Read'], 3800);
  assert.deepEqual(summary.shell, { search: 1, dump: 1, list: 1, git: 0, build: 0, other: 0 });
  assert.deepEqual(summary.reads, [
    { path: 'docs/decisions/D7.md', bytes: 1000, whole: true },
    { path: 'src/a.ts', bytes: 300, whole: false },
    { path: 'docs/index/routes.md', bytes: 500, whole: true },
    { path: 'docs/decisions/D12.md', bytes: 2000, whole: true },
  ]);
  assert.equal(summary.indexReads, 1);
  assert.deepEqual(summary.firstEdit, { name: 'Edit', path: 'src/a.ts' });

  // With no edit at all, everything it did came before one.
  const none = tool.summarize(tool.readTranscript(transcript([['Read', { file_path: 'D:/work/repo/a.md' }, 'abc']])), 'branch-b');
  assert.equal(none.before, 1);
  assert.equal(none.firstEdit, null);
});

test('the report gives each branch its line, the totals, and the files read most, the decisions counted as one', () => {
  const summaries = [tool.summarize(tool.readTranscript(SESSION), 'branch-a'), tool.summarize(tool.readTranscript(SESSION), 'branch-b')];
  const text = tool.report(summaries);
  assert.match(text, /^branch-a: 11 calls, 9 before the first edit \(82%\), 4 KB read before it; first edit: Edit src\/a\.ts$/m);
  assert.match(text, /by tool: Read 4 \(4 KB\), Bash 2 \(0 KB\), PowerShell 1 \(0 KB\), Skill 1 \(0 KB\), Write 1 \(0 KB\)/);
  assert.match(text, /reads 4 \(3 whole, 1 ranged\), 1 of docs\/index\/; shell: search 1, dump 1, list 1, git 0, build 0, other 0/);
  assert.match(text, /^all 2: 18 calls before the first edit \(median 9\), 8 KB read before it \(median 4 KB\); shell search 2, dump 2$/m);
  // Two decision files each in two branches: one row for the record, as the measurement that asked for this counted them.
  assert.match(text, /^\s+4\s+6 KB\s+docs\/decisions\/D<n>\.md$/m);
  assert.match(text, /^\s+2\s+1 KB\s+docs\/index\/routes\.md$/m);
});

test('it runs on transcript files, says which it could not read, and prints JSON when asked', () => {
  const fx = makeFixture('orient-report-run');
  const file = fx.write('a1b2c3.output', SESSION);
  const run = (...args: string[]) => spawnSync(process.execPath, [toolPath, ...args], { encoding: 'utf8' });

  const text = run(file);
  assert.equal(text.status, 0, text.stderr);
  assert.match(text.stdout, /^a1b2c3: 11 calls, 9 before the first edit/m);

  const json = run('--json', file);
  assert.equal(json.status, 0, json.stderr);
  assert.equal(JSON.parse(json.stdout)[0].before, 9);

  const missing = run(file, join(fx.root, 'nope.output'));
  assert.equal(missing.status, 2);
  assert.match(missing.stderr, /nope\.output/);
  assert.equal(run().status, 2, 'no transcript is a usage error');
  fx.cleanup();
});

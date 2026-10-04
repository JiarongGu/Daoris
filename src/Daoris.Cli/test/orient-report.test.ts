import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
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
/** One call as a session's typed events tell it (ORIENT1e): its updates merged under its id. */
type EventCall = {
  id: string | null; kind: string | null; title: string | null; locations: string[] | null; status: string | null;
  input: string | null; chars: number; sized: boolean;
};
type SessionSummary = {
  total: number;
  before: number;
  byKind: Record<string, number>;
  chars: number;
  unsized: number;
  shell: Record<string, number>;
  knowledge: number;
  reads: { path: string; chars: number; whole: boolean; inside: boolean }[];
  whole: number;
  ranged: number;
  wholeLarge: number;
  index: string[];
  indexReads: number;
  firstEdit: { kind: string; path: string } | null;
};
type Driven = {
  session: string; repository: string; workspace: string; adapter: string; started: string; parked: number;
  summary: SessionSummary | null;
};
type Group = {
  name: string; sessions: number; withoutEvents: number; editing: number; openedIndex: number; parked: number;
  median: { before: number; chars: number; searchesAndDumps: number; wholeLarge: number } | null;
  files: { path: string; reads: number; chars: number }[];
};
type HomeRead = {
  home: string; skipped: number; left: { setups: number; other: number };
  sessions: Driven[]; repositories: Group[]; workspaces: Group[]; all: Group;
};

// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const tool = await import('../../../tools/orient-report.mjs') as {
  shellKind: (command: string) => string;
  readTranscript: (text: string) => { cwd: string | null; calls: { name: string; input: Record<string, unknown>; bytes: number }[] };
  summarize: (transcript: ReturnType<typeof tool.readTranscript>, label: string) => Summary;
  report: (summaries: Summary[]) => string;
  readEvents: (text: string) => EventCall[];
  placeOf: (path: string, home: string | null) => { tree: string[] | null; rel: string; inside: boolean } | null;
  summarizeEvents: (calls: EventCall[], options: { home: string | null; index?: string[] }) => SessionSummary;
  readHome: (home: string, options?: { repository?: string | null; since?: Date | null }) => HomeRead;
  homeReport: (read: HomeRead) => string;
};

const here = dirname(fileURLToPath(import.meta.url));
const toolPath = join(here, '..', '..', '..', 'tools', 'orient-report.mjs');
const driver = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver');

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

// ORIENT1c: whether a branch asked the workspace's knowledge server, before its first edit and after.
test('the asks of the knowledge server are counted, before the first edit and in all, and said for each branch', () => {
  const asked = transcript([
    ['mcp__daoris-knowledge__knowledge_search', { query: 'where is the SESSION_GO_ON_NEW handler' }, 'r'.repeat(400)],
    ['Read', { file_path: 'D:/work/repo/src/a.ts', offset: 90, limit: 30 }, 'q'.repeat(100)],
    ['Edit', { file_path: 'D:/work/repo/src/a.ts', old_string: 'a', new_string: 'b' }, 'updated'],
    ['mcp__daoris-knowledge__knowledge_get', { id: 'repo:docs/decisions/D7.md' }, 'p'.repeat(50)],
    ['mcp__other__tool', {}, 'o'],
  ]);
  const summary = tool.summarize(tool.readTranscript(asked), 'branch-c') as Summary & { asks: number; asksBefore: number };
  assert.equal(summary.asks, 2);
  assert.equal(summary.asksBefore, 1);

  const text = tool.report([summary, tool.summarize(tool.readTranscript(SESSION), 'branch-a')]);
  assert.match(text, /^ {2}knowledge server: 2 asks, 1 before the first edit$/m);
  assert.match(text, /^ {2}knowledge server: 0 asks, 0 before the first edit$/m);
  assert.match(text, /^knowledge server asked by 1 of 2 branches, 2 asks in all$/m);
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

// ORIENT1e: a driven session is kept as typed events (D76 §2), never as the harness's transcript, which lives in an
// account's home Daoris never reads (D125). These fixtures are each door's events as the driver writes them.

/** A session's events file as `SessionEvents` writes it: one camel-cased event a line, numbered, absent fields left out. */
function events(...list: Record<string, unknown>[]) {
  return `${list.map((e, n) => JSON.stringify({ seq: n + 1, at: '2026-10-01T09:00:00Z', ...e })).join('\n')}\n`;
}

/** The native door (`ClaudeStreamJson`): a call names its kind, title, place and input; its result is a text. */
const call = (id: string, title: string, toolKind: string, input: Record<string, unknown>, path?: string) => ({
  kind: 'tool', id, title, toolKind, status: 'in_progress', ...(path ? { locations: [path] } : {}), input: JSON.stringify(input),
});
const result = (id: string, text: string) => ({ kind: 'tool', id, status: 'completed', content: [{ type: 'text', text }] });
const diff = (path: string) => [{ type: 'diff', path, oldText: 'a', newText: 'b' }];

/** A native session in `tree`, its paths in the platform's own spelling. */
function nativeSession(tree: string) {
  const at = (rel: string) => join(tree, ...rel.split('/'));
  const read = (id: string, rel: string, extra: Record<string, unknown> = {}) => call(id, `Read ${at(rel)}`, 'read', { file_path: at(rel), ...extra }, at(rel));
  const edit = (id: string, path: string) => ({ ...call(id, `Edit ${path}`, 'edit', { file_path: path, old_string: 'a', new_string: 'b' }, path), content: diff(path) });
  return `${events(
    { kind: 'user', origin: 'target', text: 'Take the quest' },
    call('t1', 'Skill', 'other', { skill: 'doc-loader' }), result('t1', 'Launching skill: doc-loader'),
    read('t2', 'docs/decisions/D7.md'), result('t2', 'x'.repeat(1000)),
    read('t3', 'src/a.ts', { offset: 10, limit: 20 }), result('t3', 'y'.repeat(300)),
    call('t4', 'Search for foo', 'execute', { command: 'grep -rn "foo" src | head -5', description: 'Search for foo' }), result('t4', 'z'.repeat(50)),
    // The door names a PowerShell call `other`, by its own name: its command is what makes it the shell.
    call('t5', 'PowerShell', 'other', { command: 'Get-Content src\\a.ts -TotalCount 40' }), result('t5', 'w'.repeat(200)),
    // A result past the record's 64 KB is cut, and the cut says how long it was.
    read('t6', 'src/big.cs'), result('t6', `${'v'.repeat(65536)}… (90000 chars)`),
    read('t7', 'docs/where/README.md'), result('t7', 'u'.repeat(500)),
    call('t8', 'knowledge_search', 'other', { query: 'where is the parser' }), result('t8', 'r'.repeat(80)),
    { kind: 'message', id: 'm1', text: 'I will plan first.' },
    // A plan written outside the tree, and a note in its `local/`, change nothing of the work.
    edit('t9', 'D:/scratchpad/plan.md'), result('t9', 'File created'),
    edit('t10', at('local/notes.md')), result('t10', 'updated'),
    read('t11', 'docs/decisions/D12.md'), result('t11', 't'.repeat(2000)),
    edit('t12', at('src/a.ts')), result('t12', 'updated'),
    read('t13', 'src/b.ts'), result('t13', 's'.repeat(9000)),
    { kind: 'usage', used: 50000, size: 200000 },
    { kind: 'turn', stopReason: 'end_turn' },
  )}not json: a torn last line is skipped\n`;
}

/** A tree known only by its minted name, as a copy of a home names the place its sessions ran. */
const ACP_TREE = 'D:/elsewhere/data/trees/play/game/s-0f0e0d0c';

/** The protocol door (`AcpSession.Map`): a call announced, then updated under its id, its content replaced as ACP replaces it. */
function acpSession() {
  const at = (rel: string) => `${ACP_TREE}/${rel}`;
  return events(
    { kind: 'user', origin: 'target', text: 'Take the quest' },
    { kind: 'tool', id: 'c1', title: 'Read File', toolKind: 'read', status: 'pending', input: '{}' },
    { kind: 'tool', id: 'c1', title: 'Read docs/index/routes.md', locations: [at('docs/index/routes.md')], input: JSON.stringify({ file_path: at('docs/index/routes.md') }) },
    { kind: 'tool', id: 'c1', status: 'completed', content: [{ type: 'text', text: 'q'.repeat(500) }] },
    { kind: 'tool', id: 'c2', title: '`rg -n foo`', toolKind: 'execute', status: 'pending', input: '{"command":"rg -n foo"}', content: [{ type: 'text', text: 'Search for foo' }] },
    { kind: 'tool', id: 'c2', status: 'completed', content: [{ type: 'text', text: 'p'.repeat(120) }] },
    { kind: 'tool', id: 'c3', title: '`git log --oneline`', toolKind: 'execute', status: 'pending', input: '{"command":"git log --oneline"}' },
    // A raw output past 400 characters is cut by the door, saying how long it was.
    { kind: 'tool', id: 'c3', status: 'completed', output: `"${'o'.repeat(399)}… (5000 chars)` },
    // Nothing came back that the record kept: the call counts, its size is not known.
    { kind: 'tool', id: 'c4', title: 'grep "foo"', toolKind: 'search', status: 'pending' },
    { kind: 'tool', id: 'c4', status: 'completed' },
    {
      kind: 'tool', id: 'c5', title: 'Read src/lib.rs (40 - 59)', toolKind: 'read', status: 'pending', locations: [at('src/lib.rs')], line: 40,
      input: JSON.stringify({ file_path: at('src/lib.rs'), offset: 40, limit: 20 }),
    },
    { kind: 'tool', id: 'c5', status: 'completed', content: [{ type: 'text', text: 'n'.repeat(200) }] },
    { kind: 'tool', id: 'c6', title: 'mcp__daoris-knowledge__knowledge_search', toolKind: 'other', status: 'pending', input: '{"query":"chunk"}' },
    { kind: 'tool', id: 'c6', status: 'completed', output: '{"results":[]}' },
    { kind: 'tool', id: 'c7', title: 'Edit src/lib.rs', toolKind: 'edit', status: 'pending', locations: [at('src/lib.rs')], content: diff(at('src/lib.rs')) },
    { kind: 'tool', id: 'c7', status: 'completed' },
    { kind: 'tool', id: 'c8', title: 'Read src/main.rs', toolKind: 'read', status: 'completed', locations: [at('src/main.rs')], content: [{ type: 'text', text: 'm'.repeat(700) }] },
  );
}

/** A native session that read and never edited. */
function noEditSession(tree: string) {
  const at = (rel: string) => join(tree, ...rel.split('/'));
  return events(
    call('n1', `Read ${at('README.md')}`, 'read', { file_path: at('README.md') }, at('README.md')), result('n1', 'k'.repeat(400)),
    // A shell dump of the index opens it as surely as a read does.
    call('n2', 'Show the index', 'execute', { command: 'cat docs/where/README.md' }), result('n2', 'j'.repeat(600)),
    call('n3', `Read ${at('src/a.ts')}`, 'read', { file_path: at('src/a.ts'), offset: 1, limit: 10 }, at('src/a.ts')), result('n3', 'i'.repeat(100)),
  );
}

const logLine = (time: string, event: string, data: Record<string, unknown>) => JSON.stringify({ time, source: 'desktop', level: 'info', event, data });
const started = (time: string, session: string, repository: string, workspace: string, adapter: string, extra: Record<string, unknown> = {}) =>
  logLine(time, 'session.started', { session, kind: 'driven', adapter, repository, workspace, ...extra });

/** A Daoris home as the install keeps one: the machine log, each session's events beside its rendered log, and its trees. */
function home() {
  const fx = makeFixture('orient-report-home');
  const engineTree = join(fx.root, 'trees', 'main', 'engine', 's-1a2b3c4d');
  fx.write('trees/main/engine/s-1a2b3c4d/daoris.json', JSON.stringify({ source: 'daoris@0.0.1', documents: { index: 'docs/where/README.md' } }));
  fx.write('logs/2026-10-01.desktop.jsonl', `${[
    started('2026-10-01T09:00:00.000Z', 's-native', 'engine', 'main', 'claude'),
    started('2026-10-01T09:30:00.000Z', 's-setup', 'engine', 'main', 'claude', { setup: true }),
    logLine('2026-10-01T09:40:00.000Z', 'session.started', { session: 's-chat', kind: 'chat', adapter: 'claude' }),
    started('2026-10-01T10:00:00.000Z', 's-noedit', 'engine', 'main', 'claude'),
    logLine('2026-10-01T10:20:00.000Z', 'session.parked', { session: 's-noedit', kind: 'driven' }),
  ].join('\n')}\n`);
  fx.write('logs/2026-10-02.desktop.jsonl', `${[
    started('2026-10-02T09:00:00.000Z', 's-acp', 'game', 'play', 'claude-acp'),
    started('2026-10-02T10:00:00.000Z', 's-missing', 'game', 'play', 'codex-acp'),
  ].join('\n')}\n`);
  fx.write('sessions/s-native.events.jsonl', nativeSession(engineTree));
  fx.write('sessions/s-native.log', 'the rendered transcript: text a person reads, never this report\'s');
  fx.write('sessions/s-setup.events.jsonl', nativeSession(engineTree));
  fx.write('sessions/s-noedit.events.jsonl', noEditSession(join(fx.root, 'trees', 'main', 'engine', 's-5e6f7a8b')));
  fx.write('sessions/s-acp.events.jsonl', acpSession());
  return fx;
}

test('a session\'s typed events are its calls: updates merge under an id, content is replaced, a cut says its length', () => {
  const acp = tool.readEvents(acpSession());
  assert.equal(acp.length, 8);
  assert.deepEqual(acp.map((c) => [c.id, c.kind, c.chars, c.sized]), [
    ['c1', 'read', 500, true],
    // The command's description came first and the output replaced it: 120, never 120 and the description.
    ['c2', 'execute', 120, true],
    ['c3', 'execute', 5000, true],
    ['c4', 'search', 0, false],
    ['c5', 'read', 200, true],
    ['c6', 'other', 14, true],
    ['c7', 'edit', 0, false],
    ['c8', 'read', 700, true],
  ]);
  assert.equal(acp[0]?.title, 'Read docs/index/routes.md');
  assert.equal(acp[0]?.status, 'completed');

  const native = tool.readEvents(nativeSession('D:/h/trees/main/engine/s-1a2b3c4d'));
  assert.equal(native.length, 13, 'a message, the usage, the turn and a torn line are no calls');
  assert.equal(native[5]?.chars, 90000);
});

test('a path is placed in its session\'s tree by the home, or by the tree\'s minted name when the home is a copy', () => {
  const home = 'D:\\Daoris\\data';
  assert.deepEqual(tool.placeOf('D:\\Daoris\\data\\trees\\main\\engine\\s-1a2b3c4d\\src\\a.ts', home), { tree: ['main', 'engine', 's-1a2b3c4d'], rel: 'src/a.ts', inside: true });
  assert.deepEqual(tool.placeOf('d:/daoris/DATA/trees/main/engine/s-1a2b3c4d/src/a.ts', home)?.rel, 'src/a.ts');
  assert.deepEqual(tool.placeOf('E:/copy/trees/play/game/s-0f0e0d0c/docs/index/README.md', home), { tree: ['play', 'game', 's-0f0e0d0c'], rel: 'docs/index/README.md', inside: true });
  assert.deepEqual(tool.placeOf('./src/a.ts', home), { tree: null, rel: 'src/a.ts', inside: true });
  assert.equal(tool.placeOf('../other/x.ts', home)?.inside, false);
  assert.deepEqual(tool.placeOf('D:/Daoris/data/sessions/s-1.log', home), { tree: null, rel: '<home>/sessions/s-1.log', inside: false });
  assert.deepEqual(tool.placeOf('D:/scratchpad/plan.md', home), { tree: null, rel: 'D:/scratchpad/plan.md', inside: false });
  assert.equal(tool.placeOf('D:/repo/trees/a/b/c/x.ts', home)?.inside, false, 'a folder named trees elsewhere is no session tree');
});

test('what a driven session spent before its first edit, read from either door\'s events', () => {
  const home = 'D:/h';
  const native = tool.summarizeEvents(tool.readEvents(nativeSession('D:/h/trees/main/engine/s-1a2b3c4d')), { home, index: ['docs/index/', 'docs/where/'] });
  assert.equal(native.total, 13);
  assert.equal(native.before, 11);
  assert.deepEqual(native.byKind, { other: 2, read: 5, execute: 2, edit: 2 });
  assert.equal(native.chars, 27 + 1000 + 300 + 50 + 200 + 90000 + 500 + 80 + 'File created'.length + 'updated'.length + 2000);
  assert.equal(native.unsized, 0);
  assert.deepEqual(native.shell, { search: 1, dump: 1, list: 0, git: 0, build: 0, other: 0 });
  assert.equal(native.knowledge, 1);
  assert.deepEqual(native.reads.map((r) => [r.path, r.chars, r.whole]), [
    ['docs/decisions/D7.md', 1000, true],
    ['src/a.ts', 300, false],
    ['src/big.cs', 90000, true],
    ['docs/where/README.md', 500, true],
    ['docs/decisions/D12.md', 2000, true],
  ]);
  assert.deepEqual([native.whole, native.ranged, native.wholeLarge], [4, 1, 1]);
  assert.equal(native.indexReads, 1);
  assert.deepEqual(native.firstEdit, { kind: 'edit', path: 'src/a.ts' });

  const acp = tool.summarizeEvents(tool.readEvents(acpSession()), { home });
  assert.equal(acp.total, 8);
  assert.equal(acp.before, 6);
  assert.deepEqual(acp.byKind, { read: 2, execute: 2, search: 1, other: 1 });
  assert.equal(acp.chars, 500 + 120 + 5000 + 200 + 14);
  assert.equal(acp.unsized, 1);
  assert.deepEqual(acp.shell, { search: 1, dump: 0, list: 0, git: 1, build: 0, other: 0 });
  assert.equal(acp.knowledge, 1);
  assert.deepEqual(acp.reads.map((r) => [r.path, r.chars, r.whole]), [['docs/index/routes.md', 500, true], ['src/lib.rs', 200, false]]);
  assert.deepEqual(acp.index, ['docs/index/']);
  assert.equal(acp.indexReads, 1);
  assert.deepEqual(acp.firstEdit, { kind: 'edit', path: 'src/lib.rs' });

  // With no edit at all, everything it did came before one.
  const none = tool.summarizeEvents(tool.readEvents(noEditSession('D:/h/trees/main/engine/s-5e6f7a8b')), { home, index: ['docs/index/', 'docs/where/'] });
  assert.deepEqual([none.total, none.before, none.firstEdit, none.indexReads, none.shell.dump], [3, 3, null, 1, 1]);

  // An input past the record's bound is no longer JSON, and its command is still read; a delete changes the work too.
  const cut = `{"command":"grep -rn ${'x'.repeat(1990)}… (2400 chars)`;
  const deleted = tool.summarizeEvents(tool.readEvents(events(
    { kind: 'tool', id: 'b1', title: 'Search', toolKind: 'execute', status: 'completed', input: cut },
    { kind: 'tool', id: 'd1', title: 'Delete old.rs', toolKind: 'delete', status: 'completed', locations: [`${ACP_TREE}/old.rs`] },
  )), { home });
  assert.equal(deleted.shell.search, 1);
  assert.deepEqual(deleted.firstEdit, { kind: 'delete', path: 'old.rs' });
  // A change that names no place is the work's: the session runs in its tree.
  const unplaced = tool.summarizeEvents(tool.readEvents(events({ kind: 'tool', id: 'e1', title: 'Edit', toolKind: 'edit' })), { home });
  assert.deepEqual([unplaced.before, unplaced.firstEdit], [0, { kind: 'edit', path: '(unplaced)' }]);
});

test('a home is read for its driven sessions, set-ups and other doors left out, each repository\'s index from its trees', () => {
  const fx = home();
  const read = tool.readHome(fx.root);
  assert.deepEqual(read.sessions.map((s) => [s.session, s.repository, s.workspace, s.adapter, s.summary !== null, s.parked]), [
    ['s-native', 'engine', 'main', 'claude', true, 0],
    ['s-noedit', 'engine', 'main', 'claude', true, 1],
    ['s-acp', 'game', 'play', 'claude-acp', true, 0],
    ['s-missing', 'game', 'play', 'codex-acp', false, 0],
  ]);
  assert.deepEqual(read.left, { setups: 1, other: 1 });
  // The declaration is read from the session's own tree; a session of the same repository whose tree is gone keeps it.
  assert.deepEqual(read.sessions[0]?.summary?.index, ['docs/index/', 'docs/where/']);
  assert.equal(read.sessions[1]?.summary?.indexReads, 1);

  const without = (group: Group) => ({ ...group, files: undefined });
  const engine = read.repositories.find((group) => group.name === 'engine')!;
  assert.deepEqual(without(engine), {
    name: 'engine', sessions: 2, withoutEvents: 0, editing: 1, openedIndex: 1, parked: 1,
    median: { before: 11, chars: 94176, searchesAndDumps: 2, wholeLarge: 1 }, files: undefined,
  });
  assert.deepEqual(engine.files, [
    { path: 'docs/decisions/D<n>.md', reads: 2, chars: 3000 },
    { path: 'src/a.ts', reads: 2, chars: 400 },
    { path: 'src/big.cs', reads: 1, chars: 90000 },
    { path: 'docs/where/README.md', reads: 1, chars: 500 },
    { path: 'README.md', reads: 1, chars: 400 },
  ]);
  assert.deepEqual(without(read.repositories.find((group) => group.name === 'game')!), {
    name: 'game', sessions: 2, withoutEvents: 1, editing: 1, openedIndex: 1, parked: 0,
    median: { before: 6, chars: 5834, searchesAndDumps: 1, wholeLarge: 0 }, files: undefined,
  });
  assert.deepEqual(read.workspaces.map((group) => [group.name, group.sessions]), [['main', 2], ['play', 2]]);
  assert.deepEqual(without(read.all), {
    name: 'all', sessions: 4, withoutEvents: 1, editing: 2, openedIndex: 2, parked: 1,
    median: { before: 9, chars: 50005, searchesAndDumps: 2, wholeLarge: 1 }, files: undefined,
  });

  assert.deepEqual(tool.readHome(fx.root, { repository: 'ENGINE' }).sessions.map((s) => s.session), ['s-native', 's-noedit']);
  assert.deepEqual(tool.readHome(fx.root, { since: new Date('2026-10-02T00:00:00Z') }).sessions.map((s) => s.session), ['s-acp', 's-missing']);
  fx.cleanup();
});

test('the home\'s report says what it read, each session in characters, then each repository, workspace and all', () => {
  const fx = home();
  const text = tool.homeReport(tool.readHome(fx.root));
  assert.match(text, /^orient-report: read the machine log under .*logs: 4 driven sessions \(1 set-up and 1 other session left out\); 3 with events, 1 without \(s-missing\)$/m);
  assert.match(text, /characters, not bytes/);
  assert.match(text, /^s-native engine \(main, claude, 2026-10-01T09:00Z\): 13 calls, 11 before the first edit \(85%\), 94K chars read before it; first edit: edit src\/a\.ts$/m);
  assert.match(text, /^ {2}by kind: read 5, edit 2, execute 2, other 2$/m);
  assert.match(text, /^ {2}reads 5 \(4 whole, 1 ranged; 1 whole of 40K\+ chars\), 1 of the index \(docs\/index\/, docs\/where\/\); shell: search 1, dump 1, list 0, git 0, build 0, other 0; knowledge_search 1; every call's size known; parked 0$/m);
  assert.match(text, /^s-acp game \(play, claude-acp, 2026-10-02T09:00Z\): 8 calls, 6 before the first edit \(75%\), 6K chars read before it; first edit: edit src\/lib\.rs$/m);
  assert.match(text, /knowledge_search 1; 1 call with no result size; parked 0$/m);
  assert.match(text, /^s-noedit engine \(main, claude, 2026-10-01T10:00Z\): 3 calls, 3 before the first edit \(100%\), 1K chars read before it; no edit$/m);
  assert.match(text, /^s-missing game \(play, codex-acp, 2026-10-02T10:00Z\): no events file$/m);
  assert.match(text, /^repository engine: 2 sessions, 1 editing, 0 without events; the median editing session before its first edit: 11 calls, 94K chars, 2 shell searches and dumps, 1 whole read of 40K\+ chars; 1 of 1 opened the index first; 1 parked$/m);
  assert.match(text, /^\s+2\s+3K\s+docs\/decisions\/D<n>\.md$/m);
  assert.match(text, /^workspace play: 2 sessions, 1 editing, 1 without events; /m);
  assert.match(text, /^all: 4 sessions, 2 editing, 1 without events; the median editing session before its first edit: 9 calls, 50K chars, 2 shell searches and dumps, 1 whole read of 40K\+ chars; 2 of 2 opened the index first; 1 parked$/m);
  assert.doesNotMatch(text, /s-setup|s-chat/);
  fx.cleanup();
});

test('it runs on a home, narrowed by repository and date, prints JSON when asked, and refuses what it cannot read', () => {
  const fx = home();
  const run = (...args: string[]) => spawnSync(process.execPath, [toolPath, ...args], { encoding: 'utf8' });

  const text = run('--home', fx.root);
  assert.equal(text.status, 0, text.stderr);
  assert.match(text.stdout, /^s-native engine/m);

  const json = run('--home', fx.root, '--repository', 'game', '--json');
  assert.equal(json.status, 0, json.stderr);
  assert.deepEqual((JSON.parse(json.stdout) as HomeRead).sessions.map((s) => s.session), ['s-acp', 's-missing']);

  const since = run('--home', fx.root, '--since', '2026-10-02');
  assert.equal(since.status, 0, since.stderr);
  assert.doesNotMatch(since.stdout, /^s-native/m);
  assert.match(since.stdout, /^s-acp/m);

  assert.equal(run('--home').status, 2, '--home takes a folder');
  assert.equal(run('--home', fx.root, 'a.jsonl').status, 2, 'a home or transcripts, not both');
  assert.equal(run('--home', fx.root, '--since', 'yesterday').status, 2, '--since takes a date');
  assert.equal(run('--repository', 'engine', 'a.jsonl').status, 2, '--repository reads a home');
  const nowhere = run('--home', join(fx.root, 'nowhere'));
  assert.equal(nowhere.status, 2);
  assert.match(nowhere.stderr, /no machine log/);
  fx.cleanup();
});

test('the record\'s spellings it reads are the driver\'s: the tool event, its fields, each cut, the shell kinds and a tree\'s minted name', () => {
  const events = readFileSync(join(driver, 'SessionEvents.cs'), 'utf8');
  assert.match(events, /public const string Tool = "tool";/);
  for (const field of ['Id', 'Title', 'ToolKind', 'Status', 'Input', 'Output']) {
    assert.match(events, new RegExp(`public string\\? ${field} \\{ get; init; \\}`), field);
  }
  assert.match(events, /public IReadOnlyList<string>\? Locations \{ get; init; \}/);
  assert.match(events, /public IReadOnlyList<ToolContent>\? Content \{ get; init; \}/);
  assert.match(events, /JsonNamingPolicy\.CamelCase/);
  assert.match(events, /\$"\{text\[\.\.limit\]\}… \(\{text\.Length\} chars\)"/);
  assert.match(readFileSync(join(driver, 'Acp.cs'), 'utf8'), /\$"\{raw\[\.\.400\]\}… \(\{raw\.Length\} chars\)"/);
  // Only the Bash family is the native door's `execute`: a PowerShell call is `other`, which is why its command decides.
  assert.match(readFileSync(join(driver, 'StructuredOutput.cs'), 'utf8'), /"Bash" or "BashOutput" or "KillShell" => "execute",/);
  assert.match(readFileSync(join(driver, 'SessionTrees.cs'), 'utf8'), /\$"s-\{Guid\.NewGuid\(\)\.ToString\("N"\)\[\.\.8\]\}"/);
  assert.match(readFileSync(join(driver, 'ServiceClient.cs'), 'utf8'), /public const string Driven = "driven";/);
});

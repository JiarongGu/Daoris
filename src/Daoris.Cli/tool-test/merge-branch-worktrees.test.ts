import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { repoRoot, tool, refusal, type Worktree, type Facts, type Candidate, type Proc, type Folder } from './support/merge-branch.ts';
import { tree, git, age, scratch } from './support/merge-branch-kit.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


test('a prune considers each merged branch but main, the main checkout\'s, the one it runs in and the batch\'s still to merge', () => {
  const worktrees = [tree('/r', 'park'), tree('/r/w/a', 'agent-a'), tree('/r/w/here', 'here')];
  const candidates = tool.pruneCandidates({
    merged: ['agent-a', 'here', 'main', 'next', 'park', 'plain'], worktrees, current: 'here', hold: ['next'],
  });
  assert.deepEqual(candidates.map((c) => [c.branch, c.worktree?.path ?? null, c.held !== null]), [
    ['agent-a', '/r/w/a', false],
    ['here', '/r/w/here', true],
    ['next', null, true],
    ['park', '/r', true],
    ['plain', null, false],
  ], 'main is the reference, never a candidate');
  const held = Object.fromEntries(candidates.map((c) => [c.branch, c.held]));
  assert.match(held['park']!, /main checkout/);
  assert.match(held['here']!, /runs in/);
  assert.match(held['next']!, /batch/);
});


test('a prune never takes a Daoris session\'s tree or branch: a daoris/ or quest/ branch, with its worktree', () => {
  // A session's branch sits at main's tip until its first commit, which reads as merged, so a prune would take it mid-session.
  const worktrees = [
    tree('D:/work/repo', 'main'),
    tree('D:/work/repo/.claude/worktrees/agent-a', 'worktree-agent-a'),
    tree('X:/daoris/data/trees/games/repo/s-1', 'daoris/s-1'),
    tree('D:/work/repo-elsewhere', 'elsewhere'),
  ];
  const candidates = tool.pruneCandidates({
    merged: ['daoris/s-1', 'daoris/s-2', 'elsewhere', 'quest/fix-7b175b', 'worktree-agent-a'], worktrees, current: 'main',
  });
  const held = Object.fromEntries(candidates.map((c) => [c.branch, c.held]));
  assert.match(held['daoris/s-1']!, /Daoris/);
  assert.match(held['daoris/s-2']!, /Daoris/, 'a session branch with no tree is still Daoris\'s');
  assert.match(held['quest/fix-7b175b']!, /Daoris/);
  assert.equal(held['elsewhere'], null, 'a tree beside the checkout is no sign: the harness may put its trees there');
  assert.equal(held['worktree-agent-a'], null, 'an agent harness tree is still pruned as before');
});


test('a merged branch goes with its worktree only when that is unlocked, has no tracked change and holds nothing under local/', () => {
  const clean: Facts = { tracked: [], untracked: [], local: null };
  const at = (more: Partial<Worktree> = {}): Candidate => ({ branch: 'b', worktree: tree('/r/w/b', 'b', more), held: null });

  assert.deepEqual(tool.pruneVerdict({ branch: 'b', worktree: null, held: null }, null), { remove: true, why: '' });
  assert.deepEqual(tool.pruneVerdict(at(), clean), { remove: true, why: '' });
  assert.deepEqual(tool.pruneVerdict(at({ prunable: true }), { ...clean, gone: true }), { remove: true, why: '', gone: true });
  assert.deepEqual(tool.pruneVerdict({ branch: 'b', worktree: null, held: 'named in this batch' }, null), { remove: false, why: 'named in this batch' });

  // A worktree locked for any reason but the agent harness's is in use however it looks, and is never looked into.
  const locked = tool.pruneVerdict(at({ locked: true, lockReason: 'the owner keeps this' }), null);
  assert.equal(locked.remove, false);
  assert.match(locked.why, /locked, so in use \(the owner keeps this\)/);
  assert.equal(tool.pruneVerdict(at({ locked: true, prunable: true }), null).remove, false, 'a lock holds even when the folder is gone');

  const changed = tool.pruneVerdict(at(), { ...clean, tracked: ['src/a.ts', 'b.md', 'c.md', 'd.md'] });
  assert.equal(changed.remove, false);
  assert.match(changed.why, /modified or staged tracked files \(src\/a\.ts, b\.md, c\.md, … 1 more\)/);
  const local = tool.pruneVerdict(at(), { ...clean, local: 'local/scratch/out.md' });
  assert.equal(local.remove, false);
  assert.match(local.why, /under local\/ \(local\/scratch\/out\.md\), which nothing else keeps/);
  const untracked = tool.pruneVerdict(at(), { ...clean, untracked: ['new.txt'] });
  assert.equal(untracked.remove, false);
  assert.match(untracked.why, /untracked files .*--force \(new\.txt\)/);
  const unread = tool.pruneVerdict(at(), { ...clean, error: 'fatal: not a git repository' });
  assert.equal(unread.remove, false);
  assert.match(unread.why, /fatal: not a git repository/);
  assert.equal(tool.pruneVerdict(at(), null).remove, false, 'a worktree nobody looked into is kept');
  assert.match(tool.pruneVerdict(at(), { ...clean, tracked: ['a'], local: 'local/x' }).why, /tracked files[\s\S]*local\//, 'every reason is said, not only the first');
});


// The agent harness keeps its lock for its session's whole life, not only while the agent runs, so
// finished agents' merged worktrees stand locked. Its lock is lifted when nothing is left inside.
test('the agent harness\'s lock is its own reason: claude agent, an agent id and a pid', () => {
  assert.equal(tool.harnessLock('claude agent agent-a (pid 57256)'), true);
  assert.equal(tool.harnessLock('claude agent agent-aa965da03c60bca87 (pid 1)'), true);
  for (const other of ['', 'the owner keeps this', 'claude agent agent-a', 'claude agent agent-a (pid 1) for now', 'claude agent  (pid 1)']) {
    assert.equal(tool.harnessLock(other), false, other);
  }
});


// The newest write is read from the tree's top level, its folders' children, and the git folder's HEAD, index and
// log, never a full walk; a source file edited in place deeper down is not seen, which the two hours cover.
test('the newest write of a worktree is read from its top level, its folders\' children and its git folder', () => {
  const repo = scratch('newest-write', [{ name: 'first', run: 'node gate.mjs first' }], { 'src/a.ts': 'a\n' });
  try {
    const path = join(dirname(repo.root), 'tree');
    git(repo.root, 'worktree', 'add', '--quiet', '-b', 'w', path, 'main');
    age(path, 300);
    const idle = (Date.now() - tool.newestWrite(path)) / 60_000;
    assert.ok(idle >= 299 && idle < 302, `all idle: ${idle}`);
    const touched = (target: string) => {
      age(path, 300);
      writeFileSync(target, 'x\n');
      return (Date.now() - tool.newestWrite(path)) / 60_000;
    };
    assert.ok(touched(join(path, 'top.txt')) < 2, 'a file at the top');
    assert.ok(touched(join(path, 'src', 'b.ts')) < 2, 'a file in a folder');
    const gitdir = /^gitdir: (.+)$/m.exec(readFileSync(join(path, '.git'), 'utf8'))![1]!.trim();
    assert.ok(touched(join(gitdir, 'HEAD')) < 2, "the worktree's HEAD");
    assert.ok(touched(join(gitdir, 'index')) < 2, "the worktree's index");
    assert.equal(tool.newestWrite(join(path, 'missing')), 0, 'a folder that is not there has no write');
  } finally {
    repo.cleanup();
  }
});


test('a harness-locked merged worktree goes, unlocked, only when it is clean, holds nothing under local/ and no process runs inside', () => {
  const clean: Facts = { tracked: [], untracked: [], local: null, atTip: false, processes: [], idleMinutes: 300 };
  const reason = 'claude agent agent-b (pid 1)';
  const at = (lockReason = reason): Candidate => ({ branch: 'b', worktree: tree('/r/w/b', 'b', { locked: true, lockReason }), held: null });

  assert.deepEqual(tool.pruneVerdict(at(), clean), { remove: true, why: '', unlock: true });
  assert.deepEqual(tool.pruneVerdict(at(), { ...clean, gone: true }), { remove: true, why: '', gone: true, unlock: true });
  assert.equal(tool.pruneVerdict(at(), null).remove, false, 'a harness-locked worktree nobody looked into is kept');

  const inside = tool.pruneVerdict(at(), { ...clean, processes: [{ pid: 9, exe: 'C:/node.exe', command: 'node land.mjs' }] });
  assert.equal(inside.remove, false);
  assert.match(inside.why, /locked by the agent harness \(claude agent agent-b \(pid 1\)\)/);
  assert.match(inside.why, /a running process is inside it \(pid 9: node land\.mjs\)/);
  const unread = tool.pruneVerdict(at(), { tracked: [], untracked: [], local: null, atTip: false, idleMinutes: 300, processError: 'powershell could not start' });
  assert.equal(unread.remove, false);
  assert.match(unread.why, /whether a process runs inside it could not be read \(powershell could not start\)/);
  // A just-dispatched agent's branch sits at main's tip before its first commit, and counts as merged.
  const fresh = tool.pruneVerdict(at(), { ...clean, atTip: true });
  assert.equal(fresh.remove, false);
  assert.match(fresh.why, /its branch is at main's tip, as a just-dispatched agent's is/);
  // Every reason is said, not only the first.
  const all = tool.pruneVerdict(at(), {
    ...clean, tracked: ['a.ts'], untracked: ['n.txt'], local: 'local/notes.md', processes: [{ pid: 3, exe: '', command: 'code .' }],
  });
  assert.match(all.why, /tracked files \(a\.ts\)[\s\S]*local\/ \(local\/notes\.md\)[\s\S]*untracked files[\s\S]*pid 3: code \./);

  // A worker may be in the tree with no process naming its folder, so a write in the last two hours keeps it.
  const young = tool.pruneVerdict(at(), { ...clean, idleMinutes: 7 });
  assert.equal(young.remove, false);
  assert.equal('unlock' in young, false);
  assert.match(young.why, /locked by the agent harness \(claude agent agent-b \(pid 1\)\), and stays locked: written 7 minutes ago, a worker may be in it/);
  assert.equal(tool.pruneVerdict(at(), { ...clean, idleMinutes: tool.HARNESS_IDLE_MINUTES - 1 }).remove, false);
  assert.equal(tool.pruneVerdict(at(), { ...clean, idleMinutes: tool.HARNESS_IDLE_MINUTES }).remove, true, 'idle for the whole span goes');
  assert.match(tool.pruneVerdict(at(), { ...clean, idleMinutes: 1 }).why, /written 1 minute ago,/);
  const { idleMinutes: _unread, ...unwritten } = clean;
  const unreadWrite = tool.pruneVerdict(at(), unwritten);
  assert.equal(unreadWrite.remove, false);
  assert.match(unreadWrite.why, /when it was last written could not be read/);
  assert.equal(tool.pruneVerdict(at(), { ...unwritten, gone: true }).remove, true, 'a folder that is gone has no one in it');

  const ended: Facts = { ...clean, ended: true, idleMinutes: tool.ENDED_IDLE_MINUTES };
  assert.deepEqual(tool.pruneVerdict(at(), ended), { remove: true, why: '', unlock: true });
  assert.equal(tool.pruneVerdict(at(), { ...ended, atTip: true }).remove, true, 'its commits are what main now holds');
  assert.equal(tool.pruneVerdict(at(), { ...ended, idleMinutes: tool.ENDED_IDLE_MINUTES - 1 }).remove, false, 'still in the short quiet');
  assert.equal(tool.pruneVerdict(at(), { ...ended, processes: [{ pid: 9, exe: '', command: 'node land.mjs' }] }).remove, false, 'a process inside keeps it');
  assert.equal(tool.pruneVerdict(at(), { ...ended, tracked: ['a.ts'] }).remove, false, 'work in it keeps it');
  assert.equal(tool.pruneVerdict(at(), { ...clean, idleMinutes: tool.ENDED_IDLE_MINUTES }).remove, false, 'a worktree with no commit of its own waits the whole span');

  // Any other lock is in use however the worktree looks, and is never looked into or unlocked.
  for (const other of ['the owner keeps this', '']) {
    const kept = tool.pruneVerdict(at(other), clean);
    assert.equal(kept.remove, false, other);
    assert.match(kept.why, /its worktree is locked, so in use/);
    assert.match(kept.why, /never looked into or unlocked here/);
    assert.equal('unlock' in kept, false);
  }
  assert.match(tool.pruneVerdict(at('the owner keeps this'), clean).why, /\(the owner keeps this\)/);
});


test('the processes are read from a CIM query on Windows and ps elsewhere, and one is inside a folder its path or command line names', () => {
  const cim = JSON.stringify([
    { ProcessId: 0, ExecutablePath: null, CommandLine: null },
    { ProcessId: 4120, ExecutablePath: 'C:\\Program Files\\nodejs\\node.exe', CommandLine: '"C:\\Program Files\\nodejs\\node.exe" land.mjs' },
    { ProcessId: 77, ExecutablePath: 'C:\\Windows\\System32\\svchost.exe', CommandLine: null },
  ]);
  assert.deepEqual(tool.parseProcesses(cim, 'win32'), [
    { pid: 4120, exe: 'C:\\Program Files\\nodejs\\node.exe', command: '"C:\\Program Files\\nodejs\\node.exe" land.mjs' },
    { pid: 77, exe: 'C:\\Windows\\System32\\svchost.exe', command: 'C:\\Windows\\System32\\svchost.exe' },
  ], 'a process with neither path nor command line names no folder');
  // PowerShell writes one object, not an array, for a single process, and a console set to UTF-8 may write its preamble.
  assert.deepEqual(tool.parseProcesses(`﻿${JSON.stringify({ ProcessId: 5, ExecutablePath: null, CommandLine: 'x' })}\r\n`, 'win32'), [{ pid: 5, exe: '', command: 'x' }]);
  assert.deepEqual(tool.parseProcesses('    1 /sbin/init\n  234 node /work/x/land.mjs --flag\n\n', 'linux'), [
    { pid: 1, exe: '', command: '/sbin/init' }, { pid: 234, exe: '', command: 'node /work/x/land.mjs --flag' },
  ]);

  const folder = ['D:', 'work', 'repo', '.claude', 'worktrees', 'agent-a'].join('\\');
  const proc = (pid: number, command: string, exe = ''): Proc => ({ pid, exe, command });
  const found = tool.processesInside(folder, [
    proc(1, `"C:\\node.exe" ${folder}\\examples\\plugins\\github-pull-request\\land.mjs`),
    proc(2, 'node d:/WORK/repo/.claude/worktrees/agent-a/x.mjs'),
    proc(3, `node ${folder}b\\x.mjs`),
    proc(4, 'tool.exe --serve', `${folder}\\bin\\tool.exe`),
    proc(5, 'bash -c cd /d/work/repo/.claude/worktrees/agent-a && ls'),
    proc(6, `git -C "${folder}" status`),
    proc(7, `git -C ${folder}`),
    proc(8, 'node D:\\work\\repo\\tools\\merge-branch.mjs --prune'),
  ], 'win32');
  assert.deepEqual(found.map((p) => p.pid), [1, 2, 4, 5, 6, 7], 'agent-ab is not inside agent-a, and the checkout around it is not');
  assert.deepEqual(tool.processesInside('/work/x', [proc(1, 'node /work/x/land.mjs'), proc(2, 'node /work/xy/a'), proc(3, 'node /WORK/x/a')], 'linux')
    .map((p) => p.pid), [1], 'elsewhere a path is case-sensitive');
});


test('logs and scratch under local/ are an agent\'s disposable output; anything else there is kept and named', () => {
  for (const path of ['local/scratch/a.md', 'local/scratch/deep/b.json', 'local/gate.log', 'local/runs/x.log']) assert.equal(tool.disposableLocal(path), true, path);
  assert.equal(tool.disposableLocal('local/scratch', true), true, 'the scratch folder is not walked at all');
  for (const path of ['local/out.md', 'local/scratchpad.md', 'local/a.log.md', 'local/notes/log']) assert.equal(tool.disposableLocal(path), false, path);
  assert.equal(tool.disposableLocal('local/runs.log', true), false, 'a folder named like a log is walked');

  const fx = makeFixture('merge-branch-local');
  try {
    fx.write('local/scratch/a.md', 'scratch\n');
    fx.write('local/z.log', 'log\n');
    fx.write('local/runs/gate.log', 'log\n');
    assert.equal(tool.firstFile(join(fx.root, 'local'), tool.disposableLocal), null, 'only disposable output keeps nothing');
    fx.write('local/keep/notes.md', 'mine\n');
    assert.equal(tool.firstFile(join(fx.root, 'local'), tool.disposableLocal), 'local/keep/notes.md');
    assert.equal(tool.firstFile(join(fx.root, 'local')) !== null, true, 'with no rule, any file is found');
  } finally {
    fx.cleanup();
  }
});


test('a folder under .claude/worktrees/ git does not list goes when it holds no .git, is no link and no process runs inside', () => {
  const half: Folder = { path: '/r/.claude/worktrees/agent-h', link: false, git: false, processes: [] };
  assert.deepEqual(tool.folderVerdict(half), { remove: true, why: '' });
  const link = tool.folderVerdict({ ...half, link: true });
  assert.equal(link.remove, false);
  assert.match(link.why, /a link \(a reparse point\), never followed or removed here/);
  const git = tool.folderVerdict({ ...half, git: true });
  assert.equal(git.remove, false);
  assert.match(git.why, /holds a \.git/);
  const held = tool.folderVerdict({ ...half, processes: [{ pid: 7, exe: '', command: 'node land.mjs' }, { pid: 8, exe: '', command: 'x'.repeat(400) }] });
  assert.equal(held.remove, false);
  assert.match(held.why, /pid 7: node land\.mjs; pid 8: x+…\)/);
  assert.match(held.why, /never stopped here: end it, and the next prune removes the folder/);
  assert.ok(held.why.length < 400, 'a long command line is cut short');
  const unread = tool.folderVerdict({ path: half.path, link: false, git: false, processError: 'ps could not start' });
  assert.equal(unread.remove, false);
  assert.match(unread.why, /could not be read \(ps could not start\)/);
});


test('this repository commits no generated folder: its orientation index is ignored, written in each checkout', () => {
  assert.deepEqual(tool.readGenerated(repoRoot), []);
  assert.match(readFileSync(join(repoRoot, '.gitignore'), 'utf8'), /^docs\/index\/$/m);
});


test('a repository declaring no generated folder writes none, and an entry without its command is refused (GENREGEN1)', () => {
  const fx = makeFixture('merge-branch-generated');
  assert.deepEqual(tool.readGenerated(fx.root), [], 'no declaration at all');
  fx.write('daoris.gates.json', JSON.stringify({ gates: [{ name: 'g', run: 'node gate.mjs' }] }));
  assert.deepEqual(tool.readGenerated(fx.root), []);
  fx.write('daoris.gates.json', JSON.stringify({ gates: [], generated: [{ paths: 'out/', run: 'node write.mjs' }] }));
  assert.deepEqual(tool.readGenerated(fx.root), [{ folder: 'out', run: 'node write.mjs' }], 'a trailing slash is the folder');
  fx.write('daoris.gates.json', JSON.stringify({ gates: [], generated: [{ paths: 'out' }] }));
  assert.match(refusal(() => tool.readGenerated(fx.root)).message, /every generated entry needs both 'paths' and 'run'/);
  fx.cleanup();
});


// What union leaves when two branches each append a note to one decision, set apart by the merge.
test("a note's label straight under a line gets one blank line above it; nothing else in the file moves", () => {
  const glued = [
    '## D1 — a thing (2026-10-04)', '',
    'Body of it.', '',
    '**UX6e, built 2026-10-04: left.** Left text', 'more left.',
    '**UX6f, built 2026-10-04: right.** Right text.',
    '*Amended by D130 (ROW3, 2026-10-02): the change.*',
    // A fence's lines are text, and an emphasised sentence is not a note: neither is touched.
    '```', 'text', '**Built 2026-10-02 (EX1).**', '```',
    'A paragraph.', '**Proven without the rehearsal**: the checks.', '',
  ].join('\n');
  const { text, set } = tool.setApart(glued);
  assert.equal(text, [
    '## D1 — a thing (2026-10-04)', '',
    'Body of it.', '',
    '**UX6e, built 2026-10-04: left.** Left text', 'more left.', '',
    '**UX6f, built 2026-10-04: right.** Right text.', '',
    '*Amended by D130 (ROW3, 2026-10-02): the change.*',
    '```', 'text', '**Built 2026-10-02 (EX1).**', '```',
    'A paragraph.', '**Proven without the rehearsal**: the checks.', '',
  ].join('\n'));
  // Each label with its line once set apart, 1-based, as a reader opens it.
  assert.deepEqual(set, [
    { line: 8, label: '**UX6f, built 2026-10-04: right.** Right text.' },
    { line: 10, label: '*Amended by D130 (ROW3, 2026-10-02): the change.*' },
  ]);
  // Set apart already, it is left alone; and a file of CRLF lines gains a CRLF blank line, not a bare one.
  assert.deepEqual(tool.setApart(text), { text, set: [] });
  assert.equal(tool.setApart('A.\r\n**Built 2026-10-04 (X1).**\r\n').text, 'A.\r\n\r\n**Built 2026-10-04 (X1).**\r\n');
});


// A heading union leaves under another entry's last line, set apart unless a side of the merge held it so.
test("a heading straight under a line gets one blank line above it, unless a side already held that join", () => {
  const old = ['# Archive', '', '## A1 — old (2026-10-01)', 'Done.', '## A2 — glued long ago (2026-10-02)', 'Done.'];
  const merged = [...old, '## A3 — left (2026-10-20)', 'Left.', '## A4 — right (2026-10-20)', 'Right.',
    // A fence's lines are text: a heading quoted there is not one.
    '```', 'x', '## not a heading', '```', ''].join('\n');
  const { text, set } = tool.setApartHeadings(merged, [[...old, '', '## A3 — left (2026-10-20)', 'Left.', ''].join('\n')]);
  assert.equal(text, [...old, '', '## A3 — left (2026-10-20)', 'Left.', '', '## A4 — right (2026-10-20)', 'Right.',
    '```', 'x', '## not a heading', '```', ''].join('\n'));
  assert.deepEqual(set, [
    { line: 8, label: '## A3 — left (2026-10-20)' },
    { line: 11, label: '## A4 — right (2026-10-20)' },
  ]);
  assert.deepEqual(tool.setApartHeadings(text, [merged]), { text, set: [] });
  assert.equal(tool.setApartHeadings('A.\r\n## B\r\n').text, 'A.\r\n\r\n## B\r\n');
});
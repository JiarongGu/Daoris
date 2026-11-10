// node --test src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/hooks/subagent-worktree.test.mjs
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

import { subagentWorktree } from './subagent-worktree.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));

/** A reader over a map of files, as the judgement reads them: a file's text, or null. */
const reader = (files) => (file) => files[file] ?? null;

// A session's tree is a linked worktree of the person's checkout; the harness makes its subagent's beside the checkout's own.
const posix = {
  '/home/u/.daoris/trees/ws/engine/s1/.git': 'gitdir: /work/engine/.git/worktrees/s1\n',
  '/work/engine/.git/worktrees/s1/commondir': '../..\n',
  '/work/engine/.claude/worktrees/agent-a1/.git': 'gitdir: /work/engine/.git/worktrees/agent-a1\n',
  '/work/engine/.git/worktrees/agent-a1/commondir': '../..\n',
  '/work/engine/.claude/worktrees/agent-b2/.git': 'gitdir: /work/other/.git/worktrees/agent-b2\n',
  '/work/other/.git/worktrees/agent-b2/commondir': '../..\n',
};
const tree = '/home/u/.daoris/trees/ws/engine/s1';
const judge = (cwd, agentId = 'a1', files = posix) =>
  subagentWorktree({ tree, cwd, agentId, platform: 'linux', read: reader(files) });

test('a subagent standing in its own worktree of the session\'s repository is answered that worktree', () => {
  assert.equal(judge('/work/engine/.claude/worktrees/agent-a1'), '/work/engine/.claude/worktrees/agent-a1');
  assert.equal(judge('/work/engine/.claude/worktrees/agent-a1/src/deep'), '/work/engine/.claude/worktrees/agent-a1');
});

test('the session\'s own agent, a subagent in the tree and one in another repository\'s worktree are answered none', () => {
  assert.equal(judge('/work/engine/.claude/worktrees/agent-a1', null), null);
  assert.equal(judge('/work/engine/.claude/worktrees/agent-a1', ''), null);
  assert.equal(judge(tree), null);
  assert.equal(judge('/work/engine/.claude/worktrees/agent-b2'), null);
  assert.equal(judge('/work/engine/.claude/worktrees'), null);
  assert.equal(judge('/work/engine/.claude/worktrees/agent-a1/../../../src'), null);
  assert.equal(judge('/work/engine/.claude/worktrees/agent-c3'), null);
  assert.equal(judge(null), null);
});

test('a session in the main checkout itself already holds the folder, and is answered none', () => {
  assert.equal(subagentWorktree({
    tree: '/work/engine', cwd: '/work/engine/.claude/worktrees/agent-a1', agentId: 'a1', platform: 'linux', read: reader(posix),
  }), null);
});

test('on Windows the folder is matched without case, through git\'s forward slashes', () => {
  const files = {
    'C:\\Home\\.daoris\\trees\\ws\\engine\\s1\\.git':'gitdir: D:/Work/Engine/.git/worktrees/s1\n',
    'D:\\Work\\Engine\\.git\\worktrees\\s1\\commondir': '../..\n',
    'D:\\work\\engine\\.claude\\worktrees\\agent-a1\\.git': 'gitdir: D:/Work/Engine/.git/worktrees/agent-a1\n',
    'D:\\Work\\Engine\\.git\\worktrees\\agent-a1\\commondir': '../..\n',
  };
  const answer = subagentWorktree({
    tree: 'C:\\Home\\.daoris\\trees\\ws\\engine\\s1', cwd: 'd:\\work\\engine\\.claude\\worktrees\\agent-a1\\src', agentId: 'a1',
    platform: 'win32', read: (file) => files[Object.keys(files).find((key) => key.toLowerCase() === file.toLowerCase())] ?? null,
  });
  assert.equal(answer?.toLowerCase(), 'd:\\work\\engine\\.claude\\worktrees\\agent-a1');
});

test('the guard lets a subagent write in its own worktree, and refuses the session\'s agent there and the subagent outside it', () => {
  const root = path.resolve(here, '..', '..', '..', '..', '..', '_fixtures', `subagent-worktree-${process.pid}-${Date.now()}`);
  const checkout = path.join(root, 'work', 'engine');
  const session = path.join(root, 'trees', 's1');
  const worktree = path.join(checkout, '.claude', 'worktrees', 'agent-a1');
  const put = (file, text) => {
    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, text);
  };
  try {
    put(path.join(session, '.git'), `gitdir: ${path.join(checkout, '.git', 'worktrees', 's1').replaceAll('\\', '/')}\n`);
    put(path.join(checkout, '.git', 'worktrees', 's1', 'commondir'), '../..\n');
    put(path.join(worktree, '.git'), `gitdir: ${path.join(checkout, '.git', 'worktrees', 'agent-a1').replaceAll('\\', '/')}\n`);
    put(path.join(checkout, '.git', 'worktrees', 'agent-a1', 'commondir'), '../..\n');

    const hook = (call) => spawnSync(process.execPath, [path.join(here, 'tree-guard.mjs'), session], {
      input: JSON.stringify({ hook_event_name: 'PreToolUse', ...call }), encoding: 'utf8',
    });
    const write = (file) => ({ tool_name: 'Write', tool_input: { file_path: file, content: 'x' } });

    const own = hook({ ...write(path.join(worktree, 'src', 'a.ts')), cwd: worktree, agent_id: 'a1' });
    const shell = hook({ tool_name: 'Bash', tool_input: { command: 'echo x > notes.txt' }, cwd: worktree, agent_id: 'a1' });
    const sessions = hook({ ...write(path.join(worktree, 'src', 'a.ts')), cwd: session });
    const outside = hook({ ...write(path.join(checkout, 'src', 'a.ts')), cwd: worktree, agent_id: 'a1' });

    assert.equal(own.status, 0);
    assert.equal(own.stdout.trim(), '');
    assert.equal(shell.stdout.trim(), '');
    assert.match(sessions.stdout, /"permissionDecision":"deny"/);
    assert.match(outside.stdout, /"permissionDecision":"deny"/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

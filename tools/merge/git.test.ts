import { test } from 'vitest';
import assert from 'node:assert/strict';
import { ignoredVerdict, parseStatus, parseWorktrees, startRefusal, worktreeFor, type Worktree } from './git.ts';

const tree = (path: string, branch: string, extra: Partial<Worktree> = {}): Worktree => ({
  path, branch, locked: false, lockReason: '', prunable: false, bare: false, ...extra,
});

test('the status is read for the branch and every changed or untracked path', () => {
  const status = [
    '# branch.oid 0123456789abcdef0123456789abcdef01234567',
    '# branch.head main',
    '1 .M N... 100644 100644 100644 aaaaaaa aaaaaaa src/a file.ts',
    '2 R. N... 100644 100644 100644 bbbbbbb bbbbbbb R100 new name.md\told.md',
    'u UU N... 100644 100644 100644 100644 ccccccc ccccccc ccccccc shared.txt',
    '? stray.txt',
  ].join('\n');
  assert.deepEqual(parseStatus(status), {
    branch: 'main',
    changed: [
      { code: '.M', path: 'src/a file.ts' },
      { code: 'R.', path: 'new name.md' },
      { code: 'UU', path: 'shared.txt' },
      { code: '??', path: 'stray.txt' },
    ],
  });
  assert.deepEqual(parseStatus('# branch.oid 01234\r\n# branch.head (detached)\r\n'), { branch: '', changed: [] });
});

test('a merge starts only on main, with no merge open, a clean tree and ignored logs, and says the first thing wrong', () => {
  const ready = { branch: 'main', merging: false, changed: [], ignored: true };
  assert.equal(startRefusal(ready), null);
  assert.match(startRefusal({ ...ready, branch: 'work', changed: [{ code: '??', path: 'x' }] })!, /the current branch is 'work', not main/);
  assert.match(startRefusal({ ...ready, branch: '' })!, /a detached HEAD/);
  assert.match(startRefusal({ ...ready, merging: true })!, /already in progress/);
  assert.match(startRefusal({ ...ready, changed: [{ code: '??', path: 'stray.txt' }] })!, /not clean:\n {2}\?\? stray\.txt$/);
  const many = Array.from({ length: 13 }, (_, i) => ({ code: '.M', path: `f${i}` }));
  assert.match(startRefusal({ ...ready, changed: many })!, /… 1 more$/);
  assert.match(startRefusal({ ...ready, ignored: false })!, /local\/scratch\/ is not ignored/);
});

test('a check-ignore probe: status 0 is ignored, 1 is not, anything else is a tool error quoting git', () => {
  assert.equal(ignoredVerdict({ status: 0, err: '' }, 'local/scratch/x.log'), true);
  assert.equal(ignoredVerdict({ status: 1, err: '' }, 'local/scratch/x.log'), false);
  assert.throws(() => ignoredVerdict({ status: 128, err: 'fatal: unable to read index' }, 'local/scratch/x.log'), /check-ignore.*local\/scratch\/x\.log.*failed.*status 128.*fatal: unable to read index/s);
  assert.throws(() => ignoredVerdict({ status: null, err: '' }, 'p'), /failed/);
});

test('the worktree a branch is checked out in is found in the porcelain list', () => {
  const list = [
    'worktree /work/repo', 'HEAD 0123456', 'branch refs/heads/main', '',
    'worktree /work/repo/.claude/worktrees/agent-a', 'HEAD 89abcde', 'branch refs/heads/worktree-agent-a', '',
    'worktree /work/elsewhere', 'HEAD 1234567', 'detached', '',
  ].join('\n');
  assert.equal(worktreeFor(list, 'worktree-agent-a'), '/work/repo/.claude/worktrees/agent-a');
  assert.equal(worktreeFor(list, 'main'), '/work/repo');
  assert.equal(worktreeFor(list, 'worktree-agent'), null, 'a prefix is not the branch');
  assert.equal(worktreeFor(list, 'nope'), null);
});

test('the worktree list is read for each path, branch, lock and lock reason, and a folder git says is gone', () => {
  const list = [
    'worktree D:/work/repo', 'HEAD 0123456', 'branch refs/heads/main', '',
    'worktree D:/work/repo/.claude/worktrees/agent-a', 'HEAD 89abcde', 'branch refs/heads/worktree-agent-a',
    'locked claude agent agent-a (pid 57256)', '',
    'worktree D:/work/repo/.claude/worktrees/agent-b', 'HEAD 89abcde', 'branch refs/heads/worktree-agent-b', 'locked', '',
    'worktree D:/work/gone', 'HEAD 1234567', 'branch refs/heads/gone', 'prunable gitdir file points to non-existent location', '',
    'worktree D:/work/elsewhere', 'HEAD 1234567', 'detached', '',
  ].join('\r\n');
  assert.deepEqual(parseWorktrees(list), [
    tree('D:/work/repo', 'main'),
    tree('D:/work/repo/.claude/worktrees/agent-a', 'worktree-agent-a', { locked: true, lockReason: 'claude agent agent-a (pid 57256)' }),
    tree('D:/work/repo/.claude/worktrees/agent-b', 'worktree-agent-b', { locked: true }),
    tree('D:/work/gone', 'gone', { prunable: true }),
    tree('D:/work/elsewhere', ''),
  ]);
});

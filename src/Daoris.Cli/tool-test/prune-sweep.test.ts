import { test } from 'node:test';
import assert from 'node:assert/strict';
import { disposableLocal, pruneVerdict, type Facts } from '../../../tools/merge/prune-verdict.ts';
import { unmergedLines, unmergedWorktrees } from '../../../tools/merge/prune-report.ts';
import { tool } from './support/merge-branch.ts';
import { tree } from './support/merge-branch-kit.ts';

// reaches: tools/merge/prune-verdict.ts, tools/merge/prune-report.ts — the sweep of builder worktrees

const clean: Facts = { tracked: [], untracked: [], local: null };
const DAY = 86_400_000;

test('a quick run\'s base tree under local/ is derived output, so it does not keep a merged worktree (it kept every one that ran quick and was killed)', () => {
  assert.equal(disposableLocal('local/quick-base-tree', true), true, 'the tree is not walked');
  assert.equal(disposableLocal('local/quick-base-tree/src/a.cs'), true);
  assert.equal(disposableLocal('local/quick-base-tree-notes.md'), false, 'a neighbour of that name is work');
  assert.equal(pruneVerdict({ branch: 'b', worktree: tree('/r/.claude/worktrees/b', 'b'), held: null }, { ...clean, local: null }).remove, true);
});

test('the sweep removes a merged clean builder worktree and keeps a merged dirty one, an unmerged one is never a candidate', () => {
  const worktrees = [tree('/r', 'main'), tree('/r/.claude/worktrees/done', 'worktree-done'), tree('/r/.claude/worktrees/dirty', 'worktree-dirty'), tree('/r/.claude/worktrees/open', 'worktree-open')];
  const candidates = tool.pruneCandidates({ merged: ['main', 'worktree-done', 'worktree-dirty'], worktrees, current: 'main' });
  assert.deepEqual(candidates.map((c) => c.branch), ['worktree-done', 'worktree-dirty'], 'main holds only those two');
  const verdicts = Object.fromEntries(candidates.map((c) => [c.branch, pruneVerdict(c, c.branch === 'worktree-dirty' ? { ...clean, tracked: ['a.ts'] } : clean)]));
  assert.equal(verdicts['worktree-done']?.remove, true);
  assert.equal(verdicts['worktree-dirty']?.remove, false);
  assert.match(verdicts['worktree-dirty']?.why ?? '', /modified or staged/);
});

test('a harness lock is lifted only for a merged worktree that is clean, quiet and has no process in it', () => {
  const locked = tree('/r/.claude/worktrees/a', 'worktree-a', { locked: true, lockReason: 'claude agent a1 (pid 7)' });
  const candidate = { branch: 'worktree-a', worktree: locked, held: null };
  const quiet = { ...clean, atTip: false, ended: true, processes: [], idleMinutes: 600 };
  assert.deepEqual(pruneVerdict(candidate, quiet), { remove: true, why: '', unlock: true });
  assert.equal(pruneVerdict(candidate, { ...quiet, tracked: ['x'] }).remove, false);
  assert.equal(pruneVerdict(candidate, { ...quiet, processes: [{ pid: 9, command: 'node in a' }] }).remove, false);
  assert.equal(pruneVerdict({ ...candidate, worktree: { ...locked, lockReason: 'a person locked it' } }, quiet).remove, false, 'a lock that is not the harness\'s is never lifted');
});

test('the unmerged builder worktrees are reported for the steward with their age and last commit, oldest first', () => {
  const now = Date.parse('2026-11-20T12:00:00Z');
  const worktrees = [
    tree('/r', 'main'),
    tree('/r/.claude/worktrees/merged', 'worktree-merged'),
    tree('/r/.claude/worktrees/new', 'worktree-new'),
    tree('/r/.claude/worktrees/old', 'worktree-old'),
    tree('/r/.claude/worktrees/none', 'worktree-none'),
    tree('/r/.claude/worktrees/session', 'daoris/s-1'),
    tree('/elsewhere/other', 'other'),
  ];
  const last: Record<string, { at: number; sha: string; subject: string } | null> = {
    '/r/.claude/worktrees/new': { at: now - 2 * DAY, sha: 'aaaaaaa', subject: 'feat: new' },
    '/r/.claude/worktrees/old': { at: now - 9 * DAY, sha: 'bbbbbbb', subject: 'fix: old' },
    '/r/.claude/worktrees/none': null,
  };
  const found = unmergedWorktrees(worktrees, ['main', 'worktree-merged'], (path) => last[path] ?? null, now);
  assert.deepEqual(found.map((one) => one.branch), ['worktree-old', 'worktree-new', 'worktree-none'], 'no main, no merged, no Daoris session, none beside the checkout');
  const lines = unmergedLines(found).join('\n');
  assert.match(lines, /3 unmerged builder worktrees/);
  assert.match(lines, /worktree-old.*9 days.*bbbbbbb.*fix: old/);
  assert.match(lines, /worktree-none.*no commit read/);
  assert.deepEqual(unmergedLines([]), []);
});

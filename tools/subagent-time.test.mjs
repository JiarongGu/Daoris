import { test } from 'node:test';
import assert from 'node:assert/strict';
import { bucketOf, clockOf, commandClass, pathClass } from './subagent-time.mjs';

test('CONSOLIDATE1: a path and a command are classed by what they touch and run', () => {
  assert.equal(pathClass('D:/r/src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs'), 'code');
  assert.equal(pathClass('/src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Loop/DriverTests.cs'), 'tests');
  assert.equal(pathClass('/docs/decisions/D1.md'), 'records');
  assert.equal(pathClass('/docs/index/outlines/src/a.cs.md'), 'generated');
  assert.equal(pathClass('/src/a.Tests/fixtures/x.json'), 'fixtures');
  assert.equal(commandClass('dotnet test src/X --filter Y'), 'test');
  assert.equal(commandClass('npm run verify'), 'verify');
  assert.equal(commandClass('dotnet build src/X'), 'build');
  assert.equal(bucketOf('Edit', { file_path: '/docs/FIX-LOG.md' }), 'edit:records');
});

test('a builder\'s worktree path is classed by the path inside its worktree', () => {
  assert.equal(pathClass('D:\\r\\.claude\\worktrees\\agent-a1\\src\\Daoris.Service\\Program.cs'), 'code');
  assert.equal(pathClass('D:/r/.claude/worktrees/agent-a1/docs/areas/x.md'), 'records');
  assert.equal(pathClass('D:/r/.claude/agents/branch-worker.md'), 'records');
});

test('CONSOLIDATE1: a call is timed from its ask to its result, and the model from a result to its next ask', () => {
  const lines = [
    { type: 'user', timestamp: '2026-11-04T00:00:00Z', message: { content: 'go' } },
    { type: 'assistant', timestamp: '2026-11-04T00:00:10Z', message: { content: [{ type: 'tool_use', id: 't1', name: 'Bash', input: { command: 'dotnet test x' } }] } },
    { type: 'user', timestamp: '2026-11-04T00:01:10Z', message: { content: [{ type: 'tool_result', tool_use_id: 't1' }] } },
    { type: 'assistant', timestamp: '2026-11-04T00:01:30Z', message: { content: [{ type: 'text', text: 'done' }] } },
  ];
  const clock = clockOf(lines);
  assert.equal(clock.buckets['run:test'], 60000);
  assert.equal(clock.model, 30000);
  assert.equal(clock.total, 90000);
});

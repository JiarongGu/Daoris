// node --test src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/ClaudeCode/hooks/permission-ask.test.mjs
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ask, decisionOf, wouldAsk } from './permission-ask.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const scratch = path.resolve(here, '../../../../../../../_fixtures/permission-ask');

const call = (tool_name, permission_mode, tool_input = {}) => ({ hook_event_name: 'PreToolUse', tool_name, permission_mode, tool_input });

test('a call the harness would ask about is asked, and one it takes unasked is left to it', () => {
  const rules = { allow: ['Bash(git status:*)', 'mcp__daoris-knowledge'], deny: ['Bash(git push:*)'], ask: [] };

  assert.equal(wouldAsk(call('Bash', 'default', { command: 'npm test' }), rules), true);
  assert.equal(wouldAsk(call('Bash', 'acceptEdits', { command: 'npm test' }), rules), true);
  assert.equal(wouldAsk(call('Edit', 'default', { file_path: 'a.ts' }), rules), true);

  assert.equal(wouldAsk(call('Bash', 'default', { command: 'git status --short' }), rules), false, 'a rule allows it');
  assert.equal(wouldAsk(call('Bash', 'default', { command: 'git push origin' }), rules), false, 'a rule denies it: the harness refuses');
  assert.equal(wouldAsk(call('mcp__daoris-knowledge__knowledge_search', 'default'), rules), false, 'a server rule allows its tools');
  assert.equal(wouldAsk(call('Read', 'default', { file_path: 'a.ts' }), rules), false, 'a read never asks');
  assert.equal(wouldAsk(call('Edit', 'acceptEdits', { file_path: 'a.ts' }), rules), false, 'acceptEdits takes an edit');
  assert.equal(wouldAsk(call('Bash', 'bypassPermissions', { command: 'npm test' }), rules), false, 'full access asks nothing');
});

test('an ask rule asks even where an allow covers the call', () => {
  assert.equal(wouldAsk(call('Bash', 'default', { command: 'npm test' }), { allow: ['Bash'], ask: ['Bash(npm test)'] }), true);
});

test('the answer the loop writes beside the request is the decision; none by the bound refuses it, saying so', async () => {
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });
  try {
    const answering = ask(scratch, 's1', call('Bash', 'default', { command: 'npm test' }), 5, async () => {
      for (const name of readdirSync(scratch).filter((each) => each.endsWith('.json') && !each.endsWith('.answer.json'))) {
        const request = JSON.parse(readFileSync(path.join(scratch, name), 'utf8'));
        assert.equal(request.move, 'permit');
        assert.equal(request.text, 'Bash: npm test');
        rmSync(path.join(scratch, name));
        writeFileSync(path.join(scratch, `s1.${request.key}.answer.json`), JSON.stringify({ operation: 'Bash', done: true, code: 'answered', said: 'Allowed once.' }));
      }
      await new Promise((done) => setTimeout(done, 10));
    });
    const decision = decisionOf(await answering, 5);
    assert.equal(decision.hookSpecificOutput.permissionDecision, 'allow');
    assert.deepEqual(readdirSync(scratch), [], 'the answer is taken');

    const unanswered = await ask(scratch, 's1', call('Bash', 'default', { command: 'npm test' }), 0.05, (ms) => new Promise((done) => setTimeout(done, Math.min(ms, 10))));
    assert.equal(unanswered, null);
    assert.deepEqual(readdirSync(scratch), [], 'a request nobody took is withdrawn');
    const refused = decisionOf(unanswered, 900).hookSpecificOutput;
    assert.equal(refused.permissionDecision, 'deny');
    assert.match(refused.permissionDecisionReason, /Nobody answered within 15 minutes/);
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
});

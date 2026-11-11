import assert from 'node:assert/strict';
import { test } from 'node:test';
import { askedFor } from './fake-model.mjs';
import { frame, landing } from './steer-proof.mjs';

test('a word echoed before the turn result joined it; after it, it is the next turn; unechoed, never', () => {
  const echo = { type: 'user', isReplay: true, uuid: 'w' };
  const result = { type: 'result' };
  assert.equal(landing([{ type: 'assistant' }, echo, result], 'w'), 'joined');
  assert.equal(landing([result, { type: 'system' }, echo, result], 'w'), 'next-turn');
  assert.equal(landing([result, { ...echo, uuid: 'other' }], 'w'), 'never');
});

test('the word is framed as one user line with no priority under its uuid', () => {
  const line = frame('put MANGO in your reply', 'w');
  assert.ok(line.endsWith('\n'));
  assert.deepEqual(JSON.parse(line), { type: 'user', uuid: 'w', message: { role: 'user', content: [{ type: 'text', text: 'put MANGO in your reply' }] } });
});

test('the stub reads what a call asks for from its last user words, never from a tool result', () => {
  assert.equal(askedFor([{ role: 'user', content: 'run the TOOL now' }]), 'tool');
  assert.equal(askedFor([{ role: 'user', content: [{ type: 'text', text: 'STREAM some words' }] }]), 'stream');
  assert.equal(askedFor([{ role: 'user', content: [{ type: 'tool_result', content: 'TOOL' }] }]), 'reply');
  assert.equal(askedFor([{ type: 'function_call_output', output: 'TOOL' }]), 'reply');
  assert.equal(askedFor([]), 'reply');
});

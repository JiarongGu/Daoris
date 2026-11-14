import { test } from 'vitest';
import { readFileSync } from 'node:fs';
import assert from 'node:assert/strict';
import { COMMIT_FORMAT, MAKER, NAMED, commitAttributionProblems, parseCommits } from './attribution.ts';

test("the commit log is read for each commit's trailer, and a merge of main into the branch is marked", () => {
  const log = [
    'abc1234\x1fp1\x1fwork on it\x1fFixture <fixture@example.test>\n\x1e',
    "\ndef5678\x1fp1 p2\x1fMerge branch 'main' into work\x1f\x1e",
    '\n9abcdef\x1fp1\x1fbare\x1f\x1e\n',
  ].join('');
  assert.deepEqual(parseCommits(log), [
    { sha: 'abc1234', merge: false, subject: 'work on it', trailer: 'Fixture <fixture@example.test>' },
    { sha: 'def5678', merge: true, subject: "Merge branch 'main' into work", trailer: '' },
    { sha: '9abcdef', merge: false, subject: 'bare', trailer: '' },
  ]);
  assert.deepEqual(parseCommits(''), []);
});

test('the commit check accepts each allowed maker and legacy harness trailers, and names the expected makers on refusal', () => {
  const commit = (trailer: string, merge = false) => ({ sha: 'abc1234', subject: 'work', merge, trailer });
  for (const trailer of [
    'Claude Opus 4.6 <noreply@anthropic.com>',
    'Claude (Anthropic, sonnet) <fixture@example.test>',
    'Codex (OpenAI, gpt-fixture) <noreply@openai.com>',
    'OpenAI gpt-fixture <fixture@example.test>',
    'DeepSeek (fixture-model) <noreply@deepseek.com>',
    'Human <person@example.test>\nCodex gpt-fixture <fixture@example.test>',
    'Daoris <noreply@daoris.invalid>',
  ]) assert.deepEqual(commitAttributionProblems([commit(trailer)]), [], trailer);
  assert.deepEqual(commitAttributionProblems([commit('', true)]), []);
  for (const trailer of ['', 'Fixture <fixture@example.test>', 'OpenAI', 'Someone <OpenAI@example.test>', 'Someone <Daoris@example.test>']) {
    const problems = commitAttributionProblems([commit(trailer)]);
    assert.equal(problems.length, 1, trailer);
    assert.match(problems[0]!, /abc1234 work.*Co-Authored-By.*expected.*Anthropic.*OpenAI.*DeepSeek.*Daoris/);
  }
});

test("the commit check's format, maker rule and trailer shape are the driver's hand-back reading's, held to one fixture", () => {
  const contract = JSON.parse(readFileSync(new URL('./attribution-contract.json', import.meta.url), 'utf8'));
  assert.equal(COMMIT_FORMAT, contract.format);
  assert.equal(MAKER.source, contract.maker);
  assert.equal(MAKER.flags, 'i');
  assert.equal(NAMED.source, contract.named);
  const commit = (trailer: string) => ({ sha: 'abc1234', subject: 'work', merge: false, trailer });
  for (const trailer of contract.signed) assert.deepEqual(commitAttributionProblems([commit(trailer)]), [], trailer);
  for (const trailer of contract.unsigned) assert.equal(commitAttributionProblems([commit(trailer)]).length, 1, trailer);
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { HANDED_PROFILE_VERBS, HANDED_VERBS, handed } from '../../src/cli/agent.ts';
import { driverFile } from '../_csharp.ts';

/**
 * `daoris agent`'s allow-list (PUSH1f2d, the alone-path design §5): until the family's last row (PUSH1f2e) the CLI hands Daoris's driver
 * program the verbs it has, and keeps the rest. Held to the program's own lists, `AgentsCommand.Verbs` and the `profile` verbs
 * `AgentAccountsCommand.ProfileVerbs` (PUSH1f2e2), read from their source, so a verb the program lacks is never handed on, and one it
 * has is never kept here.
 */
function declared(file: string, property: string): string[] {
  const source = readFileSync(driverFile(file), 'utf8');
  const start = source.indexOf(`public static IReadOnlyList<string> ${property} { get; } =`);
  assert.ok(start > 0, `${file} declares no ${property}`);
  const block = source.slice(start, source.indexOf('];', start));
  return [...block.matchAll(/"([a-z-]+)"/g)].map((match) => match[1]!);
}

test('the verbs handed on are exactly the ones `daoris-driver agent` has', () => {
  assert.deepEqual([...HANDED_VERBS], declared('AgentsCommand.cs', 'Verbs'));
  assert.deepEqual([...HANDED_PROFILE_VERBS], declared('AgentAccountsCommand.cs', 'ProfileVerbs'));
});

test('the rules, a trust, an account\'s settings, an unpin and the account verbs but `use` are handed on; the rest stays the CLI\'s', () => {
  for (const argv of [['rules'], ['rules', 'accept', 'p0000007'], ['trust', 'claude-code', '.', '--yes'], ['settings', 'claude-code'],
    ['which', 'claude-code-acp', '--json'], ['unpin', 'codex'], ['unpin', 'codex', '--workspace', 'work'], ['profile'], ['profile', 'list'],
    ['profile', 'add', 'claude-code', 'work'], ['profile', 'remove', 'claude-code', 'work'], ['profile', 'default', 'claude-code', '--clear'],
    ['profile', 'order', 'claude-code', 'a', 'b'], ['profile', 'ready', 'claude-code', '--own'], ['profile', 'rename', 'claude-code', 'a', 'b'],
    ['profile', 'join', 'claude-code', 'a', '--machine']]) {
    assert.equal(handed(argv), true, argv.join(' '));
  }
  for (const argv of [[], ['list'], ['install', 'claude-code'], ['login', 'claude-code'], ['key', 'claude-code'], ['pin', 'codex', '1.0.0'],
    ['update', 'codex'], ['profile', 'use', 'claude-code'], ['profile', 'frobnicate'], ['frobnicate']]) {
    assert.equal(handed(argv), false, argv.join(' ') || '(none)');
  }
});

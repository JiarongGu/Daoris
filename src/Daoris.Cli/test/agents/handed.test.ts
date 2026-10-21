import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { HANDED_VERBS, handed } from '../../src/cli/agent.ts';
import { driverFile } from '../_csharp.ts';

/**
 * `daoris agent`'s allow-list (PUSH1f2d, the alone-path design §5): until the family's last row (PUSH1f2e) the CLI hands Daoris's driver
 * program the verbs it has, and keeps the rest. Held to the program's own list, `AgentsCommand.Verbs`, read from its source, so a verb
 * the program lacks is never handed on, and one it has is never kept here. `profile` is handed whole since PUSH1f2e3: the program has
 * every profile verb (`AgentAccountsCommand.ProfileVerbs`), and refuses an unknown one in the words the CLI said.
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
  assert.deepEqual(declared('AgentAccountsCommand.cs', 'ProfileVerbs'),
    ['list', 'add', 'remove', 'default', 'order', 'ready', 'use', 'rename', 'join']);
});

test('the rules, a trust, an account\'s settings, an unpin and every account verb are handed on; the rest stays the CLI\'s', () => {
  for (const argv of [['rules'], ['rules', 'accept', 'p0000007'], ['trust', 'claude-code', '.', '--yes'], ['settings', 'claude-code'],
    ['which', 'claude-code-acp', '--json'], ['unpin', 'codex'], ['unpin', 'codex', '--workspace', 'work'], ['profile'], ['profile', 'list'],
    ['profile', 'add', 'claude-code', 'work'], ['profile', 'remove', 'claude-code', 'work'], ['profile', 'default', 'claude-code', '--clear'],
    ['profile', 'order', 'claude-code', 'a', 'b'], ['profile', 'ready', 'claude-code', '--own'], ['profile', 'rename', 'claude-code', 'a', 'b'],
    ['profile', 'join', 'claude-code', 'a', '--machine'], ['profile', 'use', 'claude-code'], ['profile', 'use', 'claude-code', '--clear'],
    ['profile', 'frobnicate']]) {
    assert.equal(handed(argv), true, argv.join(' '));
  }
  for (const argv of [[], ['list'], ['install', 'claude-code'], ['login', 'claude-code'], ['key', 'claude-code'], ['pin', 'codex', '1.0.0'],
    ['update', 'codex'], ['frobnicate']]) {
    assert.equal(handed(argv), false, argv.join(' ') || '(none)');
  }
});

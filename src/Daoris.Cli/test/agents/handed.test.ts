import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { HANDED_VERBS, handed } from '../../src/cli/agent.ts';
import { driverFile } from '../_csharp.ts';

/**
 * `daoris agent`'s allow-list (PUSH1f2d, the alone-path design §5): until the family's last row (PUSH1f2e) the CLI hands Daoris's driver
 * program the verbs it has, and keeps the rest. Held to the program's own list, `AgentsCommand.Verbs`, read from its source, so a verb
 * the program lacks is never handed on, and one it has is never kept here.
 */
test('the verbs handed on are exactly the ones `daoris-driver agent` has', () => {
  const source = readFileSync(driverFile('AgentsCommand.cs'), 'utf8');
  const start = source.indexOf('public static IReadOnlyList<string> Verbs { get; } =');
  assert.ok(start > 0, 'AgentsCommand declares no Verbs');
  const block = source.slice(start, source.indexOf('];', start));
  const verbs = [...block.matchAll(/"([a-z-]+)"/g)].map((match) => match[1]);
  assert.deepEqual([...HANDED_VERBS], verbs);
});

test('the rules, a trust and an account\'s settings are handed on; the rest of the family stays the CLI\'s', () => {
  for (const argv of [['rules'], ['rules', 'accept', 'p0000007'], ['trust', 'claude-code', '.', '--yes'], ['settings', 'claude-code'],
    ['which', 'claude-code-acp', '--json']]) {
    assert.equal(handed(argv), true, argv.join(' '));
  }
  for (const argv of [[], ['list'], ['install', 'claude-code'], ['login', 'claude-code'], ['key', 'claude-code'], ['pin', 'codex', '1.0.0'],
    ['unpin', 'codex'], ['update', 'codex'], ['profile', 'list'], ['frobnicate']]) {
    assert.equal(handed(argv), false, argv.join(' ') || '(none)');
  }
});

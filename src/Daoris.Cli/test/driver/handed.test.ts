import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { HANDED_VERBS, handed } from '../../src/cli/driver.ts';
import { driverFile } from '../_csharp.ts';

/**
 * `daoris driver`'s allow-list (PUSH1f2a, the alone-path design §5): until the family's last row (PUSH1f2b) the CLI hands Daoris's
 * driver program the verbs it has, and keeps the rest. Held to the program's own list, `DriverChoicesCommand.Verbs`, read from its
 * source, so a verb the program lacks is never handed on, and one it has is never kept here.
 */
test('the verbs handed on are exactly the ones `daoris-driver driver` has', () => {
  const source = readFileSync(driverFile('DriverChoicesCommand.cs'), 'utf8');
  const start = source.indexOf('public static IReadOnlyList<string> Verbs { get; } =');
  assert.ok(start > 0, 'DriverChoicesCommand declares no Verbs');
  const block = source.slice(start, source.indexOf('];', start));
  const verbs = [...block.matchAll(/"([a-z-]+)"/g)].map((match) => match[1]);
  assert.deepEqual([...HANDED_VERBS], verbs);
});

test('a landing\'s cycle, the workflows, a work cycle and the brainstorms policy stay the CLI\'s', () => {
  assert.equal(handed([]), true);
  assert.equal(handed(['landing', 'engine', 'merge']), true);
  for (const argv of [['landing', 'engine', 'merge', '--cycle', 'c.json'], ['landing', 'engine', 'merge', '--no-cycle'],
    ['workflow', 'list'], ['work-cycle', 'engine', 'quest', '--clear'], ['brainstorms'], ['frobnicate']]) {
    assert.equal(handed(argv), false, argv.join(' '));
  }
});

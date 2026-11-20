import { test } from 'vitest';
import assert from 'node:assert/strict';
import { GATE_BUDGET_MS, budgetLines, loggedRun, overBudget } from './timing.ts';

test('a gate over five minutes is named, and none is refused for it; the whole run is the verification rule\'s (verification.ts)', () => {
  const over = overBudget([{ name: 'cli', ms: GATE_BUDGET_MS + 1 }, { name: 'comments', ms: 3_000 }]);
  assert.deepEqual(over, { gates: ['cli'] });
  assert.deepEqual(overBudget([{ name: 'cli', ms: GATE_BUDGET_MS }]), { gates: [] });
});

test('the summary says each gate over budget with its slowest classes, so a slow test becomes a row', () => {
  const lines = budgetLines(
    [{ name: 'reached', ms: 21 * 60_000, slowest: [{ name: 'FooTests', ms: 400_000, tests: 12 }] }, { name: 'cli', ms: 1_000, slowest: [] }],
  );
  assert.match(lines[0]!, /reached took 21 min, over its 5 min budget; slowest: FooTests 6\.7 min/);
  assert.equal(lines.length, 1);
  assert.deepEqual(budgetLines([{ name: 'cli', ms: 1000, slowest: [] }]), []);
});

test('a dotnet test command gets a trx logger of its own per segment, other commands none', () => {
  const one = loggedRun('dotnet test src/A --filter Category!=Process', '/d', 'driver');
  assert.equal(one.run, 'dotnet test src/A --filter Category!=Process --logger "trx;LogFileName=/d/driver.logged-1.trx"');
  assert.deepEqual(one.files, ['/d/driver.logged-1.trx']);
  const two = loggedRun('dotnet test A --filter Category=Census && dotnet test B --filter Category=Census', '/d', 'censuses');
  assert.equal(two.files.length, 2);
  assert.match(two.run, /Category=Census --logger "trx;LogFileName=\/d\/censuses\.logged-1\.trx" && dotnet test B .*censuses\.logged-2\.trx"$/);
  assert.deepEqual(loggedRun('npm run verify', '/d', 'cli'), { run: 'npm run verify', files: [] });
  assert.deepEqual(loggedRun('dotnet test A --logger "trx;LogFileName=x"', '/d', 'g'), { run: 'dotnet test A --logger "trx;LogFileName=x"', files: [] }, 'one that logs already is left');
});

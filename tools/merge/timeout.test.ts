import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { budgetMs, overBudgetNote, timeoutState } from './timeout.ts';

test('a gate\'s budget is its declared timeoutMinutes, else the lane\'s: five minutes in a merge, ten in the full set or the CI', () => {
  assert.equal(budgetMs({ name: 'a' }, 'merge'), 5 * 60_000);
  assert.equal(budgetMs({ name: 'a' }, null), 5 * 60_000);
  assert.equal(budgetMs({ name: 'a' }, 'ci'), 10 * 60_000);
  assert.equal(budgetMs({ name: 'a' }, 'checkout'), 10 * 60_000);
  assert.equal(budgetMs({ name: 'a', timeoutMinutes: 2 }, 'ci'), 2 * 60_000);
  assert.equal(budgetMs({ name: 'a', timeoutMinutes: -1 }, 'merge'), 5 * 60_000, 'a budget that does not read is the lane\'s');
});

test('the declared gates: each timeoutMinutes carries its reason, and the full set runs no package suite, only packages-lock', () => {
  const { gates } = JSON.parse(readFileSync(new URL('../../daoris.gates.json', import.meta.url), 'utf8')) as {
    gates: { name: string; timeoutMinutes?: number; timeoutWhy?: string }[];
  };
  for (const gate of gates.filter((each) => each.timeoutMinutes !== undefined)) {
    assert.ok(Number.isInteger(gate.timeoutMinutes) && gate.timeoutMinutes! > 0, `${gate.name} declares a whole number of minutes`);
    assert.ok((gate.timeoutWhy ?? '').length > 20, `${gate.name} says why its budget is what it is`);
  }
  assert.deepEqual(gates.map((each) => each.name).filter((name) => /^(adapters|packages)-/.test(name)), ['packages-lock']);
  const over = gates.find((each) => each.name === 'tool-e2e')!;
  const budget = budgetMs(over, 'ci')!;
  assert.ok(budget > 10 * 60_000, 'a gate killed at the old budget has its measured one');
  assert.match(overBudgetNote(budget + 1000, budget, []), /^over-budget: killed after/, 'a gate over its new budget still fails');
  assert.equal(budgetMs(gates.find((each) => each.name === 'reached')!, 'merge'), 20 * 60_000, 'reached: 13.4 min p90 unbudgeted, times 1.5 (MERGENOKILL1)');
});

test('--no-timeout gives no budget and says so in the summary', () => {
  timeoutState.off = true;
  try {
    assert.equal(budgetMs({ name: 'a' }, 'merge'), null);
    assert.match(timeoutState.note()!, /--no-timeout: no gate had a budget/);
  } finally {
    timeoutState.off = false;
  }
  assert.equal(timeoutState.note(), null);
});

test('an over-budget gate says how long it ran, its budget, and the slowest classes the last record knew', () => {
  const note = overBudgetNote(300_000, 300_000, [{ name: 'FooTests', ms: 200_000, tests: 4 }]);
  assert.match(note, /over-budget: killed after 5 min, its budget is 5 min; slowest in its last record: FooTests 3\.3 min/);
  assert.match(overBudgetNote(300_000, 300_000, []), /over-budget: killed after 5 min, its budget is 5 min$/);
});

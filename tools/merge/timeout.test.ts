import { test } from 'vitest';
import assert from 'node:assert/strict';
import { budgetMs, overBudgetNote, timeoutState } from './timeout.ts';

test('a gate\'s budget is its declared timeoutMinutes, else the lane\'s: five minutes in a merge, ten in the full set or the CI', () => {
  assert.equal(budgetMs({ name: 'a' }, 'merge'), 5 * 60_000);
  assert.equal(budgetMs({ name: 'a' }, null), 5 * 60_000);
  assert.equal(budgetMs({ name: 'a' }, 'ci'), 10 * 60_000);
  assert.equal(budgetMs({ name: 'a' }, 'checkout'), 10 * 60_000);
  assert.equal(budgetMs({ name: 'a', timeoutMinutes: 2 }, 'ci'), 2 * 60_000);
  assert.equal(budgetMs({ name: 'a', timeoutMinutes: -1 }, 'merge'), 5 * 60_000, 'a budget that does not read is the lane\'s');
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

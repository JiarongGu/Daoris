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

test('the declaration carries none of the members the home keeps, and the full set runs no package suite, only packages-lock', () => {
  const declared = JSON.parse(readFileSync(new URL('../../daoris.gates.json', import.meta.url), 'utf8')) as {
    gates: Record<string, unknown>[]; tests: { sets: Record<string, unknown>[] }; sensitive: Record<string, unknown>; verifyBudget: Record<string, unknown>;
  } & Record<string, unknown>;
  for (const gate of declared.gates) {
    for (const member of ['timeoutMinutes', 'timeoutWhy', 'memoryGb', 'alone']) assert.equal(member in gate, false, `${gate['name']} keeps ${member} in the home`);
  }
  for (const set of declared.tests.sets) assert.equal('timeoutMs' in set, false, `${set['name']} keeps its timeout in the home`);
  for (const member of ['devkit', 'treeBaseline', 'localInputs']) assert.equal(member in declared, false, `${member} is the home's`);
  assert.equal('patternsFile' in declared.sensitive, false);
  assert.equal('overruns' in declared.verifyBudget, false);
  assert.deepEqual(declared.gates.map((each) => each['name'] as string).filter((name) => /^(adapters|packages)-/.test(name)), ['packages-lock']);
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

import { unitsOf } from '../time-reach.mjs';
import { testBreach } from '../time-why.mjs';
import { BUDGET_MS, checkBudget, latestRuns, readRecords } from './budget.mjs';

/**
 * The census judges a test over the budget by its time alone, as the time ratchet judges time (`judgeByReach`): records written while
 * merges and builders loaded the machine are not a finding until the test's class has run again alone and is still over.
 */

/** The tests the unit-tier `records` hold over the budget and not on `list.slowTests`, as a map from the budget's `Class.test` key to its time. */
export function overBudgetOf(records, list) {
  const over = {};
  for (const [key, { ms }] of latestRuns(records)) if (ms > BUDGET_MS && list.slowTests?.[key] === undefined) over[key] = ms;
  return over;
}

/**
 * Each unit test the newest `records` hold over the budget as a breach with its cause (`tools/time-why.mjs`): failing unless
 * `list.slowTests` holds it at no less. `source` reads a test's class's source by its key, `acts` its ledger entry.
 */
export function budgetBreaches(records, list, source, acts = () => null) {
  return [...latestRuns(records)].filter(([, { ms }]) => ms > BUDGET_MS).map(([key, { ms, gate }]) => {
    const listed = list.slowTests?.[key];
    return testBreach({
      kind: 'census', key, ms, limit: BUDGET_MS, fails: !(listed >= ms), record: gate, source: source(key), acts: acts(key), ...(listed === undefined ? {} : { note: `listed at ${listed} ms` }),
    });
  });
}

/**
 * The budget's findings with each over-budget class measured again alone first: `measure(unitNames)` runs the classes and writes newer
 * records into `dir`, which are read again. A test still over budget alone is a finding; one under it alone is not. The census is read
 * (`world()`) only when something is over, and a test it holds no unit for is judged on the records it has.
 */
export async function checkBudgetAlone({ records, list, dir, world, measure }) {
  const over = overBudgetOf(records, list);
  if (!Object.keys(over).length) return { problems: checkBudget(records, list), measured: [] };
  const units = unitsOf(over, await world());
  if (!units.length) return { problems: checkBudget(records, list), measured: [] };
  measure(units);
  return { problems: checkBudget(readRecords(dir), list), measured: units };
}

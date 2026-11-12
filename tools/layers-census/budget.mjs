import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/** The longest a unit test may take, in milliseconds. */
export const BUDGET_MS = 1000;

/**
 * The duration records a unit gate writes: one JSON file per run under `dir`, each
 * `{ "gate": "<name>", "tier": "unit", "tests": [{ "class": "...", "test": "...", "ms": 12 }] }`.
 * A folder that does not exist is no records.
 */
export function readRecords(dir) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir).filter((name) => name.endsWith('.json')).map((name) => JSON.parse(readFileSync(join(dir, name), 'utf8')));
}

/** The slowest run of each unit-tier test across `records`, by `Class.test`, with the gate that ran it. */
export function slowest(records) {
  const found = new Map();
  for (const record of records.filter((each) => each.tier === 'unit')) {
    for (const test of record.tests ?? []) {
      const key = `${test.class}.${test.test}`;
      if (!found.has(key) || found.get(key).ms < test.ms) found.set(key, { ms: test.ms, gate: record.gate });
    }
  }
  return found;
}

/**
 * A unit test over the budget fails by name unless `list.slowTests` holds it at what it took, which it may not exceed; a listed test
 * that ran within the budget leaves the list. Records of a tier that is not the unit's are not read.
 */
export function checkBudget(records, list) {
  const problems = [];
  const found = slowest(records);
  for (const [key, { ms, gate }] of found) {
    const listed = list.slowTests[key];
    if (listed === undefined && ms > BUDGET_MS) {
      problems.push(`${key} took ${ms} ms in the unit gate ${gate}, over the ${BUDGET_MS} ms budget: put what it waits on on a fake, or a virtual clock`);
    } else if (listed !== undefined && ms > listed) {
      problems.push(`${key} slowed from ${listed} to ${ms} ms: a listed slow test does not get slower`);
    } else if (listed !== undefined && ms <= BUDGET_MS) {
      problems.push(`${key} is on the slow list at ${listed} ms and ran in ${ms} ms: take it off the list (node tools/layers-census.mjs --write)`);
    }
  }
  return problems;
}

/** The tests over the budget in `records`, by name, for the list's first writing. */
export function slowTestsOf(records) {
  return Object.fromEntries([...slowest(records)].filter(([, { ms }]) => ms > BUDGET_MS).map(([key, { ms }]) => [key, ms]).sort());
}

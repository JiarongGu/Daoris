import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/** The longest a unit test may take, in milliseconds. */
export const BUDGET_MS = 1000;

const MS = (duration) => {
  const [h, m, s] = duration.split(':').map(Number);
  return Math.round(((h * 60 + m) * 60 + s) * 1000);
};

/**
 * One gate's trx as the record the budget reads: each result's test name split at the last dot before its parameters, and its
 * duration in milliseconds. A trx whose gate name says Process is the process tier's, which has no second to keep.
 */
export function parseTrx(text, gate) {
  const tests = [];
  for (const [, attributes] of text.matchAll(/<UnitTestResult\s([^>]*?)\/?>/g)) {
    const name = /\stestName="([^"]*)"/.exec(` ${attributes}`)?.[1];
    const duration = /\sduration="([^"]*)"/.exec(` ${attributes}`)?.[1];
    if (!name || !duration) continue;
    const bare = name.replace(/&quot;/g, '"').replace(/&amp;/g, '&');
    const cut = bare.indexOf('(') < 0 ? bare.length : bare.indexOf('(');
    const dot = bare.lastIndexOf('.', cut);
    tests.push({ class: bare.slice(0, dot), test: bare.slice(dot + 1), ms: MS(duration) });
  }
  return { gate, tier: /process/i.test(gate) ? 'process' : 'unit', tests };
}

/** The duration records the gates keep under `dir`: a trx per .NET gate. A folder that does not exist is no records. */
export function readRecords(dir) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir).filter((name) => name.endsWith('.trx'))
    .map((name) => parseTrx(readFileSync(join(dir, name), 'utf8'), name.replace(/(\.logged-\d+)?\.trx$/, '')));
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

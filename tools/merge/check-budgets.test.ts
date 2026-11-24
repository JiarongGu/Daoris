import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  BUDGET_FACTOR, BUDGET_FLOOR_MINUTES, GREENS_KEPT, GREENS_NEEDED, RISE_BOUND,
  budgetsFile, budgetOf, readBudgets, recordGreen, saidRise, setGate, type GateKept, type Green,
} from './check-budgets.ts';

const MIN = 60_000;
const scratch: string[] = [];
const homeOf = (): string => {
  const home = mkdtempSync(join(tmpdir(), 'check-budgets-'));
  scratch.push(home);
  return home;
};
const green = (minutes: number[]): Green[] => minutes.map((each, index) => ({ ms: each * MIN, at: `2026-11-${String(10 + index).padStart(2, '0')}T00:00:00Z` }));

test.afterAll(() => { for (const each of scratch) rmSync(each, { recursive: true, force: true }); });

test('with no home, or none recorded for the gate, the budget is the product\'s default: five minutes in a merge, ten in a full set', () => {
  assert.deepEqual(budgetOf(undefined, 'merge'), { minutes: 5, source: 'default' });
  assert.deepEqual(budgetOf(undefined, 'full'), { minutes: 10, source: 'default' });
  assert.deepEqual(budgetOf({ green: green([1, 1]) }, 'merge'), { minutes: 5, source: 'default' }, `fewer than ${GREENS_NEEDED} green times teach nothing`);
});

test('a gate\'s timeout follows its green times: the slowest ninetieth percentile of the recent ones times the factor, rounded up to a whole minute', () => {
  const greens = green([10, 10, 10, 12, 14]);
  const budget = budgetOf({ green: greens }, 'merge');
  assert.equal(budget.source, 'learned');
  assert.equal(budget.minutes, Math.ceil(14 * BUDGET_FACTOR));
  const slower = budgetOf({ green: [...greens, ...green([30])] }, 'merge');
  assert.equal(slower.minutes, Math.ceil(30 * BUDGET_FACTOR), 'a slower green time moves the timeout');
  const faster = budgetOf({ green: green([2, 2, 2, 2, 2, 2]) }, 'merge');
  assert.equal(faster.minutes, BUDGET_FLOOR_MINUTES, 'a fast gate never has less than the floor');
});

test('only the newest greens count, so a gate that got faster is let go of its old time', () => {
  const old = green(Array.from({ length: GREENS_KEPT }, () => 40));
  const now = green(Array.from({ length: GREENS_KEPT }, () => 3));
  assert.equal(budgetOf({ green: [...old, ...now] }, 'merge').minutes, BUDGET_FLOOR_MINUTES);
});

test('a pin stands over what the green times say, and carries its why', () => {
  const kept: GateKept = { green: green([10, 10, 10, 12, 14]), pin: { minutes: 33, why: 'a quiet machine killed it at 20' } };
  assert.deepEqual(budgetOf(kept, 'merge'), { minutes: 33, source: 'pin', why: 'a quiet machine killed it at 20' });
  assert.equal(budgetOf({ pin: { minutes: 0, why: 'x' }, green: green([10, 10, 10]) }, 'merge').source, 'learned', 'a pin that is no positive number pins nothing');
});

test('a rise of the learned timeout past the bound is said once: the first learning sets what is said, the next rise past the bound is told and then marked', () => {
  const home = homeOf();
  for (const each of [10, 10, 10]) recordGreen(home, 'Fixture', 'slow', each * MIN, '2026-11-01T00:00:00Z');
  const first = budgetOf(readBudgets(home).repositories['Fixture']!.gates!['slow'], 'merge');
  assert.equal(first.minutes, 15);
  assert.equal(first.rise, undefined, 'nothing was said before, so there is nothing it rose from');

  saidRise(home, 'Fixture', 'slow', first.minutes);
  for (const each of [12, 12, 12]) recordGreen(home, 'Fixture', 'slow', each * MIN, '2026-11-02T00:00:00Z');
  const small = budgetOf(readBudgets(home).repositories['Fixture']!.gates!['slow'], 'merge');
  assert.equal(small.minutes, 18);
  assert.equal(small.rise, undefined, `${18}/${15} is within the bound of ${RISE_BOUND}`);

  for (const each of [20, 20, 20]) recordGreen(home, 'Fixture', 'slow', each * MIN, '2026-11-03T00:00:00Z');
  const rose = budgetOf(readBudgets(home).repositories['Fixture']!.gates!['slow'], 'merge');
  assert.equal(rose.minutes, 30);
  assert.deepEqual(rose.rise, { from: 15, to: 30 });

  saidRise(home, 'Fixture', 'slow', rose.minutes);
  assert.equal(budgetOf(readBudgets(home).repositories['Fixture']!.gates!['slow'], 'merge').rise, undefined, 'said once, not again');
});

test('a pinned gate says no rise: the owner chose its timeout', () => {
  const kept: GateKept = { green: green([20, 20, 20]), pin: { minutes: 12, why: 'w' }, said: 5 };
  assert.equal(budgetOf(kept, 'merge').rise, undefined);
});

test('a green time is kept in the home\'s file, the newest last, no more than the kept count', () => {
  const home = homeOf();
  for (let at = 0; at < GREENS_KEPT + 5; at += 1) recordGreen(home, 'Fixture', 'g', (at + 1) * 1000, `2026-11-01T00:00:${String(at).padStart(2, '0')}Z`);
  const kept = readBudgets(home).repositories['Fixture']!.gates!['g']!.green!;
  assert.equal(kept.length, GREENS_KEPT);
  assert.equal(kept.at(-1)!.ms, (GREENS_KEPT + 5) * 1000);
  assert.equal(kept[0]!.ms, 6 * 1000);
  assert.equal(JSON.parse(readFileSync(budgetsFile(home), 'utf8')).schema, 1);
});

test('the door\'s values (memory, alone, a pin) are kept beside the greens and neither write loses the other', () => {
  const home = homeOf();
  recordGreen(home, 'Fixture', 'g', MIN, '2026-11-01T00:00:00Z');
  setGate(home, 'Fixture', 'g', { memoryGb: 6, alone: true, pin: { minutes: 20, why: 'why' } });
  recordGreen(home, 'Fixture', 'g', 2 * MIN, '2026-11-02T00:00:00Z');
  const kept = readBudgets(home).repositories['Fixture']!.gates!['g']!;
  assert.equal(kept.memoryGb, 6);
  assert.equal(kept.alone, true);
  assert.deepEqual(kept.pin, { minutes: 20, why: 'why' });
  assert.equal(kept.green!.length, 2);
  setGate(home, 'Fixture', 'g', { pin: null });
  assert.equal(readBudgets(home).repositories['Fixture']!.gates!['g']!.pin, undefined, 'a pin set to null is taken away');
});

test('no home reads as no record and writes nothing; a file that does not read is named, not read as empty', () => {
  assert.deepEqual(readBudgets(null).repositories, {});
  recordGreen(null, 'Fixture', 'g', MIN, '2026-11-01T00:00:00Z');
  const home = homeOf();
  mkdirSync(join(home, 'data'), { recursive: true });
  writeFileSync(budgetsFile(home), '{ not json');
  assert.throws(() => readBudgets(home), /check-budgets\.json/);
  assert.ok(existsSync(budgetsFile(home)));
});

test('the shared cases: the derivation answers each as the driver\'s keeper does', () => {
  const { constants, cases } = JSON.parse(readFileSync(new URL('./fixtures/check-budget-cases.json', import.meta.url), 'utf8')) as {
    constants: Record<string, number>;
    cases: { name: string; lane: string; greens: number[]; pin?: GateKept['pin']; said?: number; expect: Record<string, unknown> }[];
  };
  assert.deepEqual(constants, { factor: BUDGET_FACTOR, floorMinutes: BUDGET_FLOOR_MINUTES, greensNeeded: GREENS_NEEDED, greensKept: GREENS_KEPT, riseBound: RISE_BOUND, mergeDefault: 5, fullDefault: 10 });
  for (const each of cases) {
    const kept: GateKept = { green: green(each.greens), ...(each.pin ? { pin: each.pin } : {}), ...(each.said !== undefined ? { said: each.said } : {}) };
    assert.deepEqual(budgetOf(kept, each.lane), each.expect, each.name);
  }
});

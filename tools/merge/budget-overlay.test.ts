import { test } from 'vitest';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { recordGreen, setGate } from './check-budgets.ts';
import { budgetSourceOf, learnGreen, overlay, riseLines } from './budget-overlay.ts';
import { declaredIn } from './plan.ts';
import { scratchRoot } from './fixture-home.ts';

const MIN = 60_000;
const scratch: string[] = [];
const homeOf = (): string => {
  const home = mkdtempSync(join(scratchRoot(), 'budget-overlay-'));
  scratch.push(home);
  return home;
};
test.afterAll(() => { for (const each of scratch) rmSync(each, { recursive: true, force: true }); });

const learn = (home: string, gate: string, minutes: number[]): void => { for (const each of minutes) recordGreen(home, 'Fixture', gate, each * MIN, '2026-11-01T00:00:00Z'); };

test('a gate\'s timeout follows its green times in the plan, over the member an older declaration still holds', () => {
  const home = homeOf();
  learn(home, 'slow', [10, 10, 10, 12]);
  const [gate] = overlay([{ name: 'slow', run: 'x', timeoutMinutes: 3 }], { home, repository: 'Fixture' });
  assert.equal(gate!.timeoutMinutes, 18);
  assert.equal(gate!.budget?.source, 'learned');
});

test('a pin stands, and the home\'s memory and load settings are the gate\'s over the declaration\'s', () => {
  const home = homeOf();
  learn(home, 'slow', [10, 10, 10]);
  setGate(home, 'Fixture', 'slow', { pin: { minutes: 40, why: 'quiet machines kill it' }, memoryGb: 6, alone: true });
  const [gate] = overlay([{ name: 'slow', run: 'x', memoryGb: 2 }], { home, repository: 'Fixture' });
  assert.equal(gate!.timeoutMinutes, 40);
  assert.equal(gate!.budget?.source, 'pin');
  assert.equal(gate!.memoryGb, 6);
  assert.equal(gate!.alone, true);
});

test('with no home a gate has what its declaration says, and none where it says none, so the lane\'s default applies', () => {
  const gates = [{ name: 'a', run: 'x' }, { name: 'b', run: 'x', timeoutMinutes: 7, memoryGb: 3, alone: true }];
  const [a, b] = overlay(gates, { home: null, repository: 'Fixture' });
  assert.equal(a!.timeoutMinutes, undefined);
  assert.equal(a!.budget, undefined);
  assert.equal(b!.timeoutMinutes, 7);
  assert.equal(b!.memoryGb, 3);
  assert.equal(b!.alone, true);
  assert.deepEqual(overlay(gates, null), gates);
});

test('a slower gate\'s rise is said once, in a line naming the gate, and marked so the next read says nothing', () => {
  const home = homeOf();
  const source = { home, repository: 'Fixture' };
  learn(home, 'slow', [10, 10, 10]);
  assert.deepEqual(riseLines(overlay([{ name: 'slow', run: 'x' }], source), source), []);
  learn(home, 'slow', [20, 20, 20]);
  const lines = riseLines(overlay([{ name: 'slow', run: 'x' }], source), source);
  assert.equal(lines.length, 1);
  assert.match(lines[0]!, /slow.*15.*30 min/);
  assert.deepEqual(riseLines(overlay([{ name: 'slow', run: 'x' }], source), source), []);
});

test('a gate that ran whole and green learns its time; a failed, replayed or partial run teaches nothing', () => {
  const home = homeOf();
  const source = { home, repository: 'Fixture' };
  for (const result of [{ verdict: 'FAIL', ms: MIN }, { verdict: 'PASS', ms: MIN, replayed: true }, { verdict: 'PASS', ms: 0 }]) learnGreen(source, 'g', result, true, '2026-11-01T00:00:00Z');
  learnGreen(source, 'g', { verdict: 'PASS', ms: MIN }, false, '2026-11-01T00:00:00Z');
  assert.equal(overlay([{ name: 'g', run: 'x' }], source)[0]!.budget, undefined);
  for (let at = 0; at < 3; at += 1) learnGreen(source, 'g', { verdict: 'PASS', ms: 4 * MIN }, true, '2026-11-01T00:00:00Z');
  assert.equal(overlay([{ name: 'g', run: 'x' }], source)[0]!.timeoutMinutes, 6);
});

test('the declaration read hands the gates the home\'s values', () => {
  const home = homeOf();
  learn(home, 'a', [10, 10, 10]);
  const gates = declaredIn(JSON.stringify({ gates: [{ name: 'a', run: 'x' }] }), 'test', { home, repository: 'Fixture' });
  assert.equal(gates[0]!.timeoutMinutes, 15);
  assert.equal(declaredIn(JSON.stringify({ gates: [{ name: 'a', run: 'x' }] }), 'test')[0]!.timeoutMinutes, undefined);
});

test('the repository is named by the folder of its main checkout, from a linked tree too, and a folder inside another repository by itself', () => {
  const base = mkdtempSync(join(scratchRoot(), 'budget-name-'));
  scratch.push(base);
  const main = join(base, 'Named');
  mkdirSync(main);
  const git = (cwd: string, ...args: string[]) => spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true });
  git(main, 'init', '-q');
  git(main, '-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '--no-verify', '--allow-empty', '-q', '-m', 'x');
  const linked = join(base, 'tree-one');
  git(main, 'worktree', 'add', '-q', '--detach', linked);
  mkdirSync(join(main, 'inside'));
  assert.equal(budgetSourceOf(main, {}).repository, 'Named');
  assert.equal(budgetSourceOf(linked, {}).repository, 'Named');
  assert.equal(budgetSourceOf(join(main, 'inside'), {}).repository, 'inside');
  assert.equal(budgetSourceOf(main, { DAORIS_HOME: 'h' }).home, 'h');
  assert.equal(budgetSourceOf(main, {}).home, null);
}, 60_000);

import { test } from 'vitest';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { PassedClasses, unitId } from './class-reuse.ts';
import { classify, mainStandsRed, outcomeLine, parseOutcome } from './reached-classify.ts';
import { runPlan, type Exec } from './reached-run.ts';
import type { ReachedPlan, ReachedUnit } from './reached.ts';
import { vitestFailures } from './file-passes.ts';

const unit = (name: string): ReachedUnit => ({ suite: 'tool-tests', kind: 'node', unit: `src/Daoris.Cli/tool-test/${name}.test.ts`, name, file: `src/Daoris.Cli/tool-test/${name}.test.ts` });
const planOf = (...names: string[]): ReachedPlan => ({ groups: [{ runner: 'node', suite: 'tool-tests', units: names.map(unit) }], left: {}, reached: names.length, replayed: {} });
const keysOf = (...names: string[]): Map<string, string> => new Map(names.map((name) => [unitId(unit(name)), `key-${name}`]));

/** A run whose command exits with the next of `codes`, each call's argument list kept. */
const exits = (codes: number[]): { exec: Exec; calls: string[][] } => {
  const calls: string[][] = [];
  return { calls, exec: (_command, args) => { calls.push(args); return codes.shift() ?? 0; } };
};

const withStore = (body: (store: PassedClasses, root: string) => void): void => {
  const { dir, remove } = fixtureFolder('reached-run');
  try {
    body(new PassedClasses(join(dir, 'store.json')), dir);
  } finally {
    remove();
  }
};

test('a clean run records each class it ran and says how many ran and were replayed', () => withStore((store, root) => {
  const run = exits([0]);
  const lines: string[] = [];
  const outcome = runPlan({ root, plan: planOf('a', 'b'), keys: keysOf('a', 'b'), store, exec: run.exec, say: (line) => lines.push(line), mainRedOf: () => false, replayed: 7 });
  assert.deepEqual(outcome, { ran: 2, replayed: 7, flaked: [], mainRed: [], red: [] });
  assert.equal(store.has(unitId(unit('a')), 'key-a'), true);
  assert.deepEqual(parseOutcome(lines.join('\n')), outcome);
}));

test('a class that fails and passes alone is a flake: said, counted, and not recorded as a pass', () => withStore((store, root) => {
  const run = exits([1, 0]);
  const lines: string[] = [];
  const outcome = runPlan({ root, plan: planOf('a'), keys: keysOf('a'), store, exec: run.exec, say: (line) => lines.push(line), mainRedOf: () => false });
  assert.equal(run.calls.length, 2, 'the failed class is run once more, alone');
  assert.deepEqual(outcome.flaked, [unitId(unit('a'))]);
  assert.equal(outcome.red.length, 0);
  assert.equal(store.has(unitId(unit('a')), 'key-a'), false);
  assert.ok(lines.some((line) => line.includes('FLAKE')));
}));

test('a class that fails again alone is main\'s red where main stands it red, and a new red otherwise', () => withStore((store, root) => {
  const outcome = runPlan({ root, plan: planOf('a', 'b'), keys: keysOf('a', 'b'), store, exec: exits([1, 1]).exec, say: () => undefined, mainRedOf: (each) => each.unit.includes('/a.') });
  assert.deepEqual(outcome.mainRed, [unitId(unit('a'))]);
  assert.deepEqual(outcome.red, [unitId(unit('b'))]);
}));

test('the classification and the outcome line read back, and main stands red only what its records say', () => {
  const failed = [{ suite: 's', unit: 'u1' }, { suite: 's', unit: 'u2' }, { suite: 's', unit: 'u3' }];
  assert.deepEqual(classify({ failed, passedAlone: new Set(['s|u1']), mainRedOf: (each) => each.unit === 'u2' }), { flaked: ['s|u1'], mainRed: ['s|u2'], red: ['s|u3'] });
  assert.equal(parseOutcome('no line here'), null);
  assert.deepEqual(parseOutcome(`x\n${outcomeLine({ ran: 1, replayed: 2, flaked: [], mainRed: [], red: ['s|u'] })}\n`), { ran: 1, replayed: 2, flaked: [], mainRed: [], red: ['s|u'] });
  const findings = [{ state: 'red', unit: 'A.B.OneTests', test: 'A.B.OneTests.Does' }, { state: 'cleared', unit: 'A.B.TwoTests' }];
  assert.equal(mainStandsRed({ suite: 'driver', unit: 'A.B.OneTests' }, findings, {}), true);
  assert.equal(mainStandsRed({ suite: 'driver', unit: 'A.B.TwoTests' }, findings, {}), false);
  assert.equal(mainStandsRed({ suite: 'driver', unit: 'A.B.ThreeTests' }, findings, { driver: { 'A.B.ThreeTests': [{ verdict: 'PASS' }, { verdict: 'FAIL' }] } }), true);
});

test('a vitest report names each failed test with its file, and a file that failed before any test', () => {
  const json = JSON.stringify({ testResults: [
    { name: '/r/a.test.ts', status: 'failed', assertionResults: [{ status: 'passed', fullName: 'ok' }, { status: 'failed', fullName: 'it breaks' }] },
    { name: '/r/b.test.ts', status: 'failed', message: 'Cannot find module\nmore', assertionResults: [] },
    { name: '/r/c.test.ts', status: 'passed', assertionResults: [] },
  ] });
  assert.deepEqual(vitestFailures(json), [{ file: '/r/a.test.ts', test: 'it breaks' }, { file: '/r/b.test.ts', test: 'Cannot find module' }]);
});

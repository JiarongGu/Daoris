import { test } from 'vitest';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { PassedClasses, unitId } from './class-reuse.ts';
import { classify, mainStandsRed, outcomeLine, parseOutcome } from './reached-classify.ts';
import { runPlan, type Exec } from './reached-run.ts';
import type { ReachedPlan, ReachedUnit } from './reached.ts';
import { vitestFailures } from './file-passes.ts';
import { StepClock } from './reached-steps.ts';

const unit = (name: string): ReachedUnit => ({ suite: 'tool-tests', kind: 'node', unit: `src/Daoris.Cli/tool-test/${name}.test.ts`, name, file: `src/Daoris.Cli/tool-test/${name}.test.ts` });
const planOf = (...names: string[]): ReachedPlan => ({ groups: [{ runner: 'node', suite: 'tool-tests', units: names.map(unit) }], left: {}, reached: names.length, replayed: {} });
const keysOf = (...names: string[]): Map<string, string> => new Map(names.map((name) => [unitId(unit(name)), `key-${name}`]));

/** A run whose command exits with the next of `codes`, each call's argument list kept. */
const exits = (codes: number[]): { exec: Exec; calls: string[][] } => {
  const calls: string[][] = [];
  return { calls, exec: (_command, args) => { calls.push(args); return codes.shift() ?? 0; } };
};

const withStore = async (body: (store: PassedClasses, root: string) => Promise<void>): Promise<void> => {
  const { dir, remove } = fixtureFolder('reached-run');
  try {
    await body(new PassedClasses(join(dir, 'store.json')), dir);
  } finally {
    remove();
  }
};

test('a clean run records each class it ran and says how many ran and were replayed', () => withStore(async (store, root) => {
  const run = exits([0]);
  const lines: string[] = [];
  const outcome = await runPlan({ root, plan: planOf('a', 'b'), keys: keysOf('a', 'b'), store, exec: run.exec, say: (line) => lines.push(line), mainRedOf: () => false, replayed: 7, deferred: ['driver-process|C'] });
  assert.deepEqual(outcome, { ran: 2, replayed: 7, flaked: [], mainRed: [], red: [], deferred: ['driver-process|C'] });
  assert.equal(store.has(unitId(unit('a')), 'key-a'), true);
  assert.deepEqual(parseOutcome(lines.join('\n')), outcome);
}));

test('a group\'s passes are kept in the store as the group ends, so a run ended before its last group (a check stood down, stopped or bound) replays them next time', () => withStore(async (store, root) => {
  const file = join(root, 'store.json');
  const plan: ReachedPlan = { groups: [{ runner: 'node', suite: 'tool-tests', units: [unit('a')] }, { runner: 'node', suite: 'tool-tests', units: [unit('b')] }], left: {}, reached: 2, replayed: {} };
  let seen = false;
  const exec: Exec = async (_command, args) => {
    if (!args.some((arg) => arg.includes('b.test.ts'))) return 0;
    for (let tries = 0; tries < 50 && !seen; tries += 1) {
      seen = new PassedClasses(file).has(unitId(unit('a')), 'key-a');
      if (!seen) await new Promise((done) => setTimeout(done, 20));
    }
    return 0;
  };
  await runPlan({ root, plan, keys: keysOf('a', 'b'), store, exec, say: () => undefined, mainRedOf: () => false });
  assert.equal(seen, true, 'the first group\'s pass was on disk while the second group ran');
}));

test('each test project is built once before its classes, which run on that build; a project whose build failed has its classes red unrun; the outcome says what stands built and each step', () => withStore(async (store, root) => {
  const dotnetUnit = (project: string, name: string): ReachedUnit => ({ suite: 'driver', kind: 'dotnet', unit: `D.${name}`, name, file: `${project}/${name}.cs`, project });
  const plan: ReachedPlan = {
    groups: [
      { runner: 'dotnet', suite: 'driver', project: 'src/D.Tests', settings: null, units: [dotnetUnit('src/D.Tests', 'ATests'), dotnetUnit('src/D.Tests', 'BTests')] },
      { runner: 'dotnet', suite: 'modules', project: 'src/M.Tests', settings: null, units: [dotnetUnit('src/M.Tests', 'MTests')] },
    ],
    left: {}, reached: 3, replayed: {},
  };
  const built: string[][] = [];
  const run = exits([0]);
  const lines: string[] = [];
  const clock = new StepClock((line) => lines.push(line));
  const outcome = await runPlan({
    root, plan, keys: new Map(), store, exec: run.exec, say: (line) => lines.push(line), mainRedOf: () => false, clock,
    build: async (projects) => { built.push(projects); return { ready: ['src/D.Tests'], failed: new Map([['src/M.Tests', 'its build failed (exit 1)']]) }; },
  });
  assert.deepEqual(built, [['src/D.Tests', 'src/M.Tests']], 'every project built once, before any class runs');
  assert.equal(run.calls.length, 1, 'the project whose build failed runs nothing');
  assert.deepEqual(run.calls[0]!.slice(0, 2), ['test', 'src/D.Tests']);
  assert.ok(run.calls[0]!.includes('--no-build') && run.calls[0]!.includes('--no-restore'));
  assert.deepEqual(outcome.red, ['driver|D.MTests']);
  assert.deepEqual(outcome.built, ['src/D.Tests']);
  assert.deepEqual(outcome.steps?.map((step) => step.step), ['run driver']);
  assert.ok(lines.some((line) => /modules: 1 class\(es\) not run: src\/M\.Tests its build failed/.test(line)));
  assert.deepEqual(parseOutcome(lines.join('\n'))?.built, ['src/D.Tests']);
}));

test('a light suite\'s groups run beside the build and the .NET runs, which need nothing of them; a heavy suite\'s wait for the build; the outcome keeps the plan\'s order', () => withStore(async (store, root) => {
  const dotnetUnit = (name: string): ReachedUnit => ({ suite: 'driver', kind: 'dotnet', unit: `D.${name}`, name, file: `src/D.Tests/${name}.cs`, project: 'src/D.Tests' });
  const nodeUnit = (suite: string, name: string): ReachedUnit => ({ suite, kind: 'node', unit: `tools/${name}.test.ts`, name, file: `tools/${name}.test.ts` });
  const plan: ReachedPlan = {
    groups: [
      { runner: 'dotnet', suite: 'driver', project: 'src/D.Tests', settings: null, units: [dotnetUnit('ATests')] },
      { runner: 'node', suite: 'tool-e2e', units: [nodeUnit('tool-e2e', 'e')] },
      { runner: 'node', suite: 'tool-tests', units: [nodeUnit('tool-tests', 't')] },
      { runner: 'node', suite: 'cli', units: [nodeUnit('cli', 'c')] },
      { runner: 'node', suite: 'cli', cwd: '.', units: [nodeUnit('cli', 'd')] },
      { runner: 'node', suite: 'cli', cwd: '.', units: [nodeUnit('cli', 'f')] },
    ],
    left: {}, reached: 6, replayed: {},
  };
  const order: string[] = [];
  let finishBuild = (): void => undefined;
  const building = new Promise<void>((done) => { finishBuild = done; });
  const exec: Exec = async (command, args) => {
    const what = command === 'dotnet' ? 'driver' : args.find((arg) => arg.includes('.test.ts'))!.replace(/^.*\/(\w+)\.test\.ts$/, '$1');
    order.push(`start ${what}`);
    if (what === 't') finishBuild();
    await Promise.resolve();
    order.push(`end ${what}`);
    return what === 'driver' ? 1 : 0;
  };
  const outcome = await runPlan({
    root, plan, keys: new Map(), store, exec, say: () => undefined, mainRedOf: () => false,
    build: async () => { order.push('build'); await building; order.push('built'); return { ready: ['src/D.Tests'], failed: new Map() }; },
  });
  assert.ok(order.indexOf('start t') < order.indexOf('built'), `the light group ran while the build was under way: ${order.join(', ')}`);
  assert.ok(order.indexOf('start e') > order.indexOf('end driver'), `the end-to-end group waited for the .NET runs: ${order.join(', ')}`);
  assert.ok(order.indexOf('start c') < order.indexOf('end t'), `each light suite runs beside the others: ${order.join(', ')}`);
  assert.ok(order.indexOf('start d') < order.indexOf('end c'), `a suite's command of another folder runs beside: ${order.join(', ')}`);
  assert.ok(order.indexOf('start f') > order.indexOf('end d'), `the chunks of one command run in turn: ${order.join(', ')}`);
  assert.deepEqual(outcome.red, ['driver|D.ATests']);
  assert.equal(outcome.ran, 6);
}));

test('a class that fails and passes alone is a flake: said, counted, and not recorded as a pass', () => withStore(async (store, root) => {
  const run = exits([1, 0]);
  const lines: string[] = [];
  const outcome = await runPlan({ root, plan: planOf('a'), keys: keysOf('a'), store, exec: run.exec, say: (line) => lines.push(line), mainRedOf: () => false });
  assert.equal(run.calls.length, 2, 'the failed class is run once more, alone');
  assert.deepEqual(outcome.flaked, [unitId(unit('a'))]);
  assert.equal(outcome.red.length, 0);
  assert.equal(store.has(unitId(unit('a')), 'key-a'), false);
  assert.ok(lines.some((line) => line.includes('FLAKE')));
}));

test('a class that fails again alone is main\'s red where main stands it red, and a new red otherwise', () => withStore(async (store, root) => {
  const outcome = await runPlan({ root, plan: planOf('a', 'b'), keys: keysOf('a', 'b'), store, exec: exits([1, 1]).exec, say: () => undefined, mainRedOf: (each) => each.unit.includes('/a.') });
  assert.deepEqual(outcome.mainRed, [unitId(unit('a'))]);
  assert.deepEqual(outcome.red, [unitId(unit('b'))]);
}));

test('the classification and the outcome line read back, and main stands red only what its records say', () => {
  const failed = [{ suite: 's', unit: 'u1' }, { suite: 's', unit: 'u2' }, { suite: 's', unit: 'u3' }];
  assert.deepEqual(classify({ failed, passedAlone: new Set(['s|u1']), mainRedOf: (each) => each.unit === 'u2' }), { flaked: ['s|u1'], mainRed: ['s|u2'], red: ['s|u3'] });
  assert.equal(parseOutcome('no line here'), null);
  assert.deepEqual(parseOutcome(`x\n${outcomeLine({ ran: 1, replayed: 2, flaked: [], mainRed: [], red: ['s|u'], deferred: [] })}\n`), { ran: 1, replayed: 2, flaked: [], mainRed: [], red: ['s|u'], deferred: [] });
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

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { tool, repoRoot, repoPlan, rootScripts, type Gate } from '../support/merge-branch.ts';

// The census of test units reads this file's strings as the plan's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the tests hold the covered gate and the pre-step's gates to the repository's own declared gates

interface Steps {
  censusCovers: (gates: Gate[]) => Map<string, string[]>;
  coverOutcome: (covers: Map<string, string[]>, results: { gate: { name: string }; verdict: string }[]) => { passed: string[]; unmet: { name: string; by: string[] }[] };
  contractsGates: (gates: Gate[], scripts: Record<string, string>, webScripts: Record<string, string>) => string[];
}

const steps = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam the tool's other tests use
  '../../../../tools/merge-branch/ci-steps.mjs') as Steps;

const gate = (name: string, run: string): Gate => ({ name, run, kind: 'suite' });
const DRIVER = 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process';
const MODULES = 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests --filter Category!=Process';
const CENSUSES = 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category=Census && dotnet test src/Daoris.Desktop/Daoris.Desktop.Modules.Tests --filter Category=Census';

test('a gate that runs only the census classes is covered where every project it names has its fast half in the run', () => {
  const run = [gate('driver', DRIVER), gate('modules', MODULES), gate('censuses', CENSUSES)];
  assert.deepEqual([...steps.censusCovers(run)], [['censuses', ['driver', 'modules']]]);
});

test('a census gate whose fast half is not run with it is run, and so is one that names more than the census', () => {
  assert.equal(steps.censusCovers([gate('driver', DRIVER), gate('censuses', CENSUSES)]).size, 0, 'the modules half is skipped');
  assert.equal(steps.censusCovers([gate('modules', MODULES), gate('censuses', CENSUSES)]).size, 0, 'the driver half is skipped');
  const process = gate('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  assert.equal(steps.censusCovers([process, gate('modules', MODULES), gate('censuses', CENSUSES)]).size, 0, 'a Process half runs no census class');
  const more = gate('censuses', `${CENSUSES} && node tools/test-map.mjs --check`);
  assert.equal(steps.censusCovers([gate('driver', DRIVER), gate('modules', MODULES), more]).size, 0, 'a command with another step is no census gate');
  assert.equal(steps.censusCovers([gate('driver', DRIVER), gate('modules', MODULES), gate('other', 'dotnet test src/Daoris.Service')]).size, 0);
});

test("a covered gate passes with its halves' passes and stays unmet, naming the half, when one did not pass", () => {
  const covers = new Map([['censuses', ['driver', 'modules']]]);
  const result = (name: string, verdict: string) => ({ gate: { name }, verdict });
  assert.deepEqual(steps.coverOutcome(covers, [result('driver', 'PASS'), result('modules', 'PASS')]), { passed: ['censuses'], unmet: [] });
  assert.deepEqual(steps.coverOutcome(covers, [result('driver', 'PASS'), result('modules', 'FAIL')]), { passed: [], unmet: [{ name: 'censuses', by: ['modules'] }] });
  assert.deepEqual(steps.coverOutcome(covers, [result('driver', 'PASS')]), { passed: [], unmet: [{ name: 'censuses', by: ['modules'] }] }, 'a half that never ran has not passed');
});

test("the repository's own plan: the census gate is covered by the driver's and the modules' fast halves", () => {
  const covers = steps.censusCovers(repoPlan);
  assert.deepEqual([...covers], [['censuses', ['driver', 'modules']]]);
});

test("the web gates that run the page's pre-scripts are the ones the contracts step serves", () => {
  const web = JSON.parse(readFileSync(join(repoRoot, 'src', 'Daoris.Web', 'package.json'), 'utf8')).scripts as Record<string, string>;
  const served = steps.contractsGates(repoPlan, rootScripts(), web);
  assert.deepEqual(served.sort(), ['web', 'web-rules', 'web-types', 'web-unit']);
  assert.deepEqual(steps.contractsGates([gate('x', 'npm --prefix p run lint')], {}, { lint: 'eslint', prelint: 'node ../../tools/contracts.mjs --if-stale' }), ['x']);
  assert.deepEqual(steps.contractsGates([gate('x', 'npm --prefix p run lint')], {}, { lint: 'eslint' }), []);
  assert.deepEqual(steps.contractsGates([gate('x', 'npm run a')], { a: 'npm run b', b: 'npm --prefix p run lint' }, { prelint: 'node ../../tools/contracts.mjs --if-stale' }), ['x'], 'read through the root scripts');
});

test('a gate the contracts step serves starts once the step has ended, while the others run beside it', async () => {
  const events: string[] = [];
  let release!: () => void;
  const ready = new Promise<void>((resolve) => { release = resolve; });
  const gates = [gate('web-a', 'x'), gate('web-b', 'y'), gate('other', 'z')];
  const run = async (_root: string, each: Gate) => {
    events.push(`start ${each.name}`);
    if (each.name === 'other') release();
    await new Promise((resolve) => setTimeout(resolve, 5));
    events.push(`end ${each.name}`);
    return { gate: each, code: 0, log: `${each.name}.log`, verdict: 'PASS', note: '', ms: 5 };
  };
  const results = await tool.runGates('root', gates, 'logs', { run, print: () => {}, keepGoing: true, waitFor: { gates: ['web-a', 'web-b'], ready } });
  assert.deepEqual(results.map((each) => each.verdict), ['PASS', 'PASS', 'PASS']);
  assert.equal(events[0], 'start other', `the gate the step does not serve starts first: ${events.join(', ')}`);
  assert.ok(events.indexOf('start web-a') > events.indexOf('start other'), events.join(', '));
  assert.ok(events.indexOf('start web-b') > events.indexOf('start other'), events.join(', '));
});

test('a step that has ended already holds nothing back', async () => {
  const started: string[] = [];
  const run = async (_root: string, each: Gate) => {
    started.push(each.name);
    return { gate: each, code: 0, log: 'l', verdict: 'PASS', note: '', ms: 1 };
  };
  await tool.runGates('root', [gate('web-a', 'x')], 'logs', { run, print: () => {}, waitFor: { gates: ['web-a'], ready: Promise.resolve() } });
  assert.deepEqual(started, ['web-a']);
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { tool, repoRoot, repoPlan, rootScripts, type Gate } from '../support/merge-branch.ts';

// The census of test units reads this file's strings as the plan's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the tests hold the covered gate and the pre-step's gates to the repository's own declared gates

interface Steps {
  contractsGates: (gates: Gate[], scripts: Record<string, string>, webScripts: Record<string, string>) => string[];
}

const steps = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam the tool's other tests use
  '../../../../tools/merge-branch/ci-steps.mjs') as Steps;

const gate = (name: string, run: string): Gate => ({ name, run, kind: 'suite' });
test("the repository's own plan: each desktop fast half leaves the census classes to the censuses gate, so each runs once a run", () => {
  const run = (name: string) => repoPlan.find((each) => each.name === name)?.run ?? '';
  for (const half of ['driver', 'modules']) assert.match(run(half), /--filter "Category!=Process&Category!=Census"/, half);
  assert.match(run('censuses'), /--filter Category=Census\b/);
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

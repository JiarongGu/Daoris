import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { privateDigest } from '../private-gate-files.mjs';
import { BuildLedger } from './build-once.ts';
import { runOne, verdictExtras, type FlowDeps } from './flow.ts';
import { LockInstalls } from './lock-install.ts';
import type { StoredVerdict } from './verdict-store.ts';
import { PassedClasses, classKey, unitId, type KeyContext, type Unit } from './class-reuse.ts';
import { fixtureFolder } from './fixture-home.ts';
import { classPass, planClasses, recordPasses, replayLine } from './full-replay.ts';
import { reuseContext } from './reached-reuse.ts';

const a: Unit = { suite: 'driver-process-git', unit: 'G.Tests.OneTests' };
const b: Unit = { suite: 'driver-process-git', unit: 'G.Tests.TwoTests' };
const reads: Record<string, string[]> = { [a.unit]: ['src/Git.cs'], [b.unit]: ['src/Other.cs'] };
const context = (text: Record<string, string> = {}, over: Partial<KeyContext> = {}): KeyContext => ({
  candidates: () => ['src/Git.cs', 'src/Other.cs'],
  reaches: (unit, path) => (reads[unit.unit] ?? []).includes(path),
  digestOf: (path) => text[path] ?? path,
  declaration: () => 'dotnet test --settings S|settings-1',
  toolchain: 'node24+10',
  ...over,
});

const trx = (rows: Array<[string, string, 'Passed' | 'Failed']>): string => `<TestRun><TestDefinitions>${rows.map(([id, cls]) => `<UnitTest name="m" id="${id}"><TestMethod className="${cls}" name="m"/></UnitTest>`).join('')}</TestDefinitions>`
  + `<Results>${rows.map(([id, , outcome]) => `<UnitTestResult testId="${id}" outcome="${outcome}"/>`).join('')}</Results></TestRun>`;

function seeded() {
  const { dir, remove } = fixtureFolder('full-replay');
  const store = new PassedClasses(join(dir, 'store.json'));
  const { keys } = planClasses([a, b], context(), store);
  for (const unit of [a, b]) store.add(unitId(unit), keys.get(unitId(unit)) as string, '2026-11-12T10:00:00Z', 'abc1234');
  return { store, remove };
}

test('with every pass standing, no class runs and the plan says which run it replays from', () => {
  const { store, remove } = seeded();
  try {
    const plan = planClasses([a, b], context(), store);
    assert.equal(plan.run.length, 0);
    assert.equal(plan.replayed.length, 2);
    assert.match(replayLine('driver-process-git', plan), /2 of 2 classes replayed.*abc1234.*2026-11-12T10:00:00Z/);
  } finally {
    remove();
  }
});

test('a change to a file one class reads runs that class and replays the other', () => {
  const { store, remove } = seeded();
  try {
    const plan = planClasses([a, b], context({ 'src/Git.cs': 'changed' }), store);
    assert.deepEqual(plan.run, [a]);
    assert.deepEqual(plan.replayed, [b]);
  } finally {
    remove();
  }
});

test('the command, the run settings and the toolchain are in the key: a pass under another never replays', () => {
  const { store, remove } = seeded();
  try {
    for (const over of [{ declaration: () => 'dotnet test --settings S|settings-2' }, { toolchain: 'node25+10' }, { declaration: () => 'dotnet test --settings T|settings-1' }]) {
      assert.equal(planClasses([a, b], context({}, over), store).replayed.length, 0);
    }
    assert.notEqual(classKey(a, context()), classKey(a, context({}, { declaration: () => 'x' })));
  } finally {
    remove();
  }
});

test('only a class the report shows passing is recorded, and a flake records none', () => {
  const { dir, remove } = fixtureFolder('full-replay');
  try {
    const store = new PassedClasses(join(dir, 'store.json'));
    const plan = planClasses([a, b], context(), store);
    const report = trx([['1', a.unit, 'Passed'], ['2', b.unit, 'Failed']]);
    assert.equal(recordPasses({ units: [a, b], keys: plan.keys, trx: report, verdict: 'FLAKE', store, at: 'now', run: 'r' }), 0);
    assert.equal(recordPasses({ units: [a, b], keys: plan.keys, trx: report, verdict: 'FAIL', store, at: 'now', run: 'r' }), 1);
    const after = planClasses([a, b], context(), store);
    assert.deepEqual(after.replayed, [a]);
    assert.deepEqual(after.run, [b]);
  } finally {
    remove();
  }
});

test('a class a run did not report stays unrecorded, so one the census never saw cannot be replayed', () => {
  assert.deepEqual([...classPass(trx([['1', a.unit, 'Passed']]))], [a.unit]);
});

test('a change to a private file the tool copies into the tree changes its digest, and a missing one is told from an empty one', () => {
  const { dir, remove } = fixtureFolder('full-replay-private');
  try {
    mkdirSync(join(dir, 'local'), { recursive: true });
    const missing = privateDigest(dir);
    writeFileSync(join(dir, 'local', 'sensitive-patterns.txt'), '');
    const empty = privateDigest(dir);
    writeFileSync(join(dir, 'local', 'sensitive-patterns.txt'), 'bad');
    const bad = privateDigest(dir);
    assert.equal(new Set([missing, empty, bad]).size, 3);
    assert.equal(privateDigest(dir), bad);
  } finally {
    remove();
  }
});

test('a gate whose private files changed is run again, not replayed', async () => {
  const { dir, remove } = fixtureFolder('full-replay-flow');
  try {
    mkdirSync(join(dir, 'logs'));
    let digest = 'one';
    let ran = 0;
    const stored: StoredVerdict[] = [];
    const deps = {
      runGate: async (_w: string, each: never) => { ran += 1; return { gate: each, code: 0, verdict: 'PASS', note: '', ms: 1, log: join(dir, 'logs', 'g.log') }; },
      reaching: (_g: string, paths: string[]) => paths, reachSource: 't', readVerdicts: () => stored, entriesOf: () => [{ path: 'a', blob: 'b' }], diffOf: () => [],
      toolchain: { node: 'v', dotnet: '1' }, slowestOf: () => [], failuresOf: () => [], isProcessGate: () => false, ledger: new BuildLedger(), listProcesses: () => [],
      killTree: () => true, timeoutMs: () => null, lastSlowest: () => [], replayAll: true, privateOf: () => digest,
      installs: new LockInstalls({ changed: () => [], current: () => false, install: async () => ({ ok: true, note: '' }), say: () => {} }),
    } as unknown as FlowDeps;
    const ask = () => runOne(deps, { where: dir, gate: { name: 'g', run: 'x', reuse: '' }, logs: join(dir, 'logs'), beside: {}, tree: 'T', trees: null, step: undefined, noBuild: false });
    const first = await ask();
    stored.push({ gate: 'g', verdict: 'PASS', tree: 'T', at: 'a', ...verdictExtras(first) });
    await ask();
    assert.equal(ran, 1, 'the same private files replay');
    digest = 'two';
    await ask();
    assert.equal(ran, 2, 'a changed private file withdraws the pass');
  } finally {
    remove();
  }
});

test('the run settings a gate names are part of its declaration', () => {
  const declared = (settings: string): string => reuseContext({
    map: { dotnet: [], units: [] }, files: ['src/process.runsettings'], read: () => settings, toolchain: 't',
    gates: [{ name: 'driver-process', run: 'dotnet test src/Driver.Tests --settings src/process.runsettings' }],
    readers: { tsSuites: [], tsReaches: () => false, unitReaches: () => false, pathReach: () => ({}) },
  }).declaration('driver-process');
  assert.notEqual(declared('<Filter>A</Filter>'), declared('<Filter>B</Filter>'));
  assert.equal(declared('<Filter>A</Filter>'), declared('<Filter>A</Filter>'));
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { BuildLedger } from './build-once.ts';
import { runOne, settleMerge, verdictExtras, type FlowDeps, type GateResult } from './flow.ts';
import type { StoredVerdict } from './verdict-store.ts';

const gate = { name: 'censuses', run: 'dotnet test src/A --filter Category=Census', reuse: 'inputs' };

function harness(over: Partial<FlowDeps> = {}) {
  const dir = mkdtempSync(join(tmpdir(), 'flow-'));
  mkdirSync(join(dir, 'logs'));
  const asked: Array<{ run: string; noBuild: unknown }> = [];
  const deps: FlowDeps = {
    runGate: async (_where, each, _logs, options) => {
      asked.push({ run: each.run, noBuild: options.noBuild });
      return { gate: each, code: 0, verdict: 'PASS', note: '', ms: 1, log: join(dir, 'logs', `${each.name}.log`) };
    },
    reaching: (_gate, paths) => paths.filter((path) => path.startsWith('src/')),
    reachSource: 'table-1',
    readVerdicts: () => [],
    entriesOf: () => [{ path: 'src/a.cs', blob: 'a' }],
    diffOf: () => [],
    toolchain: { node: 'v24', dotnet: '10' },
    slowestOf: () => [],
    failuresOf: () => [],
    isProcessGate: () => false,
    ledger: new BuildLedger(),
    listProcesses: () => [],
    killTree: () => true,
    timeoutMs: () => null,
    lastSlowest: () => [],
    ...over,
  };
  return { dir, deps, asked, done: () => rmSync(dir, { recursive: true, force: true }) };
}
const call = (h: ReturnType<typeof harness>, over: { tree?: string; gate?: typeof gate } = {}) => runOne(h.deps, {
  where: h.dir, gate: over.gate ?? gate, logs: join(h.dir, 'logs'), beside: {}, tree: over.tree ?? 'T2', trees: null, step: undefined, noBuild: false,
});

test('a gate whose reached inputs, command and toolchain are unchanged is replayed, runs nothing and says from where', async () => {
  const h = harness();
  try {
    const first = await call(h);
    assert.equal(first.replayed, undefined);
    const key = verdictExtras(first).key!;
    const standing: StoredVerdict[] = [{ gate: 'censuses', verdict: 'PASS', tree: 'T1', at: '2026-11-13T10:00:00Z', key }];
    const again = harness({ readVerdicts: () => standing });
    const result = await call(again);
    assert.equal(result.verdict, 'PASS');
    assert.equal(again.asked.length, 0);
    assert.match(result.note, /replayed from tree T1/);
    assert.match(readFileSync(String(result.log), 'utf8'), /replayed from tree T1/);
    again.done();
  } finally {
    h.done();
  }
});

test('a gate declaring no reuse always runs, though a verdict of its key stands', async () => {
  const h = harness();
  try {
    const first = await call(h, { gate: { ...gate, reuse: '' } });
    const key = verdictExtras(first).key!;
    const again = harness({ readVerdicts: () => [{ gate: 'censuses', verdict: 'PASS', tree: 'T1', at: 'x', key }] });
    await call(again, { gate: { ...gate, reuse: '' } });
    assert.equal(again.asked.length, 1);
    again.done();
  } finally {
    h.done();
  }
});

test('a test gate that follows a passed build of its project on the same tree runs with --no-build, and logs its classes', async () => {
  const h = harness();
  try {
    h.deps.ledger.passed('driver', 'T2', 'dotnet test src/A --filter Category!=Process');
    await call(h, { gate: { ...gate, reuse: '' } });
    assert.match(h.asked[0]!.run, /--no-build/);
    assert.match(h.asked[0]!.run, /--logger "trx;LogFileName=/);
    assert.equal(h.asked[0]!.noBuild, false, 'the hub\'s own reuse adds none beside it');
  } finally {
    h.done();
  }
});

test('a gate that throws is an abnormal end, said so, and the run is told to go on', async () => {
  const h = harness({ runGate: async () => { throw new Error('spawn exploded'); } });
  try {
    const result = await call(h, { gate: { ...gate, reuse: '' } });
    assert.equal(result.verdict, 'FAIL');
    assert.equal(result.abnormal, true);
    assert.match(result.note, /ended abnormally: spawn exploded/);
  } finally {
    h.done();
  }
});

test('a gate that exits with a signal\'s status is abnormal; its own failure is not', async () => {
  const at = (code: number) => harness({ runGate: async (_w, each) => ({ gate: each, code, verdict: 'FAIL', note: `exit ${code}`, ms: 1, log: 'l' }) });
  const killed = at(255);
  const failed = at(1);
  try {
    assert.equal((await call(killed, { gate: { ...gate, reuse: '' } })).abnormal, true);
    assert.equal((await call(failed, { gate: { ...gate, reuse: '' } })).abnormal, undefined);
  } finally {
    killed.done();
    failed.done();
  }
});

test('what a long gate left running beneath its steps is stopped, by pid and creation time, and recorded', async () => {
  const stopped: number[] = [];
  const h = harness({
    runGate: async (_w, each, _l, options) => {
      const trees = options.trees as { hold(pid: number): void };
      trees.hold(100);
      return { gate: each, code: 0, verdict: 'PASS', note: '', ms: 60_000, log: 'l' };
    },
    listProcesses: () => [
      { pid: 200, ppid: 100, created: '2999-01-01T00:00:00.000Z', command: 'rehearsal instance' },
      { pid: 201, ppid: 100, created: '2999-01-01T00:00:00.000Z', command: 'dotnet VBCSCompiler.dll' },
      { pid: 300, ppid: 1, created: '2999-01-01T00:00:00.000Z', command: 'another gate' },
    ],
    killTree: (pid) => { stopped.push(pid); return true; },
  });
  try {
    const result = await call(h, { gate: { ...gate, reuse: '' } });
    assert.deepEqual(stopped, [200], 'a build server and a sibling\'s process are not the gate\'s to stop');
    assert.deepEqual(result.left, [{ pid: 200, created: '2999-01-01T00:00:00.000Z', stopped: true }]);
  } finally {
    h.done();
  }
});

test('a gate past its budget has what it started killed, and fails as over-budget with the slowest classes its last record knew', async () => {
  const killed: number[] = [];
  const h = harness({
    timeoutMs: () => 20,
    lastSlowest: () => [{ name: 'SlowTests', ms: 90_000, tests: 3 }],
    killTree: (pid) => { killed.push(pid); return true; },
    runGate: async (_w, each, _l, options) => {
      const trees = options.trees as { hold(pid: number): void; ended: string | null };
      trees.hold(77);
      for (let waited = 0; waited < 2000 && !trees.ended; waited += 5) await new Promise((done) => setTimeout(done, 5));
      return { gate: each, code: null, verdict: trees.ended ? 'ENDED' : 'PASS', note: 'ended', ms: 20, log: 'l' };
    },
  });
  try {
    const result = await call(h, { gate: { ...gate, reuse: '' } });
    assert.equal(result.verdict, 'FAIL');
    assert.equal(result.overBudget, true);
    assert.match(result.note, /over-budget: killed after .* min, its budget is 0 min; slowest in its last record: SlowTests 1\.5 min/);
    assert.equal(result.slowest![0]!.name, 'SlowTests');
    assert.ok(killed.includes(77), 'what the gate started is killed');
  } finally {
    h.done();
  }
});

test('settling a merge sets down main\'s reds, fails the merge on the branch\'s, and builds the record and the budget lines', () => {
  const make = (name: string, verdict: string, failures: Array<{ unit: string; key: string }>, ms = 1000): GateResult => ({
    gate: { name, run: 'x' }, verdict, note: '', ms, failures, log: 'l',
  });
  const mains = [{ gate: 'reached', key: 'k1', from: 'finding' as const, at: '2026-11-12T08:00:00Z', commit: 'abc' }];
  const onlyMain = settleMerge({ branch: 'b', at: 'now', wallMs: 1500, results: [make('reached', 'FAIL', [{ unit: 'X', key: 'k1' }]), { ...make('cli', 'FLAKE', []), flakes: ['A.Tests'] }], mains, previous: [] });
  assert.deepEqual(onlyMain.failed, []);
  assert.equal(onlyMain.record.passed, true);
  assert.deepEqual([onlyMain.record.ms, onlyMain.record.wallMs, onlyMain.record.flakes], [2000, 1500, 1], 'gate time summed, wall time as measured, the flaky tests counted');
  assert.equal(onlyMain.record.mainReds.length, 1);
  assert.match(onlyMain.lines.join('\n'), /main's red/);
  const both = settleMerge({ branch: 'b', at: 'now', wallMs: 11 * 60_000, results: [make('reached', 'FAIL', [{ unit: 'X', key: 'k1' }, { unit: 'Y', key: 'k2' }]), make('cli', 'PASS', [], 6 * 60_000)], mains, previous: [] });
  assert.deepEqual(both.failed.map((each) => each.gate.name), ['reached']);
  assert.match(both.lines.join('\n'), /cli took 6 min, over its 5 min budget/);
  assert.equal(both.record.firstPass, false);
});

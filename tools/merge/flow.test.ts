import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { BuildLedger } from './build-once.ts';
import { fixtureFolder } from './fixture-home.ts';
import { runOne, settleMerge, verdictExtras, type FlowDeps, type GateResult } from './flow.ts';
import { LockInstalls } from './lock-install.ts';
import type { StoredVerdict } from './verdict-store.ts';

const gate = { name: 'censuses', run: 'dotnet test src/A --filter Category=Census', reuse: 'inputs' };

function harness(over: Partial<FlowDeps> = {}) {
  const { dir, remove } = fixtureFolder('flow');
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
    installs: new LockInstalls({ changed: () => [], current: () => false, install: async () => ({ ok: true, note: '' }), say: () => {} }),
    ...over,
  };
  return { dir, deps, asked, done: remove };
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

test('a gate runs after the npm installs its tree owes, which run once however many gates start at once', async () => {
  const order: string[] = [];
  const installs = new LockInstalls({
    changed: () => ['package-lock.json'], current: () => false, say: () => {},
    install: async (_where, folder) => {
      await new Promise((done) => setTimeout(done, 5));
      order.push(`npm ci ${folder}`);
      return { ok: true, note: 'exit 0' };
    },
  });
  const h = harness({
    installs,
    runGate: async (_w, each) => {
      order.push(each.name);
      return { gate: each, code: 0, verdict: 'PASS', note: '', ms: 1, log: 'l' };
    },
  });
  try {
    const results = await Promise.all(['a', 'b'].map((name) => call(h, { gate: { ...gate, name, reuse: '' } })));
    assert.deepEqual(results.map((result) => result.verdict), ['PASS', 'PASS']);
    assert.deepEqual(order, ['npm ci .', 'a', 'b']);
  } finally {
    h.done();
  }
});

test('a failed install fails each gate without running it, and says which folder', async () => {
  const h = harness({
    installs: new LockInstalls({ changed: () => ['package-lock.json'], current: () => false, say: () => {}, install: async () => ({ ok: false, note: 'exit 1' }) }),
  });
  try {
    const result = await call(h, { gate: { ...gate, reuse: '' } });
    assert.equal(result.verdict, 'FAIL');
    assert.equal(result.abnormal, true);
    assert.match(result.note, /npm ci failed in \. \(exit 1\)/);
    assert.equal(h.asked.length, 0, 'the gate did not run');
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
      while (!trees.ended) await new Promise((done) => setTimeout(done, 5));
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
  const make = (name: string, verdict: string, failures: Array<{ unit: string; key: string; test: string | null }>, ms?: number): GateResult => ({
    gate: { name, run: 'x' }, verdict, note: '', ms: ms ?? 1000, failures, log: 'l',
  });
  const X = { unit: 'X', key: 'k1', test: 'X.a' };
  const mains = [{ gate: 'reached', key: 'k1', test: 'X.a', from: 'finding' as const, at: '2026-11-12T08:00:00Z', commit: 'abc', stale: false }];
  const onlyMain = settleMerge({ branch: 'b', at: 'now', wallMs: 1500, results: [make('reached', 'FAIL', [X]), { ...make('cli', 'FLAKE', []), flakes: ['A.Tests'] }], mains, previous: [] });
  assert.deepEqual(onlyMain.failed, []);
  assert.equal(onlyMain.record.passed, true);
  assert.deepEqual([onlyMain.record.ms, onlyMain.record.wallMs, onlyMain.record.flakes], [2000, 1500, 1], 'gate time summed, wall time as measured, the flaky tests counted');
  assert.equal(onlyMain.record.mainReds.length, 1);
  assert.match(onlyMain.lines.join('\n'), /main's red/);
  const both = settleMerge({ branch: 'b', at: 'now', wallMs: 11 * 60_000, results: [make('reached', 'FAIL', [X, { unit: 'Y', key: 'k2', test: 'Y.b' }]), make('cli', 'PASS', [], 6 * 60_000)], mains, previous: [] });
  assert.deepEqual(both.failed.map((each) => each.gate.name), ['reached']);
  assert.match(both.lines.join('\n'), /cli took 6 min, over its 5 min budget/);
  assert.equal(both.record.firstPass, false);
});

test('a gate whose failure was read whole fails the merge under a finding of main\'s that names no test, and the summary says why', () => {
  const unread = { unit: 'web-rules', key: 'g1', test: null };
  const mains = [{ gate: 'web-rules', key: 'g1', test: null, from: 'finding' as const, at: '2026-11-11T09:07:19Z', commit: 'abc', stale: true }];
  const settled = settleMerge({
    branch: 'b', at: 'now', wallMs: 1000, mains, previous: [],
    results: [{ gate: { name: 'web-rules', run: 'x' }, verdict: 'FAIL', note: '', ms: 1000, failures: [unread], log: 'l' }],
  });
  assert.deepEqual(settled.failed.map((each) => each.gate.name), ['web-rules']);
  assert.deepEqual(settled.held, []);
  assert.match(settled.lines.join('\n'), /web-rules: a finding of main's names no test, so it excuses none of its failures/);
});

const plain = { name: 'deployment', run: 'npm run rehearse:deploy', reuse: '' };
const askNarrowed = (h: ReturnType<typeof harness>, narrow: NonNullable<Parameters<typeof runOne>[1]['narrow']>) => runOne(h.deps, {
  where: h.dir, gate: plain, logs: join(h.dir, 'logs'), beside: {}, tree: 'T2', trees: null, step: undefined, noBuild: false, narrow,
});

test('a full set replays a gate that declares no reuse, from the run it names; a merge does not', async () => {
  const h = harness();
  try {
    const key = verdictExtras(await call(h, { gate: plain })).key!;
    const standing: StoredVerdict[] = [{ gate: 'deployment', verdict: 'PASS', tree: 'T1', at: '2026-11-12T10:00:00Z', commit: 'abc1234def', key }];
    const full = harness({ readVerdicts: () => standing, replayAll: true });
    const result = await call(full, { gate: plain });
    assert.equal(full.asked.length, 0);
    assert.match(result.note, /replayed from tree T1.*abc1234/);
    const merge = harness({ readVerdicts: () => standing });
    await call(merge, { gate: plain });
    assert.equal(merge.asked.length, 1);
    full.done();
    merge.done();
  } finally {
    h.done();
  }
});

test('a full set never replays a red or a flake of the same key', async () => {
  const h = harness();
  try {
    const key = verdictExtras(await call(h, { gate: plain })).key!;
    for (const verdict of ['FAIL', 'FLAKE']) {
      const again = harness({ replayAll: true, readVerdicts: () => [{ gate: 'deployment', verdict: 'PASS', tree: 'T0', at: 'a', key }, { gate: 'deployment', verdict, tree: 'T1', at: 'b', key }] });
      await call(again, { gate: plain });
      assert.equal(again.asked.length, 1, verdict);
      again.done();
    }
  } finally {
    h.done();
  }
});

test('a reached run that ends with a class flaked reads FLAKE, and its class counts reach the gate record', async () => {
  const outcome = { ran: 3, replayed: 9, flaked: ['driver|A.OneTests'], mainRed: [], red: [], deferred: ['driver-process|B.TwoTests'] };
  const h = harness({
    runGate: async (_where, each, logs) => {
      const log = join(logs, `${each.name}.log`);
      writeFileSync(log, `reached: ran\nreached-classes: ${JSON.stringify(outcome)}\n`);
      return { gate: each, code: 0, verdict: 'PASS', note: '', ms: 1, log };
    },
  });
  try {
    const result = await call(h);
    assert.equal(result.verdict, 'FLAKE');
    assert.deepEqual(result.flakes, ['driver|A.OneTests']);
    const { record } = settleMerge({ branch: 'b', at: '2026-11-14T00:00:00Z', wallMs: 1, results: [result], mains: [], previous: [] });
    assert.deepEqual(record.gates[0]!.classes, { ran: 3, replayed: 9, flaked: 1, mainRed: 0, red: 0, deferred: 1 });
    assert.deepEqual(record.heavy, { deferred: ['driver-process|B.TwoTests'] });
  } finally {
    h.done();
  }
});

test('a gate narrowed to the classes no pass covers runs the narrowed command, keyed as the declared gate; none left passes unrun', async () => {
  const h = harness({ replayAll: true });
  try {
    const narrowed = await askNarrowed(h, () => ({ run: 'npm run narrowed', replay: { replayedCount: 3, ran: 1, runs: ['abc1234'], oldest: 'x' } }));
    assert.equal(h.asked[0]!.run, 'npm run narrowed');
    assert.equal(narrowed.classReplay?.ran, 1);
    assert.deepEqual(verdictExtras(narrowed).key, verdictExtras(await call(harness(), { gate: plain })).key);
    const none = await askNarrowed(h, () => ({ run: null, replay: { replayedCount: 4, ran: 0, runs: ['abc1234'], oldest: 'x' } }));
    assert.equal(h.asked.length, 1);
    assert.equal(none.verdict, 'PASS');
    assert.match(none.note, /4 classes replayed/);
  } finally {
    h.done();
  }
});

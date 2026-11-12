import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, readFileSync, utimesSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
import { repoRoot, tool, refusal, type Verdict, type CpuTimes, type Gate, type GateTreeFacts } from './support/merge-branch.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


/**
 * The parent's one merge tool (`docs/2026-09-30-parallel-development-design.md` §3 rule 6). The plan is read from
 * the declared set: a merge runs the gates its paths can reach, and the lane table's tests hold it to what each suite
 * reads and each rehearsal runs. The full set is still owed before a stage.
 *
 * 🔴 The end-to-end tests below run the tool in a scratch repository of their own, and assert that it is
 * its own repository before the first run: git walks UP, and a tool that found the repository this suite sits in would merge into it.
 */

// The census of test units reads this file's strings as the lane table's data, not as reads; what it does read is declared.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row

// ---------------------------------------------------------------------------------------------------
// Arguments

// ---------------------------------------------------------------------------------------------------
// The full set in its own worktree, beside the merges

// ---------------------------------------------------------------------------------------------------
// The local CI runner, main's tip gated after each merge so a release waits only for the last delta

test('a CI run and a stage hold the gate tree as a full set does, and a refusal names which holds it', () => {
  const fx = makeFixture('merge-branch-gate-holders');
  const sha = 'a'.repeat(40);
  tool.takeGateLock(fx.root, { commit: sha, holder: 'ci', alive: () => true, now: () => '2026-10-22T10:00:00Z' });
  assert.equal(tool.readGateLock(fx.root, () => true)!.holder, 'ci');
  let error = refusal(() => tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => true }));
  assert.equal(error.exitCode, 2);
  assert.match(error.message, new RegExp(`a CI run holds the gate tree already: on aaaaaaa since 2026-10-22 10:00 UTC \\(pid ${process.pid}, local/gate-tree\\); one runs at a time`));
  assert.equal((error as unknown as { busy?: boolean }).busy, true, 'the watch reads a held tree as one to wait for, never a failure');
  tool.releaseGateLock(fx.root);
  // A stage from the gate tree (publish:desktop --from-gated) holds it through its build.
  writeFileSync(join(fx.root, tool.GATE_TREE_LOCK), `${JSON.stringify({ pid: process.pid, commit: sha, at: '2026-10-22T11:00:00Z', holder: 'stage' })}\n`);
  error = refusal(() => tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), holder: 'ci', alive: () => true }));
  assert.match(error.message, new RegExp(`a stage of aaaaaaa is building from the gate tree since 2026-10-22 11:00 UTC \\(pid ${process.pid}, local/gate-tree\\): it holds the tree until its build ends`));
  // The prune keeps it while it builds, saying whose it is.
  const lock = tool.readGateLock(fx.root, () => true);
  assert.match(tool.gateTreeVerdict({ listed: true, head: sha, changes: [], lock, staged: null }).why, /its stage is building/);
  fx.cleanup();
});


test("a CI run gates what this tree's verdicts lack: a gate with none, or whose last pass a changed path reaches; a failure stays owed; the records are no excuse", () => {
  const plan = ['universal', 'service', 'web', 'cli'];
  const verdict = (gate: string, tree: string, value = 'PASS'): Verdict => ({ gate, verdict: value, tree, at: '2026-10-22T10:00:00Z', commit: `${tree}-commit`, branch: null });
  const drifts: Record<string, string[]> = { t1: ['src/Daoris.Web/src/a.ts'], t0: ['src/Daoris.Web/src/a.ts', 'TASKS.md'] };
  const facts = {
    tree: 't2',
    drift: (from: string) => drifts[from] ?? null,
    // The lane table, stood in: every path reaches the baseline, a page source the web gate, the backlog the service suite.
    reaching: (gate: string, paths: string[]) => paths.filter((path) => ['universal', 'cli'].includes(gate)
      || (gate === 'web' && path.startsWith('src/Daoris.Web/')) || (gate === 'service' && path === 'TASKS.md')),
  };
  // No verdict at all, a machine's first run: every gate runs.
  assert.deepEqual(tool.ciPlan({ ...facts, plan, verdicts: [] }).run, plan);
  // Every gate passed on t1, and t2 differs from it by a page source: the gates it reaches run, the service suite is kept.
  let chosen = tool.ciPlan({ ...facts, plan, verdicts: plan.map((gate) => verdict(gate, 't1')) });
  assert.deepEqual(chosen.run, ['universal', 'web', 'cli']);
  assert.deepEqual(chosen.kept.map((kept) => `${kept.name} ${kept.verdict.verdict} ${kept.standing.standing}`), ['service PASS unreached']);
  // The stage forgives the records; the CI does not: a backlog edit since reaches the service suite, which runs.
  chosen = tool.ciPlan({ ...facts, plan, verdicts: plan.map((gate) => verdict(gate, 't0')) });
  assert.deepEqual(chosen.run, plan);
  // A failure stays owed whatever changed since: the service suite failed on t1, nothing since reaches it, and it runs again.
  chosen = tool.ciPlan({ ...facts, plan, verdicts: plan.map((gate) => verdict(gate, 't1', gate === 'service' ? 'FAIL' : 'PASS')) });
  assert.deepEqual(chosen.run, plan);
  // A pass on this very tree is kept, and so is a flake: nothing to run.
  chosen = tool.ciPlan({ ...facts, plan, verdicts: plan.map((gate) => verdict(gate, 't2', gate === 'web' ? 'FLAKE' : 'PASS')) });
  assert.deepEqual([chosen.run, chosen.kept.map((kept) => kept.standing.standing)], [[], ['exact', 'exact', 'exact', 'exact']]);
  // A tree git can no longer compare is no pass: its gates run.
  assert.deepEqual(tool.ciPlan({ ...facts, plan, verdicts: plan.map((gate) => verdict(gate, 'gone')) }).run, plan);
});


test('CIFAST1: a plain CI run owes the family and deployment rehearsals a stage, neither run nor failed; the stage\'s path runs them, and the stage still asks their pass', () => {
  assert.deepEqual([...tool.STAGE_REHEARSALS], ['rehearse:family', 'rehearse:deploy']);
  assert.equal(tool.isStageRehearsal({ name: 'renamed', run: 'npm run rehearse:family' }), true);
  assert.equal(tool.isStageRehearsal({ name: 'renamed', run: 'npm run -s rehearse:deploy' }), true);
  for (const run of ['npm run rehearse', 'npm run rehearse:family-x', 'npm run test:web:e2e', 'dotnet test src/X/X.Tests --settings src/Daoris.Desktop/git.process.runsettings']) {
    assert.equal(tool.isStageRehearsal({ name: 'x', run }), false, run);
  }
  // The release rehearsal (1 to 4 minutes) stays in every run, and each read from this repository's declared gates.
  const declared = tool.readPlan(repoRoot).filter(tool.isStageRehearsal).map((gate) => gate.name);
  assert.deepEqual(declared, ['rehearse-family', 'deployment']);
  assert.equal(tool.stageCommand('a'.repeat(40)), 'node tools/merge-branch.mjs --ci --stage aaaaaaa');

  const plan = ['universal', 'rehearse', 'rehearse-family', 'deployment'];
  const owe = ['rehearse-family', 'deployment'];
  const verdict = (gate: string, tree: string, value = 'PASS'): Verdict => ({ gate, verdict: value, tree, at: '2026-10-24T10:00:00Z', commit: `${tree}-commit`, branch: null });
  const facts = { tree: 't2', drift: (from: string) => (from === 't1' ? ['src/a.ts'] : null), reaching: (_gate: string, paths: string[]) => paths };
  // No verdict: a plain run runs the rest and owes the two; the stage's path (owing none) runs every gate.
  let chosen = tool.ciPlan({ ...facts, plan, owe, verdicts: [] });
  assert.deepEqual([chosen.run, chosen.owed, chosen.kept], [['universal', 'rehearse'], owe, []]);
  assert.deepEqual([tool.ciPlan({ ...facts, plan, verdicts: [] }).run, tool.ciPlan({ ...facts, plan, verdicts: [] }).owed], [plan, []]);
  // A failed rehearsal is owed too, never run again by a plain run, and never read as passed.
  chosen = tool.ciPlan({ ...facts, plan, owe, verdicts: plan.map((gate) => verdict(gate, 't2', gate === 'deployment' ? 'FAIL' : 'PASS')) });
  assert.deepEqual([chosen.run, chosen.owed, chosen.kept.map((kept) => kept.name)], [[], ['deployment'], ['universal', 'rehearse', 'rehearse-family']]);
  // A rehearsal pass that stands is kept, owed or not; one a change since reaches is owed again.
  chosen = tool.ciPlan({ ...facts, plan, owe, verdicts: plan.map((gate) => verdict(gate, 't1')) });
  assert.deepEqual([chosen.run, chosen.owed], [['universal', 'rehearse'], owe]);
  // The stage's judgement is unchanged: it still lacks what a plain run owes.
  const judged = tool.stageJudgement({ ...facts, plan, verdicts: plan.filter((gate) => !owe.includes(gate)).map((gate) => verdict(gate, 't2')), isRecord: () => false });
  assert.deepEqual([judged.passed, judged.missing], [false, owe]);
});


test('a CI run measures the machine before its gates start, and runs the light lane wider only when it is idle', async () => {
  const gb = 2 ** 30;
  const idle = tool.ciWidth({ busy: 0.1, physical: 40 * gb, commit: 50 * gb });
  assert.deepEqual([idle.width, idle.idle], [tool.CI_IDLE.width, true]);
  assert.match(idle.why, /idle: 10% of its processors busy, 50\.0 GB free commit and 40\.0 GB free physical memory/);
  assert.ok(tool.CI_IDLE.width > tool.LIGHT_WIDTH, 'wider than a merge\'s light lane');
  for (const load of [
    { busy: 0.4, physical: 40 * gb, commit: 50 * gb }, { busy: 0.1, physical: 8 * gb, commit: 50 * gb },
    { busy: 0.1, physical: 40 * gb, commit: 4 * gb }, { busy: null, physical: 40 * gb, commit: 50 * gb }, { busy: 0.1, physical: null, commit: null },
  ]) {
    const chosen = tool.ciWidth(load);
    assert.deepEqual([chosen.width, chosen.idle], [tool.LIGHT_WIDTH, false], JSON.stringify(load));
  }
  assert.match(tool.ciWidth({ busy: 0.4, physical: 40 * gb, commit: 50 * gb }).why, /not idle: 40% of its processors busy/);
  assert.match(tool.ciWidth({ busy: 0.1, physical: 8 * gb, commit: 50 * gb }).why, /not idle: .*8\.0 GB free physical memory/);
  assert.match(tool.ciWidth({ busy: null, physical: 40 * gb, commit: 50 * gb }).why, /its processors' load could not be measured/);
  // Where the platform keeps no commit to read, the free physical memory judges alone.
  assert.equal(tool.ciWidth({ busy: 0.1, physical: 40 * gb, commit: null }).width, tool.CI_IDLE.width);

  // The share busy is the processors' own times between two readings, across all of them.
  const cpu = (user: number, idleTime: number): CpuTimes => ({ times: { user, nice: 0, sys: 0, idle: idleTime, irq: 0 } });
  assert.equal(tool.cpuBusy([cpu(100, 100), cpu(100, 100)], [cpu(130, 170), cpu(120, 180)]), 0.25);
  assert.equal(tool.cpuBusy([cpu(1, 1)], [cpu(1, 1)]), null, 'no time passed: not measured');
  assert.equal(tool.cpuBusy([cpu(1, 1)], []), null, 'a count of processors that changed is not measured');
  let slept = 0;
  const readings = [[cpu(0, 0)], [cpu(50, 50)]];
  const load = await tool.measureLoad({ cpus: () => readings.shift()!, sleep: async (ms) => { slept = ms; }, read: () => ({ physical: 3, commit: 4 }), ms: 750 });
  assert.deepEqual([load, slept], [{ busy: 0.5, physical: 3, commit: 4 }, 750]);
});


test("a CI run's status names each gate of the plan: those it runs waiting, the rest kept with the verdict that stands and where", () => {
  const plan: Gate[] = [
    { name: 'universal', run: 'node u.mjs', kind: 'check' },
    { name: 'driver-process', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings', kind: 'suite' },
    { name: 'web', run: 'npm run test:web:e2e', kind: 'rehearsal' },
  ];
  const kept: Verdict = { gate: 'web', verdict: 'PASS', tree: 't1', at: '2026-10-22T09:00:00Z', commit: 'c'.repeat(40), branch: null };
  const status = tool.ciStatus({
    commit: 'a'.repeat(40), tree: 't2', plan,
    chosen: { run: ['universal', 'driver-process'], kept: [{ name: 'web', verdict: kept, standing: { standing: 'unreached', paths: ['docs/a.md'], records: [] } }] },
    since: { commit: 'b'.repeat(40), at: '2026-10-22T08:00:00Z', paths: 3 },
    light: { width: 5, idle: true, why: 'idle' }, pid: 42, at: '2026-10-22T10:00:00Z',
    logOf: (gate, commit) => (commit === kept.commit ? `local/scratch/ci-ccccccc/${gate}.log` : null),
  });
  assert.deepEqual(Object.keys(status), ['schema', '_why', 'commit', 'tree', 'state', 'pid', 'started', 'updated', 'ended', 'since', 'light', 'stage', 'kind', 'owed', 'gates']);
  assert.deepEqual([status.stage, status.owed], [false, []]);
  assert.deepEqual([status.schema, status.commit, status.tree, status.state, status.pid, status.started, status.updated, status.ended],
    [1, 'a'.repeat(40), 't2', 'running', 42, '2026-10-22T10:00:00Z', '2026-10-22T10:00:00Z', null]);
  assert.deepEqual(status.since, { commit: 'b'.repeat(40), at: '2026-10-22T08:00:00Z', paths: 3 });
  assert.deepEqual(status.light, { width: 5, idle: true, why: 'idle' });
  assert.deepEqual(status.gates[0], {
    name: 'universal', kind: plan[0]!.kind, lane: 'light', state: 'waiting', verdict: null, at: null, ms: null, log: null, note: null, kept: null,
  });
  assert.equal(status.gates[1]!.lane, 'process');
  assert.deepEqual(status.gates[2], {
    name: 'web', kind: plan[2]!.kind, lane: 'light', state: 'kept', verdict: 'PASS', at: '2026-10-22T09:00:00Z', ms: null,
    log: 'local/scratch/ci-ccccccc/web.log', note: null, kept: { commit: 'c'.repeat(40), tree: 't1', standing: 'unreached' },
  });

  // A gate a plain run owes a stage is `owed`, neither failed nor passed, naming the stage's path.
  const owing = tool.ciStatus({
    commit: 'a'.repeat(40), tree: 't2', plan: [...plan, { name: 'rehearse-family', run: 'npm run rehearse:family', kind: 'rehearsal' }],
    chosen: { run: ['universal', 'driver-process'], kept: [], owed: ['web', 'rehearse-family'] },
    since: { commit: null, at: null, paths: null }, light: { width: 3, idle: false, why: 'busy' }, pid: 42, at: '2026-10-24T10:00:00Z',
    logOf: () => null,
  });
  assert.deepEqual([owing.stage, owing.owed], [false, ['web', 'rehearse-family']]);
  assert.deepEqual(owing.gates[3], {
    name: 'rehearse-family', kind: 'rehearsal', lane: 'process', state: 'owed', verdict: null, at: null, ms: null, log: null,
    note: 'owed for a stage: node tools/merge-branch.mjs --ci --stage aaaaaaa runs it', kept: null,
  });
  assert.equal(owing.gates[2]!.state, 'owed');
  assert.equal(tool.ciStatus({ commit: 'a'.repeat(40), tree: 't2', plan, chosen: { run: [], kept: [] }, since: { commit: null, at: null, paths: null },
    light: { width: 3, idle: false, why: 'busy' }, pid: 42, at: '2026-10-24T10:00:00Z', logOf: () => null, stage: true }).stage, true);
});


test("the CI watch gates main's tip when it moves and the gate tree is idle: once each, the newest only, and it ends when asked", async () => {
  let tip: string | null = 'aaa';
  let held: string | null = null;
  let stop = false;
  let clock = 0;
  let turn = 0;
  const gated = new Set<string>();
  const ran: string[] = [];
  const said: string[] = [];
  const failing = new Map<string, Error>();
  // What happens while the watch sleeps, by its turn: each turn is a tick of its own clock.
  const script: Record<number, () => void> = {
    1: () => { tip = 'ddd'; held = 'a stage of ccc is building from the gate tree'; },
    3: () => { held = null; },
    5: () => { tip = 'eee'; failing.set('eee', new Error('npm ci failed in the gate tree')); },
    8: () => { tip = 'fff'; },
    10: () => { stop = true; },
  };
  const code = await tool.watchCi({
    tip: () => tip,
    gated: (commit) => gated.has(commit),
    holder: () => held,
    run: async () => {
      const at = tip!;
      ran.push(at);
      const error = failing.get(at);
      if (error) throw error;
      // Two merges land while the first run goes: bbb, then ccc. The next run takes the newest; bbb is never gated alone.
      if (at === 'aaa') tip = 'ccc';
      gated.add(at);
      return 0;
    },
    stopAsked: () => stop,
    sleep: async (ms) => { clock += ms; turn += 1; script[turn]?.(); },
    say: (line) => said.push(line),
    pollMs: 1000, tickMs: 1000, now: () => clock,
  });
  assert.equal(code, 0);
  assert.deepEqual(ran, ['aaa', 'ccc', 'ddd', 'eee', 'fff'], 'each tip once, a superseded one never, a failed one not again');
  assert.equal(said.filter((line) => /ddd waits: a stage of ccc is building from the gate tree/.test(line)).length, 1, `said once while it waits:\n${said.join('\n')}`);
  assert.equal(said.filter((line) => /npm ci failed in the gate tree/.test(line)).length, 1, `a failure said once:\n${said.join('\n')}`);
  assert.match(said.at(-1)!, /asked to stop/);
});


test('the gate tree, its lock and its record are under local/, and the publish names the same three', async () => {
  assert.deepEqual([tool.GATE_TREE, tool.GATE_TREE_LOCK, tool.GATE_TREE_RECORD], ['local/gate-tree', 'local/gate-tree.lock', 'local/gate-tree.json']);
  const named = await import(
    // @ts-expect-error — untyped workspace tooling; the same seam as above
    '../../../tools/desktop-publish.mjs') as { GATE_TREE: string; GATE_TREE_LOCK: string; GATE_TREE_RECORD: string };
  assert.deepEqual([named.GATE_TREE, named.GATE_TREE_LOCK, named.GATE_TREE_RECORD], [tool.GATE_TREE, tool.GATE_TREE_LOCK, tool.GATE_TREE_RECORD]);
  assert.deepEqual(tool.DEPENDENCY_FOLDERS, ['.', 'src/Daoris.Web'], 'the two installs AGENTS.md has a fresh checkout make');
});


test('one full set beside at a time: a second is refused naming the run that holds the lock; one whose process ended is taken over', () => {
  const fx = makeFixture('merge-branch-gate-lock');
  const sha = 'a'.repeat(40);
  const first = tool.takeGateLock(fx.root, { commit: sha, alive: () => true, startOf: () => null, now: () => '2026-10-18T10:00:00Z' });
  assert.equal(first.stale, null);
  const held = tool.readGateLock(fx.root, () => true);
  assert.deepEqual(held, { pid: process.pid, commit: sha, at: '2026-10-18T10:00:00Z', started: null, running: true, holder: 'full' });

  const error = refusal(() => tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => true }));
  assert.equal(error.exitCode, 2);
  assert.match(error.message, new RegExp(`a full set beside the merges is running already: on aaaaaaa since 2026-10-18 10:00 UTC \\(pid ${process.pid}, local/gate-tree\\); one runs at a time`));
  assert.equal(tool.readGateLock(fx.root, () => true)!.commit, sha, 'a refusal leaves the lock as it was');

  // Its process ended without letting go: the lock is taken over, and the caller says so.
  const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => false, end: () => [] });
  assert.deepEqual(taken.stale, { pid: process.pid, commit: sha, at: '2026-10-18T10:00:00Z', ended: [] });
  assert.equal(tool.readGateLock(fx.root, () => true)!.commit, 'b'.repeat(40));
  tool.releaseGateLock(fx.root);
  assert.equal(tool.readGateLock(fx.root), null);
  assert.equal(existsSync(join(fx.root, tool.GATE_TREE_LOCK)), false);
  fx.cleanup();
});


test('a taken-over lock ends what its dead pid started since the lock\'s at, and only then (CIPRIO1d)', () => {
  const fx = makeFixture('merge-branch-gate-lock-end');
  const calls: [number, number][] = [];
  const end = (pid: number, since: number): number[] => { calls.push([pid, since]); return [7, 8]; };
  const strays: [string, number][] = [];
  const endStrays = (folder: string, since: number): number[] => { strays.push([folder, since]); return [8, 9]; };
  tool.takeGateLock(fx.root, { commit: 'a'.repeat(40), alive: () => true, startOf: () => null, now: () => '2026-10-18T10:00:00Z', end, endStrays });
  assert.deepEqual(calls, [], 'a first take has no dead run to end');
  refusal(() => tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => true, end, endStrays }));
  assert.deepEqual(calls, [], 'a refusal ends nothing: the run holding the lock runs');
  assert.deepEqual(strays, []);
  const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => false, end, endStrays });
  assert.deepEqual(calls, [[process.pid, Date.parse('2026-10-18T10:00:00Z')]]);
  // The shells' orphans, by the gate tree's folder, beside the dead pid's children, each pid once
  assert.deepEqual(strays, [[join(fx.root, tool.GATE_TREE), Date.parse('2026-10-18T10:00:00Z')]]);
  assert.deepEqual(taken.stale, { pid: process.pid, commit: 'a'.repeat(40), at: '2026-10-18T10:00:00Z', ended: [7, 8, 9] });
  fx.cleanup();
});


test('a taken-over lock whose at does not parse ends nothing', () => {
  const fx = makeFixture('merge-branch-gate-lock-badat');
  mkdirSync(join(fx.root, 'local'), { recursive: true });
  writeFileSync(join(fx.root, tool.GATE_TREE_LOCK), `${JSON.stringify({ pid: 4242, commit: 'a'.repeat(40), at: 'not a time' })}\n`);
  const calls: number[] = [];
  const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => false, end: (pid: number) => { calls.push(pid); return []; }, endStrays: () => { calls.push(-1); return []; } });
  assert.deepEqual(calls, []);
  assert.deepEqual(taken.stale?.ended, []);
  fx.cleanup();
});


test('the gate tree\'s run logs are kept in the main checkout as beside-<sha7>, replacing an older run; a killed run\'s are rescued once (GATEWT1c)', () => {
  const fx = makeFixture('merge-branch-keep-logs');
  const home = join(fx.root, 'home');
  const tree = join(fx.root, 'tree');
  const sha = 'abcdef1' + '0'.repeat(33);
  const logs = join(tree, 'local', 'scratch', 'full-abcdef1');
  mkdirSync(logs, { recursive: true });
  writeFileSync(join(logs, 'first.log'), 'new\n');
  writeFileSync(join(logs, 'first.rerun.log'), 'again\n');
  const kept = join(home, 'local', 'scratch', 'beside-abcdef1');
  mkdirSync(kept, { recursive: true });
  writeFileSync(join(kept, 'old.log'), 'old\n');

  assert.equal(tool.keepGateLogs(tree, home, sha), true);
  assert.deepEqual(readdirSync(kept).sort(), ['first.log', 'first.rerun.log'], 'the newest run replaces the folder');
  assert.equal(readFileSync(join(kept, 'first.log'), 'utf8'), 'new\n');
  assert.equal(tool.keepGateLogs(tree, home, '1234567' + '0'.repeat(33)), false, 'no run folder, nothing kept');

  // A killed run leaves its folder in the tree and none in the main checkout.
  const killed = join(tree, 'local', 'scratch', 'full-9999999');
  mkdirSync(killed, { recursive: true });
  writeFileSync(join(killed, 'last.partial'), 'half\n');
  const said = tool.rescueGateLogs(tree, home);
  assert.equal(readFileSync(join(home, 'local', 'scratch', 'beside-9999999', 'last.partial'), 'utf8'), 'half\n');
  assert.equal(said.length, 1);
  assert.equal(readFileSync(join(kept, 'first.log'), 'utf8'), 'new\n', 'a folder already kept is left as it is');
  fx.cleanup();
});


test('the prune never takes the gate tree while its full set runs; a merge\'s prune takes it once staged, --prune once idle, each said', () => {
  const sha = 'a'.repeat(40);
  const idle: GateTreeFacts = { listed: true, head: sha, changes: [], lock: null, staged: null };
  const running = { ...idle, lock: { pid: 7, commit: sha, at: '2026-10-18T10:00:00Z', running: true } };
  for (const atMerge of [true, false]) {
    const kept = tool.gateTreeVerdict(running, { atMerge });
    assert.equal(kept.remove, false);
    assert.match(kept.why, /^its full set is running \(pid 7, since 2026-10-18 10:00 UTC, on aaaaaaa\): never removed while it runs$/);
  }
  // A merge's prune keeps a gated tree its stage has not read: that is what the run is for.
  const waiting = tool.gateTreeVerdict(idle, { atMerge: true });
  assert.equal(waiting.remove, false);
  assert.match(waiting.why, /its stage has not read it: npm run publish:desktop -- --to <install> --service --stage --from-gated aaaaaaa; --prune removes it/);
  const staged = { ...idle, staged: { commit: sha, at: '2026-10-18T12:00:00Z' } };
  const done = tool.gateTreeVerdict(staged, { atMerge: true });
  assert.equal(done.remove, true);
  assert.match(done.said!, /the gate tree at aaaaaaa: its stage is done \(2026-10-18 12:00 UTC\)/);
  // A stage of another commit is not this tree's.
  assert.equal(tool.gateTreeVerdict({ ...idle, staged: { commit: 'b'.repeat(40), at: '2026-10-18T12:00:00Z' } }, { atMerge: true }).remove, false);
  const asked = tool.gateTreeVerdict(idle, { atMerge: false });
  assert.equal(asked.remove, true);
  assert.match(asked.said!, /the gate tree at aaaaaaa, never staged/);
  // A lock whose process ended is no run: it goes with the tree, and is said.
  const left = tool.gateTreeVerdict({ ...idle, lock: { ...running.lock, running: false } }, { atMerge: false });
  assert.equal(left.remove, true);
  assert.match(left.said!, /a lock left by pid 7, which no longer runs/);
  // A full set started there by hand holds no lock: a process inside keeps the tree, and so does not knowing.
  for (const atMerge of [true, false]) {
    const busy = tool.gateTreeVerdict({ ...staged, processes: [{ pid: 9, exe: '', command: 'node tools/merge-branch.mjs --full' }] }, { atMerge });
    assert.equal(busy.remove, false);
    assert.match(busy.why, /a running process is inside it \(pid 9: node tools\/merge-branch\.mjs --full\), and no lock says whose run: it is never removed while one runs/);
    const unknown = tool.gateTreeVerdict({ ...staged, processError: 'the process query failed' }, { atMerge });
    assert.equal(unknown.remove, false);
    assert.match(unknown.why, /whether a process runs inside it could not be read \(the process query failed\)/);
  }
  // Changes are never forced away, at either prune.
  for (const atMerge of [true, false]) {
    const changed = tool.gateTreeVerdict({ ...staged, changes: ['code.txt'] }, { atMerge });
    assert.equal(changed.remove, false);
    assert.match(changed.why, /it holds changes git removes only with --force \(code\.txt\)/);
  }
  // A folder git no longer lists is the half-removed rule's: no .git, no process inside, it goes.
  const half = tool.gateTreeVerdict({ ...idle, listed: false, folder: { path: 'x', link: false, git: false, processes: [] } }, { atMerge: true });
  assert.equal(half.remove, true);
  assert.equal(tool.gateTreeVerdict({ ...idle, listed: false, folder: { path: 'x', link: false, git: true } }).remove, false);
});


test('two runs write one verdict store under a lock beside it, and a lock a dead writer left is taken', () => {
  const fx = makeFixture('merge-branch-store-lock');
  const verdict = (gate: string): Verdict => ({ gate, verdict: 'PASS', tree: 't', at: '2026-10-18T10:00:00Z', commit: null, branch: null });
  tool.addVerdicts(fx.root, [verdict('first')]);
  const lock = join(fx.root, `${tool.VERDICTS_FILE}.lock`);
  assert.equal(existsSync(lock), false, 'the lock is let go once written');
  // A writer that died mid-write left its lock a minute ago: a write takes milliseconds, so it is no writer's now.
  writeFileSync(lock, '');
  const old = new Date(Date.now() - 60_000);
  utimesSync(lock, old, old);
  tool.addVerdicts(fx.root, [verdict('second')]);
  assert.deepEqual(tool.readVerdicts(fx.root).verdicts.map((one) => one.gate), ['first', 'second']);
  assert.equal(existsSync(lock), false);
  fx.cleanup();
});


test('the gate tree installs from each lockfile when its dependencies are missing or the lockfile changed since', () => {
  const fx = makeFixture('merge-branch-gate-deps');
  assert.deepEqual(tool.staleDependencies(fx.root), [], 'no lockfile, nothing to install');
  fx.write('package-lock.json', '{"lockfileVersion":3}\n');
  assert.deepEqual(tool.staleDependencies(fx.root), ['.'], 'no node_modules');
  mkdirSync(join(fx.root, 'node_modules'), { recursive: true });
  assert.deepEqual(tool.staleDependencies(fx.root), ['.'], 'node_modules no install of this lockfile stamped');
  tool.stampDependencies(fx.root, '.');
  assert.deepEqual(tool.staleDependencies(fx.root), []);
  fx.write('src/Daoris.Web/package-lock.json', '{"lockfileVersion":3}\n');
  assert.deepEqual(tool.staleDependencies(fx.root), ['src/Daoris.Web']);
  fx.write('package-lock.json', '{"lockfileVersion":3,"changed":true}\n');
  assert.deepEqual(tool.staleDependencies(fx.root), ['.', 'src/Daoris.Web'], 'a lockfile changed since its install');
  fx.cleanup();
});
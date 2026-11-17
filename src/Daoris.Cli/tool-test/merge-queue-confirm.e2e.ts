/**
 * The queue's confirm run and its unconfirmed policies end to end.
 */
import { describe, test } from 'node:test';
import { join, delimiter } from 'node:path';
import assert from 'node:assert/strict';
import { writeFileSync, existsSync, readFileSync, mkdirSync } from 'node:fs';
import {
  queueScratch, confirmGate, TRAILER, pause, pollutedDotnet, pathOf, treeGate, TREE, pidsIn, alive,
} from './support/merge-queue-scratch.ts';
import { machine, type ScopeRun, q, type Evidence, tool } from './support/merge-queue-kit.ts';
import { CASES_AT_ONCE } from './support/e2e-repository.ts';

describe("The confirm run and the unconfirmed policies, end to end in a scratch repository", { concurrency: CASES_AT_ONCE }, () => {
  // The confirm run (design §4.5, §5.3 step 3) in the queue's runner, before any base or prefix run.

  /** Two branches queued into one candidate: the runner is held until both are queued, then forms them together. Answers both waits. */
  const queueTogether = async (repo: ReturnType<typeof queueScratch>, names: [string, string]) => {
    const hold = join(repo.root, 'local', 'go');
    for (const name of names) assert.equal((await repo.run([name, '--preview'])).status, 0);
    const first = repo.run([names[0], '--message', `local/${names[0]}.msg`], { MERGE_BRANCH_QUEUE_HOLD: hold });
    await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
    const second = repo.run([names[1], '--message', `local/${names[1]}.msg`], { MERGE_BRANCH_QUEUE_HOLD: hold });
    await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
    writeFileSync(hold, '');
    return [first, second] as const;
  };
  const runsOf = (dir: string) => (existsSync(join(dir, 'runs')) ? readFileSync(join(dir, 'runs'), 'utf8').length : 0);
  const verdictsOf = (repo: { root: string }) => (JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { verdicts: { gate: string; verdict: string; tree: string }[] }).verdicts;

  for (const policy of ['split', 'land', 'hold'] as const) {
    const goes = { split: 'splits, and each member promotes alone', land: 'promotes as it stands, its pass standing for no other tip', hold: 'waits for a decide, which no door gives yet' }[policy];
    test(`a gate that fails once beside a held load and passes quiet holding the whole admission is a sighting; under ${policy}${policy === 'split' ? ', the default with no policy written,' : ''} the candidate ${goes}`, async () => {
      const repo = queueScratch(`confirm-${policy}`);
      const dir = join(repo.root, 'local', 'confirm');
      mkdirSync(dir, { recursive: true });
      // The load: a place in the case's admission this process holds, as a driven session's heavy run holds one beside the queue.
      const load = await machine.createAdmission({ daorisHome: repo.home, capacity: 2, say: () => {} }).take({ what: 'the load beside it' });
      try {
        assert.ok(load, 'the load holds a place');
        if (policy !== 'split') writeFileSync(join(repo.root, 'local', 'integration-policy.json'), JSON.stringify({ unconfirmed: policy }));
        writeFileSync(join(repo.root, 'qgate.mjs'), confirmGate(dir, { failWhen: 'load > 0', admission: machine.admissionFile(repo.home) }));
        repo.git('commit', '--quiet', '-am', `a check that fails beside a heavy load${TRAILER}`);
        const base = repo.git('rev-parse', 'main');
        repo.branch('b1', { 'one.txt': '1\n' });
        repo.branch('b2', { 'two.txt': '2\n' });
        const waits = await queueTogether(repo, ['b1', 'b2']);
        // The confirm run asks every place, so it waits for the load; nothing more of the gate runs while it does.
        await repo.until(() => /waiting for the machine's whole heavy-run admission before the confirm run of check/.test(repo.log()));
        assert.match(repo.log(), /waiting for the machine's whole heavy-run admission before the confirm run of check/, repo.log());
        assert.equal(runsOf(dir), 1, 'no base or prefix run while the confirm run waits');
        await pause(300);
        load!.release();
        let red: ScopeRun | undefined;
        if (policy === 'hold') {
          await repo.until(() => repo.queue()?.candidate?.held?.on === 'decide');
          const said = await repo.run(['--queue']);
          assert.match(said.out, /held on decide: an unconfirmed failure holds the candidate for a decide: check; no decide door exists yet/, `${said.out}\n${repo.log()}`);
          assert.equal(repo.git('rev-parse', 'main'), base, 'nothing promoted while it is held');
          red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red');
          assert.equal(red?.outcome, 'hold');
          assert.equal((await repo.run(['--drop-batch', 'b1'])).status, 0);
          for (const done of await Promise.all(waits)) assert.equal(done.status, 1, done.out);
          assert.match((await waits[0]).out, /the queue is held on decide/);
        } else {
          for (const done of await Promise.all(waits)) assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
          assert.equal(existsSync(join(repo.root, 'one.txt')) && existsSync(join(repo.root, 'two.txt')), true, 'both members are in main');
        }
        const runs = q.readScopeRuns(repo.root) as ScopeRun[];
        red = runs.find((each) => each.state === 'red');
        assert.deepEqual(red?.members.map((each) => each.branch), ['b1', 'b2'], repo.log());
        assert.deepEqual(red?.judged?.map((each) => each.outcome), ['unconfirmed']);
        assert.equal(red?.policy?.unconfirmed, policy);
        // The scope run's record keeps the confirm run: its composition, its admission's evidence and its wait (§11 row b's measures).
        const [confirm] = red!.confirms!;
        assert.deepEqual([confirm!.gate, confirm!.result], ['check', 'unconfirmed']);
        assert.equal(confirm!.admission?.conclusive, true, JSON.stringify(confirm));
        assert.equal(confirm!.admission?.ids?.length, 2, 'every place of the two');
        assert.ok((confirm!.waitedMs ?? 0) >= 300, `it waited for the load: ${confirm!.waitedMs}`);
        assert.equal(confirm!.composition?.units, null, 'the gate ran whole, and ran again whole');
        assert.match(confirm!.composition?.digest ?? '', /^sha256:[0-9a-f]{64}$/);
        // The sighting, apart from the known red and the flakes.
        const sightings = q.readSightings(repo.root);
        assert.equal(sightings.length, 1, JSON.stringify(sightings));
        assert.deepEqual([sightings[0]!['gate'], sightings[0]!['commit'], sightings[0]!['base'], sightings[0]!['policy'], sightings[0]!['members']], ['check', red!.tip, base, policy, ['b1', 'b2']]);
        assert.equal((sightings[0]!['admission'] as Evidence).conclusive, true);
        const findings = join(repo.root, 'local', 'ci-findings.json');
        assert.deepEqual(existsSync(findings) ? (JSON.parse(readFileSync(findings, 'utf8')) as { flakes?: unknown[] }).flakes ?? [] : [], [], 'no flake is filed for it');
        if (policy === 'split') {
          assert.equal(red!.outcome, 'split');
          assert.deepEqual(runs.slice(runs.indexOf(red!) + 1).map((each) => [each.state, each.members.map((member) => member.branch).join()]), [['green', 'b1'], ['green', 'b2']], repo.log());
          assert.equal(runsOf(dir), 4, 'the candidate, its confirm run, then each member alone: no base or prefix run');
        }
        if (policy === 'land') {
          assert.equal(red!.outcome, 'land');
          assert.deepEqual(red!.passedOnConfirm, ['check']);
          assert.equal(runs.indexOf(red!), runs.length - 1, 'no other run: the candidate promoted as it stood');
          assert.equal(repo.git('rev-parse', 'main'), red!.tip, 'main is the candidate itself');
          assert.equal(runsOf(dir), 2);
          assert.match(repo.log(), /lands on its confirm run alone/);
          assert.equal(verdictsOf(repo).some((each) => each.gate === 'check' && each.tree === red!.tree && ['PASS', 'FLAKE'].includes(each.verdict)), false, 'no pass of check stands on its tree');
        }
      } finally {
        load?.release();
        await repo.cleanup();
      }
    });
  }

  test('a pollution within a class fails again as composed: the confirm run repeats the gate as it ran, and the failure is bisected as before', async () => {
    const repo = queueScratch('confirm-polluted');
    const shims = join(repo.root, 'local', 'shims');
    mkdirSync(shims, { recursive: true });
    try {
      const calls = join(repo.root, 'local', 'dotnet-calls.jsonl');
      writeFileSync(join(shims, 'dotnet.mjs'), pollutedDotnet(calls, 'pollute'));
      if (process.platform === 'win32') writeFileSync(join(shims, 'dotnet.cmd'), '@node "%~dp0dotnet.mjs" %*\r\n');
      else writeFileSync(join(shims, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/dotnet.mjs" "$@"\n', { mode: 0o755 });
      // The stand-in is first on the runner's own PATH as it starts: the test process's environment is never changed.
      const PATH = `${shims}${delimiter}${pathOf(repo.env)}`;
      writeFileSync(join(repo.root, 'Fixture.csproj'), '<Project />\n');
      writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'fixture', run: 'dotnet test Fixture.csproj --filter "Category!=Process"' }] }, null, 2)}\n`);
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a test project${TRAILER}`);
      repo.branch('good', { 'good.txt': 'fine\n' });
      repo.branch('bad', { pollute: 'x\n' });
      const hold = join(repo.root, 'local', 'go');
      for (const name of ['good', 'bad']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const good = repo.run(['good', '--message', 'local/good.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold, PATH });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const bad = repo.run(['bad', '--message', 'local/bad.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold, PATH });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [goodOut, badOut] = await Promise.all([good, bad]);
      assert.equal(badOut.status, 1, `${badOut.out}\n${repo.log()}`);
      assert.match(badOut.out, /bad is handed back \(pinned: Fixture\.PollutedTests\.B_Reads\)/);
      assert.equal(goodOut.status, 0, `${goodOut.out}\n${repo.log()}`);
      const red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red')!;
      assert.deepEqual(red.confirms?.map((each) => [each.gate, each.result]), [['fixture', 'confirmed']], repo.log());
      assert.deepEqual(red.judged?.map((each) => [each.outcome, each.member]), [['pinned', 2]]);
      // The confirm run is the gate's run again, its whole selection and settings, never the failing class or test alone.
      const [scoped, confirm, onBase] = readFileSync(calls, 'utf8').trim().split('\n');
      assert.doesNotMatch(confirm!, /FullyQualifiedName~/, 'as composed: not narrowed to the failing class');
      assert.equal(confirm, scoped!.replace('fixture.scoped.trx', 'fixture.confirm.trx'), 'the same command, into a trx of its own');
      assert.match(onBase!, /\(Category!=Process\)&\(FullyQualifiedName~Fixture\.PollutedTests\)/, 'then the failing class on the base, as today');
    } finally {
      await repo.cleanup();
    }
  });

  test('another live hold beside the confirm run makes it inconclusive, read as split even under land: one of one is handed back unresolved, saying so, with no base run', async () => {
    const repo = queueScratch('confirm-beside');
    const dir = join(repo.root, 'local', 'confirm');
    mkdirSync(dir, { recursive: true });
    let beside: { release: () => void } | null = null;
    try {
      writeFileSync(join(repo.root, 'local', 'integration-policy.json'), JSON.stringify({ unconfirmed: 'land' }));
      // The candidate's run fails; its second run, the confirm run, waits for `release` and then passes.
      writeFileSync(join(repo.root, 'qgate.mjs'), confirmGate(dir, { failWhen: 'count === 1', holdWhen: 'count === 2' }));
      repo.git('commit', '--quiet', '-am', `a check that fails once${TRAILER}`);
      repo.branch('b1', { 'one.txt': '1\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => existsSync(join(dir, '2.started')));
      assert.ok(existsSync(join(dir, '2.started')), repo.log());
      // A writer that counts a larger capacity takes a place while the whole admission is held, as an older build's would.
      beside = await machine.createAdmission({ daorisHome: repo.home, capacity: 3, say: () => {} }).take({ what: 'a run beside it' });
      assert.ok(beside, 'it took a place beside the whole admission');
      writeFileSync(join(dir, 'release'), '');
      const done = await waiting;
      assert.equal(done.status, 1, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is handed back \(unresolved: check; its confirm run is inconclusive \(another live hold stands beside it: the merge queue's a run beside it\): read as split, never land/);
      const red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red')!;
      assert.deepEqual(red.judged?.map((each) => each.outcome), ['inconclusive']);
      const [confirm] = red.confirms!;
      assert.equal(confirm!.result, 'inconclusive');
      assert.equal(confirm!.admission?.conclusive, false);
      assert.match(confirm!.admission?.doubts[0]?.why ?? '', /another live hold stands beside it/);
      assert.equal(runsOf(dir), 2, 'the candidate and its confirm run: no base or prefix run');
      assert.deepEqual(q.readSightings(repo.root), [], 'an inconclusive confirm run is no sighting');
    } finally {
      beside?.release();
      writeFileSync(join(dir, 'release'), '');
      await repo.cleanup();
    }
  });

  test('an admission the confirm run cannot write is inconclusive: the gate is not run again, nothing runs on the base, and one of one is handed back unresolved', async () => {
    const repo = queueScratch('confirm-unwritable');
    const dir = join(repo.root, 'local', 'confirm');
    mkdirSync(dir, { recursive: true });
    try {
      // The machine's record is a folder, so no write of it lands.
      mkdirSync(machine.admissionFile(repo.home), { recursive: true });
      writeFileSync(join(repo.root, 'qgate.mjs'), confirmGate(dir, { failWhen: 'true' }));
      repo.git('commit', '--quiet', '-am', `a check that always fails${TRAILER}`);
      repo.branch('b1', { 'one.txt': '1\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const done = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(done.status, 1, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is handed back \(unresolved: check; its confirm run is inconclusive \(the machine's record could not be written/);
      const red = (q.readScopeRuns(repo.root) as ScopeRun[]).find((each) => each.state === 'red')!;
      const [confirm] = red.confirms!;
      assert.deepEqual([confirm!.result, confirm!.admission?.conclusive, confirm!.admission?.doubts[0]?.at], ['inconclusive', false, 'take']);
      assert.equal(runsOf(dir), 1, 'neither a confirm run nor a base run: an admission not shown is never run without waiting');
    } finally {
      await repo.cleanup();
    }
  });

  test('a drop ends a confirm run with its tree, starts no base or prefix run, and lets every place of the whole admission go', async () => {
    const repo = queueScratch('confirm-drop');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    try {
      // The candidate's run of `check` fails; its second, the confirm run, holds with a child and a grandchild.
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, 'count === 2'));
      repo.git('commit', '--quiet', '-am', `a gate that holds its confirm run${TRAILER}`);
      repo.branch('bad', { 'break-check': 'x\n' });
      assert.equal((await repo.run(['bad', '--preview'])).status, 0);
      const report = join(dir, 'report');
      const waiting = repo.run(['bad', '--message', 'local/bad.msg'], { MERGE_BRANCH_DRAIN_HOLD: report });
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      assert.equal(runsOf(dir), 2, 'the candidate ran once, and its confirm run is the one held');
      const holds = () => (JSON.parse(readFileSync(machine.admissionFile(repo.home), 'utf8')) as { holds: { what: string }[] }).holds;
      assert.equal(holds().length, 2, 'the confirm run holds every place');
      const asked = Date.now();
      const dropping = repo.run(['--drop-batch', 'bad']);
      // The runner has ended the tree, and its report is kept late past the drop's thirty seconds on the machine's clock, as
      // a loaded runner's can be: the drop waits on the clock only for the runner to take it, then on what it says.
      // The tree is looked at before the pause, whose half minute lets Windows give its pids to other processes.
      await repo.until(() => pids.every((pid) => !alive(pid)));
      assert.deepEqual(pids.filter(alive), [], 'the confirm run, its child and its grandchild are gone');
      await pause(Math.max(0, asked + tool.DRAIN_WAIT_MS + 5_000 - Date.now()));
      writeFileSync(report, '');
      const dropped = await dropping;
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /the probe check\.confirm/);
      assert.equal((await waiting).status, 1);
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(repo.queue()?.runner, null, repo.log());
      assert.equal(runsOf(dir), 2, 'no base or prefix run after the drop');
      assert.match(repo.log(), /was dropped or replaced: ended no gate running, the probe check\.confirm/);
      const last = (q.readScopeRuns(repo.root) as ScopeRun[]).at(-1)!;
      assert.deepEqual([last.state, last.drained?.probe, last.judged], ['ended', 'check.confirm', undefined]);
      assert.deepEqual(holds(), [], 'every place of the whole admission let go');
      assert.deepEqual(q.readSightings(repo.root), []);
    } finally {
      writeFileSync(join(dir, 'release'), '');
      writeFileSync(join(dir, 'report'), '');
      await repo.cleanup();
    }
  });
});

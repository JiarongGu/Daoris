import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { tool, type Verdict, type GateResult, publish, type CiStatus } from './support/merge-branch.ts';
import { git, TRAILER, CI_GATE, unordered, scratch } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row

describe('the tool, end to end in a scratch repository (e2e-ci)', { concurrency: true }, () => {

  // A release's verification runs every gate at publish time when nothing verified main between.
  test("--ci gates main's tip in the gate tree: every gate at first, then what the paths since reach; a failure stays owed; its status and logs are the main checkout's, and the stage reads its verdicts", async () => {
    const repo = scratch('ci', [
      { name: 'universal', run: 'node ci-gate.mjs universal' },
      { name: 'service', run: 'node ci-gate.mjs service' },
      { name: 'tool-tests', run: 'node ci-gate.mjs tool-tests' },
    ], { 'ci-gate.mjs': CI_GATE, 'docs/a.md': 'a\n' });
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const ranFile = join(gateTree, 'local', 'ran.txt');
    const ranThere = () => (existsSync(ranFile) ? unordered(readFileSync(ranFile, 'utf8').split('\n').filter(Boolean)) : []);
    const statusOf = (sha: string) => JSON.parse(readFileSync(join(repo.root, 'local', 'scratch', `ci-${sha.slice(0, 7)}`, 'status.json'), 'utf8')) as CiStatus;
    const states = (status: CiStatus) => status.gates.map((gate) => `${gate.name} ${gate.state} ${gate.verdict}`);
    const commit = (path: string, text: string) => {
      mkdirSync(dirname(join(repo.root, path)), { recursive: true });
      writeFileSync(join(repo.root, path), text);
      git(repo.root, 'add', '-A');
      git(repo.root, 'commit', '--quiet', '-m', `change ${path}${TRAILER}`);
      rmSync(ranFile, { force: true });
      return git(repo.root, 'rev-parse', 'HEAD');
    };
    const first = git(repo.root, 'rev-parse', 'HEAD');

    // The first run: no gate has a verdict, so every gate runs.
    let result = await repo.run(['--ci']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(ranThere(), ['service', 'tool-tests', 'universal']);
    assert.match(result.out, new RegExp(`the gate tree: made at ${first.slice(0, 7)} \\(local/gate-tree\\)`));
    assert.match(result.out, /no green CI run is recorded here/);
    assert.match(result.out, new RegExp(`PASS\\s+service\\s+.*local/scratch/ci-${first.slice(0, 7)}/service\\.log`), 'its logs are named from the main checkout');
    assert.match(result.out, new RegExp(`merge-branch: CI green on ${first.slice(0, 7)}`));
    assert.match(result.out, new RegExp(`npm run publish:desktop -- --to <install> --service --stage --from-gated ${first.slice(0, 7)}`));
    assert.match(result.out, new RegExp(`status: local/scratch/ci-${first.slice(0, 7)}/status\\.json`));
    let status = statusOf(first);
    assert.deepEqual([status.state, status.commit, status.tree, status.pid > 0, typeof status.ended], ['green', first, git(repo.root, 'rev-parse', `${first}^{tree}`), true, 'string']);
    assert.deepEqual(states(status), ['universal done PASS', 'service done PASS', 'tool-tests done PASS']);
    assert.match(readFileSync(join(repo.root, status.gates[1]!.log!), 'utf8'), /service line 3/);
    assert.equal(status.light.width > 0 && /MERGE_BRANCH_MEMORY=off/.test(status.light.why), true, status.light.why);
    const record = () => JSON.parse(readFileSync(join(repo.root, 'local', 'ci.json'), 'utf8')) as { lastGreen: { commit: string } | null; runs: { commit: string; state: string }[] };
    assert.equal(record().lastGreen!.commit, first);
    assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');
    assert.deepEqual(git(repo.root, 'status', '--porcelain'), '', 'the main checkout is untouched: only its ignored local/ was written');

    // A change to the docs reaches the service suite and the baseline: the tool tests keep their verdict.
    const second = commit('docs/a.md', 'changed\n');
    result = await repo.run(['--ci']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(ranThere(), ['service', 'universal']);
    assert.match(result.out, new RegExp(`since the last green run \\(${first.slice(0, 7)}, [^)]+\\): 1 path changed`));
    assert.match(result.out, /tool-tests\s+PASS .*on a tree that differs from this one only by paths it does not reach \(docs\/a\.md\)/);
    status = statusOf(second);
    assert.deepEqual(states(status), ['universal done PASS', 'service done PASS', 'tool-tests kept PASS']);
    assert.deepEqual(status.gates[2]!.kept, { commit: first, tree: git(repo.root, 'rev-parse', `${first}^{tree}`), standing: 'unreached' });
    assert.equal(status.gates[2]!.log, `local/scratch/ci-${first.slice(0, 7)}/tool-tests.log`);
    assert.deepEqual([status.since.commit, status.since.paths], [first, 1]);

    // The stage asks the gate tree, which holds what the CI gated: its verdicts pass it, the kept one among them.
    result = await repo.run(['--passed'], {}, gateTree);
    assert.equal(result.status, 0, result.out);
    assert.equal(publish.ungatedRefusal(join(dirname(repo.root), 'install'), gateTree).refusal, null);
    // Each run leaves its receipt, by tree, with its kind and each gate's verdict and the kind that gave it.
    const receipts = () => readFileSync(join(repo.root, 'local', 'ci-runs.jsonl'), 'utf8').split('\n').filter(Boolean)
      .map((line) => JSON.parse(line) as { commit: string; tree: string; kind: string; finished: boolean; state: string; gates: { name: string; state: string; verdict: string | null; kind: string | null }[] });
    assert.deepEqual(receipts().map((each) => [each.commit, each.tree, each.kind, each.finished, each.state]), [
      [first, git(repo.root, 'rev-parse', `${first}^{tree}`), 'ci', true, 'green'],
      [second, git(repo.root, 'rev-parse', `${second}^{tree}`), 'ci', true, 'green'],
    ]);
    assert.deepEqual(receipts()[1]!.gates.map((gate) => `${gate.name} ${gate.state} ${gate.verdict} ${gate.kind}`), ['universal done PASS ci', 'service done PASS ci', 'tool-tests kept PASS ci']);
    const verdictKinds = (JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { verdicts: Verdict[] }).verdicts.map((each) => (each as Verdict & { kind?: string }).kind);
    assert.deepEqual([...new Set(verdictKinds)], ['ci'], 'every verdict carries the kind of run that gave it');

    // Again on the same commit: nothing is owed and nothing runs.
    rmSync(ranFile, { force: true });
    result = await repo.run(['--ci', second.slice(0, 7)]);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(ranThere(), []);
    assert.match(result.out, /nothing to run: every gate's verdict stands on this tree/);

    // A failure: the run goes on to every gate, says it, and the stage refuses the commit.
    const third = commit('docs/b.md', 'b\n');
    result = await repo.run(['--ci'], { CI_FAIL: 'universal' });
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(ranThere(), ['service', 'universal'], 'a CI run keeps going past a failure');
    assert.match(result.out, new RegExp(`merge-branch: CI did NOT pass ${third.slice(0, 7)}: universal failed`));
    assert.equal(statusOf(third).state, 'failed');
    assert.equal(record().lastGreen!.commit, second, 'the last green run is still the one before');
    result = await repo.run(['--passed'], {}, gateTree);
    assert.equal(result.status, 1, 'a failed commit is still refused at the stage');
    // The failure is filed as known red, by its gate's step where it names no test, first seen on this commit.
    type Filed = { gate: string; test: string; state: string; firstSeen: { commit: string }; lastSeen: { commit: string }; cleared: { commit: string; kind: string } | null };
    const findings = () => (JSON.parse(readFileSync(join(repo.root, 'local', 'ci-findings.json'), 'utf8')) as { findings: Filed[] }).findings;
    assert.deepEqual(findings().map((each) => [each.gate, each.test, each.state, each.firstSeen.commit]), [['universal', 'universal', 'red', third]]);
    assert.match(result.out, /universal\s+FAIL/);
    assert.match(readFileSync(join(repo.root, 'local', 'scratch', `ci-${third.slice(0, 7)}`, 'status.json'), 'utf8'), /"knownRed"/);

    // A change that reaches only the tool tests: the failed gate runs with it, owed whatever the change.
    const fourth = commit('tools/merge-branch.mjs', '// the scratch repository\'s own\n');
    result = await repo.run(['--ci']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(ranThere(), ['tool-tests', 'universal']);
    assert.deepEqual(states(statusOf(fourth)), ['universal done PASS', 'service kept PASS', 'tool-tests done PASS']);
    // Its pass on a commit that descends from the commit it last failed on clears it.
    assert.deepEqual(findings().map((each) => [each.state, each.cleared?.commit, each.cleared?.kind]), [['cleared', fourth, 'ci']]);
    assert.match(result.out, /known red \(local\/ci-findings\.json\): 0 filed, 1 cleared by this run; 0 open/);
    // A late run on the older, failing commit, which fourth does not precede: its pass clears nothing, and its failure is
    // the same signature seen within a day of the clearing, which restores the finding with its first sighting.
    rmSync(ranFile, { force: true });
    result = await repo.run(['--ci', third.slice(0, 7)], { CI_FAIL: 'universal' });
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(findings().map((each) => [each.state, each.firstSeen.commit, each.lastSeen.commit]), [['red', third, third]]);
    assert.deepEqual(record().runs.map((run) => `${run.commit.slice(0, 7)} ${run.state}`),
      [first, second, second, third, fourth, third].map((sha, i) => `${sha.slice(0, 7)} ${i === 3 || i === 5 ? 'failed' : 'green'}`));
    repo.cleanup();
  });

  // A stage CI keeps scoped passes of two gates and leaves their older findings open.
  test('--ci clears a gate\'s older finding from the pass it kept on this tree, with the kept verdict\'s provenance; a pass that does not stand on this tree clears nothing', async () => {
    const repo = scratch('ci-keep', [
      { name: 'universal', run: 'node ci-gate.mjs universal' },
      { name: 'service', run: 'node ci-gate.mjs service' },
    ], { 'ci-gate.mjs': CI_GATE, 'docs/a.md': 'a\n' });
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const ranFile = join(gateTree, 'local', 'ran.txt');
    const ranThere = () => (existsSync(ranFile) ? unordered(readFileSync(ranFile, 'utf8').split('\n').filter(Boolean)) : []);
    const commit = (path: string, text: string) => {
      writeFileSync(join(repo.root, path), text);
      git(repo.root, 'add', '-A');
      git(repo.root, 'commit', '--quiet', '-m', `change ${path}${TRAILER}`);
      rmSync(ranFile, { force: true });
      return git(repo.root, 'rev-parse', 'HEAD');
    };
    type Filed = { gate: string; state: string; lastSeen: { commit: string }; cleared: { commit: string; kind: string; kept?: { commit: string; tree: string; kind: string } } | null };
    const findings = () => (JSON.parse(readFileSync(join(repo.root, 'local', 'ci-findings.json'), 'utf8')) as { findings: Filed[] }).findings;
    const verdictsFile = join(repo.root, 'local', 'gate-verdicts.json');
    const keep = (entry: object) => {
      const store = JSON.parse(readFileSync(verdictsFile, 'utf8')) as { verdicts: object[] };
      store.verdicts.push(entry);
      writeFileSync(verdictsFile, `${JSON.stringify(store, null, 2)}\n`);
    };
    try {
      let result = await repo.run(['--ci']);
      assert.equal(result.status, 0, result.out);
      const second = commit('docs/a.md', 'b\n');
      result = await repo.run(['--ci'], { CI_FAIL: 'universal' });
      assert.equal(result.status, 1, result.out);
      assert.deepEqual(findings().map((each) => [each.gate, each.state, each.lastSeen.commit]), [['universal', 'red', second]]);

      // A kept pass on another tree, a stale one, clears nothing: the gate runs and passes, and that clears it as it did before.
      commit('docs/a.md', 'c\n');
      const scopedTip = 'f'.repeat(40);
      keep({ gate: 'universal', verdict: 'PASS', tree: git(repo.root, 'rev-parse', `${second}^{tree}`), at: new Date().toISOString(), commit: scopedTip, branch: null, kind: 'scoped' });
      result = await repo.run(['--ci'], { CI_FAIL: 'universal' });
      assert.equal(result.status, 1, result.out);
      assert.equal(findings()[0]!.state, 'red');

      // A scoped exact pass of the same gate on the tree of the next commit: the run keeps it, runs nothing of that gate, and clears
      // the finding on its own commit, recording the kept verdict's.
      const fourth = commit('docs/a.md', 'd\n');
      const fourthTree = git(repo.root, 'rev-parse', `${fourth}^{tree}`);
      keep({ gate: 'universal', verdict: 'PASS', tree: fourthTree, at: new Date().toISOString(), commit: scopedTip, branch: null, kind: 'scoped' });
      result = await repo.run(['--ci']);
      assert.equal(result.status, 0, result.out);
      assert.equal(ranThere().includes('universal'), false, 'the kept pass stands, so the gate does not run');
      assert.equal(findings()[0]!.state, 'cleared');
      assert.deepEqual([findings()[0]!.cleared!.commit, findings()[0]!.cleared!.kind], [fourth, 'ci']);
      assert.deepEqual([findings()[0]!.cleared!.kept!.commit, findings()[0]!.cleared!.kept!.tree, findings()[0]!.cleared!.kept!.kind], [scopedTip, fourthTree, 'scoped']);
      assert.match(result.out, /known red \(local\/ci-findings\.json\): 0 filed, 1 cleared by this run \(of them, 1 from kept proof\); 0 open/);
    } finally {
      repo.cleanup();
    }
  });

  // A CI run takes 45 to 75 minutes, the family and deployment rehearsals 14 to 23 and 14 to 22 of it.
  test('--ci owes the family and deployment rehearsals a stage, running neither; --ci --stage runs them on the commit to stage, one after the other, and the stage asks their pass', async () => {
    const repo = scratch('ci-stage', [
      { name: 'universal', run: 'node ci-gate.mjs universal' },
      { name: 'rehearse-family', run: 'npm run rehearse:family' },
      { name: 'deployment', run: 'npm run rehearse:deploy' },
    ], {
      'ci-gate.mjs': CI_GATE,
      // A rehearsal stand-in: it notes its start and its end, so two that overlapped would show it.
      'rehearsal.mjs': [
        "import { appendFileSync, mkdirSync } from 'node:fs';",
        'const name = process.argv[2];',
        "mkdirSync('local', { recursive: true });",
        "appendFileSync('local/rehearsals.txt', `${name} start\\n`);",
        'await new Promise((done) => setTimeout(done, 300));',
        "appendFileSync('local/rehearsals.txt', `${name} end\\n`);",
        'console.log(`${name} rehearsed`);',
        "process.exit((process.env.CI_FAIL ?? '').split(',').includes(name) ? 1 : 0);",
        '',
      ].join('\n'),
      'package.json': `${JSON.stringify({ name: 'scratch', private: true, scripts: { 'rehearse:family': 'node rehearsal.mjs rehearse-family', 'rehearse:deploy': 'node rehearsal.mjs deployment' } }, null, 2)}\n`,
    });
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const read = (file: string) => (existsSync(join(gateTree, 'local', file)) ? readFileSync(join(gateTree, 'local', file), 'utf8').split('\n').filter(Boolean) : []);
    const clear = () => { for (const file of ['ran.txt', 'rehearsals.txt']) rmSync(join(gateTree, 'local', file), { force: true }); };
    const statusOf = (sha: string) => JSON.parse(readFileSync(join(repo.root, 'local', 'scratch', `ci-${sha.slice(0, 7)}`, 'status.json'), 'utf8')) as CiStatus;
    const states = (status: CiStatus) => status.gates.map((gate) => `${gate.name} ${gate.state} ${gate.verdict}`);
    const record = () => JSON.parse(readFileSync(join(repo.root, 'local', 'ci.json'), 'utf8')) as { lastGreen: { commit: string } | null; runs: { commit: string; state: string; at: string; stage?: boolean; owed?: string[] }[] };
    const tip = git(repo.root, 'rev-parse', 'HEAD');
    const sha = tip.slice(0, 7);
    try {
      // A plain run: the rehearsals are owed, not run; it is green on the rest, and says what pays them.
      let result = await repo.run(['--ci']);
      assert.equal(result.status, 0, result.out);
      assert.deepEqual([read('ran.txt'), read('rehearsals.txt')], [['universal'], []]);
      assert.match(result.out, /1 of 3 gates to run, 0 verdicts kept, which still stand on this tree, rehearse-family and deployment owed for a stage/);
      assert.match(result.out, new RegExp(`owed, not run \\(CIFAST1\\): rehearse-family, deployment, which node tools/merge-branch\\.mjs --ci --stage ${sha} runs before a stage`));
      assert.match(result.out, new RegExp(`CI green on ${sha}: 1 gate run and passed, 0 verdicts kept; rehearse-family and deployment owed for a stage, neither failed nor passed`));
      assert.doesNotMatch(result.out, /Stage it from the gate tree/);
      let status = statusOf(tip);
      assert.deepEqual([status.state, status.stage, status.owed], ['green', false, ['rehearse-family', 'deployment']]);
      assert.deepEqual(states(status), ['universal done PASS', 'rehearse-family owed null', 'deployment owed null']);
      assert.deepEqual(record().runs.at(-1), { commit: tip, state: 'green', at: record().runs.at(-1)!.at, status: `local/scratch/ci-${sha}/status.json`, kind: 'ci', owed: ['rehearse-family', 'deployment'] });
      // The stage asks their pass, and a plain run gave none.
      result = await repo.run(['--passed'], {}, gateTree);
      assert.equal(result.status, 1, result.out);
      assert.match(result.out, /rehearse-family\s+none/);

      // The stage's path, failing: it runs both on this commit, one after the other, and the stage still refuses.
      clear();
      result = await repo.run(['--ci', '--stage'], { CI_FAIL: 'deployment' });
      assert.equal(result.status, 1, result.out);
      assert.match(result.out, new RegExp(`CI on ${sha} \\(CI1, for a stage: CIFAST1\\): 2 of 3 gates to run, 1 verdict kept`));
      assert.deepEqual(read('rehearsals.txt'), ['rehearse-family start', 'rehearse-family end', 'deployment start', 'deployment end'], 'one after the other, never side by side');
      assert.equal(read('ran.txt').length, 0, 'the light gate\'s pass stands');
      assert.match(result.out, /CI did NOT pass .*: deployment failed/);
      assert.equal((await repo.run(['--passed'], {}, gateTree)).status, 1);
      // A plain run after it owes the failed one again, runs nothing, and never reads it as passed.
      clear();
      result = await repo.run(['--ci']);
      assert.equal(result.status, 0, result.out);
      assert.deepEqual(read('rehearsals.txt'), []);
      assert.deepEqual(states(statusOf(tip)), ['universal kept PASS', 'rehearse-family kept PASS', 'deployment owed null']);
      assert.equal((await repo.run(['--passed'], {}, gateTree)).status, 1, 'the stage still refuses it');

      // The stage's path, passing: what it lacks runs, and the stage stands.
      clear();
      result = await repo.run(['--ci', '--stage', sha]);
      assert.equal(result.status, 0, result.out);
      assert.deepEqual(read('rehearsals.txt'), ['deployment start', 'deployment end']);
      assert.match(result.out, new RegExp(`CI green on ${sha}: 1 gate run and passed, 2 verdicts kept; every gate's verdict stands on this tree`));
      assert.match(result.out, new RegExp(`--stage --from-gated ${sha}`));
      status = statusOf(tip);
      assert.deepEqual([status.state, status.stage, status.owed], ['green', true, []]);
      assert.deepEqual(record().runs.at(-1)!.stage, true);
      result = await repo.run(['--passed'], {}, gateTree);
      assert.equal(result.status, 0, result.out);
      // The stage's path leaves a receipt of its kind on this tree, which the stage counts.
      const receipts = readFileSync(join(repo.root, 'local', 'ci-runs.jsonl'), 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line) as { kind: string; state: string; finished: boolean });
      assert.deepEqual(receipts.map((each) => `${each.kind} ${each.state} ${each.finished}`), ['ci green true', 'stage failed true', 'ci green true', 'stage green true']);
      assert.match(result.out, /receipt\s+a stage run finished on this commit's tree/);
      // Their passes stand for a plain run after: nothing is owed or run.
      clear();
      result = await repo.run(['--ci']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /nothing to run/);
      assert.deepEqual(statusOf(tip).owed, []);

      // The night runs every gate whole, the rehearsals included, whatever stands, and a finished one satisfies it.
      clear();
      result = await repo.run(['--ci', '--night']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, new RegExp(`CI on ${sha} \\(CI1, the nightly run: every gate whole \\(CISCOPE1d\\)\\): 3 of 3 gates to run, 0 verdicts kept`));
      assert.deepEqual([read('ran.txt'), read('rehearsals.txt')], [['universal'], ['rehearse-family start', 'rehearse-family end', 'deployment start', 'deployment end']]);
      assert.match(result.out, /the night is satisfied: this nightly run finished/);
      const sweep = JSON.parse(readFileSync(join(repo.root, 'local', 'ci-sweep.json'), 'utf8')) as { night: { commit: string } | null };
      assert.equal(sweep.night?.commit, tip);
      assert.equal(statusOf(tip).kind, 'nightly');
      // A red night that finished satisfies it too, its failure filed (a stopped one is `nightSatisfied`'s, ci-background.test.ts).
      clear();
      result = await repo.run(['--ci', '--night'], { CI_FAIL: 'universal' });
      assert.equal(result.status, 1, result.out);
      assert.match(result.out, /the night is satisfied: this nightly run finished/);
      assert.match(result.out, /known red \(local\/ci-findings\.json\): 1 filed/);
    } finally {
      repo.cleanup();
    }
  });

  // A stage needs every gate green, so its first failed gate ends the run; a plain run keeps going for the whole picture.
  test('--ci --stage ends at the first failed gate: the next never starts, the lock is let go, and it says nothing was staged; a plain --ci runs all three', async () => {
    const repo = scratch('ci-stagefail', [
      { name: 'universal', run: 'node ci-gate.mjs universal' },
      { name: 'service', run: 'node ci-gate.mjs service' },
      { name: 'tool-tests', run: 'node ci-gate.mjs tool-tests' },
    ], { 'ci-gate.mjs': CI_GATE, 'docs/a.md': 'a\n' });
    try {
      const gateTree = join(repo.root, 'local', 'gate-tree');
      const ranFile = join(gateTree, 'local', 'ran.txt');
      const ranThere = () => (existsSync(ranFile) ? unordered(readFileSync(ranFile, 'utf8').split('\n').filter(Boolean)) : []);
      const sha = git(repo.root, 'rev-parse', 'HEAD').slice(0, 7);
      let result = await repo.run(['--ci', '--stage', '--one-at-a-time'], { CI_FAIL: 'service' });
      assert.equal(result.status, 1, result.out);
      assert.deepEqual(ranThere(), ['service', 'universal'], 'the gate after the failed one never started');
      assert.match(result.out, /CI did NOT pass .*: service failed/);
      assert.match(result.out, /ended at the first failed gate \(CISTAGEFAIL1\): service failed; not run: tool-tests; nothing was staged/);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');
      const status = JSON.parse(readFileSync(join(repo.root, 'local', 'scratch', `ci-${sha}`, 'status.json'), 'utf8')) as CiStatus;
      assert.equal(status.state, 'failed');
      assert.deepEqual(status.gates.map((gate) => `${gate.name} ${gate.state}`), ['universal done', 'service done', 'tool-tests not run']);

      // The same plan without --stage keeps going past the failure.
      rmSync(ranFile, { force: true });
      result = await repo.run(['--ci', '--one-at-a-time'], { CI_FAIL: 'service' });
      assert.equal(result.status, 1, result.out);
      assert.deepEqual(ranThere(), ['service', 'tool-tests'], 'a plain run runs what is owed: the failed gate and the one never run');
      assert.doesNotMatch(result.out, /ended at the first failed gate/);
    } finally {
      repo.cleanup();
    }
  });

  test('a failure ends a stage run only when it failed again alone: a sighting that passed alone, and a gate ended with the run, do not', () => {
    const gate = { name: 'g', run: 'x' };
    assert.equal(tool.endsStageRun({ gate, verdict: 'FAIL' } as GateResult), true);
    assert.equal(tool.endsStageRun({ gate, verdict: 'FLAKE', flakes: ['t'] } as GateResult), false);
    assert.equal(tool.endsStageRun({ gate, verdict: 'FAIL', again: true } as GateResult), false, 'one that died of memory runs again at the end');
    assert.equal(tool.endsStageRun({ gate, verdict: 'ENDED' } as GateResult), false);
    assert.equal(tool.endsStageRun({ gate, verdict: 'PASS' } as GateResult), false);
  });
});

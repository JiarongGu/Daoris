import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join, resolve } from 'node:path';
import { tool, type Verdict, type ClassVerdict, type CiStatus, type CiGate } from './support/merge-branch.ts';
import { git, TRAILER, CI_GATE, FAKE_DOTNET_REACH, DESK_FILES, waitFor, scratch } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row

describe('the tool, end to end in a scratch repository (e2e-ci-more)', { concurrency: true }, () => {

  test('--ci runs a Process half by reach:the classes a change reaches, the rest kept and named; a file every class reads, every class; one read unnamed, the whole half; a sweep past its bound before a stage', async () => {
    const repo = scratch('ci-reach', [
      { name: 'universal', run: 'node ci-gate.mjs universal' },
      { name: 'desk-process', run: 'dotnet test src/Desk/Desk.Tests --settings src/Desk/process.runsettings' },
    ], { 'ci-gate.mjs': CI_GATE, ...DESK_FILES });
    const reachBin = join(dirname(repo.root), 'reach-bin');
    mkdirSync(reachBin, { recursive: true });
    writeFileSync(join(reachBin, 'fake-dotnet.mjs'), FAKE_DOTNET_REACH);
    if (process.platform === 'win32') writeFileSync(join(reachBin, 'dotnet.cmd'), '@echo off\r\nnode "%~dp0fake-dotnet.mjs" %*\r\n');
    else {
      writeFileSync(join(reachBin, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/fake-dotnet.mjs" "$@"\n');
      chmodSync(join(reachBin, 'dotnet'), 0o755);
    }
    const env = { PATH: `${reachBin}${delimiter}${process.env['PATH'] ?? process.env['Path'] ?? ''}` };
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const ranFile = join(gateTree, 'local', 'ran.txt');
    const dotnetRan = () => (existsSync(ranFile) ? readFileSync(ranFile, 'utf8').split('\n').filter((line) => line.startsWith('dotnet-test')) : []);
    type ReachGate = CiGate & { classes?: { census: number | null; run: { name: string; why: string }[]; kept: { name: string; at: string; commit: string | null; standing: string }[]; whole: string | null; sweep: string | null } };
    const halfOf = (sha: string) => (JSON.parse(readFileSync(join(repo.root, 'local', 'scratch', `ci-${sha.slice(0, 7)}`, 'status.json'), 'utf8')) as CiStatus)
      .gates.find((gate) => gate.name === 'desk-process') as ReachGate;
    const commit = (path: string, text: string) => {
      writeFileSync(join(repo.root, path), text);
      git(repo.root, 'add', '-A');
      git(repo.root, 'commit', '--quiet', '-m', `change ${path}${TRAILER}`);
      rmSync(ranFile, { force: true });
      return git(repo.root, 'rev-parse', 'HEAD');
    };
    const verdictsFile = join(repo.root, 'local', 'gate-verdicts.json');
    const halfVerdicts = () => (JSON.parse(readFileSync(verdictsFile, 'utf8')) as { verdicts: Verdict[] }).verdicts.filter((each) => each.gate === 'desk-process');
    const first = git(repo.root, 'rev-parse', 'HEAD');

    // The first run: no class has a verdict, so the half runs whole, and each class's verdict is kept with its tree.
    let result = await repo.run(['--ci'], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), ['dotnet-test AlphaTests BetaTests LonelyTests']);
    assert.match(result.out, /desk-process: the whole half runs \(3 classes\): no class has a pass that stands on this tree/);
    let half = halfOf(first);
    assert.deepEqual([half.state, half.verdict, half.classes!.census, half.classes!.run.length, half.classes!.kept.length], ['done', 'PASS', 3, 3, 0]);
    assert.deepEqual(halfVerdicts().at(-1)!.classes, { ran: 3, kept: 0, sweep: halfVerdicts().at(-1)!.at });
    const store = () => tool.readClassVerdicts(repo.root).gates['desk-process'] ?? {};
    assert.deepEqual(Object.keys(store()).sort(), ['Desk.Tests.AlphaTests', 'Desk.Tests.BetaTests', 'Desk.Tests.LonelyTests']);

    // One area changed: its class and the planted class with no reach run; the rest is kept, named with its verdict.
    const second = commit('src/Desk/Desk.Core/Alpha/AlphaThing.cs', 'namespace Desk;\npublic sealed class AlphaThing { public int Two => 2; }\n');
    result = await repo.run(['--ci'], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), ['dotnet-test AlphaTests LonelyTests']);
    assert.match(result.out, /desk-process: 2 of 3 classes run by reach \(AlphaTests, LonelyTests\); 1 kept \(BetaTests\), each with a pass that stands on this tree/);
    half = halfOf(second);
    assert.deepEqual(half.classes!.run, [
      { name: 'Desk.Tests.AlphaTests', why: 'src/Desk/Desk.Core/Alpha/AlphaThing.cs changed since its pass, which reaches it' },
      { name: 'Desk.Tests.LonelyTests', why: 'src/Desk/Desk.Core/Alpha/AlphaThing.cs changed since its pass, which reaches it' },
    ]);
    assert.deepEqual(half.classes!.kept.map((kept) => `${kept.name} ${kept.commit} ${kept.standing}`), [`Desk.Tests.BetaTests ${first} unreached`]);
    assert.equal(halfVerdicts().at(-1)!.classes!.kept, 1);
    const narrowed = readFileSync(join(repo.root, 'local', 'scratch', `ci-${second.slice(0, 7)}`, 'desk-process.process.runsettings'), 'utf8');
    assert.match(narrowed, /<TestCaseFilter>\(Category=Process\)&amp;FullyQualifiedName!~Desk\.Tests\.BetaTests\.<\/TestCaseFilter>/, 'the half\'s filter ANDed with the kept class kept out');
    result = await repo.run(['--passed'], env, gateTree);
    assert.equal(result.status, 0, result.out);

    // The stage refuses a half that kept classes on verdicts older than the bound, and the next CI run sweeps it whole.
    const aged = (iso: string) => new Date(Date.parse(iso) - 2 * tool.SWEEP_BOUND_MS).toISOString();
    const verdicts = JSON.parse(readFileSync(verdictsFile, 'utf8')) as { verdicts: Verdict[] };
    for (const entry of verdicts.verdicts) if (entry.classes) entry.classes.sweep = aged(entry.classes.sweep);
    writeFileSync(verdictsFile, JSON.stringify(verdicts));
    const classFile = join(repo.root, tool.CLASS_VERDICTS_FILE);
    const classStore = JSON.parse(readFileSync(classFile, 'utf8')) as { gates: Record<string, Record<string, ClassVerdict[]>> };
    for (const entries of Object.values(classStore.gates['desk-process']!)) for (const entry of entries) entry.at = aged(entry.at);
    writeFileSync(classFile, JSON.stringify(classStore));
    result = await repo.run(['--passed'], env, gateTree);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /desk-process\s+swept\s+PASS .*kept 1 class on verdicts from .*, older than the 24 hours a sweep stands/);
    rmSync(ranFile, { force: true });
    result = await repo.run(['--ci', second.slice(0, 7)], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), ['dotnet-test AlphaTests BetaTests LonelyTests']);
    assert.match(result.out, /desk-process: the whole half runs \(3 classes\): its oldest kept class verdict \(Desk\.Tests\.BetaTests, [^)]+\) is older than 24 hours: a sweep runs the whole half/);
    result = await repo.run(['--passed'], env, gateTree);
    assert.equal(result.status, 0, result.out);

    // The due record holds the half's last sweep, its whole run's; past its bound the stage refuses, and the stage's
    // own run runs the half whole though its pass stands, which satisfies it again.
    const sweepFile = join(repo.root, 'local', 'ci-sweep.json');
    const dueRecord = JSON.parse(readFileSync(sweepFile, 'utf8')) as { halves: Record<string, { last: string; commit: string; kind: string }> };
    assert.deepEqual([dueRecord.halves['desk-process']!.commit, dueRecord.halves['desk-process']!.kind], [second, 'ci']);
    dueRecord.halves['desk-process']!.last = aged(dueRecord.halves['desk-process']!.last);
    writeFileSync(sweepFile, JSON.stringify(dueRecord));
    result = await repo.run(['--passed'], env, gateTree);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /not staged\s+desk-process's sweep is overdue \(local\/ci-sweep\.json\): last run whole/);
    assert.match(result.out, new RegExp(`Run the stage's own CI run on it: node tools/merge-branch\\.mjs --ci --stage ${second.slice(0, 7)}`));
    rmSync(ranFile, { force: true });
    result = await repo.run(['--ci', '--stage', second.slice(0, 7)], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), ['dotnet-test AlphaTests BetaTests LonelyTests']);
    assert.match(result.out, /due a sweep \(local\/ci-sweep\.json\): desk-process, run whole whatever its reach/);
    assert.match(result.out, /desk-process: the whole half runs \(3 classes\): its sweep is due \(ci-sweep\.json\)/);
    assert.match(result.out, /swept \(local\/ci-sweep\.json\): desk-process, each class given a verdict/);
    result = await repo.run(['--passed'], env, gateTree);
    assert.equal(result.status, 0, result.out);

    // A helper every class reads reaches every class: the whole half. So does a file read unnamed (a global using).
    commit('src/Desk/Desk.Tests/Shared.cs', 'namespace Desk.Tests;\ninternal static class Shared { public const int Seed = 2; }\n');
    result = await repo.run(['--ci'], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), ['dotnet-test AlphaTests BetaTests LonelyTests']);
    assert.match(result.out, /desk-process: the whole half runs \(3 classes\): no class has a pass that stands on this tree/);
    const fifth = commit('src/Desk/Desk.Tests/GlobalUsings.cs', 'global using Xunit;\nglobal using System.IO;\n');
    result = await repo.run(['--ci'], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), ['dotnet-test AlphaTests BetaTests LonelyTests']);
    assert.match(halfOf(fifth).classes!.whole ?? '', /src\/Desk\/Desk\.Tests\/GlobalUsings\.cs acts on every test unnamed/);

    // A fixture no class names reaches the gate and none of its classes; the last pass, whole, went stale by it
    // alone, so the half passes by its kept classes without running, and the stage takes that pass.
    const sixth = commit('src/Desk/Desk.Tests/unused.txt', 'nothing reads this\n');
    result = await repo.run(['--ci'], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), [], 'no class ran');
    assert.match(result.out, /desk-process: passes without running \(CISCOPE1b\): all 3 classes kept, each with a pass that stands on this tree, the oldest from [^;]+; its last verdict, a pass on [^,]+, went stale by reach alone \(src\/Desk\/Desk\.Tests\/unused\.txt\), which reaches none of its classes/);
    assert.match(result.out, /CI on \w+ \(CI1\): 1 of 2 gates to run[\s\S]*CI green on \w+: 1 gate run and passed, 1 passed by its kept classes \(desk-process\)/);
    half = halfOf(sixth);
    assert.deepEqual([half.state, half.verdict, half.ms, half.classes!.run, half.classes!.kept.length], ['done', 'PASS', 0, [], 3]);
    assert.match(half.note ?? '', /^passed without running \(CISCOPE1b\): /);
    assert.deepEqual(halfVerdicts().at(-1)!.classes, { ran: 0, kept: 3, sweep: halfVerdicts().at(-2)!.at });
    result = await repo.run(['--passed'], env, gateTree);
    assert.equal(result.status, 0, result.out);
    // After a composed pass, a change that reaches a class runs it by reach as before.
    commit('src/Desk/Desk.Core/Alpha/AlphaThing.cs', 'namespace Desk;\npublic sealed class AlphaThing { public int Three => 3; }\n');
    result = await repo.run(['--ci'], env);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(dotnetRan(), ['dotnet-test AlphaTests LonelyTests']);
    repo.cleanup();
  });

  test("--ci of main's tip gates the newest tip when main moved before its gates started", async () => {
    const repo = scratch('ci-newest', [{ name: 'universal', run: 'node ci-gate.mjs universal' }], {
      'ci-gate.mjs': CI_GATE, '.gitignore': 'local/\nnode_modules/\n', 'package-lock.json': '{"lockfileVersion":3}\n',
    });
    const first = git(repo.root, 'rev-parse', 'HEAD');
    // `npm ci`, faked: it lays node_modules where it runs, and a merge lands on main while it installs.
    const npmBin = join(dirname(repo.root), 'npm-bin');
    mkdirSync(npmBin, { recursive: true });
    writeFileSync(join(npmBin, 'fake-npm.mjs'), [
      "import { mkdirSync, writeFileSync } from 'node:fs';",
      "import { execFileSync } from 'node:child_process';",
      "import { join } from 'node:path';",
      "mkdirSync('node_modules', { recursive: true });",
      'const main = process.env.MAIN_CHECKOUT;',
      "writeFileSync(join(main, 'landed.txt'), 'landed\\n');",
      "execFileSync('git', ['add', '-A'], { cwd: main, windowsHide: true });",
      `execFileSync('git', ['commit', '--quiet', '-m', ${JSON.stringify(`landed${TRAILER}`)}], { cwd: main, windowsHide: true });`,
      '',
    ].join('\n'));
    if (process.platform === 'win32') writeFileSync(join(npmBin, 'npm.cmd'), '@echo off\r\nnode "%~dp0fake-npm.mjs" %*\r\n');
    else {
      writeFileSync(join(npmBin, 'npm'), '#!/bin/sh\nexec node "$(dirname "$0")/fake-npm.mjs" "$@"\n');
      chmodSync(join(npmBin, 'npm'), 0o755);
    }
    const PATH = `${npmBin}${delimiter}${join(dirname(repo.root), 'bin')}${delimiter}${process.env['PATH'] ?? process.env['Path'] ?? ''}`;
    const result = await repo.run(['--ci'], { PATH, MAIN_CHECKOUT: repo.root });
    assert.equal(result.status, 0, result.out);
    const landed = git(repo.root, 'rev-parse', 'HEAD');
    assert.notEqual(landed, first);
    assert.match(result.out, new RegExp(`main moved from ${first.slice(0, 7)} to ${landed.slice(0, 7)} before this run started: it gates ${landed.slice(0, 7)}, the newest`));
    assert.equal(git(join(repo.root, 'local', 'gate-tree'), 'rev-parse', 'HEAD'), landed);
    assert.equal((JSON.parse(readFileSync(join(repo.root, 'local', 'scratch', `ci-${landed.slice(0, 7)}`, 'status.json'), 'utf8')) as CiStatus).state, 'green');
    assert.equal(existsSync(join(repo.root, 'local', 'scratch', `ci-${first.slice(0, 7)}`)), false, 'the tip it gave way to has no run of its own');
    repo.cleanup();
  });

  test('--ci --watch gates main\'s tip as it moves, once each; a second watch has nothing to do; --ci --stop ends it', async () => {
    const repo = scratch('ci-watch', [{ name: 'universal', run: 'node ci-gate.mjs universal' }], { 'ci-gate.mjs': CI_GATE });
    const ranFile = join(repo.root, 'local', 'gate-tree', 'local', 'ran.txt');
    const ran = () => (existsSync(ranFile) ? readFileSync(ranFile, 'utf8').split('\n').filter(Boolean) : []);
    const green = (sha: string) => {
      const file = join(repo.root, 'local', 'scratch', `ci-${sha.slice(0, 7)}`, 'status.json');
      try {
        return (JSON.parse(readFileSync(file, 'utf8')) as CiStatus).state === 'green';
      } catch {
        return false;
      }
    };
    const first = git(repo.root, 'rev-parse', 'HEAD');
    const watch = repo.run(['--ci', '--watch'], { MERGE_BRANCH_CI_WATCH_MS: '200' });
    let stopped = { status: null as number | null, out: '' };
    try {
      await waitFor(() => green(first), 'the watch gating main\'s tip');
      let result = await repo.run(['--ci', '--watch']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /a CI watch runs here already \(pid \d+, since [^)]+\): this one has nothing to do/);
      writeFileSync(join(repo.root, 'next.txt'), 'next\n');
      git(repo.root, 'add', '-A');
      git(repo.root, 'commit', '--quiet', '-m', `next${TRAILER}`);
      const second = git(repo.root, 'rev-parse', 'HEAD');
      await waitFor(() => green(second), 'the watch gating the tip main moved to');
      // A few of its polls more: a tip it gated is never gated again.
      // patience: staging — a window in which nothing should happen, not a wait for a signal
      await new Promise((done) => setTimeout(done, 1500));
      assert.deepEqual(ran(), ['universal', 'universal'], 'once each tip');
      result = await repo.run(['--ci']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /nothing to run/);
    } finally {
      stopped = await repo.run(['--ci', '--stop']);
    }
    assert.equal(stopped.status, 0, stopped.out);
    assert.match(stopped.out, /asked the CI watch \(pid \d+\) to stop/);
    assert.match(stopped.out, /the CI watch has ended/);
    const done = await watch;
    assert.equal(done.status, 0, done.out);
    assert.match(done.out, /the CI watch: asked to stop, so it ends/);
    assert.equal(existsSync(join(repo.root, 'local', 'ci-watch.json')), false, 'the watch lets its record go');
    assert.equal(existsSync(join(repo.root, 'local', 'ci-watch.stop')), false, 'and the stop it was asked');
    const none = await repo.run(['--ci', '--stop']);
    assert.equal(none.status, 0, none.out);
    assert.match(none.out, /no CI watch is running here: nothing to stop/);
    repo.cleanup();
  });

  test('--ci writes its green status only once it has let the gate tree go (CIWATCHHOLD1)', async () => {
    const repo = scratch('ci-hold', [{ name: 'universal', run: 'node ci-gate.mjs universal' }], { 'ci-gate.mjs': CI_GATE });
    const sha = git(repo.root, 'rev-parse', 'HEAD');
    const statusFile = join(repo.root, 'local', 'scratch', `ci-${sha.slice(0, 7)}`, 'status.json');
    const lockFile = join(repo.root, 'local', 'gate-tree.lock');
    const stateOf = () => {
      try {
        return (JSON.parse(readFileSync(statusFile, 'utf8')) as CiStatus).state;
      } catch {
        return null;
      }
    };
    let ended = false;
    const run = repo.run(['--ci']).finally(() => { ended = true; });
    // A reader that finds `green` and then the lock still held is the tool contradicting itself: the status is read first, so
    // the lock it reads after can only have been let go before the status said so. The run keeps its receipt and its
    // background bookkeeping between its last gate and its end, so the window a wrong order leaves is wide.
    const contradictions = new Set<string>();
    while (!ended) {
      if (stateOf() === 'green' && existsSync(lockFile)) contradictions.add('a green status was read while local/gate-tree.lock stood');
      await new Promise((done) => { setImmediate(done); });
    }
    const result = await run;
    assert.equal(result.status, 0, result.out);
    assert.equal(stateOf(), 'green');
    assert.equal(existsSync(lockFile), false, 'the run lets the gate tree go');
    assert.deepEqual([...contradictions], [], 'a green status was read while the run still held the gate tree');
    repo.cleanup();
  });

// A builder's worktree sets GIT_DIR; the fixtures' git must still be the fixture's own, and the real repository registers
// no worktree of the fixture's. Outside the concurrent suite: the variables are the process's, and every test beside would read them.
// The real repository's worktrees move while this runs, so only the fixture's are looked for.
test('a fixture repository ignores the variables that locate the real one: its git and the tool it runs change its worktrees alone', async () => {
  const realDir = git(process.cwd(), 'rev-parse', '--git-dir');
  const listed = (cwd: string) => spawnSync('git', ['worktree', 'list', '--porcelain'], { cwd, encoding: 'utf8', windowsHide: true }).stdout;
  const repo = scratch('ci-env', [{ name: 'universal', run: 'node ci-gate.mjs universal' }], { 'ci-gate.mjs': CI_GATE, 'docs/a.md': 'a\n' });
  const fixture = realpathSync(repo.root).replaceAll('\\', '/').toLowerCase();
  const saved = { dir: process.env['GIT_DIR'], tree: process.env['GIT_WORK_TREE'] };
  process.env['GIT_DIR'] = resolve(realDir);
  process.env['GIT_WORK_TREE'] = process.cwd();
  try {
    assert.equal(realpathSync(git(repo.root, 'rev-parse', '--show-toplevel')).toLowerCase(), realpathSync(repo.root).toLowerCase());
    await repo.run(['--ci']);
    const leaked = listed(process.cwd()).split('\n')
      .filter((line) => line.startsWith('worktree ') && line.slice('worktree '.length).replaceAll('\\', '/').toLowerCase().startsWith(fixture));
    assert.deepEqual(leaked, [], 'the real repository registered no worktree of the fixture\'s');
    assert.match(listed(repo.root), /gate-tree|worktree /);
  } finally {
    if (saved.dir === undefined) delete process.env['GIT_DIR']; else process.env['GIT_DIR'] = saved.dir;
    if (saved.tree === undefined) delete process.env['GIT_WORK_TREE']; else process.env['GIT_WORK_TREE'] = saved.tree;
    repo.cleanup();
  }
});
});

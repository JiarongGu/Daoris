import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync, realpathSync, rmSync, writeFileSync, writeSync } from 'node:fs';
import { delimiter, dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from './_fixture.ts';

/**
 * MOD9 (`docs/2026-09-30-parallel-development-design.md` §3 rule 6): the parent's one merge tool. It
 * replaced gate chains typed by hand, one of which lost a rehearsal's reason, and a batch that skipped
 * the .NET suites as "web only" while two C# tests read the web's catalogues. So the plan is read from
 * the declared set. Since GATE3 a merge runs the gates its paths can reach, and the tests of the lane
 * table below hold it to what each suite reads and each rehearsal runs, so that lesson stays a test; the
 * full set is still owed before a stage.
 *
 * 🔴 The end-to-end tests below run the tool in a scratch repository of their own, and assert that it is
 * its own repository before the first run: git walks UP, and a merge tool that found the repository
 * this suite sits in would merge into it.
 */

interface Gate { name: string; run: string; kind: string; before: string[]; cwd?: string }
interface Lane { id: string; title: string; summary: string; steward: boolean; paths: string[]; gates?: string[] }
interface Classified { lanes: { id: string; title: string; files: string[] }[]; steward: string[]; shared: string[]; laneless: string[]; outside: string[] }
interface Options {
  branches: string[]; keepGoing: boolean; commitCheck: boolean; resume: boolean; dropBatch: boolean; plan: boolean; prune: boolean; autoPrune: boolean;
  full: boolean; rerun: string[]; passed: boolean;
}
interface Worktree { path: string; branch: string; locked: boolean; lockReason: string; prunable: boolean; bare: boolean }
interface Candidate { branch: string; worktree: Worktree | null; held: string | null }
interface Facts { gone?: boolean; error?: string; tracked: string[]; untracked: string[]; local: string | null }
interface Slow { name: string; ms: number; tests: number }
interface GateResult { gate: Gate; code: number; log: string; verdict: string; note: string; ms: number; slowest?: Slow[]; trx?: string }
type Step = (command: string, cwd: string, fd: number) => Promise<number>;
interface Rule { paths?: string[]; lane?: string; gates: string[] | '*'; why: string }
interface Selected { gate: Gate; run: boolean; why: string }
interface Verdict { gate: string; verdict: string; tree: string | null; at: string; commit?: string | null; branch?: string | null }

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  '../../../tools/merge-branch.mjs') as {
  parseArgs: (argv: string[]) => Options;
  runGate: (root: string, gate: Gate, dir: string, options?: { step?: Step }) => Promise<GateResult>;
  runGates: (root: string, gates: Gate[], dir: string, options?: {
    keepGoing?: boolean;
    run?: (root: string, gate: Gate, dir: string) => Promise<GateResult>;
    print?: (line: string) => void;
  }) => Promise<GateResult[]>;
  gateKind: (run: string) => string;
  gatePlan: (declared: { name: string; run: string }[], workflow?: string) => Gate[];
  readPlan: (root: string) => Gate[];
  flakeDecision: (output: string, isProcess?: (test: string) => boolean) => { rerun: string[]; reason?: string };
  rerunCommand: (run: string, test: string) => string;
  rerunPassed: (output: string) => boolean;
  processExit: (code: number) => boolean;
  rehearsalDecision: (log: string, code: number, command?: string) => { rerun: boolean; reason: string };
  globToRegExp: (glob: string) => RegExp;
  laneMatcher: (paths: string[]) => (path: string) => boolean;
  classify: (paths: string[], map: { lanes?: Lane[]; union?: string[]; laneless?: string[] }) => Classified;
  LANES_FILE: string;
  lanesProblems: (parsed: unknown, gateNames: string[]) => string[];
  readLanes: (root: string) => { lanes: Lane[]; laneless: string[] } | null;
  unionRecords: (root: string) => string[];
  parseStatus: (text: string) => { branch: string; changed: { code: string; path: string }[] };
  startRefusal: (facts: { branch: string; merging: boolean; changed: { code: string; path: string }[]; ignored: boolean }) => string | null;
  parseCommits: (text: string) => { sha: string; merge: boolean; subject: string; trailer: string }[];
  worktreeFor: (porcelain: string, branch: string) => string | null;
  isProcessGate: (gate: { name: string; run: string }) => boolean;
  parseWorktrees: (porcelain: string) => Worktree[];
  pruneCandidates: (facts: { merged: string[]; worktrees: Worktree[]; current: string; hold?: string[] }) => Candidate[];
  pruneVerdict: (candidate: Candidate, facts: Facts | null) => { remove: boolean; why: string; gone?: boolean };
  BASELINE: readonly string[];
  GENERATED: readonly { folder: string; tool: string }[];
  isGenerated: (path: string) => boolean;
  REACH: readonly Rule[];
  selectGates: (plan: Gate[], changed: string[], options?: { lanes?: Lane[]; full?: boolean; reach?: readonly Rule[] }) => { gates: Selected[]; unplaced: string[] };
  rerunPlan: (plan: string[], chosen: string[], results: Record<string, { verdict: string; at: string }>, named: string[]) => {
    run: string[]; kept: { name: string; verdict: string; at: string }[]; fresh: string[];
  };
  VERDICTS_FILE: string;
  stageJudgement: (facts: { plan: string[]; verdicts: Verdict[]; tree: string; recordsOnly: (tree: string) => boolean }) => {
    passed: boolean; missing: string[]; gates: { name: string; verdict: Verdict | null; exact: boolean }[];
  };
  recordMatcher: (map: { lanes?: Lane[]; union?: string[] }) => (path: string) => boolean;
  trxCommand: (run: string, file: string) => string;
  slowestClasses: (trx: string, count?: number) => Slow[];
};

const TOOL = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'tools', 'merge-branch.mjs');
const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

function refusal(fn: () => unknown): { exitCode: number; message: string } {
  try {
    fn();
  } catch (error) {
    return error as { exitCode: number; message: string };
  }
  throw new Error('expected a refusal, but the call returned');
}

// ---------------------------------------------------------------------------------------------------
// Arguments

test('the arguments name one branch, or several after --batch, and the flags', () => {
  assert.deepEqual(tool.parseArgs(['mod9']), {
    branches: ['mod9'], keepGoing: false, commitCheck: true, resume: false, dropBatch: false, plan: false, prune: false, autoPrune: true,
    full: false, rerun: [], passed: false,
  });
  // GATE3: every gate for one merge, or for the checkout as it stands; and whether the full set passed it.
  assert.equal(tool.parseArgs(['a', '--full']).full, true);
  assert.equal(tool.parseArgs(['--plan', 'a', '--full']).full, true);
  assert.equal(tool.parseArgs(['--continue', '--full']).full, true);
  const alone = tool.parseArgs(['--full']);
  assert.deepEqual([alone.full, alone.branches], [true, []]);
  assert.equal(tool.parseArgs(['--passed']).passed, true);
  // GATE4: the named gates again, on the merge in place.
  assert.deepEqual(tool.parseArgs(['--rerun', 'web', 'driver-process']).rerun, ['web', 'driver-process']);
  assert.equal(tool.parseArgs(['--rerun', 'web', '--keep-going']).keepGoing, true);
  assert.deepEqual(tool.parseArgs(['a', '--batch', 'b', 'c']).branches, ['a', 'b', 'c']);
  const flagged = tool.parseArgs(['a', '--keep-going', '--no-commit-check']);
  assert.equal(flagged.keepGoing, true);
  assert.equal(flagged.commitCheck, false);
  assert.equal(tool.parseArgs(['--continue']).resume, true);
  assert.equal(tool.parseArgs(['--continue', '--keep-going']).keepGoing, true);
  assert.equal(tool.parseArgs(['--drop-batch']).dropBatch, true);
  assert.equal(tool.parseArgs(['--plan', 'a', '--batch', 'b']).plan, true);
  // GATE2: the prune on its own, its plan, and a merge that leaves the merged branches standing.
  const prune = tool.parseArgs(['--prune']);
  assert.deepEqual([prune.prune, prune.plan, prune.branches], [true, false, []]);
  const planned = tool.parseArgs(['--prune', '--plan']);
  assert.deepEqual([planned.prune, planned.plan], [true, true]);
  assert.equal(tool.parseArgs(['a', '--no-prune']).autoPrune, false);
  assert.equal(tool.parseArgs(['--continue', '--no-prune']).autoPrune, false);
});

test('the arguments refuse what would merge the wrong thing, with exit 2', () => {
  const cases: [string[], RegExp][] = [
    [[], /name the branch/],
    [['a', 'b'], /--batch/],
    [['a', '--batch'], /at least one branch/],
    [['--batch', 'b'], /follows the first branch/],
    [['a', '--batch', 'a'], /named twice/],
    [['a', '--bogus'], /unknown option '--bogus'/],
    [['--continue', 'a'], /takes no branch/],
    [['--continue', '--drop-batch'], /one of/],
    [['--plan', '--continue'], /one of/],
    [['--prune', 'a'], /--prune takes no branch/],
    [['--prune', '--continue'], /one of/],
    [['--prune', '--drop-batch'], /one of/],
    [['--prune', '--no-prune'], /--no-prune/],
    [['--rerun'], /--rerun names the gates to run again/],
    [['--rerun', 'web', '--continue'], /one of/],
    [['--rerun', 'web', '--full'], /--rerun takes gate names and --keep-going/],
    [['--rerun', 'web', 'web'], /'web' is named twice/],
    [['--passed', 'a'], /--passed takes no branch/],
    [['--passed', '--full'], /one of/],
    [['--prune', '--full'], /--full is for a merge or the checkout/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

// ---------------------------------------------------------------------------------------------------
// What git reports, read without starting git: the refusals and the commit check

test('the status is read for the branch and every changed or untracked path', () => {
  const status = [
    '# branch.oid 0123456789abcdef0123456789abcdef01234567',
    '# branch.head main',
    '1 .M N... 100644 100644 100644 aaaaaaa aaaaaaa src/a file.ts',
    '2 R. N... 100644 100644 100644 bbbbbbb bbbbbbb R100 new name.md\told.md',
    'u UU N... 100644 100644 100644 100644 ccccccc ccccccc ccccccc shared.txt',
    '? stray.txt',
  ].join('\n');
  assert.deepEqual(tool.parseStatus(status), {
    branch: 'main',
    changed: [
      { code: '.M', path: 'src/a file.ts' },
      { code: 'R.', path: 'new name.md' },
      { code: 'UU', path: 'shared.txt' },
      { code: '??', path: 'stray.txt' },
    ],
  });
  assert.deepEqual(tool.parseStatus('# branch.oid 01234\r\n# branch.head (detached)\r\n'), { branch: '', changed: [] });
});

test('a merge starts only on main, with no merge open, a clean tree and ignored logs, and says the first thing wrong', () => {
  const ready = { branch: 'main', merging: false, changed: [], ignored: true };
  assert.equal(tool.startRefusal(ready), null);
  assert.match(tool.startRefusal({ ...ready, branch: 'work', changed: [{ code: '??', path: 'x' }] })!, /the current branch is 'work', not main/);
  assert.match(tool.startRefusal({ ...ready, branch: '' })!, /a detached HEAD/);
  assert.match(tool.startRefusal({ ...ready, merging: true })!, /already in progress/);
  assert.match(tool.startRefusal({ ...ready, changed: [{ code: '??', path: 'stray.txt' }] })!, /not clean:\n {2}\?\? stray\.txt$/);
  const many = Array.from({ length: 13 }, (_, i) => ({ code: '.M', path: `f${i}` }));
  assert.match(tool.startRefusal({ ...ready, changed: many })!, /… 1 more$/);
  assert.match(tool.startRefusal({ ...ready, ignored: false })!, /local\/scratch\/ is not ignored/);
});

test("the commit log is read for each commit's trailer, and a merge of main into the branch is marked", () => {
  const log = [
    'abc1234\x1fp1\x1fwork on it\x1fFixture <fixture@example.test>\n\x1e',
    "\ndef5678\x1fp1 p2\x1fMerge branch 'main' into work\x1f\x1e",
    '\n9abcdef\x1fp1\x1fbare\x1f\x1e\n',
  ].join('');
  assert.deepEqual(tool.parseCommits(log), [
    { sha: 'abc1234', merge: false, subject: 'work on it', trailer: 'Fixture <fixture@example.test>' },
    { sha: 'def5678', merge: true, subject: "Merge branch 'main' into work", trailer: '' },
    { sha: '9abcdef', merge: false, subject: 'bare', trailer: '' },
  ]);
  assert.deepEqual(tool.parseCommits(''), []);
});

test('the worktree a branch is checked out in is found in the porcelain list', () => {
  const list = [
    'worktree /work/repo', 'HEAD 0123456', 'branch refs/heads/main', '',
    'worktree /work/repo/.claude/worktrees/agent-a', 'HEAD 89abcde', 'branch refs/heads/worktree-agent-a', '',
    'worktree /work/elsewhere', 'HEAD 1234567', 'detached', '',
  ].join('\n');
  assert.equal(tool.worktreeFor(list, 'worktree-agent-a'), '/work/repo/.claude/worktrees/agent-a');
  assert.equal(tool.worktreeFor(list, 'main'), '/work/repo');
  assert.equal(tool.worktreeFor(list, 'worktree-agent'), null, 'a prefix is not the branch');
  assert.equal(tool.worktreeFor(list, 'nope'), null);
});

// ---------------------------------------------------------------------------------------------------
// The prune (GATE2): which merged branches go, with their worktrees, and which stay and why

const tree = (path: string, branch: string, more: Partial<Worktree> = {}): Worktree => ({
  path, branch, locked: false, lockReason: '', prunable: false, bare: false, ...more,
});

test('the worktree list is read for each path, branch, lock and lock reason, and a folder git says is gone', () => {
  const list = [
    'worktree D:/work/repo', 'HEAD 0123456', 'branch refs/heads/main', '',
    'worktree D:/work/repo/.claude/worktrees/agent-a', 'HEAD 89abcde', 'branch refs/heads/worktree-agent-a',
    'locked claude agent agent-a (pid 57256)', '',
    'worktree D:/work/repo/.claude/worktrees/agent-b', 'HEAD 89abcde', 'branch refs/heads/worktree-agent-b', 'locked', '',
    'worktree D:/work/gone', 'HEAD 1234567', 'branch refs/heads/gone', 'prunable gitdir file points to non-existent location', '',
    'worktree D:/work/elsewhere', 'HEAD 1234567', 'detached', '',
  ].join('\r\n');
  assert.deepEqual(tool.parseWorktrees(list), [
    tree('D:/work/repo', 'main'),
    tree('D:/work/repo/.claude/worktrees/agent-a', 'worktree-agent-a', { locked: true, lockReason: 'claude agent agent-a (pid 57256)' }),
    tree('D:/work/repo/.claude/worktrees/agent-b', 'worktree-agent-b', { locked: true }),
    tree('D:/work/gone', 'gone', { prunable: true }),
    tree('D:/work/elsewhere', ''),
  ]);
});

test('a prune considers each merged branch but main, the main checkout\'s, the one it runs in and the batch\'s still to merge', () => {
  const worktrees = [tree('/r', 'park'), tree('/r/w/a', 'agent-a'), tree('/r/w/here', 'here')];
  const candidates = tool.pruneCandidates({
    merged: ['agent-a', 'here', 'main', 'next', 'park', 'plain'], worktrees, current: 'here', hold: ['next'],
  });
  assert.deepEqual(candidates.map((c) => [c.branch, c.worktree?.path ?? null, c.held !== null]), [
    ['agent-a', '/r/w/a', false],
    ['here', '/r/w/here', true],
    ['next', null, true],
    ['park', '/r', true],
    ['plain', null, false],
  ], 'main is the reference, never a candidate');
  const held = Object.fromEntries(candidates.map((c) => [c.branch, c.held]));
  assert.match(held['park']!, /main checkout/);
  assert.match(held['here']!, /runs in/);
  assert.match(held['next']!, /batch/);
});

test('a merged branch goes with its worktree only when that is unlocked, has no tracked change and holds nothing under local/', () => {
  const clean: Facts = { tracked: [], untracked: [], local: null };
  const at = (more: Partial<Worktree> = {}): Candidate => ({ branch: 'b', worktree: tree('/r/w/b', 'b', more), held: null });

  assert.deepEqual(tool.pruneVerdict({ branch: 'b', worktree: null, held: null }, null), { remove: true, why: '' });
  assert.deepEqual(tool.pruneVerdict(at(), clean), { remove: true, why: '' });
  assert.deepEqual(tool.pruneVerdict(at({ prunable: true }), { ...clean, gone: true }), { remove: true, why: '', gone: true });
  assert.deepEqual(tool.pruneVerdict({ branch: 'b', worktree: null, held: 'named in this batch' }, null), { remove: false, why: 'named in this batch' });

  // A branch at main's tip is merged; a locked worktree is in use however it looks, and is never looked into.
  const locked = tool.pruneVerdict(at({ locked: true, lockReason: 'claude agent agent-b (pid 1)' }), null);
  assert.equal(locked.remove, false);
  assert.match(locked.why, /locked, so in use \(claude agent agent-b \(pid 1\)\)/);
  assert.equal(tool.pruneVerdict(at({ locked: true, prunable: true }), null).remove, false, 'a lock holds even when the folder is gone');

  const changed = tool.pruneVerdict(at(), { ...clean, tracked: ['src/a.ts', 'b.md', 'c.md', 'd.md'] });
  assert.equal(changed.remove, false);
  assert.match(changed.why, /modified or staged tracked files \(src\/a\.ts, b\.md, c\.md, … 1 more\)/);
  const local = tool.pruneVerdict(at(), { ...clean, local: 'local/scratch/out.md' });
  assert.equal(local.remove, false);
  assert.match(local.why, /under local\/ \(local\/scratch\/out\.md\), which nothing else keeps/);
  const untracked = tool.pruneVerdict(at(), { ...clean, untracked: ['new.txt'] });
  assert.equal(untracked.remove, false);
  assert.match(untracked.why, /untracked files .*--force \(new\.txt\)/);
  const unread = tool.pruneVerdict(at(), { ...clean, error: 'fatal: not a git repository' });
  assert.equal(unread.remove, false);
  assert.match(unread.why, /fatal: not a git repository/);
  assert.equal(tool.pruneVerdict(at(), null).remove, false, 'a worktree nobody looked into is kept');
  // Every reason is said, not only the first.
  assert.match(tool.pruneVerdict(at(), { ...clean, tracked: ['a'], local: 'local/x' }).why, /tracked files[\s\S]*local\//);
});

// ---------------------------------------------------------------------------------------------------
// The gate plan: read from the declared set, every gate once, fast first

test('a gate is a check, a suite or a rehearsal by what it runs', () => {
  assert.equal(tool.gateKind('dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- map --check'), 'check');
  // A tool's own --check is a check too, run with the fast ones (ORIENT1a); a tool run otherwise is not.
  assert.equal(tool.gateKind('node tools/orient-index.mjs --check'), 'check');
  assert.equal(tool.gateKind('node tools/orient-index.mjs'), 'suite');
  assert.equal(tool.gateKind('npm run verify'), 'suite');
  assert.equal(tool.gateKind('dotnet test src/Daoris.Service'), 'suite');
  assert.equal(tool.gateKind('npm run test:web'), 'rehearsal');
  assert.equal(tool.gateKind('npm run rehearse'), 'rehearsal');
  assert.equal(tool.gateKind('npm run rehearse:deploy'), 'rehearsal');
  // Anything it cannot place still runs, as a suite: the kind orders a gate, it never drops one.
  assert.equal(tool.gateKind('make everything'), 'suite');
});

test('the plan is the declared gates plus the rehearsals only the workflow runs, fast first, the declared order kept within a kind', () => {
  const declared = [
    { name: 'cli', run: 'npm run verify' },
    { name: 'universal', run: 'dotnet run --project devkit -- verify --universal-only' },
    { name: 'service', run: 'dotnet test src/Daoris.Service' },
    { name: 'web', run: 'npm run test:web' },
    { name: 'deployment', run: 'npm run rehearse:deploy' },
  ];
  const workflow = [
    '# run: npm run a-comment-is-not-a-step',
    '    steps:',
    '      - name: Deployment rehearsal',
    '        run: npm run rehearse:deploy',
    '      - run: npm run verify',
    '        run: npm run rehearse',
    '        run: npm run rehearse:family',
    '        run: |',
    '          npm run inside-a-block',
    '        run: npm run rehearse',
  ].join('\n');

  const plan = tool.gatePlan(declared, workflow);
  assert.deepEqual(plan.map((gate) => gate.name), ['universal', 'cli', 'service', 'rehearse', 'rehearse-family', 'web', 'deployment']);
  assert.deepEqual(plan.map((gate) => gate.kind), ['check', 'suite', 'suite', 'rehearsal', 'rehearsal', 'rehearsal', 'rehearsal']);
  // A long-lived build server carried a broken environment into a publish on 2026-09-30.
  assert.deepEqual(plan.find((gate) => gate.name === 'deployment')!.before, ['dotnet build-server shutdown']);
  assert.ok(plan.filter((gate) => gate.name !== 'deployment').every((gate) => gate.before.length === 0));
});

test("this repository's plan runs every declared gate once, fast first, and the four rehearsals last", () => {
  const declared = (JSON.parse(readFileSync(join(repoRoot, 'daoris.gates.json'), 'utf8')) as { gates: { run: string }[] }).gates;
  const plan = tool.readPlan(repoRoot);

  for (const gate of declared) {
    assert.equal(plan.filter((planned) => planned.run === gate.run).length, 1, `${gate.run} must run exactly once`);
  }
  // The batch that broke main skipped these as "web only": every .NET suite is in the plan, the full set a stage needs.
  assert.ok(plan.filter((gate) => /^dotnet test /.test(gate.run)).length >= 4, 'the .NET suites left the plan');

  const rank = ['check', 'suite', 'rehearsal'];
  plan.forEach((gate, i) => {
    if (i > 0) assert.ok(rank.indexOf(gate.kind) >= rank.indexOf(plan[i - 1]!.kind), `${gate.name} runs before a faster kind`);
  });
  assert.deepEqual(plan.filter((gate) => gate.kind === 'rehearsal').map((gate) => gate.run),
    ['npm run rehearse', 'npm run rehearse:family', 'npm run test:web', 'npm run rehearse:deploy']);
  assert.deepEqual(plan.at(-1)!.before, ['dotnet build-server shutdown']);
});

// ---------------------------------------------------------------------------------------------------
// GATE3: a merge runs the gates its lanes reach; the full set runs before the install is staged

const repoPlan = tool.readPlan(repoRoot);
const repoLanes = tool.readLanes(repoRoot)!.lanes;
const reached = (paths: string[], full = false): string[] =>
  tool.selectGates(repoPlan, paths, { lanes: repoLanes, full }).gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
const BASE = ['universal', 'code-map', 'orient-index', 'cli'];
const ALL = repoPlan.map((gate) => gate.name);

test('a merge runs the baseline, and the gates each changed path can reach, in the plan\'s order', () => {
  const cases: [string, string[], string[]][] = [
    // The service's suite scans this repository's own doctrine and decisions, so the docs reach it.
    ['docs only', ['docs/2026-09-30-parallel-development-design.md'], [...BASE, 'service']],
    ["the steward's records", ['TASKS.md', 'docs/task-archive.md'], [...BASE, 'service']],
    // The .NET suites read the page's catalogues and sources: MOD9's incident.
    ['the web shell', ['src/Daoris.Web/src/App.tsx'], [...BASE, 'service', 'driver', 'modules', 'web']],
    ["the web's settings", ['src/Daoris.Web/src/settings/DriverDomain.tsx'], [...BASE, 'service', 'driver', 'modules', 'web']],
    ['the driver', ['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'],
      [...BASE, 'driver', 'modules', 'driver-process', 'modules-process', 'rehearse-family', 'deployment']],
    ['ServiceHostLocator', ['src/Daoris.Desktop/Daoris.Desktop.Driver/ServiceHostLocator.cs'],
      [...BASE, 'driver', 'modules', 'driver-process', 'modules-process', 'rehearse-family', 'deployment']],
    ["the driver's tests", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs'], [...BASE, 'driver', 'driver-process']],
    ['the modules', ['src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs'], [...BASE, 'modules', 'modules-process', 'deployment']],
    ['the desktop app', ['src/Daoris.Desktop/Daoris.Desktop.App/Program.cs'], [...BASE, 'modules', 'modules-process', 'deployment']],
    ['the publish script', ['tools/desktop-publish.mjs'], [...BASE, 'rehearse-family', 'deployment']],
    ['the deployment rehearsal', ['tools/deployment-rehearsal.mjs'], [...BASE, 'deployment']],
    ['the canon', ['canon/core/rules/task-lifecycle.md'], [...BASE, 'service', 'rehearse', 'rehearse-family']],
    ['the examples', ['examples/engine/README.md'], [...BASE, 'service', 'rehearse-family', 'web']],
    ["the install's offers", ['examples/plugins/github-pull-request/land.mjs'], [...BASE, 'service', 'rehearse-family', 'web', 'deployment']],
    ['release tooling', ['tools/release-rehearsal.mjs'], [...BASE, 'rehearse']],
    ['the CLI', ['src/Daoris.Cli/src/cli.ts'], [...BASE, 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ['the merge tool and its tests', ['tools/merge-branch.mjs', 'src/Daoris.Cli/test/merge-branch.test.ts'], BASE],
    ['the service', ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'],
      [...BASE, 'service', 'devkit', 'driver', 'rehearse-family', 'web', 'deployment']],
    ['the devkit', ['src/Daoris.Devkit/Daoris.Devkit.Core/Gates.cs'], [...BASE, 'devkit']],
    ["the Process halves' settings", ['src/Daoris.Desktop/process.runsettings'], [...BASE, 'driver-process', 'modules-process']],
    ['the declared gates', ['daoris.gates.json'], ALL],
    // Written into every merge and held by its own check, which is in the baseline (ORIENT1a).
    ['the orientation index', ['docs/index/routes.md', 'docs/index/outlines/tools/merge-branch.mjs.md'], BASE],
    ['nothing at all', [], BASE],
  ];
  for (const [what, paths, expected] of cases) assert.deepEqual(reached(paths), expected, what);
  // Lanes add up: a branch crossing two runs what either reaches.
  assert.deepEqual(reached(['docs/x.md', 'tools/release-rehearsal.mjs', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs']),
    [...BASE, 'service', 'modules', 'modules-process', 'rehearse', 'deployment']);
});

test('what a merge writes rather than merges is the orientation index, by its folder, and its tool is this repository\'s', () => {
  assert.equal(tool.isGenerated('docs/index/routes.md'), true);
  assert.equal(tool.isGenerated('docs/index/outlines/src/Daoris.Web/src/App.tsx.md'), true);
  assert.equal(tool.isGenerated('docs/index.md'), false);
  assert.equal(tool.isGenerated('docs/decisions/D1.md'), false);
  for (const { folder, tool: writer } of tool.GENERATED) {
    assert.ok(existsSync(join(repoRoot, writer)), `${writer} writes ${folder}/ and is not here`);
    // A hand merge meets no textual merge of it either: the attribute keeps one side whole, and the tool writes it again.
    assert.match(readFileSync(join(repoRoot, '.gitattributes'), 'utf8'), new RegExp(`^${folder.replace(/\//g, '\\/')}/\\*\\* -merge$`, 'm'));
  }
});

test('a path no rule places runs every gate, and says which path; --full runs every gate and says so', () => {
  const unplaced = tool.selectGates(repoPlan, ['docs/x.md', 'src/Daoris.Somewhere/Program.cs'], { lanes: repoLanes });
  assert.deepEqual(unplaced.unplaced, ['src/Daoris.Somewhere/Program.cs']);
  assert.ok(unplaced.gates.every((entry) => entry.run), 'when unsure, a path runs everything');
  const driverProcess = unplaced.gates.find((entry) => entry.gate.name === 'driver-process')!;
  assert.match(driverProcess.why, /src\/Daoris\.Somewhere\/Program\.cs/);

  const full = tool.selectGates(repoPlan, ['docs/x.md'], { lanes: repoLanes, full: true });
  assert.deepEqual(full.gates.filter((entry) => entry.run).map((entry) => entry.gate.name), ALL);
  assert.ok(full.gates.filter((entry) => !BASE.includes(entry.gate.name)).every((entry) => /--full/.test(entry.why)));

  // Every gate says why it runs or why it is skipped.
  const docs = tool.selectGates(repoPlan, ['docs/x.md'], { lanes: repoLanes });
  const why = Object.fromEntries(docs.gates.map((entry) => [entry.gate.name, entry.why]));
  assert.match(why['cli']!, /every merge/);
  assert.match(why['service']!, /docs\/x\.md/);
  assert.match(why['driver-process']!, /no changed path reaches it/);
});

test('a gate the lane table never names runs at every merge, and a rule naming a gate not in the plan names nothing', () => {
  const plan = tool.gatePlan([
    { name: 'cli', run: 'npm run verify' },
    { name: 'mystery', run: 'node mystery.mjs' },
    { name: 'web', run: 'npm run test:web' },
  ]);
  const reach: Rule[] = [{ paths: ['docs/**'], gates: ['nowhere'], why: 'a gate this repository does not run' }];
  const selected = tool.selectGates(plan, ['docs/x.md'], { reach });
  assert.deepEqual(selected.gates.map((entry) => [entry.gate.name, entry.run]), [['cli', true], ['mystery', true], ['web', true]]);
  assert.match(selected.gates.find((entry) => entry.gate.name === 'mystery')!.why, /the lane table does not name it/);
  // Once a rule names a gate, a merge whose paths do not reach it skips it.
  const named = tool.selectGates(plan, ['docs/x.md'], { reach: [...reach, { paths: ['src/**'], gates: ['web', 'mystery'], why: 'the page' }] });
  assert.deepEqual(named.gates.map((entry) => [entry.gate.name, entry.run]), [['cli', true], ['mystery', false], ['web', false]]);
});

test('every gate the lane table names is one this repository runs, and every lane of the map has its rule', () => {
  const names = new Set(ALL);
  for (const gate of tool.BASELINE) assert.ok(names.has(gate), `baseline: '${gate}' is not in the plan`);
  for (const rule of tool.REACH) {
    assert.ok(rule.why.trim(), `a rule says why: ${JSON.stringify(rule)}`);
    assert.ok((rule.paths?.length ?? 0) > 0 !== (rule.lane !== undefined), `a rule names paths or a lane, not both: ${JSON.stringify(rule)}`);
    if (rule.gates !== '*') for (const gate of rule.gates) assert.ok(names.has(gate), `'${gate}' (${rule.lane ?? rule.paths!.join(', ')}) is not in the plan`);
  }
  const ruled = new Set(tool.REACH.map((rule) => rule.lane).filter(Boolean));
  for (const lane of repoLanes) assert.ok(ruled.has(lane.id), `the lane '${lane.id}' has no rule, so every path in it would run everything`);
  for (const id of ruled) assert.ok(repoLanes.some((lane) => lane.id === id), `a rule names the lane '${id}', which the map does not declare`);
  // A path no rule places is not refused here: it runs every gate, and the merge names it. A branch in
  // another lane may add one, and the table is the tools lane's to extend.
});

/**
 * What a rehearsal runs is code: its own script, everything that imports, and the tools it starts. A
 * change to any of those must run it, or the table hides the rehearsal's own failure. The spawned
 * scripts are named here by hand (a spawn is not an import); the imports are followed.
 */
test('every tool a rehearsal imports or starts is one whose change runs that rehearsal', () => {
  const closure = (entry: string): string[] => {
    const seen = new Set<string>();
    const stack = [entry];
    while (stack.length) {
      const file = stack.pop()!;
      if (seen.has(file)) continue;
      seen.add(file);
      for (const match of readFileSync(join(repoRoot, file), 'utf8').matchAll(/(?:from\s+|import\(\s*)'(\.{1,2}\/[^']+)'/g)) {
        const target = posix.normalize(posix.join(posix.dirname(file), match[1]!));
        if (existsSync(join(repoRoot, target))) stack.push(target);
      }
    }
    return [...seen];
  };
  const entries: [string, string][] = [
    ['tools/release-rehearsal.mjs', 'rehearse'], ['tools/stage-package.mjs', 'rehearse'],
    ['tools/family-rehearsal.mjs', 'rehearse-family'], ['tools/usage-report.mjs', 'rehearse-family'],
    ['tools/deployment-rehearsal.mjs', 'deployment'], ['tools/desktop-publish.mjs', 'deployment'],
    ['src/Daoris.Web/scripts/e2e-host.mjs', 'web'], ['tools/fsx.mjs', 'web'],
  ];
  for (const [entry, gate] of entries) {
    for (const file of closure(entry)) assert.ok(reached([file]).includes(gate), `${file} (from ${entry}) does not run ${gate}`);
  }
});

/**
 * MOD9's incident, as a test: two C# tests read the web's catalogues, and a batch called "web only"
 * skipped them. Every repository path a .NET test reads must be one whose change runs that test's
 * suite, and its Process half when the reading class is a Process class.
 */
test("every repository file a .NET suite reads is one whose change runs that suite (MOD9's incident)", () => {
  const suites: [string, string, string | null][] = [
    ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/', 'driver', 'driver-process'],
    ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/', 'modules', 'modules-process'],
    ['src/Daoris.Service/Daoris.Service.Tests/', 'service', null],
    ['src/Daoris.Service/Daoris.Service.Http.Tests/', 'service', null],
    ['src/Daoris.Devkit/Daoris.Devkit.Tests/', 'devkit', null],
  ];
  let reads = 0;
  for (const file of trackedFiles().filter((path) => path.endsWith('.cs'))) {
    const suite = suites.find(([folder]) => file.startsWith(folder));
    if (!suite) continue;
    const text = readFileSync(join(repoRoot, file), 'utf8');
    const isProcess = /\[Trait\(Category\.Name, Category\.Process\)\]/.test(text);
    for (const match of text.matchAll(/"src",\s*"(Daoris\.[A-Za-z.]+)"((?:,\s*"[^"]*")*)/g)) {
      const segments = ['src', match[1]!, ...[...match[2]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)];
      const read = /\.(cs|ts|tsx|mjs|js|json|css|md|props|txt)$/.test(segments.at(-1)!) ? segments.join('/') : `${segments.join('/')}/probe.txt`;
      reads += 1;
      const gates = reached([read]);
      assert.ok(gates.includes(suite[1]), `${file} reads ${read}, and a change there does not run ${suite[1]}`);
      if (isProcess && suite[2]) assert.ok(gates.includes(suite[2]), `${file} (a Process class) reads ${read}, and a change there does not run ${suite[2]}`);
    }
  }
  assert.ok(reads >= 10, `the scan found only ${reads} reads: its pattern no longer matches how the tests read the repository`);
});

// ---------------------------------------------------------------------------------------------------
// Flakes: a real-process test that fails is run alone once

const PROCESS_FAILURE = [
  'Test run for Daoris.Desktop.Driver.Tests.dll (.NETCoreApp,Version=v10.0)',
  '  Failed Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked [5 s]',
  '  Error Message:',
  '   Assert.True() Failure',
  'Failed!  - Failed:     1, Passed:  1144, Skipped:     0, Total:  1145, Duration: 3 m 4 s - Daoris.Desktop.Driver.Tests.dll (net10.0)',
].join('\r\n');

const CATALOGUE_FAILURE = [
  '  Failed Daoris.Desktop.Modules.Tests.RefusalCatalogueTests.Every_refusal_a_module_can_raise_has_a_translation(language: "zh") [< 1 ms]',
  '  Failed Daoris.Desktop.Modules.Tests.RefusalCatalogueTests.Every_refusal_a_module_can_raise_has_a_translation(language: "en") [< 1 ms]',
  'Failed!  - Failed:     2, Passed:   397, Skipped:     0, Total:   399, Duration: 1 m 1 s - Daoris.Desktop.Modules.Tests.dll (net10.0)',
].join('\n');

test('only a gate that runs the Process category is one whose failures may be flakes (MOD8)', () => {
  assert.equal(tool.isProcessGate({ name: 'driver-process', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings' }), true);
  assert.equal(tool.isProcessGate({ name: 'driver', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process' }), false);
  assert.equal(tool.isProcessGate({ name: 'service', run: 'dotnet test src/Daoris.Service' }), false);
});

test('a real-process test that failed is re-run alone; a theory is re-run by its method, once', () => {
  assert.deepEqual(tool.flakeDecision(PROCESS_FAILURE).rerun,
    ['Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked']);

  const theory = [
    '  Failed Daoris.Desktop.Driver.Tests.DrivenSessionInputTests.A_stop_is_the_person_s(harness: "acp-stub") [15 s]',
    '  Failed Daoris.Desktop.Driver.Tests.DrivenSessionInputTests.A_stop_is_the_person_s(harness: "claude") [15 s]',
    'Failed!  - Failed:     2, Passed:   934, Skipped:     0, Total:   936, Duration: 3 m - Daoris.Desktop.Driver.Tests.dll (net10.0)',
  ].join('\n');
  assert.deepEqual(tool.flakeDecision(theory).rerun, ['Daoris.Desktop.Driver.Tests.DrivenSessionInputTests.A_stop_is_the_person_s']);
});

test('a failure that is not a real-process test is never re-run, and says why', () => {
  const catalogue = tool.flakeDecision(CATALOGUE_FAILURE, () => false);
  assert.deepEqual(catalogue.rerun, []);
  assert.match(catalogue.reason!, /not a real-process test: RefusalCatalogueTests\.Every_refusal/);

  // A flake beside a real failure is still a failure: nothing is re-run.
  const mixed = [PROCESS_FAILURE.replace(/Failed!.*$/, ''), CATALOGUE_FAILURE.replace('Failed:     2', 'Failed:     3')].join('\n');
  assert.deepEqual(tool.flakeDecision(mixed, (name) => name.includes("ProcessJobTests")).rerun, []);

  const build = 'src/X.cs(3,1): error CS1002: ; expected\nBuild FAILED.\n';
  assert.match(tool.flakeDecision(build).reason!, /printed no result/);

  // A count the named lines do not reach means a failure went unnamed; re-running the named ones proves nothing.
  const unnamed = PROCESS_FAILURE.replace('Failed:     1,', 'Failed:     2,');
  assert.match(tool.flakeDecision(unnamed).reason!, /2 failed but 1 are named/);

  const crashed = 'Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 1 s - A.dll (net10.0)\nThe active test run was aborted.';
  assert.match(tool.flakeDecision(crashed).reason!, /no failing test is named/);
});

test('a solution prints one summary per project, and the counts are summed', () => {
  const service = [
    'Passed!  - Failed:     0, Passed:   652, Skipped:     0, Total:   652, Duration: 7 s - Daoris.Service.Tests.dll (net10.0)',
    '  Failed Daoris.Service.Http.Tests.HostTests.A_route [1 s]',
    'Failed!  - Failed:     1, Passed:    45, Skipped:     0, Total:    46, Duration: 10 s - Daoris.Service.Http.Tests.dll (net10.0)',
  ].join('\n');
  assert.match(tool.flakeDecision(service, () => false).reason!, /not a real-process test: HostTests\.A_route/);
});

test('a re-run runs that one test with no build, and passes only on a test that ran and passed', () => {
  assert.equal(tool.rerunCommand('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests', 'N.ProcessJobTests.A'),
    'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --no-build --filter "FullyQualifiedName=N.ProcessJobTests.A"');
  assert.equal(tool.rerunPassed('Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)'), true);
  // A filter that matches nothing exits 0: that is not a pass.
  assert.equal(tool.rerunPassed('No test matches the given testcase filter `FullyQualifiedName=N.A` in D.dll'), false);
  assert.equal(tool.rerunPassed(PROCESS_FAILURE), false);
});

// ---------------------------------------------------------------------------------------------------
// The lane map

test('a lane path is a glob: * stays in a folder, ** crosses folders, {a,b} is either', () => {
  const deep = tool.globToRegExp('src/Daoris.Web/src/settings/**');
  assert.ok(deep.test('src/Daoris.Web/src/settings/PluginsDomain.tsx'));
  assert.ok(deep.test('src/Daoris.Web/src/settings/deeper/x.ts'));
  assert.ok(!deep.test('src/Daoris.Web/src/settingsX/y.ts'));

  const shallow = tool.globToRegExp('src/Daoris.Web/src/SettingsView*.tsx');
  assert.ok(shallow.test('src/Daoris.Web/src/SettingsView.shell.test.tsx'));
  assert.ok(!shallow.test('src/Daoris.Web/src/settings/SettingsView.tsx'));

  const either = tool.globToRegExp('src/App{,.test}.tsx');
  assert.ok(either.test('src/App.tsx') && either.test('src/App.test.tsx') && !either.test('src/App.stories.tsx'));
  assert.ok(tool.globToRegExp('a.b').test('a.b') && !tool.globToRegExp('a.b').test('axb'), 'a dot is a dot');
});

test("a lane's `!` path carves a narrower lane's paths out of a wider one (LEFT1)", () => {
  const shell = tool.laneMatcher(['src/Daoris.Web/**', '!src/Daoris.Web/src/settings/**', '!src/Daoris.Web/src/SettingsView*.tsx']);
  assert.ok(shell('src/Daoris.Web/src/queries.ts'));
  assert.ok(shell('src/Daoris.Web/vite.config.ts'));
  assert.ok(shell('src/Daoris.Web/src/settingsX/y.ts'), 'a carve-out is its own glob, not a prefix');
  assert.ok(!shell('src/Daoris.Web/src/settings/DriverDomain.tsx'));
  assert.ok(!shell('src/Daoris.Web/src/SettingsView.tsx'));
  assert.ok(!shell('src/Daoris.Cli/src/cli.ts'));
  // A lane of carve-outs alone owns nothing.
  assert.ok(!tool.laneMatcher(['!src/**'])('README.md'));
});

test("a branch's files are placed in lanes, the steward's lane, the shared records, no lane, or outside every lane", () => {
  const map = tool.readLanes(repoRoot)!;
  const placed = tool.classify([
    'src/Daoris.Web/src/settings/PluginsDomain.tsx',
    'src/Daoris.Web/src/locales/zh/settings.ai.json',
    'src/Daoris.Web/src/locales/en/work.panel.json',
    'src/Daoris.Web/src/queries.ts',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/HelpRoom.cs',
    'src/Daoris.Devkit/Daoris.Devkit.Core/Gates.cs',
    'TASKS.md',
    'docs/task-archive.md',
    'daoris.lanes.json',
    'CHANGELOG.md',
    'docs/2026-09-30-parallel-development-design.md',
    'src/Daoris.Somewhere/Program.cs',
  ], { ...map, union: tool.unionRecords(repoRoot) });

  assert.deepEqual(placed.lanes, [
    { id: 'web-shell', title: 'Web shell', files: ['src/Daoris.Web/src/locales/en/work.panel.json', 'src/Daoris.Web/src/queries.ts'] },
    { id: 'web-settings', title: 'Web settings', files: ['src/Daoris.Web/src/settings/PluginsDomain.tsx', 'src/Daoris.Web/src/locales/zh/settings.ai.json'] },
    { id: 'driver', title: 'Driver library', files: ['src/Daoris.Desktop/Daoris.Desktop.Driver/Help/HelpRoom.cs'] },
    { id: 'modules', title: 'Desktop modules', files: ['src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs', 'src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs'] },
    { id: 'tools', title: 'Tools', files: ['src/Daoris.Devkit/Daoris.Devkit.Core/Gates.cs'] },
  ]);
  // DEV2: the steward's lane holds what the parent's list held, and the map itself. The archive merges
  // by union too, but it is the steward's, and that is what the parent needs to hear.
  assert.deepEqual(placed.steward, ['TASKS.md', 'docs/task-archive.md', 'daoris.lanes.json']);
  assert.deepEqual(placed.shared, ['CHANGELOG.md']);
  // A document belongs to no lane on purpose; a new source tree is a path nothing placed.
  assert.deepEqual(placed.laneless, ['docs/2026-09-30-parallel-development-design.md']);
  assert.deepEqual(placed.outside, ['src/Daoris.Somewhere/Program.cs']);
});

test("the lane map's lanes are the design's §5 lanes, by id and title", () => {
  const design = readFileSync(join(repoRoot, 'docs', '2026-09-30-parallel-development-design.md'), 'utf8').replace(/\r\n/g, '\n');
  const section = design.slice(design.indexOf('## 5. The lane map'), design.indexOf('## 6.'));
  const rows = section.split('\n')
    .filter((line) => line.startsWith('|') && !/^\|\s*-/.test(line))
    .map((line) => line.split('|').slice(1, 3).map((cell) => cell.trim().replace(/^`|`$/g, '')))
    .filter(([id]) => id !== 'Lane');
  assert.ok(rows.length >= 8, `the design's lane table was not found: ${rows.map(([id]) => id).join(', ')}`);
  assert.deepEqual(tool.readLanes(repoRoot)!.lanes.map((lane) => [lane.id, lane.title]), rows);
});

/**
 * DEV2 (`docs/2026-10-01-self-development-design.md` §2.1): the map is the repository's declaration,
 * and what makes it unreadable is a rule, so the driver's reader (DEV6) can hold the same table.
 */
test('the lanes file is unreadable for a bad or repeated id, a lane with no paths, two stewards, or an undeclared gate', () => {
  const lane = (id: string, more: Record<string, unknown> = {}) => ({ id, title: id, summary: `the ${id} lane`, paths: [`${id}/**`], ...more });
  assert.deepEqual(tool.lanesProblems({ lanes: [lane('web-shell'), lane('records', { steward: true, gates: ['cli'] })] }, ['cli']), []);
  assert.deepEqual(tool.lanesProblems({ lanes: [] }, []), [], 'an empty list is no lanes, not a fault');
  assert.deepEqual(tool.lanesProblems({ laneless: [] }, []), [], 'no list is no lanes, not a fault');

  const cases: [unknown, RegExp][] = [
    [[], /not a JSON object/],
    [{ lanes: {} }, /'lanes' is not a list/],
    [{ lanes: [lane('Web')] }, /lane 'Web': its id must be lower-case letters, digits and dashes, starting with a letter/],
    [{ lanes: [lane('2d')] }, /lane '2d': its id must/],
    [{ lanes: [{ title: 'No id', paths: ['x/**'] }] }, /lane 1: its id must/],
    [{ lanes: [lane('cli'), lane('cli')] }, /lane 'cli': the id is used twice/],
    [{ lanes: [lane('cli', { paths: [] })] }, /lane 'cli': it has no paths/],
    [{ lanes: [lane('cli', { paths: ['!src/**'] })] }, /lane 'cli': it has no paths/],
    [{ lanes: [lane('a', { steward: true }), lane('b', { steward: true })] }, /two stewards \(a, b\)/],
    [{ lanes: [lane('records', { gates: ['cli', 'nope'] })] }, /lane 'records': its gate 'nope' is not declared in daoris\.gates\.json/],
    [{ lanes: [lane('records', { gates: 'cli' })] }, /lane 'records': 'gates' is not a list/],
    [{ lanes: [lane('cli')], laneless: {} }, /'laneless' is not a list/],
  ];
  for (const [parsed, expected] of cases) {
    const problems = tool.lanesProblems(parsed, ['cli']);
    assert.ok(problems.some((problem) => expected.test(problem)), `${JSON.stringify(parsed)}: ${problems.join('; ') || 'no problem found'}`);
  }
});

test('no lanes file is no lanes; an unreadable one is refused with exit 2, naming each problem', () => {
  const fx = makeFixture('merge-branch-lanes-file');
  assert.equal(tool.readLanes(fx.root), null);
  fx.write('daoris.gates.json', JSON.stringify({ gates: [{ name: 'cli', run: 'npm test' }] }));
  fx.write(tool.LANES_FILE, JSON.stringify({ lanes: [] }));
  assert.deepEqual(tool.readLanes(fx.root), { lanes: [], laneless: [] });

  fx.write(tool.LANES_FILE, '{ "lanes": [');
  let refused = refusal(() => tool.readLanes(fx.root));
  assert.equal(refused.exitCode, 2);
  assert.match(refused.message, /daoris\.lanes\.json is not valid JSON/);

  fx.write(tool.LANES_FILE, JSON.stringify({ lanes: [
    { id: 'cli', title: 'CLI', summary: 's', paths: [] },
    { id: 'records', title: 'Records', summary: 's', steward: true, paths: ['TASKS.md'], gates: ['cli', 'web'] },
  ] }));
  refused = refusal(() => tool.readLanes(fx.root));
  assert.equal(refused.exitCode, 2);
  assert.match(refused.message, /daoris\.lanes\.json cannot be read:\n\s+lane 'cli': it has no paths\n\s+lane 'records': its gate 'web' is not declared/);
  fx.cleanup();
});

test("this repository's lanes each carry an id, a title and a one-line summary, and one steward keeps the records", () => {
  const { lanes } = tool.readLanes(repoRoot)!;
  for (const lane of lanes) {
    assert.ok(lane.title.trim(), `${lane.id}: no title`);
    assert.ok(lane.summary.trim() && !lane.summary.includes('\n'), `${lane.id}: its summary is one line, for the people and agents that address the lane`);
  }
  assert.deepEqual(lanes.map((lane) => lane.id), ['web-shell', 'web-settings', 'driver', 'modules', 'service', 'cli', 'tools', 'records']);
  const stewards = lanes.filter((lane) => lane.steward);
  assert.deepEqual(stewards.map((lane) => [lane.id, lane.paths, lane.gates]),
    [['records', ['TASKS.md', 'docs/task-archive.md', 'daoris.lanes.json'], ['universal', 'cli']]]);
  // A code lane never narrows its gates: MOD9's "web only" batch skipped the .NET suites.
  assert.deepEqual(lanes.filter((lane) => lane.gates !== undefined).map((lane) => lane.id), ['records']);
});

// A declaration, so the lane table's tests above can use it.
function trackedFiles(): string[] {
  const listed = spawnSync('git', ['ls-files'], { cwd: repoRoot, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  assert.equal(listed.status, 0, listed.stderr);
  return listed.stdout.split('\n').filter(Boolean);
}

test('every lane path and every laneless path matches a tracked file, and no tracked file has two places', () => {
  const files = trackedFiles();
  const { lanes, laneless } = tool.readLanes(repoRoot)!;

  // A carve-out (`!`) that matches nothing carves nothing, and is as stale as a path that points at nothing.
  for (const [id, globs] of [...lanes.map((lane) => [lane.id, lane.paths] as const), ['laneless', laneless] as const]) {
    for (const glob of globs) {
      const pattern = tool.globToRegExp(glob.replace(/^!/, ''));
      assert.ok(files.some((file) => pattern.test(file)), `${id}: '${glob}' matches no tracked file — the map points at nothing`);
    }
  }
  // The steward's lane is a lane like the rest here: its records are carved out of the docs, never
  // left to the classification order to settle.
  const owns = lanes.map((lane) => ({ id: lane.id, owns: tool.laneMatcher(lane.paths) }));
  const declared = tool.laneMatcher(laneless);
  for (const file of files) {
    const owners = owns.filter((lane) => lane.owns(file)).map((lane) => lane.id);
    assert.ok(owners.length <= 1, `${file} is in two lanes: ${owners.join(', ')}`);
    assert.ok(!(owners.length && declared(file)), `${file} is in ${owners[0]} and declared laneless`);
  }
});

/**
 * LEFT1: `queries.ts` and a few paths sat outside every lane unremarked, and a new source tree would
 * have too. Every tracked file now has a place: a lane (the steward's among them, DEV2), a record that
 * merges by union, or the map's `laneless` list (the docs, the doctrine, the harness's settings). A new
 * top-level path fails here until the map places it on purpose.
 */
test('every tracked file has a place, so a new path is placed on purpose, never left outside silently', () => {
  const map = tool.readLanes(repoRoot)!;
  const placed = tool.classify(trackedFiles(), { ...map, union: tool.unionRecords(repoRoot) });
  assert.deepEqual(placed.outside, [],
    `outside every lane: ${placed.outside.slice(0, 12).join(', ')}${placed.outside.length > 12 ? ', …' : ''}\n`
    + '  Give each a lane in daoris.lanes.json, or list it under "laneless" with the reason no lane owns it.');
  // The laneless list names files, never a source tree: a `src/**` there would place everything and
  // guard nothing.
  for (const glob of map.laneless.filter((path) => /^(src|tools)\//.test(path))) {
    assert.ok(!glob.includes('*'), `laneless: '${glob}' is a glob over a source tree; give that tree a lane`);
  }
});

// ---------------------------------------------------------------------------------------------------
// Running gates, with the step that starts a process replaced: the stop, the logs, the flakes

const planned = (name: string, run: string, before: string[] = []): Gate => ({ name, run, kind: 'suite', before });

/**
 * A step that answers each command from a function and records what it was asked, in order. It heads
 * its output with the command, as `runStep` does, since a rehearsal's own output is read from there.
 */
function fakeStep(answer: (command: string) => { out: string; code: number }): { asked: string[]; step: Step } {
  const asked: string[] = [];
  const step: Step = async (command, _cwd, fd) => {
    asked.push(command);
    const { out, code } = answer(command);
    writeSync(fd, `$ ${command}\n${out}`);
    return code;
  };
  return { asked, step };
}

test('the first failing gate stops the run and names every gate after it; --keep-going runs them all', async () => {
  const root = join(repoRoot, 'scratch-root');
  const dir = join(root, 'local', 'scratch', 'merge-x');
  const gates = ['a', 'b', 'c'].map((name) => planned(name, `node ${name}.mjs`));
  const run = async (_root: string, gate: Gate, at: string): Promise<GateResult> => {
    const failed = gate.name === 'b';
    return { gate, code: failed ? 3 : 0, log: join(at, `${gate.name}.log`), verdict: failed ? 'FAIL' : 'PASS', note: failed ? 'exit 3' : '', ms: 1000 };
  };

  const printed: string[] = [];
  const results = await tool.runGates(root, gates, dir, { run, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => result.gate.name), ['a', 'b']);
  assert.match(printed[0]!, /PASS\s+a\s+1 s\s+local\/scratch\/merge-x\/a\.log$/);
  assert.match(printed[1]!, /FAIL\s+b\s.*local\/scratch\/merge-x\/b\.log\s+\(exit 3\)/);
  assert.match(printed[2]!, /NOT RUN\s+c/);

  const all = await tool.runGates(root, gates, dir, { run, keepGoing: true, print: () => {} });
  assert.deepEqual(all.map((result) => result.gate.name), ['a', 'b', 'c']);
});

test("a gate's whole output is kept in its log, and the build servers are shut down before the deployment rehearsal", async () => {
  const fx = makeFixture('merge-branch-gate-log');
  const long = Array.from({ length: 5000 }, (_, i) => `line ${i + 1}`).join('\n');
  const { asked, step } = fakeStep((command) => ({ out: command.startsWith('dotnet build-server') ? 'shut down\n' : `${long}\n`, code: 0 }));

  const result = await tool.runGate(fx.root, planned('deployment', 'npm run rehearse:deploy', ['dotnet build-server shutdown']), fx.root, { step });
  assert.equal(result.verdict, 'PASS');
  assert.deepEqual(asked, ['dotnet build-server shutdown', 'npm run rehearse:deploy']);
  const log = readFileSync(join(fx.root, 'deployment.log'), 'utf8');
  assert.match(log, /\nline 1\n/);
  assert.match(log, /\nline 5000\n/);
  assert.ok(log.indexOf('shut down') < log.indexOf('\nline 1\n'), 'the shutdown ran first');
  assert.equal(existsSync(join(fx.root, 'deployment.log.partial')), false, 'the log is renamed into place when its gate ends');
  fx.cleanup();
});

test('a real-process failure is re-run alone once: a pass alone is a FLAKE, a second failure a FAIL, and nothing else is re-run', async () => {
  const fx = makeFixture('merge-branch-flake-runs');
  const driver = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  const flaky = 'Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked';
  const alone = 'Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)\n';

  let fake = fakeStep((command) => (command.includes('--filter') ? { out: alone, code: 0 } : { out: PROCESS_FAILURE, code: 1 }));
  let result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FLAKE');
  // The full run leaves its trx (PROC1); the re-run of one test adds none.
  assert.deepEqual(fake.asked, [tool.trxCommand(driver.run, join(fx.root, 'driver-process.trx')), tool.rerunCommand(driver.run, flaky)]);
  assert.match(result.note, /failed in the full run and passed alone: ProcessJobTests\.A_child.*driver-process\.rerun\.log/);
  assert.match(readFileSync(join(fx.root, 'driver-process.rerun.log'), 'utf8'), /Passed!/);

  fake = fakeStep(() => ({ out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.match(result.note, /failed again alone/);

  // A re-run whose filter matched nothing exits 0, and is still not a pass.
  fake = fakeStep((command) => (command.includes('--filter')
    ? { out: 'No test matches the given testcase filter\n', code: 0 } : { out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');

  // Outside the Process category a failure is real: the fast half, the service, any gate without the settings.
  const fast = planned('driver', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process');
  fake = fakeStep(() => ({ out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, fast, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.deepEqual(fake.asked, [fast.run], 'a failure outside the Process category is never re-run');
  assert.match(result.note, /outside the Process category, a failure is real and never re-run/);

  // A rehearsal is not read for tests: one that said why it stopped has failed, and is not run again.
  fake = fakeStep(() => ({ out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, planned('family', 'npm run rehearse:family'), fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 1);
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// A rehearsal that died, rather than failed, is run again once (LEFT1)

// What the tool and npm write around a rehearsal's own lines: none of it is the rehearsal speaking.
const NPM_BANNER = '$ npm run rehearse:family\n\n> daoris-workspace@0.0.1 rehearse:family\n> node tools/family-rehearsal.mjs\n\n';
const OK_LINES = '\n1. The example family\n  ok    the HTTP host builds\n  ok    engine is current and clean\n';

test('a process-level exit is the shell, a signal or a Windows crash status, never an ordinary failure', () => {
  // 127 and 126 are a POSIX shell's, 9009 is cmd's for a command it cannot find; 128 up is a signal (this
  // tool reads one as 128); a crash status comes through cmd and npm unsigned, and in its signed form elsewhere.
  for (const code of [126, 127, 9009, 128, 134, 255, 0xC0000142, 0xC0000409, -1073741819]) {
    assert.equal(tool.processExit(code), true, `exit ${code}`);
  }
  for (const code of [1, 2, 3, 100]) assert.equal(tool.processExit(code), false, `exit ${code}`);
});

test('a failed rehearsal is run again only when it reached no verdict: its process ended, or it said nothing', () => {
  // 2026-09-30: the family rehearsal exited 127 straight after building the HTTP host, with no transcript.
  const died = tool.rehearsalDecision(`${NPM_BANNER}${OK_LINES}\n(exited 127)\n`, 127);
  assert.equal(died.rerun, true);
  assert.match(died.reason, /exit 127/);
  assert.equal(tool.rehearsalDecision(`${NPM_BANNER}(exited 3221225794)\n`, 0xC0000142).rerun, true);

  const silent = tool.rehearsalDecision(`${NPM_BANNER}\n(exited 1)\n`, 1);
  assert.equal(silent.rerun, true);
  assert.match(silent.reason, /nothing of its own/);
  // A step before the gate is not the gate speaking: the build servers' shutdown says "shut down".
  const deploy = '$ dotnet build-server shutdown\nshut down\n\n(exited 0)\n\n$ npm run rehearse:deploy\n\n'
    + '> daoris-workspace@0.0.1 rehearse:deploy\n> node tools/deployment-rehearsal.mjs\n\n\n(exited 1)\n';
  assert.equal(tool.rehearsalDecision(deploy, 1, 'npm run rehearse:deploy').rerun, true);

  // A report is a verdict, whatever the exit that follows it.
  const kit = `${NPM_BANNER}${OK_LINES}  FAIL  a quest is refused where it should be\n          409\n\n  299/301 checks passed\n`;
  for (const code of [1, 127]) {
    const failed = tool.rehearsalDecision(kit, code);
    assert.equal(failed.rerun, false, `exit ${code}`);
    assert.match(failed.reason, /reported failed checks/);
  }
  // The platform's own suites inside `test:web`: vitest's summary and a failed file, and Playwright's.
  assert.equal(tool.rehearsalDecision(`${NPM_BANNER} FAIL  src/App.test.tsx > the frame\n      Tests  1 failed | 1712 passed (1713)\n`, 1).rerun, false);
  assert.equal(tool.rehearsalDecision(`${NPM_BANNER}  1 failed\n    [chromium] › e2e/platform.spec.ts:10:5 › a quest\n  20 passed (1.2m)\n`, 1).rerun, false);

  // Anything else said why it stopped: a thrown error is a failure, not a flake.
  const thrown = tool.rehearsalDecision(`${NPM_BANNER}${OK_LINES}Error: take failed: 500\n    at file:///x.mjs:630:9\n`, 1);
  assert.equal(thrown.rerun, false);
  assert.match(thrown.reason, /said why it stopped/);
});

test('a rehearsal that died is run again once, with its steps: a pass is a FLAKE, a second death a FAIL', async () => {
  const fx = makeFixture('merge-branch-rehearsal-reruns');
  const family = planned('family', 'npm run rehearse:family');
  let runs = 0;
  let fake = fakeStep(() => (++runs === 1
    ? { out: OK_LINES, code: 127 }
    : { out: `${OK_LINES}\n  301/301 checks passed\n`, code: 0 }));
  let result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FLAKE');
  assert.deepEqual(fake.asked, [family.run, family.run]);
  assert.match(result.note, /exit 127.*passed when run again.*family\.rerun\.log/);
  assert.match(readFileSync(join(fx.root, 'family.rerun.log'), 'utf8'), /301\/301 checks passed/);
  assert.match(readFileSync(join(fx.root, 'family.log'), 'utf8'), /\(exited 127\)/, 'the first run keeps its own log');

  fake = fakeStep(() => ({ out: '', code: 127 }));
  result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 2, 'run again once, not until it passes');
  assert.match(result.note, /exit 127.*failed again.*family\.rerun\.log/);

  // The deployment rehearsal's re-run shuts the build servers down first, as its first run did.
  const deployment = planned('deployment', 'npm run rehearse:deploy', ['dotnet build-server shutdown']);
  runs = 0;
  fake = fakeStep((command) => (command.startsWith('dotnet') ? { out: 'shut down\n', code: 0 } : (++runs === 1 ? { out: '', code: 1 } : { out: '  70/70 checks passed\n', code: 0 })));
  result = await tool.runGate(fx.root, deployment, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FLAKE');
  assert.deepEqual(fake.asked, ['dotnet build-server shutdown', deployment.run, 'dotnet build-server shutdown', deployment.run]);

  // A rehearsal that reported failed checks is never run again.
  fake = fakeStep(() => ({ out: `${OK_LINES}  FAIL  a quest closes done\n`, code: 1 }));
  result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 1);
  assert.match(result.note, /reported failed checks/);

  // The rule is the rehearsals': a suite that dies is read as it always was.
  fake = fakeStep(() => ({ out: '', code: 127 }));
  result = await tool.runGate(fx.root, planned('cli', 'npm run verify'), fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(fake.asked.length, 1);
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// GATE4: a fixed gate re-runs alone, and the rest's verdicts are kept, saying from when

test('a re-run runs the named gates and any the merge has not run yet, and keeps the rest\'s verdicts with their time', () => {
  const plan = ['universal', 'cli', 'driver', 'driver-process', 'web', 'deployment'];
  const chosen = ['universal', 'cli', 'driver', 'driver-process', 'web'];
  const results = {
    universal: { verdict: 'PASS', at: '2026-10-04T10:00:00Z' },
    cli: { verdict: 'PASS', at: '2026-10-04T10:03:00Z' },
    driver: { verdict: 'FAIL', at: '2026-10-04T10:09:00Z' },
  };
  // The failing driver suite, fixed: it runs again, and so do the gates its failure stopped, which have no verdict.
  assert.deepEqual(tool.rerunPlan(plan, chosen, results, ['driver']), {
    run: ['driver', 'driver-process', 'web'],
    kept: [{ name: 'universal', verdict: 'PASS', at: '2026-10-04T10:00:00Z' }, { name: 'cli', verdict: 'PASS', at: '2026-10-04T10:03:00Z' }],
    fresh: ['driver-process', 'web'],
  });
  // A gate the lanes did not reach runs when named, in the plan's order; a kept FAIL stays a FAIL.
  const named = tool.rerunPlan(plan, chosen, { ...results, 'driver-process': { verdict: 'PASS', at: 'x' }, web: { verdict: 'PASS', at: 'y' } }, ['deployment', 'cli']);
  assert.deepEqual(named.run, ['cli', 'deployment']);
  assert.deepEqual(named.kept.map((kept) => `${kept.name} ${kept.verdict}`), ['universal PASS', 'driver FAIL', 'driver-process PASS', 'web PASS']);
  assert.deepEqual(named.fresh, []);
});

// ---------------------------------------------------------------------------------------------------
// The record a stage reads: which gates passed which tree

test('the stage counts a gate passed on this tree, or on one that differs from it only by the records; the newest verdict there decides', () => {
  const at = (hour: number) => `2026-10-04T${String(hour).padStart(2, '0')}:00:00Z`;
  const verdicts: Verdict[] = [
    { gate: 'universal', verdict: 'PASS', tree: 'T', at: at(9) },
    // Passed before the parent wrote the records and committed: the trees differ by the records alone.
    { gate: 'cli', verdict: 'PASS', tree: 'R', at: at(9) },
    // Passed before a fix in code: that verdict is not this tree's.
    { gate: 'driver', verdict: 'PASS', tree: 'C', at: at(9) },
    // Passed, then failed on the same tree: the newest says.
    { gate: 'web', verdict: 'PASS', tree: 'T', at: at(9) },
    { gate: 'web', verdict: 'FAIL', tree: 'T', at: at(11) },
    // A flake is a pass that was said.
    { gate: 'driver-process', verdict: 'FLAKE', tree: 'T', at: at(10) },
    { gate: 'deployment', verdict: 'PASS', tree: null, at: at(10) },
  ];
  const judged = tool.stageJudgement({
    plan: ['universal', 'cli', 'driver', 'driver-process', 'web', 'deployment', 'rehearse'], verdicts, tree: 'T', recordsOnly: (tree) => tree === 'R',
  });
  assert.equal(judged.passed, false);
  assert.deepEqual(judged.missing, ['driver', 'web', 'deployment', 'rehearse']);
  const byName = Object.fromEntries(judged.gates.map((gate) => [gate.name, gate]));
  assert.equal(byName['universal']!.exact, true);
  assert.equal(byName['cli']!.exact, false);
  assert.equal(byName['cli']!.verdict!.tree, 'R');
  assert.equal(byName['web']!.verdict!.verdict, 'FAIL');
  assert.equal(byName['rehearse']!.verdict, null);

  const all = tool.stageJudgement({ plan: ['universal', 'cli'], verdicts, tree: 'T', recordsOnly: (tree) => tree === 'R' });
  assert.deepEqual([all.passed, all.missing], [true, []]);
});

test("the records a stage forgives are the steward's lane and the records that merge by union, and nothing else", () => {
  const isRecord = tool.recordMatcher({
    lanes: repoLanes, union: tool.unionRecords(repoRoot),
  });
  for (const path of ['TASKS.md', 'docs/task-archive.md', 'daoris.lanes.json', 'CHANGELOG.md', 'docs/decisions/D115.md', 'docs/FIX-LOG.md', 'docs/README.md', '.claude/knowledge/twins.md']) {
    assert.ok(isRecord(path), path);
  }
  for (const path of ['README.md', 'docs/2026-09-30-parallel-development-design.md', 'canon/CHANGELOG.md', 'src/Daoris.Cli/src/cli.ts', 'tools/merge-branch.mjs']) {
    assert.ok(!isRecord(path), path);
  }
  assert.equal(tool.VERDICTS_FILE, 'local/gate-verdicts.json', 'gitignored, under local/');
});

// ---------------------------------------------------------------------------------------------------
// PROC1: a Process gate leaves a trx with each test's duration, and the slowest classes are named

const TRX = [
  '<?xml version="1.0" encoding="utf-8"?>',
  '<TestRun id="r" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">',
  '  <Results>',
  '    <UnitTestResult executionId="e1" testId="t1" testName="N.SlowTests.A" computerName="m" duration="00:03:00.5000000" outcome="Passed" />',
  '    <UnitTestResult executionId="e2" testId="t2" testName="N.SlowTests.B" duration="00:01:00.0000000" outcome="Failed" computerName="m" />',
  '    <UnitTestResult testId="t3" duration="00:00:30.2500000" testName="N.QuickTests.C(x: &quot;1&quot;)" outcome="Passed" />',
  '    <UnitTestResult testId="t4" testName="N.Outer+InnerTests.D" duration="00:02:00" outcome="Passed" />',
  '    <UnitTestResult testId="t5" testName="N.SkippedTests.E" outcome="NotExecuted" />',
  '  </Results>',
  '  <TestDefinitions>',
  '    <UnitTest name="N.SlowTests.A" id="t1"><Execution id="e1" /><TestMethod codeBase="x.dll" className="N.SlowTests" name="A" /></UnitTest>',
  '    <UnitTest name="N.SlowTests.B" id="t2"><TestMethod className="N.SlowTests" name="B" codeBase="x.dll" /></UnitTest>',
  '    <UnitTest id="t3" name="N.QuickTests.C"><TestMethod className="N.QuickTests" name="C" /></UnitTest>',
  '    <UnitTest id="t4" name="N.Outer+InnerTests.D"><TestMethod className="N.Outer+InnerTests" name="D" /></UnitTest>',
  '    <UnitTest id="t5" name="N.SkippedTests.E"><TestMethod className="N.SkippedTests" name="E" /></UnitTest>',
  '  </TestDefinitions>',
  '</TestRun>',
].join('\r\n');

test('the slowest classes are read from a trx: each class\'s tests summed, slowest first, at most ten', () => {
  assert.deepEqual(tool.slowestClasses(TRX), [
    { name: 'SlowTests', ms: 240_500, tests: 2 },
    { name: 'Outer+InnerTests', ms: 120_000, tests: 1 },
    { name: 'QuickTests', ms: 30_250, tests: 1 },
    { name: 'SkippedTests', ms: 0, tests: 1 },
  ]);
  assert.deepEqual(tool.slowestClasses(TRX, 2).map((slow) => slow.name), ['SlowTests', 'Outer+InnerTests']);
  assert.deepEqual(tool.slowestClasses('not a trx'), []);
  // Thirteen classes: ten are named.
  const many = Array.from({ length: 13 }, (_, i) => `<UnitTestResult testId="m${i}" testName="N.C${i}Tests.T" duration="00:00:${String(10 + i).padStart(2, '0')}" />`
    + `<UnitTest id="m${i}"><TestMethod className="N.C${i}Tests" name="T" /></UnitTest>`).join('\n');
  assert.equal(tool.slowestClasses(many).length, 10);
  assert.equal(tool.slowestClasses(many)[0]!.name, 'C12Tests');
});

test('a Process gate writes its trx beside its log, and the slowest classes are printed after its line', async () => {
  const fx = makeFixture('merge-branch-trx');
  const processGate = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  assert.equal(tool.trxCommand(processGate.run, join(fx.root, 'driver-process.trx')),
    `${processGate.run} --logger "trx;LogFileName=${join(fx.root, 'driver-process.trx')}"`);

  const fake = fakeStep((command) => {
    const file = /LogFileName=([^"]+)"/.exec(command)?.[1];
    if (file) writeFileSync(file, TRX);
    return { out: 'Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 7 m - D.dll (net10.0)\n', code: 0 };
  });
  const result = await tool.runGate(fx.root, processGate, fx.root, { step: fake.step });
  assert.match(fake.asked[0]!, /--logger "trx;LogFileName=.*driver-process\.trx"$/);
  assert.equal(result.trx, join(fx.root, 'driver-process.trx'));
  assert.deepEqual(result.slowest!.map((slow) => slow.name), ['SlowTests', 'Outer+InnerTests', 'QuickTests', 'SkippedTests']);

  // Its re-run of one test adds no logger: the full run's trx is the one measured.
  assert.doesNotMatch(tool.rerunCommand(processGate.run, 'N.SlowTests.A'), /trx/);
  // A gate outside the Process category writes none.
  const plain = fakeStep(() => ({ out: '', code: 0 }));
  const fast = await tool.runGate(fx.root, planned('driver', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process'), fx.root, { step: plain.step });
  assert.doesNotMatch(plain.asked[0]!, /trx/);
  assert.equal(fast.slowest, undefined);

  const printed: string[] = [];
  await tool.runGates(fx.root, [processGate], fx.root, { run: async () => result, print: (line) => printed.push(line) });
  assert.match(printed[0]!, /PASS\s+driver-process/);
  assert.match(printed[1]!, /slowest classes of driver-process, from driver-process\.trx:/);
  assert.match(printed[2]!, /^\s+1\.\s+4 m 1 s\s+2 tests\s+SlowTests$/);
  assert.match(printed[3]!, /^\s+2\.\s+2 m 0 s\s+1 test\s+Outer\+InnerTests$/);
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// End to end, in a scratch repository

// The scratch identity and line endings, by environment rather than three `git config` starts per
// repository. The tool and its gates inherit it too.
const GIT_ENV = {
  GIT_TERMINAL_PROMPT: '0',
  GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
  GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.autocrlf', GIT_CONFIG_VALUE_0: 'false',
};

function git(cwd: string, ...args: string[]): string {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', env: { ...process.env, ...GIT_ENV } });
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

const TRAILER = '\n\nCo-Authored-By: Fixture <fixture@example.test>';

// Every gate appends its name to local/ran.txt and prints a thousand lines, so a test can read the order
// the gates ran in and whether a log kept the whole output. Given `once`, it exits with its code the first
// time only, as a rehearsal that died under load and passes run again.
const GATE = [
  "import { appendFileSync, existsSync, writeFileSync } from 'node:fs';",
  "const [name, code = '0', once] = process.argv.slice(2);",
  "appendFileSync('local/ran.txt', name + '\\n');",
  'for (let i = 1; i <= 1000; i++) console.log(`${name} line ${i}`);',
  'if (once && existsSync(`local/${name}.once`)) process.exit(0);',
  "if (once) writeFileSync(`local/${name}.once`, '');",
  'process.exit(Number(code));',
  '',
].join('\n');

// `dotnet`, faked: `run` is a check, `build-server shutdown` is noted, and `test` prints what the real one
// prints. With FAKE_DOTNET=flake a real-process test fails in the suite and passes when filtered to itself,
// so the re-run's command line is proven to survive the platform shell's quoting.
const FAKE_DOTNET = [
  "import { appendFileSync } from 'node:fs';",
  'const args = process.argv.slice(2);',
  "const note = (what) => appendFileSync('local/ran.txt', what + '\\n');",
  "const pass = (n) => console.log(`Passed!  - Failed:     0, Passed:  ${n}, Skipped:     0, Total:  ${n}, Duration: 1 s - Fake.Tests.dll (net10.0)`);",
  "if (args[0] === 'build-server') { note('build-server-shutdown'); console.log('shut down'); process.exit(0); }",
  "if (args[0] === 'run') { note('dotnet-run'); console.log('checked'); process.exit(0); }",
  "const filter = args[args.indexOf('--filter') + 1];",
  "if (args.includes('--filter')) { note(`dotnet-test-alone ${filter}`); pass(1); process.exit(0); }",
  "note('dotnet-test');",
  "if (process.env.FAKE_DOTNET === 'flake') { console.log('  Failed N.ProcessJobTests.A_child [5 s]'); console.log('Failed!  - Failed:     1, Passed:    41, Skipped:     0, Total:    42, Duration: 9 s - Fake.Tests.dll (net10.0)'); process.exit(1); }",
  'pass(42); process.exit(0);',
  '',
].join('\n');

interface Scratch { root: string; run(args: string[], extra?: Record<string, string>): Promise<{ status: number | null; out: string }>; ran(): string[]; cleanup(): void }

function scratch(name: string, gates: { name: string; run: string }[], files: Record<string, string> = {}): Scratch {
  const fx = makeFixture(`merge-branch-${name}`);
  const root = join(fx.root, 'repo');
  mkdirSync(root, { recursive: true });
  // No template: the sample hooks are a dozen files each repository would write for nothing.
  git(root, 'init', '--quiet', '--template=', '-b', 'main');
  // The guard the whole file rests on: this is its own repository, never the one around it.
  assert.equal(realpathSync(git(root, 'rev-parse', '--show-toplevel')).toLowerCase(), realpathSync(root).toLowerCase());

  const write = (path: string, text: string) => {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  };
  write('.gitignore', 'local/\n');
  write('.gitattributes', 'CHANGELOG.md merge=union\n');
  write('CHANGELOG.md', '# Changelog\n\n## Unreleased\n\n');
  write('gate.mjs', GATE);
  write('daoris.gates.json', `${JSON.stringify({ gates }, null, 2)}\n`);
  for (const [path, text] of Object.entries(files)) write(path, text);
  git(root, 'add', '-A');
  git(root, 'commit', '--quiet', '-m', `first${TRAILER}`);

  const bin = join(fx.root, 'bin');
  mkdirSync(bin, { recursive: true });
  writeFileSync(join(bin, 'fake-dotnet.mjs'), FAKE_DOTNET);
  if (process.platform === 'win32') {
    writeFileSync(join(bin, 'dotnet.cmd'), '@echo off\r\nnode "%~dp0fake-dotnet.mjs" %*\r\n');
  } else {
    writeFileSync(join(bin, 'dotnet'), '#!/bin/sh\nexec node "$(dirname "$0")/fake-dotnet.mjs" "$@"\n');
    chmodSync(join(bin, 'dotnet'), 0o755);
  }

  return {
    root,
    // Asynchronous, so the end-to-end tests below can run side by side: each has a repository of its own.
    run(args, extra = {}) {
      const env: Record<string, string | undefined> = {};
      for (const [key, value] of Object.entries(process.env)) {
        if (key.toUpperCase() !== 'PATH' && key !== 'NODE_TEST_CONTEXT') env[key] = value;
      }
      env['PATH'] = `${bin}${delimiter}${process.env['PATH'] ?? process.env['Path'] ?? ''}`;
      const child = spawn(process.execPath, [TOOL, ...args], { cwd: root, env: { ...env, ...GIT_ENV, ...extra } });
      let out = '';
      child.stdout.on('data', (chunk) => { out += String(chunk); });
      child.stderr.on('data', (chunk) => { out += String(chunk); });
      return new Promise((done) => child.on('close', (status) => done({ status, out })));
    },
    ran: () => existsSync(join(root, 'local', 'ran.txt'))
      ? readFileSync(join(root, 'local', 'ran.txt'), 'utf8').split('\n').filter(Boolean) : [],
    cleanup: () => fx.cleanup(),
  };
}

function branch(root: string, name: string, files: Record<string, string>, { trailer = true, from = 'main' } = {}): void {
  git(root, 'checkout', '--quiet', '-b', name, from);
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  git(root, 'add', '-A');
  git(root, 'commit', '--quiet', '-m', `work on ${name}${trailer ? TRAILER : ''}`);
  git(root, 'checkout', '--quiet', 'main');
}

const merging = (root: string) => spawnSync('git', ['rev-parse', '-q', '--verify', 'MERGE_HEAD'], { cwd: root }).status === 0;

// Four repositories side by side, each carrying several scenarios in turn. Every git and node start
// costs tens of milliseconds on Windows, and more under the rest of the suite's load, so what needs no
// real git is tested above without it, and each start here is one a scenario needs.
describe('the tool, end to end in a scratch repository', { concurrency: true }, () => {
  test('it refuses before touching anything, and the commit check refuses what a hand-back left out', async () => {
    const repo = scratch('refusals', [{ name: 'first', run: 'node gate.mjs first' }]);
    // Merged into main, so the merge's prune removes it (GATE2); a refusal comes first and touches nothing.
    git(repo.root, 'branch', 'old');
    branch(repo.root, 'bare', { 'bare.txt': 'x\n' }, { trailer: false });
    const tree = join(dirname(repo.root), 'bare-tree');
    git(repo.root, 'worktree', 'add', '--quiet', tree, 'bare');
    writeFileSync(join(tree, 'forgotten.txt'), 'not committed\n');

    let result = await repo.run([]);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /usage/i);
    result = await repo.run(['nope']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /no branch 'nope'/);
    result = await repo.run(['bare']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /no Co-Authored-By line/);
    assert.match(result.out, /uncommitted[\s\S]*forgotten\.txt/);
    assert.match(result.out, /--no-commit-check/);
    assert.doesNotMatch(result.out, /prune/, 'the commit check refuses before the prune runs');
    writeFileSync(join(repo.root, 'stray.txt'), 'left here\n');
    result = await repo.run(['bare']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not clean:\s+\?\? stray\.txt/);
    rmSync(join(repo.root, 'stray.txt'));
    assert.equal(merging(repo.root), false, 'a refusal merged something');
    assert.deepEqual(repo.ran(), [], 'a refusal ran a gate');
    assert.doesNotMatch(result.out, /prune/, 'a refusal pruned');
    // `old` was still there to prune below: no refusal removed it.

    result = await repo.run(['bare', '--no-commit-check']);
    assert.equal(result.status, 0, result.out);
    assert.equal(merging(repo.root), true, '--no-commit-check lets the bare commit through');
    assert.match(result.out, /prune: 1 branch merged into main\n\s+removed\s+old\s+the branch \(no worktree has it checked out\)/);
    assert.ok(result.out.indexOf('removed') < result.out.indexOf('merged bare'), 'the prune runs before the merge');
    assert.equal(git(repo.root, 'branch', '--list', 'old'), '', 'the merged branch is gone');
    git(repo.root, 'merge', '--abort');
    result = await repo.run(['--drop-batch']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /forgot the batch \(bare\)/);
    git(repo.root, 'worktree', 'remove', '--force', tree);
    repo.cleanup();
  });

  test('a clean merge prints its lanes, runs every gate in order, keeps each whole output, and never commits', async () => {
    const repo = scratch('clean', [
      { name: 'first', run: 'node gate.mjs first' },
      { name: 'second', run: 'node gate.mjs second' },
    ], {
      'daoris.lanes.json': JSON.stringify({ lanes: [
        { id: 'work', title: 'Work', summary: 'The work.', paths: ['work/**'] },
        { id: 'records', title: 'Records', summary: 'The backlog and this map.', steward: true, paths: ['TASKS.md', 'daoris.lanes.json'], gates: ['first'] },
      ] }),
      'TASKS.md': '# Tasks\n',
    });
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n', 'notes.txt': 'y\n', 'TASKS.md': '# Tasks\n\n- a row\n' });

    const head = git(repo.root, 'rev-parse', 'HEAD');
    const plan = await repo.run(['--plan', 'feature/one']);
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /work\s+Work\s+1 file/);
    assert.match(plan.out, /1\.\s+first\s+suite\s+node gate\.mjs first/);
    assert.match(plan.out, /2\.\s+second/);
    assert.match(plan.out, /prune \(at the merge's start; a plan, so nothing is removed\): no branch but main is merged into main/);
    assert.equal(merging(repo.root), false, '--plan merges nothing');
    assert.match((await repo.run(['--plan', 'feature/one', '--no-prune'])).out, /prune: skipped \(--no-prune\)/);

    const result = await repo.run(['feature/one']);
    assert.equal(result.status, 0, result.out);
    // Lanes by id and title; the steward's lane is named, and so is what a subagent never edits.
    assert.match(result.out, /work\s+Work\s+1 file/);
    assert.match(result.out, /records\s+Records\s+1 file/);
    assert.match(result.out, /note: it edits the steward's records \(TASKS\.md\); a subagent never does/);
    assert.doesNotMatch(result.out, /crosses/, "the steward's lane is not a crossing: it is named on its own");
    assert.match(result.out, /outside every lane\s+1 file: notes\.txt/);
    // The steward's `gates` is for a landing of its lane alone (the queue's, D115): this tool chooses by its
    // own lane table, which names neither gate here, so both run.
    assert.deepEqual(repo.ran(), ['first', 'second']);
    assert.match(result.out, /PASS\s+first\s.*local\/scratch\/merge-feature-one\/first\.log/);
    assert.match(result.out, /PASS\s+second\s.*local\/scratch\/merge-feature-one\/second\.log/);

    const log = readFileSync(join(repo.root, 'local', 'scratch', 'merge-feature-one', 'first.log'), 'utf8');
    assert.match(log, /first line 1\r?\n/);
    assert.match(log, /first line 1000/);

    assert.equal(git(repo.root, 'rev-parse', 'HEAD'), head, 'the tool committed');
    assert.equal(git(repo.root, 'rev-parse', 'MERGE_HEAD'), git(repo.root, 'rev-parse', 'feature/one'));
    assert.match(git(repo.root, 'diff', '--cached', '--name-only'), /work\/a\.txt/);
    assert.match(result.out, /NOT committed/);
    repo.cleanup();
  });

  test('a conflict stops the merge, names the file and runs no gate; --continue gates it once resolved', async () => {
    const repo = scratch('conflict', [{ name: 'first', run: 'node gate.mjs first' }], { 'shared.txt': 'base\n' });
    branch(repo.root, 'left', { 'shared.txt': 'left\n' });
    writeFileSync(join(repo.root, 'shared.txt'), 'main\n');
    git(repo.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);

    let result = await repo.run(['left']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /conflict[\s\S]*shared\.txt/i);
    assert.deepEqual(repo.ran(), []);
    assert.equal(merging(repo.root), true, 'the merge is left for the parent to resolve');

    result = await repo.run(['--continue']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /shared\.txt/);
    assert.deepEqual(repo.ran(), []);

    writeFileSync(join(repo.root, 'shared.txt'), 'resolved\n');
    git(repo.root, 'add', 'shared.txt');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first']);
    repo.cleanup();
  });

  test('the orientation index is written into each merge from the merged tree, and a conflict in it is resolved by that writing', async () => {
    // A stand-in for the index's tool: it lists the tree's .txt files, so two branches that each add one disagree on it.
    const generator = [
      "import { mkdirSync, readdirSync, writeFileSync } from 'node:fs';",
      "const files = readdirSync('.').filter((name) => name.endsWith('.txt')).sort();",
      "mkdirSync('docs/index', { recursive: true });",
      "writeFileSync('docs/index/README.md', files.join('\\n') + '\\n');",
      "console.log('orient-index: docs/index/ written, 1 of 1 files changed');",
      '',
    ].join('\n');
    const repo = scratch('generated', [{ name: 'first', run: 'node gate.mjs first' }], {
      '.gitattributes': 'CHANGELOG.md merge=union\ndocs/index/** -merge\n',
      'tools/orient-index.mjs': generator,
      'docs/index/README.md': 'base.txt\n',
      'base.txt': 'base\n',
      'shared.md': 'base\n',
    });
    const index = () => readFileSync(join(repo.root, 'docs', 'index', 'README.md'), 'utf8');
    const unmerged = () => git(repo.root, 'diff', '--name-only', '--diff-filter=U');

    // Both sides wrote the index for their own tree: the merge stops on it alone, and writes it for the merged one.
    branch(repo.root, 'left', { 'left.txt': 'l\n', 'docs/index/README.md': 'base.txt\nleft.txt\n' });
    writeFileSync(join(repo.root, 'main.txt'), 'm\n');
    writeFileSync(join(repo.root, 'docs', 'index', 'README.md'), 'base.txt\nmain.txt\n');
    git(repo.root, 'add', '-A');
    git(repo.root, 'commit', '--quiet', '-m', `main moved${TRAILER}`);
    let result = await repo.run(['left']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /docs\/index\/: written from the merged tree \(docs\/index\/ written, 1 of 1 files changed\)/);
    assert.equal(index(), 'base.txt\nleft.txt\nmain.txt\n');
    assert.equal(unmerged(), '');
    assert.match(git(repo.root, 'diff', '--cached', '--name-only'), /docs\/index\/README\.md/);
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // Beside a conflict a person resolves, the index waits: it is written once that one is resolved.
    branch(repo.root, 'right', { 'right.txt': 'r\n', 'shared.md': 'right\n', 'docs/index/README.md': 'right only\n' });
    writeFileSync(join(repo.root, 'shared.md'), 'main\n');
    writeFileSync(join(repo.root, 'docs', 'index', 'README.md'), 'main only\n');
    git(repo.root, 'commit', '--quiet', '-am', `main moved again${TRAILER}`);
    result = await repo.run(['right']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /conflict in 1 file:\n\s+shared\.md\n\s+\(and 1 generated file, written again from the merged tree once these are resolved\)/);
    assert.deepEqual(repo.ran(), ['first']);
    writeFileSync(join(repo.root, 'shared.md'), 'resolved\n');
    git(repo.root, 'add', 'shared.md');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.equal(index(), 'base.txt\nleft.txt\nmain.txt\nright.txt\n');
    assert.equal(unmerged(), '');
    assert.deepEqual(repo.ran(), ['first', 'first']);
    repo.cleanup();
  });

  test('a batch gates each merge, waits for the parent to commit it, runs the rehearsals once after the last, and names a flake', async () => {
    const repo = scratch('batch', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'check', run: 'dotnet run --project devkit -- check' },
      { name: 'driver', run: 'dotnet test fake/Driver.Tests --settings fake/process.runsettings' },
      { name: 'deployment', run: 'npm run rehearse:deploy' },
    ], {
      // The release rehearsal dies the first time with the shell's 127, through npm, and passes run again.
      'package.json': `${JSON.stringify({ name: 'scratch', private: true, scripts: {
        rehearse: 'node gate.mjs rehearse 127 once', 'rehearse:deploy': 'node gate.mjs deployment',
      } }, null, 2)}\n`,
      // `rehearse` is the workflow's alone, as the release and family rehearsals are this repository's.
      '.github/workflows/release.yml': 'jobs:\n  release:\n    steps:\n      - run: npm run rehearse:deploy\n      - run: npm run rehearse\n',
    });
    // Both append to the changelog at one place: the union attribute keeps both, with no conflict.
    const changelog = readFileSync(join(repo.root, 'CHANGELOG.md'), 'utf8');
    branch(repo.root, 'one', { 'one.txt': '1\n', 'CHANGELOG.md': `${changelog}- **One** landed, and its line is long enough.\n` });
    branch(repo.root, 'two', { 'two.txt': '2\n', 'CHANGELOG.md': `${changelog}- **Two** landed, and its line is long enough.\n` });

    let result = await repo.run(['one', '--batch', 'two']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['dotnet-run', 'cli', 'dotnet-test'], 'the check runs first, and the rehearsals wait for the last branch');
    assert.match(result.out, /--continue/);

    // The next merge waits until this one is in main: an open merge, or an abandoned one, is refused.
    git(repo.root, 'merge', '--abort');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not committed/);
    // However the parent lands it, what --continue asks is that the branch's tip is in main's history.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-edit', 'one');

    // The second merge meets a real-process test that fails in the suite and passes alone, and a
    // rehearsal that dies once.
    result = await repo.run(['--continue'], { FAKE_DOTNET: 'flake' });
    assert.equal(result.status, 0, result.out);
    // The next merge's start prunes the branch the last one landed (GATE2).
    assert.match(result.out, /removed\s+one\s+the branch/);
    assert.deepEqual(repo.ran(), [
      'dotnet-run', 'cli', 'dotnet-test',
      'dotnet-run', 'cli', 'dotnet-test', 'dotnet-test-alone FullyQualifiedName=N.ProcessJobTests.A_child',
      'rehearse', 'rehearse', 'build-server-shutdown', 'deployment',
    ]);
    assert.match(result.out, /FLAKE\s+driver\s.*ProcessJobTests\.A_child.*driver\.rerun\.log/);
    assert.match(result.out, /FLAKE\s+rehearse\s.*exit 127.*passed when run again.*rehearse\.rerun\.log/);
    assert.match(result.out, /with 2 flakes \(driver, rehearse\)/);
    const merged = git(repo.root, 'show', ':CHANGELOG.md');
    assert.match(merged, /\*\*One\*\*/);
    assert.match(merged, /\*\*Two\*\*/);
    assert.ok(existsSync(join(repo.root, 'local', 'scratch', 'merge-two', 'deployment.log')));

    git(repo.root, 'commit', '--quiet', '--no-edit');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /batch is done/);
    // And the batch's end prunes the last.
    assert.match(result.out, /removed\s+two\s+the branch/);
    assert.equal(git(repo.root, 'branch', '--list', 'one', 'two'), '');
    assert.equal(existsSync(join(repo.root, 'local', 'scratch', 'merge-batch.json')), false);
    repo.cleanup();
  });

  test('a merge runs the gates its paths reach, and a batch\'s last merge runs each rehearsal any of its merges reached', async () => {
    const repo = scratch('reach', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'driver-process', run: 'node gate.mjs driver-process' },
      { name: 'rehearse-family', run: 'npm run rehearse:family' },
    ], {
      'package.json': `${JSON.stringify({ name: 'scratch', private: true, scripts: { 'rehearse:family': 'node gate.mjs rehearse-family' } }, null, 2)}\n`,
    });
    // The canon reaches the family rehearsal; a document reaches neither it nor the driver's Process half.
    branch(repo.root, 'canon-edit', { 'canon/core/rules/x.md': 'x\n' });
    branch(repo.root, 'docs-edit', { 'docs/y.md': 'y\n' });

    const plan = await repo.run(['--plan', 'docs-edit']);
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /gates it reaches: 1 of 3/);
    assert.match(plan.out, /driver-process\s+suite\s+no changed path reaches it/);
    assert.match(plan.out, /2 gates of the plan skipped by the lane table; the full set runs before the install is staged \(node tools\/merge-branch\.mjs --full/);

    let result = await repo.run(['canon-edit', '--batch', 'docs-edit']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['cli'], 'the rehearsal it reaches waits for the batch\'s last merge');
    assert.match(result.out, /rehearse-family\s+rehearsal\s+waits for the batch's last branch \(docs-edit\)/);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['cli', 'cli', 'rehearse-family'], 'never the Process half, which neither merge reached');
    assert.match(result.out, /rehearse-family\s+rehearsal\s+reached by canon-edit, earlier in this batch/);
    assert.match(result.out, /1 gate of the plan skipped by the lane table/);
    repo.cleanup();
  });

  test('--rerun runs a fixed gate again on the merge in place, and names the verdicts it kept and from when', async () => {
    const repo = scratch('rerun', [
      { name: 'first', run: 'node gate.mjs first' },
      // Fails the first time only, as a gate does once the parent has fixed what it found.
      { name: 'second', run: 'node gate.mjs second 3 once' },
      { name: 'third', run: 'node gate.mjs third' },
    ]);
    branch(repo.root, 'fix', { 'fix.txt': 'x\n' });
    let result = await repo.run(['--rerun', 'first']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /nothing to re-run/);

    result = await repo.run(['fix']);
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(repo.ran(), ['first', 'second']);
    assert.match(result.out, /NOT RUN\s+third/);
    assert.match(result.out, /--rerun second/, 'the failure says how to run the fixed gate alone');

    result = await repo.run(['--rerun', 'nope']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /no gate 'nope' in the plan: first, second, third/);

    result = await repo.run(['--rerun', 'second']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first', 'second', 'second', 'third'], 'the fixed gate, then the one its failure stopped; never the one that passed');
    assert.match(result.out, /kept\s+first\s+PASS\s+from \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC/);
    assert.match(result.out, /2 gates run and passed; 1 verdict kept \(first, from \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC\)/);
    assert.equal(merging(repo.root), true, 'still merged in place, and never committed');

    // --continue keeps its meaning: every gate again.
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().slice(4), ['first', 'second', 'third']);
    assert.doesNotMatch(result.out, /kept/);
    repo.cleanup();
  });

  test('--full gates the checkout as it stands, and --passed says whether the full set passed it, forgiving the records alone', async () => {
    const repo = scratch('full', [
      { name: 'first', run: 'node gate.mjs first' },
      { name: 'second', run: 'node gate.mjs second' },
    ], { 'code.txt': 'base\n' });
    let result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /the full set has NOT passed this checkout/);
    assert.match(result.out, /first\s+none\s+no verdict on this tree/);
    assert.match(result.out, /node tools\/merge-branch\.mjs --full/);

    result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first', 'second']);
    assert.match(result.out, /PASS\s+first\s.*local\/scratch\/full-[0-9a-f]{7}\/first\.log/);
    assert.equal(merging(repo.root), false, '--full alone merges nothing');
    const record = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { schema: number; verdicts: Verdict[] };
    assert.equal(record.schema, 1);
    assert.deepEqual(record.verdicts.map((verdict) => [verdict.gate, verdict.verdict]), [['first', 'PASS'], ['second', 'PASS']]);
    assert.equal(record.verdicts[0]!.tree, git(repo.root, 'rev-parse', 'HEAD^{tree}'), 'the tree that was gated, the clean checkout\'s');
    assert.equal(record.verdicts[0]!.commit, git(repo.root, 'rev-parse', 'HEAD'));

    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /the full set has passed this checkout/);
    assert.match(result.out, /first\s+PASS\s+\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC\s+on this tree/);

    // The parent writes the records after the gates: a tree that differs only by them is forgiven.
    writeFileSync(join(repo.root, 'CHANGELOG.md'), '# Changelog\n\n## Unreleased\n\n- **A** line, long enough.\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /on a tree that differs from this one only by the records \(CHANGELOG\.md\)/);
    // Code is not.
    writeFileSync(join(repo.root, 'code.txt'), 'changed\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /second\s+none/);
    repo.cleanup();
  });

  test('--prune removes each merged branch with its worktree, and keeps one in use, changed or holding private output, saying why', async () => {
    const repo = scratch('prune', [{ name: 'first', run: 'node gate.mjs first' }], { '.gitignore': 'local/\nbuild/\n', 'tracked.txt': 'base\n' });
    const trees = join(dirname(repo.root), 'trees');
    const add = (name: string): string => {
      const path = join(trees, name);
      git(repo.root, 'worktree', 'add', '--quiet', '-b', name, path, 'main');
      return path;
    };
    // `ahead` has a commit main lacks, made before main moved on: never merged, so never considered.
    branch(repo.root, 'ahead', { 'ahead.txt': 'x\n' });
    git(repo.root, 'commit', '--quiet', '--allow-empty', '-m', `main moves on${TRAILER}`);
    git(repo.root, 'worktree', 'add', '--quiet', join(trees, 'ahead'), 'ahead');

    // Clean but for ignored build output, which a plain `git worktree remove` takes with it.
    const free = add('free');
    mkdirSync(join(free, 'build'), { recursive: true });
    writeFileSync(join(free, 'build', 'out.dll'), 'built\n');
    // At main's tip, so merged: the case a naive rule deletes while an agent works in it. Locked, it is in
    // use: `fresh` is an agent just dispatched, clean, and `in-use` one mid-edit, which is never looked into.
    const fresh = add('fresh');
    git(repo.root, 'worktree', 'lock', '--reason', 'claude agent agent-y (pid 2)', fresh);
    const inUse = add('in-use');
    git(repo.root, 'worktree', 'lock', '--reason', 'claude agent agent-x (pid 1)', inUse);
    writeFileSync(join(inUse, 'tracked.txt'), 'the agent is mid-edit\n');
    writeFileSync(join(add('changed'), 'tracked.txt'), 'changed\n');
    const kept = add('private');
    mkdirSync(join(kept, 'local'), { recursive: true });
    writeFileSync(join(kept, 'local', 'out.md'), 'an agent left this\n');
    const stray = add('stray');
    writeFileSync(join(stray, 'new.txt'), 'never added\n');
    // Its folder deleted by hand: git still lists the worktree, and holds the branch for it.
    rmSync(add('gone'), { recursive: true, force: true });
    git(repo.root, 'branch', 'plain');
    // Merged into main, but its upstream lacks main's last commit, so `git branch -d` refuses; nothing forces it.
    git(repo.root, 'branch', 'tracking');
    git(repo.root, 'config', 'branch.tracking.remote', '.');
    git(repo.root, 'config', 'branch.tracking.merge', 'refs/heads/ahead');
    // A folder git does not know is not the prune's to remove.
    mkdirSync(join(trees, 'orphan'), { recursive: true });
    writeFileSync(join(trees, 'orphan', 'keep.txt'), 'mine\n');
    const branches = () => git(repo.root, 'branch', '--format=%(refname:short)').split('\n');
    const every = branches();

    const plan = await repo.run(['--prune', '--plan']);
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /prune \(a plan, so nothing is removed\): 9 branches merged into main/);
    assert.match(plan.out, /\n\s+keep\s+fresh\s+its worktree is locked, so in use \(claude agent agent-y \(pid 2\)\)/);
    assert.match(plan.out, /\n\s+remove\s+free\s+its worktree \.\.\/trees\/free and the branch\n/);
    assert.match(plan.out, /\n\s+remove\s+gone\s+its worktree's record \(the folder \.\.\/trees\/gone is gone already\) and the branch\n/);
    assert.match(plan.out, /\n\s+remove\s+plain\s+the branch \(no worktree has it checked out\)\n/);
    assert.match(plan.out, /\n\s+remove\s+tracking\s/, 'only the deletion itself can hear git refuse');
    const inUseLine = plan.out.split('\n').find((line) => /^\s+keep\s+in-use\s/.test(line)) ?? '';
    assert.match(inUseLine, /its worktree is locked, so in use \(claude agent agent-x \(pid 1\)\)/);
    assert.doesNotMatch(inUseLine, /tracked/, 'a locked worktree is never looked into');
    assert.match(plan.out, /\n\s+keep\s+changed\s+its worktree has modified or staged tracked files \(tracked\.txt\)\n/);
    assert.match(plan.out, /\n\s+keep\s+private\s+its worktree has private output under local\/ \(local\/out\.md\), which nothing else keeps\n/);
    assert.match(plan.out, /\n\s+keep\s+stray\s+its worktree has untracked files git removes only with --force \(new\.txt\)\n/);
    assert.ok(!plan.out.split('\n').some((line) => /^\s+(remove|keep)\s+(ahead|main)\s/.test(line)), 'an unmerged branch, and main, are never considered');
    assert.deepEqual(branches(), every, '--plan removes nothing');
    assert.ok(existsSync(free));

    const result = await repo.run(['--prune']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /prune: 9 branches merged into main\n/);
    for (const name of ['free', 'gone', 'plain']) assert.match(result.out, new RegExp(`\\n\\s+removed\\s+${name}\\s`));
    for (const name of ['changed', 'fresh', 'in-use', 'private', 'stray']) assert.match(result.out, new RegExp(`\\n\\s+kept\\s+${name}\\s`));
    assert.match(result.out, /\n\s+kept\s+tracking\s+git refused to delete the branch, which is never forced: error: the branch 'tracking' is not fully merged/);

    assert.deepEqual(branches(), ['ahead', 'changed', 'fresh', 'in-use', 'main', 'private', 'stray', 'tracking']);
    const listed = tool.parseWorktrees(git(repo.root, 'worktree', 'list', '--porcelain'));
    assert.deepEqual(listed.map((t) => t.branch).sort(), ['ahead', 'changed', 'fresh', 'in-use', 'main', 'private', 'stray']);
    assert.ok(listed.filter((t) => t.locked).map((t) => t.branch).sort().join() === 'fresh,in-use', 'a locked worktree is never unlocked');
    assert.equal(readFileSync(join(inUse, 'tracked.txt'), 'utf8'), 'the agent is mid-edit\n');
    assert.equal(existsSync(free), false, "the removed worktree's folder is gone, its ignored build output with it");
    assert.ok(existsSync(join(kept, 'local', 'out.md')) && existsSync(join(stray, 'new.txt')));
    assert.equal(readFileSync(join(trees, 'orphan', 'keep.txt'), 'utf8'), 'mine\n');
    repo.cleanup();
  });
});

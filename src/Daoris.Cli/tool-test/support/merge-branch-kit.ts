import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, lstatSync, mkdirSync, readdirSync, readFileSync, realpathSync, utimesSync, writeFileSync, writeSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { makeFixture, PATIENCE } from '../../test/_fixture.ts';
import { TOOL, tool, type Gate, type Worktree, type Step, type GateResult, type Beside, type Reading, type Judging, processRun } from './merge-branch.ts';


// ---------------------------------------------------------------------------------------------------
// The prune: which merged branches go, with their worktrees, and which stay and why

export const tree = (path: string, branch: string, more: Partial<Worktree> = {}): Worktree => ({
  path, branch, locked: false, lockReason: '', prunable: false, bare: false, ...more,
});


// ---------------------------------------------------------------------------------------------------
// Flakes: a real-process test that fails is run alone once

export const PROCESS_FAILURE = [
  'Test run for Daoris.Desktop.Driver.Tests.dll (.NETCoreApp,Version=v10.0)',
  '  Failed Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked [5 s]',
  '  Error Message:',
  '   Assert.True() Failure',
  'Failed!  - Failed:     1, Passed:  1144, Skipped:     0, Total:  1145, Duration: 3 m 4 s - Daoris.Desktop.Driver.Tests.dll (net10.0)',
].join('\r\n');


export const CATALOGUE_FAILURE = [
  '  Failed Daoris.Desktop.Modules.Tests.RefusalCatalogueTests.Every_refusal_a_module_can_raise_has_a_translation(language: "zh") [< 1 ms]',
  '  Failed Daoris.Desktop.Modules.Tests.RefusalCatalogueTests.Every_refusal_a_module_can_raise_has_a_translation(language: "en") [< 1 ms]',
  'Failed!  - Failed:     2, Passed:   397, Skipped:     0, Total:   399, Duration: 1 m 1 s - Daoris.Desktop.Modules.Tests.dll (net10.0)',
].join('\n');


// The tail of a real measured run (the list reporter, a retry beside it): the summary names each failed spec.
export const TIMED_SPEC = String.raw`[chromium] › measured\large.spec.ts:73:3 › timed › large/Hundred: 100 nodes, p95 frame within 16.7 ms while panning and zooming, the DOM bounded`;

export const PLAIN_SPEC = String.raw`[chromium] › measured\canvas.spec.ts:108:3 › canvas/Lifecycle: the ink, every box and every ＋ in world units`;

export const measuredLog = (specs: string[], count = specs.length, { retry = true } = {}) => [
  '$ npm run test:web:measured',
  '> daoris-web@0.0.1 e2e:measured',
  `  ✘    3 ${TIMED_SPEC} (14.7s)`,
  ...(retry ? [`  ✘  141 ${TIMED_SPEC} (retry #1) (6.2s)`] : []),
  '  ✓  157 [chromium] › measured\\large.spec.ts:73:3 › timed › large/Thousand: 1000 nodes (5.9s)',
  '',
  `  ${count} failed`,
  ...specs.map((spec) => `    ${spec} `),
  '  287 passed (9.1m)',
  '',
  '(exited 1)',
].join('\n');


// ---------------------------------------------------------------------------------------------------
// Running gates, with the step that starts a process replaced: the stop, the logs, the flakes

export const planned = (name: string, run: string): Gate => ({ name, run, kind: 'suite' });


/**
 * A step that answers each command from a function and records what it is asked, in order. It heads
 * its output with the command, as `runStep` does, since a rehearsal's own output is read from there.
 */
export function fakeStep(answer: (command: string) => { out: string; code: number }): { asked: string[]; step: Step } {
  const asked: string[] = [];
  const step: Step = async (command, _cwd, fd) => {
    asked.push(command);
    const { out, code } = answer(command);
    writeSync(fd, `$ ${command}\n${out}`);
    return code;
  };
  return { asked, step };
}


// ---------------------------------------------------------------------------------------------------
// The light gates run beside the Process lane, which stays one gate at a time

/** A promise resolved from outside. */
export function signal(): { promise: Promise<void>; resolve: () => void } {
  let resolve!: () => void;
  const promise = new Promise<void>((done) => { resolve = done; });
  return { promise, resolve };
}


/** What a fake gate waits for, failing the test rather than hanging it when that never comes: two gates run in turn never overlap. */
export async function within(promise: Promise<unknown>, what: string, ms = PATIENCE): Promise<void> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  const late = new Promise<never>((_, fail) => { timer = setTimeout(() => fail(new Error(`never happened: ${what}`)), ms); });
  try {
    await Promise.race([promise, late]);
  } finally {
    clearTimeout(timer);
  }
}


/**
 * A fake gate runner for the lanes: each gate notes its start and its end, ends when what `until` names for it has
 * happened, and fails when `failing` names it. What each gate is handed beside it is kept.
 */
export function laneRun(names: string[]) {
  const events: string[] = [];
  const started = Object.fromEntries(names.map((name) => [name, signal()]));
  const ended = Object.fromEntries(names.map((name) => [name, signal()]));
  const until: Record<string, () => Promise<unknown>> = {};
  const failing = new Set<string>();
  const heard: Record<string, Beside> = {};
  const run = async (_root: string, gate: Gate, at: string, beside: Beside = {}): Promise<GateResult> => {
    heard[gate.name] = beside;
    events.push(`start ${gate.name}`);
    started[gate.name]!.resolve();
    const wait = until[gate.name];
    if (wait) await within(wait(), `what ${gate.name} waits for`);
    events.push(`end ${gate.name}`);
    ended[gate.name]!.resolve();
    const failed = failing.has(gate.name);
    return { gate, code: failed ? 1 : 0, log: join(at, `${gate.name}.log`), verdict: failed ? 'FAIL' : 'PASS', note: failed ? 'exit 1' : '', ms: 1000 };
  };
  return { events, started, ended, until, failing, heard, run };
}


/** The summary's gates and verdicts, from the line that heads it on. */
export function summary(printed: string[]): string[] {
  const at = printed.findIndex((line) => /in the plan's order/.test(line));
  assert.ok(at >= 0, `no summary in:\n${printed.join('\n')}`);
  return printed.slice(at + 1).map((line) => /^\s+(PASS|FAIL|FLAKE|MEMORY|NOT RUN)\s+(\S+)/.exec(line)!.slice(1).join(' '));
}


// a and b build one component, so the light lane runs them in turn, as both lanes' tests below need.
export const laneGates = (): Gate[] => [
  planned('a', 'dotnet build src/L/A'), planned('b', 'dotnet build src/L/B'), planned('p', processRun('fake/P.Tests')), planned('q', processRun('fake/Q.Tests')),
];


// ---------------------------------------------------------------------------------------------------
// The family rehearsal beside the driver's git half; the web gate's unit run, a light gate of its own

/** The projects a rehearsal script names as `const name = join(repoRoot, 'src', …)`, by name, as repository paths. */
export function rehearsalProjects(text: string): Record<string, string> {
  const named: Record<string, string> = {};
  for (const [, name, parts] of text.matchAll(/const (\w+) = join\(repoRoot, ((?:'[^']+'(?:,\s*)?)+)\);/g)) {
    named[name!] = [...parts!.matchAll(/'([^']+)'/g)].map((part) => part[1]).join('/');
  }
  return named;
}


// ---------------------------------------------------------------------------------------------------
// A rehearsal that died, rather than failed, is run again once

// What the tool and npm write around a rehearsal's own lines: none of it is the rehearsal speaking.
export const NPM_BANNER = '$ npm run rehearse:family\n\n> daoris-workspace@0.0.1 rehearse:family\n> node tools/family-rehearsal.mjs\n\n';

export const OK_LINES = '\n1. The example family\n  ok    the HTTP host builds\n  ok    engine is current and clean\n';


// ---------------------------------------------------------------------------------------------------
// A heavy gate waits for memory rather than failing for want of it, and a gate that died of memory is said so
// and run again once, alone. On 2026-10-22 a full set beside the install's driven sessions and two builders ran a 64 GB
// machine out of commit: the sensitive scan threw OutOfMemoryException, web-rules could not allocate, the family
// rehearsal's landing said "Out of memory." twice, each read as the change's failure, and each passed run later.

export const GiB = 2 ** 30;


/**
 * A stand-in machine (never the real one): each reading is the next of `commits` (GB of free commit, the last one kept),
 * with `physical` GB of free physical memory and `total` GB in all; each read is noted, in `events` too when given. Its
 * clock moves only when the wait sleeps.
 */
export function standIn(commits: number[], { physical = 32, total = 64, events }: { physical?: number; total?: number; events?: string[] } = {}) {
  let at = 0;
  let clock = 0;
  const reads: number[] = [];
  const slept: number[] = [];
  const read = (): Reading => {
    const commit = commits[Math.min(at, commits.length - 1)]!;
    at += 1;
    reads.push(commit);
    events?.push(`read ${commit}`);
    return { physical: physical * GiB, commit: commit * GiB, total: total * GiB };
  };
  return { read, reads, slept, sleep: async (ms: number) => { slept.push(ms); clock += ms; }, now: () => clock };
}


/** The lines a gate that died of memory leaves, one per way the 2026-10-22 run and its like said it. */
export const MEMORY_SIGNS = [
  "Unhandled exception. System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.",
  'fatal error: runtime: cannot allocate memory',
  'DataCloneError: Data cannot be cloned, out of memory.',
  'Out of memory.',
  'FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory',
  'Error: The paging file is too small for this operation to complete.',
];


/** A clock gate and a light gate that ends only once the clock gate's first line is printed, run as `runGates` runs them. */
export async function measuredRun(name: string, firstLog: string, second: { out: string; code: number }, { clock = true, run = 'npm run test:web:measured', hold = true } = {}) {
  const fx = makeFixture(name);
  const measured = { ...planned('web-measured', run), ...(clock ? { clock: true } : {}) } as Gate;
  const beside = planned('u', 'node u.mjs');
  const firstLine = signal();
  const asked: string[] = [];
  const printed: string[] = [];
  let runs = 0;
  const step: Step = async (command, _cwd, fd) => {
    asked.push(command);
    if (command === beside.run) {
      if (hold) await within(firstLine.promise, "the measured gate's first line");
      asked.push('u ended');
    }
    const first = command === measured.run && ++runs === 1;
    const answer = command === beside.run ? { out: 'ok\n', code: 0 } : first ? { out: firstLog, code: 1 } : second;
    writeSync(fd, `$ ${command}\n${answer.out}`);
    return answer.code;
  };
  const results = await tool.runGates(fx.root, [measured, beside], fx.root, {
    run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step }),
    print: (line) => {
      printed.push(line);
      if (/^\s+(FAIL|FLAKE|PASS)\s+web-measured/.test(line)) firstLine.resolve();
    },
  });
  firstLine.resolve();
  fx.cleanup();
  return { results, asked, printed, measured };
}


// A tool-tests log's end: a git child that exited 4294967295 beside builders, and a test the run's own clock ended.
export const NODE_TITLE_A = "INTEGRATE1j: a tree the caller hands is the run's tree: the runner makes none, takes no lock and copies no private file; one at another commit is refused";

export const NODE_TITLE_B = 'INTEGRATE1b: a gate that fails once beside a held load and passes quiet holding the whole admission is a sighting; under split, the default with no policy written, the candidate splits (a+b) [x]';

export const nodeLog = (failures: [string, string][], fail = failures.length) => [
  '$ npm run tooltests',
  '> daoris@0.0.1 tooltests',
  '> node --test "tool-test/**/*.ts" ../../tools/as-merged.test.mjs',
  'ℹ tests 685', 'ℹ suites 21', 'ℹ pass 677', `ℹ fail ${fail}`, 'ℹ duration_ms 1592628.3636', '',
  '✖ failing tests:', '',
  ...failures.flatMap(([file, title]) => [
    `test at ${file}:2334:3`,
    `✖ ${title} (219308.091ms)`,
    '  AssertionError [ERR_ASSERTION]: git add -A: ',
    '  ',
    '  4294967295 !== 0',
    '  ',
    '      at sh (file:///x/src/Daoris.Cli/tool-test/merge-queue.test.ts:1279:10) {',
    "    code: 'ERR_ASSERTION',",
    '  }',
    '',
  ]),
  '(exited 1)',
].join('\n');


/**
 * A `tooltests` gate (read from the fixture's package.json files, not named) and a light gate that ends only once the first
 * has printed its line, run as `runGates` runs them; `second` answers each re-run asked.
 */
export async function nodeRun(name: string, firstLog: string, second: { out: string; code: number }, { run = 'npm run tooltests', hold = true } = {}) {
  const fx = makeFixture(name);
  fx.write('package.json', JSON.stringify({ workspaces: ['pkg'], scripts: { tooltests: 'npm run tooltests -w cli', verify: 'npm run check -w cli && node --test tools/x.test.mjs && node tools/doc.mjs', other: 'node tools/other.mjs' } }));
  fx.write('pkg/package.json', JSON.stringify({ name: 'cli', scripts: { tooltests: 'node --test "tool-test/**/*.ts"', check: 'tsc --noEmit' } }));
  for (const file of ['pkg/tool-test/merge-queue.test.ts', 'pkg/tool-test/a.test.ts', 'tools/x.test.mjs']) fx.write(file, '');
  const tests = { ...planned('tool-tests', run), clock: true } as Gate;
  const beside = planned('u', 'node u.mjs');
  const firstLine = signal();
  const asked: { command: string; cwd: string }[] = [];
  const printed: string[] = [];
  let runs = 0;
  const step: Step = async (command, cwd, fd) => {
    asked.push({ command, cwd });
    if (command === beside.run && hold) await within(firstLine.promise, "the tests gate's first line");
    const answer = command === beside.run ? { out: 'ok\n', code: 0 } : ++runs === 1 ? { out: firstLog, code: 1 } : second;
    writeSync(fd, `$ ${command}\n${answer.out}`);
    return answer.code;
  };
  const results = await tool.runGates(fx.root, [tests, beside], fx.root, {
    run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step }),
    print: (line) => {
      printed.push(line);
      if (/^\s+(FAIL|FLAKE|PASS)\s+tool-tests/.test(line)) firstLine.resolve();
    },
  });
  firstLine.resolve();
  const root = fx.root;
  fx.cleanup();
  return { results, asked, printed, tests, root };
}


// ---------------------------------------------------------------------------------------------------
// A gate re-run on the checkout as it stands, and the smallest command a refusal names

export const hourOf = (hour: number) => `2026-10-05T${String(hour).padStart(2, '0')}:00:00Z`;

// T is the checkout; S is a tree before a fix that reaches every gate here.
export const fixJudging: Judging = {
  tree: 'T', drift: (from: string) => ({ S: ['src/fix.ts'] } as Record<string, string[]>)[from] ?? null,
  isRecord: () => false, reaching: (_gate: string, paths: string[]) => paths,
};


// ---------------------------------------------------------------------------------------------------
// A Process gate leaves a trx with each test's duration, and the slowest classes are named

export const TRX = [
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


// A superseded run's running gates end too, or a gate on an old tip runs up to an hour for a verdict nobody would use

export const sleep = (ms: number) => new Promise((done) => setTimeout(done, ms));


/** Whether a process still runs: signal 0 asks and sends nothing. */
export const alive = (pid: number): boolean => {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === 'EPERM';
  }
};


// A gate with a tree under it: it starts a grandchild, each writes its pid into the folder named (argument or TREE_PIDS),
// and both wait ten minutes, far past any wait here, so only an end from outside stops them.
export const TREE = [
  "import { spawn } from 'node:child_process';",
  "import { writeFileSync } from 'node:fs';",
  'const at = process.argv[2] ?? process.env.TREE_PIDS;',
  "const wait = \"require('fs').writeFileSync(process.argv[1], String(process.pid)); setTimeout(() => {}, 600000)\";",
  "spawn(process.execPath, ['-e', wait, at + '/grandchild.pid'], { stdio: 'ignore', windowsHide: true });",
  "writeFileSync(at + '/child.pid', String(process.pid));",
  'setTimeout(() => {}, 600000);',
  '',
].join('\n');


/** The two pids a TREE gate wrote under `at`, once both have (bounded: a start under load). */
export async function treePids(at: string, ms = PATIENCE): Promise<number[]> {
  const files = ['child.pid', 'grandchild.pid'].map((name) => join(at, name));
  const read = (file: string) => (existsSync(file) ? Number(readFileSync(file, 'utf8')) : 0);
  for (let waited = 0; waited < ms && !files.every((file) => read(file) > 0); waited += 100) await sleep(100);
  const pids = files.map(read);
  assert.ok(pids.every((pid) => pid > 0 && alive(pid)), `the gate's child and grandchild never both ran: ${pids.join(', ')}`);
  return pids;
}


/** Waits for every pid to have ended, a little patience for an exit the system is still recording: those still running. */
export async function stillRunning(pids: number[], ms = PATIENCE): Promise<number[]> {
  for (let waited = 0; waited < ms && pids.some(alive); waited += 100) await sleep(100);
  return pids.filter(alive);
}


/** A scratch folder removed just after its processes were killed: Windows may still hold it a moment, and `rmSync` does not
 * wait on a denied access as it does on a busy one. */
export async function cleanupAfterKill(fx: { cleanup(): void }): Promise<void> {
  for (const end = Date.now() + PATIENCE; ;) {
    try {
      fx.cleanup();
      return;
    } catch (error) {
      if (Date.now() > end || !['EPERM', 'EBUSY', 'EACCES'].includes((error as NodeJS.ErrnoException).code ?? '')) throw error;
      await sleep(250);
    }
  }
}


// Killed outright (no SIGINT), the run's node leaves its steps' shells and everything under them running
export const PARENT = [
  "import { spawn } from 'node:child_process';",
  "import { mkdirSync, writeFileSync } from 'node:fs';",
  "const [home, tree, at] = process.argv.slice(2);",
  "mkdirSync(home + '/local', { recursive: true });",
  "writeFileSync(home + '/local/gate-tree.lock', JSON.stringify({ pid: process.pid, commit: 'a'.repeat(40), at: new Date().toISOString() }) + '\\n');",
  // A direct child that outlives its parent, as a step's shell does under a console. Detached: a child node starts in
  // libuv's job object and ends with its killed parent, as a `cmd.exe` shell does in this harness (the limit of
  // `endOrphans`, which lists the dead pid's direct children only: what such a shell started is left under a dead parent).
  "spawn(process.execPath, [tree, at], { detached: true, stdio: 'ignore', windowsHide: true }).unref();",
  'setTimeout(() => {}, 600000);',
  '',
].join('\n');


// A non-detached shell ends with its killed parent here, and its child runs on under a dead parent id, which a
// listing by the dead pid cannot find: the gate tree's folder in the command line does.
export const SHELL_PARENT = [
  "import { spawn } from 'node:child_process';",
  "import { mkdirSync, writeFileSync } from 'node:fs';",
  "const [home, tree, at] = process.argv.slice(2);",
  "mkdirSync(home + '/local', { recursive: true });",
  "writeFileSync(home + '/local/gate-tree.lock', JSON.stringify({ pid: process.pid, commit: 'a'.repeat(40), at: new Date().toISOString() }) + '\\n');",
  'spawn(`"${process.execPath}" "${tree}" "${at}"`, { shell: true, stdio: \'ignore\', windowsHide: true });',
  'setTimeout(() => {}, 600000);',
  '',
].join('\n');


// ---------------------------------------------------------------------------------------------------
// End to end, in a scratch repository

// The scratch identity and line endings, by environment rather than three `git config` starts per
// repository. The tool and its gates inherit it too.
export const GIT_ENV = {
  GIT_TERMINAL_PROMPT: '0',
  GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
  GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.autocrlf', GIT_CONFIG_VALUE_0: 'false',
};


// A builder's worktree sets the variables that locate a repository; inherited, they send a fixture's git to the real one's .git
export const LOCATING = /^GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES)$/i;

/** A test runner's own variables (node:test's, Vitest's, `NODE_ENV`), which the tool under test never inherits. */
export const RUNNER_OWN = /^(NODE_TEST_CONTEXT|NODE_ENV|VITEST\w*)$/i;
export const fixtureEnv = () => Object.fromEntries(Object.entries(process.env).filter(([key]) => !LOCATING.test(key)));


export function git(cwd: string, ...args: string[]): string {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', env: { ...fixtureEnv(), ...GIT_ENV }, windowsHide: true });
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}


export const TRAILER = '\n\nCo-Authored-By: Claude Fixture (Anthropic) <fixture@example.test>';


/**
 * A worktree as a finished agent's is, nothing in it or in its git folder written for `minutes`: every
 * entry's times set back (links are not followed), and the files of the git folder a worktree keeps beside the repository.
 */
export function age(worktree: string, minutes: number): void {
  const when = new Date(Date.now() - minutes * 60_000);
  const back = (path: string) => {
    const info = lstatSync(path);
    if (info.isSymbolicLink()) return;
    if (info.isDirectory()) for (const name of readdirSync(path)) back(join(path, name));
    utimesSync(path, when, when);
  };
  const pointer = /^gitdir: (.+)$/m.exec(readFileSync(join(worktree, '.git'), 'utf8'));
  assert.ok(pointer, 'the worktree has a .git file');
  back(pointer[1]!.trim());
  back(worktree);
}


// Every gate appends its name to local/ran.txt and prints a thousand lines, so a test can read the order
// the gates ran in and whether a log kept the whole output. Given `once`, it exits with its code the first
// time only, as a rehearsal that died under load and passes run again.
// Given GATE_HOLD, it waits until that file is written (ten minutes at most: load makes a minute too short), as a long gate runs beside a test.
export const GATE = [
  "import { appendFileSync, existsSync, writeFileSync } from 'node:fs';",
  "const [name, code = '0', once] = process.argv.slice(2);",
  "appendFileSync('local/ran.txt', name + '\\n');",
  'const hold = process.env.GATE_HOLD;',
  'for (let waited = 0; hold && !existsSync(hold) && waited < 600000; waited += 50) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 50);',
  'for (let i = 1; i <= 1000; i++) console.log(`${name} line ${i}`);',
  'if (once && existsSync(`local/${name}.once`)) process.exit(0);',
  "if (once) writeFileSync(`local/${name}.once`, '');",
  'process.exit(Number(code));',
  '',
].join('\n');


// A gate for the CI's runs, which write their logs in the main checkout and leave the gate tree's `local/` unmade: it
// makes its own, notes its name, and fails where CI_FAIL names it, as a change that broke it would.
export const CI_GATE = [
  "import { appendFileSync, mkdirSync } from 'node:fs';",
  'const name = process.argv[2];',
  "mkdirSync('local', { recursive: true });",
  "appendFileSync('local/ran.txt', name + '\\n');",
  'for (let i = 1; i <= 3; i++) console.log(`${name} line ${i}`);',
  "process.exit((process.env.CI_FAIL ?? '').split(',').includes(name) ? 1 : 0);",
  '',
].join('\n');


/**
 * `dotnet test`, faked for a Process half run by reach. It reads the filter of the settings it is handed, runs each
 * class of the project's `*Tests.cs` that no `FullyQualifiedName!~<class>.` keeps out, notes which in `local/ran.txt`, and writes
 * the trx the merge tool asked for, a test a class, as the real one does.
 */
export const FAKE_DOTNET_REACH = [
  "import { appendFileSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';",
  "import { join } from 'node:path';",
  'const args = process.argv.slice(2);',
  "if (args[0] !== 'test') process.exit(0);",
  'const project = args[1];',
  "const settings = readFileSync(args[args.indexOf('--settings') + 1], 'utf8');",
  "const trx = /LogFileName=(.+)$/.exec(args[args.indexOf('--logger') + 1] ?? '')?.[1];",
  "const filter = (/<TestCaseFilter>([^<]*)</.exec(settings)?.[1] ?? '').replace(/&amp;/g, '&');",
  "const kept = [...filter.matchAll(/FullyQualifiedName!~([^&]+)\\./g)].map((m) => m[1]);",
  'const classes = [];',
  "for (const file of readdirSync(project).filter((name) => name.endsWith('Tests.cs'))) {",
  "  const text = readFileSync(join(project, file), 'utf8');",
  '  const space = /namespace ([\\w.]+);/.exec(text)[1];',
  '  for (const m of text.matchAll(/class (\\w+Tests)\\b/g)) classes.push(`${space}.${m[1]}`);',
  '}',
  'const run = classes.filter((name) => !kept.includes(name)).sort();',
  "mkdirSync('local', { recursive: true });",
  "appendFileSync('local/ran.txt', `dotnet-test ${run.map((name) => name.split('.').at(-1)).join(' ')}\\n`);",
  'const results = run.map((name, i) => `<UnitTestResult testId="t${i}" testName="${name}.Runs" duration="00:00:01" outcome="Passed" />`).join(\'\');',
  'const tests = run.map((name, i) => `<UnitTest id="t${i}" name="${name}.Runs"><TestMethod className="${name}" name="Runs" /></UnitTest>`).join(\'\');',
  'if (trx) writeFileSync(trx, `<TestRun><Results>${results}</Results><TestDefinitions>${tests}</TestDefinitions></TestRun>`);',
  'console.log(`Passed!  - Failed:     0, Passed:  ${run.length}, Skipped:     0, Total:  ${run.length}, Duration: 1 s - Desk.Tests.dll (net10.0)`);',
  '',
].join('\n');


/** A desktop of its own for the CI's runs by reach: a test project compiling against two areas, and three Process classes. */
export const DESK_FILES: Record<string, string> = {
  'src/Desk/process.runsettings': '<RunSettings><RunConfiguration><TestCaseFilter>Category=Process</TestCaseFilter></RunConfiguration></RunSettings>\n',
  'src/Desk/Desk.Tests/Desk.Tests.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="..\\Desk.Core\\Desk.Core.csproj" /></ItemGroup></Project>\n',
  'src/Desk/Desk.Core/Desk.Core.csproj': '<Project Sdk="Microsoft.NET.Sdk" />\n',
  'src/Desk/Desk.Core/Alpha/AlphaThing.cs': 'namespace Desk;\npublic sealed class AlphaThing { }\n',
  'src/Desk/Desk.Core/Beta/BetaThing.cs': 'namespace Desk;\npublic sealed class BetaThing { }\n',
  'src/Desk/Desk.Tests/GlobalUsings.cs': 'global using Xunit;\n',
  'src/Desk/Desk.Tests/Shared.cs': 'namespace Desk.Tests;\ninternal static class Shared { public const int Seed = 1; }\n',
  'src/Desk/Desk.Tests/AlphaTests.cs': 'namespace Desk.Tests;\n[Trait("Category", "Process")]\npublic sealed class AlphaTests { [Fact] public void Runs() { _ = new AlphaThing(); _ = Shared.Seed; } }\n',
  'src/Desk/Desk.Tests/BetaTests.cs': 'namespace Desk.Tests;\n[Trait("Category", "Process")]\npublic sealed class BetaTests { [Fact] public void Runs() { _ = new BetaThing(); _ = Shared.Seed; } }\n',
  // Planted: its sources name nothing the census places, so every change that reaches its half runs it.
  'src/Desk/Desk.Tests/LonelyTests.cs': 'namespace Desk.Tests;\n[Trait("Category", "Process")]\npublic sealed class LonelyTests { [Fact] public void Runs() { _ = Shared.Seed; } }\n',
};


/**
 * What a test waits for, read again until it holds, failing rather than hanging when it never does. The bound
 * is the tool tests' hang guard, five patiences (ten minutes, as `HANG_MS` in merge-queue.test.ts): it elapses only on failure,
 * and a watch gating a tip under a whole-file run's load took longer than two minutes (287 s run).
 */
export async function waitFor(holds: () => boolean, what: string, ms = 5 * PATIENCE): Promise<void> {
  for (const end = Date.now() + ms; !holds();) {
    if (Date.now() > end) throw new Error(`never happened: ${what}`);
    await new Promise((done) => setTimeout(done, 100));
  }
}


// `dotnet`, faked: `run` is a check, `build-server shutdown` is noted, and `test` prints what the real one
// prints. With FAKE_DOTNET=flake a real-process test fails in the suite and passes when filtered to itself,
// so the re-run's command line is proven to survive the platform shell's quoting.
export const FAKE_DOTNET = [
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


/** What ran, or what is recorded, in name order: light gates that build nothing in common run side by side. */
export const unordered = (list: string[]): string[] => [...list].sort();


/**
 * A background run's receipt on HEAD's tree, as `local/ci-runs.jsonl` holds one, for the tests whose subject is the
 * gates' judgement rather than where the receipt comes from: a stage needs one beside its gates' verdicts.
 */
export function receipt(root: string, fields: { kind?: string; finished?: boolean } = {}): void {
  const file = join(root, 'local', 'ci-runs.jsonl');
  mkdirSync(dirname(file), { recursive: true });
  const held = existsSync(file) ? readFileSync(file, 'utf8') : '';
  const at = new Date().toISOString().replace(/\.\d+Z$/, 'Z');
  const line = { commit: git(root, 'rev-parse', 'HEAD'), tree: git(root, 'rev-parse', 'HEAD^{tree}'), kind: fields.kind ?? 'ci', started: at, ended: at, finished: fields.finished ?? true, state: 'failed', gates: [] };
  writeFileSync(file, `${held}${JSON.stringify(line)}\n`);
}


export interface Scratch {
  root: string;
  /** `cwd`: where the tool runs, the repository's main checkout unless named (the gate tree's run names it). */
  run(args: string[], extra?: Record<string, string>, cwd?: string): Promise<{ status: number | null; out: string }>;
  ran(): string[];
  cleanup(): void;
}


export function scratch(name: string, gates: { name: string; run: string }[], files: Record<string, string> = {}): Scratch {
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
    run(args, extra = {}, cwd = root) {
      const env: Record<string, string | undefined> = {};
      for (const [key, value] of Object.entries(process.env)) {
        if (key.toUpperCase() !== 'PATH' && !RUNNER_OWN.test(key) && !LOCATING.test(key)) env[key] = value;
      }
      env['PATH'] = `${bin}${delimiter}${process.env['PATH'] ?? process.env['Path'] ?? ''}`;
      // A merge blocks on the checks alone by default, so the tests that watch gates run read the merge with --gated;
      // `BY_DEFAULT` in `extra` is the default itself, and the verbs that name their own gates take no flag.
      const named = args.some((arg) => ['--full', '--rerun', '--stale', '--passed', '--prune', '--gated', '--ci'].includes(arg));
      const given = named || extra['BY_DEFAULT'] !== undefined ? args : [...args, '--gated'];
      // A scratch's heavy gates never read the machine's memory, so no test here waits on what the machine holds.
      const child = spawn(process.execPath, [TOOL, ...given], { cwd, env: { ...env, ...GIT_ENV, MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_CI_NIGHT_HOUR: 'off', ...extra }, windowsHide: true });
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


export function branch(root: string, name: string, files: Record<string, string>, { trailer = true, from = 'main' } = {}): void {
  git(root, 'checkout', '--quiet', '-b', name, from);
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  git(root, 'add', '-A');
  git(root, 'commit', '--quiet', '-m', `work on ${name}${trailer ? TRAILER : ''}`);
  git(root, 'checkout', '--quiet', 'main');
}


export const merging = (root: string) => spawnSync('git', ['rev-parse', '-q', '--verify', 'MERGE_HEAD'], { cwd: root, windowsHide: true }).status === 0;

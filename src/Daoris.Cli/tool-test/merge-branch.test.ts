import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { type ChildProcess, spawn, spawnSync } from 'node:child_process';
import { EventEmitter } from 'node:events';
import os from 'node:os';
import { chmodSync, closeSync, existsSync, lstatSync, mkdirSync, openSync, readdirSync, readFileSync, realpathSync, rmSync, symlinkSync, utimesSync, writeFileSync, writeSync } from 'node:fs';
import { delimiter, dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from '../test/_fixture.ts';

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

// The census of test units (CISCOPE1c) reads this file's strings as the lane table's data, not as reads: it names nearly every
// path the table places. What it does read is declared.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/backlog-merges.json — LANDBACKLOG1's twin reads the table row for row

interface Gate { name: string; run: string; kind: string; cwd?: string; clock?: boolean; memoryGb?: number }
interface Lane { id: string; title: string; summary: string; steward: boolean; paths: string[]; gates?: string[] }
interface Classified { lanes: { id: string; title: string; files: string[] }[]; steward: string[]; shared: string[]; laneless: string[]; outside: string[] }
interface Options {
  branches: string[]; keepGoing: boolean; commitCheck: boolean; resume: boolean; dropBatch: boolean; plan: boolean; prune: boolean; autoPrune: boolean;
  full: boolean; gated: boolean; rerun: string[]; passed: boolean; stale: boolean; oneAtATime: boolean;
  /** GATEWT1: the full set in a worktree of its own, at this commit (main's tip when null). */
  beside: boolean; besideCommit: string | null;
  /** CI1: the CI runner on this commit (main's tip when null), its watch, and the stop asked of the watch. */
  ci: boolean; ciCommit: string | null; watch: boolean; stop: boolean;
  /** CIFAST1: the stage's path, which runs the rehearsals a plain run owes. */
  ciStage: boolean;
}
/** GATEWT1: what the prune knows of the gate tree: git lists it, the commit it holds, its changes, its run's lock and its stage. */
interface GateTreeFacts {
  listed: boolean; head: string | null; changes: string[]; error?: string;
  lock: { pid: number; commit: string; at: string; running: boolean } | null;
  staged: { commit: string; at: string } | null;
  folder?: Folder;
  /** The processes inside it, read when no running lock is held (a full set started by hand holds none). */
  processes?: Proc[]; processError?: string;
}
/**
 * GATELANES1: the two lanes, the light one's run order, and what each Process-lane gate starts after. GATELANES2: how
 * many light gates run at once, and the components each builds, of which no two gates hold one at once. GATEBUILD1: the
 * fast half whose build each Process half reuses.
 */
interface Schedule {
  light: Gate[]; process: Gate[]; waits: Record<string, { after: string[]; component: string | null }>;
  width: number; components: Record<string, string[]>; reuses: Record<string, string>;
  /** REHLANE1: the Process-lane gates each may run beside, the git half and the rehearsals with their own build output. */
  beside: Record<string, string[]>;
}
/** GATEBUILD1: the fast half that passed in this run, and the tree it ran on. */
interface Built { gate: string; tree: string | null }
/**
 * What a lane runner hands a gate: when the light lane has ended, for a Process gate's re-run alone; and to a Process
 * half whose fast half passed in this run, that pass (GATEBUILD1).
 */
interface Beside { settled?: () => Promise<void>; built?: Built; memory?: MemoryWait | null; again?: GateResult | null }
interface Worktree { path: string; branch: string; locked: boolean; lockReason: string; prunable: boolean; bare: boolean }
interface Candidate { branch: string; worktree: Worktree | null; held: string | null }
/** CLEANUP2: what a prune knows of a harness-locked worktree beyond its changes: its branch at main's tip, the processes inside it. */
interface Facts {
  gone?: boolean; error?: string; tracked: string[]; untracked: string[]; local: string | null;
  atTip?: boolean; processes?: Proc[]; processError?: string;
  /** CLEANUP2b: minutes since the newest write read in the worktree. */
  idleMinutes?: number;
}
interface Proc { pid: number; exe: string; command: string }
/** CLEANUP2: a folder under `.claude/worktrees/` that git does not list. */
interface Folder { path: string; link: boolean; git: boolean; processes?: Proc[]; processError?: string }
interface Slow { name: string; ms: number; tests: number }
interface GateResult {
  gate: Gate; code: number; log: string; verdict: string; note: string; ms: number; slowest?: Slow[]; trx?: string;
  /** MEMGATE1: 'memory' when the gate died of memory; `again` while its run again alone is still to come. */
  cause?: string; again?: boolean; flakes?: string[];
}
/** MEMGATE1: what a reading of the machine's memory answers, in bytes; null where it cannot be read, and why. */
interface Reading { physical: number | null; commit: number | null; total?: number | null; why?: string }
/** MEMGATE1: what a run's memory reading is made of, each a stand-in in a test. */
interface MemoryOptions { read?: () => Reading; sleep?: (ms: number) => Promise<void>; now?: () => number; pollMs?: number; boundMs?: number }
/** MEMGATE1: a run's wait for a gate's need of memory, before it starts or runs again. */
type MemoryWait = (gate: Gate, options?: { again?: boolean; halted?: () => boolean }) => Promise<void>;
/** CIPRIO1c: the step trees a run has running, by each step's own pid; `ended` says why once the run ended them. */
interface Trees { ended: string | null; hold: (pid: number) => void; release: (pid: number) => boolean; end: (why: string) => void }
type Step = (command: string, cwd: string, fd: number, options?: { raise?: ((pid: number) => void) | null; trees?: Trees | null }) => Promise<number>;
interface Rule { paths?: string[]; lane?: string; gates: string[] | '*'; why: string }
interface Selected { gate: Gate; run: boolean; reached: boolean; why: string }
interface Verdict {
  gate: string; verdict: string; tree: string | null; at: string; commit?: string | null; branch?: string | null;
  /** PROCREACH1: a Process half's classes: how many ran, how many it kept by reach, and the oldest verdict it kept (its own start when none). */
  classes?: { ran: number | null; kept: number; sweep: string };
}
/** PROCREACH1: one class's verdict, kept with the tree it ran on. */
interface ClassVerdict { verdict: string; tree: string | null; at: string; commit?: string | null }
/** PROCREACH1: what a CI run does with a Process half it owes: the whole half and why, or the classes it runs and those it keeps. */
interface ClassPlan {
  whole: string | null; run: { name: string; why: string }[]; kept: { name: string; verdict: ClassVerdict; standing: string }[]; sweep: string | null;
}
type Reached = { whole: { path: string; why: string }[]; classes: string[]; by: Map<string, string> };
/** GATE6: how a verdict given on one tree stands for another, for its gate, and the paths that say so. */
interface Standing { standing: 'exact' | 'records' | 'unreached' | 'stale'; paths: string[]; records: string[] }
/** What a stage judges by: this tree, the paths another differs by (null when git cannot say), which are records, which reach a gate. */
interface Judging { tree: string; drift: (from: string) => string[] | null; isRecord: (path: string) => boolean; reaching: (gate: string, paths: string[]) => string[] }
/** One gate as the stage judges it: the verdict that counts and how it stands, or a newer one that no longer does. */
interface Judged { name: string; verdict: Verdict | null; exact: boolean; standing: Standing | null; stale: (Standing & { verdict: Verdict }) | null }

/** REHLANE2b: the build servers of its own the deployment rehearsal builds on, the twin of a run beside's. */
const rehearsalKit = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  '../../../tools/rehearsal-kit.mjs') as { OWN_BUILD_SERVERS: Readonly<Record<string, string>> };

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  '../../../tools/merge-branch.mjs') as {
  parseArgs: (argv: string[]) => Options;
  runGate: (root: string, gate: Gate, dir: string, options?: { step?: Step; noBuild?: boolean; trees?: Trees | null } & Beside) => Promise<GateResult>;
  runGates: (root: string, gates: Gate[], dir: string, options?: {
    keepGoing?: boolean;
    oneAtATime?: boolean;
    width?: number;
    scripts?: Record<string, string>;
    run?: (root: string, gate: Gate, dir: string, beside?: Beside) => Promise<GateResult>;
    print?: (line: string) => void;
    halt?: () => boolean;
    memory?: MemoryOptions | null;
    /** CI1: a Process gate waits only for what builds its component, the heavy light gates beside it as memory allows. */
    byMemory?: boolean;
  }) => Promise<GateResult[]>;
  /** MEMGATE1: a heavy gate waits for memory rather than failing for want of it. */
  memoryNeed: (gate: { name: string; run: string; memoryGb?: number }) => number;
  MEMORY_NEEDS: Readonly<Record<string, number>>;
  MEMORY_POLL_MS: number;
  MEMORY_WAIT_MS: number;
  memoryGate: (options?: MemoryOptions & { say?: (line: string) => void }) => MemoryWait;
  memoryDeath: (log: string, ...codes: number[]) => { said: string } | null;
  readMemory: (options?: {
    platform?: string; run?: (command: string, args: string[], options: object) => { status: number | null; stdout?: string; stderr?: string; error?: Error };
    freemem?: () => number; totalmem?: () => number; read?: (path: string) => string;
  }) => Reading;
  parseWindowsMemory: (text: string) => { physical: number | null; commit: number | null };
  parseMeminfo: (text: string) => { commit: number | null };
  flakeNote: (flakes: GateResult[]) => string;
  memoryLine: (failed: GateResult[]) => string | null;
  isProcessLane: (gate: { name: string; run: string }) => boolean;
  isGitHalf: (gate: { name: string; run: string }) => boolean;
  BESIDE_GIT_HALF: readonly string[];
  runsBesideGitHalf: (gate: { name: string; run: string }) => boolean;
  /** CIFAST1: the rehearsals a plain CI run owes a stage, and the stage's path that runs them. */
  STAGE_REHEARSALS: readonly string[];
  isStageRehearsal: (gate: { name: string; run: string }) => boolean;
  stageCommand: (commit: string) => string;
  dotnetComponent: (run: string) => string | null;
  isHeavyGate: (gate: { name: string; run: string }) => boolean;
  laneSchedule: (gates: Gate[], options?: { scripts?: Record<string, string>; byMemory?: boolean }) => Schedule;
  LIGHT_WIDTH: number;
  gateComponents: (gate: { name: string; run: string }, scripts?: Record<string, string>) => string[];
  testBuild: (run: string) => string | null;
  noBuildCommand: (run: string) => string;
  reuseBuild: (built: Built | undefined, tree: string | null) => boolean;
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
  ignoredVerdict: (probe: { status: number | null; err: string }, path: string) => boolean;
  startRefusal: (facts: { branch: string; merging: boolean; changed: { code: string; path: string }[]; ignored: boolean }) => string | null;
  parseCommits: (text: string) => { sha: string; merge: boolean; subject: string; trailer: string }[];
  commitAttributionProblems: (commits: { sha: string; merge: boolean; subject: string; trailer: string }[]) => string[];
  worktreeFor: (porcelain: string, branch: string) => string | null;
  isProcessGate: (gate: { name: string; run: string }) => boolean;
  parseWorktrees: (porcelain: string) => Worktree[];
  pruneCandidates: (facts: { merged: string[]; worktrees: Worktree[]; current: string; hold?: string[] }) => Candidate[];
  pruneVerdict: (candidate: Candidate, facts: Facts | null) => { remove: boolean; why: string; gone?: boolean; unlock?: boolean };
  harnessLock: (reason: string) => boolean;
  HARNESS_IDLE_MINUTES: number;
  newestWrite: (path: string) => number;
  parseProcesses: (text: string, platform?: string) => Proc[];
  processesInside: (folder: string, processes: Proc[], platform?: string) => Proc[];
  disposableLocal: (path: string, directory?: boolean) => boolean;
  firstFile: (dir: string, skip?: (path: string, directory: boolean) => boolean) => string | null;
  folderVerdict: (folder: Folder) => { remove: boolean; why: string };
  BASELINE: readonly string[];
  readGenerated: (root: string) => { folder: string; run: string }[];
  isGenerated: (path: string, generated: readonly { folder: string }[]) => boolean;
  setApart: (text: string) => { text: string; set: { line: number; label: string }[] };
  setApartHeadings: (text: string, sides?: string[]) => { text: string; set: { line: number; label: string }[] };
  noteName: (label: string) => string;
  backlogPath: (root: string) => string;
  backlogOf: (text: string | null) => string | null;
  mergeByBase: (base: string, ours: string, theirs: string) => { text: string | null; kept: number; dropped: number; both: { line: number }[] };
  REACH: readonly Rule[];
  PATH_CHECKS: readonly { gate: string; paths: string[]; why: string }[];
  selectionLines: (gates: Selected[]) => string[];
  isLongGate: (gate: { name: string; run: string }) => boolean;
  selectGates: (plan: Gate[], changed: string[], options?: { lanes?: Lane[]; full?: boolean; gated?: boolean; reach?: readonly Rule[] }) => { gates: Selected[]; unplaced: string[] };
  pathsReaching: (gate: string, paths: string[], options?: { lanes?: Lane[]; reach?: readonly Rule[] }) => string[];
  rerunPlan: (plan: string[], chosen: string[], results: Record<string, { verdict: string; at: string }>, named: string[], stale?: Record<string, string[]>) => {
    run: string[]; kept: { name: string; verdict: string; at: string }[]; fresh: string[]; stale: { name: string; verdict: string; at: string; paths: string[] }[];
  };
  VERDICTS_FILE: string;
  verdictStanding: (facts: Judging & { from: string | null; gate: string }) => Standing;
  stageJudgement: (facts: Judging & { plan: string[]; verdicts: Verdict[]; now?: number; bound?: number }) => { passed: boolean; missing: string[]; gates: Judged[] };
  stageRemedy: (gates: Judged[]) => { command: string; what: string; runs: string[] } | null;
  checkoutRerun: (gates: Judged[], named: string[]) => {
    run: string[]; kept: { name: string; verdict: string; at: string }[]; stale: { name: string; verdict: string; at: string; paths: string[] }[]; none: string[];
  };
  FULL_COMMAND: string;
  STALE_COMMAND: string;
  recordMatcher: (map: { lanes?: Lane[]; union?: string[] }) => (path: string) => boolean;
  trxCommand: (run: string, file: string) => string;
  slowestClasses: (trx: string, count?: number) => Slow[];
  GATE_TREE: string;
  GATE_TREE_LOCK: string;
  GATE_TREE_RECORD: string;
  DEPENDENCY_FOLDERS: readonly string[];
  verdictsHome: (root: string) => string;
  readVerdicts: (root: string) => { schema: number; verdicts: Verdict[] };
  addVerdicts: (root: string, entries: Verdict[], options?: { keep?: number }) => void;
  readGateLock: (home: string, alive?: (pid: number) => boolean) => { pid: number; commit: string; at: string; running: boolean; holder: string } | null;
  takeGateLock: (home: string, options: {
    commit: string; holder?: string; alive?: (pid: number) => boolean; now?: () => string; end?: (pid: number, since: number) => number[]; endStrays?: (folder: string, since: number) => number[];
  }) => { stale: { pid: number; commit: string; at: string; ended: number[] } | null };
  releaseGateLock: (home: string) => void;
  gateTreeVerdict: (facts: GateTreeFacts, options?: { atMerge?: boolean }) => { remove: boolean; why: string; said?: string };
  staleDependencies: (tree: string) => string[];
  stampDependencies: (tree: string, folder: string) => void;
  privateGateFiles: (home: string) => string[];
  copyPrivateFiles: (home: string, tree: string, files: string[]) => string[];
  keepGateLogs: (tree: string, home: string, sha: string) => boolean;
  rescueGateLogs: (tree: string, home: string) => string[];
  /** CIPRIO1: a full set beside the merges is a guest: below normal priority, its own build servers, stopped when main moves on. */
  lowerPriority: (setPriority?: (pid: number, priority: number) => void) => string | null;
  BESIDE_ENVIRONMENT: Readonly<Record<string, string>>;
  startBesideRun: (options?: { setPriority?: (pid: number, priority: number) => void; env?: Record<string, string | undefined>; say?: (line: string) => void }) => void;
  /** FAMILYFAIL1b: the raise a run beside hands a rehearsal's step, null in a run that did not lower itself. */
  rehearsalRaise: () => ((pid: number) => void) | null;
  watchTip: (commit: string | null, resolveTip?: () => string | null, options?: { onMove?: (moved: { from: string; to: string }) => void }) => {
    check: () => boolean; poll: (ms: number) => () => void; superseded: { from: string; to: string } | null;
  };
  supersededLines: (superseded: { from: string; to: string }, ran: string[], left: string[], ended?: string[]) => string[];
  besideOutcome: (code: number, superseded: { from: string; to: string } | null) => { passed: boolean; superseded: string | null };
  runStep: Step;
  /** CIPRIO1c: a superseded or stopped run ends the gates it is running, each with its whole tree. */
  STEP_ENDED: number;
  TIP_POLL_MS: number;
  stepTrees: (options?: { end?: (pid: number) => void }) => Trees;
  endTree: (pid: number, options?: {
    platform?: string; run?: (command: string, args: string[], options: object) => { stdout?: string | null }; kill?: (pid: number, signal: string) => void;
  }) => void;
  descendants: (listing: string, pid: number) => number[];
  stoppedLines: (ran: string[], left: string[], ended?: string[]) => string[];
  stopOnInterrupt: (trees: Trees, options?: { on?: { once: (event: string, listener: () => void) => unknown; off: (event: string, listener: () => void) => unknown }; say?: (line: string) => void }) => () => void;
  /** CI1: the local CI runner, its record, its watch and the stop it is asked by. */
  CI_RECORD: string;
  CI_WATCH: string;
  CI_STOP: string;
  CI_IDLE: Readonly<{ busy: number; freeGb: number; width: number; sampleMs: number }>;
  ciPlan: (facts: Omit<Judging, 'isRecord'> & { plan: string[]; verdicts: Verdict[]; owe?: string[]; now?: number; bound?: number }) => { run: string[]; kept: { name: string; verdict: Verdict; standing: Standing }[]; owed: string[]; gates: Judged[] };
  cpuBusy: (before: CpuTimes[], after: CpuTimes[]) => number | null;
  measureLoad: (options?: { cpus?: () => CpuTimes[]; sleep?: (ms: number) => Promise<void>; read?: () => Reading; ms?: number }) => Promise<Load>;
  ciWidth: (load: Load) => { width: number; idle: boolean; why: string };
  ciStatus: (facts: {
    commit: string; tree: string; plan: Gate[]; chosen: { run: string[]; kept: { name: string; verdict: Verdict; standing: Standing }[]; owed?: string[] };
    since: { commit: string | null; at: string | null; paths: number | null }; light: { width: number; idle: boolean; why: string };
    pid: number; at: string; logOf: (gate: string, commit: string | null) => string | null; stage?: boolean;
  }) => CiStatus;
  watchCi: (seams: {
    tip: () => string | null; gated: (commit: string) => boolean; holder: () => string | null; run: () => Promise<number>;
    stopAsked: () => boolean; sleep: (ms: number) => Promise<void>; say: (line: string) => void; pollMs?: number; tickMs?: number; now?: () => number;
  }) => Promise<number>;
  /** PROCREACH1: a Process half run by the classes a change reaches, each class's verdict kept with its tree, a whole half at least daily. */
  CLASS_VERDICTS_FILE: string;
  SWEEP_BOUND_MS: number;
  classResults: (trx: string) => Map<string, { tests: number; failed: string[] }>;
  classVerdictsOf: (trx: string, result: { verdict: string; flakes?: string[] }) => { name: string; verdict: string }[];
  readClassVerdicts: (home: string) => { schema: number; gates: Record<string, Record<string, ClassVerdict[]>> };
  addClassVerdicts: (home: string, gate: string, entries: ({ name: string } & ClassVerdict)[], options?: { keep?: number }) => void;
  classPlan: (facts: {
    classes: string[]; verdicts: Record<string, ClassVerdict[]>; tree: string; drift: (from: string) => string[] | null;
    reached: (paths: string[]) => Reached; now?: number; bound?: number;
  }) => ClassPlan;
};
/** CI1: what `os.cpus()` answers of a processor, the times it spent by kind since the machine started. */
interface CpuTimes { times: { user: number; nice: number; sys: number; idle: number; irq: number } }
/** CI1: the machine's load as a CI run measures it before its gates start: the share of its processors busy, its free memory. */
interface Load { busy: number | null; physical: number | null; commit: number | null }
/** CI1: one gate in a CI run's status file. */
interface CiGate {
  name: string; kind: string; lane: 'light' | 'process'; state: 'kept' | 'waiting' | 'running' | 'done' | 'not run' | 'owed';
  verdict: string | null; at: string | null; ms: number | null; log: string | null; note: string | null;
  kept: { commit: string | null; tree: string | null; standing: string } | null;
}
/** CI1: a CI run's status file, `local/scratch/ci-<sha7>/status.json`, which the parent reads and a later page may. */
interface CiStatus {
  schema: number; commit: string; tree: string; state: string; pid: number; started: string; updated: string; ended: string | null;
  since: { commit: string | null; at: string | null; paths: number | null }; light: { width: number; idle: boolean; why: string };
  stage: boolean; owed: string[]; gates: CiGate[]; error?: string;
}

// The stage's own question (GATE6b's proof): the publish asks the merge tool's `--passed` before it builds.
const publish = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam as the merge tool above
  '../../../tools/desktop-publish.mjs') as { ungatedRefusal: (to: string, repoRoot: string) => { refusal: string | null; note: string | null } };

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
    full: false, gated: false, rerun: [], passed: false, stale: false, oneAtATime: false, beside: false, besideCommit: null,
    ci: false, ciCommit: null, watch: false, stop: false, ciStage: false,
  });
  assert.equal(tool.parseArgs(['a', '--gated']).gated, true);
  assert.equal(tool.parseArgs(['--continue', '--gated']).gated, true);
  assert.throws(() => tool.parseArgs(['--rerun', 'web', '--gated']), /--rerun takes gate names/);
  // GATELANES1: every verb that runs gates runs them one at a time when told to, as on a loaded machine.
  for (const argv of [['a', '--one-at-a-time'], ['--continue', '--one-at-a-time'], ['--rerun', 'web', '--one-at-a-time'],
    ['--stale', '--one-at-a-time'], ['--full', '--one-at-a-time'], ['--full', '--keep-going', '--one-at-a-time']]) {
    assert.equal(tool.parseArgs(argv).oneAtATime, true, argv.join(' '));
  }
  // GATE6b: each stale gate run again on the checkout as it stands.
  assert.equal(tool.parseArgs(['--stale']).stale, true);
  assert.equal(tool.parseArgs(['--stale', '--keep-going']).keepGoing, true);
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
    [['--rerun', 'web', '--full'], /--rerun takes gate names, --keep-going and --one-at-a-time/],
    [['--rerun', 'web', 'web'], /'web' is named twice/],
    [['--passed', 'a'], /--passed takes no branch/],
    [['--passed', '--full'], /one of/],
    [['--prune', '--full'], /--full is for a merge or the checkout/],
    [['--stale', 'a'], /--stale takes no branch/],
    [['--stale', '--rerun', 'web'], /one of/],
    [['--stale', '--passed'], /one of/],
    [['--stale', '--full'], /--stale takes only --keep-going and --one-at-a-time/],
    [['--stale', '--no-prune'], /--stale takes only --keep-going and --one-at-a-time/],
    [['--prune', '--one-at-a-time'], /--one-at-a-time is for a run of gates; --prune runs no gate/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

// ---------------------------------------------------------------------------------------------------
// GATEWT1: the full set in its own worktree, beside the merges

test('--full --beside gates a commit in a worktree of its own, main\'s tip when it names none, and takes only the run\'s flags', () => {
  const plain = tool.parseArgs(['--full', '--beside']);
  assert.deepEqual([plain.full, plain.beside, plain.besideCommit, plain.branches], [true, true, null, []]);
  const named = tool.parseArgs(['--full', '--beside', 'abc1234', '--keep-going']);
  assert.deepEqual([named.beside, named.besideCommit, named.keepGoing, named.branches], [true, 'abc1234', true, []]);
  assert.equal(tool.parseArgs(['--beside', '--full', '--one-at-a-time']).oneAtATime, true);
  const cases: [string[], RegExp][] = [
    [['--beside'], /--beside runs the full set: --full --beside \[<commit>\]/],
    [['a', '--full', '--beside'], /--beside takes no branch/],
    [['--full', '--beside', 'abc1234', 'b'], /--beside takes no branch/],
    [['--full', '--beside', '--no-commit-check'], /--full --beside takes only a commit, --keep-going and --one-at-a-time/],
    [['--full', '--beside', '--beside'], /--beside is given once/],
    [['--prune', '--beside'], /one of|--beside/],
    [['--passed', '--beside'], /one of|--beside/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

// ---------------------------------------------------------------------------------------------------
// CI1: the local CI runner, main's tip gated after each merge so a release waits only for the last delta

test("--ci gates a commit in the gate tree, main's tip unless named; --watch follows main's tip, --stop ends the watch", () => {
  const plain = tool.parseArgs(['--ci']);
  assert.deepEqual([plain.ci, plain.ciCommit, plain.watch, plain.stop, plain.branches, plain.full], [true, null, false, false, [], false]);
  const named = tool.parseArgs(['--ci', 'abc1234', '--one-at-a-time']);
  assert.deepEqual([named.ci, named.ciCommit, named.oneAtATime], [true, 'abc1234', true]);
  assert.equal(tool.parseArgs(['--ci', '--keep-going']).ci, true, 'a CI run keeps going anyway: the flag is taken and changes nothing');
  const watch = tool.parseArgs(['--ci', '--watch']);
  assert.deepEqual([watch.ci, watch.watch, watch.ciCommit], [true, true, null]);
  assert.equal(tool.parseArgs(['--watch', '--ci']).watch, true);
  assert.deepEqual([tool.parseArgs(['--ci', '--stop']).stop], [true]);
  assert.equal(plain.ciStage, false, 'a plain run is no stage\'s');
  // CIFAST1: the stage's path, its commit after --stage or after --ci.
  for (const argv of [['--ci', '--stage', 'abc1234'], ['--ci', 'abc1234', '--stage'], ['--stage', '--ci', 'abc1234']]) {
    const stage = tool.parseArgs(argv);
    assert.deepEqual([stage.ci, stage.ciStage, stage.ciCommit, stage.branches], [true, true, 'abc1234', []], argv.join(' '));
  }
  assert.deepEqual([tool.parseArgs(['--ci', '--stage']).ciStage, tool.parseArgs(['--ci', '--stage']).ciCommit], [true, null]);
  const cases: [string[], RegExp][] = [
    [['--watch'], /--watch is the CI's: --ci --watch/],
    [['--stop'], /--stop is the CI watch's: --ci --stop/],
    [['--ci', 'abc1234', 'b'], /--ci takes no branch/],
    [['a', '--ci'], /--ci takes no branch/],
    [['--ci', '--watch', 'abc1234'], /--ci --watch gates main's tip as it moves: it takes no commit/],
    [['--ci', '--stop', 'abc1234'], /--ci --stop takes nothing else/],
    [['--ci', '--stop', '--watch'], /--ci --stop takes nothing else/],
    [['--ci', '--full'], /--ci takes only a commit, --stage, --watch, --stop, --keep-going and --one-at-a-time/],
    [['--stage'], /--stage is the CI's: --ci --stage \[<commit>\]/],
    [['--ci', '--watch', '--stage'], /--ci --watch owes the stage's rehearsals/],
    [['--ci', '--stop', '--stage'], /--ci --stop takes nothing else/],
    [['--ci', '--stage', '--stage'], /--stage is given once/],
    [['--ci', '--stage', 'abc1234', 'b'], /--ci takes no branch/],
    [['--ci', '--beside'], /--ci takes only/],
    [['--ci', '--no-prune'], /--ci takes only/],
    [['--ci', '--ci'], /--ci is given once/],
    [['--passed', '--ci'], /one of|--ci takes only/],
    [['--prune', '--ci'], /one of|--ci takes only/],
  ];
  for (const [argv, message] of cases) {
    const error = refusal(() => tool.parseArgs(argv));
    assert.equal(error.exitCode, 2, argv.join(' '));
    assert.match(error.message, message, argv.join(' '));
  }
});

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
  // The stage forgives the records (GATE6); the CI does not: a backlog edit since reaches the service suite, which runs.
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
  assert.deepEqual(Object.keys(status), ['schema', '_why', 'commit', 'tree', 'state', 'pid', 'started', 'updated', 'ended', 'since', 'light', 'stage', 'owed', 'gates']);
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

  // CIFAST1: a gate a plain run owes a stage is `owed`, neither failed nor passed, naming the stage's path.
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
  const first = tool.takeGateLock(fx.root, { commit: sha, alive: () => true, now: () => '2026-10-18T10:00:00Z' });
  assert.equal(first.stale, null);
  const held = tool.readGateLock(fx.root, () => true);
  assert.deepEqual(held, { pid: process.pid, commit: sha, at: '2026-10-18T10:00:00Z', running: true, holder: 'full' });

  const error = refusal(() => tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => true }));
  assert.equal(error.exitCode, 2);
  assert.match(error.message, new RegExp(`a full set beside the merges is running already: on aaaaaaa since 2026-10-18 10:00 UTC \\(pid ${process.pid}, local/gate-tree\\); one runs at a time`));
  assert.equal(tool.readGateLock(fx.root, () => true)!.commit, sha, 'a refusal leaves the lock as it was');

  // Its process ended without letting go (a window closed on it): the lock is taken over, and the caller says so.
  const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => false, now: () => '2026-10-18T11:00:00Z', end: () => [] });
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
  tool.takeGateLock(fx.root, { commit: 'a'.repeat(40), alive: () => true, now: () => '2026-10-18T10:00:00Z', end, endStrays });
  assert.deepEqual(calls, [], 'a first take has no dead run to end');
  refusal(() => tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => true, end, endStrays }));
  assert.deepEqual(calls, [], 'a refusal ends nothing: the run holding the lock runs');
  assert.deepEqual(strays, []);
  const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive: () => false, end, endStrays });
  assert.deepEqual(calls, [[process.pid, Date.parse('2026-10-18T10:00:00Z')]]);
  // CIPRIO1e: the shells' orphans, by the gate tree's folder, beside the dead pid's children, each pid once
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

test('the private files a gate reads come from the declaration, only under local/; a copy is refreshed and a missing source says so (GATEWT1b)', () => {
  const fx = makeFixture('merge-branch-private-files');
  const home = join(fx.root, 'home');
  const tree = join(fx.root, 'tree');
  mkdirSync(join(home, 'local'), { recursive: true });
  mkdirSync(tree, { recursive: true });
  const declare = (sensitive?: unknown) => writeFileSync(join(home, 'daoris.gates.json'), JSON.stringify({ gates: [{ name: 'a', run: 'x' }], ...(sensitive ? { sensitive } : {}) }));

  declare();
  assert.deepEqual(tool.privateGateFiles(home), ['local/sensitive-patterns.txt'], 'the default');
  declare({ patternsFile: 'local/own.txt' });
  assert.deepEqual(tool.privateGateFiles(home), ['local/own.txt']);
  declare({ patternsFile: 'private/patterns.txt' });
  assert.deepEqual(tool.privateGateFiles(home), [], 'not under local/: nothing copied');
  declare({ patternsFile: 'local/../secret.txt' });
  assert.deepEqual(tool.privateGateFiles(home), [], 'no way out of local/');

  const files = ['local/sensitive-patterns.txt'];
  assert.match(tool.copyPrivateFiles(home, tree, files).join('\n'), /local\/sensitive-patterns\.txt.*missing/);
  assert.equal(existsSync(join(tree, files[0]!)), false);
  writeFileSync(join(home, files[0]!), 'one\n');
  assert.match(tool.copyPrivateFiles(home, tree, files).join('\n'), /copied/);
  assert.equal(readFileSync(join(tree, files[0]!), 'utf8'), 'one\n');
  writeFileSync(join(home, files[0]!), 'two\n');
  tool.copyPrivateFiles(home, tree, files);
  assert.equal(readFileSync(join(tree, files[0]!), 'utf8'), 'two\n', 'refreshed on every run');
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

  // A run that was killed left its folder in the tree and none in the main checkout.
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
  // A merge's prune keeps a gated tree its stage has not read: that is what the run was for.
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

test('a check-ignore probe: status 0 is ignored, 1 is not, anything else is a tool error quoting git (GATEIGNORE1)', () => {
  assert.equal(tool.ignoredVerdict({ status: 0, err: '' }, 'local/scratch/x.log'), true);
  assert.equal(tool.ignoredVerdict({ status: 1, err: '' }, 'local/scratch/x.log'), false);
  assert.throws(() => tool.ignoredVerdict({ status: 128, err: 'fatal: unable to read index' }, 'local/scratch/x.log'), /check-ignore.*local\/scratch\/x\.log.*failed.*status 128.*fatal: unable to read index/s);
  assert.throws(() => tool.ignoredVerdict({ status: null, err: '' }, 'p'), /failed/);
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

test('MAKERTRAIL1 accepts each allowed maker, legacy harness trailers, and names expected makers on refusal', () => {
  const commit = (trailer: string, merge = false) => ({ sha: 'abc1234', subject: 'work', merge, trailer });
  for (const trailer of [
    'Claude Opus 4.6 <noreply@anthropic.com>',
    'Claude (Anthropic, sonnet) <fixture@example.test>',
    'Codex (OpenAI, gpt-fixture) <noreply@openai.com>',
    'OpenAI gpt-fixture <fixture@example.test>',
    'DeepSeek (fixture-model) <noreply@deepseek.com>',
    'Human <person@example.test>\nCodex gpt-fixture <fixture@example.test>',
  ]) assert.deepEqual(tool.commitAttributionProblems([commit(trailer)]), [], trailer);
  assert.deepEqual(tool.commitAttributionProblems([commit('', true)]), []);
  for (const trailer of ['', 'Fixture <fixture@example.test>', 'OpenAI', 'Someone <OpenAI@example.test>']) {
    const problems = tool.commitAttributionProblems([commit(trailer)]);
    assert.equal(problems.length, 1, trailer);
    assert.match(problems[0]!, /abc1234 work.*Co-Authored-By.*expected.*Anthropic.*OpenAI.*DeepSeek/);
  }
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

test('a prune never takes a Daoris session\'s tree or branch: a daoris/ or quest/ branch, with its worktree', () => {
  // DOGFOOD1: the install drives this repository, its sessions' trees under its own home and their branches at main's tip
  // until their first commit, which reads as merged; a merge's prune took one mid-session (HOMEFIX2's batch, 2026-10-20).
  const worktrees = [
    tree('D:/work/repo', 'main'),
    tree('D:/work/repo/.claude/worktrees/agent-a', 'worktree-agent-a'),
    tree('X:/daoris/data/trees/games/repo/s-1', 'daoris/s-1'),
    tree('D:/work/repo-elsewhere', 'elsewhere'),
  ];
  const candidates = tool.pruneCandidates({
    merged: ['daoris/s-1', 'daoris/s-2', 'elsewhere', 'quest/fix-7b175b', 'worktree-agent-a'], worktrees, current: 'main',
  });
  const held = Object.fromEntries(candidates.map((c) => [c.branch, c.held]));
  assert.match(held['daoris/s-1']!, /Daoris/);
  assert.match(held['daoris/s-2']!, /Daoris/, 'a session branch with no tree is still Daoris\'s');
  assert.match(held['quest/fix-7b175b']!, /Daoris/);
  assert.equal(held['elsewhere'], null, 'a tree beside the checkout is no sign: the harness may put its trees there');
  assert.equal(held['worktree-agent-a'], null, 'an agent harness tree is still pruned as before');
});

test('a merged branch goes with its worktree only when that is unlocked, has no tracked change and holds nothing under local/', () => {
  const clean: Facts = { tracked: [], untracked: [], local: null };
  const at = (more: Partial<Worktree> = {}): Candidate => ({ branch: 'b', worktree: tree('/r/w/b', 'b', more), held: null });

  assert.deepEqual(tool.pruneVerdict({ branch: 'b', worktree: null, held: null }, null), { remove: true, why: '' });
  assert.deepEqual(tool.pruneVerdict(at(), clean), { remove: true, why: '' });
  assert.deepEqual(tool.pruneVerdict(at({ prunable: true }), { ...clean, gone: true }), { remove: true, why: '', gone: true });
  assert.deepEqual(tool.pruneVerdict({ branch: 'b', worktree: null, held: 'named in this batch' }, null), { remove: false, why: 'named in this batch' });

  // A branch at main's tip is merged; a worktree locked for any reason but the agent harness's is in use however it
  // looks, and is never looked into (CLEANUP2 lifts the harness's own lock, below).
  const locked = tool.pruneVerdict(at({ locked: true, lockReason: 'the owner keeps this' }), null);
  assert.equal(locked.remove, false);
  assert.match(locked.why, /locked, so in use \(the owner keeps this\)/);
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

// CLEANUP2: the agent harness keeps its lock for its session's whole life, not only while the agent runs, so nine
// finished agents' merged worktrees stood locked (2026-10-18). Its lock is lifted when nothing is left inside.
test('the agent harness\'s lock is its own reason: claude agent, an agent id and a pid', () => {
  assert.equal(tool.harnessLock('claude agent agent-a (pid 57256)'), true);
  assert.equal(tool.harnessLock('claude agent agent-aa965da03c60bca87 (pid 1)'), true);
  for (const other of ['', 'the owner keeps this', 'claude agent agent-a', 'claude agent agent-a (pid 1) for now', 'claude agent  (pid 1)']) {
    assert.equal(tool.harnessLock(other), false, other);
  }
});

// CLEANUP2b: the newest write is read from the tree's top level, its folders' children, and the git folder's HEAD, index and
// log, never a full walk; a source file edited in place deeper down is not seen, which the two hours cover.
test('the newest write of a worktree is read from its top level, its folders\' children and its git folder', () => {
  const repo = scratch('newest-write', [{ name: 'first', run: 'node gate.mjs first' }], { 'src/a.ts': 'a\n' });
  try {
    const path = join(dirname(repo.root), 'tree');
    git(repo.root, 'worktree', 'add', '--quiet', '-b', 'w', path, 'main');
    age(path, 300);
    const idle = (Date.now() - tool.newestWrite(path)) / 60_000;
    assert.ok(idle >= 299 && idle < 302, `all idle: ${idle}`);
    const touched = (target: string) => {
      age(path, 300);
      writeFileSync(target, 'x\n');
      return (Date.now() - tool.newestWrite(path)) / 60_000;
    };
    assert.ok(touched(join(path, 'top.txt')) < 2, 'a file at the top');
    assert.ok(touched(join(path, 'src', 'b.ts')) < 2, 'a file in a folder');
    const gitdir = /^gitdir: (.+)$/m.exec(readFileSync(join(path, '.git'), 'utf8'))![1]!.trim();
    assert.ok(touched(join(gitdir, 'HEAD')) < 2, "the worktree's HEAD");
    assert.ok(touched(join(gitdir, 'index')) < 2, "the worktree's index");
    assert.equal(tool.newestWrite(join(path, 'missing')), 0, 'a folder that is not there has no write');
  } finally {
    repo.cleanup();
  }
});

test('a harness-locked merged worktree goes, unlocked, only when it is clean, holds nothing under local/ and no process runs inside', () => {
  const clean: Facts = { tracked: [], untracked: [], local: null, atTip: false, processes: [], idleMinutes: 300 };
  const reason = 'claude agent agent-b (pid 1)';
  const at = (lockReason = reason): Candidate => ({ branch: 'b', worktree: tree('/r/w/b', 'b', { locked: true, lockReason }), held: null });

  assert.deepEqual(tool.pruneVerdict(at(), clean), { remove: true, why: '', unlock: true });
  assert.deepEqual(tool.pruneVerdict(at(), { ...clean, gone: true }), { remove: true, why: '', gone: true, unlock: true });
  assert.equal(tool.pruneVerdict(at(), null).remove, false, 'a harness-locked worktree nobody looked into is kept');

  const inside = tool.pruneVerdict(at(), { ...clean, processes: [{ pid: 9, exe: 'C:/node.exe', command: 'node land.mjs' }] });
  assert.equal(inside.remove, false);
  assert.match(inside.why, /locked by the agent harness \(claude agent agent-b \(pid 1\)\)/);
  assert.match(inside.why, /a running process is inside it \(pid 9: node land\.mjs\)/);
  const unread = tool.pruneVerdict(at(), { tracked: [], untracked: [], local: null, atTip: false, idleMinutes: 300, processError: 'powershell could not start' });
  assert.equal(unread.remove, false);
  assert.match(unread.why, /whether a process runs inside it could not be read \(powershell could not start\)/);
  // A just-dispatched agent's branch sits at main's tip before its first commit, and counts as merged.
  const fresh = tool.pruneVerdict(at(), { ...clean, atTip: true });
  assert.equal(fresh.remove, false);
  assert.match(fresh.why, /its branch is at main's tip, as a just-dispatched agent's is/);
  // Every reason is said, not only the first.
  const all = tool.pruneVerdict(at(), {
    ...clean, tracked: ['a.ts'], untracked: ['n.txt'], local: 'local/notes.md', processes: [{ pid: 3, exe: '', command: 'code .' }],
  });
  assert.match(all.why, /tracked files \(a\.ts\)[\s\S]*local\/ \(local\/notes\.md\)[\s\S]*untracked files[\s\S]*pid 3: code \./);

  // CLEANUP2b: a worker may be in the tree with no process naming its folder, so a write in the last two hours keeps it.
  const young = tool.pruneVerdict(at(), { ...clean, idleMinutes: 7 });
  assert.equal(young.remove, false);
  assert.equal('unlock' in young, false);
  assert.match(young.why, /locked by the agent harness \(claude agent agent-b \(pid 1\)\), and stays locked: written 7 minutes ago, a worker may be in it/);
  assert.equal(tool.pruneVerdict(at(), { ...clean, idleMinutes: tool.HARNESS_IDLE_MINUTES - 1 }).remove, false);
  assert.equal(tool.pruneVerdict(at(), { ...clean, idleMinutes: tool.HARNESS_IDLE_MINUTES }).remove, true, 'idle for the whole span goes');
  assert.match(tool.pruneVerdict(at(), { ...clean, idleMinutes: 1 }).why, /written 1 minute ago,/);
  const { idleMinutes: _unread, ...unwritten } = clean;
  const unreadWrite = tool.pruneVerdict(at(), unwritten);
  assert.equal(unreadWrite.remove, false);
  assert.match(unreadWrite.why, /when it was last written could not be read/);
  assert.equal(tool.pruneVerdict(at(), { ...unwritten, gone: true }).remove, true, 'a folder that is gone has no one in it');

  // Any other lock is in use however the worktree looks, and is never looked into or unlocked.
  for (const other of ['the owner keeps this', '']) {
    const kept = tool.pruneVerdict(at(other), clean);
    assert.equal(kept.remove, false, other);
    assert.match(kept.why, /its worktree is locked, so in use/);
    assert.match(kept.why, /never looked into or unlocked here/);
    assert.equal('unlock' in kept, false);
  }
  assert.match(tool.pruneVerdict(at('the owner keeps this'), clean).why, /\(the owner keeps this\)/);
});

test('the processes are read from a CIM query on Windows and ps elsewhere, and one is inside a folder its path or command line names', () => {
  const cim = JSON.stringify([
    { ProcessId: 0, ExecutablePath: null, CommandLine: null },
    { ProcessId: 4120, ExecutablePath: 'C:\\Program Files\\nodejs\\node.exe', CommandLine: '"C:\\Program Files\\nodejs\\node.exe" land.mjs' },
    { ProcessId: 77, ExecutablePath: 'C:\\Windows\\System32\\svchost.exe', CommandLine: null },
  ]);
  assert.deepEqual(tool.parseProcesses(cim, 'win32'), [
    { pid: 4120, exe: 'C:\\Program Files\\nodejs\\node.exe', command: '"C:\\Program Files\\nodejs\\node.exe" land.mjs' },
    { pid: 77, exe: 'C:\\Windows\\System32\\svchost.exe', command: 'C:\\Windows\\System32\\svchost.exe' },
  ], 'a process with neither path nor command line names no folder');
  // PowerShell writes one object, not an array, for a single process, and a console set to UTF-8 may write its preamble.
  assert.deepEqual(tool.parseProcesses(`﻿${JSON.stringify({ ProcessId: 5, ExecutablePath: null, CommandLine: 'x' })}\r\n`, 'win32'), [{ pid: 5, exe: '', command: 'x' }]);
  assert.deepEqual(tool.parseProcesses('    1 /sbin/init\n  234 node /work/x/land.mjs --flag\n\n', 'linux'), [
    { pid: 1, exe: '', command: '/sbin/init' }, { pid: 234, exe: '', command: 'node /work/x/land.mjs --flag' },
  ]);

  const folder = ['D:', 'work', 'repo', '.claude', 'worktrees', 'agent-a'].join('\\');
  const proc = (pid: number, command: string, exe = ''): Proc => ({ pid, exe, command });
  const found = tool.processesInside(folder, [
    proc(1, `"C:\\node.exe" ${folder}\\examples\\plugins\\github-pull-request\\land.mjs`),
    proc(2, 'node d:/WORK/repo/.claude/worktrees/agent-a/x.mjs'),
    proc(3, `node ${folder}b\\x.mjs`),
    proc(4, 'tool.exe --serve', `${folder}\\bin\\tool.exe`),
    proc(5, 'bash -c cd /d/work/repo/.claude/worktrees/agent-a && ls'),
    proc(6, `git -C "${folder}" status`),
    proc(7, `git -C ${folder}`),
    proc(8, 'node D:\\work\\repo\\tools\\merge-branch.mjs --prune'),
  ], 'win32');
  assert.deepEqual(found.map((p) => p.pid), [1, 2, 4, 5, 6, 7], 'agent-ab is not inside agent-a, and the checkout around it is not');
  assert.deepEqual(tool.processesInside('/work/x', [proc(1, 'node /work/x/land.mjs'), proc(2, 'node /work/xy/a'), proc(3, 'node /WORK/x/a')], 'linux')
    .map((p) => p.pid), [1], 'elsewhere a path is case-sensitive');
});

test('logs and scratch under local/ are an agent\'s disposable output; anything else there is kept and named', () => {
  for (const path of ['local/scratch/a.md', 'local/scratch/deep/b.json', 'local/gate.log', 'local/runs/x.log']) assert.equal(tool.disposableLocal(path), true, path);
  assert.equal(tool.disposableLocal('local/scratch', true), true, 'the scratch folder is not walked at all');
  for (const path of ['local/out.md', 'local/scratchpad.md', 'local/a.log.md', 'local/notes/log']) assert.equal(tool.disposableLocal(path), false, path);
  assert.equal(tool.disposableLocal('local/runs.log', true), false, 'a folder named like a log is walked');

  const fx = makeFixture('merge-branch-local');
  try {
    fx.write('local/scratch/a.md', 'scratch\n');
    fx.write('local/z.log', 'log\n');
    fx.write('local/runs/gate.log', 'log\n');
    assert.equal(tool.firstFile(join(fx.root, 'local'), tool.disposableLocal), null, 'only disposable output keeps nothing');
    fx.write('local/keep/notes.md', 'mine\n');
    assert.equal(tool.firstFile(join(fx.root, 'local'), tool.disposableLocal), 'local/keep/notes.md');
    assert.equal(tool.firstFile(join(fx.root, 'local')) !== null, true, 'with no rule, any file is found');
  } finally {
    fx.cleanup();
  }
});

test('a folder under .claude/worktrees/ git does not list goes when it holds no .git, is no link and no process runs inside', () => {
  const half: Folder = { path: '/r/.claude/worktrees/agent-h', link: false, git: false, processes: [] };
  assert.deepEqual(tool.folderVerdict(half), { remove: true, why: '' });
  const link = tool.folderVerdict({ ...half, link: true });
  assert.equal(link.remove, false);
  assert.match(link.why, /a link \(a reparse point\), never followed or removed here/);
  const git = tool.folderVerdict({ ...half, git: true });
  assert.equal(git.remove, false);
  assert.match(git.why, /holds a \.git/);
  const held = tool.folderVerdict({ ...half, processes: [{ pid: 7, exe: '', command: 'node land.mjs' }, { pid: 8, exe: '', command: 'x'.repeat(400) }] });
  assert.equal(held.remove, false);
  assert.match(held.why, /pid 7: node land\.mjs; pid 8: x+…\)/);
  assert.match(held.why, /never stopped here: end it, and the next prune removes the folder/);
  assert.ok(held.why.length < 400, 'a long command line is cut short');
  const unread = tool.folderVerdict({ path: half.path, link: false, git: false, processError: 'ps could not start' });
  assert.equal(unread.remove, false);
  assert.match(unread.why, /could not be read \(ps could not start\)/);
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
  // A long-lived build server carried a broken environment into a publish on 2026-09-30, and the deployment rehearsal ran
  // after a shutdown of every build server; since REHLANE2b its builds keep servers of their own, and BEFORESTEP1 left a gate no
  // step before it at all: a planned gate has no `before`.
  assert.ok(plan.every((gate) => !('before' in gate)));
});

test("this repository's plan runs every declared gate once, fast first, and the rehearsals and the web gates last", () => {
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
    ['npm run rehearse', 'npm run rehearse:family', 'npm run test:web:e2e', 'npm run test:web:unit', 'npm run test:web:measured', 'npm run rehearse:deploy']);
  assert.ok(!('before' in plan.at(-1)!));
});

// ---------------------------------------------------------------------------------------------------
// GATE3: a merge runs the gates its lanes reach; the full set runs before the install is staged

const repoPlan = tool.readPlan(repoRoot);
const repoLanes = tool.readLanes(repoRoot)!.lanes;
// GATESLIM1: `runs` and `selected` are what `--gated` runs (what a merge's paths reach, the long gates apart): the lane table's
// own answer, which these tests hold to the code. `mergeRuns` is what a merge runs by default: the blocking checks only.
const selected = (paths: string[], full = false, gated = true) => tool.selectGates(repoPlan, paths, { lanes: repoLanes, full, gated }).gates;
const mergeRuns = (paths: string[], full = false): string[] => selected(paths, full, false).filter((entry) => entry.run).map((entry) => entry.gate.name);
/** What a merge runs. */
const runs = (paths: string[], full = false): string[] => selected(paths, full).filter((entry) => entry.run).map((entry) => entry.gate.name);
/** What the lane table says can see the change, run or not: what holds the table to the code. */
const reaches = (paths: string[]): string[] => selected(paths).filter((entry) => entry.reached).map((entry) => entry.gate.name);
/** The gates whose verdicts a change to these paths makes stale (GATE6), in the plan's order. */
const stalens = (paths: string[]): string[] => ALL.filter((gate) => tool.pathsReaching(gate, paths, { lanes: repoLanes }).length > 0);
const BASE = ['universal', 'code-map', 'orient-index', 'test-map', 'cli'];
const ALL = repoPlan.map((gate) => gate.name);
// GATE5: the real-process halves and the deployment rehearsal, 30 to 70 minutes a merge, run only in the full set.
// GATESLIM1: and the release and family rehearsals, 11 minutes of light gates plus 27 of the family rehearsal on one service merge.
const LONG = ['driver-process', 'driver-process-git', 'modules-process', 'deployment'];
/** GATESLIM1: what a merge runs by default, the blocking checks; every other gate it reaches is "in the CI beside". */
const BLOCKING = ['universal', 'code-map', 'orient-index', 'test-map'];
/** The kept names' table (SESSDEL1c): among the driver's tests, and the page's twin reads it too. */
const KEPT_NAMES = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/kept-names.json';
/** The backlog merges' table (LANDBACKLOG1): among the driver's tests, and the merge tool's twin reads it too. */
const BACKLOG_MERGES = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/backlog-merges.json';
/**
 * The driver's sources whose declarations the page's twin vitests parse (MOD9e): SESSDEL1c's scan found them. Each by its
 * name, at whatever depth of the driver's folder it is tracked (STRUCT1, D166).
 */
const PAGE_PARSED_NAMES = ['NoteCodes.cs', 'SessionEvents.cs', 'InstructionAccount.cs', 'Trace.Chain.cs', 'AutoLanding.cs', 'Landing.cs'];
const DRIVER_FOLDER = 'src/Daoris.Desktop/Daoris.Desktop.Driver/';
const PAGE_PARSED = PAGE_PARSED_NAMES
  .map((name) => trackedFiles().find((path) => path.startsWith(DRIVER_FOLDER) && posix.basename(path) === name) ?? `${DRIVER_FOLDER}${name}`);
/** MOD9e's rule, by the names it holds wherever the driver keeps them. */
const isPageParsedRule = (rule: Rule) => rule.paths?.some((path) => PAGE_PARSED_NAMES.some((name) => path === `${DRIVER_FOLDER}**/${name}`)) ?? false;

test('the long gates are the real-process halves and the deployment rehearsal, read from what each runs', () => {
  assert.deepEqual(repoPlan.filter((gate) => tool.isLongGate(gate)).map((gate) => gate.name), LONG);
  assert.equal(tool.isLongGate({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/process.runsettings' }), true);
  assert.equal(tool.isLongGate({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/git.process.runsettings' }), true);
  assert.equal(tool.isLongGate({ name: 'renamed', run: 'npm run rehearse:deploy' }), true);
  assert.equal(tool.isLongGate({ name: 'driver', run: 'dotnet test x --filter Category!=Process' }), false);
  assert.equal(tool.isLongGate({ name: 'rehearse-family', run: 'npm run rehearse:family' }), false);
});

test('a merge runs the baseline and what each changed path can reach, but the long gates only in the full set', () => {
  // Each case is what the lane table says can see the change; a merge runs it all but the long gates.
  const cases: [string, string[], string[]][] = [
    // The service's suite scans this repository's own doctrine and decisions, so the docs reach it.
    ['docs only', ['docs/2026-09-30-parallel-development-design.md'], [...BASE, 'service']],
    ["the steward's records", ['TASKS.md', 'docs/task-archive.md'], [...BASE, 'service']],
    // The .NET suites read the page's catalogues and sources: MOD9's incident.
    ['the web shell', ['src/Daoris.Web/src/App.tsx'], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit']],
    ["the web's settings", ['src/Daoris.Web/src/settings/DriverDomain.tsx'], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit']],
    ['the driver', ['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'],
      [...BASE, 'driver', 'modules', 'driver-process', 'driver-process-git', 'modules-process', 'rehearse-family', 'deployment']],
    ['ServiceHostLocator', ['src/Daoris.Desktop/Daoris.Desktop.Driver/ServiceHostLocator.cs'],
      [...BASE, 'driver', 'modules', 'driver-process', 'driver-process-git', 'modules-process', 'rehearse-family', 'deployment']],
    // MOD9e: the page's twin vitests parse these driver sources' declarations, so a change to one runs the web gate's unit
    // run as well (WEBSPLIT1), and not its build.
    ...PAGE_PARSED.map((path): [string, string[], string[]] => [`${path.split('/').at(-1)}, which a page twin parses`, [path],
      [...BASE, 'driver', 'modules', 'driver-process', 'driver-process-git', 'modules-process', 'rehearse-family', 'web-unit', 'deployment']]),
    // PROC1b: the driver's Process classes run in two halves, the git-only ones four at once and the rest eight at once (PROCPAR1).
    ["the driver's tests", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs'], [...BASE, 'driver', 'driver-process', 'driver-process-git']],
    // GATE6: a browser test is the web gate's alone; no .NET suite reads it (the test below holds that).
    ['a browser test', ['src/Daoris.Web/e2e/platform.spec.ts'], [...BASE, 'web']],
    // FLOWCHART1e: what the measured-box check draws reaches its gate beside the web shell's; its own specs and config reach it alone.
    ...['flowLayout.ts', 'Flow.tsx', 'Flow.stories.tsx', 'flowFrames.tsx', 'ui.tsx', 'ui.stories.tsx', 'storyWidths.tsx'].map((name): [string, string[], string[]] =>
      [`${name}, which the measured-box check draws`, [`src/Daoris.Web/src/design/${name}`], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit', 'web-measured']]),
    ["the measured-box check's spec", ['src/Daoris.Web/measured/flow.spec.ts'], [...BASE, 'web-measured']],
    ["the measured-box check's config", ['src/Daoris.Web/playwright.measured.config.ts'], [...BASE, 'web-measured']],
    ['a design file it does not draw', ['src/Daoris.Web/src/design/theme.ts'], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit']],
    // MOD9c: the driver's console and occasion scans read every C# source of the modules and the app, from its source root.
    ['the modules', ['src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/DriverModule.cs'], [...BASE, 'driver', 'modules', 'modules-process', 'deployment']],
    ["the modules' loop", ['src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/DriverLoop.cs'], [...BASE, 'driver', 'modules', 'modules-process', 'deployment']],
    ['the desktop app', ['src/Daoris.Desktop/Daoris.Desktop.App/Program.cs'], [...BASE, 'driver', 'modules', 'modules-process', 'deployment']],
    // What those scans do not read stays the modules lane's alone: a project file, the icon, the modules' tests (the launcher is built by the driver gate, LAUNCHGATE1).
    ["the modules' project", ['src/Daoris.Desktop/Daoris.Desktop.Modules/Daoris.Desktop.Modules.csproj'], [...BASE, 'modules', 'modules-process', 'deployment']],
    ["the app's icon", ['src/Daoris.Desktop/Daoris.Desktop.App/daoris.ico'], [...BASE, 'modules', 'modules-process', 'deployment']],
    ['the launcher', ['src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs'], [...BASE, 'driver', 'modules', 'modules-process', 'deployment']],
    ["the modules' tests", ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/DriverModuleRoutesTests.cs'], [...BASE, 'modules', 'modules-process']],
    // GATESLIM1: the tests of the tools only the parent runs are the tool-tests gate, reached by those tools and their folder.
    ['the publish script', ['tools/desktop-publish.mjs'], [...BASE, 'tool-tests', 'rehearse-family', 'deployment']],
    // FAMILYFAIL1: the rehearsals' kit imports it too, to end what a timed-out command left running.
    ['the process queries the publish and the kit import', ['tools/processes.mjs'], [...BASE, 'tool-tests', 'rehearse', 'rehearse-family', 'deployment']],
    ['the service publisher the publish calls', ['tools/service-publish.mjs'], [...BASE, 'tool-tests', 'rehearse', 'rehearse-family', 'deployment']],
    ["the family rehearsal's set-up kit", ['tools/setup-kit.mjs'], [...BASE, 'tool-tests', 'rehearse-family']],
    ["the rehearsals' kit", ['tools/rehearsal-kit.mjs'], [...BASE, 'tool-tests', 'rehearse', 'rehearse-family', 'deployment']],
    ['a tool-test file', ['src/Daoris.Cli/tool-test/setup-kit.test.ts'], [...BASE, 'tool-tests']],
    ['the fixture the tool-tests share', ['src/Daoris.Cli/test/_fixture.ts'], [...BASE, 'tool-tests']],
    ['the tarball reader the publish imports', ['src/Daoris.Cli/src/platform/tarball.ts'], [...BASE, 'tool-tests', 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ["the CLI's keeping lock the driver's Process half runs", ['src/Daoris.Cli/src/platform/keeplock.ts'], [...BASE, 'driver', 'driver-process', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ['what the merge tool imports',['tools/doc-duplicates.mjs'], [...BASE, 'tool-tests']],
    ['the deployment rehearsal', ['tools/deployment-rehearsal.mjs'], [...BASE, 'deployment']],
    ['the canon', ['canon/core/rules/task-lifecycle.md'], [...BASE, 'service', 'rehearse', 'rehearse-family']],
    ['the examples', ['examples/engine/README.md'], [...BASE, 'service', 'rehearse-family', 'web']],
    // MOD9b: the driver reads the offers and the adoption playbook, as the service reads the digest's note table.
    ["the install's offers", ['examples/plugins/github-pull-request/land.mjs'], [...BASE, 'service', 'driver', 'driver-process', 'rehearse-family', 'web', 'deployment']],
    ['the adoption playbook', ['.claude/knowledge/adoption.md'], [...BASE, 'service', 'driver']],
    ["the digest's note table", ['tools/orient-index-fixtures/decision-notes.json'], [...BASE, 'service']],
    // HIST1m: the history words' table sits among the service's tests, and the driver's twin reads it too.
    ["the history words' table", ['src/Daoris.Service/Daoris.Service.Tests/fixtures/history-words.json'], [...BASE, 'service', 'driver']],
    // SESSDEL1c: the kept names' table sits among the driver's tests, and the page's twin reads it too.
    ["the kept names' table", [KEPT_NAMES], [...BASE, 'driver', 'web-unit']],
    // LANDBACKLOG1: the backlog merges' table sits among the driver's tests, and the merge tool's twin reads it too.
    ["the backlog merges' table", [BACKLOG_MERGES], [...BASE, 'tool-tests', 'driver']],
    // MIXAGENT1c: the person's order's table sits among the driver's tests, and the page's twin reads it too.
    ["the hand-over order's table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/hand-over-order.json'], [...BASE, 'driver', 'web-unit']],
    // REVIEWENV1c4: the review rules' table sits among the driver's tests, and the service's tool test reads its gate rows.
    ["the review rules' table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/review-rules.json'], [...BASE, 'service', 'driver']],
    // XAGENT1f4: the second-opinion rules' table, whose gate rows the service's tool test reads too.
    ["the second-opinion rules' table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/opinion-rules.json'], [...BASE, 'service', 'driver']],
    // BRAINSTORM1a2: the brainstorm answers' table, which the service's twin validator reads too; and the posture's list of the
    // connector's tools, which the service's registration test holds to its host's.
    ["the brainstorm answers' table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-answers.json'], [...BASE, 'service', 'driver']],
    // BRAINSTORM1a3: the brainstorm files' table, which the service's brainstorm desk reads too.
    ["the brainstorm files' table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-files.json'], [...BASE, 'service', 'driver']],
    ["the brainstorm posture", ['src/Daoris.Desktop/Daoris.Desktop.Driver/Brainstorms/Domain/BrainstormPermissions.cs'],
      [...BASE, 'service', 'driver', 'modules', 'driver-process', 'driver-process-git', 'modules-process', 'rehearse-family', 'deployment']],
    ["the service's tests", ['src/Daoris.Service/Daoris.Service.Tests/HistoryDeskTests.cs'], [...BASE, 'service']],
    // ORIENT2h6: the fence table sits among the CLI's fixtures, and the driver's SelfDescription twin reads it too, and
    // since ORIENT2h6b the service's MarkdownFence twin.
    ['the fence table', ['src/Daoris.Cli/test/fixtures/fence-cases.json'], [...BASE, 'service', 'driver']],
    // SWITCH1a: the rotation-use table, among the driver's fixtures since PUSH1f2e, which its twin reads; since SWITCH1d the
    // page's curve rules.
    ['the rotation-use table', ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/rotation-use.json'], [...BASE, 'driver', 'web-unit']],
    // PUSH1f2b (D194 point 7): the driver family's shared tables moved to the driver's fixtures with their last CLI readers gone, the
    // landing sentences, which cycle governs a unit, the bytes an edit of driver.json writes, D157's versions as graphs, a named graph
    // as a version, and a graph judged when saved; the CLI's keep lock test still reads the lock rows, and verify runs it at every merge.
    ["the driver's tables the CLI's TypeScript left", [
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/landing-says.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/graph-homes.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/driver-config-keeps.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/workflow-graphs.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/workflow-graph-versions.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/graph-problems.json',
    ], [...BASE, 'driver', 'driver-process', 'driver-process-git']],
    // KEEPALL1: the bytes an edit of each other home file a door edits writes sit among the CLI's fixtures, and the driver's
    // HomeFilesKeepTests reads them too; since KEEPALL1e the service's RemoteConfigKeepTests its remotes.json rows' reads, and since
    // KEEPALL1c the modules' BrowserSettingsTests browser/settings.json's.
    ['the home-files-keep table', ['src/Daoris.Cli/test/fixtures/home-files-keep.json'], [...BASE, 'service', 'driver', 'modules']],
    // SWITCH1b-iii: the usage curve's table, among the driver's fixtures since PUSH1f2e, which its twin reads; since SWITCH1d the
    // page's twin.
    ['the usage curve table', ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/usage-curves.json'], [...BASE, 'driver', 'modules', 'web-unit']],
    // PARKVIEW1: a park's question as each reader takes it, the driver's QuestionFilesTests and the page's parkQuestion.test.ts.
    ['the park question table', ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/park-question.json'], [...BASE, 'driver', 'web-unit']],
    ['release tooling', ['tools/release-rehearsal.mjs'], [...BASE, 'rehearse']],
    ['the CLI', ['src/Daoris.Cli/src/platform/args.ts'], [...BASE, 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    // PUSH1f2a: the driver's DriverProgramWordsProcessTests runs the CLI's entry, dispatcher, driver row and seam under node.
    ["the CLI's seam", ['src/Daoris.Cli/src/cli.ts'], [...BASE, 'driver', 'driver-process', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ["the CLI's seam's module", ['src/Daoris.Cli/src/platform/driverprogram.ts'], [...BASE, 'driver', 'driver-process', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    // ORIENT2e2: the service's roles twin reads the CLI's ROLES from its file, so that file reaches the service too; by its
    // name in whichever module folder it sits (STRUCT5d).
    ["the CLI's roles", ['src/Daoris.Cli/src/layout/documents.ts'], [...BASE, 'service', 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ["the CLI's roles, at the sources' top", ['src/Daoris.Cli/src/documents.ts'], [...BASE, 'service', 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ['the merge tool and its tests', ['tools/merge-branch.mjs', 'src/Daoris.Cli/tool-test/merge-branch.test.ts'], [...BASE, 'tool-tests']],
    ['the service', ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'],
      [...BASE, 'service', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit', 'deployment']],
    ['the devkit', ['src/Daoris.Devkit/Daoris.Devkit.Core/Gates.cs'], [...BASE, 'devkit']],
    // The driver's fast CategoryTests read the file too (PROCPAR1), so a change there runs the driver's fast half.
    ["the Process halves' settings", ['src/Daoris.Desktop/process.runsettings'], [...BASE, 'driver', 'driver-process', 'modules-process']],
    // PROC1c: the second file runs only the driver's git-only classes, four at once.
    ["the git-only classes' settings", ['src/Daoris.Desktop/git.process.runsettings'], [...BASE, 'driver-process-git']],
    ['the declared gates', ['daoris.gates.json'], ALL],
    // Written into every merge and held by its own check, which is in the baseline (ORIENT1a).
    ['the orientation index', ['docs/index/routes.md', 'docs/index/outlines/tools/merge-branch.mjs.md'], BASE],
    ['nothing at all', [], BASE],
  ];
  for (const [what, paths, listed] of cases) {
    // In the plan's order: the web's typecheck (MERGETSC1) is a check, before the CLI's verify.
    const expected = ALL.filter((gate) => listed.includes(gate));
    assert.deepEqual(reaches(paths), expected, `${what}: what the table says can see it`);
    assert.deepEqual(runs(paths), expected.filter((gate) => !LONG.includes(gate)), `${what}: what a merge runs`);
    // GATE6: the stage reads the same table, so the gates a changed path makes stale are the ones it reaches.
    if (paths.length) assert.deepEqual(stalens(paths), expected, `${what}: the verdicts it makes stale`);
  }
  // Lanes add up: a branch crossing two runs what either reaches.
  assert.deepEqual(runs(['docs/x.md', 'tools/release-rehearsal.mjs', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs']),
    [...BASE, 'service', 'driver', 'modules', 'rehearse']);

  // FLOWCHART1e's proof: a merge touching the flowchart's layout lists the measured-box check, saying what reached it, and
  // --gated runs it.
  const flowLayout = ['src/Daoris.Web/src/design/flowLayout.ts'];
  const measured = selected(flowLayout, false, false).find((entry) => entry.gate.name === 'web-measured')!;
  assert.equal(measured.reached, true);
  assert.match(measured.why, /^in the CI beside; src\/Daoris\.Web\/src\/design\/flowLayout\.ts/);
  assert.ok(runs(flowLayout).includes('web-measured'));
  assert.equal(runs(['src/Daoris.Web/src/App.tsx']).includes('web-measured'), false, 'a page change it does not draw');

  // GATESLIM1's proof: a branch that changes no tool the parent alone runs skips the tool-tests gate; --full runs it, as a light suite.
  assert.equal(runs(['src/Daoris.Service/Daoris.Service.Core/Asks.cs']).includes('tool-tests'), false);
  assert.equal(runs(['src/Daoris.Cli/src/cli.ts']).includes('tool-tests'), false);
  assert.equal(runs(['tools/merge-branch.mjs']).includes('tool-tests'), true);
  assert.equal(runs([], true).includes('tool-tests'), true);
  const slim = repoPlan.find((gate) => gate.name === 'tool-tests')!;
  assert.equal(slim.kind, 'suite');
  assert.equal(tool.isHeavyGate(slim), false);

  // GATE5's proof: a driver change runs seven gates, and says the long four are in the full set before staging.
  const driver = selected(['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs']);
  assert.deepEqual(driver.filter((entry) => entry.run).map((entry) => entry.gate.name),
    ['universal', 'code-map', 'orient-index', 'test-map', 'cli', 'driver', 'modules', 'rehearse-family']);
  for (const name of LONG) {
    const entry = driver.find((candidate) => candidate.gate.name === name)!;
    assert.equal(entry.run, false, name);
    assert.match(entry.why, /^in the full set before staging/, name);
    assert.match(entry.why, /Driver\.cs/, `${name} says what reached it`);
  }
  // GATESLIM1: by default a merge runs the blocking checks alone; every other gate it reaches is listed, skipped, "in the CI beside";
  // the long gates keep their own reason; --gated runs what the paths reach; --full runs every gate and a rerun names one.
  const service = ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'];
  assert.deepEqual(mergeRuns(service), BLOCKING);
  assert.deepEqual(mergeRuns([]), BLOCKING);
  assert.deepEqual(runs(service), [...BASE, 'service', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit']);
  assert.deepEqual(mergeRuns(service, true), ALL);
  const byDefault = selected(service, false, false);
  for (const name of ['cli', 'tool-tests', 'service', 'rehearse', 'rehearse-family', 'web']) {
    const entry = byDefault.find((candidate) => candidate.gate.name === name)!;
    assert.equal(entry.run, false, name);
    assert.match(entry.why, /^in the CI beside/, name);
  }
  assert.match(byDefault.find((entry) => entry.gate.name === 'service')!.why, /Asks\.cs/, 'it still says what reached the gate');
  assert.match(byDefault.find((entry) => entry.gate.name === 'deployment')!.why, /^in the full set before staging/);
  assert.match(byDefault.find((entry) => entry.gate.name === 'driver-process')!.why, /^in the full set before staging/);
  assert.equal(byDefault.find((entry) => entry.gate.name === 'cli')!.reached, true, 'the table still reaches the CLI at every merge');
  // --full still names every gate.
  assert.deepEqual(runs(['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'], true), ALL);
});

test("a merge that changes the web's sources blocks on its typecheck, and one that does not lists it in the CI beside (MERGETSC1)", () => {
  const page = ['src/Daoris.Web/src/App.tsx'];
  const service = ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'];
  const check = repoPlan.find((gate) => gate.name === 'web-types')!;
  assert.equal(check.run, 'npm run typecheck:web');
  assert.equal(check.kind, 'check', 'among the checks, fast first');
  assert.equal(tool.isHeavyGate(check), false);
  assert.equal(tool.isProcessLane(check), false);
  assert.deepEqual(tool.laneSchedule(repoPlan, { scripts: rootScripts() }).components['web-types'], [], 'it builds no .NET component');

  // The plan names it, and why, as it names the other blocking checks.
  assert.deepEqual(mergeRuns(page), [...BLOCKING, 'web-types', 'web-rules']);
  const entry = selected(page, false, false).find((candidate) => candidate.gate.name === 'web-types')!;
  assert.match(entry.why, /^blocks the merge: src\/Daoris\.Web\/src\/App\.tsx \(the web's typecheck/);
  assert.match(tool.selectionLines(selected(page, false, false)).join('\n'), /5\.\s+web-types\s+check\s+blocks the merge/);
  assert.deepEqual(mergeRuns(['src/Daoris.Web/tsconfig.json']), [...BLOCKING, 'web-types'], 'what configures its compiler');

  // A merge that changes nothing it reads skips it, even a path the lane table places nowhere.
  for (const paths of [service, ['src/Daoris.Web/e2e/rail.spec.ts'], ['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'], ['TASKS.md']]) {
    assert.deepEqual(mergeRuns(paths), BLOCKING, paths[0]);
    assert.equal(runs(paths).includes('web-types'), false, `--gated: ${paths[0]}`);
    assert.equal(stalens(paths).includes('web-types'), false, `its verdict stands: ${paths[0]}`);
  }
  assert.equal(selected(service, false, false).find((candidate) => candidate.gate.name === 'web-types')!.why, 'in the CI beside');
  assert.ok(runs(page).includes('web-types'));
  assert.ok(stalens(page).includes('web-types'));
  assert.ok(mergeRuns(service, true).includes('web-types'), '--full runs it');
});

test("a merge that changes the web's sources blocks on its design and structure checks, and one that does not lists them in the CI beside (MERGEWEBRULES1)", () => {
  const story = ['src/Daoris.Web/src/design/ui.stories.tsx'];
  const service = ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'];
  const check = repoPlan.find((gate) => gate.name === 'web-rules')!;
  assert.equal(check.run, 'npm run check:web-rules');
  assert.equal(rootScripts()['check:web-rules'], 'npm --prefix src/Daoris.Web run test -- src/design/tokens.test.ts src/test/structure.test.ts',
    'the two files, not the whole unit run');
  assert.equal(check.kind, 'check', 'among the checks, fast first');
  assert.equal(tool.isHeavyGate(check), false);
  assert.equal(tool.isProcessLane(check), false);
  assert.deepEqual(tool.laneSchedule(repoPlan, { scripts: rootScripts() }).components['web-rules'], [], 'it builds no .NET component');

  // The plan names it, and why, beside the web's typecheck.
  assert.deepEqual(mergeRuns(story), [...BLOCKING, 'web-types', 'web-rules']);
  const entry = selected(story, false, false).find((candidate) => candidate.gate.name === 'web-rules')!;
  assert.match(entry.why, /^blocks the merge: src\/Daoris\.Web\/src\/design\/ui\.stories\.tsx \(the web's design and structure checks/);
  assert.match(tool.selectionLines(selected(story, false, false)).join('\n'), /6\.\s+web-rules\s+check\s+blocks the merge/);
  assert.deepEqual(mergeRuns(['src/Daoris.Web/vite.config.ts']), [...BLOCKING, 'web-types', 'web-rules'], 'what configures its tests');
  assert.deepEqual(mergeRuns(['src/Daoris.Web/tsconfig.json']), [...BLOCKING, 'web-types'], 'the compiler alone is the typecheck\'s');

  // A merge that changes nothing it reads skips it, even a path the lane table places nowhere.
  for (const paths of [service, ['src/Daoris.Web/e2e/rail.spec.ts'], ['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'], ['TASKS.md']]) {
    assert.deepEqual(mergeRuns(paths), BLOCKING, paths[0]);
    assert.equal(runs(paths).includes('web-rules'), false, `--gated: ${paths[0]}`);
    assert.equal(stalens(paths).includes('web-rules'), false, `its verdict stands: ${paths[0]}`);
  }
  assert.equal(selected(service, false, false).find((candidate) => candidate.gate.name === 'web-rules')!.why, 'in the CI beside');
  assert.ok(runs(story).includes('web-rules'));
  assert.ok(stalens(story).includes('web-rules'));
  assert.ok(mergeRuns(service, true).includes('web-rules'), '--full runs it');
});

/**
 * STRUCT1 (D166): the driver's files move into module folders one module per branch, so the rule naming the sources the page's
 * twins parse names each by its file's name at any depth of the driver's folder. A move edits no rule, a file of the same
 * name in another project is not one of them, and each name is one tracked source, so a rename fails here rather than
 * leaving a glob that matches nothing.
 */
test("the driver's sources the page's twins parse reach the web gate by their names, wherever the driver keeps them (STRUCT1)", () => {
  const rule = tool.REACH.find(isPageParsedRule);
  assert.ok(rule, "MOD9e's rule names each source by its name under the driver's folder");
  for (const name of PAGE_PARSED_NAMES) {
    for (const path of [`${DRIVER_FOLDER}${name}`, `${DRIVER_FOLDER}Platform/${name}`, `${DRIVER_FOLDER}Trees/Domain/${name}`]) {
      assert.ok(runs([path]).includes('web-unit'), `${path} reaches the web gate's unit run`);
    }
  }
  assert.equal(runs(['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Landing.cs']).includes('web-unit'), false, "the driver's tests' file of that name");
  assert.equal(runs(['src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Landing.cs.bak']).includes('web-unit'), false, 'a name that only begins so');
  const files = trackedFiles();
  for (const glob of rule.paths!) {
    assert.ok(glob.startsWith(`${DRIVER_FOLDER}**/`), `${glob} names its file at any depth of the driver's folder`);
    assert.equal(files.filter(tool.laneMatcher([glob])).length, 1, `${glob} is one tracked source`);
  }
});

test('what a merge writes rather than merges is the orientation index, by its folder, as daoris.gates.json declares it (GENREGEN1)', () => {
  const declared = tool.readGenerated(repoRoot);
  assert.deepEqual(declared, [{ folder: 'docs/index', run: 'node tools/orient-index.mjs' }]);
  assert.equal(tool.isGenerated('docs/index/routes.md', declared), true);
  assert.equal(tool.isGenerated('docs/index/outlines/src/Daoris.Web/src/App.tsx.md', declared), true);
  assert.equal(tool.isGenerated('docs/index.md', declared), false);
  assert.equal(tool.isGenerated('docs/decisions/D1.md', declared), false);
  assert.ok(existsSync(join(repoRoot, 'tools/orient-index.mjs')), 'the command that writes docs/index/ runs a tool that is not here');
  for (const { folder } of declared) {
    // A hand merge meets no textual merge of it either: the attribute keeps one side whole, and the tool writes it again. It is
    // marked generated too (LANDREBASE1), so a landing's replay takes the line's copy of it.
    assert.match(readFileSync(join(repoRoot, '.gitattributes'), 'utf8'), new RegExp(`^${folder.replace(/\//g, '\\/')}/\\*\\* -merge linguist-generated$`, 'm'));
  }
});

test('a repository declaring no generated folder writes none, and an entry without its command is refused (GENREGEN1)', () => {
  const fx = makeFixture('merge-branch-generated');
  assert.deepEqual(tool.readGenerated(fx.root), [], 'no declaration at all');
  fx.write('daoris.gates.json', JSON.stringify({ gates: [{ name: 'g', run: 'node gate.mjs' }] }));
  assert.deepEqual(tool.readGenerated(fx.root), []);
  fx.write('daoris.gates.json', JSON.stringify({ gates: [], generated: [{ paths: 'out/', run: 'node write.mjs' }] }));
  assert.deepEqual(tool.readGenerated(fx.root), [{ folder: 'out', run: 'node write.mjs' }], 'a trailing slash is the folder');
  fx.write('daoris.gates.json', JSON.stringify({ gates: [], generated: [{ paths: 'out' }] }));
  assert.match(refusal(() => tool.readGenerated(fx.root)).message, /every generated entry needs both 'paths' and 'run'/);
  fx.cleanup();
});

// MERGEJOIN1: what union leaves when two branches each append a note to one decision, set apart by the merge.
test("a note's label straight under a line gets one blank line above it; nothing else in the file moves", () => {
  const glued = [
    '## D1 — a thing (2026-10-04)', '',
    'Body of it.', '',
    '**UX6e, built 2026-10-04: left.** Left text', 'more left.',
    '**UX6f, built 2026-10-04: right.** Right text.',
    '*Amended by D130 (ROW3, 2026-10-02): the change.*',
    // A fence's lines are text, and an emphasised sentence is not a note: neither is touched.
    '```', 'text', '**Built 2026-10-02 (EX1).**', '```',
    'A paragraph.', '**Proven without the rehearsal**: the checks.', '',
  ].join('\n');
  const { text, set } = tool.setApart(glued);
  assert.equal(text, [
    '## D1 — a thing (2026-10-04)', '',
    'Body of it.', '',
    '**UX6e, built 2026-10-04: left.** Left text', 'more left.', '',
    '**UX6f, built 2026-10-04: right.** Right text.', '',
    '*Amended by D130 (ROW3, 2026-10-02): the change.*',
    '```', 'text', '**Built 2026-10-02 (EX1).**', '```',
    'A paragraph.', '**Proven without the rehearsal**: the checks.', '',
  ].join('\n'));
  // Each label with its line once set apart, 1-based, as a reader opens it.
  assert.deepEqual(set, [
    { line: 8, label: '**UX6f, built 2026-10-04: right.** Right text.' },
    { line: 10, label: '*Amended by D130 (ROW3, 2026-10-02): the change.*' },
  ]);
  // Set apart already, it is left alone; and a file of CRLF lines gains a CRLF blank line, not a bare one.
  assert.deepEqual(tool.setApart(text), { text, set: [] });
  assert.equal(tool.setApart('A.\r\n**Built 2026-10-04 (X1).**\r\n').text, 'A.\r\n\r\n**Built 2026-10-04 (X1).**\r\n');
});

// UNIONGLUE1: a heading union left under another entry's last line, set apart unless a side of the merge held it so.
test("a heading straight under a line gets one blank line above it, unless a side already held that join", () => {
  const old = ['# Archive', '', '## A1 — old (2026-10-01)', 'Done.', '## A2 — glued long ago (2026-10-02)', 'Done.'];
  const merged = [...old, '## A3 — left (2026-10-20)', 'Left.', '## A4 — right (2026-10-20)', 'Right.',
    // A fence's lines are text: a heading quoted there is not one.
    '```', 'x', '## not a heading', '```', ''].join('\n');
  const { text, set } = tool.setApartHeadings(merged, [[...old, '', '## A3 — left (2026-10-20)', 'Left.', ''].join('\n')]);
  assert.equal(text, [...old, '', '## A3 — left (2026-10-20)', 'Left.', '', '## A4 — right (2026-10-20)', 'Right.',
    '```', 'x', '## not a heading', '```', ''].join('\n'));
  assert.deepEqual(set, [
    { line: 8, label: '## A3 — left (2026-10-20)' },
    { line: 11, label: '## A4 — right (2026-10-20)' },
  ]);
  assert.deepEqual(tool.setApartHeadings(text, [merged]), { text, set: [] });
  assert.equal(tool.setApartHeadings('A.\r\n## B\r\n').text, 'A.\r\n\r\n## B\r\n');
});

// MERGETASKS1: the backlog merged by its base, a row added or removed beside the other side's kept so, one both edited left.
test('the backlog merged by its base keeps a row a side added, drops one a side removed, and leaves one both sides edited', () => {
  const base = ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A2** two.', '- [ ] **A3** three.', ''].join('\n');
  // An added row beside a removed one, which git reads as one conflict.
  const added = tool.mergeByBase(base, base.replace('- [ ] **A2** two.\n', ''), base.replace('- [ ] **A3** three.\n', '- [ ] **A3** three.\n- [ ] **A4** four.\n'));
  assert.equal(added.text, ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A3** three.', '- [ ] **A4** four.', ''].join('\n'));
  assert.deepEqual([added.kept, added.dropped, added.both], [1, 1, []]);
  // Two rows removed side by side, one by each.
  const removed = tool.mergeByBase(base, base.replace('- [ ] **A1** one.\n', ''), base.replace('- [ ] **A2** two.\n', ''));
  assert.equal(removed.text, ['# Tasks', '', '- [ ] **A3** three.', ''].join('\n'));
  // Rows both add at one place are both kept, this side's first; a row both removed is removed once.
  const twice = tool.mergeByBase(base, base.replace('**A3** three.\n', '**A3** three.\n- [ ] **B1** mine.\n').replace('- [ ] **A1** one.\n', ''),
    base.replace('**A3** three.\n', '**A3** three.\n- [ ] **C1** theirs.\n').replace('- [ ] **A1** one.\n', ''));
  assert.equal(twice.text, ['# Tasks', '', '- [ ] **A2** two.', '- [ ] **A3** three.', '- [ ] **B1** mine.', '- [ ] **C1** theirs.', ''].join('\n'));
  // One row both sides edited is the person's: no text, and the base's line it is.
  const edited = tool.mergeByBase(base, base.replace('**A2** two.', '**A2** two, mine.'), base.replace('**A2** two.', '**A2** two, theirs.'));
  assert.equal(edited.text, null);
  assert.deepEqual(edited.both, [{ line: 4 }]);
  // A row one side edited and the other removed is the person's too; the same edit on both sides is not.
  assert.equal(tool.mergeByBase(base, base.replace('**A2** two.', '**A2** edited.'), base.replace('- [ ] **A2** two.\n', '')).text, null);
  const same = base.replace('**A2** two.', '**A2** edited.');
  assert.equal(tool.mergeByBase(base, same, same).text, same);
});

// LANDBACKLOG1: the rule is a twin. A landing's replay settles a backlog-only conflict by it, and the driver's BacklogMerge holds the
// same table, row for row.
test("the declared backlog and its merge by base read as the table the driver's landing holds too (LANDBACKLOG1)", () => {
  type Row = { name: string; base: string[]; ours: string[]; theirs: string[]; text: string[] | null; kept: number; dropped: number; both: number[] };
  const table = JSON.parse(readFileSync(join(repoRoot, BACKLOG_MERGES), 'utf8')) as {
    paths: { name: string; manifest: string | null; backlog: string | null }[];
    merges: Row[];
  };
  assert.ok(table.paths.length >= 10 && table.merges.length >= 8, 'the table holds its rows');
  for (const row of table.paths) assert.equal(tool.backlogOf(row.manifest), row.backlog, row.name);
  for (const row of table.merges) {
    const merged = tool.mergeByBase(row.base.join('\n'), row.ours.join('\n'), row.theirs.join('\n'));
    assert.deepEqual(
      { text: merged.text, kept: merged.kept, dropped: merged.dropped, both: merged.both.map((entry: { line: number }) => entry.line) },
      { text: row.text?.join('\n') ?? null, kept: row.kept, dropped: row.dropped, both: row.both },
      row.name);
  }
});

test('a note set apart is named by its task, or by its label when it names none', () => {
  assert.equal(tool.noteName('**UX6e, built 2026-10-04: Agents is a place.** The bar.'), 'UX6e note');
  assert.equal(tool.noteName('**Built 2026-10-03 (DOC8b): the check for a folder** (point 4).'), 'DOC8b note');
  assert.equal(tool.noteName('*Amended by D130 (ROW3, 2026-10-02): the change.*'), 'ROW3 note');
  assert.equal(tool.noteName('**Read 2026-10-02: the reading.** More.'), 'note "Read 2026-10-02: the reading."');
});

test('a path no rule places runs every gate but the long ones, and says which path; --full runs every gate and says so', () => {
  const unplaced = tool.selectGates(repoPlan, ['docs/x.md', 'src/Daoris.Somewhere/Program.cs'], { lanes: repoLanes, gated: true });
  assert.deepEqual(unplaced.unplaced, ['src/Daoris.Somewhere/Program.cs']);
  // MERGETSC1, MERGEWEBRULES1: but a path check, which its own paths reach.
  const typechecks = tool.PATH_CHECKS.map((check) => check.gate);
  assert.deepEqual(unplaced.gates.filter((entry) => !entry.reached).map((entry) => entry.gate.name), typechecks, 'when unsure, a path reaches everything');
  assert.deepEqual(unplaced.gates.filter((entry) => entry.run).map((entry) => entry.gate.name), ALL.filter((gate) => !LONG.includes(gate) && !typechecks.includes(gate)));
  const driverProcess = unplaced.gates.find((entry) => entry.gate.name === 'driver-process')!;
  assert.match(driverProcess.why, /^in the full set before staging/);
  assert.match(driverProcess.why, /src\/Daoris\.Somewhere\/Program\.cs/);
  assert.match(unplaced.gates.find((entry) => entry.gate.name === 'devkit')!.why, /src\/Daoris\.Somewhere\/Program\.cs/);

  const full = tool.selectGates(repoPlan, ['docs/x.md'], { lanes: repoLanes, full: true });
  assert.deepEqual(full.gates.filter((entry) => entry.run).map((entry) => entry.gate.name), ALL);
  assert.ok(full.gates.filter((entry) => !BASE.includes(entry.gate.name)).every((entry) => /--full/.test(entry.why)));

  // Every gate says why it runs or why it is skipped.
  const docs = tool.selectGates(repoPlan, ['docs/x.md'], { lanes: repoLanes, gated: true });
  const why = Object.fromEntries(docs.gates.map((entry) => [entry.gate.name, entry.why]));
  assert.match(why['cli']!, /every merge/);
  assert.match(why['service']!, /docs\/x\.md/);
  assert.match(why['devkit']!, /no changed path reaches it/);
  // A long gate skipped says where it runs, reached or not.
  assert.match(why['driver-process']!, /^in the full set before staging/);
});

test('a gate the lane table never names runs at every merge, and a rule naming a gate not in the plan names nothing', () => {
  const plan = tool.gatePlan([
    { name: 'cli', run: 'npm run verify' },
    { name: 'mystery', run: 'node mystery.mjs' },
    { name: 'web', run: 'npm run test:web' },
  ]);
  const reach: Rule[] = [{ paths: ['docs/**'], gates: ['nowhere'], why: 'a gate this repository does not run' }];
  const selected = tool.selectGates(plan, ['docs/x.md'], { reach, gated: true });
  assert.deepEqual(selected.gates.map((entry) => [entry.gate.name, entry.run]), [['cli', true], ['mystery', true], ['web', true]]);
  assert.match(selected.gates.find((entry) => entry.gate.name === 'mystery')!.why, /the lane table does not name it/);
  // Once a rule names a gate, a merge whose paths do not reach it skips it.
  const named = tool.selectGates(plan, ['docs/x.md'], { reach: [...reach, { paths: ['src/**'], gates: ['web', 'mystery'], why: 'the page' }], gated: true });
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
 * change to any of those must reach it, or the table hides the rehearsal's own failure (and, for the
 * deployment rehearsal, which only the full set runs since GATE5, says the wrong thing about it). The
 * spawned scripts are named here by hand (a spawn is not an import); the imports are followed.
 */
test('every tool a rehearsal imports or starts is one whose change reaches that rehearsal', () => {
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
    for (const file of closure(entry)) assert.ok(reaches([file]).includes(gate), `${file} (from ${entry}) does not reach ${gate}`);
  }
});

/**
 * MOD9's incident, as a test: two C# tests read the web's catalogues, and a batch called "web only"
 * skipped them. Every repository path a .NET test reads must be one whose change reaches that test's
 * suite, which a merge then runs, and its Process half when the reading class is a Process class,
 * which the full set runs (GATE5). A read is a path under `src/Daoris.…` (`srcReads`), or under any other
 * top-level folder (`treeReads`, MOD9b): the service's twin read the digest's note table under `tools/`,
 * and the scan, seeing `src/` alone, never asked for the row that sends its change there (ORIENT1h). Or it
 * is a path under a source root (`sourceRootReads`, MOD9c): the driver's console and occasion scans read
 * the modules' and the app's sources from `SourceRoot()`, which neither pattern saw. Or it is read through
 * a local holding a repository folder (`localReads`, MOD9d).
 *
 * MOD9d: each read must also be a file or folder this repository tracks, and one a rule of the lane table
 * places. A read no rule placed reached every gate, so the checks after it passed while holding nothing:
 * three modules tests read the whole desktop tree, `src/Daoris.Desktop`, through a local, and a driver test's
 * scratch workspace in this repository's layout read as `src/Daoris.Desktop/app/bin`.
 */
test("every repository file a .NET suite reads is one whose change reaches that suite (MOD9's incident)", () => {
  // A suite's fast gate, its Process half, and the half its Parallel=Git classes run in (PROC1b), where it has one.
  const suites: [string, string, string | null, string | null][] = [
    ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/', 'driver', 'driver-process', 'driver-process-git'],
    ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/', 'modules', 'modules-process', null],
    ['src/Daoris.Service/Daoris.Service.Tests/', 'service', null, null],
    ['src/Daoris.Service/Daoris.Service.Http.Tests/', 'service', null, null],
    ['src/Daoris.Devkit/Daoris.Devkit.Tests/', 'devkit', null, null],
  ];
  const files = trackedFiles();
  const folders = folderSet(files);
  let reads = 0;
  let outsideSrc = 0;
  let rooted = 0;
  let throughLocals = 0;
  let byName = 0;
  const problems: string[] = [];
  for (const file of files.filter((path) => path.endsWith('.cs'))) {
    const suite = suites.find(([folder]) => file.startsWith(folder));
    if (!suite) continue;
    const text = readFileSync(join(repoRoot, file), 'utf8');
    // A class's traits are its code's, not its strings': CategoryTests holds both traits in its fixtures' raw strings (PROCPAR1).
    const code = text.replace(/"""[\s\S]*?"""/g, '""').replace(/"(?:[^"\\\r\n]|\\.)*"/g, '""');
    const isProcess = /\[Trait\(Category\.Name, Category\.Process\)\]/.test(code);
    const half = /\[Trait\(Category\.Parallel, Category\.Git\)\]/.test(code) ? suite[3] ?? suite[2] : suite[2];
    const found = srcReads(text, files);
    const outside = treeReads(text, files);
    const fromRoot = sourceRootReads(file, text, files);
    rooted += fromRoot.length;
    const fromLocals = localReads(text, files);
    throughLocals += fromLocals.length;
    const named = namedReads(text, files);
    byName += named.length;
    outsideSrc += [...outside, ...fromLocals].filter((read) => !read.startsWith('src/')).length;
    for (const read of [...found, ...outside, ...fromRoot, ...fromLocals, ...named]) {
      reads += 1;
      // MOD9d: a read no rule places reaches every gate, so the two checks below pass while holding nothing.
      if (!isReal(read, files, folders)) problems.push(`${file} reads ${read}, which is no file or folder this repository tracks`);
      else if (tool.selectGates(repoPlan, [read], { lanes: repoLanes }).unplaced.length) problems.push(`${file} reads ${read}, which no rule of the lane table places`);
      else if (!runs([read]).includes(suite[1])) problems.push(`${file} reads ${read}, and a merge changing it does not run ${suite[1]}`);
      else if (isProcess && half && !reaches([read]).includes(half)) problems.push(`${file} (a Process class) reads ${read}, and a change there does not reach ${half}`);
    }
  }
  assert.deepEqual(problems, [], 'each a read the lane table does not hold');
  assert.ok(reads >= 10, `the scan found only ${reads} reads: its pattern no longer matches how the tests read the repository`);
  assert.ok(outsideSrc >= 3, `the scan found only ${outsideSrc} reads outside src/: its pattern no longer matches how the tests read the repository`);
  assert.ok(rooted >= 10, `the scan found only ${rooted} reads from a source root: its pattern no longer matches how the tests read their sources`);
  assert.ok(throughLocals >= 10, `the scan found only ${throughLocals} reads through a local: its pattern no longer matches how the tests read through one`);
  assert.ok(byName >= 8, `the scan found only ${byName} reads by name: its pattern no longer matches how the tests find a source (STRUCT1)`);
});

test('the scan reads a source found by name wherever the tree keeps it, and a walk of a project for its sources (STRUCT1)', () => {
  const files = [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/Landing.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpGoProposals.cs',
  ];
  const text = [
    '        var source = File.ReadAllText(SourceFiles.Driver("Landing.cs"));',
    '        var loop = SourceFiles.Find("Daoris.Desktop.Modules", "DriverLoop.cs");',
    // A name no file has reads nothing here; the finder refuses it when the test runs.
    '        SourceFiles.Driver("Gone.cs");',
    '        var sources = SourceFiles.All(SourceFiles.DriverProject, "GitBranches*.cs");',
    '        var folder = Path.GetDirectoryName(SourceFiles.Driver($"{kinds[0]}.cs"));',
  ].join('\n');
  assert.deepEqual(namedReads(text, files), [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/Landing.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/probe.cs',
  ]);
});

/**
 * STRUCT1 (D166): the paths a .NET test reads through `SourceFiles`, which finds a source by its name at any depth of its
 * project, so the read is the one tracked file of that name under the project (`SourceFiles.Driver("Landing.cs")`,
 * `SourceFiles.Find("Daoris.Desktop.Modules", "DriverLoop.cs")`). A walk of a project for a pattern (`SourceFiles.All`), or a
 * name the scan cannot read (`SourceFiles.Driver($"{name}.cs")`), reads the project's folder, for its C# sources when the
 * pattern names them.
 */
function namedReads(text: string, files: readonly string[]): string[] {
  const driver = 'Daoris.Desktop.Driver';
  const under = (project: string) => `src/Daoris.Desktop/${project}/`;
  const reads = new Set<string>();
  const one = (project: string, name: string) => {
    const found = files.filter((path) => path.startsWith(under(project)) && posix.basename(path) === name);
    if (found.length === 1) reads.add(found[0]!);
  };
  for (const match of text.matchAll(/\bSourceFiles\.Driver\(\s*"([^"\\]+)"\s*\)/g)) one(driver, match[1]!);
  for (const match of text.matchAll(/\bSourceFiles\.Find\(\s*"([^"\\]+)"\s*,\s*"([^"\\]+)"\s*\)/g)) one(match[1]!, match[2]!);
  for (const match of text.matchAll(/\bSourceFiles\.All\(\s*(?:"([^"\\]+)"|SourceFiles\.DriverProject)\s*,\s*"([^"\\]+)"\s*\)/g)) {
    reads.add(`${under(match[1] ?? driver)}${match[2]!.endsWith('.cs') ? 'probe.cs' : 'probe.txt'}`);
  }
  if (/\bSourceFiles\.Driver\(\s*\$/.test(text)) reads.add(`${under(driver)}probe.cs`);
  return [...reads];
}

/** Every folder that holds a tracked file, at any depth. */
function folderSet(files: readonly string[]): Set<string> {
  return new Set(files.flatMap((path) => path.split('/').slice(0, -1).map((_, i, parts) => parts.slice(0, i + 1).join('/'))));
}

/** MOD9d: a read names something here, a tracked file or a file in a tracked folder (`probe.txt`, `probe.cs`), or it holds nothing. */
function isReal(read: string, files: readonly string[], folders: ReadonlySet<string>): boolean {
  return files.includes(read) || (/\/probe\.(?:txt|cs)$/.test(read) && folders.has(posix.dirname(read)));
}

test('the scan sees a read under any top-level folder, and never a name, a sentence or a folder the test made (MOD9b)', () => {
  const files = [
    'tools/orient-index-fixtures/decision-notes.json', 'examples/plugins/a/plugin.json', '.claude/knowledge/adoption.md',
    '.claude/settings.json', 'docs/README.md', 'daoris.json', 'src/Daoris.Web/src/App.tsx',
  ];
  const source = [
    'File.ReadAllText(Path.Combine(Root(), "tools", "orient-index-fixtures", "decision-notes.json"));',
    'foreach (var plugin in Directory.EnumerateDirectories(Path.Combine(WorkspaceRoot.Folder, "examples", "plugins"))) { }',
    'return Path.Combine(at?.FullName ?? throw new InvalidOperationException("no root above the test"),',
    '    ".claude", "knowledge", "adoption.md");',
    // A folder the test made in this repository's own layout, by a local, a field or a property: not this repository.
    'File.ReadAllText(Path.Combine(room, ".claude", "settings.json"));',
    'File.WriteAllText(Path.Combine(_home, "docs", "README.md"), "x");',
    'File.WriteAllText(Path.Combine(Root, "docs", "README.md"), "x");',
    // A workspace's name, a sentence, a marker file, a path deeper in another run, and `src/`, which the scan reads itself.
    'Entry("beta", "tools", "Dedicated readers integrate with approvals, so lookups never interrupt.");',
    'new UsageEntry("s2", "tools", "claude-code", "work");',
    'AcrossRules.Reading(config, "tools", "forge");',
    'AcrossRules.Reading(DriverConfig.Empty.WithWorkspaceReadAcross("default", false), "tools", null);',
    'while (!File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;',
    '[InlineData("CLAUDE.md", "docs", "docs/README.md", true)]',
    'Path.Combine(Root(), "src", "Daoris.Web", "src", "App.tsx");',
  ].join('\n');
  assert.deepEqual(treeReads(source, files), [
    'tools/orient-index-fixtures/decision-notes.json', 'examples/plugins/probe.txt', '.claude/knowledge/adoption.md',
  ]);

  // What the scan above then asks of the lane table: without the digest's row, the twin's read reaches no service suite.
  const without = tool.REACH.filter((rule) => !rule.paths?.includes('tools/orient-index-fixtures/**'));
  const reached = (reach: readonly Rule[]) => tool.selectGates(repoPlan, ['tools/orient-index-fixtures/decision-notes.json'], { lanes: repoLanes, reach, gated: true })
    .gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
  assert.equal(reached(without).includes('service'), false);
  assert.equal(reached(tool.REACH).includes('service'), true);
});

/**
 * MOD9b: the paths a .NET source reads under this repository's top-level folders but `src`, whose reads the scan above
 * matches by its own pattern. A read is a run of string literals whose joined path is tracked, as
 * `Path.Combine(Root(), "tools", "orient-index-fixtures", "decision-notes.json")`, so a workspace's name or a sentence
 * is none. Its base, the argument before the run, is a call or a member (`Root()`, `WorkspaceRoot.Folder`), as every
 * read of `src/` is: a bare name is a folder the test made, often in this repository's own layout (`room`, `_home`), and
 * a string is the start of a longer run. A folder read is a file in it. A top-level folder alone (`"tools"`) is a
 * workspace's name far more often than a read, and a top-level file (`daoris.json`, the marker the tests walk up to) is
 * not read this way; neither is taken. A run that is a local's whole initializer is no read itself (MOD9d): what is read
 * through the local is (`localReads`).
 */
function treeReads(text: string, files: readonly string[]): string[] {
  const tracked = new Set(files);
  const folders = folderSet(files);
  const { declarations } = localRoots(text, files);
  const reads: string[] = [];
  for (const match of text.matchAll(/,\s*"([^"\\\s]+)"((?:\s*,\s*"[^"\\]*")*)/g)) {
    if (declarations.some(([start, end]) => match.index >= start && match.index < end)) continue;
    const base = text.slice(0, match.index).trimEnd();
    if (base.endsWith('"') || BARE_NAME.test(base)) continue;
    const path = [match[1]!, ...[...match[2]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)].join('/');
    const top = path.split('/')[0]!;
    if (top === 'src' || !folders.has(top) || top === path) continue;
    if (tracked.has(path)) reads.push(path);
    else if (folders.has(path)) reads.push(`${path}/probe.txt`);
  }
  return reads;
}

test('the scan resolves a source root as its walk does, and reads under it what the file names there (MOD9c)', () => {
  const files = [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Hooks.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/ScanTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs',
    'src/Daoris.Service/Daoris.Service.Core/Asks.cs', 'src/Daoris.Service/Daoris.Service.Tests/GateTests.cs', 'daoris.json',
  ];
  const helper = (marker: string, returns = 'folder?.FullName ?? throw new InvalidOperationException("no source tree above the test")') => [
    '    private static string SourceRoot()',
    '    {',
    '        var folder = new DirectoryInfo(AppContext.BaseDirectory);',
    `        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "${marker}")))`,
    '        {',
    '            folder = folder.Parent;',
    '        }',
    '',
    `        return ${returns};`,
    '    }',
  ].join('\n');
  const scanner = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/ScanTests.cs';

  // The project list a scan hands to the root, a read through the helper, and one through the file's own wrapper of it.
  const driver = [
    '    private static readonly string[] Projects =',
    '        ["Daoris.Desktop.Driver", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];',
    '    private static readonly string[] Occasions = ["TidyCarriedAsync", "CleanPlanAsync"];',
    '        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories)) { }',
    '        var loop = Directory.EnumerateFiles(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver"), "Driver*.cs")',
    '            .Append(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "DriverLoop.cs"));',
    '    private static string Source(string project, string file) => File.ReadAllText(Path.Combine(SourceRoot(), project, file));',
    '        Assert.DoesNotContain("AskStatesAsync", Source("Daoris.Desktop.Driver", "Hooks.cs"));',
    // Names that are no path under the root are not reads, and nor is the folder the walk looks for.
    '        Assert.Equal([("LandingPlugins.cs", "AskOneAsync")], speakers);',
    helper('Daoris.Desktop.Driver'),
  ].join('\n');
  // Every search pattern names C# sources, so a folder is read for its C# sources.
  assert.deepEqual(sourceRootReads(scanner, driver, files), [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/probe.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/probe.cs',
    'src/Daoris.Desktop/Daoris.Desktop.App/probe.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Hooks.cs',
  ]);
  // A file that enumerates anything else reads a folder for any file in it.
  const json = ['        Directory.GetFiles(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver"), "*.json");', helper('Daoris.Desktop.Driver')].join('\n');
  assert.deepEqual(sourceRootReads(scanner, json, files), ['src/Daoris.Desktop/Daoris.Desktop.Driver/probe.txt']);

  // A helper that returns a path under the folder it found is rooted there.
  const service = [
    '        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)) { }',
    '        File.ReadAllText(Path.Combine(SourceRoot(), "Asks.cs"));',
    helper('Daoris.Service.Core', 'folder is null ? throw new InvalidOperationException("none") : Path.Combine(folder.FullName, "Daoris.Service.Core")'),
  ].join('\n');
  assert.deepEqual(sourceRootReads('src/Daoris.Service/Daoris.Service.Tests/GateTests.cs', service, files), ['src/Daoris.Service/Daoris.Service.Core/Asks.cs']);

  // A walk to the repository's own marker is the repository, whose reads the scan takes by its other patterns.
  const repository = [
    '        File.ReadAllText(Path.Combine(SourceRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App", "Program.cs"));',
    helper('daoris.json'),
  ].join('\n');
  assert.deepEqual(sourceRootReads(scanner, repository, files), []);

  // What the scan above then asks of the lane table: without MOD9c's row, the modules' and the app's sources reach no driver suite.
  const without = tool.REACH.filter((rule) => !rule.paths?.includes('src/Daoris.Desktop/Daoris.Desktop.Modules/**/*.cs'));
  const reached = (path: string, reach: readonly Rule[]) => tool.selectGates(repoPlan, [path], { lanes: repoLanes, reach, gated: true })
    .gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
  for (const read of ['src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs', 'src/Daoris.Desktop/Daoris.Desktop.App/probe.cs']) {
    assert.equal(reached(read, without).includes('driver'), false, read);
    assert.equal(reached(read, tool.REACH).includes('driver'), true, read);
  }
});

/**
 * MOD9c: the paths a .NET source reads from a source root, a folder above the test other than the repository, which the
 * `src/` pattern and `treeReads` cannot see: `Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "DriverLoop.cs")`. A
 * source-root helper is a method of the file with no parameters that walks up from the test binary until a named folder
 * is there (`!Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver"))`) and returns the folder it
 * stopped at, or a path under it (`Path.Combine(folder.FullName, "Daoris.Service.Core")`). It resolves as the walk does:
 * to the nearest folder above the test that holds the named one, with what the return adds. A walk to the repository's
 * marker finds the repository, which is no source root.
 *
 * Under each root, a run of string literals whose joined path is tracked is a read of it, through the helper or through
 * the file's own wrapper of it (`Source("Daoris.Desktop.Driver", "Hooks.cs")`). Of a run that joins to nothing tracked,
 * each literal naming a folder directly under the root is a read of that folder: the project list a scan hands to the
 * root (`Projects`, then `Path.Combine(root, project)`). A folder is read for its C# sources (`probe.cs`) when every
 * search pattern the file enumerates names C# sources, and for any file in it otherwise. The helper's own body is no
 * read: the folder it names is the marker its walk looks for.
 */
function sourceRootReads(file: string, text: string, files: readonly string[]): string[] {
  const tracked = new Set(files);
  const folders = folderSet(files);
  const roots: string[] = [];
  let rest = text;
  for (const helper of text.matchAll(/^([ \t]*)(?:(?:private|internal|public|protected)\s+)?static\s+string\s+\w+\(\)\s*\r?\n\1\{([\s\S]*?)\r?\n\1\}/gm)) {
    const body = helper[2]!;
    const marker = /Exists\(Path\.Combine\(\w+\.FullName,\s*"([^"]+)"\)\)/.exec(body)?.[1];
    if (!marker) continue;
    let at = posix.dirname(file);
    while (at !== '.' && !tracked.has(`${at}/${marker}`) && !folders.has(`${at}/${marker}`)) at = posix.dirname(at);
    if (at === '.') continue;
    const added = /\breturn\b[\s\S]*?Path\.Combine\(\w+\.FullName((?:\s*,\s*"[^"]*")+)\)/.exec(body)?.[1] ?? '';
    roots.push([at, ...[...added.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)].join('/'));
    rest = rest.replace(helper[0], '');
  }
  const probe = probeFor(rest);
  const reads = new Set<string>();
  for (const root of roots) {
    for (const match of rest.matchAll(/"([^"\\\r\n]*)"((?:\s*,\s*"[^"\\\r\n]*")*)/g)) {
      const run = [match[1]!, ...[...match[2]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)];
      const path = `${root}/${run.join('/')}`;
      if (tracked.has(path)) reads.add(path);
      else if (folders.has(path)) reads.add(`${path}/${probe}`);
      else for (const name of run) if (name && !name.includes('/') && folders.has(`${root}/${name}`)) reads.add(`${root}/${name}/${probe}`);
    }
  }
  return [...reads];
}

test('the scan reads through a local holding a repository folder, and never through a folder the test made (MOD9d)', () => {
  const files = [
    'src/Daoris.Desktop/Directory.Packages.props', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs', 'src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpGoProposals.cs', 'src/Daoris.Web/src/bridge/call.ts',
    'src/Daoris.Service/Daoris.Service.Http/Program.cs', 'examples/plugins/github-pull-request/plugin.json', 'daoris.json',
  ];
  const desktopTree = 'src/Daoris.Desktop/probe.txt';

  // The project list a scan hands to a local holding the desktop tree. The tree spans lanes, so it is no read itself, and
  // neither is a path named relative to it, nor a mention in a comment.
  const projects = [
    '    private static readonly string[] Projects = ["Daoris.Desktop.Modules", "Daoris.Desktop.Launcher"];',
    '        var root = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");',
    '        foreach (var project in Projects)',
    '            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))',
    '                missing.Add(Path.GetRelativePath(root, file)); // relative to the root, as Assert.Empty(root) would say',
  ].join('\n');
  assert.deepEqual(srcReads(projects, files), []);
  assert.deepEqual(localReads(projects, files), ['src/Daoris.Desktop/Daoris.Desktop.Modules/probe.cs', 'src/Daoris.Desktop/Daoris.Desktop.Launcher/probe.cs']);

  // Files named through a local and through a narrower local on it; a check that a retired project is gone names nothing.
  const named = [
    '        var desktop = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");',
    '        var app = Path.Combine(desktop, "Daoris.Desktop.App");',
    '        var program = File.ReadAllText(Path.Combine(app, "Program.cs"));',
    '        Assert.False(File.Exists(Path.Combine(desktop, "Daoris.Desktop.Browser", "Daoris.Desktop.Browser.csproj")));',
    '        Assert.DoesNotContain("CefSharp", File.ReadAllText(Path.Combine(desktop, "Directory.Packages.props")));',
  ].join('\n');
  assert.deepEqual(srcReads(named, files), []);
  assert.deepEqual(localReads(named, files), ['src/Daoris.Desktop/Directory.Packages.props', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs']);

  // An expression-bodied helper, an interpolated name and a local handed on whole: a folder read for a file the scan cannot name.
  const whole = [
    '    private static string Page => Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Web", "src");',
    '        foreach (var file in Directory.GetFiles(Path.Combine(Page, "bridge"), "*.ts")) { }',
    '        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Help", "Proposals");',
    '        Assert.Matches("IHelpProposalKind", File.ReadAllText(Path.Combine(folder, $"{name}.cs")));',
    '        var host = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Http");',
    '        Assert.True(Directory.Exists(host));',
    '        var examples = Path.Combine(Repository(), "examples", "plugins");',
    '        PluginOffers.Needs(Path.Combine(examples, id));',
    '        PluginOffers.Needs(Path.Combine(examples, "github-pull-request"));',
  ].join('\n');
  assert.deepEqual(srcReads(whole, files), []);
  assert.deepEqual(treeReads(whole, files), []);
  assert.deepEqual(localReads(whole, files), [
    'src/Daoris.Web/src/bridge/probe.txt', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/probe.txt',
    'src/Daoris.Service/Daoris.Service.Http/probe.txt', 'examples/plugins/github-pull-request/probe.txt',
  ]);

  // A folder the test made: a scratch workspace on a field the test set, in this repository's own layout. No read at all.
  const scratch = [
    '    private readonly string _root = Path.Combine(',
    '        Path.GetTempPath(), "daoris-locator-" + Guid.NewGuid().ToString("N")[..8]);',
    '        var workspace = Path.Combine(_root, "workspace");',
    '        var deep = Path.Combine(workspace, "src", "Daoris.Desktop", "app", "bin");',
    '        var project = Path.Combine(workspace, "src", "Daoris.Service", "Daoris.Service.Http");',
    '        File.WriteAllText(Path.Combine(workspace, "daoris.json"), "{}");',
  ].join('\n');
  assert.deepEqual([...srcReads(scratch, files), ...treeReads(scratch, files), ...localReads(scratch, files)], []);
  // What was read before is the repository's own root, from a call or a member, as every other read of `src/` is.
  assert.deepEqual(srcReads('File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App", "Program.cs"));', files),
    ['src/Daoris.Desktop/Daoris.Desktop.App/Program.cs']);

  // What the scan then asks of the lane table: the desktop tree no rule places, and what is read through it is placed.
  const unplaced = (path: string) => tool.selectGates(repoPlan, [path], { lanes: repoLanes }).unplaced;
  assert.deepEqual(unplaced(desktopTree), [desktopTree]);
  for (const read of [...localReads(projects, files), ...localReads(named, files)]) {
    assert.deepEqual(unplaced(read), [], read);
    assert.ok(runs([read]).includes('modules'), read);
  }
});

/**
 * MOD9 and MOD9d: the paths a .NET source reads under `src/Daoris.…`, a run of string literals from `"src"`. Its base, the
 * argument before the run, is a call or a member (`RepositoryRoot()`, `WorkspaceRoot.Folder`), as `treeReads` takes it: a
 * bare name is a folder the test made (`Path.Combine(workspace, "src", "Daoris.Desktop", "app", "bin")`, a scratch workspace
 * on a field the test set) or a local holding a repository folder, whose reads `localReads` takes; and a string is the middle
 * of a longer run. A run that is a local's whole initializer is no read itself, since what is read through the local is. A
 * run that names no file is a folder read, a file in it.
 */
function srcReads(text: string, files: readonly string[]): string[] {
  const { declarations } = localRoots(text, files);
  const reads: string[] = [];
  for (const match of text.matchAll(/"src",\s*"(Daoris\.[A-Za-z.]+)"((?:,\s*"[^"]*")*)/g)) {
    if (declarations.some(([start, end]) => match.index >= start && match.index < end)) continue;
    const base = text.slice(0, match.index).trimEnd().replace(/,$/, '').trimEnd();
    if (base.endsWith('"') || BARE_NAME.test(base)) continue;
    const segments = ['src', match[1]!, ...[...match[2]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)];
    // A tracked file is read as itself whatever its extension (PROCPAR1's `process.runsettings`), never as a folder.
    if (/\.(cs|ts|tsx|mjs|js|json|css|md|props|txt)$/.test(segments.at(-1)!) || files.includes(segments.join('/'))) {
      reads.push(segments.join('/'));
      continue;
    }
    // STRUCT5d: a folder searched for one name at any depth (`Directory.EnumerateFiles(Path.Combine(…, "src"), "documents.ts",
    // SearchOption.AllDirectories)`) reads the file of that name wherever it lies under it, and nothing else of the folder.
    const folder = segments.join('/');
    const named = /^\s*\)\s*,\s*"([^"*?\\/]+)"\s*,\s*SearchOption\.AllDirectories\b/.exec(text.slice(match.index + match[0].length));
    const found = named ? files.filter((file) => file.startsWith(`${folder}/`) && file.split('/').at(-1) === named[1]) : [];
    reads.push(...(named ? (found.length ? found : [`${folder}/${named[1]}`]) : [`${folder}/probe.txt`]));
  }
  return reads;
}

test('a folder searched for one name at any depth reads that file wherever it lies, and nothing else of it (STRUCT5d)', () => {
  const files = ['src/Daoris.Cli/src/layout/documents.ts', 'src/Daoris.Cli/src/cli.ts'];
  const byName = 'var source = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(WorkspaceRoot(), "src", "Daoris.Cli", "src"), "documents.ts", SearchOption.AllDirectories).Single());';
  assert.deepEqual(srcReads(byName, files), ['src/Daoris.Cli/src/layout/documents.ts']);
  // A name found nowhere is still that name's read, and a pattern is no name: the folder is read whole, as before.
  assert.deepEqual(srcReads(byName.replace('"documents.ts"', '"gone.ts"'), files), ['src/Daoris.Cli/src/gone.ts']);
  assert.deepEqual(srcReads(byName.replace('"documents.ts"', '"*.ts"'), files), ['src/Daoris.Cli/src/probe.txt']);
  // What the scan then asks of the lane table: a change to the roles' file runs the service's suite (ORIENT2e2).
  assert.ok(runs(['src/Daoris.Cli/src/layout/documents.ts']).includes('service'));
});

/** A base that is a name alone, a local, a field or a parameter, rather than a call or a member: MOD9b's folder the test made. */
const BARE_NAME = /(?:^|[^\w.!?])@?[A-Za-z_]\w*$/;

/**
 * MOD9d: the locals, fields and expression-bodied helpers of a .NET source that hold a folder of this repository, each by
 * name, and where each is declared. One is declared as `Path.Combine` of a base and a run of string literals whose joined
 * path is a tracked folder below the top level: `var root = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop")`, or
 * `static string Page => Path.Combine(…)`. The base is the repository's root, a call or a member as for every other read
 * (never a temporary folder's), or another such local (`Path.Combine(desktop, "Daoris.Desktop.App")`). A local on anything
 * else (a field the test set to a scratch folder, a parameter) holds a folder the test made.
 */
function localRoots(text: string, files: readonly string[]): { roots: Map<string, string[]>; declarations: [number, number][] } {
  const folders = folderSet(files);
  const roots = new Map<string, string[]>();
  const declarations: [number, number][] = [];
  const found = [...text.matchAll(/\b(?:var|(?:static\s+)?(?:readonly\s+)?string)\s+(\w+)\s*(?:\(\s*\))?\s*=>?\s*Path\.Combine\(\s*([^,;"]+?)\s*((?:,\s*"[^"\\\r\n]*")+)\s*\)\s*;/g)];
  const settled = new Set<number>();
  for (let more = true; more;) {
    more = false;
    for (const [i, match] of found.entries()) {
      if (settled.has(i)) continue;
      const base = match[2]!;
      const under = /^@?[A-Za-z_]\w*$/.test(base) ? roots.get(base) : /(?:\)|\.\w+)$/.test(base) && !/temp/i.test(base) ? [''] : [];
      if (!under) continue;
      settled.add(i);
      const run = [...match[3]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!);
      const held = under.map((at) => [at, ...run].filter(Boolean).join('/')).filter((path) => path.includes('/') && folders.has(path));
      if (!held.length) continue;
      roots.set(match[1]!, [...(roots.get(match[1]!) ?? []), ...held]);
      declarations.push([match.index, match.index + match[0].length]);
      more = true;
    }
  }
  return { roots, declarations };
}

/**
 * MOD9d: the paths a .NET source reads through a local holding a repository folder (`localRoots`), which the `src/` pattern
 * read as the whole folder: `src/Daoris.Desktop`, which no rule places, since it holds every desktop lane. A use the code
 * makes, never one in a comment or a string, is read by its shape:
 * - `Path.Combine(local, "Program.cs")`: the run of string literals joined under the folder, when it names a tracked file
 *   or folder; a path that names nothing here (a check that a retired project is gone) is no read.
 * - `Path.Combine(local, project)` or `Path.Combine(local, $"{name}.cs")`: a name the scan cannot read. The folders directly
 *   under the local's that the file names are read, the project list a scan hands it (as MOD9c reads a source root's), and
 *   with none named, any file in the local's folder.
 * - the base of a narrower local, whose own uses are read, or `Path.GetRelativePath(local, …)`, which names a path and reads
 *   nothing: no read.
 * - any other use (handed to a read, an enumeration, a return or a helper): any file in the local's folder.
 * A folder is read for its C# sources when every search pattern the file enumerates names C# sources (MOD9c).
 */
function localReads(text: string, files: readonly string[]): string[] {
  const tracked = new Set(files);
  const folders = folderSet(files);
  const { roots, declarations } = localRoots(text, files);
  const probe = probeFor(text);
  // Strings blanked, then comments, keeping every offset: a name in a sentence or a comment is no use.
  const code = text.replace(/"(?:[^"\\\r\n]|\\.)*"/g, (literal) => `"${' '.repeat(literal.length - 2)}"`).replace(/\/\/[^\r\n]*/g, (comment) => ' '.repeat(comment.length));
  const literals = [...text.matchAll(/"([^"\\\r\n]*)"/g)].map((match) => match[1]!);
  const reads = new Set<string>();
  for (const [name, held] of roots) {
    for (const use of code.matchAll(new RegExp(`(?<![\\w.$@])${name}\\b(?:\\s*\\(\\s*\\))?`, 'g'))) {
      if (declarations.some(([start, end]) => use.index >= start && use.index < end)) continue;
      const before = code.slice(0, use.index);
      if (/Path\.GetRelativePath\(\s*$/.test(before)) continue;
      if (!/Path\.Combine\(\s*$/.test(before)) {
        for (const folder of held) reads.add(`${folder}/${probe}`);
        continue;
      }
      const args = /^\s*((?:,\s*"[^"\\\r\n]*")*)\s*(,)?/.exec(text.slice(use.index + use[0].length))!;
      const run = [...args[1]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!);
      for (const folder of held) {
        if (run.length) {
          const path = `${folder}/${run.join('/')}`;
          if (tracked.has(path)) reads.add(path);
          else if (folders.has(path)) reads.add(`${path}/${probe}`);
          continue;
        }
        const named = args[2] ? literals.filter((one) => one && !one.includes('/') && folders.has(`${folder}/${one}`)) : [];
        for (const one of named.length ? named : ['']) reads.add([folder, one, probe].filter(Boolean).join('/'));
      }
    }
  }
  return [...reads];
}

/** MOD9c: a folder a file reads is read for its C# sources when every search pattern the file enumerates names C# sources. */
function probeFor(text: string): string {
  const patterns = [...text.matchAll(/\b(?:Enumerate|Get)Files\s*\([^;]*?"([^"]*\*[^"]*)"/g)].map((match) => match[1]!);
  return patterns.length > 0 && patterns.every((pattern) => pattern.endsWith('.cs')) ? 'probe.cs' : 'probe.txt';
}

/**
 * Reads outside the page by the page's twins that no rule sends to the web gate, each named exactly with its reason, so a new
 * read is placed on purpose or owed here, and a row that places one fails this test until the row drops it. None is owed: the
 * six driver sources SESSDEL1c's scan found are placed by MOD9e's row (`PAGE_PARSED`).
 */
const OWED_PAGE_READS: string[] = [];

/**
 * SESSDEL1c: MOD9's incident from the page's side. The page's vitests run in the web gate alone, so a file outside the page a
 * vitest reads (a twin's table, a writer's declarations) is one whose change must reach the web gate, or a merge changing it
 * passes and the full set fails. The kept names' table sits among the driver's tests, whose rule reaches the driver's
 * suites alone.
 */
test("every file outside the page a page's vitest reads is one whose change reaches the web gate (SESSDEL1c)", () => {
  const files = trackedFiles();
  const reads = new Set<string>();
  for (const file of files.filter((path) => /^src\/Daoris\.Web\/src\/.+\.test\.tsx?$/.test(path))) {
    for (const read of pageReads(file, readFileSync(join(repoRoot, file), 'utf8'), files)) reads.add(read);
  }
  // WEBSPLIT1: the vitests run in the web gate's unit run.
  const unreached = [...reads].filter((read) => !runs([read]).includes('web-unit')).sort();
  assert.deepEqual(unreached, OWED_PAGE_READS, "each a file a vitest reads, and a merge changing it does not run the web gate's unit run");
  assert.ok(reads.has(KEPT_NAMES), "the scan does not see the page's twin read the kept names' table");
  for (const path of PAGE_PARSED) assert.ok(reads.has(path), `the scan does not see a page twin parse ${path}`);
  assert.ok(reads.size >= 8, `the scan found only ${reads.size} reads: its pattern no longer matches how the page's tests read the repository`);

  // MOD9e: what the scan asks of the lane table, with the row taken away: the six reach the driver's gates and no vitest.
  const without = tool.REACH.filter((rule) => !isPageParsedRule(rule));
  assert.equal(without.length, tool.REACH.length - 1, "MOD9e's rule names each source by its name under the driver's folder");
  for (const path of PAGE_PARSED) {
    const reached = (reach: readonly Rule[]) => tool.selectGates(repoPlan, [path], { lanes: repoLanes, reach, gated: true }).gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
    assert.deepEqual(reached(without), ['universal', 'code-map', 'orient-index', 'test-map', 'cli', 'driver', 'modules', 'rehearse-family'], path);
    assert.deepEqual(reached(tool.REACH), ['universal', 'code-map', 'orient-index', 'test-map', 'cli', 'driver', 'modules', 'rehearse-family', 'web-unit'], path);
  }
});

test("the page's scan reads through node:fs from the page, the repository or the test, and never a path a test only says (SESSDEL1c)", () => {
  const files = [
    'src/Daoris.Cli/test/fixtures/shell-words.json', KEPT_NAMES,
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Landing.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Trace.Chain.cs',
    'src/Daoris.Service/Daoris.Service.Core/NoteParts.cs', 'src/Daoris.Web/src/tokens.css', 'src/Daoris.Web/src/format.ts',
    'docs/code-map.json',
  ];
  const at = 'src/Daoris.Web/src/work/twin.test.ts';
  const reading = [
    "import { readFileSync } from 'node:fs';",
    "import { list } from '../format';",
    // From where vitest runs, the page; from the repository; one literal of a run that names a file; a helper's files by name.
    "const table = join(process.cwd(), '..', 'Daoris.Cli', 'test', 'fixtures', 'shell-words.json');",
    "const root = join(process.cwd(), '..', '..');",
    "const kept = readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'kept-names.json'), 'utf8');",
    "const ledger = declared('service', 'src/Daoris.Service/Daoris.Service.Core/NoteParts.cs');",
    "const driver = (file: string) => readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', file), 'utf8');",
    "const chain = driver('Trace.Chain.cs') + driver('Landing.cs');",
    // The page's own files are the web gate's already.
    "const css = readFileSync(join(process.cwd(), 'src', 'tokens.css'), 'utf8');",
  ].join('\n');
  assert.deepEqual(pageReads(at, reading, files), [
    'src/Daoris.Cli/test/fixtures/shell-words.json', KEPT_NAMES, 'src/Daoris.Service/Daoris.Service.Core/NoteParts.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Trace.Chain.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Landing.cs',
  ]);
  // A folder it names no file of is read for any file in it.
  assert.deepEqual(pageReads(at, "import { readdirSync } from 'node:fs';\nreaddirSync(join(root, 'src', 'Daoris.Cli', 'test', 'fixtures'));", files),
    ['src/Daoris.Cli/test/fixtures/probe.txt']);
  // A test that does not read the disk only says a path, as a map's data; an import out of the page is a read whatever it imports.
  assert.deepEqual(pageReads(at, "import { render } from '@testing-library/react';\nexpect(map.source).toBe('docs/code-map.json');", files), []);
  assert.deepEqual(pageReads(at, "import table from '../../../Daoris.Cli/test/fixtures/shell-words.json';", files),
    ['src/Daoris.Cli/test/fixtures/shell-words.json']);
  // STRUCT1: a twin that reads the driver by name reads each source it names, wherever the driver keeps it, and the helper
  // it imports from the page is the page's own.
  const moved = ['src/Daoris.Desktop/Daoris.Desktop.Driver/Sessions/Trace.Chain.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Landing.cs'];
  const byName = [
    "import { driverSource } from '../test/driverSource';",
    "const chain = driverSource('Trace.Chain.cs');",
    "const codes = codesOf('Landing.cs', 'SweepKind');",
    "expect(kind).toBe('closed');",
  ].join('\n');
  assert.deepEqual(pageReads(at, byName, moved), moved);

  // What the scan above then asks of the lane table: without SESSDEL1c's row, the kept names' table reaches no web gate.
  const without = tool.REACH.filter((rule) => !rule.paths?.includes(KEPT_NAMES));
  const reached = (reach: readonly Rule[]) => tool.selectGates(repoPlan, [KEPT_NAMES], { lanes: repoLanes, reach, gated: true })
    .gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
  assert.equal(reached(without).includes('web-unit'), false);
  assert.equal(reached(tool.REACH).includes('web-unit'), true);
});

/**
 * SESSDEL1c: the paths outside the page a page's vitest reads. A read from disk goes through `node:fs`, so a test that does not
 * import it reads nothing from disk: a path it says (`docs/code-map.json`, a map's data) is no read. In one that does, a read
 * is a run of string literals, joined, naming a tracked file or folder from where vitest runs, the page
 * (`join(process.cwd(), '..', 'Daoris.Cli', …)`), from the repository (`join(root, 'src', …)`) or from the test's own folder;
 * or, where the run names nothing, one literal of it that does (`declared('service', 'src/…/NoteParts.cs')`). A folder is read
 * for each file in it the test names (`driver('Trace.Chain.cs')`), else for any file in it. A relative import naming a file
 * outside the page is a read in any test. The page's own files are no read: the web gate runs with them.
 */
function pageReads(file: string, text: string, files: readonly string[]): string[] {
  const page = 'src/Daoris.Web';
  const tracked = new Set(files);
  const folders = new Set(files.flatMap((path) => path.split('/').slice(0, -1).map((_, i, parts) => parts.slice(0, i + 1).join('/'))));
  const outside = (path: string) => path.includes('/') && !path.startsWith('..') && path !== page && !path.startsWith(`${page}/`);
  const reads = new Set<string>();
  for (const match of text.matchAll(/(?:\bfrom\s+|\bimport\s*\(\s*)'(\.{1,2}\/[^'?]+)'/g)) {
    const path = posix.normalize(posix.join(posix.dirname(file), match[1]!));
    const found = ['', '.ts', '.tsx', '.mjs', '.js', '.json'].map((extension) => `${path}${extension}`).find((candidate) => tracked.has(candidate));
    if (found && outside(found)) reads.add(found);
  }
  // STRUCT1 (D166): a twin that reads the driver by name (`driverSource('Trace.Chain.cs')`, or a name it hands on to it) reads
  // each driver source it names, the one tracked file of that name at any depth of the driver's folder.
  if (/\bdriverSource\b/.test(text)) {
    const driver = 'src/Daoris.Desktop/Daoris.Desktop.Driver/';
    for (const match of text.matchAll(/'([^'\\\r\n/]+)'/g)) {
      const named = files.filter((path) => path.startsWith(driver) && posix.basename(path) === match[1]);
      if (named.length === 1) reads.add(named[0]!);
    }
  }
  if (!/\bfrom\s+'(?:node:)?fs(?:\/promises)?'/.test(text)) return [...reads];
  const resolve = (path: string) => [page, '.', posix.dirname(file)]
    .map((base) => posix.normalize(posix.join(base, path)))
    .find((candidate) => outside(candidate) && (tracked.has(candidate) || folders.has(candidate)));
  const literals = [...text.matchAll(/'([^'\\\r\n]*)'/g)].map((match) => match[1]!);
  for (const match of text.matchAll(/'([^'\\\r\n]*)'((?:\s*,\s*'[^'\\\r\n]*')*)/g)) {
    const run = [match[1]!, ...[...match[2]!.matchAll(/'([^']*)'/g)].map((part) => part[1]!)];
    const whole = resolve(run.join('/'));
    const found = whole ? [whole] : run.filter((one) => one.includes('/')).map(resolve).filter((path): path is string => path !== undefined);
    for (const path of found) {
      if (tracked.has(path)) {
        reads.add(path);
        continue;
      }
      const named = literals.filter((name) => name && !name.includes('/') && tracked.has(`${path}/${name}`));
      for (const name of named.length > 0 ? named : ['probe.txt']) reads.add(`${path}/${name}`);
    }
  }
  return [...reads];
}

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
  // PROC1c: the git-only classes' half is a Process half too, by its settings file's name: its trx and its alone-rerun hold.
  assert.equal(tool.isProcessGate({ name: 'driver-process-git', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/git.process.runsettings' }), true);
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

const planned = (name: string, run: string): Gate => ({ name, run, kind: 'suite' });

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
  // GATELANES2: three builds of one component, so the light lane runs them in turn and the third is not yet started.
  const gates = ['a', 'b', 'c'].map((name) => planned(name, `dotnet build src/S/${name}`));
  const run = async (_root: string, gate: Gate, at: string): Promise<GateResult> => {
    const failed = gate.name === 'b';
    return { gate, code: failed ? 3 : 0, log: join(at, `${gate.name}.log`), verdict: failed ? 'FAIL' : 'PASS', note: failed ? 'exit 3' : '', ms: 1000 };
  };

  for (const oneAtATime of [false, true]) {
    const printed: string[] = [];
    const results = await tool.runGates(root, gates, dir, { run, oneAtATime, print: (line) => printed.push(line) });
    assert.deepEqual(results.map((result) => result.gate.name), ['a', 'b']);
    assert.ok(printed.some((line) => /PASS\s+a\s+1 s\s+local\/scratch\/merge-x\/a\.log$/.test(line)), printed.join('\n'));
    assert.ok(printed.some((line) => /FAIL\s+b\s.*local\/scratch\/merge-x\/b\.log\s+\(exit 3\)/.test(line)), printed.join('\n'));
    assert.match(printed.at(-1)!, /NOT RUN\s+c/);
  }

  const all = await tool.runGates(root, gates, dir, { run, keepGoing: true, print: () => {} });
  assert.deepEqual(all.map((result) => result.gate.name), ['a', 'b', 'c']);
});

// ---------------------------------------------------------------------------------------------------
// GATELANES1: the light gates run beside the Process lane, which stays one gate at a time

const processRun = (project: string) => `dotnet test ${project} --settings src/Daoris.Desktop/process.runsettings`;

test("this repository's Process lane is the real-process halves and the rehearsals; every other gate is light, the web gate too", () => {
  assert.deepEqual(repoPlan.filter((gate) => tool.isProcessLane(gate)).map((gate) => gate.name),
    ['driver-process', 'driver-process-git', 'modules-process', 'rehearse', 'rehearse-family', 'deployment']);
  assert.deepEqual(repoPlan.filter((gate) => !tool.isProcessLane(gate)).map((gate) => gate.name),
    ['universal', 'code-map', 'orient-index', 'test-map', 'web-types', 'web-rules', 'cli', 'tool-tests', 'service', 'devkit', 'driver', 'modules', 'web', 'web-unit', 'web-measured']);
  // By what each runs, as the long gates are: a renamed gate, a new Process half or a new rehearsal is still one.
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'dotnet test x --settings src/Daoris.Desktop/git.process.runsettings' }), true);
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'npm run rehearse:anything' }), true);
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'dotnet test x --filter Category!=Process' }), false);
  assert.equal(tool.isProcessLane({ name: 'renamed', run: 'npm run test:web' }), false);
});

test("a .NET gate's component is the first two folders of the project its command names", () => {
  assert.equal(tool.dotnetComponent('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process'), 'src/Daoris.Desktop');
  assert.equal(tool.dotnetComponent(processRun('src/Daoris.Desktop/Daoris.Desktop.Modules.Tests')), 'src/Daoris.Desktop');
  assert.equal(tool.dotnetComponent('dotnet test src/Daoris.Service'), 'src/Daoris.Service');
  assert.equal(tool.dotnetComponent('node tools/as-merged.mjs dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- verify --universal-only'), 'src/Daoris.Devkit');
  assert.equal(tool.dotnetComponent('dotnet build "src\\Daoris.Service\\Daoris.Service.Http"'), 'src/Daoris.Service');
  assert.equal(tool.dotnetComponent('dotnet build-server shutdown'), null);
  assert.equal(tool.dotnetComponent('dotnet test'), null);
  assert.equal(tool.dotnetComponent('npm run test:web'), null);
});

// What makes a component the unit of the build rule: two gates of two components never build one project.
test('every project a .NET project references is in its own component', () => {
  const projects = trackedFiles().filter((path) => path.startsWith('src/') && path.endsWith('.csproj'));
  assert.ok(projects.length >= 10, 'the .NET projects left src/');
  for (const project of projects) {
    const text = readFileSync(join(repoRoot, project), 'utf8');
    for (const [, include] of text.matchAll(/<ProjectReference\s+Include="([^"]+)"/g)) {
      const referenced = posix.normalize(posix.join(posix.dirname(project), include!.replace(/\\/g, '/')));
      assert.equal(tool.dotnetComponent(`dotnet build ${referenced}`), tool.dotnetComponent(`dotnet build ${project}`), `${project} references ${referenced}`);
    }
  }
});

// The schedule reads a gate's command; a root script that builds behind its name is read here, one level deep.
test("what a light gate's script builds behind its name is never a component the Process lane builds", () => {
  const scripts = (JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
  const built = new Set(repoPlan.filter((gate) => tool.isProcessLane(gate)).map((gate) => tool.dotnetComponent(gate.run)).filter(Boolean));
  assert.deepEqual([...built], ['src/Daoris.Desktop']);
  for (const gate of repoPlan.filter((planned) => !tool.isProcessLane(planned))) {
    const script = /^npm run (\S+)$/.exec(gate.run)?.[1];
    if (!script) continue;
    assert.ok(scripts[script], `${gate.name} runs a script the workspace declares`);
    for (const command of scripts[script]!.split('&&')) {
      const component = tool.dotnetComponent(command.trim());
      assert.ok(component === null || !built.has(component), `${gate.name} builds ${component}, which a Process-lane gate builds too`);
    }
  }
});

test("this repository's light lane runs the desktop suites' fast halves first, and the Process lane waits for what it shares", () => {
  const schedule = tool.laneSchedule(repoPlan);
  assert.deepEqual(schedule.light.map((gate) => gate.name),
    ['driver', 'modules', 'universal', 'code-map', 'orient-index', 'test-map', 'web-types', 'web-rules', 'cli', 'tool-tests', 'service', 'devkit', 'web', 'web-unit', 'web-measured']);
  // REHLANE1, REHLANE2, REHLANE2b: the release, family and deployment rehearsals right after the git half, which they run
  // beside, one after the other.
  assert.deepEqual(schedule.process.map((gate) => gate.name),
    ['driver-process', 'driver-process-git', 'rehearse', 'rehearse-family', 'deployment', 'modules-process']);
  for (const name of ['driver-process', 'driver-process-git', 'modules-process']) {
    // GATEORDER1: and for the heavy light gates (the CLI, service, devkit and web suites), so no Process half runs under their load.
    assert.deepEqual(schedule.waits[name], { after: ['driver', 'modules', 'cli', 'service', 'devkit', 'web', 'web-unit', 'web-measured'], component: 'src/Daoris.Desktop' }, name);
  }
  assert.deepEqual(repoPlan.filter((gate) => tool.isHeavyGate(gate)).map((gate) => gate.name), ['cli', 'service', 'devkit', 'driver', 'modules', 'web', 'web-unit', 'web-measured']);
  assert.equal(tool.isHeavyGate({ name: 'x', run: 'node tools/orient-index.mjs --check' }), false);
  // A rehearsal builds what its command does not name. REHLANE2: and the release and deployment rehearsals' packs stage the
  // CLI's dist/, which the bin the cli gate's `daoris check` runs prefers.
  for (const name of ['rehearse', 'rehearse-family', 'deployment']) {
    assert.deepEqual(schedule.waits[name], { after: schedule.light.map((gate) => gate.name), component: null }, name);
  }
});

test('a CI run schedules by memory: a Process half waits only for the gates that build its component, and the heavy light gates run beside it (CI1)', () => {
  const schedule = tool.laneSchedule(repoPlan, { byMemory: true });
  for (const name of ['driver-process', 'driver-process-git', 'modules-process']) {
    assert.deepEqual(schedule.waits[name], { after: ['driver', 'modules'], component: 'src/Daoris.Desktop' }, name);
  }
  // The rehearsals still wait for the whole light lane: what they build and stage is state, not load.
  for (const name of ['rehearse', 'rehearse-family', 'deployment']) {
    assert.deepEqual(schedule.waits[name]!.after, schedule.light.map((gate) => gate.name), name);
  }
  // The rest is the merge's and the full set's schedule: the lanes, the pairs beside the git half, the builds reused.
  const plain = tool.laneSchedule(repoPlan);
  assert.deepEqual([schedule.light, schedule.process, schedule.beside, schedule.reuses, schedule.width], [plain.light, plain.process, plain.beside, plain.reuses, plain.width]);
});

/** A promise resolved from outside. */
function signal(): { promise: Promise<void>; resolve: () => void } {
  let resolve!: () => void;
  const promise = new Promise<void>((done) => { resolve = done; });
  return { promise, resolve };
}

/** What a fake gate waits for, failing the test rather than hanging it when that never comes: two gates run in turn never overlap. */
async function within(promise: Promise<unknown>, what: string, ms = 5000): Promise<void> {
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
 * happened, and fails when `failing` names it. What each gate was handed beside it is kept.
 */
function laneRun(names: string[]) {
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
function summary(printed: string[]): string[] {
  const at = printed.findIndex((line) => /in the plan's order/.test(line));
  assert.ok(at >= 0, `no summary in:\n${printed.join('\n')}`);
  return printed.slice(at + 1).map((line) => /^\s+(PASS|FAIL|FLAKE|MEMORY|NOT RUN)\s+(\S+)/.exec(line)!.slice(1).join(' '));
}

// a and b build one component, so the light lane runs them in turn (GATELANES2), as both lanes' tests below need.
const laneGates = (): Gate[] => [
  planned('a', 'dotnet build src/L/A'), planned('b', 'dotnet build src/L/B'), planned('p', processRun('fake/P.Tests')), planned('q', processRun('fake/Q.Tests')),
];

test("the light lane runs beside the Process lane: both at once, each verdict its own, the summary in the plan's order", async () => {
  const lanes = laneRun(['a', 'b', 'p', 'q']);
  // a ends only once p has started, and p only once b has: run in turn, neither would.
  lanes.until['a'] = () => lanes.started['p']!.promise;
  lanes.until['p'] = () => lanes.started['b']!.promise;
  const printed: string[] = [];
  const results = await tool.runGates('root', laneGates(), join('root', 'local', 'scratch', 'merge-x'), { run: lanes.run, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => result.gate.name), ['a', 'b', 'p', 'q'], "the results in the plan's order");
  assert.ok(lanes.events.indexOf('start p') < lanes.events.indexOf('end a'), `p started while a ran: ${lanes.events.join(', ')}`);
  assert.ok(lanes.events.indexOf('start b') < lanes.events.indexOf('end p'), `b started while p ran: ${lanes.events.join(', ')}`);
  assert.ok(lanes.events.indexOf('start q') > lanes.events.indexOf('end p'), 'the Process lane runs one gate at a time');
  assert.match(printed.join('\n'), /light\s+a, b\n.*Process\s+p, q/);
  assert.match(printed.join('\n'), /--one-at-a-time/);
  assert.deepEqual(summary(printed), ['PASS a', 'PASS b', 'PASS p', 'PASS q']);
  assert.equal(lanes.heard['a']!.settled, undefined, 'a light gate waits for nothing beside it');
  assert.equal(typeof lanes.heard['p']!.settled, 'function', "a Process gate is handed the light lane's end, for its re-run alone");
});

test('scheduled by memory, a Process gate starts beside a heavy light gate once the gate that builds its component has ended (CI1)', async () => {
  // a builds p's component; h is a heavy light gate of none, which a merge's Process gate waits for (GATEORDER1).
  const gates = [planned('a', 'dotnet build fake/P.Tests'), planned('h', 'npm run verify'), planned('p', processRun('fake/P.Tests'))];
  const lanes = laneRun(['a', 'h', 'p']);
  lanes.until['h'] = () => lanes.started['p']!.promise;
  const printed: string[] = [];
  await tool.runGates('root', gates, 'logs', { run: lanes.run, print: (line) => printed.push(line), byMemory: true });
  assert.ok(lanes.events.indexOf('start p') > lanes.events.indexOf('end a'), `p waits for the gate that builds its component: ${lanes.events.join(', ')}`);
  assert.ok(lanes.events.indexOf('start p') < lanes.events.indexOf('end h'), `p runs beside the heavy light gate: ${lanes.events.join(', ')}`);
  assert.match(printed.join('\n'), /p starts once a has ended: a builds fake\/P\.Tests first/);
  assert.match(printed.join('\n'), /the heavy light gates run beside the Process lane, each once its free memory allows \(CI1, MEMGATE1\)/);
});

test('a failure in either lane starts nothing more in either; the gate running beside it ends and keeps its verdict', async () => {
  // The light lane fails while p runs: p ends and keeps its pass, and neither b nor q starts.
  let lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('a');
  const light = lanes;
  lanes.until['a'] = () => light.started['p']!.promise;
  lanes.until['p'] = () => light.ended['a']!.promise;
  let printed: string[] = [];
  let results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => `${result.verdict} ${result.gate.name}`), ['FAIL a', 'PASS p']);
  assert.deepEqual(summary(printed), ['FAIL a', 'NOT RUN b', 'PASS p', 'NOT RUN q']);

  // The Process lane fails while a runs: a ends and keeps its pass, and neither b nor q starts.
  lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('p');
  const first = lanes;
  lanes.until['a'] = () => first.ended['p']!.promise;
  printed = [];
  results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => `${result.verdict} ${result.gate.name}`), ['PASS a', 'FAIL p']);
  assert.deepEqual(summary(printed), ['PASS a', 'NOT RUN b', 'FAIL p', 'NOT RUN q']);

  // --keep-going: a failure in each lane, and every gate still runs.
  lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('a');
  lanes.failing.add('p');
  printed = [];
  results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, keepGoing: true, print: (line) => printed.push(line) });
  assert.deepEqual(summary(printed), ['FAIL a', 'PASS b', 'FAIL p', 'PASS q']);
  assert.equal(results.length, 4);
});

test("a Process gate starts once the light gates of its component have ended, which run first; a rehearsal once the light lane has", async () => {
  const gates = [
    planned('u', 'node u.mjs'),
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'),
    planned('m', 'dotnet test src/X/M.Tests --filter Category!=Process'),
    planned('h', 'npm run test:web'),
    planned('dp', processRun('src/X/X.Tests')),
    planned('r', 'npm run rehearse:family'),
  ];
  const schedule = tool.laneSchedule(gates);
  assert.deepEqual(schedule.light.map((gate) => gate.name), ['d', 'm', 'u', 'h']);
  // GATEORDER1: the heavy light gate h is waited for too; the cheap one, u, is not.
  assert.deepEqual(schedule.waits['dp'], { after: ['d', 'm', 'h'], component: 'src/X' });
  assert.deepEqual(schedule.waits['r'], { after: ['d', 'm', 'u', 'h'], component: null });

  const lanes = laneRun(['u', 'd', 'm', 'h', 'dp', 'r']);
  // dp waits, as its re-run alone would, for the light lane's end that it is handed.
  lanes.until['dp'] = async () => {
    await lanes.heard['dp']!.settled!();
    lanes.events.push('settled');
  };
  const printed: string[] = [];
  await tool.runGates('root', gates, 'logs', { run: lanes.run, print: (line) => printed.push(line) });
  const at = (event: string) => lanes.events.indexOf(event);
  assert.equal(lanes.events[0], 'start d', 'the light gates the Process lane waits for go first');
  assert.ok(at('start dp') > at('end m'), `dp starts after the light gates of src/X: ${lanes.events.join(', ')}`);
  assert.ok(at('start dp') > at('end h'), `dp starts after the heavy light gate: ${lanes.events.join(', ')}`);
  assert.ok(at('settled') > at('end u'), 'the light lane ended before what dp was handed resolved');
  assert.ok(at('start r') > at('end u'), 'the rehearsal starts once the light lane has ended');
  assert.match(printed.join('\n'), /dp starts once d, m, h have ended: d, m build src\/X first, h loads the machine/);
  assert.match(printed.join('\n'), /r starts once the light lane has ended/);
  assert.deepEqual(summary(printed), ['PASS u', 'PASS d', 'PASS m', 'PASS h', 'PASS dp', 'PASS r']);
});

test('--one-at-a-time runs every gate in turn, in the plan\'s order, as before the lanes', async () => {
  const lanes = laneRun(['a', 'b', 'p', 'q']);
  lanes.failing.add('b');
  const printed: string[] = [];
  const results = await tool.runGates('root', laneGates(), 'logs', { run: lanes.run, oneAtATime: true, print: (line) => printed.push(line) });
  assert.deepEqual(lanes.events, ['start a', 'end a', 'start b', 'end b']);
  assert.deepEqual(results.map((result) => result.gate.name), ['a', 'b']);
  assert.match(printed[2]!, /NOT RUN\s+p/);
  assert.match(printed[3]!, /NOT RUN\s+q/);
  assert.equal(printed.length, 4, 'no lanes and no summary: the lines are already in order');
  assert.equal(lanes.heard['a']!.settled, undefined);
});

// ---------------------------------------------------------------------------------------------------
// GATELANES2: the light lane's gates of different components side by side; GATEBUILD1: a Process half reuses its fast half's build

test("a gate's components are what its command builds, and what the root scripts it names build", () => {
  const scripts = {
    verify: 'npm test -w x && node tools/as-merged.mjs dotnet run --project src/K/K.Cli -- verify',
    'test:web': 'npm --prefix w run build && dotnet build src/S/S.Http && npm --prefix w run e2e',
    lint: 'node lint.mjs',
    all: 'npm run lint && npm run -s verify',
    loop: 'npm run loop',
  };
  assert.deepEqual(tool.gateComponents({ name: 'cli', run: 'npm run verify' }, scripts), ['src/K']);
  assert.deepEqual(tool.gateComponents({ name: 'web', run: 'npm run test:web' }, scripts), ['src/S']);
  assert.deepEqual(tool.gateComponents({ name: 'service', run: 'dotnet test src/S' }, scripts), ['src/S']);
  assert.deepEqual(tool.gateComponents({ name: 'universal', run: 'node tools/as-merged.mjs dotnet run --project src/K/K.Cli -- verify' }, scripts), ['src/K']);
  assert.deepEqual(tool.gateComponents({ name: 'all', run: 'npm run all' }, scripts), ['src/K'], 'a script that names another is read through');
  assert.deepEqual(tool.gateComponents({ name: 'loop', run: 'npm run loop' }, scripts), [], 'a script that names itself is read once');
  assert.deepEqual(tool.gateComponents({ name: 'lint', run: 'npm run lint' }, scripts), []);
  assert.deepEqual(tool.gateComponents({ name: 'index', run: 'node tools/orient-index.mjs --check' }, scripts), []);
  assert.deepEqual(tool.gateComponents({ name: 'gone', run: 'npm run gone' }, scripts), [], 'npm refuses a script the workspace does not declare');
});

test("this repository's light gates run three at once, never two that build one component, the CLI's verify and web read behind their names", () => {
  const scripts = (JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
  const schedule = tool.laneSchedule(repoPlan, { scripts });
  assert.equal(tool.LIGHT_WIDTH, 3);
  assert.equal(schedule.width, tool.LIGHT_WIDTH);
  const building = (component: string) => schedule.light.filter((gate) => schedule.components[gate.name]!.includes(component)).map((gate) => gate.name);
  assert.deepEqual(building('src/Daoris.Desktop'), ['driver', 'modules']);
  // The CLI's verify ends in the devkit's universal gates, and the web gate builds the HTTP host its own build writes into.
  assert.deepEqual(building('src/Daoris.Devkit'), ['universal', 'code-map', 'cli', 'devkit']);
  assert.deepEqual(building('src/Daoris.Service'), ['service', 'web']);
  assert.deepEqual(schedule.components['orient-index'], []);
  // WEBSPLIT1: the web gate's unit run builds none, so it runs beside any.
  assert.deepEqual(schedule.components['web-unit'], []);
  // FLOWCHART1e: nor does the measured-box check, which serves the page's source with Vite.
  assert.deepEqual(schedule.components['web-measured'], []);
  // GATELANES1's light order and GATEORDER1's waits hold.
  assert.deepEqual(schedule.light.map((gate) => gate.name),
    ['driver', 'modules', 'universal', 'code-map', 'orient-index', 'test-map', 'web-types', 'web-rules', 'cli', 'tool-tests', 'service', 'devkit', 'web', 'web-unit', 'web-measured']);
  assert.deepEqual(schedule.waits['driver-process']!.after, ['driver', 'modules', 'cli', 'service', 'devkit', 'web', 'web-unit', 'web-measured']);
  // GATEBUILD1: each Process half reuses the build of its own test project's fast half.
  assert.deepEqual(schedule.reuses, { 'driver-process': 'driver', 'driver-process-git': 'driver', 'modules-process': 'modules' });
});

test("light gates of different components run at once, up to the width, two of one component never; the summary keeps the plan's order", async () => {
  const gates = [
    planned('a1', 'dotnet test src/A/A1.Tests'), planned('a2', 'dotnet test src/A/A2.Tests'),
    planned('b', 'dotnet test src/B/B.Tests'), planned('c', 'node c.mjs'), planned('d', 'node d.mjs'),
  ];
  const counted = (lanes: ReturnType<typeof laneRun>) => {
    const seen = { now: 0, most: 0 };
    const run = async (root: string, gate: Gate, at: string, beside?: Beside) => {
      seen.now += 1;
      seen.most = Math.max(seen.most, seen.now);
      try {
        return await lanes.run(root, gate, at, beside);
      } finally {
        seen.now -= 1;
      }
    };
    return { seen, run };
  };
  const lanes = laneRun(['a1', 'a2', 'b', 'c', 'd']);
  // a1 ends only once b has ended with c beside it, b once c has started: one gate at a time, none of them would.
  lanes.until['a1'] = () => Promise.all([lanes.ended['b']!.promise, lanes.started['c']!.promise]);
  lanes.until['b'] = () => lanes.started['c']!.promise;
  lanes.until['c'] = () => lanes.ended['a1']!.promise;
  const printed: string[] = [];
  const { seen, run } = counted(lanes);
  const results = await tool.runGates('root', gates, 'logs', { run, scripts: {}, print: (line) => printed.push(line) });
  const at = (event: string) => lanes.events.indexOf(event);
  assert.equal(seen.most, 3, `three at once, and never more: ${lanes.events.join(', ')}`);
  assert.ok(at('start a2') > at('end a1'), `a1 and a2 build src/A, so never at once: ${lanes.events.join(', ')}`);
  assert.ok(at('start d') > at('end b'), `d waited for a place: ${lanes.events.join(', ')}`);
  assert.match(printed[0]!, /the light lane, up to 3 gates at once \(--one-at-a-time runs every gate in turn/);
  assert.match(printed.join('\n'), /light\s+a1, a2, b, c, d\n/);
  assert.match(printed.join('\n'), /a1, a2 build src\/A: one at a time/);
  assert.match(printed.find((line) => /^\s+(PASS|FAIL)/.test(line))!, /PASS\s+b\s/, 'each line is printed as its gate ends');
  assert.deepEqual(summary(printed), ['PASS a1', 'PASS a2', 'PASS b', 'PASS c', 'PASS d']);
  assert.deepEqual(results.map((result) => result.gate.name), ['a1', 'a2', 'b', 'c', 'd'], "the results in the plan's order");

  // A narrower lane holds to its width.
  const narrow = laneRun(['a1', 'a2', 'b', 'c', 'd']);
  for (const name of ['a1', 'a2', 'b', 'c', 'd']) narrow.until[name] = () => new Promise((done) => setTimeout(done, 5));
  const two = counted(narrow);
  await tool.runGates('root', gates, 'logs', { run: two.run, width: 2, scripts: {}, print: () => {} });
  assert.equal(two.seen.most, 2, narrow.events.join(', '));
});

test('a failure starts no more light gates; the gates running beside it end and keep their verdicts', async () => {
  const gates = [planned('x1', 'dotnet test src/X/X1.Tests'), planned('y', 'dotnet test src/Y/Y.Tests'), planned('x2', 'dotnet test src/X/X2.Tests')];
  const lanes = laneRun(['x1', 'y', 'x2']);
  lanes.failing.add('x1');
  lanes.until['x1'] = () => lanes.started['y']!.promise;
  lanes.until['y'] = () => lanes.ended['x1']!.promise;
  const printed: string[] = [];
  const results = await tool.runGates('root', gates, 'logs', { run: lanes.run, scripts: {}, print: (line) => printed.push(line) });
  assert.deepEqual(results.map((result) => `${result.verdict} ${result.gate.name}`), ['FAIL x1', 'PASS y']);
  assert.deepEqual(summary(printed), ['FAIL x1', 'PASS y', 'NOT RUN x2']);
  assert.equal(lanes.events.includes('start x2'), false);
});

test("a Process half's build is its own test project's, with the same build options; --no-build only when its fast half passed on this tree", () => {
  const fast = 'dotnet test src/X/X.Tests --filter Category!=Process';
  assert.ok(tool.testBuild(fast));
  assert.equal(tool.testBuild(processRun('src/X/X.Tests')), tool.testBuild(fast));
  assert.equal(tool.testBuild('dotnet test "src\\X\\X.Tests\\" -s src/X/git.process.runsettings --logger "trx;LogFileName=a b.trx"'), tool.testBuild(fast));
  assert.notEqual(tool.testBuild(processRun('src/X/M.Tests')), tool.testBuild(fast));
  assert.notEqual(tool.testBuild('dotnet test src/X/X.Tests -c Release --settings src/X/process.runsettings'), tool.testBuild(fast), 'a build option one side lacks');
  for (const run of ['npm run test:web', 'dotnet build src/X/X.Tests', 'dotnet test', 'dotnet test --filter Category!=Process']) {
    assert.equal(tool.testBuild(run), null, run);
  }
  assert.equal(tool.noBuildCommand(`${processRun('src/X/X.Tests')} `), `${processRun('src/X/X.Tests')} --no-build`);
  // A changed tree always builds, and so does a tree git could not name.
  assert.equal(tool.reuseBuild({ gate: 'd', tree: 't1' }, 't1'), true);
  assert.equal(tool.reuseBuild({ gate: 'd', tree: 't1' }, 't2'), false);
  assert.equal(tool.reuseBuild({ gate: 'd', tree: null }, null), false);
  assert.equal(tool.reuseBuild(undefined, 't1'), false);
});

test("a Process half is handed its fast half's pass in this run and the tree it ran on; nothing when that failed or did not run", async () => {
  const gates = [
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'), planned('m', 'dotnet test src/X/M.Tests --filter Category!=Process'),
    planned('dp', processRun('src/X/X.Tests')), planned('dg', 'dotnet test src/X/X.Tests --settings src/X/git.process.runsettings'),
    planned('mp', processRun('src/X/M.Tests')),
  ];
  assert.deepEqual(tool.laneSchedule(gates).reuses, { dp: 'd', dg: 'd', mp: 'm' });
  const names = gates.map((gate) => gate.name);
  const treed = (lanes: ReturnType<typeof laneRun>) => async (root: string, gate: Gate, at: string, beside?: Beside) => ({ ...(await lanes.run(root, gate, at, beside)), tree: 't1' });

  for (const oneAtATime of [false, true]) {
    const lanes = laneRun(names);
    await tool.runGates('root', gates, 'logs', { run: treed(lanes), oneAtATime, scripts: {}, print: () => {} });
    assert.deepEqual(lanes.heard['dp']!.built, { gate: 'd', tree: 't1' }, `one at a time: ${oneAtATime}`);
    assert.deepEqual(lanes.heard['dg']!.built, { gate: 'd', tree: 't1' });
    assert.deepEqual(lanes.heard['mp']!.built, { gate: 'm', tree: 't1' });
    assert.equal(lanes.heard['d']!.built, undefined, 'a fast half builds');
  }

  // A fast half that failed: its Process halves build (--keep-going ran them), the other component's reuse.
  let lanes = laneRun(names);
  lanes.failing.add('d');
  await tool.runGates('root', gates, 'logs', { run: treed(lanes), keepGoing: true, scripts: {}, print: () => {} });
  assert.equal(lanes.heard['dp']!.built, undefined);
  assert.equal(lanes.heard['dg']!.built, undefined);
  assert.deepEqual(lanes.heard['mp']!.built, { gate: 'm', tree: 't1' });

  // --rerun or --stale naming the Process halves alone: no fast half ran in this run, so they build.
  lanes = laneRun(['dp', 'mp']);
  await tool.runGates('root', gates.filter((gate) => ['dp', 'mp'].includes(gate.name)), 'logs', { run: treed(lanes), scripts: {}, print: () => {} });
  assert.equal(lanes.heard['dp']!.built, undefined);
  assert.equal(lanes.heard['mp']!.built, undefined);
});

test("a Process half told not to build runs its command with --no-build and says whose build it ran; its re-run alone is as before", async () => {
  const fx = makeFixture('merge-branch-no-build');
  const { asked, step } = fakeStep(() => ({ out: 'Passed!\n', code: 0 }));
  const dp = planned('dp', processRun('src/X/X.Tests'));
  const result = await tool.runGate(fx.root, dp, fx.root, { step, noBuild: true, built: { gate: 'd', tree: 't1' } });
  assert.equal(asked[0], tool.trxCommand(tool.noBuildCommand(dp.run), join(fx.root, 'dp.trx')));
  assert.equal(result.verdict, 'PASS');
  assert.match(result.note, /no build: d built this tree in this run/);
  assert.match(readFileSync(join(fx.root, 'dp.log'), 'utf8'), /^\$ dotnet test src\/X\/X\.Tests --settings \S+ --no-build --logger/);

  const built = await tool.runGate(fx.root, dp, fx.root, { step });
  assert.equal(asked[1], tool.trxCommand(dp.run, join(fx.root, 'dp.trx')));
  assert.equal(built.note, '');
  // Only a dotnet test command is told so.
  await tool.runGate(fx.root, planned('r', 'npm run rehearse'), fx.root, { step, noBuild: true, built: { gate: 'd', tree: 't1' } });
  assert.equal(asked[2], 'npm run rehearse');
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// REHLANE1: the family rehearsal beside the driver's git half. WEBSPLIT1: the web gate's unit run, a light gate of its own

const gitRun = (project: string) => `dotnet test ${project} --settings src/Daoris.Desktop/git.process.runsettings`;
const rootScripts = (): Record<string, string> =>
  (JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;

test('the git half is the Process half run with the git settings, and the rehearsals that may run beside it are named by their scripts', () => {
  assert.equal(tool.isGitHalf({ name: 'renamed', run: gitRun('src/X/X.Tests') }), true);
  assert.equal(tool.isGitHalf({ name: 'driver-process', run: processRun('src/X/X.Tests') }), false);
  assert.equal(tool.isGitHalf({ name: 'driver', run: 'dotnet test src/X/X.Tests --filter Category!=Process' }), false);
  // REHLANE2: the release rehearsal too, which builds no .NET project at all. REHLANE2b: and the deployment rehearsal, whose
  // publish and build go into an output of its own.
  assert.deepEqual([...tool.BESIDE_GIT_HALF], ['rehearse', 'rehearse:family', 'rehearse:deploy']);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run rehearse:family' }), true);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run -s rehearse:family' }), true);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run rehearse' }), true);
  assert.equal(tool.runsBesideGitHalf({ name: 'renamed', run: 'npm run rehearse:deploy' }), true);
  for (const run of ['npm run rehearse-x', 'npm run rehearse:family-x', 'npm run rehearse:deploy-x', processRun('src/X/X.Tests')]) {
    assert.equal(tool.runsBesideGitHalf({ name: 'x', run }), false, run);
  }
});

test("this repository's Process lane runs the release, family and deployment rehearsals beside the git half, one after the other, and every other Process-lane gate alone", () => {
  const schedule = tool.laneSchedule(repoPlan, { scripts: rootScripts() });
  assert.deepEqual(schedule.process.map((gate) => gate.name),
    ['driver-process', 'driver-process-git', 'rehearse', 'rehearse-family', 'deployment', 'modules-process']);
  // Never beside each other (REHLANE2): the family runs the CLI's bin, which prefers the dist/ the release rehearsal's pack
  // stages in the CLI's folder and removes again; the deployment rehearsal packs that folder too, and runs the bin
  // (REHLANE2b); and two Process-lane gates at once is the measured ceiling.
  assert.deepEqual(schedule.beside, {
    'driver-process-git': ['rehearse', 'rehearse-family', 'deployment'],
    rehearse: ['driver-process-git'], 'rehearse-family': ['driver-process-git'], deployment: ['driver-process-git'],
  });
  // They still start once the light lane has ended: the service's hosts the family builds are the light lane's service and web
  // gates' too, and the cli gate runs the CLI's bin, which a rehearsal's staged dist/ would shadow.
  for (const name of ['rehearse', 'rehearse-family', 'deployment']) {
    assert.deepEqual(schedule.waits[name], { after: schedule.light.map((gate) => gate.name), component: null }, name);
  }
  // No gate is planned a step before it: the deployment rehearsal's builds keep build servers of their own in place of the
  // shutdown it once ran after, which would stop the builds of every gate beside it (REHLANE2b).
  assert.deepEqual(repoPlan.filter((gate) => 'before' in gate).map((gate) => gate.name), []);
});

/** The projects a rehearsal script names as `const name = join(repoRoot, 'src', …)`, by name, as repository paths. */
function rehearsalProjects(text: string): Record<string, string> {
  const named: Record<string, string> = {};
  for (const [, name, parts] of text.matchAll(/const (\w+) = join\(repoRoot, ((?:'[^']+'(?:,\s*)?)+)\);/g)) {
    named[name!] = [...parts!.matchAll(/'([^']+)'/g)].map((part) => part[1]).join('/');
  }
  return named;
}

/**
 * REHLANE1: a rehearsal that runs beside the git half builds a project of a component the Process lane builds only into its
 * own output (`ownBuild`), never into that project's folders, which the Process halves build into and run from; and it shuts
 * no build server down. Read from the tool its root script runs, as REACH's rows for the rehearsals are. REHLANE2: or it runs
 * no `dotnet` at all, as the release rehearsal does. REHLANE2b: and every publish of the desktop it runs is told an output of
 * its own (`tools/desktop-publish.mjs`'s `--artifacts-path`, whose test holds every `dotnet publish` that tool runs to the
 * output it is told), and no such gate is planned a step before it, as the deployment rehearsal's shutdown once was.
 */
test('a rehearsal that runs beside the git half builds what the Process lane builds into its own output, or no .NET at all, and shuts no build server down', () => {
  const scripts = rootScripts();
  const processBuilds = new Set(repoPlan.filter((gate) => tool.isProcessGate(gate)).map((gate) => tool.dotnetComponent(gate.run)));
  assert.deepEqual([...processBuilds], ['src/Daoris.Desktop']);
  const building: string[] = [];
  const publishing: string[] = [];
  for (const script of tool.BESIDE_GIT_HALF) {
    const file = /^node (tools\/\S+\.mjs)$/.exec(scripts[script] ?? '')?.[1];
    assert.ok(file, `${script} runs one tool`);
    const text = readFileSync(join(repoRoot, file), 'utf8');
    assert.doesNotMatch(text, /build-server/, `${file} shuts a build server down, which a build beside it would lose`);
    const roots = new Set([...text.matchAll(/const (\w+) = ownBuildRoot\(repoRoot, '[^']+'\);/g)].map((match) => match[1]!));
    const publishes = [...text.matchAll(/desktop-publish\.mjs'\)\}"([^`]*)`/g)].map((match) => match[1]!);
    if (publishes.length) publishing.push(script);
    for (const publish of publishes) {
      const told = /--artifacts-path "\$\{(\w+)\}"/.exec(publish)?.[1];
      assert.ok(told && roots.has(told), `${file} publishes the desktop into the projects' own folders: ${publish.trim()}`);
    }
    if (!/\bdotnet\s+(?:build|publish|test|run)\b|\bownBuild\(/.test(text)) continue;
    building.push(script);
    const projects = rehearsalProjects(text);
    const own = [...text.matchAll(/\bownBuild\((\w+),/g)].map((match) => match[1]!);
    assert.ok(own.length >= 1, `${file} builds nothing into its own output: this test has stopped reading it`);
    for (const name of own) assert.ok(projects[name], `${file} builds \${${name}} into its own output, a project this test cannot place`);
    for (const [, name] of text.matchAll(/dotnet (?:build|publish|test|run)\s+"\$\{(\w+)\}"/g)) {
      const project = projects[name!];
      assert.ok(project, `${file} builds \${${name}}, a project this test cannot place`);
      assert.ok(!processBuilds.has(tool.dotnetComponent(`dotnet build ${project}`)),
        `${file} builds ${project} into its own folders, which a Process half builds into and runs from`);
    }
  }
  // The family's and the deployment's tools are read for their builds: a rehearsal that names no `dotnet` is not, so those
  // that do are named here, and the one that publishes the desktop.
  assert.deepEqual(building, ['rehearse:family', 'rehearse:deploy']);
  assert.deepEqual(publishing, ['rehearse:deploy']);
  // In place of that shutdown, every build the deployment rehearsal makes keeps build servers of its own, as a run beside's
  // do (CIPRIO1): it joins none a build before it left, one of which once carried a broken environment into a publish.
  const deploy = readFileSync(join(repoRoot, 'tools', 'deployment-rehearsal.mjs'), 'utf8');
  assert.match(deploy, /\n {2}Object\.assign\(process\.env, OWN_BUILD_SERVERS\);/, 'the deployment rehearsal builds on servers it may share');
  assert.deepEqual({ ...rehearsalKit.OWN_BUILD_SERVERS }, { ...tool.BESIDE_ENVIRONMENT });
});

/**
 * REHLANE2 (the tool's header says why): the release rehearsal runs beside the git half only while no desktop test reads the
 * CLI package's folder outside its `src/` and `test/`, which no pack writes, nor runs its bin. Read from every tracked
 * desktop source, and from the stager's own list.
 */
test('the release rehearsal stages nothing a desktop test reads: they read the CLI package only under src/ and test/, which no pack writes', () => {
  const stager = readFileSync(join(repoRoot, 'tools', 'stage-package.mjs'), 'utf8');
  const staged = [...(/const STAGED = \[([^\]]+)\]/.exec(stager)?.[1] ?? '').matchAll(/'([^']+)'/g)].map((match) => match[1]!);
  assert.deepEqual(staged, ['canon', 'LICENSE', 'README.md'], 'the stager has stopped saying what it stages: read it again');
  assert.match(stager, /\[\.\.\.STAGED, 'dist'\]/, 'the stager no longer cleans dist/ with them: read it again');
  const unwritten = new Set(['src', 'test']);
  for (const folder of [...staged, 'dist']) assert.ok(!unwritten.has(folder), `a pack writes the CLI's ${folder}/`);
  const sources = trackedFiles().filter((path) => path.startsWith('src/Daoris.Desktop/') && path.endsWith('.cs'));
  assert.ok(sources.length >= 100, 'the desktop sources left src/Daoris.Desktop');
  let read = 0;
  for (const path of sources) {
    const text = readFileSync(join(repoRoot, path), 'utf8');
    assert.doesNotMatch(text, /daoris\.mjs/, `${path} names the CLI's bin, which prefers a staged dist/`);
    for (const match of text.matchAll(/Daoris\.Cli"\s*,\s*"([^"]+)"|Daoris\.Cli[\\/]+([^\\/\s"`'<]+)/g)) {
      const folder = match[1] ?? match[2]!;
      read += 1;
      assert.ok(unwritten.has(folder), `${path} reads the CLI package's ${folder}, which a pack beside it may stage or remove`);
    }
  }
  assert.ok(read >= 10, 'no desktop source reads the CLI package any more: this test has stopped reading them');
});

test('the release, family and deployment rehearsals run beside the git half, one after the other, never beside another Process half', async () => {
  const gates = [
    planned('d', 'dotnet test src/X/X.Tests --filter Category!=Process'),
    planned('dp', processRun('src/X/X.Tests')), planned('dg', gitRun('src/X/X.Tests')), planned('mp', processRun('src/X/M.Tests')),
    planned('r', 'npm run rehearse'), planned('f', 'npm run rehearse:family'), planned('dep', 'npm run rehearse:deploy'),
  ];
  const lanes = laneRun(gates.map((gate) => gate.name));
  // dg ends only once dep has started beside it, and r, f and dep only once dg has: one at a time, none would.
  lanes.until['dg'] = () => lanes.started['dep']!.promise;
  for (const name of ['r', 'f', 'dep']) lanes.until[name] = () => lanes.started['dg']!.promise;
  const printed: string[] = [];
  const results = await tool.runGates('root', gates, 'logs', { run: lanes.run, scripts: {}, print: (line) => printed.push(line) });
  const at = (event: string) => lanes.events.indexOf(event);
  const order = lanes.events.join(', ');
  assert.ok(['r', 'f', 'dep'].every((name) => at(`start ${name}`) > at('end dp')), `none runs beside the driver's other half: ${order}`);
  assert.ok(at('start f') > at('end r'), `the family never runs beside the release rehearsal, whose staged dist/ its CLI would run: ${order}`);
  // REHLANE2b: the deployment rehearsal packs the CLI's folder as the release rehearsal does, and runs its bin as the family does.
  assert.ok(at('start dep') > at('end r') && at('start dep') > at('end f'), `the deployment rehearsal runs beside neither: ${order}`);
  assert.ok(at('start dep') < at('end dg'), `the deployment rehearsal runs beside the git half: ${order}`);
  assert.ok(['dg', 'r', 'f', 'dep'].every((name) => at('start mp') > at(`end ${name}`)), `the next Process half waits for all four: ${order}`);
  // Two Process-lane gates at once at most, the measured ceiling.
  let running = 0;
  let most = 0;
  for (const event of lanes.events) {
    if (event === 'start d' || event === 'end d') continue;
    running += event.startsWith('start ') ? 1 : -1;
    most = Math.max(most, running);
  }
  assert.equal(most, 2, order);
  assert.match(printed.join('\n'), /Process\s+dp, dg, r, f, dep, mp\b/);
  assert.match(printed.join('\n'), /r, f, dep run beside dg, one after the other: none builds into a folder a Process half runs from, or shuts a build server down/);
  assert.deepEqual(summary(printed), ['PASS d', 'PASS dp', 'PASS dg', 'PASS mp', 'PASS r', 'PASS f', 'PASS dep'], "the summary in the plan's order");
  assert.deepEqual(results.map((result) => result.gate.name), gates.map((gate) => gate.name));

  // --one-at-a-time runs every gate in turn, in the plan's order, as before.
  const turn = laneRun(gates.map((gate) => gate.name));
  await tool.runGates('root', gates, 'logs', { run: turn.run, oneAtATime: true, scripts: {}, print: () => {} });
  assert.deepEqual(turn.events.filter((event) => event.startsWith('start')), gates.map((gate) => `start ${gate.name}`));
});

test("the git half's failures are re-run alone: after the rehearsal beside it has ended, and none starts meanwhile", async () => {
  const gates = [planned('u', 'node u.mjs'), planned('dg', gitRun('src/X/X.Tests')), planned('f', 'npm run rehearse:family')];
  const names = gates.map((gate) => gate.name);

  // f runs when dg wants to be alone: what dg is handed resolves once f has ended.
  const running = laneRun(names);
  running.until['f'] = () => running.started['dg']!.promise;
  running.until['dg'] = async () => {
    await running.started['f']!.promise;
    await running.heard['dg']!.settled!();
    running.events.push('alone');
  };
  await tool.runGates('root', gates, 'logs', { run: running.run, scripts: {}, print: () => {} });
  assert.ok(running.events.indexOf('alone') > running.events.indexOf('end f'), running.events.join(', '));

  // f has not started when dg wants to be alone: it starts once dg has ended.
  const waiting = laneRun(names);
  waiting.until['u'] = () => waiting.started['dg']!.promise;
  waiting.until['dg'] = async () => {
    await waiting.heard['dg']!.settled!();
    waiting.events.push('alone');
  };
  await tool.runGates('root', gates, 'logs', { run: waiting.run, scripts: {}, print: () => {} });
  assert.ok(waiting.events.indexOf('start f') > waiting.events.indexOf('end dg'), waiting.events.join(', '));
  assert.ok(waiting.events.indexOf('alone') > waiting.events.indexOf('end u'), 'the light lane ended first, as before');

  // REHLANE2: the release rehearsal runs beside dg when dg wants to be alone, and the family waits for it: dg's re-run waits
  // for the release rehearsal to end, and the family, next beside dg, starts only once dg has ended.
  const chain = [...gates.slice(0, 2), planned('r', 'npm run rehearse'), gates[2]!];
  const chained = laneRun(chain.map((gate) => gate.name));
  chained.until['r'] = () => chained.started['dg']!.promise;
  chained.until['dg'] = async () => {
    await chained.started['r']!.promise;
    await chained.heard['dg']!.settled!();
    chained.events.push('alone');
  };
  await tool.runGates('root', chain, 'logs', { run: chained.run, scripts: {}, print: () => {} });
  const at = (event: string) => chained.events.indexOf(event);
  assert.ok(at('alone') > at('end r'), chained.events.join(', '));
  assert.ok(at('start f') > at('end dg'), chained.events.join(', '));
});

test("this repository's web gate runs its unit tests as a light gate of its own, which builds nothing, with WEBGATE1's cap", () => {
  const scripts = rootScripts();
  const web = repoPlan.find((gate) => gate.name === 'web')!;
  const unit = repoPlan.find((gate) => gate.name === 'web-unit');
  assert.ok(unit, "the web gate's unit run is a declared gate of its own");
  assert.equal(unit.run, 'npm run test:web:unit');
  assert.equal(scripts['test:web:unit'], 'npm --prefix src/Daoris.Web run test');
  assert.equal(web.run, 'npm run test:web:e2e');
  assert.equal(scripts['test:web:e2e'],
    'npm --prefix src/Daoris.Web run build && dotnet build src/Daoris.Service/Daoris.Service.Http && npm --prefix src/Daoris.Web run e2e');
  // A person's whole web run is both, its unit tests first, as it was.
  assert.equal(scripts['test:web'], 'npm run test:web:unit && npm run test:web:e2e');
  const schedule = tool.laneSchedule(repoPlan, { scripts });
  assert.deepEqual(schedule.components['web-unit'], []);
  assert.deepEqual(schedule.components['web'], ['src/Daoris.Service']);
  assert.equal(tool.isProcessLane(unit), false);
  assert.equal(tool.isHeavyGate(unit), true, 'vitest loads the machine, so a Process half waits for it (GATEORDER1)');
  // The web gate goes before its unit run, so the place service frees goes to the build the light lane's floor waits on.
  const light = schedule.light.map((gate) => gate.name);
  assert.ok(light.indexOf('web') < light.indexOf('web-unit'), light.join(', '));
  // WEBGATE1: vitest's workers, a quarter of the machine, set in the page's own config, which the unit run reads wherever it runs.
  assert.match(readFileSync(join(repoRoot, 'src', 'Daoris.Web', 'vite.config.ts'), 'utf8'), /maxWorkers: process\.env\.DAORIS_WEB_WORKERS \?\? '25%'/);
});

test("the web gate's unit run starts beside service, and the web gate's build after it, as before", async () => {
  const scripts = { 'test:web:e2e': 'npm --prefix w run build && dotnet build src/S/S.Http && npm --prefix w run e2e', 'test:web:unit': 'npm --prefix w run test' };
  const gates = [planned('service', 'dotnet test src/S'), planned('k', 'dotnet test src/K'), planned('web', 'npm run test:web:e2e'), planned('web-unit', 'npm run test:web:unit')];
  const lanes = laneRun(gates.map((gate) => gate.name));
  lanes.until['service'] = () => lanes.started['web-unit']!.promise;
  await tool.runGates('root', gates, 'logs', { run: lanes.run, scripts, print: () => {} });
  const at = (event: string) => lanes.events.indexOf(event);
  assert.ok(at('start web-unit') < at('end service'), lanes.events.join(', '));
  assert.ok(at('start web') > at('end service'), lanes.events.join(', '));
});

test("a gate's whole output is kept in its log", async () => {
  const fx = makeFixture('merge-branch-gate-log');
  const long = Array.from({ length: 5000 }, (_, i) => `line ${i + 1}`).join('\n');
  const { asked, step } = fakeStep(() => ({ out: `${long}\n`, code: 0 }));

  const result = await tool.runGate(fx.root, planned('deployment', 'npm run rehearse:deploy'), fx.root, { step });
  assert.equal(result.verdict, 'PASS');
  assert.deepEqual(asked, ['npm run rehearse:deploy']);
  const log = readFileSync(join(fx.root, 'deployment.log'), 'utf8');
  assert.match(log, /\nline 1\n/);
  assert.match(log, /\nline 5000\n/);
  assert.equal(existsSync(join(fx.root, 'deployment.log.partial')), false, 'the log is renamed into place when its gate ends');
  fx.cleanup();
});

test('a real-process failure is re-run alone once: a pass alone is a FLAKE, a second failure a FAIL, and nothing else is re-run', async () => {
  const fx = makeFixture('merge-branch-flake-runs');
  const driver = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  const flaky = 'Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked';
  const alone = 'Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)\n';

  let fake = fakeStep((command) => (command.includes('--filter') ? { out: alone, code: 0 } : { out: PROCESS_FAILURE, code: 1 }));
  // GATELANES1: the re-run alone waits for the light lane beside it to end, so a pass alone is still a pass with nothing beside.
  const first = fake;
  let result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step, settled: async () => { first.asked.push('the light lane ended'); } });
  assert.equal(result.verdict, 'FLAKE');
  // The full run leaves its trx (PROC1); the re-run of one test adds none.
  assert.deepEqual(fake.asked, [tool.trxCommand(driver.run, join(fx.root, 'driver-process.trx')), 'the light lane ended', tool.rerunCommand(driver.run, flaky)]);
  assert.match(result.note, /failed in the full run and passed alone: ProcessJobTests\.A_child.*driver-process\.rerun\.log/);
  assert.match(readFileSync(join(fx.root, 'driver-process.rerun.log'), 'utf8'), /Passed!/);

  fake = fakeStep(() => ({ out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.match(result.note, /failed again alone/);

  // Two failed in the full run, one of them passing alone: the note names only the one that failed again, and says the
  // other passed alone, so a reader chases the right test (a parent once briefed a worker on three that had passed).
  const second = 'Daoris.Desktop.Driver.Tests.StopHoldTickTests.A_take_the_person_stopped_is_held';
  const twoFailed = PROCESS_FAILURE.replace('Failed!  - Failed:     1', `  Failed ${second} [9 s]\r\nFailed!  - Failed:     2`);
  fake = fakeStep((command) => (!command.includes('--filter') ? { out: twoFailed, code: 1 }
    : command.includes('StopHoldTickTests') ? { out: alone, code: 0 } : { out: PROCESS_FAILURE, code: 1 }));
  result = await tool.runGate(fx.root, driver, fx.root, { step: fake.step });
  assert.equal(result.verdict, 'FAIL');
  assert.match(result.note, /failed again alone: ProcessJobTests\.A_child[^;]*; passed alone: StopHoldTickTests\.A_take/);
  assert.doesNotMatch(result.note, /failed again alone: [^;]*StopHoldTickTests/);

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
// MEMGATE1: a heavy gate waits for memory rather than failing for want of it, and a gate that died of memory is said so
// and run again once, alone. On 2026-10-22 a full set beside the install's driven sessions and two builders ran a 64 GB
// machine out of commit: the sensitive scan threw OutOfMemoryException, web-rules could not allocate, the family
// rehearsal's landing said "Out of memory." twice, each read as the change's failure, and each passed run later.

const GiB = 2 ** 30;

/**
 * A stand-in machine (never the real one): each reading is the next of `commits` (GB of free commit, the last one kept),
 * with `physical` GB of free physical memory and `total` GB in all; each read is noted, in `events` too when given. Its
 * clock moves only when the wait sleeps.
 */
function standIn(commits: number[], { physical = 32, total = 64, events }: { physical?: number; total?: number; events?: string[] } = {}) {
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
const MEMORY_SIGNS = [
  "Unhandled exception. System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.",
  'fatal error: runtime: cannot allocate memory',
  'DataCloneError: Data cannot be cloned, out of memory.',
  'Out of memory.',
  'FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory',
  'Error: The paging file is too small for this operation to complete.',
];

test('MEMGATE1: the memory a gate needs is its class\'s, or what daoris.gates.json declares; a light gate needs none', () => {
  const needs = Object.fromEntries(repoPlan.map((gate) => [gate.name, tool.memoryNeed(gate)]));
  const { suite, process: half, rehearsal } = tool.MEMORY_NEEDS;
  assert.deepEqual(needs, {
    universal: 0, 'code-map': 0, 'orient-index': 0, 'test-map': 0, 'web-types': 0, 'web-rules': 0, 'tool-tests': 0,
    cli: suite, service: suite, devkit: suite, driver: suite, modules: suite, web: suite, 'web-unit': suite, 'web-measured': suite,
    'driver-process': half, 'driver-process-git': half, 'modules-process': half,
    rehearse: rehearsal, 'rehearse-family': rehearsal, deployment: rehearsal,
  });
  assert.ok(suite! > 0 && half! >= suite! && rehearsal! >= half!, 'a heavier class needs no less');
  assert.ok(tool.MEMORY_NEEDS['again']! > 0, 'a light gate that died of memory needs some to run again');

  // `"memoryGb"` on a declared gate is its need, whatever its class: more, less, or none at all.
  const fx = makeFixture('merge-branch-memory-declared');
  fx.write('daoris.gates.json', JSON.stringify({ gates: [
    { name: 'deployment', run: 'npm run rehearse:deploy', memoryGb: 14 },
    { name: 'scan', run: 'node scan.mjs', memoryGb: 2 },
    { name: 'service', run: 'dotnet test src/S', memoryGb: 0 },
    { name: 'plain', run: 'dotnet test src/T' },
  ] }));
  const plan = tool.readPlan(fx.root);
  assert.deepEqual(Object.fromEntries(plan.map((gate) => [gate.name, tool.memoryNeed(gate)])), { scan: 2, service: 0, plain: suite, deployment: 14 });
  assert.equal('memoryGb' in plan.find((gate) => gate.name === 'plain')!, false);
  for (const wrong of ['lots', -1, null]) {
    fx.write('daoris.gates.json', JSON.stringify({ gates: [{ name: 'x', run: 'node x.mjs', memoryGb: wrong }] }));
    const refused = refusal(() => tool.readPlan(fx.root));
    assert.equal(refused.exitCode, 2);
    assert.match(refused.message, /gate 'x'.*memoryGb.*GB/);
  }
  fx.cleanup();
});

test('MEMGATE1: a stand-in machine short of memory delays a heavy gate and says why; it starts once the memory is free', async () => {
  const lanes = laneRun(['u', 'p']);
  const machine = standIn([3, 3, 12], { events: lanes.events });
  const printed: string[] = [];
  const gates = [planned('u', 'node u.mjs'), planned('p', processRun('fake/P.Tests'))];
  await tool.runGates('root', gates, 'logs', { run: lanes.run, print: (line) => printed.push(line), memory: machine });
  // Read before it, again every few seconds while short, and started only once the reading had its need.
  const at = (event: string) => lanes.events.indexOf(event);
  assert.ok(at('start p') > lanes.events.lastIndexOf('read 12'), lanes.events.join(', '));
  assert.ok(at('start p') > at('read 3'), lanes.events.join(', '));
  assert.deepEqual(machine.reads, [3, 3, 12], 'the light gate beside it was never read for');
  assert.deepEqual(machine.slept, [tool.MEMORY_POLL_MS, tool.MEMORY_POLL_MS]);
  const said = printed.join('\n');
  assert.match(said, /waiting for memory before p: 3\.0 GB free commit, the gate needs 8 GB; read again every 5 s, for up to 10 m/);
  assert.match(said, /memory for p: 12\.0 GB free commit and 32\.0 GB free physical memory after 10 s; it starts/);
  assert.deepEqual(summary(printed), ['PASS u', 'PASS p']);
});

test('MEMGATE1: past its bound a heavy gate runs anyway, saying so; a commit that cannot be read is said once, and physical memory judges alone', async () => {
  const deploy = planned('deployment', 'npm run rehearse:deploy');
  // Short the whole time: read every poll until the bound, then it runs, saying so.
  let machine = standIn([2]);
  let said: string[] = [];
  await tool.memoryGate({ ...machine, say: (line) => said.push(line) })(deploy);
  assert.equal(machine.slept.reduce((sum, ms) => sum + ms, 0), tool.MEMORY_WAIT_MS);
  assert.equal(machine.reads.length, tool.MEMORY_WAIT_MS / tool.MEMORY_POLL_MS + 1);
  assert.match(said.join('\n'), /waiting for memory before deployment: 2\.0 GB free commit, the gate needs 10 GB/);
  assert.match(said.at(-1)!, /memory for deployment: still 2\.0 GB free commit after 10 m, the gate needs 10 GB; it runs anyway, past the bound/);

  // A run that stops while a gate waits stops the wait.
  machine = standIn([2]);
  let reads = 0;
  await tool.memoryGate({ ...machine, say: () => {} })(deploy, { halted: () => ++reads > 3 });
  assert.ok(machine.slept.length <= 3, 'the wait ends with the run');

  // A machine smaller than the needs were set on waits for a quarter of its memory at most, never for what it has not got.
  machine = standIn([5], { total: 16 });
  said = [];
  await tool.memoryGate({ ...machine, say: (line) => said.push(line) })(deploy);
  assert.deepEqual(said, [], 'a quarter of 16 GB is free');

  // Where the commit cannot be read, it is said once a run, and the physical memory alone is waited for.
  let physical = [1, 1, 20];
  said = [];
  let clock = 0;
  const unread = tool.memoryGate({
    read: () => ({ physical: physical.shift()! * GiB, commit: null, total: 64 * GiB, why: 'this platform keeps none' }),
    sleep: async (ms) => { clock += ms; }, now: () => clock, say: (line) => said.push(line),
  });
  await unread(deploy);
  physical = [30];
  await unread(planned('rehearse', 'npm run rehearse'));
  assert.equal(said.filter((line) => /cannot be read/.test(line)).length, 1, said.join('\n'));
  assert.match(said[0]!, /memory: free commit cannot be read here \(this platform keeps none\); the heavy gates wait on free physical memory alone/);
  assert.match(said.join('\n'), /waiting for memory before deployment: 1\.0 GB free physical memory, the gate needs 10 GB/);
  // Nothing readable at all: said once, and no gate waits.
  said = [];
  const blind = tool.memoryGate({ read: () => { throw new Error('no query'); }, sleep: async () => { throw new Error('never sleeps'); }, say: (line) => said.push(line) });
  await blind(deploy);
  await blind(deploy);
  assert.deepEqual(said, ['  memory: it cannot be read here (the reading failed: no query); the heavy gates run without waiting for it']);
});

test('MEMGATE1: a light gate never waits: its memory is not even read, in two lanes or one at a time', async () => {
  for (const oneAtATime of [false, true]) {
    const lanes = laneRun(['a', 'b']);
    let asked = 0;
    const memory = { read: () => { asked += 1; return { physical: 0, commit: 0, total: 64 * GiB }; }, sleep: async () => {}, now: () => 0 };
    await tool.runGates('root', [planned('a', 'node a.mjs'), planned('b', 'npm run check:web-rules')], 'logs', { run: lanes.run, print: () => {}, memory, oneAtATime });
    assert.equal(asked, 0, `oneAtATime ${oneAtATime}`);
    assert.deepEqual(lanes.events.filter((event) => event.startsWith('start')).sort(), ['start a', 'start b']);
  }
  // The same in turn for a heavy one: it waits there too.
  const lanes = laneRun(['h']);
  const machine = standIn([1, 9]);
  await tool.runGates('root', [planned('h', 'npm run verify')], 'logs', { run: lanes.run, print: () => {}, memory: machine, oneAtATime: true });
  assert.deepEqual(machine.reads, [1, 9]);
});

test('MEMGATE1: each out-of-memory sign in a log is a death of memory, and the platform\'s exit for it; a quoted one, a failed check and another crash are not', () => {
  for (const sign of MEMORY_SIGNS) {
    const died = tool.memoryDeath(`$ npm run check:web-rules\nsome output\n${sign}\n    at somewhere\n\n(exited 1)\n`, 1);
    assert.ok(died, sign);
    assert.match(died.said, /memory|paging file/i, sign);
  }
  // Windows' statuses for an allocation it refused, unsigned as cmd and npm pass them on, and signed.
  for (const code of [0xC0000017, 0xC000012D, -1073741801, -1073741523]) {
    assert.match(tool.memoryDeath('', code)?.said ?? '', /exit 0xC000(0017|012D)/, `exit ${code}`);
  }
  // An assertion that shows the phrase as a value, a failed check, and a process that crashed some other way.
  assert.equal(tool.memoryDeath('  Error Message:\n   Assert.Equal() Failure: Collections differ\nExpected: ["out of memory"]\nActual:   ["full"]\n', 1), null);
  assert.equal(tool.memoryDeath(PROCESS_FAILURE, 1), null);
  assert.equal(tool.memoryDeath('', 0xC0000142), null);
  assert.equal(tool.memoryDeath('', 127), null);
});

test('MEMGATE1: a gate that died of memory is classed as memory and run again once, alone, after waiting: a pass is a FLAKE of memory', async () => {
  const fx = makeFixture('merge-branch-memory-again');
  for (const sign of MEMORY_SIGNS) {
    const died = planned('web-rules', 'npm run check:web-rules');
    const beside = planned('u', 'node u.mjs');
    const printed: string[] = [];
    const firstLine = signal();
    const asked: string[] = [];
    let runs = 0;
    // u ends only once the first run's line is printed, so a run again that came before u ended would show here.
    const step: Step = async (command, _cwd, fd) => {
      asked.push(command);
      if (command === beside.run) {
        await within(firstLine.promise, "the dead gate's first line");
        asked.push('u ended');
      }
      const dies = command === died.run && ++runs === 1;
      writeSync(fd, `$ ${command}\n${dies ? `${sign}\n` : 'ok\n'}`);
      return dies ? 1 : 0;
    };
    const machine = standIn([2, 9]);
    const results = await tool.runGates(fx.root, [died, beside], fx.root, {
      run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step }),
      print: (line) => {
        printed.push(line);
        if (/MEMORY\s+web-rules/.test(line)) firstLine.resolve();
      },
      memory: machine,
    });
    assert.deepEqual(asked, [died.run, beside.run, 'u ended', died.run], `${sign}: run again once, after every other gate ended`);
    const [again] = results;
    assert.equal(again!.verdict, 'FLAKE', sign);
    assert.equal(again!.cause, 'memory', sign);
    const said = printed.join('\n');
    assert.match(said, /MEMORY\s+web-rules\s.*\(exit 1; died of memory \(.+\): run again alone once every other gate has ended, after waiting for memory\)/, sign);
    // A light gate waits for memory only to run again, for what a light gate that died of memory needs.
    assert.deepEqual(machine.reads, [2, 9], sign);
    assert.match(said, new RegExp(`waiting for memory before web-rules: 2\\.0 GB free commit, the gate needs ${tool.MEMORY_NEEDS['again']} GB to run again`), sign);
    assert.match(said, /FLAKE\s+web-rules\s.*died of memory \(.+\), and passed run again alone; web-rules\.rerun\.log/, sign);
    assert.deepEqual(summary(printed), ['FLAKE web-rules', 'PASS u'], sign);
    assert.ok(readFileSync(join(fx.root, 'web-rules.log'), 'utf8').includes(sign), 'the first run keeps its own log');
  }
  fx.cleanup();
});

test('MEMGATE1: a second death of memory is a failure still named as memory; a second failure of another kind is a plain one', async () => {
  const fx = makeFixture('merge-branch-memory-twice');
  const died = planned('web-rules', 'npm run check:web-rules');
  const gateOn = (answer: (run: number) => { out: string; code: number }) => {
    let runs = 0;
    const fake = fakeStep(() => answer(++runs));
    const printed: string[] = [];
    return {
      fake, printed,
      results: tool.runGates(fx.root, [died], fx.root, {
        run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step: fake.step }), print: (line) => printed.push(line),
      }),
    };
  };
  let run = gateOn(() => ({ out: 'DataCloneError: Data cannot be cloned, out of memory.\n', code: 1 }));
  let [result] = await run.results;
  assert.equal(run.fake.asked.length, 2, 'run again once, not until it passes');
  assert.equal(result!.verdict, 'FAIL', 'a failure all the same: the gate did not pass');
  assert.equal(result!.cause, 'memory');
  assert.match(result!.note, /died of memory twice \(.*out of memory.*; then .*out of memory.*\), not of a check: run it again when the machine has memory to spare; web-rules\.rerun\.log/);
  assert.match(run.printed.at(-1)!, /^\s+MEMORY\s+web-rules\s/, 'one at a time and alone, its line is its verdict, said as memory');

  // The platform's exit for it counts the same, with nothing printed.
  run = gateOn(() => ({ out: '', code: 0xC000012D }));
  [result] = await run.results;
  assert.equal(result!.cause, 'memory');
  assert.match(result!.note, /died of memory twice \(exit 0xC000012D/);

  // Died of memory, then failed a check: a plain failure, and it says so.
  run = gateOn((n) => (n === 1 ? { out: 'Out of memory.\n', code: 1 } : { out: 'AssertionError: expected 1 to equal 2\n', code: 1 }));
  [result] = await run.results;
  assert.equal(result!.verdict, 'FAIL');
  assert.equal(result!.cause, undefined);
  assert.match(result!.note, /exit 1; died of memory \("Out of memory\."\), then failed run again alone, not of memory; web-rules\.rerun\.log/);
  assert.match(run.printed.at(-1)!, /^\s+FAIL\s+web-rules\s/);

  // How a summary says them: a flake of memory is the machine's, never recorded under FLAKE1; a failure names it.
  const as = (name: string, verdict: string, cause?: string): GateResult => ({ gate: planned(name, 'x'), code: 0, log: '', verdict, note: '', ms: 0, ...(cause ? { cause } : {}) });
  assert.equal(tool.flakeNote([as('driver', 'FLAKE'), as('rehearse', 'FLAKE')]), ', with 2 flakes (driver, rehearse): record it under FLAKE1');
  assert.equal(tool.flakeNote([as('driver', 'FLAKE'), as('web-rules', 'FLAKE', 'memory')]),
    ', with 1 flake (driver): record it under FLAKE1; and 1 flake of memory (web-rules): it died of memory and passed run again alone, the machine\'s and not the change\'s');
  assert.equal(tool.flakeNote([]), '');
  assert.equal(tool.memoryLine([as('cli', 'FAIL')]), null);
  assert.equal(tool.memoryLine([as('cli', 'FAIL'), as('web-rules', 'FAIL', 'memory')]),
    '  Of memory, not of a check: web-rules died of memory; run it again when the machine has memory to spare.');
  fx.cleanup();
});

test('MEMGATE1: a rehearsal that died of memory is run again for memory, though its checks failed with it', async () => {
  const fx = makeFixture('merge-branch-memory-rehearsal');
  const family = planned('rehearse-family', 'npm run rehearse:family');
  // The 2026-10-22 landing step: two checks failed, each saying Out of memory.
  const landing = `${OK_LINES}  FAIL  the landing plugin lands\n          Out of memory.\n  FAIL  the landing is kept\n          Out of memory.\n\n  299/301 checks passed\n`;
  const fake = fakeStep(() => ({ out: landing, code: 1 }));
  const result = await tool.runGate(fx.root, family, fx.root, { step: fake.step });
  assert.equal(fake.asked.length, 1, 'not run again in place, beside the gates still running');
  assert.equal(result.cause, 'memory');
  assert.equal(result.again, true);
  assert.equal(result.verdict, 'FAIL', 'a verdict recorded before it runs again is no pass');
  assert.match(result.note, /died of memory \("Out of memory\."\)/);
  fx.cleanup();
});

test("MEMGATE1: a Process half's tests that died of memory run alone once its memory is free; a whole half that died runs again whole", async () => {
  const fx = makeFixture('merge-branch-memory-half');
  const half = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
  const flaky = 'Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked';
  const oom = PROCESS_FAILURE.replace('   Assert.True() Failure', '   System.OutOfMemoryException: Insufficient memory to continue the execution of the program.');
  const alone = 'Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)\n';
  const order: string[] = [];
  const memory: MemoryWait = async (gate, options) => { order.push(`memory ${gate.name}${options?.again ? ' again' : ''}`); };
  const settled = async () => { order.push('settled'); };

  let fake = fakeStep((command) => (command.includes('--filter') ? { out: alone, code: 0 } : { out: oom, code: 1 }));
  let result = await tool.runGate(fx.root, half, fx.root, { step: fake.step, settled, memory });
  assert.equal(result.verdict, 'FLAKE');
  assert.equal(result.cause, 'memory');
  assert.deepEqual(order, ['settled', 'memory driver-process again'], 'alone first, then its memory');
  assert.equal(fake.asked.at(-1), tool.rerunCommand(half.run, flaky));
  assert.match(result.note, /died of memory in the full run \(.*OutOfMemoryException.*\) and passed alone: ProcessJobTests\.A_child.*driver-process\.rerun\.log/);

  // Alone, it died of memory again: a failure, named as memory.
  fake = fakeStep((command) => (command.includes('--filter') ? { out: `${oom}\n`, code: 1 } : { out: oom, code: 1 }));
  result = await tool.runGate(fx.root, half, fx.root, { step: fake.step, settled, memory });
  assert.equal(result.verdict, 'FAIL');
  assert.equal(result.cause, 'memory');
  assert.match(result.note, /died of memory again alone \(.*OutOfMemoryException.*\): ProcessJobTests\.A_child/);

  // The test host itself died of memory, naming no test: the half runs again whole, at the run's end.
  fake = fakeStep(() => ({ out: 'The active test run was aborted. Reason: Test host process crashed : Out of memory.\n', code: 1 }));
  result = await tool.runGate(fx.root, half, fx.root, { step: fake.step, settled, memory });
  assert.equal(result.again, true);
  assert.equal(result.cause, 'memory');
  assert.equal(fake.asked.length, 1);
  fx.cleanup();
});

test('MEMGATE1: a run a failure stopped runs no gate again for memory, and says why; --keep-going runs it again', async () => {
  const fx = makeFixture('merge-branch-memory-stopped');
  // Of two components, so they run side by side.
  const gates = [planned('a', 'dotnet build src/A/A'), planned('b', 'dotnet build src/B/B')];
  for (const keepGoing of [false, true]) {
    const fake = fakeStep((command) => (command === gates[0]!.run ? { out: 'Out of memory.\n', code: 1 } : { out: 'error CS0103\n', code: 1 }));
    const printed: string[] = [];
    const results = await tool.runGates(fx.root, gates, fx.root, {
      keepGoing, run: (root, gate, dir, given) => tool.runGate(root, gate, dir, { ...given, step: fake.step }), print: (line) => printed.push(line),
    });
    assert.equal(fake.asked.filter((command) => command === gates[0]!.run).length, keepGoing ? 2 : 1, `keepGoing ${keepGoing}`);
    assert.deepEqual(summary(printed), ['MEMORY a', 'FAIL b'], `keepGoing ${keepGoing}`);
    if (!keepGoing) assert.match(results[0]!.note, /not run again: b failed, and the run stopped there/);
  }
  fx.cleanup();
});

test("MEMGATE1: the machine's memory is read from the platform: Windows' free physical memory and free commit, Linux's commit only where it is held", () => {
  // Win32_OperatingSystem's fields are in KB, as the CIM query answers them.
  assert.deepEqual(tool.parseWindowsMemory('{"FreePhysicalMemory":22818728,"FreeVirtualMemory":15787368}'),
    { physical: 22818728 * 1024, commit: 15787368 * 1024 });
  assert.deepEqual(tool.parseWindowsMemory('﻿{"FreePhysicalMemory":null,"FreeVirtualMemory":1024}\r\n'), { physical: null, commit: 1024 * 1024 });
  const asked: string[] = [];
  const windows = tool.readMemory({
    platform: 'win32', freemem: () => 7, totalmem: () => 64 * GiB,
    run: (command, args) => { asked.push(`${command} ${args.join(' ')}`); return { status: 0, stdout: '{"FreePhysicalMemory":1048576,"FreeVirtualMemory":2097152}' }; },
  });
  assert.deepEqual(windows, { physical: GiB, commit: 2 * GiB, total: 64 * GiB });
  assert.match(asked[0]!, /^powershell\.exe .*Win32_OperatingSystem.*FreePhysicalMemory.*FreeVirtualMemory/);
  // A query that fails leaves Node's free memory, and says why the commit is not read.
  const failed = tool.readMemory({ platform: 'win32', freemem: () => 7, totalmem: () => 8, run: () => ({ status: 1, stdout: '', stderr: 'denied' }) });
  assert.equal(failed.physical, 7);
  assert.equal(failed.commit, null);
  assert.match(failed.why!, /the memory query failed: denied/);

  // Linux holds a process to a commit limit only under strict overcommit (mode 2); otherwise there is none to read.
  const meminfo = 'MemTotal:       16000000 kB\nMemAvailable:    8000000 kB\nCommitLimit:    12000000 kB\nCommitted_AS:    9000000 kB\n';
  assert.deepEqual(tool.parseMeminfo(meminfo), { commit: 3000000 * 1024 });
  const files = (mode: string) => (path: string) => (path.endsWith('overcommit_memory') ? `${mode}\n` : meminfo);
  assert.deepEqual(tool.readMemory({ platform: 'linux', freemem: () => 5, totalmem: () => 9, read: files('2') }), { physical: 5, commit: 3000000 * 1024, total: 9 });
  const loose = tool.readMemory({ platform: 'linux', freemem: () => 5, totalmem: () => 9, read: files('0') });
  assert.deepEqual([loose.physical, loose.commit], [5, null]);
  assert.match(loose.why!, /overcommit/);
  const mac = tool.readMemory({ platform: 'darwin', freemem: () => 5, totalmem: () => 9 });
  assert.deepEqual([mac.physical, mac.commit], [5, null]);
  assert.match(mac.why!, /darwin/);
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
    stale: [],
  });
  // A gate the lanes did not reach runs when named, in the plan's order; a kept FAIL stays a FAIL.
  const named = tool.rerunPlan(plan, chosen, { ...results, 'driver-process': { verdict: 'PASS', at: 'x' }, web: { verdict: 'PASS', at: 'y' } }, ['deployment', 'cli']);
  assert.deepEqual(named.run, ['cli', 'deployment']);
  assert.deepEqual(named.kept.map((kept) => `${kept.name} ${kept.verdict}`), ['universal PASS', 'driver FAIL', 'driver-process PASS', 'web PASS']);
  assert.deepEqual(named.fresh, []);

  // GATE6: a verdict a fixed path reaches is not kept: that gate runs again, saying which path, and a named one is just named.
  const passed = { ...results, driver: { verdict: 'PASS', at: '2026-10-04T10:09:00Z' }, 'driver-process': { verdict: 'PASS', at: 'x' }, web: { verdict: 'FAIL', at: 'y' } };
  const fixed = tool.rerunPlan(plan, chosen, passed, ['web'], { universal: ['src/Daoris.Web/e2e/platform.spec.ts'], web: ['src/Daoris.Web/e2e/platform.spec.ts'] });
  assert.deepEqual(fixed.run, ['universal', 'web']);
  assert.deepEqual(fixed.kept.map((kept) => kept.name), ['cli', 'driver', 'driver-process']);
  assert.deepEqual(fixed.stale, [{ name: 'universal', verdict: 'PASS', at: '2026-10-04T10:00:00Z', paths: ['src/Daoris.Web/e2e/platform.spec.ts'] }]);
  assert.deepEqual(fixed.fresh, []);
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
  // Every path reaches every gate here, as it did before GATE6: what a stage forgives without the lane table.
  const judging = {
    tree: 'T', drift: (from: string) => ({ R: ['TASKS.md'], C: ['src/x.cs', 'TASKS.md'] } as Record<string, string[]>)[from] ?? null,
    isRecord: (path: string) => path === 'TASKS.md', reaching: (_gate: string, paths: string[]) => paths,
  };
  const judged = tool.stageJudgement({ ...judging, plan: ['universal', 'cli', 'driver', 'driver-process', 'web', 'deployment', 'rehearse'], verdicts });
  assert.equal(judged.passed, false);
  assert.deepEqual(judged.missing, ['driver', 'web', 'deployment', 'rehearse']);
  const byName = Object.fromEntries(judged.gates.map((gate) => [gate.name, gate]));
  assert.equal(byName['universal']!.exact, true);
  assert.equal(byName['cli']!.exact, false);
  assert.equal(byName['cli']!.verdict!.tree, 'R');
  assert.deepEqual(byName['cli']!.standing, { standing: 'records', paths: [], records: ['TASKS.md'] });
  assert.equal(byName['web']!.verdict!.verdict, 'FAIL');
  assert.equal(byName['rehearse']!.verdict, null);
  assert.equal(byName['rehearse']!.stale, null, 'no verdict anywhere is not a stale one');
  // A stale verdict is said with the paths that made it so, the records left out; one with no tree, with none.
  assert.equal(byName['driver']!.verdict, null);
  assert.deepEqual([byName['driver']!.stale!.standing, byName['driver']!.stale!.paths, byName['driver']!.stale!.verdict.tree], ['stale', ['src/x.cs'], 'C']);
  assert.deepEqual([byName['deployment']!.stale!.standing, byName['deployment']!.stale!.paths], ['stale', []]);

  const all = tool.stageJudgement({ ...judging, plan: ['universal', 'cli'], verdicts });
  assert.deepEqual([all.passed, all.missing], [true, []]);

  // A newer verdict that no longer stands is said only where it explains a gate that does not count.
  const later = tool.stageJudgement({ ...judging, plan: ['service', 'devkit'], verdicts: [
    { gate: 'service', verdict: 'PASS', tree: 'T', at: at(9) },
    { gate: 'service', verdict: 'FAIL', tree: 'C', at: at(11) },
    { gate: 'devkit', verdict: 'FAIL', tree: 'T', at: at(9) },
    { gate: 'devkit', verdict: 'PASS', tree: 'C', at: at(11) },
  ] });
  assert.deepEqual(later.missing, ['devkit']);
  assert.deepEqual(later.gates.map((gate) => [gate.name, gate.verdict!.verdict, gate.stale?.verdict.verdict ?? null]), [
    ['service', 'PASS', null],
    ['devkit', 'FAIL', 'PASS'],
  ]);
});

// ---------------------------------------------------------------------------------------------------
// GATE6: a verdict stays good until a path its gate reaches changes

test('a path reaches a gate as a merge reads the table: the baseline and an unnamed gate by every path, any gate by an unplaced one', () => {
  const reach: Rule[] = [
    { paths: ['web/**'], gates: ['web'], why: 'the page' },
    { paths: ['driver/**'], gates: ['driver', 'driver-process'], why: 'the driver' },
    { paths: ['gates.json'], gates: '*', why: 'how every gate runs' },
    { lane: 'service', gates: ['service'], why: 'a lane this map does not declare places nothing' },
  ];
  const paths = ['web/a.ts', 'driver/b.cs'];
  assert.deepEqual(tool.pathsReaching('web', paths, { reach }), ['web/a.ts']);
  assert.deepEqual(tool.pathsReaching('driver-process', paths, { reach }), ['driver/b.cs']);
  assert.deepEqual(tool.pathsReaching('cli', paths, { reach }), paths, 'the baseline runs at every merge, so every path reaches it');
  assert.deepEqual(tool.pathsReaching('mystery', paths, { reach }), paths, 'a gate no rule names runs at every merge');
  assert.deepEqual(tool.pathsReaching('web', ['gates.json', 'driver/b.cs'], { reach }), ['gates.json']);
  assert.deepEqual(tool.pathsReaching('web', ['elsewhere/c.cs', 'driver/b.cs', 'service/d.cs'], { reach }), ['elsewhere/c.cs', 'service/d.cs'],
    'a path no rule places reaches every gate');
  assert.deepEqual(tool.pathsReaching('web', [], { reach }), []);
});

test("a verdict stands until a path that reaches its gate changes: a browser test keeps the driver's verdicts, a driver change does not", () => {
  const at = (hour: number) => `2026-10-04T${String(hour).padStart(2, '0')}:00:00Z`;
  const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
  const driverCs = 'src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs';
  // The full set passed tree F. W is F with a browser test fixed and the records written; D is F with the driver changed.
  const between: Record<string, string[]> = { 'F>W': [spec, 'TASKS.md', 'docs/decisions/D150.md'], 'F>D': [driverCs] };
  const judging = (tree: string) => ({
    tree,
    drift: (from: string) => between[`${from}>${tree}`] ?? null,
    isRecord: tool.recordMatcher({ lanes: repoLanes, union: tool.unionRecords(repoRoot) }),
    reaching: (gate: string, paths: string[]) => tool.pathsReaching(gate, paths, { lanes: repoLanes }),
  });
  const full: Verdict[] = ALL.map((gate) => ({ gate, verdict: 'PASS', tree: 'F', at: at(9) }));
  const inPlan = (gates: string[]) => ALL.filter((gate) => gates.includes(gate));

  // The browser test reaches the web gate and the baseline; every other verdict stands, the driver's among them.
  const web = tool.stageJudgement({ ...judging('W'), plan: ALL, verdicts: full });
  assert.deepEqual(web.missing, inPlan([...BASE, 'web']));
  const w = Object.fromEntries(web.gates.map((gate) => [gate.name, gate]));
  assert.deepEqual(w['web']!.stale!.paths, [spec], 'which path made it stale, the records forgiven');
  assert.equal(w['web']!.stale!.verdict.at, at(9));
  for (const gate of ['driver', 'driver-process', 'driver-process-git', 'modules', 'modules-process', 'deployment', 'rehearse-family']) {
    assert.equal(w[gate]!.verdict!.tree, 'F', gate);
    assert.deepEqual(w[gate]!.standing, { standing: 'unreached', paths: [spec], records: ['TASKS.md', 'docs/decisions/D150.md'] }, gate);
  }
  // Once those pass again on W, the stage accepts it: no second full run.
  const again = [...full, ...inPlan([...BASE, 'web']).map((gate) => ({ gate, verdict: 'PASS', tree: 'W', at: at(11) }))];
  assert.deepEqual(tool.stageJudgement({ ...judging('W'), plan: ALL, verdicts: again }).missing, []);

  // A driver change voids the driver's verdicts, and every gate it reaches; the page's and the service's stand.
  const driver = tool.stageJudgement({ ...judging('D'), plan: ALL, verdicts: full });
  assert.deepEqual(driver.missing, inPlan([...BASE, 'driver', 'modules', 'driver-process', 'driver-process-git', 'modules-process', 'rehearse-family', 'deployment']));
  const d = Object.fromEntries(driver.gates.map((gate) => [gate.name, gate]));
  assert.deepEqual(d['driver-process']!.stale!.paths, [driverCs]);
  for (const gate of ['service', 'devkit', 'rehearse', 'web']) assert.equal(d[gate]!.standing!.standing, 'unreached', gate);

  // A tree git can no longer compare stands for nothing, and says so with no path.
  assert.deepEqual(tool.verdictStanding({ ...judging('X'), from: 'F', gate: 'web' }), { standing: 'stale', paths: [], records: [] });
  assert.deepEqual(tool.verdictStanding({ ...judging('F'), from: 'F', gate: 'web' }), { standing: 'exact', paths: [], records: [] });
});

test("a stage forgives only known records declared by the steward or union, never gate policy", () => {
  const isRecord = tool.recordMatcher({
    lanes: repoLanes, union: tool.unionRecords(repoRoot),
  });
  for (const path of ['TASKS.md', 'docs/task-archive.md', 'CHANGELOG.md', 'docs/decisions/D115.md', 'docs/FIX-LOG.md', 'docs/README.md', '.claude/knowledge/twins.md']) {
    assert.ok(isRecord(path), path);
  }
  for (const path of ['daoris.lanes.json', 'README.md', 'docs/2026-09-30-parallel-development-design.md', 'canon/CHANGELOG.md', 'src/Daoris.Cli/src/cli.ts', 'tools/merge-branch.mjs']) {
    assert.ok(!isRecord(path), path);
  }
  assert.equal(tool.VERDICTS_FILE, 'local/gate-verdicts.json', 'gitignored, under local/');
});

test('a changed lane map cannot forgive itself or source by broadening its steward lane or union patterns', () => {
  const lanes = repoLanes.map((lane) => lane.steward ? { ...lane, paths: ['**'] } : lane);
  const paths = ['daoris.lanes.json', 'src/Daoris.Web/src/App.tsx', 'TASKS.md'];
  const judgement = tool.stageJudgement({
    plan: ALL, tree: 'changed',
    verdicts: ALL.map((gate) => ({ gate, verdict: 'PASS', tree: 'tested', at: hourOf(9) })),
    drift: () => paths,
    isRecord: tool.recordMatcher({ lanes, union: ['**'] }),
    reaching: (gate, changed) => tool.pathsReaching(gate, changed, { lanes }),
  });
  assert.deepEqual(judgement.missing, ALL, 'editing the map changes what every verdict means');
  for (const gate of judgement.gates) {
    assert.ok(gate.stale?.paths.includes('daoris.lanes.json'), gate.name);
    assert.deepEqual(gate.stale?.records, ['TASKS.md']);
  }
});

// ---------------------------------------------------------------------------------------------------
// GATE6b: a gate re-run on the checkout as it stands, and the smallest command a refusal names

const hourOf = (hour: number) => `2026-10-05T${String(hour).padStart(2, '0')}:00:00Z`;
// T is the checkout; S is a tree before a fix that reaches every gate here.
const fixJudging: Judging = {
  tree: 'T', drift: (from: string) => ({ S: ['src/fix.ts'] } as Record<string, string[]>)[from] ?? null,
  isRecord: () => false, reaching: (_gate: string, paths: string[]) => paths,
};

test('a refusal names the smallest command that would pass the stage: --stale, --rerun naming what failed or never ran, or --full', () => {
  const plan = ['universal', 'cli', 'web', 'deployment'];
  const judge = (verdicts: Verdict[]) => tool.stageJudgement({ ...fixJudging, plan, verdicts }).gates;
  const onT: Verdict[] = plan.map((gate) => ({ gate, verdict: 'PASS', tree: 'T', at: hourOf(9) }));
  const beforeFix = (gate: string): Verdict => ({ gate, verdict: 'PASS', tree: 'S', at: hourOf(9) });

  assert.equal(tool.stageRemedy(judge(onT)), null, 'nothing to name when the stage passes');
  // A fix committed past the full set: the gates it reaches are stale, and --stale runs exactly those.
  const fixed = [...onT.filter((v) => v.gate !== 'cli' && v.gate !== 'web'), beforeFix('cli'), beforeFix('web')];
  assert.deepEqual(tool.stageRemedy(judge(fixed)), { command: tool.STALE_COMMAND, what: 'the 2 stale gates', runs: ['cli', 'web'] });
  assert.deepEqual(tool.stageRemedy(judge([...onT.filter((v) => v.gate !== 'web'), beforeFix('web')]))!.what, 'the stale gate');
  assert.equal(tool.STALE_COMMAND, 'node tools/merge-branch.mjs --stale');
  // One gate flaked on this tree: --rerun names it alone.
  const flaked = onT.map((v) => (v.gate === 'cli' ? { ...v, verdict: 'FAIL' } : v));
  assert.deepEqual(tool.stageRemedy(judge(flaked)), { command: 'node tools/merge-branch.mjs --rerun cli', what: 'the gate it lacks', runs: ['cli'] });
  // A failure, a gate never run and a stale one: --rerun names the first two, and runs the stale one with them.
  const mixed = [...flaked.filter((v) => v.gate !== 'web' && v.gate !== 'deployment'), beforeFix('web')];
  assert.deepEqual(tool.stageRemedy(judge(mixed)), {
    command: 'node tools/merge-branch.mjs --rerun cli deployment', what: 'the 3 gates it lacks', runs: ['cli', 'web', 'deployment'],
  });
  // When that would run every gate anyway, the full set is named.
  assert.deepEqual(tool.stageRemedy(judge([])), { command: tool.FULL_COMMAND, what: 'every gate', runs: plan });
  assert.deepEqual(tool.stageRemedy(judge(onT.map((v) => ({ ...v, verdict: 'FAIL' }))))!.command, tool.FULL_COMMAND);
});

test("a re-run on the checkout runs the named gates and each the stage calls stale, in the plan's order, and keeps every other verdict", () => {
  const plan = ['universal', 'cli', 'devkit', 'web', 'deployment'];
  const gates = tool.stageJudgement({
    ...fixJudging, plan, verdicts: [
      { gate: 'universal', verdict: 'PASS', tree: 'T', at: hourOf(9) },
      { gate: 'cli', verdict: 'FAIL', tree: 'T', at: hourOf(9) },
      // Passed before the fix, then failed on this tree: the newest that stands says FAIL, and it is not stale.
      { gate: 'devkit', verdict: 'PASS', tree: 'S', at: hourOf(9) },
      { gate: 'devkit', verdict: 'FAIL', tree: 'T', at: hourOf(11) },
      { gate: 'web', verdict: 'PASS', tree: 'S', at: hourOf(9) },
    ],
  }).gates;
  const named = tool.checkoutRerun(gates, ['cli']);
  assert.deepEqual(named.run, ['cli', 'web']);
  assert.deepEqual(named.stale, [{ name: 'web', verdict: 'PASS', at: hourOf(9), paths: ['src/fix.ts'] }]);
  assert.deepEqual(named.kept.map((kept) => `${kept.name} ${kept.verdict}`), ['universal PASS', 'devkit FAIL']);
  assert.deepEqual(named.none, ['deployment'], 'a gate with no verdict at all runs only when named');
  // --stale names nothing: the stale gates alone.
  const stale = tool.checkoutRerun(gates, []);
  assert.deepEqual(stale.run, ['web']);
  assert.deepEqual(stale.kept.map((kept) => kept.name), ['universal', 'cli', 'devkit']);
  assert.deepEqual(tool.checkoutRerun(gates, ['deployment']).run, ['web', 'deployment']);
  assert.deepEqual(tool.checkoutRerun(gates, ['deployment']).none, []);
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
  result.ms = 1000;
  await tool.runGates(fx.root, [processGate], fx.root, { run: async () => result, print: (line) => printed.push(line) });
  assert.match(printed[0]!, /PASS\s+driver-process/);
  // PROCPAR1b: the classes run at once, so the wall is the run's own (result.ms), never the classes' sum.
  assert.match(printed[1]!, /slowest classes of driver-process, by their own times; the half's wall was 1 s \(its classes ran at once, so the times do not add up to it\), from driver-process\.trx:/);
  assert.match(printed[2]!, /^\s+1\.\s+4 m 1 s\s+2 tests\s+SlowTests$/);
  assert.match(printed[3]!, /^\s+2\.\s+2 m 0 s\s+1 test\s+Outer\+InnerTests$/);
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// PROCREACH1: a Process half run by the classes a change reaches, each class's verdict kept with its tree

test("a Process half's trx gives each class's verdict: a failed test fails its class, and one that passed alone is its class's flake", () => {
  assert.deepEqual([...tool.classResults(TRX)].map(([name, result]) => `${name} ${result.tests} ${result.failed.join(',')}`),
    ['N.SlowTests 2 N.SlowTests.B', 'N.QuickTests 1 ', 'N.Outer+InnerTests 1 ', 'N.SkippedTests 1 ']);
  assert.deepEqual(tool.classVerdictsOf(TRX, { verdict: 'FAIL' }), [
    { name: 'N.SlowTests', verdict: 'FAIL' }, { name: 'N.QuickTests', verdict: 'PASS' },
    { name: 'N.Outer+InnerTests', verdict: 'PASS' }, { name: 'N.SkippedTests', verdict: 'PASS' },
  ]);
  assert.deepEqual(tool.classVerdictsOf(TRX, { verdict: 'FLAKE', flakes: ['N.SlowTests.B'] })[0], { name: 'N.SlowTests', verdict: 'FLAKE' });
  assert.deepEqual(tool.classVerdictsOf(TRX, { verdict: 'ENDED' }), [], 'a gate the run ended gives no verdict');
  assert.deepEqual(tool.classVerdictsOf('not a trx', { verdict: 'PASS' }), []);
});

test("each class's verdicts are kept beside the gates', keyed by gate and class with the tree each ran on, the newest last and a few each", () => {
  const fx = makeFixture('merge-branch-class-verdicts');
  const at = (hour: number) => `2026-10-22T${String(hour).padStart(2, '0')}:00:00Z`;
  assert.equal(tool.CLASS_VERDICTS_FILE, 'local/class-verdicts.json', "under local/, outside local/scratch/: it outlives a run's logs");
  assert.deepEqual(tool.readClassVerdicts(fx.root), { schema: 1, gates: {} });
  tool.addClassVerdicts(fx.root, 'driver-process', [
    { name: 'N.ATests', verdict: 'PASS', tree: 't1', at: at(1), commit: 'c1' },
    { name: 'N.BTests', verdict: 'FAIL', tree: 't1', at: at(1), commit: 'c1' },
  ]);
  tool.addClassVerdicts(fx.root, 'driver-process', [{ name: 'N.ATests', verdict: 'PASS', tree: 't2', at: at(2), commit: 'c2' }]);
  tool.addClassVerdicts(fx.root, 'driver-process', [{ name: 'N.ATests', verdict: 'FLAKE', tree: 't3', at: at(3), commit: 'c3' }], { keep: 2 });
  tool.addClassVerdicts(fx.root, 'modules-process', [{ name: 'N.ATests', verdict: 'PASS', tree: 't3', at: at(3), commit: 'c3' }]);
  const store = tool.readClassVerdicts(fx.root);
  assert.deepEqual(store.gates['driver-process']!['N.ATests']!.map((each) => `${each.verdict} ${each.tree}`), ['PASS t2', 'FLAKE t3']);
  assert.deepEqual(store.gates['driver-process']!['N.BTests'], [{ verdict: 'FAIL', tree: 't1', at: at(1), commit: 'c1' }]);
  assert.equal(store.gates['modules-process']!['N.ATests']!.length, 1, 'one gate\'s class is not another\'s');
  writeFileSync(join(fx.root, tool.CLASS_VERDICTS_FILE), '{ not json');
  assert.deepEqual(tool.readClassVerdicts(fx.root), { schema: 1, gates: {} }, 'a store that does not read is none: every class runs');
  fx.cleanup();
});

test('a half the CI owes runs the classes a change reaches and keeps the rest; a path every class reads runs it whole; a class with no reach, a failure or none runs', () => {
  const at = (hour: number) => `2026-10-22T${String(hour).padStart(2, '0')}:00:00Z`;
  const now = Date.parse(at(12));
  const classes = ['N.AlphaTests', 'N.BetaTests', 'N.LonelyTests', 'N.ToolTests'];
  const drifts: Record<string, string[]> = {
    t1: ['src/Core/Alpha/AlphaThing.cs'],
    t0: ['src/Core/Alpha/AlphaThing.cs', 'src/Tests/GlobalUsings.cs'],
  };
  // The census, stood in: Alpha's class reads Alpha, Beta's and the tool's read Beta, the lonely class's sources place nothing,
  // and the global usings act on every test.
  const reached = (paths: string[]): Reached => {
    const whole = paths.filter((path) => path.endsWith('GlobalUsings.cs')).map((path) => ({ path, why: `${path} acts on every test unnamed` }));
    const by = new Map<string, string>();
    for (const path of paths) {
      for (const [name, area] of [['N.AlphaTests', 'Alpha'], ['N.BetaTests', 'Beta'], ['N.ToolTests', 'Beta'], ['N.LonelyTests', '']] as const) {
        if (path.startsWith(`src/Core/${area}`) && !by.has(name)) by.set(name, path);
      }
    }
    return { whole, classes: whole.length ? classes : [...by.keys()], by };
  };
  const facts = { classes, tree: 't2', drift: (from: string) => drifts[from] ?? null, reached, now };
  const pass = (tree: string, hour = 10, verdict = 'PASS'): ClassVerdict => ({ verdict, tree, at: at(hour), commit: `${tree}-commit` });
  const every = (entry: ClassVerdict) => Object.fromEntries(classes.map((name) => [name, [entry]]));

  // One module changed since every class passed: its class and the planted class with no reach run, the rest are kept.
  let plan = tool.classPlan({ ...facts, verdicts: every(pass('t1')) });
  assert.equal(plan.whole, null);
  assert.deepEqual(plan.run, [
    { name: 'N.AlphaTests', why: 'src/Core/Alpha/AlphaThing.cs changed since its pass, which reaches it' },
    { name: 'N.LonelyTests', why: 'src/Core/Alpha/AlphaThing.cs changed since its pass, which reaches it' },
  ]);
  assert.deepEqual(plan.kept.map((kept) => `${kept.name} ${kept.verdict.verdict} ${kept.verdict.tree} ${kept.standing}`),
    ['N.BetaTests PASS t1 unreached', 'N.ToolTests PASS t1 unreached']);
  assert.equal(plan.sweep, at(10), 'the oldest verdict it keeps');

  // A file every class reads without naming it changed since: the whole half.
  plan = tool.classPlan({ ...facts, verdicts: every(pass('t0')) });
  assert.match(plan.whole ?? '', /src\/Tests\/GlobalUsings\.cs acts on every test unnamed/);
  assert.deepEqual([plan.run.length, plan.kept, plan.sweep], [4, [], null]);

  // A failure stays owed whatever changed; a class with no verdict runs; a pass on this very tree is kept.
  plan = tool.classPlan({ ...facts, verdicts: {
    'N.AlphaTests': [pass('t1')],
    'N.BetaTests': [pass('t1', 9), pass('t1', 10, 'FAIL')],
    'N.LonelyTests': [pass('t2', 11, 'FLAKE')],
  } });
  assert.deepEqual(plan.run.map((run) => `${run.name}: ${run.why}`), [
    'N.AlphaTests: src/Core/Alpha/AlphaThing.cs changed since its pass, which reaches it',
    'N.BetaTests: it failed on 2026-10-22 10:00 UTC, and a failure runs until it passes',
    'N.ToolTests: it has no verdict of its own yet',
  ]);
  assert.deepEqual(plan.kept.map((kept) => `${kept.name} ${kept.standing}`), ['N.LonelyTests exact']);

  // Past the bound, the oldest kept verdict makes the run a sweep: the whole half.
  plan = tool.classPlan({ ...facts, verdicts: every(pass('t1')), now: Date.parse(at(10)) + tool.SWEEP_BOUND_MS + 60_000 });
  assert.match(plan.whole ?? '', /its oldest kept class verdict \(N\.BetaTests, 2026-10-22 10:00 UTC\) is older than 24 hours: a sweep runs the whole half/);
  assert.equal(tool.SWEEP_BOUND_MS, 24 * 60 * 60 * 1000);
  assert.match(tool.classPlan({ ...facts, verdicts: every(pass('t1')), now: Date.parse(at(11)), bound: 30 * 60_000 }).whole ?? '', /older than 0\.5 hours/);

  // Every class kept while the half is owed (it failed outside a test): the whole half, for a verdict of its own.
  plan = tool.classPlan({ ...facts, verdicts: every(pass('t2')) });
  assert.match(plan.whole ?? '', /every class is kept, but the half owes a verdict/);
  // No class with a pass that stands (a tree git cannot compare): the whole half.
  plan = tool.classPlan({ ...facts, verdicts: every(pass('gone')) });
  assert.match(plan.whole ?? '', /no class has a pass that stands on this tree/);
});

test('the stage refuses a Process half that kept classes on verdicts older than the sweep bound, and the CI owes it; a whole pass of any age stands', () => {
  const now = Date.parse('2026-10-23T12:00:00Z');
  const verdicts: Verdict[] = [
    // A pass that kept 75 classes, the oldest of them from 27 hours before: past the bound.
    { gate: 'driver-process-git', verdict: 'PASS', tree: 'T', at: '2026-10-23T11:00:00Z', classes: { ran: 5, kept: 75, sweep: '2026-10-22T09:00:00Z' } },
    // One whose oldest kept class is 11 hours old: within it.
    { gate: 'driver-process', verdict: 'PASS', tree: 'T', at: '2026-10-23T11:00:00Z', classes: { ran: 5, kept: 95, sweep: '2026-10-23T01:00:00Z' } },
    // The whole half, three days ago, on a tree this one differs from by paths it does not reach: a sweep stands whatever its age.
    { gate: 'modules-process', verdict: 'PASS', tree: 'S', at: '2026-10-20T11:00:00Z', classes: { ran: 19, kept: 0, sweep: '2026-10-20T11:00:00Z' } },
    // A whole pass, then a newer one that kept classes past the bound: the whole one still stands and counts.
    { gate: 'other-process', verdict: 'PASS', tree: 'S', at: '2026-10-20T11:00:00Z' },
    { gate: 'other-process', verdict: 'PASS', tree: 'T', at: '2026-10-23T10:00:00Z', classes: { ran: 1, kept: 3, sweep: '2026-10-21T10:00:00Z' } },
  ];
  const judging = { tree: 'T', drift: (from: string) => (from === 'S' ? ['docs/a.md'] : null), isRecord: () => false, reaching: () => [] };
  const plan = ['driver-process-git', 'driver-process', 'modules-process', 'other-process'];
  const judged = tool.stageJudgement({ ...judging, plan, verdicts, now });
  assert.deepEqual(judged.missing, ['driver-process-git']);
  const git = judged.gates[0] as Judged & { swept?: { verdict: Verdict; sweep: string } };
  assert.deepEqual([git.verdict, git.swept?.sweep, git.swept?.verdict.at], [null, '2026-10-22T09:00:00Z', '2026-10-23T11:00:00Z']);
  assert.equal(judged.gates[3]!.verdict!.tree, 'S', 'the older whole pass counts');
  assert.deepEqual(tool.stageRemedy(judged.gates), { command: 'node tools/merge-branch.mjs --rerun driver-process-git', what: 'the gate it lacks', runs: ['driver-process-git'] });
  assert.deepEqual(tool.ciPlan({ ...judging, plan, verdicts, now }).run, ['driver-process-git'], 'the CI owes it: its next run sweeps it');
  // Within the bound it passes; with no clock given, the stage reads the machine's.
  assert.equal(tool.stageJudgement({ ...judging, plan, verdicts, now: Date.parse('2026-10-23T08:00:00Z') }).passed, true);
  const old: Verdict = { gate: 'old-process', verdict: 'PASS', tree: 'T', at: '2020-01-02T00:00:00Z', classes: { ran: 1, kept: 1, sweep: '2020-01-01T00:00:00Z' } };
  assert.equal(tool.stageJudgement({ ...judging, plan: ['old-process'], verdicts: [old] }).passed, false);
  assert.equal(tool.stageJudgement({ ...judging, plan: ['old-process'], verdicts: [{ ...old, classes: { ran: 1, kept: 1, sweep: 'never' } }] }).passed, false,
    'a sweep whose time does not read is no sweep within the bound');
});

// ---------------------------------------------------------------------------------------------------
// CIPRIO1: a full set beside the merges is a guest on the machine

test('a full set beside lowers its own priority to below normal at its start, and says so when it cannot', () => {
  const asked: [number, number][] = [];
  const said: string[] = [];
  const env: Record<string, string | undefined> = {};
  tool.startBesideRun({ setPriority: (pid, priority) => { asked.push([pid, priority]); }, env, say: (line) => said.push(line) });
  assert.deepEqual(asked, [[0, os.constants.priority.PRIORITY_BELOW_NORMAL]]);
  assert.deepEqual(said, []);

  // A failure to lower is said and the run goes on: the environment is still set.
  const failed: string[] = [];
  const bare: Record<string, string | undefined> = {};
  tool.startBesideRun({ setPriority: () => { throw new Error('access denied'); }, env: bare, say: (line) => failed.push(line) });
  assert.match(failed.join('\n'), /could not lower its priority \(access denied\)/);
  assert.equal(bare.MSBUILDDISABLENODEREUSE, '1');
});

test('the gates beside get their own build servers: none reused, none borrowed, none left for the owner\'s builds', () => {
  assert.deepEqual({ ...tool.BESIDE_ENVIRONMENT }, { MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false' });
  const env: Record<string, string | undefined> = { KEEP: 'x' };
  tool.startBesideRun({ setPriority: () => {}, env, say: () => {} });
  assert.deepEqual(env, { KEEP: 'x', MSBUILDDISABLENODEREUSE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', UseSharedCompilation: 'false' });
});

test('a gate step started after the run lowered itself runs at the lowered priority', async () => {
  const fx = makeFixture('merge-branch-priority');
  const before = os.getPriority(0);
  try {
    assert.equal(tool.lowerPriority(), null);
    const lowered = os.getPriority(0);
    // At least below normal: a run beside already lowered itself, so its tool-tests start there (the CI of 2026-10-19).
    assert.ok(lowered >= os.constants.priority.PRIORITY_BELOW_NORMAL, `lowered from ${before} to ${lowered}`);
    const log = join(fx.root, 'priority.log');
    writeFileSync(log, '');
    const fd = openSync(log, 'a');
    const code = await tool.runStep('node -e "console.log(\'priority=\' + require(\'os\').getPriority())"', fx.root, fd);
    closeSync(fd);
    assert.equal(code, 0);
    assert.match(readFileSync(log, 'utf8'), new RegExp(`priority=${lowered}\\b`));
  } finally {
    os.setPriority(0, before);
    fx.cleanup();
  }
});

// FAMILYFAIL1b: the rehearsals' bounds are wall-clock, so in a run beside they start at normal priority

test('a run beside raises a rehearsal step\'s child to normal, touches no suite\'s, and says once when it cannot', async () => {
  const fx = makeFixture('merge-branch-raise');
  try {
    const asked: [number, number][] = [];
    tool.startBesideRun({ setPriority: (pid, priority) => { asked.push([pid, priority]); }, env: {}, say: () => {} });
    const raise = tool.rehearsalRaise();
    assert.ok(raise);
    raise(4242);
    assert.deepEqual(asked.slice(-1), [[4242, os.constants.priority.PRIORITY_NORMAL]]);

    // runGate hands a rehearsal's step the raise and a suite's none.
    const given: Record<string, boolean> = {};
    const step: Step = async (command, _cwd, _fd, options) => { given[command] = Boolean(options?.raise); return 0; };
    await tool.runGate(fx.root, planned('rf', 'npm run rehearse:family'), fx.root, { step });
    await tool.runGate(fx.root, planned('s', 'dotnet test src/S'), fx.root, { step });
    assert.deepEqual(given, { 'npm run rehearse:family': true, 'dotnet test src/S': false });

    // FAMILYFAIL1c: a Process half's bounds are wall-clock too, so its full run and each test it runs alone get the raise;
    // the fast half beside them does not.
    const raised: string[] = [];
    const flaky = 'Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked';
    const alone = 'Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)\n';
    const processStep: Step = async (command, _cwd, fd, options) => {
      if (options?.raise) raised.push(command.includes('--filter') ? 'alone' : 'full');
      writeSync(fd, command.includes('--filter') ? alone : PROCESS_FAILURE);
      return command.includes('--filter') ? 0 : 1;
    };
    const half = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
    assert.equal((await tool.runGate(fx.root, half, fx.root, { step: processStep })).verdict, 'FLAKE');
    assert.deepEqual(raised, ['full', 'alone'], `${flaky} was run alone without the raise`);
    let fastRaised = false;
    await tool.runGate(fx.root, planned('fast', 'dotnet test src/D --filter Category!=Process'), fx.root, {
      step: async (_command, _cwd, _fd, options) => { fastRaised = Boolean(options?.raise); return 0; },
    });
    assert.equal(fastRaised, false);

    // FAMILYFAIL1d: a gate the repository marks clock-bound (`"clock": true`) is raised too, though it is no Process half:
    // the CLI's suites hold real processes on wall-clock bounds. The mark is read from daoris.gates.json and planned.
    writeFileSync(join(fx.root, 'daoris.gates.json'), JSON.stringify({ gates: [
      { name: 'cli', run: 'npm run verify', clock: true },
      { name: 'light', run: 'node tools/check.mjs' },
    ] }));
    const plan = tool.readPlan(fx.root);
    assert.equal(plan.find((gate) => gate.name === 'cli')?.clock, true);
    assert.equal('clock' in (plan.find((gate) => gate.name === 'light') ?? {}), false);
    let clockRaised = false;
    await tool.runGate(fx.root, plan.find((gate) => gate.name === 'cli')!, fx.root, {
      step: async (_command, _cwd, _fd, options) => { clockRaised = Boolean(options?.raise); return 0; },
    });
    assert.equal(clockRaised, true);

    // A run that could not lower itself raises nothing; one whose raise fails says so once.
    tool.startBesideRun({ setPriority: () => { throw new Error('denied'); }, env: {}, say: () => {} });
    assert.equal(tool.rehearsalRaise(), null);
    const said: string[] = [];
    let lowered = false;
    tool.startBesideRun({ setPriority: (pid) => { if (pid !== 0) throw new Error('denied'); lowered = true; }, env: {}, say: (line) => said.push(line) });
    assert.ok(lowered);
    const again = tool.rehearsalRaise();
    assert.ok(again);
    again(1);
    again(2);
    assert.equal(said.length, 1);
    assert.match(said[0] ?? '', /could not raise a rehearsal to normal priority \(denied\)/);
  } finally {
    tool.startBesideRun({ setPriority: () => { throw new Error('reset'); }, env: {}, say: () => {} });
    fx.cleanup();
  }
});

test('in a real spawn, a rehearsal step\'s grandchild starts at normal and a suite step\'s stays lowered', async () => {
  const fx = makeFixture('merge-branch-raise-real');
  const before = os.getPriority(0);
  try {
    assert.equal(tool.lowerPriority(), null);
    tool.startBesideRun({ setPriority: os.setPriority, env: {}, say: () => {} });
    const command = `node -e "require('child_process').execFileSync(process.execPath, ['-e', 'console.log(\\"grand=\\" + require(\\"os\\").getPriority())'], {stdio: 'inherit'})"`;
    const grand = async (run: string) => {
      const gate = planned('g', run);
      await tool.runGate(fx.root, gate, fx.root, {
        step: (_cmd, cwd, fd, options) => tool.runStep(command, cwd, fd, options),
      });
      return Number(/grand=(-?\d+)/.exec(readFileSync(join(fx.root, 'g.log'), 'utf8'))?.[1]);
    };
    // The CI itself runs below normal, so the grandchild is compared with the constants, not the starting value.
    assert.equal(await grand('npm run rehearse'), os.constants.priority.PRIORITY_NORMAL);
    assert.equal(await grand('dotnet test src/S'), os.constants.priority.PRIORITY_BELOW_NORMAL);
  } finally {
    os.setPriority(0, before);
    tool.startBesideRun({ setPriority: () => { throw new Error('reset'); }, env: {}, say: () => {} });
    fx.cleanup();
  }
});

test('a run of main\'s tip stops before its next gate once a newer tip supersedes it, and is recorded superseded, never passed', async () => {
  let tip: string | null = 'aaaaaaa1';
  const watch = tool.watchTip('aaaaaaa1', () => tip);
  const gates = ['a', 'b', 'c'].map((name) => planned(name, `dotnet build src/S/${name}`));
  const lanes = laneRun(['a', 'b', 'c']);
  const printed: string[] = [];
  const results = await tool.runGates('root', gates, 'logs', {
    keepGoing: true,
    run: async (root, gate, at, beside) => {
      const result = await lanes.run(root, gate, at, beside);
      tip = 'bbbbbbb2';
      return result;
    },
    halt: watch.check,
    print: (line) => printed.push(line),
  });
  assert.deepEqual(results.map((result) => result.gate.name), ['a'], 'gate b never ran');
  assert.deepEqual(lanes.events, ['start a', 'end a']);
  assert.deepEqual(watch.superseded, { from: 'aaaaaaa1', to: 'bbbbbbb2' });
  assert.match(printed.at(-1)!, /NOT RUN\s+c/);
  const lines = tool.supersededLines(watch.superseded!, ['a'], ['b', 'c']).join('\n');
  assert.match(lines, /superseded/);
  assert.match(lines, /aaaaaaa.*bbbbbbb/);
  assert.match(lines, /ran: a/);
  assert.match(lines, /left: b, c/);
  assert.deepEqual(tool.besideOutcome(1, watch.superseded), { passed: false, superseded: 'bbbbbbb2' });
  assert.deepEqual(tool.besideOutcome(0, null), { passed: true, superseded: null });
  // Even a run whose gates all passed is not passed once it was superseded.
  assert.equal(tool.besideOutcome(0, watch.superseded).passed, false);
});

test('a run given a named commit is never superseded: no commit to watch, no tip read, none polled', async () => {
  let reads = 0;
  let moved = 0;
  const watch = tool.watchTip(null, () => { reads += 1; return 'bbbbbbb2'; }, { onMove: () => { moved += 1; } });
  assert.equal(watch.check(), false);
  // CIPRIO1c: nor polled while its gates run.
  const unwatch = watch.poll(5);
  await sleep(50);
  unwatch();
  assert.equal(reads, 0);
  assert.equal(moved, 0);
  assert.equal(watch.superseded, null);
  // A tip that cannot be read is not a newer one.
  assert.equal(tool.watchTip('aaaaaaa1', () => null).check(), false);
  // The same tip is not newer.
  assert.equal(tool.watchTip('aaaaaaa1', () => 'aaaaaaa1').check(), false);
});

// CIPRIO1c: a superseded run ended only before its next gate, so a gate on an old tip (a Process half, a rehearsal) ran
// to its end, up to an hour of the machine's time beside five workers, for a verdict nobody would use

const sleep = (ms: number) => new Promise((done) => setTimeout(done, ms));

/** Whether a process still runs: signal 0 asks and sends nothing. */
const alive = (pid: number): boolean => {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === 'EPERM';
  }
};

// A gate with a tree under it: it starts a grandchild, each writes its pid into the folder named (argument or TREE_PIDS),
// and both wait ten minutes, far past any wait here, so only an end from outside stops them.
const TREE = [
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
async function treePids(at: string, ms = 20_000): Promise<number[]> {
  const files = ['child.pid', 'grandchild.pid'].map((name) => join(at, name));
  const read = (file: string) => (existsSync(file) ? Number(readFileSync(file, 'utf8')) : 0);
  for (let waited = 0; waited < ms && !files.every((file) => read(file) > 0); waited += 100) await sleep(100);
  const pids = files.map(read);
  assert.ok(pids.every((pid) => pid > 0 && alive(pid)), `the gate's child and grandchild never both ran: ${pids.join(', ')}`);
  return pids;
}

/** Waits for every pid to have ended, a little patience for an exit the system is still recording: those still running. */
async function stillRunning(pids: number[], ms = 10_000): Promise<number[]> {
  for (let waited = 0; waited < ms && pids.some(alive); waited += 100) await sleep(100);
  return pids.filter(alive);
}

/**
 * A scratch folder removed just after its processes were killed: Windows may still hold it a moment, and `rmSync` does not
 * wait on a denied access as it does on a busy one.
 */
async function cleanupAfterKill(fx: { cleanup(): void }): Promise<void> {
  for (let tries = 1; ; tries += 1) {
    try {
      fx.cleanup();
      return;
    } catch (error) {
      if (tries >= 40 || !['EPERM', 'EBUSY', 'EACCES'].includes((error as NodeJS.ErrnoException).code ?? '')) throw error;
      await sleep(250);
    }
  }
}

test('a run that ends its trees ends a running step\'s whole tree, its shell, child and grandchild, and the step answers ended', async () => {
  const fx = makeFixture('merge-branch-end-tree');
  const script = join(fx.root, 'tree.mjs');
  writeFileSync(script, TREE);
  const log = join(fx.root, 'step.log');
  writeFileSync(log, '');
  const fd = openSync(log, 'a');
  let trees: Trees | null = null;
  const pids: number[] = [];
  try {
    trees = tool.stepTrees();
    const ran = tool.runStep(`node "${script}" "${fx.root}"`, fx.root, fd, { trees });
    pids.push(...await treePids(fx.root));
    trees.end('main moved from aaaaaaa to bbbbbbb');
    let code = 0;
    await within(ran.then((answer) => { code = answer; }), 'the ended step answering', 15_000);
    assert.equal(code, tool.STEP_ENDED, 'a code of its own, never a failure\'s');
    assert.notEqual(tool.STEP_ENDED, 0);
    assert.deepEqual(await stillRunning(pids), [], 'every process the step started has ended');
    assert.equal(trees.ended, 'main moved from aaaaaaa to bbbbbbb');
  } finally {
    trees?.end('the test ended');
    for (const pid of pids) if (alive(pid)) process.kill(pid);
    await stillRunning(pids);
    closeSync(fd);
    await cleanupAfterKill(fx);
  }
});

// CIPRIO1d: killed outright (no SIGINT), the run's node leaves its steps' shells and everything under them running
const PARENT = [
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

test('a run killed outright leaves no gate tree: the next run taking its lock ends what the dead pid started (CIPRIO1d)', { skip: process.platform !== 'win32' }, async () => {
  const fx = makeFixture('merge-branch-killed-run');
  const tree = join(fx.root, 'tree.mjs');
  const parent = join(fx.root, 'parent.mjs');
  const at = join(fx.root, 'pids');
  mkdirSync(at, { recursive: true });
  writeFileSync(tree, TREE);
  writeFileSync(parent, PARENT);
  const run = spawn(process.execPath, [parent, fx.root, tree, at], { stdio: 'ignore', windowsHide: true });
  const pids: number[] = [];
  try {
    pids.push(...await treePids(at));
    assert.ok(alive(run.pid!));
    process.kill(run.pid!, 'SIGKILL');
    await stillRunning([run.pid!]);
    const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40) });
    assert.equal(taken.stale?.pid, run.pid);
    assert.ok((taken.stale?.ended.length ?? 0) > 0, 'the shell the dead run started was ended');
    assert.deepEqual(await stillRunning(pids), [], 'the shell, child and grandchild have ended');
  } finally {
    for (const pid of [run.pid!, ...pids]) if (alive(pid)) process.kill(pid);
    await stillRunning([run.pid!, ...pids]);
    await cleanupAfterKill(fx);
  }
});

// CIPRIO1e: a non-detached shell ends with its killed parent here, and its child runs on under a dead parent id, which a
// listing by the dead pid cannot find: the gate tree's folder in the command line does.
const SHELL_PARENT = [
  "import { spawn } from 'node:child_process';",
  "import { mkdirSync, writeFileSync } from 'node:fs';",
  "const [home, tree, at] = process.argv.slice(2);",
  "mkdirSync(home + '/local', { recursive: true });",
  "writeFileSync(home + '/local/gate-tree.lock', JSON.stringify({ pid: process.pid, commit: 'a'.repeat(40), at: new Date().toISOString() }) + '\\n');",
  'spawn(`"${process.execPath}" "${tree}" "${at}"`, { shell: true, stdio: \'ignore\', windowsHide: true });',
  'setTimeout(() => {}, 600000);',
  '',
].join('\n');

test('a run killed outright leaves no orphan naming the gate tree: the takeover ends what runs on under a dead parent (CIPRIO1e)', { skip: process.platform !== 'win32' }, async () => {
  const fx = makeFixture('merge-branch-killed-shell');
  const tree = join(fx.root, 'tree.mjs');
  const parent = join(fx.root, 'parent.mjs');
  const at = join(fx.root, 'local', 'gate-tree', 'pids');
  mkdirSync(at, { recursive: true });
  writeFileSync(tree, TREE);
  writeFileSync(parent, SHELL_PARENT);
  const run = spawn(process.execPath, [parent, fx.root, tree, at], { stdio: 'ignore', windowsHide: true });
  const pids: number[] = [];
  try {
    pids.push(...await treePids(at));
    process.kill(run.pid!, 'SIGKILL');
    await stillRunning([run.pid!]);
    assert.ok(pids.every(alive), 'the shell\'s child and grandchild outlive the killed run');
    const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40) });
    assert.equal(taken.stale?.pid, run.pid);
    // The shell itself may be the one stranded (its parent dead, its command line naming the tree), the child going with it.
    assert.ok((taken.stale?.ended.length ?? 0) > 0, 'the orphan was ended');
    assert.deepEqual(await stillRunning(pids), [], 'the orphan and its grandchild have ended');
  } finally {
    for (const pid of [run.pid!, ...pids]) if (alive(pid)) process.kill(pid);
    await stillRunning([run.pid!, ...pids]);
    await cleanupAfterKill(fx);
  }
});

test('a step\'s tree is ended by its own pid: taskkill /T on Windows, every descendant ps lists elsewhere, never by image name', () => {
  const calls: [string, string[]][] = [];
  const killed: number[] = [];
  tool.endTree(4242, { platform: 'win32', run: (command, args) => { calls.push([command, args]); return {}; } });
  assert.deepEqual(calls, [['taskkill', ['/T', '/F', '/PID', '4242']]]);
  // Elsewhere a kill reaches the one process it names, and a shell's command outlives it: `ps` says what is under it.
  const listing = ['    1     0', '   10     1', '   11    10', '   12    11', '   13    10', '   20     1', ''].join('\n');
  assert.deepEqual([...tool.descendants(listing, 10)].sort((a, b) => a - b), [11, 12, 13]);
  assert.deepEqual(tool.descendants(listing, 20), []);
  calls.length = 0;
  tool.endTree(10, {
    platform: 'linux',
    run: (command, args) => { calls.push([command, args]); return { stdout: listing }; },
    kill: (pid) => { killed.push(pid); },
  });
  assert.deepEqual(calls.map(([command]) => command), ['ps']);
  assert.deepEqual([...killed].sort((a, b) => a - b), [10, 11, 12, 13]);
});

test('a gate whose step the run ended reads ENDED, never FAIL, and nothing of it starts after: no step before it, no rehearsal run again, no test run alone', async () => {
  const fx = makeFixture('merge-branch-ended-gate');
  const why = 'main moved from aaaaaaa to bbbbbbb';
  try {
    const gateOf = async (gate: Gate, step: (trees: Trees) => Step) => {
      const trees = tool.stepTrees({ end: () => {} });
      const ran: string[] = [];
      const handed: unknown[] = [];
      const inner = step(trees);
      const result = await tool.runGate(fx.root, gate, fx.root, {
        trees,
        step: (command, cwd, fd, options) => { ran.push(command); handed.push(options?.trees); return inner(command, cwd, fd, options); },
      });
      assert.ok(handed.every((given) => given === trees), 'every step is handed the run\'s trees, so its child is held');
      return { result, ran, log: readFileSync(result.log, 'utf8') };
    };
    const endedAt = (at: string, code = tool.STEP_ENDED) => (trees: Trees): Step => async (command) => {
      if (command !== at) return 0;
      trees.end(why);
      return code;
    };
    const isEnded = ({ result, log }: { result: GateResult; log: string }) => {
      assert.equal(result.verdict, 'ENDED');
      assert.match(result.note, /ended, not failed: main moved from aaaaaaa to bbbbbbb/);
      assert.match(log, /\(ended, not failed: main moved from aaaaaaa to bbbbbbb\)/);
      assert.doesNotMatch(log, /\(exited -?\d+\)\s*$/);
    };

    // Ended while it ran: a rehearsal is not run again.
    const running = await gateOf(planned('rf', 'npm run rehearse:family'), endedAt('npm run rehearse:family'));
    isEnded(running);
    assert.deepEqual(running.ran, ['npm run rehearse:family']);
    assert.equal(existsSync(join(fx.root, 'rf.rerun.log')), false);

    // A step that answered a failure of its own once the run had ended it (a person's Ctrl+C reaches the console's
    // processes too) is ended, not failed, and not run again: it printed nothing, which alone would run it again.
    const own = await gateOf(planned('rf', 'npm run rehearse:family'), endedAt('npm run rehearse:family', 1));
    isEnded(own);
    assert.deepEqual(own.ran, ['npm run rehearse:family']);
    assert.equal(existsSync(join(fx.root, 'rf.rerun.log')), false);

    // A Process half that failed on its own, then waited to run its failure alone while main moved: nothing runs alone.
    const half = planned('driver-process', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings');
    const trees = tool.stepTrees({ end: () => {} });
    const alone: string[] = [];
    const waited = await tool.runGate(fx.root, half, fx.root, {
      trees,
      settled: async () => { trees.end(why); },
      step: async (command, _cwd, fd) => {
        if (command.includes('--filter')) alone.push(command);
        writeSync(fd, PROCESS_FAILURE);
        return 1;
      },
    });
    assert.equal(waited.verdict, 'ENDED');
    assert.match(waited.note, /exit 1.*ended, not failed: main moved/);
    assert.deepEqual(alone, []);
    assert.equal(existsSync(join(fx.root, 'driver-process.rerun.log')), false);

    // Once the run has ended its trees, a gate asked to run starts no step at all.
    const late = await gateOf(planned('s', 'dotnet build src/S'), (ended) => { ended.end(why); return async () => 0; });
    assert.equal(late.result.verdict, 'ENDED');
    assert.deepEqual(late.ran, []);
    assert.match(late.log, /not started: dotnet build src\/S/);
  } finally {
    fx.cleanup();
  }
});

test('a run of main\'s tip polls the tip while a gate runs and, on the first move, ends what it is running and starts nothing more', async () => {
  const fx = makeFixture('merge-branch-poll');
  let tip = 'aaaaaaa1';
  const endedPids: number[] = [];
  let release = () => {};
  const trees = tool.stepTrees({ end: (pid) => { endedPids.push(pid); release(); } });
  const moves: string[] = [];
  const watch = tool.watchTip('aaaaaaa1', () => tip, {
    onMove: (moved) => {
      moves.push(moved.to);
      trees.end(`main moved from ${moved.from.slice(0, 7)} to ${moved.to.slice(0, 7)}`);
    },
  });
  const unwatch = watch.poll(20);
  try {
    // b holds a pid as a real step holds its child, and main moves while it runs, so only the poll can see it.
    const step: Step = (command, _cwd, _fd, options) => new Promise((done) => {
      if (!command.endsWith(' b')) return done(0);
      const held = options!.trees!;
      held.hold(4242);
      release = () => done(held.release(4242) ? tool.STEP_ENDED : 0);
      tip = 'bbbbbbb2';
    });
    const gates = ['a', 'b', 'c'].map((name) => planned(name, `node gate.mjs ${name}`));
    const printed: string[] = [];
    let results: GateResult[] = [];
    await within(tool.runGates(fx.root, gates, fx.root, {
      oneAtATime: true,
      run: (root, gate, dir, beside) => tool.runGate(root, gate, dir, { ...beside, step, trees }),
      halt: () => Boolean(trees.ended) || watch.check(),
      print: (line) => printed.push(line),
    }).then((answer) => { results = answer; }), 'the gate running ended by the poll');
    assert.deepEqual(results.map((result) => `${result.gate.name} ${result.verdict}`), ['a PASS', 'b ENDED']);
    assert.deepEqual(endedPids, [4242]);
    assert.deepEqual(moves, ['bbbbbbb2'], 'said once, however often it is asked after');
    assert.deepEqual(watch.superseded, { from: 'aaaaaaa1', to: 'bbbbbbb2' });
    assert.match(printed.join('\n'), /ENDED\s+b\s.*ended, not failed: main moved from aaaaaaa to bbbbbbb/);
    assert.match(printed.at(-1)!, /NOT RUN\s+c/);
    assert.doesNotMatch(printed.join('\n'), /FAIL/);

    const lines = tool.supersededLines(watch.superseded!, ['a'], ['c'], ['b']).join('\n');
    assert.match(lines, /superseded: main moved from aaaaaaa to bbbbbbb, so this run ended the gates it was running and started no more; it is not a pass/);
    assert.match(lines, /ran: a\n\s+ended: b\n\s+left: c/);
    // The cadence is a few seconds: a gate on an old tip runs that long at most, for a git read every few seconds.
    assert.ok(tool.TIP_POLL_MS >= 1000 && tool.TIP_POLL_MS <= 10_000, `${tool.TIP_POLL_MS} ms`);
  } finally {
    unwatch();
    fx.cleanup();
  }
});

test('a person\'s stop (Ctrl+C) ends the trees the run has running and is heard once; a second exits as it always did', () => {
  const ended: number[] = [];
  const trees = tool.stepTrees({ end: (pid) => ended.push(pid) });
  trees.hold(7);
  const said: string[] = [];
  const terminal = new EventEmitter();
  const unhook = tool.stopOnInterrupt(trees, { on: terminal, say: (line) => said.push(line) });
  assert.equal(terminal.listenerCount('SIGINT'), 1);
  terminal.emit('SIGINT');
  assert.deepEqual(ended, [7]);
  assert.match(trees.ended ?? '', /a person stopped the run/);
  assert.match(said.join('\n'), /stopped: ending the gates running/);
  assert.equal(terminal.listenerCount('SIGINT'), 0, 'once: a second Ctrl+C is the default exit');
  unhook();

  // A run that ends with no stop leaves no listener behind.
  const quiet = new EventEmitter();
  const off = tool.stopOnInterrupt(tool.stepTrees({ end: () => {} }), { on: quiet, say: () => {} });
  off();
  assert.equal(quiet.listenerCount('SIGINT'), 0);

  const lines = tool.stoppedLines(['a'], ['c'], ['b']).join('\n');
  assert.match(lines, /stopped: a person stopped this run, so it ended the gates it was running and started no more; it is not a pass/);
  assert.match(lines, /ran: a\n\s+ended: b\n\s+left: c/);
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

const TRAILER = '\n\nCo-Authored-By: Claude Fixture (Anthropic) <fixture@example.test>';

/**
 * CLEANUP2b: a worktree as a finished agent's is, nothing in it or in its git folder written for `minutes`: every
 * entry's times set back (links are not followed), and the files of the git folder a worktree keeps beside the repository.
 */
function age(worktree: string, minutes: number): void {
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
// Given GATE_HOLD, it waits (ten minutes at most, well past any test's own wait: load made a minute too short, TESTWAIT4) until that file is written, as a long gate runs while a test acts beside it.
const GATE = [
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

// CI1: a gate for the CI's runs, which write their logs in the main checkout and leave the gate tree's `local/` unmade: it
// makes its own, notes its name, and fails where CI_FAIL names it, as a change that broke it would.
const CI_GATE = [
  "import { appendFileSync, mkdirSync } from 'node:fs';",
  'const name = process.argv[2];',
  "mkdirSync('local', { recursive: true });",
  "appendFileSync('local/ran.txt', name + '\\n');",
  'for (let i = 1; i <= 3; i++) console.log(`${name} line ${i}`);',
  "process.exit((process.env.CI_FAIL ?? '').split(',').includes(name) ? 1 : 0);",
  '',
].join('\n');

/**
 * PROCREACH1: `dotnet test`, faked for a Process half run by reach. It reads the filter of the settings it is handed, runs each
 * class of the project's `*Tests.cs` that no `FullyQualifiedName!~<class>.` keeps out, notes which in `local/ran.txt`, and writes
 * the trx the merge tool asked for, a test a class, as the real one does.
 */
const FAKE_DOTNET_REACH = [
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

/** PROCREACH1: a desktop of its own for the CI's runs by reach: a test project compiling against two areas, and three Process classes. */
const DESK_FILES: Record<string, string> = {
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

/** CI1: what a test waits for, read again until it holds, failing rather than hanging when it never does. */
async function waitFor(holds: () => boolean, what: string, ms = 120_000): Promise<void> {
  for (const end = Date.now() + ms; !holds();) {
    if (Date.now() > end) throw new Error(`never happened: ${what}`);
    await new Promise((done) => setTimeout(done, 100));
  }
}

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

/** What ran, or what was recorded, in name order: light gates that build nothing in common run side by side (GATELANES2). */
const unordered = (list: string[]): string[] => [...list].sort();

interface Scratch {
  root: string;
  /** `cwd`: where the tool runs, the repository's main checkout unless named (GATEWT1 runs it in the gate tree). */
  run(args: string[], extra?: Record<string, string>, cwd?: string): Promise<{ status: number | null; out: string }>;
  ran(): string[];
  cleanup(): void;
}

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
    run(args, extra = {}, cwd = root) {
      const env: Record<string, string | undefined> = {};
      for (const [key, value] of Object.entries(process.env)) {
        if (key.toUpperCase() !== 'PATH' && key !== 'NODE_TEST_CONTEXT') env[key] = value;
      }
      env['PATH'] = `${bin}${delimiter}${process.env['PATH'] ?? process.env['Path'] ?? ''}`;
      // GATESLIM1: a merge blocks on the checks alone by default, so the tests that watch gates run read the merge with --gated;
      // `BY_DEFAULT` in `extra` is the default itself, and the verbs that name their own gates take no flag.
      const named = args.some((arg) => ['--full', '--rerun', '--stale', '--passed', '--prune', '--gated', '--ci'].includes(arg));
      const given = named || extra['BY_DEFAULT'] !== undefined ? args : [...args, '--gated'];
      // MEMGATE1: a scratch's heavy gates never read the machine's memory, so no test here waits on what the machine holds.
      const child = spawn(process.execPath, [TOOL, ...given], { cwd, env: { ...env, ...GIT_ENV, MERGE_BRANCH_MEMORY: 'off', ...extra } });
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
  test('MAKERTRAIL1 a Codex stand-in commit passes the real git trailer check', async () => {
    const repo = scratch('codex-attribution', [{ name: 'first', run: 'node gate.mjs first' }]);
    try {
      branch(repo.root, 'codex-work', { 'codex.txt': 'built by the stand-in\n' });
      git(repo.root, 'checkout', '--quiet', 'codex-work');
      git(repo.root, 'commit', '--amend', '--quiet', '-m', 'fix: Codex stand-in\n\nCo-Authored-By: Codex (OpenAI, gpt-fixture) <noreply@openai.com>');
      git(repo.root, 'checkout', '--quiet', 'main');
      const result = await repo.run(['--plan', 'codex-work']);
      assert.equal(result.status, 0, result.out);
      assert.doesNotMatch(result.out, /no Co-Authored-By|no allowed maker/);
      assert.deepEqual(repo.ran(), []);
      assert.equal(merging(repo.root), false);
    } finally {
      repo.cleanup();
    }
  });

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

  test('by default a merge runs the blocking checks alone and lists every other gate it reaches as "in the CI beside" (GATESLIM1)', async () => {
    const repo = scratch('by-default', [
      { name: 'first', run: 'node gate.mjs first' },
      { name: 'universal', run: 'node gate.mjs universal' },
    ], {});
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n' });
    const plan = await repo.run(['--plan', 'feature/one'], { BY_DEFAULT: '1' });
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /first[^\n]*in the CI beside/);
    const result = await repo.run(['feature/one'], { BY_DEFAULT: '1' });
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /in the CI beside/);
    assert.deepEqual(repo.ran(), ['universal'], 'only the blocking check ran');
    repo.cleanup();
    // OVERVIEWNOISE1a: a scratch repository left under `_fixtures/` kept a landed session's tree from the tidy.
    assert.equal(existsSync(dirname(repo.root)), false, 'the case left its scratch repository');
  });

  test("a merge whose two sides typecheck but not together stops at the web's typecheck, before any commit (MERGETSC1)", async () => {
    // The real compiler on a page of two files: main renames a helper, the branch names it by its old name.
    const tsc = join(repoRoot, 'node_modules', 'typescript', 'bin', 'tsc');
    const typecheck = [
      "import { spawnSync } from 'node:child_process';",
      "import { appendFileSync } from 'node:fs';",
      "appendFileSync('local/ran.txt', 'web-types\\n');",
      `const run = spawnSync(process.execPath, [${JSON.stringify(tsc)}, '--noEmit', '-p', 'src/Daoris.Web'], { stdio: 'inherit' });`,
      'process.exit(run.status ?? 2);',
      '',
    ].join('\n');
    const repo = scratch('typecheck', [
      { name: 'universal', run: 'node gate.mjs universal' },
      { name: 'web-types', run: 'node typecheck.mjs' },
    ], {
      'typecheck.mjs': typecheck,
      'src/Daoris.Web/tsconfig.json': `${JSON.stringify({ compilerOptions: { strict: true, noEmit: true, module: 'ESNext', moduleResolution: 'bundler', target: 'ES2022', types: [] }, include: ['src'] })}\n`,
      'src/Daoris.Web/src/rail.ts': 'export const railLine = (text: string): string => text.trim();\n',
    });
    branch(repo.root, 'feature/notes', { 'notes.txt': 'x\n' });
    branch(repo.root, 'feature/story', { 'src/Daoris.Web/src/story.ts': "import { railLine } from './rail';\nexport const story = railLine(' a ');\n" });
    writeFileSync(join(repo.root, 'src/Daoris.Web/src/rail.ts'), 'export const callLine = (text: string): string => text.trim();\n');
    git(repo.root, 'commit', '--quiet', '-am', `rename the helper${TRAILER}`);

    // A merge that changes nothing the typecheck reads lists it in the CI beside and does not run it.
    const plain = await repo.run(['--plan', 'feature/notes'], { BY_DEFAULT: '1' });
    assert.equal(plain.status, 0, plain.out);
    assert.match(plain.out, /web-types[^\n]*in the CI beside/);

    // The plan names it among the checks a merge blocks on, and why.
    const plan = await repo.run(['--plan', 'feature/story'], { BY_DEFAULT: '1' });
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /web-types[^\n]*blocks the merge: src\/Daoris\.Web\/src\/story\.ts/);

    const head = git(repo.root, 'rev-parse', 'HEAD');
    const result = await repo.run(['feature/story'], { BY_DEFAULT: '1' });
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /FAIL\s+web-types/);
    assert.match(readFileSync(join(repo.root, 'local', 'scratch', 'merge-feature-story', 'web-types.log'), 'utf8'), /railLine/, 'the compiler said what broke');
    assert.deepEqual(repo.ran().sort(), ['universal', 'web-types'], 'the light gates run side by side');
    assert.equal(git(repo.root, 'rev-parse', 'HEAD'), head, 'nothing was committed');
    git(repo.root, 'merge', '--abort');
    repo.cleanup();
  });

  test('a clean merge prints its lanes, runs every gate, keeps each whole output, and never commits', async () => {
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
    // own lane table, which names neither gate here, so both run: side by side, as they build nothing (GATELANES2).
    assert.deepEqual(unordered(repo.ran()), ['first', 'second']);
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

  test("the backlog's line conflict is resolved by the merge base and gated, and one row both sides edited is left (MERGETASKS1)", async () => {
    const rows = ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A2** two.', '- [ ] **A3** three.', ''].join('\n');
    const repo = scratch('backlog', [{ name: 'first', run: 'node gate.mjs first' }], { 'TASKS.md': rows });
    const backlog = () => readFileSync(join(repo.root, 'TASKS.md'), 'utf8');
    const unmerged = () => git(repo.root, 'diff', '--name-only', '--diff-filter=U');

    // The branch removes a row next to the one main adds after: git stops on it, and the tool resolves it by the base.
    branch(repo.root, 'left', { 'TASKS.md': rows.replace('- [ ] **A2** two.\n', '') });
    writeFileSync(join(repo.root, 'TASKS.md'), rows.replace('- [ ] **A1** one.\n', '- [ ] **A1** one.\n- [ ] **B1** new.\n'));
    git(repo.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);
    let result = await repo.run(['left']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /TASKS\.md: resolved by the merge base, 1 line a side added kept and 1 line a side removed dropped/);
    assert.equal(backlog(), ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **B1** new.', '- [ ] **A3** three.', ''].join('\n'));
    assert.equal(unmerged(), '');
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // Both sides edit one row: it is left as git wrote it, named, and no gate runs.
    const now = backlog();
    branch(repo.root, 'right', { 'TASKS.md': now.replace('**A3** three.', '**A3** three, right.') });
    writeFileSync(join(repo.root, 'TASKS.md'), now.replace('**A3** three.', '**A3** three, main.'));
    git(repo.root, 'commit', '--quiet', '-am', `main moved again${TRAILER}`);
    result = await repo.run(['right']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /TASKS\.md: both sides changed the same line \(the merge base's line 5\); left for you/);
    assert.match(result.out, /conflict in 1 file:\n\s+TASKS\.md/);
    assert.match(backlog(), /^<<<<<<< /m);
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'merge', '--abort');
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
    // The folder and its command are the repository's declaration (GENREGEN1), as this repository's own are.
    const gates = [{ name: 'first', run: 'node gate.mjs first' }];
    const repo = scratch('generated', gates, {
      'daoris.gates.json': `${JSON.stringify({ gates, generated: [{ paths: 'docs/index', run: 'node tools/orient-index.mjs' }] }, null, 2)}\n`,
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

  test('two notes a union merge leaves touching under one decision are set apart in the merge, and at --continue after a conflict', async () => {
    const decision = '## D1 — a thing (2026-10-04)\n\nBody of it.\n';
    const note = (task: string, words: string) => `\n**${task}, built 2026-10-04: ${words}.** What it settled.\n`;
    // The merge's one gate is the real check `verify` runs, copied with the module it imports and what that starts git through.
    const repo = scratch('notes', [{ name: 'duplicates', run: 'node tools/doc-duplicates.mjs' }], {
      '.gitattributes': 'CHANGELOG.md merge=union\ndocs/decisions/*.md merge=union\n',
      'daoris.json': JSON.stringify({ documents: { decisions: 'docs/decisions' } }),
      'tools/doc-duplicates.mjs': readFileSync(join(repoRoot, 'tools', 'doc-duplicates.mjs'), 'utf8'),
      'tools/fsx.mjs': readFileSync(join(repoRoot, 'tools', 'fsx.mjs'), 'utf8'),
      ...Object.fromEntries(['tools/gitforwindows.ts', 'platform/fsx.ts', 'platform/errors.ts'].map((file) =>
        [`src/Daoris.Cli/src/${file}`, readFileSync(join(repoRoot, 'src', 'Daoris.Cli', 'src', file), 'utf8')])),
      'docs/decisions/D1.md': decision,
      'shared.md': 'base\n',
    });
    const file = () => readFileSync(join(repo.root, 'docs', 'decisions', 'D1.md'), 'utf8');
    const duplicates = () => spawnSync(process.execPath, ['tools/doc-duplicates.mjs'], { cwd: repo.root, encoding: 'utf8' });

    // Main took one branch's note; a branch from before it appended its own to the same decision.
    branch(repo.root, 'right', { 'docs/decisions/D1.md': decision + note('UX6f', 'right') });
    writeFileSync(join(repo.root, 'docs', 'decisions', 'D1.md'), decision + note('UX6e', 'left'));
    git(repo.root, 'commit', '--quiet', '-am', `main took a note${TRAILER}`);

    // Without the tool: union keeps once the blank line both sides opened with, so the second label lands under the first note.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-commit', 'right');
    assert.equal(file(), decision + note('UX6e', 'left') + note('UX6f', 'right').slice(1));
    const without = duplicates();
    assert.equal(without.status, 1, without.stdout);
    assert.match(without.stderr, /docs\/decisions\/D1\.md: a note label after a non-blank line: \*\*UX6f/);
    git(repo.root, 'merge', '--abort');

    // With it: one blank line before the gates choose, said with its file and line, staged, and the check passes as the gate.
    let result = await repo.run(['right']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /set apart: D1's UX6f note \(docs\/decisions\/D1\.md:7\)/);
    assert.ok(result.out.indexOf('set apart') < result.out.indexOf('gates for right'), 'set apart before the gates choose');
    assert.equal(file(), decision + note('UX6e', 'left') + note('UX6f', 'right'));
    assert.equal(git(repo.root, 'diff', '--name-only'), '', 'what was set apart is staged with the merge');
    assert.match(result.out, /PASS\s+duplicates/);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // Beside a conflict a person resolves, nothing is set apart until --continue.
    const merged = file();
    branch(repo.root, 'third', { 'docs/decisions/D1.md': merged + note('UX6c', 'third'), 'shared.md': 'third\n' });
    writeFileSync(join(repo.root, 'docs', 'decisions', 'D1.md'), merged + note('UX6b', 'main'));
    writeFileSync(join(repo.root, 'shared.md'), 'main\n');
    git(repo.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);
    result = await repo.run(['third']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /conflict in 1 file:\n\s+shared\.md/);
    assert.doesNotMatch(result.out, /set apart/);
    writeFileSync(join(repo.root, 'shared.md'), 'resolved\n');
    git(repo.root, 'add', 'shared.md');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /set apart: D1's UX6c note \(docs\/decisions\/D1\.md:11\)/);
    assert.equal(file(), merged + note('UX6b', 'main') + note('UX6c', 'third'));
    assert.match(result.out, /PASS\s+duplicates/);
    repo.cleanup();
  });

  test('two archive entries a union merge leaves touching are spaced in the merge, and a join main already held is left', async () => {
    // Main's archive holds one join from before, which the merge never made and so never touches.
    const archive = '# Archive\n\n## A1 — old (2026-10-01)\n\nDone.\n## A2 — glued long ago (2026-10-02)\n\nDone.\n';
    const entry = (task: string) => `\n## ${task} — built (2026-10-20)\n\n- [x] **${task}**\n**Outcome** 2026-10-20: ${task} landed.\n`;
    const repo = scratch('archive', [{ name: 'first', run: 'node gate.mjs first' }], {
      '.gitattributes': 'docs/task-archive.md merge=union\n',
      'docs/task-archive.md': archive,
    });
    const file = () => readFileSync(join(repo.root, 'docs', 'task-archive.md'), 'utf8');

    // Main took one branch's entry; a branch from before it appended its own.
    branch(repo.root, 'right', { 'docs/task-archive.md': archive + entry('RIGHT1') });
    writeFileSync(join(repo.root, 'docs', 'task-archive.md'), archive + entry('LEFT1'));
    git(repo.root, 'commit', '--quiet', '-am', `main took an entry${TRAILER}`);

    // Without the tool: union keeps once the blank line both sides opened with, so the second heading lands under the first entry.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-commit', 'right');
    assert.equal(file(), archive + entry('LEFT1') + entry('RIGHT1').slice(1));
    git(repo.root, 'merge', '--abort');

    // With it: one blank line before the gates choose, said with its file and line, staged; the old join stays.
    const result = await repo.run(['right']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /set apart: docs\/task-archive\.md's heading "## RIGHT1 — built \(2026-10-20\)" \(docs\/task-archive\.md:15\)/);
    assert.ok(result.out.indexOf('set apart') < result.out.indexOf('gates for right'), 'set apart before the gates choose');
    assert.equal((result.out.match(/set apart/g) ?? []).length, 1, result.out);
    assert.equal(file(), archive + entry('LEFT1') + entry('RIGHT1'));
    assert.equal(git(repo.root, 'diff', '--name-only'), '', 'what was set apart is staged with the merge');
    repo.cleanup();
  });

  test('the knowledge server is rebuilt once a merge\'s gates pass, and a build that fails is said and fails nothing', async () => {
    // A stand-in for the server's tool: `build` notes itself and says what the real one says, failing on request.
    const server = [
      "import { appendFileSync, existsSync } from 'node:fs';",
      "appendFileSync('local/ran.txt', `knowledge-server ${process.argv.slice(2).join(' ')}\\n`);",
      "if (existsSync('local/fail-build')) { console.error('knowledge-server: the build failed (no build yet); sessions keep the build they had'); process.exit(1); }",
      "console.log('knowledge-server: built 20261004T100000Z-aaaaaaaa (no build yet) in 9 s; 0 older removed');",
      '',
    ].join('\n');
    const repo = scratch('knowledge-server', [{ name: 'first', run: 'node gate.mjs first' }], { 'tools/knowledge-server.mjs': server });

    branch(repo.root, 'one', { 'one.txt': '1\n' });
    let result = await repo.run(['one']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first', 'knowledge-server build'], 'built after the gates, never before them');
    assert.match(result.out, /knowledge server: built 20261004T100000Z-aaaaaaaa \(no build yet\)/);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // A failed gate builds nothing: the merge is not one to serve.
    branch(repo.root, 'two', { 'two.txt': '2\n' });
    writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'first', run: 'node gate.mjs first 1' }] }, null, 2)}\n`);
    git(repo.root, 'commit', '--quiet', '-am', `a failing gate${TRAILER}`);
    result = await repo.run(['two']);
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(repo.ran(), ['first', 'knowledge-server build', 'first']);
    git(repo.root, 'merge', '--abort');

    writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'first', run: 'node gate.mjs first' }] }, null, 2)}\n`);
    git(repo.root, 'commit', '--quiet', '-am', `the gate passes again${TRAILER}`);
    writeFileSync(join(repo.root, 'local', 'fail-build'), '');
    result = await repo.run(['two']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /knowledge server: NOT rebuilt, and the merge stands \(knowledge-server: the build failed/);
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

    // --full: the real-process half and the deployment rehearsal run in the full set only (GATE5).
    let result = await repo.run(['one', '--batch', 'two', '--full']);
    assert.equal(result.status, 0, result.out);
    // GATELANES1: the Process half runs beside the light lane, so only the Process lane's own order is fixed; and the
    // check and the CLI's gate build nothing in common, so they run side by side too (GATELANES2).
    const ran = repo.ran();
    assert.deepEqual(unordered(ran), ['cli', 'dotnet-run', 'dotnet-test'], 'the rehearsals wait for the last branch');
    assert.match(result.out, /light\s+check, cli\n.*Process\s+driver/);
    assert.match(result.out, /in the plan's order:\n\s+PASS\s+check\s[^\n]*\n\s+PASS\s+cli\s[^\n]*\n\s+PASS\s+driver\s/);
    assert.match(result.out, /--continue/);

    // The next merge waits until this one is in main: an open merge, or an abandoned one, is refused.
    git(repo.root, 'merge', '--abort');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not committed/);
    // However the parent lands it, what --continue asks is that the branch's tip is in main's history.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-edit', 'one');

    // The second merge meets a real-process test that fails in the suite and passes alone, and a
    // rehearsal that dies once; told to, it runs every gate in turn, as before the lanes.
    result = await repo.run(['--continue', '--one-at-a-time'], { FAKE_DOTNET: 'flake' });
    assert.equal(result.status, 0, result.out);
    // The next merge's start prunes the branch the last one landed (GATE2).
    assert.match(result.out, /removed\s+one\s+the branch/);
    assert.deepEqual(repo.ran().slice(0, 3), ran, 'the first merge\'s, as read above');
    assert.deepEqual(repo.ran().slice(3), [
      'dotnet-run', 'cli', 'dotnet-test', 'dotnet-test-alone FullyQualifiedName=N.ProcessJobTests.A_child',
      'rehearse', 'rehearse', 'deployment',
    ]);
    assert.match(result.out, /FLAKE\s+driver\s.*ProcessJobTests\.A_child.*driver\.rerun\.log/);
    assert.match(result.out, /FLAKE\s+rehearse\s.*exit 127.*passed when run again.*rehearse\.rerun\.log/);
    assert.match(result.out, /with 2 flakes \(driver, rehearse\)/);
    assert.doesNotMatch(result.out, /in the plan's order/, 'one gate at a time needs no summary: its lines are in order');
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

  test('a merge runs the gates its paths reach, never the long ones, and a batch\'s last merge runs each rehearsal any of its merges reached', async () => {
    const repo = scratch('reach', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'driver-process', run: 'dotnet test fake/Driver.Tests --settings fake/process.runsettings' },
      { name: 'rehearse-family', run: 'npm run rehearse:family' },
    ], {
      'package.json': `${JSON.stringify({ name: 'scratch', private: true, scripts: { 'rehearse:family': 'node gate.mjs rehearse-family' } }, null, 2)}\n`,
    });
    // The canon reaches the family rehearsal; a document reaches neither it nor the driver's Process half.
    branch(repo.root, 'canon-edit', { 'canon/core/rules/x.md': 'x\n' });
    branch(repo.root, 'docs-edit', { 'docs/y.md': 'y\n' });
    // A path no rule places reaches every gate, and a merge still leaves the Process half to the full set (GATE5).
    branch(repo.root, 'unplaced-edit', { 'elsewhere/z.cs': 'z\n' });

    let plan = await repo.run(['--plan', 'docs-edit']);
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /gates it reaches: 1 of 3/);
    assert.match(plan.out, /driver-process\s+suite\s+in the full set before staging/);
    assert.match(plan.out, /2 gates of the plan skipped; the full set runs before the install is staged \(node tools\/merge-branch\.mjs --full/);
    plan = await repo.run(['--plan', 'unplaced-edit']);
    assert.match(plan.out, /gates it reaches: 2 of 3/);
    assert.match(plan.out, /driver-process\s+suite\s+in the full set before staging.*elsewhere\/z\.cs/);
    plan = await repo.run(['--plan', 'unplaced-edit', '--full']);
    assert.match(plan.out, /gates it reaches: 3 of 3 \(--full\)/);

    let result = await repo.run(['canon-edit', '--batch', 'docs-edit']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['cli'], 'the rehearsal it reaches waits for the batch\'s last merge');
    assert.match(result.out, /rehearse-family\s+rehearsal\s+waits for the batch's last branch \(docs-edit\)/);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['cli', 'cli', 'rehearse-family'], 'never the Process half, which only the full set runs');
    assert.match(result.out, /rehearse-family\s+rehearsal\s+reached by canon-edit, earlier in this batch/);
    assert.match(result.out, /1 gate of the plan skipped;/);
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
    // A merge this tool did not leave is not one it re-gates; with none open, --rerun gates the checkout (GATE6b).
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-commit', 'fix');
    let result = await repo.run(['--rerun', 'first']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /a merge is open here that this tool did not leave/);
    assert.deepEqual(repo.ran(), []);
    git(repo.root, 'merge', '--abort');

    // One at a time, which the batch keeps, so the failure leaves the third gate unstarted: side by side, the three
    // build nothing in common and would start together (GATELANES2).
    result = await repo.run(['fix', '--one-at-a-time']);
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

  // GATE6's proof at --rerun: the gates are named as this repository names them, so the tool's own lane table places
  // the paths (a scratch repository has no lanes file, and the table's path rules need none).
  test('--rerun keeps a verdict no fixed path reaches, and runs again each one a fixed path reaches, saying which path', async () => {
    const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
    const driverTest = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs';
    const repo = scratch('rerun-reach', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'driver', run: 'node gate.mjs driver' },
      { name: 'driver-process', run: 'node gate.mjs driver-process' },
      // Fails the first time only: the browser test the parent fixes.
      { name: 'web', run: 'node gate.mjs web 3 once' },
    ]);
    branch(repo.root, 'both', { [spec]: 'one\n', [driverTest]: 'one\n' });
    let result = await repo.run(['both']);
    assert.equal(result.status, 1, result.out);
    // Side by side, as they build nothing (GATELANES2): which ran counts here, not in what order.
    assert.deepEqual(unordered(repo.ran()), ['cli', 'driver', 'driver-process', 'web']);

    // The browser test fixed in the merge: the web gate, and the baseline every path reaches; the driver's verdicts are kept.
    writeFileSync(join(repo.root, spec), 'two\n');
    result = await repo.run(['--rerun', 'web']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(unordered(repo.ran().slice(4)), ['cli', 'web']);
    assert.match(result.out, /re-run on the merge of both: web, then cli, whose verdict a changed path reaches; 2 verdicts kept/);
    assert.match(result.out, /stale\s+cli\s+PASS\s+from \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC: src\/Daoris\.Web\/e2e\/platform\.spec\.ts changed since, which reaches it/);
    assert.match(result.out, /kept\s+driver\s+PASS\s+from .*no path changed since reaches it/);
    assert.match(result.out, /kept\s+driver-process\s+PASS/);
    assert.doesNotMatch(result.out, /a stage does not count it/, 'every verdict kept is one a stage counts');

    // The driver's tests fixed too: the driver's gates run again, and the web gate's verdict stands.
    writeFileSync(join(repo.root, driverTest), 'two\n');
    result = await repo.run(['--rerun', 'cli']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(unordered(repo.ran().slice(6)), ['cli', 'driver', 'driver-process']);
    assert.match(result.out, /stale\s+driver\s+PASS\s+from .*: src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\/LandingTests\.cs changed since, which reaches it/);
    assert.match(result.out, /kept\s+web\s+PASS/);
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
    // Side by side, as they build nothing (GATELANES2), so each verdict is recorded as its gate ends, in either order.
    assert.deepEqual(unordered(repo.ran()), ['first', 'second']);
    assert.match(result.out, /PASS\s+first\s.*local\/scratch\/full-[0-9a-f]{7}\/first\.log/);
    assert.equal(merging(repo.root), false, '--full alone merges nothing');
    const record = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { schema: number; verdicts: Verdict[] };
    assert.equal(record.schema, 1);
    assert.deepEqual(unordered(record.verdicts.map((verdict) => `${verdict.gate} ${verdict.verdict}`)), ['first PASS', 'second PASS']);
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
    // Code is not: no rule of the lane table places it, so it reaches every gate, and each says it made its verdict stale.
    writeFileSync(join(repo.root, 'code.txt'), 'changed\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /second\s+stale\s+PASS \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC, on a tree before code\.txt changed, which reaches it/);
    repo.cleanup();
  });

  // GATE6's proof at the stage: a browser test fixed after the full set, and re-gated by the web gate alone.
  test('--passed counts a verdict no path changed since reaches: a browser test re-gated alone stages, a driver change does not', async () => {
    const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
    const driverTest = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs';
    const repo = scratch('passed-reach', [
      { name: 'driver', run: 'node gate.mjs driver' },
      { name: 'driver-process', run: 'node gate.mjs driver-process' },
      { name: 'web', run: 'node gate.mjs web' },
    ], { [spec]: 'one\n', [driverTest]: 'one\n' });
    let result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);

    // Changed since the full set: the web gate's verdict is stale, and the refusal names the path; the driver's stand.
    writeFileSync(join(repo.root, spec), 'two\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /2 of 3 gates/);
    assert.match(result.out, /web\s+stale\s+PASS .* UTC, on a tree before src\/Daoris\.Web\/e2e\/platform\.spec\.ts changed, which reaches it/);
    assert.match(result.out, /driver\s+PASS .* UTC\s+on a tree that differs from this one only by paths it does not reach \(src\/Daoris\.Web\/e2e\/platform\.spec\.ts\)/);
    assert.match(result.out, /driver-process\s+PASS/);

    // The fix landed by a merge, which runs the web gate alone: the tree stages, with no second full run.
    git(repo.root, 'checkout', '--', spec);
    branch(repo.root, 'spec-fix', { [spec]: 'two\n' });
    result = await repo.run(['spec-fix']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().slice(3), ['web']);
    git(repo.root, 'commit', '--quiet', '--no-edit');
    writeFileSync(join(repo.root, 'CHANGELOG.md'), '# Changelog\n\n## Unreleased\n\n- **A** browser test, fixed.\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /the full set has passed this checkout/);
    assert.match(result.out, /web\s+PASS .* UTC\s+on a tree that differs from this one only by the records \(CHANGELOG\.md\)/);
    assert.match(result.out, /driver\s+PASS .* UTC\s+on a tree that differs from this one only by paths it does not reach \(src\/Daoris\.Web\/e2e\/platform\.spec\.ts\) and the records \(CHANGELOG\.md\)/);

    // A driver change reaches the driver's gates: those are stale, naming it, and the web gate's verdict stands.
    writeFileSync(join(repo.root, driverTest), 'two\n');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /driver\s+stale\s+PASS .*, on a tree before src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\/LandingTests\.cs changed, which reaches it/);
    assert.match(result.out, /driver-process\s+stale/);
    assert.match(result.out, /web\s+PASS .*only by paths it does not reach \(src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\/LandingTests\.cs\)/);
    repo.cleanup();
  });

  // GATE6b's proof: a fix committed on the checkout after the full set stages once the gates it reaches run again.
  test('--stale runs exactly the gates a fix committed past the full set reaches, and the stage then accepts the tree', async () => {
    const spec = 'src/Daoris.Web/e2e/platform.spec.ts';
    const driverTest = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs';
    const repo = scratch('stale', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'driver', run: 'node gate.mjs driver' },
      { name: 'driver-process', run: 'node gate.mjs driver-process' },
      { name: 'web', run: 'node gate.mjs web' },
    ], { [spec]: 'one\n', [driverTest]: 'one\n' });
    let result = await repo.run(['--full']);
    assert.equal(result.status, 0, result.out);

    // The refusal names --stale, which runs two gates, not a second full run.
    writeFileSync(join(repo.root, spec), 'two\n');
    git(repo.root, 'commit', '--quiet', '-am', `fix the spec${TRAILER}`);
    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /\n {2}Run the 2 stale gates on it: node tools\/merge-branch\.mjs --stale\n/);
    assert.doesNotMatch(result.out, /--full/);

    // Work left uncommitted is refused, by name, and nothing runs.
    writeFileSync(join(repo.root, 'stray.txt'), 'left here\n');
    result = await repo.run(['--stale']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not clean:\s+\?\? stray\.txt/);
    rmSync(join(repo.root, 'stray.txt'));
    assert.deepEqual(repo.ran().slice(4), [], 'a refusal ran a gate');

    result = await repo.run(['--stale']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(unordered(repo.ran().slice(4)), ['cli', 'web'], "the baseline and the web gate; the driver's verdicts stand");
    assert.match(result.out, /stale\s+web\s+PASS\s+from .* UTC: src\/Daoris\.Web\/e2e\/platform\.spec\.ts changed since, which reaches it/);
    assert.match(result.out, /driver-process\s+PASS .* UTC\s+on a tree that differs from this one only by paths it does not reach/);
    assert.match(result.out, /2 gates run and passed on [0-9a-f]{7}; the full set has passed this checkout: 4 of 4 gates\. A stage of this tree is allowed\./);
    assert.equal(merging(repo.root), false, 'nothing is merged');
    // What publish:desktop asks before it builds.
    assert.equal(publish.ungatedRefusal(join(dirname(repo.root), 'install'), repo.root).refusal, null);

    // Nothing is stale now: --stale runs nothing, and says the stage passes.
    result = await repo.run(['--stale']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /nothing is stale on the checkout/);
    assert.deepEqual(repo.ran().slice(6), []);
    repo.cleanup();
  });

  test('a flaked gate re-run alone on the checkout makes the stage accept it, and a dirty tree is refused, naming the files', async () => {
    const repo = scratch('flake-rerun', [
      { name: 'first', run: 'node gate.mjs first' },
      // Fails the first time only: a test that timed out under load.
      { name: 'second', run: 'node gate.mjs second 3 once' },
      { name: 'third', run: 'node gate.mjs third' },
    ], { 'code.txt': 'base\n' });
    let result = await repo.run(['--full', '--keep-going']);
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(unordered(repo.ran()), ['first', 'second', 'third']);
    assert.match(result.out, /\n {2}Run the gate it lacks on it: node tools\/merge-branch\.mjs --rerun second\n/, 'the failed full set names the smaller command');

    result = await repo.run(['--passed']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /second\s+FAIL\s+.* UTC\s+on this tree/);
    assert.match(result.out, /\n {2}Run the gate it lacks on it: node tools\/merge-branch\.mjs --rerun second\n/);

    writeFileSync(join(repo.root, 'code.txt'), 'changed\n');
    result = await repo.run(['--rerun', 'second']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not clean:\s+\.M code\.txt/);
    git(repo.root, 'checkout', '--', 'code.txt');
    result = await repo.run(['--rerun', 'nope']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /no gate 'nope' in the plan: first, second, third/);
    assert.deepEqual(repo.ran().slice(3), [], 'a refusal ran a gate');

    result = await repo.run(['--rerun', 'second']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran().slice(3), ['second'], 'the flaked gate alone: nothing changed, so no other verdict is stale');
    assert.match(result.out, /re-run on the checkout \(HEAD [0-9a-f]{7}\): second; 2 verdicts kept/);
    assert.match(result.out, /PASS\s+second\s.*local\/scratch\/full-[0-9a-f]{7}\/second\.log/);
    assert.match(result.out, /A stage of this tree is allowed\./);
    assert.equal(merging(repo.root), false, 'nothing is merged');
    result = await repo.run(['--passed']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /second\s+PASS .* UTC\s+on this tree/);
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
    // Idle for hours, so only what the plan says of them decides (CLEANUP2b keeps a tree written in the last two).
    age(fresh, 300);
    age(inUse, 300);
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
    assert.match(plan.out, /\n\s+keep\s+fresh\s+its worktree is locked by the agent harness \(claude agent agent-y \(pid 2\)\), and stays locked: its branch is at main's tip, as a just-dispatched agent's is\n/);
    assert.match(plan.out, /\n\s+remove\s+free\s+its worktree \.\.\/trees\/free and the branch\n/);
    assert.match(plan.out, /\n\s+remove\s+gone\s+its worktree's record \(the folder \.\.\/trees\/gone is gone already\) and the branch\n/);
    assert.match(plan.out, /\n\s+remove\s+plain\s+the branch \(no worktree has it checked out\)\n/);
    assert.match(plan.out, /\n\s+remove\s+tracking\s/, 'only the deletion itself can hear git refuse');
    const inUseLine = plan.out.split('\n').find((line) => /^\s+keep\s+in-use\s/.test(line)) ?? '';
    assert.match(inUseLine, /its worktree is locked by the agent harness \(claude agent agent-x \(pid 1\)\), and stays locked: /);
    assert.match(inUseLine, /main's tip, as a just-dispatched agent's is; it has modified or staged tracked files \(tracked\.txt\)$/, 'the harness\'s lock is looked behind (CLEANUP2)');
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

  // CLEANUP2: 24 worktrees stood with three agents running (2026-10-18): five kept for worker logs under local/, nine
  // for the harness's lock, which it holds for its session's life, and one half-removed folder two leaked processes held.
  test('--prune lifts the harness\'s lock from a merged worktree left with nothing, takes worker logs and half-removed folders, and names what holds the rest', async () => {
    const repo = scratch('prune-leftovers', [{ name: 'first', run: 'node gate.mjs first' }], { '.gitignore': 'local/\n.claude/worktrees/\n', 'tracked.txt': 'base\n' });
    const trees = join(dirname(repo.root), 'trees');
    const holders: ChildProcess[] = [];
    // A process that names `named` in its command line, as the two `node land.mjs` a test left running named theirs.
    const hold = (named: string, cwd = dirname(repo.root), args = [join(named, 'held')]): ChildProcess => {
      const child = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)', ...args], { cwd, stdio: 'ignore' });
      holders.push(child);
      return child;
    };
    try {
      const add = (name: string, reason?: string): string => {
        const path = join(trees, name);
        git(repo.root, 'worktree', 'add', '--quiet', '-b', name, path, 'main');
        if (reason !== undefined) git(repo.root, 'worktree', 'lock', '--reason', reason, path);
        return path;
      };
      const harness = (id: string) => `claude agent agent-${id} (pid 4242)`;
      const done = add('done', harness('d'));
      const busy = add('busy', harness('b'));
      const busyHolder = hold(busy);
      const logs = add('logs', harness('l'));
      for (const path of ['local/scratch/run/out.md', 'local/gate.log']) {
        mkdirSync(dirname(join(logs, path)), { recursive: true });
        writeFileSync(join(logs, path), 'a worker wrote this\n');
      }
      const notes = add('notes');
      for (const path of ['local/scratch/a.log', 'local/notes.md']) {
        mkdirSync(dirname(join(notes, path)), { recursive: true });
        writeFileSync(join(notes, path), 'a worker wrote this\n');
      }
      add('owned', 'the owner keeps this');
      // CLEANUP2b: a worker started minutes ago, clean, merged, behind main's tip and with no process naming its folder
      // (a shell's working folder is not on a command line) is kept; the others were last written hours ago.
      const young = add('young', harness('y'));
      for (const path of [done, busy, logs]) age(path, 300);
      // Main moves on, so these merged branches stand behind its tip, as a finished agent's always does.
      git(repo.root, 'commit', '--quiet', '--allow-empty', '-m', `main moves on${TRAILER}`);

      const worktrees = join(repo.root, '.claude', 'worktrees');
      const outside = join(dirname(repo.root), 'outside');
      mkdirSync(outside, { recursive: true });
      writeFileSync(join(outside, 'keep.txt'), 'never followed\n');
      const half = join(worktrees, 'agent-h');
      mkdirSync(join(half, 'examples', 'plugins', 'p'), { recursive: true });
      writeFileSync(join(half, 'examples', 'plugins', 'p', 'land.mjs'), 'left\n');
      symlinkSync(outside, join(half, 'linked'), 'junction');
      symlinkSync(outside, join(worktrees, 'agent-link'), 'junction');
      const held = join(worktrees, 'agent-held');
      mkdirSync(held, { recursive: true });
      writeFileSync(join(held, 'a.txt'), 'held\n');
      const heldHolder = hold(held, held);
      mkdirSync(join(worktrees, 'agent-git'), { recursive: true });
      writeFileSync(join(worktrees, 'agent-git', '.git'), 'gitdir: elsewhere\n');
      // Windows keeps a folder that is a process's working folder, and that process's command line need not name it.
      const stuck = join(worktrees, 'agent-stuck');
      const windows = process.platform === 'win32';
      if (windows) {
        mkdirSync(stuck, { recursive: true });
        writeFileSync(join(stuck, 'b.txt'), 'stuck\n');
        hold(stuck, stuck, []);
      }
      const folders = windows ? 5 : 4;

      const branches = () => git(repo.root, 'branch', '--format=%(refname:short)').split('\n');
      const every = branches();
      const listing = () => tool.parseWorktrees(git(repo.root, 'worktree', 'list', '--porcelain'));
      const before = listing();

      const plan = await repo.run(['--prune', '--plan']);
      assert.equal(plan.status, 0, plan.out);
      assert.match(plan.out, new RegExp(`prune \\(a plan, so nothing is removed\\): 6 branches merged into main; ${folders} folders under \\.claude/worktrees/ git does not list\\n`));
      assert.match(plan.out, /\n\s+keep\s+young\s+its worktree is locked by the agent harness \(claude agent agent-y \(pid 4242\)\), and stays locked: written \d+ minutes? ago, a worker may be in it\n/);
      assert.match(plan.out, /\n\s+remove\s+done\s+its worktree \.\.\/trees\/done, unlocked from the agent harness \(claude agent agent-d \(pid 4242\)\), and the branch\n/);
      assert.match(plan.out, new RegExp(`\\n\\s+keep\\s+busy\\s+its worktree is locked by the agent harness \\(claude agent agent-b \\(pid 4242\\)\\), and stays locked: a running process is inside it \\(pid ${busyHolder.pid}: `));
      assert.match(plan.out, /\n\s+remove\s+logs\s+its worktree \.\.\/trees\/logs, unlocked from the agent harness/, 'worker logs keep nothing');
      assert.match(plan.out, /\n\s+keep\s+notes\s+its worktree has private output under local\/ \(local\/notes\.md\), which nothing else keeps\n/);
      assert.match(plan.out, /\n\s+keep\s+owned\s+its worktree is locked, so in use \(the owner keeps this\); it is never looked into or unlocked here\n/);
      assert.match(plan.out, /\n\s+remove\s+\.claude\/worktrees\/agent-h\s+a folder git does not list, with no \.git: what a worktree removal that did not finish left\n/);
      assert.match(plan.out, /\n\s+keep\s+\.claude\/worktrees\/agent-link\s+a link \(a reparse point\), never followed or removed here\n/);
      assert.match(plan.out, new RegExp(`\\n\\s+keep\\s+\\.claude/worktrees/agent-held\\s+.*a running process is inside it \\(pid ${heldHolder.pid}: `));
      assert.match(plan.out, /\n\s+keep\s+\.claude\/worktrees\/agent-git\s+.*holds a \.git/);
      if (windows) assert.match(plan.out, /\n\s+remove\s+\.claude\/worktrees\/agent-stuck\s/, 'only the removal itself finds the folder held');
      assert.deepEqual(branches(), every, '--plan removes no branch');
      assert.deepEqual(listing(), before, '--plan removes, unlocks and locks no worktree');
      for (const path of [done, logs, half, held, join(worktrees, 'agent-link', 'keep.txt')]) assert.ok(existsSync(path), path);

      const result = await repo.run(['--prune']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, new RegExp(`prune: 6 branches merged into main; ${folders} folders under \\.claude/worktrees/ git does not list\\n`));
      assert.match(result.out, /\n\s+removed\s+done\s+its worktree \.\.\/trees\/done, unlocked from the agent harness/);
      assert.match(result.out, /\n\s+removed\s+logs\s/);
      for (const name of ['busy', 'notes', 'owned', 'young']) assert.match(result.out, new RegExp(`\\n\\s+kept\\s+${name}\\s`));
      assert.match(result.out, /\n\s+removed\s+\.claude\/worktrees\/agent-h\s/);
      for (const name of ['agent-link', 'agent-held', 'agent-git']) assert.match(result.out, new RegExp(`\\n\\s+kept\\s+\\.claude/worktrees/${name}\\s`));
      if (windows) {
        assert.match(result.out, /\n\s+kept\s+\.claude\/worktrees\/agent-stuck\s+it could not be removed \(E[A-Z]+\b[^)]*\); no running process names it in its command line, so one holds it another way \(its working folder, an open file\); the next prune tries again\n/);
      }

      assert.deepEqual(branches(), ['busy', 'main', 'notes', 'owned', 'young']);
      const after = listing();
      assert.deepEqual(after.filter((t) => t.locked).map((t) => [t.branch, t.lockReason]).sort(), [['busy', harness('b')], ['owned', 'the owner keeps this'], ['young', harness('y')]]);
      assert.ok(existsSync(young), 'a worker written minutes ago keeps its tree, locked as it was (CLEANUP2b)');
      assert.equal(existsSync(done), false);
      assert.equal(existsSync(logs), false, 'the worker logs went with their worktree');
      assert.equal(existsSync(half), false, 'the half-removed folder is gone');
      assert.equal(readFileSync(join(outside, 'keep.txt'), 'utf8'), 'never followed\n', 'a link inside is removed, never followed');
      assert.ok(existsSync(join(worktrees, 'agent-link')) && existsSync(join(held, 'a.txt')) && existsSync(join(worktrees, 'agent-git', '.git')));
      assert.ok(existsSync(busy) && existsSync(join(notes, 'local', 'notes.md')));
      assert.equal(busyHolder.exitCode, null, 'a process is never stopped by the prune');
    } finally {
      await Promise.all(holders.map((child) => new Promise<void>((done) => {
        if (child.exitCode !== null || child.signalCode !== null) return done();
        child.once('exit', () => done());
        child.kill();
      })));
      repo.cleanup();
    }
  });

  // GATEWT1's proof: the full set ran in the main checkout, so no merge could run for its hour and a half.
  test('--full --beside gates main\'s tip in a worktree of its own while a merge lands, its verdicts in the main checkout\'s store', async () => {
    // The install's output is ignored, as this repository's is: the tree gated is the commit's.
    const repo = scratch('beside', [{ name: 'first', run: 'node gate.mjs first' }], {
      '.gitignore': 'local/\nnode_modules/\n', 'code.txt': 'base\n', 'package-lock.json': '{"lockfileVersion":3}\n',
    });
    branch(repo.root, 'feature', { 'feature.txt': 'x\n' });
    const gated = git(repo.root, 'rev-parse', 'HEAD');
    const sha = gated.slice(0, 7);
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const hold = join(dirname(repo.root), 'hold');
    // `npm`, faked: `ci` lays node_modules where it runs and notes it, as the real one installs from the lockfile.
    const npmBin = join(dirname(repo.root), 'npm-bin');
    const npmLog = join(dirname(repo.root), 'npm.log');
    mkdirSync(npmBin, { recursive: true });
    writeFileSync(join(npmBin, 'fake-npm.mjs'), [
      "import { appendFileSync, mkdirSync } from 'node:fs';",
      "mkdirSync('node_modules', { recursive: true });",
      `appendFileSync(${JSON.stringify(npmLog)}, process.argv.slice(2).join(' ') + '\\n');`,
      '',
    ].join('\n'));
    if (process.platform === 'win32') writeFileSync(join(npmBin, 'npm.cmd'), '@echo off\r\nnode "%~dp0fake-npm.mjs" %*\r\n');
    else {
      writeFileSync(join(npmBin, 'npm'), '#!/bin/sh\nexec node "$(dirname "$0")/fake-npm.mjs" "$@"\n');
      chmodSync(join(npmBin, 'npm'), 0o755);
    }
    const PATH = `${npmBin}${delimiter}${join(dirname(repo.root), 'bin')}${delimiter}${process.env['PATH'] ?? process.env['Path'] ?? ''}`;
    const npmRan = () => (existsSync(npmLog) ? readFileSync(npmLog, 'utf8').split('\n').filter(Boolean) : []);
    const ranThere = join(gateTree, 'local', 'ran.txt');

    // Named, though it is main's tip: the merge that lands meanwhile would supersede a run of main's tip (CIPRIO1c).
    const beside = repo.run(['--full', '--beside', gated], { GATE_HOLD: hold, PATH });
    try {
      // Bounded: the gate tree's gate has started, and holds until the test lets it go.
      for (let i = 0; i < 3000 && !(existsSync(ranThere) && readFileSync(ranThere, 'utf8').includes('first')); i += 1) {
        await new Promise((done) => setTimeout(done, 100));
      }
      assert.ok(existsSync(ranThere), 'the full set runs in the gate tree');
      assert.equal(git(gateTree, 'rev-parse', 'HEAD'), gated, 'detached at main\'s tip');
      assert.ok(existsSync(join(repo.root, 'local', 'gate-tree.lock')), 'the run holds its lock in the main checkout');

      // One at a time.
      let result = await repo.run(['--full', '--beside']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, new RegExp(`a full set beside the merges is running already: on ${sha} since .*\\(pid \\d+, local/gate-tree\\); one runs at a time`));

      // A merge lands meanwhile, and its prune keeps the gate tree.
      result = await repo.run(['feature']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, new RegExp(`\\n\\s+kept\\s+local/gate-tree\\s+its full set is running \\(pid \\d+, since [^)]+, on ${sha}\\): never removed while it runs\\n`));
      git(repo.root, 'commit', '--quiet', '-m', `merge feature${TRAILER}`);
      result = await repo.run(['--prune']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /\n\s+kept\s+local\/gate-tree\s+its full set is running/);
      assert.ok(existsSync(gateTree));
    } finally {
      writeFileSync(hold, '');
    }
    const done = await beside;
    assert.equal(done.status, 0, done.out);
    assert.match(done.out, new RegExp(`the gate tree: made at ${sha} \\(local/gate-tree\\)`));
    assert.match(done.out, /installed \. from its lockfile \(npm ci\)/);
    assert.match(done.out, /under load: a full set beside the merges/i);
    assert.match(done.out, new RegExp(`merge-branch: the full set passed ${sha}`));
    assert.match(done.out, new RegExp(`npm run publish:desktop -- --to <install> --service --stage --from-gated ${sha}`));
    assert.deepEqual(npmRan(), ['ci --no-audit --no-fund'], 'installed once, in the gate tree\'s root folder');
    assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');

    // Its verdict is in the main checkout's store, keyed by the tree it gated; none is in the gate tree's own.
    const record = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { verdicts: Verdict[] };
    // The merge's own verdict is there too, on the merged tree, with the branch it merged.
    const there = record.verdicts.filter((verdict) => verdict.tree === git(repo.root, 'rev-parse', `${gated}^{tree}`));
    assert.deepEqual(there.map((verdict) => `${verdict.gate} ${verdict.verdict} ${verdict.commit} ${verdict.branch}`), [`first PASS ${gated} null`]);
    assert.deepEqual(record.verdicts.filter((verdict) => verdict.branch === 'feature').map((verdict) => verdict.gate), ['first']);
    assert.equal(existsSync(join(gateTree, 'local', 'gate-verdicts.json')), false);
    // `--passed` asked in the gate tree, as its stage asks, reads the main checkout's store.
    let result = await repo.run(['--passed'], {}, gateTree);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /first\s+PASS\s+\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC\s+on this tree/);

    // Run again at the same commit, it is reused and installs nothing: its lockfile is as installed.
    result = await repo.run(['--full', '--beside', gated], { PATH });
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, new RegExp(`the gate tree: reused at ${sha} \\(local/gate-tree\\)`));
    assert.equal(npmRan().length, 1);

    // --prune removes it once its run has ended, and says so.
    result = await repo.run(['--prune']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, new RegExp(`\\n\\s+removed\\s+local/gate-tree\\s+the gate tree at ${sha}, never staged`));
    assert.equal(existsSync(gateTree), false);
    assert.equal(tool.parseWorktrees(git(repo.root, 'worktree', 'list', '--porcelain')).length, 1);
    repo.cleanup();
  });

  // CIPRIO1c's proof: a gate on an old tip ran to its end, up to an hour of the machine's time, for a verdict nobody used.
  test('--full --beside on main\'s tip ends the gate it is running, with its whole tree, once main moves on, and is recorded superseded', async () => {
    const repo = scratch('beside-superseded', [{ name: 'held', run: 'node tree.mjs' }, { name: 'after', run: 'node gate.mjs after' }], { 'tree.mjs': TREE });
    const at = join(dirname(repo.root), 'pids');
    mkdirSync(at, { recursive: true });
    const from = git(repo.root, 'rev-parse', 'HEAD');
    const sha = from.slice(0, 7);
    const pids: number[] = [];
    const beside = repo.run(['--full', '--beside', '--one-at-a-time'], { TREE_PIDS: at });
    try {
      // Bounded: the tool, its gate tree and the gate start first, under the rest of the suite's load.
      pids.push(...await treePids(at, 120_000));
      writeFileSync(join(repo.root, 'next.txt'), 'next\n');
      git(repo.root, 'add', '-A');
      git(repo.root, 'commit', '--quiet', '-m', `next${TRAILER}`);
      const to = git(repo.root, 'rev-parse', 'HEAD');
      // The gate waits ten minutes otherwise; the poll sees the move within seconds, and two minutes is load's margin.
      let done = { status: null as number | null, out: '' };
      await within(beside.then((answer) => { done = answer; }), 'the run ending once main moved on', 120_000);
      assert.equal(done.status, 1, done.out);
      assert.deepEqual(await stillRunning(pids), [], `the gate's child and grandchild still run:\n${done.out}`);
      assert.match(done.out, new RegExp(`superseded: main moved from ${sha} to ${to.slice(0, 7)}, so this run ended the gates it was running and started no more; it is not a pass`));
      assert.match(done.out, /ENDED\s+held\s.*\(ended, not failed: main moved from/);
      assert.match(done.out, /ran: none\n\s+ended: held\n\s+left: after/);
      assert.doesNotMatch(done.out, /FAIL\s+held/);
      assert.match(done.out, /Run it again on the new tip/);

      const record = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-tree.json'), 'utf8')) as { commit: string; passed: boolean | null; superseded?: string };
      assert.deepEqual([record.commit, record.passed, record.superseded], [from, false, to]);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');
      // Its log says it was ended, and an ended gate gives no verdict on the tree.
      assert.match(readFileSync(join(repo.root, 'local', 'scratch', `beside-${sha}`, 'held.log'), 'utf8'), /\(ended, not failed: main moved from/);
      const store = join(repo.root, 'local', 'gate-verdicts.json');
      const verdicts = existsSync(store) ? (JSON.parse(readFileSync(store, 'utf8')) as { verdicts: Verdict[] }).verdicts : [];
      assert.deepEqual(verdicts.filter((verdict) => verdict.gate === 'held'), []);
    } finally {
      for (const pid of pids) if (alive(pid)) process.kill(pid);
      await stillRunning(pids);
      await beside;
      await cleanupAfterKill(repo);
    }
  });

  // GATEWT1b and GATEWT1c: the gate tree's universal gate failed for want of the private patterns file, and a prune
  // after the stage deleted the only copy of a failed run's logs.
  test('--full --beside copies the declared private file into the tree on every run and keeps the run\'s logs past --prune', async () => {
    const needs = [
      "import { existsSync, readFileSync } from 'node:fs';",
      "const file = 'local/sensitive-patterns.txt';",
      "if (!existsSync(file)) { console.log('missing ' + file); process.exit(1); }",
      "const text = readFileSync(file, 'utf8');",
      'console.log(`read ${text.trim()}`);',
      "process.exit(text.startsWith('bad') ? 1 : 0);",
      '',
    ].join('\n');
    const repo = scratch('beside-private', [{ name: 'needs', run: 'node needs.mjs' }], {
      'needs.mjs': needs,
      'daoris.gates.json': `${JSON.stringify({ gates: [{ name: 'needs', run: 'node needs.mjs' }], sensitive: { patternsFile: 'local/sensitive-patterns.txt' } })}\n`,
    });
    const sha = git(repo.root, 'rev-parse', 'HEAD').slice(0, 7);
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const mine = join(repo.root, 'local', 'sensitive-patterns.txt');
    const theirs = join(gateTree, 'local', 'sensitive-patterns.txt');
    const kept = join(repo.root, 'local', 'scratch', `beside-${sha}`);

    // Without the file the run goes on and says so; the gate's own answer decides.
    let result = await repo.run(['--full', '--beside']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /local\/sensitive-patterns\.txt.*missing/);

    writeFileSync(mine, 'good\n');
    result = await repo.run(['--full', '--beside']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /the gate tree: reused/);
    assert.equal(readFileSync(theirs, 'utf8'), 'good\n');
    assert.deepEqual(git(gateTree, 'status', '--porcelain'), '', 'ignored, so the tree is still clean');
    assert.match(readFileSync(join(kept, 'needs.log'), 'utf8'), /read good/);

    // Refreshed on a reused tree; a failed run's logs are kept, and its prune removes the tree but not them.
    writeFileSync(mine, 'bad\n');
    result = await repo.run(['--full', '--beside']);
    assert.equal(result.status, 1, result.out);
    assert.equal(readFileSync(theirs, 'utf8'), 'bad\n');
    result = await repo.run(['--prune']);
    assert.equal(result.status, 0, result.out);
    assert.equal(existsSync(gateTree), false);
    assert.match(readFileSync(join(kept, 'needs.log'), 'utf8'), /read bad/, 'the newest run\'s log outlives the tree');
    repo.cleanup();
  });

  // CI1's proof: a release's verification ran 20 gates for 2 h 48 min at publish time, since nothing verified main between.
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

    // A change that reaches only the tool tests: the failed gate runs with it, owed whatever the change.
    const fourth = commit('tools/merge-branch.mjs', '// the scratch repository\'s own\n');
    result = await repo.run(['--ci']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(ranThere(), ['tool-tests', 'universal']);
    assert.deepEqual(states(statusOf(fourth)), ['universal done PASS', 'service kept PASS', 'tool-tests done PASS']);
    assert.deepEqual(record().runs.map((run) => `${run.commit.slice(0, 7)} ${run.state}`),
      [first, second, second, third, fourth].map((sha, i) => `${sha.slice(0, 7)} ${i === 3 ? 'failed' : 'green'}`));
    repo.cleanup();
  });

  // CIFAST1's proof: a CI run took 45 to 75 minutes, the family and deployment rehearsals 14 to 23 and 14 to 22 of it.
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
      assert.deepEqual(record().runs.at(-1), { commit: tip, state: 'green', at: record().runs.at(-1)!.at, status: `local/scratch/ci-${sha}/status.json`, owed: ['rehearse-family', 'deployment'] });
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
      // Their passes stand for a plain run after: nothing is owed or run.
      clear();
      result = await repo.run(['--ci']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /nothing to run/);
      assert.deepEqual(statusOf(tip).owed, []);
    } finally {
      repo.cleanup();
    }
  });

  test('--ci runs a Process half by reach: the classes a change reaches, the rest kept and named; a file every class reads, every class; one read unnamed, the whole half; a sweep past its bound before a stage', async () => {
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
      "execFileSync('git', ['add', '-A'], { cwd: main });",
      `execFileSync('git', ['commit', '--quiet', '-m', ${JSON.stringify(`landed${TRAILER}`)}], { cwd: main });`,
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
});

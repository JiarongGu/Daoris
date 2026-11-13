/**
 * What the merge tool's test files share: its typed exports, the tools beside it they read, the records' shapes, and
 * where the tool and the repository are.
 */
import { join } from 'node:path';
import { readFileSync } from 'node:fs';
import { BASELINE } from '../../../../tools/merge/reach.ts';
import { REACH } from '../../../../tools/merge/reach-table.ts';
import { repoRoot, unnamedGates } from '../../../../tools/merge/repo-gates.ts';

// The census of test units reads this file's strings as the lane table's data, not as reads.
// test-map: strings name paths as data

export { repoRoot };
export const TOOL = join(repoRoot, 'tools', 'merge-branch.mjs');

export interface Gate { name: string; run: string; kind: string; cwd?: string; clock?: boolean; memoryGb?: number }
export interface Lane { id: string; title: string; summary: string; steward: boolean; paths: string[]; gates?: string[] }
interface Classified { lanes: { id: string; title: string; files: string[] }[]; steward: string[]; shared: string[]; laneless: string[]; outside: string[] }
/** What the prune knows of the gate tree: git lists it, the commit it holds, its changes, its run's lock and its stage. */
export interface GateTreeFacts {
  listed: boolean; head: string | null; changes: string[]; error?: string;
  lock: { pid: number; commit: string; at: string; running: boolean } | null;
  staged: { commit: string; at: string } | null;
  folder?: Folder;
  /** The processes inside it, read when no running lock is held (a full set started by hand holds none). */
  processes?: Proc[]; processError?: string;
}
/**
 * The two lanes, the light one's run order, and what each Process-lane gate starts after; how
 * many light gates run at once, and the components each builds, of which no two gates hold one at once; the
 * fast half whose build each Process half reuses.
 */
interface Schedule {
  light: Gate[]; process: Gate[]; waits: Record<string, { after: string[]; component: string | null }>;
  width: number; components: Record<string, string[]>; reuses: Record<string, string>;
  /** The Process-lane gates each may run beside, the git half and the rehearsals with their own build output. */
  beside: Record<string, string[]>;
}
/** The fast half that passed in this run, and the tree it ran on. */
interface Built { gate: string; tree: string | null }
/** What a gate ran with, as its composition records it beside its log. */
interface CompositionSettings {
  gate: string; run: string; cwd: string | null; runsettings: { path: string; text: string | null } | null; narrowed: string[];
  environment: { removed: string[]; added: Record<string, string> };
}
/**
 * What a lane runner hands a gate: when the light lane has ended, for a Process gate's re-run alone; and to a Process
 * half whose fast half passed in this run, that pass.
 */
export interface Beside { settled?: () => Promise<void>; built?: Built; memory?: MemoryWait | null; again?: GateResult | null }
export interface Worktree { path: string; branch: string; locked: boolean; lockReason: string; prunable: boolean; bare: boolean }
export interface Candidate { branch: string; worktree: Worktree | null; held: string | null }
/** What a prune knows of a harness-locked worktree beyond its changes: its branch at main's tip, the processes inside it. */
export interface Facts {
  gone?: boolean; error?: string; tracked: string[]; untracked: string[]; local: string | null;
  atTip?: boolean; processes?: Proc[]; processError?: string;
  /** Minutes since the newest write read in the worktree. */
  idleMinutes?: number;
}
export interface Proc { pid: number; exe: string; command: string }
/** A folder under `.claude/worktrees/` that git does not list. */
export interface Folder { path: string; link: boolean; git: boolean; processes?: Proc[]; processError?: string }
interface Slow { name: string; ms: number; tests: number }
export interface GateResult {
  gate: Gate; code: number; log: string; verdict: string; note: string; ms: number; slowest?: Slow[]; trx?: string;
  /** 'memory' when the gate died of memory; `again` while its run again alone is still to come. */
  cause?: string; again?: boolean; flakes?: string[];
}
/** What a reading of the machine's memory answers, in bytes; null where it cannot be read, and why. */
export interface Reading { physical: number | null; commit: number | null; total?: number | null; why?: string }
/** What a run's memory reading is made of, each a stand-in in a test. */
interface MemoryOptions { read?: () => Reading; sleep?: (ms: number) => Promise<void>; now?: () => number; pollMs?: number; boundMs?: number }
/** A run's wait for a gate's need of memory, before it starts or runs again. */
export type MemoryWait = (gate: Gate, options?: { again?: boolean; halted?: () => boolean }) => Promise<void>;
/** The step trees a run has running, by each step's own pid; `ended` says why once the run ended them. */
export interface Trees { ended: string | null; hold: (pid: number) => void; release: (pid: number) => boolean; end: (why: string) => void }
export type Step = (command: string, cwd: string, fd: number, options?: { raise?: ((pid: number) => void) | null; trees?: Trees | null }) => Promise<number>;
export interface Rule { paths?: string[]; lane?: string; gates: string[] | '*'; why: string }
interface Selected { gate: Gate; run: boolean; reached: boolean; why: string }
export interface Verdict {
  gate: string; verdict: string; tree: string | null; at: string; commit?: string | null; branch?: string | null;
  /** A Process half's classes: how many ran, how many it kept by reach, and the oldest verdict it kept (its own start when none). */
  classes?: { ran: number | null; kept: number; sweep: string };
}
/** One class's verdict, kept with the tree it ran on. */
export interface ClassVerdict { verdict: string; tree: string | null; at: string; commit?: string | null }
/** What a CI run does with a Process half it owes: the whole half and why, or the classes it runs and those it keeps. */
interface ClassPlan {
  whole: string | null; run: { name: string; why: string }[]; kept: { name: string; verdict: ClassVerdict; standing: string }[]; sweep: string | null;
  /** Why a half whose every class is kept passes without running, when it does. */
  composed?: string;
}
/** A half's newest gate verdict and how it stands on the tree a CI run gates. */
export interface Last { verdict: Verdict; standing: Standing }
export type Reached = { whole: { path: string; why: string }[]; classes: string[]; by: Map<string, string> };
/** The backlog merged by its rows' ids (`tools/merge-branch/backlog-merge.mjs`), the driver's `BacklogRows.Merge`'s twin. */
export type RowsMerged = {
  text: string | null; kept: number; dropped: number; both: { line: number }[];
  differ: { id: string; ours: string | null; theirs: string | null }[]; twice: string[]; unread: string[];
};
/** How a verdict given on one tree stands for another, for its gate, and the paths that say so. */
export interface Standing { standing: 'exact' | 'records' | 'unreached' | 'stale'; paths: string[]; records: string[] }
/** What a stage judges by: this tree, the paths another differs by (null when git cannot say), which are records, which reach a gate. */
export interface Judging { tree: string; drift: (from: string) => string[] | null; isRecord: (path: string) => boolean; reaching: (gate: string, paths: string[]) => string[] }
/** One gate as the stage judges it: the verdict that counts and how it stands, or a newer one that no longer does. */
export interface Judged { name: string; verdict: Verdict | null; exact: boolean; standing: Standing | null; stale: (Standing & { verdict: Verdict }) | null }

/** The build servers of its own the deployment rehearsal builds on, the twin of a run beside's. */
export const rehearsalKit = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  '../../../../tools/rehearsal-kit.mjs') as { OWN_BUILD_SERVERS: Readonly<Record<string, string>> };

/** The page's types' generator, which every web gate's pre-script runs: built into an output of its own. */
export const contracts = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  '../../../../tools/contracts.mjs') as { OWN_OUTPUT: string; generatorBuild: (output: string) => { build: string[]; built: string } };

export const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
  '../../../../tools/merge-branch.mjs') as {
  laneReport: (root: string, branch: string) => string[];
  treeDrift: (root: string, from: string, to: string) => string[] | null;
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
    /** A Process gate waits only for what builds its component, the heavy light gates beside it as memory allows. */
    byMemory?: boolean;
    waitFor?: { gates: string[]; ready: Promise<unknown> } | null;
  }) => Promise<GateResult[]>;
  /** A heavy gate waits for memory rather than failing for want of it. */
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
  /** The rehearsals a plain CI run owes a stage, and the stage's path that runs them. */
  STAGE_REHEARSALS: readonly string[];
  isStageRehearsal: (gate: { name: string; run: string }) => boolean;
  stagePlan: <T extends { name: string; run: string }>(plan: T[], options?: { light?: boolean }) => T[];
  stageCommand: (commit: string) => string;
  dotnetComponent: (run: string) => string | null;
  SHARED_PROJECTS: ReadonlyMap<string | null, readonly (string | null)[]>;
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
  timedDecision: (output: string) => { playwright: boolean; rerun: string[]; reason?: string };
  timedRerunCommand: (run: string, specs: string[]) => string;
  nodeTestDirs: (root: string, gate: Gate) => string[];
  nodeTestDecision: (output: string) => { node: boolean; rerun: { file: string; tests: string[] }[]; reason?: string };
  nodeTestRerunCommand: (file: string, title: string) => string;
  rerunCommand: (run: string, test: string) => string;
  prefixCommand: (gate: { run: string }, failure: { names: string; unit?: string; test?: string }) => string;
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
  rowTablesOf: (text: string | null) => string[];
  integrationOf: (text: string | null) => { integration: Record<string, unknown> | null; problem: string | null };
  verifyInputs: (env: Record<string, string | undefined>, options?: { besideCommit?: string | null }) => {
    commit: string; result: string; mode: string | null; base: string | null; declaration: string; tree: string | null; gates: string[] | null; failure: string | null;
  };
  /** The gates DAORIS_VERIFY_GATES names, by their declared names, in the plan's order. */
  verifyNamed: <G extends { name: string; declared: string }>(plan: G[], names: string[]) => G[];
  compositionEnvironment: () => { removed: string[]; added: Record<string, string> };
  compositionDigest: (units: string[] | null, settings: Record<string, unknown>) => string;
  verifyComposition: (facts: {
    gate: { name: string; declared?: string; run: string; cwd?: string }; runsettings?: { path: string; text: string | null } | null; units?: string[] | null; narrowed?: string[];
  }) => { units: string[] | null; settings: CompositionSettings; digest: string };
  compositionDifference: (built: CompositionSettings, kept: CompositionSettings) => string[];
  verifyRunGates: (
    plan: { name: string; declared: string }[],
    results: { gate: { name: string }; verdict: string; code?: number; log?: string }[],
    options: {
      keep?: (path: string) => string | undefined; seconds?: (name: string) => number | undefined;
      compositions: Map<string, { units: string[] | null; digest: string; file: string }>; failures: Map<string, unknown[]>;
    },
  ) => Record<string, unknown>[];
  verifyPlanAnswer: (facts: {
    plan: { name: string; declared: string }[];
    set?: { run: { name: string; why: string }[]; kept: { name: string; why: string }[]; deferred: { name: string; why: string }[] };
    kept?: { name: string; verdict: { commit?: string | null; tree?: string | null }; standing: { standing: string } }[];
    gates?: { name: string; units: string[] | null; narrowed: string[]; deferredClasses?: string[]; reach?: { whole: string | null } }[];
    composed?: { name: string; why: string; last: { verdict: { commit?: string | null; tree?: string | null } } | null }[];
    named?: string[] | null;
  }) => { run: { name: string; why: string }[]; kept: { name: string; why: string; commit: string | null; tree: string | null; standing: string | null }[]; deferred: { name: string; why: string }[] };
  verifyFailure: <G extends { name: string; declared: string }>(text: string, plan: G[]) => {
    gate: G; failure: { names: string; gate: string; unit: string; test: string; signature: Record<string, unknown> };
    composition: { units: string[] | null; settings: string; digest: string };
  };
  verifyUnfinished: (mode: string | null, facts: {
    commit: string; base?: string | null; declaration: string; why: string; plan?: { name: string; declared: string }[];
    results?: { gate: { name: string }; verdict: string; code?: number; log?: string }[]; kept?: unknown[]; deferred?: unknown[];
  }) => Record<string, unknown>;
  verifyPlan: (text: string) => { name: string; run: string; declared: string }[];
  verifyGates: (
    plan: { name: string; declared: string }[],
    results: { gate: { name: string }; verdict: string; code?: number; log?: string }[],
    options?: { keep?: (log: string) => string; seconds?: (name: string) => number | undefined },
  ) => { name: string; verdict: string; exit?: number; wallSeconds?: number; log?: string }[];
  verifyCommitsAnswer: (facts: { commit: string; base: string; declaration: string; commits: { sha: string; merge: boolean; subject: string; trailer: string }[] }) => Record<string, unknown>;
  mergeByBase: (base: string, ours: string, theirs: string) => { text: string | null; kept: number; dropped: number; both: { line: number }[] };
  mergeByRows: (base: string, ours: string, theirs: string) => RowsMerged;
  rowOf: (line: string) => { key: string; ids: string[] | null } | null;
  rowsHeld: (base: string, ours: string, theirs: string, made: string) => { twice: string[]; kept: string[]; lost: string[]; refused: RowsMerged | null };
  rowsRefused: (path: string, merged: RowsMerged) => string[];
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
  stageJudgement: (facts: Judging & { plan: string[]; verdicts: Verdict[]; now?: number; bound?: number; ageWhole?: boolean }) => { passed: boolean; missing: string[]; gates: Judged[] };
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
  readGateLock: (home: string, alive?: (pid: number) => boolean, startOf?: (pid: number) => number | null) => { pid: number; commit: string; at: string; started?: number | null; running: boolean; holder: string } | null;
  readCiWatch: (home: string, alive?: (pid: number) => boolean, startOf?: (pid: number) => number | null) => { pid: number; at: string; running: boolean } | null;
  takeGateLock: (home: string, options: {
    commit: string; holder?: string; alive?: (pid: number) => boolean; startOf?: (pid: number) => number | null; now?: () => string;
    end?: (pid: number, since: number, until: number | null) => number[]; endStrays?: (folder: string, since: number) => number[];
  }) => { stale: { pid: number; commit: string; at: string; ended: number[] } | null };
  releaseGateLock: (home: string) => void;
  gateTreeVerdict: (facts: GateTreeFacts, options?: { atMerge?: boolean }) => { remove: boolean; why: string; said?: string };
  staleDependencies: (tree: string) => string[];
  stampDependencies: (tree: string, folder: string) => void;
  keepGateLogs: (tree: string, home: string, sha: string) => boolean;
  rescueGateLogs: (tree: string, home: string) => string[];
  /** A full set beside the merges is a guest: below normal priority, its own build servers, stopped when main moves on. */
  lowerPriority: (setPriority?: (pid: number, priority: number) => void) => string | null;
  BESIDE_ENVIRONMENT: Readonly<Record<string, string>>;
  startBesideRun: (options?: { setPriority?: (pid: number, priority: number) => void; env?: Record<string, string | undefined>; say?: (line: string) => void }) => void;
  /** The raise a run beside hands a rehearsal's step, null in a run that did not lower itself. */
  rehearsalRaise: () => ((pid: number) => void) | null;
  watchTip: (commit: string | null, resolveTip?: () => string | null, options?: { onMove?: (moved: { from: string; to: string }) => void }) => {
    check: () => boolean; poll: (ms: number) => () => void; superseded: { from: string; to: string } | null;
  };
  supersededLines: (superseded: { from: string; to: string }, ran: string[], left: string[], ended?: string[]) => string[];
  besideOutcome: (code: number, superseded: { from: string; to: string } | null) => { passed: boolean; superseded: string | null };
  runStep: Step;
  /** A superseded or stopped run ends the gates it is running, each with its whole tree. */
  STEP_ENDED: number;
  TIP_POLL_MS: number;
  stepTrees: (options?: { end?: (pid: number) => void }) => Trees;
  endTree: (pid: number, options?: {
    platform?: string; run?: (command: string, args: string[], options: object) => { status?: number | null; stdout?: string | null };
    kill?: (pid: number, signal: string) => void; pause?: (ms: number) => void;
  }) => boolean;
  descendants: (listing: string, pid: number) => number[];
  stoppedLines: (ran: string[], left: string[], ended?: string[]) => string[];
  /** Whether a result is the failure that ends a stage's CI run. */
  endsStageRun: (result: GateResult) => boolean;
  stopOnInterrupt: (trees: Trees, options?: { on?: { once: (event: string, listener: () => void) => unknown; off: (event: string, listener: () => void) => unknown }; say?: (line: string) => void }) => () => void;
  /** The local CI runner, its record, its watch and the stop it is asked by. */
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
    tip: () => string | null; gated: (commit: string) => boolean; holder: () => string | null; run: (kind?: string) => Promise<number>;
    stopAsked: () => boolean; sleep: (ms: number) => Promise<void>; say: (line: string) => void; pollMs?: number; tickMs?: number; now?: () => number;
    schedule?: ((tip: string) => { kind: string; key: string; why?: string } | null) | null; report?: (() => string[]) | null;
  }) => Promise<number>;
  /** The gate store kept per gate, the scoped run's apart from the background's. */
  keptVerdicts: (verdicts: (Verdict & { kind?: string })[], keep?: number) => (Verdict & { kind?: string })[];
  VERDICTS_KEPT_PER_GATE: number;
  nightHour: (env: Record<string, string | undefined>) => number | null;
  /** A Process half run by the classes a change reaches, each class's verdict kept with its tree, a whole half at least daily. */
  CLASS_VERDICTS_FILE: string;
  SWEEP_BOUND_MS: number;
  classResults: (trx: string) => Map<string, { tests: number; failed: string[] }>;
  classVerdictsOf: (trx: string, result: { verdict: string; flakes?: string[] }) => { name: string; verdict: string }[];
  readClassVerdicts: (home: string) => { schema: number; gates: Record<string, Record<string, ClassVerdict[]>> };
  addClassVerdicts: (home: string, gate: string, entries: ({ name: string } & ClassVerdict)[], options?: { keep?: number }) => void;
  classPlan: (facts: {
    classes: string[]; verdicts: Record<string, ClassVerdict[]>; tree: string; drift: (from: string) => string[] | null;
    reached: (paths: string[]) => Reached; last?: Last | null; now?: number; bound?: number;
  }) => ClassPlan;
  composeRefusal: (facts: { classes: string[]; verdicts: Record<string, ClassVerdict[]>; last: Last | null }) => string | null;
  lastVerdict: (gate: string, verdicts: Verdict[], judging: Judging) => Last | null;
};
/** What `os.cpus()` answers of a processor, the times it spent by kind since the machine started. */
export interface CpuTimes { times: { user: number; nice: number; sys: number; idle: number; irq: number } }
/** The machine's load as a CI run measures it before its gates start: the share of its processors busy, its free memory. */
interface Load { busy: number | null; physical: number | null; commit: number | null }
/** One gate in a CI run's status file. */
export interface CiGate {
  name: string; kind: string; lane: 'light' | 'process'; state: 'kept' | 'waiting' | 'running' | 'done' | 'not run' | 'owed';
  verdict: string | null; at: string | null; ms: number | null; log: string | null; note: string | null;
  kept: { commit: string | null; tree: string | null; standing: string } | null;
}
/** A CI run's status file, `local/scratch/ci-<sha7>/status.json`, which the parent reads and a later page may. */
export interface CiStatus {
  schema: number; commit: string; tree: string; state: string; pid: number; started: string; updated: string; ended: string | null;
  since: { commit: string | null; at: string | null; paths: number | null }; light: { width: number; idle: boolean; why: string };
  stage: boolean; kind?: string; owed: string[]; gates: CiGate[]; error?: string;
}

// The stage's own question: the publish asks the merge tool's `--passed` before it builds.
export const publish = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam as the merge tool above
  '../../../../tools/desktop-publish.mjs') as { ungatedRefusal: (to: string, repoRoot: string) => { refusal: string | null; note: string | null } };

export function refusal(fn: () => unknown): { exitCode: number; message: string } {
  try {
    fn();
  } catch (error) {
    return error as { exitCode: number; message: string };
  }
  throw new Error('expected a refusal, but the call returned');
}

// The lane table's own tests (Vitest, `tools/merge/`) read the repository through `tools/merge/repo-gates.ts`; these tests share it.
export {
  repoPlan, repoLanes, selected, mergeRuns, runs, reaches, stalens, ALL, LONG, BLOCKING, KEPT_NAMES, BACKLOG_MERGES, UNION_JOINS,
  INTEGRATION_DECLARATIONS, PAGE_PARSED_NAMES, DRIVER_FOLDER, PAGE_PARSED, isPageParsedRule, trackedFiles,
} from '../../../../tools/merge/repo-gates.ts';
export const BASE = [...BASELINE];
export const ALWAYS_REACHED = unnamedGates(REACH);

export const processRun = (project: string) => `dotnet test ${project} --settings src/Daoris.Desktop/process.runsettings`;

export const gitRun = (project: string) => `dotnet test ${project} --settings src/Daoris.Desktop/git.process.runsettings`;
export const rootScripts = (): Record<string, string> =>
  (JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;

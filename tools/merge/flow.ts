/**
 * One gate of a merge, run the fast and the non-blocking way: replayed where its verdict stands on the same inputs, run after
 * the npm installs a moved lockfile owes, built once per tree, timed with its slowest classes, ended abnormally without ending the run, and its leftovers stopped. And the merge's
 * settling: which failures are main's, what the record and the summary say. `tools/merge-branch.mjs` hands this its own seams.
 */
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { BuildLedger } from './build-once.ts';
import type { ClassReplay } from './full-replay.ts';
import type { LockInstalls } from './lock-install.ts';
import { attribute, gateOutcome, mainRedLines, unspecificLines, type Held, type MainRed } from './main-red.ts';
import { buildRecord, type GateRecord, type MergeRecord } from './merge-record.ts';
import { abnormalEnd, ownedAfter, stopOwned, type Proc } from './processes.ts';
import { overBudgetNote } from './timeout.ts';
import { budgetLines, loggedRun, type Slow } from './timing.ts';
import { keyOf, replayFor, type GateDeclaration, type Replay, type StoredVerdict, type Toolchain, type TreeEntry, type VerdictKey } from './verdict-store.ts';

export type PlanGate = GateDeclaration & { [other: string]: unknown };
export type GateResult = {
  gate: PlanGate; verdict: string; note: string; ms?: number; log?: string; code?: unknown;
  failures?: Array<{ unit: string; key: string; test?: string | null }>; replayed?: Replay; classReplay?: ClassReplay; abnormal?: boolean; slowest?: Slow[];
  left?: Array<{ pid: number; created: string; stopped: boolean }>; mainRed?: boolean; key?: VerdictKey; flakes?: string[]; [other: string]: unknown;
};
type StepTrees = { ended: string | null; hold(pid: number): void; release(pid: number): boolean; end(why: string): void };

export type FlowDeps = {
  runGate: (where: string, gate: PlanGate, logs: string, options: Record<string, unknown>) => Promise<GateResult>;
  reaching: (gate: string, paths: string[]) => string[];
  reachSource: string;
  readVerdicts: () => StoredVerdict[];
  entriesOf: (where: string, tree: string) => TreeEntry[];
  diffOf: (where: string) => string[];
  toolchain: Toolchain;
  slowestOf: (trx: string) => Slow[];
  failuresOf: (where: string, result: GateResult, trx: string | null) => Array<{ unit: string; key: string; test: string | null }>;
  isProcessGate: (gate: PlanGate) => boolean;
  ledger: BuildLedger;
  listProcesses: () => Proc[];
  killTree: (pid: number) => boolean;
  /** The gate's wall-clock budget in milliseconds, or null where it has none. */
  timeoutMs: (gate: PlanGate) => number | null;
  /** The slowest classes the gate's newest record knew. */
  lastSlowest: (gate: string) => Slow[];
  installs: LockInstalls;
  /** A full set replays every gate whose key has a standing pass, not only the gates that declare `reuse`. */
  replayAll?: boolean;
};

/** A gate's run cut down to the classes no pass covers (`run` null: every class has a pass, so nothing runs), with what was replayed. */
export type Narrowed = { run: string | null; replay: ClassReplay };

/** Build servers a test step leaves by design, which the next gate's build reuses: not a leftover to stop. */
const BY_DESIGN = /VBCSCompiler|MSBuild|build-server|nodeReuse/i;
/** A gate shorter than this has no time to leave anything behind unless it failed. */
const SWEEP_AFTER_MS = 30_000;

const keyFor = (deps: FlowDeps, where: string, gate: PlanGate, tree: string): VerdictKey | null => {
  try {
    return keyOf({
      gate, entries: deps.entriesOf(where, tree), reaching: (paths) => deps.reaching(gate.name, paths), toolchain: deps.toolchain,
      extra: gate.reuse === 'inputs+diff' ? deps.diffOf(where).join('\n') : '', reachTable: deps.reachSource,
    });
  } catch {
    return null;
  }
};

export async function runOne(deps: FlowDeps, { where, gate, logs, beside, tree, trees, step, noBuild, narrow }: {
  where: string; gate: PlanGate; logs: string; beside: Record<string, unknown>; tree: string | null; trees: StepTrees | null; step: unknown; noBuild: boolean;
  /** Asked once the gate is to run; the key and the verdict stay the declared gate's. */
  narrow?: (gate: PlanGate) => Narrowed | null;
}): Promise<GateResult> {
  const key = tree ? keyFor(deps, where, gate, tree) : null;
  const standing = key && (gate.reuse || deps.replayAll) ? replayFor(deps.readVerdicts().filter((each) => each.gate === gate.name), key) : null;
  if (standing && key) {
    const log = join(logs, `${gate.name}.log`);
    writeFileSync(log, `${standing.note}\n`);
    return { gate, code: 0, log, verdict: 'PASS', note: standing.note, ms: 0, replayed: standing, key };
  }
  const owed = await deps.installs.ready(where);
  if (owed !== null) {
    const log = join(logs, `${gate.name}.log`);
    writeFileSync(log, `${owed}\n`);
    return { gate, code: null, log, verdict: 'FAIL', note: owed, ms: 0, abnormal: true };
  }

  const narrowed = narrow?.(gate) ?? null;
  if (narrowed && narrowed.run === null) {
    const note = `${narrowed.replay.replayedCount} classes replayed from standing passes of the same inputs (${narrowed.replay.runs.join(', ')}); none left to run`;
    const log = join(logs, `${gate.name}.log`);
    writeFileSync(log, `${note}\n`);
    return { gate, code: 0, log, verdict: 'PASS', note, ms: 0, classReplay: narrowed.replay, replayed: { from: 'classes', at: narrowed.replay.oldest ?? '', note }, ...(key ? { key } : {}) };
  }
  let run = narrowed?.run ?? gate.run;
  let reused = false;
  if (tree && !deps.isProcessGate(gate)) {
    const once = deps.ledger.reuse(tree, run);
    reused = once.by.length > 0;
    run = once.run;
  }
  const logged = !deps.isProcessGate(gate) ? loggedRun(run, logs.replaceAll('\\', '/'), gate.name) : { run, files: [] as string[] };
  const roots: number[] = [];
  let expired: string | null = null;
  const mine: StepTrees = {
    get ended() { return trees?.ended ?? expired; },
    hold(pid) { roots.push(pid); trees?.hold(pid); },
    release(pid) { return trees ? trees.release(pid) : false; },
    end(why) { trees?.end(why); },
  };
  const started = Date.now();
  const budget = deps.timeoutMs(gate);
  const timer = budget === null ? null : setTimeout(() => {
    expired = 'over its budget';
    for (const pid of roots) deps.killTree(pid);
  }, budget);
  const startedAt = new Date(started).toISOString();
  let result: GateResult;
  try {
    result = await deps.runGate(where, { ...gate, run: logged.run }, logs, { ...beside, noBuild: reused ? false : noBuild, trees: mine, step });
  } catch (error) {
    result = { gate, code: null, log: join(logs, `${gate.name}.log`), verdict: 'FAIL', note: '', ms: Date.now() - started, abnormal: true };
    result.note = `ended abnormally: ${error instanceof Error ? error.message : String(error)}; the run goes on with the next gate`;
  }
  if (timer) clearTimeout(timer);
  result.gate = gate;
  if (key) result.key = key;
  if (narrowed) result.classReplay = narrowed.replay;
  if (expired !== null && budget !== null && !trees?.ended) {
    Object.assign(result, { verdict: 'FAIL', overBudget: true, again: false, note: overBudgetNote(Date.now() - started, budget, deps.lastSlowest(gate.name)) });
    result.slowest = deps.lastSlowest(gate.name);
  }
  if (!result.overBudget && result.verdict === 'FAIL' && typeof result.code === 'number' && abnormalEnd(result.code)) {
    result.abnormal = true;
    result.note = `ended abnormally (exit ${result.code}: a signal or a crash, not a check's verdict); ${result.note}`.replace(/; $/, '');
  }

  const trxFiles = logged.files.filter((file) => existsSync(file));
  if (trxFiles.length && !result.slowest?.length) {
    result.slowest = trxFiles.flatMap((file) => deps.slowestOf(readFileSync(file, 'utf8'))).sort((a, b) => b.ms - a.ms).slice(0, 5);
  }
  if (result.verdict === 'PASS' && tree) deps.ledger.passed(gate.name, tree, gate.run);
  if (result.verdict === 'FAIL' && !result.abnormal) result.failures = deps.failuresOf(where, result, trxFiles[0] ?? null);

  if (roots.length && ((result.ms ?? 0) >= SWEEP_AFTER_MS || result.verdict !== 'PASS')) {
    const after = deps.listProcesses();
    const owned = ownedAfter({ before: [], after, roots, startedAt }).filter((proc) => !BY_DESIGN.test(proc.command));
    if (owned.length) result.left = stopOwned(owned.map(({ pid, created }) => ({ pid, created })), { list: () => after, kill: deps.killTree });
  }
  return result;
}

/** The fields a gate's verdict adds to the store beside its tree: the key it carries, and the signatures of a failure. */
export function verdictExtras(result: GateResult): { key?: VerdictKey; signatures?: string[]; replayedFrom?: string } {
  return {
    ...(result.key ? { key: result.key } : {}),
    ...(result.verdict === 'FAIL' && result.failures?.length ? { signatures: result.failures.map((each) => each.key) } : {}),
    ...(result.replayed ? { replayedFrom: result.replayed.from } : {}),
  };
}

const gateRecord = (result: GateResult): GateRecord => ({
  name: result.gate.name, verdict: result.verdict, ms: result.ms ?? 0, replayed: result.replayed !== undefined, mainRed: result.mainRed === true,
  abnormal: result.abnormal === true, slowest: result.slowest ?? [], over: false, left: result.left ?? [], flakes: result.flakes ?? [],
});

/**
 * A merge's gates settled: each failure main already has (its test, its signature) set down as main's, the failed gates left
 * for the branch's, the record and the summary's lines built. `wallMs` is the time the merge's gating took on the clock.
 */
export function settleMerge({ branch, at, wallMs, results, mains, previous }: {
  branch: string; at: string; wallMs: number; results: GateResult[]; mains: MainRed[]; previous: MergeRecord[];
}): { failed: GateResult[]; held: Held[]; lines: string[]; record: MergeRecord } {
  const ms = results.reduce((sum, result) => sum + (result.ms ?? 0), 0);
  const held: Held[] = [];
  for (const result of results) {
    if (result.verdict !== 'FAIL' || result.abnormal) continue;
    const failures = (result.failures ?? []).map((each) => ({ gate: result.gate.name, unit: each.unit, key: each.key, test: each.test ?? null }));
    if (gateOutcome(failures, mains) === 'main') {
      result.mainRed = true;
      held.push(...attribute(failures, mains).main);
    }
  }
  const records = results.map(gateRecord);
  const record = buildRecord({
    branch, at, ms, wallMs, gates: records, previous, mainReds: held.map((each) => ({ gate: each.gate, unit: each.unit, key: each.key })),
  });
  const timed = results.map((result) => ({ name: result.gate.name, ms: result.ms ?? 0, slowest: result.slowest ?? [] }));
  const failed = results.filter((result) => result.verdict === 'FAIL' && !result.mainRed);
  return {
    failed, held, record,
    lines: [...mainRedLines(held), ...unspecificLines(mains, failed.map((result) => result.gate.name)), ...budgetLines(timed, ms)],
  };
}

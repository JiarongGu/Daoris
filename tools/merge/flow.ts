/**
 * One gate of a merge, run the fast and the non-blocking way: replayed where its verdict stands on the same inputs, run after
 * the npm installs a moved lockfile owes, built once per tree, timed with its slowest classes, ended abnormally without ending the run, and its leftovers stopped. And the merge's
 * settling: which failures are main's, what the record, the summary and the time breaches say. `tools/merge-branch.mjs` hands this its own seams.
 */
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  breachLines, gateBreach, hangBreaches, hungUnderLoad, parseBreaches, separations, sourceReader, writeBreaches, type Breach, type LoadSeam, type Separation,
} from '../time-why.mjs';
import { ALONE_HOLDS, stampLoad } from '../time-load.mjs';
import { BuildLedger } from './build-once.ts';
import type { ClassReplay } from './full-replay.ts';
import type { LockInstalls } from './lock-install.ts';
import { attribute, gateOutcome, mainRedLines, unspecificLines, type Held, type MainRed } from './main-red.ts';
import { buildRecord, type GateRecord, type MergeRecord } from './merge-record.ts';
import { abnormalEnd, ownedAfter, stopOwned, type Proc } from './processes.ts';
import { parseOutcome, type ClassOutcome } from './reached-classify.ts';
import { parseSteps, type Step } from './reached-steps.ts';
import { againNote, cappedNote, LOAD_CAP, overBudgetNote, ranOnNote, waitFree } from './timeout.ts';
import { budgetLines, loggedRun, type Slow } from './timing.ts';
import { settleVerification, type Planned } from './verification.ts';
import { keyOf, replayFor, type GateDeclaration, type Replay, type StoredVerdict, type Toolchain, type TreeEntry, type VerdictKey } from './verdict-store.ts';

export type PlanGate = GateDeclaration & { [other: string]: unknown };
export type GateResult = {
  gate: PlanGate; verdict: string; note: string; ms?: number; log?: string; code?: unknown;
  failures?: Array<{ unit: string; key: string; test?: string | null }>; replayed?: Replay; classReplay?: ClassReplay; abnormal?: boolean; slowest?: Slow[];
  left?: Array<{ pid: number; created: string; stopped: boolean }>; mainRed?: boolean; classes?: ClassOutcome; key?: VerdictKey; flakes?: string[];
  steps?: Step[]; breaches?: Breach[]; [other: string]: unknown;
};
type StepTrees = { ended: string | null; hold(pid: number): void; release(pid: number): boolean; end(why: string): void };

export type FlowDeps = {
  runGate: (where: string, gate: PlanGate, logs: string, options: Record<string, unknown>) => Promise<GateResult>;
  reaching: (gate: string, paths: string[]) => string[];
  reachSource: string;
  readVerdicts: () => StoredVerdict[];
  entriesOf: (where: string, tree: string) => TreeEntry[];
  diffOf: (where: string, base?: string) => string[];
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
  source: (where: string) => ReturnType<typeof sourceReader>;
  load?: LoadSeam;
  replayAll?: boolean;
  privateOf?: (where: string) => string;
};

/** A gate's run cut down to the classes no pass covers (`run` null: every class has one, so nothing runs), with the replay. */
export type Narrowed = { run: string | null; replay: ClassReplay };

/** Build servers a test step leaves by design, which the next gate's build reuses: not a leftover to stop. */
const BY_DESIGN = /VBCSCompiler|MSBuild|build-server|nodeReuse/i;
/** A gate shorter than this has no time to leave anything behind unless it failed. */
const SWEEP_AFTER_MS = 30_000;
const TIMING_DIR = '_fixtures/test-timing';
const TIMING_FOLDER = /_fixtures\/test-timing/;
const HANG_SAID = /The specified inactivity time of \d+ (?:seconds?|minutes?) has elapsed/;

const keyFor = (deps: FlowDeps, where: string, gate: PlanGate, tree: string, base?: string): VerdictKey | null => {
  try {
    return keyOf({
      gate, entries: deps.entriesOf(where, tree), reaching: (paths) => deps.reaching(gate.name, paths), toolchain: deps.toolchain,
      extra: `${gate.reuse === 'inputs+diff' ? deps.diffOf(where, base).join('\n') : ''}\0${deps.privateOf?.(where) ?? ''}`, reachTable: deps.reachSource,
    });
  } catch {
    return null;
  }
};

export const withBase = (run: string, base = 'HEAD'): string => run.replaceAll('{base}', base);

export async function runOne(deps: FlowDeps, { where, gate, logs, beside, tree, trees, step, noBuild, narrow, base }: {
  where: string; gate: PlanGate; logs: string; beside: Record<string, unknown>; tree: string | null; trees: StepTrees | null; step: unknown; noBuild: boolean;
  narrow?: (gate: PlanGate) => Narrowed | null; base?: string;
}): Promise<GateResult> {
  const key = tree ? keyFor(deps, where, gate, tree, base) : null;
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
  const first = (beside.again as GateResult | undefined)?.cause === 'load' ? beside.again as GateResult : null;
  const { again: _again, ...fresh } = beside;
  const waited = first && deps.load ? await waitFree(deps.load, () => Boolean(trees?.ended)) : null;
  const started = Date.now();
  const budget = deps.timeoutMs(gate);
  const load = { sampler: null as ReturnType<LoadSeam['sample']> | null };
  const stamps = TIMING_FOLDER.test(logged.run) && deps.load !== undefined;
  if (stamps) load.sampler = deps.load?.sample() ?? null;
  const watch = stamps || budget === null || !deps.load ? null : setTimeout(() => { load.sampler = deps.load?.sample() ?? null; }, budget / 2);
  const kill = (why: string) => {
    expired = why;
    for (const pid of roots) deps.killTree(pid);
  };
  let ranOn: number | null = null;
  let cap: ReturnType<typeof setTimeout> | null = null;
  const timer = budget === null ? null : setTimeout(() => {
    const seen = load.sampler?.peak() ?? null;
    if (seen === null || seen <= ALONE_HOLDS) return kill('over its budget');
    ranOn = seen;
    cap = setTimeout(() => kill('over its cap'), budget * (LOAD_CAP - 1));
  }, budget);
  const startedAt = new Date(started).toISOString();
  let result: GateResult;
  try {
    result = await deps.runGate(where, { ...gate, run: logged.run }, logs, { ...(first ? fresh : beside), noBuild: reused ? false : noBuild, trees: mine, step, ...(base ? { base } : {}) });
  } catch (error) {
    result = { gate, code: null, log: join(logs, `${gate.name}.log`), verdict: 'FAIL', note: '', ms: Date.now() - started, abnormal: true };
    result.note = `ended abnormally: ${error instanceof Error ? error.message : String(error)}; the run goes on with the next gate`;
  }
  for (const each of [timer, cap, watch]) if (each) clearTimeout(each);
  const said = result.log && existsSync(result.log) ? readFileSync(result.log, 'utf8') : '';
  if (!load.sampler && deps.load && HANG_SAID.test(said)) load.sampler = deps.load.sample();
  const peak = load.sampler ? load.sampler.peak() : null;
  load.sampler?.stop();
  if (stamps && peak !== null) stampLoad(join(where, TIMING_DIR), peak, started);
  result.gate = gate;
  if (key) result.key = key;
  if (narrowed) result.classReplay = narrowed.replay;
  const outcome = said ? parseOutcome(said) : null;
  const steps = outcome?.steps ?? parseSteps(said, Date.now() - started);
  if (steps.length) result.steps = steps;
  const breaches = [...parseBreaches(said), ...hangBreaches(said, deps.source(where), peak, { alone: first !== null })];
  if (hungUnderLoad(breaches) && expired === null && !first) {
    Object.assign(result, { verdict: 'FAIL', again: true, cause: 'load', note: `${result.note}; killed by the hang bound under load (${peak} heavy runs at its peak): run again alone once every other gate has ended and the machine is free`.replace(/^; /, '') });
  }
  const ranMs = Date.now() - started;
  if (expired !== null && budget !== null && !trees?.ended) {
    const breach = gateBreach({ gate: gate.name, ms: ranMs, budget, steps, peak, fails: true });
    const note = overBudgetNote(ranMs, budget, deps.lastSlowest(gate.name), steps) + (ranOn !== null ? cappedNote(ranOn) : '');
    Object.assign(result, { verdict: 'FAIL', overBudget: true, again: false, note, ...(breach.cause === 'load' ? { cause: 'load' } : {}) });
    result.slowest = deps.lastSlowest(gate.name);
    breaches.push(breach);
  } else {
    if (ranOn !== null && budget !== null) {
      breaches.push(gateBreach({ gate: gate.name, ms: ranMs, budget, steps, peak: Math.max(peak ?? 0, ranOn), fails: false }));
      result.note = [result.note, ranOnNote(ranMs, budget, ranOn)].filter(Boolean).join('; ');
    }
    if (first) result.note = againNote(result.verdict, waited ?? { free: true, ms: 0 }, result.note);
  }
  if (first) {
    const ran = result.overBudget ? 'killed again' : result.verdict === 'PASS' ? 'passed' : `ended ${result.verdict}`;
    const carried = (first.breaches ?? []).map((each) => (each.kind === 'hang' && each.cause === 'load' ? { ...each, evidence: `${each.evidence}; run again alone: ${ran}` } : each));
    breaches.unshift(...carried);
    result.ms = (first.ms ?? 0) + (result.ms ?? 0);
  }
  if (breaches.length) result.breaches = breaches;
  if (!result.overBudget && result.verdict === 'FAIL' && typeof result.code === 'number' && abnormalEnd(result.code)) {
    result.abnormal = true;
    result.note = `ended abnormally (exit ${result.code}: a signal or a crash, not a check's verdict); ${result.note}`.replace(/; $/, '');
  }

  const trxFiles = logged.files.filter((file) => existsSync(file));
  if (trxFiles.length && !result.slowest?.length) {
    result.slowest = trxFiles.flatMap((file) => deps.slowestOf(readFileSync(file, 'utf8'))).sort((a, b) => b.ms - a.ms).slice(0, 5);
  }
  if (outcome) {
    result.classes = outcome;
    if (result.verdict === 'PASS' && outcome.flaked.length) {
      Object.assign(result, { verdict: 'FLAKE', flakes: outcome.flaked, note: `${outcome.flaked.length} class(es) failed and passed alone: ${outcome.flaked.join(', ')}; ${result.note}`.replace(/; $/, '') });
    }
  }
  if (result.verdict === 'PASS' && tree) deps.ledger.passed(gate.name, tree, gate.run);
  if (outcome?.built?.length && tree) deps.ledger.passed(gate.name, tree, outcome.built.map((project) => `dotnet test ${project}`).join(' && '));
  if (result.verdict === 'FAIL' && !result.abnormal) result.failures = deps.failuresOf(where, result, trxFiles[0] ?? null);

  if (roots.length && ((result.ms ?? 0) >= SWEEP_AFTER_MS || result.verdict !== 'PASS')) {
    const after = deps.listProcesses();
    const owned = ownedAfter({ before: [], after, roots, startedAt }).filter((proc) => !BY_DESIGN.test(proc.command));
    if (owned.length) result.left = stopOwned(owned.map(({ pid, created }) => ({ pid, created })), { list: () => after, kill: deps.killTree });
  }
  return result;
}

/** Why a gate's first result runs it again alone once every other gate has ended, said in the run's line. */
export const againWhy = (first: GateResult): string => (first.nodes ? 'whose node tests failed' : first.timed ? 'whose timed specs failed'
  : first.cause === 'load' ? 'killed by its hang bound under load' : 'which died of memory');

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
  ...(result.classes ? { classes: { ran: result.classes.ran, replayed: result.classes.replayed, flaked: result.classes.flaked.length, mainRed: result.classes.mainRed.length, red: result.classes.red.length, deferred: result.classes.deferred.length } } : {}),
  ...(result.steps?.length ? { steps: result.steps } : {}),
});

/** The time breaches of a run's gates with the separation candidates they make, written to `breaches.json` in `dir` where given. */
function breachReport(results: readonly GateResult[], dir: string | null): { breaches: Breach[]; found: Separation[]; file: string | null } {
  const breaches = results.flatMap((result) => result.breaches ?? []);
  const found = separations(breaches);
  return { breaches, found, file: dir ? writeBreaches(dir, breaches, found) : null };
}

/** What the queue's scope-run record carries of its gates' time breaches, written beside their logs in `dir`: nothing where there is none. */
export function reportBreaches(results: readonly GateResult[], dir: string | null = null): { breaches?: Breach[]; separations?: Separation[] } {
  const { breaches, found } = breachReport(results, dir);
  return { ...(breaches.length ? { breaches } : {}), ...(found.length ? { separations: found } : {}) };
}

/**
 * A merge's gates settled: each failure main already has (its test, its signature) set down as main's, the failed gates left
 * for the branch's, the record and the summary's lines built, the breaches written in `dir`. `wallMs` is the gating's wall time.
 */
export function settleMerge({ branch, at, wallMs, results, mains, previous, dir = null, verification = null }: {
  branch: string; at: string; wallMs: number; results: GateResult[]; mains: MainRed[]; previous: MergeRecord[]; dir?: string | null;
  verification?: (Planned & { file: string; shown: string }) | null;
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
  const { breaches, found, file } = breachReport(results, dir);
  const timed = results.map((result) => ({ name: result.gate.name, ms: result.ms ?? 0, slowest: result.slowest ?? [], steps: result.steps ?? [] }));
  const verified = verification ? settleVerification(verification, { branch, at, wallMs, gates: timed, file: verification.file, shown: verification.shown }) : null;
  const record = buildRecord({
    branch, at, ms, wallMs, gates: records, previous, mainReds: held.map((each) => ({ gate: each.gate, unit: each.unit, key: each.key })),
    deferred: results.flatMap((result) => result.classes?.deferred ?? []), breaches, separations: found, ...(verified ? { verification: verified.record } : {}),
  });
  const failed = results.filter((result) => result.verdict === 'FAIL' && !result.mainRed);
  return {
    failed, held, record,
    lines: [
      ...mainRedLines(held), ...unspecificLines(mains, failed.map((result) => result.gate.name)), ...(verified?.lines ?? []), ...budgetLines(timed),
      ...breachLines(breaches, found, { file }),
    ],
  };
}

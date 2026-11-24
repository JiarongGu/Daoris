/**
 * This workspace's verification rule, read from `daoris.gates.json`'s `verifyBudget`: a task's verification
 * stays within its budget and is scoped to what its paths reach (the light gates on its paths and the reached classes before
 * the landing, the heavy tier after it); a full run is taken only for a reason the change bears (a declared wide path, a reach
 * of the declared share or more) or one its kind is (the run after a landing, a release), and says it; and a verification past
 * its budget is said with where its time went and filed as a finding the steward reads. A repository whose gates file sets no
 * `verifyBudget` has no rule, and nothing here applies to it. The rule is data this unit reads; the merge tool and the reached
 * checks wire it, until a supervisor actor keeps it.
 */
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { laneMatcher } from './lanes-file.ts';
import { stepsLine, type Step } from './reached-steps.ts';

// test-map: strings name paths as data — the gates file is the declaration this unit reads, and the reasons name commands

/** The rule as read: the budget, the paths whose change is code-wide, the share of a reach that is, the runs a reason by their kind, and the findings file. */
export type Verification = { budgetMs: number; widePaths: readonly string[]; reachShare: number; runs: readonly RunKind[]; overruns: string };
export type RunKind = 'after-landing' | 'release';

export type ReasonKind = 'wide-path' | 'reach-share' | 'after-landing' | 'release';
/** Why a run is full, as the summary says it and the record keeps it. */
export type FullReason = { kind: ReasonKind; said: string };
/** What a run ran: the full set, or what its paths reach. A change may bear a full run's reason and still run its reach, and a batch's `--full` runs full a branch that bears none. */
export type Ran = 'full' | 'reach';
/** How much of what a run could leave out the change's paths reach: the gates the lane table can skip at a merge, the census's classes in the reached run. */
export type Reach = { reached: number; of: number; what: string };
/** A gate's time as the overrun reads it: the reached run's steps where it logged them. */
export type Timed = { name: string; ms: number; steps?: readonly Step[] };
/**
 * What the merge record keeps of the rule: the budget, the wall time, whether it ran over, the scope the change calls for (`full`
 * where it bears a reason, kept with the reason), and what ran.
 */
export type VerificationRecord = { budgetMs: number; wallMs: number; over: boolean; scope: Ran; ran: Ran; reason?: string; kind?: ReasonKind };
/** One overrun as it is filed: the run, its record and where its time went. */
export type Overrun = VerificationRecord & { at: string; branch: string; gates: { name: string; ms: number; steps?: readonly Step[] }[] };

export const GATES_FILE = 'daoris.gates.json';
const DEFAULT_OVERRUNS = 'local/verify-overruns.jsonl';

/** The runs that are a full run's reason by their kind, not by a diff. */
export const RUN_REASONS: Readonly<Record<RunKind, FullReason>> = Object.freeze({
  'after-landing': { kind: 'after-landing', said: 'the run after a landing (--ci, --full --beside)' },
  release: { kind: 'release', said: 'a release (--full on the checkout, --ci --stage)' },
});

const strings = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string' && each.length > 0) : []);

type Declared = { budgetMinutes?: unknown; fullRunReasons?: { widePaths?: unknown; reachShare?: unknown; runs?: unknown } };

/** A gates file's `verifyBudget` as written, or undefined where it sets none (or is not JSON). */
function declaredOf(gatesText: string): Declared | undefined {
  try {
    return (JSON.parse(gatesText) as { verifyBudget?: Declared } | null)?.verifyBudget ?? undefined;
  } catch {
    return undefined;
  }
}


/**
 * The paths a gates file's text declares wide, read apart from the budget: the lane table's every-gate rule reads them as its
 * one source, so a budget missing or mistyped never drops them. None where no `verifyBudget` is declared; a `verifyBudget`
 * declared without them is an error, said at once, rather than every merge silently reaching too little.
 */
export function declaredWidePaths(gatesText: string): readonly string[] {
  const declared = declaredOf(gatesText);
  if (!declared) return Object.freeze([]);
  const raw = declared.fullRunReasons?.widePaths;
  const paths = strings(raw);
  if (!Array.isArray(raw) || !paths.length || paths.length !== raw.length) {
    throw new Error(`${GATES_FILE} declares a verifyBudget with no fullRunReasons.widePaths (a list of paths): the lane table's every-gate rule reads them (D349)`);
  }
  return Object.freeze(paths);
}

/** The wide paths of the repository at `root`, none where its gates file is missing or sets no rule. */
export function readWidePaths(root: string): readonly string[] {
  const file = join(root, GATES_FILE);
  return existsSync(file) ? declaredWidePaths(readFileSync(file, 'utf8')) : Object.freeze([]);
}

/**
 * The rule a gates file's text declares, or null where it declares none (or no budget): nothing applies then. A findings file
 * declared outside the checkout is an error, said at once.
 */
export function verifyBudgetOf(gatesText: string): Verification | null {
  const declared = declaredOf(gatesText);
  const minutes = declared?.budgetMinutes;
  if (!declared || typeof minutes !== 'number' || !(minutes > 0)) return null;
  const share = declared.fullRunReasons?.reachShare;
  return {
    budgetMs: Math.round(minutes * 60_000),
    widePaths: Object.freeze(strings(declared.fullRunReasons?.widePaths)),
    reachShare: typeof share === 'number' && share > 0 && share <= 1 ? share : 1,
    runs: Object.freeze(strings(declared.fullRunReasons?.runs).filter((run): run is RunKind => Object.hasOwn(RUN_REASONS, run))),
    overruns: DEFAULT_OVERRUNS,
  };
}

/** The rule of the repository at `root`, or null where its gates file is missing or sets none. */
export function readVerifyBudget(root: string): Verification | null {
  const file = join(root, GATES_FILE);
  return existsSync(file) ? verifyBudgetOf(readFileSync(file, 'utf8')) : null;
}

/** Of `changed`, the paths the rule declares wide. */
export function widePathsIn(rule: Verification, changed: readonly string[]): string[] {
  if (!rule.widePaths.length) return [];
  const wide = laneMatcher([...rule.widePaths]);
  return changed.filter((path) => wide(path));
}

const percent = (share: number): number => Math.round(share * 100);
const count = (n: number, word: string): string => `${n} ${word}${n === 1 ? '' : 's'}`;
const minutes = (ms: number): string => `${Number((ms / 60_000).toFixed(1))}`;
const budgetSaid = (rule: Verification): string => `budget ${minutes(rule.budgetMs)} min (D349)`;

/** The reason the change bears for a full run, or null: a declared wide path first, then a reach of the declared share or more. */
export function fullReason(rule: Verification, changed: readonly string[], reach?: Reach): FullReason | null {
  const wide = widePathsIn(rule, changed);
  if (wide.length === 1) return { kind: 'wide-path', said: `${wide[0]} is a declared wide path` };
  if (wide.length > 1) return { kind: 'wide-path', said: `${wide[0]} and ${wide.length - 1} more are declared wide paths` };
  if (reach && reach.of > 0 && reach.reached / reach.of >= rule.reachShare) {
    return { kind: 'reach-share', said: `its paths reach ${reach.reached} of ${reach.of} ${reach.what} (${percent(reach.reached / reach.of)}%), at or over the declared ${percent(rule.reachShare)}%` };
  }
  return null;
}

/**
 * The line a plan and a merge say of the verification's scope, by what it runs: full with its reason (or its batch's, where
 * this change bears none), or scoped to the reach it measured, naming a full run's reason the change bears and does not take.
 */
export function scopeLine(rule: Verification, changed: readonly string[], reach: Reach | undefined, reason: FullReason | null, ran: Ran): string {
  const paths = `${count(changed.length, 'path')} ${changed.length === 1 ? 'reaches' : 'reach'}`;
  const reached = reach ? `${paths} ${reach.reached} of ${reach.of} ${reach.what}` : count(changed.length, 'path');
  if (ran === 'full') {
    return reason
      ? `verification: full: ${reason.said}; ${budgetSaid(rule)}`
      : `verification: full: its batch's --full, for a reason another of its branches bears; its own ${reached}; ${budgetSaid(rule)}`;
  }
  const bears = reason ? `; it bears a full run's reason (${reason.said})` : '';
  return `verification: scoped to its reach: ${reached}${bears}; the light gates on its paths and the reached classes now, the heavy tier after the landing; ${budgetSaid(rule)}`;
}

const listed = (paths: readonly string[], most = 6): string => (paths.length <= most
  ? paths.join(', ')
  : `${paths.slice(0, most).join(', ')} and ${paths.length - most} more in ${GATES_FILE}'s verifyBudget`);

/** Why `--full` is refused for `changed`, naming the reasons it accepts; null where the change bears one. */
export function fullRefusal(rule: Verification, changed: readonly string[], reach: Reach): string | null {
  if (fullReason(rule, changed, reach)) return null;
  const bears = changed.length === 1 ? 'bears' : 'bear';
  const accepted = [
    `a declared wide path (${listed(rule.widePaths)})`, `a reach of ${percent(rule.reachShare)}% or more of the ${reach.what}`, ...rule.runs.map((run) => RUN_REASONS[run].said),
  ];
  return [
    `--full needs a reason the change bears (D349), and its ${count(changed.length, 'path')} ${bears} none: it reaches ${reach.reached} of ${reach.of} ${reach.what}.`,
    `The reasons it accepts: ${accepted.slice(0, -1).join(', ')} or ${accepted.at(-1)}.`,
    'Merge without --full: its verification runs scoped to its reach, the heavy tier after the landing.',
  ].join(' ');
}

/** What the merge record keeps of the rule for one run. */
export function verificationRecord(rule: Verification, { wallMs, reason, ran }: { wallMs: number; reason: FullReason | null; ran: Ran }): VerificationRecord {
  return {
    budgetMs: rule.budgetMs, wallMs, over: wallMs > rule.budgetMs, scope: reason ? 'full' : 'reach', ran,
    ...(reason ? { reason: reason.said, kind: reason.kind } : {}),
  };
}

const byTime = <T extends { ms: number }>(list: readonly T[]): T[] => list.filter((each) => each.ms > 0).sort((a, b) => b.ms - a.ms);

/** Why a run went over, read from what it ran and the reason its change bears. */
function overrunCause(reason: FullReason | null, ran: Ran): string {
  if (ran === 'full') return reason ? `ran full: ${reason.said}` : "ran full for its batch's --full, a reason another of its branches bears";
  return reason
    ? `ran scoped to its reach, and the change is code-wide (${reason.said}): its reach is most of the gates`
    : 'ran scoped to its reach, so the scope was too big for the change or the change is code-wide and should have said so';
}

/**
 * The lines a summary says of a verification past its budget, none within it: its time and why it went over, where its time
 * went (each gate by its time, the reached run's steps beside it), and the file it is filed in, or why it could not be.
 */
export function overrunLines(rule: Verification, { wallMs, gates, reason, ran, file, unfiled = null }: {
  wallMs: number; gates: readonly Timed[]; reason: FullReason | null; ran: Ran; file: string; unfiled?: string | null;
}): string[] {
  if (wallMs <= rule.budgetMs) return [];
  const went = byTime(gates).map((gate) => {
    const steps = byTime(gate.steps ?? []);
    return `${gate.name} ${minutes(gate.ms)} min${steps.length ? ` (${stepsLine(steps)})` : ''}`;
  });
  return [
    `  verification: ${minutes(wallMs)} min, over this workspace's ${minutes(rule.budgetMs)} min budget (D349); its scope: ${overrunCause(reason, ran)}`,
    `    where its time went: ${went.join(', ') || 'no gate recorded its time'}`,
    unfiled === null ? `    filed in ${file} for the steward` : `    NOT filed in ${file} for the steward (${unfiled}): this summary and the merge record keep it`,
  ];
}

/** Files one overrun: a line appended to the findings file the steward reads, the folder made where none stands. */
export function fileOverrun(file: string, overrun: Overrun): void {
  mkdirSync(dirname(file), { recursive: true });
  appendFileSync(file, `${JSON.stringify(overrun)}\n`);
}

/**
 * A run's verification as planned: the rule, the reason its change bears (or its kind is) for a full run, or null, what it runs,
 * and the line it says; null where the repository sets no rule.
 */
export type Planned = { rule: Verification; reason: FullReason | null; ran: Ran; line: string };

/** `kind`: a reason the run is by its kind, which runs full; `ran`: what it runs otherwise, its reach unless named. */
export function planVerification(rule: Verification | null, changed: readonly string[], reach: Reach | undefined, { kind = null, ran }: { kind?: FullReason | null; ran?: Ran } = {}): Planned | null {
  if (!rule) return null;
  const reason = kind ?? fullReason(rule, changed, reach);
  const runs: Ran = ran ?? (kind ? 'full' : 'reach');
  return { rule, reason, ran: runs, line: scopeLine(rule, changed, reach, reason, runs) };
}

/**
 * A run's verification settled as it ends: what its record keeps, the lines its summary says and, past the budget, the overrun
 * filed in `file` (`shown` is how the summary names it). A file that cannot be written is said, never thrown: the gates have
 * run, and the record keeps the overrun.
 */
export function settleVerification(planned: Planned, { branch, at, wallMs, gates, file, shown }: {
  branch: string; at: string; wallMs: number; gates: readonly Timed[]; file: string; shown: string;
}): { record: VerificationRecord; lines: string[] } {
  const record = verificationRecord(planned.rule, { wallMs, reason: planned.reason, ran: planned.ran });
  let unfiled: string | null = null;
  if (record.over) {
    try {
      fileOverrun(file, { at, branch, ...record, gates: gates.map(({ name, ms, steps }) => ({ name, ms, ...(steps?.length ? { steps } : {}) })) });
    } catch (error) {
      unfiled = error instanceof Error ? error.message : String(error);
    }
  }
  return { record, lines: overrunLines(planned.rule, { wallMs, gates, reason: planned.reason, ran: planned.ran, file: shown, unfiled }) };
}

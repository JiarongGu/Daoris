/**
 * This workspace's verification rule, read from `daoris.gates.json`'s `verification`: a task's verification
 * stays within its budget and is scoped to what its paths reach (the light gates on its paths and the reached classes before
 * the landing, the heavy tier after it); a full run is taken only for a reason the change bears (a declared wide path, a reach
 * of the declared share or more) or one its kind is (the run after a landing, a release), and says it; and a verification past
 * its budget is said with where its time went and filed as a finding the steward reads. A repository whose gates file sets no
 * `verification` has no rule, and nothing here applies to it. The rule is data this unit reads; the merge tool and the reached
 * checks wire it, until a supervisor actor keeps it.
 */
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { laneMatcher } from './lanes-file.ts';
import { stepsLine, type Step } from './reached-steps.ts';

// test-map: strings name paths as data — the gates file is the declaration this unit reads, and the reasons name commands

/** The rule as read: the budget, the paths whose change is code-wide, the share of a reach that is, and the findings file. */
export type Verification = { budgetMs: number; widePaths: readonly string[]; reachShare: number; overruns: string };

export type ReasonKind = 'wide-path' | 'reach-share' | 'after-landing' | 'release';
/** Why a run is full, as the summary says it and the record keeps it. */
export type FullReason = { kind: ReasonKind; said: string };
/** How much of what a run would run the change's paths reach: the lane table's gates at a merge, the census's classes in the reached run. */
export type Reach = { reached: number; of: number; what: string };
/** A gate's time as the overrun reads it: the reached run's steps where it logged them. */
export type Timed = { name: string; ms: number; steps?: readonly Step[] };
/** What the merge record keeps of the rule: the budget, the wall time, whether it ran over, the scope and a full run's reason. */
export type VerificationRecord = { budgetMs: number; wallMs: number; over: boolean; scope: 'reach' | 'full'; reason?: string; kind?: ReasonKind };
/** One overrun as it is filed: the run, its record and where its time went. */
export type Overrun = VerificationRecord & { at: string; branch: string; gates: { name: string; ms: number; steps?: readonly Step[] }[] };

export const GATES_FILE = 'daoris.gates.json';
const DEFAULT_OVERRUNS = 'local/verify-overruns.jsonl';

/** The runs that are a full run's reason by their kind, not by a diff. */
export const RUN_REASONS: Readonly<Record<'after-landing' | 'release', FullReason>> = Object.freeze({
  'after-landing': { kind: 'after-landing', said: 'the run after a landing (--ci, --full --beside)' },
  release: { kind: 'release', said: 'a release (--full on the checkout, --ci --stage)' },
});

const strings = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string' && each.length > 0) : []);

/** The rule a gates file's text declares, or null where it declares none (or no budget): nothing applies then. */
export function verificationOf(gatesText: string): Verification | null {
  let parsed: { verification?: { budgetMinutes?: unknown; overruns?: unknown; fullRunReasons?: { widePaths?: unknown; reachShare?: unknown } } };
  try {
    parsed = JSON.parse(gatesText);
  } catch {
    return null;
  }
  const declared = parsed?.verification;
  const minutes = declared?.budgetMinutes;
  if (!declared || typeof minutes !== 'number' || !(minutes > 0)) return null;
  const share = declared.fullRunReasons?.reachShare;
  return {
    budgetMs: Math.round(minutes * 60_000),
    widePaths: Object.freeze(strings(declared.fullRunReasons?.widePaths)),
    reachShare: typeof share === 'number' && share > 0 && share <= 1 ? share : 1,
    overruns: typeof declared.overruns === 'string' && declared.overruns ? declared.overruns : DEFAULT_OVERRUNS,
  };
}

/** The rule of the repository at `root`, or null where its gates file is missing or sets none. */
export function readVerification(root: string): Verification | null {
  const file = join(root, GATES_FILE);
  return existsSync(file) ? verificationOf(readFileSync(file, 'utf8')) : null;
}

/** Of `changed`, the paths the rule declares wide. */
export function widePathsIn(rule: Verification, changed: readonly string[]): string[] {
  if (!rule.widePaths.length) return [];
  const wide = laneMatcher([...rule.widePaths]);
  return changed.filter((path) => wide(path));
}

/** How many of a selection's gates the lane table reaches: a merge's measure of a change's reach. */
export const gateReach = (selection: readonly { reached: boolean }[]): Reach => ({ reached: selection.filter((entry) => entry.reached).length, of: selection.length, what: 'gates' });

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
    return { kind: 'reach-share', said: `its paths reach ${reach.reached} of ${reach.of} ${reach.what} (${percent(reach.reached / reach.of)}%), over the declared ${percent(rule.reachShare)}%` };
  }
  return null;
}

/** The line a plan and a merge say of the verification's scope: full with its reason, or scoped to the reach it measured. */
export function scopeLine(rule: Verification, changed: readonly string[], reach: Reach | undefined, reason: FullReason | null): string {
  if (reason) return `verification: full: ${reason.said}; ${budgetSaid(rule)}`;
  const paths = `${count(changed.length, 'path')} ${changed.length === 1 ? 'reaches' : 'reach'}`;
  const reached = reach ? `${paths} ${reach.reached} of ${reach.of} ${reach.what}` : count(changed.length, 'path');
  return `verification: scoped to its reach: ${reached}; the light gates on its paths and the reached classes now, the heavy tier after the landing; ${budgetSaid(rule)}`;
}

const listed = (paths: readonly string[], most = 6): string => (paths.length <= most
  ? paths.join(', ')
  : `${paths.slice(0, most).join(', ')} and ${paths.length - most} more in ${GATES_FILE}'s verification`);

/** Why `--full` is refused for `changed`, naming the reasons it accepts; null where the change bears one. */
export function fullRefusal(rule: Verification, changed: readonly string[], reach: Reach): string | null {
  if (fullReason(rule, changed, reach)) return null;
  const bears = changed.length === 1 ? 'bears' : 'bear';
  return [
    `--full needs a reason the change bears (D349), and its ${count(changed.length, 'path')} ${bears} none: it reaches ${reach.reached} of ${reach.of} ${reach.what}.`,
    `The reasons it accepts: a declared wide path (${listed(rule.widePaths)}), a reach of ${percent(rule.reachShare)}% or more of the ${reach.what},`,
    `${RUN_REASONS['after-landing'].said} or ${RUN_REASONS.release.said}.`,
    'Merge without --full: its verification runs scoped to its reach, the heavy tier after the landing.',
  ].join(' ');
}

/** What the merge record keeps of the rule for one run. */
export function verificationRecord(rule: Verification, { wallMs, reason }: { wallMs: number; reason: FullReason | null }): VerificationRecord {
  return {
    budgetMs: rule.budgetMs, wallMs, over: wallMs > rule.budgetMs, scope: reason ? 'full' : 'reach',
    ...(reason ? { reason: reason.said, kind: reason.kind } : {}),
  };
}

const byTime = <T extends { ms: number }>(list: readonly T[]): T[] => list.filter((each) => each.ms > 0).sort((a, b) => b.ms - a.ms);

/**
 * The lines a summary says of a verification past its budget, none within it: its time and scope, where its time went (each gate
 * by its time, the reached run's steps beside it), and the file it is filed in.
 */
export function overrunLines(rule: Verification, { wallMs, gates, reason, file }: { wallMs: number; gates: readonly Timed[]; reason: FullReason | null; file: string }): string[] {
  if (wallMs <= rule.budgetMs) return [];
  const scope = reason ? `full: ${reason.said}` : 'scoped to its reach, so the scope was too big for the change or the change is code-wide and should have said so';
  const went = byTime(gates).map((gate) => {
    const steps = byTime(gate.steps ?? []);
    return `${gate.name} ${minutes(gate.ms)} min${steps.length ? ` (${stepsLine(steps)})` : ''}`;
  });
  return [
    `  verification: ${minutes(wallMs)} min, over this workspace's ${minutes(rule.budgetMs)} min budget (D349); its scope: ${scope}`,
    `    where its time went: ${went.join(', ') || 'no gate recorded its time'}`,
    `    filed in ${file} for the steward`,
  ];
}

/** Files one overrun: a line appended to the findings file the steward reads, the folder made where none stands. */
export function fileOverrun(file: string, overrun: Overrun): void {
  mkdirSync(dirname(file), { recursive: true });
  appendFileSync(file, `${JSON.stringify(overrun)}\n`);
}

/** A run's verification as planned: the rule, why it is full (or null) and the line it says; null where the repository sets no rule. */
export type Planned = { rule: Verification; reason: FullReason | null; line: string };

export function planVerification(rule: Verification | null, changed: readonly string[], reach: Reach | undefined, kind: FullReason | null = null): Planned | null {
  if (!rule) return null;
  const reason = kind ?? fullReason(rule, changed, reach);
  return { rule, reason, line: scopeLine(rule, changed, reach, reason) };
}

/**
 * A run's verification settled as it ends: what its record keeps, the lines its summary says and, past the budget, the overrun
 * filed in `file` (`shown` is how the summary names it).
 */
export function settleVerification(planned: Planned, { branch, at, wallMs, gates, file, shown }: {
  branch: string; at: string; wallMs: number; gates: readonly Timed[]; file: string; shown: string;
}): { record: VerificationRecord; lines: string[] } {
  const record = verificationRecord(planned.rule, { wallMs, reason: planned.reason });
  const lines = overrunLines(planned.rule, { wallMs, gates, reason: planned.reason, file: shown });
  if (record.over) fileOverrun(file, { at, branch, ...record, gates: gates.map(({ name, ms, steps }) => ({ name, ms, ...(steps?.length ? { steps } : {}) })) });
  return { record, lines };
}

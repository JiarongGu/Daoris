/**
 * The full set's replay of passes main already proved, by test class. A class whose key (the digest of every file that reaches
 * it, its suite's command with its run settings, the toolchain) has a standing pass is left out of the run; the rest run, and each
 * class the run's report shows passing is recorded, so the next full set replays it. A red or a flake is never recorded.
 */
import { classKey, splitByReuse, unitId, type KeyContext, type PassedClasses, type Unit } from './class-reuse.ts';
import { passedClasses } from './trx-passes.ts';

/** What a gate's classes came to: how many were replayed, how many run, and the standing passes the replays came from. */
export type ClassReplay = { replayedCount: number; ran: number; runs: string[]; oldest: string | null };

export type ClassPlan<U extends Unit> = { run: U[]; replayed: U[]; keys: Map<string, string> } & ClassReplay;

/** The units split by whether a pass of their key stands, with the runs the standing passes were given in. */
export function planClasses<U extends Unit>(units: U[], context: KeyContext, store: PassedClasses): ClassPlan<U> {
  const { run, replayed, keys } = splitByReuse(units, (unit) => classKey(unit, context), store);
  const standing = replayed.flatMap((unit) => store.standing(unitId(unit), keys.get(unitId(unit)) as string) ?? []);
  const runs = [...new Set(standing.map((each) => each.run ?? each.at))].sort();
  const oldest = standing.map((each) => each.at).sort()[0] ?? null;
  return { run, replayed, keys, runs, oldest, ran: run.length, replayedCount: replayed.length };
}

/** The classes of a trx that passed, every test of each. */
export const classPass = (trx: string): Set<string> => passedClasses(trx);

/**
 * Records a pass for each unit the trx shows passing, under the key it was planned with. A flake records none (it is run again,
 * never replayed), a failure only its classes that passed. Returns how many were recorded.
 */
export function recordPasses({ units, keys, trx, verdict, store, at, run }: {
  units: readonly Unit[]; keys: ReadonlyMap<string, string>; trx: string; verdict: string; store: PassedClasses; at: string; run: string;
}): number {
  if (verdict !== 'PASS' && verdict !== 'FAIL') return 0;
  const passed = classPass(trx);
  let recorded = 0;
  for (const unit of units) {
    const key = keys.get(unitId(unit));
    if (key === undefined || !passed.has(unit.unit)) continue;
    store.add(unitId(unit), key, at, run);
    recorded += 1;
  }
  return recorded;
}

/** The line that says how many of a gate's classes were replayed and from which runs. */
export function replayLine(gate: string, plan: Pick<ClassPlan<Unit>, 'replayed' | 'run' | 'runs' | 'oldest'>): string {
  const total = plan.replayed.length + plan.run.length;
  const from = plan.runs.length ? ` from ${plan.runs.length === 1 ? 'run' : 'runs'} ${plan.runs.slice(0, 3).join(', ')}${plan.runs.length > 3 ? ` and ${plan.runs.length - 3} more` : ''} (oldest ${plan.oldest})` : '';
  return `${gate}: ${plan.replayed.length} of ${total} classes replayed${from}; ${plan.run.length} run`;
}

/**
 * The wall-clock budget of a gate: its declared `timeoutMinutes`, else five minutes in a merge and ten in the full set or the CI.
 * A gate past it is killed with what it started and fails as over-budget. `--no-timeout` turns it off for an investigation and
 * the summary says so.
 */
import type { Slow } from './timing.ts';

export const MERGE_GATE_MINUTES = 5;
export const FULL_GATE_MINUTES = 10;

export const timeoutState = {
  off: false,
  note: (): string | null => (timeoutState.off ? '  --no-timeout: no gate had a budget in this run, so a slow gate was not ended; do not land on this run as a normal one' : null),
};

/** The gate's budget in milliseconds for a run of `kind` (`merge`, or null, is a merge's), or null where there is none. */
export function budgetMs(gate: { name: string; timeoutMinutes?: unknown }, kind: string | null): number | null {
  if (timeoutState.off) return null;
  const declared = gate.timeoutMinutes;
  if (typeof declared === 'number' && Number.isFinite(declared) && declared > 0) return declared * 60_000;
  return (kind === null || kind === 'merge' ? MERGE_GATE_MINUTES : FULL_GATE_MINUTES) * 60_000;
}

const minutes = (ms: number): string => `${Number((ms / 60_000).toFixed(1))}`;

export function overBudgetNote(ranMs: number, budget: number, slowest: Slow[]): string {
  const slow = slowest.slice(0, 3).map((each) => `${each.name} ${minutes(each.ms)} min`).join(', ');
  return `over-budget: killed after ${minutes(ranMs)} min, its budget is ${minutes(budget)} min${slow ? `; slowest in its last record: ${slow}` : ''}`;
}

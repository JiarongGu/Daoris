/**
 * The wall-clock budget of a gate: its declared `timeoutMinutes`, else five minutes in a merge and ten in the full set or the CI.
 * A gate past it is killed with what it started, a breach that says why (`tools/time-why.mjs`). A kill while the load sampler (from
 * half the budget) saw more heavy runs than the gate's own fails nothing yet: the gate runs again once, whole and alone, after every
 * other gate and once the machine is free (`waitFree`), and only a kill alone fails as its time. The budget never scales with the
 * load: a kill alone is the one measure of the gate's own time. `--no-timeout` turns it off for an investigation, said in the summary.
 */
import type { LoadSeam } from '../time-why.mjs';
import { stepsLine, type Step } from './reached-steps.ts';
import type { Slow } from './timing.ts';

export const MERGE_GATE_MINUTES = 5;
export const FULL_GATE_MINUTES = 10;

/** How long a gate killed under load waits for the machine to be free before it runs again anyway, and how often it reads it. */
export const FREE_WAIT_MS = 10 * 60_000;
export const FREE_EVERY_MS = 15_000;

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

export function overBudgetNote(ranMs: number, budget: number, slowest: Slow[], steps: readonly Step[] = []): string {
  const slow = slowest.slice(0, 3).map((each) => `${each.name} ${minutes(each.ms)} min`).join(', ');
  const said = steps.length ? `; its steps: ${stepsLine(steps)}` : '';
  return `over-budget: killed after ${minutes(ranMs)} min, its budget is ${minutes(budget)} min${said}${slow ? `; slowest in its last record: ${slow}` : ''}`;
}

/** Until the machine is free, read every `everyMs` for up to `deadlineMs` or until `halted()`: whether it is free, and the wait's length. */
export async function waitFree(load: LoadSeam, halted: () => boolean = () => false, deadlineMs = FREE_WAIT_MS, everyMs = FREE_EVERY_MS): Promise<{ free: boolean; ms: number }> {
  const from = load.now();
  for (;;) {
    if (load.free()) return { free: true, ms: load.now() - from };
    if (halted() || load.now() - from >= deadlineMs) return { free: false, ms: load.now() - from };
    await load.sleep(everyMs);
  }
}

/** What a kill adds to the over-budget note: under load, that it runs again alone; on the run again, what that run showed. */
export function killNote({ loaded, peak, again, waited }: { loaded: boolean; peak: number | null; again: boolean; waited: { free: boolean } | null }): string {
  if (again) return `; under load (${peak} heavy runs at its peak): run again alone once every other gate has ended and the machine is free`;
  if (!waited) return '';
  if (!loaded) return '; killed at its budget under load, then killed again run alone: the time is the gate\'s';
  const why = waited.free ? 'the load came back while it ran' : `the machine was not free within ${minutes(FREE_WAIT_MS)} min`;
  return `; killed at its budget under load, then again under load: ${why}; run it again when the machine is free`;
}

/** The note of a gate killed under load whose run again alone ended without a kill. */
export const againNote = (verdict: string, waited: { free: boolean; ms: number }, note: string): string => [
  `killed at its budget under load, then ${verdict === 'PASS' ? 'passed' : `ended ${verdict}`} run again alone`
  + `${waited.ms ? ` after ${minutes(waited.ms)} min waiting for the machine${waited.free ? '' : ', which was not free'}` : ''}`,
  note,
].filter(Boolean).join('; ');

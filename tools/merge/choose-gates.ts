/** The gates the merge in place runs, among those of the plan. */
import { mergeChanges } from './changes.ts';
import { readLanes } from './lanes.ts';
import type { Planned } from './plan.ts';
import { selectGates } from './reach.ts';
import type { Selection } from './reach.ts';

/** The batch's record as the choice reads it: its branches, the one in turn, what each merge reached, and the run's mode. */
export type Batch = { branches: string[]; at: number; full?: boolean; gated?: boolean; reached?: Record<string, string[]> };

/**
 * The gates the merge in place runs, and every gate of the plan with why it runs or is skipped. Checks and
 * suites run at each merge of a batch by its own paths; the rehearsals run once, at the batch's last merge,
 * each one any of the batch's merges reached. `state.reached` keeps what each merge reached, for that.
 */
export function chooseGates(root: string, state: Batch, plan: readonly Planned[]): { gates: Selection[]; chosen: Planned[]; skipped: number } {
  const branch = state.branches[state.at] as string;
  const last = state.at === state.branches.length - 1;
  const selection = selectGates(plan, mergeChanges(root), { lanes: readLanes(root)?.lanes ?? [], full: state.full === true, gated: state.gated === true });
  const reached = { ...(state.reached ?? {}), [branch]: selection.gates.filter((entry) => entry.run).map((entry) => entry.gate.name) };
  state.reached = reached;
  const waiting = new Set<string>();
  const display = selection.gates.map((entry): Selection => {
    if (entry.gate.kind !== 'rehearsal') return entry;
    if (!last) {
      if (!entry.run) return entry;
      waiting.add(entry.gate.name);
      return { ...entry, run: false, why: `waits for the batch's last branch (${state.branches.at(-1)}); ${entry.why}` };
    }
    if (entry.run) return entry;
    const by = state.branches.filter((other) => other !== branch && reached[other]?.includes(entry.gate.name));
    return by.length ? { ...entry, run: true, why: `reached by ${by.join(', ')}, earlier in this batch` } : entry;
  });
  return {
    gates: display,
    chosen: display.filter((entry) => entry.run).map((entry) => entry.gate),
    // What the lane table left out of this merge, not counting a rehearsal that waits for the batch's end.
    skipped: display.filter((entry) => !entry.run && !waiting.has(entry.gate.name)).length,
  };
}

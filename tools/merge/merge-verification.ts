/**
 * The merge tool's reading of this workspace's verification rule (`verification.ts`): the scope a merge, a plan and a preview
 * say before anything runs, what a merge keeps for its record and its overrun, and the refusal of a `--full` no change bears.
 * Every function here is silent where the repository's gates file sets no rule.
 */
import { join } from 'node:path';
import { Refusal } from './args.ts';
import { branchChanges, mergeChanges } from './changes.ts';
import { verdictsHome } from './home.ts';
import { readLanes } from './lanes.ts';
import { readPlan, type Planned as PlanGate } from './plan.ts';
import { selectGates, selectionLines, type Selection } from './reach.ts';
import { RUN_REASONS, fullRefusal, gateReach, planVerification, readVerification, type Planned } from './verification.ts';

const lanesOf = (root: string) => readLanes(root)?.lanes ?? [];

/** The full set, a release's run: every gate on the checkout as it stands. */
export const FULL_COMMAND = 'node tools/merge-branch.mjs --full';

/** The line that ends a merge's run with gates skipped: the full set is still owed before a stage. */
export const owedLine = (skipped: number): string | null => (skipped
  ? `  ${skipped} gate${skipped === 1 ? '' : 's'} of the plan skipped; the full set runs before the install is staged (${FULL_COMMAND}).`
  : null);

/** The merge in place: its scope said, and the rule kept with the findings file, in the main checkout, its overrun is filed in. */
export function mergeVerifying(root: string, selection: readonly Selection[], say: (line: string) => void = console.log): (Planned & { file: string; shown: string }) | null {
  const planned = planVerification(readVerification(root), mergeChanges(root), gateReach(selection));
  if (!planned) return null;
  say(planned.line);
  return { ...planned, file: join(verdictsHome(root), planned.rule.overruns), shown: planned.rule.overruns };
}

/** What a branch's paths reach for `--plan`: the gates, and the lines saying why, its scope first and a `--full` it would refuse. */
export function planReaches(root: string, branch: string, plan: PlanGate[], { full, gated }: { full: boolean; gated: boolean }): { runs: string[]; lines: string[] } {
  const changed = branchChanges(root, branch);
  const selection = selectGates(plan, changed, { lanes: lanesOf(root), full, gated }).gates;
  const rule = readVerification(root);
  const reach = gateReach(selection);
  const refused = rule && full ? fullRefusal(rule, changed, reach) : null;
  const said = planVerification(rule, changed, reach);
  return {
    runs: selection.filter((entry) => entry.run).map((entry) => entry.gate.name),
    lines: [...(said ? [said.line] : []), ...(refused ? [`refused: ${refused}`] : []), ...selectionLines(selection)],
  };
}

/** The scope a branch's preview says, from the selection it read. */
export function previewScope(home: string, branch: string, selection: readonly Selection[], say: (line: string) => void = console.log): void {
  const said = planVerification(readVerification(home), branchChanges(home, branch), gateReach(selection));
  if (said) say(said.line);
}

/** The full set on the checkout, a release's run by its kind: said, then `run`. */
export function checkoutScope<T>(root: string, run: () => T, say: (line: string) => void = console.log): T {
  const said = planVerification(readVerification(root), [], undefined, RUN_REASONS.release);
  if (said) say(said.line);
  return run();
}

/** `--full` on a merge of `branches`, refused where their changes bear no reason the rule accepts. */
export function requireFullReason(root: string, branches: readonly string[]): void {
  const rule = readVerification(root);
  if (!rule) return;
  const plan = readPlan(root);
  const changed = [...new Set(branches.flatMap((branch) => branchChanges(root, branch)))];
  const refused = fullRefusal(rule, changed, gateReach(selectGates(plan, changed, { lanes: lanesOf(root) }).gates));
  if (refused) throw new Refusal(refused);
}

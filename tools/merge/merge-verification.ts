/**
 * The merge tool's reading of this workspace's verification rule (`verification.ts`): the scope a merge, a plan and a preview
 * say before anything runs, what a merge keeps for its record and its overrun, and the refusal of a `--full` no change bears.
 * Every function here is silent where the repository's gates file sets no rule.
 */
import { join } from 'node:path';
import { Refusal } from './args.ts';
import { branchChanges, mergeChanges } from './changes.ts';
import { mergeInProgress, requireBranch } from './git.ts';
import { verdictsHome } from './home.ts';
import { readLanes } from './lanes.ts';
import { readPlan, type Planned as PlanGate } from './plan.ts';
import { gateReach, selectGates, selectionLines, type Selection } from './reach.ts';
import { RUN_REASONS, fullRefusal, planVerification, readVerification, type Planned } from './verification.ts';

const lanesOf = (root: string) => readLanes(root)?.lanes ?? [];

/** The full set, a release's run: every gate on the checkout as it stands. */
export const FULL_COMMAND = 'node tools/merge-branch.mjs --full';

/** The line that ends a merge's run with gates skipped: the full set is still owed before a stage. */
export const owedLine = (skipped: number): string | null => (skipped
  ? `  ${skipped} gate${skipped === 1 ? '' : 's'} of the plan skipped; the full set runs before the install is staged (${FULL_COMMAND}).`
  : null);

/**
 * The merge in place: its scope said by what it runs (`full`, the batch's `--full`, from its state), and the rule kept with the
 * findings file, in the main checkout, its overrun is filed in.
 */
export function mergeVerifying(root: string, selection: readonly Selection[], { full }: { full?: boolean }, say: (line: string) => void = console.log): (Planned & { file: string; shown: string }) | null {
  const planned = planVerification(readVerification(root), mergeChanges(root), gateReach(selection), { ran: full === true ? 'full' : 'reach' });
  if (!planned) return null;
  say(planned.line);
  return { ...planned, file: join(verdictsHome(root), planned.rule.overruns), shown: planned.rule.overruns };
}

/** What a branch's paths reach for `--plan`: the gates, and the lines saying why, its scope first (`full`: the batch's `--full`, taken). */
export function planReaches(root: string, branch: string, plan: PlanGate[], { full, gated }: { full: boolean; gated: boolean }): { runs: string[]; lines: string[] } {
  const changed = branchChanges(root, branch);
  const selection = selectGates(plan, changed, { lanes: lanesOf(root), full, gated }).gates;
  const said = planVerification(readVerification(root), changed, gateReach(selection), { ran: full ? 'full' : 'reach' });
  return {
    runs: selection.filter((entry) => entry.run).map((entry) => entry.gate.name),
    lines: [...(said ? [said.line] : []), ...selectionLines(selection)],
  };
}

/** The scope a branch's preview says, from the selection it read. */
export function previewScope(home: string, branch: string, selection: readonly Selection[], say: (line: string) => void = console.log): void {
  const said = planVerification(readVerification(home), branchChanges(home, branch), gateReach(selection));
  if (said) say(said.line);
}

/**
 * The full set on the checkout, a release's run by its kind: said, then `run`. With a merge open, nothing is said: `run`
 * refuses it, an open merge being `--continue`'s to gate.
 */
export function checkoutScope<T>(root: string, run: () => T, say: (line: string) => void = console.log): T {
  const said = mergeInProgress(root) ? null : planVerification(readVerification(root), [], undefined, { kind: RUN_REASONS.release });
  if (said) say(said.line);
  return run();
}

/**
 * Why `--full` on a merge of `branches` is refused, or null where their changes bear a reason the rule accepts (or it sets no
 * rule). The batch is judged as one, by the merge, `--continue` and `--plan` alike: each branch's changes since it left main,
 * each branch named first; with `open`, the first is the merge in place, judged on what it changes, a fix made during it
 * included, which is what its run reads.
 */
export function fullRefused(root: string, branches: readonly string[], { open = false }: { open?: boolean } = {}): string | null {
  const rule = readVerification(root);
  if (!rule) return null;
  const apart = open ? branches.slice(1) : branches;
  for (const branch of apart) requireBranch(root, branch);
  const changed = [...new Set([...(open ? mergeChanges(root) : []), ...apart.flatMap((branch) => branchChanges(root, branch))])];
  return fullRefusal(rule, changed, gateReach(selectGates(readPlan(root), changed, { lanes: lanesOf(root) }).gates));
}

/** `--full` on a merge of `branches` (the first the merge in place, with `open`), refused where they bear no reason the rule accepts. */
export function requireFullReason(root: string, branches: readonly string[], options: { open?: boolean } = {}): void {
  const refused = fullRefused(root, branches, options);
  if (refused) throw new Refusal(refused);
}

/** `--continue --full` on a batch at `at`: judged on the merge in place, where one is open, and the branches still to merge. */
export function requireContinueReason(root: string, branches: readonly string[], at: number): void {
  const open = mergeInProgress(root);
  requireFullReason(root, branches.slice(open ? at : at + 1), { open });
}

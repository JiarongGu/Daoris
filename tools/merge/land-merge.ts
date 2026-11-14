/**
 * `--commit`: the merge left open in place, committed by the tool once every gate chosen for it has a standing pass, so a landing
 * never depends on a hand-made commit. Whatever way the gates ran (a normal run, a `--rerun`, with budgets or without), the landing
 * ends the same: the commit, the heavy tier started on the new tip, and the prune of what is merged.
 */
import { startHeavy, type StartSeams } from './heavy-after.ts';

/** A gate's recorded result, as the batch record keeps it. */
export type Result = { verdict: string };

export type Readiness = { ready: boolean; lacking: string[]; failed: string[]; stale: string[] };

/** Whether every chosen gate has a passing verdict that no path changed since reaches. */
export function readiness(chosen: readonly string[], results: Readonly<Record<string, Result | undefined>>, stale: Readonly<Record<string, readonly string[]>>): Readiness {
  const lacking = chosen.filter((name) => !results[name]);
  const failed = chosen.filter((name) => results[name] && results[name]!.verdict !== 'PASS' && results[name]!.verdict !== 'FLAKE');
  const staled = chosen.filter((name) => results[name] && name in stale);
  return { ready: lacking.length + failed.length + staled.length === 0, lacking, failed, stale: staled };
}

/** The reasons a merge is not committed, one line each, naming what to run. */
export function notReadyLines(branch: string, state: Readiness): string[] {
  const parts = [
    state.failed.length ? `failed: ${state.failed.join(', ')}` : '',
    state.lacking.length ? `no verdict yet: ${state.lacking.join(', ')}` : '',
    state.stale.length ? `verdict stale (a path changed since reaches it): ${state.stale.join(', ')}` : '',
  ].filter(Boolean);
  return [`merge-branch: the merge of ${branch} is not committed: ${parts.join('; ')}.`, '  node tools/merge-branch.mjs --rerun <gate>… runs what is owed; --commit then lands it.'];
}

/** The commit's message: what merged, how many gates passed, and that no gate had a budget where none had. */
export function landMessage(input: { branch: string; gates: number; unbudgeted: boolean; trailer: string }): string {
  const budgets = input.unbudgeted ? ' with no time budget on a gate (--no-timeout)' : '';
  return `Merge ${input.branch}\n\n${input.gates} gate${input.gates === 1 ? '' : 's'} passed on the merge${budgets}; committed by merge-branch --commit.\n\n${input.trailer}`;
}

export type LandSeams = StartSeams & {
  /** Commits the open merge with `message`, answering the new commit and the main tip it merged into. */
  commit: (message: string) => { tip: string; base: string };
  /** The prune of what is merged, with the batch's remaining branches held. */
  prune: () => void;
  log: string;
  say: (line: string) => void;
};

/** The landing, after the readiness check passed: commit, the heavy run on the new tip, the prune. */
export function landMerge(
  input: { branch: string; gates: number; unbudgeted: boolean; trailer: string; prune: boolean; heavy: boolean },
  seams: LandSeams,
): { tip: string; heavy: string | null } {
  const message = landMessage(input);
  const { tip, base } = seams.commit(message);
  seams.say(`merge-branch: committed ${input.branch} as ${tip.slice(0, 7)}${input.unbudgeted ? ' (no gate had a time budget in this run)' : ''}`);
  // A scratch repository holds no runner (`hasRunner`), and starts no run.
  const started = input.heavy ? startHeavy({ commit: tip, base, branches: [input.branch], log: seams.log }, seams) : null;
  for (const line of started?.lines ?? []) seams.say(line);
  if (input.prune) seams.prune();
  else seams.say('prune: skipped (--no-prune)');
  return { tip, heavy: started?.run.id ?? null };
}
